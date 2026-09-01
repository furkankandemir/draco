using UnityEngine;
using UnityEngine.EventSystems;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Preview modunda a_result (çıkış) slotuna tıklandığında yükseltilmiş eşyanın
    /// özelliklerini (tooltip) yerel olarak gösteren yardımcı bileşen.
    /// </summary>
    public class KOUpgradeResultPreviewHandler : MonoBehaviour, IPointerClickHandler
    {
        public int previewItemDefId = 0;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (previewItemDefId <= 0) return;

            var tooltip = GetTooltip();
            if (tooltip != null)
            {
                // Tooltip'i göster (Fiyat bilgisi ve satın alma bayrakları kapalı)
                tooltip.ShowByItemId(previewItemDefId, eventData.position, false, false);
            }
        }

        private static KOItemTooltip GetTooltip()
        {
            if (KOUIManager.Instance == null || KOUIManager.Instance.Canvas == null)
                return null;

            Canvas canvas = KOUIManager.Instance.Canvas;
            var tooltip = canvas.GetComponentInChildren<KOItemTooltip>(true);
            if (tooltip == null)
            {
                var go = new GameObject("KOItemTooltip");
                go.transform.SetParent(canvas.transform, false);
                tooltip = go.AddComponent<KOItemTooltip>();
            }
            return tooltip;
        }
    }
}
