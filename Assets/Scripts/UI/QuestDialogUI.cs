using UnityEngine;
using EntropyOnline.Network.KO;
using System.Collections.Generic;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Quest / NPC Diyalog paket handler'ı.
    /// UI panelleri KOUIManager tarafından UIF dosyalarından oluşturuluyor:
    ///   - co_QuestTalk_us.uif → KOUIManager.ShowQuestTalk()
    ///   - co_QuestMenu_us.uif → KOUIManager.ShowQuestMenu()
    /// Bu sınıf sadece paketleri parse edip KOUIManager'a yönlendirir.
    ///
    /// C++ Referans:
    ///   Server: SendNpcSay() → WIZ_NPC_SAY (User.cpp:13081-13096)
    ///   Server: SelectMsg() → WIZ_SELECT_MSG (User.cpp:13125-13156)
    /// </summary>
    public class QuestDialogUI : MonoBehaviour
    {
        public static QuestDialogUI Instance { get; private set; }

        // Yerel quest state cache
        private readonly Dictionary<short, byte> _questStates = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (Instance != this) return;
            // KOPacketHandler event'lerine abone ol
            KOPacketHandler.OnNpcSay += HandleNpcSay_KO;
            KOPacketHandler.OnSelectMsg += HandleSelectMsg_KO;
            KOPacketHandler.OnQuest += HandleQuest_KO;
        }

        private void OnDestroy()
        {
            KOPacketHandler.OnNpcSay -= HandleNpcSay_KO;
            KOPacketHandler.OnSelectMsg -= HandleSelectMsg_KO;
            KOPacketHandler.OnQuest -= HandleQuest_KO;
            if (Instance == this) Instance = null;
        }

        // ================================================================
        // WIZ_NPC_SAY — Open-KO birebir: SendNpcSay (User.cpp:13081-13096)
        // Wire: [opcode][10 × int32]
        //   int32[0] = eventIdUp, int32[1] = eventIdOk, int32[2-9] = msg1..msg8
        // ================================================================

        private void HandleNpcSay_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            int eventIdUp = r.ReadInt32(); // cpp:13091 — pExec->m_ExecInt[0]
            int eventIdOk = r.ReadInt32(); // cpp:13092 — pExec->m_ExecInt[1]
            int[] messageIds = new int[8];
            for (int i = 0; i < 8; i++)
                messageIds[i] = r.ReadInt32(); // cpp:13093 — pExec->m_ExecInt[2-9]

            // KOUIManager'daki C++ birebir panele yönlendir
            // UIQuestTalk.cpp:36-66 birebir
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowQuestTalk(eventIdUp, eventIdOk, messageIds);

        }

        // ================================================================
        // WIZ_SELECT_MSG — Open-KO birebir: SelectMsg (User.cpp:13125-13156)
        // Wire: [opcode][npcId:int16][talkId:int32][menuText × MAX_MESSAGE_EVENT(10):int32]
        // ================================================================

        private void HandleSelectMsg_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            short npcId = r.ReadInt16();    // cpp:13134 — m_sEventNid
            int talkId = r.ReadInt32();     // cpp:13136 — pExec->m_ExecInt[1]

            // Custom Notification: -1 NPC ID signals that a gem/chest drop has been stocked in warehouse
            if (npcId == -1)
            {
                if (KOGemChestExchangeManager.Instance != null && KOGemChestExchangeManager.Instance.gameObject.activeSelf)
                {
                    KOGemChestExchangeManager.Instance.OnWarehouseItemWon(talkId);
                }
                return;
            }

            int[] menuTextIds = new int[10]; // MAX_MESSAGE_EVENT = 10
            for (int i = 0; i < 10; i++)
                menuTextIds[i] = r.ReadInt32(); // cpp:13140 — pExec->m_ExecInt[chat]

            // KOUIManager'daki C++ birebir panele yönlendir
            // UIQuestMenu.cpp:149-254 birebir
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowQuestMenu(npcId, talkId, menuTextIds);

        }

        // ================================================================
        // WIZ_QUEST — Open-KO birebir: GameProcMain.cpp:1078-1090
        // Wire: [opcode][start:byte][questId:uint16][state:byte]
        // ================================================================

        // Görev durumları değiştikçe UI ve HUD listelerini yenilemek için event
        public static event System.Action OnQuestStatesChanged;

        private void HandleQuest_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte start = r.ReadByte();
            short questId = (short)r.ReadUInt16();
            byte state = r.ReadByte();

            _questStates[questId] = state;

            // Olayı tetikle
            OnQuestStatesChanged?.Invoke();
        }

        /// <summary>Tüm görevlerin durum listesini döndürür.</summary>
        public Dictionary<short, byte> GetQuestStates()
        {
            return _questStates;
        }

        /// <summary>Belirli bir quest'in mevcut state'ini döndürür.</summary>
        public byte GetQuestState(short questId)
        {
            return _questStates.TryGetValue(questId, out byte state) ? state : (byte)0;
        }
    }
}
