using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.World;
using EntropyOnline.UI;

namespace EntropyOnline.World
{
    /// <summary>
    /// Spawns an OPEN button in world-space floating above the loot box.
    /// Appears when the player is close enough, and opens the loot window when clicked.
    /// </summary>
    public class KOLootBoxInteractButton : MonoBehaviour, KOProximityInteractRegistry.IProximityInteractable
    {
        private KOLootBox _lootBox;
        private Canvas _canvas;
        private RectTransform _canvasRT;
        private Transform _localPlayerTransform;
        private float _heightOffset = 0.5f;

        private float _currentDistanceToPlayer = 999f;
        private bool _isInRange = false;

        public Transform GetTransform() { return transform; }
        public float GetCurrentDistance() { return _currentDistanceToPlayer; }
        public void SetCurrentDistance(float dist) { _currentDistanceToPlayer = dist; }
        public void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.gameObject.activeSelf != visible)
            {
                _canvas.gameObject.SetActive(visible);
            }
        }
        public bool IsInRange() { return _isInRange; }

        private void OnDisable()
        {
            KOProximityInteractRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            KOProximityInteractRegistry.Unregister(this);
        }

        public void Initialize(KOLootBox lootBox)
        {
            _lootBox = lootBox;
            
            // Adjust height offset based on BoxCollider size
            var col = GetComponent<BoxCollider>();
            if (col != null)
            {
                _heightOffset = col.center.y + (col.size.y * 0.5f) + 0.15f;
            }
            
            CreateUI();
        }

        private void CreateUI()
        {
            // Create ZTest Always material using our custom shader so the UI is not obscured by models
            Material customUiMat = null;
            var uiShader = Shader.Find("UI/AlwaysOnTop") ?? Shader.Find("UI/Default");
            if (uiShader != null)
            {
                customUiMat = new Material(uiShader);
                if (uiShader.name == "UI/Default")
                {
                    customUiMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                }
            }

            // 1. Create Canvas
            GameObject canvasObj = new GameObject("LootBoxInteractCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, _heightOffset, 0f);
            canvasObj.transform.localRotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.one * 0.016f;

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 105; // Make sure it renders in front

            _canvasRT = canvasObj.GetComponent<RectTransform>();
            _canvasRT.sizeDelta = new Vector2(96f, 36f);

            // Add GraphicRaycaster for click support
            canvasObj.AddComponent<GraphicRaycaster>();

            // 2. Button Background
            GameObject btnObj = new GameObject("OpenButton");
            btnObj.transform.SetParent(canvasObj.transform, false);
            var btnRT = btnObj.AddComponent<RectTransform>();
            btnRT.anchorMin = Vector2.zero;
            btnRT.anchorMax = Vector2.one;
            btnRT.sizeDelta = Vector2.zero;

            var img = btnObj.AddComponent<Image>();
            img.sprite = CreateGlassButtonSprite(192, 72);
            img.color = Color.white;
            if (customUiMat != null)
            {
                img.material = customUiMat;
            }

            var button = btnObj.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.2f, 1.3f, 1f); // Parlama
            colors.pressedColor = new Color(0.7f, 0.8f, 0.9f, 1f); // Koyu
            colors.selectedColor = new Color(1.1f, 1.2f, 1.3f, 1f);
            button.colors = colors;
            button.onClick.AddListener(OnButtonClick);

            // 3. Button Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.5f, 0.5f);
            textRT.anchorMax = new Vector2(0.5f, 0.5f);
            textRT.sizeDelta = new Vector2(96f, 36f);
            textRT.pivot = new Vector2(0.5f, 0.5f);
            textRT.anchoredPosition = new Vector2(0f, 1.5f); // Dikeyde tam ortalama için Y değerini +1.5f yukarı kaydırdık

            var txt = textObj.AddComponent<Text>();
            txt.text = "Open";
            txt.fontSize = 18;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
            if (txt.font == null)
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null)
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (customUiMat != null)
            {
                txt.material = customUiMat;
            }

            // Gölge ekle
            var shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.1f, 0.3f, 0.9f); // Mavi-koyu gölge
            shadow.effectDistance = new Vector2(1f, -1f);

            _canvas.gameObject.SetActive(false);
        }

        private void OnButtonClick()
        {
            if (_lootBox != null)
            {
                _lootBox.OpenChest();
                if (LootDropUI.Instance != null)
                {
                    LootDropUI.Instance.SendBundleOpen(_lootBox.BundleID, _lootBox.CorpseEntity);
                }
            }
        }

        private void LateUpdate()
        {
            if (_canvas == null || _lootBox == null) return;

            // Find local player
            if (_localPlayerTransform == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
                if (playerObj != null)
                {
                    _localPlayerTransform = playerObj.transform;
                }
            }

            // Proximity check
            bool showButton = false;
            if (_localPlayerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _localPlayerTransform.position);
                float fDLimit = 8.0f; // Loot distance limit
                if (dist <= fDLimit)
                {
                    showButton = true;
                    _currentDistanceToPlayer = dist;
                }
                else
                {
                    _currentDistanceToPlayer = 999f;
                }
            }
            else
            {
                _currentDistanceToPlayer = 999f;
            }

            _isInRange = showButton;

            if (_isInRange)
            {
                KOProximityInteractRegistry.Register(this);
            }
            else
            {
                KOProximityInteractRegistry.Unregister(this);
                if (_canvas.gameObject.activeSelf)
                {
                    _canvas.gameObject.SetActive(false);
                }
            }

            KOProximityInteractRegistry.UpdateRegistry();

            if (!_canvas.gameObject.activeSelf) return;

            // Rotate towards camera, keep constant scale and offset slightly towards camera
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                float offsetX = 0f;
                float offsetZ = 0f;
                float boxRadius = 0.5f;
                var col = GetComponent<BoxCollider>();
                if (col != null)
                {
                    offsetX = col.center.x;
                    offsetZ = col.center.z;
                    boxRadius = Mathf.Max(col.size.x, col.size.z) * 0.5f;
                }

                Vector3 localPos = new Vector3(offsetX, _heightOffset, offsetZ);
                Vector3 worldPos = transform.TransformPoint(localPos);
                Vector3 toCam = (cam.transform.position - worldPos).normalized;

                float scaleX = transform.localScale.x;
                float offsetDist = (boxRadius * scaleX) + 0.15f;
                
                _canvas.transform.position = worldPos + toCam * offsetDist;
                _canvas.transform.rotation = cam.transform.rotation;

                // Scale based on distance
                float distance = Vector3.Distance(_canvas.transform.position, cam.transform.position);
                float scale = 0.016f * (distance / 9.0f);
                
                float parentScale = transform.lossyScale.x;
                if (parentScale > 0.01f)
                {
                    _canvas.transform.localScale = Vector3.one * (scale / parentScale);
                }
                else
                {
                    _canvas.transform.localScale = Vector3.one * scale;
                }
            }
        }

        /// <summary>
        /// Envanter slotundaki procedural glassmorphism sprite'ını butona özel üretir.
        /// </summary>
        private Sprite CreateGlassButtonSprite(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color centerColor = new Color(0.02f, 0.28f, 0.60f, 0.95f);    // rich cobalt blue (referans görseldeki asil koyu mavi)
            Color edgeColor = new Color(0.01f, 0.15f, 0.35f, 0.95f);      // deep navy blue (kenar laciverti)
            Color horizontalColor = new Color(0.12f, 0.22f, 0.35f, 0.95f); // metalik mavi/çelik dış kenarlık
            Color brightBronzeColor = new Color(0.35f, 0.55f, 0.75f, 0.95f); // gümüş/cyan iç parlama çizgisi

            float cy = h / 2f;
            float indent = h * 0.4f; // sivri ok uçlarının derinliği
            float cosTheta = 0.78f; // eğik uçlarda çizgi kalınlığının eşit olması için açı çarpanı

            // Yüksek çözünürlük ölçek faktörü (örneğin 192x72, 96x36'nın 2 katı çözünürlüğündedir)
            float resScale = h / 36f;
            float borderOuter = 1.5f * resScale;
            float borderInner = 3.0f * resScale;
            float shadowGap = 4.0f * resScale;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Altıgen üyelik testi
                    float leftBound = Mathf.Abs(y - cy) * (indent / cy);
                    float rightBound = w - leftBound;

                    // Sınırlara olan en yakın mesafeyi bul
                    float distToLeft = (x - leftBound) * cosTheta;
                    float distToRight = (rightBound - x) * cosTheta;
                    float distToTop = (h - 1) - y;
                    float distToBottom = y;
                    float minDist = Mathf.Min(Mathf.Min(distToLeft, distToRight), Mathf.Min(distToTop, distToBottom));

                    // Kenarlarda Anti-Aliasing (Yumuşatma) hesabı
                    if (minDist < 0f)
                    {
                        // 1 piksellik yumuşak geçiş alanı (minDist -1 ile 0 arasında)
                        float edgeFade = Mathf.Clamp01(1f + minDist);
                        if (edgeFade > 0.01f)
                        {
                            Color edgeC = horizontalColor;
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
                        // Dış metalik mavi kenarlık
                        tex.SetPixel(x, y, horizontalColor);
                    }
                    else if (minDist < borderInner)
                    {
                        // İç gümüş parlaklık çizgisi (bevel)
                        tex.SetPixel(x, y, brightBronzeColor);
                    }
                    else if (minDist < shadowGap)
                    {
                        // Gölgelendirme oluğu (koyu mavi/siyah)
                        tex.SetPixel(x, y, new Color(0.01f, 0.05f, 0.15f, 0.95f));
                    }
                    else
                    {
                        // Dikey mavi gradyan dolgu
                        float fade = Mathf.Abs(y - cy) / cy;
                        Color fillColor = Color.Lerp(centerColor, edgeColor, fade);

                        // Çok hafif cam parlaması
                        float hx = w * 0.35f;
                        float hy = h * 0.65f;
                        float hdx = (x - hx) * ((float)h / w);
                        float hdy = y - hy;
                        float distToHighlight = Mathf.Sqrt(hdx * hdx + hdy * hdy);
                        float highlightRadius = h * 0.35f;

                        if (distToHighlight < highlightRadius)
                        {
                            float factor = 1f - (distToHighlight / highlightRadius);
                            factor = factor * factor * (3f - 2f * factor);
                            float gloss = factor * 0.06f; // Yansıma gücü %6'ya düşürülerek yumuşatıldı
                            fillColor.r = Mathf.Clamp01(fillColor.r + gloss * 0.5f);
                            fillColor.g = Mathf.Clamp01(fillColor.g + gloss * 0.8f);
                            fillColor.b = Mathf.Clamp01(fillColor.b + gloss);
                            fillColor.a = Mathf.Clamp01(fillColor.a + gloss * 0.5f);
                        }

                        tex.SetPixel(x, y, fillColor);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f));
        }
    }
}
