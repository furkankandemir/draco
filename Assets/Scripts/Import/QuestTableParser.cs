using System.Collections.Generic;
using System.IO;
using System.Text;
using EntropyOnline.Import;
using UnityEngine;

namespace KOImport
{
    /// <summary>
    /// Open-KO birebir: Quest .tbl parser — 3 quest tablosu.
    ///
    /// GameBase.cpp:62-66:
    ///   szFN = "Data\\Quest_Menu" + szLangTail; s_pTbl_QuestMenu.LoadFromFile(szFN);
    ///   szFN = "Data\\Quest_Talk" + szLangTail; s_pTbl_QuestTalk.LoadFromFile(szFN);
    ///   szFN = "Data\\Quest_Content" + szLangTail; s_pTbl_QuestContent.LoadFromFile(szFN);
    ///
    /// __TABLE_QUEST_MENU (GameDef.h:1212-1216):
    ///   uint32 dwID     — 01 ID
    ///   string szMenu   — 02 Menu text
    ///
    /// __TABLE_QUEST_TALK (GameDef.h:1218-1222):
    ///   uint32 dwID     — 01 ID
    ///   string szTalk   — 02 Dialogue text
    ///
    /// __TABLE_QUEST_CONTENT (GameDef.h:1224-1232):
    ///   uint32 dwID       — 01 ID
    ///   int    iReqLevel  — 02 Required level
    ///   int    iReqClass  — 03 Required class
    ///   string szName     — 04 Quest name
    ///   string szDesc     — 05 Quest description
    ///   string szReward   — 06 Reward text
    ///
    /// TBL decrypt: N3TableBaseImpl.cpp:51-77 rolling key XOR (0x0816, 0x6081, 0x1608)
    ///
    /// UIQuestMenu.cpp:171-175 — SELECT_MSG paketi gelince:
    ///   talkId → s_pTbl_QuestTalk.Find(talkId) → szTalk
    ///   menuId → s_pTbl_QuestMenu.Find(menuId) → szMenu
    ///
    /// UIQuestTalk.cpp:50-53 — NPC_SAY paketi gelince:
    ///   msgId → s_pTbl_QuestTalk.Find(msgId) → szTalk
    /// </summary>
    public static class QuestTableParser
    {
        /// <summary>Quest_Menu_us.tbl → menuId → text eşleme</summary>
        private static readonly Dictionary<int, string> _menuTable = new();

        /// <summary>Quest_Talk_us.tbl → talkId → text eşleme</summary>
        private static readonly Dictionary<int, string> _talkTable = new();

        /// <summary>Quest_Content_us.tbl → contentId → QuestContentEntry eşleme</summary>
        private static readonly Dictionary<int, QuestContentEntry> _contentTable = new();

        /// <summary>Tüm quest tanımlarını döndürür</summary>
        public static Dictionary<int, QuestContentEntry> GetAllContent() => _contentTable;

        public static bool IsLoaded { get; private set; }

        /// <summary>Menü tablosu kayıt sayısı</summary>
        public static int MenuCount => _menuTable.Count;

        /// <summary>Diyalog tablosu kayıt sayısı</summary>
        public static int TalkCount => _talkTable.Count;

        /// <summary>İçerik tablosu kayıt sayısı</summary>
        public static int ContentCount => _contentTable.Count;

