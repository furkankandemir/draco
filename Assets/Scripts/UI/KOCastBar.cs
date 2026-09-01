using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Combat;
using KOImport;

namespace EntropyOnline.UI
{
    public class KOCastBar : MonoBehaviour
    {
        private GameObject _castBarPanel;
        
        private Image _bgImage;
        private Image _fillImage;
        private Text _skillNameText;
        private Text _timeText;

        private Sprite _bgSprite;
        private Sprite _fillSprite;
        private bool _isSubscribed = false;

        private void Update()
        {
            if (!_isSubscribed)
            {
                if (KOMagicSkillManager.Instance != null)
                {
                    KOMagicSkillManager.Instance.OnCastingProgress += UpdateProgress;
                    KOMagicSkillManager.Instance.OnCastingComplete += HideCastBar;
                    KOMagicSkillManager.Instance.OnCastingFail += HideCastBar;
                    _isSubscribed = true;
                }
            }
        }

        private void Start()
        {
            // İstemci ekran arayüzü Canvas'ını bul (3D WorldSpace olan isimlik vb. canvas'ları pas geç)
            Canvas mainCanvas = null;
            var uiMgr = Object.FindAnyObjectByType<KOUIManager>();
            if (uiMgr != null)
            {
                mainCanvas = uiMgr.GetComponentInParent<Canvas>();
                if (mainCanvas != null && mainCanvas.renderMode == RenderMode.WorldSpace)
                {
                    mainCanvas = null;
                }
            }
            if (mainCanvas == null)
            {
                var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
                foreach (var c in canvases)
                {
                    if (c.renderMode != RenderMode.WorldSpace && c.gameObject.activeInHierarchy)
                    {
                        mainCanvas = c;
                        break;
                    }
                }
            }

            if (mainCanvas == null)
            {
                Debug.LogError("[KOCastBar] Ekran arayüzü (Screen Space) UI Canvas bulunamadı!");
                return;
            }

            // Çok daha ince ve kibar arayüz sprite'ları (Dikey gradyan geçişli)
            _bgSprite = CreateGradientSprite(130, 7, new Color(0.12f, 0.12f, 0.12f, 0.85f), new Color(0.04f, 0.04f, 0.04f, 0.85f));
            _fillSprite = CreateGradientSprite(130, 7, new Color(253f / 255f, 184f / 255f, 99f / 255f, 1f), new Color(229f / 255f, 103f / 255f, 23f / 255f, 1f));

            // Ekran üzerinde sabit konumda duracak panel (Screen Space)
            _castBarPanel = new GameObject("PlayerCastBarPanel", typeof(RectTransform));
            _castBarPanel.transform.SetParent(mainCanvas.transform, false);

            var panelRT = _castBarPanel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(200f, 40f);
            // Y pozisyonunu -140f olarak güncelledik (karakterin altında ve menü barının üzerinde kalması için)
            panelRT.anchoredPosition = new Vector2(0f, -140f); 

            // 1. Arka Plan Resmi
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(_castBarPanel.transform, false);
            _bgImage = bgObj.AddComponent<Image>();
            _bgImage.sprite = _bgSprite;
            _bgImage.color = Color.white;

            var bgRT = bgObj.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.5f, 0.5f);
            bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.sizeDelta = new Vector2(130f, 7f); // Yatay uzunluk 130px, dikey kalınlık 7px yapıldı

            // 2. Dolan Bar (Progress Fill)
            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(bgObj.transform, false);
            _fillImage = fillObj.AddComponent<Image>();
            _fillImage.sprite = _fillSprite;
            _fillImage.color = Color.white; // Gradyan geçişini göstermek için beyaz yapıldı
            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Horizontal;
            _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _fillImage.fillAmount = 0f;

            var fillRT = fillObj.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = new Vector2(1f, 1f);
            fillRT.offsetMax = new Vector2(-1f, -1f);

            // 3. Yetenek Adı (Barın üzerinde)
            var nameObj = new GameObject("SkillName");
            nameObj.transform.SetParent(_castBarPanel.transform, false);
            _skillNameText = nameObj.AddComponent<Text>();
            _skillNameText.alignment = TextAnchor.MiddleCenter;
            _skillNameText.fontSize = 12; // Font size 12 yapıldı
            _skillNameText.fontStyle = FontStyle.Bold;
            _skillNameText.color = Color.white;
            _skillNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _skillNameText.verticalOverflow = VerticalWrapMode.Overflow;
            _skillNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_skillNameText.font == null)
                _skillNameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var nameOutline = nameObj.AddComponent<Outline>();
            nameOutline.effectColor = Color.black;
            nameOutline.effectDistance = new Vector2(1f, -1f);

