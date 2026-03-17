// Ezpzoptimizer - Main Editor Window
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public class TextureOptimizerWindow : EditorWindow
    {
        // Common
        GameObject _avatar;
        int _tab;

        // Atlas tab
        [System.Serializable]
        public class AtlasMaterialEntry
        {
            public Material mat;
            public bool selected;
        }
        [SerializeField] List<AtlasMaterialEntry> _atlasMaterials = new List<AtlasMaterialEntry>();
        Vector2 _atlasScroll;
        // Texture tab
        List<Texture2D> _textures = new List<Texture2D>();
        bool[] _selected = new bool[0];
        Vector2 _texScroll;
        int _sizeIdx = 2; // default 512
        TextureProcessor.CompressionPreset _preset = TextureProcessor.CompressionPreset.Auto;
        bool _questOpt;

        // Material tab
        [System.Serializable]
        public class MaterialEntry
        {
            public Material mat;
            public bool selected;
            public bool hasUnusedProps;
            public bool isDuplicate;
        }
        [SerializeField] List<MaterialEntry> _matEntries = new List<MaterialEntry>();
        Vector2 _matScroll;
        int _questShaderIndex = 0;

        // Mesh tab
        List<SkinnedMeshRenderer> _smrs = new List<SkinnedMeshRenderer>();
        List<bool> _smrSelected = new List<bool>();
        // Bone Deletion
        [System.Serializable]
        public class BoneEntry
        {
            public Transform t;
            public string typeLabel; // "PhysBone", "Collider", etc.
            public string displayLabel;
            public bool selected;
        }

        [System.Serializable]
        public class FoldoutState
        {
            public string name;
            public bool isOpen;
            public List<BoneEntry> entries = new List<BoneEntry>();
        }

        [SerializeField] List<FoldoutState> _boneCategories = new List<FoldoutState>();
        [SerializeField] List<string> _combinationGroupNames = new List<string>();
        [SerializeField] string _combinationGroupName = "CombinedMesh";
        [SerializeField] bool _safetyDuplicate = false;
        GameObject _lastAvatar;
        Vector2 _boneScroll;
        Vector2 _meshScroll;

        // Simplify tab - direct edit, per-object quality persistence
        GameObject _simplifyTarget;
        Mesh[] _simplifyOriginalMeshes;
        SkinnedMeshRenderer[] _simplifySmrs;
        float _simplifyQuality = 1f;
        float _simplifyLastApplied = 1f;
        List<MeshSimplifier.SolverInstance> _simplifySolvers = new List<MeshSimplifier.SolverInstance>();
        int _simplifyOriginalTris;
        Vector2 _simplifyScroll;
        static Dictionary<int, float> _simplifyQualityMap = new Dictionary<int, float>(); // instanceID -> quality
        static Dictionary<int, Mesh[]> _simplifyOriginalMeshesMap = new Dictionary<int, Mesh[]>();
        static Dictionary<int, SkinnedMeshRenderer[]> _simplifySmrsMap = new Dictionary<int, SkinnedMeshRenderer[]>();
        static Dictionary<int, int> _simplifyOriginalTrisMap = new Dictionary<int, int>();
        static Dictionary<int, List<MeshSimplifier.SolverInstance>> _simplifySolversMap = new Dictionary<int, List<MeshSimplifier.SolverInstance>>();

        // Optimization results
        [System.Serializable]
        public class AvatarStatsSnapshot
        {
            public long texMB;
            public int polys;
            public int bones;
            public int pbComponents;
            public int pbTransforms;
            public long vramMB;
            public bool valid = false;

            public void Capture(GameObject avatar)
            {
                if (avatar == null) { valid = false; return; }
                var diag = PerformanceDiagnostic.Scan(avatar);
                polys = diag.stats.polygons;
                bones = diag.stats.bones;
                pbComponents = diag.stats.physBoneComponents;
                pbTransforms = diag.stats.physBoneTransforms;
                
                long totalVram = 0;
                var textures = TextureScanner.Scan(avatar);
                foreach (var tex in textures)
                {
                    if (tex == null) continue;
                    totalVram += VRAMCalc.EstimateFromTexture(tex);
                }
                vramMB = totalVram / 1024 / 1024;
                texMB = vramMB / 4; // Simple heuristic for "Texture Size" display
                if (texMB == 0 && vramMB > 0) texMB = 1;
                valid = true;
            }
        }

        [SerializeField] AvatarStatsSnapshot _baseStats = new AvatarStatsSnapshot();
        [SerializeField] AvatarStatsSnapshot _currentStats = new AvatarStatsSnapshot();
        bool _hasOptimizationResult = false;

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            RefreshAll();
            Repaint();
        }

        void RefreshAll()
        {
            if (_avatar == null)
            {
                _smrs.Clear();
                _smrSelected.Clear();
                _boneCategories.Clear();
                _atlasMaterials.Clear();
                _matEntries.Clear();
                _textures.Clear();
                _perfResult = null;
                return;
            }
            RefreshSMRs();
            RefreshBones();
            RefreshMaterials();
            RefreshAtlasMaterials();
            DoTexScan();
            _perfResult = PerformanceDiagnostic.Scan(_avatar);
        }

        [MenuItem("Tools/Ezpzoptimizer")]
        static void Open() => GetWindow<TextureOptimizerWindow>(L.WindowTitle);

        void OnGUI()
        {
            // --- Header ---
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(L.LangBtn, EditorStyles.toolbarButton, GUILayout.Width(32)))
            {
                L.Toggle();
                titleContent = new GUIContent(L.WindowTitle);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // --- Avatar Selection ---
            EditorGUILayout.BeginHorizontal();
            _avatar = (GameObject)EditorGUILayout.ObjectField(L.AvatarLabel, _avatar, typeof(GameObject), true);
#if VRC_SDK_EXISTS
            if (GUILayout.Button(L.AutoDetect, GUILayout.Width(160)))
            {
                var found = TextureScanner.FindVRCAvatar();
                if (found != null) _avatar = found;
            }
#endif
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // --- Unified Refresh Check ---
            if (_avatar != _lastAvatar)
            {
                RefreshAll();
                if (_avatar != null) _baseStats.Capture(_avatar);
                _lastAvatar = _avatar;
                _hasOptimizationResult = false; // Reset summary on avatar switch
            }

            // --- Tabs ---
            _tab = GUILayout.Toolbar(_tab, new[] { L.TabTexture, L.TabMaterial, L.TabPerf, L.TabMesh, L.TabBone, L.TabAtlas, L.TabSimplify, L.TabExport });
            EditorGUILayout.Space(4);

            if (_tab == 0) DrawTextureTab();
            else if (_tab == 1) DrawMaterialTab();
            else if (_tab == 2) DrawPerfTab();
            else if (_tab == 3) DrawMeshTab();
            else if (_tab == 4) DrawBoneTab();
            else if (_tab == 5) DrawAtlasTab();
            else if (_tab == 6) DrawSimplifyTab();
            else DrawExportTab();
        }

        // ------------------------------------------------
        //  TEXTURE TAB
        // ------------------------------------------------
        void DrawTextureTab()
        {
            if (GUILayout.Button(L.ScanBtn, GUILayout.Height(28)))
                DoTexScan();

            if (_textures.Count == 0)
            {
                EditorGUILayout.HelpBox(L.NoTex, MessageType.Info);
                DrawRestoreButton();
                return;
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(L.TexCount(_textures.Count), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L.SelectAll, GUILayout.Width(80))) SetAll(true);
            if (GUILayout.Button(L.DeselectAll, GUILayout.Width(80))) SetAll(false);
            EditorGUILayout.EndHorizontal();

            // Texture list
            EditorGUILayout.Space(2);
            _texScroll = EditorGUILayout.BeginScrollView(_texScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _textures.Count; i++)
            {
                var tex = _textures[i];
                if (tex == null) continue;
                int targetSize = TextureProcessor.SizeOptions[_sizeIdx];
                if (Mathf.Max(tex.width, tex.height) <= targetSize) { _selected[i] = false; continue; }

                EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
                EditorGUI.BeginChangeCheck();
                bool val = EditorGUILayout.Toggle(_selected[i], GUILayout.Width(16));
                if (EditorGUI.EndChangeCheck())
                {
                    _selected[i] = val;
                    GUI.FocusControl(null);
                }
                var preview = AssetPreview.GetMiniThumbnail(tex);
                if (preview != null && preview.width > 0)
                    GUILayout.Label(preview, GUILayout.Width(20), GUILayout.Height(20));
                string vram = VRAMCalc.FormatSize(VRAMCalc.EstimateFromTexture(tex));
                EditorGUILayout.LabelField($"{tex.name}  ({tex.width}x{tex.height})  {vram}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            DrawVRAMSummary();
            EditorGUILayout.Space(4);

            // Settings
            EditorGUILayout.LabelField("=== Settings ===", EditorStyles.centeredGreyMiniLabel);
            _sizeIdx = EditorGUILayout.Popup(L.TargetSize, _sizeIdx, SizeLabels());
            _preset = (TextureProcessor.CompressionPreset)EditorGUILayout.EnumPopup(L.CompFormat, _preset);
            _questOpt = EditorGUILayout.Toggle(L.QuestOpt, _questOpt);

            EditorGUILayout.Space(8);

            // Buttons
            var selectedCount = CountSelected();
            GUI.enabled = selectedCount > 0;
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button($"{L.Optimize} ({selectedCount})", GUILayout.Height(36)))
                DoOptimize();
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            DrawRestoreButton();
        }

        // ------------------------------------------------
        //  MATERIAL TAB
        // ------------------------------------------------
        void DrawMaterialTab()
        {
            if (_matEntries.Count == 0 && _avatar != null) RefreshMaterials();

            if (_matEntries.Count == 0)
            {
                EditorGUILayout.HelpBox(L.MatNone, MessageType.Info);
                if (GUILayout.Button(L.MatScanBtn, GUILayout.Height(28))) RefreshMaterials();
                return;
            }

            EditorGUILayout.LabelField(L.MatCount(_matEntries.Count), EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L.SelectAll, GUILayout.Width(80))) foreach (var e in _matEntries) e.selected = true;
            if (GUILayout.Button(L.DeselectAll, GUILayout.Width(80))) foreach (var e in _matEntries) e.selected = false;
            if (GUILayout.Button("Refresh", GUILayout.Width(80))) RefreshMaterials();
            EditorGUILayout.EndHorizontal();

            // Material list
            _matScroll = EditorGUILayout.BeginScrollView(_matScroll, "box", GUILayout.ExpandHeight(true));
            int totalUnused = 0;
            int totalDups = 0;
            int selectedCount = 0;

            foreach (var entry in _matEntries)
            {
                if (entry.mat == null) continue;
                if (entry.selected) selectedCount++;
                if (entry.hasUnusedProps) totalUnused++;
                if (entry.isDuplicate) totalDups++;

                EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
                EditorGUI.BeginChangeCheck();
                bool val = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(16));
                if (EditorGUI.EndChangeCheck())
                {
                    entry.selected = val;
                    GUI.FocusControl(null);
                }

                var icon = AssetPreview.GetMiniThumbnail(entry.mat);
                if (icon != null && icon.width > 0)
                    GUILayout.Label(icon, GUILayout.Width(18), GUILayout.Height(18));

                string shaderName = entry.mat.shader != null ? entry.mat.shader.name : "?";
                int lastSlash = shaderName.LastIndexOf('/');
                string shortShader = lastSlash >= 0 ? shaderName.Substring(lastSlash + 1) : shaderName;
                
                string label = $"{entry.mat.name}  [{shortShader}]";
                if (entry.hasUnusedProps) label += " (!)";
                if (entry.isDuplicate) label += " (D)";

                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);

            // Actions
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("=== " + (L.IsJP ? "一括最適化 (選択中)" : "Batch Optimize (Selected)") + " ===", EditorStyles.centeredGreyMiniLabel);
            
            GUI.enabled = selectedCount > 0;
            
            EditorGUILayout.BeginHorizontal();
            
            // 1) Unused properties
            EditorGUILayout.BeginVertical("box", GUILayout.Width(position.width * 0.48f));
            EditorGUILayout.LabelField(L.IsJP ? "不要データのクリーンアップ" : "Clean Unused Data", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(totalUnused > 0 ? L.MatUnusedProps(totalUnused) : L.MatAllClean, EditorStyles.miniLabel);
            
            GUI.backgroundColor = totalUnused > 0 ? new Color(0.5f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button(L.MatCleanup, GUILayout.Height(30)))
            {
                var selected = _matEntries.Where(e => e.selected && e.hasUnusedProps).Select(e => e.mat).ToList();
                int n = MaterialOptimizer.CleanUnusedProperties(selected);
                EditorUtility.DisplayDialog(L.Confirm, L.MatCleanDone(n), L.OK);
                RefreshMaterials();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // 2) Duplicate materials (Merge)
            EditorGUILayout.BeginVertical("box", GUILayout.Width(position.width * 0.48f));
            EditorGUILayout.LabelField(L.IsJP ? "マテリアルの統合" : "Merge Materials", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(totalDups > 0 ? L.MatDups(totalDups) : L.MatAllClean, EditorStyles.miniLabel);
            
            GUI.backgroundColor = totalDups > 0 ? new Color(0.9f, 0.5f, 0.9f) : Color.white;
            if (GUILayout.Button(L.MatMerge, GUILayout.Height(30)))
            {
                var selected = _matEntries.Where(e => e.selected).Select(e => e.mat).ToList();
                int n = MaterialOptimizer.MergeDuplicates(_avatar, selected);
                EditorUtility.DisplayDialog(L.Confirm, L.MatMergeDone(n), L.OK);
                RefreshMaterials();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            GUI.enabled = true;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

        // Quest Shader Conversion - REMOVED
        }

        // ------------------------------------------------
        //  PERFORMANCE TAB (VRChat SDK Style)
        // ------------------------------------------------
        PerformanceDiagnostic.DiagResult _perfResult;
        Vector2 _perfScroll;
        [SerializeField] List<bool> _pbSelected = new List<bool>();
        bool _pbFoldout = false;

        void DrawPerfTab()
        {
            // --- Optimization Result Section (Matching Mockup) ---
            if (_hasOptimizationResult && _baseStats.valid && _currentStats.valid)
            {
                EditorGUILayout.BeginVertical("box");
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(L.ResultTitle, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(20))) _hasOptimizationResult = false;
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(2);
                Rect divider = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(divider, new Color(1f, 1f, 1f, 0.5f));
                EditorGUILayout.Space(4);

                DrawResultRow(L.ResultTexture, _baseStats.texMB, _currentStats.texMB, "MB");
                DrawResultRow(L.ResultPolygon, _baseStats.polys, _currentStats.polys, "");
                DrawResultRow(L.ResultBone, _baseStats.bones, _currentStats.bones, "");
                
                // Physics stats in summary
                if (_baseStats.pbComponents > 0 || _currentStats.pbComponents > 0)
                {
                    DrawResultRow(L.ResultPB, _baseStats.pbComponents, _currentStats.pbComponents, "");
                    DrawResultRow(L.ResultPBTrans, _baseStats.pbTransforms, _currentStats.pbTransforms, "");
                }

                DrawResultRow(L.ResultVRAM, _baseStats.vramMB, _currentStats.vramMB, "MB");

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(10);
            }

            if (GUILayout.Button(L.PerfScanBtn, GUILayout.Height(28)))
            {
                _perfResult = PerformanceDiagnostic.Scan(_avatar);
                if (_avatar != null) _baseStats.Capture(_avatar);
            }

            if (_perfResult == null) return;

            var r = _perfResult;
            _perfScroll = EditorGUILayout.BeginScrollView(_perfScroll, GUILayout.ExpandHeight(true));

            // Use a dark box for the entire list like the VRC SDK
            EditorGUILayout.BeginVertical("box");

            // Display Avatar Stats
            var s = r.stats;
            
            EditorGUILayout.LabelField($"=== Overall Rank: {s.OverallRank} ===", GetRankStyle(s.OverallRank, 14, true));
            EditorGUILayout.Space(2);

            // Always show Polygons
            DrawPerfIconRow(L.StatPolygons(s.polygons), s.PolyRank);
            DrawPerfIconRow(L.StatSkinnedMeshes(s.skinnedMeshes), s.SkinnedRank);
            DrawPerfIconRow(L.StatMaterialSlots(s.materialSlots), s.MatRank);
            DrawPerfIconRow(L.StatBones(s.bones), s.BonesRank);
            DrawPerfIconRow(L.StatPB(s.physBoneComponents), s.PBRank);
            DrawPerfIconRow(L.StatPBTrans(s.physBoneTransforms), s.PBTransRank);

            // 1. Unused AudioSources
            if (r.unusedAudio.Count > 0)
            {
                DrawPerfRow(L.PerfUnusedAudio(r.unusedAudio.Count), "Poor", GetRankColor("Poor"), "console.erroricon", () => {
                    int n = PerformanceDiagnostic.RemoveUnusedAudio(r.unusedAudio);
                    EditorUtility.DisplayDialog(L.Confirm, L.PerfFixed(n), L.OK);
                    _perfResult = PerformanceDiagnostic.Scan(_avatar);
                    CaptureOptimizationResult();
                });
            }

            // 2. Empty Particle Systems
            if (r.emptyParticles.Count > 0)
            {
                DrawPerfRow(L.PerfEmptyPtcl(r.emptyParticles.Count), "Medium", new Color(0.9f, 0.7f, 0.1f), "console.warnicon", () => {
                    int n = PerformanceDiagnostic.DisableEmptyParticles(r.emptyParticles);
                    EditorUtility.DisplayDialog(L.Confirm, L.PerfFixed(n), L.OK);
                    _perfResult = PerformanceDiagnostic.Scan(_avatar);
                    CaptureOptimizationResult();
                });
            }

            // 3. Missing Scripts
            if (r.missingScriptCount > 0)
            {
                DrawPerfRow(L.PerfMissingObj(r.missingScriptObjects.Count), "VeryPoor", new Color(0.9f, 0.2f, 0.2f), "console.erroricon", () => {
                    int n = PerformanceDiagnostic.CleanMissingScripts(r.missingScriptObjects);
                    EditorUtility.DisplayDialog(L.Confirm, L.PerfFixed(n), L.OK);
                    _perfResult = PerformanceDiagnostic.Scan(_avatar);
                    CaptureOptimizationResult();
                });
            }
            // 4. Exceeding PhysBones
            if (r.tooManyPhysBones.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                _pbFoldout = EditorGUILayout.Foldout(_pbFoldout, L.PerfPBLimit(r.tooManyPhysBones.Count), true, EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                
                // Rank label (VeryPoor)
                var rankStyle = GetRankStyle("VeryPoor", 11, true);
                EditorGUILayout.LabelField("VeryPoor", rankStyle, GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();

                if (_pbFoldout)
                {
                    EditorGUILayout.Space(2);
                    SyncPBSelection(r.tooManyPhysBones);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(L.SelectAll, EditorStyles.miniButtonLeft, GUILayout.Width(60)))
                        for (int i = 0; i < _pbSelected.Count; i++) _pbSelected[i] = true;
                    if (GUILayout.Button(L.DeselectAll, EditorStyles.miniButtonRight, GUILayout.Width(60)))
                        for (int i = 0; i < _pbSelected.Count; i++) _pbSelected[i] = false;
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel++;
                    for (int i = 0; i < r.tooManyPhysBones.Count; i++)
                    {
                        var pb = r.tooManyPhysBones[i];
                        if (pb == null) continue;
                        EditorGUI.BeginChangeCheck();
                        bool val = EditorGUILayout.ToggleLeft(pb.name, _pbSelected[i], EditorStyles.miniLabel);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _pbSelected[i] = val;
                            GUI.FocusControl(null);
                        }
                    }
                    EditorGUI.indentLevel--;

                    int selCount = 0;
                    for (int i = 0; i < _pbSelected.Count; i++) if (_pbSelected[i]) selCount++;

                    GUI.enabled = selCount > 0;
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button($"{L.PerfFixTxt} ({selCount})", GUILayout.Height(26)))
                    {
                        var toRemove = new List<Component>();
                        for (int i = 0; i < r.tooManyPhysBones.Count; i++)
                            if (_pbSelected[i] && r.tooManyPhysBones[i] != null)
                                toRemove.Add(r.tooManyPhysBones[i]);
                        int n = PerformanceDiagnostic.RemovePhysBones(toRemove);
                        EditorUtility.DisplayDialog(L.Confirm, L.PerfFixed(n), L.OK);
                        _perfResult = PerformanceDiagnostic.Scan(_avatar);
                        _pbSelected.Clear();
                        CaptureOptimizationResult();
                    }
                    GUI.backgroundColor = Color.white;
                    GUI.enabled = true;
                }
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        void DrawResultRow(string label, long before, long after, string unit)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label + ":", GUILayout.Width(100));
            
            string beforeStr = before >= 10000 ? string.Format("{0:N0}", before) : before.ToString();
            string afterStr = after >= 10000 ? string.Format("{0:N0}", after) : after.ToString();
            
            float pct = before > 0 ? (float)(after - before) / before * 100f : 0;
            string pctStr = string.Format("({0}{1:F0}%)", pct >= 0 ? "+" : "", pct);
            
            // Format to match mockup: "8 MB → 2 MB (-74%)"
            string unitStr = string.IsNullOrEmpty(unit) ? "" : $" {unit}";
            EditorGUILayout.LabelField($"{beforeStr}{unitStr} → {afterStr}{unitStr} {pctStr}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        void CaptureOptimizationResult()
        {
            if (_avatar == null) return;
            _currentStats.Capture(_avatar);
            _hasOptimizationResult = true;
        }

        void DrawPerfRow(string text, string rank, Color rankColor, string iconName, System.Action onFix)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(24));

            var style = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            style.normal.textColor = rankColor;
            GUILayout.Label($"{text}: {rank}", style);

            GUILayout.FlexibleSpace();

            if (onFix != null)
            {
                var btnStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 20 };
                if (GUILayout.Button(L.PerfFixTxt, btnStyle, GUILayout.Width(50)))
                    onFix.Invoke();
            }

            EditorGUILayout.EndHorizontal();
        }

        Color GetRankColor(string rank)
        {
            if (rank == "Excellent" || rank == "Good") return new Color(0.2f, 0.8f, 0.3f);
            if (rank == "Medium") return new Color(0.9f, 0.7f, 0.1f);
            if (rank == "Poor") return new Color(0.9f, 0.4f, 0.2f);
            if (rank == "VeryPoor") return new Color(0.9f, 0.2f, 0.2f);
            return Color.white;
        }

        GUIStyle GetRankStyle(string rank, int fontSize = 12, bool bold = false)
        {
            var style = new GUIStyle(bold ? EditorStyles.boldLabel : EditorStyles.label);
            style.fontSize = fontSize;
            style.normal.textColor = GetRankColor(rank);
            return style;
        }

        void DrawPerfIconRow(string text, string rank)
        {
            Color color;
            string iconName;

            if (rank == "VeryPoor")
            {
                color = new Color(0.9f, 0.2f, 0.2f);
                iconName = "console.erroricon";
            }
            else if (rank == "Poor")
            {
                color = new Color(0.9f, 0.4f, 0.2f); // Orangeish
                iconName = "console.erroricon";
            }
            else if (rank == "Medium")
            {
                color = new Color(0.9f, 0.7f, 0.1f); // Yellow
                iconName = "console.warnicon";
            }
            else
            {
                color = new Color(0.2f, 0.8f, 0.3f);
                iconName = "Collab";
            }

            DrawPerfRow(text, rank, color, iconName, null);
        }

        // ------------------------------------------------
        void SyncPBSelection(List<Component> list)
        {
            if (_pbSelected == null) _pbSelected = new List<bool>();
            if (list == null || list.Count == 0)
            {
                _pbSelected.Clear();
                return;
            }
            // Only re-initialize if the count truly changed (e.g. after a fix)
            if (_pbSelected.Count != list.Count)
            {
                _pbSelected = new List<bool>();
                for (int i = 0; i < list.Count; i++) _pbSelected.Add(false);
            }
        }

        //  Shared Helpers
        // ------------------------------------------------
        void DrawRestoreButton()
        {
            if (!BackupManager.HasBackup()) return;
            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.9f, 0.7f, 0.3f);
            if (GUILayout.Button(L.Restore, GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog(L.Confirm, L.ConfirmRestore, L.OK, L.Cancel))
                {
                    if (BackupManager.Restore())
                    {
                        EditorUtility.DisplayDialog(L.Confirm, L.RestoreDone, L.OK);
                        DoTexScan();
                    }
                    else
                        EditorUtility.DisplayDialog(L.Confirm, L.NoBackup, L.OK);
                }
            }
            GUI.backgroundColor = Color.white;
        }

        void DrawVRAMSummary()
        {
            long before = 0, after = 0;
            int targetSize = TextureProcessor.SizeOptions[_sizeIdx];
            var fmt = GetEstimateFormat();
            for (int i = 0; i < _textures.Count; i++)
            {
                if (!_selected[i] || _textures[i] == null) continue;
                before += VRAMCalc.EstimateFromTexture(_textures[i]);
                after += VRAMCalc.EstimateAfter(_textures[i], targetSize, fmt);
            }
            if (before == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{L.VRAMBefore}: {VRAMCalc.FormatSize(before)}");
            EditorGUILayout.LabelField($"{L.VRAMAfter}: {VRAMCalc.FormatSize(after)}");
            long saved = before - after;
            if (saved > 0)
            {
                float pct = (float)saved / before * 100f;
                EditorGUILayout.LabelField($"✓ {L.Reduction}: {VRAMCalc.FormatSize(saved)} ({pct:F0}%)",
                    new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.2f, 0.8f, 0.3f) } });
            }
            EditorGUILayout.EndVertical();
        }

        TextureImporterFormat GetEstimateFormat()
        {
            if (_questOpt) return TextureImporterFormat.ASTC_6x6; // Realistic for Quest
            switch (_preset)
            {
                case TextureProcessor.CompressionPreset.HighQ: return TextureImporterFormat.BC7;
                case TextureProcessor.CompressionPreset.LowQ: return TextureImporterFormat.DXT1Crunched;
                case TextureProcessor.CompressionPreset.Normal: return TextureImporterFormat.DXT5;
                default: return TextureImporterFormat.DXT5;
            }
        }

        void DoTexScan()
        {
            _textures = TextureScanner.Scan(_avatar);
            _selected = new bool[_textures.Count];
            SetAll(true);
        }

        void DoOptimize()
        {
            if (!EditorUtility.DisplayDialog(L.Confirm, L.ConfirmOpt, L.OK, L.Cancel)) return;
            BackupManager.Save(_textures);
            int targetSize = TextureProcessor.SizeOptions[_sizeIdx];
            TextureProcessor.Optimize(_textures, _selected, targetSize, _preset, _questOpt);
            EditorUtility.DisplayDialog(L.Confirm, L.Done, L.OK);
            DoTexScan();
            CaptureOptimizationResult();
        }

        void SetAll(bool v) { for (int i = 0; i < _selected.Length; i++) _selected[i] = v; }

        int CountSelected()
        {
            int c = 0;
            for (int i = 0; i < _selected.Length; i++) if (_selected[i]) c++;
            return c;
        }

        string[] SizeLabels()
        {
            var opts = TextureProcessor.SizeOptions;
            var labels = new string[opts.Length];
            for (int i = 0; i < opts.Length; i++) labels[i] = opts[i].ToString();
            return labels;
        }
        // ------------------------------------------------
        //  MESH TAB (Phase 3)
        // ------------------------------------------------
        void DrawMeshTab()
        {
            if (_avatar == null)
            {
                EditorGUILayout.HelpBox(L.NoTex, MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(L.DuplicateAvatar, MessageType.Info);
            _safetyDuplicate = EditorGUILayout.ToggleLeft(L.OptSafetyToggle, _safetyDuplicate, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L.OptSafetyDesc, EditorStyles.miniLabel);
            EditorGUILayout.Space(5);
            
            // BlendShape Optimizer (One unified block)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L.MeshBlendShapeOpt, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L.MeshBlendShapeDesc, EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(L.BtnRemoveShapes, GUILayout.Height(30)))
            {
                int count = BlendShapeOptimizer.OptimizeAvatar(_avatar);
                EditorUtility.DisplayDialog(L.Confirm, L.BlendShapeDone(count), L.OK);
                RefreshAll();
                CaptureOptimizationResult();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Mesh Combination Section
            if (_smrs.Count == 0 || _lastAvatar != _avatar) RefreshSMRs();

            EditorGUILayout.LabelField(L.MeshCombine, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(L.MeshCombineDesc, MessageType.Info);
            
            EditorGUILayout.Space(5);

            _meshScroll = EditorGUILayout.BeginScrollView(_meshScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _smrs.Count; i++)
            {
                var smr = _smrs[i];
                if (smr == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                bool val = EditorGUILayout.Toggle(_smrSelected[i], GUILayout.Width(16));
                if (EditorGUI.EndChangeCheck())
                {
                    _smrSelected[i] = val;
                    GUI.FocusControl(null);
                }
                EditorGUILayout.ObjectField(smr, typeof(SkinnedMeshRenderer), true);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            
            // Combination Group Name
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(L.MeshGroupLabel, GUILayout.Width(100));
            GUI.SetNextControlName("MeshGroupName");
            _combinationGroupName = EditorGUILayout.TextField(_combinationGroupName);
            EditorGUILayout.EndHorizontal();

            GUI.enabled = _smrSelected.Count(s => s) >= 2;
            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
            if (GUILayout.Button(L.BtnCombine, GUILayout.Height(36)))
            {
                // Store selections before refresh
                var selectedIndices = new List<int>();
                for (int i = 0; i < _smrSelected.Count; i++) if (_smrSelected[i]) selectedIndices.Add(i);

                var copy = DuplicateBeforeOp(_avatar, "_Combined");
                if (copy != null)
                {
                    var toCombine = new List<SkinnedMeshRenderer>();
                    for (int i = 0; i < selectedIndices.Count; i++)
                    {
                        int idx = selectedIndices[i];
                        if (idx < _smrs.Count) toCombine.Add(_smrs[idx]);
                    }

                    MeshCombiner.Combine(copy, toCombine, _combinationGroupName);
                    EditorUtility.DisplayDialog(L.Confirm, L.Done, L.OK);
                    RefreshAll();
                    CaptureOptimizationResult();
                }
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

        }

        void DrawBoneTab()
        {
            if (_avatar == null)
            {
                EditorGUILayout.HelpBox(L.NoTex, MessageType.Info);
                return;
            }

            // Auto refresh if avatar changed
            if (_avatar != _lastAvatar)
            {
                _lastAvatar = _avatar;
                RefreshBones();
            }

            EditorGUILayout.Space(4);

            // --- Performance Summary (QuestTools style) ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Performance Summary", EditorStyles.boldLabel);
            
            int physBoneCount = 0;
            int physBoneColliderCount = 0;
            int contactCount = 0;
            
            foreach (var comp in _avatar.GetComponentsInChildren<Component>(true))
            {
                string typeName = comp.GetType().Name;
                if (typeName == "VRCPhysBone" || typeName == "PhysBone") physBoneCount++;
                else if (typeName == "VRCPhysBoneCollider" || typeName == "PhysBoneCollider") physBoneColliderCount++;
                else if (typeName == "ContactReceiver" || typeName == "ContactSender") contactCount++;
            }
            
            int transformCount = _avatar.GetComponentsInChildren<Transform>(true).Length;
            
            // Display as rows
            EditorGUILayout.LabelField($"PhysBone Components: {physBoneCount}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"PhysBone Colliders: {physBoneColliderCount}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Contacts: {contactCount}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Total Transforms: {transformCount}", EditorStyles.miniLabel);
            
            // Quest compatibility warning
            if (transformCount > 256)
            {
                EditorGUILayout.HelpBox($"⚠️ This avatar has {transformCount} bones (Quest limit: 256)", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // Auto Bone Stripping
            EditorGUILayout.LabelField(L.MeshBoneOpt, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L.MeshBoneDesc, EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(L.BtnRemoveBones, GUILayout.Height(30)))
            {
                int removed = BoneOptimizer.Optimize(_avatar);
                EditorUtility.DisplayDialog(L.Confirm, L.BoneDone(removed), L.OK);
                RefreshBones();
                CaptureOptimizationResult();
            }

            EditorGUILayout.Space(10);

            // Manual Bone Deletion (Categorized)
            EditorGUILayout.LabelField(L.MeshBoneManual, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L.MeshBoneManualDesc, EditorStyles.wordWrappedMiniLabel);

            if (_boneCategories.Count == 0) RefreshBones();

            EditorGUILayout.Space(2);
            _boneScroll = EditorGUILayout.BeginScrollView(_boneScroll, "box", GUILayout.ExpandHeight(true));

            int totalSelected = 0;
            foreach (var cat in _boneCategories)
            {
                if (cat.entries.Count == 0) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                // Dynamic name based on language
                string localizedName = cat.name;
                if (cat.name == "PhysBones") localizedName = L.CategoryPhysBones;
                else if (cat.name == "PhysBone Colliders" || cat.name == "PhysBone コライダー") localizedName = L.CategoryColliders;
                else if (cat.name == "Contacts (Sender/Receiver)" || cat.name == "コンタクト (Sender/Receiver)") localizedName = L.CategoryContacts;
                else if (cat.name == "Constraints" || cat.name == "コンストレイント") localizedName = L.CategoryConstraints;

                cat.isOpen = EditorGUILayout.Foldout(cat.isOpen, $"{localizedName} ({cat.entries.Count})", true, EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L.SelectAll, EditorStyles.miniButtonLeft)) { foreach (var e in cat.entries) e.selected = true; }
                if (GUILayout.Button(L.DeselectAll, EditorStyles.miniButtonRight)) { foreach (var e in cat.entries) e.selected = false; }
                EditorGUILayout.EndHorizontal();

                if (cat.isOpen)
                {
                    EditorGUI.indentLevel++;
                    foreach (var entry in cat.entries)
                    {
                        if (entry.t == null) continue;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginChangeCheck();
                        bool val = EditorGUILayout.ToggleLeft(entry.displayLabel, entry.selected);
                        if (EditorGUI.EndChangeCheck())
                        {
                            entry.selected = val;
                            GUI.FocusControl(null);
                        }
                        if (GUILayout.Button("⊙", EditorStyles.miniButton, GUILayout.Width(20)))
                        {
                            Selection.activeTransform = entry.t;
                            EditorGUIUtility.PingObject(entry.t);
                        }
                        EditorGUILayout.EndHorizontal();
                        if (entry.selected) totalSelected++;
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Refresh", GUILayout.Height(24))) RefreshBones();

            GUI.enabled = totalSelected > 0;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button(L.BtnRemoveManualBones, GUILayout.Height(30)))
            {
                var selected = new List<Transform>();
                foreach (var cat in _boneCategories)
                    foreach (var e in cat.entries)
                        if (e.selected && e.t != null) selected.Add(e.t);

                int n = BoneOptimizer.DeleteSelectedBones(selected);
                EditorUtility.DisplayDialog(L.Confirm, L.BoneDone(n), L.OK);
                RefreshBones();
                CaptureOptimizationResult();
            }

            if (GUILayout.Button(L.BtnRemoveManualComponents, GUILayout.Height(30)))
            {
                var selected = new List<Transform>();
                foreach (var cat in _boneCategories)
                    foreach (var e in cat.entries)
                        if (e.selected && e.t != null) selected.Add(e.t);

                int n = BoneOptimizer.DeleteSelectedComponents(selected);
                EditorUtility.DisplayDialog(L.Confirm, L.ComponentDone(n), L.OK);
                RefreshBones();
                CaptureOptimizationResult();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        void RefreshSMRs()
        {
            if (_avatar == null) return;
            var oldSmrs = _smrs;
            var oldSelected = _smrSelected;
            
            _smrs = new List<SkinnedMeshRenderer>(_avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            _smrSelected = new List<bool>();
            
            for (int i = 0; i < _smrs.Count; i++)
            {
                bool wasSelected = false;
                if (oldSmrs != null && i < oldSmrs.Count && oldSelected != null && i < oldSelected.Count)
                {
                    // If switching avatars, don't preserve selection
                    if (oldSmrs.Any(s => s != null && s.transform.root == _avatar.transform.root))
                        wasSelected = oldSelected[i];
                }
                _smrSelected.Add(wasSelected);
            }
        }

        void RefreshBones()
        {
            if (_avatar == null) return;
            
            // Store old selections to preserve them
            var oldSelections = new Dictionary<Transform, bool>();
            foreach (var cat in _boneCategories)
                foreach (var entry in cat.entries)
                    if (entry.t != null) oldSelections[entry.t] = entry.selected;

            _boneCategories.Clear();

            var phys = new FoldoutState { name = L.CategoryPhysBones, isOpen = true };
            var coll = new FoldoutState { name = L.CategoryColliders, isOpen = true };
            var cont = new FoldoutState { name = L.CategoryContacts, isOpen = true };
            var cons = new FoldoutState { name = L.CategoryConstraints, isOpen = false };

            var all = _avatar.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t == _avatar.transform) continue;

                var comps = t.GetComponents<Component>();
                if (comps.Length <= 1) continue;

                foreach (var c in comps)
                {
                    if (c == null || c is Transform) continue;
                    string typeName = c.GetType().FullName;

                    bool sel = oldSelections.ContainsKey(t) ? oldSelections[t] : false;

                    if (typeName.Contains("VRCPhysBone") && !typeName.Contains("Collider"))
                        phys.entries.Add(new BoneEntry { t = t, typeLabel = "PhysBone", selected = sel });
                    else if (typeName.Contains("VRCPhysBoneCollider"))
                        coll.entries.Add(new BoneEntry { t = t, typeLabel = "Collider", selected = sel });
                    else if (typeName.Contains("VRCContact"))
                        cont.entries.Add(new BoneEntry { t = t, typeLabel = "Contact", selected = sel });
                    else if (typeName.Contains("Constraint"))
                        cons.entries.Add(new BoneEntry { t = t, typeLabel = "Constraint", selected = sel });
                }
            }

            _boneCategories.Add(phys);
            _boneCategories.Add(coll);
            _boneCategories.Add(cont);
            _boneCategories.Add(cons);

            // Post-process labels to handle duplicates
            foreach (var cat in _boneCategories)
            {
                var transformCounts = new Dictionary<Transform, int>();
                foreach (var entry in cat.entries)
                {
                    if (entry.t == null) continue;
                    if (!transformCounts.ContainsKey(entry.t)) transformCounts[entry.t] = 0;
                    transformCounts[entry.t]++;

                    int countOnThisTransform = cat.entries.Count(e => e.t == entry.t);
                    if (countOnThisTransform > 1)
                        entry.displayLabel = $"{entry.t.name} ({transformCounts[entry.t]})";
                    else
                        entry.displayLabel = entry.t.name;
                }
            }
        }

        // ------------------------------------------------
        //  ATLAS TAB (Phase 4)
        // ------------------------------------------------
        void DrawAtlasTab()
        {
            if (_avatar == null)
            {
                EditorGUILayout.HelpBox(L.NoTex, MessageType.Info);
                return;
            }

            // Refresh materials if needed
            if (_atlasMaterials.Count == 0 || _lastAvatar != _avatar)
            {
                RefreshAtlasMaterials();
            }

            EditorGUILayout.LabelField(L.AtlasTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(L.AtlasDesc, MessageType.Info);

            EditorGUILayout.Space(5);
            
            // Selection UI
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(L.SelectAll, EditorStyles.miniButtonLeft, GUILayout.Width(60)))
                foreach (var m in _atlasMaterials) m.selected = true;
            if (GUILayout.Button(L.DeselectAll, EditorStyles.miniButtonRight, GUILayout.Width(60)))
                foreach (var m in _atlasMaterials) m.selected = false;
            EditorGUILayout.EndHorizontal();

            _atlasScroll = EditorGUILayout.BeginScrollView(_atlasScroll, "box", GUILayout.ExpandHeight(true));
            foreach (var entry in _atlasMaterials)
            {
                if (entry.mat == null) continue;
                EditorGUI.BeginChangeCheck();
                bool val = EditorGUILayout.ToggleLeft(entry.mat.name, entry.selected);
                if (EditorGUI.EndChangeCheck())
                {
                    entry.selected = val;
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(L.DuplicateAvatar, MessageType.Info);
            _safetyDuplicate = EditorGUILayout.ToggleLeft(L.OptSafetyToggle, _safetyDuplicate, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L.OptSafetyDesc, EditorStyles.miniLabel);

            int selectedCount = _atlasMaterials.Count(m => m.selected);
            GUI.enabled = selectedCount >= 2;
            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
            if (GUILayout.Button($"{L.BtnAtlasRun} ({selectedCount})", GUILayout.Height(40)))
            {
                var selected = _atlasMaterials.Where(m => m.selected).Select(m => m.mat).ToList();
                
                // Non-destructive: Duplicate first
                var copy = DuplicateBeforeOp(_avatar, "_Atlassed");

                int count = TextureAtlasser.AtlasAvatar(copy, selected);
                if (count > 0)
                {
                    EditorUtility.DisplayDialog(L.Confirm, L.AtlasDone(count), L.OK);
                    RefreshAll();
                    CaptureOptimizationResult();
                }
                else
                {
                    EditorUtility.DisplayDialog(L.Confirm, L.MatNone, L.OK);
                }
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        GameObject DuplicateBeforeOp(GameObject original, string suffix)
        {
            if (original == null) return null;
            if (!_safetyDuplicate) return original;
            
            // If already a copy from this tool, don't duplicate again
            if (original.name.Contains("_Optimized") || original.name.Contains("_Combined") || original.name.Contains("_Atlassed"))
            {
                RefreshSMRsPreserveSelection();
                return original;
            }

            Undo.RecordObject(original, "Hide Original Avatar");
            original.SetActive(false);

            var copy = Instantiate(original);
            copy.SetActive(true);
            copy.name = original.name + suffix;
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate for Mesh Op");

            Selection.activeGameObject = copy;
            _avatar = copy;
            RefreshSMRsPreserveSelection();
            return copy;
        }

        void RefreshSMRsPreserveSelection()
        {
            if (_avatar == null) return;
            var oldSmrs = _smrs;
            var oldSelected = _smrSelected;
            
            _smrs = new List<SkinnedMeshRenderer>(_avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            _smrSelected = new List<bool>();
            
            for (int i = 0; i < _smrs.Count; i++)
            {
                bool wasSelected = false;
                if (oldSmrs != null && i < oldSmrs.Count && oldSelected != null && i < oldSelected.Count)
                {
                    wasSelected = oldSelected[i];
                }
                _smrSelected.Add(wasSelected);
            }
        }

        void RefreshAtlasMaterials()
        {
            if (_avatar == null) return;
            var oldSel = new Dictionary<Material, bool>();
            foreach (var m in _atlasMaterials) if (m.mat != null) oldSel[m.mat] = m.selected;

            _atlasMaterials.Clear();
            var renderers = _avatar.GetComponentsInChildren<Renderer>(true);
            var mats = new HashSet<Material>();
            foreach (var r in renderers)
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.shader != null && m.shader.name.Contains("lilToon")) mats.Add(m);

            foreach (var m in mats)
            {
                bool sel = oldSel.ContainsKey(m) ? oldSel[m] : false;
                _atlasMaterials.Add(new AtlasMaterialEntry { mat = m, selected = sel });
            }
        }

        void RefreshMaterials()
        {
            if (_avatar == null) return;
            
            var oldSel = new Dictionary<Material, bool>();
            foreach (var e in _matEntries) if (e.mat != null) oldSel[e.mat] = e.selected;

            var res = MaterialOptimizer.Scan(_avatar);
            _matEntries.Clear();

            foreach (var m in res.allMaterials)
            {
                bool sel = oldSel.ContainsKey(m) ? oldSel[m] : true; // Default selected
                _matEntries.Add(new MaterialEntry {
                    mat = m,
                    selected = sel,
                    hasUnusedProps = res.unusedPropMats.Contains(m),
                    isDuplicate = res.duplicateMats.Contains(m)
                });
            }
        }

        // ------------------------------------------------
        //  EXPORT TAB
        // ------------------------------------------------
        List<AnimationPathFixer.BrokenBinding> _brokenPaths = new List<AnimationPathFixer.BrokenBinding>();
        Vector2 _exportScroll;
        bool _pathScanned = false;

        void DrawExportTab()
        {
            if (_avatar == null)
            {
                EditorGUILayout.HelpBox(L.NoTex, MessageType.Info);
                return;
            }

            _exportScroll = EditorGUILayout.BeginScrollView(_exportScroll);

            // --- MA Bake ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L.MABakeTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L.MABakeDesc, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            bool maAvailable = MABakeExporter.IsMAAvailable();
            if (!maAvailable)
            {
                EditorGUILayout.HelpBox(L.MANotFound, MessageType.Warning);
            }

            GUI.enabled = maAvailable;
            GUI.backgroundColor = maAvailable ? new Color(0.4f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button(L.MABakeBtn, GUILayout.Height(36)))
            {
                if (EditorUtility.DisplayDialog(L.Confirm, L.MABakeDesc, L.OK, L.Cancel))
                {
                    var baked = MABakeExporter.Bake(_avatar);
                    if (baked != null)
                    {
                        _avatar = baked;
                        RefreshAll();
                        EditorUtility.DisplayDialog(L.Confirm, L.MABakeDone, L.OK);
                    }
                }
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // --- Animation Path Fixer ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L.PathFixerTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(L.PathFixerDesc, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            if (GUILayout.Button(L.PathFixerScan, GUILayout.Height(30)))
            {
                _brokenPaths = AnimationPathFixer.Scan(_avatar);
                _pathScanned = true;
            }

            if (_pathScanned)
            {
                EditorGUILayout.Space(4);
                if (_brokenPaths.Count == 0)
                {
                    EditorGUILayout.HelpBox(L.PathFixerNone, MessageType.Info);
                }
                else
                {
                    var realBroken = _brokenPaths.Where(b => !b.isMaybeMAManaged).ToList();
                    var maBroken   = _brokenPaths.Where(b =>  b.isMaybeMAManaged).ToList();

                    EditorGUILayout.LabelField(L.PathFixerFound(realBroken.Count), EditorStyles.boldLabel);

                    if (maBroken.Count > 0)
                    {
                        var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
                        EditorGUILayout.LabelField(L.MAManagedNote(maBroken.Count), style);
                    }

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(L.SelectAll, EditorStyles.miniButtonLeft))
                        foreach (var b in _brokenPaths) b.selected = b.suggestedPath != null;
                    if (GUILayout.Button(L.DeselectAll, EditorStyles.miniButtonRight))
                        foreach (var b in _brokenPaths) b.selected = false;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(2);

                    foreach (var entry in _brokenPaths)
                    {
                        if (entry.isMaybeMAManaged) continue;

                        EditorGUILayout.BeginVertical("box");

                        EditorGUILayout.BeginHorizontal();
                        GUI.enabled = entry.suggestedPath != null;
                        EditorGUI.BeginChangeCheck();
                        bool val = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(16));
                        if (EditorGUI.EndChangeCheck()) { entry.selected = val; GUI.FocusControl(null); }
                        GUI.enabled = true;

                        EditorGUILayout.LabelField(entry.clip != null ? entry.clip.name : "?", EditorStyles.miniLabel, GUILayout.Width(120));
                        EditorGUILayout.EndHorizontal();

                        var old = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.9f, 0.3f, 0.3f) } };
                        EditorGUILayout.LabelField($"  ✗  {entry.binding.path}", old);

                        if (entry.suggestedPath != null)
                        {
                            var fix = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.2f, 0.8f, 0.3f) } };
                            EditorGUILayout.LabelField($"  ✓  {entry.suggestedPath}", fix);
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"  ?  {L.PathFixerNoFix}", EditorStyles.centeredGreyMiniLabel);
                        }

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(2);
                    }

                    int selCount = _brokenPaths.Count(b => b.selected);
                    GUI.enabled = selCount > 0;
                    GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
                    if (GUILayout.Button(L.PathFixerFix(selCount), GUILayout.Height(32)))
                    {
                        int n = AnimationPathFixer.Fix(_brokenPaths);
                        EditorUtility.DisplayDialog(L.Confirm, L.PathFixerDone(n), L.OK);
                        _brokenPaths = AnimationPathFixer.Scan(_avatar);
                    }
                    GUI.backgroundColor = Color.white;
                    GUI.enabled = true;
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
        // ------------------------------------------------
        //  SIMPLIFY TAB (Direct Edit)
        // ------------------------------------------------
        void DrawSimplifyTab()
        {
            _simplifyScroll = EditorGUILayout.BeginScrollView(_simplifyScroll);

            EditorGUILayout.LabelField(L.SimplifyTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(L.SimplifyDesc, MessageType.Info);
            EditorGUILayout.Space(6);

            // --- D&D Target ---
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L.SimplifyDrop, EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(2);

            var newTarget = (GameObject)EditorGUILayout.ObjectField(
                _simplifyTarget, typeof(GameObject), true, GUILayout.Height(30));

            // Handle Drag & Drop
            var dropArea = GUILayoutUtility.GetLastRect();
            var evt = Event.current;
            if (evt.type == EventType.DragUpdated && dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform && dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                    if (obj is GameObject go) { newTarget = go; break; }
                evt.Use();
            }

            // Target changed
            if (newTarget != _simplifyTarget)
            {
                // 不要な復元はせず、ターゲットの切り替えのみ行う
                _simplifyTarget = newTarget;
                if (_simplifyTarget != null)
                    CacheSimplifyMeshes();
            }

            EditorGUILayout.EndVertical();

            if (_simplifyTarget == null || _simplifySmrs == null)
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            // Safety: if target was deleted
            if (_simplifyTarget == null)
            {
                _simplifyOriginalMeshes = null;
                _simplifySmrs = null;
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space(8);

            // --- Polygon Info ---
            int currentTris = MeshSimplifier.GetTriangleCount(_simplifyTarget);
            if (_simplifyTarget != null && _simplifyOriginalTrisMap.ContainsKey(_simplifyTarget.GetInstanceID()))
                _simplifyOriginalTris = _simplifyOriginalTrisMap[_simplifyTarget.GetInstanceID()];
            else
                _simplifyOriginalTris = MeshSimplifier.GetTriangleCount(_simplifyTarget);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(L.SimplifyTris(_simplifyOriginalTris, currentTris), EditorStyles.boldLabel);

            float ratio = _simplifyOriginalTris > 0 ? (float)currentTris / _simplifyOriginalTris : 1f;
            var barRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(barRect, ratio, $"{(ratio * 100f):F0}%");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // --- Quality Slider (realtime) ---
            EditorGUILayout.LabelField(L.SimplifyQuality, EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            _simplifyQuality = EditorGUILayout.Slider(_simplifyQuality, 0.3f, 1f);
            
            if (EditorGUI.EndChangeCheck())
            {
                ApplySimplify();
                Repaint();
            }

            EditorGUILayout.Space(10);

            // --- Restore Button only ---
            GUI.backgroundColor = new Color(0.9f, 0.7f, 0.3f);
            if (GUILayout.Button(L.IsJP ? "元に戻す" : "Restore", GUILayout.Height(36)))
            {
                RestoreSimplifyMeshes();
                _simplifyQuality = 1f;
                _simplifyLastApplied = 1f;
                if (_simplifyTarget != null)
                    _simplifyQualityMap[_simplifyTarget.GetInstanceID()] = 1f;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndScrollView();
        }

        void CacheSimplifyMeshes()
        {
            if (_simplifyTarget == null) return;
            int id = _simplifyTarget.GetInstanceID();

            if (!_simplifySmrsMap.ContainsKey(id))
            {
                var smrs = _simplifyTarget.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var meshes = new Mesh[smrs.Length];
                for (int i = 0; i < smrs.Length; i++)
                {
                    if (smrs[i] != null && smrs[i].sharedMesh != null)
                        meshes[i] = smrs[i].sharedMesh;
                }
                _simplifyOriginalTrisMap[id] = MeshSimplifier.GetTriangleCount(_simplifyTarget);
                _simplifySmrsMap[id] = smrs;
                _simplifyOriginalMeshesMap[id] = meshes;
                
                var solvers = new List<MeshSimplifier.SolverInstance>();
                foreach (var m in meshes)
                    if (m != null) solvers.Add(new MeshSimplifier.SolverInstance(m));
                _simplifySolversMap[id] = solvers;
            }

            _simplifySmrs = _simplifySmrsMap[id];
            _simplifyOriginalMeshes = _simplifyOriginalMeshesMap[id];
            _simplifyOriginalTris = _simplifyOriginalTrisMap[id];
            _simplifySolvers = _simplifySolversMap[id];
            
            if (_simplifyQualityMap.ContainsKey(id))
            {
                _simplifyQuality = _simplifyQualityMap[id];
                _simplifyLastApplied = -1f;
                ApplySimplify();
            }
            else
            {
                _simplifyQuality = 1f;
                _simplifyLastApplied = 1f;
            }
        }

        void ApplySimplify()
        {
            if (_simplifySmrs == null || _simplifyOriginalMeshes == null) return;
            if (Mathf.Abs(_simplifyQuality - _simplifyLastApplied) < 0.001f) return;

            if (_simplifyQuality >= 0.999f)
            {
                for (int i = 0; i < _simplifySmrs.Length; i++)
                    if (_simplifySmrs[i] != null && _simplifyOriginalMeshes[i] != null)
                        _simplifySmrs[i].sharedMesh = _simplifyOriginalMeshes[i];
            }
            else
            {
                for (int i = 0; i < _simplifySmrs.Length; i++)
                {
                    if (i < _simplifySolvers.Count && _simplifySolvers[i] != null)
                    {
                        var simplified = _simplifySolvers[i].Simplify(_simplifyQuality);
                        if (simplified != null) _simplifySmrs[i].sharedMesh = simplified;
                    }
                }
            }
            _simplifyLastApplied = _simplifyQuality;
            if (_simplifyTarget != null)
                _simplifyQualityMap[_simplifyTarget.GetInstanceID()] = _simplifyQuality;
            
            CaptureOptimizationResult();
            SceneView.RepaintAll();
        }

        // Quest Conversion - REMOVED

        void RestoreSimplifyMeshes()
        {
            if (_simplifySmrs == null || _simplifyOriginalMeshes == null) return;
            for (int i = 0; i < _simplifySmrs.Length; i++)
            {
                if (_simplifySmrs[i] != null && _simplifyOriginalMeshes[i] != null)
                    _simplifySmrs[i].sharedMesh = _simplifyOriginalMeshes[i];
            }
        }
    }
}
