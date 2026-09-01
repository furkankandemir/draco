using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EntropyOnline.Import;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;

namespace EntropyOnline.UI
{
    public class KOAuctionHouseUI : MonoBehaviour
    {
        public static KOAuctionHouseUI Instance { get; private set; }

        public bool IsVisible => _panelRoot != null && _panelRoot.activeSelf;

        [Header("UI Containers")]
        private GameObject _panelRoot;
        private Transform _browseScrollContent;

        [Header("Left Panel Elements")]
        private TMP_InputField _searchField;
        private TMP_Dropdown _armorDropdown;
        private TMP_Dropdown _weaponDropdown;

        [Header("Right Top Elements")]
        private TMP_Text _ahCoinsText;
        private Button _btnRefresh;
        private Button _btnClose;

        [Header("Bottom Panel Elements")]
        private Button _btnRegisterItem;
        private Button _btnHistory;
        private Button _btnMyMerchant;
        private Button _btnPrevPage;
        private Button _btnNextPage;
        private TMP_Text _pageText;

        [Header("Inventory Modal Popup")]
        private GameObject _inventoryModal;
        private Transform _invGridContent;
        private Transform _regActiveListingsScrollContent;

        [Header("My Merchant Modal Popup")]
        private GameObject _myMerchantModal;
        private Transform _myMerchantScrollContent;



        private List<AuctionListingData> _listings = new List<AuctionListingData>();
        private ulong _pendingGold = 0;
        private int _currentPage = 1;
        private int _totalPages = 1;

        // Filter states
        private string _filterName = "";
        private int _filterArmorType = 0; // 0: All, 1: Pauldron, 2: Helmet, 3: Pad, 4: Gauntlet, 5: Boot
        private int _filterWeaponType = 0; // 0: All, 1: Dagger, 2: Sword, 3: Bow, 4: Staff, 5: Shield, 6: Club/Axe/Spear
        private bool _filterMyItemsOnly = false;

        // Premium Color Scheme aligned with Merchant Control & Skill Theme
        private Color _colorBgTop = new Color(0.12f, 0.10f, 0.08f, 0.98f);     // Warm dark charcoal
        private Color _colorBgBottom = new Color(0.04f, 0.04f, 0.04f, 0.98f);  // Opaque black charcoal
        private Color _colorBorder = new Color(0.6f, 0.48f, 0.22f, 0.9f);      // Premium gold-bronze border
        private Color _colorTextGold = new Color(0.9f, 0.8f, 0.6f, 1f);        // Golden text
        private Color _colorTitleGold = new Color(0.95f, 0.85f, 0.35f, 1f);     // Gold title text
        private Color _colorBtnBg = new Color(0.06f, 0.05f, 0.04f, 0.95f);      // Row/Button dark body bg
        private Color _colorInputBg = new Color(0.05f, 0.04f, 0.04f, 1f);      // Dark input field bg
        private Color _colorGreenBtn = new Color(0.12f, 0.28f, 0.12f, 1f);     // Dark green button
        private Color _colorGreenBorder = new Color(0.25f, 0.55f, 0.25f, 1f);  // Bright green border
        private Color _colorRedBtn = new Color(0.25f, 0.08f, 0.08f, 1f);       // Dark red button
        private Color _colorRedBorder = new Color(0.75f, 0.15f, 0.15f, 1f);     // Bright red border

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            KOPacketHandler.OnMarketBBS += HandleMarketBBSPacket;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnMarketBBS -= HandleMarketBBSPacket;
        }

        public void InitPanel(Transform parent)
        {
            if (_panelRoot != null) return;

            // 1. Create main fullscreen blocker panel
            _panelRoot = new GameObject("AH_PanelRoot", typeof(RectTransform));
            _panelRoot.transform.SetParent(parent, false);

            var mainRT = _panelRoot.GetComponent<RectTransform>();
            mainRT.anchorMin = Vector2.zero;
            mainRT.anchorMax = Vector2.one;
            mainRT.sizeDelta = Vector2.zero;

            // Add Canvas component to override sorting order and make it render on top of MobileSkillBar (which is 110)
            var canvas = _panelRoot.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 120;
            _panelRoot.AddComponent<GraphicRaycaster>();

            // Transparent background blocker
            var bgImg = _panelRoot.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.65f);

