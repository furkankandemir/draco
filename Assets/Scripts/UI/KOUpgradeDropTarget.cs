using UnityEngine;
using UnityEngine.EventSystems;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: UIItemUpgrade.cpp:343-377 — ReceiveIconDrop() mobil portu.
    /// 
    /// Upgrade panelindeki hedef area'lara (a_upgrade, a_m_0..8) sürükle-bırak desteği.
    /// KOItemDragHandler'dan gelen drop event'lerini yakalar ve
    /// KOItemUpgradeManager'a yönlendirir.
    /// 
    /// C++ karşılığı:
    ///   ReceiveIconDrop() satır 349-361: upgrade slot'a drop
    ///   ReceiveIconDrop() satır 363-374: requirement slot'a drop
    /// </summary>
    public class KOUpgradeDropTarget : MonoBehaviour, IDropHandler
    {
        public enum TargetType
        {
            /// <summary>C++ m_pAreaUpgrade — "a_upgrade" (cpp:621)</summary>
            UpgradeSlot,

            /// <summary>C++ m_pSlotArea[i] — "a_m_X" (cpp:640-641)</summary>
            RequirementSlot
        }

        [Header("Slot Bilgisi")]
        public TargetType targetType;

        /// <summary>Requirement slot index (0..8). UpgradeSlot için kullanılmaz.</summary>
        public int slotIndex;

        /// <summary>
        /// Open-KO birebir: UIItemUpgrade.cpp:343-377 — ReceiveIconDrop()
        /// 
        /// KOItemDragHandler'dan gelen drop event'ini işler.
        /// Sadece BagSlot (envanter) kaynaklı drop'ları kabul eder.
        /// </summary>
        public void OnDrop(PointerEventData eventData)
        {
            var dragSource = KOItemDragHandler.CurrentDragSource;
            if (dragSource == null)
                return;

            // C++ birebir: Sadece envanter slot'undan drop kabul et
            if (dragSource.district != KOItemDragHandler.SlotDistrict.BagSlot)
                return;

            bool isFast = KOUIManager.Instance != null && KOUIManager.Instance.IsFastUpgradeUIOpen;
            IKOUpgradeManager mgr = isFast ? (IKOUpgradeManager)KOFastUpgradeManager.Instance : (IKOUpgradeManager)KOItemUpgradeManager.Instance;
            if (mgr == null)
                return;

            int invPos = dragSource.slotIndex;

            switch (targetType)
            {
                case TargetType.UpgradeSlot:
                    // C++ satır 349-361: IsAllowedUpgradeItem → m_iUpgradeItemSlotInvPos = pos
                    bool success = isFast ? ((KOFastUpgradeManager)mgr).SetUpgradeItem(invPos, slotIndex) : mgr.SetUpgradeItem(invPos);
                    if (success)
                    {
                        // UI güncelle
                        if (KOUIManager.Instance != null)
                            KOUIManager.Instance.PopulateUpgradeInventory();
                    }
                    break;

                case TargetType.RequirementSlot:
                    // C++ satır 363-374: IsValidRequirementItem → SetRequirementItemSlot
                    if (mgr.SetRequirementItem(invPos, slotIndex))
                    {
                        if (KOUIManager.Instance != null)
                            KOUIManager.Instance.PopulateUpgradeInventory();
                    }
                    break;
            }
        }
    }
}
