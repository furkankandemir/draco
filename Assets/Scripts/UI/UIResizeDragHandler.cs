using UnityEngine;

namespace EntropyOnline.UI
{
    public class UIResizeDragHandler : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IDragHandler
    {
        private RectTransform _targetRect;
        private Vector2 _pointerStartLocalCursor;
        private Vector2 _startSize;
        private Vector2 _minSize;
        private Vector2 _maxSize;
        private System.Action<float, float> _onSizeChanged;

        public void Initialize(RectTransform targetRect, Vector2 minSize, Vector2 maxSize, System.Action<float, float> onSizeChanged)
        {
            _targetRect = targetRect;
            _minSize = minSize;
            _maxSize = maxSize;
            _onSizeChanged = onSizeChanged;
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _startSize = _targetRect.sizeDelta;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_targetRect, eventData.position, eventData.pressEventCamera, out _pointerStartLocalCursor);
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_targetRect == null) return;

            Vector2 localCursor;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_targetRect, eventData.position, eventData.pressEventCamera, out localCursor))
            {
                Vector2 diff = localCursor - _pointerStartLocalCursor;
                float newWidth = Mathf.Clamp(_startSize.x + diff.x, _minSize.x, _maxSize.x);
                float newHeight = Mathf.Clamp(_startSize.y + diff.y, _minSize.y, _maxSize.y);
                _targetRect.sizeDelta = new Vector2(newWidth, newHeight);
                _onSizeChanged?.Invoke(newWidth, newHeight);
            }
        }
    }
}
