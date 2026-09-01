using UnityEngine;
using UnityEngine.UI;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Küçültülmüş PM Simgesi (co_whisper_close_us) Kontrolcüsü.
    /// </summary>
    public class KOWhisperClosePanel : MonoBehaviour
    {
        public string TargetName { get; private set; }

        [SerializeField] private Text exit_id;
        [SerializeField] private Button btn_open;
        [SerializeField] private Transform btn_bar;

        public void Initialize(string targetName)
        {
            TargetName = targetName;

            if (exit_id == null) exit_id = transform.Find("exit_id")?.GetComponent<Text>();
            if (btn_open == null) btn_open = transform.Find("btn_open")?.GetComponent<Button>();
            if (btn_bar == null) btn_bar = transform.Find("btn_bar");

            if (exit_id != null)
            {
                exit_id.text = targetName;
            }

            if (btn_open != null)
            {
                btn_open.onClick.RemoveAllListeners();
                btn_open.onClick.AddListener(OnRestoreClicked);
            }

            // Başlık çubuğuna tıklanırsa da geri yükle
            var btnBarClick = btn_bar?.GetComponent<Button>();
            if (btnBarClick != null)
            {
                btnBarClick.onClick.RemoveAllListeners();
                btnBarClick.onClick.AddListener(OnRestoreClicked);
            }

            ApplyModernTheme();
        }

        private void AddLocalTextShadow(Text txt)
        {
            if (txt == null || txt.gameObject.GetComponent<Shadow>() != null) return;
            var shadow = txt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private void ApplyModernTheme()
        {
            var uiMgr = KOUIManager.Instance;
            if (uiMgr == null) return;

            // 1. RawImage arka planlarını Skill Theme yap veya devre dışı bırak
            var rawImages = GetComponentsInChildren<RawImage>(true);
            foreach (var raw in rawImages)
            {
                if (raw.gameObject == btn_bar?.gameObject)
                {
                    var sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_close_bar_bg", 120, 10, 0,
                        new Color(0.18f, 0.15f, 0.12f, 0.95f),
                        new Color(0.45f, 0.35f, 0.15f, 0.9f),
                        1);
                    if (sprite != null)
                    {
                        raw.texture = sprite.texture;
                        raw.uvRect = new Rect(0, 0, 1, 1);
                        raw.enabled = true;
                    }
                }
                else if (raw.gameObject == gameObject)
                {
                    var sprite = uiMgr.GetSkillThemePanelBgSprite("whisper_close_bg", 120, 30, 4,
                        new Color(0.12f, 0.10f, 0.08f, 0.98f),
                        new Color(0.04f, 0.04f, 0.04f, 0.98f),
                        new Color(0.6f, 0.48f, 0.22f, 0.9f),
                        2);
                    if (sprite != null)
                    {
                        raw.texture = sprite.texture;
                        raw.uvRect = new Rect(0, 0, 1, 1);
                        raw.enabled = true;
                    }
                    
                    var outline = raw.gameObject.GetComponent<Outline>();
                    if (outline != null) Destroy(outline);
                }
                else
                {
                    raw.enabled = false;
                }
            }

            // 3. Yazı rengi
            if (exit_id != null)
            {
                exit_id.color = new Color(0.9f, 0.75f, 0.55f, 1f); // Altın sarısı metin
                exit_id.fontStyle = FontStyle.Bold;
                AddLocalTextShadow(exit_id);
            }

            // 4. Restore butonu tıklama geri bildirimi
            if (btn_open != null && btn_open.gameObject.GetComponent<UIButtonScaleFeedback>() == null)
            {
                btn_open.gameObject.AddComponent<UIButtonScaleFeedback>();
            }
        }

        private void OnRestoreClicked()
        {
            KOWhisperManager.Instance.RestoreWindow(TargetName);
        }
    }
}
