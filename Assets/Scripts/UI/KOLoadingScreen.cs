using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using EntropyOnline.Core;
using EntropyOnline.Import;
using EntropyOnline.Network.KO;
using EntropyOnline.World;

namespace EntropyOnline.UI
{
    public class KOLoadingScreen : MonoBehaviour
    {
        public static KOLoadingScreen Instance { get; private set; }

        // .uif'den yüklenen UI — cpp: s_pUILoading
        private GameObject _loadingUI;
        private Canvas _canvas;
        private Image _blackBackground; // cpp: D3D Clear(0x00000000) — tam ekran siyah

        // .uif elemanları — UILoading.h:15-17
        private Text _textVersion;
        private Text _textInfo;
        private Slider _progressBar; // CN3UIProgress → Unity Slider
        private KOProgressFill _progressFillCtrl; // KO Progress bar fill control

        // Fallback UI (eğer .uif yüklenemezse)
        private GameObject _fallbackPanel;
        private Text _fallbackText;
        private Slider _fallbackSlider;

        // Smooth progress animation variables
        private float _currentDisplayPercentage = 0f;
        private string _baseInfoText = "Loading...";
        private bool _isActualLoadingDone = false;

        public float CurrentDisplayPercentage => _currentDisplayPercentage;
        public bool IsLoadingScene { get; private set; }

        public void SetActualLoadingDone(bool done)
        {
            _isActualLoadingDone = done;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_canvas != null && _canvas.gameObject.activeSelf)
            {
                float target = _isActualLoadingDone ? 100f : 90f;
                if (_currentDisplayPercentage < target)
                {
                    // Climb smoothly (50% per second, takes 2 seconds total to 100%)
                    _currentDisplayPercentage = Mathf.MoveTowards(_currentDisplayPercentage, target, Time.deltaTime * 50f);
                    UpdateUIValues();
                }
            }
        }

        /// <summary>
        /// Open-KO birebir: LoadingUIChange() — GameProcedure.cpp:1058-1097
        /// Nation bazlı loading .uif seçer ve yükler.
        /// </summary>
        public void Show(byte nation = 0)
        {
            IsLoadingScene = true;

            // Canvas oluştur (yoksa)
            if (_canvas == null)
                CreateCanvas();

            // Önceki loading UI'ı temizle — cpp:1066-1068
            if (_loadingUI != null)
                Destroy(_loadingUI);
            if (_fallbackPanel != null)
                _fallbackPanel.SetActive(false);

            // cpp:1080-1093 — victoryNation'a göre .uif dosyası seç
            // Biz nation bilgisini kullanıyoruz (Karus=1, ElMorad=2)
            string prefix;
            switch (nation)
            {
                case 1: prefix = "ka"; break;  // Karus
                case 2: prefix = "el"; break;  // El Morad
                default: prefix = "co"; break; // Common / varsayılan
            }

            string uifPath = Path.Combine("UI_US", $"{prefix}_loading_us.uif");

            bool uifLoaded = false;
            // .uif'den loading UI yükle — cpp:1096
            var fullScreen = new UIFImporter.Rect { Left = 0, Top = 0, Right = 1024, Bottom = 768 };
            _loadingUI = KOUIRenderer.LoadUI(uifPath, _canvas.transform, fullScreen);

            if (_loadingUI != null)
            {
                // C++ UILoading.cpp:51 birebir: SetPosCenter()
                // .uif 1024x768 doğal boyutunda — CanvasScaler referenceResolution ile aynı
                // Merkeze konumla, stretch YAPMA
                var rt = _loadingUI.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Vector2 originalSize = rt.sizeDelta;
                    // Merkez anchor + pivot
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = originalSize;
                    rt.anchoredPosition = Vector2.zero;
                }

                // .uif elemanlarını bul — UILoading.cpp:41-49
                BindUIFElements();
                uifLoaded = true;

                // cpp:44-46 — Version string set
                if (_textVersion != null)
                    _textVersion.text = "Ver. 1.298";

                // cpp:52 birebir: m_pText_Version->SetPos(10, 10)
                // Version text sol üst köşeye taşı (.uif içindeki konumunu override et)
                if (_textVersion != null)
                {
                    var verRT = _textVersion.GetComponent<RectTransform>();
                    if (verRT != null)
                    {
                        verRT.anchorMin = new Vector2(0, 1);
                        verRT.anchorMax = new Vector2(0, 1);
                        verRT.pivot = new Vector2(0, 1);
                        verRT.anchoredPosition = new Vector2(10, -10);
                    }
                }

                // Text_Info metnini Progress_Loading ile hizala ve ortala
                var progressTr = KOUIRenderer.FindChildByID(_loadingUI.transform, "Progress_Loading");
                if (_textInfo != null && progressTr != null)
                {
                    var infoRT = _textInfo.GetComponent<RectTransform>();
                    var progressRT = progressTr.GetComponent<RectTransform>();
                    if (infoRT != null && progressRT != null)
                    {
                        _textInfo.alignment = TextAnchor.MiddleCenter;
                        infoRT.pivot = new Vector2(0.5f, 0.5f);

                        float progressCenterX = progressRT.anchoredPosition.x + progressRT.sizeDelta.x * (0.5f - progressRT.pivot.x);
                        float progressCenterY = progressRT.anchoredPosition.y + progressRT.sizeDelta.y * (0.5f - progressRT.pivot.y);
                        float targetY = progressCenterY + 25f;

                        infoRT.anchorMin = progressRT.anchorMin;
                        infoRT.anchorMax = progressRT.anchorMax;
                        infoRT.sizeDelta = new Vector2(progressRT.sizeDelta.x + 100f, infoRT.sizeDelta.y);
                        infoRT.anchoredPosition = new Vector2(progressCenterX, targetY);
                    }
                }

            }

