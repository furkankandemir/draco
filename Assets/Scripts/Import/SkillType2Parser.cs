using System.Collections.Generic;
using System.IO;
using System.Text;
using EntropyOnline.Import;
using UnityEngine;

namespace KOImport
{
    /// <summary>
    /// Open-KO birebir: __TABLE_UPC_SKILL_TYPE_2 parser — skill_magic_2.tbl
    ///
    /// GameDef.h:1112-1120:
    ///   struct __TABLE_UPC_SKILL_TYPE_2 {
    ///     uint32_t dwID    = 0; // 01 Skill ID
    ///     int iSuccessType = 0; // 02 FX bundle move type (FIXED/FLEXABLE)
    ///     int iPower       = 0; // 03 Attack power
    ///     int iAddDamage   = 0; // 04 Bonus damage
    ///     int iAddDist     = 0; // 05 Distance increase
    ///     int iNumArrow    = 0; // 06 Number of arrows used
    ///   };
    ///
    /// MagicSkillMng.cpp:2157 — FlyingType2():
    ///   pType2 = m_pTbl_Type_2->Find(pSkill->dwID)
    ///   pType2->iNumArrow → çoklu ok sayısı (multi-arrow spread)
    ///   pType2->iSuccessType → FX_BUNDLE_MOVE_DIR_FIXEDTARGET / FLEXABLETARGET
    ///
    /// GameBase.cpp:
    ///   szFN = "Data\\skill_magic_2";
    ///   s_pTbl_Type_2.LoadFromFile(szFN);
    /// </summary>
    public static class SkillType2Parser
    {
        private static readonly Dictionary<int, SkillType2Entry> _table = new();

        public static bool IsLoaded { get; private set; }
        public static int Count => _table.Count;

        /// <summary>
        /// Open-KO birebir: s_pTbl_Type_2.LoadFromFile("Data\\skill_magic_2")
        /// </summary>
        public static void Load(string tblPath)
        {
            _table.Clear();
            IsLoaded = false;

            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<EntropyOnline.Import.KOSkillType2Asset>("KOData/skill_magic_2");
            if (asset != null && asset.entries != null)
            {
                foreach (var e in asset.entries)
                {
                    if (e == null || e.Id <= 0) continue;
                    _table[e.Id] = new SkillType2Entry
                    {
                        Id = e.Id, SuccessType = e.SuccessType, Power = e.Power,
                        AddDamage = e.AddDamage, AddDist = e.AddDist, NumArrow = e.NumArrow
                    };
                }
                IsLoaded = _table.Count > 0;
                return;
            }

            byte[] raw = KOTableProvider.LoadRaw(tblPath);
            if (raw == null)
            {
                Debug.LogWarning($"[SkillType2Parser] Dosya bulunamadı: {tblPath}");
                return;
            }

            byte[] decrypted = SkillTableParser.DecryptTblPublic(raw);

            using var ms = new MemoryStream(decrypted);
            using var reader = new BinaryReader(ms, Encoding.ASCII);

            int columnCount = reader.ReadInt32();
            var colTypes = new int[columnCount];
            for (int c = 0; c < columnCount; c++)
                colTypes[c] = reader.ReadInt32();

            int rowCount = reader.ReadInt32();

            for (int r = 0; r < rowCount; r++)
            {
                var entry = new SkillType2Entry();

                for (int c = 0; c < columnCount; c++)
                {
                    // GameDef.h:1112-1120 birebir kolon sırası
                    switch (c)
                    {
                        case 0: entry.Id = ReadAsInt(reader, colTypes[c]); break;          // dwID
                        case 1: entry.SuccessType = ReadAsInt(reader, colTypes[c]); break;  // iSuccessType
                        case 2: entry.Power = ReadAsInt(reader, colTypes[c]); break;        // iPower
                        case 3: entry.AddDamage = ReadAsInt(reader, colTypes[c]); break;    // iAddDamage
                        case 4: entry.AddDist = ReadAsInt(reader, colTypes[c]); break;      // iAddDist
                        case 5: entry.NumArrow = ReadAsInt(reader, colTypes[c]); break;     // iNumArrow
                        default: SkipColumn(reader, colTypes[c]); break;
                    }
                }

                if (entry.Id > 0)
                    _table[entry.Id] = entry;
            }

            IsLoaded = _table.Count > 0;
        }

        /// <summary>
        /// Open-KO birebir: m_pTbl_Type_2->Find(dwMagicID)
        /// </summary>
        public static SkillType2Entry Find(int skillId)
        {
            _table.TryGetValue(skillId, out var entry);
            return entry;
        }

        // Column readers — SkillTableParser ile aynı mantık
        private static int ReadAsInt(BinaryReader reader, int colType)
        {
            switch (colType)
            {
                case 1: return reader.ReadByte();
                case 2: return reader.ReadInt16();
                case 3: return reader.ReadSByte();
                case 4: return reader.ReadUInt16();
                case 5: return reader.ReadInt32();
                case 6: return (int)reader.ReadUInt32();
                case 8: return (int)reader.ReadSingle();
                default: SkipColumn(reader, colType); return 0;
            }
        }

        private static void SkipColumn(BinaryReader reader, int colType)
        {
            switch (colType)
            {
                case 1: reader.ReadByte(); break;
                case 2: reader.ReadInt16(); break;
                case 3: reader.ReadSByte(); break;
                case 4: reader.ReadUInt16(); break;
                case 5: reader.ReadInt32(); break;
                case 6: reader.ReadUInt32(); break;
                case 7: int len = reader.ReadInt32(); if (len > 0 && len < 100000) reader.ReadBytes(len); break;
                case 8: reader.ReadSingle(); break;
                case 9: reader.ReadDouble(); break;
            }
        }
    }

    /// <summary>
    /// Open-KO birebir: __TABLE_UPC_SKILL_TYPE_2 (GameDef.h:1112-1120)
    /// </summary>
    public class SkillType2Entry
    {
        /// <summary>01 dwID — Skill ID</summary>
        public int Id;
        /// <summary>02 iSuccessType — FX bundle move type (FIXED=3 / FLEXABLE=4)</summary>
        public int SuccessType;
        /// <summary>03 iPower — Attack power multiplier</summary>
        public int Power;
        /// <summary>04 iAddDamage — Bonus flat damage</summary>
        public int AddDamage;
        /// <summary>05 iAddDist — Distance increase</summary>
        public int AddDist;
        /// <summary>06 iNumArrow — Number of arrows (multi-arrow spread)</summary>
        public int NumArrow;
    }
}
