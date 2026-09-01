using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using EntropyOnline.Core;
using EntropyOnline.Camera;
using EntropyOnline.World;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Entropy Online — Mobil Sağ Üst Arayüz & Kamera Kontrolleri
    /// Premium Tasarım (Prosedürel Yuvarlatılmış Köşeler & Gold Kenarlıklar ile AAA Mobil HUD).
    /// </summary>
    public class KOMobileTopRightUI : MonoBehaviour
    {
        private UnityEngine.Camera _mainCamera;
        private CameraController _cameraController;

        // UI Elemanları
        private Text _txtServerName;
        private Text _txtPremium;
        private Text _txtEvents;
        private Text _txtGenieTime;

        private Font _uiFont;

        // Prosedürel oluşturulan sprite'ları hafızada tutalım (çift oluşturmamak için)
        private Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        private List<CanvasGroup> _cameraCanvasGroups = new List<CanvasGroup>();
        private GameObject _eyeSlashLine;
        private bool _cameraButtonsVisible = true;

        private void Awake()
        {
            // Bu objenin kendi RectTransform ayarlarını yapalım (Sağ Üst Köşeye Sabitleme)
            var rect = GetComponent<RectTransform>();
            if (rect == null) rect = gameObject.AddComponent<RectTransform>();
            
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(0, 0); // Tam sağ üst köşeye sıfırlandı
            rect.sizeDelta = new Vector2(150, 300); // Genişlik 150 birime çekilerek kutular simetrik yapıldı
        }

        private void Start()
        {
            _mainCamera = UnityEngine.Camera.main;
            if (_mainCamera != null)
            {
                _cameraController = _mainCamera.GetComponent<CameraController>();
            }

            // Orijinal fontu bulalım
            _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            CreateLayout();
            StartCoroutine(UpdateFPSTimer());
            ApplyExpandMargins();
        }

        public void ApplyExpandMargins()
        {
            var rect = GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchoredPosition = Vector2.zero;
        }

        private void CreateLayout()
        {
            // Dikey sıralama grubu (Vertical Layout Group)
            var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0f; // Bitişik görünmesi için aradaki dikey boşluk sıfırlandı
            vlg.childAlignment = TextAnchor.UpperRight;
            vlg.childControlWidth = false; // Kutuların kendi özel genişliklerini (150f / 170f) korumasını sağlar
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false; // Genişlemeye zorlamaz
            vlg.childForceExpandHeight = false;

            // ==========================================
            // 1. GENIE (AUTO ATTACK) PANELI (Daha kompakt)
            // ==========================================
            var genieObj = CreateRowContainer("GenieRow", 150f, 46f);
            
            // Genie arka planı - Keskin köşeli düz panel
            var genieImg = genieObj.AddComponent<Image>();
            genieImg.sprite = GetRoundedRectSprite("genie_panel", 150, 46, 0, new Color(0.06f, 0.06f, 0.06f, 0.9f), new Color(0.45f, 0.35f, 0.15f, 0.6f), 1);

            // Genie Butonlar Konteyneri (Horizontal)
            var genieButtons = new GameObject("GenieButtons", typeof(RectTransform));
            genieButtons.transform.SetParent(genieObj.transform, false);
            var gbRect = genieButtons.GetComponent<RectTransform>();
            gbRect.anchorMin = new Vector2(0, 0.38f);
            gbRect.anchorMax = new Vector2(1, 1);
            gbRect.sizeDelta = Vector2.zero;

            var hlg = genieButtons.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f; // Sol/sağ mesafelerin eşitlenmesi için spacing 6 yapıldı
            hlg.padding = new RectOffset(6, 6, 4, 2);
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Genie Butonları (Play, Pause, Settings)
            CreateGenieButton(genieButtons.transform, "▶", new Color(0.12f, 0.12f, 0.12f, 0.95f), () => StartGenie());
            CreateGenieButton(genieButtons.transform, "‖", new Color(0.12f, 0.12f, 0.12f, 0.95f), () => StopGenie());
            CreateGenieButton(genieButtons.transform, "⚙", new Color(0.12f, 0.12f, 0.12f, 0.95f), () => {
                var aaPanel = KOUIManager.Instance?.AutoAttackSettingsPanel;
                if (aaPanel != null)
                {
                    bool nextState = !aaPanel.activeSelf;
                    KOUIManager.Instance.ShowAutoAttackSettings(nextState);
                    KOUIManager.Instance.ShowSkillTree(nextState);
                }
            });
            
            // AUTO ATTACK Butonu
            CreateAutoAttackButton(genieButtons.transform);

            // Genie Kalan Süre Etiketi
            var genieTimeObj = new GameObject("GenieTime", typeof(RectTransform));
            genieTimeObj.transform.SetParent(genieObj.transform, false);
            var gtRect = genieTimeObj.GetComponent<RectTransform>();
            gtRect.anchorMin = new Vector2(0, 0.02f);
            gtRect.anchorMax = new Vector2(1, 0.38f);
            gtRect.sizeDelta = Vector2.zero;

            _txtGenieTime = genieTimeObj.AddComponent<Text>();
            _txtGenieTime.font = _uiFont;
            _txtGenieTime.fontSize = 9;
            _txtGenieTime.fontStyle = FontStyle.Bold;
            _txtGenieTime.alignment = TextAnchor.MiddleCenter;
            _txtGenieTime.color = new Color(0.85f, 0.85f, 0.7f);
            _txtGenieTime.text = "Time Left : 66 Minute(s)";

            var shadow = genieTimeObj.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1, -1);

            // ==========================================
            // 2. SUNUCU BILGI BARı
            // ==========================================
            var serverObj = CreateRowContainer("ServerRow", 150f, 19f);
            var serverImg = serverObj.AddComponent<Image>();
            serverImg.sprite = GetRoundedRectSprite("server_bar", 150, 19, 0, new Color(0.1f, 0.1f, 0.1f, 0.95f), new Color(0.45f, 0.35f, 0.15f, 0.8f), 1);

            var serverTextObj = new GameObject("ServerText", typeof(RectTransform));
            serverTextObj.transform.SetParent(serverObj.transform, false);
            StretchUI(serverTextObj);

            _txtServerName = serverTextObj.AddComponent<Text>();
            _txtServerName.font = _uiFont;
            _txtServerName.fontSize = 10;
            _txtServerName.fontStyle = FontStyle.Bold;
            _txtServerName.alignment = TextAnchor.MiddleCenter;
            _txtServerName.color = new Color(0.9f, 0.75f, 0.25f);
            _txtServerName.text = "ARES-1";
            AddTextShadow(_txtServerName.gameObject);

            // ==========================================
            // 3. PREMIUM DURUM BARı
            // ==========================================
            var premiumObj = CreateRowContainer("PremiumRow", 150f, 19f);
            var premBtn = premiumObj.AddComponent<Button>();
            var premImg = premiumObj.AddComponent<Image>();
            premImg.sprite = GetRoundedRectSprite("premium_bar", 150, 19, 0, new Color(0.48f, 0.35f, 0.12f, 0.95f), new Color(0.7f, 0.55f, 0.2f, 0.8f), 1);

            var premTextObj = new GameObject("PremiumText", typeof(RectTransform));
            premTextObj.transform.SetParent(premiumObj.transform, false);
            StretchUI(premTextObj);

            _txtPremium = premTextObj.AddComponent<Text>();
            _txtPremium.font = _uiFont;
            _txtPremium.fontSize = 10;
            _txtPremium.fontStyle = FontStyle.Bold;
            _txtPremium.alignment = TextAnchor.MiddleCenter;
            _txtPremium.color = Color.white;
            _txtPremium.text = "◀ PK Premium";
            AddTextShadow(_txtPremium.gameObject);


            // ==========================================
            // 4. ETKINLIK BARı
            // ==========================================
            var eventsObj = CreateRowContainer("EventsRow", 150f, 19f);
            var eventsBtn = eventsObj.AddComponent<Button>();
            var eventsImg = eventsObj.AddComponent<Image>();
            eventsImg.sprite = GetRoundedRectSprite("events_bar", 150, 19, 0, new Color(0.32f, 0.1f, 0.44f, 0.95f), new Color(0.55f, 0.25f, 0.7f, 0.8f), 1);

            var eventsTextObj = new GameObject("EventsText", typeof(RectTransform));
            eventsTextObj.transform.SetParent(eventsObj.transform, false);
            StretchUI(eventsTextObj);

            _txtEvents = eventsTextObj.AddComponent<Text>();
            _txtEvents.font = _uiFont;
            _txtEvents.fontSize = 10;
            _txtEvents.fontStyle = FontStyle.Bold;
            _txtEvents.alignment = TextAnchor.MiddleCenter;
            _txtEvents.color = Color.white;
            _txtEvents.text = "◀ Events";
            AddTextShadow(_txtEvents.gameObject);


            // Spacer (Kamera butonlarıyla arayı hafif açmak için)
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(transform, false);
            spacer.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 8f);

            // ==========================================
            // 5. KAMERA KONTROL BUTONLARı (180, YUKARı, ASAĞı, RESET)
            // ==========================================
            var cameraRow = CreateRowContainer("CameraRow", 150f, 95f);
            
            // Kamera Butonları için Grid Layout Group
            var gridObj = new GameObject("CameraGrid", typeof(RectTransform));
            gridObj.transform.SetParent(cameraRow.transform, false);
            StretchUI(gridObj);

            var grid = gridObj.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(42, 42); // Boyutlar korundu
            grid.spacing = new Vector2(6, 6);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleCenter;

            _cameraCanvasGroups.Clear();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            // 1. Row - Col 1: 180°
            var btn180 = CreateCircularButton(gridObj.transform, "180°", "180", () => {
                if (_cameraController != null) _cameraController.Rotate180();
            });
            if (btn180 != null) _cameraCanvasGroups.Add(btn180.AddComponent<CanvasGroup>());

            // 1. Row - Col 2: ▲ (Tilt Up)
            var upBtnObj = CreateCircularButton(gridObj.transform, "▲", "Up", null);
            if (upBtnObj != null)
            {
                _cameraCanvasGroups.Add(upBtnObj.AddComponent<CanvasGroup>());
                var hold = upBtnObj.AddComponent<KOMobileHoldButton>();
                hold.OnHold = () => {
                    if (_cameraController != null) _cameraController.TiltUp(Time.deltaTime * 60f);
                };
            }

            // 1. Row - Col 3: ▼ (Tilt Down)
            var downBtnObj = CreateCircularButton(gridObj.transform, "▼", "Down", null);
            if (downBtnObj != null)
            {
                _cameraCanvasGroups.Add(downBtnObj.AddComponent<CanvasGroup>());
                var hold = downBtnObj.AddComponent<KOMobileHoldButton>();
                hold.OnHold = () => {
                    if (_cameraController != null) _cameraController.TiltDown(Time.deltaTime * 60f);
                };
            }

            // 2. Row - Col 1: Yer tutucu dummy
            var dummy = new GameObject("DummySpacer", typeof(RectTransform));
            dummy.transform.SetParent(gridObj.transform, false);
            dummy.GetComponent<RectTransform>().sizeDelta = new Vector2(42, 42);

            // 2. Row - Col 2: RESET
            var resetBtnObj = CreateCircularButton(gridObj.transform, "RESET", "Reset", () => {
                if (_cameraController != null) _cameraController.ResetCamera();
            });
            if (resetBtnObj != null) _cameraCanvasGroups.Add(resetBtnObj.AddComponent<CanvasGroup>());

            // 2. Row - Col 3: GÖZ / GİZLE (Etiketsiz oluşturuyoruz)
            var eyeBtnObj = CreateCircularButton(gridObj.transform, "", "EyeToggle", ToggleCameraButtons);
            if (eyeBtnObj != null)
            {
                // Prosedürel Göz İkonu Çerçevesi (Yatay oval göz akı)
                var eyeContainer = new GameObject("EyeIcon", typeof(RectTransform));
                eyeContainer.transform.SetParent(eyeBtnObj.transform, false);
                
                var eyeRT = eyeContainer.GetComponent<RectTransform>();
                eyeRT.anchorMin = new Vector2(0.5f, 0.5f);
                eyeRT.anchorMax = new Vector2(0.5f, 0.5f);
                eyeRT.pivot = new Vector2(0.5f, 0.5f);
                eyeRT.sizeDelta = new Vector2(20f, 12f); // Yatay oval göz akı çerçevesi
                eyeRT.anchoredPosition = Vector2.zero;

                // Beyaz yuvarlak şablonu alıp dairesel göz çehresi için kullanıyoruz
                var circleSprite = GetRoundedRectSprite("shared_white_circle", 64, 64, 32, Color.white, Color.white, 0);

                var eyeImg = eyeContainer.AddComponent<Image>();
                eyeImg.sprite = circleSprite; // Daireyi yatay ovale esnetiyoruz
                eyeImg.color = new Color(0.9f, 0.75f, 0.25f, 0.95f); // Mat altın rengi göz çerçevesi
                eyeImg.raycastTarget = false;

                // Göz Bebeği (Pupil)
                var pupil = new GameObject("Pupil", typeof(RectTransform));
                pupil.transform.SetParent(eyeContainer.transform, false);
                
                var pupilRT = pupil.GetComponent<RectTransform>();
                pupilRT.anchorMin = new Vector2(0.5f, 0.5f);
                pupilRT.anchorMax = new Vector2(0.5f, 0.5f);
                pupilRT.pivot = new Vector2(0.5f, 0.5f);
                pupilRT.sizeDelta = new Vector2(6f, 6f); // Yuvarlak göz bebeği
                pupilRT.anchoredPosition = Vector2.zero;

                var pupilImg = pupil.AddComponent<Image>();
                pupilImg.sprite = circleSprite;
                pupilImg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f); // Koyu renk göz bebeği
                pupilImg.raycastTarget = false;

                // Gözün üzerine çizilecek yan çizgi (Slash Line)
                _eyeSlashLine = new GameObject("SlashLine", typeof(RectTransform));
                _eyeSlashLine.transform.SetParent(eyeBtnObj.transform, false);

                var slashRT = _eyeSlashLine.GetComponent<RectTransform>();
                slashRT.anchorMin = new Vector2(0.5f, 0.5f);
                slashRT.anchorMax = new Vector2(0.5f, 0.5f);
                slashRT.pivot = new Vector2(0.5f, 0.5f);
                slashRT.sizeDelta = new Vector2(2f, 22f); // 2px kalınlık, 22px uzunluk
                slashRT.anchoredPosition = Vector2.zero;
                slashRT.localRotation = Quaternion.Euler(0f, 0f, -45f); // 45 derece eğik çizgi

                var slashImg = _eyeSlashLine.AddComponent<Image>();
                slashImg.color = new Color(0.9f, 0.75f, 0.25f, 0.95f); // Altın rengi çizgi
                slashImg.raycastTarget = false;

                // Varsayılan durum: Butonlar görünür, yan çizgi aktif (Eye with Slash)
                _eyeSlashLine.SetActive(true);
                _cameraButtonsVisible = true;
            }
        }

        // ==========================================
        // PROSEDÜREL PREMIUM SPRITE GENERATOR
        // ==========================================

        private Sprite GetRoundedRectSprite(string key, int w, int h, int radius, Color fillColor, Color borderColor, int borderWidth)
        {
            if (_spriteCache.TryGetValue(key, out Sprite sp))
                return sp;

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = radius <= 0 ? FilterMode.Point : FilterMode.Bilinear; // Düz keskin kutular için Point filtresi
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (radius <= 0)
                    {
                        // Düz keskin köşeli dikdörtgen çizimi
                        bool isBorder = (x < borderWidth || x >= w - borderWidth || y < borderWidth || y >= h - borderWidth);
                        tex.SetPixel(x, y, isBorder ? borderColor : fillColor);
                        continue;
                    }

                    bool isInside = true;
                    float dx = 0, dy = 0;

                    // Köşe koordinat kontrolleri
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

                        // Yumuşak geçişli gölgeleme için kenar yumuşatma (anti-aliasing)
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

        private Sprite GetPremiumCameraSprite(string key, int size, Color borderCol)
        {
            if (_spriteCache.TryGetValue(key, out Sprite sp))
                return sp;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float center = size / 2f;
            float radius = center - 1f;

            float borderWidth = 1.5f;

            // Yarı saydam cam-morfik arka plan renkleri (Skillbar slotları ile uyumlu)
            Color centerColor = new Color(0.24f, 0.22f, 0.20f, 0.80f); // Merkez %80 opak koyu antrasit
            Color edgeColor = new Color(0.04f, 0.04f, 0.04f, 0.85f);   // Kenar %85 opak koyu siyah

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    Color pxColor = Color.clear;

                    if (dist <= radius)
                    {
                        if (dist >= radius - borderWidth)
                        {
                            pxColor = borderCol;
                        }
                        else
                        {
                            float fillDist = dist / (radius - borderWidth);
                            pxColor = Color.Lerp(centerColor, edgeColor, fillDist);

                            // Küre Parlaması (Sol-Üst Glossy Highlight)
                            float dx = x - (center - radius * 0.35f);
                            float dy = y - (center + radius * 0.35f);
                            float distToHighlight = Mathf.Sqrt(dx * dx + dy * dy);
                            float highlightRadius = radius * 0.35f;
                            if (distToHighlight < highlightRadius)
                            {
                                float gloss = (1f - distToHighlight / highlightRadius) * 0.32f;
                                pxColor.r = Mathf.Clamp01(pxColor.r + gloss);
                                pxColor.g = Mathf.Clamp01(pxColor.g + gloss * 0.95f);
                                pxColor.b = Mathf.Clamp01(pxColor.b + gloss * 0.90f);
                            }
                        }
                    }

                    // Yumuşatma (Anti-Aliasing) filtresi
                    if (dist > radius - 1f && dist <= radius + 1f)
                    {
                        float alpha = 1f - (dist - (radius - 1f)) / 2f;
                        pxColor.a *= Mathf.Clamp01(alpha);
                    }

                    tex.SetPixel(x, y, pxColor);
                }
            }

            tex.Apply();
            Sprite premiumSp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _spriteCache[key] = premiumSp;
            return premiumSp;
        }

        // ==========================================
        // YARDıMCı METOTLAR
        // ==========================================

        private GameObject CreateRowContainer(string name, float width, float height)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(transform, false);
            var rect = row.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            return row;
        }

        private void StretchUI(GameObject obj)
        {
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void AddTextShadow(GameObject obj)
        {
            var shadow = obj.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1, -1);
        }

        private void CreateGenieButton(Transform parent, string text, Color color, UnityEngine.Events.UnityAction action)
        {
            var btnObj = new GameObject("Btn_" + text, typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(24, 24); // Küçültüldü (28'den 24'e)

            var img = btnObj.AddComponent<Image>();
            img.sprite = GetRoundedRectSprite("btn_" + text, 24, 24, 4, color, new Color(0.45f, 0.35f, 0.15f, 0.8f), 1);

            var btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(action);

            var txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btnObj.transform, false);
            StretchUI(txtObj);

            var txt = txtObj.AddComponent<Text>();
            txt.font = _uiFont;
            txt.fontSize = 10;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.9f, 0.75f, 0.25f);
            txt.text = text;
            AddTextShadow(txtObj);
        }

        private void CreateAutoAttackButton(Transform parent)
        {
            var btnObj = new GameObject("Btn_AutoAttack", typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(48, 24); // Küçültüldü (56x28'den 48x24'e)

            var img = btnObj.AddComponent<Image>();
            img.sprite = GetRoundedRectSprite("btn_auto_attack", 48, 24, 4, new Color(0.12f, 0.12f, 0.12f, 0.95f), new Color(0.45f, 0.35f, 0.15f, 0.8f), 1);

            var btn = btnObj.AddComponent<Button>();

            var txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btnObj.transform, false);
            StretchUI(txtObj);

            var txt = txtObj.AddComponent<Text>();
            txt.font = _uiFont;
            txt.fontSize = 8;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.lineSpacing = 0.85f; // İki satırın birbirine yakın durması için
            txt.color = new Color(0.9f, 0.75f, 0.25f);
            txt.text = "AUTO\nATTACK"; // Alt alta iki satır
            AddTextShadow(txtObj);
        }

        private GameObject CreateCircularButton(Transform parent, string label, string id, UnityEngine.Events.UnityAction action)
        {
            var btnObj = new GameObject("Btn_Cam_" + id, typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(42, 42);

            var img = btnObj.AddComponent<Image>();
            // Kamera butonlarına premium cam ve mat altın efekti uyguluyoruz
            Color borderCol = new Color(0.60f, 0.48f, 0.22f, 0.90f);
            img.sprite = GetPremiumCameraSprite("cam_btn_premium_" + id, 42, borderCol);

            var btn = btnObj.AddComponent<Button>();
            if (action != null) btn.onClick.AddListener(action);

            var txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btnObj.transform, false);
            StretchUI(txtObj);

            var txt = txtObj.AddComponent<Text>();
            txt.font = _uiFont;
            txt.fontSize = label.Length > 2 ? 10 : 13;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.9f, 0.75f, 0.25f); // Gold rengi
            txt.text = label;
            AddTextShadow(txtObj);

            return btnObj;
        }

        private IEnumerator UpdateFPSTimer()
        {
            while (true)
            {
                yield return new WaitForSeconds(2.0f);
                if (GameManager.Instance != null && _txtServerName != null)
                {
                    _txtServerName.text = "ARES-1";
                }
            }
        }

        // ================================================================
        // GENIE (AUTO ATTACK) ÇALIŞTIRMA MANTISI
        // ================================================================

        private bool _isGenieActive = false;
        private Coroutine _genieCoroutine;
        private int _lastUsedSkillIndex = -1;

        private void StartGenie()
        {
            if (_isGenieActive) return;
            _isGenieActive = true;
            _lastUsedSkillIndex = -1;
            if (_genieCoroutine != null) StopCoroutine(_genieCoroutine);
            _genieCoroutine = StartCoroutine(AutoAttackLoop());
            
            KOUIManager.Instance?.AddMsgOutput("Genie Start", KOUIManager.D3DColorToUnity(0xff00ffff));
        }

        private void StopGenie()
        {
            _isGenieActive = false;
            if (_genieCoroutine != null)
            {
                StopCoroutine(_genieCoroutine);
                _genieCoroutine = null;
            }
            var pc = FindAnyObjectByType<Character.PlayerController>();
            if (pc != null)
            {
                pc.StopAutoAttack();
            }
            
            KOUIManager.Instance?.AddMsgOutput("Genie Stop", KOUIManager.D3DColorToUnity(0xff00ffff));
        }

        private IEnumerator AutoAttackLoop()
        {
            while (_isGenieActive)
            {
                var pc = FindAnyObjectByType<Character.PlayerController>();
                if (pc != null)
                {
                    float range = 40f;
                    if (KOMobileAutoAttackSettingsUI.Instance != null)
                    {
                        range = KOMobileAutoAttackSettingsUI.Instance.AttackRange;
                    }
                    // 1. Can / Mana Potu Kullanımı ve Destek/Buff Büyüleri
                    var gm = EntropyOnline.Core.GameManager.Instance;
                    if (gm != null)
                    {
                        var skillMgr = EntropyOnline.Combat.KOMagicSkillManager.Instance;
                        if (skillMgr != null)
                        {
                            // A. Can Potu Kontrolü
                            if (KOMobileAutoAttackSettingsUI.Instance != null && KOMobileAutoAttackSettingsUI.Instance.HpPotItemId > 0)
                            {
                                float hpPercent = gm.MaxHp > 0 ? (float)gm.CurrentHp / gm.MaxHp * 100f : 100f;
                                if (hpPercent <= KOMobileAutoAttackSettingsUI.Instance.HpPotThreshold)
                                {
                                    var pSkill = KOImport.SkillTableParser.FindByExhaustItem((uint)KOMobileAutoAttackSettingsUI.Instance.HpPotItemId);
                                    if (pSkill != null)
                                    {
                                        if (skillMgr.MsgSend_MagicProcess((int)gm.CharacterId, pSkill))
                                        {
                                            if (KOUIManager.Instance != null)
                                                KOUIManager.Instance.AddMsgOutput($"Using {pSkill.Name}", KOUIManager.D3DColorToUnity(0xffffff00));
                                        }
                                    }
                                }
                            }

                            // B. Mana Potu Kontrolü
                            if (KOMobileAutoAttackSettingsUI.Instance != null && KOMobileAutoAttackSettingsUI.Instance.MpPotItemId > 0)
                            {
                                float mpPercent = gm.MaxMp > 0 ? (float)gm.CurrentMp / gm.MaxMp * 100f : 100f;
                                if (mpPercent <= KOMobileAutoAttackSettingsUI.Instance.MpPotThreshold)
                                {
                                    var pSkill = KOImport.SkillTableParser.FindByExhaustItem((uint)KOMobileAutoAttackSettingsUI.Instance.MpPotItemId);
                                    if (pSkill != null)
                                    {
                                        if (skillMgr.MsgSend_MagicProcess((int)gm.CharacterId, pSkill))
                                        {
                                            if (KOUIManager.Instance != null)
                                                KOUIManager.Instance.AddMsgOutput($"Using {pSkill.Name}", KOUIManager.D3DColorToUnity(0xffffff00));
                                        }
                                    }
                                }
                            }

                            // C. Buff Yetenekleri
                            if (KOMobileAutoAttackSettingsUI.Instance != null && KOMobileAutoAttackSettingsUI.Instance.BuffSkillIds != null)
                            {
                                int[] buffSkills = KOMobileAutoAttackSettingsUI.Instance.BuffSkillIds;
                                for (int i = 0; i < buffSkills.Length; i++)
                                {
                                    int magicId = buffSkills[i];
                                    if (magicId <= 0) continue;

                                    if (KOUIManager.Instance != null && KOUIManager.Instance.IsBuffActive(magicId))
                                        continue;

                                    var pSkill = KOImport.SkillTableParser.Find(magicId);
                                    if (pSkill == null) continue;

                                    if (skillMgr.MsgSend_MagicProcess((int)gm.CharacterId, pSkill))
                                    {
                                        if (KOUIManager.Instance != null)
                                            KOUIManager.Instance.AddMsgOutput($"Using {pSkill.Name}", KOUIManager.D3DColorToUnity(0xffffff00));
                                        break; // Her adımda en fazla 1 buff tetiklensin
                                    }
                                }
                            }

                            // D. Buff Eşyaları (Scroll vb.)
                            if (KOMobileAutoAttackSettingsUI.Instance != null && KOMobileAutoAttackSettingsUI.Instance.BuffItemIds != null)
                            {
                                int[] buffItems = KOMobileAutoAttackSettingsUI.Instance.BuffItemIds;
                                for (int i = 0; i < buffItems.Length; i++)
                                {
                                    int itemId = buffItems[i];
                                    if (itemId <= 0) continue;

                                    var pSkill = KOImport.SkillTableParser.FindByExhaustItem((uint)itemId);
                                    if (pSkill == null) continue;

                                    if (KOUIManager.Instance != null && KOUIManager.Instance.IsBuffActive(pSkill.Id))
                                        continue;

                                    if (skillMgr.MsgSend_MagicProcess((int)gm.CharacterId, pSkill))
                                    {
                                        if (KOUIManager.Instance != null)
                                            KOUIManager.Instance.AddMsgOutput($"Using {pSkill.Name}", KOUIManager.D3DColorToUnity(0xffffff00));
                                        break; // Her adımda en fazla 1 buff tetiklensin
                                    }
                                }
                            }
                        }
                    }

                    // 1. Hedef kontrolü
                    var currentTarget = EntropyOnline.World.KOTargetSelector.Instance != null ? 
                        EntropyOnline.World.KOTargetSelector.Instance.CurrentTarget : null;

                    bool followLeader = KOMobileAutoAttackSettingsUI.Instance != null && KOMobileAutoAttackSettingsUI.Instance.FollowLeaderEnabled;
                    
                    if (followLeader)
                    {
                        // Liderin hedefine kilitlen (Target Assist)
                        if (KOPartyManager.Instance != null && KOPartyManager.Instance.MemberCount > 0)
                        {
                            long leaderTargetId = KOPartyManager.Instance.LeaderTargetId;
                            if (leaderTargetId > 0)
                            {
                                var leaderTarget = EntropyOnline.World.EntityManager.Instance != null ?
                                    EntropyOnline.World.EntityManager.Instance.GetEntityByInstanceId(leaderTargetId) : null;

                                if (leaderTarget != null && !leaderTarget.IsDead &&
                                    Vector3.Distance(pc.transform.position, leaderTarget.transform.position) <= range)
                                {
                                    if (currentTarget != leaderTarget)
                                    {
                                        currentTarget = leaderTarget;
                                        if (EntropyOnline.World.KOTargetSelector.Instance != null)
                                            EntropyOnline.World.KOTargetSelector.Instance.SelectTargetByID(currentTarget.ServerInstanceId, true);
                                    }
                                }
                                else
                                {
                                    // Hedef menzil dışı, ölü veya geçersizse temizle
                                    if (currentTarget != null)
                                    {
                                        currentTarget = null;
                                        if (EntropyOnline.World.KOTargetSelector.Instance != null)
                                            EntropyOnline.World.KOTargetSelector.Instance.ClearTarget();
                                    }
                                }
                            }
                            else
                            {
                                if (currentTarget != null)
                                {
                                    currentTarget = null;
                                    if (EntropyOnline.World.KOTargetSelector.Instance != null)
                                        EntropyOnline.World.KOTargetSelector.Instance.ClearTarget();
                                }
                            }
                        }
                    }
                    else
                    {
                        // Lideri takip etme aktif değilse, normal en yakın hedefi bul
                        if (currentTarget == null || currentTarget.IsDead || Vector3.Distance(pc.transform.position, currentTarget.transform.position) > range)
                        {
                            if (EntropyOnline.World.KOTargetSelector.Instance != null)
                            {
                                EntropyOnline.World.KOTargetSelector.Instance.ClearTarget();
                            }
                            
                            currentTarget = FindClosestMonster(pc.transform.position, range);
                            if (currentTarget != null && EntropyOnline.World.KOTargetSelector.Instance != null)
                            {
                                EntropyOnline.World.KOTargetSelector.Instance.SelectTargetByID(currentTarget.ServerInstanceId, true);
                            }
                        }
                    }

                    // 2. Yetenek kullanımı veya düz vuruş (R)
                    if (currentTarget != null && !currentTarget.IsDead)
                    {
                        bool skillUsed = false;
                        
                        if (KOMobileAutoAttackSettingsUI.Instance != null && KOMobileAutoAttackSettingsUI.Instance.AttackSkillIds != null)
                        {
                            var skillMgr = EntropyOnline.Combat.KOMagicSkillManager.Instance;
                            if (skillMgr != null)
                            {
                                int[] skills = KOMobileAutoAttackSettingsUI.Instance.AttackSkillIds;
                                bool skillInOrder = KOMobileAutoAttackSettingsUI.Instance.SkillInOrderEnabled;

                                if (skillInOrder)
                                {
                                    int startIdx = (_lastUsedSkillIndex + 1) % skills.Length;
                                    for (int i = 0; i < skills.Length; i++)
                                    {
                                        int idx = (startIdx + i) % skills.Length;
                                        int magicId = skills[idx];
                                        if (magicId <= 0) continue;

                                        var pSkill = KOImport.SkillTableParser.Find(magicId);
                                        if (pSkill == null) continue;

                                        if (skillMgr.MsgSend_MagicProcess((int)currentTarget.ServerInstanceId, pSkill))
                                        {
                                            if (KOUIManager.Instance != null)
                                            {
                                                KOUIManager.Instance.AddMsgOutput($"Using {pSkill.Name}", KOUIManager.D3DColorToUnity(0xffffff00));
                                            }
                                            _lastUsedSkillIndex = idx;
                                            skillUsed = true;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < skills.Length; i++)
                                    {
                                        int magicId = skills[i];
                                        if (magicId <= 0) continue;

                                        var pSkill = KOImport.SkillTableParser.Find(magicId);
                                        if (pSkill == null) continue;

                                        if (skillMgr.MsgSend_MagicProcess((int)currentTarget.ServerInstanceId, pSkill))
                                        {
                                            if (KOUIManager.Instance != null)
                                            {
                                                KOUIManager.Instance.AddMsgOutput($"Using {pSkill.Name}", KOUIManager.D3DColorToUnity(0xffffff00));
                                            }
                                            skillUsed = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        // Hiçbir yetenek kullanılmadıysa veya eklenmediyse, R durumunu kontrol et
                        bool basicAttackEnabled = KOMobileAutoAttackSettingsUI.Instance == null || KOMobileAutoAttackSettingsUI.Instance.BasicAttackEnabled;
                        
                        if (basicAttackEnabled)
                        {
                            if (!skillUsed && !pc.IsAutoAttacking)
                            {
                                pc.StartAutoAttack(currentTarget);
                            }
                        }
                        else
                        {
                            // Basic Attack kapalıysa, fiziksel R vuruşlarını durdur
                            if (pc.IsAutoAttacking)
                            {
                                pc.StopAutoAttack();
                            }
                        }
                    }
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        private EntropyOnline.World.KOEntity FindClosestMonster(Vector3 playerPos, float maxRange)
        {
            if (EntropyOnline.World.EntityManager.Instance == null) return null;
            
            EntropyOnline.World.KOEntity closest = null;
            float minDist = maxRange;
            
            foreach (var kvp in EntropyOnline.World.EntityManager.Instance.Monsters)
            {
                var mv = kvp.Value;
                if (mv == null || mv.Root == null) 
                    continue;
                    
                var entity = mv.Root.GetComponent<EntropyOnline.World.KOEntity>();
                if (entity == null || entity.IsDead)
                    continue;
                    
                float dist = Vector3.Distance(playerPos, mv.Root.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = entity;
                }
            }
            return closest;
        }

        private void ToggleCameraButtons()
        {
            _cameraButtonsVisible = !_cameraButtonsVisible;

            // Yan çizgi durumunu güncelle (Gizliyken çizgi kalkar, görünürken çizgi geri gelir)
            if (_eyeSlashLine != null)
            {
                _eyeSlashLine.SetActive(_cameraButtonsVisible);
            }

            // Diğer 4 butonu pürüzsüzce gizle/göster (Layout bozulmadan)
            foreach (var cg in _cameraCanvasGroups)
            {
                if (cg != null)
                {
                    cg.alpha = _cameraButtonsVisible ? 1f : 0f;
                    cg.blocksRaycasts = _cameraButtonsVisible;
                    cg.interactable = _cameraButtonsVisible;
                }
            }

        }
    }

    /// <summary>
    /// Butona basılı tutulduğunda sürekli tetikleme sağlayan helper bileşeni
    /// </summary>
    public class KOMobileHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public System.Action OnHold;
        private bool _isPressed = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPressed = false;
        }

        private void Update()
        {
            if (_isPressed)
            {
                OnHold?.Invoke();
            }
        }
    }
}
