using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EntropyOnline.Import;
using UnityEngine;

namespace KOImport
{
    /// <summary>
    /// Open-KO CGameBase::s_pTbl_Items_Basic + s_pTbl_Items_Exts karşılığı.
    /// Client-side item veri yöneticisi.
    /// 
    /// Item_Org_us.tbl → __TABLE_ITEM_BASIC (byExtIndex)
    /// Item_Ext_0..23_us.tbl → __TABLE_ITEM_EXT (siAttackIntervalPercentage, vb.)
    /// 
    /// Open-KO attack interval formülü (PlayerMySelf.cpp:221-223):
    ///   fIntervalTable = (siAttackInterval / 100.0f) * (siAttackIntervalPercentage / 100.0f)
    /// 
    /// Open-KO ext lookup (GameProcMain.cpp:2022-2023):
    ///   pItemExt = s_pTbl_Items_Exts[pItem->byExtIndex].Find(dwItemID % 1000)
    /// </summary>
    public static class ItemDataManager
    {
        private const int MAX_ITEM_EXTENSION = 24;
        
        /// <summary>
        /// __TABLE_ITEM_BASIC'ten sadece byExtIndex. (GameDef.h:828)
        /// Key = dwID (item num), Value = byExtIndex
        /// </summary>
        private static Dictionary<int, byte> s_ExtIndexMap = new();
        
        /// <summary>
        /// __TABLE_ITEM_EXT verileri. (GameDef.h:880-953)
        /// Key = (extIndex, extRowId), Value = ItemExtEntry
        /// Open-KO: s_pTbl_Items_Exts[MAX_ITEM_EXTENSION]
        /// </summary>
        private static Dictionary<long, ItemExtEntry> s_ExtEntries = new();
        
        /// <summary>Veriler yüklendi mi?</summary>
        public static bool IsLoaded { get; private set; }
        
