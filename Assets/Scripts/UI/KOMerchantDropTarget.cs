using UnityEngine;
using UnityEngine.EventSystems;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Merchant Setup slotlarına eklenen drag-drop hedefi.
    /// Envanter slotundan sürüklenen item buraya bırakıldığında KOUIManager üzerinden adet/fiyat girişi başlar.
    /// </summary>
    public class KOMerchantDropTarget : MonoBehaviour, IDropHandler
    {
        [HideInInspector] public int merchantSlotIndex; // 0..11

        public void OnDrop(PointerEventData eventData)
        {
            var dragSource = KOItemDragHandler.CurrentDragSource;
            if (dragSource == null) return;

            // Kaynak envanter olmalı
            if (dragSource.district != KOItemDragHandler.SlotDistrict.BagSlot && 
                dragSource.district != (KOItemDragHandler.SlotDistrict)3) // MerchantSetupInvSlot fallback
            {
                return;
            }

            int srcInvPos = dragSource.slotIndex;
            var koInv = EntropyOnline.UI.KOInventory.Instance;
            if (koInv == null || srcInvPos < 0 || srcInvPos >= 28) return;

            var slotItem = koInv.m_pMyInvWnd[srcInvPos];
            if (slotItem == null || slotItem.IsEmpty) return;


            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.PromptMerchantItemAdd(srcInvPos, merchantSlotIndex);
            }
        }
    }
}