        /// <summary>
        /// 3 quest tablosunu yükle.
        /// Open-KO birebir: GameBase.cpp:62-66 (StaticMemberInit)
        /// </summary>
        public static void LoadAll(string dataDir)
        {
            _menuTable.Clear();
            _talkTable.Clear();
            _contentTable.Clear();

            // Önce convert edilmiş asset'ten yükle
            var asset = Resources.Load<EntropyOnline.Import.KOQuestDataAsset>("KOData/QuestData");
            if (asset != null)
            {
                if (asset.menuEntries != null)
                    foreach (var e in asset.menuEntries)
                        if (e != null && e.dwID > 0 && !string.IsNullOrEmpty(e.szMenu))
                            _menuTable[e.dwID] = e.szMenu;

                if (asset.talkEntries != null)
                    foreach (var e in asset.talkEntries)
                        if (e != null && e.dwID > 0 && !string.IsNullOrEmpty(e.szTalk))
                            _talkTable[e.dwID] = e.szTalk;

                if (asset.contentEntries != null)
                    foreach (var e in asset.contentEntries)
                        if (e != null && e.dwID > 0)
                            _contentTable[e.dwID] = new QuestContentEntry
                            {
                                Id = e.dwID,
                                ReqLevel = e.iReqLevel,
                                ReqClass = e.iReqClass,
                                Name = e.szName ?? "",
                                Description = e.szDesc ?? "",
                                Reward = e.szReward ?? ""
                            };

                IsLoaded = _menuTable.Count > 0 || _talkTable.Count > 0;
                return;
            }

            string menuPath = Path.Combine(dataDir, "Quest_Menu_us.tbl");
            string talkPath = Path.Combine(dataDir, "Quest_Talk_us.tbl");
            string contentPath = Path.Combine(dataDir, "Quest_Content_us.tbl");

            // Quest_Menu_us.tbl — 2 kolon: dwID(uint32) + szMenu(string)
            byte[] menuRaw = KOTableProvider.LoadRaw(menuPath);
            if (menuRaw != null)
            {
                LoadMenuTable(menuRaw);
            }
            else
            {
                Debug.LogWarning($"[QuestTableParser] Quest_Menu_us.tbl bulunamadı: {menuPath}");
            }

            // Quest_Talk_us.tbl — 2 kolon: dwID(uint32) + szTalk(string)
            byte[] talkRaw = KOTableProvider.LoadRaw(talkPath);
            if (talkRaw != null)
            {
                LoadTalkTable(talkRaw);
            }
            else
            {
                Debug.LogWarning($"[QuestTableParser] Quest_Talk_us.tbl bulunamadı: {talkPath}");
            }

            // Quest_Content_us.tbl — 6 kolon: dwID + iReqLevel + iReqClass + szName + szDesc + szReward
            byte[] contentRaw = KOTableProvider.LoadRaw(contentPath);
            if (contentRaw != null)
            {
                LoadContentTable(contentRaw);
            }
            else
            {
                Debug.LogWarning($"[QuestTableParser] Quest_Content_us.tbl bulunamadı: {contentPath}");
            }

            IsLoaded = _menuTable.Count > 0 || _talkTable.Count > 0;
        }

        // ================================================================
        // Open-KO: s_pTbl_QuestMenu.Find(menuId)
        // UIQuestMenu.cpp:186-188
        // ================================================================

        /// <summary>
        /// Menü ID'den buton metnini bul.
        /// Open-KO: __TABLE_QUEST_MENU.szMenu
        /// </summary>
        public static string FindMenu(int menuId)
        {
            _menuTable.TryGetValue(menuId, out string text);
            return text;
        }

        // ================================================================
        // Open-KO: s_pTbl_QuestTalk.Find(talkId)
        // UIQuestMenu.cpp:171-175, UIQuestTalk.cpp:50-53
        // ================================================================

        /// <summary>
        /// Talk ID'den diyalog metnini bul.
        /// Open-KO: __TABLE_QUEST_TALK.szTalk
        /// </summary>
        public static string FindTalk(int talkId)
        {
            _talkTable.TryGetValue(talkId, out string text);
            return text;
        }

        // ================================================================
        // Open-KO: s_pTbl_QuestContent.Find(contentId)
        // ================================================================

        /// <summary>
        /// Content ID'den quest bilgisini bul.
        /// Open-KO: __TABLE_QUEST_CONTENT
        /// </summary>
        public static QuestContentEntry FindContent(int contentId)
        {
            _contentTable.TryGetValue(contentId, out var entry);
            return entry;
        }

        // ================================================================
        // Quest_Menu_us.tbl parser
        // __TABLE_QUEST_MENU: dwID(uint32), szMenu(string) — 2 kolon
        // ================================================================

        private static void LoadMenuTable(byte[] raw)
        {
            byte[] decrypted = DecryptTbl(raw);

            using var ms = new MemoryStream(decrypted);
            using var reader = new BinaryReader(ms, Encoding.ASCII);

            // Column header — N3TableBaseImpl.cpp:80-93
            int columnCount = reader.ReadInt32();
            var colTypes = new int[columnCount];
            for (int c = 0; c < columnCount; c++)
                colTypes[c] = reader.ReadInt32();

            int rowCount = reader.ReadInt32();

            for (int r = 0; r < rowCount; r++)
            {
                int id = 0;
                string menu = "";

                for (int c = 0; c < columnCount; c++)
                {
                    switch (colTypes[c])
                    {
                        case 5: // DT_INT
                        {
                            int val = reader.ReadInt32();
                            if (c == 0) id = val;
                            break;
                        }
                        case 6: // DT_DWORD
                        {
                            uint val = reader.ReadUInt32();
                            if (c == 0) id = (int)val;
                            break;
                        }
                        case 7: // DT_STRING
                        {
                            int len = reader.ReadInt32();
                            string str = "";
                            if (len > 0 && len < 100000)
                            {
                                byte[] strBytes = reader.ReadBytes(len);
                                str = Encoding.ASCII.GetString(strBytes).TrimEnd('\0');
                            }
                            if (c == 1) menu = str;
                            break;
                        }
                        default:
                            SkipColumn(reader, colTypes[c]);
                            break;
                    }
                }

                if (id > 0 && !string.IsNullOrEmpty(menu))
                    _menuTable[id] = menu;
            }
        }

