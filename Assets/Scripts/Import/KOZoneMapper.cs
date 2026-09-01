using System.Collections.Generic;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Zone ID → KO asset dosya adı eşleme tablosu.
    /// Server zone ID'lerinden doğru .gtd/.opd dosyalarını bulur.
    /// 
    /// Open-KO v1.298 client'ta sunucudan gelen zone ID (uint8) × 10 ile çarpılarak
    /// Zones.tbl'deki key'e dönüştürülür. (GameProcedure.cpp:961 — iZoneCur *= 10)
    /// Bu dosyadaki mapping Zones.tbl'den decrypt edilmiş veriye dayanır.
    /// 
    /// Zone düzeni (Open-KO globals.h e_ZoneID + Zones.tbl):
    ///   ZONE_KARUS          =  1  → TBL Key  10 → karus2004.gtd
    ///   ZONE_ELMORAD        =  2  → TBL Key  20 → elmo2004.gtd
    ///   ZONE_ESLANT_KARUS   = 11  → TBL Key 110 → EslantZone.gtd
    ///   ZONE_ESLANT_ELMORAD = 12  → TBL Key 120 → EslantZone.gtd
    ///   ZONE_MORADON        = 21  → TBL Key 210 → Moradon_xmas.gtd
    ///   ZONE_DELOS          = 30  → TBL Key 300 → war_a.gtd
    ///   ZONE_DESPERATION    = 32  → TBL Key 320 → dungeon_b1th.gtd
    ///   ZONE_HELL_ABYSS     = 33  → TBL Key 330 → dungeon_b2th.gtd
    ///   ZONE_ARENA          = 48  → TBL Key 480 → arena.gtd
    ///   ZONE_BATTLE         =101  → TBL Key 1010→ BattleZone.gtd
    ///   ZONE_BATTLE2        =102  → TBL Key 1020→ BattleZone_b.gtd
    ///   ZONE_BATTLE3        =103  → TBL Key 1030→ BattleZone_c.gtd
    ///   ZONE_SNOW_BATTLE    =111  → TBL Key 1110→ BattleZone_b.gtd
    ///   ZONE_FRONTIER       =201  → TBL Key 2010→ FreeZone_b.gtd
    /// </summary>
    public static class KOZoneMapper
    {
        /// <summary>
        /// Zone bilgileri.
        /// </summary>
        public class ZoneInfo
        {
            public short ZoneId;
            public string ZoneName;
            public string GtdFile;      // terrain heightmap
            public string OpdFile;      // object placement
            public string DxtFile;      // colormap texture (TCT in Zones.tbl)
            public string TltFile;      // tile lookup (light map)
            public string GloFile;      // zone light objects (GLO in Zones.tbl)
            public string SkyFile;      // sky/atmosphere settings (N3Sky — szSkySetting in Zones.tbl)
            public int FixedSunDirection; // iFixedSundDirection — 0=normal cycle, >0=fixed hour
            public Vector3 DefaultSpawn; // varsayılan spawn noktası
        }

        private static readonly Dictionary<short, ZoneInfo> _zones = new()
        {
            // ZONE_KARUS (1) — TBL Key 10 → karus2004
            // START_POSITION: zone=1, sKarusX=441, sKarusZ=1625, sElmoradX=1859, sElmoradZ=170, range=10
            {1, new ZoneInfo {
                ZoneId = 1, ZoneName = "Karus",
                GtdFile = "karus2004.gtd",
                OpdFile = "karus2004.opd",
                DxtFile = "karus2004.dxt",
                TltFile = "karus2004.tlt",
                GloFile = "karus_start.glo",
                SkyFile = "Misc/Sky/Karus.n3sky",
                FixedSunDirection = 0,
                DefaultSpawn = new Vector3(441, 0, 1625)
            }},

            // ZONE_ELMORAD (2) — TBL Key 20 → elmo2004
            // START_POSITION: zone=2, sKarusX=219, sKarusZ=1859, sElmoradX=1595, sElmoradZ=412, range=10
            {2, new ZoneInfo {
                ZoneId = 2, ZoneName = "El Morad",
                GtdFile = "elmo2004.gtd",
                OpdFile = "elmo2004.opd",
                DxtFile = "elmo2004.dxt",
                TltFile = "elmo2004.tlt",
                GloFile = "elmorad_start.glo",
                SkyFile = "Misc/Sky/Elmorad.n3sky",
                FixedSunDirection = 0,
                DefaultSpawn = new Vector3(1595, 0, 412)
            }},

            // ZONE_ESLANT_KARUS (11) — TBL Key 110 → EslantZone
            // START_POSITION: zone=11, sKarusX=527, sKarusZ=543, sElmoradX=527, sElmoradZ=543, range=10
            {11, new ZoneInfo {
                ZoneId = 11, ZoneName = "Eslant (Karus)",
                GtdFile = "eslantzone.gtd",
                OpdFile = "eslantzone.opd",
                DxtFile = "eslantzone.dxt",
                TltFile = "eslantzone.tlt",
                GloFile = "eslantzone.glo",
                DefaultSpawn = new Vector3(527, 0, 543)
            }},

            // ZONE_ESLANT_ELMORAD (12) — TBL Key 120 → EslantZone
            // START_POSITION: zone=12, sKarusX=527, sKarusZ=543, sElmoradX=527, sElmoradZ=543, range=10
            {12, new ZoneInfo {
                ZoneId = 12, ZoneName = "Eslant (El Morad)",
                GtdFile = "eslantzone.gtd",
                OpdFile = "eslantzone.opd",
                DxtFile = "eslantzone.dxt",
                TltFile = "eslantzone.tlt",
                GloFile = "eslantzone.glo",
                DefaultSpawn = new Vector3(527, 0, 543)
            }},

            // ZONE_MORADON (21)
            // START_POSITION: zone=21, sKarusX=306, sKarusZ=352, sElmoradX=306, sElmoradZ=352, range=10
            {21, new ZoneInfo {
                ZoneId = 21, ZoneName = "Moradon",
                GtdFile = "moradon.gtd",
                OpdFile = "moradon.opd",
                DxtFile = "moradon.dxt",
                TltFile = "moradon.tlt",
                GloFile = "moradon.glo",
                SkyFile = "Misc/Sky/moradon.n3sky",
                FixedSunDirection = 0,
                DefaultSpawn = new Vector3(306, 0, 352)
            }},

            // ZONE_DELOS (30) — TBL Key 300 → war_a
            // START_POSITION: zone=30, sKarusX=505, sKarusZ=837, sElmoradX=500, sElmoradZ=250, range=10
            {30, new ZoneInfo {
                ZoneId = 30, ZoneName = "Delos",
                GtdFile = "war_a.gtd",
                OpdFile = "war_a.opd",
                DxtFile = "war_a.dxt",
                TltFile = "war_a.tlt",
                GloFile = "war_a.glo",
                DefaultSpawn = new Vector3(505, 0, 837)
            }},

            // ZONE_DESPERATION_ABYSS (32) — TBL Key 320 → dungeon_b1th
            // START_POSITION: zone=32, sKarusX=50, sKarusZ=69, sElmoradX=50, sElmoradZ=69, range=1
            {32, new ZoneInfo {
                ZoneId = 32, ZoneName = "Desperation Abyss",
                GtdFile = "dungeon_b1th.gtd",
                OpdFile = "dungeon_b1th.opd",
                DxtFile = "",
                TltFile = "dungeon_b1th.tlt",
                GloFile = "dungeon_b1th.glo",
                DefaultSpawn = new Vector3(50, 0, 69)
            }},

            // ZONE_HELL_ABYSS (33) — TBL Key 330 → dungeon_b2th
            // START_POSITION: zone=33, sKarusX=50, sKarusZ=69, sElmoradX=50, sElmoradZ=69, range=1
            {33, new ZoneInfo {
                ZoneId = 33, ZoneName = "Hell Abyss",
                GtdFile = "dungeon_b2th.gtd",
                OpdFile = "dungeon_b2th.opd",
                DxtFile = "",
                TltFile = "dungeon_b2th.tlt",
                GloFile = "dungeon_b2th.glo",
                DefaultSpawn = new Vector3(50, 0, 69)
            }},

            // ZONE_ARENA (48) — TBL Key 480 → arena
            // START_POSITION: zone=48, sKarusX=128, sKarusZ=120, sElmoradX=128, sElmoradZ=120, range=5
            {48, new ZoneInfo {
                ZoneId = 48, ZoneName = "Arena",
                GtdFile = "arena.gtd",
                OpdFile = "arena.opd",
                DxtFile = "",
                TltFile = "arena.tlt",
                GloFile = "arena.glo",
                DefaultSpawn = new Vector3(128, 0, 120)
            }},

            // ZONE_BATTLE (101) — TBL Key 1010 → BattleZone
            // START_POSITION: zone=101, sKarusX=820, sKarusZ=98, sElmoradX=113, sElmoradZ=771, range=5
            {101, new ZoneInfo {
                ZoneId = 101, ZoneName = "Battle Zone",
                GtdFile = "battlezone.gtd",
                OpdFile = "battlezone.opd",
                DxtFile = "battlezone.dxt",
                TltFile = "battlezone.tlt",
                GloFile = "",
                DefaultSpawn = new Vector3(820, 0, 98)
            }},

            // ZONE_BATTLE2 (102) — TBL Key 1020 → BattleZone_b
            // START_POSITION: zone=102, sKarusX=48, sKarusZ=155, sElmoradX=974, sElmoradZ=869, range=5
            {102, new ZoneInfo {
                ZoneId = 102, ZoneName = "Battle Zone B",
                GtdFile = "battlezone_b.gtd",
                OpdFile = "battlezone_b.opd",
                DxtFile = "battlezone_b.dxt",
                TltFile = "battlezone_b.tlt",
                GloFile = "battlezone_b.glo",
                DefaultSpawn = new Vector3(48, 0, 155)
            }},

            // ZONE_BATTLE3 (103) — TBL Key 1030 → BattleZone_c
            // START_POSITION: zone=103, sKarusX=172, sKarusZ=61, sElmoradX=822, sElmoradZ=937, range=5
            {103, new ZoneInfo {
                ZoneId = 103, ZoneName = "Battle Zone C",
                GtdFile = "battlezone_c.gtd",
                OpdFile = "battlezone_c.opd",
                DxtFile = "battlezone_c.dxt",
                TltFile = "battlezone_c.tlt",
                GloFile = "battlezone_c.glo",
                DefaultSpawn = new Vector3(172, 0, 61)
            }},

            // ZONE_SNOW_BATTLE (111) — TBL Key 1110 → BattleZone_b
            // START_POSITION: zone=111, sKarusX=143, sKarusZ=73, sElmoradX=900, sElmoradZ=900, range=5
            {111, new ZoneInfo {
                ZoneId = 111, ZoneName = "Snow Battle Zone",
                GtdFile = "battlezone_b.gtd",
                OpdFile = "battlezone_b.opd",
                DxtFile = "battlezone_b.dxt",
                TltFile = "battlezone_b.tlt",
                GloFile = "battlezone_b.glo",
                DefaultSpawn = new Vector3(143, 0, 73)
            }},

            // ZONE_FRONTIER (201) — TBL Key 2010 → FreeZone_b
            // START_POSITION: zone=201, sKarusX=1380, sKarusZ=1090, sElmoradX=617, sElmoradZ=919, range=10
            {201, new ZoneInfo {
                ZoneId = 201, ZoneName = "Colony Zone",
                GtdFile = "freezone_b.gtd",
                OpdFile = "freezone_b.opd",
                DxtFile = "freezone_b.dxt",
                TltFile = "freezone_b.tlt",
                GloFile = "freezone_b.glo",
                DefaultSpawn = new Vector3(1380, 0, 1090)
            }},
        };

        /// <summary>
        /// Zone ID'den bilgi al. Bulunamazsa null döner.
        /// </summary>
        public static ZoneInfo GetZoneInfo(short zoneId)
        {
            return _zones.TryGetValue(zoneId, out var info) ? info : null;
        }

        /// <summary>
        /// Tam dosya yolunu oluşturur.
        /// </summary>
        public static string GetGtdPath(short zoneId)
        {
            var info = GetZoneInfo(zoneId);
            if (info == null) return null;

            return System.IO.Path.Combine("Zones", info.GtdFile);
        }

        /// <summary>
        /// Tüm kayıtlı zone'ları döndürür.
        /// </summary>
        public static IReadOnlyDictionary<short, ZoneInfo> GetAllZones() => _zones;
    }
}
