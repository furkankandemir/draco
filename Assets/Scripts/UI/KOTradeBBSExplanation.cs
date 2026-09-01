using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUITradeSellBBS :: m_UIExplanation
    /// UIF: co_saleboardmemolist_us.uif
    /// </summary>
    public class KOTradeBBSExplanation : MonoBehaviour
    {
        public static KOTradeBBSExplanation Instance { get; private set; }

        private Text _textTitle; // C++ Text_Title: explanation memo display
        private Button _btnPageUp;
        private Button _btnPageDown;
        private Button _btnClose;

        private void Awake()
        {
            Instance = this;
            BindElements();
        }

        private void Update()
        {
            // ESC ile kapat
            if (gameObject.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetVisible(false);
            }
        }

        private void BindElements()
        {
            var t = transform;
            _textTitle = KOUIRenderer.FindChildText(t, "Text_Title");
            _btnPageUp = KOUIRenderer.FindChildButton(t, "btn_pageup");
            _btnPageDown = KOUIRenderer.FindChildButton(t, "btn_pagedown");
            _btnClose = KOUIRenderer.FindChildButton(t, "btn_close");

            if (_btnPageUp != null)
                _btnPageUp.onClick.AddListener(() => KOTradeBBS.Instance?.RefreshExplanation(true));
            if (_btnPageDown != null)
                _btnPageDown.onClick.AddListener(() => KOTradeBBS.Instance?.RefreshExplanation(false));
            if (_btnClose != null)
                _btnClose.onClick.AddListener(() => SetVisible(false));
        }

        public void Show(string explanationText)
        {
            SetExplanationText(explanationText);
            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetExplanationText(string text)
        {
            if (_textTitle != null)
            {
                _textTitle.text = text;
            }
        }
    }
}
