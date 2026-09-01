using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace EntropyOnline.UI
{
    public class KOWhisperDirectoryPanel : MonoBehaviour
    {
        [SerializeField] private Transform _scrollContent;
        [SerializeField] private Button _btnExit;
        [SerializeField] private Button _btnRemoveAll;
        [SerializeField] private Button _btnPmActive;

        public void Initialize()
        {
            var uiMgr = KOUIManager.Instance;
            if (uiMgr == null) return;

            // Centering/Positioning on left side of the screen
            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(304f, 180f);

                if (gameObject.GetComponent<KOUIScaleIndependent>() == null)
                {
                    gameObject.AddComponent<KOUIScaleIndependent>();
                }

                float s = uiMgr.CanvasScaleFactor;
                if (s > 0f)
                {
                    rt.anchoredPosition = new Vector2(50f / s, 190f / s); // opens 50px from left edge
                }
                else
                {
                    rt.anchoredPosition = new Vector2(50f, 190f);
                }
            }

            bool isLegacy = (_scrollContent == null);
            if (!isLegacy)
            {
                ApplyModernTheme();
                return;
            }

            // 1. Background Image using Skill Theme
            var bgImg = gameObject.GetComponent<Image>();
            if (bgImg == null) bgImg = gameObject.AddComponent<Image>();
            bgImg.sprite = uiMgr.GetSkillThemePanelBgSprite("whisper_dir_bg_180", 304, 180, 0,
                new Color(0.12f, 0.10f, 0.08f, 0.98f),
                new Color(0.04f, 0.04f, 0.04f, 0.98f),
                new Color(0.6f, 0.48f, 0.22f, 0.9f),
                2);
            bgImg.color = Color.white;
            bgImg.raycastTarget = true;

            // 2. Drag handler on header (contained inside border)
            var dragBar = new GameObject("DragBar", typeof(RectTransform));
            dragBar.transform.SetParent(transform, false);
            var rtBar = dragBar.GetComponent<RectTransform>();
            rtBar.anchorMin = new Vector2(0f, 1f);
            rtBar.anchorMax = new Vector2(1f, 1f);
            rtBar.pivot = new Vector2(0.5f, 1f);
            rtBar.anchoredPosition = new Vector2(0f, -2f); // 2px down from top border
            rtBar.sizeDelta = new Vector2(-4f, 30f); // 4px narrower to fit inside border, 30px height

            var barImg = dragBar.AddComponent<Image>();
            barImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_dir_bar_30", 300, 30, 0,
                new Color(0.18f, 0.15f, 0.12f, 0.95f),
                new Color(0.45f, 0.35f, 0.15f, 0.9f),
                1);
            dragBar.AddComponent<UIDragHandler>();

            // Title
            var titleObj = new GameObject("Title", typeof(RectTransform));
            titleObj.transform.SetParent(dragBar.transform, false);
            var rtTitle = titleObj.GetComponent<RectTransform>();
            rtTitle.anchorMin = Vector2.zero;
            rtTitle.anchorMax = Vector2.one;
            rtTitle.offsetMin = new Vector2(30f, 0f);
            rtTitle.offsetMax = new Vector2(-30f, 0f);
            var txtTitle = titleObj.AddComponent<Text>();
            txtTitle.text = "PRIVATE MESSAGES";
            txtTitle.font = uiMgr.GetSafeFont(11);
            txtTitle.fontSize = 11;
            txtTitle.color = new Color(0.9f, 0.75f, 0.55f, 1f);
            txtTitle.fontStyle = FontStyle.Bold;
            txtTitle.alignment = TextAnchor.MiddleCenter;
            AddLocalTextShadow(txtTitle);

            // Envelope Icon (Chat Bubble)
            var envelopeObj = new GameObject("EnvelopeIcon", typeof(RectTransform));
            envelopeObj.transform.SetParent(dragBar.transform, false);
            var rtEnv = envelopeObj.GetComponent<RectTransform>();
            rtEnv.anchorMin = new Vector2(0f, 0.5f);
            rtEnv.anchorMax = new Vector2(0f, 0.5f);
            rtEnv.pivot = new Vector2(0f, 0.5f);
            rtEnv.anchoredPosition = new Vector2(10f, 0f);
            rtEnv.sizeDelta = new Vector2(18f, 18f);
            var envImg = envelopeObj.AddComponent<Image>();
            envImg.sprite = Resources.Load<Sprite>("UI/chat-bubble");
            envImg.color = new Color(0.9f, 0.75f, 0.55f, 1f);

            // Exit Button
            var exitObj = new GameObject("btn_exit", typeof(RectTransform));
            exitObj.transform.SetParent(dragBar.transform, false);
            var rtExit = exitObj.GetComponent<RectTransform>();
            rtExit.anchorMin = new Vector2(1f, 0.5f);
            rtExit.anchorMax = new Vector2(1f, 0.5f);
            rtExit.pivot = new Vector2(1f, 0.5f);
            rtExit.anchoredPosition = new Vector2(-4f, 0f);
            rtExit.sizeDelta = new Vector2(18f, 18f);
            _btnExit = exitObj.AddComponent<Button>();
            var exitImg = exitObj.AddComponent<Image>();
            exitImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_dir_exit", 18, 18, 0,
                new Color(0.18f, 0.18f, 0.18f, 1f),
                new Color(0.45f, 0.35f, 0.15f, 1f),
                1);
            var txtExitObj = new GameObject("Text");
            txtExitObj.transform.SetParent(exitObj.transform, false);
            var txtExit = txtExitObj.AddComponent<Text>();
            txtExit.text = "\uEAEC"; // ra-x-mark
            txtExit.font = uiMgr.GetRPGAwesomeFont();
            txtExit.fontSize = 12;
            txtExit.color = new Color(0.9f, 0.8f, 0.6f, 1f);
            txtExit.alignment = TextAnchor.MiddleCenter;
            txtExit.rectTransform.anchorMin = Vector2.zero;
            txtExit.rectTransform.anchorMax = Vector2.one;
            txtExit.rectTransform.offsetMin = Vector2.zero;
            txtExit.rectTransform.offsetMax = Vector2.zero;

            _btnExit.onClick.AddListener(() => {
                gameObject.SetActive(false);
            });
            exitObj.AddComponent<UIButtonScaleFeedback>();

            // 3. Scroll Area for conversation list
            var scrollObj = new GameObject("ScrollArea", typeof(RectTransform));
            scrollObj.transform.SetParent(transform, false);
            var rtScroll = scrollObj.GetComponent<RectTransform>();
            rtScroll.anchorMin = new Vector2(0f, 0f);
            rtScroll.anchorMax = new Vector2(1f, 1f);
            rtScroll.offsetMin = new Vector2(10f, 32f);
            rtScroll.offsetMax = new Vector2(-10f, -34f); // adjusted for 30px bar + 2px top gap + 2px padding

            var scrollImg = scrollObj.AddComponent<Image>();
            scrollImg.sprite = null;
            scrollImg.color = new Color(0.04f, 0.03f, 0.02f, 0.6f);
            scrollImg.raycastTarget = true;

            var viewportObj = new GameObject("Viewport", typeof(RectTransform));
            viewportObj.transform.SetParent(scrollObj.transform, false);
            var rtViewport = viewportObj.GetComponent<RectTransform>();
            rtViewport.anchorMin = Vector2.zero;
            rtViewport.anchorMax = Vector2.one;
            rtViewport.offsetMin = Vector2.zero;
            rtViewport.offsetMax = Vector2.zero;
            viewportObj.AddComponent<RectMask2D>();

            var contentObj = new GameObject("Content", typeof(RectTransform));
            contentObj.transform.SetParent(viewportObj.transform, false);
            _scrollContent = contentObj.transform;
            var rtContent = contentObj.GetComponent<RectTransform>();
            rtContent.anchorMin = new Vector2(0f, 1f);
            rtContent.anchorMax = new Vector2(1f, 1f);
            rtContent.pivot = new Vector2(0.5f, 1f);
            rtContent.anchoredPosition = Vector2.zero;
            rtContent.sizeDelta = new Vector2(0f, 0f);

            var contentVlg = contentObj.AddComponent<VerticalLayoutGroup>();
            contentVlg.spacing = 4f;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;
            contentVlg.padding = new RectOffset(4, 4, 4, 4);

            var contentCsf = contentObj.AddComponent<ContentSizeFitter>();
            contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.viewport = rtViewport;
            scrollRect.content = rtContent;

            // 4. Footer Buttons
            // Remove All Message
            var removeAllObj = new GameObject("btn_remove_all", typeof(RectTransform));
            removeAllObj.transform.SetParent(transform, false);
            var rtRemove = removeAllObj.GetComponent<RectTransform>();
            rtRemove.anchorMin = new Vector2(0f, 0f);
            rtRemove.anchorMax = new Vector2(0f, 0f);
            rtRemove.pivot = new Vector2(0f, 0f);
            rtRemove.anchoredPosition = new Vector2(8f, 6f);
            rtRemove.sizeDelta = new Vector2(120f, 20f);
            _btnRemoveAll = removeAllObj.AddComponent<Button>();
            var remImg = removeAllObj.AddComponent<Image>();
            remImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_dir_rem_all", 120, 20, 4,
                new Color(0.2f, 0.08f, 0.08f, 1f),
                new Color(0.6f, 0.2f, 0.2f, 1f),
                1);
            var txtRemObj = new GameObject("Text");
            txtRemObj.transform.SetParent(removeAllObj.transform, false);
            var txtRem = txtRemObj.AddComponent<Text>();
            txtRem.text = "Remove All Message";
            txtRem.font = uiMgr.GetSafeFont(9);
            txtRem.fontSize = 9;
            txtRem.fontStyle = FontStyle.Bold;
            txtRem.color = new Color(0.95f, 0.9f, 0.9f, 1f);
            txtRem.alignment = TextAnchor.MiddleCenter;
            txtRem.rectTransform.anchorMin = Vector2.zero;
            txtRem.rectTransform.anchorMax = Vector2.one;
            txtRem.rectTransform.offsetMin = Vector2.zero;
            txtRem.rectTransform.offsetMax = Vector2.zero;
            _btnRemoveAll.onClick.AddListener(() => {
                KOWhisperManager.Instance?.RemoveAllConversations();
            });
            removeAllObj.AddComponent<UIButtonScaleFeedback>();

            // PM Active
            var pmActiveObj = new GameObject("btn_pm_active", typeof(RectTransform));
            pmActiveObj.transform.SetParent(transform, false);
            var rtActive = pmActiveObj.GetComponent<RectTransform>();
            rtActive.anchorMin = new Vector2(1f, 0f);
            rtActive.anchorMax = new Vector2(1f, 0f);
            rtActive.pivot = new Vector2(1f, 0f);
            rtActive.anchoredPosition = new Vector2(-8f, 6f);
            rtActive.sizeDelta = new Vector2(120f, 20f);
            _btnPmActive = pmActiveObj.AddComponent<Button>();
            var actImg = pmActiveObj.AddComponent<Image>();
            actImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_dir_active", 120, 20, 4,
                new Color(0.12f, 0.10f, 0.08f, 1f),
                new Color(0.6f, 0.48f, 0.22f, 1f),
                1);
            var txtActObj = new GameObject("Text");
            txtActObj.transform.SetParent(pmActiveObj.transform, false);
            var txtAct = txtActObj.AddComponent<Text>();
            txtAct.text = "PM ACTIVE";
            txtAct.font = uiMgr.GetSafeFont(9);
            txtAct.fontSize = 9;
            txtAct.fontStyle = FontStyle.Bold;
            txtAct.color = new Color(0.9f, 0.75f, 0.55f, 1f);
            txtAct.alignment = TextAnchor.MiddleCenter;
            txtAct.rectTransform.anchorMin = Vector2.zero;
            txtAct.rectTransform.anchorMax = Vector2.one;
            txtAct.rectTransform.offsetMin = Vector2.zero;
            txtAct.rectTransform.offsetMax = Vector2.zero;
            AddLocalTextShadow(txtAct);
            pmActiveObj.AddComponent<UIButtonScaleFeedback>();

            ApplyModernTheme();
        }

        private void ApplyModernTheme()
        {
            var uiMgr = KOUIManager.Instance;
            if (uiMgr == null) return;

            // 1. Root Background
            var bgImg = GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.sprite = uiMgr.GetSkillThemePanelBgSprite("whisper_dir_bg_180", 304, 180, 0,
                    new Color(0.12f, 0.10f, 0.08f, 0.98f),
                    new Color(0.04f, 0.04f, 0.04f, 0.98f),
                    new Color(0.6f, 0.48f, 0.22f, 0.9f),
                    2);
                bgImg.color = Color.white;
            }

            // 2. Drag Bar Image
            var dragBarTrans = transform.Find("DragBar");
            if (dragBarTrans != null)
            {
                var barImg = dragBarTrans.GetComponent<Image>();
                if (barImg != null)
                {
                    barImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_dir_bar_30", 300, 30, 0,
                        new Color(0.18f, 0.15f, 0.12f, 0.95f),
                        new Color(0.45f, 0.35f, 0.15f, 0.9f),
                        1);
                }

                if (dragBarTrans.gameObject.GetComponent<UIDragHandler>() == null)
                {
                    dragBarTrans.gameObject.AddComponent<UIDragHandler>();
                }
            }

            // 3. Exit Button Image & Listeners & Feedback
            if (_btnExit != null)
            {
                var exitImg = _btnExit.GetComponent<Image>();
                if (exitImg != null)
                {
                    exitImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_dir_exit", 18, 18, 0,
                        new Color(0.18f, 0.18f, 0.18f, 1f),
                        new Color(0.45f, 0.35f, 0.15f, 1f),
                        1);
                }

                _btnExit.onClick.RemoveAllListeners();
                _btnExit.onClick.AddListener(() => {
                    gameObject.SetActive(false);
                });

                if (_btnExit.gameObject.GetComponent<UIButtonScaleFeedback>() == null)
                {
                    _btnExit.gameObject.AddComponent<UIButtonScaleFeedback>();
                }
            }

            // 4. Remove All Button Image & Listeners & Feedback
            if (_btnRemoveAll != null)
            {
                var remImg = _btnRemoveAll.GetComponent<Image>();
                if (remImg != null)
                {
                    remImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_dir_rem_all", 120, 20, 4,
                        new Color(0.2f, 0.08f, 0.08f, 1f),
                        new Color(0.6f, 0.2f, 0.2f, 1f),
                        1);
                }

                _btnRemoveAll.onClick.RemoveAllListeners();
                _btnRemoveAll.onClick.AddListener(() => {
                    KOWhisperManager.Instance?.RemoveAllConversations();
                });

                if (_btnRemoveAll.gameObject.GetComponent<UIButtonScaleFeedback>() == null)
                {
                    _btnRemoveAll.gameObject.AddComponent<UIButtonScaleFeedback>();
                }
            }

            // 5. PM Active Button Image & Listeners & Feedback
            if (_btnPmActive != null)
            {
                var actImg = _btnPmActive.GetComponent<Image>();
                if (actImg != null)
                {
                    actImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_dir_active", 120, 20, 4,
                        new Color(0.12f, 0.10f, 0.08f, 1f),
                        new Color(0.6f, 0.48f, 0.22f, 1f),
                        1);
                }

                if (_btnPmActive.gameObject.GetComponent<UIButtonScaleFeedback>() == null)
                {
                    _btnPmActive.gameObject.AddComponent<UIButtonScaleFeedback>();
                }
            }

            // 6. Scale Independent Check
            if (gameObject.GetComponent<KOUIScaleIndependent>() == null)
            {
                gameObject.AddComponent<KOUIScaleIndependent>();
            }
        }

        public void RefreshList(Dictionary<string, WhisperConversation> conversations)
        {
            if (_scrollContent == null) return;

            // Clear existing rows
            foreach (Transform child in _scrollContent)
            {
                Destroy(child.gameObject);
            }

            var uiMgr = KOUIManager.Instance;
            if (uiMgr == null) return;

            foreach (var kvp in conversations)
            {
                var conv = kvp.Value;
                string pName = kvp.Key;

                var row = new GameObject($"Row_{pName}", typeof(RectTransform));
                row.transform.SetParent(_scrollContent, false);
                var rtRow = row.GetComponent<RectTransform>();
                rtRow.sizeDelta = new Vector2(0f, 28f);

                // Row background (hover/subtle tint)
                var rowImg = row.AddComponent<Image>();
                rowImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_dir_row", 280, 28, 4,
                    new Color(0.18f, 0.16f, 0.14f, 0.3f),
                    new Color(0.3f, 0.25f, 0.2f, 0.2f),
                    1);
                rowImg.color = Color.white;

                // Click row to open chat
                var rowBtn = row.AddComponent<Button>();
                rowBtn.onClick.AddListener(() => {
                    KOWhisperManager.Instance?.ShowWhisperWindow(pName);
                    gameObject.SetActive(false);
                });
                row.AddComponent<UIButtonScaleFeedback>();

                // Text: [Unread] [Lv.83]PlayerName
                var textObj = new GameObject("Text_Info", typeof(RectTransform));
                textObj.transform.SetParent(row.transform, false);
                var rtText = textObj.GetComponent<RectTransform>();
                rtText.anchorMin = Vector2.zero;
                rtText.anchorMax = Vector2.one;
                rtText.offsetMin = new Vector2(8f, 0f);
                rtText.offsetMax = new Vector2(-54f, 0f);

                var txt = textObj.AddComponent<Text>();
                txt.text = $"[{conv.UnreadCount}] [Lv.{conv.PlayerLevel}]{pName}";
                txt.font = uiMgr.GetSafeFont(14);
                txt.fontSize = 14;
                txt.color = conv.UnreadCount > 0 ? new Color(1f, 0.85f, 0.4f, 1f) : new Color(0.9f, 0.85f, 0.8f, 1f);
                txt.alignment = TextAnchor.MiddleLeft;
                txt.fontStyle = conv.UnreadCount > 0 ? FontStyle.Bold : FontStyle.Normal;
                AddLocalTextShadow(txt);

                // Delete Button "X"
                var delObj = new GameObject("btn_delete", typeof(RectTransform));
                delObj.transform.SetParent(row.transform, false);
                var rtDel = delObj.GetComponent<RectTransform>();
                rtDel.anchorMin = new Vector2(1f, 0.5f);
                rtDel.anchorMax = new Vector2(1f, 0.5f);
                rtDel.pivot = new Vector2(1f, 0.5f);
                rtDel.anchoredPosition = new Vector2(-28f, 0f);
                rtDel.sizeDelta = new Vector2(18f, 18f);

                var delBtn = delObj.AddComponent<Button>();
                var delImg = delObj.AddComponent<Image>();
                delImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_dir_row_del", 18, 18, 2,
                    new Color(0.2f, 0.15f, 0.15f, 1f),
                    new Color(0.5f, 0.3f, 0.3f, 1f),
                    1);
                var txtDelObj = new GameObject("Text");
                txtDelObj.transform.SetParent(delObj.transform, false);
                var txtDel = txtDelObj.AddComponent<Text>();
                txtDel.text = "X";
                txtDel.font = uiMgr.GetSafeFont(10);
                txtDel.fontSize = 10;
                txtDel.fontStyle = FontStyle.Bold;
                txtDel.color = new Color(0.9f, 0.7f, 0.7f, 1f);
                txtDel.alignment = TextAnchor.MiddleCenter;
                txtDel.rectTransform.anchorMin = Vector2.zero;
                txtDel.rectTransform.anchorMax = Vector2.one;
                txtDel.rectTransform.offsetMin = Vector2.zero;
                txtDel.rectTransform.offsetMax = Vector2.zero;

                delBtn.onClick.AddListener(() => {
                    KOWhisperManager.Instance?.CloseWindow(pName);
                });
                delObj.AddComponent<UIButtonScaleFeedback>();

                // Open Button "≡"
                var openObj = new GameObject("btn_open", typeof(RectTransform));
                openObj.transform.SetParent(row.transform, false);
                var rtOpen = openObj.GetComponent<RectTransform>();
                rtOpen.anchorMin = new Vector2(1f, 0.5f);
                rtOpen.anchorMax = new Vector2(1f, 0.5f);
                rtOpen.pivot = new Vector2(1f, 0.5f);
                rtOpen.anchoredPosition = new Vector2(-6f, 0f);
                rtOpen.sizeDelta = new Vector2(18f, 18f);

                var openBtn = openObj.AddComponent<Button>();
                var openImg = openObj.AddComponent<Image>();
                openImg.sprite = uiMgr.GetSkillThemeRoundedRectSprite("whisper_dir_row_open", 18, 18, 2,
                    new Color(0.15f, 0.15f, 0.15f, 1f),
                    new Color(0.45f, 0.35f, 0.15f, 1f),
                    1);
                var txtOpenObj = new GameObject("Text");
                txtOpenObj.transform.SetParent(openObj.transform, false);
                var txtOpen = txtOpenObj.AddComponent<Text>();
                txtOpen.text = "≡";
                txtOpen.font = uiMgr.GetSafeFont(10);
                txtOpen.fontSize = 10;
                txtOpen.fontStyle = FontStyle.Bold;
                txtOpen.color = new Color(0.9f, 0.8f, 0.6f, 1f);
                txtOpen.alignment = TextAnchor.MiddleCenter;
                txtOpen.rectTransform.anchorMin = Vector2.zero;
                txtOpen.rectTransform.anchorMax = Vector2.one;
                txtOpen.rectTransform.offsetMin = Vector2.zero;
                txtOpen.rectTransform.offsetMax = Vector2.zero;

                openBtn.onClick.AddListener(() => {
                    KOWhisperManager.Instance?.ShowWhisperWindow(pName);
                    gameObject.SetActive(false);
                });
                openObj.AddComponent<UIButtonScaleFeedback>();
            }
        }

        private void AddLocalTextShadow(Text txt)
        {
            if (txt == null || txt.gameObject.GetComponent<Shadow>() != null) return;
            var shadow = txt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private Sprite CreateEnvelopeSprite()
        {
            Texture2D tex = new Texture2D(16, 12, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            
            Color white = Color.white;
            Color transparent = Color.clear;

            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    if (x == 0 || x == 15 || y == 0 || y == 11)
                    {
                        tex.SetPixel(x, y, white);
                    }
                    else if (y == 10 - x || y == 10 - (15 - x))
                    {
                        tex.SetPixel(x, y, white);
                    }
                    else
                    {
                        tex.SetPixel(x, y, transparent);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 16, 12), new Vector2(0.5f, 0.5f));
        }
    }
}
