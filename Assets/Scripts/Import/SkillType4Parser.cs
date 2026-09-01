using System.Collections.Generic;
using System.IO;
using System.Text;
using EntropyOnline.Import;
using UnityEngine;

namespace KOImport
{
    /// <summary>
    /// Open-KO birebir: __TABLE_UPC_SKILL_TYPE_4 parser — skill_magic_4.tbl
    ///
    /// GameDef.h:1133-1163:
    ///   struct __TABLE_UPC_SKILL_TYPE_4 {
    ///     uint32_t dwID        = 0; // 01 Skill ID
    ///     int iBuffType        = 0; // 02 Buff type (e_SkillMagicType4 enum)
    ///     int iRadius          = 0; // 03 Buff radius
    ///     int iDuration        = 0; // 04 Buff duration
    ///     int iAttackSpeed     = 0; // 05 Attack speed %
    ///     int iMoveSpeed       = 0; // 06 Move speed %
    ///     int iAC              = 0; // 07 Flat defense
    ///     int iACPct           = 0; // 08 Defense %
    ///     int iAttack          = 0; // 09 Attack power %
    ///     int iMagicAttack     = 0; // 10 Magic attack %
    ///     int iMaxHP           = 0; // 11 Flat max HP
    ///     int iMaxHPPct        = 0; // 12 Max HP %
    ///     int iMaxMP           = 0; // 13 Flat max MP
    ///     int iMaxMPPct        = 0; // 14 Max MP %
    ///     int iStr             = 0; // 15 Strength
    ///     int iSta             = 0; // 16 Stamina
    ///     int iDex             = 0; // 17 Dexterity
    ///     int iInt             = 0; // 18 Intelligence
    ///     int iMAP             = 0; // 19 Charisma/MAP
    ///     int iFireResist      = 0; // 20 Fire resist
    ///     int iColdResist      = 0; // 21 Cold resist
    ///     int iLightningResist = 0; // 22 Lightning resist
    ///     int iMagicResist     = 0; // 23 Magic resist
    ///     int iDeseaseResist   = 0; // 24 Disease resist
    ///     int iPoisonResist    = 0; // 25 Poison resist
    ///     int iExpPct          = 0; // 26 EXP gain %
    ///   };
    ///
    /// CheckValidCondition (MagicSkillMng.cpp:614-661):
    ///   pType4 = m_pTbl_Type_4->Find(dwMagicID)
    ///   pType4->iBuffType → buff stacking kontrolü
    ///
    /// GameBase.cpp:
    ///   szFN = "Data\\skill_magic_4";
    ///   s_pTbl_Type_4.LoadFromFile(szFN);
    /// </summary>
    public static class SkillType4Parser
    {
        private static readonly Dictionary<int, SkillType4Entry> _table = new();

        public static bool IsLoaded { get; private set; }
        public static int Count => _table.Count;

