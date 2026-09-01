using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Binary dosyalarını Resources/KOBinary/ altından yükleyen yardımcı sınıf.
    /// Tüm parser'lar bu sınıf üzerinden dosya açar.
    ///
    /// Dosya isimlendirme: baseName_extension.bytes
    /// Örnek: upc_el_rm_rog.n3joint → upc_el_rm_rog_n3joint.bytes
    /// </summary>
    public static class KOBinaryProvider
    {
        /// <summary>
        /// Binary dosya için BinaryReader oluştur.
        /// Resources/KOBinary/{subDir}/{baseName}_{ext}.bytes formatında arar.
        /// Dönen stream'i caller dispose etmelidir.
        /// </summary>
        public static BinaryReader OpenReader(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            // Dosya adından Resources yolunu çıkar
            // upc_el_rm_rog.n3joint → baseName=upc_el_rm_rog, ext=n3joint
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string ext = Path.GetExtension(filePath).TrimStart('.');
            string resourceName = string.IsNullOrEmpty(ext) ? baseName : baseName + "_" + ext;
            string subDir = GuessSubDir(filePath);

            // 1. Tahmin edilen subDir'den dene
            if (!string.IsNullOrEmpty(subDir))
            {
                var reader = TryLoad(subDir, resourceName);
                if (reader != null) return reader;
            }

            // 2. Tüm subdirectory'lerde dene
            string[] allDirs = { "Chr", "Item", "ChrSelect", "Zones", "Misc", "Object", "FX" };
            foreach (var dir in allDirs)
            {
                if (dir == subDir) continue;
                var reader = TryLoad(dir, resourceName);
                if (reader != null) return reader;
            }

            // Bulunamadı
            return null;
        }

        /// <summary>
        /// Binary dosyanın Resources/KOBinary/ altında var olup olmadığını kontrol et.
        /// FindAssetFile yerine kullanılır.
        /// </summary>
        public static bool Exists(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;

            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string ext = Path.GetExtension(filePath).TrimStart('.');
            string resourceName = string.IsNullOrEmpty(ext) ? baseName : baseName + "_" + ext;
            string subDir = GuessSubDir(filePath);

            if (!string.IsNullOrEmpty(subDir))
            {
                var ta = Resources.Load<TextAsset>($"KOBinary/{subDir}/{resourceName}");
                if (ta != null) { Resources.UnloadAsset(ta); return true; }
            }

            string[] allDirs = { "Chr", "Item", "ChrSelect", "Zones", "Misc", "Object", "FX" };
            foreach (var dir in allDirs)
            {
                if (dir == subDir) continue;
                var ta = Resources.Load<TextAsset>($"KOBinary/{dir}/{resourceName}");
                if (ta != null) { Resources.UnloadAsset(ta); return true; }
            }

            return false;
        }

        /// <summary>
        /// Binary dosyayı byte[] olarak yükle (BinaryReader overhead olmadan).
        /// </summary>
        public static byte[] LoadBytes(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string ext = Path.GetExtension(filePath).TrimStart('.');
            string resourceName = string.IsNullOrEmpty(ext) ? baseName : baseName + "_" + ext;
            string subDir = GuessSubDir(filePath);

            if (!string.IsNullOrEmpty(subDir))
            {
                var ta = Resources.Load<TextAsset>($"KOBinary/{subDir}/{resourceName}");
                if (ta != null) { byte[] d = ta.bytes; Resources.UnloadAsset(ta); return d; }
            }

            string[] allDirs = { "Chr", "Item", "ChrSelect", "Zones", "Misc", "Object", "FX" };
            foreach (var dir in allDirs)
            {
                if (dir == subDir) continue;
                var ta = Resources.Load<TextAsset>($"KOBinary/{dir}/{resourceName}");
                if (ta != null) { byte[] d = ta.bytes; Resources.UnloadAsset(ta); return d; }
            }

            return null;
        }

        private static BinaryReader TryLoad(string subDir, string resourceName)
        {
            var textAsset = Resources.Load<TextAsset>($"KOBinary/{subDir}/{resourceName}");
            if (textAsset != null)
            {
                var ms = new MemoryStream(textAsset.bytes);
                Resources.UnloadAsset(textAsset);
                return new BinaryReader(ms);
            }
            return null;
        }

        /// <summary>
        /// Dosya yolundan alt klasörü tahmin et.
        /// </summary>
        private static string GuessSubDir(string filePath)
        {
            string normalized = filePath.Replace('\\', '/').ToLowerInvariant();

            if (normalized.Contains("/chr/") || normalized.StartsWith("chr/"))
                return "Chr";
            if (normalized.Contains("/item/") || normalized.StartsWith("item/") || normalized.StartsWith("item\\"))
                return "Item";
            if (normalized.Contains("/chrselect/") || normalized.StartsWith("chrselect/") || normalized.StartsWith("chrselect\\"))
                return "ChrSelect";
            if (normalized.Contains("/zones/") || normalized.StartsWith("zones/"))
                return "Zones";
            if (normalized.Contains("/misc/") || normalized.StartsWith("misc/"))
                return "Misc";
            if (normalized.Contains("/object/") || normalized.StartsWith("object/"))
                return "Object";
            if (normalized.Contains("/fx/") || normalized.StartsWith("fx/"))
                return "FX";

            return null;
        }
    }
}