        /// <summary>
        /// Tüm item tablolarını yükler. Uygulama başlangıcında 1 kez çağrılır.
        /// Open-KO: CGameBase::LoadItemBasic() + Item_Ext loop (GameBase.cpp:59-80)
        /// </summary>
        public static void LoadAll(string koDataDir)
        {
            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<EntropyOnline.Import.KOItemDataAsset>("KOData/ItemData");
            if (asset != null)
            {
                s_ExtIndexMap.Clear();
                s_ItemBasicMap.Clear();
                s_ExtEntries.Clear();

                if (asset.basicItems != null)
                {
                    foreach (var b in asset.basicItems)
                    {
                        if (b == null || b.ItemNum <= 0) continue;
                        s_ExtIndexMap[b.ItemNum] = b.ExtIndex;
                        s_ItemBasicMap[b.ItemNum] = new ItemBasicEntry
                        {
                            DwID = (uint)b.ItemNum, ByExtIndex = b.ExtIndex,
                            SzName = (b.Name ?? "").Trim(), SzRemark = (b.Remark ?? "").Trim(),
                            DwIDK0 = b.IDK0, ByIDK1 = b.IDK1,
                            DwIDResrc = b.IDResrc, DwIDIcon = b.IDIcon,
                            DwSoundID0 = b.SoundID0, DwSoundID1 = b.SoundID1,
                            ByClass = b.Class, ByIsRobeType = b.IsRobeType,
                            ByAttachPoint = b.AttachPoint, ByNeedRace = b.NeedRace,
                            ByNeedClass = b.NeedClass, SiDamage = b.Damage,
                            SiAttackInterval = b.AttackInterval, SiAttackRange = b.AttackRange,
                            SiWeight = b.Weight, SiMaxDurability = b.MaxDurability,
                            IPrice = b.Price, ISaleType = b.SaleType,
                            SiDefense = b.Defense, ByContable = b.Contable,
                            DwEffectID1 = b.EffectID1, DwEffectID2 = b.EffectID2,
                            CNeedLevel = b.NeedLevel, CIDK2 = b.IDK2,
                            ByNeedRank = b.NeedRank, ByNeedTitle = b.NeedTitle,
                            ByNeedStrength = b.NeedStrength, ByNeedStamina = b.NeedStamina,
                            ByNeedDexterity = b.NeedDexterity, ByNeedInteli = b.NeedInteli,
                            ByNeedMagicAttack = b.NeedMagicAttack,
                            BySellGroup = b.SellGroup, ByGrade = b.Grade
                        };
                    }
                }

                if (asset.extGroups != null)
                {
                    foreach (var grp in asset.extGroups)
                    {
                        if (grp?.items == null) continue;
                        foreach (var ext in grp.items)
                        {
                            if (ext == null) continue;
                            long key = ((long)grp.extIndex << 32) | (uint)ext.DwID;
                            s_ExtEntries[key] = new ItemExtEntry
                            {
                                DwID = (int)ext.DwID,
                                SzHeader = (ext.SzHeader ?? "").Trim(),
                                DwBaseID = ext.DwBaseID,
                                SzRemark = (ext.SzRemark ?? "").Trim(),
                                DwIDK0 = ext.DwIDK0,
                                DwIDResrc = ext.DwIDResrc,
                                DwIDIcon = ext.DwIDIcon,
                                ByMagicOrRare = ext.ByMagicOrRare,
                                Damage = ext.Damage,
                                AttackIntervalPct = ext.AttackIntervalPercentage,
                                HitRate = ext.HitRate,
                                EvasionRate = ext.EvasionRate,
                                SiMaxDurability = ext.SiMaxDurability,
                                SiPriceMultiply = ext.SiPriceMultiply,
                                SiDefense = ext.SiDefense,
                                SiDefenseRateDagger = ext.SiDefenseRateDagger,
                                SiDefenseRateSword = ext.SiDefenseRateSword,
                                SiDefenseRateBlow = ext.SiDefenseRateBlow,
                                SiDefenseRateAxe = ext.SiDefenseRateAxe,
                                SiDefenseRateSpear = ext.SiDefenseRateSpear,
                                SiDefenseRateArrow = ext.SiDefenseRateArrow,
                                ByDamageFire = ext.ByDamageFire,
                                ByDamageIce = ext.ByDamageIce,
                                ByDamageThuner = ext.ByDamageThuner,
                                ByDamagePoison = ext.ByDamagePoison,
                                ByStillHP = ext.ByStillHP,
                                ByDamageMP = ext.ByDamageMP,
                                ByStillMP = ext.ByStillMP,
                                ByReturnPhysicalDamage = ext.ByReturnPhysicalDamage,
                                BySoulBind = ext.BySoulBind,
                                SiBonusStr = ext.SiBonusStr,
                                SiBonusSta = ext.SiBonusSta,
                                SiBonusDex = ext.SiBonusDex,
                                SiBonusInt = ext.SiBonusInt,
                                SiBonusMagicAttak = ext.SiBonusMagicAttak,
                                SiBonusHP = ext.SiBonusHP,
                                SiBonusMSP = ext.SiBonusMSP,
                                SiRegistFire = ext.SiRegistFire,
                                SiRegistIce = ext.SiRegistIce,
                                SiRegistElec = ext.SiRegistElec,
                                SiRegistMagic = ext.SiRegistMagic,
                                SiRegistPoison = ext.SiRegistPoison,
                                SiRegistCurse = ext.SiRegistCurse,
                                DwEffectID1 = ext.DwEffectID1,
                                DwEffectID2 = ext.DwEffectID2,
                                SiNeedLevel = ext.SiNeedLevel,
                                SiNeedRank = ext.SiNeedRank,
                                SiNeedTitle = ext.SiNeedTitle,
                                SiNeedStrength = ext.SiNeedStrength,
                                SiNeedStamina = ext.SiNeedStamina,
                                SiNeedDexterity = ext.SiNeedDexterity,
                                SiNeedInteli = ext.SiNeedInteli,
                                SiNeedMagicAttack = ext.SiNeedMagicAttack,
                            };
                        }
                    }
                }

                IsLoaded = true;
                return;
            }

            // 1. Item_Org_us.tbl → byExtIndex map
            string basicPath = Path.Combine(koDataDir, "Item_Org_us.tbl");
            s_ExtIndexMap = LoadExtIndexMap(basicPath);
            
            // 2. Item_Ext_0..23_us.tbl → ext entries
            // Open-KO: GameBase.cpp:72-79
            s_ExtEntries.Clear();
            for (int i = 0; i < MAX_ITEM_EXTENSION; i++)
            {
                string extPath = Path.Combine(koDataDir, $"Item_Ext_{i}_us.tbl");
                var entries = LoadExtTable(extPath, i);
                foreach (var kvp in entries)
                {
                    s_ExtEntries[kvp.Key] = kvp.Value;
                }
            }
            
            IsLoaded = true;
            
            // DEBUG: sellGroup dağılımını logla
            var sgCounts = new Dictionary<byte, int>();
            foreach (var kvp in s_ItemBasicMap)
            {
                byte sg = kvp.Value.BySellGroup;
                if (!sgCounts.ContainsKey(sg)) sgCounts[sg] = 0;
                sgCounts[sg]++;
            }
            var sgList = new List<string>();
            foreach (var kvp in sgCounts)
                sgList.Add($"{kvp.Key}:{kvp.Value}");
            sgList.Sort();
        }
        
