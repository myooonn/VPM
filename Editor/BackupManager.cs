// Ezpzoptimizer - Backup System
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace EzPz
{
    [Serializable]
    public class TexBackupEntry
    {
        public string assetPath;
        public int maxSize;
        public int format;         // TextureImporterFormat as int
        public int compression;    // TextureImporterCompression as int
        public bool hasMips;
        public int androidFormatInt;
        public bool androidFormatIntSet;
        public string androidFormat;
        public int androidMaxSize;
        public bool androidOverride;
    }

    [Serializable]
    public class BackupData
    {
        public List<TexBackupEntry> entries = new List<TexBackupEntry>();
    }

    public static class BackupManager
    {
        static string BackupPath => Path.Combine(Application.dataPath, "..", "Library", "EzPzTexOptBackup.json");

        public static void Save(List<Texture2D> textures)
        {
            var data = new BackupData();
            foreach (var tex in textures)
            {
                if (tex == null) continue;
                string path = AssetDatabase.GetAssetPath(tex);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;

                var def = imp.GetDefaultPlatformTextureSettings();
                var entry = new TexBackupEntry
                {
                    assetPath = path,
                    maxSize = def.maxTextureSize,
                    format = (int)def.format,
                    compression = (int)imp.textureCompression,
                    hasMips = imp.mipmapEnabled,
                };

                var android = imp.GetPlatformTextureSettings("Android");
                if (android != null && android.overridden)
                {
                    entry.androidOverride = true;
                    entry.androidFormatInt = (int)android.format;
                    entry.androidFormatIntSet = true;
                    entry.androidFormat = android.format.ToString(); // legacy compatibility
                    entry.androidMaxSize = android.maxTextureSize;
                }
                data.entries.Add(entry);
            }
            File.WriteAllText(BackupPath, JsonUtility.ToJson(data, true));
        }

        public static bool Restore()
        {
            if (!File.Exists(BackupPath)) return false;

            BackupData data = null;
            try
            {
                data = JsonUtility.FromJson<BackupData>(File.ReadAllText(BackupPath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Ezpzoptimizer] Failed to read backup: {e.Message}");
                return false;
            }

            if (data == null || data.entries == null || data.entries.Count == 0) return false;

            int count = data.entries.Count;
            for (int i = 0; i < count; i++)
            {
                var e = data.entries[i];
                var imp = AssetImporter.GetAtPath(e.assetPath) as TextureImporter;
                if (imp == null) continue;

                EditorUtility.DisplayProgressBar(L.IsJP ? "復元中..." : "Restoring...", e.assetPath, (float)i / count);

                imp.textureCompression = (TextureImporterCompression)e.compression;
                imp.mipmapEnabled = e.hasMips;

                var def = imp.GetDefaultPlatformTextureSettings();
                def.maxTextureSize = e.maxSize;
                def.format = (TextureImporterFormat)e.format;
                imp.SetPlatformTextureSettings(def);

                if (e.androidOverride)
                {
                    var android = imp.GetPlatformTextureSettings("Android");
                    android.overridden = true;
                    android.maxTextureSize = e.androidMaxSize;
                    if (e.androidFormatIntSet)
                    {
                        android.format = (TextureImporterFormat)e.androidFormatInt;
                    }
                    else if (!string.IsNullOrEmpty(e.androidFormat)
                        && Enum.TryParse<TextureImporterFormat>(e.androidFormat, out var fmt))
                    {
                        android.format = fmt;
                    }
                    imp.SetPlatformTextureSettings(android);
                }
                else
                {
                    imp.ClearPlatformTextureSettings("Android");
                }
                imp.SaveAndReimport();
            }
            EditorUtility.ClearProgressBar();
            return true;
        }

        public static bool HasBackup() => File.Exists(BackupPath);
    }
}
