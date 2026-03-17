// EzPz Texture Optimizer - Localization
using UnityEditor;

namespace EzPz
{
    public static class L
    {
        static bool _jp = true;
        const string PrefKey = "EzPz_Lang";

        static L() { _jp = EditorPrefs.GetBool(PrefKey, true); }

        public static bool IsJP => _jp;

        public static void Toggle()
        {
            _jp = !_jp;
            EditorPrefs.SetBool(PrefKey, _jp);
        }

        // UI Labels
        public static string WindowTitle => "Ezpzoptimizer";
        public static string LangBtn => _jp ? "EN" : "JP";
        public static string AvatarLabel => _jp ? "アバター (GameObject)" : "Avatar (GameObject)";
        public static string ScanBtn => _jp ? "テクスチャ検出" : "Scan Textures";
        public static string SelectAll => _jp ? "全選択" : "Select All";
        public static string DeselectAll => _jp ? "全解除" : "Deselect All";
        public static string TargetSize => _jp ? "ターゲットサイズ" : "Target Size";
        public static string QuestOpt => _jp ? "Quest (Android) 最適化" : "Quest (Android) Optimize";
        public static string Optimize => _jp ? "▶ 最適化実行" : "▶ Optimize";
        public static string Restore => _jp ? "◀ 復元" : "◀ Restore";
        public static string NoTex            => _jp ? "テクスチャが見つかりません" : "No textures found";
        public static string NoAvatarSelected => _jp ? "アバターが選択されていません" : "No avatar selected";
        public static string VRAMBefore => _jp ? "変更前VRAM" : "VRAM Before";
        public static string VRAMAfter => _jp ? "変更後VRAM (推定)" : "VRAM After (Est.)";
        public static string Current => _jp ? "現在" : "Current";
        public static string Format => _jp ? "形式" : "Format";
        public static string Processing => _jp ? "処理中..." : "Processing...";
        public static string Done => _jp ? "完了しました" : "Done";
        public static string RestoreDone => _jp ? "復元しました" : "Restored";
        public static string NoBackup => _jp ? "バックアップがありません" : "No backup found";
        public static string ConfirmOpt => _jp ? "選択したテクスチャを最適化しますか？" : "Optimize selected textures?";
        public static string ConfirmRestore => _jp ? "バックアップから復元しますか？" : "Restore from backup?";
        public static string Confirm => _jp ? "確認" : "Confirm";
        public static string OK => "OK";
        public static string Cancel => _jp ? "キャンセル" : "Cancel";
        public static string AutoDetect => _jp ? "VRCアバター自動検出" : "Auto-detect VRC Avatar";
        public static string Reduction => _jp ? "削減" : "Reduction";
        public static string TexCount(int n) => _jp ? $"{n} テクスチャ検出" : $"{n} textures found";
        public static string CompFormat => _jp ? "圧縮形式" : "Compression";
        public static string TabTexel => _jp ? "密度解析" : "Scan Pixels";
        public static string ResultTitle => _jp ? "最適化結果 (Latest)" : "Optimization Results (Latest)";
        public static string ResultTexture => _jp ? "テクスチャ" : "Texture";
        public static string ResultPolygon => _jp ? "ポリゴン" : "Polygon";
        public static string ResultBone => _jp ? "ボーン" : "Bone";
        public static string ResultPB => _jp ? "PhysBone" : "PhysBone";
        public static string ResultPBTrans => _jp ? "PB Transform" : "PB Transform";
        public static string ResultVRAM => _jp ? "VRAM推定" : "VRAM Estimated";