        /// <summary>
        /// Verilen item num için siAttackIntervalPercentage değerini döndürür.
        /// Open-KO birebir: s_pTbl_Items_Exts[pItem->byExtIndex].Find(dwItemID % 1000)
        /// </summary>
        public static short GetAttackIntervalPct(int itemNum)
        {
            if (!IsLoaded) return 100;
            
            // Adım 1: byExtIndex bul
            // Open-KO: pItem->byExtIndex
            byte extIndex;
            if (!s_ExtIndexMap.TryGetValue(itemNum, out extIndex))
            {
                // Doğrudan bulunamadı → aynı prefix'teki base item'ı ara
                // Open-KO'da her item num __TABLE_ITEM_BASIC'te, ama bizde
                // sadece .tbl'deki 807 base item var. Aynı item ailesinden bul.
                int prefix = itemNum / 1000;
                bool found = false;
                foreach (var kvp in s_ExtIndexMap)
                {
                    if (kvp.Key / 1000 == prefix)
                    {
                        extIndex = kvp.Value;
                        found = true;
                        break;
                    }
                }
                if (!found) return 100;
            }
            
            // Adım 2: ext tablosunda lookup
            // Open-KO: s_pTbl_Items_Exts[byExtIndex].Find(dwItemID % 1000)
            int extRowId = itemNum % 1000;
            long key = ((long)extIndex << 32) | (uint)extRowId;
            
            if (s_ExtEntries.TryGetValue(key, out var entry))
            {
                return entry.AttackIntervalPct;
            }
            
            return 100;
        }
        
        /// <summary>
        /// Verilen item num için tüm ext bilgisini döndürür.
        /// </summary>
        public static ItemExtEntry GetItemExt(int itemNum)
        {
            if (!IsLoaded) return null;
            
            byte extIndex;
            if (!s_ExtIndexMap.TryGetValue(itemNum, out extIndex))
            {
                int prefix = itemNum / 1000;
                bool found = false;
                foreach (var kvp in s_ExtIndexMap)
                {
                    if (kvp.Key / 1000 == prefix)
                    {
                        extIndex = kvp.Value;
                        found = true;
                        break;
                    }
                }
                if (!found) return null;
            }
            
            int extRowId = itemNum % 1000;
            long key = ((long)extIndex << 32) | (uint)extRowId;
            s_ExtEntries.TryGetValue(key, out var entry);
            return entry;
        }
        
        #region TBL Parsers
        
        /// <summary>
        /// Open-KO: __TABLE_ITEM_BASIC (GameDef.h:821-874) — 37 kolon.
        /// s_pTbl_Items_Basic karşılığı. Client item metadata lookup için kullanılır.
        /// Key = dwID (item num)
        /// </summary>
        private static Dictionary<int, ItemBasicEntry> s_ItemBasicMap = new();
        
        /// <summary>
        /// Verilen itemID için __TABLE_ITEM_BASIC kaydını döndürür.
        /// Open-KO birebir: s_pTbl_Items_Basic.Find(dwItemID / 1000 * 1000)
        /// </summary>
        public static ItemBasicEntry GetItemBasic(int itemNum)
        {
            if (!IsLoaded) return null;
            
            // Önce doğrudan bul
            if (s_ItemBasicMap.TryGetValue(itemNum, out var entry))
                return entry;
            
            // Bulunamadıysa, base item'ı dene (C++: dwItemID / 1000 * 1000)
            int baseId = (itemNum / 1000) * 1000;
            s_ItemBasicMap.TryGetValue(baseId, out entry);
            return entry;
        }

        public static byte GetClassByResourceID(uint resID)
        {
            if (!IsLoaded) return 0;
            foreach (var b in s_ItemBasicMap.Values)
            {
                if (b.DwIDResrc == resID)
                    return b.ByClass;
            }
            return 0;
        }
        
