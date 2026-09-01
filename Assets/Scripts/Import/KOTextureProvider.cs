using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// KO texture yükleme soyutlama katmanı.
    /// 
    /// Öncelik sırası:
    ///   1. Memory cache (aynı texture tekrar yüklenmez)
    ///   2. Unity Resources (dönüştürülmüş PNG — Assets/Resources/KOTextures/)
    ///   3. DxtTextureImporter fallback (orijinal KO .DXT dosyası)
    ///
    /// NOT: Resources PNG'leri flipY=true ile kaydedilir.
    /// flipY=false gereken call site'lar için texture runtime'da çevrilir ve cache'lenir.
    /// </summary>
    public static class KOTextureProvider
    {
        // Memory cache — flipY varyantları ayrı key'lerle saklanır
        private static readonly Dictionary<string, Texture2D> s_cache = new Dictionary<string, Texture2D>();

        // Cache istatistikleri
        private static int s_cacheHits = 0;
        private static int s_resourceLoads = 0;
        private static int s_dxtFallbacks = 0;
        private static int s_flipCount = 0;

        /// <summary>
        /// KO texture yükle.
        /// 
        /// fullDxtPath: DXT dosyasının yolu (ör: "fx/30305safety.DXT")
        /// flipY: Y-flip (default: true — Unity convention)
        /// </summary>
        public static Texture2D Load(string fullDxtPath, bool flipY = true)
        {
            if (string.IsNullOrEmpty(fullDxtPath))
                return null;

            // Cache key: path + flipY varyantı
            string basePath = Path.GetFullPath(fullDxtPath).Replace('\\', '/').ToLowerInvariant();
            string cacheKey = flipY ? basePath : basePath + ":nf";

            // 1. Cache kontrolü
            if (s_cache.TryGetValue(cacheKey, out var cached))
            {
                if (cached != null) // Destroyed texture kontrolü
                {
                    s_cacheHits++;
                    return cached;
                }
                s_cache.Remove(cacheKey);
            }

            Texture2D result = null;

            // 2. Resources'tan yüklemeyi dene (dönüştürülmüş PNG, flipY=true ile kaydedilmiş)
            string resourcePath = GetResourcePath(fullDxtPath);
            if (resourcePath != null)
            {
                var resTex = Resources.Load<Texture2D>(resourcePath);
                if (resTex != null)
                {
                    s_resourceLoads++;

                    if (flipY)
                    {
                        // Converter flipY=true ile kaydettiği için doğrudan kullan
                        result = resTex;
                    }
                    else
                    {
                        // flipY=false gerekiyor — texture'ı çevir
                        result = FlipTextureY(resTex);
                        s_flipCount++;
                    }

                    s_cache[cacheKey] = result;
                    return result;
                }
            }

            // 3. DXT fallback — orijinal dosyadan runtime decode
            if (KOBinaryProvider.Exists(fullDxtPath))
            {
                result = DxtTextureImporter.Load(fullDxtPath, flipY);
            }

            if (result != null)
            {
                s_dxtFallbacks++;
                s_cache[cacheKey] = result;

                // Her 100 fallback'te bir uyarı
                if (s_dxtFallbacks % 100 == 0)
                {
                    Debug.LogWarning($"[KOTextureProvider] {s_dxtFallbacks} texture DXT fallback kullanıyor! " +
                                     "KO Tools → Convert All DXT Textures çalıştırın.");
                }
            }

            return result;
        }

        /// <summary>
        /// Texture'ı Y ekseninde çevir (flip vertically).
        /// Resources PNG'leri flipY=true ile kaydedildiğinden,
        /// flipY=false gerektiren call site'lar için bu dönüşüm yapılır.
        /// </summary>
        private static Texture2D FlipTextureY(Texture2D source)
        {
            int width = source.width;
            int height = source.height;

            // Kaynak piksellerini oku
            Color32[] srcPixels = source.GetPixels32();
            Color32[] dstPixels = new Color32[srcPixels.Length];

            // Satırları ters sırayla kopyala
            for (int y = 0; y < height; y++)
            {
                int srcRow = (height - 1 - y) * width;
                int dstRow = y * width;
                System.Array.Copy(srcPixels, srcRow, dstPixels, dstRow, width);
            }

            // Yeni texture oluştur
            var flipped = new Texture2D(width, height, TextureFormat.RGBA32, source.mipmapCount > 1);
            flipped.SetPixels32(dstPixels);
            flipped.filterMode = source.filterMode;
            flipped.wrapMode = source.wrapMode;
            flipped.Apply(true, false); // mipmaps oluştur, readable kalsın
            return flipped;
        }

        /// <summary>
        /// fullDxtPath'ten Resources yolunu türet.
        /// Dosya adından baseName çıkarıp KOTextures/ dizinlerinde arar.
        ///
        /// Örnek:
        ///   fullDxtPath: "fx/30305safety.DXT"
        ///   → "KOTextures/FX/30305safety" (uzantısız, Resources convention)
        /// </summary>
        private static string GetResourcePath(string fullDxtPath)
        {
            string normalized = fullDxtPath.Replace('\\', '/');
            string baseName = Path.GetFileNameWithoutExtension(normalized);
            if (string.IsNullOrEmpty(baseName)) return null;

            string[] searchDirs = { "FX", "UI", "UI_US", "DTex", "Chr", "Item", "Misc", "Object", "Zones" };

            // 1. Try with full relative path structure (e.g. misc/river/caust00 -> KOTextures/Misc/river/caust00)
            string dirName = Path.GetDirectoryName(normalized);
            string relPathWithoutExt = string.IsNullOrEmpty(dirName) ? baseName : Path.Combine(dirName, baseName).Replace('\\', '/');

            // Case-normalize the first directory segment to match standard folders (for mobile/case-sensitive platforms)
            string[] parts = relPathWithoutExt.Split('/');
            if (parts.Length > 1)
            {
                string firstDir = parts[0];
                foreach (var dir in searchDirs)
                {
                    if (string.Equals(firstDir, dir, System.StringComparison.OrdinalIgnoreCase))
                    {
                        parts[0] = dir;
                        break;
                    }
                }
                relPathWithoutExt = string.Join("/", parts);
            }

            string directPath = $"KOTextures/{relPathWithoutExt}";
            if (Resources.Load<Texture2D>(directPath) != null)
                return directPath;

            string lowerDirectPath = $"KOTextures/{relPathWithoutExt.ToLowerInvariant()}";
            if (Resources.Load<Texture2D>(lowerDirectPath) != null)
                return lowerDirectPath;

            // 2. Standart KOTextures alt dizinlerinde flat ara (fallback)
            foreach (var dir in searchDirs)
            {
                string resPath = $"KOTextures/{dir}/{baseName}";
                if (Resources.Load<Texture2D>(resPath) != null)
                    return resPath;

                // Case-insensitive / lowercase safety for mobile & Editor platforms
                string lowerResPath = $"KOTextures/{dir}/{baseName.ToLowerInvariant()}";
                if (Resources.Load<Texture2D>(lowerResPath) != null)
                    return lowerResPath;
            }

            // Doğrudan flat dene
            string directFlatPath = $"KOTextures/{baseName}";
            if (Resources.Load<Texture2D>(directFlatPath) != null)
                return directFlatPath;

            string lowerDirectFlatPath = $"KOTextures/{baseName.ToLowerInvariant()}";
            if (Resources.Load<Texture2D>(lowerDirectFlatPath) != null)
                return lowerDirectFlatPath;

            return null;
        }

        /// <summary>
        /// Texture cache'ini temizle.
        /// Zone değişimi veya bellek baskısında çağrılmalı.
        /// </summary>
        public static void ClearCache()
        {
            s_cache.Clear();
        }

        /// <summary>
        /// Debug: Mevcut cache durumunu logla.
        /// </summary>
        public static void LogStats()
        {
        }
    }
}
