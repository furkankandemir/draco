// =====================================================================================
// Open-KO birebir: UIMessageBox.cpp — CUIMessageBox
// + UIMessageBoxManager.cpp — CUIMessageBoxManager
//
// C++ MessageBoxPost(szMsg, szTitle, MB_YESNO, BEHAVIOR_xxx) karşılığı.
// UIF dosyaları: co_MsgBoxOkCancel_us.uif (Yes/No) ve co_MsgBoxOk_us.uif (OK)
// Behavior enumları ve geri çağrı mantığı birebir.
// =====================================================================================

using System;
using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: UIMessageBox.h:37-51 — e_MsgBoxBehavior
    /// </summary>
    public enum MsgBoxBehavior
    {
        BEHAVIOR_NOTHING = 0,
        BEHAVIOR_EXIT,
        BEHAVIOR_RESTART_GAME,
        BEHAVIOR_REGENERATION,
        BEHAVIOR_PARTY_PERMIT,
        BEHAVIOR_PARTY_DISBAND,
        BEHAVIOR_REQUEST_BINDPOINT,
        BEHAVIOR_KNIGHTS_CREATE,
        BEHAVIOR_KNIGHTS_DESTROY,
        BEHAVIOR_KNIGHTS_WITHDRAW,
        BEHAVIOR_PERSONAL_TRADE_PERMIT,
        BEHAVIOR_MGAME_LOGIN,
        BEHAVIOR_DELETE_CHR,
        BEHAVIOR_CLAN_JOIN,
        BEHAVIOR_PARTY_BBS_REGISTER,
        BEHAVIOR_PARTY_BBS_REGISTER_CANCEL,
        BEHAVIOR_EXECUTE_OPTION,
        BEHAVIOR_PERSONAL_TRADE_FMT_WAIT,
        BEHAVIOR_DISCONNECT,
    }

    /// <summary>
    /// Open-KO birebir: UIMessageBox.h:27-30 — iStyle
    /// </summary>
    public enum MsgBoxStyle
    {
        MB_OK = 0,
        MB_YESNO = 1,
        MB_CANCEL = 2,
    }

    /// <summary>
    /// Open-KO birebir: CUIMessageBox (UIMessageBox.cpp)
    /// + CUIMessageBoxManager (UIMessageBoxManager.cpp)
    ///
    /// UIF dosyaları:
    ///   co_MsgBoxOkCancel_us.uif — MB_YESNO stilinde (Btn_Yes + Btn_No)
    ///   co_MsgBoxOk_us.uif      — MB_OK stilinde (Btn_OK)
    ///
    /// KOUIManager.LoadUIPanel ile yüklenir, button/text binding KOUIRenderer ile yapılır.
    ///
    /// Kullanım:
    ///   KOMessageBox.Instance.ShowYesNo("Mesaj", "Başlık",
    ///     MsgBoxBehavior.BEHAVIOR_PARTY_PERMIT, onYes, onNo);
    /// </summary>
    public class KOMessageBox : MonoBehaviour
    {
        private static KOMessageBox _instance;
        public static KOMessageBox Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<KOMessageBox>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        // UIF panelleri — KOUIManager tarafından LoadUIPanel ile yüklenir
        private GameObject _uiMsgBoxOkCancel; // co_MsgBoxOkCancel_us.uif
        private GameObject _uiMsgBoxOk;       // co_MsgBoxOk_us.uif

        // State
        private MsgBoxBehavior _eBehavior = MsgBoxBehavior.BEHAVIOR_NOTHING;
        private MsgBoxStyle _iStyle = MsgBoxStyle.MB_OK;
        private Action _onYes;
        private Action _onNo;
        private GameObject _callerPanel;
        // Countdown state
        private Coroutine _countdownCoroutine;
        private GameObject _countdownProgressArea;
        private GameObject _countdownTextObj;


        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var canvas = KOUIManager.Instance?.Canvas;
            if (canvas != null && canvas.scaleFactor > 0f)
            {
                float targetScale = 1f / canvas.scaleFactor;
                if (_uiMsgBoxOkCancel != null && _uiMsgBoxOkCancel.activeSelf)
                {
                    var rt = _uiMsgBoxOkCancel.GetComponent<RectTransform>();
                    if (rt != null && Mathf.Abs(rt.localScale.x - targetScale) > 0.0001f)
                    {
                        rt.localScale = new Vector3(targetScale, targetScale, targetScale);
                    }
                }
                if (_uiMsgBoxOk != null && _uiMsgBoxOk.activeSelf)
                {
                    var rt = _uiMsgBoxOk.GetComponent<RectTransform>();
                    if (rt != null && Mathf.Abs(rt.localScale.x - targetScale) > 0.0001f)
                    {
                        rt.localScale = new Vector3(targetScale, targetScale, targetScale);
                    }
                }
                if (_inputDialog != null && _inputDialog.activeSelf)
                {
                    var rt = _inputDialog.GetComponent<RectTransform>();
                    if (rt != null && Mathf.Abs(rt.localScale.x - targetScale) > 0.0001f)
                    {
                        rt.localScale = new Vector3(targetScale, targetScale, targetScale);
                    }
                }
            }

            // Continuously center on callerPanel if active to prevent drift on screen resize
            if (_callerPanel != null && _callerPanel.activeSelf && KOUIManager.Instance != null)
            {
                var activePanel = _iStyle == MsgBoxStyle.MB_YESNO ? _uiMsgBoxOkCancel : _uiMsgBoxOk;
                if (activePanel != null && activePanel.activeSelf)
                {
                    KOUIManager.Instance.CenterPanelOnPanel(activePanel, _callerPanel);

                    // Apply upgrade Y offset
                    bool isAnyUpgradeOpen = KOUIManager.Instance.IsFastUpgradeUIOpen || 
                                           KOUIManager.Instance.IsUpgradeUIOpen || 
                                           KOUIManager.Instance.IsRingUpgradeOpen;
                    bool isTransaction = _callerPanel.name.ToLower().Contains("transaction") || _callerPanel.name.ToLower().Contains("trade");
                    bool isInventory = _callerPanel.name.ToLower().Contains("inventory");
                    if (!isAnyUpgradeOpen && !isTransaction && !isInventory)
                    {
                        var rt = activePanel.GetComponent<RectTransform>();
                        if (rt != null)
                            rt.anchoredPosition += new Vector2(0, 80f);
                    }
                }
            }
        }

        // ============================
        // UIF LOADING — KOUIManager tarafından çağrılır
        // ============================

        /// <summary>
        /// Open-KO birebir: CGameProcMain::InitUI (GameProcMain.cpp)
        /// m_pMsgBoxMgr = new CUIMessageBoxManager();
        /// → co_MsgBoxOkCancel_us.uif ve co_MsgBoxOk_us.uif yüklenir.
        ///
        /// KOUIManager.InitializeGameUI() içinden çağrılmalı.
        /// </summary>
        public void LoadMsgBoxPanels(string uiDir)
        {
            _uiMsgBoxOkCancel = KOUIManager.Instance?.LoadUIPanel(uiDir, "co_MsgBoxOkCancel_us.uif");
            _uiMsgBoxOk = KOUIManager.Instance?.LoadUIPanel(uiDir, "co_MsgBoxOk_us.uif");

            if (_uiMsgBoxOkCancel != null)
            {
                if (_uiMsgBoxOkCancel.GetComponent<KOUIScaleIndependent>() == null)
                    _uiMsgBoxOkCancel.AddComponent<KOUIScaleIndependent>();

                var canvasComp = _uiMsgBoxOkCancel.GetComponent<Canvas>();
                if (canvasComp == null)
                {
                    canvasComp = _uiMsgBoxOkCancel.AddComponent<Canvas>();
                }
                canvasComp.overrideSorting = true;
                canvasComp.sortingOrder = 200;
                if (_uiMsgBoxOkCancel.GetComponent<GraphicRaycaster>() == null)
                    _uiMsgBoxOkCancel.AddComponent<GraphicRaycaster>();

                _uiMsgBoxOkCancel.SetActive(false);
                var rt = _uiMsgBoxOkCancel.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(360f, 160f);

                KOUIManager.Instance?.ModernizeMessageBoxUI(_uiMsgBoxOkCancel.transform);
                BindButtons(_uiMsgBoxOkCancel);
            }

            if (_uiMsgBoxOk != null)
            {
                if (_uiMsgBoxOk.GetComponent<KOUIScaleIndependent>() == null)
                    _uiMsgBoxOk.AddComponent<KOUIScaleIndependent>();

                var canvasComp = _uiMsgBoxOk.GetComponent<Canvas>();
                if (canvasComp == null)
                {
                    canvasComp = _uiMsgBoxOk.AddComponent<Canvas>();
                }
                canvasComp.overrideSorting = true;
                canvasComp.sortingOrder = 200;
                if (_uiMsgBoxOk.GetComponent<GraphicRaycaster>() == null)
                    _uiMsgBoxOk.AddComponent<GraphicRaycaster>();

                _uiMsgBoxOk.SetActive(false);
                var rt = _uiMsgBoxOk.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(360f, 160f);

                KOUIManager.Instance?.ModernizeMessageBoxUI(_uiMsgBoxOk.transform);
                BindButtons(_uiMsgBoxOk);
            }
        }

        /// <summary>
        /// Open-KO birebir: UIMessageBox.cpp Load() — GetChildByID ile bağlama.
        ///   m_pBtn_OK      = GetChildByID<CN3UIButton>("Btn_OK")
        ///   m_pBtn_Yes     = GetChildByID<CN3UIButton>("Btn_Yes")
        ///   m_pBtn_No      = GetChildByID<CN3UIButton>("Btn_No")
        ///   m_pBtn_Cancel  = GetChildByID<CN3UIButton>("Btn_Cancel")
        ///   m_pText_Message = GetChildByID<CN3UIString>("Text_Message")
        ///   m_pText_Title   = GetChildByID<CN3UIString>("Text_Title")
        /// </summary>
        private void BindButtons(GameObject panel)
        {
            // Open-KO birebir: UIMessageBox.cpp:50-56
            var btnYes = KOUIRenderer.FindChildButton(panel, "Btn_Yes");
            var btnNo = KOUIRenderer.FindChildButton(panel, "Btn_No");
            // co_MsgBoxOkCancel_us.uif: buton ID'leri btn_ok / btn_cancel (küçük harf)
            var btnOk = KOUIRenderer.FindChildButton(panel, "btn_ok");
            var btnCancel = KOUIRenderer.FindChildButton(panel, "btn_cancel");

            // Sibling (panel altındaki) metinleri tespit et ve ilgili butonun altına taşı
            var allTexts = panel.GetComponentsInChildren<Text>(true);
            foreach (var txt in allTexts)
            {
                if (txt.gameObject.name.IndexOf("msg", StringComparison.OrdinalIgnoreCase) >= 0 || 
                    txt.gameObject.name.IndexOf("message", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    txt.gameObject.name.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                if (txt.transform.GetComponentInParent<Button>() != null)
                    continue;

                string txtName = txt.gameObject.name.ToLower();
                string txtContent = txt.text.ToLower();
                if (txtName.Contains("ok") || txtName.Contains("yes") || 
                    txtContent.Contains("ok") || txtContent.Contains("yes") || (txtContent.Contains("o") && txtContent.Contains("k")))
                {
                    var targetBtn = btnOk ?? btnYes;
                    if (targetBtn != null) txt.transform.SetParent(targetBtn.transform, false);
                }
                else if (txtName.Contains("cancel") || txtName.Contains("no") || 
                         txtContent.Contains("cancel") || txtContent.Contains("no"))
                {
                    var targetBtn = btnCancel ?? btnNo;
                    if (targetBtn != null) txt.transform.SetParent(targetBtn.transform, false);
                }
            }


            // Fallback: tüm Button child'larını tara
            if (btnOk == null && btnYes == null)
            {
                var allBtns = panel.GetComponentsInChildren<Button>(true);
            }

            if (btnYes != null)
            {
                btnYes.onClick.AddListener(OnYesClicked);
                StyleMessageBoxButton(btnYes, new Color(0.12f, 0.28f, 0.12f, 0.95f), new Color(0.25f, 0.55f, 0.25f, 0.95f), "Yes");
                var rt = btnYes.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(104f, 26f);
                    rt.anchoredPosition = new Vector2(-75f, 30f);
                }
            }
            if (btnNo != null)
            {
                btnNo.onClick.AddListener(OnNoClicked);
                StyleMessageBoxButton(btnNo, new Color(0.45f, 0.05f, 0.08f, 0.95f), new Color(0.75f, 0.15f, 0.15f, 0.95f), "No");
                var rt = btnNo.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(104f, 26f);
                    rt.anchoredPosition = new Vector2(75f, 30f);
                }
            }
            if (btnOk != null)
            {
                btnOk.onClick.AddListener(OnOkClicked);
                StyleMessageBoxButton(btnOk, new Color(0.12f, 0.28f, 0.12f, 0.95f), new Color(0.25f, 0.55f, 0.25f, 0.95f), "OK");
                var rt = btnOk.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(104f, 26f);
                    bool isOkCancelPanel = panel.name.Contains("OkCancel") || 
                                           KOUIRenderer.FindChildButton(panel, "btn_cancel") != null || 
                                           KOUIRenderer.FindChildButton(panel, "Btn_No") != null;
                    rt.anchoredPosition = isOkCancelPanel ? new Vector2(-75f, 30f) : new Vector2(0f, 30f);
                }
            }
            if (btnCancel != null)
            {
                btnCancel.onClick.AddListener(OnCancelClicked);
                StyleMessageBoxButton(btnCancel, new Color(0.45f, 0.05f, 0.08f, 0.95f), new Color(0.75f, 0.15f, 0.15f, 0.95f), "Cancel");
                var rt = btnCancel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(104f, 26f);
                    rt.anchoredPosition = new Vector2(75f, 30f);
                }
            }
        }

        private void StyleMessageBoxButton(Button button, Color normalColor, Color borderColor, string buttonText)
        {
            if (button == null) return;

            var raw = button.GetComponent<RawImage>();
            if (raw != null) DestroyImmediate(raw);

            var img = button.GetComponent<Image>();
            if (img == null) img = button.gameObject.AddComponent<Image>();

            int w = 104;
            int h = 26;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x < 1 || x >= w - 1 || y < 1 || y >= h - 1)
                        tex.SetPixel(x, y, borderColor);
                    else
                        tex.SetPixel(x, y, normalColor);
                }
            }
            tex.Apply();

            img.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            img.color = Color.white;

            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = img;

            var txtTrans = button.transform.Find("Text") ?? button.transform.Find("text") ?? (button.transform.childCount > 0 ? button.transform.GetChild(0) : null);
            if (txtTrans == null && button.transform.parent != null)
            {
                string btnName = button.gameObject.name.ToLower();
                string targetTxtName = btnName.Contains("ok") || btnName.Contains("yes") ? "text_ok" : "text_cancel";
                txtTrans = button.transform.parent.Find(targetTxtName) ?? button.transform.parent.Find("text_cancel") ?? button.transform.parent.Find("text_msg");
                if (txtTrans != null && txtTrans.gameObject.name != "text_msg" && txtTrans.gameObject.name != "Text_Message")
                {
                    txtTrans.SetParent(button.transform, false);
                }
                else
                {
                    txtTrans = null;
                }
            }
            else if (txtTrans != null)
            {
                txtTrans.SetParent(button.transform, false);
            }

            if (txtTrans != null)
            {
                var rtTxt = txtTrans.GetComponent<RectTransform>();
                if (rtTxt != null)
                {
                    rtTxt.anchorMin = Vector2.zero;
                    rtTxt.anchorMax = Vector2.one;
                    rtTxt.pivot = new Vector2(0.5f, 0.5f);
                    rtTxt.sizeDelta = Vector2.zero;
                    rtTxt.anchoredPosition = Vector2.zero;
                }

                var textComp = txtTrans.GetComponent<Text>();
                if (textComp != null)
                {
                    textComp.text = buttonText;
                    textComp.alignment = TextAnchor.MiddleCenter;
                    textComp.color = Color.white;
                    textComp.fontStyle = FontStyle.Bold;
                    textComp.fontSize = 14;
                }
            }
        }

        // ============================
        // PUBLIC API
        // ============================

        /// <summary>
        /// Open-KO birebir: CUIMessageBoxManager::MessageBoxPost
        /// (UIMessageBoxManager.cpp:45-82)
        ///
        /// pBox->SetText(szMsg);
        /// pBox->SetTitle(szTitle);
        /// pBox->SetBoxStyle(iStyle);
        /// pBox->m_eBehavior = eBehavior;
        /// pBox->SetVisible(true);
        /// </summary>
        public void Show(string message, string title, MsgBoxStyle style,
            MsgBoxBehavior behavior, Action onYes = null, Action onNo = null,
            GameObject callerPanel = null, int countdownDuration = 0, bool forceFixedCenter = false,
            bool autoClickOnTimeout = false, bool useBlocker = false)
        {
            _eBehavior = behavior;
            _iStyle = style;
            _onYes = onYes;
            _onNo = onNo;
            _callerPanel = callerPanel;

            // Stop any existing countdown
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }
            if (_countdownProgressArea != null)
            {
                Destroy(_countdownProgressArea);
                _countdownProgressArea = null;
            }

            // Aktif panel seç
            GameObject activePanel = (style == MsgBoxStyle.MB_YESNO || style == MsgBoxStyle.MB_CANCEL)
                ? _uiMsgBoxOkCancel
                : _uiMsgBoxOk;

            if (activePanel == null)
            {
                Debug.LogWarning($"[MSGBOX] Panel yüklenemedi: style={style}");
                return;
            }

            // Open-KO birebir: co_MsgBoxOkCancel_us.uif — text_msg
            KOUIRenderer.SetChildText(activePanel, "text_msg", message);
            KOUIRenderer.SetChildText(activePanel, "Text_Message", message); // fallback

            // Diğer paneli kapat
            if (_uiMsgBoxOkCancel != null) _uiMsgBoxOkCancel.SetActive(false);
            if (_uiMsgBoxOk != null) _uiMsgBoxOk.SetActive(false);

            activePanel.SetActive(true);

            // Canvas ve GraphicRaycaster zaten LoadMsgBoxPanels ile yüklendi, sortingOrder=200 olarak ayarlandı.
            // Sadece sorting order'ı garanti edelim
            var canvasComp = activePanel.GetComponent<Canvas>();
            if (canvasComp != null)
            {
                canvasComp.overrideSorting = true;
                canvasComp.sortingOrder = 200; // Above PUS (120)
            }

            activePanel.transform.SetAsLastSibling();

            // Button visibility and centering for MB_CANCEL
            var btnOk = KOUIRenderer.FindChildButton(activePanel, "btn_ok") ?? KOUIRenderer.FindChildButton(activePanel, "Btn_Yes");
            var btnCancel = KOUIRenderer.FindChildButton(activePanel, "btn_cancel") ?? KOUIRenderer.FindChildButton(activePanel, "Btn_No");

            if (style == MsgBoxStyle.MB_CANCEL)
            {
                if (btnOk != null) btnOk.gameObject.SetActive(false);
                if (btnCancel != null)
                {
                    btnCancel.gameObject.SetActive(true);
                    var rt = btnCancel.GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2(0f, 30f); // Center Cancel button
                }
            }
            else
            {
                if (btnOk != null)
                {
                    btnOk.gameObject.SetActive(true);
                    var rt = btnOk.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        bool isOkCancelPanel = activePanel.name.Contains("OkCancel") || 
                                               KOUIRenderer.FindChildButton(activePanel, "btn_cancel") != null || 
                                               KOUIRenderer.FindChildButton(activePanel, "Btn_No") != null;
                        rt.anchoredPosition = isOkCancelPanel ? new Vector2(-75f, 30f) : new Vector2(0f, 30f);
                    }
                }
                if (btnCancel != null)
                {
                    btnCancel.gameObject.SetActive(true);
                    var rt = btnCancel.GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2(75f, 30f);
                }
            }

            // Message text'i bul ve konumlandır
            var textMsgTrans = activePanel.transform.Find("text_msg") ?? activePanel.transform.Find("Text_Message");
            if (textMsgTrans != null)
            {
                var rtText = textMsgTrans.GetComponent<RectTransform>();
                if (rtText != null)
                {
                    rtText.anchorMin = new Vector2(0f, 1f);
                    rtText.anchorMax = new Vector2(1f, 1f);
                    rtText.pivot = new Vector2(0.5f, 1f);
                    if (!string.IsNullOrEmpty(title))
                    {
                        rtText.offsetMin = new Vector2(20f, -85f);
                        rtText.offsetMax = new Vector2(-20f, -32f);
                    }
                    else
                    {
                        rtText.offsetMin = new Vector2(20f, -85f);
                        rtText.offsetMax = new Vector2(-20f, -16f);
                    }
                }

                var textComp = textMsgTrans.GetComponent<Text>();
                if (textComp != null)
                {
                    textComp.fontSize = 14;
                    textComp.alignment = TextAnchor.MiddleCenter;
                    textComp.color = Color.white;
                }
            }

            // Handle Title text creation/update
            var titleTrans = activePanel.transform.Find("MsgBoxTitle");
            if (!string.IsNullOrEmpty(title))
            {
                GameObject titleObj;
                if (titleTrans == null)
                {
                    titleObj = new GameObject("MsgBoxTitle", typeof(RectTransform));
                    titleObj.transform.SetParent(activePanel.transform, false);
                }
                else
                {
                    titleObj = titleTrans.gameObject;
                }

                var titleRt = titleObj.GetComponent<RectTransform>() ?? titleObj.AddComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.sizeDelta = new Vector2(-40f, 25f);
                titleRt.anchoredPosition = new Vector2(0f, -12f);

                var titleText = titleObj.GetComponent<Text>() ?? titleObj.AddComponent<Text>();
                titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (titleText.font == null) titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                titleText.text = title;
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.color = new Color(0.95f, 0.85f, 0.35f, 1f); // Bright Gold/Yellow matching SKILL PAGE
                titleText.fontStyle = FontStyle.Bold;
                titleText.fontSize = 14;

                var titleShadow = titleObj.GetComponent<Shadow>() ?? titleObj.AddComponent<Shadow>();
                titleShadow.effectColor = Color.black;
                titleShadow.effectDistance = new Vector2(1f, -1f);

                titleObj.SetActive(true);
            }
            else
            {
                if (titleTrans != null)
                {
                    titleTrans.gameObject.SetActive(false);
                }
            }

            if (forceFixedCenter)
            {
                var rt = activePanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(360f, 160f);
                    rt.anchoredPosition = Vector2.zero;
                }
            }
            else if (callerPanel != null && callerPanel.activeSelf && KOUIManager.Instance != null)
            {
                // Caller panel merkezine konumla
                KOUIManager.Instance.CenterPanelOnPanel(activePanel, callerPanel);
                
                // Hızlı, normal ve takı upgrade paneli için Y offseti uygulama (tam ortasında çıksın)
                bool isAnyUpgradeOpen = KOUIManager.Instance.IsFastUpgradeUIOpen || 
                                       KOUIManager.Instance.IsUpgradeUIOpen || 
                                       KOUIManager.Instance.IsRingUpgradeOpen;
                bool isTransaction = callerPanel.name.ToLower().Contains("transaction") || callerPanel.name.ToLower().Contains("trade");
                bool isInventory = callerPanel.name.ToLower().Contains("inventory");
                if (!isAnyUpgradeOpen && !isTransaction && !isInventory)
                {
                    var rt = activePanel.GetComponent<RectTransform>();
                    if (rt != null)
                        rt.anchoredPosition += new Vector2(0, 80f); // Unity Y: yukarı = pozitif
                }
            }
            else
            {
                // Fallback: ekran ortası
                KOUIManager.Instance?.SetPanelPosCenter(activePanel);
            }

            // Setup full-screen modal blocker to prevent interactions with elements under the message box
            var canvasTrans = activePanel.transform.parent;
            if (canvasTrans != null)
            {
                if (useBlocker)
                {
                    var blockerName = "KOMsgBoxBlocker";
                    var blockerTrans = canvasTrans.Find(blockerName);
                    GameObject blockerGo;
                    if (blockerTrans == null)
                    {
                        blockerGo = new GameObject(blockerName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                        blockerGo.transform.SetParent(canvasTrans, false);
                    }
                    else
                    {
                        blockerGo = blockerTrans.gameObject;
                    }

                    blockerGo.SetActive(true);
                    
                    var blockerCanvas = blockerGo.GetComponent<Canvas>();
                    blockerCanvas.overrideSorting = true;
                    blockerCanvas.sortingOrder = 199; // Render immediately behind the MessageBox (200)

                    var rtBlocker = blockerGo.GetComponent<RectTransform>();
                    rtBlocker.anchorMin = Vector2.zero;
                    rtBlocker.anchorMax = Vector2.one;
                    rtBlocker.offsetMin = Vector2.zero;
                    rtBlocker.offsetMax = Vector2.zero;
                    rtBlocker.pivot = new Vector2(0.5f, 0.5f);
                    rtBlocker.localScale = Vector3.one;

                    var imgBlocker = blockerGo.GetComponent<UnityEngine.UI.Image>();
                    imgBlocker.color = new Color(0f, 0f, 0f, 0.4f); // Subtle semi-transparent black overlay
                    imgBlocker.raycastTarget = true;
                }
                else
                {
                    var blockerTrans = canvasTrans.Find("KOMsgBoxBlocker");
                    if (blockerTrans != null) blockerTrans.gameObject.SetActive(false);
                }
            }

            // Start countdown if requested
            if ((style == MsgBoxStyle.MB_YESNO || style == MsgBoxStyle.MB_CANCEL) && countdownDuration > 0)
            {
                var btnConfirm = style == MsgBoxStyle.MB_CANCEL
                    ? (KOUIRenderer.FindChildButton(activePanel, "btn_cancel") ?? KOUIRenderer.FindChildButton(activePanel, "Btn_No"))
                    : (KOUIRenderer.FindChildButton(activePanel, "Btn_Yes") ?? KOUIRenderer.FindChildButton(activePanel, "btn_ok"));
                if (btnConfirm != null)
                {
                    _countdownCoroutine = StartCoroutine(CountdownCoroutine(countdownDuration, btnConfirm, activePanel, autoClickOnTimeout));
                }
            }

        }


        /// <summary>
        /// MB_YESNO kısa yol — Open-KO'da en yaygın kullanım.
        /// </summary>
        public void ShowYesNo(string message, string title,
            MsgBoxBehavior behavior, Action onYes = null, Action onNo = null,
            GameObject callerPanel = null, int countdownDuration = 0, bool forceFixedCenter = false,
            bool autoClickOnTimeout = false)
        {
            Show(message, title, MsgBoxStyle.MB_YESNO, behavior, onYes, onNo, callerPanel, countdownDuration, forceFixedCenter, autoClickOnTimeout);
        }

        /// <summary>
        /// MB_OK kısa yol.
        /// </summary>
        public void ShowOk(string message, string title,
            MsgBoxBehavior behavior = MsgBoxBehavior.BEHAVIOR_NOTHING)
        {
            Show(message, title, MsgBoxStyle.MB_OK, behavior);
        }

        /// <summary>
        /// Input dialog — Open-KO UICreateClanName.cpp karşılığı.
        /// Mesaj gösterir ve kullanıcıdan metin girişi alır.
        /// Callback'e girilen metin gönderilir.
        /// </summary>
        private GameObject _inputDialog;
        private InputField _inputField;
        private Action<string> _onInputSubmit;

        public void ShowInput(string message, string title,
            MsgBoxBehavior behavior, Action<string> onSubmit)
        {
            _eBehavior = behavior;
            _onInputSubmit = onSubmit;

            if (_inputDialog == null)
            {
                CreateInputDialog();
            }

            if (_inputDialog != null)
            {
                _inputDialog.SetActive(true);
                _inputDialog.transform.SetAsLastSibling();

                _inputDialog.transform.localScale = Vector3.one;

                var msgTr = _inputDialog.transform.Find("MsgText");
                var msgText = msgTr != null ? msgTr.GetComponent<Text>() : null;
                if (msgText != null) msgText.text = message;

                if (_inputField != null) _inputField.text = "";
            }
        }

        private void CreateInputDialog()
        {
            // NPC'lerin başındaki veya dünya üzerindeki WorldSpace Canvas'ları es geçip ana HUD Canvas'ını bul
            Transform parentTransform = null;
            if (_uiMsgBoxOkCancel != null)
            {
                parentTransform = _uiMsgBoxOkCancel.transform.parent;
            }
            if (parentTransform == null && KOUIManager.Instance != null)
            {
                var canvas = KOUIManager.Instance.GetComponentInParent<Canvas>();
                if (canvas != null) parentTransform = canvas.transform;
            }
            if (parentTransform == null)
            {
                var canvases = FindObjectsByType<Canvas>();
                foreach (var c in canvases)
                {
                    if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
                    {
                        parentTransform = c.transform;
                        break;
                    }
                }
            }

            if (parentTransform == null) return;

            // Main Panel (Orijinal eşya satma onay penceresi boyutu: 360 x 160)
            _inputDialog = new GameObject("InputDialog", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _inputDialog.transform.SetParent(parentTransform, false);
            
            var canvasComp = _inputDialog.GetComponent<Canvas>();
            canvasComp.overrideSorting = true;
            canvasComp.sortingOrder = 200; // Standart mesaj kutusu sıralaması

            var rt = _inputDialog.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360f, 160f); // Eşya satma onay kutusu boyutu (360 x 160)
            rt.anchoredPosition = Vector2.zero; // Her zaman yatay ve dikeyde ekranın tam merkezinde tut

            var bg = _inputDialog.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                // Eşya satma onay kutusu stili: tam opak koyu zemin (alpha = 1.0), köşeler keskin (radius = 0), altın çerçeve
                bg.sprite = KOUIManager.Instance.GetSkillThemePanelBgSprite("msgbox_input_bg", 360, 160, 0,
                    new Color(0.12f, 0.10f, 0.08f, 1.0f),
                    new Color(0.05f, 0.04f, 0.04f, 1.0f),
                    new Color(0.55f, 0.45f, 0.20f, 1.0f), 2);
                bg.type = Image.Type.Simple;
            }
            else
            {
                bg.color = new Color(0.12f, 0.10f, 0.08f, 1.0f);
            }

            // 1. Title Text ("Create Clan" Başlığı)
            var titleObj = new GameObject("Text_Title", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(_inputDialog.transform, false);
            var titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.sizeDelta = new Vector2(340f, 22f);
            titleRt.anchoredPosition = new Vector2(0f, -20f);

            var titleText = titleObj.GetComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (titleText.font == null) titleText.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            titleText.fontSize = 16;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(0.95f, 0.82f, 0.45f); // Orta çağ altın sarısı
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.text = "Create Clan";

            // 1a. Title Divider (Skilltree başlık altı ayıracının aynısı)
            var dividerObj = new GameObject("TitleDivider", typeof(RectTransform), typeof(Image));
            dividerObj.transform.SetParent(_inputDialog.transform, false);
            var divRt = dividerObj.GetComponent<RectTransform>();
            divRt.anchorMin = new Vector2(0.5f, 1f);
            divRt.anchorMax = new Vector2(0.5f, 1f);
            divRt.pivot = new Vector2(0.5f, 0.5f);
            divRt.sizeDelta = new Vector2(260f, 2f); // Panel boyutuna uygun genişlik (260)
            divRt.anchoredPosition = new Vector2(0f, -34f);

            var dividerImg = dividerObj.GetComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                dividerImg.sprite = KOUIManager.Instance.GetSkillThemeFadingDividerSprite("input_dialog_title_divider", 260, 2, new Color(0.9f, 0.75f, 0.25f, 0.8f));
                dividerImg.type = Image.Type.Simple;
            }
            dividerImg.color = Color.white;

            // 2. Message Text (Açıklama/Şartlar)
            var msgObj = new GameObject("MsgText", typeof(RectTransform), typeof(Text));
            msgObj.transform.SetParent(_inputDialog.transform, false);
            var msgRt = msgObj.GetComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0.5f, 1f);
            msgRt.anchorMax = new Vector2(0.5f, 1f);
            msgRt.pivot = new Vector2(0.5f, 0.5f);
            msgRt.sizeDelta = new Vector2(340f, 36f);
            msgRt.anchoredPosition = new Vector2(0f, -56f);

            var msgText = msgObj.GetComponent<Text>();
            msgText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (msgText.font == null) msgText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            msgText.fontSize = 14;
            msgText.fontStyle = FontStyle.Bold;
            msgText.color = Color.white;
            msgText.alignment = TextAnchor.MiddleCenter;
            msgText.supportRichText = true;

            // 3. InputField (Köşeleri Yuvarlaklaştırılmış Metin Yuvası)
            var inputObj = new GameObject("InputField", typeof(RectTransform), typeof(Image));
            inputObj.transform.SetParent(_inputDialog.transform, false);
            var inputRt = inputObj.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.5f, 1f);
            inputRt.anchorMax = new Vector2(0.5f, 1f);
            inputRt.pivot = new Vector2(0.5f, 0.5f);
            inputRt.sizeDelta = new Vector2(240f, 26f); // Birebir MsgBoxOkCancel genişliğiyle uyumlu
            inputRt.anchoredPosition = new Vector2(0f, -92f);

            var inputBg = inputObj.GetComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                inputBg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("countable_edit_bg_socket_rounded", 240, 26, 4,
                    new Color(0.06f, 0.06f, 0.06f, 1.0f),
                    new Color(0.55f, 0.45f, 0.20f, 1.0f), 1);
                inputBg.type = Image.Type.Simple;
            }
            else
            {
                inputBg.color = new Color(0.06f, 0.06f, 0.06f, 1.0f);
            }

            var textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(inputObj.transform, false);
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f, 0f);
            textRt.offsetMax = new Vector2(-8f, 0f);

            var inputText = textObj.GetComponent<Text>();
            inputText.font = Font.CreateDynamicFontFromOSFont("Arial", 13);
            inputText.fontSize = 13;
            inputText.color = new Color(0.95f, 0.92f, 0.85f);
            inputText.alignment = TextAnchor.MiddleCenter;

            _inputField = inputObj.AddComponent<InputField>();
            _inputField.textComponent = inputText;
            _inputField.characterLimit = 20;

            // 4. OK Button (Standart Onay Kutusu OK Butonu)
            var okObj = new GameObject("BtnOK", typeof(RectTransform), typeof(Image), typeof(Button));
            okObj.transform.SetParent(_inputDialog.transform, false);
            var okRt = okObj.GetComponent<RectTransform>();
            okRt.anchorMin = new Vector2(0.5f, 1f);
            okRt.anchorMax = new Vector2(0.5f, 1f);
            okRt.pivot = new Vector2(0.5f, 0.5f);
            okRt.sizeDelta = new Vector2(104f, 26f); // Birebir MsgBoxOkCancel butonu boyutu (104 x 26)
            okRt.anchoredPosition = new Vector2(-75f, -130f); // Sol buton konumu

            var okTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            okTextObj.transform.SetParent(okObj.transform, false);
            var okTxtRt = okTextObj.GetComponent<RectTransform>();
            okTxtRt.anchorMin = Vector2.zero;
            okTxtRt.anchorMax = Vector2.one;
            okTxtRt.offsetMin = Vector2.zero;
            okTxtRt.offsetMax = Vector2.zero;

            var okText = okTextObj.GetComponent<Text>();
            okText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (okText.font == null) okText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            okText.fontSize = 14;
            okText.fontStyle = FontStyle.Bold;
            okText.color = Color.white;
            okText.alignment = TextAnchor.MiddleCenter;
            okText.text = "OK";

            var okBtn = okObj.GetComponent<Button>();
            StyleMessageBoxButton(okBtn, new Color(0.12f, 0.28f, 0.12f, 0.95f), new Color(0.25f, 0.55f, 0.25f, 0.95f), "OK");
            okBtn.onClick.AddListener(() =>
            {
                string text = _inputField != null ? _inputField.text : "";
                _inputDialog.SetActive(false);
                _onInputSubmit?.Invoke(text);
                _onInputSubmit = null;
            });

            // 5. Cancel Button (Standart Onay Kutusu Cancel Butonu)
            var cancelObj = new GameObject("BtnCancel", typeof(RectTransform), typeof(Image), typeof(Button));
            cancelObj.transform.SetParent(_inputDialog.transform, false);
            var cancelRt = cancelObj.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(0.5f, 1f);
            cancelRt.anchorMax = new Vector2(0.5f, 1f);
            cancelRt.pivot = new Vector2(0.5f, 0.5f);
            cancelRt.sizeDelta = new Vector2(104f, 26f); // Birebir MsgBoxOkCancel butonu boyutu (104 x 26)
            cancelRt.anchoredPosition = new Vector2(75f, -130f); // Sağ buton konumu

            var cancelTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            cancelTextObj.transform.SetParent(cancelObj.transform, false);
            var cancelTxtRt = cancelTextObj.GetComponent<RectTransform>();
            cancelTxtRt.anchorMin = Vector2.zero;
            cancelTxtRt.anchorMax = Vector2.one;
            cancelTxtRt.offsetMin = Vector2.zero;
            cancelTxtRt.offsetMax = Vector2.zero;

            var cancelText = cancelTextObj.GetComponent<Text>();
            cancelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cancelText.font == null) cancelText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            cancelText.fontSize = 14;
            cancelText.fontStyle = FontStyle.Bold;
            cancelText.color = Color.white;
            cancelText.alignment = TextAnchor.MiddleCenter;
            cancelText.text = "Cancel";

            var cancelBtn = cancelObj.GetComponent<Button>();
            StyleMessageBoxButton(cancelBtn, new Color(0.45f, 0.05f, 0.08f, 0.95f), new Color(0.75f, 0.15f, 0.15f, 0.95f), "Cancel");
            cancelBtn.onClick.AddListener(() =>
            {
                _inputDialog.SetActive(false);
                _onInputSubmit = null;
            });
        }

        public bool IsVisible =>
            (_uiMsgBoxOkCancel != null && _uiMsgBoxOkCancel.activeSelf) ||
            (_uiMsgBoxOk != null && _uiMsgBoxOk.activeSelf);

        public MsgBoxBehavior CurrentBehavior => _eBehavior;
        public GameObject CallerPanel => _callerPanel;

        /// <summary>Aktif olan MsgBox panelini döndürür (pozisyonlama için).</summary>
        public GameObject ActivePanel =>
            (_uiMsgBoxOkCancel != null && _uiMsgBoxOkCancel.activeSelf) ? _uiMsgBoxOkCancel :
            (_uiMsgBoxOk != null && _uiMsgBoxOk.activeSelf) ? _uiMsgBoxOk : null;

        // ============================
        // BUTTON HANDLERS
        // ============================

        /// <summary>
        /// Open-KO birebir: UIMessageBox.cpp:88 — pSender == m_pBtn_Yes
        /// </summary>
        private void OnYesClicked()
        {
            var callback = _onYes;
            Hide();
            callback?.Invoke();
        }

        /// <summary>
        /// Open-KO birebir: UIMessageBox.cpp:88 — pSender == m_pBtn_OK
        /// </summary>
        private void OnOkClicked()
        {
            var callback = _onYes;
            if (_eBehavior == MsgBoxBehavior.BEHAVIOR_DISCONNECT)
            {
                var btn = KOUIRenderer.FindChildButton(_uiMsgBoxOk, "btn_ok") ?? KOUIRenderer.FindChildButton(_uiMsgBoxOk, "Btn_Yes");
                if (btn != null) btn.interactable = false;
            }
            else
            {
                Hide();
            }
            callback?.Invoke();
        }

        /// <summary>
        /// Open-KO birebir: UIMessageBox.cpp:178 — pSender == m_pBtn_No
        /// </summary>
        private void OnNoClicked()
        {
            var callback = _onNo;
            Hide();
            callback?.Invoke();
        }

        /// <summary>
        /// Open-KO birebir: UIMessageBox.cpp:178 — pSender == m_pBtn_Cancel
        /// </summary>
        private void OnCancelClicked()
        {
            var callback = _onNo;
            Hide();
            callback?.Invoke();
        }

        public void Hide()
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }
            if (_countdownProgressArea != null)
            {
                Destroy(_countdownProgressArea);
                _countdownProgressArea = null;
            }
            if (_countdownTextObj != null)
            {
                Destroy(_countdownTextObj);
                _countdownTextObj = null;
            }

            // Restore canvas parent when hiding to prevent it from getting disabled with caller panels
            if (KOUIManager.Instance != null && KOUIManager.Instance.Canvas != null)
            {
                var canvasTrans = KOUIManager.Instance.Canvas.transform;
                var blockerTrans = canvasTrans.Find("KOMsgBoxBlocker");
                if (blockerTrans != null) blockerTrans.gameObject.SetActive(false);

                if (_uiMsgBoxOkCancel != null && _uiMsgBoxOkCancel.transform.parent != canvasTrans)
                {
                    _uiMsgBoxOkCancel.transform.SetParent(canvasTrans, false);
                }
                if (_uiMsgBoxOk != null && _uiMsgBoxOk.transform.parent != canvasTrans)
                {
                    _uiMsgBoxOk.transform.SetParent(canvasTrans, false);
                }
            }

            if (_uiMsgBoxOkCancel != null)
            {
                // Restore confirm button state in case it was locked by countdown
                var btnConfirm = KOUIRenderer.FindChildButton(_uiMsgBoxOkCancel, "Btn_Yes") ?? KOUIRenderer.FindChildButton(_uiMsgBoxOkCancel, "btn_ok");
                if (btnConfirm != null)
                {
                    btnConfirm.interactable = true;
                    var textTrans = btnConfirm.transform.Find("Text") ?? btnConfirm.transform.Find("text") ?? (btnConfirm.transform.childCount > 0 ? btnConfirm.transform.GetChild(0) : null);
                    var txtComp = textTrans != null ? textTrans.GetComponent<Text>() : null;
                    if (txtComp != null)
                    {
                        string originalName = btnConfirm.gameObject.name.ToLower().Contains("ok") ? "OK" : "Yes";
                        txtComp.text = originalName;
                    }
                }
                _uiMsgBoxOkCancel.SetActive(false);
            }
            if (_uiMsgBoxOk != null) _uiMsgBoxOk.SetActive(false);
            _eBehavior = MsgBoxBehavior.BEHAVIOR_NOTHING;
            _onYes = null;
            _onNo = null;
        }

        private System.Collections.IEnumerator CountdownCoroutine(int duration, Button btnConfirm, GameObject panel, bool autoClickOnTimeout)
        {
            // 1. Create Progress Area parent
            _countdownProgressArea = new GameObject("CountdownProgressArea");
            _countdownProgressArea.transform.SetParent(panel.transform, false);
            var areaRT = _countdownProgressArea.AddComponent<RectTransform>();
            areaRT.anchorMin = new Vector2(0.5f, 0f);
            areaRT.anchorMax = new Vector2(0.5f, 0f);
            areaRT.pivot = new Vector2(0.5f, 0.5f);
            areaRT.sizeDelta = new Vector2(250f, 10f); // 10f height
            areaRT.anchoredPosition = new Vector2(0f, 60f);

            // Background Image with rounded corners
            var bgImg = _countdownProgressArea.AddComponent<Image>();
            bgImg.sprite = CreateRoundedRectSprite(1000, 40, 20f, new Color(0.08f, 0.08f, 0.08f, 0.9f));
            bgImg.color = Color.white;

            // 2. Create Filling Image (child) - Maskless, Filled type
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(_countdownProgressArea.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(1f, 1f);
            fillRT.pivot = new Vector2(0f, 0.5f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = CreateRoundedRectSprite(1000, 40, 20f, new Color(1.0f, 0.83f, 0.0f, 1.0f));
            fillImg.color = Color.white;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;

            // 3. Create Countdown Text (sibling of ProgressArea to avoid Mask/Layout issues)
            _countdownTextObj = new GameObject("CountdownText");
            _countdownTextObj.transform.SetParent(panel.transform, false);
            var textRT = _countdownTextObj.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.5f, 0f);
            textRT.anchorMax = new Vector2(0.5f, 0f);
            textRT.pivot = new Vector2(0.5f, 0.5f);
            textRT.sizeDelta = new Vector2(250f, 12f);
            textRT.anchoredPosition = new Vector2(0f, 60f);
            var txt = _countdownTextObj.AddComponent<Text>();
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 8);
            txt.fontSize = 8;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontStyle = FontStyle.Bold;

            // Modify button appearance
            var confirmTextTrans = btnConfirm.transform.Find("Text") ?? btnConfirm.transform.Find("text") ?? (btnConfirm.transform.childCount > 0 ? btnConfirm.transform.GetChild(0) : null);
            var confirmText = confirmTextTrans != null ? confirmTextTrans.GetComponent<Text>() : null;
            string originalText = confirmText != null ? confirmText.text : (btnConfirm.gameObject.name.ToLower().Contains("ok") ? "OK" : "Yes");

            if (!autoClickOnTimeout)
            {
                btnConfirm.interactable = false;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float ratio = Mathf.Clamp01(1f - (elapsed / duration));
                
                // Update fill amount
                fillImg.fillAmount = ratio;
                
                int remaining = Mathf.CeilToInt(duration - elapsed);
                txt.text = remaining.ToString();

                if (confirmText != null)
                {
                    confirmText.text = $"{originalText} ({remaining})";
                }

                yield return null;
            }

            // Restore confirm button
            btnConfirm.interactable = true;
            if (confirmText != null)
            {
                confirmText.text = originalText;
            }

            // Clean up progress bar
            if (_countdownProgressArea != null)
            {
                Destroy(_countdownProgressArea);
                _countdownProgressArea = null;
            }
            if (_countdownTextObj != null)
            {
                Destroy(_countdownTextObj);
                _countdownTextObj = null;
            }
            _countdownCoroutine = null;

            if (autoClickOnTimeout)
            {
                OnNoClicked();
            }
        }

        private Sprite CreateRoundedRectSprite(int w, int h, float r, Color color)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Determine if pixel is in a corner
                    float cx = x;
                    float cy = y;
                    float targetX = -1f;
                    float targetY = -1f;

                    if (x < r)
                    {
                        targetX = r;
                    }
                    else if (x > w - 1 - r)
                    {
                        targetX = w - 1 - r;
                    }

                    if (y < r)
                    {
                        targetY = r;
                    }
                    else if (y > h - 1 - r)
                    {
                        targetY = h - 1 - r;
                    }

                    if (targetX >= 0f && targetY >= 0f)
                    {
                        // In a corner, compute distance to corner center
                        float dx = cx - targetX;
                        float dy = cy - targetY;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        float aa = 1.5f;
                        if (dist > r + aa / 2f)
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                        else if (dist < r - aa / 2f)
                        {
                            tex.SetPixel(x, y, color);
                        }
                        else
                        {
                            float alpha = 0.5f - (dist - r) / aa;
                            tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(alpha)));
                        }
                    }
                    else
                    {
                        tex.SetPixel(x, y, color);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }
    }
}