        // Material Tab
        public static string TabTexture => _jp ? "テクスチャ" : "Texture";
        public static string TabMaterial => _jp ? "マテリアル" : "Material";
        public static string MatScanBtn => _jp ? "マテリアル検出" : "Scan Materials";
        public static string MatCount(int n) => _jp ? $"{n} マテリアル検出" : $"{n} materials found";
        public static string MatCleanup => _jp ? "🧹 不要プロパティ削除" : "🧹 Clean Unused Properties";
        public static string MatCleanupDesc => _jp ? "シェーダーで使われていないプロパティを削除してファイルサイズを軽量化" : "Remove properties not used by shader to reduce file size";
        public static string MatDupDesc => _jp ? "同じシェーダー・テクスチャを使うマテリアルを検出" : "Materials with identical shader & textures detected";
        public static string MatUnusedProps(int n) => _jp ? $"{n} 個のマテリアルに不要プロパティあり" : $"{n} materials have unused properties";
        public static string MatDups(int n) => _jp ? $"{n} 個の重複マテリアルグループ" : $"{n} duplicate material groups";
        public static string MatMerge => _jp ? "🔗 マテリアル統合 (Slot Merger)" : "🔗 Merge Duplicate Materials";
        public static string MatMergeDesc => _jp ? "同じ設定のマテリアルを1つに統合してDrawCallを削減します" : "Merge identical materials into one to reduce DrawCalls";
        public static string MatMergeDone(int n) => _jp ? $"{n} 個のRendererでマテリアルを統合しました" : $"Merged materials on {n} renderers";
        public static string MatCleanDone(int n) => _jp ? $"{n} 個のマテリアルをクリーンアップしました" : $"Cleaned {n} materials";
        public static string MatNone => _jp ? "マテリアルが見つかりません" : "No materials found";
        public static string MatAllClean => _jp ? "✓ すべてクリーン" : "✓ All clean";
        public static string Renderers(int n) => _jp ? $"レンダラー: {n}" : $"Renderers: {n}";

        // Performance Tab
        public static string TabPerf => _jp ? "パフォーマンス" : "Performance";
        public static string TabAtlas => _jp ? "アトラス" : "Atlas";
        public static string PerfScanBtn => _jp ? "パフォーマンス診断" : "Run Diagnostics";
        public static string PerfUnusedAudio(int n) => _jp ? $"{n} 個の不要なAudioSource" : $"{n} unused AudioSources";
        public static string PerfUnusedAudioDesc => _jp ? "音素がない、または音量0のAudioSourceを削除" : "Remove AudioSources with no clip or zero volume";
        public static string PerfEmptyPtcl(int n) => _jp ? $"{n} 個の空ParticleSystem" : $"{n} empty ParticleSystems";
        public static string PerfEmptyPtclDesc => _jp ? "何も放出していないパーティクルを無効化" : "Disable particles that emit nothing";
        public static string PerfMissingObj(int n) => _jp ? $"{n} 個のオブジェクトにMissing Script" : $"{n} objects have Missing Scripts";
        public static string PerfMissingDesc => _jp ? "エラーになっているMissing Scriptコンポーネントを削除" : "Remove broken Missing Script components";
        public static string PerfPBLimit(int n) => _jp ? $"{n} 個の過剰なPhysBone (16超過分)" : $"{n} excessive PhysBones (over 16)";
        public static string PerfPBLimitDesc => _jp ? "Excellent制限(16個)を超えた分のPBを手動確認" : "Review PhysBones exceeding the Excellent rank limit (16)";
        public static string PerfFixTxt => _jp ? "修正" : "Fix";
        public static string PerfFixed(int n) => _jp ? $"{n} 箇所を修正しました" : $"Fixed {n} items";
        public static string PerfAllClean => _jp ? "✓ パフォーマンス問題なし" : "✓ No performance issues found";

        public static string StatPolygons(int n) => _jp ? $"ポリゴン数: {n}" : $"Polygons: {n}";
        public static string StatSkinnedMeshes(int n) => _jp ? $"Skinned Meshes: {n}" : $"Skinned Meshes: {n}";
        public static string StatMaterialSlots(int n) => _jp ? $"マテリアルスロット: {n}" : $"Material Slots: {n}";
        public static string StatBones(int n) => _jp ? $"ボーン数: {n}" : $"Bones: {n}";
        public static string StatPB(int n) => _jp ? $"PhysBoneコンポーネント: {n}" : $"PhysBone Components: {n}";
        public static string StatPBTrans(int n) => _jp ? $"PhysBone Transform数: {n}" : $"PhysBone Transforms: {n}";

