using System.Collections.Generic;
using System.IO;
using System.Text;
using EntropyOnline.Import;
using UnityEngine;

namespace KOImport
{
    /// <summary>
    /// Open-KO birebir: __TABLE_UPC_SKILL parser — Skill_Magic_Main_us.tbl
    ///
    /// GameBase.cpp:81-82:
    ///   szFN = "Data\\skill_magic_main" + szLangTail;
    ///   s_pTbl_Skill.LoadFromFile(szFN);
    ///
    /// __TABLE_UPC_SKILL (GameDef.h:1057-1096) — 30 kolon:
    ///   01 dwID          uint32  Skill ID
    ///   02 szEngName     string  English name
    ///   03 szName        string  Korean name (display name)
    ///   04 szDesc        string  Description
    ///   05 iSelfAnimID1  int     Start animation (caster)
    ///   06 iSelfAnimID2  int     End animation (caster)
    ///   07 idwTargetAnimID int   Target animation
    ///   08 iSelfFX1      int     Effect on caster (1)
    ///   09 iSelfPart1    int     Effect position for iSelfFX1
    ///   10 iSelfFX2      int     Effect on caster (2)
    ///   11 iSelfPart2    int     Effect position for iSelfFX2
    ///   12 iFlyingFX     int     Flying effect
    ///   13 iTargetFX     int     Target effect
    ///   14 iTargetPart   int     Effect position for iTargetFX
    ///   15 iTarget       int     Target type/"moral"
    ///   16 iNeedLevel    int     Required player level
    ///   17 iNeedSkill    int     Required skill
    ///   18 iExhaustMSP   int     MSP consumed
    ///   19 iExhaustHP    int     HP consumed
    ///   20 dwNeedItem    uint32  Required item
    ///   21 dwExhaustItem uint32  Item consumed
    ///   22 iCastTime     int     Cast time
    ///   23 iReCastTime   int     Cooldown time
    ///   24 fIDK0         float   Unknown
    ///   25 fIDK1         float   Unknown
    ///   26 iPercentSuccess int   Success rate
    ///   27 dw1stTableType uint32 Primary skill type
    ///   28 dw2ndTableType uint32 Secondary skill type
    ///   29 iValidDist    int     Effective skill range
    ///   30 iIDK2         int     Unknown
    ///
    /// TBL decrypt: N3TableBaseImpl.cpp:51-77 rolling key XOR (0x0816, 0x6081, 0x1608)
    ///
    /// MagicSkillMng.cpp:1924-1927 — MsgRecv_Effecting:
    ///   pSkill->iTargetFX, pSkill->iTargetPart kullanılarak TriggerBundle çağrılır
    /// </summary>
    public static class SkillTableParser
    {
        private static readonly Dictionary<int, SkillEntry> _skillTable = new();

        public static bool IsLoaded { get; private set; }

        /// <summary>Yüklenen skill sayısı</summary>
        public static int Count => _skillTable.Count;

