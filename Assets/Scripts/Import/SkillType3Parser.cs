using System.Collections.Generic;
using System.IO;
using System.Text;
using EntropyOnline.Import;
using UnityEngine;

namespace KOImport
{
    /// <summary>
    /// Open-KO birebir: __TABLE_UPC_SKILL_TYPE_3 parser — skill_magic_3.tbl
    ///
    /// GameDef.h:1122-1131:
    ///   struct __TABLE_UPC_SKILL_TYPE_3 {
    ///     uint32_t dwID     = 0; // 01 Skill ID
    ///     int iRadius       = 0; // 02 Skill radius
    ///     int iDDType       = 0; // 03 DoT/HoT type
    ///     int iStartDamage  = 0; // 04 Initial damage
    ///     int iDuraDamage   = 0; // 05 Duration damage
    ///     int iDurationTime = 0; // 06 Duration (seconds)
    ///     int iAttribute    = 0; // 07 Elemental type
    ///   };
    ///
    /// CheckValidCondition (MagicSkillMng.cpp:593-611):
    ///   pType3 = m_pTbl_Type_3->Find(pSkill->dwID)
    ///   pType3->iDDType, pType3->iStartDamage → stacking key hesaplama
    ///
    /// GameBase.cpp:
    ///   szFN = "Data\\skill_magic_3";
    ///   s_pTbl_Type_3.LoadFromFile(szFN);
    /// </summary>
    public static class SkillType3Parser
    {
        private static readonly Dictionary<int, SkillType3Entry> _table = new();

        public static bool IsLoaded { get; private set; }
        public static int Count => _table.Count;

        public static void Load(string tblPath)
        {
            _table.Clear();
            IsLoaded = false;

            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<EntropyOnline.Import.KOSkillType3Asset>("KOData/skill_magic_3");
            if (asset != null && asset.entries != null)
            {
                foreach (var e in asset.entries)
                {
                    if (e == null || e.Id <= 0) continue;
                    _table[e.Id] = new SkillType3Entry
                    {
                        Id = e.Id, Radius = e.Radius, DDType = e.DDType,
                        StartDamage = e.StartDamage, DuraDamage = e.DuraDamage,
                        DurationTime = e.DurationTime, Attribute = e.Attribute
                    };
                }
                IsLoaded = _table.Count > 0;
                return;
            }

            byte[] raw = KOTableProvider.LoadRaw(tblPath);
            if (raw == null)
            {
                Debug.LogWarning($"[SkillType3Parser] Dosya bulunamadı: {tblPath}");
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
                var entry = new SkillType3Entry();

                for (int c = 0; c < columnCount; c++)
                {
                    // GameDef.h:1122-1131 birebir kolon sırası
                    switch (c)
                    {
                        case 0: entry.Id = ReadAsInt(reader, colTypes[c]); break;            // dwID
                        case 1: entry.Radius = ReadAsInt(reader, colTypes[c]); break;        // iRadius
                        case 2: entry.DDType = ReadAsInt(reader, colTypes[c]); break;        // iDDType
                        case 3: entry.StartDamage = ReadAsInt(reader, colTypes[c]); break;   // iStartDamage
                        case 4: entry.DuraDamage = ReadAsInt(reader, colTypes[c]); break;    // iDuraDamage
                        case 5: entry.DurationTime = ReadAsInt(reader, colTypes[c]); break;  // iDurationTime
                        case 6: entry.Attribute = ReadAsInt(reader, colTypes[c]); break;     // iAttribute
                        default: SkipColumn(reader, colTypes[c]); break;
                    }
                }

                if (entry.Id > 0)
                    _table[entry.Id] = entry;
            }

            IsLoaded = _table.Count > 0;
        }

        /// <summary>
        /// Open-KO birebir: m_pTbl_Type_3->Find(dwMagicID)
        /// </summary>
        public static SkillType3Entry Find(int skillId)
        {
            _table.TryGetValue(skillId, out var entry);
            return entry;
        }

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
    /// Open-KO birebir: __TABLE_UPC_SKILL_TYPE_3 (GameDef.h:1122-1131)
    /// </summary>
    public class SkillType3Entry
    {
        public int Id;            // 01 dwID
        public int Radius;        // 02 iRadius
        public int DDType;        // 03 iDDType (DoT/HoT type)
        public int StartDamage;   // 04 iStartDamage
        public int DuraDamage;    // 05 iDuraDamage
        public int DurationTime;  // 06 iDurationTime
        public int Attribute;     // 07 iAttribute (elemental type)
    }
}