        // Mesh Tab (Phase 3)
        public static string TabMesh => _jp ? "メッシュ" : "Mesh";
        public static string TabBone => _jp ? "ボーン" : "Bone";
        public static string MeshBlendShapeOpt => _jp ? "👤 ブレンドシェイプ軽量化 (未使用ShapeKey削除)" : "👤 Strip Unused BlendShapes";
        public static string MeshBlendShapeDesc => _jp ? "現在のウェイトが0のシェイプキーを削除したメッシュを自動生成し、VRAMとファイルサイズを激減させます。" : "Removes BlendShapes with 0 weight by generating optimized meshes, drastically reducing VRAM/file size.";
        public static string MeshCombine => _jp ? "📦 メッシュ統合 (SkinnedMesh Combine)" : "📦 Combine Skinned Meshes";
        public static string MeshCombineDesc => _jp ? "選択した服や素体のメッシュを合体させ、1つのSkinnedMeshRendererにしてSkinnedMesh数を削減します。" : "Combines selected SkinnedMeshRenderers into a single one to reduce SkinnedMesh count.";
        public static string DuplicateAvatar => _jp ? "※ メッシュ操作はアバターを複製 (Duplicate) して実行することを推奨します" : "* Recommending avatar duplication for mesh operations.";
        public static string OptSafetyToggle => _jp ? "実行前にアバターを複製する (安全)" : "Safety Duplicate Before Operation";
        public static string OptSafetyDesc => _jp ? "チェックを外すと現在のアバターを直接加工します (Undo可能)" : "Modify current avatar directly if unchecked (Undoable)";
        public static string BtnDuplicate => _jp ? "アバターを複製する (Duplicate)" : "Duplicate Avatar First";
        public static string BtnRemoveShapes => _jp ? "未使用ShapeKeyを全削除して最適化" : "Remove All Unused ShapeKeys";
        public static string BtnCombine => _jp ? "選択したメッシュを結合 (Combine)" : "Combine Selected Meshes";
        public static string MeshGroupLabel => _jp ? "結合グループ名" : "Combination Group Name";
        public static string MeshBoneOpt => _jp ? "🦴 ボーン軽量化 (ボーン削除)" : "🦴 Bone Stripping (Remove Bones)";
        public static string MeshBoneDesc => _jp ? "ウェイトが乗っていない、またはアニメーション等で使用されていない不要なボーンをヒエラルキーから削除します。" : "Removes bones from the hierarchy that have no weights and are not used by animations.";
        public static string MeshBoneManual => _jp ? "🔍 ボーン選択削除 (手動)" : "🔍 Manual Bone Deletion";
        public static string MeshBoneManualDesc => _jp ? "リストから選択したボーンをヒエラルキーから削除します。削除ミスに注意してください。" : "Deletes selected bones from the hierarchy. Use with caution.";
        public static string BtnRemoveBones => _jp ? "ボーンを削除して最適化" : "Strip Bones";
        public static string BtnRemoveManualBones => _jp ? "選択したボーンを削除" : "Delete Selected Bones";
        public static string BtnRemoveManualComponents => _jp ? "選択したコンポーネントのみ削除" : "Delete Selected Components Only";
        public static string BoneDone(int n) => _jp ? $"{n} 個のボーンを削除しました" : $"Stripped {n} bones";
        public static string ComponentDone(int n) => _jp ? $"{n} 個のコンポーネントを削除しました" : $"Removed {n} components";
        public static string MeshCombineDone(int n) => _jp ? $"新しい頂点数: {n}" : $"New Vertex Count: {n}";
        public static string BlendShapeDone(int n) => _jp ? $"合計 {n} 個の未使用シェイプキーを削除しました" : $"Stripped {n} unused BlendShapes in total";

        // Atlas Tab
        public static string AtlasTitle => _jp ? "🎨 テクスチャアトラス化 (lilToon)" : "🎨 Texture Atlassing (lilToon)";
        public static string AtlasDesc => _jp ? "複数のlilToonマテリアルを1つのアトラス画像とマテリアルに統合し、DrawCallを大幅に削減します。" : "Combines multiple lilToon materials into a single atlas texture and material to drastically reduce DrawCalls.";
        public static string BtnAtlasRun => _jp ? "アトラス化を実行 (Duplicate & Atlas)" : "Run Atlassing (Duplicate & Atlas)";
        public static string AtlasDone(int n) => _jp ? $"{n} 個のマテリアルをアトラス化しました" : $"Atlassed {n} materials successfully";

