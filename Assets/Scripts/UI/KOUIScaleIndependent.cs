using UnityEngine;

namespace EntropyOnline.UI
{
    public class KOUIScaleIndependent : MonoBehaviour
    {
        private Canvas _canvas;

        private void OnEnable()
        {
            Canvas.willRenderCanvases += UpdateScale;
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= UpdateScale;
        }

        private void FindCanvas()
        {
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
                if (_canvas == null && KOUIManager.Instance != null)
                {
                    _canvas = KOUIManager.Instance.Canvas;
                }
            }
        }

        private void UpdateScale()
        {
            if (_canvas == null)
            {
                FindCanvas();
            }

            if (_canvas != null && _canvas.scaleFactor > 0f)
            {
                float multiplier = KOUIManager.Instance != null ? KOUIManager.Instance.UIScaleMultiplier : 1f;
                float targetScale = (1f / _canvas.scaleFactor) * multiplier;
                if (Mathf.Abs(transform.localScale.x - targetScale) > 0.0001f)
                {
                    transform.localScale = new Vector3(targetScale, targetScale, 1f);
                }
            }
        }
    }
}
