using UnityEngine;
using UnityEngine.UI;

namespace EntropyOnline.UI
{
    public class KOPMButtonBlinker : MonoBehaviour
    {
        private Image _img;
        private RawImage _rawImg;
        private Color _origColor;
        private Sprite _origSprite;
        private Sprite _opaqueSprite;
        private bool _isBlinking = false;
        private float _timer = 0f;

        private void Awake()
        {
            _img = GetComponent<Image>();
            _rawImg = GetComponent<RawImage>();
        }

        public void StartBlinking()
        {
            if (!_isBlinking)
            {
                _timer = 0f;
                if (_img != null)
                {
                    _origColor = _img.color;
                    _origSprite = _img.sprite;
                    
                    // Create an opaque version of the button background sprite dynamically using KOUIManager
                    if (KOUIManager.Instance != null && _img.sprite != null)
                    {
                        _opaqueSprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                            "btn_pm_opaque_bg", 72, 28, 0,
                            new Color(0.06f, 0.05f, 0.04f, 1.0f),  // Opaque background
                            new Color(0.5f, 0.4f, 0.18f, 1.0f),    // Opaque frame
                            1
                        );
                        if (_opaqueSprite != null)
                        {
                            _img.sprite = _opaqueSprite;
                        }
                    }
                }
                else if (_rawImg != null)
                {
                    _origColor = _rawImg.color;
                }
                else
                {
                    _origColor = Color.white;
                }
            }
            _isBlinking = true;
        }

        public void StopBlinking()
        {
            if (!_isBlinking) return;
            _isBlinking = false;
            
            if (_img != null)
            {
                _img.color = _origColor;
                if (_origSprite != null)
                {
                    _img.sprite = _origSprite;
                }
            }
            if (_rawImg != null) _rawImg.color = _origColor;
        }

        private void LateUpdate()
        {
            // Continuously update timer so it is never reset by toggling blinking
            _timer += Time.deltaTime;

            if (!_isBlinking) return;

            // Ping-pong interpolation factor between 0.0 (normal) and 1.0 (fully opaque)
            float t = Mathf.PingPong(_timer * 1.2f, 1.0f);
            
            // Interpolate alpha from 0.6f (normal/semi-transparent) to 1.0f (fully opaque/darker)
            float alpha = Mathf.Lerp(0.6f, 1.0f, t);
            
            Color c = new Color(_origColor.r, _origColor.g, _origColor.b, alpha);

            if (_img != null) _img.color = c;
            if (_rawImg != null) _rawImg.color = c;
        }

        private void OnDisable()
        {
            StopBlinking();
        }
    }
}