        // Bone Categories
        public static string CategoryPhysBones => "PhysBones";
        public static string CategoryColliders => _jp ? "PhysBone コライダー" : "PhysBone Colliders";
        public static string CategoryContacts => _jp ? "コンタクト (Sender/Receiver)" : "Contacts (Sender/Receiver)";
        public static string CategoryConstraints => _jp ? "コンストレイント" : "Constraints";

        public static string MAManagedNote(int n) => _jp
            ? $"  うち {n} 件はMA管理オブジェクト（Bake前は正常）"
            : $"  {n} are MA-managed objects (normal before baking)";

        // Quest Shader Conversion - REMOVED

        // Export Tab
        public static string TabExport => _jp ? "書き出し" : "Export";

        // Animation Path Fixer
        public static string PathFixerTitle => _jp ? "🔧 アニメーションパス自動修正" : "🔧 Animation Path Auto-Fix";
        public static string PathFixerDesc => _jp ? "メッシュ統合やリネーム後に動かなくなった表情・アニメーションのパスを自動で検出し修正します。" : "Detects and fixes broken animation paths caused by mesh combining or renaming.";
        public static string PathFixerScan => _jp ? "壊れたパスを検出" : "Scan for Broken Paths";
        public static string PathFixerNone => _jp ? "✓ 壊れたパスは見つかりませんでした" : "✓ No broken paths found";
        public static string PathFixerFound(int n) => _jp ? $"{n} 件の壊れたパスを検出" : $"{n} broken bindings found";
        public static string PathFixerFix(int n) => _jp ? $"選択した {n} 件を修正" : $"Fix {n} Selected";
        public static string PathFixerDone(int n) => _jp ? $"{n} 件のパスを修正しました" : $"Fixed {n} bindings";
        public static string PathFixerClipLabel => _jp ? "クリップ" : "Clip";
        public static string PathFixerNoFix => _jp ? "修正候補なし（手動で確認してください）" : "No fix found (check manually)";

        // MA Bake Export
        public static string MABakeTitle => _jp ? "🧩 Modular Avatar 書き出し (MA Bake)" : "🧩 Modular Avatar Bake Export";
        public static string MABakeDesc => _jp ? "MAで着せ替えた状態を1つのアバターに焼き固めて書き出します。書き出し後はEzpzOptimizerで最適化することをおすすめします。" : "Bakes all Modular Avatar components into a single avatar for upload. Run EzpzOptimizer optimizations after baking.";
        public static string MABakeBtn => _jp ? "▶ MA Bake を実行" : "▶ Run MA Bake";
        public static string MANotFound => _jp ? "Modular Avatar がプロジェクトに見つかりません\nVCC または UPM でインストールしてください" : "Modular Avatar not found in project.\nInstall it via VCC or UPM.";
        public static string MABakeDone => _jp ? "MAのBakeが完了しました！元のアバターは非表示になっています。" : "MA Bake complete! Original avatar has been hidden.";

        // Simplify Tab
        public static string TabSimplify      => _jp ? "減ポリ (Beta)" : "Simplify (Beta)";
        public static string SimplifyTitle     => _jp ? "🔺 ポリゴン削減 (Mesh Simplify) (Beta)" : "🔺 Polygon Reduction (Mesh Simplify) (Beta)";
        public static string SimplifyDesc      => _jp ? "衣装や素体のポリゴン数をスライダーで直感的に削減します。" : "Reduce polygon count intuitively via a slider.";
        public static string SimplifyDrop      => _jp ? "衣装や素体をここにD&Dしてください" : "Drag & Drop a GameObject here";
        public static string SimplifyQuality   => _jp ? "クオリティ" : "Quality";
        public static string SimplifyTris(int before, int after) => _jp ? $"ポリゴン数: {before:N0} → 約 {after:N0}" : $"Polygons: {before:N0} → ~{after:N0}";
        public static string SimplifyBtn       => _jp ? "▶ 削減を適用 (コピーに適用)" : "▶ Simplify (Apply to Copy)";
        public static string SimplifyDone(string name) => _jp ? $"{name} を生成しました" : $"Created {name}";

    }
}