            // 2. Create the themed main window frame in the center
            var frameObj = new GameObject("AH_MainFrame", typeof(RectTransform));
            frameObj.transform.SetParent(_panelRoot.transform, false);
            var frameRT = frameObj.GetComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0.5f, 0.5f);
            frameRT.anchorMax = new Vector2(0.5f, 0.5f);
            frameRT.pivot = new Vector2(0.5f, 0.5f);
            frameRT.sizeDelta = new Vector2(1024, 600);

            var frameImg = frameObj.AddComponent<Image>();
            frameImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemePanelBgSprite("auction_house_panel_bg", 1024, 600, 0,
                    _colorBgTop, _colorBgBottom, _colorBorder, 2) : null;
            frameImg.color = Color.white;

            // Save reference to our styled container for all child components
            var uiContainer = frameObj.transform;

            // 2. Left Filter Panel
            CreateLeftPanel(uiContainer);

            // 3. Right Top Header & Coins Info
            CreateRightTopHeader(uiContainer);

            // 4. Main Listings Table (Headers & Scroll view)
            CreateMainTable(uiContainer);

            // 5. Bottom Panel (Buttons & Pagination)
            CreateBottomPanel(uiContainer);

            // 6. Create hidden Inventory Listing modal (needs to cover full screen, so parent to _panelRoot)
            CreateInventoryModal(_panelRoot.transform);
            CreateMyMerchantModal(_panelRoot.transform);

            _panelRoot.SetActive(false);
        }

        private void CreateLeftPanel(Transform parent)
        {
            var leftPanel = new GameObject("LeftPanel", typeof(RectTransform));
            leftPanel.transform.SetParent(parent, false);
            var rt = leftPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(220, 0);

            var img = leftPanel.AddComponent<Image>();
            img.color = _colorInputBg;

            // Border line on the right
            var border = new GameObject("Border", typeof(RectTransform));
            border.transform.SetParent(leftPanel.transform, false);
            var borderRT = border.GetComponent<RectTransform>();
            borderRT.anchorMin = new Vector2(1, 0);
            borderRT.anchorMax = new Vector2(1, 1);
            borderRT.sizeDelta = new Vector2(2, 0);
            border.AddComponent<Image>().color = new Color(_colorBorder.r, _colorBorder.g, _colorBorder.b, 0.6f);

            // Title / Logo
            var logoObj = new GameObject("Logo", typeof(RectTransform));
            logoObj.transform.SetParent(leftPanel.transform, false);
            var logoRT = logoObj.GetComponent<RectTransform>();
            logoRT.anchorMin = new Vector2(0.5f, 1);
            logoRT.anchorMax = new Vector2(0.5f, 1);
            logoRT.anchoredPosition = new Vector2(0, -45);
            logoRT.sizeDelta = new Vector2(180, 30);
            var logoTxt = logoObj.AddComponent<TextMeshProUGUI>();
            logoTxt.text = "DRACO";
            logoTxt.fontSize = 24;
            logoTxt.fontStyle = FontStyles.Bold | FontStyles.Italic;
            logoTxt.color = _colorTitleGold;
            logoTxt.alignment = TextAlignmentOptions.Center;

            var subtitleObj = new GameObject("SubLogo", typeof(RectTransform));
            subtitleObj.transform.SetParent(leftPanel.transform, false);
            var subRT = subtitleObj.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0.5f, 1);
            subRT.anchorMax = new Vector2(0.5f, 1);
            subRT.anchoredPosition = new Vector2(0, -68);
            subRT.sizeDelta = new Vector2(180, 15);
            var subTxt = subtitleObj.AddComponent<TextMeshProUGUI>();
            subTxt.text = "Online World";
            subTxt.fontSize = 10;
            subTxt.color = Color.gray;
            subTxt.alignment = TextAlignmentOptions.Center;

            // Character / Item Name Search Field (Y = -110, placeholder = "character")
            var searchObj = new GameObject("SearchField", typeof(RectTransform));
            searchObj.transform.SetParent(leftPanel.transform, false);
            var sRT = searchObj.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0.5f, 1);
            sRT.anchorMax = new Vector2(0.5f, 1);
            sRT.anchoredPosition = new Vector2(0, -110);
            sRT.sizeDelta = new Vector2(180, 32);
            searchObj.AddComponent<Image>().color = _colorInputBg;
            var borderInput = new GameObject("Border", typeof(RectTransform));
            borderInput.transform.SetParent(searchObj.transform, false);
            borderInput.GetComponent<RectTransform>().sizeDelta = new Vector2(182, 34);
            var borderInputImg = borderInput.AddComponent<Image>();
            borderInputImg.color = new Color(_colorBorder.r, _colorBorder.g, _colorBorder.b, 0.5f);
            borderInputImg.raycastTarget = false;
            borderInput.transform.SetAsFirstSibling();

            _searchField = searchObj.AddComponent<TMP_InputField>();
            var textArea = new GameObject("TextArea", typeof(RectTransform));
            textArea.transform.SetParent(searchObj.transform, false);
            var taRT = textArea.GetComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero;
            taRT.anchorMax = Vector2.one;
            taRT.sizeDelta = new Vector2(-10, 0);
            
            var placeholder = new GameObject("Placeholder", typeof(RectTransform));
            placeholder.transform.SetParent(textArea.transform, false);
            var pRT = placeholder.GetComponent<RectTransform>();
            pRT.anchorMin = Vector2.zero;
            pRT.anchorMax = Vector2.one;
            pRT.sizeDelta = Vector2.zero;
            var pText = placeholder.AddComponent<TextMeshProUGUI>();
            pText.text = "character";
            pText.fontSize = 11;
            pText.color = Color.gray;
            pText.fontStyle = FontStyles.Italic;
            pText.alignment = TextAlignmentOptions.Left;

            var textDisplay = new GameObject("TextDisplay", typeof(RectTransform));
            textDisplay.transform.SetParent(textArea.transform, false);
            var tdRT = textDisplay.GetComponent<RectTransform>();
            tdRT.anchorMin = Vector2.zero;
            tdRT.anchorMax = Vector2.one;
            tdRT.sizeDelta = Vector2.zero;
            var tText = textDisplay.AddComponent<TextMeshProUGUI>();
            tText.fontSize = 11;
            tText.color = Color.white;
            tText.alignment = TextAlignmentOptions.Left;

            _searchField.textViewport = textArea.GetComponent<RectTransform>();
            _searchField.textComponent = tText;
            _searchField.placeholder = pText;
            _searchField.onValueChanged.AddListener(s => OnFilterChanged());

            // Armor Type Dropdown (Y = -165)
            var armorObj = new GameObject("ArmorDropdown", typeof(RectTransform));
            armorObj.transform.SetParent(leftPanel.transform, false);
            var aRT = armorObj.GetComponent<RectTransform>();
            aRT.anchorMin = new Vector2(0.5f, 1);
            aRT.anchorMax = new Vector2(0.5f, 1);
            aRT.anchoredPosition = new Vector2(0, -165);
            aRT.sizeDelta = new Vector2(180, 32);
            _armorDropdown = armorObj.AddComponent<TMP_Dropdown>();
            SetupDropdown(_armorDropdown, new List<string> { "Armor Type", "Pauldron", "Helmet", "Pad", "Gauntlet", "Boot" });
            _armorDropdown.onValueChanged.AddListener(val => OnFilterChanged());

            // Weapon Type Dropdown (Y = -215)
            var weaponObj = new GameObject("WeaponDropdown", typeof(RectTransform));
            weaponObj.transform.SetParent(leftPanel.transform, false);
            var wRT = weaponObj.GetComponent<RectTransform>();
            wRT.anchorMin = new Vector2(0.5f, 1);
            wRT.anchorMax = new Vector2(0.5f, 1);
            wRT.anchoredPosition = new Vector2(0, -215);
            wRT.sizeDelta = new Vector2(180, 32);
            _weaponDropdown = weaponObj.AddComponent<TMP_Dropdown>();
            SetupDropdown(_weaponDropdown, new List<string> { "Weapon Type", "Dagger", "One-Handed Sword", "Two-Handed Sword", "Bow", "Staff", "Shield", "Club/Axe/Spear" });
            _weaponDropdown.onValueChanged.AddListener(val => OnFilterChanged());
        }

        private void SetupDropdown(TMP_Dropdown dropdown, List<string> options)
        {
            var btnSprite = KOUIManager.Instance?.GetSkillThemePanelBgSprite("ah_dropdown_btn_r10", 180, 32, 10, _colorInputBg, _colorInputBg, new Color(_colorBorder.r, _colorBorder.g, _colorBorder.b, 0.5f), 1);
            var mainImg = dropdown.gameObject.AddComponent<Image>();
            mainImg.sprite = btnSprite;
            mainImg.color = Color.white;

            // Setup button highlight transitions
            dropdown.targetGraphic = mainImg;
            dropdown.transition = Selectable.Transition.ColorTint;
            var dcb = dropdown.colors;
            dcb.normalColor = Color.white;
            dcb.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            dcb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            dcb.selectedColor = Color.white;
            dropdown.colors = dcb;

            var labelObj = new GameObject("Label", typeof(RectTransform));
            labelObj.transform.SetParent(dropdown.transform, false);
            var lRT = labelObj.GetComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero;
            lRT.anchorMax = Vector2.one;
            lRT.sizeDelta = new Vector2(-30, 0);
            lRT.anchoredPosition = new Vector2(10, 0);
            var label = labelObj.AddComponent<TextMeshProUGUI>();
            label.fontSize = 11;
            label.color = _colorTextGold;
            label.alignment = TextAlignmentOptions.Left;

            var arrowObj = new GameObject("Arrow", typeof(RectTransform));
            arrowObj.transform.SetParent(dropdown.transform, false);
            var arrRT = arrowObj.GetComponent<RectTransform>();
            arrRT.anchorMin = new Vector2(1, 0.5f);
            arrRT.anchorMax = new Vector2(1, 0.5f);
            arrRT.anchoredPosition = new Vector2(-15, 0);
            arrRT.sizeDelta = new Vector2(15, 15);
            var arrow = arrowObj.AddComponent<TextMeshProUGUI>();
            arrow.text = "▼";
            arrow.fontSize = 8;
            arrow.color = _colorBorder;
            arrow.alignment = TextAlignmentOptions.Center;

            dropdown.captionText = label;

            // Setup Options
            dropdown.ClearOptions();
            dropdown.AddOptions(options);

            // Calculate dynamic height to fit all items (each item is 24px + 8px viewport padding)
            int itemHeight = 24;
            int padding = 8;
            int totalHeight = options.Count * itemHeight + padding;

            // Setup template structure
            var template = new GameObject("Template", typeof(RectTransform));
            template.transform.SetParent(dropdown.transform, false);
            template.SetActive(false);
            var tRT = template.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0, 0);
            tRT.anchorMax = new Vector2(1, 0);
            tRT.pivot = new Vector2(0.5f, 1);
            tRT.sizeDelta = new Vector2(0, totalHeight);

            // Round all 4 corners with radius = 12
            var templateSprite = KOUIManager.Instance?.GetSkillThemePanelBgSprite($"ah_dropdown_template_h{totalHeight}_r12", 180, totalHeight, 12, new Color(0.06f, 0.05f, 0.04f, 0.98f), new Color(0.03f, 0.02f, 0.02f, 0.98f), _colorBorder, 1);
            var templateImg = template.AddComponent<Image>();
            templateImg.sprite = templateSprite;
            templateImg.color = Color.white;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(template.transform, false);
            var vRT = viewport.GetComponent<RectTransform>();
            vRT.anchorMin = new Vector2(0, 0);
            vRT.anchorMax = new Vector2(1, 1);
            vRT.sizeDelta = new Vector2(0, -8);
            vRT.anchoredPosition = new Vector2(0, 0);
            var vImg = viewport.AddComponent<Image>();
            vImg.color = Color.white;
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cRT = content.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0, 1);
            cRT.anchorMax = new Vector2(1, 1);
            cRT.pivot = new Vector2(0.5f, 1);
            cRT.sizeDelta = new Vector2(0, options.Count * itemHeight);

            var item = new GameObject("Item", typeof(RectTransform));
            item.transform.SetParent(content.transform, false);
            var iRT = item.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0, 1);
            iRT.anchorMax = new Vector2(1, 1);
            iRT.pivot = new Vector2(0.5f, 1);
            iRT.anchoredPosition = Vector2.zero;
            iRT.sizeDelta = new Vector2(0, itemHeight);
            var itemBtn = item.AddComponent<Toggle>();

            var itemBgObj = new GameObject("Item Background", typeof(RectTransform));
            itemBgObj.transform.SetParent(item.transform, false);
            var ibRT = itemBgObj.GetComponent<RectTransform>();
            ibRT.anchorMin = Vector2.zero;
            ibRT.anchorMax = Vector2.one;
            ibRT.sizeDelta = Vector2.zero;
            var itemBgImg = itemBgObj.AddComponent<Image>();
            itemBgImg.color = new Color(1f, 1f, 1f, 0f);

            itemBtn.targetGraphic = itemBgImg;
            itemBtn.transition = Selectable.Transition.ColorTint;
            var icb = itemBtn.colors;
            icb.normalColor = Color.clear;
            icb.highlightedColor = new Color(0.6f, 0.48f, 0.22f, 0.2f);
            icb.pressedColor = new Color(0.6f, 0.48f, 0.22f, 0.35f);
            icb.selectedColor = new Color(0.6f, 0.48f, 0.22f, 0.15f);
            itemBtn.colors = icb;

            var itemCheckObj = new GameObject("Item Checkmark", typeof(RectTransform));
            itemCheckObj.transform.SetParent(item.transform, false);
            var icRT = itemCheckObj.GetComponent<RectTransform>();
            icRT.anchorMin = new Vector2(1, 0.5f);
            icRT.anchorMax = new Vector2(1, 0.5f);
            icRT.anchoredPosition = new Vector2(-15, 0);
            icRT.sizeDelta = new Vector2(8, 8);
            var itemCheckImg = itemCheckObj.AddComponent<Image>();
            itemCheckImg.sprite = KOUIManager.Instance?.GetSkillThemePanelBgSprite("ah_drp_check", 8, 8, 4, _colorBorder, _colorBorder, _colorBorder, 0);
            itemCheckImg.color = Color.white;

            itemBtn.graphic = itemCheckImg;
            itemBtn.isOn = false;

            var itemLabel = new GameObject("Item Label", typeof(RectTransform));
            itemLabel.transform.SetParent(item.transform, false);
            var ilRT = itemLabel.GetComponent<RectTransform>();
            ilRT.anchorMin = Vector2.zero;
            ilRT.anchorMax = Vector2.one;
            ilRT.sizeDelta = new Vector2(-30, 0);
            ilRT.anchoredPosition = new Vector2(10, 0);
            var ilText = itemLabel.AddComponent<TextMeshProUGUI>();
            ilText.fontSize = 11;
            ilText.color = Color.white;
            ilText.alignment = TextAlignmentOptions.Left;

            dropdown.itemText = ilText;
            dropdown.template = tRT;
        }

        private void CreateRightTopHeader(Transform parent)
        {
            // Title
            var titleObj = new GameObject("Title", typeof(RectTransform));
            titleObj.transform.SetParent(parent, false);
            var titleRT = titleObj.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 1);
            titleRT.anchorMax = new Vector2(0.5f, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.anchoredPosition = new Vector2(50, -20);
            titleRT.sizeDelta = new Vector2(400, 40);
            var title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = "AUCTION HOUSE";
            title.fontSize = 22;
            title.fontStyle = FontStyles.Bold;
            title.color = _colorTitleGold;
            title.alignment = TextAlignmentOptions.Center;

            // Close Button
            var closeObj = new GameObject("CloseBtn", typeof(RectTransform));
            closeObj.transform.SetParent(parent, false);
            var closeRT = closeObj.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1, 1);
            closeRT.anchorMax = new Vector2(1, 1);
            closeRT.pivot = new Vector2(1, 1);
            closeRT.anchoredPosition = new Vector2(-20, -20);
            closeRT.sizeDelta = new Vector2(32, 32);
            var closeImg = closeObj.AddComponent<Image>();
            closeImg.sprite = KOUIManager.Instance?.GetSkillThemePanelBgSprite("close_btn", 32, 32, 0, _colorBtnBg, _colorBtnBg, _colorBorder, 1);
            closeImg.color = Color.white;

            _btnClose = closeObj.AddComponent<Button>();
            _btnClose.onClick.AddListener(() => ShowAH(false));
            SetupButtonTransition(_btnClose, closeImg);

            var closeTextObj = new GameObject("Text", typeof(RectTransform));
            closeTextObj.transform.SetParent(closeObj.transform, false);
            var closeTextRT = closeTextObj.GetComponent<RectTransform>();
            closeTextRT.anchorMin = Vector2.zero;
            closeTextRT.anchorMax = Vector2.one;
            closeTextRT.sizeDelta = Vector2.zero;
            var closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "X";
            closeText.fontSize = 14;
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = _colorBorder;
            closeText.fontStyle = FontStyles.Bold;
            closeText.raycastTarget = false;

            // Refresh Button
            var refreshObj = new GameObject("RefreshBtn", typeof(RectTransform));
            refreshObj.transform.SetParent(parent, false);
            var refRT = refreshObj.GetComponent<RectTransform>();
            refRT.anchorMin = new Vector2(1, 1);
            refRT.anchorMax = new Vector2(1, 1);
            refRT.pivot = new Vector2(1, 1);
            refRT.anchoredPosition = new Vector2(-20, -60);
            refRT.sizeDelta = new Vector2(32, 32);
            var refImg = refreshObj.AddComponent<Image>();
            refImg.sprite = KOUIManager.Instance?.GetSkillThemePanelBgSprite("refresh_btn", 32, 32, 0, _colorBtnBg, _colorBtnBg, _colorBorder, 1);
            refImg.color = Color.white;
            _btnRefresh = refreshObj.AddComponent<Button>();
            _btnRefresh.onClick.AddListener(() => RequestListingsFromServer());
            var refTextObj = new GameObject("Text", typeof(RectTransform));
            refTextObj.transform.SetParent(refreshObj.transform, false);
            var refTextRT = refTextObj.GetComponent<RectTransform>();
            refTextRT.anchorMin = Vector2.zero;
            refTextRT.anchorMax = Vector2.one;
            refTextRT.sizeDelta = Vector2.zero;
            var refText = refTextObj.AddComponent<TextMeshProUGUI>();
            refText.text = "R";
            refText.fontSize = 16;
            refText.alignment = TextAlignmentOptions.Center;
            refText.color = _colorBorder;
            refText.fontStyle = FontStyles.Bold;
            refText.raycastTarget = false;

            // YOUR A.H COINS Info Box
            var coinsPanel = new GameObject("CoinsPanel", typeof(RectTransform));
            coinsPanel.transform.SetParent(parent, false);
            var cPanelRT = coinsPanel.GetComponent<RectTransform>();
            cPanelRT.anchorMin = new Vector2(1, 1);
            cPanelRT.anchorMax = new Vector2(1, 1);
            cPanelRT.pivot = new Vector2(1, 1);
            cPanelRT.anchoredPosition = new Vector2(-70, -20);
            cPanelRT.sizeDelta = new Vector2(180, 32);
            var coinsImg = coinsPanel.AddComponent<Image>();
            coinsImg.sprite = KOUIManager.Instance?.GetSkillThemePanelBgSprite("coins_panel", 180, 32, 0, _colorBtnBg, _colorBtnBg, _colorBorder, 1);
            coinsImg.color = Color.white;

            // Label (YOUR A.H COINS)
            var coinsLabel = new GameObject("Label", typeof(RectTransform));
            coinsLabel.transform.SetParent(coinsPanel.transform, false);
            var lRT = coinsLabel.GetComponent<RectTransform>();
            lRT.anchorMin = new Vector2(0.5f, 1);
            lRT.anchorMax = new Vector2(0.5f, 1);
            lRT.anchoredPosition = new Vector2(0, 13);
            lRT.sizeDelta = new Vector2(180, 15);
            var lTxt = coinsLabel.AddComponent<TextMeshProUGUI>();
            lTxt.text = "YOUR A.H COINS";
            lTxt.fontSize = 9;
            lTxt.color = _colorTextGold;
            lTxt.alignment = TextAlignmentOptions.Center;
            lTxt.fontStyle = FontStyles.Bold;

            // Plus / Circle C+ indicator (from screenshot)
            var coinPoolIcon = new GameObject("PoolIcon", typeof(RectTransform));
            coinPoolIcon.transform.SetParent(coinsPanel.transform, false);
            var cpIRT = coinPoolIcon.GetComponent<RectTransform>();
            cpIRT.anchorMin = new Vector2(0, 0.5f);
            cpIRT.anchorMax = new Vector2(0.5f, 0.5f);
            cpIRT.anchoredPosition = new Vector2(18, 0);
            cpIRT.sizeDelta = new Vector2(20, 20);
            coinPoolIcon.AddComponent<Image>().color = _colorBorder;
            var cpITxtObj = new GameObject("Text", typeof(RectTransform));
            cpITxtObj.transform.SetParent(coinPoolIcon.transform, false);
            var cpITxtRT = cpITxtObj.GetComponent<RectTransform>();
            cpITxtRT.anchorMin = Vector2.zero;
            cpITxtRT.anchorMax = Vector2.one;
            cpITxtRT.sizeDelta = Vector2.zero;
            var cpITxt = cpITxtObj.AddComponent<TextMeshProUGUI>();
            cpITxt.text = "C+";
            cpITxt.fontSize = 10;
            cpITxt.color = _colorBtnBg;
            cpITxt.fontStyle = FontStyles.Bold;
            cpITxt.alignment = TextAlignmentOptions.Center;

            // Value text
            var coinsTextObj = new GameObject("CoinsText", typeof(RectTransform));
            coinsTextObj.transform.SetParent(coinsPanel.transform, false);
            var ctRT = coinsTextObj.GetComponent<RectTransform>();
            ctRT.anchorMin = Vector2.zero;
            ctRT.anchorMax = Vector2.one;
            ctRT.sizeDelta = new Vector2(-45, 0);
            ctRT.anchoredPosition = new Vector2(15, 0);
            _ahCoinsText = coinsTextObj.AddComponent<TextMeshProUGUI>();
            _ahCoinsText.text = "0";
            _ahCoinsText.fontSize = 12;
            _ahCoinsText.fontStyle = FontStyles.Bold;
            _ahCoinsText.color = Color.white;
            _ahCoinsText.alignment = TextAlignmentOptions.Right;
        }

        private void CreateMainTable(Transform parent)
        {
            var tableContainer = new GameObject("TableContainer", typeof(RectTransform));
            tableContainer.transform.SetParent(parent, false);
            var tRT = tableContainer.GetComponent<RectTransform>();
            // Margins: Left=250, Bottom=90, Right=80 (from 1024 width), Top=100 (from 600 height)
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(250, 90);
            tRT.offsetMax = new Vector2(-80, -100);

            // Table headers row
            var headerRow = new GameObject("Headers", typeof(RectTransform));
            headerRow.transform.SetParent(tableContainer.transform, false);
            var hRT = headerRow.GetComponent<RectTransform>();
            hRT.anchorMin = new Vector2(0, 1);
            hRT.anchorMax = new Vector2(1, 1);
            hRT.pivot = new Vector2(0.5f, 1);
            hRT.anchoredPosition = new Vector2(0, 0);
            hRT.sizeDelta = new Vector2(0, 24);

            CreateHeaderLabel(headerRow.transform, "ITEM NAME", new Vector2(0, 0), new Vector2(0.35f, 1));
            CreateHeaderLabel(headerRow.transform, "ITEM AMOUNT", new Vector2(0.35f, 0), new Vector2(0.5f, 1));
            CreateHeaderLabel(headerRow.transform, "EXPIRE TIME", new Vector2(0.5f, 0), new Vector2(0.7f, 1));
            CreateHeaderLabel(headerRow.transform, "BUY PRICE", new Vector2(0.7f, 0), new Vector2(0.85f, 1));

            // Divider line below headers
            var div = new GameObject("Divider", typeof(RectTransform));
            div.transform.SetParent(tableContainer.transform, false);
            var divRT = div.GetComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0, 1);
            divRT.anchorMax = new Vector2(1, 1);
            divRT.anchoredPosition = new Vector2(0, -26);
            divRT.sizeDelta = new Vector2(0, 2);
            div.AddComponent<Image>().color = new Color(_colorBorder.r, _colorBorder.g, _colorBorder.b, 0.5f);

            // Scroll View
            var scrollObj = new GameObject("ScrollView", typeof(RectTransform));
            scrollObj.transform.SetParent(tableContainer.transform, false);
            var scrollRT = scrollObj.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 0);
            scrollRT.offsetMax = new Vector2(0, -32); // below divider

            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollObj.transform, false);
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.1f);
            viewport.AddComponent<Mask>();
            var viewportRT = viewport.GetComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            _browseScrollContent = content.transform;
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 300);

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRT;
            scrollRect.content = contentRT;
        }

        private void CreateHeaderLabel(Transform parent, string labelText, Vector2 anchorMin, Vector2 anchorMax)
        {
            var label = new GameObject("Header_" + labelText, typeof(RectTransform));
            label.transform.SetParent(parent, false);
            var lRT = label.GetComponent<RectTransform>();
            lRT.anchorMin = anchorMin;
            lRT.anchorMax = anchorMax;
            lRT.sizeDelta = Vector2.zero;
            var txt = label.AddComponent<TextMeshProUGUI>();
            string display = labelText;
            if (labelText == "ITEM NAME") display = "ITEM NAME";
            else if (labelText == "ITEM AMOUNT") display = "▲ ITEM AMOUNT";
            else if (labelText == "EXPIRE TIME") display = "▲ EXPIRE TIME";
            else if (labelText == "BUY PRICE") display = "▼ BUY PRICE";
            txt.text = display;
            txt.fontSize = 11;
            txt.color = _colorTextGold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
        }

        private void CreateBottomPanel(Transform parent)
        {
               // REGISTER YOUR ITEM button
            var regObj = new GameObject("RegisterItemBtn", typeof(RectTransform));
            regObj.transform.SetParent(parent, false);
            var regRT = regObj.GetComponent<RectTransform>();
            regRT.anchorMin = new Vector2(0, 0);
            regRT.anchorMax = new Vector2(0, 0);
            regRT.pivot = new Vector2(0, 0);
            regRT.anchoredPosition = new Vector2(250, 25);
            regRT.sizeDelta = new Vector2(180, 38);
            var regImg = regObj.AddComponent<Image>();
            regImg.sprite = KOUIManager.Instance?.GetSkillThemePanelBgSprite("reg_item_btn_round", 180, 38, 19, new Color(0.12f, 0.38f, 0.12f, 1f), new Color(0.04f, 0.16f, 0.04f, 1f), new Color(0.25f, 0.65f, 0.25f, 1f), 1);
            regImg.color = Color.white;

            _btnRegisterItem = regObj.AddComponent<Button>();
            _btnRegisterItem.onClick.AddListener(OnRegisterItemClicked);
            SetupButtonTransition(_btnRegisterItem, regImg);

            var regTxtObj = new GameObject("Text", typeof(RectTransform));
            regTxtObj.transform.SetParent(regObj.transform, false);
            var regTxtRT = regTxtObj.GetComponent<RectTransform>();
            regTxtRT.anchorMin = Vector2.zero;
            regTxtRT.anchorMax = Vector2.one;
            regTxtRT.sizeDelta = Vector2.zero;
            var regTxt = regTxtObj.AddComponent<TextMeshProUGUI>();
            regTxt.text = "REGISTER YOUR ITEM";
            regTxt.fontSize = 12;
            regTxt.fontStyle = FontStyles.Bold;
            regTxt.color = Color.white;
            regTxt.alignment = TextAlignmentOptions.Center;
            regTxt.raycastTarget = false;

            // Sayfalama (Pagination)
            var pagePanel = new GameObject("Pagination", typeof(RectTransform));
            pagePanel.transform.SetParent(parent, false);
            var pRT = pagePanel.GetComponent<RectTransform>();
            pRT.anchorMin = new Vector2(0.5f, 0);
            pRT.anchorMax = new Vector2(0.5f, 0);
            pRT.pivot = new Vector2(0.5f, 0);
            pRT.anchoredPosition = new Vector2(100, 25); // shift slightly right of center to balance
            pRT.sizeDelta = new Vector2(180, 38);

            // Prev button
            var prevObj = new GameObject("PrevBtn", typeof(RectTransform));
            prevObj.transform.SetParent(pagePanel.transform, false);
            var prevRT = prevObj.GetComponent<RectTransform>();
            prevRT.anchorMin = new Vector2(0, 0.5f);
            prevRT.anchorMax = new Vector2(0, 0.5f);
            prevRT.anchoredPosition = new Vector2(20, 0);
            prevRT.sizeDelta = new Vector2(30, 30);
            var prevImg = prevObj.AddComponent<Image>();
            prevImg.sprite = KOUIManager.Instance?.GetSkillThemePanelBgSprite("prev_page_btn", 30, 30, 0, _colorBgTop, _colorBgBottom, _colorBorder, 1);
            prevImg.color = Color.white;
            _btnPrevPage = prevObj.AddComponent<Button>();
            _btnPrevPage.onClick.AddListener(OnPrevPageClicked);
            var prevTxtObj = new GameObject("Text", typeof(RectTransform));
            prevTxtObj.transform.SetParent(prevObj.transform, false);
            var prevTxtRT = prevTxtObj.GetComponent<RectTransform>();
            prevTxtRT.anchorMin = Vector2.zero;
            prevTxtRT.anchorMax = Vector2.one;
            prevTxtRT.sizeDelta = Vector2.zero;
            var prevTxt = prevTxtObj.AddComponent<TextMeshProUGUI>();
            prevTxt.text = "<";
            prevTxt.fontSize = 11;
            prevTxt.color = _colorBorder;
            prevTxt.fontStyle = FontStyles.Bold;
            prevTxt.alignment = TextAlignmentOptions.Center;
            prevTxt.raycastTarget = false;

            // Page text
            var pageTextObj = new GameObject("PageText", typeof(RectTransform));
            pageTextObj.transform.SetParent(pagePanel.transform, false);
            var ptRT = pageTextObj.GetComponent<RectTransform>();
            ptRT.anchorMin = new Vector2(0.5f, 0.5f);
            ptRT.anchorMax = new Vector2(0.5f, 0.5f);
            ptRT.sizeDelta = new Vector2(80, 30);
            _pageText = pageTextObj.AddComponent<TextMeshProUGUI>();
            _pageText.text = "1/1";
            _pageText.fontSize = 14;
            _pageText.fontStyle = FontStyles.Bold;
            _pageText.color = _colorTextGold;
            _pageText.alignment = TextAlignmentOptions.Center;

            // Next button
            var nextObj = new GameObject("NextBtn", typeof(RectTransform));
            nextObj.transform.SetParent(pagePanel.transform, false);
            var nextRT = nextObj.GetComponent<RectTransform>();
            nextRT.anchorMin = new Vector2(1, 0.5f);
            nextRT.anchorMax = new Vector2(1, 0.5f);
            nextRT.anchoredPosition = new Vector2(-20, 0);
            nextRT.sizeDelta = new Vector2(30, 30);
            var nextImg = nextObj.AddComponent<Image>();
            nextImg.sprite = KOUIManager.Instance?.GetSkillThemePanelBgSprite("next_page_btn", 30, 30, 0, _colorBgTop, _colorBgBottom, _colorBorder, 1);
            nextImg.color = Color.white;
            _btnNextPage = nextObj.AddComponent<Button>();
            _btnNextPage.onClick.AddListener(OnNextPageClicked);
            var nextTxtObj = new GameObject("Text", typeof(RectTransform));
            nextTxtObj.transform.SetParent(nextObj.transform, false);
            var nextTxtRT = nextTxtObj.GetComponent<RectTransform>();
            nextTxtRT.anchorMin = Vector2.zero;
            nextTxtRT.anchorMax = Vector2.one;
            nextTxtRT.sizeDelta = Vector2.zero;
            var nextTxt = nextTxtObj.AddComponent<TextMeshProUGUI>();
            nextTxt.text = ">";
            nextTxt.fontSize = 11;
            nextTxt.color = _colorBorder;
            nextTxt.fontStyle = FontStyles.Bold;
            nextTxt.alignment = TextAlignmentOptions.Center;
            nextTxt.raycastTarget = false;

            // SELL / BUY HISTORY button
            var histObj = new GameObject("HistoryBtn", typeof(RectTransform));
            histObj.transform.SetParent(parent, false);
            var histRT = histObj.GetComponent<RectTransform>();
            histRT.anchorMin = new Vector2(1, 0);
            histRT.anchorMax = new Vector2(1, 0);
            histRT.pivot = new Vector2(1, 0);
            histRT.anchoredPosition = new Vector2(-230, 25);
            histRT.sizeDelta = new Vector2(140, 38);
            var histImg = histObj.AddComponent<Image>();
            histImg.sprite = KOUIManager.Instance?.GetSkillThemePanelBgSprite("history_btn_round", 140, 38, 19, new Color(0.1f, 0.22f, 0.42f, 1f), new Color(0.03f, 0.08f, 0.18f, 1f), new Color(0.3f, 0.55f, 0.9f, 1f), 1);
            histImg.color = Color.white;
            _btnHistory = histObj.AddComponent<Button>();
            _btnHistory.onClick.AddListener(OnHistoryClicked);
            SetupButtonTransition(_btnHistory, histImg);

            var histTxtObj = new GameObject("Text", typeof(RectTransform));
            histTxtObj.transform.SetParent(histObj.transform, false);
            var histTxtRT = histTxtObj.GetComponent<RectTransform>();
            histTxtRT.anchorMin = Vector2.zero;
            histTxtRT.anchorMax = Vector2.one;
            histTxtRT.sizeDelta = Vector2.zero;
            var histTxt = histTxtObj.AddComponent<TextMeshProUGUI>();
            histTxt.text = "SELL / BUY HISTORY";
            histTxt.fontSize = 10;
            histTxt.fontStyle = FontStyles.Bold;
            histTxt.color = Color.white;
            histTxt.alignment = TextAlignmentOptions.Center;
            histTxt.raycastTarget = false;

            // MY MERCHANT / MY LISTINGS button
            var merchObj = new GameObject("MyMerchantBtn", typeof(RectTransform));
            merchObj.transform.SetParent(parent, false);
            var merRT = merchObj.GetComponent<RectTransform>();
            merRT.anchorMin = new Vector2(1, 0);
            merRT.anchorMax = new Vector2(1, 0);
            merRT.pivot = new Vector2(1, 0);
            merRT.anchoredPosition = new Vector2(-80, 25);
            merRT.sizeDelta = new Vector2(140, 38);
            var merImg = merchObj.AddComponent<Image>();
            merImg.sprite = KOUIManager.Instance?.GetSkillThemePanelBgSprite("my_merchant_btn_round", 140, 38, 19, new Color(0.38f, 0.26f, 0.08f, 1f), new Color(0.16f, 0.08f, 0.02f, 1f), new Color(0.88f, 0.68f, 0.22f, 1f), 1);
            merImg.color = Color.white;
            _btnMyMerchant = merchObj.AddComponent<Button>();
            _btnMyMerchant.onClick.AddListener(OnMyMerchantClicked);
            SetupButtonTransition(_btnMyMerchant, merImg);

            var merTxtObj = new GameObject("Text", typeof(RectTransform));
            merTxtObj.transform.SetParent(merchObj.transform, false);
            var merTxtRT = merTxtObj.GetComponent<RectTransform>();
            merTxtRT.anchorMin = Vector2.zero;
            merTxtRT.anchorMax = Vector2.one;
            merTxtRT.sizeDelta = Vector2.zero;
            var merTxt = merTxtObj.AddComponent<TextMeshProUGUI>();
            merTxt.text = "MY MERCHANT";
            merTxt.fontSize = 10;
            merTxt.fontStyle = FontStyles.Bold;
            merTxt.color = Color.white;
            merTxt.alignment = TextAlignmentOptions.Center;
            merTxt.raycastTarget = false;       }

        private void CreateInventoryModal(Transform parent)
        {
            // Semi-transparent blocker
            _inventoryModal = new GameObject("AH_InventoryModal", typeof(RectTransform));
            _inventoryModal.transform.SetParent(parent, false);
            var mainRT = _inventoryModal.GetComponent<RectTransform>();
            mainRT.anchorMin = Vector2.zero;
            mainRT.anchorMax = Vector2.one;
            mainRT.sizeDelta = Vector2.zero;
            _inventoryModal.AddComponent<Image>().color = new Color(0, 0, 0, 0.75f);

            // Modal Frame
            var frame = new GameObject("Frame", typeof(RectTransform));
            frame.transform.SetParent(_inventoryModal.transform, false);
            var fRT = frame.GetComponent<RectTransform>();
            fRT.anchorMin = new Vector2(0.5f, 0.5f);
            fRT.anchorMax = new Vector2(0.5f, 0.5f);
            fRT.pivot = new Vector2(0.5f, 0.5f);
            fRT.sizeDelta = new Vector2(374, 430);

            var frameImg = frame.AddComponent<Image>();
            frameImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemePanelBgSprite("ah_inventory_modal_bg", 374, 430, 0,
                    _colorBgTop, _colorBgBottom, _colorBorder, 2) : null;
            frameImg.color = Color.white;

            var uiContainer = frame.transform;

            // Title
            var titleObj = new GameObject("Title", typeof(RectTransform));
            titleObj.transform.SetParent(uiContainer, false);
            var tRT = titleObj.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0, 1);
            tRT.anchorMax = new Vector2(1, 1);
            tRT.anchoredPosition = new Vector2(0, -25);
            tRT.sizeDelta = new Vector2(-40, 30);
            
            var title = titleObj.AddComponent<Text>();
            title.text = "INVENTORY & REGISTER YOUR ITEM";
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 14;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.95f, 0.85f, 0.35f, 1f); // Bright Gold/Yellow matching SKILL PAGE
            title.fontStyle = FontStyle.Bold;

            var shadowTitle = titleObj.AddComponent<Shadow>();
            shadowTitle.effectColor = new Color(0, 0, 0, 0.85f);
            shadowTitle.effectDistance = new Vector2(1, -1);

            // X close button
            var closeObj = new GameObject("CloseBtn", typeof(RectTransform));
            closeObj.transform.SetParent(uiContainer, false);
            var closeRT = closeObj.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1, 1);
            closeRT.anchorMax = new Vector2(1, 1);
            closeRT.pivot = new Vector2(1, 1);
            closeRT.anchoredPosition = new Vector2(-15, -15);
            closeRT.sizeDelta = new Vector2(25, 25);
            
            var closeImg = closeObj.AddComponent<Image>();
            closeImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemePanelBgSprite("ah_close_btn", 25, 25, 0, _colorBtnBg, _colorBtnBg, _colorBorder, 1) : null;
            closeImg.color = Color.white;



            var closeBtn = closeObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => _inventoryModal.SetActive(false));
            SetupButtonTransition(closeBtn, closeImg);

            var closeTxtObj = new GameObject("Text", typeof(RectTransform));
            closeTxtObj.transform.SetParent(closeObj.transform, false);
            var closeTxtRT = closeTxtObj.GetComponent<RectTransform>();
            closeTxtRT.anchorMin = Vector2.zero;
            closeTxtRT.anchorMax = Vector2.one;
            closeTxtRT.sizeDelta = Vector2.zero;

            var closeTxt = closeTxtObj.AddComponent<Text>();
            closeTxt.text = "X";
            closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeTxt.fontSize = 11;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.color = _colorBorder;
            closeTxt.fontStyle = FontStyle.Bold;

            // Subtitle instructions (Moved below ActiveListingsScroll)
            var descObj = new GameObject("Description", typeof(RectTransform));
            descObj.transform.SetParent(uiContainer, false);
            var descRT = descObj.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0, 1);
            descRT.anchorMax = new Vector2(1, 1);
            descRT.pivot = new Vector2(0.5f, 1f); // Set pivot to Top
            descRT.anchoredPosition = new Vector2(0, -172); // Positioned under active listings
            descRT.sizeDelta = new Vector2(-40, 30);
            var desc = descObj.AddComponent<TextMeshProUGUI>();
            desc.text = "Click on the item you want to sell, enter its price, and send it to the Auction House.";
            desc.fontSize = 11;
            desc.fontStyle = FontStyles.Bold;
            desc.color = _colorTextGold;
            desc.alignment = TextAlignmentOptions.Center;

            // Scroll view for active listings in register panel (Moved above Description)
            var activeScrollObj = new GameObject("ActiveListingsScroll", typeof(RectTransform));
            activeScrollObj.transform.SetParent(uiContainer, false);
            var activeScrollRT = activeScrollObj.GetComponent<RectTransform>();
            activeScrollRT.anchorMin = new Vector2(0.5f, 1);
            activeScrollRT.anchorMax = new Vector2(0.5f, 1);
            activeScrollRT.pivot = new Vector2(0.5f, 1);
            activeScrollRT.anchoredPosition = new Vector2(0, -50); // Set right below Title
            activeScrollRT.sizeDelta = new Vector2(334, 112); // Exactly 2 rows of 49 height + spacing & padding

            var scrollBg = activeScrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0, 0, 0, 0.25f);

            var activeSRect = activeScrollObj.AddComponent<ScrollRect>();
            activeSRect.horizontal = false;
            activeSRect.vertical = true;

            var activeVp = new GameObject("Viewport", typeof(RectTransform));
            activeVp.transform.SetParent(activeScrollObj.transform, false);
            var activeVpImg = activeVp.AddComponent<Image>();
            activeVpImg.color = Color.white;
            var activeVpMask = activeVp.AddComponent<Mask>();
            activeVpMask.showMaskGraphic = false;
            var activeVpRT = activeVp.GetComponent<RectTransform>();
            activeVpRT.anchorMin = Vector2.zero;
            activeVpRT.anchorMax = Vector2.one;
            activeVpRT.sizeDelta = Vector2.zero;

            var activeContent = new GameObject("Content", typeof(RectTransform));
            activeContent.transform.SetParent(activeVp.transform, false);
            _regActiveListingsScrollContent = activeContent.transform;
            var activeContentRT = activeContent.GetComponent<RectTransform>();
            activeContentRT.anchorMin = new Vector2(0, 1);
            activeContentRT.anchorMax = new Vector2(1, 1);
            activeContentRT.pivot = new Vector2(0.5f, 1);
            activeContentRT.sizeDelta = new Vector2(0, 0);

            var activeVlg = activeContent.AddComponent<VerticalLayoutGroup>();
            activeVlg.spacing = 6;
            activeVlg.padding = new RectOffset(4, 4, 4, 4);
            activeVlg.childAlignment = TextAnchor.UpperCenter;
            activeVlg.childControlHeight = false;
            activeVlg.childControlWidth = false;
            activeVlg.childForceExpandHeight = false;
            activeVlg.childForceExpandWidth = false;

            var activeCsf = activeContent.AddComponent<ContentSizeFitter>();
            activeCsf.verticalFit = ContentSizeFitter.FitMode.MinSize;

            activeSRect.viewport = activeVpRT;
            activeSRect.content = activeContentRT;

            // Grid for inventory slots (Scroll view)
            var scrollObj = new GameObject("GridScroll", typeof(RectTransform));
            scrollObj.transform.SetParent(uiContainer, false);
            var scrollRT = scrollObj.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.5f, 1);
            scrollRT.anchorMax = new Vector2(0.5f, 1);
            scrollRT.pivot = new Vector2(0.5f, 1);
            scrollRT.anchoredPosition = new Vector2(0, -212); // Symmetrically balanced
            scrollRT.sizeDelta = new Vector2(334, 201);

            var sRect = scrollObj.AddComponent<ScrollRect>();
            sRect.horizontal = false;
            sRect.vertical = true;
            var vp = new GameObject("Viewport", typeof(RectTransform));
            vp.transform.SetParent(scrollObj.transform, false);
            var vpImg = vp.AddComponent<Image>();
            vpImg.color = Color.white;
            var vpMask = vp.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;
            var vpRT = vp.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.sizeDelta = Vector2.zero;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vp.transform, false);
            _invGridContent = content.transform;
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 201);

            var glg = content.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(45, 45);
            glg.spacing = new Vector2(3, 3);
            glg.padding = new RectOffset(0, 0, 6, 6);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 7; // 7 columns, 4 rows = 28 slots

            sRect.viewport = vpRT;
            sRect.content = contentRT;

            _inventoryModal.SetActive(false);
        }

        private void CreateMyMerchantModal(Transform parent)
        {
            // Semi-transparent blocker
            _myMerchantModal = new GameObject("AH_MyMerchantModal", typeof(RectTransform));
            _myMerchantModal.transform.SetParent(parent, false);
            var mainRT = _myMerchantModal.GetComponent<RectTransform>();
            mainRT.anchorMin = Vector2.zero;
            mainRT.anchorMax = Vector2.one;
            mainRT.sizeDelta = Vector2.zero;
            _myMerchantModal.AddComponent<Image>().color = new Color(0, 0, 0, 0.75f);

            // Modal Frame - Widened to 540 to prevent headers from overlapping
            var frame = new GameObject("Frame", typeof(RectTransform));
            frame.transform.SetParent(_myMerchantModal.transform, false);
            var fRT = frame.GetComponent<RectTransform>();
            fRT.anchorMin = new Vector2(0.5f, 0.5f);
            fRT.anchorMax = new Vector2(0.5f, 0.5f);
            fRT.pivot = new Vector2(0.5f, 0.5f);
            fRT.sizeDelta = new Vector2(540, 430);

            var frameImg = frame.AddComponent<Image>();
            frameImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemePanelBgSprite("ah_my_merchant_modal_bg", 540, 430, 0,
                    _colorBgTop, _colorBgBottom, _colorBorder, 2) : null;
            frameImg.color = Color.white;

            var uiContainer = frame.transform;

            // Title (Styled exactly like SKILL PAGE)
            var titleObj = new GameObject("Title", typeof(RectTransform));
            titleObj.transform.SetParent(uiContainer, false);
            var tRT = titleObj.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0, 1);
            tRT.anchorMax = new Vector2(1, 1);
            tRT.anchoredPosition = new Vector2(0, -25);
            tRT.sizeDelta = new Vector2(-40, 30);
            
            var title = titleObj.AddComponent<Text>();
            title.text = "MY MERCHANT";
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 14;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.95f, 0.85f, 0.35f, 1f); // Gold title color
            title.fontStyle = FontStyle.Bold;

            var shadowTitle = titleObj.AddComponent<Shadow>();
            shadowTitle.effectColor = new Color(0, 0, 0, 0.85f);
            shadowTitle.effectDistance = new Vector2(1, -1);

            // X close button
            var closeObj = new GameObject("CloseBtn", typeof(RectTransform));
            closeObj.transform.SetParent(uiContainer, false);
            var closeRT = closeObj.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1, 1);
            closeRT.anchorMax = new Vector2(1, 1);
            closeRT.pivot = new Vector2(1, 1);
            closeRT.anchoredPosition = new Vector2(-15, -15);
            closeRT.sizeDelta = new Vector2(25, 25);
            
            var closeImg = closeObj.AddComponent<Image>();
            closeImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemePanelBgSprite("ah_mymerch_close_btn", 25, 25, 0, _colorBtnBg, _colorBtnBg, _colorBorder, 1) : null;
            closeImg.color = Color.white;



            var closeBtn = closeObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => _myMerchantModal.SetActive(false));
            SetupButtonTransition(closeBtn, closeImg);

            var closeTxtObj = new GameObject("Text", typeof(RectTransform));
            closeTxtObj.transform.SetParent(closeObj.transform, false);
            var closeTxtRT = closeTxtObj.GetComponent<RectTransform>();
            closeTxtRT.anchorMin = Vector2.zero;
            closeTxtRT.anchorMax = Vector2.one;
            closeTxtRT.sizeDelta = Vector2.zero;

            var closeTxt = closeTxtObj.AddComponent<Text>();
            closeTxt.text = "X";
            closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeTxt.fontSize = 11;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.color = _colorBorder;
            closeTxt.fontStyle = FontStyle.Bold;


            // Table Headers Row - Widened to 500
            var headerRow = new GameObject("HeaderRow", typeof(RectTransform));
            headerRow.transform.SetParent(uiContainer, false);
            var hRT = headerRow.GetComponent<RectTransform>();
            hRT.anchorMin = new Vector2(0.5f, 1);
            hRT.anchorMax = new Vector2(0.5f, 1);
            hRT.pivot = new Vector2(0.5f, 1);
            hRT.anchoredPosition = new Vector2(0, -48);
            hRT.sizeDelta = new Vector2(500, 20); // Widened header width

            CreateMyMerchHeaderLabel(headerRow.transform, "ITEM NAME", new Vector2(0f, 0), new Vector2(0.38f, 1));
            CreateMyMerchHeaderLabel(headerRow.transform, "AMOUNT", new Vector2(0.38f, 0), new Vector2(0.52f, 1));
            CreateMyMerchHeaderLabel(headerRow.transform, "EXPIRE TIME", new Vector2(0.54f, 0), new Vector2(0.69f, 1));
            CreateMyMerchHeaderLabel(headerRow.transform, "PRICE", new Vector2(0.69f, 0), new Vector2(0.876f, 1));

            // Scroll view for active listings
            var scrollObj = new GameObject("MyMerchantScroll", typeof(RectTransform));
            scrollObj.transform.SetParent(uiContainer, false);
            var scrollRT = scrollObj.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.5f, 1);
            scrollRT.anchorMax = new Vector2(0.5f, 1);
            scrollRT.pivot = new Vector2(0.5f, 1);
            scrollRT.anchoredPosition = new Vector2(0, -72);
            scrollRT.sizeDelta = new Vector2(500, 330); // Width 500 matching header row

            var scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0, 0, 0, 0.25f);

            var sRect = scrollObj.AddComponent<ScrollRect>();
            sRect.horizontal = false;
            sRect.vertical = true;

            var vp = new GameObject("Viewport", typeof(RectTransform));
            vp.transform.SetParent(scrollObj.transform, false);
            var vpImg = vp.AddComponent<Image>();
            vpImg.color = Color.white;
            var vpMask = vp.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;
            var vpRT = vp.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.sizeDelta = Vector2.zero;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vp.transform, false);
            _myMerchantScrollContent = content.transform;
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 0);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.MinSize;

            sRect.viewport = vpRT;
            sRect.content = contentRT;

            _myMerchantModal.SetActive(false);
        }





        private void CreateMyMerchHeaderLabel(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax)
        {
            var label = new GameObject("Header_" + text, typeof(RectTransform));
            label.transform.SetParent(parent, false);
            var lRT = label.GetComponent<RectTransform>();
            lRT.anchorMin = anchorMin;
            lRT.anchorMax = anchorMax;
            lRT.sizeDelta = Vector2.zero;
            var txt = label.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.fontSize = 10;
            txt.color = _colorTextGold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
          }
        private void PopulateMyMerchantModal()
        {
            if (_myMerchantScrollContent == null) return;

            // Clear previous items
            foreach (Transform child in _myMerchantScrollContent)
            {
                Destroy(child.gameObject);
            }

            string myNameClean = EntropyOnline.Core.GameManager.Instance.CharacterName.Trim('\0', ' ');

            int myItemsCount = 0;
            foreach (var listing in _listings)
            {
                string sellerClean = listing.sellerName.Trim('\0', ' ');
                if (sellerClean != myNameClean)
                    continue;

                string itemName = "Unknown Item";
                if (KOInventory.s_pTbl_Items_Basic.TryGetValue((uint)(listing.itemId / 1000 * 1000), out var basic))
                {
                    itemName = basic.szName;
                }

                CreateMyMerchantListingRowUI(_myMerchantScrollContent, listing, itemName);
                myItemsCount++;
            }

            if (myItemsCount == 0)
            {
                var emptyRow = new GameObject("EmptyRow", typeof(RectTransform));
                emptyRow.transform.SetParent(_myMerchantScrollContent, false);
                var rowRT = emptyRow.GetComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(492, 50); // Widened empty row to 492

                var txtObj = new GameObject("Text", typeof(RectTransform));
                txtObj.transform.SetParent(emptyRow.transform, false);
                var txtRT = txtObj.GetComponent<RectTransform>();
                txtRT.anchorMin = Vector2.zero;
                txtRT.anchorMax = Vector2.one;
                txtRT.sizeDelta = Vector2.zero;

                var txt = txtObj.AddComponent<Text>();
                txt.text = "No active listings found.";
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 11;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.gray;
            }
        }

        private void CreateMyMerchantListingRowUI(Transform parent, AuctionListingData listing, string itemName)
        {
            var rowObj = new GameObject("MyMerchRow_" + listing.id, typeof(RectTransform));
            rowObj.transform.SetParent(parent, false);
            var rowRT = rowObj.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(492, 49); // Width 492 (matching widened ScrollRect content area width)

            // Row Border (Gold) - Higher opacity for better separation
            var rowBorder = rowObj.AddComponent<Image>();
            rowBorder.color = new Color(_colorBorder.r, _colorBorder.g, _colorBorder.b, 0.65f);
            rowBorder.raycastTarget = false;

            // Row Inner Content Area
            var innerRow = new GameObject("Inner", typeof(RectTransform));
            innerRow.transform.SetParent(rowObj.transform, false);
            var innerRT = innerRow.GetComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.sizeDelta = new Vector2(-2, -2); // 1px border around
            innerRow.AddComponent<Image>().color = _colorBtnBg;

            // 1. Icon Slot Box (Column 1) - Aligned to the far left
            var iconBox = new GameObject("IconBox", typeof(RectTransform));
            iconBox.transform.SetParent(innerRow.transform, false);
            var ibRT = iconBox.GetComponent<RectTransform>();
            ibRT.anchorMin = new Vector2(0, 0.5f);
            ibRT.anchorMax = new Vector2(0, 0.5f);
            ibRT.pivot = new Vector2(0, 0.5f);
            ibRT.anchoredPosition = new Vector2(2, 0); // Far left alignment
            ibRT.sizeDelta = new Vector2(45, 45); // 45x45 inventory slot size
            
            var slotImg = iconBox.AddComponent<Image>();
            slotImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemeGlassSlotSprite("slot_socket_glass_v5", 45) : null;
            slotImg.color = Color.white;

            // Colored Border around icon
            int ext = (int)(listing.itemId % 100);
            Color iconBrdColor = new Color(_colorBorder.r, _colorBorder.g, _colorBorder.b, 0.4f);
            if (ext == 7) iconBrdColor = new Color(0.29f, 0.56f, 0.89f, 0.8f); // +7 blue
            else if (ext == 8) iconBrdColor = new Color(0.63f, 0.4f, 1f, 0.8f); // +8 purple
            else if (ext >= 9) iconBrdColor = new Color(1f, 0.29f, 0.29f, 0.8f); // >=+9 red

            var iconBrd = new GameObject("Border", typeof(RectTransform));
            iconBrd.transform.SetParent(iconBox.transform, false);
            iconBrd.GetComponent<RectTransform>().sizeDelta = new Vector2(45, 45);
            var iconBrdImg = iconBrd.AddComponent<Image>();
            iconBrdImg.color = iconBrdColor;
            iconBrdImg.raycastTarget = false;
            iconBrd.transform.SetAsFirstSibling();

            var iconObj = new GameObject("Icon", typeof(RectTransform));
            iconObj.transform.SetParent(iconBox.transform, false);
            var iconRT = iconObj.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.sizeDelta = Vector2.zero;
            var iconImg = iconObj.AddComponent<Image>();
            var sprite = KOItemIconLoader.LoadItemIcon(ResolveIconId((int)listing.itemId));
            if (sprite != null)
            {
                iconImg.sprite = sprite;
            }
            else
            {
                iconImg.color = Color.clear;
            }
            iconImg.preserveAspect = true;

            // 2. Name details - Widen item name space
            var nameObj = new GameObject("NameText", typeof(RectTransform));
            nameObj.transform.SetParent(innerRow.transform, false);
            var nameRT = nameObj.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.5f);
            nameRT.anchorMax = new Vector2(0, 0.5f);
            nameRT.pivot = new Vector2(0, 0.5f);
            nameRT.anchoredPosition = new Vector2(55, 0); // After slot width
            nameRT.sizeDelta = new Vector2(130, 40);
            var nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = itemName;
            nameText.fontSize = 10;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            // 3. Amount (Count) - Centered at X = 225, formatted as sold/unsold (0/count)
            var amountObj = new GameObject("AmountText", typeof(RectTransform));
            amountObj.transform.SetParent(innerRow.transform, false);
            var amountRT = amountObj.GetComponent<RectTransform>();
            amountRT.anchorMin = new Vector2(0, 0.5f);
            amountRT.anchorMax = new Vector2(0, 0.5f);
            amountRT.pivot = new Vector2(0.5f, 0.5f);
            amountRT.anchoredPosition = new Vector2(225, 0);
            amountRT.sizeDelta = new Vector2(60, 40);
            var amountText = amountObj.AddComponent<TextMeshProUGUI>();
            amountText.text = $"0/{listing.count}"; // Sold/Unsold format (0/count)
            amountText.fontSize = 10;
            amountText.color = Color.white;
            amountText.alignment = TextAlignmentOptions.Center;

            // 4. Expire Time - Centered at X = 307.5
            var expObj = new GameObject("ExpireTimeText", typeof(RectTransform));
            expObj.transform.SetParent(innerRow.transform, false);
            var expRT = expObj.GetComponent<RectTransform>();
            expRT.anchorMin = new Vector2(0, 0.5f);
            expRT.anchorMax = new Vector2(0, 0.5f);
            expRT.pivot = new Vector2(0.5f, 0.5f);
            expRT.anchoredPosition = new Vector2(307.5f, 0);
            expRT.sizeDelta = new Vector2(70, 40);
            var expText = expObj.AddComponent<TextMeshProUGUI>();
            
            uint mins = listing.remainingMinutes;
            string expireStr = "0m";
            if (mins >= 60)
            {
                uint hrs = mins / 60;
                uint rMins = mins % 60;
                expireStr = rMins > 0 ? $"{hrs}h {rMins}m" : $"{hrs}h";
            }
            else
            {
                expireStr = $"{mins}m";
            }
            expText.text = expireStr;
            expText.fontSize = 10;
            expText.color = Color.white;
            expText.alignment = TextAlignmentOptions.Center;

            // 5. Buy Price - Centered under PRICE column (X = 349, width = 85)
            var priceObj = new GameObject("PriceText", typeof(RectTransform));
            priceObj.transform.SetParent(innerRow.transform, false);
            var priceRT = priceObj.GetComponent<RectTransform>();
            priceRT.anchorMin = new Vector2(0, 0.5f);
            priceRT.anchorMax = new Vector2(0, 0.5f);
            priceRT.pivot = new Vector2(0, 0.5f);
            priceRT.anchoredPosition = new Vector2(349, 0);
            priceRT.sizeDelta = new Vector2(85, 40);
            var priceText = priceObj.AddComponent<TextMeshProUGUI>();
            float myMerchGb = listing.price / 100000000.0f;
            priceText.text = $"{myMerchGb.ToString("0.##")} GB";
            priceText.fontSize = 9;
            priceText.color = Color.white;
            priceText.alignment = TextAlignmentOptions.Center;

            // 6. Cancel Button - Styled exactly like the rounded red close merchant button (from KOMerchantControlUI)
            var cancelBtnObj = new GameObject("CancelBtn", typeof(RectTransform));
            cancelBtnObj.transform.SetParent(innerRow.transform, false);
            var cbRT = cancelBtnObj.GetComponent<RectTransform>();
            cbRT.anchorMin = new Vector2(1, 0.5f);
            cbRT.anchorMax = new Vector2(1, 0.5f);
            cbRT.pivot = new Vector2(1, 0.5f);
            cbRT.anchoredPosition = new Vector2(-5, 0);
            cbRT.sizeDelta = new Vector2(50, 28);

            var cancelImg = cancelBtnObj.AddComponent<Image>();
            cancelImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemeRoundedRectSprite("ah_cancel_btn_bg", 50, 28, 8,
                    new Color(0.45f, 0.05f, 0.08f, 1f), new Color(0.75f, 0.15f, 0.15f, 1f), 1) : null;
            cancelImg.color = Color.white;



            var cancelBtn = cancelBtnObj.AddComponent<Button>();
            cancelBtn.onClick.AddListener(() => OnCancelListingClicked(listing.id));
            SetupButtonTransition(cancelBtn, cancelImg);

            var cancelTxtObj = new GameObject("Text", typeof(RectTransform));
            cancelTxtObj.transform.SetParent(cancelBtnObj.transform, false);
            var cancelTxtRT = cancelTxtObj.GetComponent<RectTransform>();
            cancelTxtRT.anchorMin = Vector2.zero;
            cancelTxtRT.anchorMax = Vector2.one;
            cancelTxtRT.sizeDelta = Vector2.zero;

            var cancelTxt = cancelTxtObj.AddComponent<Text>();
            cancelTxt.text = "CANCEL";
            cancelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cancelTxt.fontSize = 8;
            cancelTxt.alignment = TextAnchor.MiddleCenter;
            cancelTxt.color = Color.white;
            cancelTxt.fontStyle = FontStyle.Bold;
        }
        private void CreateInputFieldHelpers(GameObject parent, string placeholderText)
        {
            var textVal = new GameObject("Text", typeof(RectTransform));
            textVal.transform.SetParent(parent.transform, false);
            var rtText = textVal.GetComponent<RectTransform>();
            rtText.anchorMin = Vector2.zero;
            rtText.anchorMax = Vector2.one;
            rtText.offsetMin = new Vector2(8, 0);
            rtText.offsetMax = new Vector2(-8, 0);

            var txt = textVal.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 12;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Left;
            txt.raycastTarget = false;

            var placeholder = new GameObject("Placeholder", typeof(RectTransform));
            placeholder.transform.SetParent(parent.transform, false);
            var rtPlace = placeholder.GetComponent<RectTransform>();
            rtPlace.anchorMin = Vector2.zero;
            rtPlace.anchorMax = Vector2.one;
            rtPlace.offsetMin = new Vector2(8, 0);
            rtPlace.offsetMax = new Vector2(-8, 0);

            var pTxt = placeholder.AddComponent<TextMeshProUGUI>();
            pTxt.text = placeholderText;
            pTxt.fontSize = 12;
            pTxt.color = Color.gray;
            pTxt.alignment = TextAlignmentOptions.Left;
            pTxt.raycastTarget = false;

            var field = parent.GetComponent<TMP_InputField>();
            field.textComponent = txt;
            field.placeholder = pTxt;
        }

        public void ShowAH(bool show)
        {
            if (_panelRoot == null) return;
            _panelRoot.SetActive(show);

            if (show)
            {
                _filterMyItemsOnly = false;
                _currentPage = 1;
                if (_btnMyMerchant != null)
                {
                    _btnMyMerchant.GetComponent<Image>().color = Color.white;
                }
                RequestListingsFromServer();
                if (KOUIManager.Instance != null)
                {
                    UpdateScale(KOUIManager.Instance.CanvasScaleFactor);
                }
            }
        }

        private void RequestListingsFromServer()
        {
            using (var writer = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS))
            {
                writer.WriteByte(3); // MARKET_BBS_REPORT
                writer.WriteUInt16(0); // startIndex = 0
                KONetworkManager.Instance?.SendPacket(writer);
            }
        }

        private void OnFilterChanged()
        {
            _filterName = _searchField.text;
            _filterArmorType = _armorDropdown.value;
            _filterWeaponType = _weaponDropdown.value;
            _currentPage = 1;
            PopulateBrowseList();
        }

        private void OnPrevPageClicked()
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                PopulateBrowseList();
            }
        }

        private void OnNextPageClicked()
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                PopulateBrowseList();
            }
        }

        private void OnMyMerchantClicked()
        {
            if (_myMerchantModal != null)
            {
                PopulateMyMerchantModal();
                _myMerchantModal.SetActive(true);
            }
        }

        private void OnHistoryClicked()
        {
            // Simple claim list toggle / history placeholder
            KOUIManager.Instance?.AddMsgOutput("No transaction history found.", new Color(0.9f, 0.7f, 0.2f));
            if (_pendingGold > 0)
            {
                // Prompt player to claim if there is pending gold
                KOMessageBox.Instance?.ShowYesNo(
                    $"You have pending {(_pendingGold / 100000000.0f):F2} GB ({_pendingGold:N0} Noah) gold coins waiting to be collected. Would you like to collect them?",
                    "Auction House Claims",
                    MsgBoxBehavior.BEHAVIOR_NOTHING,
                    OnClaimGoldClicked,
                    null
                );
            }
        }

        private void OnRegisterItemClicked()
        {
            PopulateInventoryGrid();
            PopulateRegisterActiveListings();
            _inventoryModal.SetActive(true);
        }

        private void PopulateInventoryGrid()
        {
            foreach (Transform child in _invGridContent)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < KOInventory.MAX_ITEM_INVENTORY; i++)
            {
                int invIdx = i;
                var item = KOInventory.Instance.m_pMyInvWnd[invIdx];

                var slot = new GameObject("Slot_" + invIdx, typeof(RectTransform));
                slot.transform.SetParent(_invGridContent, false);
                var slotImg = slot.AddComponent<Image>();
                slotImg.sprite = KOUIManager.Instance?.GetSkillThemeGlassSlotSprite("slot_socket_glass_v5", 45);
                slotImg.color = Color.white;

                if (item != null && !item.IsEmpty)
                {
                    // Item Icon
                    var iconObj = new GameObject("Icon", typeof(RectTransform));
                    iconObj.transform.SetParent(slot.transform, false);
                    var iRT = iconObj.GetComponent<RectTransform>();
                    iRT.anchorMin = Vector2.zero;
                    iRT.anchorMax = Vector2.one;
                    iRT.sizeDelta = Vector2.zero;
                    var iconImg = iconObj.AddComponent<Image>();
                    var sprite = KOItemIconLoader.LoadItemIcon(ResolveIconId(item.itemId));
                    if (sprite != null)
                    {
                        iconImg.sprite = sprite;
                    }
                    else
                    {
                        iconImg.color = Color.clear;
                    }
                    iconImg.preserveAspect = true;

                    // Button
                    var btn = slot.AddComponent<Button>();
                    btn.onClick.AddListener(() => OnInventorySlotSelected(invIdx, item.itemId, item.count, iconImg.sprite, item.pItemBasic?.szName ?? "Unknown Item"));

                    // Stack Count
                    if (item.count > 1)
                    {
                        var cntObj = new GameObject("Count", typeof(RectTransform));
                        cntObj.transform.SetParent(slot.transform, false);
                        var cRT = cntObj.GetComponent<RectTransform>();
                        cRT.anchorMin = new Vector2(0.5f, 0);
                        cRT.anchorMax = new Vector2(1, 0.5f);
                        cRT.sizeDelta = Vector2.zero;
                        var cntText = cntObj.AddComponent<TextMeshProUGUI>();
                        cntText.text = item.count.ToString();
                        cntText.fontSize = 10;
                        cntText.color = Color.white;
                        cntText.alignment = TextAlignmentOptions.BottomRight;
                    }
                }
            }
        }

        private void PopulateRegisterActiveListings()
        {
            if (_regActiveListingsScrollContent == null) return;

            // Clear previous items
            foreach (Transform child in _regActiveListingsScrollContent)
            {
                Destroy(child.gameObject);
            }

            string myNameClean = EntropyOnline.Core.GameManager.Instance.CharacterName.Trim('\0', ' ');

            foreach (var listing in _listings)
            {
                string sellerClean = listing.sellerName.Trim('\0', ' ');
                if (sellerClean != myNameClean)
                    continue;

                string itemName = "Unknown Item";
                if (KOInventory.s_pTbl_Items_Basic.TryGetValue((uint)(listing.itemId / 1000 * 1000), out var basic))
                {
                    itemName = basic.szName;
                }

                CreateRegisterListingRowUI(_regActiveListingsScrollContent, listing, itemName);
            }
        }

        private void CreateRegisterListingRowUI(Transform parent, AuctionListingData listing, string itemName)
        {
            var rowObj = new GameObject("RegListingRow_" + listing.id, typeof(RectTransform));
            rowObj.transform.SetParent(parent, false);
            var rowRT = rowObj.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(326, 49); // Shortened to height 49

            // Row Border (Gold) - Higher opacity for better separation
            var rowBorder = rowObj.AddComponent<Image>();
            rowBorder.color = new Color(_colorBorder.r, _colorBorder.g, _colorBorder.b, 0.65f);
            rowBorder.raycastTarget = false;

            // Row Inner Content Area
            var innerRow = new GameObject("Inner", typeof(RectTransform));
            innerRow.transform.SetParent(rowObj.transform, false);
            var innerRT = innerRow.GetComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.sizeDelta = new Vector2(-2, -2); // 1px border around
            innerRow.AddComponent<Image>().color = _colorBtnBg;

            // 1. Icon Slot Box (Column 1) - Aligned to the far left
            var iconBox = new GameObject("IconBox", typeof(RectTransform));
            iconBox.transform.SetParent(innerRow.transform, false);
            var ibRT = iconBox.GetComponent<RectTransform>();
            ibRT.anchorMin = new Vector2(0, 0.5f);
            ibRT.anchorMax = new Vector2(0, 0.5f);
            ibRT.pivot = new Vector2(0, 0.5f);
            ibRT.anchoredPosition = new Vector2(2, 0); // Far left alignment
            ibRT.sizeDelta = new Vector2(45, 45); // 45x45 inventory slot size
            
            var slotImg = iconBox.AddComponent<Image>();
            slotImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemeGlassSlotSprite("slot_socket_glass_v5", 45) : null;
            slotImg.color = Color.white;

            // Colored Border around icon
            int ext = (int)(listing.itemId % 100);
            Color iconBrdColor = new Color(_colorBorder.r, _colorBorder.g, _colorBorder.b, 0.4f);
            if (ext == 7) iconBrdColor = new Color(0.29f, 0.56f, 0.89f, 0.8f); // +7 blue
            else if (ext == 8) iconBrdColor = new Color(0.63f, 0.4f, 1f, 0.8f); // +8 purple
            else if (ext >= 9) iconBrdColor = new Color(1f, 0.29f, 0.29f, 0.8f); // >=+9 red

            var iconBrd = new GameObject("Border", typeof(RectTransform));
            iconBrd.transform.SetParent(iconBox.transform, false);
            iconBrd.GetComponent<RectTransform>().sizeDelta = new Vector2(45, 45);
            var iconBrdImg = iconBrd.AddComponent<Image>();
            iconBrdImg.color = iconBrdColor;
            iconBrdImg.raycastTarget = false;
            iconBrd.transform.SetAsFirstSibling();

            var iconObj = new GameObject("Icon", typeof(RectTransform));
            iconObj.transform.SetParent(iconBox.transform, false);
            var iconRT = iconObj.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.sizeDelta = Vector2.zero;
            var iconImg = iconObj.AddComponent<Image>();
            var sprite = KOItemIconLoader.LoadItemIcon(ResolveIconId((int)listing.itemId));
            if (sprite != null)
            {
                iconImg.sprite = sprite;
            }
            else
            {
                iconImg.color = Color.clear;
            }
            iconImg.preserveAspect = true;

            // 2. Name details
            var nameObj = new GameObject("NameText", typeof(RectTransform));
            nameObj.transform.SetParent(innerRow.transform, false);
            var nameRT = nameObj.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.5f);
            nameRT.anchorMax = new Vector2(0, 0.5f);
            nameRT.pivot = new Vector2(0, 0.5f);
            nameRT.anchoredPosition = new Vector2(55, 0); // After slot width
            nameRT.sizeDelta = new Vector2(110, 40); // Width 110
            var nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = itemName;
            nameText.fontSize = 11;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            // 3. Item Amount
            var amountObj = new GameObject("AmountText", typeof(RectTransform));
            amountObj.transform.SetParent(innerRow.transform, false);
            var amountRT = amountObj.GetComponent<RectTransform>();
            amountRT.anchorMin = new Vector2(0, 0.5f);
            amountRT.anchorMax = new Vector2(0, 0.5f);
            amountRT.pivot = new Vector2(0.5f, 0.5f);
            amountRT.anchoredPosition = new Vector2(185, 0);
            amountRT.sizeDelta = new Vector2(50, 40);
            var amountText = amountObj.AddComponent<TextMeshProUGUI>();
            amountText.text = listing.count.ToString("N0");
            amountText.fontSize = 11;
            amountText.color = _colorTextGold;
            amountText.alignment = TextAlignmentOptions.Center;

            // 4. Buy Price
            var priceObj = new GameObject("PriceText", typeof(RectTransform));
            priceObj.transform.SetParent(innerRow.transform, false);
            var priceRT = priceObj.GetComponent<RectTransform>();
            priceRT.anchorMin = new Vector2(1, 0.5f);
            priceRT.anchorMax = new Vector2(1, 0.5f);
            priceRT.pivot = new Vector2(1, 0.5f);
            priceRT.anchoredPosition = new Vector2(-10, 0); // 10px from right edge
            priceRT.sizeDelta = new Vector2(75, 40);
            var priceText = priceObj.AddComponent<TextMeshProUGUI>();
            float regGb = listing.price / 100000000.0f;
            priceText.text = $"{regGb.ToString("0.##")} GB";
            priceText.fontSize = 11;
            priceText.color = _colorTextGold;
            priceText.alignment = TextAlignmentOptions.Right;
        }

        private void OnInventorySlotSelected(int slotIdx, int itemId, int count, Sprite icon, string name)
        {
            // Highlight selected slot
            foreach (Transform child in _invGridContent)
            {
                child.GetComponent<Image>().color = Color.white;
            }
            _invGridContent.GetChild(slotIdx).GetComponent<Image>().color = new Color(0.9f, 0.7f, 0.2f, 1f);

            // Prompt user directly via CountableEdit dialog
            KOUIManager.Instance?.PromptAHRegisterItem(slotIdx);
        }

        public void RegisterItemDirect(int slotIdx, int itemId, int count, ulong price)
        {
            if (slotIdx == -1 || itemId == 0)
            {
                KOUIManager.Instance?.AddMsgOutput("Please select an item you want to list!", Color.red);
                return;
            }
            if (price < 2100000000UL || price > 50000000000UL)
            {
                KOUIManager.Instance?.AddMsgOutput("Only items priced between 21 GB and 500 GB can be listed in the Auction House!", Color.red);
                return;
            }
            // Send WIZ_MARKET_BBS REGISTER packet
            using (var writer = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS))
            {
                writer.WriteByte(1); // MARKET_BBS_REGISTER
                writer.WriteByte((byte)slotIdx);
                writer.WriteUInt16((ushort)count);
                writer.WriteUInt64(price);
                KONetworkManager.Instance?.SendPacket(writer);
            }
        }
        private void OnBuyItemClicked(uint id, ulong price)
        {
            KOMessageBox.Instance?.ShowYesNo(
                $"Are you sure you want to purchase this item for {(price / 100000000.0f):F2} GB ({price:N0} Noah)?",
                "Auction House Purchase",
                MsgBoxBehavior.BEHAVIOR_NOTHING,
                () =>
                {
                    using (var writer = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS))
                    {
                        writer.WriteByte(5); // MARKET_BBS_REMOTE_PURCHASE
                        writer.WriteUInt32(id);
                        KONetworkManager.Instance?.SendPacket(writer);
                    }
                },
                null
            );
        }

        private void OnCancelListingClicked(uint id)
        {
            KOMessageBox.Instance?.ShowYesNo(
                "Are you sure you want to cancel this listing? The item will be returned to your inventory (or warehouse).",
                "Auction House Cancel Listing",
                MsgBoxBehavior.BEHAVIOR_NOTHING,
                () =>
                {
                    using (var writer = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS))
                    {
                        writer.WriteByte(2); // MARKET_BBS_DELETE
                        writer.WriteUInt32(id);
                        KONetworkManager.Instance?.SendPacket(writer);
                    }
                },
                null
            );
        }

        private void OnClaimGoldClicked()
        {
            using (var writer = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS))
            {
                writer.WriteByte(7); // MARKET_BBS_CLAIM_GOLD
                KONetworkManager.Instance?.SendPacket(writer);
            }
        }

        private void HandleMarketBBSPacket(byte[] data)
        {
            var reader = new KOPacketReader(data);
            byte sub = reader.ReadByte();

            switch (sub)
            {
                case 1: // REGISTER response
                    byte regResult = reader.ReadByte();
                    byte regCode = reader.ReadByte();
                    if (regResult == 1)
                    {
                        KOUIManager.Instance?.AddMsgOutput("Your item has been listed successfully.", Color.green);
                        RequestListingsFromServer();
                    }
                    else
                    {
                        if (regCode == 2)
                            KOUIManager.Instance?.AddMsgOutput("Price must be at least 21 GB (2,100,000,000 Noah)!", Color.red);
                        else
                            KOUIManager.Instance?.AddMsgOutput("Listing failed!", Color.red);
                    }
                    break;

                case 3: // REPORT response
                    uint totalCount = reader.ReadUInt32();
                    ushort inPacket = reader.ReadUInt16();

                    _listings.Clear();
                    for (int i = 0; i < inPacket; i++)
                    {
                        var item = new AuctionListingData();
                        item.id = reader.ReadUInt32();
                        item.itemId = reader.ReadUInt32();
                        item.count = reader.ReadUInt16();
                        item.price = reader.ReadUInt64();
                        item.sellerName = reader.ReadKOString1();
                        item.durability = reader.ReadUInt16();
                        item.itemSerial = reader.ReadUInt64();
                        item.remainingMinutes = reader.ReadUInt32();

                        _listings.Add(item);
                    }

                    PopulateBrowseList();
                    break;

                case 2: // DELETE response
                    byte delResult = reader.ReadByte();
                    byte delCode = reader.ReadByte();
                    if (delResult == 1)
                    {
                        KOUIManager.Instance?.AddMsgOutput("Your listing has been cancelled, item returned.", Color.green);
                        RequestListingsFromServer();
                    }
                    else
                    {
                        KOUIManager.Instance?.AddMsgOutput("Listing cancellation failed!", Color.red);
                    }
                    break;

                case 5: // PURCHASE response
                    byte purResult = reader.ReadByte();
                    if (purResult == 1)
                    {
                        uint purItemId = reader.ReadUInt32();
                        ushort purCount = reader.ReadUInt16();
                        KOUIManager.Instance?.AddMsgOutput("Item purchased successfully!", Color.green);
                        RequestListingsFromServer();
                    }
                    else
                    {
                        KOUIManager.Instance?.AddMsgOutput("Purchase failed!", Color.red);
                    }
                    break;

                case 7: // CLAIM_GOLD response
                    byte claimResult = reader.ReadByte();
                    if (claimResult == 1)
                    {
                        ulong claimed = reader.ReadUInt64();
                        _pendingGold = reader.ReadUInt64();
                        _ahCoinsText.text = _pendingGold.ToString("N0");
                        KOUIManager.Instance?.AddMsgOutput($"{claimed:N0} Noah has been successfully collected.", Color.green);
                    }
                    else
                    {
                        KOUIManager.Instance?.AddMsgOutput("Collection failed!", Color.red);
                    }
                    break;
            }
        }

        private void PopulateBrowseList()
        {
            if (_inventoryModal != null && _inventoryModal.activeSelf)
            {
                PopulateRegisterActiveListings();
            }

            if (_myMerchantModal != null && _myMerchantModal.activeSelf)
            {
                PopulateMyMerchantModal();
            }

            if (_browseScrollContent == null) return;

            // Clear content
            foreach (Transform child in _browseScrollContent)
            {
                Destroy(child.gameObject);
            }

            var filtered = new List<AuctionListingData>();

            foreach (var listing in _listings)
            {
                string sellerClean = listing.sellerName.Trim('\0', ' ');
                string myNameClean = EntropyOnline.Core.GameManager.Instance.CharacterName.Trim('\0', ' ');

                // Filter my items only (for MY MERCHANT view)
                if (_filterMyItemsOnly)
                {
                    if (sellerClean != myNameClean)
                        continue;
                }
                else
                {
                    // Hide my items from general Browse list (cannot buy own items)
                    if (sellerClean == myNameClean)
                        continue;
                }

                // Get item info
                string itemName = "Unknown Item";
                KOTableReader.TableItemBasic basic = null;
                if (KOInventory.s_pTbl_Items_Basic.TryGetValue((uint)(listing.itemId / 1000 * 1000), out var b))
                {
                    basic = b;
                    itemName = basic.szName;
                }

                // Search filter (character/item name query)
                if (!string.IsNullOrEmpty(_filterName))
                {
                    if (!itemName.ToLower().Contains(_filterName.ToLower()) && !listing.sellerName.ToLower().Contains(_filterName.ToLower()))
                        continue;
                }

                // Armor Type filter
                if (_filterArmorType > 0 && basic != null)
                {
                    // 1: Pauldron (UPPER=5), 2: Helmet (HEAD=7), 3: Pad (LOWER=6), 4: Gauntlet (ARM=8), 5: Boot (FOOT=9)
                    int ap = basic.byAttachPoint;
                    if (_filterArmorType == 1 && ap != 5) continue;
                    if (_filterArmorType == 2 && ap != 7) continue;
                    if (_filterArmorType == 3 && ap != 6) continue;
                    if (_filterArmorType == 4 && ap != 8) continue;
                    if (_filterArmorType == 5 && ap != 9) continue;
                }

                // Weapon Type filter
                if (_filterWeaponType > 0 && basic != null)
                {
                    int ap = basic.byAttachPoint;
                    bool isWeapon = (ap >= 0 && ap <= 4);
                    if (!isWeapon && _filterWeaponType != 5) continue; // if filter is shield, allow ap==2 (which is left hand)

                    string nameLower = itemName.ToLower();
                    if (_filterWeaponType == 1) // Dagger
                    {
                        bool isDagger = nameLower.Contains("dagger") || nameLower.Contains("shard") || nameLower.Contains("knife") || nameLower.Contains("kukri") || nameLower.Contains("dirk") || nameLower.Contains("cleaver");
                        if (!isDagger) continue;
                    }
                    else if (_filterWeaponType == 2) // Sword
                    {
                        bool isSword = (nameLower.Contains("sword") || nameLower.Contains("blade") || nameLower.Contains("rapier") || nameLower.Contains("slayer") || nameLower.Contains("mirage") || nameLower.Contains("ii") || nameLower.Contains("lugias")) && !nameLower.Contains("staff");
                        if (!isSword) continue;
                    }
                    else if (_filterWeaponType == 3) // Bow
                    {
                        bool isBow = nameLower.Contains("bow") || nameLower.Contains("crossbow") || nameLower.Contains("windforce");
                        if (!isBow) continue;
                    }
                    else if (_filterWeaponType == 4) // Staff
                    {
                        bool isStaff = nameLower.Contains("staff") || nameLower.Contains("elixir") || nameLower.Contains("woe") || nameLower.Contains("scorching") || nameLower.Contains("oasis");
                        if (!isStaff) continue;
                    }
                    else if (_filterWeaponType == 5) // Shield
                    {
                        bool isShield = ap == 2 && (nameLower.Contains("shield") || nameLower.Contains("defender") || nameLower.Contains("aegis") || nameLower.Contains("plate"));
                        if (!isShield) continue;
                    }
                    else if (_filterWeaponType == 6) // Club/Axe/Spear
                    {
                        bool isOther = nameLower.Contains("spear") || nameLower.Contains("axe") || nameLower.Contains("club") || nameLower.Contains("halberd") || nameLower.Contains("bardish") || nameLower.Contains("totemic") || nameLower.Contains("glave") || nameLower.Contains("raptor") || nameLower.Contains("iron impact") || nameLower.Contains("hell breaker");
                        if (!isOther) continue;
                    }
                }

                filtered.Add(listing);
            }

            // Pagination calculations
            int itemsPerPage = 6;
            _totalPages = Mathf.Max(1, (filtered.Count + itemsPerPage - 1) / itemsPerPage);
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            _pageText.text = $"{_currentPage}/{_totalPages}";

            int startIdx = (_currentPage - 1) * itemsPerPage;
            for (int i = startIdx; i < startIdx + itemsPerPage && i < filtered.Count; i++)
            {
                var listing = filtered[i];
                string itemName = "Unknown Item";
                if (KOInventory.s_pTbl_Items_Basic.TryGetValue((uint)(listing.itemId / 1000 * 1000), out var basic))
                {
                    itemName = basic.szName;
                }
                CreateListingItemUI(_browseScrollContent, listing, itemName, listing.sellerName.Trim('\0', ' ') == EntropyOnline.Core.GameManager.Instance.CharacterName.Trim('\0', ' '));
            }
        }

        private void CreateListingItemUI(Transform parent, AuctionListingData listing, string itemName, bool isOwner)
        {
            var rowObj = new GameObject("ListingRow_" + listing.id, typeof(RectTransform));
            rowObj.transform.SetParent(parent, false);
            var rowRT = rowObj.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0, 58); // slightly tighter row height to look professional

            // Row Border (Gold)
            var rowBorder = rowObj.AddComponent<Image>();
            rowBorder.color = new Color(_colorBorder.r, _colorBorder.g, _colorBorder.b, 0.35f);
            rowBorder.raycastTarget = false;

            // Row Inner Content Area
            var innerRow = new GameObject("Inner", typeof(RectTransform));
            innerRow.transform.SetParent(rowObj.transform, false);
            var innerRT = innerRow.GetComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.sizeDelta = new Vector2(-2, -2); // 1px border around
            innerRow.AddComponent<Image>().color = _colorBtnBg;

            // 1. Icon (Column 1)
            var iconBox = new GameObject("IconBox", typeof(RectTransform));
            iconBox.transform.SetParent(innerRow.transform, false);
            var ibRT = iconBox.GetComponent<RectTransform>();
            ibRT.anchorMin = new Vector2(0, 0.5f);
            ibRT.anchorMax = new Vector2(0, 0.5f);
            ibRT.pivot = new Vector2(0, 0.5f);
            ibRT.anchoredPosition = new Vector2(15, 0);
            ibRT.sizeDelta = new Vector2(40, 40);
            iconBox.AddComponent<Image>().color = _colorInputBg;

            // Colored Border around icon based on upgrade level quality (from screenshot)
            int ext = (int)(listing.itemId % 100);
            Color iconBrdColor = new Color(_colorBorder.r, _colorBorder.g, _colorBorder.b, 0.4f); // default gold/bronze
            if (ext == 7) iconBrdColor = new Color(0.29f, 0.56f, 0.89f, 0.8f); // +7 blue
            else if (ext == 8) iconBrdColor = new Color(0.63f, 0.4f, 1f, 0.8f); // +8 purple
            else if (ext >= 9) iconBrdColor = new Color(1f, 0.29f, 0.29f, 0.8f); // >=+9 red

            var iconBrd = new GameObject("Border", typeof(RectTransform));
            iconBrd.transform.SetParent(iconBox.transform, false);
            iconBrd.GetComponent<RectTransform>().sizeDelta = new Vector2(42, 42);
            var iconBrdImg = iconBrd.AddComponent<Image>();
            iconBrdImg.color = iconBrdColor;
            iconBrdImg.raycastTarget = false;
            iconBrd.transform.SetAsFirstSibling();

            var iconObj = new GameObject("Icon", typeof(RectTransform));
            iconObj.transform.SetParent(iconBox.transform, false);
            var iconRT = iconObj.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.sizeDelta = Vector2.zero;
            var iconImg = iconObj.AddComponent<Image>();
            var sprite = KOItemIconLoader.LoadItemIcon(ResolveIconId((int)listing.itemId));
            if (sprite != null)
            {
                iconImg.sprite = sprite;
            }
            else
            {
                iconImg.color = Color.clear;
            }
            iconImg.preserveAspect = true;

            // 2. Name details (Column 1)
            var detailObj = new GameObject("Details", typeof(RectTransform));
            detailObj.transform.SetParent(innerRow.transform, false);
            var detailRT = detailObj.GetComponent<RectTransform>();
            detailRT.anchorMin = new Vector2(0, 0);
            detailRT.anchorMax = new Vector2(0.35f, 1);
            detailRT.pivot = new Vector2(0, 0.5f);
            detailRT.anchoredPosition = new Vector2(65, 0);
            var detailsText = detailObj.AddComponent<TextMeshProUGUI>();

            // Format name with upgrade level suffix color-coded
            string displayName = itemName;
            if (ext > 0 && ext <= 15)
            {
                string colorCode = "#ffffff"; // +0 to +5
                if (ext == 7) colorCode = "#4a90e2"; // +7 sky blue
                else if (ext == 8) colorCode = "#bf55ec"; // +8 purple
                else if (ext >= 9) colorCode = "#ff4b4b"; // +9 pink/red
                displayName += $" <color={colorCode}>(+{ext})</color>";
            }
            else
            {
                displayName = $"<color=#e5cc99>{displayName}</color>";
            }

            detailsText.text = displayName;
            detailsText.fontSize = 12;
            detailsText.color = Color.white;
            detailsText.alignment = TextAlignmentOptions.Left;

            // 3. Amount (Column 2)
            var amtObj = new GameObject("Amount", typeof(RectTransform));
            amtObj.transform.SetParent(innerRow.transform, false);
            var amtRT = amtObj.GetComponent<RectTransform>();
            amtRT.anchorMin = new Vector2(0.35f, 0);
            amtRT.anchorMax = new Vector2(0.5f, 1);
            amtRT.pivot = new Vector2(0.5f, 0.5f);
            amtRT.sizeDelta = Vector2.zero;
            var amtText = amtObj.AddComponent<TextMeshProUGUI>();
            amtText.text = listing.count.ToString();
            amtText.fontSize = 12;
            amtText.color = Color.white;
            amtText.alignment = TextAlignmentOptions.Center;

            // 4. Expire Time (Column 3)
            var expObj = new GameObject("ExpireTime", typeof(RectTransform));
            expObj.transform.SetParent(innerRow.transform, false);
            var expRT = expObj.GetComponent<RectTransform>();
            expRT.anchorMin = new Vector2(0.5f, 0);
            expRT.anchorMax = new Vector2(0.7f, 1);
            expRT.pivot = new Vector2(0.5f, 0.5f);
            expRT.sizeDelta = Vector2.zero;
            var expText = expObj.AddComponent<TextMeshProUGUI>();
            
            uint mins = listing.remainingMinutes;
            string expireStr = "0 Minute(s)";
            if (mins >= 60)
            {
                uint hrs = mins / 60;
                uint rMins = mins % 60;
                expireStr = rMins > 0 ? $"{hrs} Hour(s) {rMins} Minute(s)" : $"{hrs} Hour(s)";
            }
            else
            {
                expireStr = $"{mins} Minute(s)";
            }

            expText.text = expireStr;
            expText.fontSize = 11;
            expText.color = _colorTextGold;
            expText.alignment = TextAlignmentOptions.Center;

            // 5. Price Display (Column 4)
            var priceObj = new GameObject("Price", typeof(RectTransform));
            priceObj.transform.SetParent(innerRow.transform, false);
            var priceRT = priceObj.GetComponent<RectTransform>();
            priceRT.anchorMin = new Vector2(0.7f, 0);
            priceRT.anchorMax = new Vector2(0.85f, 1);
            priceRT.pivot = new Vector2(0.5f, 0.5f);
            priceRT.sizeDelta = Vector2.zero;
            var priceText = priceObj.AddComponent<TextMeshProUGUI>();
            
            double gbCount = listing.price / 100000000.0;
            priceText.text = $"{listing.price:N0}\n<size=9>({gbCount:F2} GB(s))</size>";
            priceText.fontSize = 11;
            priceText.color = _colorTextGold;
            priceText.alignment = TextAlignmentOptions.Right;

            // Price Gold Coin icon (from screenshot)
            var coinIconObj = new GameObject("CoinIcon", typeof(RectTransform));
            coinIconObj.transform.SetParent(innerRow.transform, false);
            var ciRT = coinIconObj.GetComponent<RectTransform>();
            ciRT.anchorMin = new Vector2(0.85f, 0.5f);
            ciRT.anchorMax = new Vector2(0.85f, 0.5f);
            ciRT.pivot = new Vector2(0.5f, 0.5f);
            ciRT.anchoredPosition = new Vector2(-8, 0);
            ciRT.sizeDelta = new Vector2(10, 10);
            coinIconObj.AddComponent<Image>().color = _colorTextGold;

            // 6. Action Button (BUY or CANCEL)
            var actionBtnObj = new GameObject("ActionBtn", typeof(RectTransform));
            actionBtnObj.transform.SetParent(innerRow.transform, false);
            var actionBtnRT = actionBtnObj.GetComponent<RectTransform>();
            actionBtnRT.anchorMin = new Vector2(1, 0.5f);
            actionBtnRT.anchorMax = new Vector2(1, 0.5f);
            actionBtnRT.pivot = new Vector2(1, 0.5f);
            actionBtnRT.anchoredPosition = new Vector2(-15, 0);
            actionBtnRT.sizeDelta = new Vector2(80, 32);

            var actionImg = actionBtnObj.AddComponent<Image>();
            actionImg.color = isOwner ? _colorRedBtn : _colorBtnBg;

            var actionBrd = new GameObject("Border", typeof(RectTransform));
            actionBrd.transform.SetParent(actionBtnObj.transform, false);
            actionBrd.GetComponent<RectTransform>().sizeDelta = new Vector2(82, 34);
            var actionBrdImg = actionBrd.AddComponent<Image>();
            actionBrdImg.color = isOwner ? _colorRedBorder : _colorBorder;
            actionBrdImg.raycastTarget = false;
            actionBrd.transform.SetAsFirstSibling();

            var actionBtn = actionBtnObj.AddComponent<Button>();

            var actionTxtObj = new GameObject("Text", typeof(RectTransform));
            actionTxtObj.transform.SetParent(actionBtnObj.transform, false);
            var actionTxtRT = actionTxtObj.GetComponent<RectTransform>();
            actionTxtRT.anchorMin = Vector2.zero;
            actionTxtRT.anchorMax = Vector2.one;
            actionTxtRT.sizeDelta = Vector2.zero;
            var actionTxt = actionTxtObj.AddComponent<TextMeshProUGUI>();
            actionTxt.text = isOwner ? "CANCEL" : "BUY";
            actionTxt.fontSize = 11;
            actionTxt.color = isOwner ? Color.white : _colorTextGold;
            actionTxt.fontStyle = FontStyles.Bold;
            actionTxt.alignment = TextAlignmentOptions.Center;

            if (isOwner)
            {
                actionBtn.onClick.AddListener(() => OnCancelListingClicked(listing.id));
            }
            else
            {
                actionBtn.onClick.AddListener(() => OnBuyItemClicked(listing.id, listing.price));
            }
        }

        private static int ResolveIconId(int itemId)
        {
            return KOUIManager.ResolveIconId(itemId);
        }

        public void UpdateScale(float s)
        {
            if (_panelRoot != null && s > 0f)
            {
                var frameTrans = _panelRoot.transform.Find("AH_MainFrame");
                if (frameTrans != null)
                {
                    frameTrans.localScale = new Vector3(1f / s, 1f / s, 1f / s);
                }

                var invModalFrame = _panelRoot.transform.Find("AH_InventoryModal/Frame");
                if (invModalFrame != null)
                {
                    invModalFrame.localScale = new Vector3(1f / s, 1f / s, 1f / s);
                }

                var merchantModalFrame = _panelRoot.transform.Find("AH_MyMerchantModal/Frame");
                if (merchantModalFrame != null)
                {
                    merchantModalFrame.localScale = new Vector3(1f / s, 1f / s, 1f / s);
                }
            }
        }
        private void SetupButtonTransition(Button btn, Image img)
        {
            if (btn == null || img == null) return;
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;
        }
    }

    public class AuctionListingData
    {
        public uint id;
        public uint itemId;
        public ushort count;
        public ulong price;
        public string sellerName;
        public ushort durability;
        public ulong itemSerial;
        public uint remainingMinutes;
    }

}
