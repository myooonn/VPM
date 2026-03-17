// Ezpzoptimizer - VRAM Calculator
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public static class VRAMCalc
    {
        /// <summary>Estimate VRAM in bytes for a texture at given size and format.</summary>
        public static long Estimate(int width, int height, TextureImporterFormat fmt, bool hasMips = true)
        {
            float bpp = GetBPP(fmt);
            long raw = (long)(width * height * bpp / 8f);
            return hasMips ? (long)(raw * 1.333f) : raw;
        }

        /// <summary>Estimate VRAM from current texture asset.</summary>
        public static long EstimateFromTexture(Texture2D tex)
        {
            if (tex == null) return 0;
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return 0;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return 0;
            var settings = imp.GetDefaultPlatformTextureSettings();
            int maxSize = settings.maxTextureSize;
            int w = Mathf.Min(tex.width, maxSize);
            int h = Mathf.Min(tex.height, maxSize);
            return Estimate(w, h, settings.format, imp.mipmapEnabled);
        }

        /// <summary>Estimate VRAM after optimization to target size.</summary>
        public static long EstimateAfter(Texture2D tex, int targetSize, TextureImporterFormat fmt, bool hasMips = true)
        {
            if (tex == null) return 0;
            int w = Mathf.Min(tex.width, targetSize);
            int h = Mathf.Min(tex.height, targetSize);
            return Estimate(w, h, fmt, hasMips);
        }

        static float GetBPP(TextureImporterFormat fmt)
        {
            switch (fmt)
            {
                case TextureImporterFormat.DXT1:
                case TextureImporterFormat.DXT1Crunched:
                case TextureImporterFormat.ETC_RGB4:
                case TextureImporterFormat.ETC2_RGB4:
                    return 4f;
                case TextureImporterFormat.DXT5:
                case TextureImporterFormat.DXT5Crunched:
                case TextureImporterFormat.ETC2_RGBA8:
                case TextureImporterFormat.BC7:
                case TextureImporterFormat.ASTC_6x6:
                    return 8f;
                case TextureImporterFormat.ASTC_4x4:
                    return 8f;
                case TextureImporterFormat.ASTC_8x8:
                    return 2f;
                case TextureImporterFormat.ASTC_10x10:
                    return 1.28f;
                case TextureImporterFormat.ASTC_12x12:
                    return 0.89f;
                case TextureImporterFormat.RGBA32:
                    return 32f;
                case TextureImporterFormat.RGB24:
                    return 24f;
                case TextureImporterFormat.RGBA16:
                    return 16f;
                default:
                    return 8f;
            }
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024f:F1} KB";
            return $"{bytes / 1048576f:F2} MB";
        }
    }
}