        /// <summary>
        /// Item_Org_us.tbl → __TABLE_ITEM_BASIC (GameDef.h:821-874) 37 kolon parser.
        /// Hem byExtIndex map'i hem de tam ItemBasicEntry map'i doldurur.
        /// </summary>
        private static Dictionary<int, byte> LoadExtIndexMap(string tblPath)
        {
            var map = new Dictionary<int, byte>();
            s_ItemBasicMap.Clear();
            
            byte[] raw = KOTableProvider.LoadRaw(tblPath);
            if (raw == null)
            {
                Debug.LogWarning($"[ItemDataManager] Item_Org_us.tbl bulunamadı: {tblPath}");
                return map;
            }
            
            byte[] decrypted = DecryptTbl(raw);
            using var ms = new MemoryStream(decrypted);
            using var reader = new BinaryReader(ms, Encoding.ASCII);
            
            int columnCount = reader.ReadInt32();
            
            // TBL_DATA_TYPE enum (N3TableBaseImpl.h:9-21)
            var colTypes = new int[columnCount];
            for (int c = 0; c < columnCount; c++)
                colTypes[c] = reader.ReadInt32();
            
            int rowCount = reader.ReadInt32();
            
            for (int r = 0; r < rowCount; r++)
            {
                var basic = new ItemBasicEntry();
                
                // Her kolonu oku ve ilgili alana ata
                // __TABLE_ITEM_BASIC — GameDef.h:821-874 birebir kolon sırası
                int colIdx = 0;
                for (int c = 0; c < columnCount; c++)
                {
                    switch (colTypes[c])
                    {
                        case 1: case 2: // DT_CHAR, DT_BYTE
                        {
                            byte val = reader.ReadByte();
                            switch (colIdx)
                            {
                                case 1: basic.ByExtIndex = val; break;       // 02 byExtIndex
                                case 5: basic.ByIDK1 = val; break;          // 06 byIDK1
                                case 10: basic.ByClass = val; break;        // 11 byClass (e_ItemClass)
                                case 11: basic.ByIsRobeType = val; break;   // 12 byIsRobeType
                                case 12: basic.ByAttachPoint = val; break;  // 13 byAttachPoint (equip slot)
                                case 13: basic.ByNeedRace = val; break;     // 14 byNeedRace
                                case 14: basic.ByNeedClass = val; break;    // 15 byNeedClass
                                case 23: basic.ByContable = val; break;     // 24 byContable
                                case 26: basic.CNeedLevel = (sbyte)val; break; // 27 cNeedLevel
                                case 27: basic.CIDK2 = (sbyte)val; break;   // 28 cIDK2
                                case 28: basic.ByNeedRank = val; break;     // 29 byNeedRank
                                case 29: basic.ByNeedTitle = val; break;    // 30 byNeedTitle
                                case 30: basic.ByNeedStrength = val; break; // 31 byNeedStrength
                                case 31: basic.ByNeedStamina = val; break;  // 32 byNeedStamina
                                case 32: basic.ByNeedDexterity = val; break;// 33 byNeedDexterity
                                case 33: basic.ByNeedInteli = val; break;   // 34 byNeedInteli
                                case 34: basic.ByNeedMagicAttack = val; break; // 35 byNeedMagicAttack
                                case 35: basic.BySellGroup = val; break;    // 36 bySellGroup
                                case 36: basic.ByGrade = val; break;        // 37 byGrade
                            }
                            break;
                        }
                        case 3: // DT_SHORT
                        {
                            short val = reader.ReadInt16();
                            switch (colIdx)
                            {
                                case 15: basic.SiDamage = val; break;           // 16 siDamage
                                case 16: basic.SiAttackInterval = val; break;   // 17 siAttackInterval
                                case 17: basic.SiAttackRange = val; break;      // 18 siAttackRange
                                case 18: basic.SiWeight = val; break;           // 19 siWeight
                                case 19: basic.SiMaxDurability = val; break;    // 20 siMaxDurability
                                case 22: basic.SiDefense = val; break;          // 23 siDefense
                            }
                            break;
                        }
                        case 4: reader.ReadUInt16(); break; // DT_WORD
                        case 5: // DT_INT
                        {
                            int val = reader.ReadInt32();
                            switch (colIdx)
                            {
                                case 0: basic.DwID = (uint)val; break;       // 01 dwID
                                case 4: basic.DwIDK0 = (uint)val; break;    // 05 dwIDK0
                                case 6: basic.DwIDResrc = (uint)val; break; // 07 dwIDResrc
                                case 7: basic.DwIDIcon = (uint)val; break;  // 08 dwIDIcon
                                case 8: basic.DwSoundID0 = (uint)val; break;// 09 dwSoundID0
                                case 9: basic.DwSoundID1 = (uint)val; break;// 10 dwSoundID1
                                case 20: basic.IPrice = val; break;         // 21 iPrice
                                case 21: basic.ISaleType = val; break;      // 22 iSaleType
                                case 24: basic.DwEffectID1 = (uint)val; break; // 25 dwEffectID1
                                case 25: basic.DwEffectID2 = (uint)val; break; // 26 dwEffectID2
                            }
                            break;
                        }
                        case 6: // DT_DWORD
                        {
                            uint val = reader.ReadUInt32();
                            switch (colIdx)
                            {
                                case 0: basic.DwID = val; break;
                                case 4: basic.DwIDK0 = val; break;
                                case 6: basic.DwIDResrc = val; break;
                                case 7: basic.DwIDIcon = val; break;
                                case 8: basic.DwSoundID0 = val; break;
                                case 9: basic.DwSoundID1 = val; break;
                                case 24: basic.DwEffectID1 = val; break;
                                case 25: basic.DwEffectID2 = val; break;
                            }
                            break;
                        }
                        case 7: // DT_STRING
                        {
                            int len = reader.ReadInt32();
                            string val = "";
                            if (len > 0 && len < 10000)
                            {
                                byte[] strBytes = reader.ReadBytes(len);
                                val = Encoding.ASCII.GetString(strBytes);
                            }
                            switch (colIdx)
                            {
                                case 2: basic.SzName = val; break;     // 03 szName
                                case 3: basic.SzRemark = val; break;   // 04 szRemark
                            }
                            break;
                        }
                        case 8: reader.ReadSingle(); break; // DT_FLOAT
                        case 9: reader.ReadDouble(); break; // DT_DOUBLE
                    }
                    colIdx++;
                }
                
                if (basic.DwID > 0)
                {
                    // Enion Bow (169101000) base item interception
                    if (basic.DwID / 1000 * 1000 == 169101000)
                    {
                        basic.DwIDResrc = 16910000;
                        basic.DwIDIcon = 16910000;
                    }

                    // Horn Bow (160650000 / 160660000) base item interception
                    if (basic.DwID / 1000 * 1000 == 160650000 || basic.DwID / 1000 * 1000 == 160660000)
                    {
                        basic.DwIDResrc = 16211000;
                        basic.DwIDIcon = 16211000;
                    }


                    map[(int)basic.DwID] = basic.ByExtIndex;
                    s_ItemBasicMap[(int)basic.DwID] = basic;
                }
            }
            
            return map;
        }
        
