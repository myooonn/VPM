// Ezpzoptimizer - Texture Processor
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    public static class TextureProcessor
    {
        public static readonly int[] SizeOptions = { 128, 256, 512, 1024, 2048 };

        public enum CompressionPreset
        {
            Auto,     // Unity automatic
            Normal,   // DXT5 / ASTC 6x6
            HighQ,    // BC7 / ASTC 4x4
            LowQ,     // DXT1 / ASTC 8x8
        }

        public static void Optimize(List<Texture2D> textures, bool[] selected, int targetSize, CompressionPreset preset, bool questOpt)
        {
            int count = 0;
            for (int i = 0; i < textures.Count; i++)
                if (selected[i]) count++;

            if (count == 0) return;

            int done = 0;
            for (int i = 0; i < textures.Count; i++)
            {
                if (!selected[i]) continue;
                var tex = textures[i];
                if (tex == null) continue;

                string path = AssetDatabase.GetAssetPath(tex);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;

                EditorUtility.DisplayProgressBar(L.IsJP ? "処理中..." : "Processing...", $"({done + 1}/{count}) {tex.name}", (float)done / count);

                // PC (Standalone) settings
                var pc = imp.GetDefaultPlatformTextureSettings();
                pc.maxTextureSize = targetSize;
                if (preset != CompressionPreset.Auto)
                    pc.format = GetPCFormat(preset, imp.DoesSourceTextureHaveAlpha());
                imp.SetPlatformTextureSettings(pc);

                imp.textureCompression = preset == CompressionPreset.LowQ
                    ? TextureImporterCompression.CompressedLQ
                    : TextureImporterCompression.Compressed;

                // Quest / Android
                if (questOpt)
                {
                    var android = imp.GetPlatformTextureSettings("Android");
                    android.overridden = true;
                    android.maxTextureSize = Mathf.Min(targetSize, 512); // Quest cap
                    android.format = GetAndroidFormat(preset, imp.DoesSourceTextureHaveAlpha());
                    imp.SetPlatformTextureSettings(android);
                }

                imp.SaveAndReimport();
                done++;
            }
            EditorUtility.ClearProgressBar();
        }

        static TextureImporterFormat GetPCFormat(CompressionPreset preset, bool hasAlpha)
        {
            switch (preset)
            {
                case CompressionPreset.HighQ:
                    return TextureImporterFormat.BC7;
                case CompressionPreset.LowQ:
                    return hasAlpha ? TextureImporterFormat.DXT5Crunched : TextureImporterFormat.DXT1Crunched;
                default: // Normal
                    return hasAlpha ? TextureImporterFormat.DXT5 : TextureImporterFormat.DXT1;
            }
        }

        static TextureImporterFormat GetAndroidFormat(CompressionPreset preset, bool hasAlpha)
        {
            switch (preset)
            {
                case CompressionPreset.HighQ:
                    return TextureImporterFormat.ASTC_4x4;
                case CompressionPreset.LowQ:
                    return TextureImporterFormat.ASTC_8x8;
                default:
                    return TextureImporterFormat.ASTC_6x6;
            }
        }

        public static string PresetLabel(CompressionPreset p)
        {
            switch (p)
            {
                case CompressionPreset.Auto: return L.IsJP ? "自動" : "Auto";
                case CompressionPreset.Normal: return L.IsJP ? "標準 (DXT5/ASTC6)" : "Normal (DXT5/ASTC6)";
                case CompressionPreset.HighQ: return L.IsJP ? "高品質 (BC7/ASTC4)" : "High Quality (BC7/ASTC4)";
                case CompressionPreset.LowQ: return L.IsJP ? "軽量 (Crunched/ASTC8)" : "Lightweight (Crunched/ASTC8)";
                default: return p.ToString();
            }
        }
    }
}
