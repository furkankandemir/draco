using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// .N3Sky dosyalarını parse eder ve zone ortam verisi çıkarır.
    /// Open-KO v1.298: CN3SkyMng::Load (N3SkyMng.cpp:1571-1653)
    ///
    /// Binary format:
    ///   for i=0..NUM_SUNPART(3):  int32 len + string (sun texture)
    ///   for i=0..NUM_CLOUD(6):    int32 len + string (cloud texture)
    ///   int32 len + string (moon texture)
    ///   int32 dayChangeCount
    ///   for each dayChange:
    ///     __SKY_DAYCHANGE::Load:
    ///       int32 nameLen + name
    ///       int32 eSkyDayChange (enum)
    ///       int32 dwWhen (game-seconds since midnight, 0-86400)
    ///       uint32 dwParam1 (D3DCOLOR or value)
    ///       uint32 dwParam2 (D3DCOLOR or value)
    ///       float fHowLong (real seconds for transition)
    ///
    /// eSKY_DAYCHANGE enum:
    ///   0=SDC_SKYCOLOR, 1=SDC_FOGCOLOR, 2=SDC_STARCOUNT, 3=SDC_MOONPHASE,
    ///   4=SDC_SUNCOLOR, 5=SDC_GLOWCOLOR, 6=SDC_FLARECOLOR,
    ///   7=SDC_CLOUD1COLOR, 8=SDC_CLOUD2COLOR, 9=SDC_CLOUDTEX,
    ///   10=SDC_LIGHT0COLOR, 11=SDC_LIGHT1COLOR, 12=SDC_LIGHT2COLOR
    /// </summary>
    public static class N3SkyImporter
    {
        public const int NUM_SUNPART = 3;
        public const int NUM_CLOUD = 6;

        /// <summary>
        /// eSKY_DAYCHANGE enum karşılığı.
        /// </summary>
        public enum SkyDayChangeType
        {
            SkyColor = 0,
            FogColor = 1,
            StarCount = 2,
            MoonPhase = 3,
            SunColor = 4,
            GlowColor = 5,
            FlareColor = 6,
            Cloud1Color = 7,
            Cloud2Color = 8,
            CloudTex = 9,
            Light0Color = 10,
            Light1Color = 11,
            Light2Color = 12
        }

        /// <summary>
        /// Parse edilmiş DayChange verisi.
        /// </summary>
        public class DayChangeEntry
        {
            public string Name;
            public SkyDayChangeType ChangeType;
            public uint When;     // game-seconds since midnight (0-86400)
            public uint Param1;   // D3DCOLOR (ARGB) or value
            public uint Param2;   // D3DCOLOR (ARGB) or value
            public float HowLong; // real seconds for transition
        }

        /// <summary>
        /// Parse edilmiş N3Sky verisi.
        /// </summary>
        public class N3SkyData
        {
            public string[] SunTextures = new string[NUM_SUNPART];
            public string[] CloudTextures = new string[NUM_CLOUD];
            public string MoonTexture;
            public List<DayChangeEntry> DayChanges = new();
        }

        /// <summary>
        /// .N3Sky dosyasını parse eder.
        /// Open-KO: CN3SkyMng::Load (N3SkyMng.cpp:1571-1653)
        /// </summary>
        public static N3SkyData Load(string skyPath)
        {
            if (!KOBinaryProvider.Exists(skyPath))
            {
                Debug.LogWarning($"[N3Sky] Dosya bulunamadı: {skyPath}");
                return null;
            }

            try
            {
                using var fs = File.OpenRead(skyPath);
                using var br = new BinaryReader(fs);

                var data = new N3SkyData();

                // --- Sun textures (NUM_SUNPART=3) ---
                // N3SkyMng.cpp:1577-1586
                for (int i = 0; i < NUM_SUNPART; i++)
                    data.SunTextures[i] = ReadLenString(br);

                // --- Cloud textures (NUM_CLOUD=6) ---
                // N3SkyMng.cpp:1588-1597
                for (int i = 0; i < NUM_CLOUD; i++)
                    data.CloudTextures[i] = ReadLenString(br);

                // --- Moon texture ---
                // N3SkyMng.cpp:1599-1605
                data.MoonTexture = ReadLenString(br);

                // --- DayChange entries ---
                // N3SkyMng.cpp:1636-1649
                int dayChangeCount = br.ReadInt32();
                for (int i = 0; i < dayChangeCount; i++)
                {
                    var entry = new DayChangeEntry();

                    // __SKY_DAYCHANGE::Load (N3SkyMng.h:76-97)
                    entry.Name = ReadLenString(br);

                    // eSkyDayChange (int32)
                    int changeType = br.ReadInt32();
                    entry.ChangeType = (SkyDayChangeType)changeType;

                    // dwWhen (uint32 — game-seconds)
                    entry.When = br.ReadUInt32();

                    // dwParam1, dwParam2 (uint32 — D3DCOLOR ARGB)
                    entry.Param1 = br.ReadUInt32();
                    entry.Param2 = br.ReadUInt32();

                    // fHowLong (float — real seconds)
                    entry.HowLong = br.ReadSingle();

                    data.DayChanges.Add(entry);
                }

                // Sort by When time (qsort in original — CompareTime)
                data.DayChanges.Sort((a, b) => a.When.CompareTo(b.When));


                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[N3Sky] Parse hatası: {skyPath} — {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// D3DCOLOR (ARGB uint32) → Unity Color dönüşümü.
        /// D3DCOLOR format: 0xAARRGGBB
        /// </summary>
        public static Color D3DColorToUnity(uint d3dColor)
        {
            float a = ((d3dColor >> 24) & 0xFF) / 255f;
            float r = ((d3dColor >> 16) & 0xFF) / 255f;
            float g = ((d3dColor >> 8) & 0xFF) / 255f;
            float b = (d3dColor & 0xFF) / 255f;
            return new Color(r, g, b, a);
        }

        /// <summary>
        /// Belirli bir oyun saatine göre (gameTimeSeconds, 0-86400) en güncel değeri bulur.
        /// Open-KO: CN3SkyMng::SetCheckGameTime → GetLatestChange mantığı
        ///
        /// DayChange listesi zamana göre sıralı. Verilen saat için,
        /// o saatte veya öncesinde en son geçerli olan değeri döndürür.
        /// Bulamazsa listede en sondakini (wrap-around) döndürür.
        /// </summary>
        public static DayChangeEntry GetLatestChange(
            List<DayChangeEntry> dayChanges, SkyDayChangeType type, uint gameTimeSec)
        {
            // Ters yönde ara — verilen saatte veya öncesinde aynı türdeki en son entry
            DayChangeEntry found = null;
            DayChangeEntry lastOfType = null;

            foreach (var dc in dayChanges)
            {
                if (dc.ChangeType != type) continue;
                lastOfType = dc; // Son bulunan (wrap-around için)

                if (dc.When <= gameTimeSec)
                    found = dc;
            }

            // Eğer verilen saatten önce bulamadıysa, wrap-around: en son entry'yi kullan
            // (gece yarısını geçen durum — örn saat 02:00 ama en son entry 21:00'de)
            return found ?? lastOfType;
        }

        /// <summary>
        /// Belirli bir saat için gökyüzü rengini bulur.
        /// </summary>
        public static Color GetSkyColorAtTime(N3SkyData data, uint gameTimeSec)
        {
            var entry = GetLatestChange(data.DayChanges, SkyDayChangeType.SkyColor, gameTimeSec);
            return entry != null ? D3DColorToUnity(entry.Param1) : new Color(0.3f, 0.5f, 0.8f);
        }

        /// <summary>
        /// Belirli bir saat için sis rengini bulur.
        /// </summary>
        public static Color GetFogColorAtTime(N3SkyData data, uint gameTimeSec)
        {
            var entry = GetLatestChange(data.DayChanges, SkyDayChangeType.FogColor, gameTimeSec);
            return entry != null ? D3DColorToUnity(entry.Param1) : new Color(0.6f, 0.65f, 0.75f);
        }

        /// <summary>
        /// Belirli bir saat için Light0 (global directional) diffuse rengini bulur.
        /// </summary>
        public static Color GetLight0ColorAtTime(N3SkyData data, uint gameTimeSec)
        {
            var entry = GetLatestChange(data.DayChanges, SkyDayChangeType.Light0Color, gameTimeSec);
            return entry != null ? D3DColorToUnity(entry.Param1) : Color.white;
        }

        /// <summary>
        /// Belirli bir saat için Light1 (terrain) diffuse rengini bulur.
        /// </summary>
        public static Color GetLight1ColorAtTime(N3SkyData data, uint gameTimeSec)
        {
            var entry = GetLatestChange(data.DayChanges, SkyDayChangeType.Light1Color, gameTimeSec);
            return entry != null ? D3DColorToUnity(entry.Param1) : Color.white;
        }

        /// <summary>
        /// int32 len + string okuyan yardımcı.
        /// </summary>
        private static string ReadLenString(BinaryReader br)
        {
            int len = br.ReadInt32();
            if (len > 0 && len <= 512)
                return new string(br.ReadChars(len));
            else if (len > 512)
                throw new InvalidDataException($"String uzunluğu çok büyük: {len}");
            return "";
        }
    }
}
