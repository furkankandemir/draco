using UnityEngine;
using UnityEngine.UI;

namespace EntropyOnline.UI
{
    public class CmdListUI : MonoBehaviour
    {
        private GameObject _btnCancel;
        private Transform _listCmdCat;
        private Transform _listCmds;

        private RectTransform _rectTransform;

        private bool _opening;
        private bool _closing;
        private float _moveDelta;
        private float _width = 180f;
        private const float BaseYOffset = 45f; // Fixed screen pixel offset (aligned nicely with other buttons)

        public bool IsClosing => _closing;

        private void Awake()
        {
            // 1. Disable all original texts from UIF file to prevent them from showing behind the new buttons
            var existingTexts = GetComponentsInChildren<Text>(true);
            foreach (var txt in existingTexts)
            {
                txt.enabled = false;
            }

            _rectTransform = GetComponent<RectTransform>();
            ForceLayoutProperties();

            // Find child references from converted UIF
            _btnCancel = transform.Find("btn_cancel")?.gameObject;
            _listCmdCat = transform.Find("list_curtailment");
            _listCmds = transform.Find("list_content");

            if (_btnCancel != null)
            {
                var btn = _btnCancel.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(Close);
                }
            }

            // Hide old KO category/command list panels
            if (_listCmdCat != null) _listCmdCat.gameObject.SetActive(false);
            if (_listCmds != null) _listCmds.gameObject.SetActive(false);

            // Style and modernize the background and close button matching our other panels
            ModernizeBackground();
            ModernizeCloseButton();

            // Create custom vertical button menu container
            var containerGO = new GameObject("ButtonContainer");
            containerGO.transform.SetParent(transform, false);
            var containerRT = containerGO.AddComponent<RectTransform>();
            if (containerRT != null)
            {
                containerRT.anchorMin = new Vector2(0.5f, 1f); // Anchor to top-center of the panel
                containerRT.anchorMax = new Vector2(0.5f, 1f);
                containerRT.pivot = new Vector2(0.5f, 1f);
                
                // Layout dimensions: fit nicely inside the shortened panel height (370px)
                containerRT.sizeDelta = new Vector2(160f, 320f);
                containerRT.anchoredPosition = new Vector2(0f, -40f); // 40px below the title
            }

            var layout = containerGO.AddComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 3; // Reduced spacing to fit button + divider rows neatly
                layout.padding = new RectOffset(5, 5, 5, 5);
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlHeight = false;
                layout.childControlWidth = false;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = false;
            }

