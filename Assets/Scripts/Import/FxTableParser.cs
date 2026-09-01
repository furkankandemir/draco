using System.Collections.Generic;
using System.IO;
using System.Text;
using EntropyOnline.Import;
using UnityEngine;

namespace KOImport
{
    /// <summary>
    /// Open-KO birebir: CGameBase::s_pTbl_FXSource — fx.tbl parser.
    ///
    /// fx.tbl → CN3TableBase&lt;__TABLE_FX&gt; yüklemesi (GameBase.cpp:83-84)
    ///   szFN = "Data\\fx.tbl";
    ///   s_pTbl_FXSource.LoadFromFile(szFN);
    ///
    /// __TABLE_FX yapısı (GameDef.h:1337-1344):
    ///   uint32 dwID       — 01 FX ID (ör: 10002 = FXID_BLOOD)
    ///   string szName     — 02 Efekt adı
    ///   string szFN       — 03 .fxb dosya yolu
    ///   uint32 dwSoundID  — 04 Ses efekti ID
    ///   uint8  byAOE      — 05 Alan etkisi flag
    ///
    /// TBL decrypt: N3TableBaseImpl.cpp:51-77 rolling key XOR (0x0816, 0x6081, 0x1608)
    /// </summary>
    public static class FxTableParser
    {
        /// <summary>Parse edilmiş FX kayıtları (FXID → entry)</summary>
        private static readonly Dictionary<int, FxTableEntry> _entries = new();

        /// <summary>Veriler yüklendi mi?</summary>
        public static bool IsLoaded { get; private set; }

        /// <summary>
        /// fx.tbl dosyasını yükle.
        /// Open-KO birebir: CGameBase::StaticMemberInit → s_pTbl_FXSource.LoadFromFile("Data\\fx.tbl")
        /// GameBase.cpp:83-84
        /// </summary>
        public static void Load(string fxTblPath)
        {
            _entries.Clear();

            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<EntropyOnline.Import.KOFxParserAsset>("KOData/fx_parser");
            if (asset != null && asset.entries != null)
            {
                foreach (var e in asset.entries)
                {
                    if (e == null || e.Id <= 0) continue;
                    _entries[e.Id] = new FxTableEntry
                    {
                        Id = e.Id,
                        Name = e.Name ?? "",
                        FileName = e.FileName ?? "",
                        SoundId = e.SoundId,
                        AOE = e.AOE
                    };
                }
                IsLoaded = true;
                return;
            }

            // Step 1: Load raw bytes — KOTableProvider first, filesystem fallback
            byte[] raw = KOTableProvider.LoadRaw(fxTblPath);
            if (raw == null)
            {
                Debug.LogWarning($"[FxTableParser] fx.tbl bulunamadı: {fxTblPath}");
                return;
            }

            // Step 1b: Decrypt — N3TableBaseImpl.cpp:51-77 birebir
            byte[] decrypted = DecryptTbl(raw);

            using var ms = new MemoryStream(decrypted);
            using var reader = new BinaryReader(ms, Encoding.ASCII);

            // Step 2: Column header — N3TableBaseImpl.cpp:80-93
            int columnCount = reader.ReadInt32();

            // TBL_DATA_TYPE enum (N3TableBaseImpl.h:9-21)
            // 1=DT_CHAR, 2=DT_BYTE, 3=DT_SHORT, 4=DT_WORD, 5=DT_INT, 6=DT_DWORD, 7=DT_STRING, 8=DT_FLOAT, 9=DT_DOUBLE
            var colTypes = new int[columnCount];
            for (int c = 0; c < columnCount; c++)
                colTypes[c] = reader.ReadInt32();

            // Step 3: Row count
            int rowCount = reader.ReadInt32();

            // Step 4: Row parse — __TABLE_FX 5 kolon
            for (int r = 0; r < rowCount; r++)
            {
                var entry = new FxTableEntry();

                for (int c = 0; c < columnCount; c++)
                {
                    switch (colTypes[c])
                    {
                        case 1: // DT_CHAR
                        case 2: // DT_BYTE
                        {
                            byte val = reader.ReadByte();
                            // __TABLE_FX kolon 4 (byAOE) — GameDef.h:1343
                            if (c == 4) entry.AOE = val;
                            break;
                        }
                        case 3: // DT_SHORT
                            reader.ReadInt16();
                            break;
                        case 4: // DT_WORD
                            reader.ReadUInt16();
                            break;
                        case 5: // DT_INT
                        {
                            int val = reader.ReadInt32();
                            // __TABLE_FX kolon 0 (dwID) — GameDef.h:1339
                            if (c == 0) entry.Id = val;
                            // __TABLE_FX kolon 3 (dwSoundID) — GameDef.h:1342
                            if (c == 3) entry.SoundId = val;
                            break;
                        }
                        case 6: // DT_DWORD
                        {
                            uint val = reader.ReadUInt32();
                            if (c == 0) entry.Id = (int)val;
                            if (c == 3) entry.SoundId = (int)val;
                            break;
                        }
                        case 7: // DT_STRING
                        {
                            int len = reader.ReadInt32();
                            string str = "";
                            if (len > 0 && len < 10000)
                            {
                                byte[] strBytes = reader.ReadBytes(len);
                                str = Encoding.ASCII.GetString(strBytes).TrimEnd('\0');
                            }
                            // __TABLE_FX kolon 1 (szName) — GameDef.h:1340
                            if (c == 1) entry.Name = str;
                            // __TABLE_FX kolon 2 (szFN) — GameDef.h:1341
                            if (c == 2) entry.FileName = str;
                            break;
                        }
                        case 8: // DT_FLOAT
                            reader.ReadSingle();
                            break;
                        case 9: // DT_DOUBLE
                            reader.ReadDouble();
                            break;
                    }
                }

                if (entry.Id > 0)
                {
                    _entries[entry.Id] = entry;
                }
            }

            IsLoaded = true;
        }

        /// <summary>
        /// FX ID'den entry bul.
        /// Open-KO birebir: s_pTbl_FXSource.Find(FXID) — N3FXMgr.cpp:46
        /// </summary>
        public static FxTableEntry Find(int fxId)
        {
            _entries.TryGetValue(fxId, out var entry);
            return entry;
        }

        /// <summary>Tüm kayıtları getir.</summary>
        public static IReadOnlyDictionary<int, FxTableEntry> GetAll() => _entries;

        /// <summary>
        /// Open-KO rolling key decrypt — N3TableBaseImpl.cpp:51-77 birebir.
        /// Aynı implementasyon ItemDataManager.DecryptTbl ile.
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
    /// Open-KO birebir: __TABLE_FX (GameDef.h:1337-1344)
    /// FX kaynak tablosu kaydı — FXID → .fxb dosya + ses eşlemesi.
    /// </summary>
    public class FxTableEntry
    {
        /// <summary>Open-KO: dwID — FX ID (ör: 10002 = FXID_BLOOD)</summary>
        public int Id;

        /// <summary>Open-KO: szName — Efekt adı</summary>
        public string Name = "";

        /// <summary>Open-KO: szFN — .fxb dosya yolu (ör: "fx\blood01.fxb")</summary>
        public string FileName = "";

        /// <summary>Open-KO: dwSoundID — Ses efekti ID</summary>
        public int SoundId;

        /// <summary>Open-KO: byAOE — Alan etkisi flag</summary>
        public byte AOE;
    }
}