        /// <summary>
        /// Skill tablosunu yükle.
        /// Open-KO birebir: GameBase.cpp:81-82
        /// </summary>
        public static void Load(string tblPath)
        {
            _skillTable.Clear();
            IsLoaded = false;

            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<EntropyOnline.Import.KOSkillMainAsset>("KOData/Skill_Magic_Main_us");
            if (asset != null && asset.entries != null)
            {
                foreach (var e in asset.entries)
                {
                    if (e == null || e.Id <= 0) continue;
                    _skillTable[e.Id] = new SkillEntry
                    {
                        Id = e.Id, EngName = e.EngName ?? "", Name = e.Name ?? "", Desc = e.Desc ?? "",
                        SelfAnimID1 = e.SelfAnimID1, SelfAnimID2 = e.SelfAnimID2, TargetAnimID = e.TargetAnimID,
                        SelfFX1 = e.SelfFX1, SelfPart1 = e.SelfPart1, SelfFX2 = e.SelfFX2, SelfPart2 = e.SelfPart2,
                        FlyingFX = e.FlyingFX, TargetFX = e.TargetFX, TargetPart = e.TargetPart, Target = e.Target,
                        NeedLevel = e.NeedLevel, NeedSkill = e.NeedSkill, ExhaustMSP = e.ExhaustMSP, ExhaustHP = e.ExhaustHP,
                        NeedItem = e.NeedItem, ExhaustItem = e.ExhaustItem, CastTime = e.CastTime, ReCastTime = e.ReCastTime,
                        IDK0 = e.IDK0, IDK1 = e.IDK1, PercentSuccess = e.PercentSuccess,
                        FirstTableType = e.FirstTableType, SecondTableType = e.SecondTableType,
                        ValidDist = e.ValidDist, IDK2 = e.IDK2
                    };
                }
                IsLoaded = _skillTable.Count > 0;
                return;
            }

            byte[] raw = KOTableProvider.LoadRaw(tblPath);
            if (raw == null)
            {
                Debug.LogWarning($"[SkillTableParser] Dosya bulunamadı: {tblPath}");
                return;
            }

            byte[] decrypted = DecryptTbl(raw);

            using var ms = new MemoryStream(decrypted);
            using var reader = new BinaryReader(ms, Encoding.ASCII);

            int columnCount = reader.ReadInt32();
            var colTypes = new int[columnCount];
            for (int c = 0; c < columnCount; c++)
                colTypes[c] = reader.ReadInt32();

            int rowCount = reader.ReadInt32();

            for (int r = 0; r < rowCount; r++)
            {
                var entry = new SkillEntry();

                for (int c = 0; c < columnCount; c++)
                {
                    // GameDef.h:1057-1096 birebir kolon sırası
                    switch (c)
                    {
                        case 0: entry.Id = ReadAsInt(reader, colTypes[c]); break;            // dwID
                        case 1: entry.EngName = ReadString(reader, colTypes[c]); break;      // szEngName
                        case 2: entry.Name = ReadString(reader, colTypes[c]); break;         // szName
                        case 3: entry.Desc = ReadString(reader, colTypes[c]); break;         // szDesc
                        case 4: entry.SelfAnimID1 = ReadAsInt(reader, colTypes[c]); break;   // iSelfAnimID1
                        case 5: entry.SelfAnimID2 = ReadAsInt(reader, colTypes[c]); break;   // iSelfAnimID2
                        case 6: entry.TargetAnimID = ReadAsInt(reader, colTypes[c]); break;  // idwTargetAnimID
                        case 7: entry.SelfFX1 = ReadAsInt(reader, colTypes[c]); break;       // iSelfFX1
                        case 8: entry.SelfPart1 = ReadAsInt(reader, colTypes[c]); break;     // iSelfPart1
                        case 9: entry.SelfFX2 = ReadAsInt(reader, colTypes[c]); break;       // iSelfFX2
                        case 10: entry.SelfPart2 = ReadAsInt(reader, colTypes[c]); break;    // iSelfPart2
                        case 11: entry.FlyingFX = ReadAsInt(reader, colTypes[c]); break;     // iFlyingFX
                        case 12: entry.TargetFX = ReadAsInt(reader, colTypes[c]); break;     // iTargetFX
                        case 13: entry.TargetPart = ReadAsInt(reader, colTypes[c]); break;   // iTargetPart
                        case 14: entry.Target = ReadAsInt(reader, colTypes[c]); break;       // iTarget
                        case 15: entry.NeedLevel = ReadAsInt(reader, colTypes[c]); break;    // iNeedLevel
                        case 16: entry.NeedSkill = ReadAsInt(reader, colTypes[c]); break;    // iNeedSkill
                        case 17: entry.ExhaustMSP = ReadAsInt(reader, colTypes[c]); break;   // iExhaustMSP
                        case 18: entry.ExhaustHP = ReadAsInt(reader, colTypes[c]); break;    // iExhaustHP
                        case 19: entry.NeedItem = ReadAsUInt(reader, colTypes[c]); break;    // dwNeedItem
                        case 20: entry.ExhaustItem = ReadAsUInt(reader, colTypes[c]); break; // dwExhaustItem
                        case 21: entry.CastTime = ReadAsInt(reader, colTypes[c]); break;     // iCastTime
                        case 22: entry.ReCastTime = ReadAsInt(reader, colTypes[c]); break;   // iReCastTime
                        case 23: entry.IDK0 = ReadAsFloat(reader, colTypes[c]); break;       // fIDK0
                        case 24: entry.IDK1 = ReadAsFloat(reader, colTypes[c]); break;       // fIDK1
                        case 25: entry.PercentSuccess = ReadAsInt(reader, colTypes[c]); break;// iPercentSuccess
                        case 26: entry.FirstTableType = ReadAsUInt(reader, colTypes[c]); break;// dw1stTableType
                        case 27: entry.SecondTableType = ReadAsUInt(reader, colTypes[c]); break;// dw2ndTableType
                        case 28: entry.ValidDist = ReadAsInt(reader, colTypes[c]); break;    // iValidDist
                        case 29: entry.IDK2 = ReadAsInt(reader, colTypes[c]); break;         // iIDK2
                        default: SkipColumn(reader, colTypes[c]); break;
                    }
                }

                if (entry.Id > 0)
                    _skillTable[entry.Id] = entry;
            }

            IsLoaded = _skillTable.Count > 0;
        }