        /// <summary>
        /// Open-KO birebir: s_pTbl_Type_4.LoadFromFile("Data\\skill_magic_4")
        /// </summary>
        public static void Load(string tblPath)
        {
            _table.Clear();
            IsLoaded = false;

            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<EntropyOnline.Import.KOSkillType4Asset>("KOData/skill_magic_4");
            if (asset != null && asset.entries != null)
            {
                foreach (var e in asset.entries)
                {
                    if (e == null || e.Id <= 0) continue;
                    _table[e.Id] = new SkillType4Entry
                    {
                        Id = e.Id, BuffType = e.BuffType, Radius = e.Radius, Duration = e.Duration,
                        AttackSpeed = e.AttackSpeed, MoveSpeed = e.MoveSpeed,
                        AC = e.AC, ACPct = e.ACPct, Attack = e.Attack, MagicAttack = e.MagicAttack,
                        MaxHP = e.MaxHP, MaxHPPct = e.MaxHPPct, MaxMP = e.MaxMP, MaxMPPct = e.MaxMPPct,
                        Str = e.Str, Sta = e.Sta, Dex = e.Dex, Int = e.Int_,
                        MAP = e.MAP, FireResist = e.FireResist, ColdResist = e.ColdResist,
                        LightningResist = e.LightningResist, MagicResist = e.MagicResist,
                        DiseaseResist = e.DiseaseResist, PoisonResist = e.PoisonResist, ExpPct = e.ExpPct
                    };
                }
                IsLoaded = _table.Count > 0;
                return;
            }

            byte[] raw = KOTableProvider.LoadRaw(tblPath);
            if (raw == null)
            {
                Debug.LogWarning($"[SkillType4Parser] Dosya bulunamadı: {tblPath}");
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
                var entry = new SkillType4Entry();

                for (int c = 0; c < columnCount; c++)
                {
                    // GameDef.h:1133-1163 birebir kolon sırası
                    switch (c)
                    {
                        case 0:  entry.Id = ReadAsInt(reader, colTypes[c]); break;              // dwID
                        case 1:  entry.BuffType = ReadAsInt(reader, colTypes[c]); break;        // iBuffType
                        case 2:  entry.Radius = ReadAsInt(reader, colTypes[c]); break;          // iRadius
                        case 3:  entry.Duration = ReadAsInt(reader, colTypes[c]); break;        // iDuration
                        case 4:  entry.AttackSpeed = ReadAsInt(reader, colTypes[c]); break;     // iAttackSpeed
                        case 5:  entry.MoveSpeed = ReadAsInt(reader, colTypes[c]); break;       // iMoveSpeed
                        case 6:  entry.AC = ReadAsInt(reader, colTypes[c]); break;              // iAC
                        case 7:  entry.ACPct = ReadAsInt(reader, colTypes[c]); break;           // iACPct
                        case 8:  entry.Attack = ReadAsInt(reader, colTypes[c]); break;          // iAttack
                        case 9:  entry.MagicAttack = ReadAsInt(reader, colTypes[c]); break;     // iMagicAttack
                        case 10: entry.MaxHP = ReadAsInt(reader, colTypes[c]); break;           // iMaxHP
                        case 11: entry.MaxHPPct = ReadAsInt(reader, colTypes[c]); break;        // iMaxHPPct
                        case 12: entry.MaxMP = ReadAsInt(reader, colTypes[c]); break;           // iMaxMP
                        case 13: entry.MaxMPPct = ReadAsInt(reader, colTypes[c]); break;        // iMaxMPPct
                        case 14: entry.Str = ReadAsInt(reader, colTypes[c]); break;             // iStr
                        case 15: entry.Sta = ReadAsInt(reader, colTypes[c]); break;             // iSta
                        case 16: entry.Dex = ReadAsInt(reader, colTypes[c]); break;             // iDex
                        case 17: entry.Int = ReadAsInt(reader, colTypes[c]); break;             // iInt
                        case 18: entry.MAP = ReadAsInt(reader, colTypes[c]); break;             // iMAP
                        case 19: entry.FireResist = ReadAsInt(reader, colTypes[c]); break;      // iFireResist
                        case 20: entry.ColdResist = ReadAsInt(reader, colTypes[c]); break;      // iColdResist
                        case 21: entry.LightningResist = ReadAsInt(reader, colTypes[c]); break; // iLightningResist
                        case 22: entry.MagicResist = ReadAsInt(reader, colTypes[c]); break;     // iMagicResist
                        case 23: entry.DiseaseResist = ReadAsInt(reader, colTypes[c]); break;   // iDeseaseResist
                        case 24: entry.PoisonResist = ReadAsInt(reader, colTypes[c]); break;    // iPoisonResist
                        case 25: entry.ExpPct = ReadAsInt(reader, colTypes[c]); break;          // iExpPct
                        default: SkipColumn(reader, colTypes[c]); break;
                    }
                }

                if (entry.Id > 0)
                    _table[entry.Id] = entry;
            }

            IsLoaded = _table.Count > 0;
        }

        /// <summary>
        /// Open-KO birebir: m_pTbl_Type_4->Find(dwMagicID)
        /// MagicSkillMng.cpp:617 — CheckValidCondition Type4 stacking
        /// </summary>
        public static SkillType4Entry Find(int skillId)
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
    /// Open-KO birebir: __TABLE_UPC_SKILL_TYPE_4 (GameDef.h:1133-1163)
    /// </summary>
    public class SkillType4Entry
    {
        public int Id;              // 01 dwID
        public int BuffType;        // 02 iBuffType (e_SkillMagicType4)
        public int Radius;          // 03 iRadius
        public int Duration;        // 04 iDuration
        public int AttackSpeed;     // 05 iAttackSpeed
        public int MoveSpeed;       // 06 iMoveSpeed
        public int AC;              // 07 iAC
        public int ACPct;           // 08 iACPct
        public int Attack;          // 09 iAttack
        public int MagicAttack;     // 10 iMagicAttack
        public int MaxHP;           // 11 iMaxHP
        public int MaxHPPct;        // 12 iMaxHPPct
        public int MaxMP;           // 13 iMaxMP
        public int MaxMPPct;        // 14 iMaxMPPct
        public int Str;             // 15 iStr
        public int Sta;             // 16 iSta
        public int Dex;             // 17 iDex
        public int Int;             // 18 iInt
        public int MAP;             // 19 iMAP
        public int FireResist;      // 20 iFireResist
        public int ColdResist;      // 21 iColdResist
        public int LightningResist; // 22 iLightningResist
        public int MagicResist;     // 23 iMagicResist
        public int DiseaseResist;   // 24 iDeseaseResist
        public int PoisonResist;    // 25 iPoisonResist
        public int ExpPct;          // 26 iExpPct
    }
}