            // .uif yüklenemezse fallback UI oluştur
            if (!uifLoaded)
            {
                CreateFallbackUI();
                Debug.LogWarning("[LOADING] .uif yüklenemedi, fallback UI kullanılıyor");
            }

            // Görünür yap
            _canvas.gameObject.SetActive(true);

            // Reset smooth percentage
            _currentDisplayPercentage = 0f;
            _isActualLoadingDone = false;
            UpdateUIValues();
        }

        public void SetProgress(string info, int percentage)
        {
            // No-op: progress is now driven smoothly and dynamically from 0 to 100%
        }

        private void UpdateUIValues()
        {
            // Update _baseInfoText dynamically based on percentage
            if (_currentDisplayPercentage < 15f)
                _baseInfoText = "Allocating Terrain...";
            else if (_currentDisplayPercentage < 40f)
                _baseInfoText = "Loading Terrain Tile Data...";
            else if (_currentDisplayPercentage < 70f)
                _baseInfoText = "Loading Colormap...";
            else if (_currentDisplayPercentage < 85f)
                _baseInfoText = "Loading Objects...";
            else if (_currentDisplayPercentage < 95f)
                _baseInfoText = "Loading Interface Data...";
            else
                _baseInfoText = "Loading User Data...";

            int displayInt = Mathf.RoundToInt(_currentDisplayPercentage);
            string formattedText = $"{_baseInfoText} {displayInt} %";

            if (_textInfo != null)
                _textInfo.text = formattedText;
            else if (_fallbackText != null)
                _fallbackText.text = formattedText;

            float normalizedValue = _currentDisplayPercentage / 100f;

            if (_progressBar != null)
                _progressBar.value = normalizedValue;
            else if (_progressFillCtrl != null)
                _progressFillCtrl.FillAmount = normalizedValue;
            else if (_fallbackSlider != null)
                _fallbackSlider.value = normalizedValue;
        }

        /// <summary>
        /// Loading ekranını gizle.
        /// </summary>
        public void Hide()
        {
            IsLoadingScene = false;

            if (_canvas != null)
                _canvas.gameObject.SetActive(false);

            if (_loadingUI != null)
            {
                Destroy(_loadingUI);
                _loadingUI = null;
            }

        }

        /// <summary>
        /// Open-KO birebir: GameProcMain::Init() akışı (cpp:320-443)
        /// 
        /// Sahneyi arka planda yükler, progress bar'ı günceller.
        /// C++'da Init() senkron çalışır ve her aşamada Render() çağrılır.
        /// Unity'de LoadSceneAsync + coroutine ile aynı deneyim sağlanır.
        /// </summary>
        public void LoadSceneWithProgress(string sceneName, byte nation = 0)
        {
            Show(nation);
            StartCoroutine(LoadSceneCoroutine(sceneName));
        }

        private IEnumerator LoadSceneCoroutine(string sceneName)
        {
            IsLoadingScene = true;
            _isActualLoadingDone = false;
            _currentDisplayPercentage = 0f;
            UpdateUIValues();
            yield return null;

            // Load the Unity scene asynchronously in the background and activate it immediately
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            asyncLoad.allowSceneActivation = true;

            // Wait until the scene is fully loaded and activated AND visual progress has reached 90%
            while (!asyncLoad.isDone || _currentDisplayPercentage < 90f)
            {
                yield return null;
            }

            // Unity scene is fully activated! WorldBuilder.Instance is now spawned.
            if (WorldBuilder.Instance != null)
            {
                short zoneId = GameManager.Instance != null ? GameManager.Instance.CurrentZoneId : (short)21;
                if (zoneId == 0) zoneId = 21; // Moradon fallback
                
                // Load the zone synchronously! (This blocks the thread, causing the screen to freeze at 90%)
                WorldBuilder.Instance.ChangeZone(zoneId);

                // Position the player since terrain and objects are now loaded!
                WorldBuilder.Instance.RepositionPlayer();
            }

            // Set actual loading done to allow progress bar to finish to 100%
            _isActualLoadingDone = true;

            // Ensure visual progress reaches exactly 100%
            while (_currentDisplayPercentage < 100f)
            {
                yield return null;
            }

            // Sahnenin ve tüm nesnelerin (EntityManager vb.) Start metotlarının çalışıp 
            // event'lere (OnRegionChange, OnNpcRegion vb.) abone olması için 5 frame bekliyoruz.
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            // Artık güvenli bir şekilde oyuna başlama paketini gönderebiliriz.
            KONetworkManager.Instance?.SendGameStart(2);

            Hide();
        }

        // ============================================
        // Canvas
        // ============================================

        private void CreateCanvas()
        {
            var canvasObj = new GameObject("LoadingCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999; // Her şeyin üstünde

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024, 768);
            scaler.matchWidthOrHeight = 0.5f; // Genişlik ve yükseklik arasında denge

            canvasObj.AddComponent<GraphicRaycaster>();

            // Tam ekran siyah arka plan
            var bgObj = new GameObject("BlackBackground");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgRT = bgObj.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            _blackBackground = bgObj.AddComponent<Image>();
            _blackBackground.color = Color.black;
            _blackBackground.raycastTarget = false;
        }

        private void BindUIFElements()
        {
            if (_loadingUI == null) return;
            var root = _loadingUI.transform;

            var versionTr = KOUIRenderer.FindChildByID(root, "Text_Version");
            if (versionTr != null) _textVersion = versionTr.GetComponent<Text>();

            var infoTr = KOUIRenderer.FindChildByID(root, "Text_Info");
            if (infoTr != null) _textInfo = infoTr.GetComponent<Text>();

            var progressTr = KOUIRenderer.FindChildByID(root, "Progress_Loading");
            if (progressTr != null)
            {
                _progressBar = progressTr.GetComponent<Slider>();
                _progressFillCtrl = progressTr.GetComponentInChildren<KOProgressFill>(true);
            }
        }

        private void CreateFallbackUI()
        {
            if (_fallbackPanel != null) { _fallbackPanel.SetActive(true); return; }

            _fallbackPanel = new GameObject("FallbackLoading");
            _fallbackPanel.transform.SetParent(_canvas.transform, false);
            var panelRT = _fallbackPanel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            var bgImg = _fallbackPanel.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.02f, 0.05f, 1f);

            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_fallbackPanel.transform, false);
            var titleRT = titleObj.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.3f, 0.55f);
            titleRT.anchorMax = new Vector2(0.7f, 0.7f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            var titleText = titleObj.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 32;
            titleText.color = new Color(0.85f, 0.65f, 0.2f);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.text = "KNIGHT ONLINE";

            var barObj = new GameObject("ProgressBar");
            barObj.transform.SetParent(_fallbackPanel.transform, false);
            var barRT = barObj.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.2f, 0.35f);
            barRT.anchorMax = new Vector2(0.8f, 0.39f);
            barRT.offsetMin = Vector2.zero;
            barRT.offsetMax = Vector2.zero;

            var barBg = barObj.AddComponent<Image>();
            barBg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            _fallbackSlider = barObj.AddComponent<Slider>();
            _fallbackSlider.minValue = 0f;
            _fallbackSlider.maxValue = 1f;

            var fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(barObj.transform, false);
            var fillAreaRT = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = new Vector2(3, 3);
            fillAreaRT.offsetMax = new Vector2(-3, -3);

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            var fillRT = fillObj.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            var fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.85f, 0.65f, 0.2f, 1f);

            _fallbackSlider.fillRect = fillRT;

            var textObj = new GameObject("InfoText");
            textObj.transform.SetParent(_fallbackPanel.transform, false);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.2f, 0.28f);
            textRT.anchorMax = new Vector2(0.8f, 0.35f);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            _fallbackText = textObj.AddComponent<Text>();
            _fallbackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _fallbackText.fontSize = 18;
            _fallbackText.color = new Color(0.8f, 0.8f, 0.8f);
            _fallbackText.alignment = TextAnchor.MiddleCenter;

            var verObj = new GameObject("VersionText");
            verObj.transform.SetParent(_fallbackPanel.transform, false);
            var verRT = verObj.AddComponent<RectTransform>();
            verRT.anchorMin = new Vector2(0f, 0.95f);
            verRT.anchorMax = new Vector2(0.2f, 1f);
            verRT.offsetMin = new Vector2(10, 0);
            verRT.offsetMax = Vector2.zero;

            var verText = verObj.AddComponent<Text>();
            verText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            verText.fontSize = 14;
            verText.color = new Color(0.6f, 0.6f, 0.6f);
            verText.alignment = TextAnchor.UpperLeft;
            verText.text = "Ver. 1.298";
        }
    }
}
