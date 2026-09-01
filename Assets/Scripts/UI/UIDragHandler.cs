using UnityEngine;
using UnityEngine.EventSystems;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Unity UI panellerini (örneğin Parti Paneli) sürüklemek için kullanılan bileşen.
    /// Başlık barına (Drag Area) eklenir ve hedef paneli sürükler.
    /// </summary>
    public class UIDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private RectTransform _panelToDrag;
        private Canvas _canvas;
        private Vector2 _pointerOffset;

        public void Initialize(RectTransform panelToDrag)
        {
            _panelToDrag = panelToDrag;
            _canvas = panelToDrag.GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_panelToDrag == null || _canvas == null) return;

            // Get pointer offset in parent Canvas local space relative to panel's localPosition
            Vector2 localPointerPosition;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, 
                eventData.position, 
                eventData.pressEventCamera, 
                out localPointerPosition
            ))
            {
                _pointerOffset = localPointerPosition - (Vector2)_panelToDrag.localPosition;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_panelToDrag == null || _canvas == null) return;

            // İmlecin Canvas üzerindeki local pozisyonunu bul
            Vector2 localPointerPosition;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, 
                eventData.position, 
                eventData.pressEventCamera, 
                out localPointerPosition
            ))
            {
                Vector2 targetPos = localPointerPosition - _pointerOffset;

                // Ekran sınırlarında kalmasını sağlamak için clamp uygula
                var canvasRt = _canvas.transform as RectTransform;
                if (canvasRt != null)
                {
                    float minX = canvasRt.rect.xMin + (_panelToDrag.pivot.x * _panelToDrag.rect.width);
                    float maxX = canvasRt.rect.xMax - ((1f - _panelToDrag.pivot.x) * _panelToDrag.rect.width);
                    float minY = canvasRt.rect.yMin + (_panelToDrag.pivot.y * _panelToDrag.rect.height);
                    float maxY = canvasRt.rect.yMax - ((1f - _panelToDrag.pivot.y) * _panelToDrag.rect.height);

                    targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
                    targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
                }

                _panelToDrag.localPosition = new Vector3(targetPos.x, targetPos.y, _panelToDrag.localPosition.z);
            }
        }
    }
}
