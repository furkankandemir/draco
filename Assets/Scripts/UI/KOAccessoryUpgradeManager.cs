using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    public class KOAccessoryUpgradeManager : MonoBehaviour
    {
        public static KOAccessoryUpgradeManager Instance { get; private set; }

        [Header("State")]
        private int _npcId;
        private int _accessorySlot0 = -1; // Index in KOInventory.Instance.m_pMyInvWnd
        private int _accessorySlot1 = -1;
        private int _accessorySlot2 = -1;
        private int _scrollSlot = -1;
        private bool _upgradeInProgress = false;
        private bool _isPreviewActive = false;
        private int _previewItemId = 0;
        private bool _isUpgradeSucceeded = false;
        private int _resultItemId = 0;
        private int _lastBagSlotTapIndex = -1;
        private float _lastBagSlotTapTime = 0f;
        private const float DOUBLE_TAP_THRESHOLD = 0.3f;

        private static readonly System.Collections.Generic.Dictionary<int, int> _uniqueAccessoryUpgradeOffsets = new System.Collections.Generic.Dictionary<int, int>()
        {
            // Earrings (31xxxxxxx)
            { 310110101, 100 }, // Bronze Earring
            { 310110103, 118 }, // Golden Earring
            { 310310004, 427 }, // Mage Earring
            { 310310005, 396 }, // Warrior Earring
            { 310310006, 405 }, // Rogue Earring
            { 310310007, 414 }, // Cleric Earring
            { 310410105, 136 }, // Platinum Earring
            { 310410107, 154 }, // Secret-Silver Earring
            { 310410109, 172 }, // Agate Earring
            { 310510104, 127 }, // Crystal Earring
            { 310510110, 181 }, // Opal Earring
            { 310610102, 109 }, // Silver Earring
            { 310610106, 145 }, // Elf-Metal Earring
            { 310610108, 163 }, // White-Silver Earring

            // Necklaces (32xxxxxxx)
            { 320310121, 240 }, // Amulet of Intelligence
            { 320310122, 249 }, // Amulet of Magic Power
            { 320310124, 267 }, // Elemental Necklace
            { 320310126, 285 }, // Iron Necklace
            { 320310129, 312 }, // Red Dragon Amulet
            { 320310130, 321 }, // Black Dragon Necklace
            { 320410011, 430 }, // Lobo Pendant
            { 320410012, 439 }, // Lupus Pendant
            { 320410013, 448 }, // Lycaon Pendant
            { 320510118, 213 }, // Amulet of Curse
            { 320510120, 231 }, // Amulet of Dexterity
            { 320510123, 258 }, // Amulet of Health
            { 320510131, 330 }, // Green Dragon Amulet
            { 320510132, 339 }, // White Dragon Necklace
            { 320610119, 222 }, // Amulet of Strength
            { 320610125, 276 }, // Crystal Necklace
            { 320610128, 303 }, // Blue Dragon Necklace

            // Rings (33xxxxxxx)
            { 330110255, 46 },  // Ring of Courage
            { 330110258, 73 },  // Opal Ring
            { 330110262, 109 }, // Emerald Ring
            { 330110266, 145 }, // Crystal Ring
            { 330150256, 55 },  // Ring of Magic
            { 330150257, 64 },  // Ring of Life
            { 330310014, 457 }, // Kekuri Ring
            { 330410261, 100 }, // Diamond Ring
            { 330410267, 154 }, // Platinum Ring
            { 330610259, 82 },  // Agate Ring
            { 330610260, 91 },  // Ruby Ring
            { 330610263, 118 }, // Gold Ring
            { 330610264, 127 }, // Silver Ring
            { 330610265, 136 }, // Elf Ring

            // Belts (34xxxxxxx)
            { 340110101, 210 }, // Belt of Life
            { 340110114, 327 }, // Belt of Curse
            { 340110255, 46 },  // Kekuri Belt
            { 340310102, 219 }, // Mana Belt
            { 340310103, 228 }, // Fire Belt
            { 340310108, 273 }, // Bronze Belt
            { 340410104, 237 }, // Ice Belt
            { 340410109, 282 }, // Glass Belt
            { 340410113, 318 }, // Elf Belt
            { 340410115, 336 }, // Skeleton Belt
            { 340510105, 246 }, // Lightning Belt
            { 340510110, 291 }, // Belt of Strength
            { 340510112, 309 }, // Belt of Intelligence
            { 340610106, 255 }, // Crystal belt
            { 340610107, 264 }, // Iron Belt
            { 340610111, 300 }, // Belt of Dexterity
            { 340610116, 345 }  // Helt of Harpy
        };

        public bool IsPreviewActive
        {
            get => _isPreviewActive;
            set => _isPreviewActive = value;
        }

        public int PreviewResultItemId
        {
            get => _previewItemId;
            set => _previewItemId = value;
        }

        public bool IsUpgradeInProgress
        {
            get => _upgradeInProgress;
            set => _upgradeInProgress = value;
        }

        [Header("UI Bindings")]
        private Transform _panelRoot;
        private Image _imgAccessory0;
        private Image _imgAccessory1;
        private Image _imgAccessory2;
        private Image _imgScroll;
        private Image _imgResult;
        private Text _textGold;
        private Text _textNeedCoins;
        private Text _textUpgradeRate;
        private Button _btnOk;
        private Button _btnCancel;
        private Button _btnClose;
        private Button _btnPreview;
        private GameObject _needCoinsContainer;
        private GameObject _upgradeRateContainer;

        private List<GameObject> _inventoryIcons = new List<GameObject>();
        private List<GameObject> _anvilIcons = new List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Initialize(Transform panelRoot, int npcId)
        {
            _panelRoot = panelRoot;
            _npcId = npcId;

            // Bind UI elements using KOUIRenderer.FindChildByID and GetComponent
            _imgAccessory0 = FindImage(panelRoot, "a_upgrade_0");
            _imgAccessory1 = FindImage(panelRoot, "a_upgrade_1");
            _imgAccessory2 = FindImage(panelRoot, "a_upgrade_2");
            _imgScroll = FindImage(panelRoot, "a_m_0");
            _imgResult = FindImage(panelRoot, "a_result");

            _textGold = KOUIRenderer.FindChildText(panelRoot, "text_gold");

            _btnOk = KOUIRenderer.FindChildButton(panelRoot, "btn_ok");
            _btnCancel = KOUIRenderer.FindChildButton(panelRoot, "btn_cancel");
            _btnClose = KOUIRenderer.FindChildButton(panelRoot, "btn_close");
            _btnPreview = KOUIRenderer.FindChildButton(panelRoot, "btn_conversation");

            // Register button events
            if (_btnOk != null)
            {
                _btnOk.onClick.RemoveAllListeners();
                _btnOk.onClick.AddListener(OnUpgradeClicked);
            }

            if (_btnCancel != null)
            {
                _btnCancel.onClick.RemoveAllListeners();
                _btnCancel.onClick.AddListener(ResetSlots);
            }

            if (_btnClose != null)
            {
                _btnClose.onClick.RemoveAllListeners();
                _btnClose.onClick.AddListener(() => KOUIManager.Instance.ShowRingUpgrade(false));
            }

            if (_btnPreview != null)
            {
                _btnPreview.onClick.RemoveAllListeners();
                _btnPreview.onClick.AddListener(OnPreviewButtonClicked);
            }

            // Modernize visual layout
            ModernizePanel();

            // Style the slot areas to have a premium slot border
            StyleSlotArea("a_upgrade_0", 45, 45);
            StyleSlotArea("a_upgrade_1", 45, 45);
            StyleSlotArea("a_upgrade_2", 45, 45);
            StyleSlotArea("a_m_0", 45, 45);
            StyleSlotArea("a_result", 45, 45);

            for (int i = 0; i < 28; i++)
            {
                StyleSlotArea($"a_slot_{i}", 40, 40);
            }

            // Dynamically attach drop handlers to slot areas
            SetupDropTarget("a_upgrade_0", SlotType.Accessory0);
            SetupDropTarget("a_upgrade_1", SlotType.Accessory1);
            SetupDropTarget("a_upgrade_2", SlotType.Accessory2);
            SetupDropTarget("a_m_0", SlotType.Scroll);

            for (int i = 0; i < 28; i++)
            {
                SetupDropTarget($"a_slot_{i}", SlotType.Inventory, i);
            }

            ForceEnableUIComponents(_panelRoot);

            ResetSlots();
        }

        private void ForceEnableUIComponents(Transform root)
        {
            if (root == null) return;

            var img = root.GetComponent<Image>();
            if (img != null) img.enabled = true;

            var raw = root.GetComponent<RawImage>();
            if (raw != null) raw.enabled = true;

            var txt = root.GetComponent<Text>();
            if (txt != null) txt.enabled = true;

            var btn = root.GetComponent<Button>();
            if (btn != null) btn.enabled = true;

            for (int i = 0; i < root.childCount; i++)
            {
                ForceEnableUIComponents(root.GetChild(i));
            }
        }

        private Image FindImage(Transform root, string id)
        {
            var trans = KOUIRenderer.FindChildByID(root, id);
            return trans != null ? trans.GetComponent<Image>() : null;
        }

        private void ModernizePanel()
        {
            if (_panelRoot == null)
            {
                _panelRoot = transform;
            }
            if (KOUIManager.Instance == null) return;

            // 1. Hide the old background slices and UI elements
            string[] oldBgs = new string[] {
                "ui_Image_ED02E664", "ui_Image_46A180CC", "ui_Image_8DF67D24", "ui_Image_804F217C", "ui_Image_1CA075D4",
                "ui_Image_D54B797C", "img_upgrade", "img_inven01", "img_result"
            };
            foreach (var bgName in oldBgs)
            {
                var child = _panelRoot.Find(bgName);
                if (child != null) child.gameObject.SetActive(false);
            }

            // 2. Set modern background on the root panel
            var bgImg = _panelRoot.GetComponent<Image>();
            if (bgImg == null) bgImg = _panelRoot.gameObject.AddComponent<Image>();
            bgImg.sprite = KOUIManager.Instance.GetSkillThemePanelBgSprite("ring_upgrade_custom_bg", 368, 540, 0,
                new Color(0.12f, 0.10f, 0.08f, 0.98f),
                new Color(0.04f, 0.04f, 0.04f, 0.98f),
                new Color(0.6f, 0.48f, 0.22f, 0.9f),
                2);
            bgImg.color = Color.white;
            bgImg.enabled = true;

            var bgRt = _panelRoot.GetComponent<RectTransform>();
            if (bgRt != null)
            {
                bgRt.sizeDelta = new Vector2(368f, 540f);
            }

            // Create a title bar or title text at the top
            var titleGo = _panelRoot.Find("ModernTitleText")?.gameObject;
            if (titleGo == null)
            {
                titleGo = new GameObject("ModernTitleText", typeof(RectTransform));
                titleGo.transform.SetParent(_panelRoot, false);
            }
            var titleRt = titleGo.GetComponent<RectTransform>();
            if (titleRt != null)
            {
                titleRt.anchorMin = new Vector2(0.5f, 1f);
                titleRt.anchorMax = new Vector2(0.5f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.sizeDelta = new Vector2(300f, 30f);
                titleRt.anchoredPosition = new Vector2(0f, -14f);
            }

            var titleTxt = titleGo.GetComponent<Text>() ?? titleGo.AddComponent<Text>();
            titleTxt.text = "ACCESSORY UPGRADE";
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(0.95f, 0.85f, 0.35f, 1f);
            titleTxt.fontSize = 14;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.font = KOUIManager.Instance.GetSafeFont(14);

            // Reposition scroll slot (a_m_0) and result slot (a_result) to match the normal upgrade panel positions
            var rtScroll = KOUIRenderer.FindChildByID(_panelRoot, "a_m_0")?.GetComponent<RectTransform>();
            if (rtScroll != null)
            {
                rtScroll.anchorMin = new Vector2(0f, 1f);
                rtScroll.anchorMax = new Vector2(0f, 1f);
                rtScroll.pivot = new Vector2(0f, 1f);
                rtScroll.sizeDelta = new Vector2(45f, 45f);
                rtScroll.anchoredPosition = new Vector2(46f, -114f);
            }

            var rtResult = KOUIRenderer.FindChildByID(_panelRoot, "a_result")?.GetComponent<RectTransform>();
            if (rtResult != null)
            {
                rtResult.anchorMin = new Vector2(0f, 1f);
                rtResult.anchorMax = new Vector2(0f, 1f);
                rtResult.pivot = new Vector2(0f, 1f);
                rtResult.sizeDelta = new Vector2(45f, 45f);
                rtResult.anchoredPosition = new Vector2(279f, -114f);
            }

            // Vertically center the three accessory slots inside the frame (shifted up by 16px to align perfectly)
            var rtUp0 = KOUIRenderer.FindChildByID(_panelRoot, "a_upgrade_0")?.GetComponent<RectTransform>();
            if (rtUp0 != null)
            {
                rtUp0.anchorMin = new Vector2(0f, 1f);
                rtUp0.anchorMax = new Vector2(0f, 1f);
                rtUp0.pivot = new Vector2(0f, 1f);
                rtUp0.sizeDelta = new Vector2(45f, 45f);
                rtUp0.anchoredPosition = new Vector2(164f, -84f);
            }

            var rtUp1 = KOUIRenderer.FindChildByID(_panelRoot, "a_upgrade_1")?.GetComponent<RectTransform>();
            if (rtUp1 != null)
            {
                rtUp1.anchorMin = new Vector2(0f, 1f);
                rtUp1.anchorMax = new Vector2(0f, 1f);
                rtUp1.pivot = new Vector2(0f, 1f);
                rtUp1.sizeDelta = new Vector2(45f, 45f);
                rtUp1.anchoredPosition = new Vector2(125f, -146f);
            }

            var rtUp2 = KOUIRenderer.FindChildByID(_panelRoot, "a_upgrade_2")?.GetComponent<RectTransform>();
            if (rtUp2 != null)
            {
                rtUp2.anchorMin = new Vector2(0f, 1f);
                rtUp2.anchorMax = new Vector2(0f, 1f);
                rtUp2.pivot = new Vector2(0f, 1f);
                rtUp2.sizeDelta = new Vector2(45f, 45f);
                rtUp2.anchoredPosition = new Vector2(198f, -146f);
            }

            // 3. Top slots background panel
            var topBgGo = _panelRoot.Find("ModernTopSlotsBg")?.gameObject;
            if (topBgGo == null)
            {
                topBgGo = new GameObject("ModernTopSlotsBg", typeof(RectTransform));
                topBgGo.transform.SetParent(_panelRoot, false);
            }
            var topBgRt = topBgGo.GetComponent<RectTransform>();
            if (topBgRt != null)
            {
                topBgRt.anchorMin = new Vector2(0, 1);
                topBgRt.anchorMax = new Vector2(0, 1);
                topBgRt.pivot = new Vector2(0, 1);
                topBgRt.sizeDelta = new Vector2(325f, 176f);
                topBgRt.anchoredPosition = new Vector2(22f, -50f);
            }

            var topBgImg = topBgGo.GetComponent<Image>() ?? topBgGo.AddComponent<Image>();
            topBgImg.sprite = KOUIManager.Instance.GetSkillThemePanelBgSprite("ring_upgrade_top_bg", 325, 176, 0,
                new Color(0.08f, 0.07f, 0.06f, 0.95f),
                new Color(0.03f, 0.03f, 0.03f, 0.95f),
                new Color(0.6f, 0.48f, 0.22f, 0.9f),
                1);
            topBgImg.color = Color.white;
            topBgImg.enabled = true;

            // 4. Need Coins Container (X=68.5f, Y=-168f)
            var needCoinsContainerGo = _panelRoot.Find("NeedCoinsContainer")?.gameObject;
            if (needCoinsContainerGo == null)
            {
                needCoinsContainerGo = new GameObject("NeedCoinsContainer", typeof(RectTransform));
                needCoinsContainerGo.transform.SetParent(_panelRoot, false);
            }
            _needCoinsContainer = needCoinsContainerGo;
            var needCoinsContainerRt = needCoinsContainerGo.GetComponent<RectTransform>();
            if (needCoinsContainerRt != null)
            {
                needCoinsContainerRt.anchorMin = new Vector2(0, 1);
                needCoinsContainerRt.anchorMax = new Vector2(0, 1);
                needCoinsContainerRt.pivot = new Vector2(0.5f, 1f);
                needCoinsContainerRt.sizeDelta = new Vector2(80f, 45f);
                needCoinsContainerRt.anchoredPosition = new Vector2(68.5f, -168f);
            }

            var coinLabelGo = needCoinsContainerGo.transform.Find("Label")?.gameObject;
            if (coinLabelGo == null)
            {
                coinLabelGo = new GameObject("Label", typeof(RectTransform));
                coinLabelGo.transform.SetParent(needCoinsContainerGo.transform, false);
            }
            var coinLabelRt = coinLabelGo.GetComponent<RectTransform>();
            if (coinLabelRt != null)
            {
                coinLabelRt.anchorMin = new Vector2(0.5f, 1f);
                coinLabelRt.anchorMax = new Vector2(0.5f, 1f);
                coinLabelRt.pivot = new Vector2(0.5f, 1f);
                coinLabelRt.sizeDelta = new Vector2(80f, 15f);
                coinLabelRt.anchoredPosition = new Vector2(0f, 0f);
            }

            var coinLabelTxt = coinLabelGo.GetComponent<Text>() ?? coinLabelGo.AddComponent<Text>();
            coinLabelTxt.text = "Need Coins";
            coinLabelTxt.alignment = TextAnchor.MiddleCenter;
            coinLabelTxt.color = Color.white;
            coinLabelTxt.fontSize = 11;
            coinLabelTxt.fontStyle = FontStyle.Bold;
            coinLabelTxt.font = KOUIManager.Instance.GetSafeFont(11);
            coinLabelTxt.enabled = true;

            var coinValBgGo = needCoinsContainerGo.transform.Find("ValueBg")?.gameObject;
            if (coinValBgGo == null)
            {
                coinValBgGo = new GameObject("ValueBg", typeof(RectTransform));
                coinValBgGo.transform.SetParent(needCoinsContainerGo.transform, false);
            }
            var coinValBgRt = coinValBgGo.GetComponent<RectTransform>();
            if (coinValBgRt != null)
            {
                coinValBgRt.anchorMin = new Vector2(0.5f, 1f);
                coinValBgRt.anchorMax = new Vector2(0.5f, 1f);
                coinValBgRt.pivot = new Vector2(0.5f, 1f);
                coinValBgRt.sizeDelta = new Vector2(70f, 21f);
                coinValBgRt.anchoredPosition = new Vector2(0f, -18f);
            }

            var coinValBgImg = coinValBgGo.GetComponent<Image>() ?? coinValBgGo.AddComponent<Image>();
            coinValBgImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("ring_upgrade_rate_val_bg", 560, 168, 64,
                new Color(0.05f, 0.05f, 0.05f, 0.9f),
                new Color(0.6f, 0.48f, 0.22f, 0.8f),
                8);
            coinValBgImg.color = Color.white;
            coinValBgImg.enabled = true;

            var coinValGo = coinValBgGo.transform.Find("ValueText")?.gameObject;
            if (coinValGo == null)
            {
                coinValGo = new GameObject("ValueText", typeof(RectTransform));
                coinValGo.transform.SetParent(coinValBgGo.transform, false);
            }
            var coinValRt = coinValGo.GetComponent<RectTransform>();
            if (coinValRt != null)
            {
                coinValRt.anchorMin = Vector2.zero;
                coinValRt.anchorMax = Vector2.one;
                coinValRt.sizeDelta = Vector2.zero;
                coinValRt.anchoredPosition = Vector2.zero;
            }

            _textNeedCoins = coinValGo.GetComponent<Text>() ?? coinValGo.AddComponent<Text>();
            _textNeedCoins.text = "-";
            _textNeedCoins.alignment = TextAnchor.MiddleCenter;
            _textNeedCoins.color = new Color(0.95f, 0.82f, 0.55f, 1f);
            _textNeedCoins.fontSize = 11;
            _textNeedCoins.fontStyle = FontStyle.Bold;
            _textNeedCoins.font = KOUIManager.Instance.GetSafeFont(11);

            // 5. Upgrade Rate Container (X=301.5f, Y=-168f)
            var rateContainerGo = _panelRoot.Find("UpgradeRateContainer")?.gameObject;
            if (rateContainerGo == null)
            {
                rateContainerGo = new GameObject("UpgradeRateContainer", typeof(RectTransform));
                rateContainerGo.transform.SetParent(_panelRoot, false);
            }
            _upgradeRateContainer = rateContainerGo;
            var rateContainerRt = rateContainerGo.GetComponent<RectTransform>();
            if (rateContainerRt != null)
            {
                rateContainerRt.anchorMin = new Vector2(0, 1);
                rateContainerRt.anchorMax = new Vector2(0, 1);
                rateContainerRt.pivot = new Vector2(0.5f, 1f);
                rateContainerRt.sizeDelta = new Vector2(80f, 45f);
                rateContainerRt.anchoredPosition = new Vector2(301.5f, -168f);
            }

            var rateLabelGo = rateContainerGo.transform.Find("Label")?.gameObject;
            if (rateLabelGo == null)
            {
                rateLabelGo = new GameObject("Label", typeof(RectTransform));
                rateLabelGo.transform.SetParent(rateContainerGo.transform, false);
            }
            var rateLabelRt = rateLabelGo.GetComponent<RectTransform>();
            if (rateLabelRt != null)
            {
                rateLabelRt.anchorMin = new Vector2(0.5f, 1f);
                rateLabelRt.anchorMax = new Vector2(0.5f, 1f);
                rateLabelRt.pivot = new Vector2(0.5f, 1f);
                rateLabelRt.sizeDelta = new Vector2(80f, 15f);
                rateLabelRt.anchoredPosition = new Vector2(0f, 0f);
            }

            var rateLabelTxt = rateLabelGo.GetComponent<Text>() ?? rateLabelGo.AddComponent<Text>();
            rateLabelTxt.text = "Upgrade Rate";
            rateLabelTxt.alignment = TextAnchor.MiddleCenter;
            rateLabelTxt.color = Color.white;
            rateLabelTxt.fontSize = 11;
            rateLabelTxt.fontStyle = FontStyle.Bold;
            rateLabelTxt.font = KOUIManager.Instance.GetSafeFont(11);

            var rateValBgGo = rateContainerGo.transform.Find("ValueBg")?.gameObject;
            if (rateValBgGo == null)
            {
                rateValBgGo = new GameObject("ValueBg", typeof(RectTransform));
                rateValBgGo.transform.SetParent(rateContainerGo.transform, false);
            }
            var rateValBgRt = rateValBgGo.GetComponent<RectTransform>();
            if (rateValBgRt != null)
            {
                rateValBgRt.anchorMin = new Vector2(0.5f, 1f);
                rateValBgRt.anchorMax = new Vector2(0.5f, 1f);
                rateValBgRt.pivot = new Vector2(0.5f, 1f);
                rateValBgRt.sizeDelta = new Vector2(70f, 21f);
                rateValBgRt.anchoredPosition = new Vector2(0f, -18f);
            }

            var rateValBgImg = rateValBgGo.GetComponent<Image>() ?? rateValBgGo.AddComponent<Image>();
            rateValBgImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("ring_upgrade_rate_val_bg", 560, 168, 64,
                new Color(0.05f, 0.05f, 0.05f, 0.9f),
                new Color(0.6f, 0.48f, 0.22f, 0.8f),
                8);
            rateValBgImg.color = Color.white;
            rateValBgImg.enabled = true;

            var rateValGo = rateValBgGo.transform.Find("ValueText")?.gameObject;
            if (rateValGo == null)
            {
                rateValGo = new GameObject("ValueText", typeof(RectTransform));
                rateValGo.transform.SetParent(rateValBgGo.transform, false);
            }
            var rateValRt = rateValGo.GetComponent<RectTransform>();
            if (rateValRt != null)
            {
                rateValRt.anchorMin = Vector2.zero;
                rateValRt.anchorMax = Vector2.one;
                rateValRt.sizeDelta = Vector2.zero;
                rateValRt.anchoredPosition = Vector2.zero;
            }

            _textUpgradeRate = rateValGo.GetComponent<Text>() ?? rateValGo.AddComponent<Text>();
            _textUpgradeRate.text = "-";
            _textUpgradeRate.alignment = TextAnchor.MiddleCenter;
            _textUpgradeRate.color = new Color(0.95f, 0.82f, 0.55f, 1f);
            _textUpgradeRate.fontSize = 11;
            _textUpgradeRate.fontStyle = FontStyle.Bold;
            _textUpgradeRate.font = KOUIManager.Instance.GetSafeFont(11);

            // 6. Hide old success/fail flipflop animations
            for (int i = 0; i < 19; i++)
            {
                var imgSuccess = KOUIRenderer.FindChildByID(_panelRoot, $"img_s_load_{i}");
                if (imgSuccess != null) imgSuccess.gameObject.SetActive(false);

                var imgFail = KOUIRenderer.FindChildByID(_panelRoot, $"img_f_load_{i}");
                if (imgFail != null) imgFail.gameObject.SetActive(false);
            }

            // Cover covers if active
            var cover1 = KOUIRenderer.FindChildByID(_panelRoot, "img_cover_01");
            if (cover1 != null) cover1.gameObject.SetActive(false);
            var cover2 = KOUIRenderer.FindChildByID(_panelRoot, "img_cover_02");
            if (cover2 != null) cover2.gameObject.SetActive(false);

            // 7. Re-arrange and modernize the inventory slots (a_slot_0..27)
            for (int i = 0; i < 28; i++)
            {
                var slotTrans = _panelRoot.Find($"a_slot_{i}");
                if (slotTrans != null)
                {
                    var rtSlot = slotTrans.GetComponent<RectTransform>();
                    rtSlot.anchorMin = new Vector2(0f, 1f);
                    rtSlot.anchorMax = new Vector2(0f, 1f);
                    rtSlot.pivot = new Vector2(0f, 1f);
                    rtSlot.sizeDelta = new Vector2(45f, 45f);

                    float slotX = 16f + (i % 7) * 48.5f;
                    float slotY = -330f - (i / 7) * 49.5f;
                    rtSlot.anchoredPosition = new Vector2(slotX, slotY);
                }

                var countTrans = _panelRoot.Find($"s_count_{i}");
                if (countTrans != null)
                {
                    var txt = countTrans.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.font = KOUIManager.Instance.GetSafeFont(10);
                        txt.fontStyle = FontStyle.Bold;
                    }

                    var cntRT = countTrans.GetComponent<RectTransform>();
                    cntRT.anchorMin = new Vector2(0f, 1f);
                    cntRT.anchorMax = new Vector2(0f, 1f);
                    cntRT.pivot = new Vector2(1f, 0f);
                    cntRT.sizeDelta = new Vector2(40f, 15f);

                    float slotX = 16f + (i % 7) * 48.5f;
                    float slotY = -330f - (i / 7) * 49.5f;
                    cntRT.anchoredPosition = new Vector2(slotX + 45f, slotY - 45f);
                }
            }

            // 8. Style buttons (btn_ok, btn_cancel, btn_conversation, btn_close)
            Color greenBg = new Color(0.12f, 0.28f, 0.12f, 0.95f);
            Color greenBorder = new Color(0.25f, 0.55f, 0.25f, 0.95f);

            Color redBg = new Color(0.45f, 0.05f, 0.08f, 0.95f);
            Color redBorder = new Color(0.75f, 0.15f, 0.15f, 0.95f);

            Color greyBg = new Color(0.20f, 0.18f, 0.16f, 0.95f);
            Color greyBorder = new Color(0.40f, 0.36f, 0.32f, 0.95f);

            StyleButtonSafely(_btnOk, "ring_upgrade_btn_ok", "UPGRADE", greenBg, greenBorder, 104, 26, new Vector2(-112f, -248f));
            StyleButtonSafely(_btnCancel, "ring_upgrade_btn_cancel", "CANCEL", redBg, redBorder, 104, 26, new Vector2(0f, -248f));
            StyleButtonSafely(_btnPreview, "ring_upgrade_btn_preview", "PREVIEW", greyBg, greyBorder, 104, 26, new Vector2(112f, -248f));

            if (_btnOk != null) _btnOk.enabled = true;
            if (_btnCancel != null) _btnCancel.enabled = true;
            if (_btnPreview != null) _btnPreview.enabled = true;

            // Style close button (red square with X)
            if (_btnClose != null)
            {
                var rtClose = _btnClose.GetComponent<RectTransform>();
                if (rtClose != null)
                {
                    rtClose.anchorMin = new Vector2(0.5f, 1f);
                    rtClose.anchorMax = new Vector2(0.5f, 1f);
                    rtClose.pivot = new Vector2(0.5f, 1f);
                    rtClose.anchoredPosition = new Vector2(169f, -8f);
                    rtClose.sizeDelta = new Vector2(22f, 22f);
                }

                Texture2D closeTex = new Texture2D(22, 22, TextureFormat.RGBA32, false);
                Color redCol = new Color(0.45f, 0.05f, 0.08f, 0.85f);
                Color closeBorder = new Color(0.65f, 0.15f, 0.15f, 0.90f);
                for (int y = 0; y < 22; y++)
                {
                    for (int x = 0; x < 22; x++)
                    {
                        if (x < 1 || x >= 22 - 1 || y < 1 || y >= 22 - 1)
                            closeTex.SetPixel(x, y, closeBorder);
                        else
                            closeTex.SetPixel(x, y, redCol);
                    }
                }
                closeTex.Apply();

                var rawClose = _btnClose.GetComponent<RawImage>();
                if (rawClose != null) DestroyImmediate(rawClose);

                var imgClose = _btnClose.GetComponent<Image>() ?? _btnClose.gameObject.AddComponent<Image>();
                imgClose.sprite = Sprite.Create(closeTex, new Rect(0, 0, 22, 22), new Vector2(0.5f, 0.5f));
                imgClose.color = Color.white;

                // Setup press transition visual feedback
                _btnClose.transition = Selectable.Transition.ColorTint;
                _btnClose.targetGraphic = imgClose;
                var closeColors = _btnClose.colors;
                closeColors.normalColor = Color.white;
                closeColors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                closeColors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                closeColors.selectedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                closeColors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                _btnClose.colors = closeColors;

                // Add or find "X" label inside close button
                var xLabelTr = _btnClose.transform.Find("XLabel");
                var xGo = xLabelTr != null ? xLabelTr.gameObject : null;
                if (xGo == null)
                {
                    xGo = new GameObject("XLabel", typeof(RectTransform));
                    xGo.transform.SetParent(_btnClose.transform, false);
                }
                var xRt = xGo.GetComponent<RectTransform>();
                if (xRt != null)
                {
                    xRt.anchorMin = Vector2.zero;
                    xRt.anchorMax = Vector2.one;
                    xRt.offsetMin = Vector2.zero;
                    xRt.offsetMax = Vector2.zero;
                    xRt.anchoredPosition = Vector2.zero;
                }

                var xTxt = xGo.GetComponent<Text>() ?? xGo.AddComponent<Text>();
                xTxt.text = "X";
                xTxt.alignment = TextAnchor.MiddleCenter;
                xTxt.color = Color.white;
                xTxt.fontSize = 11;
                xTxt.fontStyle = FontStyle.Bold;
                xTxt.font = KOUIManager.Instance.GetSafeFont(11);
            }

            // 9. Modernize gold text and align with slot 6 (top-right slot in the inventory area, which is at x = 307, y = -330)
            if (_textGold != null)
            {
                _textGold.font = KOUIManager.Instance.GetSafeFont(12);
                _textGold.fontStyle = FontStyle.Bold;
                _textGold.color = new Color(0.92f, 0.80f, 0.52f, 1f);
                _textGold.alignment = TextAnchor.MiddleCenter;

                float slotX = 307f;
                float slotY = -330f;
                float capsuleX = slotX + 40f;
                float capsuleY = slotY + 20f;

                var capsuleObj = _panelRoot.Find("GoldCapsule")?.gameObject;
                if (capsuleObj == null)
                {
                    capsuleObj = new GameObject("GoldCapsule", typeof(RectTransform));
                    capsuleObj.transform.SetParent(_panelRoot, false);
                }
                capsuleObj.transform.SetSiblingIndex(_textGold.transform.GetSiblingIndex());

                var capsuleImg = capsuleObj.GetComponent<Image>() ?? capsuleObj.AddComponent<Image>();
                capsuleImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("upgrade_gold_capsule_130", 130, 22, 4,
                    new Color(0.08f, 0.08f, 0.08f, 0.6f),
                    new Color(0.45f, 0.35f, 0.15f, 0.8f),
                    1);
                capsuleImg.color = Color.white;
                capsuleImg.raycastTarget = false;
                capsuleImg.enabled = true;

                var capsuleRT = capsuleObj.GetComponent<RectTransform>();
                if (capsuleRT != null)
                {
                    capsuleRT.anchorMin = new Vector2(0f, 1f);
                    capsuleRT.anchorMax = new Vector2(0f, 1f);
                    capsuleRT.pivot = new Vector2(1f, 0.5f);
                    capsuleRT.sizeDelta = new Vector2(130f, 22f);
                    capsuleRT.anchoredPosition = new Vector2(capsuleX, capsuleY);
                }

                var iconObj = capsuleObj.transform.Find("GoldIcon")?.gameObject;
                if (iconObj == null)
                {
                    iconObj = new GameObject("GoldIcon", typeof(RectTransform));
                    iconObj.transform.SetParent(capsuleObj.transform, false);
                }
                var iconImg = iconObj.GetComponent<RawImage>() ?? iconObj.AddComponent<RawImage>();
                KOUIManager.Instance.ConfigureGoldIcon(iconImg);
                iconImg.raycastTarget = false;

                var iconRT = iconObj.GetComponent<RectTransform>();
                if (iconRT != null)
                {
                    iconRT.anchorMin = new Vector2(0f, 0.5f);
                    iconRT.anchorMax = new Vector2(0f, 0.5f);
                    iconRT.pivot = new Vector2(0f, 0.5f);
                    iconRT.sizeDelta = new Vector2(14f, 14f);
                    iconRT.anchoredPosition = new Vector2(8f, 0f);
                }

                _textGold.transform.SetParent(capsuleObj.transform, false);
                var textRT = _textGold.GetComponent<RectTransform>();
                if (textRT != null)
                {
                    textRT.anchorMin = Vector2.zero;
                    textRT.anchorMax = Vector2.one;
                    textRT.pivot = new Vector2(0.5f, 0.5f);
                    textRT.offsetMin = new Vector2(24f, 0f);
                    textRT.offsetMax = new Vector2(-8f, 0f);
                    textRT.anchoredPosition = Vector2.zero;
                }
            }

            // Move ModernTopSlotsBg to the back of the slots so it acts as background
            if (topBgGo != null)
            {
                topBgGo.transform.SetAsFirstSibling();
            }
        }

        private void StyleButtonSafely(Button btn, string themeKey, string btnText, Color fill, Color border, int w, int h, Vector2 pos)
        {
            if (btn == null || KOUIManager.Instance == null) return;

            var rtBtn = btn.GetComponent<RectTransform>();
            if (rtBtn != null)
            {
                rtBtn.anchorMin = new Vector2(0.5f, 1f);
                rtBtn.anchorMax = new Vector2(0.5f, 1f);
                rtBtn.pivot = new Vector2(0.5f, 1f);
                rtBtn.sizeDelta = new Vector2(w, h);
                rtBtn.anchoredPosition = pos;
            }

            // Remove RawImage if exists to prevent DisallowMultipleComponent conflicts
            var raw = btn.GetComponent<RawImage>();
            if (raw != null) DestroyImmediate(raw);

            var img = btn.GetComponent<Image>();
            if (img == null) img = btn.gameObject.AddComponent<Image>();

            // Generate button background texture
            int width = w;
            int height = h;
            Texture2D btnTex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1)
                        btnTex.SetPixel(x, y, border);
                    else
                        btnTex.SetPixel(x, y, fill);
                }
            }
            btnTex.Apply();

            img.sprite = Sprite.Create(btnTex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
            img.color = Color.white;
            img.enabled = true;

            // Setup press transition visual feedback
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            btn.colors = colors;

            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null && !string.IsNullOrEmpty(btnText))
            {
                txt.text = btnText;
                txt.font = KOUIManager.Instance.GetSafeFont(11);
                txt.color = Color.white;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.enabled = true;

                var rtTxt = txt.GetComponent<RectTransform>();
                if (rtTxt != null)
                {
                    rtTxt.anchorMin = Vector2.zero;
                    rtTxt.anchorMax = Vector2.one;
                    rtTxt.pivot = new Vector2(0.5f, 0.5f);
                    rtTxt.offsetMin = Vector2.zero;
                    rtTxt.offsetMax = Vector2.zero;
                    rtTxt.anchoredPosition = Vector2.zero;
                }
            }
        }

        private void StyleSlotArea(string goName, int w, int h)
        {
            var trans = KOUIRenderer.FindChildByID(_panelRoot, goName);
            if (trans == null || KOUIManager.Instance == null) return;

            // Remove RawImage if exists to prevent DisallowMultipleComponent conflicts
            var raw = trans.GetComponent<RawImage>();
            if (raw != null) DestroyImmediate(raw);

            var img = trans.GetComponent<Image>();
            if (img == null) img = trans.gameObject.AddComponent<Image>();

            img.sprite = KOUIManager.Instance.GetSkillThemeGlassSlotSprite("slot_socket_glass_v5", 45);
            img.color = Color.white;
            img.enabled = true;
        }

        private void ClearInventoryIcons()
        {
            foreach (var icon in _inventoryIcons)
            {
                if (icon != null)
                    Destroy(icon);
            }
            _inventoryIcons.Clear();
        }

        private void ClearAnvilIcons()
        {
            foreach (var icon in _anvilIcons)
            {
                if (icon != null)
                    Destroy(icon);
            }
            _anvilIcons.Clear();
        }

        public void ResetSlots()
        {
            _accessorySlot0 = -1;
            _accessorySlot1 = -1;
            _accessorySlot2 = -1;
            _scrollSlot = -1;
            _upgradeInProgress = false;
            _isPreviewActive = false;
            _previewItemId = 0;
            _isUpgradeSucceeded = false;
            _resultItemId = 0;

            PopulateUpgradeInventory();
            RefreshUpgradeSlots();
        }

        private void OnPreviewButtonClicked()
        {
            if (_upgradeInProgress) return;

            bool ready = (_accessorySlot0 != -1 && _accessorySlot1 != -1 && _accessorySlot2 != -1 && _scrollSlot != -1);
            if (!ready)
            {
                KOUIManager.Instance?.AddMsgOutput("Place all required items to preview.", new Color(1f, 0f, 1f, 1f));
                return;
            }

            var item = KOInventory.Instance.m_pMyInvWnd[_accessorySlot0];
            if (item != null)
            {
                int previewId = item.itemId;
                if (_uniqueAccessoryUpgradeOffsets.TryGetValue(item.itemId, out int offset))
                {
                    previewId += offset;
                }
                else
                {
                    previewId += 1;
                }

                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.PlayPreviewAnimation(previewId);
                }
            }
        }

        private void SetupDropTarget(string goName, SlotType type, int index = -1)
        {
            var trans = KOUIRenderer.FindChildByID(_panelRoot, goName);
            if (trans != null)
            {
                var dropTarget = trans.gameObject.GetComponent<KOAccessoryUpgradeDropTarget>();
                if (dropTarget == null)
                {
                    dropTarget = trans.gameObject.AddComponent<KOAccessoryUpgradeDropTarget>();
                }
                dropTarget.slotType = type;
                dropTarget.slotIndex = index;
            }
        }

        public void PopulateUpgradeInventory()
        {
            // Clear current active inventory icons
            ClearInventoryIcons();

            if (KOInventory.Instance == null)
                return;

            // Show player's current gold
            if (_textGold != null && GameManager.Instance != null)
            {
                _textGold.text = string.Format("{0:N0}", GameManager.Instance.Gold);
            }

            // 1. Render Inventory Slots (a_slot_0 .. a_slot_27)
            for (int i = 0; i < 28; i++)
            {
                var slotArea = KOUIRenderer.FindChildByID(_panelRoot, $"a_slot_{i}");
                if (slotArea == null)
                    continue;

                var slot = KOInventory.Instance.m_pMyInvWnd[i];
                if (slot == null || slot.itemId == 0)
                    continue;

                // Subtract count if placed in upgrade slots
                int placedCount = 0;
                if (i == _accessorySlot0) placedCount++;
                if (i == _accessorySlot1) placedCount++;
                if (i == _accessorySlot2) placedCount++;
                if (i == _scrollSlot) placedCount++;

                int displayedCount = slot.count - placedCount;
                if (displayedCount <= 0)
                    continue;

                // Instantiate item icon in inventory slot
                CreateSlotIcon(slotArea, slot.itemId, displayedCount, SlotDistrict.BagSlot, i);
            }
        }

        public void RefreshUpgradeSlots()
        {
            bool ready = (_accessorySlot0 != -1 && _accessorySlot1 != -1 && _accessorySlot2 != -1 && _scrollSlot != -1);
            if (!ready)
            {
                _isPreviewActive = false;
                _previewItemId = 0;
            }

            // Clear current active anvil icons
            ClearAnvilIcons();

            if (KOInventory.Instance == null)
                return;

            // If upgrade succeeded, draw ONLY the result slot (a_result)
            if (_isUpgradeSucceeded)
            {
                var successResultArea = KOUIRenderer.FindChildByID(_panelRoot, "a_result");
                if (successResultArea != null)
                {
                    RenderResultIcon(successResultArea, _resultItemId, true);
                }

                // Hide rate/cost containers during success display
                if (_needCoinsContainer != null) _needCoinsContainer.SetActive(false);
                if (_upgradeRateContainer != null) _upgradeRateContainer.SetActive(false);
                return;
            }

            // 1. Render Accessory Slot 0
            if (_accessorySlot0 != -1)
            {
                var slotArea = KOUIRenderer.FindChildByID(_panelRoot, "a_upgrade_0");
                if (slotArea != null)
                {
                    var slot = KOInventory.Instance.m_pMyInvWnd[_accessorySlot0];
                    if (slot != null)
                        CreateSlotIcon(slotArea, slot.itemId, 1, SlotDistrict.Accessory0, _accessorySlot0);
                }
            }

            // 2. Render Accessory Slot 1
            if (_accessorySlot1 != -1)
            {
                var slotArea = KOUIRenderer.FindChildByID(_panelRoot, "a_upgrade_1");
                if (slotArea != null)
                {
                    var slot = KOInventory.Instance.m_pMyInvWnd[_accessorySlot1];
                    if (slot != null)
                        CreateSlotIcon(slotArea, slot.itemId, 1, SlotDistrict.Accessory1, _accessorySlot1);
                }
            }

            // 3. Render Accessory Slot 2
            if (_accessorySlot2 != -1)
            {
                var slotArea = KOUIRenderer.FindChildByID(_panelRoot, "a_upgrade_2");
                if (slotArea != null)
                {
                    var slot = KOInventory.Instance.m_pMyInvWnd[_accessorySlot2];
                    if (slot != null)
                        CreateSlotIcon(slotArea, slot.itemId, 1, SlotDistrict.Accessory2, _accessorySlot2);
                }
            }

            // 4. Render Scroll Slot
            if (_scrollSlot != -1)
            {
                var slotArea = KOUIRenderer.FindChildByID(_panelRoot, "a_m_0");
                if (slotArea != null)
                {
                    var slot = KOInventory.Instance.m_pMyInvWnd[_scrollSlot];
                    if (slot != null)
                        CreateSlotIcon(slotArea, slot.itemId, 1, SlotDistrict.Scroll, _scrollSlot);
                }
            }

            // 5. Render Result Slot (a_result) if preview is active
            var resultArea = KOUIRenderer.FindChildByID(_panelRoot, "a_result");
            if (resultArea != null)
            {
                if (_isPreviewActive && _previewItemId != 0)
                {
                    RenderResultIcon(resultArea, _previewItemId, false);
                }
            }

            // Calculate cost and rate
            UpdateUpgradeCostAndRate();
        }

        private void RenderResultIcon(Transform resultArea, int itemId, bool isSuccessResult)
        {
            var iconObj = new GameObject("AccessoryIcon_Result");
            iconObj.transform.SetParent(resultArea, false);

            var iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.sizeDelta = Vector2.zero;
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;

            var img = iconObj.AddComponent<Image>();
            var icon = KOItemIconLoader.LoadItemIcon(ResolveIconId(itemId));
            if (icon != null)
            {
                img.sprite = icon;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            }
            img.raycastTarget = true;

            // Attach slot handler for tooltip click
            var handler = iconObj.AddComponent<KOItemSlotHandler>();
            handler.slotType = KOItemSlotHandler.SlotType.BagSlot;
            handler.slotIndex = 99;
            handler.tooltipItemDefId = itemId;
            handler.tooltipShowPrice = false;
            handler.tooltipIsBuy = false;

            if (isSuccessResult)
            {
                // Click listener on result item to reset slots (collect/close result)
                var btn = iconObj.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    ResetSlots();
                });
            }

            _anvilIcons.Add(iconObj);
        }

        private void CreateSlotIcon(Transform parent, int itemId, int count, SlotDistrict district, int slotIndex)
        {
            var iconObj = new GameObject($"AccessoryIcon_{district}_{slotIndex}");
            iconObj.transform.SetParent(parent, false);

            var iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.sizeDelta = Vector2.zero;
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;

            var img = iconObj.AddComponent<Image>();
            var icon = KOItemIconLoader.LoadItemIcon(ResolveIconId(itemId));
            if (icon != null)
            {
                img.sprite = icon;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            }
            img.raycastTarget = true;

            // Stack count overlay (same logic as normal item upgrade)
            byte byContableCheck = 0;
            byte byAttachPointCheck = 0;
            if (KOInventory.s_pTbl_Items_Basic != null &&
                KOInventory.s_pTbl_Items_Basic.TryGetValue((uint)(itemId / 1000 * 1000), out var basicCount))
            {
                byContableCheck = basicCount.byContable;
                byAttachPointCheck = basicCount.byAttachPoint;
            }

            int displayCountVal = count;
            bool showCount = false;
            if (byContableCheck == 1 || byContableCheck == 2)
            {
                if (count > 1)
                {
                    showCount = true;
                }
            }
            else if (byAttachPointCheck == 15 && count > 1)
            {
                showCount = true;
            }

            if (showCount)
            {
                var countObj = new GameObject("CountText");
                countObj.transform.SetParent(iconObj.transform, false);

                var countRT = countObj.AddComponent<RectTransform>();
                countRT.anchorMin = new Vector2(1, 0);
                countRT.anchorMax = new Vector2(1, 0);
                countRT.pivot = new Vector2(1, 0);
                countRT.anchoredPosition = new Vector2(-2, 2);
                countRT.sizeDelta = new Vector2(30, 12);

                var countText = countObj.AddComponent<Text>();
                countText.text = displayCountVal.ToString();
                countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                countText.fontSize = 9;
                countText.color = Color.white;
                countText.alignment = TextAnchor.LowerRight;
                countText.raycastTarget = false;
            }

            // Tooltip handler (shows tooltip on tap)
            var slotHandler = iconObj.AddComponent<KOItemSlotHandler>();
            slotHandler.slotType = KOItemSlotHandler.SlotType.BagSlot;
            slotHandler.slotIndex = slotIndex;
            slotHandler.tooltipItemDefId = itemId;
            slotHandler.tooltipShowPrice = false;

            // Click listener
            var btn = iconObj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OnSlotTapped(district, slotIndex));

            // Drag support
            var dragHandler = iconObj.AddComponent<KOAccessoryDragHandler>();
            dragHandler.district = district;
            dragHandler.slotIndex = slotIndex;

            if (district == SlotDistrict.BagSlot)
            {
                _inventoryIcons.Add(iconObj);
            }
            else
            {
                _anvilIcons.Add(iconObj);
            }
        }

        private int ResolveIconId(int itemId)
        {
            return KOUIManager.ResolveIconId(itemId);
        }

        private void OnSlotTapped(SlotDistrict district, int slotIndex)
        {
            if (_upgradeInProgress) return;

            if (_isUpgradeSucceeded)
            {
                ResetSlots();
                if (district != SlotDistrict.BagSlot)
                    return;
            }

            if (district == SlotDistrict.BagSlot)
            {
                float now = Time.unscaledTime;
                if (_lastBagSlotTapIndex == slotIndex && (now - _lastBagSlotTapTime) < DOUBLE_TAP_THRESHOLD)
                {
                    _lastBagSlotTapIndex = -1;
                    _lastBagSlotTapTime = 0f;
                }
                else
                {
                    _lastBagSlotTapIndex = slotIndex;
                    _lastBagSlotTapTime = now;
                    return;
                }

                var slot = KOInventory.Instance.m_pMyInvWnd[slotIndex];
                if (slot == null || slot.itemId == 0) return;

                if (slot.itemId == 379159000)
                {
                    _scrollSlot = slotIndex;
                }
                else if (IsAccessory(slot.itemId))
                {
                    int placedSlot = GetFirstPlacedAccessorySlot();
                    if (placedSlot != -1)
                    {
                        var placedItem = KOInventory.Instance.m_pMyInvWnd[placedSlot];
                        if (placedItem == null || placedItem.itemId != slot.itemId)
                        {
                            return;
                        }
                    }

                    if (_accessorySlot0 == -1) _accessorySlot0 = slotIndex;
                    else if (_accessorySlot1 == -1) _accessorySlot1 = slotIndex;
                    else if (_accessorySlot2 == -1) _accessorySlot2 = slotIndex;
                }
            }
            else
            {
                if (district == SlotDistrict.Accessory0) _accessorySlot0 = -1;
                else if (district == SlotDistrict.Accessory1) _accessorySlot1 = -1;
                else if (district == SlotDistrict.Accessory2) _accessorySlot2 = -1;
                else if (district == SlotDistrict.Scroll) _scrollSlot = -1;
            }

            PopulateUpgradeInventory();
            RefreshUpgradeSlots();
        }

        private bool IsAccessory(int itemId)
        {
            if (itemId / 100000000 != 3)
                return false;

            if (itemId == 379159000)
                return false;

            return true;
        }

        private int GetFirstPlacedAccessorySlot()
        {
            if (_accessorySlot0 != -1) return _accessorySlot0;
            if (_accessorySlot1 != -1) return _accessorySlot1;
            if (_accessorySlot2 != -1) return _accessorySlot2;
            return -1;
        }

        public bool TryPlaceItem(int invPos, SlotType targetType)
        {
            if (_upgradeInProgress) return false;

            if (_isUpgradeSucceeded)
            {
                ResetSlots();
            }

            var slot = KOInventory.Instance.m_pMyInvWnd[invPos];
            if (slot == null || slot.itemId == 0) return false;

            if (targetType == SlotType.Scroll)
            {
                if (slot.itemId == 379159000)
                {
                    _scrollSlot = invPos;
                    PopulateUpgradeInventory();
                    RefreshUpgradeSlots();
                    return true;
                }
            }
            else if (targetType == SlotType.Accessory0 || targetType == SlotType.Accessory1 || targetType == SlotType.Accessory2)
            {
                if (IsAccessory(slot.itemId))
                {
                    int firstPlaced = GetFirstPlacedAccessorySlot();
                    if (firstPlaced != -1)
                    {
                        var placed = KOInventory.Instance.m_pMyInvWnd[firstPlaced];
                        if (placed == null || placed.itemId != slot.itemId)
                            return false;
                    }

                    if (targetType == SlotType.Accessory0) _accessorySlot0 = invPos;
                    else if (targetType == SlotType.Accessory1) _accessorySlot1 = invPos;
                    else if (targetType == SlotType.Accessory2) _accessorySlot2 = invPos;

                    PopulateUpgradeInventory();
                    RefreshUpgradeSlots();
                    return true;
                }
            }

            return false;
        }

        private void UpdateUpgradeCostAndRate()
        {
            bool ready = (_accessorySlot0 != -1 && _accessorySlot1 != -1 && _accessorySlot2 != -1 && _scrollSlot != -1);

            if (ready)
            {
                var item = KOInventory.Instance.m_pMyInvWnd[_accessorySlot0];
                int itemId = item.itemId;
                int level = itemId % 10;

                int cost = 0;
                int rate = 100;

                if (level == 4 || level == 5)
                {
                    rate = 50; 
                }

                if (itemId / 1000 % 1000 >= 500)
                {
                    cost = 2000000;
                }

                if (_needCoinsContainer != null) _needCoinsContainer.SetActive(true);
                if (_upgradeRateContainer != null) _upgradeRateContainer.SetActive(true);

                if (_textNeedCoins != null)
                {
                    _textNeedCoins.text = string.Format("{0:N0}", cost);
                }

                if (_textUpgradeRate != null)
                {
                    _textUpgradeRate.text = $"{rate}%";
                }
            }
            else
            {
                if (_needCoinsContainer != null) _needCoinsContainer.SetActive(false);
                if (_upgradeRateContainer != null) _upgradeRateContainer.SetActive(false);
                if (_textNeedCoins != null) _textNeedCoins.text = "-";
                if (_textUpgradeRate != null) _textUpgradeRate.text = "-";
            }
        }

        private void OnUpgradeClicked()
        {
            if (_upgradeInProgress) return;

            bool ready = (_accessorySlot0 != -1 && _accessorySlot1 != -1 && _accessorySlot2 != -1 && _scrollSlot != -1);
            if (!ready) return;

            var item1 = KOInventory.Instance.m_pMyInvWnd[_accessorySlot0];
            var item2 = KOInventory.Instance.m_pMyInvWnd[_accessorySlot1];
            var item3 = KOInventory.Instance.m_pMyInvWnd[_accessorySlot2];
            var scroll = KOInventory.Instance.m_pMyInvWnd[_scrollSlot];

            if (item1 == null || item2 == null || item3 == null || scroll == null) return;

            // Noah check
            int cost = 0;
            if (item1.itemId / 1000 % 1000 >= 500)
            {
                cost = 2000000;
            }

            if (GameManager.Instance == null || GameManager.Instance.Gold < cost)
            {
                if (KOMessageBox.Instance != null)
                {
                    KOMessageBox.Instance.Show(
                        "You don't have enough Coins.",
                        "Failed",
                        MsgBoxStyle.MB_OK,
                        MsgBoxBehavior.BEHAVIOR_NOTHING
                    );
                }
                return;
            }

            if (KOMessageBox.Instance != null)
            {
                KOMessageBox.Instance.ShowYesNo(
                    "Do you Want this Item Upgrade?\nYou may lose your belongings\ncompletely.", 
                    "Item Upgrade", 
                    MsgBoxBehavior.BEHAVIOR_NOTHING,
                    onYes: () => {
                        if (!_upgradeInProgress)
                        {
                            SendAccessoryUpgradePacket(item1, item2, item3, scroll);
                        }
                    },
                    onNo: null,
                    callerPanel: _panelRoot != null ? _panelRoot.gameObject : null
                );
            }
            else
            {
                SendAccessoryUpgradePacket(item1, item2, item3, scroll);
            }
        }

        private void SendAccessoryUpgradePacket(KOInventory.ItemSlot item1, KOInventory.ItemSlot item2, KOInventory.ItemSlot item3, KOInventory.ItemSlot scroll)
        {
            _upgradeInProgress = true;

            // Send network packet
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_ITEM_UPGRADE);
            pkt.WriteByte(3); // subopcode 3 = ITEM_UPGRADE_ACCESSORIES
            pkt.WriteInt16((short)_npcId);

            // Item 1 (origin)
            pkt.WriteInt32(item1.itemId);
            pkt.WriteByte((byte)_accessorySlot0);

            // Req items
            pkt.WriteInt32(item2.itemId);
            pkt.WriteByte((byte)_accessorySlot1);

            pkt.WriteInt32(item3.itemId);
            pkt.WriteByte((byte)_accessorySlot2);

            pkt.WriteInt32(scroll.itemId);
            pkt.WriteByte((byte)_scrollSlot);

            for (int i = 3; i < 9; i++)
            {
                pkt.WriteInt32(0);
                pkt.WriteByte(255);
            }

            KONetworkManager.Instance?.SendPacket(pkt);
        }

        public void HandleRawAccessoryUpgradePacket(byte[] rawData)
        {
            _upgradeInProgress = false;

            var r = new KOPacketReader(rawData);
            byte subopcode = r.ReadByte(); // 3
            byte result = r.ReadByte(); // result
            int newItemId = r.ReadInt32();
            byte item1_pos = r.ReadByte();


            // Read requirement item consumption details
            int[] reqItemIds = new int[9];
            byte[] reqItemPoss = new byte[9];
            for (int i = 0; i < 9; i++)
            {
                if (r.Remaining >= 5)
                {
                    reqItemIds[i] = r.ReadInt32();
                    reqItemPoss[i] = r.ReadByte();
                }
                else
                {
                    reqItemPoss[i] = 255;
                }
            }

            var inv = KOInventory.Instance;
            if (inv != null)
            {
                if (result == 1 || result == 0)
                {
                    // 1. Consume the requirement items (the other 2 accessories and scroll)
                    for (int i = 0; i < 9; i++)
                    {
                        byte reqPos = reqItemPoss[i];
                        if (reqPos >= 0 && reqPos < 28) // inventory slots are 0..27
                        {
                            var reqSlot = inv.m_pMyInvWnd[reqPos];
                            if (reqSlot != null && reqSlot.itemId == reqItemIds[i])
                            {
                                reqSlot.count--;
                                if (reqSlot.count <= 0)
                                {
                                    inv.m_pMyInvWnd[reqPos] = null;
                                }
                            }
                        }
                    }

                    // 2. Update origin item slot (first accessory slot)
                    if (item1_pos >= 0 && item1_pos < 28)
                    {
                        if (result == 1) // Succeeded
                        {
                            var originSlot = inv.m_pMyInvWnd[item1_pos];
                            if (originSlot != null)
                            {
                                originSlot.itemId = newItemId;
                                originSlot.count = 1;

                                KOTableReader.TableItemBasic basic = null;
                                if (KOInventory.s_pTbl_Items_Basic != null && KOInventory.s_pTbl_Items_Basic.TryGetValue((uint)newItemId / 1000 * 1000, out basic))
                                {
                                    originSlot.pItemBasic = basic;
                                    originSlot.iconFN = basic.dwIDIcon.ToString();
                                    if (originSlot.serverData != null)
                                    {
                                        originSlot.serverData.ItemDefId = newItemId;
                                        originSlot.serverData.IconId = originSlot.iconFN;
                                        originSlot.serverData.Durability = (short)basic.siMaxDurability;
                                    }
                                }
                            }
                        }
                        else // Failed / Burnt (result == 0)
                        {
                            inv.m_pMyInvWnd[item1_pos] = null;
                        }
                    }
                }

                // Force refresh UI so inventory bag slots update visual state
                KOUIManager.Instance?.RefreshInventoryUI();
                KOUIManager.Instance?.PopulateUpgradeInventory();
            }

            if (result == 1) // Success
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.AddMsgOutput("Item upgrade succeeded!", KOUIManager.D3DColorToUnity(0xff8080ff));
                }
            }
            else if (result == 3) // Need Coins
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.AddMsgOutput("Upgrade failed: Not enough coins.", KOUIManager.D3DColorToUnity(0xffff00ff));
                }
            }
            else if (result == 4) // No Match
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.AddMsgOutput("Upgrade failed: No matching upgrade rule.", KOUIManager.D3DColorToUnity(0xffff00ff));
                }
            }
            else
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.AddMsgOutput("Item upgrade failed.", KOUIManager.D3DColorToUnity(0xffff00ff));
                }
            }

            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.PlayUpgradeAnimation(result == 1);
                KOUIManager.Instance.UpdateUpgradeGold();
            }

            if (result == 1)
            {
                _isUpgradeSucceeded = true;
                _resultItemId = newItemId;
                _accessorySlot1 = -1;
                _accessorySlot2 = -1;
                _scrollSlot = -1;
                _upgradeInProgress = true; // Animation will play
            }
            else if (result == 0)
            {
                _isUpgradeSucceeded = false;
                _resultItemId = 0;
                _accessorySlot0 = -1;
                _accessorySlot1 = -1;
                _accessorySlot2 = -1;
                _scrollSlot = -1;
                _upgradeInProgress = true; // Animation will play
            }
            else
            {
                _isUpgradeSucceeded = false;
                _resultItemId = 0;
                _accessorySlot0 = -1;
                _accessorySlot1 = -1;
                _accessorySlot2 = -1;
                _scrollSlot = -1;
                _upgradeInProgress = false; // Soft error, no animation
            }

            _isPreviewActive = false;
            _previewItemId = 0;

            PopulateUpgradeInventory();
            if (!_upgradeInProgress)
            {
                RefreshUpgradeSlots();
            }
        }
    }

    public enum SlotType
    {
        Accessory0,
        Accessory1,
        Accessory2,
        Scroll,
        Inventory
    }

    public enum SlotDistrict
    {
        Accessory0,
        Accessory1,
        Accessory2,
        Scroll,
        BagSlot
    }

    // KOAccessoryUpgradeDropTarget moved to separate file

    public class KOAccessoryDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        public static KOAccessoryDragHandler CurrentDragSource { get; private set; }

        public SlotDistrict district;
        public int slotIndex;

        private CanvasGroup _canvasGroup;
        private Image _slotImage;
        private static GameObject _ghostIcon;

        private void Awake()
        {
            _slotImage = GetComponent<Image>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnDestroy()
        {
            if (CurrentDragSource == this)
            {
                DestroyGhostIcon();
                CurrentDragSource = null;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
        }

        public void OnPointerUp(PointerEventData eventData)
        {
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            CurrentDragSource = this;

            CreateGhostIcon(eventData);

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ghostIcon == null) return;
            UpdateGhostPosition(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (CurrentDragSource != this) return;

            DestroyGhostIcon();

            _canvasGroup.alpha = 1.0f;
            _canvasGroup.blocksRaycasts = true;

            CurrentDragSource = null;
        }

        private void CreateGhostIcon(PointerEventData eventData)
        {
            if (_ghostIcon != null)
                Destroy(_ghostIcon);

            var canvasObj = new GameObject("GhostDragCanvas");
            var ghostCanvas = canvasObj.AddComponent<Canvas>();
            ghostCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ghostCanvas.sortingOrder = 30000;

            _ghostIcon = canvasObj;

            var imgObj = new GameObject("GhostImage");
            imgObj.transform.SetParent(canvasObj.transform, false);

            var rt = imgObj.AddComponent<RectTransform>();
            var srcRt = GetComponent<RectTransform>();
            
            float w = srcRt.rect.width * srcRt.lossyScale.x;
            float h = srcRt.rect.height * srcRt.lossyScale.y;
            
            // Safeguard for uninitialized or zero-size rects
            if (w <= 0f || h <= 0f)
            {
                float scaleX = srcRt.lossyScale.x > 0f ? srcRt.lossyScale.x : 1f;
                float scaleY = srcRt.lossyScale.y > 0f ? srcRt.lossyScale.y : 1f;
                w = 40f * scaleX;
                h = 40f * scaleY;
            }

            rt.sizeDelta = new Vector2(w, h);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = imgObj.AddComponent<Image>();
            Sprite itemSprite = null;

            if (_slotImage == null)
            {
                _slotImage = GetComponent<Image>();
            }

            var childImages = GetComponentsInChildren<Image>();
            foreach (var childImg in childImages)
            {
                if (childImg != _slotImage && childImg.sprite != null)
                {
                    itemSprite = childImg.sprite;
                    break;
                }
            }

            if (itemSprite == null && _slotImage != null)
                itemSprite = _slotImage.sprite;

            if (itemSprite != null)
            {
                img.sprite = itemSprite;
                img.color = new Color(1f, 1f, 1f, 1f);
            }
            else
            {
                img.color = new Color(0.5f, 0.8f, 1f, 0.6f);
            }

            img.raycastTarget = false;
            UpdateGhostPosition(eventData.position);
        }

        private static void UpdateGhostPosition(Vector2 screenPos)
        {
            if (_ghostIcon == null) return;
            if (_ghostIcon.transform.childCount == 0) return;
            _ghostIcon.transform.GetChild(0).position = new Vector3(screenPos.x, screenPos.y, 0f);
        }

        private void DestroyGhostIcon()
        {
            if (_ghostIcon != null)
            {
                Destroy(_ghostIcon);
                _ghostIcon = null;
            }
        }
    }
}