        /// <summary>
        /// Item_Ext_{n}_us.tbl → ext entries.
        /// __TABLE_ITEM_EXT (GameDef.h:880-953) — 53 kolon.
        /// </summary>
        private static Dictionary<long, ItemExtEntry> LoadExtTable(string tblPath, int extIndex)
        {
            var result = new Dictionary<long, ItemExtEntry>();
            byte[] raw = KOTableProvider.LoadRaw(tblPath);
            if (raw == null) return result;
            
            byte[] decrypted = DecryptTbl(raw);
            using var ms = new MemoryStream(decrypted);
            using var reader = new BinaryReader(ms, Encoding.ASCII);
            
            int columnCount = reader.ReadInt32();
            if (columnCount != 53) return result;
            
            var colTypes = new int[columnCount];
            for (int c = 0; c < columnCount; c++)
                colTypes[c] = reader.ReadInt32();
            
            int rowCount = reader.ReadInt32();
            
            for (int r = 0; r < rowCount; r++)
            {
                var entry = new ItemExtEntry();
                int colIdx = 0;
                
                for (int c = 0; c < columnCount; c++)
                {
                    switch (colTypes[c])
                    {
                        case 1: case 2: // DT_CHAR, DT_BYTE
                        {
                            byte val = reader.ReadByte();
                            SetExtByte(entry, colIdx, val);
                            break;
                        }
                        case 3: // DT_SHORT
                        {
                            short val = reader.ReadInt16();
                            SetExtShort(entry, colIdx, val);
                            break;
                        }
                        case 4: reader.ReadUInt16(); break;
                        case 5: // DT_INT
                        {
                            int val = reader.ReadInt32();
                            SetExtInt(entry, colIdx, val);
                            break;
                        }
                        case 6: // DT_DWORD
                        {
                            uint val = reader.ReadUInt32();
                            SetExtInt(entry, colIdx, (int)val);
                            break;
                        }
                        case 7: // DT_STRING
                        {
                            int len = reader.ReadInt32();
                            string val = "";
                            if (len > 0 && len < 10000)
                            {
                                byte[] strBytes = reader.ReadBytes(len);
                                val = Encoding.ASCII.GetString(strBytes);
                            }
                            // __TABLE_ITEM_EXT string kolonları:
                            // kolon 1 = szHeader (02), kolon 3 = szRemark (04)
                            switch (colIdx)
                            {
                                case 1: entry.SzHeader = val; break;
                                case 3: entry.SzRemark = val; break;
                            }
                            break;
                        }
                        case 8: reader.ReadSingle(); break;
                        case 9: reader.ReadDouble(); break;
                    }
                    colIdx++;
                }
                
                if (entry.DwID >= 0)
                {
                    long key = ((long)extIndex << 32) | (uint)entry.DwID;
                    result[key] = entry;
                }
            }
            
            return result;
        }
        
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
        