            var nameRT = nameObj.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0.5f, 0.5f);
            nameRT.anchorMax = new Vector2(0.5f, 0.5f);
            nameRT.pivot = new Vector2(0.5f, 0.5f);
            nameRT.sizeDelta = new Vector2(200f, 16f);
            nameRT.anchoredPosition = new Vector2(0f, 12f); // Bar inceldiği için 12px yapıldı

            // 4. Kalan Süre Metni (Barın altında)
            var timeObj = new GameObject("TimeText");
            timeObj.transform.SetParent(_castBarPanel.transform, false);
            _timeText = timeObj.AddComponent<Text>();
            _timeText.alignment = TextAnchor.MiddleCenter;
            _timeText.fontSize = 11; // Font size 11 yapıldı
            _timeText.fontStyle = FontStyle.Bold;
            _timeText.color = new Color(218f / 255f, 208f / 255f, 100f / 255f, 1f);
            _timeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _timeText.verticalOverflow = VerticalWrapMode.Overflow;
            _timeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_timeText.font == null)
                _timeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var timeOutline = timeObj.AddComponent<Outline>();
            timeOutline.effectColor = Color.black;
            timeOutline.effectDistance = new Vector2(1f, -1f);

            var timeRT = timeObj.GetComponent<RectTransform>();
            timeRT.anchorMin = new Vector2(0.5f, 0.5f);
            timeRT.anchorMax = new Vector2(0.5f, 0.5f);
            timeRT.pivot = new Vector2(0.5f, 0.5f);
            timeRT.sizeDelta = new Vector2(200f, 14f);
            timeRT.anchoredPosition = new Vector2(0f, -12f); // Bar inceldiği için -12px yapıldı

            _castBarPanel.SetActive(false); // Varsayılan olarak kapalı
        }

        private void OnDestroy()
        {
            if (KOMagicSkillManager.Instance != null)
            {
                KOMagicSkillManager.Instance.OnCastingProgress -= UpdateProgress;
                KOMagicSkillManager.Instance.OnCastingComplete -= HideCastBar;
                KOMagicSkillManager.Instance.OnCastingFail -= HideCastBar;
            }
            if (_castBarPanel != null)
            {
                Destroy(_castBarPanel);
            }
            if (_bgSprite != null && _bgSprite.texture != null)
            {
                Destroy(_bgSprite.texture);
                Destroy(_bgSprite);
            }
            if (_fillSprite != null && _fillSprite.texture != null)
            {
                Destroy(_fillSprite.texture);
                Destroy(_fillSprite);
            }
        }

        private void UpdateProgress(float current, float total)
        {
            if (_castBarPanel == null) return;

            if (!_castBarPanel.activeSelf)
            {
                _castBarPanel.SetActive(true);
                
                string skillName = "Casting...";
                if (KOMagicSkillManager.Instance != null)
                {
                    var mng = KOMagicSkillManager.Instance;
                    var skill = SkillTableParser.Find((int)mng.CurrentCastingMagicID);
                    if (skill != null)
                    {
                        skillName = skill.Name;
                    }
                }
                _skillNameText.text = skillName;
            }

            _fillImage.fillAmount = Mathf.Clamp01(current / total);
            
            float remaining = Mathf.Max(0f, total - current);
            _timeText.text = string.Format("{0:F1} sec", remaining);
        }

        private void HideCastBar()
        {
            if (_castBarPanel != null && _castBarPanel.activeSelf)
            {
                _castBarPanel.SetActive(false);
            }
        }

        private Sprite CreateGradientSprite(int width, int height, Color colorTop, Color colorBottom)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] colors = new Color[width * height];
            
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / Mathf.Max(1, height - 1);
                Color rowColor = Color.Lerp(colorBottom, colorTop, t);
                for (int x = 0; x < width; x++)
                {
                    colors[y * width + x] = rowColor;
                }
            }
            
            tex.SetPixels(colors);
            tex.filterMode = FilterMode.Point; // Keskin kenarlar için Point filtreleme
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }
    }
}