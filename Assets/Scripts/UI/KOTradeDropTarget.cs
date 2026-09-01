using UnityEngine;
using UnityEngine.EventSystems;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: UIPerTradeDlg::ReceiveIconDrop (UIPerTradeDlg.cpp:534-721)
    /// 
    /// Trade MY area slot'larına eklenen drop target component.
    /// Envanter slot'undan (KOItemDragHandler) sürüklenen item burada bırakıldığında
    /// ReceiveIconDrop akışı tetiklenir.
    /// 
    /// C++ akışı:
    ///   1. GetChildAreaByiOrder(UI_AREA_TYPE_PER_TRADE_MY, i) → IsIn(ptCur)
    ///   2. Countable mı? → Adet gir popup
    ///   3. Non-countable → Direkt SendToServerItemAddMsg(pos, itemID, 1)
    /// </summary>
    public class KOTradeDropTarget : MonoBehaviour, IDropHandler
    {
        /// <summary>Trade MY area slot index (0-11)</summary>
        [HideInInspector] public int tradeSlotIndex;

        /// <summary>
        /// Open-KO birebir: UIPerTradeDlg::ReceiveIconDrop (satır 534-721)
        /// Envanter item'ı trade MY area'ya bırakıldığında çağrılır.
        /// </summary>
        public void OnDrop(PointerEventData eventData)
        {
            int srcInvPos = -1;

            // 1. Try KOItemDragHandler (from normal inventory window)
            var dragSource = KOItemDragHandler.CurrentDragSource;
            if (dragSource != null)
            {
                if (dragSource.district != KOItemDragHandler.SlotDistrict.BagSlot)
                {
                    return;
                }
                srcInvPos = dragSource.slotIndex;
            }

            // 2. Try KOItemDragDrop (from trade panel's bottom inventory slots)
            if (srcInvPos < 0)
            {
                var dragDropSource = KOItemDragDrop.DragSource;
                if (dragDropSource != null)
                {
                    if (dragDropSource.slotType != KOItemSlotHandler.SlotType.BagSlot)
                    {
                        return;
                    }
                    srcInvPos = dragDropSource.slotIndex;
                }
            }

            if (srcInvPos < 0)
            {
                return;
            }

            // C++ satır 537: m_bVisible kontrolü
            var tradeMgr = EntropyOnline.Trade.KOTradeManager.Instance;
            if (tradeMgr == null || tradeMgr.State != EntropyOnline.Trade.KOTradeManager.PerTradeState.Normal)
            {
                Debug.LogWarning("[TRADE-DROP] Trade normal state değil, drop reddedildi.");
                return;
            }

            // Envanter item bilgisini al
            var koInv = KOInventory.Instance;
            if (koInv == null) return;

            if (srcInvPos < 0 || srcInvPos >= 28) return;

            var slotItem = koInv.m_pMyInvWnd[srcInvPos];
            if (slotItem == null || slotItem.IsEmpty)
            {
                return;
            }
            var srcItem = slotItem.serverData;

            // C++ satır 569-570: s_bWaitFromServer = true, m_ePerTradeItemKindBackup = PER_TRADE_ITEM_OTHER
            // Bu satır countable/non-countable dallanmasından ÖNCE çalışır
            tradeMgr.SetPerTradeItemKindBackup(true); // true = OTHER

            // C++ satır 580-581: Countable kontrolü
            // Open-KO: UIITEM_TYPE_COUNTABLE (1) veya UIITEM_TYPE_COUNTABLE_SMALL (2)
            var itemTbl = KOImport.ItemDataManager.GetItemBasic(slotItem.itemId);
            bool isCountable = slotItem.count > 1;

            if (isCountable)
            {
                // C++ satır 582-671: Countable item akışı
                // Mevcut trade slot'unda aynı item var mı?
                int destSlot = -1;
                for (int i = 0; i < EntropyOnline.Trade.KOTradeManager.MAX_ITEM_PER_TRADE; i++)
                {
                    var existing = tradeMgr.MySlots[i];
                    if (existing != null && existing.ItemId == srcItem.ItemDefId)
                    {
                        destSlot = i;
                        break;
                    }
                }

                // C++ satır 598-610: Bulamadıysa boş slot ara
                if (destSlot < 0)
                {
                    for (int i = 0; i < EntropyOnline.Trade.KOTradeManager.MAX_ITEM_PER_TRADE; i++)
                    {
                        if (tradeMgr.MySlots[i] == null || tradeMgr.MySlots[i].ItemId == 0)
                        {
                            destSlot = i;
                            break;
                        }
                    }
                }

                // C++ satır 612-621: Boş slot yoksa fail
                if (destSlot < 0)
                {
                    Debug.LogWarning("[TRADE-DROP] Trade slotları dolu, countable item eklenemez.");
                    return;
                }

                // C++ satır 627-661: Trade slot boşsa hemen iCount=0 ile oluştur (popup'tan ÖNCE)
                // Satır 637: spItemNew->iCount = 0
                if (tradeMgr.MySlots[destSlot] == null || tradeMgr.MySlots[destSlot].ItemId == 0)
                {
                    tradeMgr.MySlots[destSlot] = new EntropyOnline.Trade.TradeSlotItem
                    {
                        ItemId = srcItem.ItemDefId,
                        Count = 0, // C++ satır 637: iCount = 0
                        Durability = srcItem.Durability,
                        OriginalInvSlot = srcInvPos
                    };
                }

                // C++ satır 663-667: Backup bilgileri kaydet ve count edit popup aç
                tradeMgr.SetPendingCountableAdd(srcInvPos, destSlot, srcItem.ItemDefId, srcItem.StackCount);

                // C++ satır 667: s_pCountableItemEdit->Open(UIWND_PER_TRADE, UIWND_DISTRICT_PER_TRADE_MY, false)
                tradeMgr.RequestItemCountEditForItem();

            }
            else
            {
                // C++ satır 673-702: Non-countable item akışı
                // C++ satır 676-682: Boş MY slot bul
                int destSlot = -1;
                for (int i = 0; i < EntropyOnline.Trade.KOTradeManager.MAX_ITEM_PER_TRADE; i++)
                {
                    if (tradeMgr.MySlots[i] == null || tradeMgr.MySlots[i].ItemId == 0)
                    {
                        destSlot = i;
                        break;
                    }
                }

                // C++ satır 685-692: Boş slot yoksa fail
                if (destSlot < 0)
                {
                    Debug.LogWarning("[TRADE-DROP] Trade slotları dolu, non-countable item eklenemez.");
                    return;
                }

                // C++ satır 695: s_sRecoveryJobInfo.UIWndSourceEnd.iOrder = i
                // C++ satır 696: m_iBackupiOrder[i] = srcOrder
                // Fail recovery için exact trade slot index'i kaydet
                tradeMgr.SetPendingNonCountableAdd(srcInvPos, destSlot);

                // C++ satır 704-708: Item'ı hemen trade slot'a taşı (optimistic — sunucu yanıtı beklenmez)
                tradeMgr.MySlots[destSlot] = new EntropyOnline.Trade.TradeSlotItem
                {
                    ItemId = srcItem.ItemDefId,
                    Count = 1,
                    Durability = srcItem.Durability,
                    OriginalInvSlot = srcInvPos
                };

                // Clear/remove from inventory immediately
                koInv.m_pMyInvWnd[srcInvPos] = null;
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.StartCoroutine(DeferRefreshInventoryUI());
                }

                // C++ satır 698-700: SendToServerItemAddMsg(pos, itemID, 1)
                tradeMgr.SendExchangeAddItem((byte)srcInvPos, srcItem.ItemDefId, 1);

            }
        }

        private System.Collections.IEnumerator DeferRefreshInventoryUI()
        {
            yield return new UnityEngine.WaitForEndOfFrame();
            KOUIManager.Instance?.RefreshInventoryUI();
        }
    }
}
