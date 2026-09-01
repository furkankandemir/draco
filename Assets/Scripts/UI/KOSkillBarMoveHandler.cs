using UnityEngine;
using UnityEngine.EventSystems;

namespace EntropyOnline.UI
{
    public class KOSkillBarMoveHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform DragTarget;
        public bool BypassEditModeCheck = false;

        private RectTransform _rectTransform;
        private Canvas _canvas;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            if (DragTarget == null)
            {
                DragTarget = _rectTransform;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!BypassEditModeCheck)
            {
                if (MobileSkillBar.Instance == null || !MobileSkillBar.Instance.IsEditMode)
                {
                    eventData.pointerDrag = null; // Do not drag if not in edit mode
                    return;
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!BypassEditModeCheck)
            {
                if (MobileSkillBar.Instance == null || !MobileSkillBar.Instance.IsEditMode) return;
            }
            if (DragTarget == null || _canvas == null) return;

            // Move the DragTarget based on screen delta scaled by the canvas scaleFactor
            DragTarget.anchoredPosition += eventData.delta / _canvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (DragTarget != null && DragTarget.gameObject.name == "BuffBarContainer")
            {
                var parentRect = DragTarget.parent as RectTransform;
                if (parentRect != null)
                {
                    float width = parentRect.rect.width;
                    float height = parentRect.rect.height;

                    float normX = DragTarget.anchorMin.x + (DragTarget.anchoredPosition.x / width);
                    float normY = DragTarget.anchorMin.y + (DragTarget.anchoredPosition.y / height);

                    normX = Mathf.Clamp01(normX);
                    normY = Mathf.Clamp01(normY);

                    // Reposition using anchors to automatically adapt to screen resolution changes natively
                    DragTarget.anchorMin = new Vector2(normX, normY);
                    DragTarget.anchorMax = new Vector2(normX, normY);
                    DragTarget.anchoredPosition = Vector2.zero;

                    PlayerPrefs.SetFloat("BuffBar_NormX", normX);
                    PlayerPrefs.SetFloat("BuffBar_NormY", normY);
                    PlayerPrefs.Save();
                }
            }
        }
    }
}
