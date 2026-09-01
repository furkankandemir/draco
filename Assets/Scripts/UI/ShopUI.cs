using UnityEngine;
using EntropyOnline.Core;
using EntropyOnline.Network.KO;

namespace EntropyOnline.UI
{
    /// <summary>
    /// NPC Mağaza packet handler'ı.
    /// UI artık KOUIManager tarafından el_transaction_us.uif'den yükleniyor.
    /// Bu sınıf sadece shop paketlerini işler.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (Instance != this) return;
            KOPacketHandler.OnTradeNpc += HandleTradeNpc_KO;
            KOPacketHandler.OnRepairNpc += HandleRepairNpc_KO;
            KOPacketHandler.OnItemTradeResult += HandleItemTradeResult_KO;
        }

        private void OnDestroy()
        {
            KOPacketHandler.OnTradeNpc -= HandleTradeNpc_KO;
            KOPacketHandler.OnRepairNpc -= HandleRepairNpc_KO;
            KOPacketHandler.OnItemTradeResult -= HandleItemTradeResult_KO;
            if (Instance == this) Instance = null;
        }

        // ============================
        // KO WRAPPER HANDLERS
        // ============================

        /// <summary>KO birebir — WIZ_TRADE_NPC (GameProcMain.cpp:4386-4395)</summary>
        private void HandleTradeNpc_KO(byte[] rawData)
        {
            // C++ birebir: MsgRecv_ItemTradeStart (GameProcMain.cpp:4386-4395)
            // Wire: [opcode][tradeId:uint32]
            var r = new KOPacketReader(rawData);
            int tradeId = (int)r.ReadUInt32(); // cpp:4388

            // C++ birebir: cpp:4392
            // m_pUINpcEvent->Open(NPC_EVENT_ITEM_TRADE, iTradeID, pNPC->GetNPCOriginID());
            // NPC_EVENT_ITEM_TRADE = 0
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowNpcEvent(tradeId, 0); // 0 = NPC_EVENT_ITEM_TRADE

        }

        /// <summary>KO birebir — WIZ_REPAIR_NPC (GameProcMain.cpp:6305-6313)</summary>
        private void HandleRepairNpc_KO(byte[] rawData)
        {
            // C++ birebir: MsgRecv_NpcEvent (GameProcMain.cpp:6305-6313)
            // Wire: [opcode][tradeId:uint32]
            var r = new KOPacketReader(rawData);
            int tradeId = (int)r.ReadUInt32(); // cpp:6307

            // C++ birebir: cpp:6312
            // m_pUINpcEvent->Open(NPC_EVENT_TRADE_REPAIR, iTradeID, pNPC->GetNPCOriginID());
            // NPC_EVENT_TRADE_REPAIR = 1
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowNpcEvent(tradeId, 1); // 1 = NPC_EVENT_TRADE_REPAIR

        }

        // ============================
        // PACKET SEND
        // ============================

        /// <summary>
        /// Open-KO birebir: SendToServerBuyMsg (UITransactionDlg.cpp:775-788)
        /// C++ Wire: WIZ_ITEM_TRADE [N3_SP_TRADE_BUY=1:byte] [m_iTradeID:uint32] [m_iNpcID:int16] [itemID:uint32] [pos:byte] [iCount:int16]
        /// </summary>
        public void BuyItem(int tradeId, byte slotPos, uint itemDefId, short quantity = 1)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;

            uint serverItemId = EntropyOnline.Import.KOItemMapping.GetServerItemId(itemDefId);

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_ITEM_TRADE);
            pkt.WriteByte(1); // N3_SP_TRADE_BUY
            pkt.WriteUInt32((uint)tradeId);   // cpp:781 — m_iTradeID
            pkt.WriteInt16((short)_lastNpcId); // cpp:782 — m_iNpcID
            pkt.WriteUInt32(serverItemId);     // cpp:783 — itemID
            pkt.WriteByte(slotPos);            // cpp:784 — pos
            pkt.WriteInt16(quantity);           // cpp:785 — iCount
            netMgr.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: SendToServerSellMsg (UITransactionDlg.cpp:762-773)
        /// C++ Wire: WIZ_ITEM_TRADE [N3_SP_TRADE_SELL=2:byte] [itemID:uint32] [pos:byte] [iCount:int16]
        /// NOT: Sell paketi tradeId İÇERMEZ — sadece itemID, pos, count.
        /// </summary>
        public void SellItem(int tradeId, byte slotPos, uint itemDefId, short quantity = 1)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;

            uint serverItemId = EntropyOnline.Import.KOItemMapping.GetServerItemId(itemDefId);

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_ITEM_TRADE);
            pkt.WriteByte(2); // N3_SP_TRADE_SELL
            pkt.WriteUInt32(serverItemId); // cpp:768 — itemID
            pkt.WriteByte(slotPos);     // cpp:769 — pos
            pkt.WriteInt16(quantity);    // cpp:770 — iCount
            netMgr.SendPacket(pkt);
        }

        /// <summary>Son etkileşilen NPC ID — C++ m_iNpcID karşılığı</summary>
        private int _lastNpcId;

        /// <summary>NPC ID'yi kaydet — shop açıldığında çağrılır</summary>
        public void SetNpcId(int npcId) { _lastNpcId = npcId; }

        /// <summary>Son etkileşilen NPC ID'yi döndürür</summary>
        public int GetNpcId() { return _lastNpcId; }

        // ============================
        // TRADE RESULT HANDLER — C++ birebir: GameProcMain.cpp:4397-4428
        // ============================

        /// <summary>
        /// C++ birebir: MsgRecv_ItemTradeResult (GameProcMain.cpp:4397-4428)
        /// Wire: [result:byte] then:
        ///   0x00 = fail → [bfType:byte]
        ///   0x01 = success → [iMoney:uint32]
        ///   0x03 = move success
        ///   0x04 = move fail
        /// On success: update gold + refresh shop/inventory UI.
        /// </summary>
        private void HandleItemTradeResult_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte bResult = r.ReadByte();

            switch (bResult)
            {
                case 0x00: // fail — C++ birebir: UITransactionDlg.cpp:1198-1246
                {
                    byte bfType = r.ReadByte();
                    if (bfType == 0x04)
                        Debug.LogWarning("[SHOP] Trade fail: IDS_ITEM_TOOMANY_OR_HEAVY");
                    else
                        Debug.LogWarning($"[SHOP] Trade fail: bfType=0x{bfType:X2}");

                    // C++ birebir: fail → lokale eklenen item'ı revert et
                    if (KOUIManager.Instance != null)
                    {
                        KOUIManager.Instance.RevertPendingTradeFromInventory();
                        KOUIManager.Instance.RefreshShopInventory();
                    }
                    break;
                }
                case 0x01: // success — C++ birebir: UITransactionDlg.cpp ReceiveResultTradeFromServer
                {
                    // C++ birebir: cpp:1256/1322 — pInfoExt->iGold = iMoney
                    uint iMoney = r.ReadUInt32();
                    var gm = GameManager.Instance;
                    if (gm != null)
                        gm.Gold = (int)iMoney;

                    // C++ birebir: cpp:1195 — switch (s_sRecoveryJobInfo.UIWndSourceStart.UIWndDistrict)
                    if (KOUIManager.Instance != null)
                    {
                        if (KOUIManager.Instance.IsShopPendingBuy)
                        {
                            // C++ birebir: UIWND_DISTRICT_TRADE_NPC (buy) cpp:1254-1260
                            // Item zaten m_pMyTradeInv'de — sadece gold güncelle
                            KOUIManager.Instance.UpdateTransactionGoldPublic();
                        }
                        else
                        {
                            // C++ birebir: UIWND_DISTRICT_TRADE_MY (sell) cpp:1295-1326
                            // cpp:1305-1308 — m_pMyTradeInv'den tamamen sil
                            KOUIManager.Instance.CompleteSellInTradeInv();
                            // cpp:1324-1325 — gold güncelle
                            KOUIManager.Instance.UpdateTransactionGoldPublic();
                        }
                        KOUIManager.Instance.RefreshShopInventory();
                    }

                    break;
                }
                case 0x03: // move success
                    break;
                case 0x04: // move fail
                    break;
                default:
                    Debug.LogWarning($"[SHOP] Unknown trade result: 0x{bResult:X2}");
                    break;
            }
        }
    }
}