        // ================================================================
        // Quest_Talk_us.tbl parser
        // __TABLE_QUEST_TALK: dwID(uint32), szTalk(string) — 2 kolon
        // ================================================================

        private static void LoadTalkTable(byte[] raw)
        {
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
                int id = 0;
                string talk = "";

                for (int c = 0; c < columnCount; c++)
                {
                    switch (colTypes[c])
                    {
                        case 5: // DT_INT
                        {
                            int val = reader.ReadInt32();
                            if (c == 0) id = val;
                            break;
                        }
                        case 6: // DT_DWORD
                        {
                            uint val = reader.ReadUInt32();
                            if (c == 0) id = (int)val;
                            break;
                        }
                        case 7: // DT_STRING
                        {
                            int len = reader.ReadInt32();
                            string str = "";
                            if (len > 0 && len < 100000)
                            {
                                byte[] strBytes = reader.ReadBytes(len);
                                str = Encoding.ASCII.GetString(strBytes).TrimEnd('\0');
                            }
                            if (c == 1) talk = str;
                            break;
                        }
                        default:
                            SkipColumn(reader, colTypes[c]);
                            break;
                    }
                }

                if (id > 0 && !string.IsNullOrEmpty(talk))
                {
                    // Open-KO: CGameBase::ConvertPipesToNewlines — pipe (|) → newline
                    // UIQuestMenu.cpp:176
                    talk = talk.Replace('|', '\n');
                    _talkTable[id] = talk;
                }
            }
        }

        // ================================================================
        // Quest_Content_us.tbl parser
        // __TABLE_QUEST_CONTENT: dwID, iReqLevel, iReqClass, szName, szDesc, szReward — 6 kolon
        // ================================================================

        private static void LoadContentTable(byte[] raw)
        {
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
                var entry = new QuestContentEntry();

                for (int c = 0; c < columnCount; c++)
                {
                    switch (colTypes[c])
                    {
                        case 5: // DT_INT
                        {
                            int val = reader.ReadInt32();
                            if (c == 0) entry.Id = val;
                            else if (c == 1) entry.ReqLevel = val;
                            else if (c == 2) entry.ReqClass = val;
                            break;
                        }
                        case 6: // DT_DWORD
                        {
                            uint val = reader.ReadUInt32();
                            if (c == 0) entry.Id = (int)val;
                            break;
                        }
                        case 7: // DT_STRING
                        {
                            int len = reader.ReadInt32();
                            string str = "";
                            if (len > 0 && len < 100000)
                            {
                                byte[] strBytes = reader.ReadBytes(len);
                                str = Encoding.ASCII.GetString(strBytes).TrimEnd('\0');
                            }
                            if (c == 3) entry.Name = str;
                            else if (c == 4) entry.Description = str.Replace('|', '\n');
                            else if (c == 5) entry.Reward = str;
                            break;
                        }
                        default:
                            SkipColumn(reader, colTypes[c]);
                            break;
                    }
                }

                if (entry.Id > 0)
                    _contentTable[entry.Id] = entry;
            }
        }

        // ================================================================
        // Yardımcı
        // ================================================================

        /// <summary>Bilinmeyen kolon tipini atla.</summary>
        private static void SkipColumn(BinaryReader reader, int colType)
        {
            switch (colType)
            {
                case 1: // DT_CHAR
                case 2: // DT_BYTE
                    reader.ReadByte();
                    break;
                case 3: // DT_SHORT
                    reader.ReadInt16();
                    break;
                case 4: // DT_WORD
                    reader.ReadUInt16();
                    break;
                case 5: // DT_INT
                    reader.ReadInt32();
                    break;
                case 6: // DT_DWORD
                    reader.ReadUInt32();
                    break;
                case 7: // DT_STRING
                {
                    int len = reader.ReadInt32();
                    if (len > 0 && len < 100000)
                        reader.ReadBytes(len);
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
    /// Open-KO birebir: __TABLE_QUEST_CONTENT (GameDef.h:1224-1232)
    /// </summary>
    public class QuestContentEntry
    {
        /// <summary>Open-KO: dwID — Quest content ID</summary>
        public int Id;

        /// <summary>Open-KO: iReqLevel — Gerekli seviye</summary>
        public int ReqLevel;

        /// <summary>Open-KO: iReqClass — Gerekli sınıf</summary>
        public int ReqClass;

        /// <summary>Open-KO: szName — Quest adı</summary>
        public string Name = "";

        /// <summary>Open-KO: szDesc — Quest açıklaması</summary>
        public string Description = "";

        /// <summary>Open-KO: szReward — Ödül açıklaması</summary>
        public string Reward = "";
    }
}
