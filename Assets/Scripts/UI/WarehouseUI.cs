using UnityEngine;
using EntropyOnline.Network;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Warehouse UI katmanı.
    /// Veri yönetimi KOWarehouseManager tarafından yapılır.
    /// Bu sınıf KOWarehouseManager event'lerini dinleyerek UI günceller.
    /// 
    /// Open-KO: UIWareHouseDlg.cpp ReceiveMessage — cpp:315-408
    ///          btn_close → LeaveWareHouseState
    ///          btn_pageUp/btn_pageDown → sayfa navigasyonu
    ///          btn_gold/btn_goldWareHouse → gold transfer
    /// </summary>
    public class WarehouseUI : MonoBehaviour
    {
        public static WarehouseUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Initialize()
        {
            // KOUIManager UIF'den yüklüyor — burada ek işlem yok
        }

        private void OnEnable()
        {
            // KOWarehouseManager event'lerine abone ol
            var mgr = KOWarehouseManager.Instance;
            if (mgr != null)
            {
                mgr.OnWarehouseOpened  += RefreshWarehousePanel;
                mgr.OnWarehouseClosed  += HideWarehousePanel;
                mgr.OnPageChanged      += RefreshPage;
                mgr.OnOperationResult  += HandleResult;
            }
        }

        private void OnDisable()
        {
            var mgr = KOWarehouseManager.Instance;
            if (mgr != null)
            {
                mgr.OnWarehouseOpened  -= RefreshWarehousePanel;
                mgr.OnWarehouseClosed  -= HideWarehousePanel;
                mgr.OnPageChanged      -= RefreshPage;
                mgr.OnOperationResult  -= HandleResult;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================
        // UI REFRESH — KOWarehouseManager event handler'ları
        // ============================

        /// <summary>
        /// Depo açıldığında panel göster.
        /// Open-KO: MsgRecv_WareHouseOpen → cpp:6520-6521
        /// </summary>
        private void RefreshWarehousePanel()
        {
            var mgr = KOWarehouseManager.Instance;
            if (mgr == null || KOUIManager.Instance == null) return;

            // C++ birebir: EnterWareHouseStateEnd() → InitIconUpdate() → m_pMyWare[m_iCurPage] populate
            // Mevcut sayfanın warehouse slot'larını WarehouseSlot array'e çevir
            var pageSlots = new System.Collections.Generic.List<WarehouseSlot>();
            for (int i = 0; i < KOWarehouseManager.MAX_ITEM_TRADE; i++)
            {
                var slotData = mgr.GetSlotOnCurrentPage(i);
                if (slotData != null && slotData.ItemId > 0)
                {
                    pageSlots.Add(new WarehouseSlot
                    {
                        Slot = (byte)i,
                        ItemId = slotData.ItemId,
                        Durability = (short)slotData.Durability,
                        Count = (short)slotData.Count
                    });
                }
            }

            // Önce data'yı populate et (warehouse + inventory), sonra paneli göster
            KOUIManager.Instance.PopulateWarehouse(mgr.WareGold, pageSlots.ToArray());
            KOUIManager.Instance.ShowWarehouse(true);

        }

        /// <summary>
        /// Depo kapatıldığında panel gizle.
        /// Open-KO: LeaveWareHouseState → cpp:489-492
        /// </summary>
        private void HideWarehousePanel()
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowWarehouse(false);

        }

        /// <summary>
        /// Sayfa değiştiğinde slotları güncelle.
        /// Open-KO: cpp:354-372 (PageUp) / cpp:388-406 (PageDown)
        /// </summary>
        private void RefreshPage(int page)
        {
            
            // C++ UIWareHouseDlg.cpp InitIconUpdate() birebir:
            // Mevcut sayfanın slotlarını WarehouseSlot array'e çevirip PopulateWarehouse çağır
            var mgr = KOWarehouseManager.Instance;
            if (mgr == null || KOUIManager.Instance == null) return;

            // C++ satır 218-234: m_pMyWare[m_iCurPage][i] → ikon güncelle
            var pageSlots = new System.Collections.Generic.List<WarehouseSlot>();
            for (int i = 0; i < KOWarehouseManager.MAX_ITEM_TRADE; i++)
            {
                var slotData = mgr.GetSlotOnCurrentPage(i);
                if (slotData != null && slotData.ItemId > 0)
                {
                    pageSlots.Add(new WarehouseSlot
                    {
                        Slot = (byte)i,
                        ItemId = slotData.ItemId,
                        Durability = (short)slotData.Durability,
                        Count = (short)slotData.Count
                    });
                }
            }

            KOUIManager.Instance.PopulateWarehouse(mgr.WareGold, pageSlots.ToArray());
        }

        /// <summary>
        /// Sunucu sonucu geldiğinde.
        /// Open-KO: ReceiveResult*Msg — cpp:1138-1437
        /// </summary>
        private void HandleResult(byte subCommand, bool success)
        {
        }

        // ============================
        // UI ACTIONS — buton handler'ları
        // Open-KO: ReceiveMessage — cpp:315-408
        // ============================

        /// <summary>cpp:336-337: btn_close → LeaveWareHouseState()</summary>
        public void OnBtnClose()
        {
            KOWarehouseManager.Instance?.LeaveWareHouseState();
        }

        /// <summary>cpp:341-373: btn_pageUp → PageUp()</summary>
        public void OnBtnPageUp()
        {
            KOWarehouseManager.Instance?.PageUp();
        }

        /// <summary>cpp:375-407: btn_pageDown → PageDown()</summary>
        public void OnBtnPageDown()
        {
            KOWarehouseManager.Instance?.PageDown();
        }

        /// <summary>
        /// cpp:322-327: btn_gold → Gold deposit to warehouse
        /// Open-KO: s_pCountableItemEdit->Open(UIWND_WARE_HOUSE, UIWND_DISTRICT_TRADE_MY, true, true)
        /// </summary>
        public void OnBtnGoldDeposit(int amount)
        {
            KOWarehouseManager.Instance?.GoldDepositToWare(amount);
        }

        /// <summary>
        /// cpp:329-334: btn_goldWareHouse → Gold withdraw from warehouse
        /// Open-KO: s_pCountableItemEdit->Open(UIWND_WARE_HOUSE, UIWND_DISTRICT_TRADE_NPC, true, true)
        /// </summary>
        public void OnBtnGoldWithdraw(int amount)
        {
            KOWarehouseManager.Instance?.GoldWithdrawFromWare(amount);
        }

        /// <summary>
        /// Deposit item (inv→ware).
        /// Open-KO: SendToServerToWareMsg — cpp:1076-1093
        /// </summary>
        public void SendDeposit(int itemId, byte page, byte srcSlot, byte dstSlot, int count)
        {
            KOWarehouseManager.Instance?.SendToServerToWareMsg(itemId, page, srcSlot, dstSlot, count);
        }

        /// <summary>
        /// Withdraw item (ware→inv).
        /// Open-KO: SendToServerFromWareMsg — cpp:1095-1108
        /// </summary>
        public void SendWithdraw(int itemId, byte page, byte srcSlot, byte dstSlot, int count)
        {
            KOWarehouseManager.Instance?.SendToServerFromWareMsg(itemId, page, srcSlot, dstSlot, count);
        }

        /// <summary>
        /// Move item within warehouse (ware→ware).
        /// Open-KO: SendToServerWareToWareMsg — cpp:1110-1122
        /// </summary>
        public void SendWareToWare(int itemId, byte page, byte srcSlot, byte dstSlot)
        {
            KOWarehouseManager.Instance?.SendToServerWareToWareMsg(itemId, page, srcSlot, dstSlot);
        }

        /// <summary>
        /// Move item within inventory (inv→inv while warehouse open).
        /// Open-KO: SendToServerInvToInvMsg — cpp:1124-1136
        /// </summary>
        public void SendInvToInv(int itemId, byte page, byte srcSlot, byte dstSlot)
        {
            KOWarehouseManager.Instance?.SendToServerInvToInvMsg(itemId, page, srcSlot, dstSlot);
        }
    }
}
