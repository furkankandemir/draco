using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Collections.Generic;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Açık PM Penceresi (co_whisper_open_us) Kontrolcüsü.
    /// </summary>
    public class KOWhisperPanel : MonoBehaviour
    {
        public string TargetName { get; private set; }
        
        [SerializeField] private Text exit_id;
        [SerializeField] private InputField edit_chat;
        [SerializeField] private Button btn_chat;
        [SerializeField] private Button btn_close;
        [SerializeField] private Button btn_hide;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private TMPro.TextMeshProUGUI chat_history_text;
        [SerializeField] private Transform btn_bar;

        private List<WhisperMessage> _messages = new List<WhisperMessage>();

        public void Initialize(string targetName, List<WhisperMessage> messages)
        {
            TargetName = targetName;
            
            bool isLegacy = (chat_history_text == null);
            
            if (isLegacy)
            {
                // Bind components
                if (exit_id == null) exit_id = transform.Find("exit_id")?.GetComponent<Text>();
                if (edit_chat == null) edit_chat = transform.Find("edit_chat")?.GetComponent<InputField>();
                if (btn_chat == null) btn_chat = transform.Find("btn_chat")?.GetComponent<Button>();
                if (btn_close == null) btn_close = transform.Find("btn_close")?.GetComponent<Button>();
                if (btn_hide == null) btn_hide = transform.Find("btn_hide")?.GetComponent<Button>();
                if (scroll == null) scroll = transform.Find("scroll")?.GetComponent<ScrollRect>();
                if (btn_bar == null) btn_bar = transform.Find("btn_bar");
                if (btn_bar != null)
                {
                    var rtBar = btn_bar.GetComponent<RectTransform>();
                    if (rtBar != null)
                    {
                        rtBar.anchorMin = new Vector2(0f, 1f);
                        rtBar.anchorMax = new Vector2(1f, 1f);
                        rtBar.pivot = new Vector2(0.5f, 1f);
                        rtBar.anchoredPosition = new Vector2(0f, -2f); // 2px down from top border
                        rtBar.sizeDelta = new Vector2(-4f, 30f); // 4px narrower to fit inside border, 30px height
                    }
                }
                if (edit_chat == null) edit_chat = transform.Find("EditText")?.GetComponent<InputField>();
                if (edit_chat != null)
                {
                    edit_chat.characterLimit = 128;
                }

                if (exit_id != null)
                {
                    exit_id.text = targetName;
                }

                var exitChatTrans = transform.Find("exit_chat");
                if (exitChatTrans != null)
                {
                    chat_history_text = exitChatTrans.GetComponent<TMPro.TextMeshProUGUI>();
                    if (chat_history_text == null)
                    {
                        var oldText = exitChatTrans.GetComponent<Text>();
                        if (oldText != null)
                        {
                            Color textColor = oldText.color;
                            float fontSize = oldText.fontSize;
                            string textVal = oldText.text;

                            DestroyImmediate(oldText);

                            chat_history_text = exitChatTrans.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
                            chat_history_text.color = textColor;
                            chat_history_text.fontSize = 13f;
                            chat_history_text.enableAutoSizing = false;
                            chat_history_text.text = textVal;
                            chat_history_text.textWrappingMode = TMPro.TextWrappingModes.Normal;
                            chat_history_text.richText = true;
                            chat_history_text.raycastTarget = true;

                            var fontAsset = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                            if (fontAsset != null)
                            {
                                chat_history_text.font = fontAsset;
                            }
                        }
                    }
                    else
                    {
                        chat_history_text.fontSize = 13f;
                        chat_history_text.enableAutoSizing = false;
                        chat_history_text.raycastTarget = true;
                    }

                    // Dynamic ScrollRect and Viewport Setup
                    GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform));
                    viewportGO.transform.SetParent(transform, false);

                    var scrollImg = viewportGO.AddComponent<UnityEngine.UI.Image>();
                    scrollImg.sprite = null;
                    scrollImg.color = new Color(0f, 0f, 0f, 0f);
                    scrollImg.raycastTarget = true;

                    var rectChat = exitChatTrans.GetComponent<RectTransform>();
                    var rectViewport = viewportGO.GetComponent<RectTransform>();

                    rectViewport.anchorMin = rectChat.anchorMin;
                    rectViewport.anchorMax = rectChat.anchorMax;
                    rectViewport.pivot = rectChat.pivot;
                    rectViewport.anchoredPosition = rectChat.anchoredPosition;
                    rectViewport.sizeDelta = rectChat.sizeDelta;

                    viewportGO.AddComponent<UnityEngine.UI.RectMask2D>();
                    scroll = viewportGO.AddComponent<ScrollRect>();
                    scroll.horizontal = false;
                    scroll.vertical = true;
                    scroll.movementType = ScrollRect.MovementType.Clamped;
                    scroll.viewport = rectViewport;

                    exitChatTrans.SetParent(viewportGO.transform, false);

                    rectChat.anchorMin = new Vector2(0f, 1f);
                    rectChat.anchorMax = new Vector2(1f, 1f);
                    rectChat.pivot = new Vector2(0.5f, 1f);
                    rectChat.anchoredPosition = Vector2.zero;
                    rectChat.sizeDelta = new Vector2(0f, rectChat.sizeDelta.y);

                    var fitter = exitChatTrans.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                    if (fitter == null)
                    {
                        fitter = exitChatTrans.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                    }
                    fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                    fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;

                    scroll.content = rectChat;
                }

                // Drag support
                if (btn_bar != null && btn_bar.GetComponent<UIDragHandler>() == null)
                {
                    btn_bar.gameObject.AddComponent<UIDragHandler>();
                }
            }
            else
            {
                // Native Prefab: Component'ler Inspector'dan atanmış durumda.
                // Sadece başlığı güncelle
                if (exit_id != null)
                {
                    exit_id.text = targetName;
                }
            }

            // Click listeners
            if (btn_close != null)
            {
                btn_close.onClick.RemoveAllListeners();
                btn_close.onClick.AddListener(OnCloseClicked);
            }

            if (btn_hide != null)
            {
                btn_hide.onClick.RemoveAllListeners();
                btn_hide.onClick.AddListener(OnMinimizeClicked);
            }

            if (btn_chat != null)
            {
                btn_chat.onClick.RemoveAllListeners();
                btn_chat.onClick.AddListener(OnSendClicked);
            }

            if (edit_chat != null)
            {
                edit_chat.onSubmit.RemoveAllListeners();
                edit_chat.onSubmit.AddListener((val) => OnSendClicked());
            }

            // Load initial history messages
            _messages.Clear();
            if (messages != null)
            {
                _messages.AddRange(messages);
            }
            UpdateHistoryUI();

            ApplyModernTheme();
        }

        public void AddMessage(string senderName, string message, bool isOutgoing)
        {
            var msgObj = new WhisperMessage { SenderName = senderName, MessageText = message, IsOutgoing = isOutgoing };
            _messages.Add(msgObj);
            UpdateHistoryUI();
        }

        private void UpdateHistoryUI()
        {
            if (chat_history_text != null)
            {
                var sb = new StringBuilder();
                Color outgoingColor = KOUIManager.D3DColorToUnity(0xff80ffff); // Standard PM color (light blue)
                Color incomingColor = KOUIManager.D3DColorToUnity(0xffffff00); // Sarı (info panel yellow)
                string hexOutgoing = ColorUtility.ToHtmlStringRGB(outgoingColor);
                string hexIncoming = ColorUtility.ToHtmlStringRGB(incomingColor);

                foreach (var msg in _messages)
                {
                    if (msg.IsOutgoing)
                    {
                        sb.AppendLine($"<align=right><margin-left=50><color=#{hexOutgoing}>{msg.MessageText}</color></margin></align>");
                    }
                    else
                    {
                        sb.AppendLine($"<align=left><margin-right=50><color=#{hexIncoming}>{msg.MessageText}</color></margin></align>");
                    }
                }

                chat_history_text.text = sb.ToString();
                
                // Scroll to bottom
                Canvas.ForceUpdateCanvases();
                if (scroll != null)
                {
                    scroll.verticalNormalizedPosition = 0f;
                }
            }
        }

        private void OnSendClicked()
        {
            if (edit_chat == null || string.IsNullOrEmpty(edit_chat.text)) return;

            string msg = edit_chat.text.Trim();
            if (msg.Length > 0)
            {
                KOWhisperManager.Instance.SendPrivateMessage(TargetName, msg);
                edit_chat.text = "";
                edit_chat.ActivateInputField();
            }
        }

        private void AddLocalTextShadow(Text txt)
        {
            if (txt == null || txt.gameObject.GetComponent<Shadow>() != null) return;
            var shadow = txt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private void SetLocalButtonThemeSprite(Button btn, Sprite sprite)
        {
            if (btn == null || sprite == null) return;
            btn.transition = Selectable.Transition.None;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.enabled = true;
            }
            else
            {
                var raw = btn.GetComponent<RawImage>();
                if (raw != null)
                {
                    raw.texture = sprite.texture;
                    raw.uvRect = new Rect(0f, 0f, 1f, 1f);
                    raw.color = Color.white;
                    raw.enabled = true;
                }
            }
        }

        private void ApplyModernTheme()
        {
            var uiMgr = KOUIManager.Instance;
            if (uiMgr == null) return;

            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(304f, 180f);

                if (gameObject.GetComponent<KOUIScaleIndependent>() == null)
                {
                    gameObject.AddComponent<KOUIScaleIndependent>();
                }
                
                float s = uiMgr.CanvasScaleFactor;
                if (s > 0f)
                {
                    rt.anchoredPosition = new Vector2(50f / s, 190f / s); // opens 50px from left edge
                }
                else
                {
                    rt.anchoredPosition = new Vector2(50f, 190f);
                }
            }

            // 1. RawImage backgrounds (disable legacy decor, enable/style main & header)
            var rawImages = GetComponentsInChildren<RawImage>(true);
            foreach (var img in rawImages)
            {
                if (img.gameObject == btn_bar?.gameObject)
                {
                    var sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_bar_bg_30", 300, 30, 0,
                        new Color(0.18f, 0.15f, 0.12f, 0.95f),
                        new Color(0.45f, 0.35f, 0.15f, 0.9f),
                        1);
                    if (sprite != null)
                    {
                        img.texture = sprite.texture;
                        img.uvRect = new Rect(0, 0, 1, 1);
                        img.enabled = true;
                    }
                }
                else if (img.name == "ui_Image_5389E3C4")
                {
                    var sprite = uiMgr.GetSkillThemePanelBgSprite("whisper_open_bg_180", 304, 180, 0,
                        new Color(0.12f, 0.10f, 0.08f, 0.98f),
                        new Color(0.04f, 0.04f, 0.04f, 0.98f),
                        new Color(0.6f, 0.48f, 0.22f, 0.9f),
                        2);
                    if (sprite != null)
                    {
                        img.texture = sprite.texture;
                        img.uvRect = new Rect(0, 0, 1, 1);
                        img.enabled = true;
                    }
                    
                    var rtBg = img.GetComponent<RectTransform>();
                    if (rtBg != null)
                    {
                        rtBg.anchorMin = Vector2.zero;
                        rtBg.anchorMax = Vector2.one;
                        rtBg.offsetMin = Vector2.zero;
                        rtBg.offsetMax = Vector2.zero;
                    }
                    
                    var outline = img.gameObject.GetComponent<Outline>();
                    if (outline != null) Destroy(outline);
                }
                else
                {
                    img.enabled = false;
                }
            }

            // 2. Position header elements to match Mockup 2
            if (exit_id != null)
            {
                var rtName = exit_id.GetComponent<RectTransform>();
                rtName.anchorMin = new Vector2(0f, 1f);
                rtName.anchorMax = new Vector2(0f, 1f);
                rtName.pivot = new Vector2(0f, 0.5f);
                rtName.anchoredPosition = new Vector2(28f, -17f); // centered vertically in 30px bar (visually centered at -17f)
                rtName.sizeDelta = new Vector2(100f, 20f);
                
                exit_id.color = new Color(0.9f, 0.75f, 0.55f, 1f);
                exit_id.fontStyle = FontStyle.Bold;
                exit_id.fontSize = 13; // increased font size
                exit_id.alignment = TextAnchor.MiddleLeft;
                AddLocalTextShadow(exit_id);
            }

            if (btn_hide != null)
            {
                // Position back button next to name
                var rtHide = btn_hide.GetComponent<RectTransform>();
                rtHide.anchorMin = new Vector2(0f, 1f);
                rtHide.anchorMax = new Vector2(0f, 1f);
                rtHide.pivot = new Vector2(0f, 0.5f);
                rtHide.anchoredPosition = new Vector2(6f, -17f); // centered vertically in 30px bar (visually centered at -17f)
                rtHide.sizeDelta = new Vector2(16f, 16f);
                
                var txt = btn_hide.GetComponentInChildren<Text>();
                if (txt != null) {
                    txt.gameObject.SetActive(false); // Hide the old "-" text
                }

                // Add plain-arrow icon as child and rotate 270 degrees (pointing left)
                var iconTrans = btn_hide.transform.Find("Icon");
                if (iconTrans == null) {
                    var iconGO = new GameObject("Icon", typeof(RectTransform));
                    iconGO.transform.SetParent(btn_hide.transform, false);
                    var rtIcon = iconGO.GetComponent<RectTransform>();
                    rtIcon.anchorMin = Vector2.zero;
                    rtIcon.anchorMax = Vector2.one;
                    rtIcon.offsetMin = new Vector2(2f, 2f); // padding
                    rtIcon.offsetMax = new Vector2(-2f, -2f);
                    rtIcon.localEulerAngles = new Vector3(0f, 0f, 270f); // Rotate 270 degrees for plain-arrow!
                    
                    var imgIcon = iconGO.AddComponent<Image>();
                    imgIcon.sprite = Resources.Load<Sprite>("UI/plain-arrow");
                    imgIcon.color = new Color(0.9f, 0.8f, 0.6f, 1f); // golden-brown color
                }
                else {
                    iconTrans.gameObject.SetActive(true);
                    var rtIcon = iconTrans.GetComponent<RectTransform>();
                    rtIcon.localEulerAngles = new Vector3(0f, 0f, 270f);
                    var imgIcon = iconTrans.GetComponent<Image>();
                    if (imgIcon != null) {
                        imgIcon.sprite = Resources.Load<Sprite>("UI/plain-arrow");
                        imgIcon.color = new Color(0.9f, 0.8f, 0.6f, 1f);
                    }
                }

                // Hide the button's background box completely so only the icon is visible
                var imgBtn = btn_hide.GetComponent<Image>();
                if (imgBtn != null) {
                    imgBtn.sprite = null;
                    imgBtn.color = Color.clear;
                }
                
                if (btn_hide.gameObject.GetComponent<UIButtonScaleFeedback>() == null)
                    btn_hide.gameObject.AddComponent<UIButtonScaleFeedback>();
            }

            if (btn_close != null)
            {
                btn_close.gameObject.SetActive(true); // Ensure Delete button is active/visible!
                var rtClose = btn_close.GetComponent<RectTransform>();
                rtClose.anchorMin = new Vector2(1f, 1f);
                rtClose.anchorMax = new Vector2(1f, 1f);
                rtClose.pivot = new Vector2(1f, 0.5f);
                rtClose.anchoredPosition = new Vector2(-74f, -17f); // centered vertically in 30px bar (visually centered at -17f)
                rtClose.sizeDelta = new Vector2(62f, 24f); // 24px height, 62px width (fits perfectly in 30px bar)

                var imgClose = btn_close.GetComponent<Image>();
                if (imgClose == null)
                {
                    var rawImg = btn_close.GetComponent<RawImage>();
                    if (rawImg != null) DestroyImmediate(rawImg);
                    imgClose = btn_close.gameObject.AddComponent<Image>();
                }

                if (imgClose != null)
                {
                    imgClose.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_delete_container_v2", 62, 24, 1,
                        new Color(0.1f, 0.09f, 0.08f, 1f),
                        new Color(0.45f, 0.35f, 0.15f, 1f),
                        1);
                    imgClose.color = Color.white;
                }

                // Create Box child for Delete button (Icon box)
                var closeBoxTrans = btn_close.transform.Find("Box");
                if (closeBoxTrans == null)
                {
                    var boxGO = new GameObject("Box", typeof(RectTransform));
                    boxGO.transform.SetParent(btn_close.transform, false);
                    closeBoxTrans = boxGO.transform;
                }
                var rtCloseBox = closeBoxTrans.GetComponent<RectTransform>();
                rtCloseBox.anchorMin = new Vector2(0f, 0.5f);
                rtCloseBox.anchorMax = new Vector2(0f, 0.5f);
                rtCloseBox.pivot = new Vector2(0f, 0.5f);
                rtCloseBox.anchoredPosition = new Vector2(3f, 0f); // 3px left padding
                rtCloseBox.sizeDelta = new Vector2(18f, 18f); // shrunk by 2 more pixels (originally 20x20, now 18x18)

                var imgCloseBox = closeBoxTrans.GetComponent<Image>();
                if (imgCloseBox == null) imgCloseBox = closeBoxTrans.gameObject.AddComponent<Image>();
                imgCloseBox.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_delete_icon_box_v4", 18, 18, 1,
                    new Color(0.1f, 0.09f, 0.08f, 1f),
                    new Color(0.45f, 0.35f, 0.15f, 1f),
                    1);
                imgCloseBox.color = Color.white;

                var txtCloseBox = closeBoxTrans.GetComponentInChildren<Text>();
                if (txtCloseBox == null)
                {
                    var txtGO = new GameObject("Text");
                    txtGO.transform.SetParent(closeBoxTrans, false);
                    txtCloseBox = txtGO.AddComponent<Text>();
                    txtCloseBox.rectTransform.anchorMin = Vector2.zero;
                    txtCloseBox.rectTransform.anchorMax = Vector2.one;
                    txtCloseBox.rectTransform.offsetMin = Vector2.zero;
                    txtCloseBox.rectTransform.offsetMax = Vector2.zero;
                }
                txtCloseBox.text = "X";
                txtCloseBox.font = uiMgr.GetSafeFont(11);
                txtCloseBox.fontStyle = FontStyle.Bold;
                txtCloseBox.fontSize = 11; // enlarged X mark from 9 to 11
                txtCloseBox.color = new Color(0.9f, 0.8f, 0.6f, 1f);
                txtCloseBox.alignment = TextAnchor.MiddleCenter;
                txtCloseBox.horizontalOverflow = HorizontalWrapMode.Overflow;
                txtCloseBox.verticalOverflow = VerticalWrapMode.Overflow;
                AddLocalTextShadow(txtCloseBox);

                // Create Label child for Delete button
                var closeLabelTrans = btn_close.transform.Find("Label");
                if (closeLabelTrans == null)
                {
                    var labelGO = new GameObject("Label", typeof(RectTransform));
                    labelGO.transform.SetParent(btn_close.transform, false);
                    closeLabelTrans = labelGO.transform;
                }
                var rtCloseLabel = closeLabelTrans.GetComponent<RectTransform>();
                rtCloseLabel.anchorMin = new Vector2(0f, 0.5f);
                rtCloseLabel.anchorMax = new Vector2(0f, 0.5f);
                rtCloseLabel.pivot = new Vector2(0f, 0.5f);
                rtCloseLabel.anchoredPosition = new Vector2(24f, 0f);
                rtCloseLabel.sizeDelta = new Vector2(38f, 24f);

                var txtCloseLabel = closeLabelTrans.GetComponentInChildren<Text>();
                if (txtCloseLabel == null)
                {
                    var txtGO = new GameObject("Text");
                    txtGO.transform.SetParent(closeLabelTrans, false);
                    txtCloseLabel = txtGO.AddComponent<Text>();
                    txtCloseLabel.rectTransform.anchorMin = Vector2.zero;
                    txtCloseLabel.rectTransform.anchorMax = Vector2.one;
                    txtCloseLabel.rectTransform.offsetMin = Vector2.zero;
                    txtCloseLabel.rectTransform.offsetMax = Vector2.zero;
                }
                txtCloseLabel.text = "Delete";
                txtCloseLabel.font = uiMgr.GetSafeFont(9);
                txtCloseLabel.fontStyle = FontStyle.Bold;
                txtCloseLabel.fontSize = 9;
                txtCloseLabel.color = new Color(0.9f, 0.8f, 0.6f, 1f);
                txtCloseLabel.alignment = TextAnchor.MiddleCenter;
                txtCloseLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                txtCloseLabel.verticalOverflow = VerticalWrapMode.Overflow;
                AddLocalTextShadow(txtCloseLabel);

                // Hide the old legacy Text component directly under btn_close
                var oldTxt = btn_close.transform.Find("Text");
                if (oldTxt != null && oldTxt != closeBoxTrans?.Find("Text") && oldTxt != closeLabelTrans?.Find("Text"))
                {
                    oldTxt.gameObject.SetActive(false);
                }

                if (btn_close.gameObject.GetComponent<UIButtonScaleFeedback>() == null)
                    btn_close.gameObject.AddComponent<UIButtonScaleFeedback>();
            }

            // Spawn Block checkbox button dynamically next to Delete
            var blockTrans = transform.Find("btn_block");
            if (blockTrans == null)
            {
                var blockGO = new GameObject("btn_block", typeof(RectTransform));
                blockGO.transform.SetParent(transform, false);
                blockTrans = blockGO.transform;
            }

            var rtBlock = blockTrans.GetComponent<RectTransform>();
            if (rtBlock != null)
            {
                rtBlock.anchorMin = new Vector2(1f, 1f);
                rtBlock.anchorMax = new Vector2(1f, 1f);
                rtBlock.pivot = new Vector2(1f, 0.5f);
                rtBlock.anchoredPosition = new Vector2(-6f, -17f); // centered vertically in 30px bar (visually centered at -17f)
                rtBlock.sizeDelta = new Vector2(62f, 24f); // 24px height, 62px width
            }

            var btnBlock = blockTrans.GetComponent<Button>();
            if (btnBlock == null) btnBlock = blockTrans.gameObject.AddComponent<Button>();
            
            var imgBlock = blockTrans.GetComponent<Image>();
            if (imgBlock == null) imgBlock = blockTrans.gameObject.AddComponent<Image>();
            if (imgBlock != null)
            {
                imgBlock.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_block_container_v2", 62, 24, 1,
                    new Color(0.1f, 0.09f, 0.08f, 1f),
                    new Color(0.45f, 0.35f, 0.15f, 1f),
                    1);
                imgBlock.color = Color.white;
            }

            // Create Box child for checkbox (starts at left, 24x24px)
            var boxTrans = blockTrans.Find("Box");
            if (boxTrans == null)
            {
                var boxGO = new GameObject("Box", typeof(RectTransform));
                boxGO.transform.SetParent(blockTrans, false);
                boxTrans = boxGO.transform;
            }
            var rtBox = boxTrans.GetComponent<RectTransform>();
            rtBox.anchorMin = new Vector2(0f, 0.5f);
            rtBox.anchorMax = new Vector2(0f, 0.5f);
            rtBox.pivot = new Vector2(0f, 0.5f);
            rtBox.anchoredPosition = new Vector2(3f, 0f); // 3px left padding
            rtBox.sizeDelta = new Vector2(18f, 18f); // shrunk by 2 more pixels (originally 20x20, now 18x18)

            var imgBox = boxTrans.GetComponent<Image>();
            if (imgBox == null) imgBox = boxTrans.gameObject.AddComponent<Image>();
            imgBox.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_block_box_v4", 18, 18, 1,
                new Color(0.1f, 0.09f, 0.08f, 1f),
                new Color(0.45f, 0.35f, 0.15f, 1f),
                1);
            imgBox.color = Color.white;

            var boxTxt = boxTrans.Find("Text")?.GetComponent<Text>();
            if (boxTxt == null)
            {
                var boxTxtGO = new GameObject("Text");
                boxTxtGO.transform.SetParent(boxTrans, false);
                boxTxt = boxTxtGO.AddComponent<Text>();
                boxTxt.rectTransform.anchorMin = Vector2.zero;
                boxTxt.rectTransform.anchorMax = Vector2.one;
                boxTxt.rectTransform.offsetMin = Vector2.zero;
                boxTxt.rectTransform.offsetMax = Vector2.zero;
            }
            boxTxt.font = uiMgr.GetSafeFont(11);
            boxTxt.fontStyle = FontStyle.Bold;
            boxTxt.fontSize = 11; // enlarged X mark from 9 to 11
            boxTxt.color = new Color(0.9f, 0.8f, 0.6f, 1f);
            boxTxt.alignment = TextAnchor.MiddleCenter;
            boxTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            boxTxt.verticalOverflow = VerticalWrapMode.Overflow;
            AddLocalTextShadow(boxTxt);

            // Create Label child for Block text (next to the checkbox box, 38x24px)
            var labelTrans = blockTrans.Find("Label");
            if (labelTrans == null)
            {
                var labelGO = new GameObject("Label", typeof(RectTransform));
                labelGO.transform.SetParent(blockTrans, false);
                labelTrans = labelGO.transform;
            }

            var rtLabel = labelTrans.GetComponent<RectTransform>();
            rtLabel.anchorMin = new Vector2(0f, 0.5f);
            rtLabel.anchorMax = new Vector2(0f, 0.5f);
            rtLabel.pivot = new Vector2(0f, 0.5f);
            rtLabel.anchoredPosition = new Vector2(24f, 0f);
            rtLabel.sizeDelta = new Vector2(38f, 24f);

            var imgLabel = labelTrans.GetComponent<Image>();
            if (imgLabel != null) DestroyImmediate(imgLabel); // Remove the old visual border from previous implementation

            var txtLabel = labelTrans.GetComponentInChildren<Text>();
            if (txtLabel == null)
            {
                var txtGO = new GameObject("Text");
                txtGO.transform.SetParent(labelTrans, false);
                txtLabel = txtGO.AddComponent<Text>();
                txtLabel.rectTransform.anchorMin = Vector2.zero;
                txtLabel.rectTransform.anchorMax = Vector2.one;
                txtLabel.rectTransform.offsetMin = Vector2.zero;
                txtLabel.rectTransform.offsetMax = Vector2.zero;
            }
            txtLabel.text = "Block";
            txtLabel.font = uiMgr.GetSafeFont(9);
            txtLabel.fontStyle = FontStyle.Bold;
            txtLabel.fontSize = 9;
            txtLabel.color = new Color(0.9f, 0.8f, 0.6f, 1f);
            txtLabel.alignment = TextAnchor.MiddleCenter;
            txtLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            txtLabel.verticalOverflow = VerticalWrapMode.Overflow;
            AddLocalTextShadow(txtLabel);

            btnBlock.onClick.RemoveAllListeners();
            btnBlock.onClick.AddListener(() => {
                KOUIManager.Instance?.ParseChattingCommand($"/block {TargetName}");
            });

            if (blockTrans.gameObject.GetComponent<UIButtonScaleFeedback>() == null)
                blockTrans.gameObject.AddComponent<UIButtonScaleFeedback>();

            // Sync visual checkbox state at load
            UpdateBlockStateVisual();

            // 3. Other UI elements (scroll, edit_chat)
            var uiImages = GetComponentsInChildren<Image>(true);
            foreach (var img in uiImages)
            {
                if (img.name.Equals("scroll", System.StringComparison.OrdinalIgnoreCase))
                {
                    img.sprite = null;
                    img.color = new Color(0.04f, 0.03f, 0.02f, 0.6f);
                }
                else if (img.gameObject == edit_chat?.gameObject || 
                         img.name.Equals("EditText", System.StringComparison.OrdinalIgnoreCase))
                {
                    img.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_input_bg", 292, 24, 10,
                        new Color(0.08f, 0.07f, 0.06f, 0.9f),
                        new Color(0.35f, 0.28f, 0.18f, 0.8f),
                        1);
                    img.color = Color.white;

                    var outline = img.gameObject.GetComponent<Outline>();
                    if (outline != null) Destroy(outline);
                }
                else if (img.gameObject == btn_chat?.gameObject)
                {
                    img.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_send_btn", 60, 22, 4,
                        new Color(0.25f, 0.2f, 0.15f, 1f),
                        new Color(0.6f, 0.48f, 0.22f, 1f),
                        1);
                    img.color = Color.white;
                }
            }

            // Enforce correct alignments and positions for the scroll area under the new 30px bar
            if (scroll != null)
            {
                var rtScroll = scroll.GetComponent<RectTransform>();
                if (rtScroll != null)
                {
                    rtScroll.anchorMin = new Vector2(0f, 0f);
                    rtScroll.anchorMax = new Vector2(1f, 1f);
                    rtScroll.offsetMin = new Vector2(10f, 32f); // leaves 32px at bottom for input, X set to 10f
                    rtScroll.offsetMax = new Vector2(-10f, -34f); // adjusted for 30px bar + 2px top gap + 2px padding, X set to -10f
                }
            }

            if (edit_chat != null)
            {
                // Ensure placeholder text exists
                var placeholder = edit_chat.placeholder as Text;
                if (placeholder == null)
                {
                    var pTrans = edit_chat.transform.Find("Placeholder");
                    if (pTrans != null) placeholder = pTrans.GetComponent<Text>();
                }
                if (placeholder == null)
                {
                    var pGO = new GameObject("Placeholder", typeof(RectTransform));
                    pGO.transform.SetParent(edit_chat.transform, false);
                    placeholder = pGO.AddComponent<Text>();
                    placeholder.font = edit_chat.textComponent != null ? edit_chat.textComponent.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
                    placeholder.fontSize = edit_chat.textComponent != null ? edit_chat.textComponent.fontSize : 11;
                    placeholder.alignment = edit_chat.textComponent != null ? edit_chat.textComponent.alignment : TextAnchor.MiddleLeft;
                    edit_chat.placeholder = placeholder;
                }

                if (placeholder != null)
                {
                    placeholder.text = "Message...";
                    placeholder.color = new Color(0.5f, 0.45f, 0.35f, 0.7f);
                    placeholder.alignment = TextAnchor.MiddleLeft; // Force left alignment

                    var rtPlaceholder = placeholder.GetComponent<RectTransform>();
                    if (rtPlaceholder != null)
                    {
                        rtPlaceholder.anchorMin = Vector2.zero;
                        rtPlaceholder.anchorMax = Vector2.one;
                        rtPlaceholder.offsetMin = Vector2.zero;
                        rtPlaceholder.offsetMax = Vector2.zero;
                    }
                }

                if (edit_chat.textComponent != null)
                {
                    edit_chat.textComponent.color = Color.white;
                }

                // Create a background sibling under the same parent to draw the rounded box
                var parentTrans = edit_chat.transform.parent;
                if (parentTrans != null)
                {
                    var bgTrans = parentTrans.Find("whisper_input_bg");
                    if (bgTrans == null)
                    {
                        var bgGO = new GameObject("whisper_input_bg", typeof(RectTransform));
                        bgGO.transform.SetParent(parentTrans, false);
                        bgTrans = bgGO.transform;
                        // Put it just behind edit_chat in rendering order
                        bgTrans.SetSiblingIndex(edit_chat.transform.GetSiblingIndex());
                    }

                    var rtBg = bgTrans.GetComponent<RectTransform>();
                    if (rtBg != null)
                    {
                        rtBg.anchorMin = new Vector2(0f, 0f);
                        rtBg.anchorMax = new Vector2(1f, 0f);
                        rtBg.pivot = new Vector2(0.5f, 0f);
                        rtBg.offsetMin = new Vector2(6f, 6f); // starts at X=6
                        rtBg.offsetMax = new Vector2(-6f, 30f); // ends at X=-6, 24px height

                        var bgImg = bgTrans.GetComponent<Image>();
                        if (bgImg == null) bgImg = bgTrans.gameObject.AddComponent<Image>();
                        bgImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_input_bg_v5", 292, 24, 10,
                            new Color(0.08f, 0.07f, 0.06f, 0.9f),
                            new Color(0.35f, 0.28f, 0.18f, 0.8f),
                            1);
                        bgImg.color = Color.white;
                        bgImg.enabled = true; // Ensure it is enabled!
                    }
                }

                // Disable input field's own Image rendering so it doesn't double-draw
                var editImg = edit_chat.GetComponent<Image>();
                if (editImg != null)
                {
                    editImg.enabled = false;
                }

                // Position actual input field 10px shifted inside the background
                var rtEdit = edit_chat.GetComponent<RectTransform>();
                if (rtEdit != null)
                {
                    rtEdit.anchorMin = new Vector2(0f, 0f);
                    rtEdit.anchorMax = new Vector2(1f, 0f);
                    rtEdit.pivot = new Vector2(0.5f, 0f);
                    rtEdit.offsetMin = new Vector2(16f, 6f); // 16px left (6px bg + 10px padding)
                    rtEdit.offsetMax = new Vector2(-16f, 30f); // -16px right (-6px bg - 10px padding)
                }
            }

            if (btn_chat != null)
            {
                var txt = btn_chat.GetComponentInChildren<Text>();
                if (txt != null) 
                { 
                    txt.color = new Color(0.9f, 0.8f, 0.6f, 1f); 
                    txt.fontStyle = FontStyle.Bold;
                    AddLocalTextShadow(txt);
                }
                if (btn_chat.gameObject.GetComponent<UIButtonScaleFeedback>() == null)
                    btn_chat.gameObject.AddComponent<UIButtonScaleFeedback>();

                // Shift btn_chat up by 6px so it stays aligned with edit_chat
                var rtChat = btn_chat.GetComponent<RectTransform>();
                if (rtChat != null)
                {
                    rtChat.anchoredPosition = new Vector2(rtChat.anchoredPosition.x, rtChat.anchoredPosition.y + 6f);
                }
            }
        }

        public void UpdateBlockStateVisual()
        {
            var blockTrans = transform.Find("btn_block");
            if (blockTrans != null)
            {
                var boxTxt = blockTrans.Find("Box/Text")?.GetComponent<Text>();
                if (boxTxt != null)
                {
                    bool isBlocked = GameOptionsManager.Instance != null && GameOptionsManager.Instance.IsPlayerBlocked(TargetName);
                    boxTxt.text = isBlocked ? "X" : "";
                }
                else
                {
                    Debug.LogError($"[WHISPER] UpdateBlockStateVisual: Box/Text not found under btn_block!");
                }
            }
            else
            {
                Debug.LogError($"[WHISPER] UpdateBlockStateVisual: btn_block not found under panel!");
            }
        }

        private void OnMinimizeClicked()
        {
            KOWhisperManager.Instance.MinimizeWindow(TargetName);
        }

        private void OnCloseClicked()
        {
            KOWhisperManager.Instance.CloseWindow(TargetName);
        }
    }
}