            CreateCommandButtons(containerGO.transform);
        }

        private void Start()
        {
            ForceLayoutProperties();
        }

        private void ForceLayoutProperties()
        {
            if (_rectTransform != null)
            {
                // Anchor and pivot to Bottom-Right to make it stay right above the HUD
                _rectTransform.anchorMin = new Vector2(1, 0);
                _rectTransform.anchorMax = new Vector2(1, 0);
                _rectTransform.pivot = new Vector2(1, 0);
                
                // Shortened height to fit our 8 buttons cleanly (Height 370), width shortened by 10px to 180f
                _rectTransform.sizeDelta = new Vector2(180f, 370f);
            }
        }

        private float GetScaledWidth()
        {
            float w = 180f;
            if (_rectTransform != null)
            {
                w = _rectTransform.rect.width;
            }
            if (w <= 0) w = 180f;
            return w * transform.localScale.x;
        }

        private void ModernizeBackground()
        {
            // 1. Disable all original raw images and images (except buttons or our custom bg)
            var rawImages = GetComponentsInChildren<RawImage>(true);
            foreach (var ri in rawImages)
            {
                if (ri.GetComponentInParent<Button>() == null)
                    ri.enabled = false;
            }

            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.GetComponentInParent<Button>() == null && img.gameObject.name != "CmdListCustomBg")
                    img.enabled = false;
            }

            // 2. Create custom background
            var customBg = transform.Find("CmdListCustomBg")?.gameObject;
            if (customBg == null)
            {
                customBg = new GameObject("CmdListCustomBg");
                customBg.transform.SetParent(transform, false);
                customBg.transform.SetAsFirstSibling(); // Ensure it renders behind everything
            }

            var bgImg = customBg.GetComponent<Image>();
            if (bgImg == null) bgImg = customBg.AddComponent<Image>();

            // Gradient colors matching our modernized UI and EXACTLY matching Merchant Control
            Color topCol = new Color(0.12f, 0.10f, 0.08f, 0.98f);    // Warm charcoal
            Color bottomCol = new Color(0.04f, 0.04f, 0.04f, 0.98f); // Almost black
            Color borderCol = new Color(0.6f, 0.48f, 0.22f, 0.9f);   // Amber gold main border

            int w = 180;
            int h = 370;

            if (bgImg != null && KOUIManager.Instance != null)
            {
                bgImg.sprite = KOUIManager.Instance.GetSkillThemePanelBgSprite(
                    "cmd_list_bg_gradient", w, h, 0, topCol, bottomCol, borderCol, 2);
                bgImg.color = Color.white;
                bgImg.type = Image.Type.Simple;
            }

            var bgRT = customBg.GetComponent<RectTransform>();
            if (bgRT != null)
            {
                bgRT.anchorMin = Vector2.zero;
                bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = Vector2.zero;
                bgRT.offsetMax = Vector2.zero;
            }

            // Create title text
            var titleObj = transform.Find("TitleText")?.gameObject;
            if (titleObj == null)
            {
                titleObj = new GameObject("TitleText");
                titleObj.transform.SetParent(transform, false);
            }
            var titleTxt = titleObj.GetComponent<Text>();
            if (titleTxt == null) titleTxt = titleObj.AddComponent<Text>();
            if (titleTxt != null)
            {
                titleTxt.text = "COMMAND";
                titleTxt.font = GetSafeFont(14);
                titleTxt.fontSize = 14;
                titleTxt.fontStyle = FontStyle.Bold;
                titleTxt.color = new Color(0.95f, 0.85f, 0.35f, 1.0f); // Premium gold color matching Merchant Control
                titleTxt.alignment = TextAnchor.MiddleCenter;
                titleTxt.enabled = true; // Ensure it is active

                // Add drop shadow for a more crisp title look
                var titleShadow = titleTxt.gameObject.GetComponent<Shadow>();
                if (titleShadow == null) titleShadow = titleTxt.gameObject.AddComponent<Shadow>();
                titleShadow.effectColor = Color.black;
                titleShadow.effectDistance = new Vector2(1f, -1f);
            }

            var titleRT = titleObj.GetComponent<RectTransform>();
            if (titleRT != null)
            {
                titleRT.anchorMin = new Vector2(0.5f, 1f);
                titleRT.anchorMax = new Vector2(0.5f, 1f);
                titleRT.pivot = new Vector2(0.5f, 1f);
                titleRT.sizeDelta = new Vector2(160f, 30f);
                titleRT.anchoredPosition = new Vector2(0f, -8f); // Top center
            }
        }

        private void ModernizeCloseButton()
        {
            if (_btnCancel != null)
            {
                _btnCancel.transform.SetAsLastSibling();

                // Remove any existing graphic components to prevent mutual exclusion conflicts with Image
                var oldRaw = _btnCancel.GetComponent<RawImage>();
                if (oldRaw != null) DestroyImmediate(oldRaw);
                var oldImg = _btnCancel.GetComponent<Image>();
                if (oldImg != null) DestroyImmediate(oldImg);

                var cancelImg = _btnCancel.AddComponent<Image>();
                if (cancelImg != null && KOUIManager.Instance != null)
                {
                    cancelImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "cmd_list_close", 22, 22, 0, // Sharp corners (no rounding)
                        new Color(0.6f, 0.1f, 0.1f, 1f), // Red color matching Merchant Control close button
                        Color.clear,
                        0);
                    cancelImg.color = Color.white;
                    cancelImg.enabled = true;
                }

                // Add 'X' text inside the close button
                var xTxtObj = _btnCancel.transform.Find("Text")?.gameObject;
                if (xTxtObj == null)
                {
                    xTxtObj = new GameObject("Text");
                    xTxtObj.transform.SetParent(_btnCancel.transform, false);
                }
                var xTxt = xTxtObj.GetComponent<Text>();
                if (xTxt == null) xTxt = xTxtObj.AddComponent<Text>();
                if (xTxt != null)
                {
                    xTxt.text = "X";
                    xTxt.font = GetSafeFont(12);
                    xTxt.fontSize = 12;
                    xTxt.fontStyle = FontStyle.Bold;
                    xTxt.color = Color.white;
                    xTxt.alignment = TextAnchor.MiddleCenter;
                    xTxt.enabled = true; // Ensure it is active
                }

                var xRT = _btnCancel.GetComponent<RectTransform>();
                if (xRT != null)
                {
                    xRT.anchorMin = new Vector2(1f, 1f); // Anchor to top-right of panel
                    xRT.anchorMax = new Vector2(1f, 1f);
                    xRT.pivot = new Vector2(1f, 1f);
                    xRT.sizeDelta = new Vector2(22f, 22f); // Enlarged to 22px
                    xRT.anchoredPosition = new Vector2(-10f, -10f); // 10px offset from top-right
                }
            }
        }

        private void CreateCommandButtons(Transform container)
        {
            string[] labels = new string[] {
                "STORE",
                "CREATE MERCHANT",
                "MERCHANT CONTROL",
                "SEEKING PARTY",
                "OPTIONS",
                "FORUM",
                "DISCORD",
                "INSTAGRAM"
            };

            for (int i = 0; i < labels.Length; i++)
            {
                string label = labels[i];
                var btnGO = new GameObject($"Btn_{label.Replace(" ", "_")}");
                btnGO.transform.SetParent(container, false);

                var rt = btnGO.AddComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(150f, 32f); // Shortened button width to 150px
                }

                var btn = btnGO.AddComponent<Button>();
                StyleCommandButton(btn, label);

                // Add click listener
                int index = i;
                btn.onClick.AddListener(() => OnButtonClicked(index));

                // Add fading divider between buttons (except after the last button)
                if (i < labels.Length - 1)
                {
                    var divGO = new GameObject("Divider_" + i);
                    divGO.transform.SetParent(container, false);
                    var divRT = divGO.AddComponent<RectTransform>();
                    if (divRT != null)
                    {
                        divRT.sizeDelta = new Vector2(140f, 1.5f); // Shortened divider width to 140px
                    }
                    var divImg = divGO.AddComponent<Image>();
                    if (divImg != null && KOUIManager.Instance != null)
                    {
                        // Fading gold/bronze divider matching Merchant Control
                        divImg.sprite = KOUIManager.Instance.GetSkillThemeFadingDividerSprite(
                            "cmd_list_divider", 140, 2, new Color(0.6f, 0.48f, 0.22f, 0.35f));
                        divImg.color = Color.white;
                    }
                }
            }
        }

        private void StyleCommandButton(Button btn, string labelText)
        {
            var img = btn.gameObject.GetComponent<Image>();
            if (img == null) img = btn.gameObject.AddComponent<Image>();

            // Completely borderless rounded button base (transparent inside, only shows soft gold highlight on hover/press)
            if (img != null && KOUIManager.Instance != null)
            {
                img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "cmd_btn_round_base", 150, 32, 6, Color.white, Color.clear, 0);
                img.color = Color.white;
                img.type = Image.Type.Sliced;
            }

            // Transition states
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f); // Completely transparent normal state
            cb.highlightedColor = new Color(0.48f, 0.38f, 0.22f, 0.2f); // Soft bronze hover highlight
            cb.pressedColor = new Color(0.48f, 0.38f, 0.22f, 0.4f); // Slightly stronger click highlight
            cb.selectedColor = new Color(0.48f, 0.38f, 0.22f, 0.15f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0f);
            btn.colors = cb;

            // Add Text
            var txtObj = btn.transform.Find("Text")?.gameObject;
            if (txtObj == null)
            {
                txtObj = new GameObject("Text");
                txtObj.transform.SetParent(btn.transform, false);
            }

            var txt = txtObj.GetComponent<Text>();
            if (txt == null) txt = txtObj.AddComponent<Text>();
            if (txt != null)
            {
                txt.text = labelText;
                txt.font = GetSafeFont(13);
                txt.fontSize = 13;
                txt.fontStyle = FontStyle.Bold;
                txt.color = new Color(0.9f, 0.8f, 0.6f, 1f); // Warm gold text matching Merchant Control
                txt.alignment = TextAnchor.MiddleCenter;
                txt.raycastTarget = false;
                txt.enabled = true; // Ensure it is active

                // Add subtle shadow to button text for premium look
                var txtShadow = txt.gameObject.GetComponent<Shadow>();
                if (txtShadow == null) txtShadow = txt.gameObject.AddComponent<Shadow>();
                txtShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
                txtShadow.effectDistance = new Vector2(1f, -1f);

                var txtRT = txt.GetComponent<RectTransform>();
                if (txtRT != null)
                {
                    txtRT.anchorMin = Vector2.zero;
                    txtRT.anchorMax = Vector2.one;
                    txtRT.offsetMin = Vector2.zero;
                    txtRT.offsetMax = Vector2.zero;
                }
            }
        }

        private void OnButtonClicked(int index)
        {
            if (KOUIManager.Instance == null) return;

            switch (index)
            {
                case 0: // STORE
                    KOUIManager.Instance.OpenShoppingMallUI();
                    Close();
                    break;
                case 1: // CREATE MERCHANT
                    KOUIManager.Instance.ParseChattingCommand("/merchant");
                    Close();
                    break;
                case 2: // MERCHANT CONTROL
                    KOUIManager.Instance.OpenMerchantControl();
                    Close();
                    break;
                case 3: // SEEKING PARTY
                    KOUIManager.Instance.ParseChattingCommand("/seeking_party");
                    Close();
                    break;
                case 4: // OPTIONS
                    KOUIManager.Instance.OpenGameOptions();
                    Close();
                    break;
                case 5: // FORUM
                    KOUIManager.Instance.AddMsgOutput("Forum is under construction.", new Color(1f, 0.5f, 0f));
                    break;
                case 6: // DISCORD
                    KOUIManager.Instance.AddMsgOutput("Discord is under construction.", new Color(1f, 0.5f, 0f));
                    break;
                case 7: // INSTAGRAM
                    KOUIManager.Instance.AddMsgOutput("Instagram is under construction.", new Color(1f, 0.5f, 0f));
                    break;
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
            ForceLayoutProperties();
            _width = GetScaledWidth();

            if (_rectTransform != null)
            {
                // Set Y position relative to localScale.x (1/s) to keep it exactly 50 screen pixels above the screen bottom
                float targetY = BaseYOffset * transform.localScale.x;
                _rectTransform.anchoredPosition = new Vector2(_width, targetY);
            }
            _moveDelta = 0;
            _opening = true;
            _closing = false;
        }

        public void Close()
        {
            ForceLayoutProperties();
            _width = GetScaledWidth();

            if (_rectTransform != null)
            {
                // Set Y position relative to localScale.x (1/s) to keep it exactly at BaseYOffset screen pixels above the screen bottom
                float targetY = BaseYOffset * transform.localScale.x;
                _rectTransform.anchoredPosition = new Vector2(0, targetY);
            }
            _moveDelta = 0;
            _opening = false;
            _closing = true;

            // Trigger smooth skillbar slide back to original position
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.RepositionSkillBarForPanel(instant: false);
            }
        }

        private void Update()
        {
            _width = GetScaledWidth();
            float targetY = BaseYOffset * transform.localScale.x;

            // Smooth opening and closing animation matching C++ / original logic
            if (_opening)
            {
                float fDelta = 5000.0f * Time.deltaTime;
                fDelta *= (_width - _moveDelta) / _width;
                if (fDelta < 2.0f) fDelta = 2.0f;
                _moveDelta += fDelta;

                float x = _width - _moveDelta;
                if (x <= 0f)
                {
                    x = 0f;
                    _opening = false;
                }
                if (_rectTransform != null)
                {
                    _rectTransform.anchoredPosition = new Vector2(x, targetY);
                }
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.RepositionSkillBarForPanel(instant: true);
                }
            }
            else if (_closing)
            {
                float fDelta = 5000.0f * Time.deltaTime;
                fDelta *= (_width - _moveDelta) / _width;
                if (fDelta < 2.0f) fDelta = 2.0f;
                _moveDelta += fDelta;

                float x = _moveDelta;
                if (x >= _width)
                {
                    x = _width;
                    _closing = false;
                    gameObject.SetActive(false);
                }
                if (_rectTransform != null)
                {
                    _rectTransform.anchoredPosition = new Vector2(x, targetY);
                }
            }
            else
            {
                // Ensure Y position is always synced every frame when open to prevent drift on resolution/scale factor changes
                if (_rectTransform != null)
                {
                    _rectTransform.anchoredPosition = new Vector2(0f, targetY);
                }
            }

            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private Font GetSafeFont(int fontSize)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            return font;
        }
    }
}
