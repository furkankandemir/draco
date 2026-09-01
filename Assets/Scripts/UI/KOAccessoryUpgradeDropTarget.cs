using UnityEngine;
using UnityEngine.EventSystems;

namespace EntropyOnline.UI
{
    public class KOAccessoryUpgradeDropTarget : MonoBehaviour, IDropHandler
    {
        public SlotType slotType;
        public int slotIndex;

        public void OnDrop(PointerEventData eventData)
        {
            var dragSource = KOAccessoryDragHandler.CurrentDragSource;
            if (dragSource == null) return;

            if (dragSource.district != SlotDistrict.BagSlot) return;

            if (KOAccessoryUpgradeManager.Instance != null)
            {
                KOAccessoryUpgradeManager.Instance.TryPlaceItem(dragSource.slotIndex, slotType);
            }
        }
    }
}
