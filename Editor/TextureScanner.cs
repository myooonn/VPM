// Ezpzoptimizer - Texture Scanner
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public static class TextureScanner
    {
        /// <summary>Find all textures used by renderers under the given root GameObject.</summary>
        public static List<Texture2D> Scan(GameObject root)
        {
            var result = new HashSet<Texture2D>();
            if (root == null) return new List<Texture2D>();

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    CollectFromMaterial(mat, result);
                }
            }
            return new List<Texture2D>(result);
        }

        static void CollectFromMaterial(Material mat, HashSet<Texture2D> set)
        {
            var shader = mat.shader;
            int count = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;
                string name = ShaderUtil.GetPropertyName(shader, i);
                var tex = mat.GetTexture(name) as Texture2D;
                if (tex != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tex)))
                    set.Add(tex);
            }
        }

#if VRC_SDK_EXISTS
        /// <summary>Find VRC Avatar Descriptor root in scene.</summary>
        public static GameObject FindVRCAvatar()
        {
            var descriptors = Object.FindObjectsOfType<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            return descriptors.Length > 0 ? descriptors[0].gameObject : null;
        }
#else
        public static GameObject FindVRCAvatar() => null;
#endif
    }
}
