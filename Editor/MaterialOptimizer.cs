// Ezpzoptimizer - Material Optimizer
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public static class MaterialOptimizer
    {
        /// <summary>Result of a material scan.</summary>
        public class ScanResult
        {
            public List<Material> allMaterials = new List<Material>();
            public List<Material> unusedPropMats = new List<Material>();  // materials with unused tex properties
            public List<Material> duplicateMats = new List<Material>();   // materials that could be merged
            public int totalRenderers;
        }

        /// <summary>Scan all materials on avatar and find optimization opportunities.</summary>
        public static ScanResult Scan(GameObject root)
        {
            var result = new ScanResult();
            if (root == null) return result;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            result.totalRenderers = renderers.Length;
            var matSet = new HashSet<Material>();

            foreach (var r in renderers)
                foreach (var m in r.sharedMaterials)
                    if (m != null) matSet.Add(m);

            result.allMaterials = matSet.ToList();

            // Check for unused texture properties
            foreach (var mat in result.allMaterials)
            {
                if (HasUnusedTexProperties(mat))
                    result.unusedPropMats.Add(mat);
            }

            // Find duplicate materials (same shader + same texture set)
            FindDuplicates(result);

            return result;
        }

        /// <summary>Remove unused texture properties from materials (set null textures to actually null).</summary>
        public static int CleanUnusedProperties(List<Material> materials)
        {
            int cleaned = 0;
            foreach (var mat in materials)
            {
                if (mat == null) continue;
                var shader = mat.shader;
                int propCount = ShaderUtil.GetPropertyCount(shader);
                var so = new SerializedObject(mat);
                var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
                if (texEnvs == null) continue;

                bool changed = false;
                // Remove serialized texture entries that aren't actual shader properties
                for (int i = texEnvs.arraySize - 1; i >= 0; i--)
                {
                    var entry = texEnvs.GetArrayElementAtIndex(i);
                    string propName = entry.FindPropertyRelative("first").stringValue;

                    bool isShaderProp = false;
                    for (int j = 0; j < propCount; j++)
                    {
                        if (ShaderUtil.GetPropertyName(shader, j) == propName)
                        {
                            isShaderProp = true;
                            break;
                        }
                    }
                    if (!isShaderProp)
                    {
                        texEnvs.DeleteArrayElementAtIndex(i);
                        changed = true;
                    }
                }

                // Also clean up unused float and color properties
                CleanPropertyArray(so, "m_SavedProperties.m_Floats", shader, propCount, ref changed);
                CleanPropertyArray(so, "m_SavedProperties.m_Colors", shader, propCount, ref changed);

                if (changed)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(mat);
                    cleaned++;
                }
            }
            if (cleaned > 0) AssetDatabase.SaveAssets();
            return cleaned;
        }

        static void CleanPropertyArray(SerializedObject so, string path, Shader shader, int propCount, ref bool changed)
        {
            var arr = so.FindProperty(path);
            if (arr == null) return;
            for (int i = arr.arraySize - 1; i >= 0; i--)
            {
                var entry = arr.GetArrayElementAtIndex(i);
                string propName = entry.FindPropertyRelative("first").stringValue;
                bool isShaderProp = false;
                for (int j = 0; j < propCount; j++)
                {
                    if (ShaderUtil.GetPropertyName(shader, j) == propName)
                    {
                        isShaderProp = true;
                        break;
                    }
                }
                if (!isShaderProp)
                {
                    arr.DeleteArrayElementAtIndex(i);
                    changed = true;
                }
            }
        }

        static bool HasUnusedTexProperties(Material mat)
        {
            var shader = mat.shader;
            int propCount = ShaderUtil.GetPropertyCount(shader);
            var so = new SerializedObject(mat);
            var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (texEnvs == null) return false;

            for (int i = 0; i < texEnvs.arraySize; i++)
            {
                string propName = texEnvs.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                bool found = false;
                for (int j = 0; j < propCount; j++)
                {
                    if (ShaderUtil.GetPropertyName(shader, j) == propName) { found = true; break; }
                }
                if (!found) return true;
            }
            return false;
        }

        static void FindDuplicates(ScanResult result)
        {
            // Group by shader name + texture asset paths
            var groups = new Dictionary<string, List<Material>>();
            foreach (var mat in result.allMaterials)
            {
                string key = GetMaterialKey(mat);
                if (!groups.ContainsKey(key)) groups[key] = new List<Material>();
                groups[key].Add(mat);
            }
            foreach (var g in groups.Values)
            {
                if (g.Count > 1)
                    result.duplicateMats.AddRange(g);
            }
        }

        static string GetMaterialKey(Material mat)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(mat.shader.name).Append("|");
            int propCount = ShaderUtil.GetPropertyCount(mat.shader);
            for (int i = 0; i < propCount; i++)
            {
                if (ShaderUtil.GetPropertyType(mat.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;
                string name = ShaderUtil.GetPropertyName(mat.shader, i);
                var tex = mat.GetTexture(name);
                sb.Append(name).Append("=");
                sb.Append(tex != null ? AssetDatabase.GetAssetPath(tex) : "null").Append(";");
            }
            return sb.ToString();
        }

        /// <summary>Merge duplicate materials on all renderers to use a single master material.</summary>
        public static int MergeDuplicates(GameObject root, List<Material> duplicateMats)
        {
            if (root == null || duplicateMats.Count == 0) return 0;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            
            // Re-group duplicates to find the "master" for each group
            var groups = new Dictionary<string, List<Material>>();
            foreach (var mat in duplicateMats)
            {
                if (mat == null) continue;
                string key = GetMaterialKey(mat);
                if (!groups.ContainsKey(key)) groups[key] = new List<Material>();
                groups[key].Add(mat);
            }

            int changedCount = 0;

            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null) continue;

                    string key = GetMaterialKey(mat);
                    if (groups.TryGetValue(key, out var group) && group.Count > 1)
                    {
                        // Use the first material in the group as the master
                        var master = group[0];
                        if (mat != master)
                        {
                            mats[i] = master;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    Undo.RecordObject(r, "Merge Materials");
                    r.sharedMaterials = mats;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                    changedCount++;
                }
            }

            return changedCount;
        }
    }
}
