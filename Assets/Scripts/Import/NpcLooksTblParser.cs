using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EntropyOnline.Import;
using UnityEngine;

namespace KOImport
{
    /// <summary>
    /// Open-KO v1.298 NPC_Looks.tbl parser.
    /// CN3TableBase<__TABLE_PLAYER_LOOKS> formatını okur.
    /// 
    /// Binary format:
    ///   1) XOR decrypt (key_r=0x0816, key_c1=0x6081, key_c2=0x1608)
    ///   2) int32 columnCount
    ///   3) DATA_TYPE[columnCount] (each uint32)
    ///   4) int32 rowCount
    ///   5) Per row: columns read per DATA_TYPE
    ///
    /// DATA_TYPE enum:
    ///   0=NONE, 1=CHAR, 2=BYTE, 3=SHORT, 4=WORD, 5=INT, 6=DWORD, 7=STRING, 8=FLOAT, 9=DOUBLE
    ///
    /// __TABLE_PLAYER_LOOKS fields (C++ GameDef.h line 1017):
    ///   dwID(DWORD), szName(STRING), szJointFN(STRING), szAniFN(STRING),
    ///   szPartFNs[10](STRING×10), szSkinFN(STRING), szChrFN(STRING), szFXPlugFN(STRING),
    ///   iIdk1(INT), iJointRH(INT), iJointLH(INT), iJointLH2(INT), iJointCloak(INT),
    ///   iSndID_Move..Reserved1 (INT×11), iIdk2(INT), iIdk3(INT),
    ///   byIdk4(BYTE), byIdk5(BYTE), byIdk6(BYTE)
    /// </summary>
    public static class NpcLooksTblParser
    {
        /// <summary>
        /// NPC model bilgisi — PID→chr dosya eşlemesi.
        /// </summary>
        public class NpcLooksEntry
        {
            public uint Id;           // PID (K_NPC.pid ile eşleşir)
            public string Name;
            public string ChrFile;    // .n3chr dosya yolu
            public string JointFile;
            public string AniFile;
            public string[] PartFiles = new string[10];
        }

        /// <summary>
        /// NPC_Looks.tbl dosyasını parse eder.
        /// </summary>
        /// <returns>PID → NpcLooksEntry dictionary</returns>
        public static Dictionary<uint, NpcLooksEntry> Load(string tblPath)
        {
            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<EntropyOnline.Import.KONpcLooksAsset>("KOData/NPC_Looks");
            if (asset != null && asset.entries != null)
            {
                var dict = new Dictionary<uint, NpcLooksEntry>();
                foreach (var e in asset.entries)
                {
                    if (e == null) continue;
                    var entry = new NpcLooksEntry
                    {
                        Id = e.dwID,
                        Name = e.szName,
                        JointFile = e.szJointFN,
                        AniFile = e.szAniFN,
                        PartFiles = e.szPartFNs ?? new string[10],
                        ChrFile = e.szChrFN
                    };
                    if (!dict.ContainsKey(e.dwID))
                        dict[e.dwID] = entry;
                }
                return dict;
            }

            var result = new Dictionary<uint, NpcLooksEntry>();

            byte[] encrypted = KOTableProvider.LoadRaw(tblPath);
            if (encrypted == null)
            {
                Debug.LogError($"[NpcLooks] Dosya bulunamadı: {tblPath}");
                return result;
            }

            // Step 1: XOR decrypt — CN3TableBaseImpl::LoadFromFile birebir
            ushort key_r = 0x0816;
            ushort key_c1 = 0x6081;
            ushort key_c2 = 0x1608;
            byte[] decrypted = new byte[encrypted.Length];
            for (int i = 0; i < encrypted.Length; i++)
            {
                decrypted[i] = (byte)(encrypted[i] ^ (key_r >> 8));
                key_r = (ushort)((encrypted[i] + key_r) * key_c1 + key_c2);
            }

            // Step 2: Parse binary
            using var ms = new MemoryStream(decrypted);
            using var reader = new BinaryReader(ms, Encoding.GetEncoding(949)); // Korean codepage

            // Column count
            int columnCount = reader.ReadInt32();
            if (columnCount <= 0 || columnCount > 200)
            {
                Debug.LogError($"[NpcLooks] Geçersiz column sayısı: {columnCount}");
                return result;
            }

            // DATA_TYPE array
            uint[] dataTypes = new uint[columnCount];
            for (int i = 0; i < columnCount; i++)
                dataTypes[i] = reader.ReadUInt32();

            // Row count
            int rowCount = reader.ReadInt32();

            // __TABLE_PLAYER_LOOKS field mapping:
            // Col 0: dwID (DWORD)
            // Col 1: szName (STRING)
            // Col 2: szJointFN (STRING)
            // Col 3: szAniFN (STRING)
            // Col 4-13: szPartFNs[0..9] (STRING×10)
            // Col 14: szSkinFN (STRING)
            // Col 15: szChrFN (STRING)  ← BİZİM İHTİYACIMIZ
            // Col 16: szFXPlugFN (STRING)
            // Col 17: iIdk1 (INT)
            // Col 18-21: iJointRH, iJointLH, iJointLH2, iJointCloak (INT×4)
            // Col 22-32: iSndID_Move..Reserved1 (INT×11)
            // Col 33-34: iIdk2, iIdk3 (INT×2)
            // Col 35-37: byIdk4, byIdk5, byIdk6 (BYTE×3)

            for (int row = 0; row < rowCount; row++)
            {
                var entry = new NpcLooksEntry();
                for (int col = 0; col < columnCount; col++)
                {
                    switch (dataTypes[col])
                    {
                        case 1: // DT_CHAR
                            byte charVal = reader.ReadByte();
                            break;
                        case 2: // DT_BYTE
                            byte byteVal = reader.ReadByte();
                            break;
                        case 3: // DT_SHORT
                            short shortVal = reader.ReadInt16();
                            break;
                        case 4: // DT_WORD
                            ushort wordVal = reader.ReadUInt16();
                            break;
                        case 5: // DT_INT
                            int intVal = reader.ReadInt32();
                            break;
                        case 6: // DT_DWORD
                            uint dwordVal = reader.ReadUInt32();
                            if (col == 0) entry.Id = dwordVal;
                            break;
                        case 7: // DT_STRING
                            int strLen = reader.ReadInt32();
                            string strVal = "";
                            if (strLen > 0)
                            {
                                byte[] strBytes = reader.ReadBytes(strLen);
                                strVal = Encoding.GetEncoding(949).GetString(strBytes);
                            }
                            // Field mapping
                            if (col == 1) entry.Name = strVal;
                            else if (col == 2) entry.JointFile = strVal;
                            else if (col == 3) entry.AniFile = strVal;
                            else if (col >= 4 && col <= 13) entry.PartFiles[col - 4] = strVal;
                            else if (col == 15) entry.ChrFile = strVal;
                            break;
                        case 8: // DT_FLOAT
                            float floatVal = reader.ReadSingle();
                            break;
                        case 9: // DT_DOUBLE
                            double doubleVal = reader.ReadDouble();
                            break;
                        default:
                            Debug.LogWarning($"[NpcLooks] Bilinmeyen DATA_TYPE: {dataTypes[col]} col={col}");
                            break;
                    }
                }

                if (entry.Id > 0 && !result.ContainsKey(entry.Id))
                {
                    result[entry.Id] = entry;
                }
            }

            return result;
        }
    }
}