        /// <summary>
        /// Open-KO birebir: s_pTbl_Skill.Find(dwMagicID)
        /// MagicSkillMng.cpp:1884 — __TABLE_UPC_SKILL* pSkill = s_pTbl_Skill.Find(dwMagicID);
        /// </summary>
        public static SkillEntry Find(int skillId)
        {
            _skillTable.TryGetValue(skillId, out var entry);
            if (entry != null && (skillId == 108802 || skillId == 208802))
            {
                entry.SelfAnimID2 = -1; // Bitiş animasyonu tetiklemeyerek cast animasyonunun sonuna kadar oynamasını sağla
            }
            return entry;
        }

        /// <summary>
        /// Consumable item ID'sine (ExhaustItem) karşılık gelen skilli bulur.
        /// </summary>
        public static SkillEntry FindByExhaustItem(uint itemId)
        {
            foreach (var kvp in _skillTable)
            {
                if (kvp.Value.ExhaustItem == itemId)
                    return kvp.Value;
            }
            return null;
        }

        /// <summary>
        /// Open-KO birebir: UISkillTreeDlg::InitIconUpdate() satır 1228-1249
        /// iSkillIDFirst = eClass * 1000 + 1
        /// dwID / 1000 == eClass olan tüm skill'leri döndürür.
        /// C++: for (i = iSkillIndexFirst; i < iSkillIndexLast; i++)
        ///        pUSkill = s_pTbl_Skill.GetIndexedData(i);
        ///        if (pUSkill->dwID / 1000 != iSkillIDFirst / 1000) break;
        /// </summary>
        public static SkillEntry[] GetSkillsForClass(int classCode)
        {
            var result = new List<SkillEntry>();
            foreach (var kvp in _skillTable)
            {
                // cpp:1242-1243: iDivide = pUSkill->dwID / 1000;
                // if (iDivide != (iSkillIDFirst / 1000)) break;
                if (kvp.Value.Id / 1000 == classCode)
                    result.Add(kvp.Value);
            }
            return result.ToArray();
        }

        // ================================================================
        // TBL column readers
        // ================================================================

        private static int ReadAsInt(BinaryReader reader, int colType)
        {
            switch (colType)
            {
                case 1: return reader.ReadByte();           // DT_BYTE
                case 2: return reader.ReadInt16();          // DT_SHORT
                case 3: return reader.ReadSByte();          // DT_CHAR
                case 4: return reader.ReadUInt16();         // DT_WORD
                case 5: return reader.ReadInt32();          // DT_INT
                case 6: return (int)reader.ReadUInt32();    // DT_DWORD
                case 8: return (int)reader.ReadSingle();    // DT_FLOAT
                default:
                    SkipColumn(reader, colType);
                    return 0;
            }
        }

        private static uint ReadAsUInt(BinaryReader reader, int colType)
        {
            switch (colType)
            {
                case 1: return reader.ReadByte();
                case 2: return (uint)reader.ReadInt16();
                case 4: return reader.ReadUInt16();
                case 5: return (uint)reader.ReadInt32();
                case 6: return reader.ReadUInt32();
                default:
                    SkipColumn(reader, colType);
                    return 0;
            }
        }

        private static float ReadAsFloat(BinaryReader reader, int colType)
        {
            switch (colType)
            {
                case 5: return reader.ReadInt32();
                case 6: return reader.ReadUInt32();
                case 8: return reader.ReadSingle();
                case 9: return (float)reader.ReadDouble();
                default:
                    SkipColumn(reader, colType);
                    return 0f;
            }
        }

        private static string ReadString(BinaryReader reader, int colType)
        {
            if (colType != 7)
            {
                SkipColumn(reader, colType);
                return "";
            }
            int len = reader.ReadInt32();
            if (len > 0 && len < 100000)
            {
                byte[] strBytes = reader.ReadBytes(len);
                return Encoding.ASCII.GetString(strBytes).TrimEnd('\0');
            }
            return "";
        }

        /// <summary>
        /// Open-KO: N3TableBaseImpl.cpp column skip — type-aware
        /// </summary>
        private static void SkipColumn(BinaryReader reader, int colType)
        {
            switch (colType)
            {
                case 1: reader.ReadByte(); break;       // DT_BYTE
                case 2: reader.ReadInt16(); break;      // DT_SHORT
                case 3: reader.ReadSByte(); break;      // DT_CHAR
                case 4: reader.ReadUInt16(); break;     // DT_WORD
                case 5: reader.ReadInt32(); break;      // DT_INT
                case 6: reader.ReadUInt32(); break;     // DT_DWORD
                case 7:                                  // DT_STRING
                {
                    int len = reader.ReadInt32();
                    if (len > 0 && len < 100000)
                        reader.ReadBytes(len);
                    break;
                }
                case 8: reader.ReadSingle(); break;     // DT_FLOAT
                case 9: reader.ReadDouble(); break;     // DT_DOUBLE
            }
        }

