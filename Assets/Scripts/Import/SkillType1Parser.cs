using System.Collections.Generic;
using System.IO;
using System.Text;
using EntropyOnline.Import;
using UnityEngine;

namespace KOImport
{
    /// <summary>
    /// Open-KO birebir: __TABLE_UPC_SKILL_TYPE_1 parser — skill_magic_1.tbl
    ///
    /// GameDef.h:1098-1110:
    ///   struct __TABLE_UPC_SKILL_TYPE_1 {
    ///     uint32_t dwID     = 0;  // 01 Skill ID
    ///     int iSuccessType  = 0;  // 02 Success type
    ///     int iSuccessRatio = 0;  // 03 Success ratio (%)
    ///     int iPower        = 0;  // 04 Attack power
    ///     int iDelay        = 0;  // 05 Skill delay (time before next action)
    ///     int iComboType    = 0;  // 06 Combo type
    ///     int iNumCombo     = 0;  // 07 Number of hits in combo
    ///     int iComboDamage  = 0;  // 08 Damage per combo hit
    ///     int iValidAngle   = 0;  // 09 Attack radius
    ///     int iAct[3]       = {}; // 10,11,12 — Combo animation e_Ani indices
    ///   };
    ///
    /// MagicSkillMng.cpp:2291 — EffectingType1():
    ///   pType1 = m_pTbl_Type_1->Find(dwMagicID)
    ///   e_Ani eAni = (e_Ani) pType1->iAct[0]
    ///
    /// MagicSkillMng.cpp:64-65:
    ///   m_pTbl_Type_1 = new CN3TableBase<struct __TABLE_UPC_SKILL_TYPE_1>;
    ///   m_pTbl_Type_1->LoadFromFile("Data\\Skill_Magic_1.tbl");
    /// </summary>
    public static class SkillType1Parser
    {
        private static readonly Dictionary<int, SkillType1Entry> _table = new();

        public static bool IsLoaded { get; private set; }
        public static int Count => _table.Count;

        /// <summary>
        /// Open-KO birebir: m_pTbl_Type_1->LoadFromFile("Data\\Skill_Magic_1.tbl")
        /// </summary>
        public static void Load(string tblPath)
        {
            _table.Clear();
            IsLoaded = false;

            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<EntropyOnline.Import.KOSkillType1Asset>("KOData/skill_magic_1");
            if (asset != null && asset.entries != null)
            {
                foreach (var e in asset.entries)
                {
                    if (e == null || e.Id <= 0) continue;
                    _table[e.Id] = new SkillType1Entry
                    {
                        Id = e.Id, SuccessType = e.SuccessType, SuccessRatio = e.SuccessRatio,
                        Power = e.Power, Delay = e.Delay, ComboType = e.ComboType,
                        NumCombo = e.NumCombo, ComboDamage = e.ComboDamage, ValidAngle = e.ValidAngle,
                        Act = e.Act ?? new int[3]
                    };
                }
                IsLoaded = _table.Count > 0;
                return;
            }

            byte[] raw = KOTableProvider.LoadRaw(tblPath);
            if (raw == null)
            {
                Debug.LogWarning($"[SkillType1Parser] Dosya bulunamadı: {tblPath}");
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
                var entry = new SkillType1Entry();

                for (int c = 0; c < columnCount; c++)
                {
                    // GameDef.h:1098-1110 birebir kolon sırası
                    switch (c)
                    {
                        case 0:  entry.Id = ReadAsInt(reader, colTypes[c]); break;            // dwID
                        case 1:  entry.SuccessType = ReadAsInt(reader, colTypes[c]); break;   // iSuccessType
                        case 2:  entry.SuccessRatio = ReadAsInt(reader, colTypes[c]); break;  // iSuccessRatio
                        case 3:  entry.Power = ReadAsInt(reader, colTypes[c]); break;         // iPower
                        case 4:  entry.Delay = ReadAsInt(reader, colTypes[c]); break;         // iDelay
                        case 5:  entry.ComboType = ReadAsInt(reader, colTypes[c]); break;     // iComboType
                        case 6:  entry.NumCombo = ReadAsInt(reader, colTypes[c]); break;      // iNumCombo
                        case 7:  entry.ComboDamage = ReadAsInt(reader, colTypes[c]); break;   // iComboDamage
                        case 8:  entry.ValidAngle = ReadAsInt(reader, colTypes[c]); break;    // iValidAngle
                        case 9:  entry.Act[0] = ReadAsInt(reader, colTypes[c]); break;        // iAct[0]
                        case 10: entry.Act[1] = ReadAsInt(reader, colTypes[c]); break;        // iAct[1]
                        case 11: entry.Act[2] = ReadAsInt(reader, colTypes[c]); break;        // iAct[2]
                        default: SkipColumn(reader, colTypes[c]); break;
                    }
                }

                if (entry.Id > 0)
                    _table[entry.Id] = entry;
            }

            IsLoaded = _table.Count > 0;
        }

        /// <summary>
        /// Open-KO birebir: m_pTbl_Type_1->Find(dwMagicID)
        /// </summary>
        public static SkillType1Entry Find(int skillId)
        {
            if (!IsLoaded)
                Debug.LogWarning($"[SkillType1Parser] Find({skillId}) çağrıldı ama tablo YÜKLENMEMİŞ!");
            bool found = _table.TryGetValue(skillId, out var entry);
            if (!found)
                Debug.LogWarning($"[SkillType1Parser] Find({skillId}) → BULUNAMADI! Tabloda {_table.Count} kayıt var. " +
                    $"İlk 5 ID: {string.Join(", ", System.Linq.Enumerable.Take(_table.Keys, 5))}");
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
    /// Open-KO birebir: __TABLE_UPC_SKILL_TYPE_1 (GameDef.h:1098-1110)
    /// </summary>
    public class SkillType1Entry
    {
        /// <summary>01 dwID — Skill ID</summary>
        public int Id;
        /// <summary>02 iSuccessType — Success type</summary>
        public int SuccessType;
        /// <summary>03 iSuccessRatio — Success ratio (%)</summary>
        public int SuccessRatio;
        /// <summary>04 iPower — Attack power multiplier</summary>
        public int Power;
        /// <summary>05 iDelay — Skill delay</summary>
        public int Delay;
        /// <summary>06 iComboType — Combo type</summary>
        public int ComboType;
        /// <summary>07 iNumCombo — Number of combo hits</summary>
        public int NumCombo;
        /// <summary>08 iComboDamage — Damage per combo hit</summary>
        public int ComboDamage;
        /// <summary>09 iValidAngle — Attack radius/angle</summary>
        public int ValidAngle;
        /// <summary>10-12 iAct[3] — Combo animation e_Ani indices</summary>
        public int[] Act = new int[3];
    }
}
