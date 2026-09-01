using UnityEngine;

namespace EntropyOnline.UI
{
    public class KOUIPanelSlideIn : MonoBehaviour
    {
        public bool IsLeft = true;
        public float TargetX = 50f;
        public float StartX = -350f;
        public float Duration = 0.2f;
        public bool IsSlidingOut { get; private set; } = false;

        private RectTransform _rt;
        private Coroutine _slideCoroutine;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            IsSlidingOut = false;
            if (_rt == null) return;
            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(SlideInLoop());
        }

        public void SlideOut(System.Action onComplete)
        {
            IsSlidingOut = true;
            if (_rt == null || !gameObject.activeInHierarchy) { onComplete?.Invoke(); return; }
            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            _slideCoroutine = StartCoroutine(SlideOutLoop(onComplete));
        }

        private System.Collections.IEnumerator SlideInLoop()
        {
            float elapsed = 0f;
            Vector2 pos = _rt.anchoredPosition;
            pos.x = StartX;
            _rt.anchoredPosition = pos;

            while (elapsed < Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / Duration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                pos.x = Mathf.Lerp(StartX, TargetX, t);
                _rt.anchoredPosition = pos;
                yield return null;
            }

            pos.x = TargetX;
            _rt.anchoredPosition = pos;
            _slideCoroutine = null;
        }

        private System.Collections.IEnumerator SlideOutLoop(System.Action onComplete)
        {
            float elapsed = 0f;
            Vector2 pos = _rt.anchoredPosition;
            float startX = _rt.anchoredPosition.x;

            while (elapsed < Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / Duration;
                t = Mathf.Sin(t * Mathf.PI * 0.5f);
                pos.x = Mathf.Lerp(startX, StartX, t);
                _rt.anchoredPosition = pos;
                yield return null;
            }

            pos.x = StartX;
            _rt.anchoredPosition = pos;
            _slideCoroutine = null;
            onComplete?.Invoke();
        }
    }
}