        /// <summary>
        /// Open-KO rolling key decrypt — N3TableBaseImpl.cpp:51-77 birebir.
        /// Public wrapper — Type2/Type4 parser'lar da kullanır.
        /// </summary>
        public static byte[] DecryptTblPublic(byte[] encrypted) => DecryptTbl(encrypted);

        /// <summary>
        /// Open-KO rolling key decrypt — N3TableBaseImpl.cpp:51-77 birebir.
        /// </summary>
        private static byte[] DecryptTbl(byte[] encrypted)
        {
            ushort key_r  = 0x0816;
            ushort key_c1 = 0x6081;
            ushort key_c2 = 0x1608;

            byte[] decrypted = new byte[encrypted.Length];
            for (int i = 0; i < encrypted.Length; i++)
            {
                decrypted[i] = (byte)(encrypted[i] ^ (key_r >> 8));
                key_r = (ushort)((encrypted[i] + key_r) * key_c1 + key_c2);
            }
            return decrypted;
        }
    }

    /// <summary>
    /// Open-KO birebir: __TABLE_UPC_SKILL (GameDef.h:1057-1096)
    /// 30 kolonluk skill entry.
    /// </summary>
    public class SkillEntry
    {
        /// <summary>01 dwID — Skill ID</summary>
        public int Id;
        /// <summary>02 szEngName — English name</summary>
        public string EngName = "";
        /// <summary>03 szName — Display name</summary>
        public string Name = "";
        /// <summary>04 szDesc — Description</summary>
        public string Desc = "";
        /// <summary>05 iSelfAnimID1 — Start animation (caster)</summary>
        public int SelfAnimID1 = -1;
        /// <summary>06 iSelfAnimID2 — End animation (caster)</summary>
        public int SelfAnimID2 = -1;
        /// <summary>07 idwTargetAnimID — Target animation</summary>
        public int TargetAnimID;
        /// <summary>08 iSelfFX1 — Effect on caster (1)</summary>
        public int SelfFX1;
        /// <summary>09 iSelfPart1 — Effect position for iSelfFX1</summary>
        public int SelfPart1;
        /// <summary>10 iSelfFX2 — Effect on caster (2)</summary>
        public int SelfFX2;
        /// <summary>11 iSelfPart2 — Effect position for iSelfFX2</summary>
        public int SelfPart2;
        /// <summary>12 iFlyingFX — Flying effect</summary>
        public int FlyingFX;
        /// <summary>13 iTargetFX — Target effect</summary>
        public int TargetFX;
        /// <summary>14 iTargetPart — Effect position for iTargetFX</summary>
        public int TargetPart;
        /// <summary>15 iTarget — Target type/"moral"</summary>
        public int Target;
        /// <summary>16 iNeedLevel — Required player level</summary>
        public int NeedLevel;
        /// <summary>17 iNeedSkill — Required skill</summary>
        public int NeedSkill;
        /// <summary>18 iExhaustMSP — MSP consumed</summary>
        public int ExhaustMSP;
        /// <summary>19 iExhaustHP — HP consumed</summary>
        public int ExhaustHP;
        /// <summary>20 dwNeedItem — Required item</summary>
        public uint NeedItem;
        /// <summary>21 dwExhaustItem — Item consumed</summary>
        public uint ExhaustItem;
        /// <summary>22 iCastTime — Cast time (x10 ms)</summary>
        public int CastTime;
        /// <summary>23 iReCastTime — Cooldown time (x10 ms)</summary>
        public int ReCastTime;
        /// <summary>24 fIDK0 — Unknown</summary>
        public float IDK0;
        /// <summary>25 fIDK1 — Unknown</summary>
        public float IDK1;
        /// <summary>26 iPercentSuccess — Success rate</summary>
        public int PercentSuccess;
        /// <summary>27 dw1stTableType — Primary skill type</summary>
        public uint FirstTableType;
        /// <summary>28 dw2ndTableType — Secondary skill type</summary>
        public uint SecondTableType;
        /// <summary>29 iValidDist — Effective skill range</summary>
        public int ValidDist;
        /// <summary>30 iIDK2 — Unknown</summary>
        public int IDK2;
    }
}
