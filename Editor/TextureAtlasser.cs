using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace EzPz
{
    public static class TextureAtlasser
    {
        [System.Serializable]
        public class AtlasGroup
        {
            public string shaderName;
            public List<Material> materials = new List<Material>();
        }

        private static readonly string[] TextureProperties = {
            "_MainTex",
            "_OutlineTex",
            "_ShadowMask",
            "_Shadow1stTex",
            "_Shadow2ndTex",
            "_Shadow3rdTex",
            "_MatCapTex",
            "_RimLightTex",
            "_EmissiveTex",
            "_NormalMap"
        };

        public static int AtlasAvatar(GameObject avatar, List<Material> selectedMaterials)
        {
            if (avatar == null || selectedMaterials == null || selectedMaterials.Count < 2) return 0;

            var group = new AtlasGroup { shaderName = selectedMaterials[0].shader.name, materials = selectedMaterials };
            if (ProcessGroup(avatar, group))
            {
                return selectedMaterials.Count;
            }

            return 0;
        }

        private static bool ProcessGroup(GameObject avatar, AtlasGroup group)
        {
            string folder = "Assets/Ezpzoptimizer/Generated/Atlas";
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            // 1. Pack Textures
            var mainTexs = new List<Texture2D>();
            foreach (var mat in group.materials)
            {
                var tex = GetTextureOrWhite(mat, "_MainTex");
                MakeTextureReadable(tex);
                mainTexs.Add(tex);
            }

            var atlasBase = new Texture2D(2048, 2048);
            Rect[] uvRects = atlasBase.PackTextures(mainTexs.ToArray(), 8, 2048);

            // 2. Create Atlases
            var atlasMaterials = new Dictionary<string, Texture2D>();
            foreach (var prop in TextureProperties)
            {
                var atlas = CreateAtlasForProperty(group.materials, prop, uvRects);
                if (atlas != null)
                {
                    string texPath = $"{folder}/{avatar.name}_Atlas_{prop}_{group.materials[0].name}.png";
                    byte[] bytes = atlas.EncodeToPNG();
                    File.WriteAllBytes(texPath, bytes);
                    AssetDatabase.ImportAsset(texPath);
                    atlasMaterials[prop] = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                }
            }

            // 3. Create Atlas Material
            var atlasMat = new Material(group.materials[0].shader);
            atlasMat.CopyPropertiesFromMaterial(group.materials[0]);
            foreach (var kv in atlasMaterials)
            {
                atlasMat.SetTexture(kv.Key, kv.Value);
            }
            string matPath = $"{folder}/{avatar.name}_Atlas_{group.materials[0].name}.mat";
            AssetDatabase.CreateAsset(atlasMat, matPath);

            // 4. Update UVs and Swap Materials
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                
                Mesh mesh = null;
                SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
                if (smr != null) mesh = smr.sharedMesh;
                else mesh = r.GetComponent<MeshFilter>()?.sharedMesh;

                if (mesh == null) continue;

                Mesh meshCopy = Object.Instantiate(mesh);
                Vector2[] uvs = meshCopy.uv;
                bool meshModified = false;

                // Track which vertices have been remapped to avoid double-processing
                HashSet<int> remappedVertices = new HashSet<int>();

                for (int i = 0; i < mats.Length; i++)
                {
                    int matIdx = group.materials.IndexOf(mats[i]);
                    if (matIdx >= 0)
                    {
                        var indices = meshCopy.GetIndices(i);
                        Rect rect = uvRects[matIdx];
                        foreach (var vIdx in indices)
                        {
                            if (!remappedVertices.Contains(vIdx))
                            {
                                Vector2 uv = uvs[vIdx];
                                uv.x = rect.x + uv.x * rect.width;
                                uv.y = rect.y + uv.y * rect.height;
                                uvs[vIdx] = uv;
                                remappedVertices.Add(vIdx);
                            }
                        }
                        mats[i] = atlasMat;
                        changed = true;
                        meshModified = true;
                    }
                }

                if (changed)
                {
                    if (meshModified)
                    {
                        meshCopy.uv = uvs;
                        string meshPath = $"{folder}/{avatar.name}_Mesh_{mesh.name}.asset";
                        AssetDatabase.CreateAsset(meshCopy, meshPath);
                        
                        Undo.RecordObject(r, "Swap Atlas Mesh");
                        if (smr != null)
                        {
                            smr.sharedMesh = meshCopy;
                            // [Fix] Update VRChat Descriptor if this mesh was used for Eyelids or Visemes
#if VRC_SDK_EXISTS
                            var descriptor = avatar.GetComponentInParent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                            if (descriptor != null)
                            {
                                if (descriptor.VisemeSkinnedMesh == smr) descriptor.VisemeSkinnedMesh = smr;
                                if (descriptor.customEyeLookSettings.eyelidsSkinnedMesh == smr) descriptor.customEyeLookSettings.eyelidsSkinnedMesh = smr;
                            }
#endif
                        }
                        else r.GetComponent<MeshFilter>().sharedMesh = meshCopy;
                    }
                    Undo.RecordObject(r, "Swap Atlas Materials");
                    r.sharedMaterials = mats;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        private static Texture2D CreateAtlasForProperty(List<Material> materials, string propName, Rect[] rects)
        {
            // Check if any material has this texture
            bool hasTexture = materials.Any(m => m.GetTexture(propName) != null);
            if (!hasTexture) return null;

            var texs = new List<Texture2D>();
            foreach (var m in materials)
            {
                var tex = GetTextureOrWhite(m, propName); // Default to white or appropriate color
                if (propName == "_NormalMap" && m.GetTexture(propName) == null) 
                {
                    // Special case for missing normal: use 128,128,255
                    tex = new Texture2D(2, 2);
                    for (int x=0; x<2; x++) for (int y=0; y<2; y++) tex.SetPixel(x,y, new Color(0.5f, 0.5f, 1.0f));
                    tex.Apply();
                }
                MakeTextureReadable(tex);
                texs.Add(tex);
            }

            var atlas = new Texture2D(2048, 2048);
            // Initialize with neutral grey to prevent transparent bleed lines on mipmaps
            Color[] atlasPixels = Enumerable.Repeat(new Color(0.5f, 0.5f, 0.5f, 1f), 2048 * 2048).ToArray();
            for (int i = 0; i < materials.Count; i++)
            {
                var tex = texs[i];
                var rect = rects[i];
                int startX = Mathf.RoundToInt(rect.x * 2048);
                int startY = Mathf.RoundToInt(rect.y * 2048);
                int width = Mathf.RoundToInt(rect.width * 2048);
                int height = Mathf.RoundToInt(rect.height * 2048);

                // Simple blit (could be optimized)
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color c = tex.GetPixelBilinear((float)x / width, (float)y / height);
                        atlasPixels[(startY + y) * 2048 + (startX + x)] = c;
                    }
                }
            }
            atlas.SetPixels(atlasPixels);
            atlas.Apply();
            return atlas;
        }

        private static Texture2D GetTextureOrWhite(Material mat, string prop)
        {
            var tex = mat.GetTexture(prop) as Texture2D;
            if (tex != null) return tex;
            
            // Dummy texture if null
            var dummy = new Texture2D(2, 2);
            Color c = Color.white;
            if (prop == "_ShadowMask") c = Color.white;
            else if (prop == "_NormalMap") c = new Color(0.5f, 0.5f, 1.0f);
            
            for (int i=0; i<4; i++) dummy.SetPixel(i%2, i/2, c);
            dummy.Apply();
            return dummy;
        }

        private static void RemapSubMeshUVs(Mesh mesh, int subMeshIndex, Vector2[] uvs, Rect rect)
        {
            var indices = mesh.GetIndices(subMeshIndex);
            var processedTargetIndices = new HashSet<int>();
            foreach (var idx in indices) if (idx < uvs.Length) processedTargetIndices.Add(idx);

            foreach (var vIdx in processedTargetIndices)
            {
                Vector2 uv = uvs[vIdx];
                uv.x = rect.x + uv.x * rect.width;
                uv.y = rect.y + uv.y * rect.height;
                uvs[vIdx] = uv;
            }
        }

        private static void MakeTextureReadable(Texture2D tex)
        {
            if (tex == null) return;
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (!importer.isReadable || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
    }
}