        // __TABLE_ITEM_EXT kolon eşleme — GameDef.h:880-953 birebir sıra (53 kolon)
        private static void SetExtInt(ItemExtEntry e, int col, int val)
        {
            switch (col)
            {
                case 0: e.DwID = val; break;             // 01 dwID
                // case 1: szHeader → string handler'da   // 02 szHeader
                case 2: e.DwBaseID = val; break;          // 03 dwBaseID
                // case 3: szRemark → string handler'da    // 04 szRemark
                case 4: e.DwIDK0 = val; break;            // 05
                case 5: 
                    e.DwIDResrc = val; 
                    if (e.DwBaseID == 160450000 && e.DwIDResrc == 16841000)
                    {
                        e.DwIDResrc = 16121000;
                    }
                    break;         // 06 dwIDResrc
                case 6: 
                    e.DwIDIcon = val; 
                    if (e.DwBaseID == 160450000 && e.DwIDIcon == 16841000)
                    {
                        e.DwIDIcon = 16121000;
                    }
                    break;          // 07 dwIDIcon
                case 43: e.DwEffectID1 = val; break;      // 44 dwEffectID1
                case 44: e.DwEffectID2 = val; break;      // 45 dwEffectID2
            }
        }
        
        private static void SetExtShort(ItemExtEntry e, int col, short val)
        {
            switch (col)
            {
                case 8: e.Damage = val; break;                    // 09 siDamage
                case 9: e.AttackIntervalPct = val; break;         // 10 siAttackIntervalPercentage
                case 10: e.HitRate = val; break;                  // 11 siHitRate
                case 11: e.EvasionRate = val; break;              // 12 siEvationRate
                case 12: e.SiMaxDurability = val; break;          // 13 siMaxDurability
                case 13: e.SiPriceMultiply = val; break;          // 14 siPriceMultiply
                case 14: e.SiDefense = val; break;                // 15 siDefense
                case 15: e.SiDefenseRateDagger = val; break;      // 16
                case 16: e.SiDefenseRateSword = val; break;       // 17
                case 17: e.SiDefenseRateBlow = val; break;        // 18
                case 18: e.SiDefenseRateAxe = val; break;         // 19
                case 19: e.SiDefenseRateSpear = val; break;       // 20
                case 20: e.SiDefenseRateArrow = val; break;       // 21
                case 30: e.SiBonusStr = val; break;               // 31
                case 31: e.SiBonusSta = val; break;               // 32
                case 32: e.SiBonusDex = val; break;               // 33
                case 33: e.SiBonusInt = val; break;               // 34
                case 34: e.SiBonusMagicAttak = val; break;        // 35
                case 35: e.SiBonusHP = val; break;                // 36
                case 36: e.SiBonusMSP = val; break;               // 37
                case 37: e.SiRegistFire = val; break;             // 38
                case 38: e.SiRegistIce = val; break;              // 39
                case 39: e.SiRegistElec = val; break;             // 40
                case 40: e.SiRegistMagic = val; break;            // 41
                case 41: e.SiRegistPoison = val; break;           // 42
                case 42: e.SiRegistCurse = val; break;            // 43
                case 45: e.SiNeedLevel = val; break;              // 46
                case 46: e.SiNeedRank = val; break;               // 47
                case 47: e.SiNeedTitle = val; break;              // 48
                case 48: e.SiNeedStrength = val; break;           // 49
                case 49: e.SiNeedStamina = val; break;            // 50
                case 50: e.SiNeedDexterity = val; break;          // 51
                case 51: e.SiNeedInteli = val; break;             // 52
                case 52: e.SiNeedMagicAttack = val; break;        // 53
            }
        }
        
        private static void SetExtByte(ItemExtEntry e, int col, byte val)
        {
            switch (col)
            {
                case 7: e.ByMagicOrRare = val; break;             // 08 byMagicOrRare (e_ItemAttrib)
                case 21: e.ByDamageFire = val; break;             // 22
                case 22: e.ByDamageIce = val; break;              // 23
                case 23: e.ByDamageThuner = val; break;           // 24 (C++ typo birebir)
                case 24: e.ByDamagePoison = val; break;           // 25
                case 25: e.ByStillHP = val; break;                // 26 (steal HP)
                case 26: e.ByDamageMP = val; break;               // 27
                case 27: e.ByStillMP = val; break;                // 28 (steal MP)
                case 28: e.ByReturnPhysicalDamage = val; break;   // 29
                case 29: e.BySoulBind = val; break;               // 30
            }
        }
        
