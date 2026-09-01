using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Network.KO;
using EntropyOnline.Core;
using EntropyOnline.World;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Tapınak Etkinlikleri (BDW, Juraid Mountain, Chaos Dungeon) için istemci arayüzü.
    /// Programatik olarak şık ve modern (koyu tema, altın çerçeveli) UI elemanlarını oluşturur ve yönetir.
    /// </summary>
    public class KOTempleEventUI : MonoBehaviour
    {
        public static KOTempleEventUI Instance { get; private set; }

        // Opcodes & Constants
        private const byte TEMPLE_EVENT = 7;
        private const byte TEMPLE_EVENT_JOIN = 8;
        private const byte TEMPLE_EVENT_DISBAND = 9;
        private const byte TEMPLE_EVENT_COUNTER = 16;

        private const byte ZONE_BDW = 84;
        private const byte ZONE_CHAOS = 85;
        private const byte ZONE_JURAID = 87;

        private const short EVENT_BDW = 4;
        private const short EVENT_CHAOS = 24;
        private const short EVENT_JURAID = 100;

        // UI GameObjects
        private GameObject _invitationPanel;
        private GameObject _scoreboardPanel;
        private GameObject _announcementOverlay;
        private bool _isMinimized = false;
        private GameObject _miniTabPanel;
        private Text _miniTabText;
        private Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        // Invitation Panel UI References
        private Text _invTitleText;
        private Text _invTimerText;
        private Text _invQueueText;
        private Button _registerButton;
        private Button _cancelButton;
        private Text _registerBtnText;
        private Text _cancelBtnText;

        // Scoreboard Panel UI References
        private Text _scoreTitleText;
        private Text _scoreTimerText;
        private Text _scoreKarusText;
        private Text _scoreElmoText;
        private Text _scoreExtraText;

        // Announcement Overlay references
        private Text _announcementText;
        private float _announcementFadeTimer;

        // Active State variables
        private short _activeEventId = -1;
        private float _invitationTimeLeft = 0f;
        private bool _isRegistered = false;
        private short _queueKarusCount = 0;
        private short _queueElmoCount = 0;
        private short _queueChaosCount = 0;

        // In-Event variables
        private float _eventTimeLeft = 1200f; // 20 minutes default
        private short _bdwKarusScore = 0;
        private short _bdwElmoScore = 0;
        private byte _bdwAltarControlNation = 0; // 0 = Neutral, 1 = Karus, 2 = Elmo
        private int _chaosKills = 0;
        private int _chaosDeaths = 0;
        private int _juraidStage = 1;

        private bool _uiInitialized = false;
        private Vector3 _invTargetPos;
        private Vector3 _invStartPos;
        private bool _slideInCompleted = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoInit()
        {
            var go = new GameObject("KOTempleEventUI");
            Instance = go.AddComponent<KOTempleEventUI>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            KOPacketHandler.OnEvent += OnEventPacket;
            KOPacketHandler.OnCapture += OnCapturePacket;
            KOPacketHandler.OnDead += OnDeadPacket;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnEvent -= OnEventPacket;
            KOPacketHandler.OnCapture -= OnCapturePacket;
            KOPacketHandler.OnDead -= OnDeadPacket;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name == "GameScene")
            {
                _uiInitialized = false;
                StartCoroutine(InitializeUIWhenCanvasReady());
            }
        }

        private void Start()
        {
            StartCoroutine(InitializeUIWhenCanvasReady());
        }

        private IEnumerator InitializeUIWhenCanvasReady()
        {
            Transform canvasTransform = null;
            while (canvasTransform == null)
            {
                if (KOUIManager.Instance != null && KOUIManager.Instance.Canvas != null)
                {
                    canvasTransform = KOUIManager.Instance.Canvas.transform;
                }
                else
                {
                    GameObject canvasObj = GameObject.Find("KOCanvas");
                    if (canvasObj != null)
                    {
                        canvasTransform = canvasObj.transform;
                    }
                }
                yield return new WaitForSeconds(0.5f);
            }

            BuildUI(canvasTransform);
        }

        private Font GetUIFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            }
            return font;
        }

        private void BuildUI(Transform parent)
        {
            Font font = GetUIFont();

            // -------------------------------------------------------------
            // 1. INVITATION PANEL (Slide-in)
            // -------------------------------------------------------------
            _invitationPanel = new GameObject("TempleInvitationPanel");
            _invitationPanel.transform.SetParent(parent, false);
            var invRT = _invitationPanel.AddComponent<RectTransform>();
            invRT.anchorMin = new Vector2(0.5f, 0.5f);
            invRT.anchorMax = new Vector2(0.5f, 0.5f);
            invRT.pivot = new Vector2(0.5f, 0.5f); // Merkez pivot
            invRT.sizeDelta = new Vector2(250, 140);
            
            _invStartPos = new Vector3(800, 0, 0); // Başlangıçta ekranın sağında dışarıda
            _invTargetPos = new Vector3(0, 0, 0);   // Ekranda tam ortalanmış pozisyon
            invRT.localPosition = _invStartPos;

            // Background (Dark glassmorphism style)
            var invBg = _invitationPanel.AddComponent<Image>();
            invBg.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);

            // Drag Area (Sürüklenebilir alan - panelin tamamını kaplar ama arka plandadır)
            var dragArea = new GameObject("DragArea", typeof(RectTransform));
            dragArea.transform.SetParent(_invitationPanel.transform, false);
            StretchUI(dragArea);
            var dragImg = dragArea.AddComponent<Image>();
            dragImg.color = Color.clear; // şeffaf
            dragArea.AddComponent<UIDragHandler>();

            // Outline (Golden border)
            var invOutline = new GameObject("Outline").AddComponent<Image>();
            invOutline.transform.SetParent(_invitationPanel.transform, false);
            var outlineRT = invOutline.GetComponent<RectTransform>();
            outlineRT.anchorMin = Vector2.zero;
            outlineRT.anchorMax = Vector2.one;
            outlineRT.sizeDelta = new Vector2(-4, -4);
            invOutline.color = new Color(0.83f, 0.69f, 0.22f, 0.6f); // Antique Gold
            invOutline.raycastTarget = false;

            var innerBg = new GameObject("InnerBg").AddComponent<Image>();
            innerBg.transform.SetParent(invOutline.transform, false);
            var innerRT = innerBg.GetComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.sizeDelta = new Vector2(-2, -2);
            innerBg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
            innerBg.raycastTarget = false;

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_invitationPanel.transform, false);
            _invTitleText = titleGo.AddComponent<Text>();
            _invTitleText.font = font;
            _invTitleText.text = "EVENT INVITATION";
            _invTitleText.color = new Color(0.83f, 0.69f, 0.22f, 1f);
            _invTitleText.fontSize = 11;
            _invTitleText.alignment = TextAnchor.MiddleCenter;
            _invTitleText.raycastTarget = false;
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 0.72f);
            titleRT.anchorMax = new Vector2(1, 0.92f);
            titleRT.sizeDelta = Vector2.zero;
            titleGo.AddComponent<Shadow>().effectColor = Color.black;

            // Minimize (Simge Durumuna Küçült) Butonu
            var minObj = new GameObject("MinBtn", typeof(RectTransform));
            minObj.transform.SetParent(_invitationPanel.transform, false);
            var minRect = minObj.GetComponent<RectTransform>();
            minRect.anchorMin = new Vector2(1f, 1f);
            minRect.anchorMax = new Vector2(1f, 1f);
            minRect.pivot = new Vector2(1f, 1f);
            minRect.anchoredPosition = new Vector2(-6, -6);
            minRect.sizeDelta = new Vector2(18, 18);

            var minImg = minObj.AddComponent<Image>();
            minImg.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);

            var minOutline = new GameObject("Outline").AddComponent<Image>();
            minOutline.transform.SetParent(minObj.transform, false);
            var minOutlineRT = minOutline.GetComponent<RectTransform>();
            minOutlineRT.anchorMin = Vector2.zero;
            minOutlineRT.anchorMax = Vector2.one;
            minOutlineRT.sizeDelta = new Vector2(-2, -2);
            minOutline.color = new Color(0.83f, 0.69f, 0.22f, 0.6f);
            minOutline.raycastTarget = false;

            var minTxtGo = new GameObject("Text", typeof(RectTransform));
            minTxtGo.transform.SetParent(minObj.transform, false);
            StretchUI(minTxtGo);
            var minTxt = minTxtGo.AddComponent<Text>();
            minTxt.font = font;
            minTxt.fontSize = 11;
            minTxt.fontStyle = FontStyle.Bold;
            minTxt.alignment = TextAnchor.MiddleCenter;
            minTxt.color = new Color(0.85f, 0.85f, 0.7f);
            minTxt.text = "_"; // Küçültme simgesi
            minTxtGo.AddComponent<Shadow>().effectColor = Color.black;

            var minBtn = minObj.AddComponent<Button>();
            minBtn.onClick.AddListener(() => {
                _isMinimized = true;
            });

            // Timer Text
            var timerGo = new GameObject("Timer");
            timerGo.transform.SetParent(_invitationPanel.transform, false);
            _invTimerText = timerGo.AddComponent<Text>();
            _invTimerText.font = font;
            _invTimerText.text = "Kalan Süre: 00:00";
            _invTimerText.color = Color.white;
            _invTimerText.fontSize = 10;
            _invTimerText.alignment = TextAnchor.MiddleCenter;
            _invTimerText.raycastTarget = false;
            var timerRT = timerGo.GetComponent<RectTransform>();
            timerRT.anchorMin = new Vector2(0, 0.50f);
            timerRT.anchorMax = new Vector2(1, 0.70f);
            timerRT.sizeDelta = Vector2.zero;

            // Queue Text
            var queueGo = new GameObject("Queue");
            queueGo.transform.SetParent(_invitationPanel.transform, false);
            _invQueueText = queueGo.AddComponent<Text>();
            _invQueueText.font = font;
            _invQueueText.text = "Kayıtlı: Karus: 0 | El Morad: 0";
            _invQueueText.color = new Color(0.7f, 0.7f, 0.8f, 1f);
            _invQueueText.fontSize = 9;
            _invQueueText.alignment = TextAnchor.MiddleCenter;
            _invQueueText.raycastTarget = false;
            var queueRT = queueGo.GetComponent<RectTransform>();
            queueRT.anchorMin = new Vector2(0, 0.32f);
            queueRT.anchorMax = new Vector2(1, 0.48f);
            queueRT.sizeDelta = Vector2.zero;

            // Buttons Container
            var buttonsContainer = new GameObject("Buttons").AddComponent<RectTransform>();
            buttonsContainer.SetParent(_invitationPanel.transform, false);
            buttonsContainer.anchorMin = new Vector2(0.05f, 0.05f);
            buttonsContainer.anchorMax = new Vector2(0.95f, 0.28f);
            buttonsContainer.sizeDelta = Vector2.zero;

            // Register Button
            var regGo = new GameObject("RegisterButton");
            regGo.transform.SetParent(buttonsContainer, false);
            _registerButton = regGo.AddComponent<Button>();
            var regImg = regGo.AddComponent<Image>();
            regImg.color = new Color(0.15f, 0.4f, 0.15f, 0.9f);
            _registerButton.targetGraphic = regImg;
            _registerButton.onClick.AddListener(OnRegisterClicked);
            
            var regTxtGo = new GameObject("Text");
            regTxtGo.transform.SetParent(regGo.transform, false);
            _registerBtnText = regTxtGo.AddComponent<Text>();
            _registerBtnText.font = font;
            _registerBtnText.text = "Kayıt Ol";
            _registerBtnText.color = Color.white;
            _registerBtnText.fontSize = 11;
            _registerBtnText.alignment = TextAnchor.MiddleCenter;
            var regTxtRT = regTxtGo.GetComponent<RectTransform>();
            regTxtRT.anchorMin = Vector2.zero;
            regTxtRT.anchorMax = Vector2.one;
            regTxtRT.sizeDelta = Vector2.zero;

            var regRT = regGo.GetComponent<RectTransform>();
            regRT.anchorMin = new Vector2(0, 0);
            regRT.anchorMax = new Vector2(0.48f, 1);
            regRT.sizeDelta = Vector2.zero;

            // Cancel Button
            var canGo = new GameObject("CancelButton");
            canGo.transform.SetParent(buttonsContainer, false);
            _cancelButton = canGo.AddComponent<Button>();
            var canImg = canGo.AddComponent<Image>();
            canImg.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);
            _cancelButton.targetGraphic = canImg;
            _cancelButton.onClick.AddListener(OnCancelClicked);

            var canTxtGo = new GameObject("Text");
            canTxtGo.transform.SetParent(canGo.transform, false);
            _cancelBtnText = canTxtGo.AddComponent<Text>();
            _cancelBtnText.font = font;
            _cancelBtnText.text = "İptal";
            _cancelBtnText.color = Color.white;
            _cancelBtnText.fontSize = 11;
            _cancelBtnText.alignment = TextAnchor.MiddleCenter;
            var canTxtRT = canTxtGo.GetComponent<RectTransform>();
            canTxtRT.anchorMin = Vector2.zero;
            canTxtRT.anchorMax = Vector2.one;
            canTxtRT.sizeDelta = Vector2.zero;

            var canRT = canGo.GetComponent<RectTransform>();
            canRT.anchorMin = new Vector2(0.52f, 0);
            canRT.anchorMax = new Vector2(1, 1);
            canRT.sizeDelta = Vector2.zero;

            _invitationPanel.SetActive(false);

            // -------------------------------------------------------------
            // 1.5 MINI TAB PANEL (Minimized Event Indicator)
            // -------------------------------------------------------------
            _miniTabPanel = new GameObject("TempleMiniTabPanel");
            _miniTabPanel.transform.SetParent(parent, false);
            var miniRT = _miniTabPanel.AddComponent<RectTransform>();
            miniRT.anchorMin = new Vector2(0, 0.5f);
            miniRT.anchorMax = new Vector2(0, 0.5f);
            miniRT.pivot = new Vector2(0, 0.5f);
            miniRT.anchoredPosition = new Vector2(10, 120f); // Ekranın sol kenarında, 10px pay ve ortadan 120px yukarıda
            miniRT.sizeDelta = new Vector2(75, 40); // small size

            var miniBg = _miniTabPanel.AddComponent<Image>();
            miniBg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            
            var miniOutline = new GameObject("Outline").AddComponent<Image>();
            miniOutline.transform.SetParent(_miniTabPanel.transform, false);
            var miniOutlineRT = miniOutline.GetComponent<RectTransform>();
            miniOutlineRT.anchorMin = Vector2.zero;
            miniOutlineRT.anchorMax = Vector2.one;
            miniOutlineRT.sizeDelta = new Vector2(-4, -4);
            miniOutline.color = new Color(0.83f, 0.69f, 0.22f, 0.7f); // Antique Gold border
            miniOutline.raycastTarget = false;

            var miniInner = new GameObject("InnerBg").AddComponent<Image>();
            miniInner.transform.SetParent(miniOutline.transform, false);
            var miniInnerRT = miniInner.GetComponent<RectTransform>();
            miniInnerRT.anchorMin = Vector2.zero;
            miniInnerRT.anchorMax = Vector2.one;
            miniInnerRT.sizeDelta = new Vector2(-2, -2);
            miniInner.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);
            miniInner.raycastTarget = false;

            var miniTxtGo = new GameObject("Text", typeof(RectTransform));
            miniTxtGo.transform.SetParent(_miniTabPanel.transform, false);
            var miniTxtRT = miniTxtGo.GetComponent<RectTransform>();
            miniTxtRT.anchorMin = Vector2.zero;
            miniTxtRT.anchorMax = Vector2.one;
            miniTxtRT.sizeDelta = Vector2.zero;

            _miniTabText = miniTxtGo.AddComponent<Text>();
            _miniTabText.font = font;
            _miniTabText.fontSize = 10;
            _miniTabText.fontStyle = FontStyle.Bold;
            _miniTabText.alignment = TextAnchor.MiddleCenter;
            _miniTabText.color = new Color(0.9f, 0.8f, 0.5f);
            _miniTabText.text = "Etkinlik\n00:00";
            _miniTabText.raycastTarget = false;
            miniTxtGo.AddComponent<Shadow>().effectColor = Color.black;

            var miniBtn = _miniTabPanel.AddComponent<Button>();
            miniBtn.onClick.AddListener(() => {
                _isMinimized = false;
                _miniTabPanel.SetActive(false);
            });

            _miniTabPanel.SetActive(false);

            // -------------------------------------------------------------
            // 2. SCOREBOARD PANEL (Top-right overlay in event zone)
            // -------------------------------------------------------------
            _scoreboardPanel = new GameObject("TempleScoreboardPanel");
            _scoreboardPanel.transform.SetParent(parent, false);
            var scoreRT = _scoreboardPanel.AddComponent<RectTransform>();
            scoreRT.anchorMin = new Vector2(1, 1);
            scoreRT.anchorMax = new Vector2(1, 1);
            scoreRT.pivot = new Vector2(1, 1);
            scoreRT.sizeDelta = new Vector2(250, 120);
            scoreRT.localPosition = new Vector3(-20, -100, 0);

            var scoreBg = _scoreboardPanel.AddComponent<Image>();
            scoreBg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            scoreBg.raycastTarget = false;

            var scoreOutline = new GameObject("Outline").AddComponent<Image>();
            scoreOutline.transform.SetParent(_scoreboardPanel.transform, false);
            var scoreOutlineRT = scoreOutline.GetComponent<RectTransform>();
            scoreOutlineRT.anchorMin = Vector2.zero;
            scoreOutlineRT.anchorMax = Vector2.one;
            scoreOutlineRT.sizeDelta = new Vector2(-2, -2);
            scoreOutline.color = new Color(0.5f, 0.5f, 0.6f, 0.4f);
            scoreOutline.raycastTarget = false;

            // Score Title
            var sTitleGo = new GameObject("ScoreTitle");
            sTitleGo.transform.SetParent(_scoreboardPanel.transform, false);
            _scoreTitleText = sTitleGo.AddComponent<Text>();
            _scoreTitleText.font = font;
            _scoreTitleText.text = "BORDER DEFENSE WAR";
            _scoreTitleText.color = new Color(0.9f, 0.75f, 0.15f, 1f);
            _scoreTitleText.fontSize = 13;
            _scoreTitleText.alignment = TextAnchor.MiddleCenter;
            _scoreTitleText.raycastTarget = false;
            var sTitleRT = sTitleGo.GetComponent<RectTransform>();
            sTitleRT.anchorMin = new Vector2(0, 0.75f);
            sTitleRT.anchorMax = new Vector2(1, 0.95f);
            sTitleRT.sizeDelta = Vector2.zero;

            // Score Timer
            var sTimerGo = new GameObject("ScoreTimer");
            sTimerGo.transform.SetParent(_scoreboardPanel.transform, false);
            _scoreTimerText = sTimerGo.AddComponent<Text>();
            _scoreTimerText.font = font;
            _scoreTimerText.text = "Kalan Süre: 20:00";
            _scoreTimerText.color = Color.white;
            _scoreTimerText.fontSize = 12;
            _scoreTimerText.alignment = TextAnchor.MiddleCenter;
            _scoreTimerText.raycastTarget = false;
            var sTimerRT = sTimerGo.GetComponent<RectTransform>();
            sTimerRT.anchorMin = new Vector2(0, 0.52f);
            sTimerRT.anchorMax = new Vector2(1, 0.72f);
            sTimerRT.sizeDelta = Vector2.zero;

            // Karus Score / Stats
            var sKarusGo = new GameObject("KarusScore");
            sKarusGo.transform.SetParent(_scoreboardPanel.transform, false);
            _scoreKarusText = sKarusGo.AddComponent<Text>();
            _scoreKarusText.font = font;
            _scoreKarusText.text = "Karus: 0";
            _scoreKarusText.color = new Color(1f, 0.3f, 0.3f, 1f); // Red
            _scoreKarusText.fontSize = 14;
            _scoreKarusText.alignment = TextAnchor.MiddleCenter;
            _scoreKarusText.raycastTarget = false;
            var sKarusRT = sKarusGo.GetComponent<RectTransform>();
            sKarusRT.anchorMin = new Vector2(0, 0.25f);
            sKarusRT.anchorMax = new Vector2(0.5f, 0.48f);
            sKarusRT.sizeDelta = Vector2.zero;

            // El Morad Score / Stats
            var sElmoGo = new GameObject("ElmoScore");
            sElmoGo.transform.SetParent(_scoreboardPanel.transform, false);
            _scoreElmoText = sElmoGo.AddComponent<Text>();
            _scoreElmoText.font = font;
            _scoreElmoText.text = "El Morad: 0";
            _scoreElmoText.color = new Color(0.3f, 0.5f, 1f, 1f); // Blue
            _scoreElmoText.fontSize = 14;
            _scoreElmoText.alignment = TextAnchor.MiddleCenter;
            _scoreElmoText.raycastTarget = false;
            var sElmoRT = sElmoGo.GetComponent<RectTransform>();
            sElmoRT.anchorMin = new Vector2(0.5f, 0.25f);
            sElmoRT.anchorMax = new Vector2(1, 0.48f);
            sElmoRT.sizeDelta = Vector2.zero;

            // Altar / Extra Text
            var sExtraGo = new GameObject("AltarState");
            sExtraGo.transform.SetParent(_scoreboardPanel.transform, false);
            _scoreExtraText = sExtraGo.AddComponent<Text>();
            _scoreExtraText.font = font;
            _scoreExtraText.text = "Altar Kontrolü: BOŞTA";
            _scoreExtraText.color = Color.gray;
            _scoreExtraText.fontSize = 11;
            _scoreExtraText.alignment = TextAnchor.MiddleCenter;
            _scoreExtraText.raycastTarget = false;
            var sExtraRT = sExtraGo.GetComponent<RectTransform>();
            sExtraRT.anchorMin = new Vector2(0, 0.02f);
            sExtraRT.anchorMax = new Vector2(1, 0.22f);
            sExtraRT.sizeDelta = Vector2.zero;

            _scoreboardPanel.SetActive(false);

            // -------------------------------------------------------------
            // 3. ANNOUNCEMENT OVERLAY (Center banner)
            // -------------------------------------------------------------
            _announcementOverlay = new GameObject("TempleAnnouncementOverlay");
            _announcementOverlay.transform.SetParent(parent, false);
            var annRT = _announcementOverlay.AddComponent<RectTransform>();
            annRT.anchorMin = new Vector2(0, 0.7f);
            annRT.anchorMax = new Vector2(1, 0.8f);
            annRT.sizeDelta = Vector2.zero;

            _announcementText = _announcementOverlay.AddComponent<Text>();
            _announcementText.font = font;
            _announcementText.text = "";
            _announcementText.color = new Color(1f, 0.85f, 0f, 0f); // Initially transparent
            _announcementText.fontSize = 22;
            _announcementText.alignment = TextAnchor.MiddleCenter;
            _announcementText.raycastTarget = false;
            _announcementOverlay.AddComponent<Shadow>().effectColor = Color.black;

            _uiInitialized = true;
        }

        private void Update()
        {
            if (!_uiInitialized || _invitationPanel == null) return;

            // 1. Update Registration Countdown
            if (_invitationTimeLeft > 0f)
            {
                _invitationTimeLeft -= Time.deltaTime;
                if (_invitationTimeLeft <= 0f)
                {
                    _invitationTimeLeft = 0f;
                    HideInvitation();
                }
                else
                {
                    TimeSpan t = TimeSpan.FromSeconds(_invitationTimeLeft);
                    string timeStr = string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
                    _invTimerText.text = "Kalan Süre: " + timeStr;

                    if (_miniTabPanel != null)
                    {
                        if (_invitationPanel.activeSelf && _isMinimized)
                        {
                            _miniTabPanel.SetActive(true);
                            _miniTabText.text = "Etkinlik\n" + timeStr;
                        }
                        else
                        {
                            _miniTabPanel.SetActive(false);
                        }
                    }
                }
            }
            else
            {
                if (_miniTabPanel != null)
                {
                    _miniTabPanel.SetActive(false);
                }
            }

            // 2. Invitation panel slide-in animation
            var invRT = _invitationPanel.GetComponent<RectTransform>();
            if (_invitationPanel.activeSelf)
            {
                if (_isMinimized)
                {
                    invRT.localPosition = Vector3.Lerp(invRT.localPosition, _invStartPos, Time.deltaTime * 6f);
                    _slideInCompleted = false;
                }
                else if (!_slideInCompleted)
                {
                    invRT.localPosition = Vector3.Lerp(invRT.localPosition, _invTargetPos, Time.deltaTime * 6f);
                    if (Vector3.Distance(invRT.localPosition, _invTargetPos) < 1f)
                    {
                        invRT.localPosition = _invTargetPos;
                        _slideInCompleted = true; // Sürüklemeyi bozmamak için slide tamamlandığında animasyonu durdur
                    }
                }
            }
            else
            {
                invRT.localPosition = _invStartPos;
                _slideInCompleted = false;
            }

            // 3. Event Zone Detection and Scoreboard Toggling
            var gm = GameManager.Instance;
            if (gm != null)
            {
                short currentZone = gm.CurrentZoneId;
                if (currentZone == ZONE_BDW || currentZone == ZONE_CHAOS || currentZone == ZONE_JURAID)
                {
                    if (!_scoreboardPanel.activeSelf)
                    {
                        SetupScoreboardForZone(currentZone);
                        _scoreboardPanel.SetActive(true);
                    }
                    UpdateScoreboardValues(currentZone);
                }
                else
                {
                    if (_scoreboardPanel.activeSelf)
                    {
                        _scoreboardPanel.SetActive(false);
                    }
                }
            }

            // 4. Announcement Fading
            if (_announcementFadeTimer > 0f)
            {
                _announcementFadeTimer -= Time.deltaTime;
                if (_announcementFadeTimer <= 0f)
                {
                    _announcementText.color = new Color(_announcementText.color.r, _announcementText.color.g, _announcementText.color.b, 0f);
                }
                else if (_announcementFadeTimer < 1.0f)
                {
                    // Fade out in the last second
                    _announcementText.color = new Color(_announcementText.color.r, _announcementText.color.g, _announcementText.color.b, _announcementFadeTimer);
                }
            }
        }

        // ==========================================
        // OPERATIONS
        // ==========================================

        private void OnRegisterClicked()
        {
            var net = KONetworkManager.Instance;
            if (net != null && net.IsConnected)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_EVENT);
                pkt.WriteByte(TEMPLE_EVENT_JOIN);
                net.SendPacket(pkt);
            }
        }

        private void OnCancelClicked()
        {
            var net = KONetworkManager.Instance;
            if (net != null && net.IsConnected)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_EVENT);
                pkt.WriteByte(TEMPLE_EVENT_DISBAND);
                net.SendPacket(pkt);
            }
        }

        private void ShowInvitation(short activeEvent, short seconds)
        {
            if (seconds <= 0)
            {
                HideInvitation();
                return;
            }

            if (_activeEventId != activeEvent)
            {
                _isRegistered = false;
            }
            _activeEventId = activeEvent;
            _invitationTimeLeft = seconds;

            string eventName = "ETKİNLİK";
            if (activeEvent == EVENT_BDW) eventName = "BORDER DEFENSE WAR";
            else if (activeEvent == EVENT_CHAOS) eventName = "CHAOS DUNGEON";
            else if (activeEvent == EVENT_JURAID) eventName = "JURAID MOUNTAIN";

            _invTitleText.text = eventName;
            _invQueueText.text = "Kayıtlı: Karus: 0 | El Morad: 0";
            
            if (_isRegistered)
            {
                _registerButton.gameObject.SetActive(false);
                _cancelButton.gameObject.SetActive(true);
                _cancelBtnText.text = "Kaydı İptal Et";
            }
            else
            {
                _registerButton.gameObject.SetActive(true);
                _cancelButton.gameObject.SetActive(false);
            }

            _isMinimized = false;
            _slideInCompleted = false; // Yeniden gösterildiğinde slayt animasyonu başlasın
            _invitationPanel.SetActive(true);
            if (_miniTabPanel != null) _miniTabPanel.SetActive(false);
            
            // Trigger a sound if any
        }

        private void HideInvitation()
        {
            _invitationPanel.SetActive(false);
            if (_miniTabPanel != null) _miniTabPanel.SetActive(false);
        }

        private void HandleJoinResult(byte result, short eventId)
        {
            if (result == 1) // Success
            {
                _isRegistered = true;
                _registerButton.gameObject.SetActive(false);
                _cancelButton.gameObject.SetActive(true);
                _cancelBtnText.text = "Kaydı İptal Et";
                
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.AddMsgOutput("Etkinliğe başarıyla kayıt oldunuz.", new Color(0.2f, 1f, 0.2f, 1f));
                }
            }
            else if (result == 3) // Missing Item
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.AddMsgOutput("Kayıt olunamadı! Çantanızda Kaos Haritası (Chaos Map) bulunmalıdır.", Color.red);
                }
            }
            else
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.AddMsgOutput("Kayıt başarısız oldu.", Color.red);
                }
            }
        }

        private void HandleDisbandResult(byte result, short eventId)
        {
            if (result == 1) // Success
            {
                _isRegistered = false;
                _registerButton.gameObject.SetActive(true);
                _cancelButton.gameObject.SetActive(false);
                
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.AddMsgOutput("Etkinlik kaydınız iptal edildi.", new Color(1f, 0.6f, 0f, 1f));
                }
            }
        }

        private void UpdateQueueCounter(short eventId, short param1, short param2)
        {
            if (eventId == EVENT_CHAOS)
            {
                _queueChaosCount = param1;
                _invQueueText.text = $"Kayıtlı Oyuncu: {param1}";
            }
            else
            {
                _queueKarusCount = param1;
                _queueElmoCount = param2;
                _invQueueText.text = $"Kayıtlı: Karus: {param1} | El Morad: {param2}";
            }
        }

        // ==========================================
        // SCOREBOARD SYSTEM
        // ==========================================

        private void SetupScoreboardForZone(short zone)
        {
            _eventTimeLeft = 1200f; // 20 minutes countdown

            if (zone == ZONE_BDW)
            {
                _scoreTitleText.text = "BORDER DEFENSE WAR";
                _scoreKarusText.gameObject.SetActive(true);
                _scoreElmoText.gameObject.SetActive(true);
                _scoreExtraText.gameObject.SetActive(true);

                _bdwKarusScore = 0;
                _bdwElmoScore = 0;
                _bdwAltarControlNation = 0;
            }
            else if (zone == ZONE_CHAOS)
            {
                _scoreTitleText.text = "CHAOS DUNGEON";
                _scoreKarusText.gameObject.SetActive(true);
                _scoreElmoText.gameObject.SetActive(true);
                _scoreExtraText.gameObject.SetActive(false);

                _scoreKarusText.text = "Öldürme: 0";
                _scoreKarusText.color = Color.green;
                _scoreElmoText.text = "Ölüm: 0";
                _scoreElmoText.color = Color.red;

                _chaosKills = 0;
                _chaosDeaths = 0;
            }
            else if (zone == ZONE_JURAID)
            {
                _scoreTitleText.text = "JURAID MOUNTAIN";
                _scoreKarusText.gameObject.SetActive(false);
                _scoreElmoText.gameObject.SetActive(false);
                _scoreExtraText.gameObject.SetActive(true);

                _juraidStage = 1;
                _scoreExtraText.text = "Bölge: 1. Oda";
                _scoreExtraText.color = Color.cyan;
            }
        }

        private void UpdateScoreboardValues(short zone)
        {
            if (_eventTimeLeft > 0f)
            {
                _eventTimeLeft -= Time.deltaTime;
                if (_eventTimeLeft < 0f) _eventTimeLeft = 0f;
            }

            TimeSpan t = TimeSpan.FromSeconds(_eventTimeLeft);
            _scoreTimerText.text = string.Format("Kalan Süre: {0:D2}:{1:D2}", t.Minutes, t.Seconds);

            if (zone == ZONE_BDW)
            {
                _scoreKarusText.text = $"Karus: {_bdwKarusScore}";
                _scoreElmoText.text = $"El Morad: {_bdwElmoScore}";
                
                if (_bdwAltarControlNation == 0)
                {
                    _scoreExtraText.text = "Altar Kontrolü: BOŞTA";
                    _scoreExtraText.color = Color.gray;
                }
                else if (_bdwAltarControlNation == 1)
                {
                    _scoreExtraText.text = "Altar Kontrolü: KARUS";
                    _scoreExtraText.color = new Color(1f, 0.3f, 0.3f, 1f);
                }
                else
                {
                    _scoreExtraText.text = "Altar Kontrolü: EL MORAD";
                    _scoreExtraText.color = new Color(0.3f, 0.5f, 1f, 1f);
                }
            }
            else if (zone == ZONE_CHAOS)
            {
                _scoreKarusText.text = $"Öldürme: {_chaosKills}";
                _scoreElmoText.text = $"Ölüm: {_chaosDeaths}";
            }
            else if (zone == ZONE_JURAID)
            {
                // Juraid updates stage based on spawns / gates
                _scoreExtraText.text = $"Bölge: {_juraidStage}. Oda";
            }
        }

        // ==========================================
        // PACKET LISTENERS
        // ==========================================

        private void OnEventPacket(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte subOpcode = r.ReadByte();

            switch (subOpcode)
            {
                case TEMPLE_EVENT:
                    {
                        short activeEvent = r.ReadInt16();
                        short remainSeconds = r.ReadInt16();
                        ShowInvitation(activeEvent, remainSeconds);
                    }
                    break;

                case TEMPLE_EVENT_JOIN:
                    {
                        byte result = r.ReadByte();
                        short activeEvent = r.ReadInt16();
                        HandleJoinResult(result, activeEvent);
                    }
                    break;

                case TEMPLE_EVENT_DISBAND:
                    {
                        byte result = r.ReadByte();
                        short activeEvent = r.ReadInt16();
                        HandleDisbandResult(result, activeEvent);
                    }
                    break;

                case TEMPLE_EVENT_COUNTER:
                    {
                        short activeEvent = r.ReadInt16();
                        if (activeEvent == EVENT_CHAOS)
                        {
                            short allCount = r.ReadInt16();
                            UpdateQueueCounter(activeEvent, allCount, 0);
                        }
                        else
                        {
                            short karusCount = r.ReadInt16();
                            short elmoCount = r.ReadInt16();
                            UpdateQueueCounter(activeEvent, karusCount, elmoCount);
                        }
                    }
                    break;
            }
        }

        private void OnCapturePacket(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte subOpcode = r.ReadByte();

            if (subOpcode == 0x05) // Announcement
            {
                byte nation = r.ReadByte();
                string npcName = r.ReadKOString();
                
                string nationName = nation == 1 ? "Karus" : "El Morad";
                Color nationColor = nation == 1 ? new Color(1f, 0.3f, 0.3f, 1f) : new Color(0.3f, 0.5f, 1f, 1f);

                _bdwAltarControlNation = nation;

                ShowBannerAnnouncement($"{nationName} ırkı {npcName} anıtını ele geçirdi!", nationColor);
            }
            else if (subOpcode == 0x04) // Score and capture timer reset
            {
                byte nation = r.ReadByte();
                short capSeconds = r.ReadInt16(); // KO = 360 seconds
                
                if (nation == 1) _bdwKarusScore++;
                else if (nation == 2) _bdwElmoScore++;

                _bdwAltarControlNation = nation;

                // Adjust event timer or show feedback
            }
        }

        private void OnDeadPacket(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            // Wire: [WIZ_DEAD] [nid:int16]
            short deadNid = r.ReadInt16();

            // Detect if we killed a boss / gatekeeper in Juraid or got a kill in Chaos
            var gm = GameManager.Instance;
            if (gm != null)
            {
                short currentZone = gm.CurrentZoneId;
                if (currentZone == ZONE_CHAOS)
                {
                    // Chaos: If the entity dead is someone else and we are in Chaos, we might get points.
                    // Note: Actual scoring is determined by server attack/death sync. 
                    // Let's increment local kills if we receive a packet indicating our kill.
                    // Normally the server sends WIZ_ATTACK or WIZ_CHAT with kill confirmations.
                }
                else if (currentZone == ZONE_JURAID)
                {
                    // Juraid: If a gatekeeper (NPCSid matches gatekeeper IDs) dies, we progress stage.
                    var entity = EntityManager.Instance?.GetEntityByInstanceId(deadNid);
                    if (entity != null)
                    {
                        // Gatekeeper prototype IDs usually end in 01, 02, etc. or contain Gatekeeper names
                        if (entity.EntityName.ToLower().Contains("gatekeeper") || entity.EntityName.ToLower().Contains("kapıcı") || entity.EntityName.ToLower().Contains("deva"))
                        {
                            _juraidStage++;
                            if (_juraidStage > 4) _juraidStage = 4;
                            
                            ShowBannerAnnouncement($"Tebrikler! Bir sonraki odaya geçtiniz.", Color.cyan);
                        }
                    }
                }
            }
        }

        // ==========================================
        // FEEDBACK BANNER UI
        // ==========================================

        private void ShowBannerAnnouncement(string text, Color textColor)
        {
            if (_announcementText == null) return;

            _announcementText.text = text;
            _announcementText.color = textColor;
            _announcementFadeTimer = 4.0f; // Display for 4 seconds
            
        }

        // ==========================================
        // EXTERNAL INTEGRATION
        // ==========================================

        public void IncrementChaosKill()
        {
            _chaosKills++;
        }

        public void IncrementChaosDeath()
        {
            _chaosDeaths++;
        }

        private void StretchUI(GameObject obj)
        {
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Sprite GetRoundedRectSprite(string key, int w, int h, int radius, Color fillColor, Color borderColor, int borderWidth)
        {
            if (_spriteCache.TryGetValue(key, out Sprite sp))
                return sp;

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = radius <= 0 ? FilterMode.Point : FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (radius <= 0)
                    {
                        bool isBorder = (x < borderWidth || x >= w - borderWidth || y < borderWidth || y >= h - borderWidth);
                        tex.SetPixel(x, y, isBorder ? borderColor : fillColor);
                        continue;
                    }

                    bool isInside = true;
                    float dx = 0, dy = 0;

                    if (x < radius && y < radius) { dx = radius - x; dy = radius - y; isInside = (dx*dx + dy*dy) <= radius*radius; }
                    else if (x >= w - radius && y < radius) { dx = x - (w - 1 - radius); dy = radius - y; isInside = (dx*dx + dy*dy) <= radius*radius; }
                    else if (x < radius && y >= h - radius) { dx = radius - x; dy = y - (h - 1 - radius); isInside = (dx*dx + dy*dy) <= radius*radius; }
                    else if (x >= w - radius && y >= h - radius) { dx = x - (w - 1 - radius); dy = y - (h - 1 - radius); isInside = (dx*dx + dy*dy) <= radius*radius; }

                    if (isInside)
                    {
                        bool isBorder = false;
                        if (x < borderWidth || x >= w - borderWidth || y < borderWidth || y >= h - borderWidth)
                            isBorder = true;
                        else if (x < radius || x >= w - radius || y < radius || y >= h - radius)
                        {
                            float dist = Mathf.Sqrt(dx*dx + dy*dy);
                            if (dist >= radius - borderWidth)
                                isBorder = true;
                        }

                        float edgeDist = 0f;
                        if (x < radius && y < radius) edgeDist = Mathf.Sqrt(dx*dx + dy*dy) - radius;
                        else if (x >= w - radius && y < radius) edgeDist = Mathf.Sqrt(dx*dx + dy*dy) - radius;
                        else if (x < radius && y >= h - radius) edgeDist = Mathf.Sqrt(dx*dx + dy*dy) - radius;
                        else if (x >= w - radius && y >= h - radius) edgeDist = Mathf.Sqrt(dx*dx + dy*dy) - radius;

                        if (edgeDist > 0f && edgeDist < 1f)
                        {
                            Color c = isBorder ? borderColor : fillColor;
                            c.a *= (1f - edgeDist);
                            tex.SetPixel(x, y, c);
                        }
                        else
                        {
                            tex.SetPixel(x, y, isBorder ? borderColor : fillColor);
                        }
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            _spriteCache[key] = sprite;
            return sprite;
        }
    }
}
