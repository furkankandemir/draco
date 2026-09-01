using UnityEngine;
using UnityEngine.UI;

namespace EntropyOnline.Import
{
    /// <summary>
    /// KO Progress bar fill kontrol component'ı.
    /// UV rect manipulasyonu ile doluluk oranını kontrol eder.
    /// C++ CN3UIProgress::SetCurValue birebir.
    /// </summary>
    public class KOProgressFill : MonoBehaviour
    {
        public RawImage FillImage;
        public Rect OriginalUV;
        public bool IsVertical;
        public bool IsReverse;

        private float _fillAmount = 1f;

        public float FillAmount
        {
            get => _fillAmount;
            set
            {
                _fillAmount = Mathf.Clamp01(value);
                UpdateFill();
            }
        }

        private void UpdateFill()
        {
            if (FillImage == null) return;

            var rt = FillImage.rectTransform;

            if (IsVertical)
            {
                if (IsReverse) // BOTTOM2TOP
                {
                    rt.anchorMin = new Vector2(0, 1f - _fillAmount);
                    rt.anchorMax = Vector2.one;
                }
                else // TOP2BOTTOM
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = new Vector2(1, _fillAmount);
                }
            }
            else
            {
                if (IsReverse) // RIGHT2LEFT
                {
                    rt.anchorMin = new Vector2(1f - _fillAmount, 0);
                    rt.anchorMax = Vector2.one;
                }
                else // LEFT2RIGHT (default)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = new Vector2(_fillAmount, 1);
                }
            }

            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // UV rect'i de fill amount'a göre ayarla
            var uv = OriginalUV;
            if (!IsVertical)
            {
                if (!IsReverse)
                    uv.width = OriginalUV.width * _fillAmount;
                else
                    uv.x = OriginalUV.x + OriginalUV.width * (1f - _fillAmount);
            }
            FillImage.uvRect = uv;
        }
    }
}