        /// <summary>
        /// Open-KO birebir: UITransactionDlg::EnterTransactionState (satır 305-369)
        /// tradeId'den sellGroup ve extRowId hesaplayarak, lokal item tablosundan
        /// o sellGroup'a ait tüm itemleri döndürür.
        /// Sunucuya paket gönderilmez — tamamen lokal veri.
        /// </summary>
        public static EntropyOnline.Network.ShopItemData[] GetShopItemsBySellGroup(int tradeId)
        {
            if (!IsLoaded) return null;

            // C++ birebir: UITransactionDlg.cpp satır 310-311
            int iOrg = tradeId / 1000;   // bySellGroup filtresi
            int iExt = tradeId % 1000;   // ext row ID

            var results = new List<EntropyOnline.Network.ShopItemData>();

            // C++ birebir: satır 316-368 — tüm basic item tablosunu tara
            int dbgSellMatch = 0, dbgExtIdxFail = 0, dbgExtLookupFail = 0, dbgExtIdFail = 0;
            foreach (var kvp in s_ItemBasicMap)
            {
                var basic = kvp.Value;

                // C++ satır 338: if (pItem->bySellGroup != iOrg) continue;
                if (basic.BySellGroup != iOrg)
                    continue;
                dbgSellMatch++;

                // C++ satır 335-336: if (pItem->byExtIndex < 0 || pItem->byExtIndex >= MAX_ITEM_EXTENSION) continue;
                if (basic.ByExtIndex >= MAX_ITEM_EXTENSION)
                {
                    dbgExtIdxFail++;
                    continue;
                }

                // C++ satır 341: pItemExt = s_pTbl_Items_Exts[pItem->byExtIndex].Find(iExt);
                long extKey = ((long)basic.ByExtIndex << 32) | (uint)iExt;
                if (!s_ExtEntries.TryGetValue(extKey, out var ext))
                {
                    dbgExtLookupFail++;
                    if (dbgExtLookupFail <= 3)
                        Debug.LogWarning($"[ItemDataManager] ExtLookup FAIL: '{basic.SzName}' dwID={basic.DwID} extIdx={basic.ByExtIndex} extKey={extKey} iExt={iExt}. s_ExtEntries has {s_ExtEntries.Count} entries.");
                    continue;
                }

                // C++ satır 349-350: if (pItemExt->dwID != iExt) continue;
                if (ext.DwID != iExt)
                {
                    dbgExtIdFail++;
                    continue;
                }

                // ShopItemData oluştur
                uint clientItemId = basic.DwID + (uint)iExt;
                uint serverItemId = KOItemMapping.GetServerItemId(clientItemId);
                int buyPrice = basic.IPrice;
                if (serverItemId != clientItemId)
                {
                    uint serverBaseId = serverItemId / 1000 * 1000;
                    if (s_ItemBasicMap.TryGetValue((int)serverBaseId, out var serverBasic))
                    {
                        buyPrice = serverBasic.IPrice;
                    }
                }

                var shopItem = new EntropyOnline.Network.ShopItemData
                {
                    ItemDefId = (int)clientItemId,
                    Name = basic.SzName ?? $"Item_{basic.DwID}",
                    BuyPrice = buyPrice,
                    IconId = basic.DwIDIcon.ToString(),
                    ByContable = basic.ByContable, // C++ pItemBasic->byContable (0=ONLYONE, 1=COUNTABLE, 2=COUNTABLE_SMALL)
                };

                results.Add(shopItem);
            }

            // DEBUG: Tablodaki unique sellGroup değerlerini logla
            if (results.Count == 0)
            {
                var sellGroups = new HashSet<byte>();
                foreach (var kvp2 in s_ItemBasicMap)
                    sellGroups.Add(kvp2.Value.BySellGroup);
                Debug.LogWarning($"[ItemDataManager] sellGroup={iOrg} bulunamadı! Tabloda {s_ItemBasicMap.Count} item, {sellGroups.Count} unique sellGroup var: [{string.Join(",", sellGroups)}]");
            }

            return results.ToArray();
        }
        
        #endregion
    }
    
