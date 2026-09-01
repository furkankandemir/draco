using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Core;
using EntropyOnline.Import;
using TMPro;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: GameProcCharacterSelect.cpp + UICharacterSelect.cpp
    /// Modernized dynamically generated UI matching the design tokens of the LoginUI.
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        // Programmatic UI panel reference
        private GameObject _uiCharacterSelect;

        // Modern UI Buttons and References
        private Button _btnDelete;     
        private Button _btnBack;       
        private Button _btnCreate;
        private Button _btnStart;
        private Button[] _slotButtons;
        private TextMeshProUGUI[] _slotLeftTexts;
        private TextMeshProUGUI[] _slotRightTexts;

        // Canvas
        private Canvas _canvas;

        // Onay diyaloğu paneli (karakter seçim ekranında KOMessageBox yüklü değil)
        private GameObject _confirmPanel;

        // Open-KO birebir: s_iChrSelectIndex (GameProcedure.cpp:85)
        private int _selectedSlotIndex = 0;

        // Open-KO birebir: MAX_AVAILABLE_CHARACTER = 3 (GameDef.h:1264)
        private const int MAX_AVAILABLE_CHARACTER = 5;

        // Karakter bilgileri — cpp: m_InfoChrs[3]
        private CharacterListItem[] _characters;
        private bool[] _slotOccupied = new bool[MAX_AVAILABLE_CHARACTER];

        // C++ birebir: m_bReceivedCharacterSelect (cpp:46)
        private bool _receivedCharacterInfo = false;
        private bool _isTransitioning = false;



        // 3D sahne yöneticisi — cpp:128-247 (kamera, arka plan, ışıklar, karakter modelleri)
        private CharSelectScene3D _scene3D;

        // Design colors
        private Color _colorBg = new Color(0.12f, 0.10f, 0.08f, 0.96f);
        private Color _colorBgInner = new Color(0.04f, 0.04f, 0.04f, 0.96f);
        private Color _colorBorder = new Color(0.6f, 0.48f, 0.22f, 0.9f);
        private Color _colorTextGold = new Color(0.95f, 0.85f, 0.35f, 1f);
        private Color _colorBtnGold = new Color(0.48f, 0.38f, 0.22f, 1f);
        private Color _colorBtnGoldBorder = new Color(0.6f, 0.48f, 0.22f, 1f);
        private Color _colorInputBg = new Color(0.05f, 0.04f, 0.04f, 1f);
        private Color _colorBtnDark = new Color(0.08f, 0.07f, 0.06f, 0.95f);

        private void Start()
        {
            // Canvas oluştur — 1024x768 (KO orijinal viewport)
            CreateCanvas();

            // Programatik karakter seçim UI oluştur
            LoadCharacterSelectUI();

            // Başlangıçta üst üste binmeleri önlemek için görsel konumları sıfırla
            UpdateSlotListVisuals();

            // 3D sahne oluştur (kamera, arka plan, ışıklar)
            Setup3DScene();

            // Server events
            KOPacketHandler.OnAllCharInfo += HandleAllCharInfo;
            KOPacketHandler.OnNewCharResult += HandleCreateCharacterResponse;
            KOPacketHandler.OnDeleteCharResult += HandleDeleteCharacterResponse;
            KOPacketHandler.OnSelectCharResult += HandleSelectCharResult;

            // cpp:252 — Init → MsgSend_RequestAllCharacterInfo()
            RequestCharacterList();
        }

        private void OnEnable()
        {
            _isTransitioning = false;

            // Reset UI elements to hidden/off-screen state for fade-in transition
            if (_uiCharacterSelect != null)
            {
                var cg = _uiCharacterSelect.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
                var rt = _uiCharacterSelect.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(200f, -50f); // Start off-screen right
                _uiCharacterSelect.SetActive(false);
            }
            if (_btnBack != null)
            {
                var cg = _btnBack.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
                var rt = _btnBack.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(-50f, -50f);
                _btnBack.gameObject.SetActive(false);
            }
            if (_btnStart != null)
            {
                var cg = _btnStart.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
                var rt = _btnStart.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(0f, 10f); // Start lower
                _btnStart.gameObject.SetActive(false);
            }

            // Ekran her aktif olduğunda listeyi yenile
            if (_receivedCharacterInfo)
                RequestCharacterList();
        }

        private void OnDestroy()
        {
            // 3D sahneyi temizle
            if (_scene3D != null)
                Destroy(_scene3D.gameObject);

            KOPacketHandler.OnAllCharInfo -= HandleAllCharInfo;
            KOPacketHandler.OnNewCharResult -= HandleCreateCharacterResponse;
            KOPacketHandler.OnDeleteCharResult -= HandleDeleteCharacterResponse;
            KOPacketHandler.OnSelectCharResult -= HandleSelectCharResult;
        }


        // ============================================
        // Update — Klavye Tuş Kontrolleri
        // ============================================
        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                CharacterSelectOrCreate();

            // Dikey listede yön tuşlarıyla yukarı-aşağı gezinebilme
            if (kb.upArrowKey.wasPressedThisFrame)
            {
                int prevIndex = _selectedSlotIndex - 1;
                if (prevIndex < 0) prevIndex = MAX_AVAILABLE_CHARACTER - 1;
                OnSlotClicked(prevIndex);
            }
            if (kb.downArrowKey.wasPressedThisFrame)
            {
                int nextIndex = _selectedSlotIndex + 1;
                if (nextIndex >= MAX_AVAILABLE_CHARACTER) nextIndex = 0;
                OnSlotClicked(nextIndex);
            }
        }

        // ============================================
        // Canvas oluşturma — KO viewport birebir 1024x768
        // ============================================
        private void CreateCanvas()
        {
            var canvasObj = new GameObject("CharSelectCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024, 768);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // ============================================
        // Programmatic UI panel reference
        // ============================================
        private void LoadCharacterSelectUI()
        {
            var fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // Back Button (Top-Left)
            GameObject backObj = new GameObject("BtnBack", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            backObj.transform.SetParent(_canvas.transform, false);
            var backRt = backObj.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0f, 1f);
            backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0f, 1f);
            backRt.sizeDelta = new Vector2(100f, 44f);
            backRt.anchoredPosition = new Vector2(-50f, -50f); // Start off-screen left

            var backImg = backObj.GetComponent<Image>();
            backImg.sprite = CreateHexagonSprite("back_btn_bg", 100, 44, 
                new Color(0.12f, 0.10f, 0.08f, 0.98f), 
                new Color(0.06f, 0.05f, 0.04f, 0.98f), 
                new Color(0.42f, 0.34f, 0.15f, 0.98f), 
                new Color(0.60f, 0.48f, 0.22f, 0.98f));
            backImg.color = Color.white;

            var backCg = backObj.GetComponent<CanvasGroup>();
            backCg.alpha = 0f;

            _btnBack = backObj.GetComponent<Button>();
            _btnBack.onClick.AddListener(OnBackClicked);
            _btnBack.gameObject.SetActive(false); // Hide initially

            GameObject backTxtObj = new GameObject("Text", typeof(RectTransform));
            backTxtObj.transform.SetParent(backObj.transform, false);
            var backTxtRt = backTxtObj.GetComponent<RectTransform>();
            backTxtRt.anchorMin = Vector2.zero;
            backTxtRt.anchorMax = Vector2.one;
            backTxtRt.offsetMin = Vector2.zero;
            backTxtRt.offsetMax = Vector2.zero;
            var backTxt = backTxtObj.AddComponent<TextMeshProUGUI>();
            backTxt.font = fontAsset;
            backTxt.fontSize = 12;
            backTxt.fontStyle = FontStyles.Bold;
            backTxt.alignment = TextAlignmentOptions.Center;
            backTxt.color = _colorTextGold;
            backTxt.text = "Back";

            // Main Selection Right Panel (Top-Right)
            GameObject rightPanel = new GameObject("RightSelectionPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            rightPanel.transform.SetParent(_canvas.transform, false);
            var rightPanelRt = rightPanel.GetComponent<RectTransform>();
            rightPanelRt.anchorMin = new Vector2(1f, 1f);
            rightPanelRt.anchorMax = new Vector2(1f, 1f);
            rightPanelRt.pivot = new Vector2(1f, 1f);
            rightPanelRt.sizeDelta = new Vector2(180f, 195f);
            rightPanelRt.anchoredPosition = new Vector2(200f, -50f); // Start off-screen right

            var rightPanelImg = rightPanel.GetComponent<Image>();
            rightPanelImg.sprite = CreatePanelBgSprite("char_select_panel_bg", 180, 195, _colorBg, _colorBgInner, _colorBorder, 2);
            rightPanelImg.color = Color.white;
            _uiCharacterSelect = rightPanel;

            // Panel Title
            GameObject titleObj = new GameObject("TitleText", typeof(RectTransform));
            titleObj.transform.SetParent(rightPanel.transform, false);
            var titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(160f, 25f);
            titleRt.anchoredPosition = new Vector2(0f, -7f);

            var titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
            titleTxt.font = fontAsset;
            titleTxt.fontSize = 12;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = _colorTextGold;
            titleTxt.text = "Character Select";

            // Slots Group (Dikey Sıralama)
            _slotButtons = new Button[MAX_AVAILABLE_CHARACTER];
            _slotLeftTexts = new TextMeshProUGUI[MAX_AVAILABLE_CHARACTER];
            _slotRightTexts = new TextMeshProUGUI[MAX_AVAILABLE_CHARACTER];
            
            float startY = -36f;
            float spacingY = -39f;
            for (int i = 0; i < MAX_AVAILABLE_CHARACTER; i++)
            {
                int index = i;
                GameObject slotObj = new GameObject($"SlotButton_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                slotObj.transform.SetParent(rightPanel.transform, false);
                var slotRt = slotObj.GetComponent<RectTransform>();
                slotRt.anchorMin = new Vector2(0.5f, 1f);
                slotRt.anchorMax = new Vector2(0.5f, 1f);
                slotRt.pivot = new Vector2(0.5f, 1f);
                slotRt.sizeDelta = new Vector2(160f, 34f);
                slotRt.anchoredPosition = new Vector2(0f, startY + i * spacingY);

                var slotImg = slotObj.GetComponent<Image>();
                slotImg.sprite = CreateRoundedRectSprite($"slot_bg_{i}", 160, 34, 8, _colorBtnDark, _colorBorder * 0.4f, 1);
                slotImg.color = Color.white;

                var slotBtn = slotObj.GetComponent<Button>();
                slotBtn.onClick.AddListener(() => OnSlotClicked(index));
                _slotButtons[i] = slotBtn;

                // Left text (Name & Level)
                GameObject leftTxtObj = new GameObject("LeftText", typeof(RectTransform));
                leftTxtObj.transform.SetParent(slotObj.transform, false);
                var leftTxtRt = leftTxtObj.GetComponent<RectTransform>();
                leftTxtRt.anchorMin = new Vector2(0f, 0.5f);
                leftTxtRt.anchorMax = new Vector2(0.55f, 0.5f);
                leftTxtRt.pivot = new Vector2(0f, 0.5f);
                leftTxtRt.sizeDelta = new Vector2(0f, 28f);
                leftTxtRt.anchoredPosition = new Vector2(8f, 0f);
                
                var leftTxt = leftTxtObj.AddComponent<TextMeshProUGUI>();
                leftTxt.font = fontAsset;
                leftTxt.fontSize = 10;
                leftTxt.alignment = TextAlignmentOptions.Left;
                leftTxt.color = Color.white;
                leftTxt.text = "";
                _slotLeftTexts[i] = leftTxt;

                // Right text (Class)
                GameObject rightTxtObj = new GameObject("RightText", typeof(RectTransform));
                rightTxtObj.transform.SetParent(slotObj.transform, false);
                var rightTxtRt = rightTxtObj.GetComponent<RectTransform>();
                rightTxtRt.anchorMin = new Vector2(0.55f, 0.5f);
                rightTxtRt.anchorMax = new Vector2(1f, 0.5f);
                rightTxtRt.pivot = new Vector2(1f, 0.5f);
                rightTxtRt.sizeDelta = new Vector2(0f, 28f);
                rightTxtRt.anchoredPosition = new Vector2(-8f, 0f);
                
                var rightTxt = rightTxtObj.AddComponent<TextMeshProUGUI>();
                rightTxt.font = fontAsset;
                rightTxt.fontSize = 9;
                rightTxt.alignment = TextAlignmentOptions.Right;
                rightTxt.color = _colorTextGold;
                rightTxt.text = "";
                _slotRightTexts[i] = rightTxt;
            }

            // Action Buttons Panel
            // Delete Button
            GameObject deleteObj = new GameObject("BtnDelete", typeof(RectTransform), typeof(Image), typeof(Button));
            deleteObj.transform.SetParent(rightPanel.transform, false);
            var deleteRt = deleteObj.GetComponent<RectTransform>();
            deleteRt.anchorMin = new Vector2(0.5f, 1f); // Set to top anchor to align with list!
            deleteRt.anchorMax = new Vector2(0.5f, 1f);
            deleteRt.pivot = new Vector2(0.5f, 1f);
            deleteRt.sizeDelta = new Vector2(160f, 28f);
            deleteRt.anchoredPosition = new Vector2(0f, 0f); // dynamic positioning

            var deleteImg = deleteObj.GetComponent<Image>();
            deleteImg.sprite = CreateRoundedRectSprite("delete_btn_bg", 160, 28, 14, _colorBtnDark, new Color(0.6f, 0.1f, 0.1f, 0.8f), 1);
            deleteImg.color = Color.white;

            _btnDelete = deleteObj.GetComponent<Button>();
            _btnDelete.onClick.AddListener(OnDeleteClicked);

            GameObject deleteTxtObj = new GameObject("Text", typeof(RectTransform));
            deleteTxtObj.transform.SetParent(deleteObj.transform, false);
            var deleteTxtRt = deleteTxtObj.GetComponent<RectTransform>();
            deleteTxtRt.anchorMin = Vector2.zero;
            deleteTxtRt.anchorMax = Vector2.one;
            deleteTxtRt.offsetMin = Vector2.zero;
            deleteTxtRt.offsetMax = Vector2.zero;
            var deleteTxt = deleteTxtObj.AddComponent<TextMeshProUGUI>();
            deleteTxt.font = fontAsset;
            deleteTxt.fontSize = 9;
            deleteTxt.fontStyle = FontStyles.Bold;
            deleteTxt.alignment = TextAlignmentOptions.Center;
            deleteTxt.color = new Color(0.9f, 0.3f, 0.3f, 1f); // Soft kırmızı
            deleteTxt.text = "Delete My Character";

            // Create Button (Create New) - Will be positioned dynamically below slots list
            GameObject createObj = new GameObject("BtnCreate", typeof(RectTransform), typeof(Image), typeof(Button));
            createObj.transform.SetParent(rightPanel.transform, false);
            var createRt = createObj.GetComponent<RectTransform>();
            createRt.anchorMin = new Vector2(0.5f, 1f); // Set to top anchor to align with slots list!
            createRt.anchorMax = new Vector2(0.5f, 1f);
            createRt.pivot = new Vector2(0.5f, 1f);
            createRt.sizeDelta = new Vector2(160f, 28f);
            createRt.anchoredPosition = new Vector2(0f, 0f); // dynamic positioning

            var createImg = createObj.GetComponent<Image>();
            createImg.sprite = CreateRoundedRectSprite("create_btn_bg", 160, 28, 8, _colorBtnDark, _colorBorder, 1);
            createImg.color = Color.white;

            _btnCreate = createObj.GetComponent<Button>();
            _btnCreate.onClick.AddListener(OnCreateNewClicked);

            GameObject createTxtObj = new GameObject("Text", typeof(RectTransform));
            createTxtObj.transform.SetParent(createObj.transform, false);
            var createTxtRt = createTxtObj.GetComponent<RectTransform>();
            createTxtRt.anchorMin = Vector2.zero;
            createTxtRt.anchorMax = Vector2.one;
            createTxtRt.offsetMin = Vector2.zero;
            createTxtRt.offsetMax = Vector2.zero;
            var createTxt = createTxtObj.AddComponent<TextMeshProUGUI>();
            createTxt.font = fontAsset;
            createTxt.fontSize = 10;
            createTxt.fontStyle = FontStyles.Bold;
            createTxt.alignment = TextAlignmentOptions.Center;
            createTxt.color = _colorTextGold;
            createTxt.text = "Create New";
            createObj.SetActive(false); // Hide by default until data is loaded

            // Start Game Button (Bottom-Center)
            GameObject startObj = new GameObject("BtnStart", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            startObj.transform.SetParent(_canvas.transform, false);
            var startRt = startObj.GetComponent<RectTransform>();
            startRt.anchorMin = new Vector2(0.5f, 0f);
            startRt.anchorMax = new Vector2(0.5f, 0f);
            startRt.pivot = new Vector2(0.5f, 0.5f);
            startRt.sizeDelta = new Vector2(220f, 48f);
            startRt.anchoredPosition = new Vector2(0f, 10f); // Start lower for slide-in

            var startImg = startObj.GetComponent<Image>();
            startImg.sprite = CreateHexagonSprite("start_btn_bg", 220, 48, 
                new Color(0.96f, 0.80f, 0.22f, 0.98f), 
                new Color(0.68f, 0.48f, 0.08f, 0.98f), 
                new Color(0.85f, 0.65f, 0.15f, 0.98f), 
                new Color(1.00f, 0.95f, 0.72f, 0.98f));
            startImg.color = Color.white;

            var startCg = startObj.GetComponent<CanvasGroup>();
            startCg.alpha = 0f;

            _btnStart = startObj.GetComponent<Button>();
            _btnStart.onClick.AddListener(CharacterSelectOrCreate);
            _btnStart.gameObject.SetActive(false); // Hide initially

            GameObject startTxtObj = new GameObject("Text", typeof(RectTransform));
            startTxtObj.transform.SetParent(startObj.transform, false);
            var startTxtRt = startTxtObj.GetComponent<RectTransform>();
            startTxtRt.anchorMin = Vector2.zero;
            startTxtRt.anchorMax = Vector2.one;
            startTxtRt.offsetMin = Vector2.zero;
            startTxtRt.offsetMax = Vector2.zero;
            var startTxt = startTxtObj.AddComponent<TextMeshProUGUI>();
            startTxt.font = fontAsset;
            startTxt.fontSize = 18;
            startTxt.fontStyle = FontStyles.Bold;
            startTxt.alignment = TextAlignmentOptions.Center;
            startTxt.color = Color.black;
            startTxt.text = "Start";
        }

        private void OnSlotClicked(int index)
        {
            if (!_receivedCharacterInfo) return;
            
            _selectedSlotIndex = index;
            UpdateDisplayInfo();
            UpdateSlotListVisuals();

            // Koltukta seçilen karakteri anında spawn ediyoruz (Slot 0, merkez)
            if (_scene3D != null)
            {
                if (_slotOccupied[_selectedSlotIndex] && _characters != null && _selectedSlotIndex < _characters.Length)
                {
                    _scene3D.AddChr(0, _characters[_selectedSlotIndex]);
                }
                else
                {
                    _scene3D.RemoveChr(0);
                }
            }
        }

        private void UpdateSlotListVisuals()
        {
            if (_slotButtons == null || _slotLeftTexts == null || _slotRightTexts == null) return;

            float startY = -36f;
            float spacingY = -39f;
            int currentIndex = 0;

            for (int i = 0; i < MAX_AVAILABLE_CHARACTER; i++)
            {
                if (_slotOccupied[i] && _characters != null && i < _characters.Length)
                {
                    // Slot is occupied - show and position it
                    _slotButtons[i].gameObject.SetActive(true);
                    
                    var slotRt = _slotButtons[i].GetComponent<RectTransform>();
                    slotRt.anchoredPosition = new Vector2(0f, startY + currentIndex * spacingY);

                    var slotImg = _slotButtons[i].GetComponent<Image>();
                    if (i == _selectedSlotIndex)
                    {
                        slotImg.sprite = CreateRoundedRectSprite($"slot_bg_active_{i}", 160, 34, 8, new Color(0.2f, 0.16f, 0.12f, 0.95f), _colorBorder, 2);
                    }
                    else
                    {
                        slotImg.sprite = CreateRoundedRectSprite($"slot_bg_inactive_{i}", 160, 34, 8, _colorBtnDark, _colorBorder * 0.4f, 1);
                    }

                    var ch = _characters[i];
                    string className = GetClassName(ch.Class);
                    
                    _slotLeftTexts[i].text = $"{ch.Name}\n<size=9><color=#A0A0A0>Lv.{ch.Level}</color></size>";
                    _slotLeftTexts[i].color = Color.white;

                    _slotRightTexts[i].text = className;
                    _slotRightTexts[i].color = _colorTextGold;

                    currentIndex++;
                }
                else
                {
                    // Slot is empty - hide it
                    _slotButtons[i].gameObject.SetActive(false);
                }
            }

            // Position and show/hide the "Create New" button and "Delete" button
            float deleteY = 0f;
            if (_btnCreate != null)
            {
                if (currentIndex < MAX_AVAILABLE_CHARACTER)
                {
                    _btnCreate.gameObject.SetActive(true);
                    var createRt = _btnCreate.GetComponent<RectTransform>();
                    createRt.anchoredPosition = new Vector2(0f, startY + currentIndex * spacingY);
                    
                    // Delete button is placed right below Create button
                    deleteY = startY + currentIndex * spacingY - 34f;
                }
                else
                {
                    _btnCreate.gameObject.SetActive(false);
                    
                    // Delete button is placed right below Slot 2
                    deleteY = startY + (MAX_AVAILABLE_CHARACTER - 1) * spacingY - 40f;
                }
            }

            // Start & Delete buttons visibility
            // Ensure selected index is occupied, otherwise fall back to first occupied slot
            if (!_slotOccupied[_selectedSlotIndex])
            {
                for (int i = 0; i < MAX_AVAILABLE_CHARACTER; i++)
                {
                    if (_slotOccupied[i])
                    {
                        _selectedSlotIndex = i;
                        break;
                    }
                }
            }

            bool hasChar = _slotOccupied[_selectedSlotIndex];
            if (!_isTransitioning)
            {
                _btnDelete.gameObject.SetActive(hasChar);
                _btnStart.gameObject.SetActive(hasChar);
            }

            if (_btnDelete != null && hasChar)
            {
                var deleteRt = _btnDelete.GetComponent<RectTransform>();
                deleteRt.anchoredPosition = new Vector2(0f, deleteY);
            }

            // Adjust panel height dynamically based on the bottom-most active element
            float lowestBottomY = 0f;
            if (hasChar)
            {
                lowestBottomY = deleteY - 28f;
            }
            else if (_btnCreate != null && _btnCreate.gameObject.activeSelf)
            {
                lowestBottomY = startY + currentIndex * spacingY - 28f;
            }
            else
            {
                lowestBottomY = startY + (currentIndex - 1) * spacingY - 34f;
            }

            float dynamicHeight = Mathf.Abs(lowestBottomY) + 12f;
            if (_uiCharacterSelect != null)
            {
                var rightPanelRt = _uiCharacterSelect.GetComponent<RectTransform>();
                rightPanelRt.sizeDelta = new Vector2(180f, dynamicHeight);

                var rightPanelImg = _uiCharacterSelect.GetComponent<Image>();
                if (rightPanelImg != null)
                {
                    // Clean up procedurally generated old sprite texture to prevent memory leak
                    if (rightPanelImg.sprite != null && rightPanelImg.sprite.texture != null)
                    {
                        var oldSprite = rightPanelImg.sprite;
                        var oldTex = oldSprite.texture;
                        rightPanelImg.sprite = null;
                        Destroy(oldSprite);
                        Destroy(oldTex);
                    }
                    rightPanelImg.sprite = CreatePanelBgSprite($"char_select_panel_bg_{(int)dynamicHeight}", 180, (int)dynamicHeight, _colorBg, _colorBgInner, _colorBorder, 2);
                }
            }
        }

        private void OnCreateNewClicked()
        {
            int targetSlot = _selectedSlotIndex;
            if (_slotOccupied[targetSlot])
            {
                targetSlot = -1;
                for (int i = 0; i < MAX_AVAILABLE_CHARACTER; i++)
                {
                    if (!_slotOccupied[i])
                    {
                        targetSlot = i;
                        break;
                    }
                }
            }

            if (targetSlot != -1)
            {
                GoToCharacterCreate(targetSlot);
            }
            else
            {
                DisplayInfo("All character slots are full.");
            }
        }

        private static string GetZoneName(short zoneId)
        {
            return zoneId switch
            {
                21 => "Moradon",
                1 => "Luferson Castle",
                2 => "El Morad Castle",
                11 => "Karus Castle",
                12 => "El Morad Castle",
                _ => $"Zone {zoneId}"
            };
        }

        private void StretchToFillScreen(GameObject panel)
        {
            // Unused stub
        }

        private void BindUIElements()
        {
            // Unused stub
        }

        private Sprite CreatePanelBgSprite(string name, int w, int h, Color topColor, Color bottomColor, Color borderColor, int borderWidth)
        {
            int scale = 2;
            int sw = w * scale;
            int sh = h * scale;
            int sborderWidth = borderWidth * scale;

            Texture2D tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < sh; y++)
            {
                float t = (float)y / sh;
                Color fillColor = Color.Lerp(bottomColor, topColor, t);

                for (int x = 0; x < sw; x++)
                {
                    bool isBorder = false;
                    if (sborderWidth > 0)
                    {
                        if (x < sborderWidth || x >= sw - sborderWidth || y < sborderWidth || y >= sh - sborderWidth)
                            isBorder = true;
                    }
                    tex.SetPixel(x, y, isBorder ? borderColor : fillColor);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sw, sh), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateHexagonSprite(string name, int w, int h, Color centerColor, Color edgeColor, Color borderColor, Color innerGlowColor)
        {
            int scale = 4; // 4x Supersampling for extremely smooth vector lines (no pixelation)
            int sw = w * scale;
            int sh = h * scale;

            Texture2D tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cy = sh / 2f;
            float indent = sh * 0.4f; // Sivri ok ucunun derinliği
            float cosTheta = 0.78f;

            float resScale = sh / 36f;
            float borderOuter = 1.5f * resScale;
            float borderInner = 3.0f * resScale;
            float shadowGap = 4.0f * resScale;

            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    // Altıgen üyelik testi
                    float leftBound = Mathf.Abs(y - cy) * (indent / cy);
                    float rightBound = sw - leftBound;

                    // Sınırlara olan en yakın mesafeyi bul
                    float distToLeft = (x - leftBound) * cosTheta;
                    float distToRight = (rightBound - x) * cosTheta;
                    float distToTop = (sh - 1) - y;
                    float distToBottom = y;
                    float minDist = Mathf.Min(Mathf.Min(distToLeft, distToRight), Mathf.Min(distToTop, distToBottom));

                    // Kenarlarda Anti-Aliasing (Subpixel Yumuşatma) hesabı
                    if (minDist < 0f)
                    {
                        float edgeFade = Mathf.Clamp01(1f + minDist);
                        if (edgeFade > 0.01f)
                        {
                            Color edgeC = borderColor;
                            edgeC.a *= edgeFade;
                            tex.SetPixel(x, y, edgeC);
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                    }
                    else if (minDist < borderOuter)
                    {
                        tex.SetPixel(x, y, borderColor);
                    }
                    else if (minDist < borderInner)
                    {
                        tex.SetPixel(x, y, innerGlowColor);
                    }
                    else if (minDist < shadowGap)
                    {
                        // Koyu gölge oluğu (merkez rengine yakın ama çok daha koyu)
                        tex.SetPixel(x, y, Color.Lerp(edgeColor, Color.black, 0.6f));
                    }
                    else
                    {
                        float fade = Mathf.Abs(y - cy) / cy;
                        Color fillColor = Color.Lerp(centerColor, edgeColor, fade);

                        // Çok hafif cam yansıması parlaması
                        float hx = sw * 0.35f;
                        float hy = sh * 0.65f;
                        float hdx = (x - hx) * ((float)sh / sw);
                        float hdy = y - hy;
                        float distToHighlight = Mathf.Sqrt(hdx * hdx + hdy * hdy);
                        float highlightRadius = sh * 0.35f;

                        if (distToHighlight < highlightRadius)
                        {
                            float factor = 1f - (distToHighlight / highlightRadius);
                            factor = factor * factor * (3f - 2f * factor);
                            float gloss = factor * 0.06f;
                            fillColor.r = Mathf.Clamp01(fillColor.r + gloss);
                            fillColor.g = Mathf.Clamp01(fillColor.g + gloss * 0.8f);
                            fillColor.b = Mathf.Clamp01(fillColor.b + gloss * 0.5f);
                            fillColor.a = Mathf.Clamp01(fillColor.a + gloss * 0.5f);
                        }

                        tex.SetPixel(x, y, fillColor);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, sw, sh), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateRoundedRectSprite(string name, int w, int h, int radius, Color fillColor, Color borderColor, int borderWidth)
        {
            int scale = 2;
            int sw = w * scale;
            int sh = h * scale;
            int sradius = radius * scale;
            int sborderWidth = borderWidth * scale;

            Texture2D tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    bool isCorner = false;
                    float dx = 0, dy = 0;

                    if (sradius > 0)
                    {
                        if (x < sradius && y < sradius) { dx = sradius - x; dy = sradius - y; isCorner = true; }
                        else if (x >= sw - sradius && y < sradius) { dx = sradius - (sw - x); dy = sradius - y; isCorner = true; }
                        else if (x < sradius && y >= sh - sradius) { dx = sradius - x; dy = sradius - (sh - y); isCorner = true; }
                        else if (x >= sw - sradius && y >= sh - sradius) { dx = sradius - (sw - x); dy = sradius - (sh - y); isCorner = true; }
                    }

                    if (isCorner)
                    {
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float outerEdgeTransition = 1.0f;
                        float delta = dist - sradius;

                        if (delta >= outerEdgeTransition)
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                        else
                        {
                            bool isBorder = (sborderWidth > 0 && dist >= (sradius - sborderWidth));
                            Color baseColor = isBorder ? borderColor : fillColor;
                            if (delta > -outerEdgeTransition)
                            {
                                float alphaPct = 1f - ((delta + outerEdgeTransition) / (2f * outerEdgeTransition));
                                baseColor.a *= Mathf.Clamp01(alphaPct);
                            }
                            tex.SetPixel(x, y, baseColor);
                        }
                    }
                    else
                    {
                        bool isBorder = false;
                        if (sborderWidth > 0)
                        {
                            if (x < sborderWidth || x >= sw - sborderWidth || y < sborderWidth || y >= sh - sborderWidth)
                                	isBorder = true;
                        }
                        tex.SetPixel(x, y, isBorder ? borderColor : fillColor);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sw, sh), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateFadingDividerSprite(string name, int w, int h, Color color)
        {
            int sw = w;
            int sh = h;
            Texture2D tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    float t = (float)x / sw;
                    float alphaPct = 1f - Mathf.Pow(2f * t - 1f, 2f);
                    Color pixelColor = color;
                    pixelColor.a *= alphaPct;
                    tex.SetPixel(x, y, pixelColor);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sw, sh), new Vector2(0.5f, 0.5f));
        }

        // ============================================
        // Slot navigasyonu — cpp:912-970 (Devre Dışı)
        // ============================================
        private void DoJobLeft() {}
        private void DojobRight() {}
        private void OnOrbitComplete() {}

        // ============================================
        // Paket gönderme/alma
        // ============================================

        /// <summary>
        /// Open-KO birebir: MsgSend_RequestAllCharacterInfo() — cpp:1462-1468
        /// WIZ_ALLCHAR_INFO_REQ gönderir
        /// </summary>
        private void RequestCharacterList()
        {
            KONetworkManager.Instance?.SendAllCharInfoReq();
            DisplayInfo("Loading characters...");
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_AllCharacterInfo() — cpp:1407-1460
        /// </summary>
        private void HandleAllCharInfo(byte[] rawData)
        {
            if (!gameObject.activeInHierarchy) return;

            _receivedCharacterInfo = true;
            _isTransitioning = true;

            // Tüm slot'ları temizle
            for (int i = 0; i < MAX_AVAILABLE_CHARACTER; i++)
                _slotOccupied[i] = false;

            if (_scene3D != null)
                _scene3D.ClearAll();

            var reader = new KOPacketReader(rawData);
            byte iResult = reader.ReadByte(); // 0x01 = success
            
            _characters = new CharacterListItem[MAX_AVAILABLE_CHARACTER];
            for (int i = 0; i < MAX_AVAILABLE_CHARACTER; i++)
                _characters[i] = new CharacterListItem();

            if (iResult != 0x01)
            {
                Debug.LogWarning("[CHARSEL] AllCharInfo failed — retrying");
                RequestCharacterList();
                return;
            }

            for (int i = 0; i < MAX_AVAILABLE_CHARACTER; i++)
            {
                string charId = reader.ReadKOString();
                byte race = reader.ReadByte();
                short classId = reader.ReadInt16();
                byte level = reader.ReadByte();
                byte face = reader.ReadByte();
                byte hair = reader.ReadByte();
                byte zone = reader.ReadByte();

                int itemHelmet = (int)reader.ReadUInt32();
                short itemHelmetDur = reader.ReadInt16();
                int itemUpper = (int)reader.ReadUInt32();
                short itemUpperDur = reader.ReadInt16();
                int itemCloak = (int)reader.ReadUInt32();
                short itemCloakDur = reader.ReadInt16();
                int itemRightHand = (int)reader.ReadUInt32();
                short itemRightHandDur = reader.ReadInt16();
                int itemLeftHand = (int)reader.ReadUInt32();
                short itemLeftHandDur = reader.ReadInt16();
                int itemLower = (int)reader.ReadUInt32();
                short itemLowerDur = reader.ReadInt16();
                int itemGloves = (int)reader.ReadUInt32();
                short itemGlovesDur = reader.ReadInt16();
                int itemShoes = (int)reader.ReadUInt32();
                short itemShoesDur = reader.ReadInt16();

                if (string.IsNullOrEmpty(charId))
                    continue;

                _characters[i].Name = charId;
                _characters[i].Race = race;
                _characters[i].Class = (byte)classId;
                _characters[i].Level = level;
                _characters[i].Face = face;
                _characters[i].Hair = hair;
                _characters[i].ZoneId = zone;

                _slotOccupied[i] = true;

                _characters[i].ItemHelmet             = itemHelmet;
                _characters[i].ItemHelmetDurability    = itemHelmetDur;
                _characters[i].ItemUpper              = itemUpper;
                _characters[i].ItemUpperDurability     = itemUpperDur;
                _characters[i].ItemCloak              = itemCloak;
                _characters[i].ItemCloakDurability     = itemCloakDur;
                _characters[i].ItemRightHand          = itemRightHand;
                _characters[i].ItemRightHandDurability = itemRightHandDur;
                _characters[i].ItemLeftHand           = itemLeftHand;
                _characters[i].ItemLeftHandDurability  = itemLeftHandDur;
                _characters[i].ItemLower              = itemLower;
                _characters[i].ItemLowerDurability     = itemLowerDur;
                _characters[i].ItemGloves             = itemGloves;
                _characters[i].ItemGlovesDurability    = itemGlovesDur;
                _characters[i].ItemShoes              = itemShoes;
                _characters[i].ItemShoesDurability     = itemShoesDur;
            }

            _selectedSlotIndex = 0;
            if (_scene3D != null)
                _scene3D.ResetOrbit();

            UpdateDisplayInfo();
            UpdateSlotListVisuals();

            // Koltukta varsayılan (Slot 0) karakteri anında göster
            if (_scene3D != null)
            {
                if (_slotOccupied[0])
                    _scene3D.AddChr(0, _characters[0]);
                else
                    _scene3D.RemoveChr(0);
            }

            StartCoroutine(TransitionFromLoginCoroutine());
        }

        /// <summary>
        /// Bilgi alanını ve buton durumlarını günceller
        /// </summary>
        private void UpdateDisplayInfo()
        {
        }

        private void DisplayInfo(string text)
        {
            Debug.Log("[CHARSEL] " + text);
        }

        // ============================================
        // CharacterSelectOrCreate — cpp:1503-1519
        // ============================================
        private void CharacterSelectOrCreate()
        {
            if (!_receivedCharacterInfo) return;

            int iIndex = _selectedSlotIndex;
            if (!_slotOccupied[iIndex])
            {
                GoToCharacterCreate(iIndex);
            }
            else
            {
                var ch = _characters[iIndex];
                MsgSend_CharacterSelect(ch);
            }
        }

        private void MsgSend_CharacterSelect(CharacterListItem ch)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.CharacterName = ch.Name;
            gm.CharClass = ch.Class;
            gm.CurrentZoneId = (short)ch.ZoneId;

            DisplayInfo($"Entering game as {ch.Name}...");
            KONetworkManager.Instance?.SendSelectCharacter(ch.Name, (byte)ch.ZoneId);
        }

        private void GoToCharacterCreate(int slotIndex)
        {
            CharacterCreateUI.PendingCharIndex = (byte)slotIndex;
            gameObject.SetActive(false);
            
            var createUI = FindAnyObjectByType<CharacterCreateUI>(FindObjectsInactive.Include);
            if (createUI == null)
            {
                var createObj = new GameObject("CharacterCreateUI");
                createUI = createObj.AddComponent<CharacterCreateUI>();
            }
            else
            {
                createUI.gameObject.SetActive(true);
            }
        }

        private void OnDeleteClicked()
        {
            if (!_receivedCharacterInfo) return;
            if (!_slotOccupied[_selectedSlotIndex]) return;

            var ch = _characters[_selectedSlotIndex];
            string szMsg = $"{ch.Name} karakterini silmek istediğinize emin misiniz?";

            if (KOMessageBox.Instance != null)
            {
                KOMessageBox.Instance.ShowYesNo(
                    szMsg, "",
                    MsgBoxBehavior.BEHAVIOR_DELETE_CHR,
                    onYes: () =>
                    {
                        KONetworkManager.Instance?.SendDeleteChar((byte)_selectedSlotIndex, ch.Name);
                    },
                    onNo: () => {}
                );
            }
            else
            {
                ShowConfirmDialog(
                    $"Are you sure you want to delete '{ch.Name}'?",
                    onYes: () =>
                    {
                        KONetworkManager.Instance?.SendDeleteChar((byte)_selectedSlotIndex, ch.Name);
                    }
                );
            }
        }

        private void OnBtnExitClick()
        {
            string szMsg = "Oyundan çıkmak istediğinize emin misiniz?";

            if (KOMessageBox.Instance != null)
            {
                KOMessageBox.Instance.ShowYesNo(
                    szMsg, "",
                    MsgBoxBehavior.BEHAVIOR_EXIT,
                    onYes: () =>
                    {
                        Application.Quit();
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#endif
                    },
                    onNo: () => {}
                );
            }
            else
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }

        private void OnBackClicked()
        {
            gameObject.SetActive(false);
            var loginUI = FindAnyObjectByType<LoginUI>(FindObjectsInactive.Include);
            if (loginUI != null)
            {
                loginUI.HideConnectingDialog();
                loginUI.OpenServerList();
            }
        }

        private System.Collections.IEnumerator TransitionFromLoginCoroutine()
        {
            var loginUI = FindAnyObjectByType<LoginUI>(FindObjectsInactive.Include);
            bool hasLoginTransition = (loginUI != null && loginUI.gameObject.activeSelf);

            if (hasLoginTransition)
            {
                // Start the smooth fade out of the connecting dialog panel
                loginUI.StartFadeOutConnectingDialog();
            }

            // Now slide-in and fade-in CharacterSelectUI elements
            float elapsed = 0f;
            float fadeInDuration = 0.4f;

            var rightPanelRt = _uiCharacterSelect != null ? _uiCharacterSelect.GetComponent<RectTransform>() : null;
            Vector2 rightStart = new Vector2(200f, -50f);
            Vector2 rightEnd = new Vector2(-50f, -50f);

            var backRt = _btnBack != null ? _btnBack.GetComponent<RectTransform>() : null;
            Vector2 backStart = new Vector2(-50f, -50f);
            Vector2 backEnd = new Vector2(50f, -50f);

            var startRt = _btnStart != null ? _btnStart.GetComponent<RectTransform>() : null;
            Vector2 startStart = new Vector2(0f, 10f);
            Vector2 startEnd = new Vector2(0f, 60f);

            var rightPanelCanvasGroup = _uiCharacterSelect != null ? _uiCharacterSelect.GetComponent<CanvasGroup>() : null;
            var backBtnCanvasGroup = _btnBack != null ? _btnBack.GetComponent<CanvasGroup>() : null;
            var startBtnCanvasGroup = _btnStart != null ? _btnStart.GetComponent<CanvasGroup>() : null;

            // Explicitly force starting alphas and positions before activating to prevent any frame flashes/blinks!
            if (rightPanelCanvasGroup != null) rightPanelCanvasGroup.alpha = 0f;
            if (backBtnCanvasGroup != null) backBtnCanvasGroup.alpha = 0f;
            if (startBtnCanvasGroup != null) startBtnCanvasGroup.alpha = 0f;

            if (rightPanelRt != null) rightPanelRt.anchoredPosition = rightStart;
            if (backRt != null) backRt.anchoredPosition = backStart;
            if (startRt != null) startRt.anchoredPosition = startStart;

            // Make sure they are visible/active!
            if (_uiCharacterSelect != null) _uiCharacterSelect.SetActive(true);
            if (_btnBack != null) _btnBack.gameObject.SetActive(true);
            
            bool hasChar = _slotOccupied[_selectedSlotIndex];
            if (_btnStart != null) _btnStart.gameObject.SetActive(hasChar);
            if (_btnDelete != null) _btnDelete.gameObject.SetActive(hasChar);

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDuration);
                float easeT = 1f - Mathf.Pow(1f - t, 3);

                if (rightPanelCanvasGroup != null) rightPanelCanvasGroup.alpha = t;
                if (backBtnCanvasGroup != null) backBtnCanvasGroup.alpha = t;
                if (startBtnCanvasGroup != null && hasChar) startBtnCanvasGroup.alpha = t;

                if (rightPanelRt != null) rightPanelRt.anchoredPosition = Vector2.Lerp(rightStart, rightEnd, easeT);
                if (backRt != null) backRt.anchoredPosition = Vector2.Lerp(backStart, backEnd, easeT);
                if (startRt != null && hasChar) startRt.anchoredPosition = Vector2.Lerp(startStart, startEnd, easeT);

                yield return null;
            }

            if (rightPanelCanvasGroup != null) rightPanelCanvasGroup.alpha = 1f;
            if (backBtnCanvasGroup != null) backBtnCanvasGroup.alpha = 1f;
            if (startBtnCanvasGroup != null) startBtnCanvasGroup.alpha = 1f;

            if (rightPanelRt != null) rightPanelRt.anchoredPosition = rightEnd;
            if (backRt != null) backRt.anchoredPosition = backEnd;
            if (startRt != null) startRt.anchoredPosition = startEnd;

            _isTransitioning = false;
        }

        private void HandleCreateCharacterResponse(byte result)
        {
            if (result == 0x00)
                RequestCharacterList();
        }

        private void HandleDeleteCharacterResponse(byte result, byte charIndex)
        {
            if (result == 0x01)
            {
                if (_scene3D != null)
                    _scene3D.RemoveChr(0);

                DisplayInfo("Character deleted.");
                RequestCharacterList();
            }
            else
            {
                DisplayInfo("Failed to delete character.");
            }
        }

        private void HandleSelectCharResult(bool success)
        {
            if (!success)
            {
                DisplayInfo("Character selection failed.");
                Debug.LogError("[CHARSEL] WIZ_SEL_CHAR failed!");
                return;
            }

            if (GameManager.Instance != null)
                GameManager.Instance.SetState(GameState.Loading);

            KONetworkManager.Instance?.SendGameStart();

            byte nation = GameManager.Instance?.Nation ?? 0;
            if (KOLoadingScreen.Instance == null)
            {
                var loadingObj = new GameObject("KOLoadingScreen");
                loadingObj.AddComponent<KOLoadingScreen>();
            }
            KOLoadingScreen.Instance.LoadSceneWithProgress("GameScene", nation);
        }

        private void Setup3DScene()
        {
            var sceneObj = new GameObject("ChrSelectScene3D");
            _scene3D = sceneObj.AddComponent<CharSelectScene3D>();

            var gm = GameManager.Instance;
            byte nation = (gm != null) ? gm.Nation : (byte)2;
            _scene3D.Initialize(nation);
        }

        private static string GetClassName(byte classId) => EntropyOnline.Core.KOTextHelper.GetTextByClass(classId);

        // ============================================
        // Onay Diyaloğu (KOMessageBox yokken fallback)
        // ============================================
        private void ShowConfirmDialog(string message, System.Action onYes)
        {
            if (_confirmPanel != null)
            {
                Destroy(_confirmPanel);
                _confirmPanel = null;
            }
            
            var fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            _confirmPanel = new GameObject("ConfirmDeletePanel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _confirmPanel.transform.SetParent(_canvas.transform, false);

            _confirmPanel.AddComponent<KOUIScaleIndependent>();

            var panelCanvas = _confirmPanel.GetComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = 300;

            var panelRt = _confirmPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(360f, 160f);
            panelRt.anchoredPosition = Vector2.zero;

            var bgObj = new GameObject("Background", typeof(RectTransform));
            bgObj.transform.SetParent(_confirmPanel.transform, false);
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.sprite = CreatePanelBgSprite("confirm_panel_bg", 360, 160,
                new Color(0.12f, 0.10f, 0.08f, 0.96f),
                new Color(0.04f, 0.04f, 0.04f, 0.96f),
                _colorBorder,
                2);
            bgImg.color = Color.white;
            var bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var txtObj = new GameObject("MessageText", typeof(RectTransform));
            txtObj.transform.SetParent(_confirmPanel.transform, false);
            var txtComp = txtObj.AddComponent<TextMeshProUGUI>();
            txtComp.font = fontAsset;
            txtComp.text = message;
            txtComp.fontSize = 14;
            txtComp.alignment = TextAlignmentOptions.Center;
            txtComp.color = new Color(0.9f, 0.85f, 0.75f, 1f);
            txtComp.fontStyle = FontStyles.Bold;
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0f, 1f);
            txtRt.anchorMax = new Vector2(1f, 1f);
            txtRt.pivot = new Vector2(0.5f, 1f);
            txtRt.offsetMin = new Vector2(20f, -85f);
            txtRt.offsetMax = new Vector2(-20f, -16f);

            var shadow = txtObj.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1f, -1f);

            CreateConfirmButton("BtnYes", "Yes", new Vector2(-75f, 30f),
                new Color(0.12f, 0.28f, 0.12f, 0.95f), new Color(0.25f, 0.55f, 0.25f, 0.95f),
                () =>
                {
                    onYes?.Invoke();
                    if (_confirmPanel != null) { Destroy(_confirmPanel); _confirmPanel = null; }
                }, fontAsset);

            CreateConfirmButton("BtnNo", "No", new Vector2(75f, 30f),
                new Color(0.45f, 0.05f, 0.08f, 0.95f), new Color(0.75f, 0.15f, 0.15f, 0.95f),
                () =>
                {
                    if (_confirmPanel != null) { Destroy(_confirmPanel); _confirmPanel = null; }
                }, fontAsset);
        }

        private void CreateConfirmButton(string name, string label, Vector2 pos,
            Color bgColor, Color borderColor, System.Action onClick, TMP_FontAsset fontAsset)
        {
            var btnObj = new GameObject(name, typeof(RectTransform));
            btnObj.transform.SetParent(_confirmPanel.transform, false);

            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0f);
            btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta = new Vector2(104f, 26f);
            btnRt.anchoredPosition = pos;

            var img = btnObj.AddComponent<Image>();
            img.sprite = CreateRoundedRectSprite(name + "_bg", 104, 26, 4, bgColor, borderColor, 1);
            img.color = Color.white;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtComp = txtObj.AddComponent<TextMeshProUGUI>();
            txtComp.font = fontAsset;
            txtComp.text = label;
            txtComp.fontSize = 13;
            txtComp.fontStyle = FontStyles.Bold;
            txtComp.alignment = TextAlignmentOptions.Center;
            txtComp.color = Color.white;
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
        }
    }
}
