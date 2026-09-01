using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO birebir: CN3TableBase + CN3TableBaseImpl binary .tbl parser
    ///
    /// C++ referans:
    ///   N3TableBaseImpl.cpp satır 19-119: LoadFromFile + XOR decrypt
    ///   N3TableBaseImpl.cpp satır 121-180: ReadData (type bazlı okuma)
    ///   N3TableBase.h satır 110-166: Load (column types + row okuma)
    ///   N3TableBaseImpl.h satır 9-21: TBL_DATA_TYPE enum
    ///
    /// Binary .tbl format:
    ///   1. Tüm dosya XOR ile şifreli (key_r=0x0816, key_c1=0x6081, key_c2=0x1608)
    ///   2. int32 columnCount
    ///   3. uint32[columnCount] — her sütunun DATA_TYPE'ı
    ///   4. int32 rowCount
    ///   5. Her satır: columnCount adet ReadData çağrısı
    ///      - DT_STRING: int32 len + char[len]
    ///      - Diğerleri: sabit boyut (1/2/4/8 byte)
    /// </summary>
    public static class KOTableReader
    {
        // ================================================
        // Open-KO birebir: TBL_DATA_TYPE enum (N3TableBaseImpl.h:9-21)
        // ================================================
        private enum DataType : uint
        {
            DT_NONE   = 0,
            DT_CHAR   = 1, // char (1 byte, signed)
            DT_BYTE   = 2, // uint8 (1 byte)
            DT_SHORT  = 3, // int16 (2 bytes)
            DT_WORD   = 4, // uint16 (2 bytes)
            DT_INT    = 5, // int32 (4 bytes)
            DT_DWORD  = 6, // uint32 (4 bytes)
            DT_STRING = 7, // int32 len + char[len]
            DT_FLOAT  = 8, // float (4 bytes)
            DT_DOUBLE = 9  // double (8 bytes)
        }

        // ================================================
        // Open-KO birebir: __TABLE_ITEM_BASIC struct (GameDef.h:821-874)
        // 37 sütun — .tbl binary formatından okunuyor
        // ================================================
        [System.Serializable]
        public class TableItemBasic
        {
            public uint dwID;               // 01
            public byte byExtIndex;          // 02
            public string szName = "";       // 03
            public string szRemark = "";     // 04
            public uint dwIDK0;              // 05
            public byte byIDK1;              // 06
            public uint dwIDResrc;           // 07
            public uint dwIDIcon;            // 08
            public uint dwSoundID0;          // 09
            public uint dwSoundID1;          // 10
            public byte byClass;             // 11
            public byte byIsRobeType;        // 12
            public byte byAttachPoint;       // 13
            public byte byNeedRace;          // 14
            public byte byNeedClass;         // 15
            public short siDamage;           // 16
            public short siAttackInterval;   // 17
            public short siAttackRange;      // 18
            public short siWeight;           // 19
            public short siMaxDurability;    // 20
            public int iPrice;               // 21
            public int iSaleType;            // 22
            public short siDefense;          // 23
            public byte byContable;          // 24
            public uint dwEffectID1;         // 25
            public uint dwEffectID2;         // 26
            public sbyte cNeedLevel;         // 27
            public sbyte cIDK2;              // 28
            public byte byNeedRank;          // 29
            public byte byNeedTitle;         // 30
            public byte byNeedStrength;      // 31
            public byte byNeedStamina;       // 32
            public byte byNeedDexterity;     // 33
            public byte byNeedInteli;        // 34
            public byte byNeedMagicAttack;   // 35
            public byte bySellGroup;         // 36
            public byte byGrade;             // 37
        }

        // ================================================
        // Open-KO birebir: LoadFromFile + XOR decrypt
        // C++ N3TableBaseImpl.cpp satır 19-119
        // ================================================
        private static byte[] DecryptTblFile(string filePath)
        {
            // Resources/KOData/ altından yükle
            return KOTableProvider.LoadDecryptedTbl(filePath);
        }

        /// <summary>Ham byte[] decrypt — public wrapper.</summary>
        public static byte[] DecryptTblPublic(byte[] encrypted) => DecryptTblBytes(encrypted);

        private static byte[] DecryptTblBytes(byte[] encrypted)
        {
            if (encrypted == null || encrypted.Length == 0) return null;

            // C++ satır 51-53: şifreleme anahtarları
            ushort key_r = 0x0816;
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

        // ================================================
        // Open-KO birebir: ReadData (N3TableBaseImpl.cpp satır 121-180)
        // ================================================
        private static object ReadData(BinaryReader reader, DataType type)
        {
            switch (type)
            {
                case DataType.DT_CHAR:   return reader.ReadSByte();
                case DataType.DT_BYTE:   return reader.ReadByte();
                case DataType.DT_SHORT:  return reader.ReadInt16();
                case DataType.DT_WORD:   return reader.ReadUInt16();
                case DataType.DT_INT:    return reader.ReadInt32();
                case DataType.DT_DWORD:  return reader.ReadUInt32();
                case DataType.DT_FLOAT:  return reader.ReadSingle();
                case DataType.DT_DOUBLE: return reader.ReadDouble();
                case DataType.DT_STRING:
                    // C++ satır 150-161: int32 len + char[len]
                    int len = reader.ReadInt32();
                    if (len <= 0) return "";
                    byte[] strBytes = reader.ReadBytes(len);
                    return Encoding.ASCII.GetString(strBytes);
                default:
                    Debug.LogError($"[KOTableReader] Bilinmeyen DataType: {type}");
                    return null;
            }
        }

        // ================================================
        // Open-KO birebir: Load (N3TableBase.h satır 110-166)
        // Item_Org_us.tbl → Dictionary<uint, TableItemBasic>
        // ================================================
        public static Dictionary<uint, TableItemBasic> LoadItemBasicTable(string filePath)
        {
            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<KOItemOrgAsset>("KOData/Item_Org_us");
            if (asset != null)
            {
                return asset.ToDictionary();
            }
                // Converted asset bulunamadıysa uyarı yerine normal log bas (gürültüyü önlemek için)

            var result = new Dictionary<uint, TableItemBasic>();

            byte[] decrypted = DecryptTblFile(filePath);
            if (decrypted == null || decrypted.Length == 0)
            {
                Debug.LogError($"[KOTableReader] Decrypt başarısız: {filePath}");
                return result;
            }

            using (var ms = new MemoryStream(decrypted))
            using (var reader = new BinaryReader(ms))
            {
                // C++ satır 117: int iDataTypeCount
                int columnCount = reader.ReadInt32();
                if (columnCount <= 0)
                {
                    Debug.LogError($"[KOTableReader] Geçersiz sütun sayısı: {columnCount}");
                    return result;
                }

                // C++ satır 126: file.Read(&m_DataTypes[0], sizeof(DATA_TYPE) * iDataTypeCount)
                var dataTypes = new DataType[columnCount];
                for (int i = 0; i < columnCount; i++)
                    dataTypes[i] = (DataType)reader.ReadUInt32();

                // C++ satır 148: int iRC — satır sayısı
                int rowCount = reader.ReadInt32();


                // C++ satır 151-163: her satırı oku
                for (int r = 0; r < rowCount; r++)
                {
                    // Her sütunu sırayla oku
                    object[] columns = new object[columnCount];
                    for (int c = 0; c < columnCount; c++)
                        columns[c] = ReadData(reader, dataTypes[c]);

                    // __TABLE_ITEM_BASIC struct'ına map et (GameDef.h:821-874)
                    var item = new TableItemBasic();
                    if (columnCount >= 1)  item.dwID             = Convert.ToUInt32(columns[0]);
                    if (columnCount >= 2)  item.byExtIndex       = Convert.ToByte(columns[1]);
                    if (columnCount >= 3)  item.szName           = columns[2]?.ToString() ?? "";
                    if (columnCount >= 4)  item.szRemark         = columns[3]?.ToString() ?? "";
                    if (columnCount >= 5)  item.dwIDK0           = Convert.ToUInt32(columns[4]);
                    if (columnCount >= 6)  item.byIDK1           = Convert.ToByte(columns[5]);
                    if (columnCount >= 7)  item.dwIDResrc        = Convert.ToUInt32(columns[6]);
                    if (columnCount >= 8)  item.dwIDIcon         = Convert.ToUInt32(columns[7]);

                    // ==========================================
                    // [Araya Girme / Interception - Dark Vane Icon]
                    // Dark Vane'in Shard ikonundan ayrışması için dwIDIcon değerini 
                    // kendine has bir ID'ye (11910000) yönlendiriyoruz.
                    // ==========================================
                    if (item.dwID / 1000 * 1000 == 119101000)
                    {
                        item.dwIDIcon = 11910000;
                    }

                    // ==========================================
                    // [Araya Girme / Interception - Iron Crossbow]
                    // Iron Crossbow'un starter yaylardan ayrılması için
                    // dwIDResrc ve dwIDIcon değerlerini kendine has ID'ye (16831000) yönlendiriyoruz.
                    // ==========================================
                    if (item.dwID / 1000 * 1000 == 168310000)
                    {
                        item.dwIDResrc = 16831000;
                        item.dwIDIcon = 16831000;
                    }

                    // ==========================================
                    // [Araya Girme / Interception - Enion Bow]
                    // Enion Bow'un Composite Bow (16211000) kaynaklarını kullanmasını engelleyip
                    // kendine has Enion Bow kaynaklarına (16910000) yönlendiriyoruz.
                    // ==========================================
                    if (item.dwID / 1000 * 1000 == 169101000)
                    {
                        item.dwIDResrc = 16910000;
                        item.dwIDIcon = 16910000;
                    }

                    // ==========================================
                    // [Araya Girme / Interception - Horn Bow]
                    // Horn Bow'un (160650000 / 160660000) Composite Bow (16211000)
                    // modeli ve ikonu ile görünmesini sağlıyoruz.
                    // ==========================================
                    if (item.dwID / 1000 * 1000 == 160650000 || item.dwID / 1000 * 1000 == 160660000)
                    {
                        item.dwIDResrc = 16211000;
                        item.dwIDIcon = 16211000;
                    }

                    if (columnCount >= 9)  item.dwSoundID0       = Convert.ToUInt32(columns[8]);
                    if (columnCount >= 10) item.dwSoundID1       = Convert.ToUInt32(columns[9]);
                    if (columnCount >= 11) item.byClass          = Convert.ToByte(columns[10]);
                    if (columnCount >= 12) item.byIsRobeType     = Convert.ToByte(columns[11]);
                    if (columnCount >= 13) item.byAttachPoint    = Convert.ToByte(columns[12]);
                    if (columnCount >= 14) item.byNeedRace       = Convert.ToByte(columns[13]);
                    if (columnCount >= 15) item.byNeedClass      = Convert.ToByte(columns[14]);
                    if (columnCount >= 16) item.siDamage         = Convert.ToInt16(columns[15]);
                    if (columnCount >= 17) item.siAttackInterval = Convert.ToInt16(columns[16]);
                    if (columnCount >= 18) item.siAttackRange    = Convert.ToInt16(columns[17]);
                    if (columnCount >= 19) item.siWeight         = Convert.ToInt16(columns[18]);
                    if (columnCount >= 20) item.siMaxDurability  = Convert.ToInt16(columns[19]);
                    if (columnCount >= 21) item.iPrice           = Convert.ToInt32(columns[20]);
                    if (columnCount >= 22) item.iSaleType        = Convert.ToInt32(columns[21]);
                    if (columnCount >= 23) item.siDefense        = Convert.ToInt16(columns[22]);
                    if (columnCount >= 24) item.byContable       = Convert.ToByte(columns[23]);
                    if (columnCount >= 25) item.dwEffectID1      = Convert.ToUInt32(columns[24]);
                    if (columnCount >= 26) item.dwEffectID2      = Convert.ToUInt32(columns[25]);
                    if (columnCount >= 27) item.cNeedLevel       = Convert.ToSByte(columns[26]);
                    if (columnCount >= 28) item.cIDK2            = Convert.ToSByte(columns[27]);
                    if (columnCount >= 29) item.byNeedRank       = Convert.ToByte(columns[28]);
                    if (columnCount >= 30) item.byNeedTitle      = Convert.ToByte(columns[29]);
                    if (columnCount >= 31) item.byNeedStrength   = Convert.ToByte(columns[30]);
                    if (columnCount >= 32) item.byNeedStamina    = Convert.ToByte(columns[31]);
                    if (columnCount >= 33) item.byNeedDexterity  = Convert.ToByte(columns[32]);
                    if (columnCount >= 34) item.byNeedInteli     = Convert.ToByte(columns[33]);
                    if (columnCount >= 35) item.byNeedMagicAttack = Convert.ToByte(columns[34]);
                    if (columnCount >= 36) item.bySellGroup      = Convert.ToByte(columns[35]);
                    if (columnCount >= 37) item.byGrade          = Convert.ToByte(columns[36]);

                    if (!result.ContainsKey(item.dwID))
                        result[item.dwID] = item;
                    else
                        Debug.LogWarning($"[KOTableReader] Duplicate key: {item.dwID}");
                }
            }

            return result;
        }

        /// <summary>
        /// Open-KO birebir: s_pTbl_Items_Basic.Find(itemId / 1000 * 1000)
        /// C++ GameProcMain.cpp satır 2021: pItem = s_pTbl_Items_Basic.Find(iItemIDInSlots[i] / 1000 * 1000)
        /// Item ID'nin base ID'sini bulur (son 3 hane upgrade/variant bilgisi)
        /// </summary>
        public static TableItemBasic FindItemBasic(Dictionary<uint, TableItemBasic> table, int itemId)
        {
            // C++ birebir: itemId / 1000 * 1000 → base item ID
            uint baseId = (uint)(itemId / 1000 * 1000);
            table.TryGetValue(baseId, out var item);
            return item;
        }

        // ================================================
        // Open-KO birebir: __TABLE_ITEM_EXT struct (GameDef.h:880-953)
        // 53 sütun — Item_Ext_N_us.tbl binary formatından okunuyor
        // ================================================
        [System.Serializable]
        public class TableItemExt
        {
            public uint dwID;                       // 01
            public string szHeader = "";            // 02
            public uint dwBaseID;                   // 03
            public string szRemark = "";            // 04
            public uint dwIDK0;                     // 05
            public uint dwIDResrc;                  // 06
            public uint dwIDIcon;                   // 07
            public byte byMagicOrRare;              // 08
            public short siDamage;                  // 09
            public short siAttackIntervalPercentage; // 10
            public short siHitRate;                 // 11
            public short siEvationRate;             // 12
            public short siMaxDurability;           // 13
            public short siPriceMultiply;           // 14
            public short siDefense;                 // 15
            public short siDefenseRateDagger;       // 16
            public short siDefenseRateSword;        // 17
            public short siDefenseRateBlow;         // 18
            public short siDefenseRateAxe;          // 19
            public short siDefenseRateSpear;        // 20
            public short siDefenseRateArrow;        // 21
            public byte byDamageFire;               // 22
            public byte byDamageIce;                // 23
            public byte byDamageThuner;             // 24
            public byte byDamagePoison;             // 25
            public byte byStillHP;                  // 26
            public byte byDamageMP;                 // 27
            public byte byStillMP;                  // 28
            public byte byReturnPhysicalDamage;     // 29
            public byte bySoulBind;                 // 30
            public short siBonusStr;                // 31
            public short siBonusSta;                // 32
            public short siBonusDex;                // 33
            public short siBonusInt;                // 34
            public short siBonusMagicAttak;         // 35
            public short siBonusHP;                 // 36
            public short siBonusMSP;                // 37
            public short siRegistFire;              // 38
            public short siRegistIce;               // 39
            public short siRegistElec;              // 40
            public short siRegistMagic;             // 41
            public short siRegistPoison;            // 42
            public short siRegistCurse;             // 43
            public uint dwEffectID1;                // 44
            public uint dwEffectID2;                // 45
            public short siNeedLevel;               // 46
            public short siNeedRank;                // 47
            public short siNeedTitle;               // 48
            public short siNeedStrength;            // 49
            public short siNeedStamina;             // 50
            public short siNeedDexterity;           // 51
            public short siNeedInteli;              // 52
            public short siNeedMagicAttack;         // 53
        }

        /// <summary>
        /// Open-KO birebir: MAX_ITEM_EXTENSION = 24 (GameDef.h:876)
        /// </summary>
        public const int MAX_ITEM_EXTENSION = 24;

        // ================================================
        // Open-KO birebir: s_pTbl_Items_Exts[MAX_ITEM_EXTENSION] yükleme
        // C++ GameBase.cpp satır 72-77
        // ================================================
        public static Dictionary<uint, TableItemExt>[] LoadItemExtTables(string dataDir)
        {
            // Önce convert edilmiş asset'lerden yükle
            var firstAsset = Resources.Load<KOItemExtAsset>("KOData/Item_Ext_0_us");
            if (firstAsset != null)
            {
                var tables = new Dictionary<uint, TableItemExt>[MAX_ITEM_EXTENSION];
                int totalItems = 0;
                for (int i = 0; i < MAX_ITEM_EXTENSION; i++)
                {
                    var extAsset = Resources.Load<KOItemExtAsset>($"KOData/Item_Ext_{i}_us");
                    tables[i] = extAsset != null ? extAsset.ToDictionary() : new Dictionary<uint, TableItemExt>();
                    totalItems += tables[i].Count;
                }
                return tables;
            }

            var tablesOrig = new Dictionary<uint, TableItemExt>[MAX_ITEM_EXTENSION];
            for (int i = 0; i < MAX_ITEM_EXTENSION; i++)
            {
                // C++ satır 74: szFNTmp = fmt::format("Data\\Item_Ext_{}", i)
                string fileName = $"Item_Ext_{i}_us.tbl";
                string filePath = Path.Combine(dataDir, fileName);

                tablesOrig[i] = LoadSingleItemExtTable(filePath);
            }
            int totalItemsOrig = 0;
            for (int i = 0; i < MAX_ITEM_EXTENSION; i++) totalItemsOrig += tablesOrig[i].Count;
            return tablesOrig;
        }

        private static Dictionary<uint, TableItemExt> LoadSingleItemExtTable(string filePath)
        {
            var result = new Dictionary<uint, TableItemExt>();

            int extIdx = 0;
            string fn = Path.GetFileNameWithoutExtension(filePath);
            if (fn.Contains("Item_Ext_"))
            {
                string numPart = fn.Replace("Item_Ext_", "").Split('_')[0];
                int.TryParse(numPart, out extIdx);
            }

            byte[] decrypted = DecryptTblFile(filePath);
            if (decrypted == null || decrypted.Length == 0) return result;

            using (var ms = new MemoryStream(decrypted))
            using (var reader = new BinaryReader(ms))
            {
                int columnCount = reader.ReadInt32();
                if (columnCount <= 0) return result;

                var dataTypes = new DataType[columnCount];
                for (int i = 0; i < columnCount; i++)
                    dataTypes[i] = (DataType)reader.ReadUInt32();

                int rowCount = reader.ReadInt32();

                for (int r = 0; r < rowCount; r++)
                {
                    object[] columns = new object[columnCount];
                    for (int c = 0; c < columnCount; c++)
                        columns[c] = ReadData(reader, dataTypes[c]);

                    var item = new TableItemExt();
                    if (columnCount >= 1)  item.dwID                       = Convert.ToUInt32(columns[0]);
                    if (columnCount >= 2)  item.szHeader                   = columns[1]?.ToString() ?? "";
                    if (columnCount >= 3)  item.dwBaseID                   = Convert.ToUInt32(columns[2]);
                    if (columnCount >= 4)  item.szRemark                   = columns[3]?.ToString() ?? "";
                    if (columnCount >= 5)  item.dwIDK0                     = Convert.ToUInt32(columns[4]);
                    if (columnCount >= 6)  item.dwIDResrc                  = Convert.ToUInt32(columns[5]);
                    if (columnCount >= 7)  item.dwIDIcon                   = Convert.ToUInt32(columns[6]);

                    // ==========================================
                    // [Araya Girme / Interception - Chitin Bow]
                    // Chitin Bow'un Iron Bow (16841000) kaynaklarını kullanmasını engelleyip
                    // kendine has Chitin Bow kaynaklarına (16121000) yönlendiriyoruz.
                    // ==========================================
                    if (item.dwBaseID == 160450000 && item.dwIDResrc == 16841000)
                    {
                        item.dwIDResrc = 16121000;
                        item.dwIDIcon = 16121000;
                    }


                    if (columnCount >= 8)  item.byMagicOrRare              = Convert.ToByte(columns[7]);
                    if (columnCount >= 9)  item.siDamage                   = Convert.ToInt16(columns[8]);
                    if (columnCount >= 10) item.siAttackIntervalPercentage = Convert.ToInt16(columns[9]);
                    if (columnCount >= 11) item.siHitRate                  = Convert.ToInt16(columns[10]);
                    if (columnCount >= 12) item.siEvationRate              = Convert.ToInt16(columns[11]);
                    if (columnCount >= 13) item.siMaxDurability            = Convert.ToInt16(columns[12]);
                    if (columnCount >= 14) item.siPriceMultiply            = Convert.ToInt16(columns[13]);
                    if (columnCount >= 15) item.siDefense                  = Convert.ToInt16(columns[14]);
                    if (columnCount >= 16) item.siDefenseRateDagger        = Convert.ToInt16(columns[15]);
                    if (columnCount >= 17) item.siDefenseRateSword         = Convert.ToInt16(columns[16]);
                    if (columnCount >= 18) item.siDefenseRateBlow          = Convert.ToInt16(columns[17]);
                    if (columnCount >= 19) item.siDefenseRateAxe           = Convert.ToInt16(columns[18]);
                    if (columnCount >= 20) item.siDefenseRateSpear         = Convert.ToInt16(columns[19]);
                    if (columnCount >= 21) item.siDefenseRateArrow         = Convert.ToInt16(columns[20]);
                    if (columnCount >= 22) item.byDamageFire               = Convert.ToByte(columns[21]);
                    if (columnCount >= 23) item.byDamageIce                = Convert.ToByte(columns[22]);
                    if (columnCount >= 24) item.byDamageThuner             = Convert.ToByte(columns[23]);
                    if (columnCount >= 25) item.byDamagePoison             = Convert.ToByte(columns[24]);
                    if (columnCount >= 26) item.byStillHP                  = Convert.ToByte(columns[25]);
                    if (columnCount >= 27) item.byDamageMP                 = Convert.ToByte(columns[26]);
                    if (columnCount >= 28) item.byStillMP                  = Convert.ToByte(columns[27]);
                    if (columnCount >= 29) item.byReturnPhysicalDamage     = Convert.ToByte(columns[28]);
                    if (columnCount >= 30) item.bySoulBind                 = Convert.ToByte(columns[29]);
                    if (columnCount >= 31) item.siBonusStr                 = Convert.ToInt16(columns[30]);
                    if (columnCount >= 32) item.siBonusSta                 = Convert.ToInt16(columns[31]);
                    if (columnCount >= 33) item.siBonusDex                 = Convert.ToInt16(columns[32]);
                    if (columnCount >= 34) item.siBonusInt                 = Convert.ToInt16(columns[33]);
                    if (columnCount >= 35) item.siBonusMagicAttak          = Convert.ToInt16(columns[34]);
                    if (columnCount >= 36) item.siBonusHP                  = Convert.ToInt16(columns[35]);
                    if (columnCount >= 37) item.siBonusMSP                 = Convert.ToInt16(columns[36]);
                    if (columnCount >= 38) item.siRegistFire               = Convert.ToInt16(columns[37]);
                    if (columnCount >= 39) item.siRegistIce                = Convert.ToInt16(columns[38]);
                    if (columnCount >= 40) item.siRegistElec               = Convert.ToInt16(columns[39]);
                    if (columnCount >= 41) item.siRegistMagic              = Convert.ToInt16(columns[40]);
                    if (columnCount >= 42) item.siRegistPoison             = Convert.ToInt16(columns[41]);
                    if (columnCount >= 43) item.siRegistCurse              = Convert.ToInt16(columns[42]);
                    if (columnCount >= 44) item.dwEffectID1                = Convert.ToUInt32(columns[43]);
                    if (columnCount >= 45) item.dwEffectID2                = Convert.ToUInt32(columns[44]);
                    if (columnCount >= 46) item.siNeedLevel                = Convert.ToInt16(columns[45]);
                    if (columnCount >= 47) item.siNeedRank                 = Convert.ToInt16(columns[46]);
                    if (columnCount >= 48) item.siNeedTitle                = Convert.ToInt16(columns[47]);
                    if (columnCount >= 49) item.siNeedStrength             = Convert.ToInt16(columns[48]);
                    if (columnCount >= 50) item.siNeedStamina              = Convert.ToInt16(columns[49]);
                    if (columnCount >= 51) item.siNeedDexterity            = Convert.ToInt16(columns[50]);
                    if (columnCount >= 52) item.siNeedInteli               = Convert.ToInt16(columns[51]);
                    if (columnCount >= 53) item.siNeedMagicAttack          = Convert.ToInt16(columns[52]);

                    if (!result.ContainsKey(item.dwID))
                        result[item.dwID] = item;
                }
            }
            return result;
        }

        /// <summary>
        /// Open-KO birebir: s_pTbl_Items_Exts[pItem->byExtIndex].Find(dwItemID % 1000)
        /// C++ GameProcCharacterSelect.cpp satır 666, GameProcMain.cpp satır 2023, 2110 vb.
        /// Item ID'nin son 3 hanesini (ext index) kullanarak ext bilgisini bulur.
        /// NOT: C++ HER YERDE `% 1000` uygular — ext tablodaki key'ler 0-999 aralığındadır.
        /// </summary>
        public static TableItemExt FindItemExt(Dictionary<uint, TableItemExt>[] extTables, int extIndex, int itemId)
        {
            if (extTables == null || extIndex < 0 || extIndex >= extTables.Length) return null;
            var table = extTables[extIndex];
            if (table == null) return null;
            // C++ birebir: .Find(dwItemID % 1000) — ext key'ler 0-999 aralığında
            uint extKey = (uint)(itemId % 1000);
            table.TryGetValue(extKey, out var item);
            return item;
        }

        // ================================================
        // Open-KO birebir: __TABLE_PLAYER_LOOKS struct (GameDef.h:1017-1052)
        // UPC_DefaultLooks.tbl — race bazlı varsayılan karakter görünümü
        // C++: CN3TableBase<__TABLE_PLAYER_LOOKS> s_pTbl_UPC_Looks (GameBase.h:21)
        //
        // CPlayerBase::PartSet (PlayerBase.cpp:1920-1923):
        //   __TABLE_PLAYER_LOOKS* pLooks = s_pTbl_UPC_Looks.Find(m_InfoBase.eRace);
        //   pPart = m_Chr.PartSet(ePos, pLooks->szPartFNs[ePos]);
        // ================================================
        [System.Serializable]
        public class TablePlayerLooks
        {
            public uint dwID;               // 01 — eRace (1=KA_Ark, 2=KA_Tur, ... 13=EL_Wom)
            public string szName = "";       // 02
            public string szJointFN = "";    // 03
            public string szAniFN = "";      // 04
            public string[] szPartFNs = new string[10]; // 05-14 (UPPER, LOWER, FACE, HANDS, FEET, HAIR, + 4 reserved)
            public string szSkinFN = "";     // 15
            public string szChrFN = "";      // 16
            public string szFXPlugFN = "";   // 17
            public int iIdk1;                // 18
            public int iJointRH = -1;        // 19 — sağ el joint index
            public int iJointLH = -1;        // 20 — sol el ucu joint index
            public int iJointLH2 = -1;       // 21 — sol el (kalkan/forearm) joint index
            public int iJointCloak = -1;     // 22 — pelerin joint index
        }

        /// <summary>
        /// Open-KO birebir: UPC_DefaultLooks.tbl yükleme
        /// C++: s_pTbl_UPC_Looks.LoadFromFile("Data\\UPC_DefaultLooks")
        ///
        /// Binary format: CN3TableBase XOR encrypted, __TABLE_PLAYER_LOOKS fields
        /// Aynı format NPC_Looks.tbl ile paylaşılır (GameDef.h:1017-1052).
        ///
        /// Column mapping (GameDef.h:1017-1052):
        ///   Col 0:  dwID (DWORD) — eRace
        ///   Col 1:  szName (STRING)
        ///   Col 2:  szJointFN (STRING)
        ///   Col 3:  szAniFN (STRING)
        ///   Col 4-13: szPartFNs[0..9] (STRING×10)
        ///   Col 14: szSkinFN (STRING)
        ///   Col 15: szChrFN (STRING)
        ///   Col 16: szFXPlugFN (STRING)
        ///   Col 17: iIdk1 (INT)
        ///   Col 18: iJointRH (INT)
        ///   Col 19: iJointLH (INT)
        ///   Col 20: iJointLH2 (INT)
        ///   Col 21: iJointCloak (INT)
        ///   Col 22-32: sound IDs (INT×11) — atlanıyor
        ///   Col 33-34: iIdk2, iIdk3 (INT) — atlanıyor
        ///   Col 35-37: byIdk4-6 (BYTE) — atlanıyor
        /// </summary>
        /// <returns>eRace → TablePlayerLooks dictionary</returns>
        public static Dictionary<uint, TablePlayerLooks> LoadUpcLooksTable(string filePath)
        {
            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<KOPlayerLooksAsset>("KOData/UPC_DefaultLooks");
            if (asset != null)
            {
                return asset.ToDictionary();
            }

            var result = new Dictionary<uint, TablePlayerLooks>();

            byte[] decrypted = DecryptTblFile(filePath);
            if (decrypted == null || decrypted.Length == 0)
            {
                Debug.LogError($"[KOTableReader] UPC_DefaultLooks decrypt başarısız: {filePath}");
                return result;
            }

            using (var ms = new MemoryStream(decrypted))
            using (var reader = new BinaryReader(ms, Encoding.GetEncoding(949)))
            {
                int columnCount = reader.ReadInt32();
                if (columnCount <= 0 || columnCount > 200)
                {
                    Debug.LogError($"[KOTableReader] UPC_DefaultLooks geçersiz column: {columnCount}");
                    return result;
                }

                var dataTypes = new DataType[columnCount];
                for (int i = 0; i < columnCount; i++)
                    dataTypes[i] = (DataType)reader.ReadUInt32();

                int rowCount = reader.ReadInt32();

                for (int r = 0; r < rowCount; r++)
                {
                    var entry = new TablePlayerLooks();

                    for (int col = 0; col < columnCount; col++)
                    {
                        object val = ReadData(reader, dataTypes[col]);

                        // Field mapping — GameDef.h:1017-1052 birebir
                        if (col == 0) entry.dwID = Convert.ToUInt32(val);
                        else if (col == 1) entry.szName = val?.ToString() ?? "";
                        else if (col == 2) entry.szJointFN = val?.ToString() ?? "";
                        else if (col == 3) entry.szAniFN = val?.ToString() ?? "";
                        else if (col >= 4 && col <= 13) entry.szPartFNs[col - 4] = val?.ToString() ?? "";
                        else if (col == 14) entry.szSkinFN = val?.ToString() ?? "";
                        else if (col == 15) entry.szChrFN = val?.ToString() ?? "";
                        else if (col == 16) entry.szFXPlugFN = val?.ToString() ?? "";
                        else if (col == 17) entry.iIdk1 = Convert.ToInt32(val);
                        else if (col == 18) entry.iJointRH = Convert.ToInt32(val);
                        else if (col == 19) entry.iJointLH = Convert.ToInt32(val);
                        else if (col == 20) entry.iJointLH2 = Convert.ToInt32(val);
                        else if (col == 21) entry.iJointCloak = Convert.ToInt32(val);
                        // Col 22+ — sound IDs ve diğer alanlar şimdilik atlanıyor
                    }

                    if (entry.dwID > 0 && !result.ContainsKey(entry.dwID))
                        result[entry.dwID] = entry;
                }
            }

            return result;
        }

        // ================================================
        // Open-KO birebir: __TABLE_FX struct (GameDef.h:1337-1344)
        // FX efekt dosya tablosu — silah enchant, skill efektleri vb.
        // C++: CN3TableBase<__TABLE_FX> s_pTbl_FXSource (GameBase.h:24)
        // C++: s_pTbl_FXSource.LoadFromFile("Data\\fx.tbl") (GameBase.cpp:83-84)
        // ================================================
        [System.Serializable]
        public class TableFX
        {
            public uint dwID;             // 01 — FX ID (FXID_SWORD_FIRE_MAIN=10021, etc.)
            public string szName = "";     // 02 — Effect name
            public string szFN = "";       // 03 — Effect filename (.fxb dosya yolu)
            public uint dwSoundID;         // 04 — Sound ID
            public byte byAOE;             // 05 — AOE flag
        }

        /// <summary>
        /// Open-KO birebir: fx.tbl yükleme
        /// C++: s_pTbl_FXSource.LoadFromFile("Data\\fx.tbl")
        ///
        /// __TABLE_FX column mapping (GameDef.h:1337-1344):
        ///   Col 0: dwID (DWORD)
        ///   Col 1: szName (STRING)
        ///   Col 2: szFN (STRING) — efekt dosya yolu
        ///   Col 3: dwSoundID (DWORD)
        ///   Col 4: byAOE (BYTE)
        /// </summary>
        public static Dictionary<uint, TableFX> LoadFxTable(string filePath)
        {
            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<KOFxTableAsset>("KOData/fx");
            if (asset != null)
            {
                return asset.ToDictionary();
            }

            var result = new Dictionary<uint, TableFX>();

            byte[] decrypted = DecryptTblFile(filePath);
            if (decrypted == null || decrypted.Length == 0)
            {
                Debug.LogError($"[KOTableReader] fx.tbl decrypt başarısız: {filePath}");
                return result;
            }

            using (var ms = new MemoryStream(decrypted))
            using (var reader = new BinaryReader(ms, Encoding.GetEncoding(949)))
            {
                int columnCount = reader.ReadInt32();
                if (columnCount <= 0 || columnCount > 100)
                {
                    Debug.LogError($"[KOTableReader] fx.tbl geçersiz column: {columnCount}");
                    return result;
                }

                var dataTypes = new DataType[columnCount];
                for (int i = 0; i < columnCount; i++)
                    dataTypes[i] = (DataType)reader.ReadUInt32();

                int rowCount = reader.ReadInt32();

                for (int r = 0; r < rowCount; r++)
                {
                    var entry = new TableFX();

                    for (int col = 0; col < columnCount; col++)
                    {
                        object val = ReadData(reader, dataTypes[col]);

                        // Field mapping — GameDef.h:1337-1344 birebir
                        if (col == 0) entry.dwID = Convert.ToUInt32(val);
                        else if (col == 1) entry.szName = val?.ToString() ?? "";
                        else if (col == 2) entry.szFN = val?.ToString() ?? "";
                        else if (col == 3) entry.dwSoundID = Convert.ToUInt32(val);
                        else if (col == 4) entry.byAOE = Convert.ToByte(val);
                        // Col 5+ — varsa atlanıyor
                    }

                    if (entry.dwID > 0 && !result.ContainsKey(entry.dwID))
                        result[entry.dwID] = entry;
                }
            }

            return result;
        }
        
        // ================================================
        // Open-KO birebir: __TABLE_NEW_CHR struct (GameProcCharacterCreate.h:39-50)
        // NewChrValue.tbl — Karakter oluşturma başlangıç stat tablosu
        // Key: race * 10000 + class
        // ================================================
        [System.Serializable]
        public class TableNewChr
        {
            public uint dwID;       // race * 10000 + class
            public string szName;   // Race+Class ismi
            public int iStr;        // Base strength
            public int iSta;        // Base stamina
            public int iDex;        // Base dexterity
            public int iInt;        // Base intelligence
            public int iMAP;        // Base magic attack power (charisma)
            public int iBonus;      // Dağıtılabilir bonus puan
        }
        
        /// <summary>
        /// Open-KO birebir: NewChrValue.tbl parse
        /// C++ referans: GameProcCharacterCreate.cpp satır 56:
        ///   m_Tbl_InitValue.LoadFromFile("Data\\NewChrValue.tbl")
        /// GameProcCharacterCreate.h:57:
        ///   CN3TableBase<__TABLE_NEW_CHR> m_Tbl_InitValue
        /// 
        /// Struct sütunları (GameProcCharacterCreate.h:39-50):
        ///   Col 0: dwID (DWORD) — race*10000+class
        ///   Col 1: szName (STRING) — ırk+sınıf adı
        ///   Col 2: iStr (INT) — base güç
        ///   Col 3: iSta (INT) — base dayanıklılık
        ///   Col 4: iDex (INT) — base çeviklik
        ///   Col 5: iInt (INT) — base zeka
        ///   Col 6: iMAP (INT) — base magic attack power
        ///   Col 7: iBonus (INT) — bonus puan
        ///   Col 8-19: dwIDK[12] (DWORD×12) — bilinmeyen
        /// </summary>
        public static Dictionary<uint, TableNewChr> LoadNewChrValue(string filePath)
        {
            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<KONewChrAsset>("KOData/NewChrValue");
            if (asset != null)
            {
                return asset.ToDictionary();
            }

            var result = new Dictionary<uint, TableNewChr>();
            
            byte[] decrypted = DecryptTblFile(filePath);
            if (decrypted == null || decrypted.Length == 0)
            {
                Debug.LogError($"[KOTableReader] NewChrValue.tbl dosyası okunamadı: {filePath}");
                return result;
            }
            
            using (var ms = new MemoryStream(decrypted))
            using (var reader = new BinaryReader(ms))
            {
                // Open-KO birebir: N3TableBase.h Load() — column count + types
                int columnCount = reader.ReadInt32();
                var columnTypes = new DataType[columnCount];
                for (int c = 0; c < columnCount; c++)
                    columnTypes[c] = (DataType)reader.ReadUInt32();
                
                int rowCount = reader.ReadInt32();
                
                for (int r = 0; r < rowCount; r++)
                {
                    var entry = new TableNewChr();
                    
                    for (int c = 0; c < columnCount; c++)
                    {
                        var val = ReadData(reader, columnTypes[c]);
                        
                        // GameProcCharacterCreate.h:39-50 — struct sırası
                        switch (c)
                        {
                            case 0: entry.dwID   = Convert.ToUInt32(val); break;
                            case 1: entry.szName  = val?.ToString() ?? ""; break;
                            case 2: entry.iStr    = Convert.ToInt32(val); break;
                            case 3: entry.iSta    = Convert.ToInt32(val); break;
                            case 4: entry.iDex    = Convert.ToInt32(val); break;
                            case 5: entry.iInt    = Convert.ToInt32(val); break;
                            case 6: entry.iMAP    = Convert.ToInt32(val); break;
                            case 7: entry.iBonus  = Convert.ToInt32(val); break;
                            // Col 8-19: dwIDK[12] — bilinmeyen alanlar, atla
                        }
                    }
                    
                    if (entry.dwID > 0 && !result.ContainsKey(entry.dwID))
                        result[entry.dwID] = entry;
                }
            }
            
            return result;
        }

        // ================================================
        // Open-KO birebir: Texts_us.tbl yükleyici
        // Sadece iki sütun: dwID (DWORD) ve szText (STRING)
        // ================================================
        public static Dictionary<uint, string> LoadTextsTable(string filePath = "Texts_us.tbl")
        {
            var result = new Dictionary<uint, string>();
            byte[] decrypted = DecryptTblFile(filePath);
            if (decrypted == null || decrypted.Length == 0)
            {
                Debug.LogError($"[KOTableReader] Texts_us decrypt başarısız: {filePath}");
                return result;
            }

            using (var ms = new MemoryStream(decrypted))
            using (var reader = new BinaryReader(ms))
            {
                int columnCount = reader.ReadInt32();
                if (columnCount != 2)
                {
                    Debug.LogError($"[KOTableReader] Texts_us columns count must be 2: {columnCount}");
                    return result;
                }

                // Data types: Column 0: DT_DWORD (6), Column 1: DT_STRING (7)
                uint type0 = reader.ReadUInt32();
                uint type1 = reader.ReadUInt32();

                int rowCount = reader.ReadInt32();
                for (int r = 0; r < rowCount; r++)
                {
                    uint id = reader.ReadUInt32();
                    int len = reader.ReadInt32();
                    string val = "";
                    if (len > 0)
                    {
                        byte[] strBytes = reader.ReadBytes(len);
                        val = Encoding.UTF8.GetString(strBytes);
                    }
                    result[id] = val;
                }
            }
            return result;
        }

        // ================================================
        // Open-KO birebir: __TABLE_HELP struct (GameDef.h:1234-1242)
        // help_us.tbl — Seviye Kılavuzu görev rehber tablosu
        // ================================================
        [System.Serializable]
        public class TableHelp
        {
            public uint dwID;
            public int iMinLevel;
            public int iMaxLevel;
            public int iReqClass;
            public string szQuestName = "";
            public string szQuestDesc = "";
        }

        /// <summary>
        /// help_us.tbl dosyasını parse eder.
        /// </summary>
        public static Dictionary<uint, TableHelp> LoadHelpTable(string filePath)
        {
            var result = new Dictionary<uint, TableHelp>();

            byte[] decrypted = DecryptTblFile(filePath);
            if (decrypted == null || decrypted.Length == 0)
            {
                Debug.LogError($"[KOTableReader] help_us.tbl decrypt başarısız: {filePath}");
                return result;
            }

            using (var ms = new MemoryStream(decrypted))
            using (var reader = new BinaryReader(ms, Encoding.GetEncoding(949)))
            {
                int columnCount = reader.ReadInt32();
                if (columnCount <= 0) return result;

                var dataTypes = new DataType[columnCount];
                for (int i = 0; i < columnCount; i++)
                    dataTypes[i] = (DataType)reader.ReadUInt32();

                int rowCount = reader.ReadInt32();

                for (int r = 0; r < rowCount; r++)
                {
                    var entry = new TableHelp();
                    for (int col = 0; col < columnCount; col++)
                    {
                        object val = ReadData(reader, dataTypes[col]);

                        if (col == 0) entry.dwID = Convert.ToUInt32(val);
                        else if (col == 1) entry.iMinLevel = Convert.ToInt32(val);
                        else if (col == 2) entry.iMaxLevel = Convert.ToInt32(val);
                        else if (col == 3) entry.iReqClass = Convert.ToInt32(val);
                        else if (col == 4) entry.szQuestName = val?.ToString() ?? "";
                        else if (col == 5) entry.szQuestDesc = val?.ToString() ?? "";
                    }

                    if (entry.dwID > 0 && !result.ContainsKey(entry.dwID))
                        result[entry.dwID] = entry;
                }
            }

            return result;
        }
    }
}

