using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.UI
{
    /// <summary>
    /// C++ GameBase::MakeResrcFileNameForUPC() satır 617-621 birebir portu.
    /// ItemID (dwIDIcon) → ikon dosya adı → Resources/KOTextures/ → Texture2D → Sprite
    /// </summary>
    public static class KOItemIconLoader
    {
        private static readonly Dictionary<int, Sprite> _cache = new Dictionary<int, Sprite>();

        public static string GetIconFileName(int dwIDIcon)
        {
            if (dwIDIcon <= 0) return null;

            // Redirect all Totamic Spear icons (normal & rebirth, all upgrade levels) to 15121000 (itemicon_1_5121_00_0)
            if ((dwIDIcon >= 15121000 && dwIDIcon <= 15121999) || 
                (dwIDIcon >= 15122000 && dwIDIcon <= 15122999))
            {
                dwIDIcon = 15121000;
            }

            // Redirect all Sherion icons (normal & rebirth, all upgrade levels) to 11930100 (itemicon_1_1930_10_0)
            if ((dwIDIcon >= 11930100 && dwIDIcon <= 11930199) || 
                (dwIDIcon >= 11931100 && dwIDIcon <= 11931199))
            {
                dwIDIcon = 11930100;
            }

            // Redirect all Dark Vane icons (normal & rebirth, all upgrade levels) to 11910000 (itemicon_1_1910_00_0)
            if ((dwIDIcon >= 11910000 && dwIDIcon <= 11910199) || 
                (dwIDIcon >= 11911100 && dwIDIcon <= 11911199))
            {
                dwIDIcon = 11910000;
            }

            // Redirect all Cold-Hearted Dagger icons (normal & rebirth, all upgrade levels) to 11910200 (itemicon_1_1910_20_0)
            if ((dwIDIcon >= 11910200 && dwIDIcon <= 11910299) || 
                (dwIDIcon >= 11911200 && dwIDIcon <= 11911299))
            {
                dwIDIcon = 11910200;
            }

            // Redirect all Syphioric icons (normal & rebirth, all upgrade levels) to 17930100 (itemicon_1_7930_10_0)
            if ((dwIDIcon >= 17930100 && dwIDIcon <= 17930199) || 
                (dwIDIcon >= 17931100 && dwIDIcon <= 17931199))
            {
                dwIDIcon = 17930100;
            }

            // Redirect all Holy Animor icons (normal & rebirth, all upgrade levels) to 19930100 (itemicon_1_9930_10_0)
            if ((dwIDIcon >= 19930100 && dwIDIcon <= 19930199) || 
                (dwIDIcon >= 19931100 && dwIDIcon <= 19931199))
            {
                dwIDIcon = 19930100;
            }

            // Fine Yard -> Dagger ikonu (11011000)
            if ((dwIDIcon >= 11021000 && dwIDIcon <= 11021999) ||
                (dwIDIcon >= 11022000 && dwIDIcon <= 11022999))
            {
                dwIDIcon = 11011000;
            }

            // Dirk -> Dagger ikonu (11011000)
            if ((dwIDIcon >= 11041000 && dwIDIcon <= 11041999) ||
                (dwIDIcon >= 11042000 && dwIDIcon <= 11042999))
            {
                dwIDIcon = 11011000;
            }

            // Mail Breaker -> Stiletto ikonu (11051000)
            if ((dwIDIcon >= 11061000 && dwIDIcon <= 11061999) ||
                (dwIDIcon >= 11062000 && dwIDIcon <= 11062999))
            {
                dwIDIcon = 11051000;
            }

            // Wooden Shield -> Small Shield ikonu (17011000)
            if ((dwIDIcon >= 17015000 && dwIDIcon <= 17015999) ||
                (dwIDIcon >= 17016000 && dwIDIcon <= 17016999))
            {
                dwIDIcon = 17011000;
            }

            // Round Shield -> Large Shield ikonu (17021000)
            if ((dwIDIcon >= 17025000 && dwIDIcon <= 17025999) ||
                (dwIDIcon >= 17026000 && dwIDIcon <= 17026999))
            {
                dwIDIcon = 17021000;
            }

            // Octagon Shield -> Kite Shield ikonu (17031000)
            if ((dwIDIcon >= 17041000 && dwIDIcon <= 17041999) ||
                (dwIDIcon >= 17042000 && dwIDIcon <= 17042999))
            {
                dwIDIcon = 17031000;
            }

            // Round Kite Shield -> Plate Shield ikonu (17111000)
            if ((dwIDIcon >= 17061000 && dwIDIcon <= 17061999) ||
                (dwIDIcon >= 17062000 && dwIDIcon <= 17062999))
            {
                dwIDIcon = 17111000;
            }

            // Redirect all Chitin Bow / Hunters Bow icons (normal & rebirth, all upgrade levels) to 16121000 (itemicon_1_6121_00_0)
            if ((dwIDIcon >= 16121000 && dwIDIcon <= 16121999) || 
                (dwIDIcon >= 16122000 && dwIDIcon <= 16122999))
            {
                dwIDIcon = 16121000;
            }

            // Redirect all Enion Bow icons (normal & rebirth, all upgrade levels) to 16910000 (itemicon_1_6910_00_0)
            if ((dwIDIcon >= 16910000 && dwIDIcon <= 16910999) || 
                (dwIDIcon >= 16911000 && dwIDIcon <= 16911999))
            {
                dwIDIcon = 16910000;
            }

            // Redirect all Eagle's Eye icons (normal & rebirth, all upgrade levels) to 16930200 (itemicon_1_6930_20_0)
            if ((dwIDIcon >= 16930200 && dwIDIcon <= 16930299) || 
                (dwIDIcon >= 16931200 && dwIDIcon <= 16931299))
            {
                dwIDIcon = 16930200;
            }

            // Redirect all Helenid Cross Bow icons (normal & rebirth, all upgrade levels) to 16930100 (itemicon_1_6930_10_0)
            if ((dwIDIcon >= 16930100 && dwIDIcon <= 16930199) || 
                (dwIDIcon >= 16931100 && dwIDIcon <= 16931199))
            {
                dwIDIcon = 16930100;
            }

            // Redirect all Crossbow and Horn Crossbow icons (normal & rebirth, all upgrade levels) to 16930100 (Helenid icon)
            if ((dwIDIcon >= 16811000 && dwIDIcon <= 16811999) || 
                (dwIDIcon >= 16812000 && dwIDIcon <= 16812999) ||
                (dwIDIcon >= 16815000 && dwIDIcon <= 16815999) ||
                (dwIDIcon >= 16821000 && dwIDIcon <= 16821999) || 
                (dwIDIcon >= 16822000 && dwIDIcon <= 16822999) ||
                (dwIDIcon >= 16825000 && dwIDIcon <= 16825999))
            {
                dwIDIcon = 16930100;
            }

            // Redirect all Iron Crossbow icons (normal & rebirth, all upgrade levels) to 16831000 (itemicon_1_6831_00_0)
            if ((dwIDIcon >= 16831000 && dwIDIcon <= 16831999) || 
                (dwIDIcon >= 16832000 && dwIDIcon <= 16832999))
            {
                dwIDIcon = 16831000;
            }

            // Redirect all Iron Bow icons (normal & rebirth, all upgrade levels) to 16841000 (itemicon_1_6841_00_0)
            if ((dwIDIcon >= 16841000 && dwIDIcon <= 16841999) || 
                (dwIDIcon >= 16842000 && dwIDIcon <= 16842999))
            {
                dwIDIcon = 16841000;
            }
 
            int part1 = dwIDIcon / 10000000;
            int part2 = (dwIDIcon / 1000) % 10000;
            int part3 = (dwIDIcon / 10) % 100;
            int part4 = dwIDIcon % 10;
 
            return $"itemicon_{part1}_{part2:D4}_{part3:D2}_{part4}";
        }

        public static Sprite LoadItemIcon(int dwIDIcon)
        {
            if (dwIDIcon <= 0) return null;

            if (_cache.TryGetValue(dwIDIcon, out var cached))
                return cached;

            string baseName = GetIconFileName(dwIDIcon);
            if (string.IsNullOrEmpty(baseName)) return null;

            Texture2D tex = LoadTextureFromResources(baseName);
            if (tex == null)
            {
                _cache[dwIDIcon] = null;
                return null;
            }

            // C++ CN3UIIcon birebir: 45x45 crop
            int iconSize = 45;
            int cropW = Mathf.Min(iconSize, tex.width);
            int cropH = Mathf.Min(iconSize, tex.height);
            int cropY = Mathf.Max(0, tex.height - cropH);

            var sprite = Sprite.Create(
                tex,
                new Rect(0, cropY, cropW, cropH),
                new Vector2(0.5f, 0.5f),
                100f
            );

            _cache[dwIDIcon] = sprite;
            return sprite;
        }

        public static void ClearCache()
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value != null)
                {
                    if (kvp.Value.texture != null)
                        Object.DestroyImmediate(kvp.Value.texture, true);
                    Object.DestroyImmediate(kvp.Value, true);
                }
            }
            _cache.Clear();
        }

        // ================================================
        // Skill Icon
        // ================================================

        private static readonly Dictionary<int, Sprite> _skillCache = new Dictionary<int, Sprite>();

        public static string GetSkillIconFileName(int dwSkillID)
        {
            if (dwSkillID <= 0) return null;
            int part1 = dwSkillID % 100;
            int part2 = dwSkillID / 100;
            return $"skillicon_{part1:D2}_{part2}";
        }

        public static Sprite LoadSkillIcon(int dwSkillID)
        {
            if (dwSkillID <= 0) return null;

            if (_skillCache.TryGetValue(dwSkillID, out var cached))
                return cached;

            string baseName = GetSkillIconFileName(dwSkillID);
            if (string.IsNullOrEmpty(baseName)) return null;

            Texture2D tex = LoadTextureFromResources(baseName);
            if (tex == null)
            {
                _skillCache[dwSkillID] = null;
                return null;
            }

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );

            _skillCache[dwSkillID] = sprite;
            return sprite;
        }

        // ================================================
        // Enigma (Kilitli Skill) Icon
        // ================================================

        private static Sprite _enigmaIconCache;
        private static bool _enigmaIconLoaded;

        public static Sprite LoadEnigmaIcon()
        {
            if (_enigmaIconLoaded) return _enigmaIconCache;
            _enigmaIconLoaded = true;

            Texture2D tex = LoadTextureFromResources("skillicon_enigma");
            if (tex == null) return null;

            _enigmaIconCache = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );

            return _enigmaIconCache;
        }

        // ================================================
        // Eski API uyumluluğu (artık boş)
        // ================================================
        public static void SetAssetsPath(string path) { }
        public static string GetAssetsPath() => "";

        // ================================================
        // Private
        // ================================================

        private static Texture2D LoadTextureFromResources(string baseName)
        {
            var tex = Resources.Load<Texture2D>($"KOTextures/UI/{baseName}");
            if (tex != null) return tex;

            tex = Resources.Load<Texture2D>($"KOTextures/DTex/{baseName}");
            return tex;
        }
    }
}