    /// <summary>
    /// __TABLE_ITEM_EXT (GameDef.h:880-953) birebir — 53 kolon.
    /// CUIImageTooltipDlg tooltip sistemi için tüm alanlar gerekli.
    /// </summary>
    public class ItemExtEntry
    {
        // 01 dwID
        public int DwID;
        // 02 szHeader — unique item prefix ismi
        public string SzHeader = "";
        // 03 dwBaseID
        public int DwBaseID;
        // 04 szRemark — item açıklaması
        public string SzRemark = "";
        // 05
        public int DwIDK0;
        // 06 dwIDResrc
        public int DwIDResrc;
        // 07 dwIDIcon
        public int DwIDIcon;
        // 08 byMagicOrRare — e_ItemAttrib (GENERAL=0, MAGIC=1, LAIR=2, CRAFT=3, UNIQUE=4, UPGRADE=5)
        public byte ByMagicOrRare;
        // 09 siDamage
        public short Damage;
        // 10 siAttackIntervalPercentage
        public short AttackIntervalPct = 100;
        // 11 siHitRate
        public short HitRate;
        // 12 siEvationRate (C++ typo birebir)
        public short EvasionRate;
        // 13 siMaxDurability
        public short SiMaxDurability;
        // 14 siPriceMultiply
        public short SiPriceMultiply;
        // 15 siDefense
        public short SiDefense;
        // 16-21 silah tipine göre savunma oranları
        public short SiDefenseRateDagger;
        public short SiDefenseRateSword;
        public short SiDefenseRateBlow;
        public short SiDefenseRateAxe;
        public short SiDefenseRateSpear;
        public short SiDefenseRateArrow;
        // 22-25 elemental damage
        public byte ByDamageFire;
        public byte ByDamageIce;
        public byte ByDamageThuner;     // C++ typo birebir
        public byte ByDamagePoison;
        // 26-29 drain/special
        public byte ByStillHP;          // HP drain (steal HP)
        public byte ByDamageMP;         // MP damage
        public byte ByStillMP;          // MP drain (steal MP)
        public byte ByReturnPhysicalDamage; // physical damage reflection
        // 30 bySoulBind
        public byte BySoulBind;
        // 31-37 stat bonusları
        public short SiBonusStr;
        public short SiBonusSta;
        public short SiBonusDex;
        public short SiBonusInt;
        public short SiBonusMagicAttak;
        public short SiBonusHP;
        public short SiBonusMSP;
        // 38-43 elemental resistance
        public short SiRegistFire;
        public short SiRegistIce;
        public short SiRegistElec;
        public short SiRegistMagic;
        public short SiRegistPoison;
        public short SiRegistCurse;
        // 44-45 effect IDs
        public int DwEffectID1;
        public int DwEffectID2;
        // 46-53 gereksinimler
        public short SiNeedLevel;
        public short SiNeedRank;
        public short SiNeedTitle;
        public short SiNeedStrength;
        public short SiNeedStamina;
        public short SiNeedDexterity;
        public short SiNeedInteli;
        public short SiNeedMagicAttack;
    }
    
    /// <summary>
    /// __TABLE_ITEM_BASIC (GameDef.h:821-874) birebir C# karşılığı.
    /// Item_Org_us.tbl'den parse edilir.
    /// </summary>
    public class ItemBasicEntry
    {
        public uint DwID;              // 01 dwID
        public byte ByExtIndex;        // 02 byExtIndex
        public string SzName = "";     // 03 szName
        public string SzRemark = "";   // 04 szRemark
        public uint DwIDK0;            // 05 dwIDK0
        public byte ByIDK1;            // 06 byIDK1
        public uint DwIDResrc;         // 07 dwIDResrc
        public uint DwIDIcon;          // 08 dwIDIcon
        public uint DwSoundID0;        // 09 dwSoundID0
        public uint DwSoundID1;        // 10 dwSoundID1
        public byte ByClass;           // 11 byClass (e_ItemClass)
        public byte ByIsRobeType;      // 12 byIsRobeType
        public byte ByAttachPoint;     // 13 byAttachPoint (equip slot)
        public byte ByNeedRace;        // 14 byNeedRace
        public byte ByNeedClass;       // 15 byNeedClass
        public short SiDamage;         // 16 siDamage
        public short SiAttackInterval; // 17 siAttackInterval
        public short SiAttackRange;    // 18 siAttackRange
        public short SiWeight;         // 19 siWeight
        public short SiMaxDurability;  // 20 siMaxDurability
        public int IPrice;             // 21 iPrice
        public int ISaleType;          // 22 iSaleType
        public short SiDefense;        // 23 siDefense
        public byte ByContable;        // 24 byContable
        public uint DwEffectID1;       // 25 dwEffectID1
        public uint DwEffectID2;       // 26 dwEffectID2
        public sbyte CNeedLevel;       // 27 cNeedLevel
        public sbyte CIDK2;            // 28 cIDK2
        public byte ByNeedRank;        // 29 byNeedRank
        public byte ByNeedTitle;       // 30 byNeedTitle
        public byte ByNeedStrength;    // 31 byNeedStrength
        public byte ByNeedStamina;     // 32 byNeedStamina
        public byte ByNeedDexterity;   // 33 byNeedDexterity
        public byte ByNeedInteli;      // 34 byNeedInteli
        public byte ByNeedMagicAttack; // 35 byNeedMagicAttack
        public byte BySellGroup;       // 36 bySellGroup
        public byte ByGrade;           // 37 byGrade
    }
}
