using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.IO;
using TMPro;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: UICharacterCreate.cpp + GameProcCharacterCreate.cpp
    /// 
    /// .uif tabanlı — Inspector bağımlılığı yok.
    /// Nation'a göre El_CharacterCreate_us.uif veya Ka_CharacterCreate_us.uif yüklenir.
    /// 
    /// UI Element ID'leri (UICharacterCreate.cpp:81-190):
    ///   edit_name           — isim girişi
    ///   text_desc           — açıklama metni
    ///   text_str/sta/dex/int/map — stat değerleri
    ///   text_bonus          — bonus puanı
    ///   area_str/sta/dex/int/map — stat up/down alanları
    ///   btn_face_left/right — yüz değiştir
    ///   btn_hair_left/right — saç değiştir
    ///   btn_race_el_ba/rm/rf — El Morad ırkları
    ///   btn_race_ka_at/tu/wt/pt — Karus ırkları
    ///   btn_class_warrior/rogue/mage/priest — sınıf butonları
    ///   area_character      — karakter görüntüleme alanı
    /// </summary>
    public class CharacterCreateUI : MonoBehaviour
    {
        /// <summary>
        /// Open-KO birebir: CGameProcedure::s_iChrSelectIndex (GameProcedure.cpp:85)
        /// </summary>
        public static byte PendingCharIndex = 0;

        // === Open-KO birebir: e_Race (GameDef.h:164-176) ===
        private const byte RACE_KA_ARKTUAREK     = 1;
        private const byte RACE_KA_TUAREK        = 2;
        private const byte RACE_KA_WRINKLETUAREK = 3;
        private const byte RACE_KA_PURITUAREK    = 4;
        private const byte RACE_EL_BABARIAN      = 11;
        private const byte RACE_EL_MAN           = 12;
        private const byte RACE_EL_WOMEN         = 13;

        // === Open-KO birebir: e_Class (globals.h:84-100) ===
        private const short CLASS_KA_WARRIOR = 101;
        private const short CLASS_KA_ROGUE   = 102;
        private const short CLASS_KA_WIZARD  = 103;
        private const short CLASS_KA_PRIEST  = 104;
        private const short CLASS_EL_WARRIOR = 201;
        private const short CLASS_EL_ROGUE   = 202;
        private const short CLASS_EL_WIZARD  = 203;
        private const short CLASS_EL_PRIEST  = 204;

        // === Open-KO birebir error codes (GameProcCharacterCreate.cpp:283-311) ===
        private static readonly string[] ErrorMessages = new string[18];

        // Seçim durumu
        private byte _nation;
        private byte _selectedRace;
        private short _selectedClass;
        private byte _face;
        private byte _hair;

        // Stat dağıtımı — UICharacterCreate.cpp:337-475
        private int _str, _sta, _dex, _int, _cha;
        private int _bonusPoint, _maxBonusPoint;
        private int _baseStr, _baseSta, _baseDex, _baseInt, _baseCha;

        // NewChrValue.tbl
        private Dictionary<uint, KOTableReader.TableNewChr> _newChrTable;

        // UPC_DefaultLooks.tbl — Open-KO birebir: s_pTbl_UPC_Looks (GameBase.h:21)
        private Dictionary<uint, KOTableReader.TablePlayerLooks> _upcLooksTable;

        // Race-Class kısıtlama — UICharacterCreate.cpp:560-601
        private static readonly Dictionary<byte, bool[]> RaceClassMap = new()
        {
            { RACE_KA_ARKTUAREK,     new[] { true,  false, false, false } }, // Only Warrior
            { RACE_KA_TUAREK,        new[] { true,  true,  false, true  } }, // Warrior, Rogue, Priest
            { RACE_KA_WRINKLETUAREK, new[] { false, false, true,  false } }, // Only Mage
            { RACE_KA_PURITUAREK,    new[] { false, false, true,  true  } }, // Mage, Priest
            { RACE_EL_BABARIAN,      new[] { true,  false, false, false } }, // Only Warrior
            { RACE_EL_MAN,           new[] { true,  true,  true,  true  } }, // All
            { RACE_EL_WOMEN,         new[] { true,  true,  true,  true  } }, // All
        };

        // UI References — .uif'den bağlanır
        private Canvas _canvas;
        private GameObject _uiRoot;
        private Image _bgImage;

        // Custom Styling Colors (Draco Theme)
        private Color _colorBg = new Color(0.12f, 0.10f, 0.08f, 0.96f);
        private Color _colorBgInner = new Color(0.04f, 0.04f, 0.04f, 0.96f);
        private Color _colorBorder = new Color(0.6f, 0.48f, 0.22f, 0.9f);
        private Color _colorTextGold = new Color(0.95f, 0.85f, 0.35f, 1f);
        private Color _colorBtnGold = new Color(0.48f, 0.38f, 0.22f, 1f);
        private Color _colorBtnGoldBorder = new Color(0.6f, 0.48f, 0.22f, 1f);
        private Color _colorInputBg = new Color(0.05f, 0.04f, 0.04f, 1f);
        private Color _colorBtnDark = new Color(0.08f, 0.07f, 0.06f, 0.95f);

        // Subclass Descriptions & Titles
        private readonly string[] _classNames = { "WARRIOR", "ROGUE", "MAGE", "PRIEST" };
        private readonly string[] _classDetailTitles = { "FRENZY / BERSERKER", "ASSASSIN / ARCHER", "FLAME / LIGHTNING", "HOLY / HEALER" };
        private readonly string[] _classDescriptions = {
            "Warriors are the ultimate front-line fighters, boasting unparalleled health and defense. Equipped with massive two-handed weapons or shields, they lead the charge and protect their allies.",
            "Rogues are swift and lethal combatants. As Assassins, they deliver quick, deadly strikes with dual daggers from the shadows. As Archers, they rain down arrows on enemies from a safe distance.",
            "Mages are masters of elemental magic. They deal devastating area-of-effect damage using Flame spells, slow down enemies with Glacier magic, or stun foes with crackling Lightning strikes.",
            "Priests are the spine of any party. They possess powerful healing magic to restore health, buff spells to increase allies' defenses, and debuffs to weaken enemies."
        };

        // UI elements
        private InputField _editName;
        private Text[] _textStats = new Text[5]; // str, sta, dex, int, map
        private Button[] _btnRaces = new Button[4];
        private Button[] _btnClasses = new Button[4];
        private Button _btnFaceLeft, _btnFaceRight;
        private Button _btnHairLeft, _btnHairRight;
        private Button _btnCreate;
        private Button _btnBack;

        // Custom UI Element bindings
        private Image[] _btnClassBgs = new Image[4];
        private TextMeshProUGUI[] _btnClassTexts = new TextMeshProUGUI[4];
        private TextMeshProUGUI _detailTitleTxt;
        private TextMeshProUGUI _detailDescTxt;
        private Image[] _armorSlots = new Image[4];
        private int _selectedArmorIndex = 0;

        // Karakter preview — cpp:78 — s_pPlayer->InventoryChrRender(m_rcChr)
        private UnityEngine.Camera _previewCam;
        private RenderTexture _previewRT;
        private GameObject _previewModel;
        private RawImage _previewImage;
        private const int PREVIEW_LAYER = 31; // Ayrı layer — UI ile çakışmasın

        private void Awake()
        {
            // Error messages — GameProcCharacterCreate.cpp:283-311
            ErrorMessages[0x00] = "Character created successfully!";
            ErrorMessages[0x01] = "Maximum number of characters reached.";
            ErrorMessages[0x02] = "Invalid character details.";
            ErrorMessages[0x03] = "This character name already exists.";
            ErrorMessages[0x04] = "Database error occurred.";
            ErrorMessages[0x05] = "Invalid character name.";
            ErrorMessages[0x06] = "Character name contains forbidden words.";
            ErrorMessages[0x07] = "Invalid race selection.";
            ErrorMessages[0x08] = "This race is not supported.";
            ErrorMessages[0x09] = "Invalid class selection.";
            ErrorMessages[0x0A] = "You still have bonus points to distribute.";
            ErrorMessages[0x11] = "Each stat must be at least 50.";
        }

        private void Start()
        {

            // NewChrValue.tbl — GameProcCharacterCreate.cpp:56
            string tblPath = "NewChrValue.tbl";
            _newChrTable = KOTableReader.LoadNewChrValue(tblPath);

            // UPC_DefaultLooks.tbl — GameProcCharacterCreate.cpp:92
            // C++: s_pTbl_UPC_Looks.Find(eRace) — SetChr() için gerekli
            string looksPath = "UPC_DefaultLooks.tbl";
            _upcLooksTable = KOTableReader.LoadUpcLooksTable(looksPath);

            // Canvas oluştur
            CreateCanvas();

            // Nation belirle
            var gm = Core.GameManager.Instance;
            _nation = (gm != null) ? gm.Nation : (byte)2;

            // Arka plan oluştur
            SetupBackground();

            // .uif yükle — cpp:64-68 — nation'a göre
            LoadCharacterCreateUI();

            // Server response
            KOPacketHandler.OnNewCharResult += HandleCreateCharacterResponse;

            ResetStats();
            SelectClassIndex(0); // Select Warrior by default
            SelectArmorPreview(0); // Select first armor set by default
        }

        private void OnDestroy()
        {
            KOPacketHandler.OnNewCharResult -= HandleCreateCharacterResponse;

            // Preview cleanup
            if (_previewModel != null) Destroy(_previewModel);
            if (_previewRT != null) { _previewRT.Release(); Destroy(_previewRT); }
        }

        // ============================================
        // Canvas — CharacterSelectUI ile aynı pattern
        // ============================================

        private void CreateCanvas()
        {
            var canvasObj = new GameObject("CreateCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024, 768);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        private void SetupBackground()
        {
            // Nation'a göre uygun arka plan görselini seç
            string bgPath = _nation == 1 
                ? "UI/create_char_orc_bg" 
                : "UI/create_char_human_bg";

            Sprite customBg = Resources.Load<Sprite>(bgPath);
            if (customBg == null)
            {
                Debug.LogWarning($"[CREATE] Özel arka plan '{bgPath}' yüklenemedi. Dosya adının ve import ayarlarının doğruluğunu kontrol edin.");
                return;
            }

            // Canvas altında arka plan GameObject'i oluştur (En arkaya yerleştir)
            GameObject bgObj = new GameObject("CustomCreateBackground", typeof(RectTransform));
            bgObj.transform.SetParent(_canvas.transform, false);
            bgObj.transform.SetAsFirstSibling();

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.sprite = customBg;
            _bgImage = bgImage;
            bgImage.raycastTarget = false; // Tıklamaları engellemesin

            // Merkez odaklı en-boy oranı koruyarak ekranı kaplama (EnvelopeParent)
            var rt = bgObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var fitter = bgObj.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = customBg.rect.width / customBg.rect.height;
        }

        // ===================
        private void LoadCharacterCreateUI()
        {
            // Canvas boyutunu güncelle
            Canvas.ForceUpdateCanvases();

            // Root panel oluştur
            _uiRoot = new GameObject("ProceduralCreateUI", typeof(RectTransform));
            _uiRoot.transform.SetParent(_canvas.transform, false);
            var rootRt = _uiRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // 1. Back Button (Top Left)
            GameObject backObj = new GameObject("btn_cancel", typeof(RectTransform));
            backObj.transform.SetParent(_uiRoot.transform, false);
            var backRt = backObj.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0f, 1f);
            backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0.5f, 0.5f);
            backRt.sizeDelta = new Vector2(84f, 28f);
            backRt.anchoredPosition = new Vector2(80f, -40f);

            var backImg = backObj.AddComponent<Image>();
            backImg.sprite = CreateRoundedRectSprite("back_btn_bg", 84, 28, 14, _colorBtnDark, _colorBorder, 1);
            backImg.color = Color.white;

            var backTxtObj = new GameObject("BackText", typeof(RectTransform));
            backTxtObj.transform.SetParent(backObj.transform, false);
            var backTxt = backTxtObj.AddComponent<TextMeshProUGUI>();
            backTxt.font = fontAsset;
            backTxt.fontSize = 13;
            backTxt.fontStyle = FontStyles.Bold;
            backTxt.alignment = TextAlignmentOptions.Center;
            backTxt.color = _colorTextGold;
            backTxt.text = "<size=11><color=#E6CC19><</color></size> Back";

            _btnBack = backObj.AddComponent<Button>();
            _btnBack.onClick.AddListener(OnBackClicked);

            // 2. Left Class Selection Panel
            GameObject classPanelObj = new GameObject("ClassSelectionPanel", typeof(RectTransform));
            classPanelObj.transform.SetParent(_uiRoot.transform, false);
            var classPanelRt = classPanelObj.GetComponent<RectTransform>();
            classPanelRt.anchorMin = new Vector2(0f, 0.5f);
            classPanelRt.anchorMax = new Vector2(0f, 0.5f);
            classPanelRt.pivot = new Vector2(0f, 0.5f);
            classPanelRt.sizeDelta = new Vector2(220f, 260f);
            classPanelRt.anchoredPosition = new Vector2(60f, 50f);

            // 4 Sınıf Butonu (Warrior, Rogue, Mage, Priest)
            float classStartY = 95f;
            float classSpacingY = -60f;

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                GameObject btnObj = new GameObject($"btn_class_{i}", typeof(RectTransform));
                btnObj.transform.SetParent(classPanelObj.transform, false);
                var btnRt = btnObj.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(0.5f, 0.5f);
                btnRt.anchorMax = new Vector2(0.5f, 0.5f);
                btnRt.pivot = new Vector2(0.5f, 0.5f);
                btnRt.sizeDelta = new Vector2(200f, 44f);
                btnRt.anchoredPosition = new Vector2(10f, classStartY + i * classSpacingY);

                var img = btnObj.AddComponent<Image>();
                img.sprite = CreateRoundedRectSprite($"class_btn_{i}", 200, 44, 12, _colorBtnDark, _colorBorder * 0.4f, 1);
                img.color = Color.white;
                _btnClassBgs[i] = img;

                // Soldaki Yuvarlak Sınıf Avatarı (Çakışan konumda)
                GameObject avatarObj = new GameObject("Avatar", typeof(RectTransform));
                avatarObj.transform.SetParent(btnObj.transform, false);
                var avRt = avatarObj.GetComponent<RectTransform>();
                avRt.anchorMin = new Vector2(0f, 0.5f);
                avRt.anchorMax = new Vector2(0f, 0.5f);
                avRt.pivot = new Vector2(0.5f, 0.5f);
                avRt.sizeDelta = new Vector2(36f, 36f);
                avRt.anchoredPosition = new Vector2(-6f, 0f);

                var avImg = avatarObj.AddComponent<Image>();
                avImg.sprite = CreateRoundedRectSprite($"class_avatar_{i}", 36, 36, 18, _colorBg, _colorBorder, 1);
                avImg.color = Color.white;

                var avTxtObj = new GameObject("AvatarText", typeof(RectTransform));
                avTxtObj.transform.SetParent(avatarObj.transform, false);
                var avTxt = avTxtObj.AddComponent<TextMeshProUGUI>();
                avTxt.font = fontAsset;
                avTxt.fontSize = 15;
                avTxt.fontStyle = FontStyles.Bold;
                avTxt.alignment = TextAlignmentOptions.Center;
                avTxt.color = _colorTextGold;
                avTxt.text = _classNames[i].Substring(0, 1); // W, R, M, P sınıf baş harfleri

                // Sınıf Yazısı
                GameObject txtObj = new GameObject("ClassText", typeof(RectTransform));
                txtObj.transform.SetParent(btnObj.transform, false);
                var txtRt = txtObj.GetComponent<RectTransform>();
                txtRt.anchorMin = new Vector2(0f, 0.5f);
                txtRt.anchorMax = new Vector2(1f, 0.5f);
                txtRt.offsetMin = new Vector2(40f, -15f);
                txtRt.offsetMax = new Vector2(-10f, 15f);

                var txt = txtObj.AddComponent<TextMeshProUGUI>();
                txt.font = fontAsset;
                txt.fontSize = 14;
                txt.fontStyle = FontStyles.Bold;
                txt.alignment = TextAlignmentOptions.Left;
                txt.color = _colorTextGold;
                txt.text = _classNames[i];
                _btnClassTexts[i] = txt;

                var btn = btnObj.AddComponent<Button>();
                btn.onClick.AddListener(() => SelectClassIndex(idx));
                _btnClasses[idx] = btn;
            }

            // 3. Armor Preview Panel (Sol Alt)
            GameObject armorPanelObj = new GameObject("ArmorPreviewPanel", typeof(RectTransform));
            armorPanelObj.transform.SetParent(_uiRoot.transform, false);
            var armorPanelRt = armorPanelObj.GetComponent<RectTransform>();
            armorPanelRt.anchorMin = new Vector2(0f, 0.5f);
            armorPanelRt.anchorMax = new Vector2(0f, 0.5f);
            armorPanelRt.pivot = new Vector2(0f, 0.5f);
            armorPanelRt.sizeDelta = new Vector2(200f, 75f);
            armorPanelRt.anchoredPosition = new Vector2(60f, -130f);

            GameObject apTitleObj = new GameObject("ArmorPreviewTitle", typeof(RectTransform));
            apTitleObj.transform.SetParent(armorPanelObj.transform, false);
            var apTitleRt = apTitleObj.GetComponent<RectTransform>();
            apTitleRt.anchorMin = new Vector2(0.5f, 1f);
            apTitleRt.anchorMax = new Vector2(0.5f, 1f);
            apTitleRt.pivot = new Vector2(0.5f, 1f);
            apTitleRt.sizeDelta = new Vector2(180f, 20f);
            apTitleRt.anchoredPosition = new Vector2(0f, 0f);

            var apTitleTxt = apTitleObj.AddComponent<TextMeshProUGUI>();
            apTitleTxt.font = fontAsset;
            apTitleTxt.fontSize = 11;
            apTitleTxt.fontStyle = FontStyles.Bold;
            apTitleTxt.alignment = TextAlignmentOptions.Center;
            apTitleTxt.color = _colorTextGold * 0.8f;
            apTitleTxt.text = "ARMOR PREVIEW";

            // 4 Zırh Slotu
            float slotStartX = -69f;
            float slotSpacingX = 46f;
            for (int i = 0; i < 4; i++)
            {
                int armorIdx = i;
                GameObject slotObj = new GameObject($"armor_slot_{i}", typeof(RectTransform));
                slotObj.transform.SetParent(armorPanelObj.transform, false);
                var slotRt = slotObj.GetComponent<RectTransform>();
                slotRt.anchorMin = new Vector2(0.5f, 0f);
                slotRt.anchorMax = new Vector2(0.5f, 0f);
                slotRt.pivot = new Vector2(0.5f, 0.5f);
                slotRt.sizeDelta = new Vector2(38f, 38f);
                slotRt.anchoredPosition = new Vector2(slotStartX + i * slotSpacingX, 20f);

                var slotImg = slotObj.AddComponent<Image>();
                slotImg.sprite = CreateRoundedRectSprite($"armor_slot_bg_{i}", 38, 38, 6, _colorBtnDark, _colorBorder * 0.4f, 1);
                slotImg.color = Color.white;
                _armorSlots[i] = slotImg;

                var slotBtn = slotObj.AddComponent<Button>();
                slotBtn.onClick.AddListener(() => SelectArmorPreview(armorIdx));
            }



            // 4. Center Bottom İsim Girişi & Oluştur Butonu
            GameObject nameInputObj = new GameObject("edit_name", typeof(RectTransform));
            nameInputObj.transform.SetParent(_uiRoot.transform, false);
            var nameInputRt = nameInputObj.GetComponent<RectTransform>();
            nameInputRt.anchorMin = new Vector2(0.5f, 0f);
            nameInputRt.anchorMax = new Vector2(0.5f, 0f);
            nameInputRt.pivot = new Vector2(0.5f, 0.5f);
            nameInputRt.sizeDelta = new Vector2(240f, 34f);
            nameInputRt.anchoredPosition = new Vector2(0f, 100f);

            var inputImg = nameInputObj.AddComponent<Image>();
            inputImg.sprite = CreateHexagonSprite("input_bg_hexagon", 240, 34,
                _colorInputBg,
                _colorInputBg,
                _colorBorder * 0.8f,
                new Color(0.12f, 0.10f, 0.08f, 0.5f));
            inputImg.color = Color.white;

            var inputTextObj = new GameObject("InputText");
            inputTextObj.transform.SetParent(nameInputObj.transform, false);
            var inputTextComp = inputTextObj.AddComponent<Text>();
            inputTextComp.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            inputTextComp.fontSize = 14;
            inputTextComp.color = Color.white;
            inputTextComp.alignment = TextAnchor.MiddleCenter;
            var inputTextRT = inputTextObj.GetComponent<RectTransform>();
            inputTextRT.anchorMin = Vector2.zero;
            inputTextRT.anchorMax = Vector2.one;
            inputTextRT.offsetMin = new Vector2(5f, 0f);
            inputTextRT.offsetMax = new Vector2(-5f, 0f);

            var phObj = new GameObject("Placeholder");
            phObj.transform.SetParent(nameInputObj.transform, false);
            var phText = phObj.AddComponent<Text>();
            phText.font = inputTextComp.font;
            phText.fontSize = 14;
            phText.fontStyle = FontStyle.Italic;
            phText.color = new Color(1f, 1f, 1f, 0.4f);
            phText.alignment = TextAnchor.MiddleCenter;
            phText.text = "Enter Character Name";
            var phRT = phObj.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(5f, 0f);
            phRT.offsetMax = new Vector2(-5f, 0f);

            _editName = nameInputObj.AddComponent<InputField>();
            _editName.textComponent = inputTextComp;
            _editName.placeholder = phText;
            _editName.characterLimit = 20;

            // CREATE CHARACTER Butonu
            GameObject createBtnObj = new GameObject("btn_create", typeof(RectTransform));
            createBtnObj.transform.SetParent(_uiRoot.transform, false);
            var createBtnRt = createBtnObj.GetComponent<RectTransform>();
            createBtnRt.anchorMin = new Vector2(0.5f, 0f);
            createBtnRt.anchorMax = new Vector2(0.5f, 0f);
            createBtnRt.pivot = new Vector2(0.5f, 0.5f);
            createBtnRt.sizeDelta = new Vector2(200f, 44f);
            createBtnRt.anchoredPosition = new Vector2(0f, 40f);

            var createBtnImg = createBtnObj.AddComponent<Image>();
            createBtnImg.sprite = CreateHexagonSprite("create_btn_bg", 200, 44,
                new Color(0.96f, 0.80f, 0.22f, 0.98f),
                new Color(0.68f, 0.48f, 0.08f, 0.98f),
                new Color(0.85f, 0.65f, 0.15f, 0.98f),
                new Color(1.00f, 0.95f, 0.72f, 0.98f));
            createBtnImg.color = Color.white;

            var createTxtObj = new GameObject("CreateText", typeof(RectTransform));
            createTxtObj.transform.SetParent(createBtnObj.transform, false);
            var createTxt = createTxtObj.AddComponent<TextMeshProUGUI>();
            createTxt.font = fontAsset;
            createTxt.fontSize = 15;
            createTxt.fontStyle = FontStyles.Bold;
            createTxt.alignment = TextAlignmentOptions.Center;
            createTxt.color = Color.black;
            createTxt.text = "CREATE CHARACTER";

            _btnCreate = createBtnObj.AddComponent<Button>();
            _btnCreate.onClick.AddListener(OnCreateClicked);

            // 5. Sağ Detay Paneli
            GameObject detailPanelObj = new GameObject("DetailPanel", typeof(RectTransform));
            detailPanelObj.transform.SetParent(_uiRoot.transform, false);
            var detailPanelRt = detailPanelObj.GetComponent<RectTransform>();
            detailPanelRt.anchorMin = new Vector2(1f, 0.5f);
            detailPanelRt.anchorMax = new Vector2(1f, 0.5f);
            detailPanelRt.pivot = new Vector2(1f, 0.5f);
            detailPanelRt.sizeDelta = new Vector2(240f, 380f);
            detailPanelRt.anchoredPosition = new Vector2(-60f, 50f);

            var detailPanelImg = detailPanelObj.AddComponent<Image>();
            detailPanelImg.sprite = CreatePanelBgSprite("detail_panel_bg", 240, 380, _colorBg, _colorBgInner, _colorBorder, 2);
            detailPanelImg.color = Color.white;

            // Detay Başlığı
            GameObject detailTitleObj = new GameObject("DetailTitleText", typeof(RectTransform));
            detailTitleObj.transform.SetParent(detailPanelObj.transform, false);
            var detailTitleRt = detailTitleObj.GetComponent<RectTransform>();
            detailTitleRt.anchorMin = new Vector2(0.5f, 1f);
            detailTitleRt.anchorMax = new Vector2(0.5f, 1f);
            detailTitleRt.pivot = new Vector2(0.5f, 1f);
            detailTitleRt.sizeDelta = new Vector2(200f, 25f);
            detailTitleRt.anchoredPosition = new Vector2(0f, -12f);

            var detailTitleTxtComp = detailTitleObj.AddComponent<TextMeshProUGUI>();
            detailTitleTxtComp.font = fontAsset;
            detailTitleTxtComp.fontSize = 13;
            detailTitleTxtComp.fontStyle = FontStyles.Bold;
            detailTitleTxtComp.alignment = TextAlignmentOptions.Center;
            detailTitleTxtComp.color = _colorTextGold;
            detailTitleTxtComp.text = "DETAIL";

            // Detay Paneli İçindeki 4 Küçük Alt Sınıf / Yetenek İkonu (Boş kutu)
            float dsStartX = -69f;
            float dsSpacingX = 46f;
            for (int i = 0; i < 4; i++)
            {
                GameObject dsSlot = new GameObject($"detail_skill_slot_{i}", typeof(RectTransform));
                dsSlot.transform.SetParent(detailPanelObj.transform, false);
                var dsRt = dsSlot.GetComponent<RectTransform>();
                dsRt.anchorMin = new Vector2(0.5f, 1f);
                dsRt.anchorMax = new Vector2(0.5f, 1f);
                dsRt.pivot = new Vector2(0.5f, 0.5f);
                dsRt.sizeDelta = new Vector2(34f, 34f);
                dsRt.anchoredPosition = new Vector2(dsStartX + i * dsSpacingX, -55f);

                var dsImg = dsSlot.AddComponent<Image>();
                dsImg.sprite = CreateRoundedRectSprite($"detail_slot_bg_{i}", 34, 34, 4, _colorBtnDark, _colorBorder * 0.4f, 1);
                dsImg.color = Color.white;
            }

            // Alt Sınıf Başlığı (Specialty)
            GameObject detailClassObj = new GameObject("DetailClassText", typeof(RectTransform));
            detailClassObj.transform.SetParent(detailPanelObj.transform, false);
            var detailClassRt = detailClassObj.GetComponent<RectTransform>();
            detailClassRt.anchorMin = new Vector2(0.5f, 1f);
            detailClassRt.anchorMax = new Vector2(0.5f, 1f);
            detailClassRt.pivot = new Vector2(0.5f, 1f);
            detailClassRt.sizeDelta = new Vector2(200f, 20f);
            detailClassRt.anchoredPosition = new Vector2(0f, -85f);

            _detailTitleTxt = detailClassObj.AddComponent<TextMeshProUGUI>();
            _detailTitleTxt.font = fontAsset;
            _detailTitleTxt.fontSize = 12;
            _detailTitleTxt.fontStyle = FontStyles.Bold;
            _detailTitleTxt.alignment = TextAlignmentOptions.Center;
            _detailTitleTxt.color = _colorTextGold;
            _detailTitleTxt.text = "SPECIALTY";

            // Seperatör Çizgisi
            GameObject divObj = new GameObject("Divider", typeof(RectTransform));
            divObj.transform.SetParent(detailPanelObj.transform, false);
            var divRt = divObj.GetComponent<RectTransform>();
            divRt.anchorMin = new Vector2(0.5f, 1f);
            divRt.anchorMax = new Vector2(0.5f, 1f);
            divRt.pivot = new Vector2(0.5f, 0.5f);
            divRt.sizeDelta = new Vector2(200f, 2f);
            divRt.anchoredPosition = new Vector2(0f, -112f);

            var divImg = divObj.AddComponent<Image>();
            divImg.sprite = CreateFadingDividerSprite("detail_div", 200, 2, _colorBorder * 0.8f);
            divImg.color = Color.white;

            // Sınıf Açıklama Metni
            GameObject descObj = new GameObject("DetailDescText", typeof(RectTransform));
            descObj.transform.SetParent(detailPanelObj.transform, false);
            var descRt = descObj.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.5f, 1f);
            descRt.anchorMax = new Vector2(0.5f, 1f);
            descRt.pivot = new Vector2(0.5f, 1f);
            descRt.sizeDelta = new Vector2(200f, 230f);
            descRt.anchoredPosition = new Vector2(0f, -125f);

            _detailDescTxt = descObj.AddComponent<TextMeshProUGUI>();
            _detailDescTxt.font = fontAsset;
            _detailDescTxt.fontSize = 11;
            _detailDescTxt.alignment = TextAlignmentOptions.TopLeft;
            _detailDescTxt.color = new Color(0.9f, 0.85f, 0.75f, 1f);
            _detailDescTxt.text = "Description of selected class.";

            // 6. Karakter 3D Görüntüleme Alanı
            GameObject areaObj = new GameObject("area_character", typeof(RectTransform));
            areaObj.transform.SetParent(_uiRoot.transform, false);
            areaObj.transform.SetAsFirstSibling();
            var areaRT = areaObj.GetComponent<RectTransform>();
            areaRT.anchorMin = new Vector2(0.5f, 0.5f);
            areaRT.anchorMax = new Vector2(0.5f, 0.5f);
            areaRT.pivot = new Vector2(0.5f, 0.5f);
            areaRT.sizeDelta = new Vector2(600f, 600f);
            areaRT.anchoredPosition = new Vector2(0f, 0f);

            SetupCharacterPreview(_uiRoot.transform);
        }

        // ============================================
        // Procedural Drawing Helpers
        // ============================================

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
            int scale = 4;
            int sw = w * scale;
            int sh = h * scale;

            int sw_s = sw;
            int sh_s = sh;

            Texture2D tex = new Texture2D(sw_s, sh_s, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cy = sh_s / 2f;
            float indent = sh_s * 0.4f;
            float cosTheta = 0.78f;

            float resScale = sh_s / 36f;
            float borderOuter = 1.5f * resScale;
            float borderInner = 3.0f * resScale;
            float shadowGap = 4.0f * resScale;

            for (int y = 0; y < sh_s; y++)
            {
                for (int x = 0; x < sw_s; x++)
                {
                    float leftBound = Mathf.Abs(y - cy) * (indent / cy);
                    float rightBound = sw_s - leftBound;

                    float distToLeft = (x - leftBound) * cosTheta;
                    float distToRight = (rightBound - x) * cosTheta;
                    float distToTop = (sh_s - 1) - y;
                    float distToBottom = y;
                    float minDist = Mathf.Min(Mathf.Min(distToLeft, distToRight), Mathf.Min(distToTop, distToBottom));

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
                        tex.SetPixel(x, y, Color.Lerp(edgeColor, Color.black, 0.6f));
                    }
                    else
                    {
                        float fade = Mathf.Abs(y - cy) / cy;
                        Color fillColor = Color.Lerp(centerColor, edgeColor, fade);

                        float hx = sw_s * 0.35f;
                        float hy = sh_s * 0.65f;
                        float hdx = (x - hx) * ((float)sh_s / sw_s);
                        float hdy = y - hy;
                        float distToHighlight = Mathf.Sqrt(hdx * hdx + hdy * hdy);
                        float highlightRadius = sh_s * 0.35f;

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
            return Sprite.Create(tex, new Rect(0f, 0f, sw_s, sh_s), new Vector2(0.5f, 0.5f));
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

        private void SelectArmorPreview(int index)
        {
            _selectedArmorIndex = index;
            for (int i = 0; i < 4; i++)
            {
                if (_armorSlots[i] != null)
                {
                    if (i == _selectedArmorIndex)
                        _armorSlots[i].sprite = CreateRoundedRectSprite($"armor_slot_active_{i}", 38, 38, 6, new Color(0.2f, 0.16f, 0.12f, 0.95f), _colorBorder, 2);
                    else
                        _armorSlots[i].sprite = CreateRoundedRectSprite($"armor_slot_inactive_{i}", 38, 38, 6, _colorBtnDark, _colorBorder * 0.4f, 1);
                }
            }
        }

        private void UpdateClassButtonVisuals()
        {
            int selectedIdx = -1;
            if (_nation == 1) // Karus
            {
                selectedIdx = _selectedClass switch
                {
                    CLASS_KA_WARRIOR => 0,
                    CLASS_KA_ROGUE => 1,
                    CLASS_KA_WIZARD => 2,
                    CLASS_KA_PRIEST => 3,
                    _ => -1
                };
            }
            else // El Morad
            {
                selectedIdx = _selectedClass switch
                {
                    CLASS_EL_WARRIOR => 0,
                    CLASS_EL_ROGUE => 1,
                    CLASS_EL_WIZARD => 2,
                    CLASS_EL_PRIEST => 3,
                    _ => -1
                };
            }

            for (int i = 0; i < 4; i++)
            {
                if (_btnClassBgs[i] != null)
                {
                    if (i == selectedIdx)
                    {
                        _btnClassBgs[i].sprite = CreateRoundedRectSprite($"class_btn_active_{i}", 200, 44, 12, new Color(0.2f, 0.16f, 0.12f, 0.95f), _colorBorder, 2);
                        if (_btnClassTexts[i] != null) _btnClassTexts[i].color = Color.white;
                    }
                    else
                    {
                        _btnClassBgs[i].sprite = CreateRoundedRectSprite($"class_btn_inactive_{i}", 200, 44, 12, _colorBtnDark, _colorBorder * 0.4f, 1);
                        if (_btnClassTexts[i] != null) _btnClassTexts[i].color = _colorTextGold;
                    }
                }
            }

            // Sağ Detay Paneli Yazılarını Güncelle
            if (selectedIdx >= 0 && selectedIdx < 4)
            {
                if (_detailTitleTxt != null) _detailTitleTxt.text = _classDetailTitles[selectedIdx];
                if (_detailDescTxt != null) _detailDescTxt.text = _classDescriptions[selectedIdx];
            }
        }

        // ============================================
        // Karakter Preview — cpp:78 InventoryChrRender(m_rcChr)
        // ============================================

        /// <summary>
        /// area_character alanına RenderTexture + kamera ile karakter model preview.
        /// C++ birebir: InventoryChrRender (PlayerMySelf.cpp:416-551)
        ///   - OrthoLH(12, 9) ortografik projeksiyon
        ///   - Point light: pos=(0,2,10), attn=0.5, diffuse=(220,255,220)/255
        ///   - LookAtLH({0,2,-10}, {0,0,0}, {0,1,0})
        /// Unity'de RenderTexture + RawImage ile aynı efekt elde ediliyor.
        /// </summary>
        private void SetupCharacterPreview(Transform root)
        {
            var areaTr = KOUIRenderer.FindChildByID(root, "area_character");
            if (areaTr == null)
            {
                Debug.LogWarning("[CREATE] area_character bulunamadı — preview yok");
                return;
            }

            var areaRT = areaTr.GetComponent<RectTransform>();
            if (areaRT == null) return;

            // area_character boyutunu al — Start() anında layout henüz hesaplanmamış olabilir
            Canvas.ForceUpdateCanvases();
            Vector2 areaSize = areaRT.rect.size;

            // Fallback: KO orijinal area_character yaklaşık 250x450 piksel (dikey dikdörtgen)
            if (areaSize.x < 10f || areaSize.y < 10f)
                areaSize = new Vector2(600f, 600f);

            float areaAspect = areaSize.x / areaSize.y; // genişlik/yükseklik
            int rtWidth = 512;
            int rtHeight = Mathf.RoundToInt(rtWidth / areaAspect);
            rtHeight = Mathf.Clamp(rtHeight, 256, 1024);

            // RenderTexture oluştur — area_character aspect ratio'suna uygun
            _previewRT = new RenderTexture(rtWidth, rtHeight, 24, RenderTextureFormat.ARGB32);
            _previewRT.antiAliasing = 4;

            // RawImage — area_character üzerine preview göster
            _previewImage = areaTr.gameObject.AddComponent<RawImage>();
            _previewImage.texture = _previewRT;
            _previewImage.raycastTarget = true;

            // Drag to rotate script'i ekle
            var rotComponent = areaTr.gameObject.AddComponent<KOInventoryChrRotate>();
            rotComponent.rotationSpeed = 0.6f;

            // Preview kamera — C++ birebir: ortografik projeksiyon
            // C++ InventoryChrRender (PlayerMySelf.cpp:452-458):
            //   mtxproj.OrthoLH(12.0f, 9.0f, 0, 100);
            //   mtxview.LookAtLH({0,2,-10}, {0,0,0}, {0,1,0});
            var camObj = new GameObject("ChrCreatePreviewCam");
            camObj.transform.SetParent(transform, false);
            // Kamera modelin biraz üstünden, +Z yönünden bakıyor
            camObj.transform.position = new Vector3(100f, 1.15f, 104f);
            camObj.transform.LookAt(new Vector3(100f, 1.15f, 100f));
            _previewCam = camObj.AddComponent<UnityEngine.Camera>();
            _previewCam.targetTexture = _previewRT;
            _previewCam.clearFlags = CameraClearFlags.SolidColor;
            _previewCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _previewCam.cullingMask = 1 << PREVIEW_LAYER;
            _previewCam.orthographic = true;
            // C++ OrthoLH(12, 9) → ortho size = height/2 = 9/2 = 4.5
            // Ama model 2.1 birime scale edilecek → 1.5 birim yeterli marjla
            _previewCam.orthographicSize = 1.7f;
            _previewCam.nearClipPlane = 0.1f;
            _previewCam.farClipPlane = 50f;

            // Preview ışık — C++ birebir: InventoryChrRender (PlayerMySelf.cpp:526-534)
            //   Light0.Type = D3DLIGHT_POINT;
            //   Light0.Attenuation0 = 0.5f;
            //   Light0.Range = 100.0f;
            //   Light0.Position = { 0.0f, 2.0f, 10.0f };
            //   Light0.Diffuse = { 220/255, 255/255, 220/255 };
            var lightObj = new GameObject("ChrCreatePreviewLight");
            lightObj.transform.SetParent(transform, false);
            // Işık pozisyonu modele göre — model (100,0,100) konumunda
            lightObj.transform.position = new Vector3(100f, 2f, 110f);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 100f;
            light.intensity = 1.0f;
            light.color = new Color(220f/255f, 255f/255f, 220f/255f);
            light.cullingMask = 1 << PREVIEW_LAYER;

        }

        /// <summary>
        /// Open-KO birebir: GameProcCharacterCreate.cpp:88-123 — SetChr()
        ///
        /// C++ akışı:
        ///   1. pLooks = s_pTbl_UPC_Looks.Find(eRace)  (satır 92)
        ///   2. s_pPlayer->InitChr(pLooks)              (satır 96) — skeleton + body
        ///   3. for (i=0..PART_POS_COUNT) PartSet(i, pLooks->szPartFNs[i])  (satır 101-114)
        ///   4. InitFace(), InitHair()                  (satır 105, 110)
        ///   5. InventoryChrAnimationInitialize()        (satır 121)
        ///   6. Action(PSA_BASIC, true)                  (satır 122) — idle anim
        ///
        /// Çağrılma yeri: UICharacterCreate.cpp:252 — race değiştirildiğinde
        ///   if (eRacePrev != pInfoBase->eRace) SetChr();
        /// </summary>
        private void UpdateCharacterPreview()
        {
            if (_previewCam == null) return;

            // Eski modeli temizle
            if (_previewModel != null)
                Destroy(_previewModel);

            if (_selectedRace == 0) return;

            // cpp:92 — pLooks = s_pTbl_UPC_Looks.Find(eRace)
            if (_upcLooksTable == null ||
                !_upcLooksTable.TryGetValue(_selectedRace, out var pLooks))
            {
                Debug.LogWarning($"[CREATE] UPC_DefaultLooks'ta race={_selectedRace} bulunamadı");
                return;
            }

            // cpp:96 — s_pPlayer->InitChr(pLooks)
            // C++ PlayerBase.cpp:2025-2055:
            //   if (!pTbl->szChrFN.empty()) m_Chr.LoadFromFile(pTbl->szChrFN);
            //   else { m_Chr.JointSet(pTbl->szJointFN); m_Chr.AniCtrlSet(pTbl->szAniFN); }
            string chrPath = null;
            if (!string.IsNullOrEmpty(pLooks.szChrFN))
                chrPath = N3CharBuilder.FindAssetFile(pLooks.szChrFN);

            if (chrPath != null && KOBinaryProvider.Exists(chrPath))
            {
                // C++ birebir: m_Chr.LoadFromFile(pTbl->szChrFN) — skeleton only
                _previewModel = N3CharBuilder.BuildWithExternalParts(
                    chrPath, new string[0]);
            }
            else if (!string.IsNullOrEmpty(pLooks.szJointFN))
            {
                // C++ birebir: else dalı — JointSet + AniCtrlSet
                string jointPath = N3CharBuilder.FindAssetFile(pLooks.szJointFN);
                _previewModel = N3CharBuilder.BuildWithJointAndAnim(
                    jointPath, pLooks.szAniFN);
            }

            if (_previewModel == null)
            {
                Debug.LogWarning($"[CREATE] Preview model oluşturulamadı (race={_selectedRace})");
                return;
            }

            // cpp:99-114 — for (i=0..PART_POS_COUNT) PartSet(i, pLooks->szPartFNs[i])
            // FACE ve HAIR özel işleniyor (InitFace/InitHair)
            for (int i = 0; i < N3CharBuilder.PART_POS_COUNT && i < pLooks.szPartFNs.Length; i++)
            {
                if (i == N3CharBuilder.PART_POS_FACE) continue;           // cpp:103-107
                if (i == N3CharBuilder.PART_POS_HAIR_HELMET) continue;     // cpp:108-112

                if (!string.IsNullOrEmpty(pLooks.szPartFNs[i]))
                {
                    string defaultPart = pLooks.szPartFNs[i];
                    defaultPart = N3CharBuilder.GetDefaultPartPath(i, _selectedRace, _selectedClass, defaultPart);
                    N3CharBuilder.PartSet(_previewModel, i, defaultPart);
                }
            }

            // cpp:105 — InitFace()
            N3CharBuilder.InitFace(_previewModel, pLooks, _face);

            // cpp:110 — InitHair()
            N3CharBuilder.InitHair(_previewModel, pLooks, _hair);

            // Sınıflara özel zırh ve silah önizlemesi giydirme
            if (_selectedClass == CLASS_KA_WARRIOR || _selectedClass == CLASS_EL_WARRIOR)
            {
                // Warrior Chitin Shell Set (+8)
                EquipPartItem(N3CharBuilder.PART_POS_UPPER, 206001008);
                EquipPartItem(N3CharBuilder.PART_POS_LOWER, 206002008);
                EquipPartItem(N3CharBuilder.PART_POS_HANDS, 206004008);
                EquipPartItem(N3CharBuilder.PART_POS_FEET, 206005008);
                EquipPartItem(N3CharBuilder.PART_POS_HAIR_HELMET, 206003008);

                // Raptor (+8)
                EquipPlugItem("PLUG_RH", 156210008, pLooks.iJointRH);
            }
            else if (_selectedClass == CLASS_KA_ROGUE || _selectedClass == CLASS_EL_ROGUE)
            {
                // Rogue Chitin Shell Set (+8)
                EquipPartItem(N3CharBuilder.PART_POS_UPPER, 246001008);
                EquipPartItem(N3CharBuilder.PART_POS_LOWER, 246002008);
                EquipPartItem(N3CharBuilder.PART_POS_HANDS, 246004008);
                EquipPartItem(N3CharBuilder.PART_POS_FEET, 246005008);
                EquipPartItem(N3CharBuilder.PART_POS_HAIR_HELMET, 246003008);

                // 2x Shard (+8)
                EquipPlugItem("PLUG_RH", 111210008, pLooks.iJointRH); // Sağ el
                EquipPlugItem("PLUG_LH", 111210008, pLooks.iJointLH); // Sol el
            }
            else if (_selectedClass == CLASS_KA_WIZARD || _selectedClass == CLASS_EL_WIZARD)
            {
                // Mage Complete Shell Set (+8)
                EquipPartItem(N3CharBuilder.PART_POS_UPPER, 266001008);
                EquipPartItem(N3CharBuilder.PART_POS_LOWER, 266002008);
                EquipPartItem(N3CharBuilder.PART_POS_HANDS, 266004008);
                EquipPartItem(N3CharBuilder.PART_POS_FEET, 266005008);
                EquipPartItem(N3CharBuilder.PART_POS_HAIR_HELMET, 266003008);

                // Elixir Staff (+8)
                EquipPlugItem("PLUG_RH", 181110008, pLooks.iJointRH);
            }
            else if (_selectedClass == CLASS_KA_PRIEST || _selectedClass == CLASS_EL_PRIEST)
            {
                // Priest Chitin Shell Set (+8)
                EquipPartItem(N3CharBuilder.PART_POS_UPPER, 286001008);
                EquipPartItem(N3CharBuilder.PART_POS_LOWER, 286002008);
                EquipPartItem(N3CharBuilder.PART_POS_HANDS, 286004008);
                EquipPartItem(N3CharBuilder.PART_POS_FEET, 286005008);
                EquipPartItem(N3CharBuilder.PART_POS_HAIR_HELMET, 286003008);

                // Priest Impact (+8) & Chitin Shield (+8)
                EquipPlugItem("PLUG_RH", 191110008, pLooks.iJointRH);
                EquipPlugItem("PLUG_LH", 170251008, pLooks.iJointLH2);
            }

            // Layer ayarla — preview kamera görsün, sahne ile çakışmasın
            SetLayerRecursive(_previewModel, PREVIEW_LAYER);

            // C++ birebir: PlayerMySelf::InitChr (PlayerMySelf.cpp:724)
            //   float fScale = 2.1f / m_Chr.Height();
            //   m_ChrInv.ScaleSet(fScale, fScale, fScale);
            // Model yüksekliğini 2.1 birime normalize et — tüm ırklar aynı boyutta görünsün
            float modelHeight = 2.0f; // fallback
            var renderers = _previewModel.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int ri = 1; ri < renderers.Length; ri++)
                    bounds.Encapsulate(renderers[ri].bounds);
                modelHeight = bounds.size.y;
            }
            float fScale = (modelHeight > 0.1f) ? 2.1f / modelHeight : 1f;
            _previewModel.transform.localScale = Vector3.one * fScale;

            // Pozisyon — kameranın baktığı yere koy
            _previewModel.transform.position = new Vector3(100f, 0f, 100f);
            // C++ birebir: InventoryChrRender (PlayerMySelf.cpp:539)
            //   qt.RotationAxis(0, 1, 0, DegreesToRadians(18.0f)); // hafif sağa dönük
            _previewModel.transform.rotation = Quaternion.Euler(0f, 18f, 0f);

            // cpp:121-122 — InventoryChrAnimationInitialize + Action(PSA_BASIC, true)
            var anim = _previewModel.GetComponent<Animation>();
            if (anim != null && anim.clip != null)
            {
                anim.clip.wrapMode = WrapMode.Loop;
                anim.Play();
            }

            // Döner bileşenin target'ını ata
            var areaTr = KOUIRenderer.FindChildByID(_uiRoot.transform, "area_character");
            if (areaTr != null)
            {
                var rotComponent = areaTr.GetComponent<KOInventoryChrRotate>();
                if (rotComponent != null)
                {
                    rotComponent.targetTransform = _previewModel.transform;
                }
            }

        }

        private KOTableReader.TableItemBasic FindItemBasic(int itemDefId)
        {
            if (KOInventory.s_pTbl_Items_Basic == null) return null;
            return KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, itemDefId);
        }

        private string MakePartResourceFileName(KOTableReader.TableItemBasic pItem, int itemDefId, byte eRace)
        {
            if (pItem == null || pItem.dwIDResrc == 0) return null;
            
            uint iIDResrc = pItem.dwIDResrc;
            if (KOInventory.s_pTbl_Items_Exts != null)
            {
                var pItemExt = KOTableReader.FindItemExt(
                    KOInventory.s_pTbl_Items_Exts, pItem.byExtIndex, itemDefId);
                if (pItemExt != null && pItemExt.dwIDResrc != 0)
                    iIDResrc = pItemExt.dwIDResrc;
            }
            
            int d1 = (int)(iIDResrc / 10000000);
            int d2 = (int)((iIDResrc / 1000) % 10000);
            int d3 = (int)((iIDResrc / 10) % 100);
            int d4 = (int)(iIDResrc % 10);
            
            int d2WithRace = d2 + eRace;
            if (eRace == 4)
            {
                string testPath = $"Item\\{d1}_{d2WithRace:D4}_{d3:D2}_{d4}.n3cpart";
                bool exists = EntropyOnline.Import.KOBinaryProvider.Exists(testPath);
                Debug.Log($"[DEBUG_MAGE] itemDefId={itemDefId}, d1={d1}, d2={d2}, d2WithRace={d2WithRace}, d3={d3}, d4={d4}, testPath={testPath}, exists={exists}");
                if (!exists)
                {
                    d2WithRace = d2 + 13;
                }
            }
            
            return $"Item\\{d1}_{d2WithRace:D4}_{d3:D2}_{d4}.n3cpart";
        }

        private string MakePlugResourceFileName(KOTableReader.TableItemBasic pItem, int itemDefId)
        {
            if (pItem == null || pItem.dwIDResrc == 0) return null;

            uint iIDResrc = pItem.dwIDResrc;
            if (KOInventory.s_pTbl_Items_Exts != null)
            {
                var pItemExt = KOTableReader.FindItemExt(
                    KOInventory.s_pTbl_Items_Exts, pItem.byExtIndex, itemDefId);
                if (pItemExt != null && pItemExt.dwIDResrc != 0)
                    iIDResrc = pItemExt.dwIDResrc;
            }

            int d1 = (int)(iIDResrc / 10000000);
            int d2 = (int)((iIDResrc / 1000) % 10000);
            int d3 = (int)((iIDResrc / 10) % 100);
            int d4 = (int)(iIDResrc % 10);

            return $"Item\\{d1}_{d2:D4}_{d3:D2}_{d4}.n3cplug";
        }

        private void EquipPartItem(int partPos, int itemId)
        {
            if (_previewModel == null) return;
            var pItem = FindItemBasic(itemId);
            if (pItem == null) return;
            string szFN = MakePartResourceFileName(pItem, itemId, _selectedRace);
            if (!string.IsNullOrEmpty(szFN))
            {
                N3CharBuilder.PartSet(_previewModel, partPos, szFN);
            }
        }

        private void EquipPlugItem(string plugTag, int itemId, int jointIndex)
        {
            if (_previewModel == null) return;
            var pItem = FindItemBasic(itemId);
            if (pItem == null) return;
            string szFN = MakePlugResourceFileName(pItem, itemId);
            if (!string.IsNullOrEmpty(szFN))
            {
                N3CharBuilder.PlugSet(_previewModel, szFN, jointIndex, plugTag);
            }
        }

        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            for (int i = 0; i < obj.transform.childCount; i++)
                SetLayerRecursive(obj.transform.GetChild(i).gameObject, layer);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // cpp:302 — DIK_RETURN → MsgSendCharacterCreate
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                OnCreateClicked();
            // Escape → geri
            if (kb.escapeKey.wasPressedThisFrame)
                OnBackClicked();
        }

        // ============================================
        // Race seçimi — UICharacterCreate.cpp:207-247
        // ============================================

        private void SelectRace(byte race)
        {
            byte prevRace = _selectedRace;
            _selectedRace = race;
            _selectedClass = 0;
            ResetStats();

            // Class availability güncelle
            UpdateClassAvailability();

            // cpp:251-252 — if (eRacePrev != pInfoBase->eRace) SetChr();
            if (prevRace != _selectedRace)
                UpdateCharacterPreview();

        }

        private void UpdateClassAvailability()
        {
            if (_selectedRace == 0) return;

            bool[] allowed = RaceClassMap.ContainsKey(_selectedRace)
                ? RaceClassMap[_selectedRace]
                : new[] { false, false, false, false };

            for (int i = 0; i < 4; i++)
            {
                if (_btnClasses[i] != null)
                    _btnClasses[i].interactable = allowed[i];
            }
        }

        // ============================================
        // Class seçimi — UICharacterCreate.cpp:299-332
        // ============================================

        private void SelectClassIndex(int classIndex)
        {
            byte autoRace = 0;
            if (_nation == 1) // Karus
            {
                autoRace = classIndex switch
                {
                    0 => RACE_KA_ARKTUAREK,     // Warrior -> Ark Tuarek (1)
                    1 => RACE_KA_TUAREK,        // Rogue -> Tuarek (2)
                    2 => RACE_KA_PURITUAREK,    // Mage -> Puri Tuarek (4)
                    3 => RACE_KA_PURITUAREK,    // Priest -> Puri Tuarek (4)
                    _ => 0
                };
            }
            else // El Morad
            {
                autoRace = classIndex switch
                {
                    0 => RACE_EL_MAN,           // Warrior -> El Morad Man (12)
                    1 => RACE_EL_MAN,           // Rogue -> El Morad Man (12)
                    2 => RACE_EL_WOMEN,         // Mage -> El Morad Women (13)
                    3 => RACE_EL_WOMEN,         // Priest -> El Morad Women (13)
                    _ => 0
                };
            }

            if (autoRace == 0) return;

            byte prevRace = _selectedRace;
            _selectedRace = autoRace;

            if (_nation == 1)
            {
                _selectedClass = classIndex switch
                {
                    0 => CLASS_KA_WARRIOR,
                    1 => CLASS_KA_ROGUE,
                    2 => CLASS_KA_WIZARD,
                    3 => CLASS_KA_PRIEST,
                    _ => 0
                };
            }
            else
            {
                _selectedClass = classIndex switch
                {
                    0 => CLASS_EL_WARRIOR,
                    1 => CLASS_EL_ROGUE,
                    2 => CLASS_EL_WIZARD,
                    3 => CLASS_EL_PRIEST,
                    _ => 0
                };
            }

            // Sınıf veya ırk değiştiğinde preview modelini güncelle
            ResetStats();
            UpdateCharacterPreview();

            LoadStatsFromTable();

            UpdateClassButtonVisuals();
        }

        // ============================================
        // Stats — GameProcCharacterCreate.cpp:125-145
        // ============================================

        private void LoadStatsFromTable()
        {
            uint key = (uint)(_selectedRace * 10000 + _selectedClass);

            if (_newChrTable != null && _newChrTable.TryGetValue(key, out var tbl))
            {
                _baseStr = tbl.iStr; _str = tbl.iStr;
                _baseSta = tbl.iSta; _sta = tbl.iSta;
                _baseDex = tbl.iDex; _dex = tbl.iDex;
                _baseInt = tbl.iInt; _int = tbl.iInt;
                _baseCha = tbl.iMAP; _cha = tbl.iMAP;
                _bonusPoint = tbl.iBonus;
                _maxBonusPoint = tbl.iBonus;

                // Seçilen sınıfa göre bonus stat puanlarını otomatik dağıt
                int points = _bonusPoint;
                if (points > 0)
                {
                    if (_selectedClass == CLASS_KA_WARRIOR || _selectedClass == CLASS_EL_WARRIOR)
                    {
                        _str += points;
                    }
                    else if (_selectedClass == CLASS_KA_ROGUE || _selectedClass == CLASS_EL_ROGUE)
                    {
                        _dex += points;
                    }
                    else if (_selectedClass == CLASS_KA_WIZARD || _selectedClass == CLASS_EL_WIZARD)
                    {
                        _cha += points; // Magic Power (iMAP/cha)
                    }
                    else if (_selectedClass == CLASS_KA_PRIEST || _selectedClass == CLASS_EL_PRIEST)
                    {
                        _int += points;
                    }
                    _bonusPoint = 0; // Puanlar dağıtıldı
                }
            }
            else
            {
                Debug.LogWarning($"[CREATE] NewChrValue key={key} bulunamadı");
                _baseStr = _str = 0; _baseSta = _sta = 0;
                _baseDex = _dex = 0; _baseInt = _int = 0;
                _baseCha = _cha = 0; _bonusPoint = _maxBonusPoint = 0;
            }

            UpdateStatTexts();
        }

        /// <summary>
        /// cpp:338-401 — btn_X_right → stat artır
        /// </summary>
        private void StatUp(int statIndex)
        {
            if (_bonusPoint <= 0) return;

            switch (statIndex)
            {
                case 0: _str++; break;
                case 1: _sta++; break;
                case 2: _dex++; break;
                case 3: _int++; break;
                case 4: _cha++; break;
            }
            _bonusPoint--;
            UpdateStatTexts();
        }

        /// <summary>
        /// cpp:410-474 — btn_X_left → stat azalt (base'den aşağı inemez)
        /// </summary>
        private void StatDown(int statIndex)
        {
            if (_bonusPoint >= _maxBonusPoint) return;

            int[] bases = { _baseStr, _baseSta, _baseDex, _baseInt, _baseCha };
            int[] stats = { _str, _sta, _dex, _int, _cha };

            if (stats[statIndex] <= bases[statIndex]) return;

            switch (statIndex)
            {
                case 0: _str--; break;
                case 1: _sta--; break;
                case 2: _dex--; break;
                case 3: _int--; break;
                case 4: _cha--; break;
            }
            _bonusPoint++;
            UpdateStatTexts();
        }

        /// <summary>
        /// cpp:269-280 — btn_face_left/right
        /// cpp:294-295 — if (iFacePrev != pInfoExt->iFace) InitFace()
        /// </summary>
        private void ChangeFace(int delta)
        {
            byte prevFace = _face;
            int f = _face + delta;
            _face = (byte)Mathf.Clamp(f, 0, 3); // cpp:272-279

            // cpp:294-295 — yüz değiştiyse preview güncelle
            if (prevFace != _face && _previewModel != null && _selectedRace > 0 &&
                _upcLooksTable != null && _upcLooksTable.TryGetValue(_selectedRace, out var pLooks))
            {
                N3CharBuilder.InitFace(_previewModel, pLooks, _face);
                SetLayerRecursive(_previewModel, PREVIEW_LAYER);
            }
        }

        /// <summary>
        /// cpp:281-292 — btn_hair_left/right
        /// cpp:296-297 — if (iHairPrev != pInfoExt->iHair) InitHair()
        /// </summary>
        private void ChangeHair(int delta)
        {
            byte prevHair = _hair;
            int h = _hair + delta;
            _hair = (byte)Mathf.Clamp(h, 0, 7); // cpp:284-291

            // cpp:296-297 — saç değiştiyse preview güncelle
            if (prevHair != _hair && _previewModel != null && _selectedRace > 0 &&
                _upcLooksTable != null && _upcLooksTable.TryGetValue(_selectedRace, out var pLooks))
            {
                N3CharBuilder.InitHair(_previewModel, pLooks, _hair);
                SetLayerRecursive(_previewModel, PREVIEW_LAYER);
            }
        }

        private void ResetStats()
        {
            _str = _sta = _dex = _int = _cha = 0;
            _baseStr = _baseSta = _baseDex = _baseInt = _baseCha = 0;
            _bonusPoint = _maxBonusPoint = 0;
            _face = 0; _hair = 0;
            UpdateStatTexts();
        }

        private void UpdateStatTexts()
        {
            if (_textStats[0] != null) _textStats[0].text = _str.ToString();
            if (_textStats[1] != null) _textStats[1].text = _sta.ToString();
            if (_textStats[2] != null) _textStats[2].text = _dex.ToString();
            if (_textStats[3] != null) _textStats[3].text = _int.ToString();
            if (_textStats[4] != null) _textStats[4].text = _cha.ToString();
        }

        // ============================================
        // Karakter oluştur — GameProcCharacterCreate.cpp:175-281
        // ============================================

        private void OnCreateClicked()
        {
            string name = _editName?.text?.Trim() ?? "";

            if (string.IsNullOrEmpty(name))
            {
                KOLoginMessageBox.Show(_canvas, "Character name cannot be empty!");
                return;
            }
            if (_selectedRace == 0)
            {
                KOLoginMessageBox.Show(_canvas, "You must select a race!");
                return;
            }
            if (_selectedClass == 0)
            {
                KOLoginMessageBox.Show(_canvas, "You must select a class!");
                return;
            }
            if (_bonusPoint > 0)
            {
                KOLoginMessageBox.Show(_canvas, "You still have bonus points!");
                return;
            }

            // Open-KO birebir: IsValidName (User.cpp:5439-5464)
            // Character names must only contain alphanumeric English characters (A-Z, a-z, 0-9)
            foreach (char c in name)
            {
                bool isValidChar = (c >= 'a' && c <= 'z') || 
                                   (c >= 'A' && c <= 'Z') || 
                                   (c >= '0' && c <= '9');
                if (!isValidChar)
                {
                    KOLoginMessageBox.Show(_canvas, "Invalid character name!");
                    return;
                }
            }


            // Open-KO birebir: WIZ_NEW_CHAR (User.cpp:869-961)
            KONetworkManager.Instance?.SendNewChar(
                PendingCharIndex,
                name,
                _selectedRace,
                _selectedClass,
                _face,
                _hair,
                (byte)_str,
                (byte)_sta,
                (byte)_dex,
                (byte)_int,
                (byte)_cha
            );
        }

        // ============================================
        // Server yanıtı — GameProcCharacterCreate.cpp:314-346
        // ============================================

        private void HandleCreateCharacterResponse(byte result)
        {
            if (result == 0x00)
            {
                GoBackToCharacterSelect();
            }
            else
            {
                string msg = (result < ErrorMessages.Length && ErrorMessages[result] != null)
                    ? ErrorMessages[result]
                    : $"Error (0x{result:X2})";
                KOLoginMessageBox.Show(_canvas, msg);
            }
        }

        // ============================================
        // Navigation
        // ============================================

        private void GoBackToCharacterSelect()
        {
            gameObject.SetActive(false);
            var selectUI = FindAnyObjectByType<CharacterSelectUI>(FindObjectsInactive.Include);
            if (selectUI != null)
            {
                selectUI.gameObject.SetActive(true);
            }
            else
            {
                // CharacterSelectUI henüz sahneye eklenmemiş — dinamik oluştur
                var selectObj = new GameObject("CharacterSelectUI");
                selectUI = selectObj.AddComponent<CharacterSelectUI>();
            }
        }

        private void OnBackClicked()
        {
            GoBackToCharacterSelect();
        }
    }
}
