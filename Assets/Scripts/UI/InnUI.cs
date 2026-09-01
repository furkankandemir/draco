using UnityEngine;
using UnityEngine.InputSystem;
using EntropyOnline.Network.KO;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUIInn (UIInn.h/UIInn.cpp)
    /// UIF: co_inn_us.uif
    ///
    /// N3_SP_WARE_INN (0x10) geldiğinde açılır.
    /// C++ UIInn.cpp:26-75 ReceiveMessage:
    ///   btn_warehouse → MsgSend_OpenWareHouse() + SetVisible(false)
    ///   btn_makeclan  → level/gold/clan kontrol + CreateClanName açma
    ///   btn_sale      → MsgSend_OpenTradeSellBBS() + SetVisible(false)
    /// C++ UIInn.cpp:108-117 OnKeyPress:
    ///   ESC → SetVisible(false)
    /// </summary>
    public class InnUI : MonoBehaviour
    {
        public static InnUI Instance { get; private set; }

        public bool IsVisible => _isVisible;

        public GameObject Panel => _panel;

        private GameObject _panel; // co_inn_us.uif'den yüklenen panel
        private bool _isVisible;

        // C++ birebir: KnightsManager.cpp:131 — pUser->m_pUserData->m_bLevel < 20
        private const int CLAN_LEVEL_LIMIT = 20;  // Sunucu kontrolü: Level >= 20
        private const int CLAN_COST = 500000;     // C++ GameDef.h — CLAN_COST

        // C++ birebir: CUICreateClanName — co_creat_clan_us.uif
        private GameObject _createClanPanel;
        private UnityEngine.UI.InputField _editClanName;

        // C++ birebir: CUIMessageBox — co_MsgBoxOkCancel_us.uif
        // InnUI kendi MsgBox panelini yönetir (KOMessageBox'a bağımlı değil)
        private GameObject _confirmPanel;
        private string _pendingClanName;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            var mgr = KOWarehouseManager.Instance;
            if (mgr != null) mgr.OnInnOpened += Show;
        }

        private void OnDisable()
        {
            var mgr = KOWarehouseManager.Instance;
            if (mgr != null) mgr.OnInnOpened -= Show;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // C++ birebir: UIInn.cpp:108-117 — ESC ile kapat
            if (_isVisible && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Hide();
        }

        // ============================
        // C++ birebir: SetVisible(true) — UIInn.cpp:99-106
        // ============================

        public void Show()

        {

            if (_panel == null) LoadPanel();

            if (_panel == null) return;

            _panel.SetActive(true);

            _isVisible = true;

            // Reposition Skill Bar to the left of this panel

            if (KOUIManager.Instance != null)

                KOUIManager.Instance.RepositionSkillBarForPanel();

        }

        public void Hide()

        {

            if (_panel != null) _panel.SetActive(false);

            _isVisible = false;

            // Restore Skill Bar position

            if (KOUIManager.Instance != null)

                KOUIManager.Instance.RepositionSkillBarForPanel();

        }

        // ============================

        // Custom Dynamic Right-Aligned Panel Loading

        // ============================

        private void LoadPanel()

        {

            Canvas canvas = FindKOCanvas();

            if (canvas == null) { Debug.LogError("[INN_UI] Canvas bulunamadı!"); return; }

            // Create panel container object

            _panel = new GameObject("InnUIPanel", typeof(RectTransform));

            _panel.transform.SetParent(canvas.transform, false);

            // Ensure scale-independent constant size dynamically
            _panel.AddComponent<KOUIScaleIndependent>();

            var rt = _panel.GetComponent<RectTransform>();

            rt.anchorMin = new Vector2(1f, 0.5f);

            rt.anchorMax = new Vector2(1f, 0.5f);

            rt.pivot = new Vector2(1f, 0.5f);

            rt.sizeDelta = new Vector2(200f, 132f); // Width 200, Height 132

            rt.anchoredPosition = new Vector2(-40f, 0f);

            // Generate background texture

            Texture2D bgTex = new Texture2D(200, 132, TextureFormat.RGBA32, false);

            Color bgColor = new Color(0.06f, 0.05f, 0.05f, 0.85f);

            Color borderColor = new Color(0.45f, 0.12f, 0.12f, 0.90f); // Dark red/bronze border

            for (int y = 0; y < 132; y++)

            {

                for (int x = 0; x < 200; x++)

                {

                    if (x < 2 || x >= 200 - 2 || y < 2 || y >= 132 - 2)

                        bgTex.SetPixel(x, y, borderColor);

                    else

                        bgTex.SetPixel(x, y, bgColor);

                }

            }

            bgTex.Apply();

            var bgImg = _panel.AddComponent<UnityEngine.UI.Image>();

            bgImg.sprite = Sprite.Create(bgTex, new Rect(0, 0, 200, 132), new Vector2(0.5f, 0.5f));

            // Create the 3 stacked buttons

            Color blueBtnColor = new Color(0.08f, 0.22f, 0.35f, 0.85f);

            Color redBtnColor = new Color(0.45f, 0.05f, 0.08f, 0.85f);

            Color textGold = new Color(0.92f, 0.80f, 0.52f, 1f);

            CreateInnButton(_panel.transform, "Warehouse Open", blueBtnColor, textGold, 180f, 32f, -10f, OnWarehouseClick);

            CreateInnButton(_panel.transform, "Item Seal/UnSeal", blueBtnColor, textGold, 180f, 32f, -50f, OnItemSealClick);

            CreateInnButton(_panel.transform, "Close", redBtnColor, textGold, 180f, 32f, -90f, Hide);

            _panel.SetActive(false);
        }

        /// <summary>
        /// C++ birebir: UIInn.cpp:26-75 ReceiveMessage
        /// Buton ID'leri: btn_warehouse, btn_makeclan, btn_sale, btn_close
        /// </summary>
        private void BindElements(Transform root)
        {
            // C++ UIInn.cpp:30 — btn_warehouse → MsgSend_OpenWareHouse()
            var btnWarehouse = KOUIRenderer.FindChildButton(root, "btn_warehouse");
            if (btnWarehouse != null)
            {
                btnWarehouse.onClick.AddListener(OnWarehouseClick);
            }

            // C++ UIInn.cpp:37 — btn_makeclan → clan oluşturma kontrolleri
            var btnMakeClan = KOUIRenderer.FindChildButton(root, "btn_makeclan");
            if (btnMakeClan != null)
            {
                btnMakeClan.onClick.AddListener(OnCreateClanClick);
            }

            // C++ UIInn.cpp:67 — btn_sale → Trade BBS
            var btnSale = KOUIRenderer.FindChildButton(root, "btn_sale");
            if (btnSale != null)
            {
                btnSale.onClick.AddListener(OnTradeBBSClick);
            }

            // btn_close — varsa bağla (bazı UIF'lerde mevcut)
            var btnClose = KOUIRenderer.FindChildButton(root, "btn_close");
            if (btnClose != null)
            {
                btnClose.onClick.AddListener(Hide);
            }
        }

        // ============================
        // C++ birebir: ReceiveMessage — UIInn.cpp:26-75
        // ============================

        /// <summary>
        /// C++ birebir: UIInn.cpp:30-35 — btn_warehouse
        /// MsgSend_OpenWareHouse() → WIZ_WAREHOUSE + N3_SP_WARE_OPEN
        /// </summary>
        private void OnWarehouseClick()
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_WAREHOUSE);
                pkt.WriteByte(KOWarehouseManager.N3_SP_WARE_OPEN);
                netMgr.SendPacket(pkt);
            }
            Hide(); // C++ birebir: UIInn.cpp:33 — SetVisible(false)
        }

        /// <summary>
        /// C++ birebir: UIInn.cpp:67-72 — btn_sale
        /// MsgSend_OpenTradeSellBBS
        /// </summary>
        private void OnTradeBBSClick()
        {
            // C++ birebir: UIInn.cpp:69 — MsgSend_OpenTradeSellBBS
            Hide();
        }

        // ============================
        // C++ birebir: UIInn.cpp:37-66 — btn_makeclan
        // ============================

        /// <summary>
        /// C++ birebir: UIInn.cpp:37-66 — btn_makeclan
        /// Level, Gold, mevcut clan kontrolü → m_pUICreateClanName->Open(IDS_CLAN_INPUT_NAME)
        /// </summary>
        private void OnCreateClanClick()
        {
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null)
            {
                // C++ UIInn.cpp:42 — CLAN_LEVEL_LIMIT kontrolü (DISABLED — ileride tekrar açılabilir)
                /*if (gm.Level < CLAN_LEVEL_LIMIT)
                {
                    if (KOUIManager.Instance != null)
                        KOUIManager.Instance.AddMsgOutput(
                            $"You need to be at least level {CLAN_LEVEL_LIMIT} to create a Knights.",
                            KOUIManager.D3DColorToUnity(0xffffff00));
                    Hide();
                    return;
                }*/
                // C++ UIInn.cpp:49 — CLAN_COST kontrolü
                if (gm.Gold < CLAN_COST)
                {
                    if (KOUIManager.Instance != null)
                        KOUIManager.Instance.AddMsgOutput(
                            "You don't have enough Noah to create a Knights.",
                            KOUIManager.D3DColorToUnity(0xffffff00));
                    Hide();
                    return;
                }
                // C++ UIInn.cpp:56 — zaten clan'da mı?
                if (!string.IsNullOrEmpty(gm.ClanName))
                {
                    if (KOUIManager.Instance != null)
                        KOUIManager.Instance.AddMsgOutput(
                            "You already belong to a Knights.",
                            KOUIManager.D3DColorToUnity(0xffffff00));
                    Hide();
                    return;
                }
            }

            // C++ UIInn.cpp:63 — m_pUICreateClanName->Open(IDS_CLAN_INPUT_NAME)
            Hide();
            OpenCreateClanNameUI();
        }

        // ============================
        // C++ birebir: CUICreateClanName (UICreateClanName.cpp)
        // UIF: co_creat_clan_us.uif
        // Elements: Text_Message, Edit_Clan, btn_yes, btn_no
        // ============================

        /// <summary>
        /// C++ birebir: CUICreateClanName::Open(int msg) — UICreateClanName.cpp:93-104
        /// </summary>
        private void OpenCreateClanNameUI()
        {
            if (_createClanPanel == null)
                LoadCreateClanPanel();
            if (_createClanPanel == null) return;

            // C++ birebir: UICreateClanName.cpp:101 — m_pEdit_ClanName->SetString("")
            if (_editClanName != null)
            {
                _editClanName.text = "";
                _editClanName.ActivateInputField();
            }

            _createClanPanel.SetActive(true);
            CenterPanel(_createClanPanel);

            // C++ birebir: Open(IDS_CLAN_INPUT_NAME) — title text set
            var titleText = KOUIRenderer.FindChildText(_createClanPanel.transform, "Text_Message");
            if (titleText != null)
                titleText.text = "Enter the name of the Knights you wish to create.";
        }

        /// <summary>
        /// C++ birebir: CUICreateClanName::Load() — UICreateClanName.cpp:33-41
        /// </summary>
        private void LoadCreateClanPanel()
        {
            Canvas canvas = FindKOCanvas();
            if (canvas == null) { Debug.LogError("[INN_UI] Canvas bulunamadı!"); return; }

            string uifPath = System.IO.Path.Combine("UI_US", "co_creat_clan_us.uif");
            _createClanPanel = KOUIRenderer.LoadUI(uifPath, canvas.transform);
            if (_createClanPanel == null)
            {
                Debug.LogError("[INN_UI] co_creat_clan_us.uif render edilemedi!");
                return;
            }
            _createClanPanel.SetActive(false);

            // C++ birebir: UICreateClanName.cpp:38-39 — Edit_Clan
            var editTr = KOUIRenderer.FindChildByID(_createClanPanel.transform, "Edit_Clan");
            if (editTr != null)
                _editClanName = editTr.GetComponent<UnityEngine.UI.InputField>();
            if (_editClanName != null)
                _editClanName.characterLimit = 20;

            // C++ birebir: UICreateClanName.cpp:48-55 — btn_yes → MakeClan()
            var btnYes = KOUIRenderer.FindChildButton(_createClanPanel.transform, "btn_yes");
            if (btnYes != null)
            {
                btnYes.onClick.AddListener(OnClanNameConfirm);
            }

            // C++ birebir: UICreateClanName.cpp:58-62 — btn_no → SetVisible(false)
            var btnNo = KOUIRenderer.FindChildButton(_createClanPanel.transform, "btn_no");
            if (btnNo != null)
            {
                btnNo.onClick.AddListener(() =>
                {
                    if (_createClanPanel != null) _createClanPanel.SetActive(false);
                    if (_editClanName != null) _editClanName.DeactivateInputField();
                });
            }

        }

        /// <summary>
        /// C++ birebir: CUICreateClanName::MakeClan() — UICreateClanName.cpp:67-78
        /// </summary>
        private void OnClanNameConfirm()
        {
            if (_editClanName == null) return;

            string clanName = _editClanName.text;

            if (string.IsNullOrWhiteSpace(clanName))
            {
                return;
            }

            if (clanName.Length > 20)
                clanName = clanName.Substring(0, 20);

            // CreateClanName panelini kapat
            if (_createClanPanel != null) _createClanPanel.SetActive(false);
            if (_editClanName != null) _editClanName.DeactivateInputField();

            // C++ birebir: MessageBoxPost(IDS_CLAN_WARNING_COST, MB_YESNO, BEHAVIOR_KNIGHTS_CREATE)
            _pendingClanName = clanName;
            ShowConfirmPanel($"Creating a Knights will cost you {CLAN_COST:N0} Noah. Do you want to continue?");
        }

        // ============================
        // Onay Paneli — co_MsgBoxOkCancel_us.uif
        // KOMessageBox'a bağımlı OLMADAN kendi panelini yönetir
        // ============================

        /// <summary>
        /// co_MsgBoxOkCancel_us.uif panelini yükler, butonlarını bağlar ve gösterir.
        /// </summary>
        private void ShowConfirmPanel(string message)
        {
            if (_confirmPanel == null)
                LoadConfirmPanel();
            if (_confirmPanel == null)
            {
                Debug.LogError("[INN_UI] Confirm panel yüklenemedi — doğrudan gönderiliyor");
                // Fallback
                KOKnightsManager.Instance?.MsgSend_KnightsCreate(_pendingClanName);
                return;
            }

            // Mesajı ayarla
            KOUIRenderer.SetChildText(_confirmPanel, "text_msg", message);
            KOUIRenderer.SetChildText(_confirmPanel, "Text_Message", message); // fallback

            _confirmPanel.SetActive(true);
            CenterPanel(_confirmPanel);

        }

        private void LoadConfirmPanel()
        {
            Canvas canvas = FindKOCanvas();
            if (canvas == null) return;

            string uifPath = System.IO.Path.Combine("UI_US", "co_MsgBoxOkCancel_us.uif");
            _confirmPanel = KOUIRenderer.LoadUI(uifPath, canvas.transform);
            if (_confirmPanel == null)
            {
                Debug.LogError("[INN_UI] co_MsgBoxOkCancel_us.uif yüklenemedi!");
                return;
            }
            _confirmPanel.SetActive(false);

            // btn_ok → clan oluştur
            var btnOk = KOUIRenderer.FindChildButton(_confirmPanel.transform, "btn_ok");
            if (btnOk != null)
            {
                btnOk.onClick.AddListener(OnConfirmOk);
            }
            else
            {
                Debug.LogError("[INN_UI] ❌ btn_ok bulunamadı!");
            }

            // btn_cancel → iptal
            var btnCancel = KOUIRenderer.FindChildButton(_confirmPanel.transform, "btn_cancel");
            if (btnCancel != null)
            {
                btnCancel.onClick.AddListener(OnConfirmCancel);
            }
        }

        private void OnConfirmOk()
        {
            if (_confirmPanel != null) _confirmPanel.SetActive(false);

            // C++ birebir: UIMessageBox.cpp:146-149
            // case BEHAVIOR_KNIGHTS_CREATE: m_pUICreateClanName->MsgSend_MakeClan();
            if (!string.IsNullOrEmpty(_pendingClanName) && KOKnightsManager.Instance != null)
            {
                KOKnightsManager.Instance.MsgSend_KnightsCreate(_pendingClanName);
            }
            else
            {
                Debug.LogError($"[INN_UI] ❌ PAKET GÖNDERİLEMEDİ! pendingName='{_pendingClanName}' mgr={KOKnightsManager.Instance}");
            }
            _pendingClanName = null;
        }

        private void OnConfirmCancel()
        {
            if (_confirmPanel != null) _confirmPanel.SetActive(false);
            _pendingClanName = null;
        }

        private void OnItemSealClick()

        {

        }

        private GameObject CreateInnButton(Transform parent, string labelText, Color normalColor, Color textColor, float width, float height, float yPos, System.Action onClickAction)

        {

            GameObject btnObj = new GameObject(labelText + "_Btn", typeof(RectTransform));

            btnObj.transform.SetParent(parent, false);

            var rt = btnObj.GetComponent<RectTransform>();

            rt.anchorMin = new Vector2(0.5f, 1f);

            rt.anchorMax = new Vector2(0.5f, 1f);

            rt.pivot = new Vector2(0.5f, 1f);

            rt.sizeDelta = new Vector2(width, height);

            rt.anchoredPosition = new Vector2(0f, yPos);

            var img = btnObj.AddComponent<UnityEngine.UI.Image>();

            // Draw button background texture with thin border

            int w = Mathf.RoundToInt(width);

            int h = Mathf.RoundToInt(height);

            Texture2D btnTex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            Color borderCol = new Color(0.45f, 0.35f, 0.18f, 0.90f);

            if (normalColor.r > 0.4f && normalColor.g < 0.2f)

            {

                borderCol = new Color(0.65f, 0.15f, 0.15f, 0.90f);

            }

            for (int y = 0; y < h; y++)

            {

                for (int x = 0; x < w; x++)

                {

                    if (x < 1 || x >= w - 1 || y < 1 || y >= h - 1)

                        btnTex.SetPixel(x, y, borderCol);

                    else

                        btnTex.SetPixel(x, y, normalColor);

                }

            }

            btnTex.Apply();

            img.sprite = Sprite.Create(btnTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));

            var btn = btnObj.AddComponent<UnityEngine.UI.Button>();

            btn.onClick.AddListener(() => onClickAction?.Invoke());

            GameObject txtObj = new GameObject("Text", typeof(RectTransform));

            txtObj.transform.SetParent(btnObj.transform, false);

            var txtRt = txtObj.GetComponent<RectTransform>();

            txtRt.anchorMin = Vector2.zero;

            txtRt.anchorMax = Vector2.one;

            txtRt.sizeDelta = Vector2.zero;

            var txt = txtObj.AddComponent<UnityEngine.UI.Text>();

            txt.text = labelText;

            txt.alignment = TextAnchor.MiddleCenter;

            txt.color = textColor;

            txt.fontSize = 12;

            txt.fontStyle = FontStyle.Bold;

            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 12);

            if (txt.font == null)

                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (txt.font == null)

                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return btnObj;

        }

        // ============================

        // Helpers

        // ============================

        private Canvas FindKOCanvas()
        {
            if (KOUIManager.Instance != null)
                return KOUIManager.Instance.Canvas;

            foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay ||
                    c.renderMode == RenderMode.ScreenSpaceCamera)
                    return c;
            }
            return Object.FindAnyObjectByType<Canvas>();
        }

        private void CenterPanel(GameObject panel)
        {
            var rt = panel.GetComponent<RectTransform>();
            if (rt == null) return;
            var canvas = panel.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var canvasRT = canvas.GetComponent<RectTransform>();
            float canvasW = canvasRT.rect.width;
            float canvasH = canvasRT.rect.height;
            float panelW = rt.sizeDelta.x;
            float panelH = rt.sizeDelta.y;
            float iX = (canvasW - panelW) / 2f;
            float iY = (canvasH - panelH) / 2f;
            rt.anchoredPosition = new Vector2(iX, -iY);
        }
    }
}
