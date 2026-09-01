using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EntropyOnline.UI
{
    public class GameOptionsUI : MonoBehaviour
    {
        private Transform _sidebarContainer;
        private Transform _contentContainer;
        private Button _btnClose;
        private Button _btnReset;

        private List<Button> _tabButtons = new List<Button>();
        private List<GameObject> _pages = new List<GameObject>();
        private List<Transform> _pageContentContainers = new List<Transform>();
        private int _activePageIndex = 0;

        private string[] _tabNames = new string[]
        {
            "Looting Option",
            "Cospre Option",
            "Effect Option",
            "Graphic Option",
            "Graphic Option2",
            "Graphic Option3",
            "Block Options",
            "PK Zone Option",
            "Sound Option",
            "PM Block",
            "Hide Option",
            "Mod Option",
            "Language settings"
        };

        // Auction House Colors
        private Color _colorBg = new Color(0.1f, 0.07f, 0.06f, 0.95f);       // Dark brown charcoal
        private Color _colorBorder = new Color(0.75f, 0.63f, 0.38f, 1f);     // Gold/bronze border
        private Color _colorTextGold = new Color(0.9f, 0.8f, 0.6f, 1f);      // Golden text
        private Color _colorBtnNormal = new Color(0.18f, 0.12f, 0.10f, 1f);   // Sidebar button normal
        private Color _colorBtnActive = new Color(0.48f, 0.38f, 0.22f, 1f);   // Sidebar button active
        private Color _colorInputBg = new Color(0.05f, 0.04f, 0.04f, 1f);    // Inner content dark input bg
        private Color _colorCheckboxActive = new Color(0.75f, 0.63f, 0.38f, 1f); // Checkbox tick color

        private void Awake()
        {
            // Try to find components if this is instantiated from a prefab
            _sidebarContainer = transform.Find("Sidebar");
            _contentContainer = transform.Find("ContentArea");
            _btnClose = transform.Find("Header/btn_close")?.GetComponent<Button>();
            _btnReset = transform.Find("btn_reset")?.GetComponent<Button>();

            // If it's a blank GameObject (no prefab loaded or missing references), build the UI dynamically!
            if (_sidebarContainer == null || _contentContainer == null)
            {
                BuildUIDynamically();
            }
            else
            {
                SetupPrefabBindings();
                ApplyThemeToPrefab();
            }
        }

        private void OnEnable()
        {
            RefreshAllValues();
            RefreshPrefabUIValues();
            KOUIManager.Instance?.RepositionSkillBarForPanel();
        }

        private void OnDisable()
        {
            KOUIManager.Instance?.RepositionSkillBarForPanel();
        }

        private void SetupPrefabBindings()
        {
            if (_btnClose != null) _btnClose.onClick.AddListener(CloseMenu);
            if (_btnReset != null) _btnReset.onClick.AddListener(ResetSettings);

            // Bind tabs and pages from prefab
            for (int i = 0; i < _tabNames.Length; i++)
            {
                int idx = i;
                string tabPath = $"Sidebar/Scroll/Viewport/Content/Tab_{idx}";
                string pagePath = $"ContentArea/Page_{idx}";

                var tabTrans = transform.Find(tabPath);
                var pageTrans = transform.Find(pagePath);

                if (tabTrans != null)
                {
                    var btn = tabTrans.GetComponent<Button>();
                    if (btn != null)
                    {
                        btn.onClick.AddListener(() => SwitchToTab(idx));
                        _tabButtons.Add(btn);
                    }
                }

                if (pageTrans != null)
                {
                    _pages.Add(pageTrans.gameObject);
                }
            }

            BindPrefabControls();
            SwitchToTab(0);
        }

        private void BindPrefabControls()
        {
            // Bind page 0: Looting
            BindPrefabToggle("ContentArea/Page_0/toggle_low_class", (val) => GameOptionsManager.Instance.Loot_LowClass = val);
            BindPrefabToggle("ContentArea/Page_0/toggle_middle_class", (val) => GameOptionsManager.Instance.Loot_MiddleClass = val);
            BindPrefabToggle("ContentArea/Page_0/toggle_high_class", (val) => GameOptionsManager.Instance.Loot_HighClass = val);
            BindPrefabToggle("ContentArea/Page_0/toggle_potion", (val) => GameOptionsManager.Instance.Loot_Potion = val);
            BindPrefabInputField("ContentArea/Page_0/input_sell_price", (val) => {
                if (int.TryParse(val, out int price)) GameOptionsManager.Instance.Loot_SellPrice = price;
            });

            // Bind page 1: Cospre
            BindPrefabToggle("ContentArea/Page_1/toggle_wing_me", (val) => GameOptionsManager.Instance.Cospre_HideWing_Me = val);
            BindPrefabToggle("ContentArea/Page_1/toggle_fairy_me", (val) => GameOptionsManager.Instance.Cospre_HideFairy_Me = val);
            BindPrefabToggle("ContentArea/Page_1/toggle_costume_me", (val) => GameOptionsManager.Instance.Cospre_HideCostumeArmor_Me = val);
            BindPrefabToggle("ContentArea/Page_1/toggle_wing_others", (val) => GameOptionsManager.Instance.Cospre_HideWing_Others = val);
            BindPrefabToggle("ContentArea/Page_1/toggle_gloves_others", (val) => GameOptionsManager.Instance.Cospre_HideGloves_Others = val);
            BindPrefabToggle("ContentArea/Page_1/toggle_fairy_others", (val) => GameOptionsManager.Instance.Cospre_HideFairy_Others = val);
            BindPrefabToggle("ContentArea/Page_1/toggle_costume_others", (val) => GameOptionsManager.Instance.Cospre_HideAllCostumes_Others = val);

            // Bind page 2: Effect
            BindPrefabToggle("ContentArea/Page_2/toggle_hide_all_players", (val) => GameOptionsManager.Instance.Effect_HideAllPlayers = val);
            BindPrefabToggle("ContentArea/Page_2/toggle_hide_minor", (val) => GameOptionsManager.Instance.Effect_HideMinorFX = val);
            BindPrefabToggle("ContentArea/Page_2/toggle_hide_heal", (val) => GameOptionsManager.Instance.Effect_HideHealFX = val);
            BindPrefabToggle("ContentArea/Page_2/toggle_hide_weapon", (val) => GameOptionsManager.Instance.Effect_HideWeaponFX = val);
            BindPrefabToggle("ContentArea/Page_2/toggle_hide_monster", (val) => GameOptionsManager.Instance.Effect_HideMonsterFX = val);
            BindPrefabToggle("ContentArea/Page_2/toggle_hide_target", (val) => GameOptionsManager.Instance.Effect_HideTargetFX = val);
            BindPrefabToggle("ContentArea/Page_2/toggle_hide_trail", (val) => GameOptionsManager.Instance.Effect_HideHandTrailFX = val);
            BindPrefabToggle("ContentArea/Page_2/toggle_hide_cape", (val) => { GameOptionsManager.Instance.Effect_HideCapeFX = val; RefreshCapesVisibility(); });
            BindPrefabToggle("ContentArea/Page_2/toggle_hide_cast", (val) => GameOptionsManager.Instance.Effect_HideCastFX = val);
            BindPrefabToggle("ContentArea/Page_2/toggle_hide_nova", (val) => GameOptionsManager.Instance.Effect_HideNovaFX = val);
            BindPrefabSlider("ContentArea/Page_2/slider_camera_shake", (val) => GameOptionsManager.Instance.Effect_CameraShakeStrength = val);

            // Bind page 3: Graphic
            BindPrefabSelector("selector_fps", new string[] { "30", "60", "90", "120" }, () => GameOptionsManager.Instance.Graphic_FPS.ToString(), (val) => {
                if (int.TryParse(val, out int fps)) GameOptionsManager.Instance.Graphic_FPS = fps;
            });
            BindPrefabZoomSelector("selector_zoom");
            BindPrefabSelector("selector_texture", new string[] { "Low", "Medium", "High" }, () => {
                int q = GameOptionsManager.Instance.Graphic_TextureQuality;
                return q == 2 ? "Low" : q == 1 ? "Medium" : "High";
            }, (val) => {
                int q = val == "Low" ? 2 : val == "Medium" ? 1 : 0;
                GameOptionsManager.Instance.Graphic_TextureQuality = q;
            });
            BindPrefabSlider("ContentArea/Page_3/slider_camera_far", (val) => GameOptionsManager.Instance.Graphic_CameraFar = val);
            BindPrefabSlider("ContentArea/Page_3/slider_quality", (val) => GameOptionsManager.Instance.Graphic_Quality = val);

            // Bind page 4: Graphic2
            BindPrefabSlider("ContentArea/Page_4/slider_skill_area", (val) => GameOptionsManager.Instance.Graphic2_SkillAreaSens = val);
            BindPrefabSlider("ContentArea/Page_4/slider_camera_sens", (val) => GameOptionsManager.Instance.Graphic2_CameraSens = val);
            BindPrefabSlider("ContentArea/Page_4/slider_z_size", (val) => { GameOptionsManager.Instance.Graphic2_ZButtonSize = val; MobileSkillBar.Instance?.ApplyZButtonScale(); });
            BindPrefabSlider("ContentArea/Page_4/slider_party_scale", (val) => { GameOptionsManager.Instance.Graphic2_PartyUIScale = val; KOUIManager.Instance?.ApplyUIScalingAndMargins(); });
            BindPrefabSlider("ContentArea/Page_4/slider_skill_bar", (val) => { GameOptionsManager.Instance.Graphic2_SkillBarSize = val; KOUIManager.Instance?.ApplyUIScalingAndMargins(); });

            // Bind page 5: Graphic3
            BindPrefabSlider("ContentArea/Page_5/slider_exposure", (val) => { GameOptionsManager.Instance.Graphic3_PostExposure = val; KOUIManager.Instance?.ApplyPostProcessingSettings(); });
            BindPrefabSlider("ContentArea/Page_5/slider_contrast", (val) => { GameOptionsManager.Instance.Graphic3_Contrast = val; KOUIManager.Instance?.ApplyPostProcessingSettings(); });
            BindPrefabSlider("ContentArea/Page_5/slider_ui_scale", (val) => { GameOptionsManager.Instance.Graphic3_UIScale = val; KOUIManager.Instance?.ApplyUIScalingAndMargins(); });

            // Bind page 6: Block
            BindPrefabToggle("ContentArea/Page_6/toggle_block_party", (val) => GameOptionsManager.Instance.Block_PartyRequests = val);
            BindPrefabToggle("ContentArea/Page_6/toggle_block_trade", (val) => GameOptionsManager.Instance.Block_TradeRequests = val);

            // Bind page 7: PK
            BindPrefabToggle("ContentArea/Page_7/toggle_priority_player", (val) => {
                GameOptionsManager.Instance.PK_TargetPriorityPlayer = val;
                if (val)
                {
                    GameOptionsManager.Instance.PK_TargetPriorityMonster = false;
                    SetToggleValueSafe("toggle_priority_monster", false);
                }
            });
            BindPrefabToggle("ContentArea/Page_7/toggle_priority_monster", (val) => {
                GameOptionsManager.Instance.PK_TargetPriorityMonster = val;
                if (val)
                {
                    GameOptionsManager.Instance.PK_TargetPriorityPlayer = false;
                    SetToggleValueSafe("toggle_priority_player", false);
                }
            });
            BindPrefabToggle("ContentArea/Page_7/toggle_z_fix", (val) => GameOptionsManager.Instance.PK_ZFix = val);
            BindPrefabToggle("ContentArea/Page_7/toggle_show_hud_bars", (val) => GameOptionsManager.Instance.PK_ShowHpMpBarHud = val);
            BindPrefabToggle("ContentArea/Page_7/toggle_combo_helper", (val) => GameOptionsManager.Instance.PK_ComboHelper = val);
            BindPrefabToggle("ContentArea/Page_7/toggle_change_z_r", (val) => {
                GameOptionsManager.Instance.PK_ChangeZAndR = val;
                MobileSkillBar.Instance?.ApplyZAndRLayout();
            });
            BindPrefabSelector("selector_extra_skills", new string[] { "0", "1", "2", "3", "4", "5", "6" }, () => GameOptionsManager.Instance.PK_ExtraSkillCount.ToString(), (val) => {
                if (int.TryParse(val, out int count)) {
                    GameOptionsManager.Instance.PK_ExtraSkillCount = count;
                    MobileSkillBar.Instance?.ApplyExtraSkillsActiveState();
                }
            });
            BindPrefabButton("btn_move_skill_slots", () => {
                if (MobileSkillBar.Instance == null) return;
                bool newMode = !MobileSkillBar.Instance.IsEditMode;
                MobileSkillBar.Instance.SetEditMode(newMode);
                if (!newMode)
                {
                    MobileSkillBar.Instance.SaveSavedPositions();
                }
                UpdateMoveButtonUI(newMode);
            });
            BindPrefabButton("btn_reset_skill_slots", () => {
                if (MobileSkillBar.Instance != null)
                {
                    MobileSkillBar.Instance.ResetPositions();
                }
                GameOptionsManager.Instance.PK_ExtraSkillCount = 0;
                MobileSkillBar.Instance?.ApplyExtraSkillsActiveState();
                GameOptionsManager.Instance.SaveSettings();
                RefreshAllValues();
                RefreshPrefabUIValues();
            });

            // Bind page 8: Sound
            BindPrefabToggle("ContentArea/Page_8/toggle_mute_all", (val) => GameOptionsManager.Instance.Sound_MuteAll = val);
            BindPrefabToggle("ContentArea/Page_8/toggle_mute_walk", (val) => GameOptionsManager.Instance.Sound_MuteWalk = val);
            BindPrefabSlider("ContentArea/Page_8/slider_bg_sound", (val) => GameOptionsManager.Instance.Sound_Background = val);
            BindPrefabSlider("ContentArea/Page_8/slider_skill_sound", (val) => GameOptionsManager.Instance.Sound_Skill = val);

            // Bind page 10: Hide
            BindPrefabToggle("ContentArea/Page_10/toggle_hide_name", (val) => GameOptionsManager.Instance.Hide_NamePlates = val);
            BindPrefabToggle("ContentArea/Page_10/toggle_hide_friends_name", (val) => GameOptionsManager.Instance.Hide_FriendsNamePlates = val);
            BindPrefabToggle("ContentArea/Page_10/toggle_hide_capes", (val) => { GameOptionsManager.Instance.Hide_AllCapes = val; RefreshCapesVisibility(); });
            BindPrefabToggle("ContentArea/Page_10/toggle_hide_wing", (val) => GameOptionsManager.Instance.Hide_WingMe = val);
            BindPrefabToggle("ContentArea/Page_10/toggle_hide_target", (val) => GameOptionsManager.Instance.Hide_TargetMark = val);
            BindPrefabToggle("ContentArea/Page_10/toggle_hide_leader", (val) => GameOptionsManager.Instance.Hide_LeaderMark = val);
            BindPrefabToggle("ContentArea/Page_10/toggle_hide_shadow", (val) => GameOptionsManager.Instance.Hide_PlayerShadow = val);
            BindPrefabToggle("ContentArea/Page_10/toggle_hide_kill_anim", (val) => GameOptionsManager.Instance.Hide_UIKillAnim = val);
            BindPrefabToggle("ContentArea/Page_10/toggle_hide_red_screen", (val) => GameOptionsManager.Instance.Hide_RedHitScreen = val);
            BindPrefabToggle("ContentArea/Page_10/toggle_hide_gray_screen", (val) => GameOptionsManager.Instance.Hide_GrayScreen = val);
            BindPrefabToggle("ContentArea/Page_10/toggle_damage_text", (val) => GameOptionsManager.Instance.Hide_DamageTextActive = val);

            // Bind page 11: Mod Option
            BindPrefabButton("btn_accept_agreement", () => {
                GameOptionsManager.Instance.Mod_DLCAccepted = true;
                GameOptionsManager.Instance.SaveSettings();
            });

            // Bind page 12: Language
            BindPrefabLanguageButtons("LangSelect_Game", true);
            BindPrefabLanguageButtons("LangSelect_Notice", false);

            RefreshPrefabUIValues();
        }

        private void BindPrefabSelector(string goName, string[] options, Func<string> getCurrentVal, Action<string> action)
        {
            var selectorGo = FindGameObjectInChildren(gameObject, goName);
            if (selectorGo == null) return;

            var leftBtnTrans = selectorGo.transform.Find("btn_left");
            var rightBtnTrans = selectorGo.transform.Find("btn_right");
            var textTrans = selectorGo.transform.Find("Text") ?? selectorGo.transform.Find("TextMeshProUGUI");

            var lBtn = leftBtnTrans?.GetComponent<Button>();
            var rBtn = rightBtnTrans?.GetComponent<Button>();
            var txt = textTrans?.GetComponent<TextMeshProUGUI>();

            if (lBtn == null || rBtn == null || txt == null) return;

            lBtn.onClick.RemoveAllListeners();
            rBtn.onClick.RemoveAllListeners();

            Action updateVal = () => {
                txt.text = getCurrentVal();
            };

            lBtn.onClick.AddListener(() => {
                string cur = getCurrentVal();
                int idx = Array.IndexOf(options, cur);
                if (idx > 0)
                {
                    action(options[idx - 1]);
                    updateVal();
                    GameOptionsManager.Instance.SaveSettings();
                }
            });

            rBtn.onClick.AddListener(() => {
                string cur = getCurrentVal();
                int idx = Array.IndexOf(options, cur);
                if (idx >= 0 && idx < options.Length - 1)
                {
                    action(options[idx + 1]);
                    updateVal();
                    GameOptionsManager.Instance.SaveSettings();
                }
            });

            updateVal();
        }

        private void BindPrefabZoomSelector(string goName)
        {
            var selectorGo = FindGameObjectInChildren(gameObject, goName);
            if (selectorGo == null) return;

            var leftBtnTrans = selectorGo.transform.Find("btn_left");
            var rightBtnTrans = selectorGo.transform.Find("btn_right");
            var textTrans = selectorGo.transform.Find("Text") ?? selectorGo.transform.Find("TextMeshProUGUI");

            var lBtn = leftBtnTrans?.GetComponent<Button>();
            var rBtn = rightBtnTrans?.GetComponent<Button>();
            var txt = textTrans?.GetComponent<TextMeshProUGUI>();

            if (lBtn == null || rBtn == null || txt == null) return;

            lBtn.onClick.RemoveAllListeners();
            rBtn.onClick.RemoveAllListeners();

            Action updateVal = () => {
                txt.text = GameOptionsManager.Instance.Graphic_CameraZoom.ToString();
            };

            lBtn.onClick.AddListener(() => {
                int current = GameOptionsManager.Instance.Graphic_CameraZoom;
                if (current > -10)
                {
                    GameOptionsManager.Instance.Graphic_CameraZoom = current - 1;
                    updateVal();

                    var cam = UnityEngine.Object.FindAnyObjectByType<EntropyOnline.Camera.CameraController>();
                    if (cam != null) cam.SetZoomLimit(current - 1);

                    GameOptionsManager.Instance.SaveSettings();
                }
            });

            rBtn.onClick.AddListener(() => {
                int current = GameOptionsManager.Instance.Graphic_CameraZoom;
                if (current < 10)
                {
                    GameOptionsManager.Instance.Graphic_CameraZoom = current + 1;
                    updateVal();

                    var cam = UnityEngine.Object.FindAnyObjectByType<EntropyOnline.Camera.CameraController>();
                    if (cam != null) cam.SetZoomLimit(current + 1);

                    GameOptionsManager.Instance.SaveSettings();
                }
            });

            updateVal();
        }

        private void BindPrefabLanguageButtons(string goName, bool isGameLang)
        {
            var selectGo = FindGameObjectInChildren(gameObject, goName);
            if (selectGo == null) return;

            for (int i = 0; i < 3; i++)
            {
                int langIdx = i;
                var btnTrans = selectGo.transform.Find($"LangBtn_{i}");
                var btn = btnTrans?.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => {
                        if (isGameLang)
                            GameOptionsManager.Instance.Lang_GameLanguage = langIdx;
                        else
                            GameOptionsManager.Instance.Lang_NoticeLanguage = langIdx;

                        GameOptionsManager.Instance.SaveSettings();
                        UpdateLanguageOutlines(selectGo.transform, isGameLang ? GameOptionsManager.Instance.Lang_GameLanguage : GameOptionsManager.Instance.Lang_NoticeLanguage);
                    });
                }
            }

            UpdateLanguageOutlines(selectGo.transform, isGameLang ? GameOptionsManager.Instance.Lang_GameLanguage : GameOptionsManager.Instance.Lang_NoticeLanguage);
        }

        private void BindPrefabButton(string goName, Action onClick)
        {
            var btn = FindComponentInChildren<Button>(goName);
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onClick());
            }
        }

        private T FindComponentInChildren<T>(string goName) where T : Component
        {
            var targetGo = FindGameObjectInChildren(gameObject, goName);
            if (targetGo != null)
            {
                return targetGo.GetComponent<T>() ?? targetGo.GetComponentInChildren<T>(true);
            }
            return null;
        }

        private GameObject FindGameObjectInChildren(GameObject parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent.transform)
            {
                var result = FindGameObjectInChildren(child.gameObject, name);
                if (result != null) return result;
            }
            return null;
        }

        private void BindPrefabToggle(string path, Action<bool> action)
        {
            string goName = path.Substring(path.LastIndexOf('/') + 1);
            var toggle = FindComponentInChildren<Toggle>(goName);
            if (toggle != null)
            {
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener((val) => {
                    action(val);
                    GameOptionsManager.Instance.SaveSettings();
                });
            }
        }

        private void BindPrefabSlider(string path, Action<float> action)
        {
            string goName = path.Substring(path.LastIndexOf('/') + 1);
            var slider = FindComponentInChildren<Slider>(goName);
            if (slider != null)
            {
                slider.onValueChanged.RemoveAllListeners();
                slider.onValueChanged.AddListener((val) => {
                    action(val);
                    GameOptionsManager.Instance.SaveSettings();
                });
            }
        }

        private void BindPrefabInputField(string path, Action<string> action)
        {
            string goName = path.Substring(path.LastIndexOf('/') + 1);
            var input = FindComponentInChildren<TMP_InputField>(goName);
            if (input != null)
            {
                input.onValueChanged.RemoveAllListeners();
                input.onValueChanged.AddListener((val) => {
                    action(val);
                    GameOptionsManager.Instance.SaveSettings();
                });
            }
        }

        // Helper method to create UI GameObjects directly with RectTransform to ensure correct parenting layout
        private GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            return go;
        }

        private void BuildUIDynamically()
        {
            // 1. Setup RectTransform for the whole window (anchored right center, compact height)
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-50f, 0f); // 50px padding from the right edge (like inventory)
            rt.sizeDelta = new Vector2(360f, 450f); // Enlarge width from 320f to 360f

            // Add background panel
            var mainImg = gameObject.GetComponent<Image>();
            if (mainImg == null) mainImg = gameObject.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                mainImg.sprite = KOUIManager.Instance.GetSkillThemePanelBgSprite(
                    "game_options_main_bg", 360, 450, 0,
                    new Color(0.12f, 0.10f, 0.08f, 0.98f), // Dark brown top
                    new Color(0.04f, 0.04f, 0.04f, 0.98f), // Black bottom
                    new Color(0.6f, 0.48f, 0.22f, 0.9f),   // Amber gold border
                    2
                );
                mainImg.color = Color.white;
            }
            else
            {
                mainImg.color = _colorBg;
            }

            // Add UI CanvasGroup for smooth fade or simply raycasting
            var cg = gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = true;

            // 2. Header
            var headerGO = CreateUIObject("Header", transform);
            var headerRT = headerGO.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.anchoredPosition = new Vector2(0, -5f);
            headerRT.sizeDelta = new Vector2(-20f, 30f);

            var titleTxt = CreateText(headerGO, "GAME OPTIONS", 16, TextAlignmentOptions.Center); // Enlarge title font to 16
            titleTxt.color = new Color(0.95f, 0.85f, 0.35f, 1.0f); // Premium gold
            var titleShadow = titleTxt.gameObject.GetComponent<Shadow>();
            if (titleShadow == null) titleShadow = titleTxt.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = Color.black;
            titleShadow.effectDistance = new Vector2(1f, -1f);

            var titleRT = titleTxt.GetComponent<RectTransform>();
            titleRT.anchorMin = Vector2.zero;
            titleRT.anchorMax = Vector2.one;
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            // Close button
            var closeGO = CreateUIObject("btn_close", headerGO.transform);
            var closeRT = closeGO.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1, 0.5f);
            closeRT.anchorMax = new Vector2(1, 0.5f);
            closeRT.pivot = new Vector2(1, 0.5f);
            closeRT.anchoredPosition = new Vector2(-5f, -2f);
            closeRT.sizeDelta = new Vector2(22f, 22f);

            var closeImg = closeGO.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                closeImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "game_options_close", 22, 22, 0, // Sharp square
                    new Color(0.6f, 0.1f, 0.1f, 1f), // Red color
                    Color.clear,
                    0
                );
                closeImg.color = Color.white;
            }
            else
            {
                closeImg.color = new Color(0.6f, 0.1f, 0.1f, 1f); // Red exit button
            }
            var closeTxt = CreateText(closeGO, "X", 14, TextAlignmentOptions.Center); // Enlarge close font to 14
            closeTxt.color = Color.white;
            _btnClose = closeGO.AddComponent<Button>();
            _btnClose.onClick.AddListener(CloseMenu);

            // 3. Sidebar (Left tab panel)
            var sidebarGO = CreateUIObject("Sidebar", transform);
            var sidebarRT = sidebarGO.GetComponent<RectTransform>();
            sidebarRT.anchorMin = new Vector2(0, 0.5f);
            sidebarRT.anchorMax = new Vector2(0, 0.5f);
            sidebarRT.pivot = new Vector2(0, 0.5f);
            sidebarRT.anchoredPosition = new Vector2(5f, -15f);
            sidebarRT.sizeDelta = new Vector2(115f, 380f); // Width enlarged to 115f

            var scrollGO = CreateUIObject("Scroll", sidebarGO.transform);
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;
            var scrollRect = scrollGO.AddComponent<ScrollRect>();

            var viewGO = CreateUIObject("Viewport", scrollGO.transform);
            var viewRT = viewGO.GetComponent<RectTransform>();
            viewRT.anchorMin = Vector2.zero;
            viewRT.anchorMax = Vector2.one;
            viewRT.offsetMin = Vector2.zero;
            viewRT.offsetMax = Vector2.zero;
            viewGO.AddComponent<RectMask2D>();

            var sideContentGO = CreateUIObject("Content", viewRT);
            var sideContentRT = sideContentGO.GetComponent<RectTransform>();
            sideContentRT.anchorMin = new Vector2(0, 1);
            sideContentRT.anchorMax = new Vector2(1, 1);
            sideContentRT.pivot = new Vector2(0.5f, 1);
            sideContentRT.anchoredPosition = Vector2.zero;
            sideContentRT.sizeDelta = new Vector2(0, 0);

            var sideLayout = sideContentGO.AddComponent<VerticalLayoutGroup>();
            sideLayout.spacing = 2;
            sideLayout.childForceExpandHeight = false;
            sideLayout.childControlHeight = false;
            sideLayout.childForceExpandWidth = true;
            sideLayout.childControlWidth = true;

            var sideFitter = sideContentGO.AddComponent<ContentSizeFitter>();
            sideFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = sideContentRT;
            scrollRect.viewport = viewRT;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            _sidebarContainer = sideContentRT.transform;

            // 4. Content Area (Right panel for pages)
            var contentGO = CreateUIObject("ContentArea", transform);
            var contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(1, 0.5f);
            contentRT.anchorMax = new Vector2(1, 0.5f);
            contentRT.pivot = new Vector2(1, 0.5f);
            contentRT.anchoredPosition = new Vector2(-5f, -15f);
            contentRT.sizeDelta = new Vector2(230f, 380f); // Width enlarged to 230f

            var contentImg = contentGO.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                contentImg.sprite = KOUIManager.Instance.GetSkillThemePanelBgSprite(
                    "game_options_content_bg", 230, 380, 0,
                    new Color(0.06f, 0.05f, 0.05f, 0.95f), // Very dark warm grey top
                    new Color(0.02f, 0.02f, 0.02f, 0.95f), // Near black bottom
                    new Color(0.6f, 0.48f, 0.22f, 0.4f),   // Subtle amber border
                    1
                );
                contentImg.color = Color.white;
            }
            else
            {
                contentImg.color = _colorInputBg;
            }

            _contentContainer = contentRT.transform;

            // 5. Generate sidebar tabs & page templates dynamically
            for (int i = 0; i < _tabNames.Length; i++)
            {
                int index = i;
                string tabName = _tabNames[i];

                // Create Tab button
                var tabBtnGO = CreateUIObject($"Tab_{index}", _sidebarContainer);
                var btnRT = tabBtnGO.GetComponent<RectTransform>();
                btnRT.sizeDelta = new Vector2(0, 30f); // Button height 30f

                var tabImg = tabBtnGO.AddComponent<Image>();
                if (KOUIManager.Instance != null)
                {
                    tabImg.sprite = KOUIManager.Instance.GetFadeGradientSprite(
                        "opt_sidebar_fade_115_30", 115, 30, Color.white, Color.clear, 0
                    );
                    tabImg.color = Color.clear;
                }
                else
                {
                    tabImg.color = Color.clear;
                }

                var btnText = CreateText(tabBtnGO, tabName, 11, TextAlignmentOptions.Center); // Enlarged font size to 11
                btnText.color = _colorTextGold;

                var btn = tabBtnGO.AddComponent<Button>();
                btn.onClick.AddListener(() => SwitchToTab(index));
                _tabButtons.Add(btn);

                // Add fading divider between buttons (except after the last button)
                if (i < _tabNames.Length - 1)
                {
                    var divGO = CreateUIObject($"Divider_{i}", _sidebarContainer);
                    var divRT = divGO.GetComponent<RectTransform>();
                    divRT.sizeDelta = new Vector2(95f, 1.5f); // 95px divider width
                    var divImg = divGO.AddComponent<Image>();
                    if (divImg != null && KOUIManager.Instance != null)
                    {
                        divImg.sprite = KOUIManager.Instance.GetSkillThemeFadingDividerSprite(
                            "opt_sidebar_divider_" + i, 95, 2, new Color(0.6f, 0.48f, 0.22f, 0.35f));
                        divImg.color = Color.white;
                    }
                }

                // Create Page GameObject
                var pageGO = CreateUIObject($"Page_{index}", _contentContainer);
                var pageRT = pageGO.GetComponent<RectTransform>();
                pageRT.anchorMin = Vector2.zero;
                pageRT.anchorMax = Vector2.one;
                pageRT.offsetMin = new Vector2(5f, 5f);
                pageRT.offsetMax = new Vector2(-5f, -5f);
                pageGO.SetActive(false);

                _pages.Add(pageGO);

                // Create ScrollRect inside each page to allow scrollable sub-options
                var subScrollGO = CreateUIObject("Scroll", pageGO.transform);
                var subScrollRT = subScrollGO.GetComponent<RectTransform>();
                subScrollRT.anchorMin = Vector2.zero;
                subScrollRT.anchorMax = Vector2.one;
                subScrollRT.offsetMin = Vector2.zero;
                subScrollRT.offsetMax = Vector2.zero;

                var subScrollRect = subScrollGO.AddComponent<ScrollRect>();
                subScrollRect.horizontal = false;
                subScrollRect.vertical = true;

                var subViewGO = CreateUIObject("Viewport", subScrollGO.transform);
                var subViewRT = subViewGO.GetComponent<RectTransform>();
                subViewRT.anchorMin = Vector2.zero;
                subViewRT.anchorMax = Vector2.one;
                subViewRT.offsetMin = Vector2.zero;
                subViewRT.offsetMax = Vector2.zero;
                subViewGO.AddComponent<RectMask2D>();
                subScrollRect.viewport = subViewRT;

                var subContentGO = CreateUIObject("Content", subViewRT);
                var subContentRT = subContentGO.GetComponent<RectTransform>();
                subContentRT.anchorMin = new Vector2(0, 1);
                subContentRT.anchorMax = new Vector2(1, 1);
                subContentRT.pivot = new Vector2(0.5f, 1);
                subContentRT.anchoredPosition = Vector2.zero;
                subContentRT.sizeDelta = new Vector2(0, 0);
                subScrollRect.content = subContentRT;

                _pageContentContainers.Add(subContentRT);

                BuildDynamicPageContent(index, subContentGO);
            }

            SwitchToTab(0);
        }

        private void BuildDynamicPageContent(int pageIndex, GameObject pageGO)
        {
            // Layout Group for page items - use GetComponent to prevent duplicate layout groups during refreshes
            var layout = pageGO.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = pageGO.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12; // Increase spacing to prevent cramped items
            layout.padding = new RectOffset(6, 6, 8, 8); // Symmetric margins inside content area
            layout.childControlHeight = false; // Fix: do not force height of elements to 0
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true; // Ensure children fill width symmetrically

            var fitter = pageGO.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = pageGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            switch (pageIndex)
            {
                case 0: // Looting
                    CreateHeading(pageGO, "Loot Class Settings");
                    CreateToggle(pageGO, "Low Class Items", GameOptionsManager.Instance.Loot_LowClass, (val) => GameOptionsManager.Instance.Loot_LowClass = val);
                    CreateToggle(pageGO, "Middle Class Items", GameOptionsManager.Instance.Loot_MiddleClass, (val) => GameOptionsManager.Instance.Loot_MiddleClass = val);
                    CreateToggle(pageGO, "High Class Items", GameOptionsManager.Instance.Loot_HighClass, (val) => GameOptionsManager.Instance.Loot_HighClass = val);
                    CreateToggle(pageGO, "Potion Items", GameOptionsManager.Instance.Loot_Potion, (val) => GameOptionsManager.Instance.Loot_Potion = val);
                    CreateHeading(pageGO, "SELL PRICE LIMIT");
                    CreateInputField(pageGO, GameOptionsManager.Instance.Loot_SellPrice.ToString(), (val) => {
                        if (int.TryParse(val, out int price)) GameOptionsManager.Instance.Loot_SellPrice = price;
                    }, "input_sell_price");
                    CreateHelpBox(pageGO, "Choose items to collect during Auto Loot.\nIf Potion is selected, it allows iksir items.");
                    break;

                case 1: // Cospre (Cosmetic hide)
                    CreateHeading(pageGO, "Hide Costumes [Self]");
                    CreateToggle(pageGO, "Hide Wing [Me]", GameOptionsManager.Instance.Cospre_HideWing_Me, (val) => GameOptionsManager.Instance.Cospre_HideWing_Me = val);
                    CreateToggle(pageGO, "Hide Fairy [Me]", GameOptionsManager.Instance.Cospre_HideFairy_Me, (val) => GameOptionsManager.Instance.Cospre_HideFairy_Me = val);
                    CreateToggle(pageGO, "Hide Costume Armor [Me]", GameOptionsManager.Instance.Cospre_HideCostumeArmor_Me, (val) => GameOptionsManager.Instance.Cospre_HideCostumeArmor_Me = val);
                    CreateHeading(pageGO, "Hide Costumes [Others]");
                    CreateToggle(pageGO, "Hide Wing [Others]", GameOptionsManager.Instance.Cospre_HideWing_Others, (val) => GameOptionsManager.Instance.Cospre_HideWing_Others = val, true);
                    CreateToggle(pageGO, "Hide Gloves [Others]", GameOptionsManager.Instance.Cospre_HideGloves_Others, (val) => GameOptionsManager.Instance.Cospre_HideGloves_Others = val, true);
                    CreateToggle(pageGO, "Hide Fairy [Others]", GameOptionsManager.Instance.Cospre_HideFairy_Others, (val) => GameOptionsManager.Instance.Cospre_HideFairy_Others = val, true);
                    CreateToggle(pageGO, "All Costumes Hide [Others]", GameOptionsManager.Instance.Cospre_HideAllCostumes_Others, (val) => GameOptionsManager.Instance.Cospre_HideAllCostumes_Others = val, true);
                    break;

                case 2: // Effect Option
                    CreateHeading(pageGO, "Performance Effects");
                    CreateToggle(pageGO, "Hide All Players", GameOptionsManager.Instance.Effect_HideAllPlayers, (val) => GameOptionsManager.Instance.Effect_HideAllPlayers = val);
                    CreateToggle(pageGO, "Hide Minor FX", GameOptionsManager.Instance.Effect_HideMinorFX, (val) => GameOptionsManager.Instance.Effect_HideMinorFX = val);
                    CreateToggle(pageGO, "Hide All Heal FX", GameOptionsManager.Instance.Effect_HideHealFX, (val) => GameOptionsManager.Instance.Effect_HideHealFX = val);
                    CreateToggle(pageGO, "Hide All Weapon FX", GameOptionsManager.Instance.Effect_HideWeaponFX, (val) => GameOptionsManager.Instance.Effect_HideWeaponFX = val);
                    CreateToggle(pageGO, "Hide Monster FX", GameOptionsManager.Instance.Effect_HideMonsterFX, (val) => GameOptionsManager.Instance.Effect_HideMonsterFX = val);
                    CreateToggle(pageGO, "Hide Target FX", GameOptionsManager.Instance.Effect_HideTargetFX, (val) => GameOptionsManager.Instance.Effect_HideTargetFX = val);
                    CreateToggle(pageGO, "Hide Hand Trail FX", GameOptionsManager.Instance.Effect_HideHandTrailFX, (val) => GameOptionsManager.Instance.Effect_HideHandTrailFX = val);
                    CreateToggle(pageGO, "Hide Cape FX", GameOptionsManager.Instance.Effect_HideCapeFX, (val) => { GameOptionsManager.Instance.Effect_HideCapeFX = val; RefreshCapesVisibility(); });
                    CreateToggle(pageGO, "Hide All Cast FX", GameOptionsManager.Instance.Effect_HideCastFX, (val) => GameOptionsManager.Instance.Effect_HideCastFX = val);
                    CreateToggle(pageGO, "Hide All Nova FX", GameOptionsManager.Instance.Effect_HideNovaFX, (val) => GameOptionsManager.Instance.Effect_HideNovaFX = val);
                    CreateHeading(pageGO, "Camera Shake Strength");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Effect_CameraShakeStrength, (val) => GameOptionsManager.Instance.Effect_CameraShakeStrength = val, "slider_camera_shake");
                    break;

                case 3: // Graphic Option
                    CreateHeading(pageGO, "FPS (Frames Per Second)");
                    CreateSelector(pageGO, new string[] { "30", "60", "90", "120" }, () => GameOptionsManager.Instance.Graphic_FPS.ToString(), (val) => {
                        if (int.TryParse(val, out int fps)) GameOptionsManager.Instance.Graphic_FPS = fps;
                    }, "selector_fps");
                    CreateHeading(pageGO, "Camera Zoom limit");
                    CreateZoomSelector(pageGO, "selector_zoom");
                    CreateHeading(pageGO, "Texture Quality");
                    CreateSelector(pageGO, new string[] { "High", "Medium", "Low" }, () => {
                        int q = GameOptionsManager.Instance.Graphic_TextureQuality;
                        return q == 0 ? "High" : q == 1 ? "Medium" : "Low";
                    }, (val) => {
                        int q = val == "High" ? 0 : val == "Medium" ? 1 : 2;
                        GameOptionsManager.Instance.Graphic_TextureQuality = q;
                    }, "selector_texture");
                    CreateHeading(pageGO, "Camera Far clip");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Graphic_CameraFar, (val) => GameOptionsManager.Instance.Graphic_CameraFar = val, "slider_camera_far");
                    CreateHeading(pageGO, "General Quality Settings");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Graphic_Quality, (val) => GameOptionsManager.Instance.Graphic_Quality = val, "slider_quality");
                    break;

                case 4: // Graphic Option 2
                    CreateHeading(pageGO, "Skill Area Sensitivity");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Graphic2_SkillAreaSens, (val) => GameOptionsManager.Instance.Graphic2_SkillAreaSens = val, "slider_skill_area");
                    CreateHeading(pageGO, "Camera Sensitivity");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Graphic2_CameraSens, (val) => GameOptionsManager.Instance.Graphic2_CameraSens = val, "slider_camera_sens");
                    CreateHeading(pageGO, "Z Button Scale");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Graphic2_ZButtonSize, (val) => { GameOptionsManager.Instance.Graphic2_ZButtonSize = val; MobileSkillBar.Instance?.ApplyZButtonScale(); }, "slider_z_size");
                    CreateHeading(pageGO, "Party UI Scale");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Graphic2_PartyUIScale, (val) => { GameOptionsManager.Instance.Graphic2_PartyUIScale = val; KOUIManager.Instance?.ApplyUIScalingAndMargins(); }, "slider_party_scale");
                    CreateHeading(pageGO, "Skill Bar Scale");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Graphic2_SkillBarSize, (val) => { GameOptionsManager.Instance.Graphic2_SkillBarSize = val; KOUIManager.Instance?.ApplyUIScalingAndMargins(); }, "slider_skill_bar");
                    break;

                case 5: // Graphic Option 3
                    CreateHeading(pageGO, "Post Exposure");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Graphic3_PostExposure, (val) => { GameOptionsManager.Instance.Graphic3_PostExposure = val; KOUIManager.Instance?.ApplyPostProcessingSettings(); }, "slider_exposure");
                    CreateHeading(pageGO, "Contrast");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Graphic3_Contrast, (val) => { GameOptionsManager.Instance.Graphic3_Contrast = val; KOUIManager.Instance?.ApplyPostProcessingSettings(); }, "slider_contrast");
                    CreateHeading(pageGO, "General UI Scale");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Graphic3_UIScale, (val) => { GameOptionsManager.Instance.Graphic3_UIScale = val; KOUIManager.Instance?.ApplyUIScalingAndMargins(); }, "slider_ui_scale");
                    break;

                case 6: // Block Option
                    CreateHeading(pageGO, "Block Requests");
                    CreateToggle(pageGO, "Block Party Requests", GameOptionsManager.Instance.Block_PartyRequests, (val) => GameOptionsManager.Instance.Block_PartyRequests = val);
                    CreateToggle(pageGO, "Block Trade Requests", GameOptionsManager.Instance.Block_TradeRequests, (val) => GameOptionsManager.Instance.Block_TradeRequests = val);
                    break;

                case 7: // PK Zone Option
                    CreateHeading(pageGO, "Combat Targeting Priority");
                    CreateToggle(pageGO, "Target Priority Player", GameOptionsManager.Instance.PK_TargetPriorityPlayer, (val) => {
                        GameOptionsManager.Instance.PK_TargetPriorityPlayer = val;
                        if (val)
                        {
                            GameOptionsManager.Instance.PK_TargetPriorityMonster = false;
                            SetToggleValueSafe("toggle_priority_monster", false);
                        }
                    });
                    CreateToggle(pageGO, "Target Priority Monster", GameOptionsManager.Instance.PK_TargetPriorityMonster, (val) => {
                        GameOptionsManager.Instance.PK_TargetPriorityMonster = val;
                        if (val)
                        {
                            GameOptionsManager.Instance.PK_TargetPriorityPlayer = false;
                            SetToggleValueSafe("toggle_priority_player", false);
                        }
                    });
                    CreateToggle(pageGO, "Z Fix (Lock on)", GameOptionsManager.Instance.PK_ZFix, (val) => GameOptionsManager.Instance.PK_ZFix = val);
                    CreateToggle(pageGO, "Show Hp Mp Bar Hud", GameOptionsManager.Instance.PK_ShowHpMpBarHud, (val) => GameOptionsManager.Instance.PK_ShowHpMpBarHud = val);
                    CreateToggle(pageGO, "Combo Helper", GameOptionsManager.Instance.PK_ComboHelper, (val) => GameOptionsManager.Instance.PK_ComboHelper = val);
                    CreateToggle(pageGO, "Change Z And R Keybind", GameOptionsManager.Instance.PK_ChangeZAndR, (val) => {
                        GameOptionsManager.Instance.PK_ChangeZAndR = val;
                        MobileSkillBar.Instance?.ApplyZAndRLayout();
                    });
                    CreateHeading(pageGO, "Extra Skill Count");
                    CreateSelector(pageGO, new string[] { "0", "1", "2", "3", "4", "5", "6" }, () => GameOptionsManager.Instance.PK_ExtraSkillCount.ToString(), (val) => {
                        if (int.TryParse(val, out int count)) {
                            GameOptionsManager.Instance.PK_ExtraSkillCount = count;
                            MobileSkillBar.Instance?.ApplyExtraSkillsActiveState();
                        }
                    }, "selector_extra_skills");
                    CreateButton(pageGO, "Move Skill Slots", new Color(0.1f, 0.4f, 0.6f, 1f), () => {
                        if (MobileSkillBar.Instance == null) return;
                        bool newMode = !MobileSkillBar.Instance.IsEditMode;
                        MobileSkillBar.Instance.SetEditMode(newMode);
                        if (!newMode)
                        {
                            MobileSkillBar.Instance.SaveSavedPositions();
                        }
                        UpdateMoveButtonUI(newMode);
                    }, "btn_move_skill_slots");
                    CreateButton(pageGO, "Reset Skill Slots", new Color(0.7f, 0.2f, 0.2f, 1f), () => {
                        if (MobileSkillBar.Instance != null)
                        {
                            MobileSkillBar.Instance.ResetPositions();
                        }
                        GameOptionsManager.Instance.PK_ExtraSkillCount = 0;
                        MobileSkillBar.Instance?.ApplyExtraSkillsActiveState();
                        GameOptionsManager.Instance.SaveSettings();
                        RefreshAllValues();
                        RefreshPrefabUIValues();
                        SwitchToTab(_activePageIndex);
                    }, "btn_reset_skill_slots");
                    break;

                case 8: // Sound
                    CreateHeading(pageGO, "Mute Settings");
                    CreateToggle(pageGO, "Mute All Sounds", GameOptionsManager.Instance.Sound_MuteAll, (val) => GameOptionsManager.Instance.Sound_MuteAll = val);
                    CreateToggle(pageGO, "Mute Walk Sounds", GameOptionsManager.Instance.Sound_MuteWalk, (val) => GameOptionsManager.Instance.Sound_MuteWalk = val);
                    CreateHeading(pageGO, "Background Music");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Sound_Background, (val) => GameOptionsManager.Instance.Sound_Background = val, "slider_bg_sound");
                    CreateHeading(pageGO, "Skill Sounds Volume");
                    CreateSlider(pageGO, GameOptionsManager.Instance.Sound_Skill, (val) => GameOptionsManager.Instance.Sound_Skill = val, "slider_skill_sound");
                    break;

                case 9: // PM Block List
                    CreateHeading(pageGO, "PM Blocked Players");
                    // Build a simple list of blocked players
                    var blockScrollGO = CreateUIObject("BlockListScroll", pageGO.transform);
                    var bScrollRT = blockScrollGO.GetComponent<RectTransform>();
                    bScrollRT.sizeDelta = new Vector2(0, 160f); // Fixed height for block scroll

                    var bScroll = blockScrollGO.AddComponent<ScrollRect>();
                    var bViewGO = CreateUIObject("Viewport", blockScrollGO.transform);
                    var bViewRT = bViewGO.GetComponent<RectTransform>();
                    bViewRT.anchorMin = Vector2.zero;
                    bViewRT.anchorMax = Vector2.one;
                    bViewRT.offsetMin = Vector2.zero;
                    bViewRT.offsetMax = Vector2.zero;
                    bViewGO.AddComponent<RectMask2D>();

                    var bContentGO = CreateUIObject("Content", bViewRT);
                    var bContentRT = bContentGO.GetComponent<RectTransform>();
                    bContentRT.anchorMin = new Vector2(0, 1);
                    bContentRT.anchorMax = new Vector2(1, 1);
                    bContentRT.pivot = new Vector2(0.5f, 1);
                    bContentRT.sizeDelta = new Vector2(0, 0);

                    var bLayout = bContentGO.AddComponent<VerticalLayoutGroup>();
                    bLayout.spacing = 6;
                    bLayout.childControlHeight = false; // Respect row height
                    bLayout.childForceExpandHeight = false;
                    bLayout.childForceExpandWidth = true;
                    bLayout.childControlWidth = true; // Ensure block rows stretch symmetrically

                    var bFitter = bContentGO.AddComponent<ContentSizeFitter>();
                    bFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    bScroll.content = bContentRT;
                    bScroll.viewport = bViewRT;

                    // Populate dynamically from BlockList
                    PopulateBlockedPlayers(bContentRT);
                    break;

                case 10: // Hide Option
                    CreateHeading(pageGO, "General Hide Options");
                    CreateToggle(pageGO, "Hide Name Plates", GameOptionsManager.Instance.Hide_NamePlates, (val) => GameOptionsManager.Instance.Hide_NamePlates = val);
                    CreateToggle(pageGO, "Hide Friends Name Plates", GameOptionsManager.Instance.Hide_FriendsNamePlates, (val) => GameOptionsManager.Instance.Hide_FriendsNamePlates = val);
                    CreateToggle(pageGO, "Hide All Capes", GameOptionsManager.Instance.Hide_AllCapes, (val) => { GameOptionsManager.Instance.Hide_AllCapes = val; RefreshCapesVisibility(); });
                    CreateToggle(pageGO, "Hide Wing [ Me ]", GameOptionsManager.Instance.Hide_WingMe, (val) => GameOptionsManager.Instance.Hide_WingMe = val);
                    CreateToggle(pageGO, "Hide Target Mark", GameOptionsManager.Instance.Hide_TargetMark, (val) => GameOptionsManager.Instance.Hide_TargetMark = val);
                    CreateToggle(pageGO, "Hide Leader Mark", GameOptionsManager.Instance.Hide_LeaderMark, (val) => GameOptionsManager.Instance.Hide_LeaderMark = val);
                    CreateToggle(pageGO, "Hide Player Shadow", GameOptionsManager.Instance.Hide_PlayerShadow, (val) => GameOptionsManager.Instance.Hide_PlayerShadow = val);
                    CreateToggle(pageGO, "Hide UI Kill Animation", GameOptionsManager.Instance.Hide_UIKillAnim, (val) => GameOptionsManager.Instance.Hide_UIKillAnim = val);
                    CreateToggle(pageGO, "Hide Red Hit Screen", GameOptionsManager.Instance.Hide_RedHitScreen, (val) => GameOptionsManager.Instance.Hide_RedHitScreen = val);
                    CreateToggle(pageGO, "Hide Gray Screen", GameOptionsManager.Instance.Hide_GrayScreen, (val) => GameOptionsManager.Instance.Hide_GrayScreen = val);
                    CreateToggle(pageGO, "Damage Text Active", GameOptionsManager.Instance.Hide_DamageTextActive, (val) => GameOptionsManager.Instance.Hide_DamageTextActive = val);
                    CreateButton(pageGO, "Hide UI", new Color(0.1f, 0.4f, 0.4f, 1f), () => {});
                    break;

                case 11: // Mod Option
                    CreateHeading(pageGO, "Sözleşme / Agreement");
                    CreateHelpBox(pageGO, "TR: Kabul Et butonuna tıklayarak DLC sözleşmesini kabul etmiş olursunuz.\n\nEN: By clicking I Accept button, you agree to the DLC Agreement.");
                    CreateButton(pageGO, "I ACCEPT", new Color(0.1f, 0.4f, 0.4f, 1f), () => {
                        GameOptionsManager.Instance.Mod_DLCAccepted = true;
                        GameOptionsManager.Instance.SaveSettings();
                    }, "btn_accept_agreement");
                    break;

                case 12: // Language
                    CreateHeading(pageGO, "Game Language / Oyun Dili");
                    CreateLanguageSelector(pageGO, true);
                    CreateHeading(pageGO, "Notice Language / Duyuru Dili");
                    CreateLanguageSelector(pageGO, false);
                    break;
            }

            // Append reset button at the bottom of the page content
            CreateResetButton(pageGO, pageIndex);
        }

        private void PopulateBlockedPlayers(Transform container)
        {
            // Clear old children
            foreach (Transform child in container) Destroy(child.gameObject);

            int count = 0;
            foreach (var blockedPlayer in GameOptionsManager.Instance.PMBlockList)
            {
                count++;
                var rowGO = CreateUIObject($"BlockRow_{blockedPlayer}", container);
                var rowRT = rowGO.GetComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(0, 28f);

                var img = rowGO.AddComponent<Image>();
                img.color = _colorBtnNormal;

                var text = CreateText(rowGO, $" {blockedPlayer}", 13, TextAlignmentOptions.MidlineLeft); // FontSize to 13
                var textRT = text.GetComponent<RectTransform>();
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = new Vector2(0.8f, 1);
                textRT.offsetMin = new Vector2(5f, 0);

                var removeBtnGO = CreateUIObject("btn_remove", rowGO.transform);
                var remRT = removeBtnGO.GetComponent<RectTransform>();
                remRT.anchorMin = new Vector2(1, 0.5f);
                remRT.anchorMax = new Vector2(1, 0.5f);
                remRT.pivot = new Vector2(1, 0.5f);
                remRT.anchoredPosition = new Vector2(-5f, 0);
                remRT.sizeDelta = new Vector2(35f, 20f);

                var remImg = removeBtnGO.AddComponent<Image>();
                remImg.color = new Color(0.6f, 0.2f, 0.2f, 1f);
                var remTxt = CreateText(removeBtnGO, "x", 12, TextAlignmentOptions.Center); // FontSize to 12
                remTxt.color = Color.white;

                var remBtn = removeBtnGO.AddComponent<Button>();
                string nameToUnblock = blockedPlayer;
                remBtn.onClick.AddListener(() => {
                    GameOptionsManager.Instance.RemoveBlockedPlayer(nameToUnblock);
                    PopulateBlockedPlayers(container);
                });
            }

            if (count == 0)
            {
                var rowGO = CreateUIObject("BlockRow_Empty", container);
                var rowRT = rowGO.GetComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(0, 30f);
                var txt = CreateText(rowGO, "No blocked players", 13, TextAlignmentOptions.Center); // FontSize to 13
                txt.color = Color.gray;
            }
        }

        private void CreateLanguageSelector(GameObject pageGO, bool isGameLang)
        {
            var selectGO = CreateUIObject(isGameLang ? "LangSelect_Game" : "LangSelect_Notice", pageGO.transform);
            var selectRT = selectGO.GetComponent<RectTransform>();
            selectRT.sizeDelta = new Vector2(0, 40f);

            var layout = selectGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true;

            string[] langs = new string[] { "English", "Español", "Türkçe" };

            for (int i = 0; i < 3; i++)
            {
                int langIdx = i;
                var langBtnGO = CreateUIObject($"LangBtn_{i}", selectGO.transform);

                var img = langBtnGO.AddComponent<Image>();
                img.color = _colorBtnNormal;

                var text = CreateText(langBtnGO, langs[i], 11, TextAlignmentOptions.Center); // FontSize to 11
                text.color = _colorTextGold;

                var outline = langBtnGO.AddComponent<Outline>();
                outline.effectColor = _colorBorder * 0.3f;
                outline.effectDistance = new Vector2(1, 1);

                var btn = langBtnGO.AddComponent<Button>();
                btn.onClick.AddListener(() => {
                    if (isGameLang)
                        GameOptionsManager.Instance.Lang_GameLanguage = langIdx;
                    else
                        GameOptionsManager.Instance.Lang_NoticeLanguage = langIdx;

                    GameOptionsManager.Instance.SaveSettings();
                    UpdateLanguageOutlines(selectGO.transform, isGameLang ? GameOptionsManager.Instance.Lang_GameLanguage : GameOptionsManager.Instance.Lang_NoticeLanguage);
                });
            }

            UpdateLanguageOutlines(selectGO.transform, isGameLang ? GameOptionsManager.Instance.Lang_GameLanguage : GameOptionsManager.Instance.Lang_NoticeLanguage);
        }

        private void UpdateLanguageOutlines(Transform container, int activeIdx)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                var outline = container.GetChild(i).GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = (i == activeIdx) ? Color.yellow : _colorBorder * 0.3f;
                    outline.effectDistance = (i == activeIdx) ? new Vector2(2, 2) : new Vector2(1, 1);
                }
            }
        }

        // --- Helper Builders ---

        private void CreateSpacer(GameObject parent, float height)
        {
            var go = CreateUIObject("Spacer", parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, height);
        }

        private void CreateResetButton(GameObject pageGO, int pageIndex)
        {
            CreateSpacer(pageGO, 10f);
            var btnName = $"RESET {(_tabNames[pageIndex].ToUpperInvariant())}";
            CreateResetButtonUI(pageGO, btnName, () => {
                ResetSettingsForTab(pageIndex);
                RefreshAllValues();
                SwitchToTab(pageIndex);
            });
        }

        private void CreateResetButtonUI(GameObject parent, string text, Action onClick)
        {
            var go = CreateUIObject("ResetButton", parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 30f);

            var img = go.AddComponent<Image>();
            Color colorCloseMerch = new Color(0.4f, 0.15f, 0.15f, 1f); // Dark red close background
            Color colorCloseMerchBorder = new Color(0.75f, 0.15f, 0.15f, 1f); // Bright red close border
            
            if (KOUIManager.Instance != null)
            {
                img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_reset_btn_bg_" + text.Replace(" ", "_"), 180, 30, 10, // Radius 10
                    colorCloseMerch, 
                    colorCloseMerchBorder, 
                    1
                );
                img.color = Color.white;
            }
            else
            {
                img.color = colorCloseMerch;
            }

            var txt = CreateText(go, text, 12, TextAlignmentOptions.Center);
            txt.color = Color.white;
            txt.fontStyle = FontStyles.Bold;

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
        }

        private TextMeshProUGUI CreateText(GameObject parent, string text, int fontSize, TextAlignmentOptions alignment)
        {
            var go = CreateUIObject("Text", parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = _colorTextGold;
            txt.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF"); // Default dynamic font in TMP
            return txt;
        }

        private void CreateHeading(GameObject parent, string text)
        {
            var go = CreateUIObject("Heading", parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 24f);

            var txt = CreateText(go, text, 13, TextAlignmentOptions.MidlineLeft); // FontSize enlarged from 11 to 13
            txt.fontStyle = FontStyles.Bold;
            txt.color = _colorTextGold;
        }

        private string GetToggleName(string text)
        {
            string t = text.ToLower().Replace("[", "").Replace("]", "").Trim();
            if (t.Contains("low class")) return "toggle_low_class";
            if (t.Contains("middle class")) return "toggle_middle_class";
            if (t.Contains("high class")) return "toggle_high_class";
            if (t.Contains("potion")) return "toggle_potion";
            if (t.Contains("wing [me]") || t.Contains("wing me")) return "toggle_wing_me";
            if (t.Contains("fairy [me]") || t.Contains("fairy me")) return "toggle_fairy_me";
            if (t.Contains("costume armor [me]") || t.Contains("costume armor me")) return "toggle_costume_me";
            if (t.Contains("wing [others]") || t.Contains("wing others")) return "toggle_wing_others";
            if (t.Contains("gloves [others]") || t.Contains("gloves others")) return "toggle_gloves_others";
            if (t.Contains("fairy [others]") || t.Contains("fairy others")) return "toggle_fairy_others";
            if (t.Contains("all costumes hide") || t.Contains("all costumes")) return "toggle_costume_others";
            if (t.Contains("hide all players")) return "toggle_hide_all_players";
            if (t.Contains("hide minor fx")) return "toggle_hide_minor";
            if (t.Contains("hide all heal")) return "toggle_hide_heal";
            if (t.Contains("hide all weapon")) return "toggle_hide_weapon";
            if (t.Contains("hide monster fx")) return "toggle_hide_monster";
            if (t.Contains("hide target fx")) return "toggle_hide_target";
            if (t.Contains("hide hand trail")) return "toggle_hide_trail";
            if (t.Contains("hide cape fx")) return "toggle_hide_cape";
            if (t.Contains("hide all cast")) return "toggle_hide_cast";
            if (t.Contains("hide all nova")) return "toggle_hide_nova";
            if (t.Contains("block party")) return "toggle_block_party";
            if (t.Contains("block trade")) return "toggle_block_trade";
            if (t.Contains("priority player")) return "toggle_priority_player";
            if (t.Contains("priority monster")) return "toggle_priority_monster";
            if (t.Contains("z fix")) return "toggle_z_fix";
            if (t.Contains("show hp/mp bar") || t.Contains("show hud bars")) return "toggle_show_hud_bars";
            if (t.Contains("combo helper")) return "toggle_combo_helper";
            if (t.Contains("change z and r")) return "toggle_change_z_r";
            if (t.Contains("mute all")) return "toggle_mute_all";
            if (t.Contains("mute walk")) return "toggle_mute_walk";
            if (t.Contains("hide name plates") || t.Contains("hide name")) return "toggle_hide_name";
            if (t.Contains("hide friends name")) return "toggle_hide_friends_name";
            if (t.Contains("hide all capes")) return "toggle_hide_capes";
            if (t.Contains("hide wing")) return "toggle_hide_wing";
            if (t.Contains("hide target mark") || t.Contains("hide target")) return "toggle_hide_target";
            if (t.Contains("hide leader")) return "toggle_hide_leader";
            if (t.Contains("hide player shadow") || t.Contains("hide shadow")) return "toggle_hide_shadow";
            if (t.Contains("hide ui kill") || t.Contains("hide kill")) return "toggle_hide_kill_anim";
            if (t.Contains("hide red hit") || t.Contains("hide red")) return "toggle_hide_red_screen";
            if (t.Contains("hide gray")) return "toggle_hide_gray_screen";
            if (t.Contains("damage text")) return "toggle_damage_text";
            return "ToggleRow_" + text.Replace(" ", "_");
        }

        private void CreateToggle(GameObject parent, string text, bool startVal, Action<bool> action, bool redText = false)
        {
            var go = CreateUIObject(GetToggleName(text), parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 28f);

            // Left-aligned Text spanning 80% width for perfect symmetry
            var txt = CreateText(go, text, 12, TextAlignmentOptions.MidlineLeft); // FontSize enlarged from 10 to 12
            var txtRT = txt.GetComponent<RectTransform>();
            txtRT.anchorMin = new Vector2(0, 0);
            txtRT.anchorMax = new Vector2(0.8f, 1);
            txtRT.pivot = new Vector2(0, 0.5f);
            txtRT.offsetMin = new Vector2(5f, 0f);
            txtRT.offsetMax = new Vector2(0f, 0f);
            if (redText) txt.color = new Color(0.9f, 0.3f, 0.3f, 1f);

            // Right-aligned checkbox button
            var toggleGO = CreateUIObject("toggle", go.transform);
            var togRT = toggleGO.GetComponent<RectTransform>();
            togRT.anchorMin = new Vector2(1, 0.5f);
            togRT.anchorMax = new Vector2(1, 0.5f);
            togRT.pivot = new Vector2(1, 0.5f);
            togRT.anchoredPosition = new Vector2(-5f, 0);
            togRT.sizeDelta = new Vector2(18f, 18f);

            var toggleBg = toggleGO.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                toggleBg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_toggle_bg", 18, 18, 4,
                    new Color(0.05f, 0.04f, 0.04f, 1f),
                    new Color(0.75f, 0.63f, 0.38f, 0.5f),
                    1
                );
                toggleBg.color = Color.white;
            }
            else
            {
                toggleBg.color = _colorInputBg;
            }

            var tickGO = CreateUIObject("tick", toggleGO.transform);
            var tickRT = tickGO.GetComponent<RectTransform>();
            tickRT.anchorMin = new Vector2(0.2f, 0.2f);
            tickRT.anchorMax = new Vector2(0.8f, 0.8f);
            tickRT.offsetMin = Vector2.zero;
            tickRT.offsetMax = Vector2.zero;
            var tickImg = tickGO.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                tickImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_toggle_tick", 10, 10, 2,
                    new Color(0.95f, 0.85f, 0.35f, 1f), // Gold tick
                    Color.clear,
                    0
                );
                tickImg.color = Color.white;
            }
            else
            {
                tickImg.color = _colorCheckboxActive;
            }

            var toggle = toggleGO.AddComponent<Toggle>();
            toggle.graphic = tickImg;
            toggle.isOn = startVal;
            toggle.onValueChanged.AddListener((val) => {
                action(val);
                GameOptionsManager.Instance.SaveSettings();
            });
        }

        private void CreateSlider(GameObject parent, float startVal, Action<float> action, string goName = "SliderRow")
        {
            var go = CreateUIObject(goName, parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 26f);

            var slider = go.AddComponent<Slider>();

            // Slider Background matching auto attack style (170 wide, 4 height, 0 radius)
            var bgGO = CreateUIObject("Background", go.transform);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.45f);
            bgRT.anchorMax = new Vector2(1, 0.55f);
            bgRT.offsetMin = new Vector2(10f, 0f);
            bgRT.offsetMax = new Vector2(-10f, 0f);
            var bgImg = bgGO.AddComponent<Image>();
            
            Color barColor = new Color(0.45f, 0.35f, 0.15f, 0.8f); // Warm bronze/brown bar
            if (KOUIManager.Instance != null)
            {
                bgImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_slider_bg_styled", 170, 4, 0,
                    barColor, barColor, 0
                );
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = barColor;
            }

            // Slider Fill Area (Root fix: slider.fillRect must be assigned to avoid UnassignedReferenceException)
            var fillAreaGO = CreateUIObject("Fill Area", go.transform);
            var faRT = fillAreaGO.GetComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0, 0.45f);
            faRT.anchorMax = new Vector2(1, 0.55f);
            faRT.offsetMin = new Vector2(10f, 0f);
            faRT.offsetMax = new Vector2(-10f, 0f);

            var fillGO = CreateUIObject("Fill", fillAreaGO.transform);
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            
            Color fillColorVal = new Color(0.75f, 0.63f, 0.38f, 0.9f); // Gold fill color
            if (KOUIManager.Instance != null)
            {
                fillImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_slider_fill_styled", 170, 4, 0,
                    fillColorVal, fillColorVal, 0
                );
                fillImg.color = Color.white;
            }
            else
            {
                fillImg.color = fillColorVal;
            }

            // Slider Handle Slide Area
            var handleAreaGO = CreateUIObject("Handle Slide Area", go.transform);
            var haRT = handleAreaGO.GetComponent<RectTransform>();
            haRT.anchorMin = new Vector2(0, 0);
            haRT.anchorMax = new Vector2(1, 1);
            haRT.offsetMin = new Vector2(15f, 0f);
            haRT.offsetMax = new Vector2(-15f, 0f);

            // Slider Handle matching auto attack handle style (28x14 Handle container with a 28x12 Visual child to prevent vertical stretching)
            var handleGO = CreateUIObject("Handle", handleAreaGO.transform);
            var hanRT = handleGO.GetComponent<RectTransform>();
            hanRT.anchorMin = new Vector2(0f, 0.5f);
            hanRT.anchorMax = new Vector2(0f, 0.5f);
            hanRT.pivot = new Vector2(0.5f, 0.5f);
            hanRT.anchoredPosition = Vector2.zero;
            hanRT.sizeDelta = new Vector2(28f, 14f); // Container size matching auto attack handle
            
            var visualGO = CreateUIObject("Visual", handleGO.transform);
            var visRT = visualGO.GetComponent<RectTransform>();
            visRT.anchorMin = new Vector2(0.5f, 0.5f);
            visRT.anchorMax = new Vector2(0.5f, 0.5f);
            visRT.pivot = new Vector2(0.5f, 0.5f);
            visRT.anchoredPosition = Vector2.zero;
            visRT.sizeDelta = new Vector2(28f, 12f); // Fixed visual height to match auto attack handle exactly
            
            var hanImg = visualGO.AddComponent<Image>();
            Color fillColor = new Color(0.85f, 0.75f, 0.55f, 1f); // Warm gold handle fill
            Color borderColor = new Color(0.45f, 0.35f, 0.15f, 1f); // Warm bronze handle border
            if (KOUIManager.Instance != null)
            {
                hanImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_slider_handle_styled", 28, 12, 6, // 6px rounded corners (capsule shape)
                    fillColor, borderColor, 1
                );
                hanImg.color = Color.white;
            }
            else
            {
                hanImg.color = fillColor;
            }

            // Stripes "|||" inside the visual container
            var stripesGO = CreateUIObject("Stripes", visualGO.transform);
            var strRT = stripesGO.GetComponent<RectTransform>();
            strRT.anchorMin = Vector2.zero;
            strRT.anchorMax = Vector2.one;
            strRT.offsetMin = Vector2.zero;
            strRT.offsetMax = Vector2.zero;
            
            var stripesText = stripesGO.AddComponent<TextMeshProUGUI>();
            stripesText.text = "|||";
            stripesText.fontSize = 8; // Original font size for stripes
            stripesText.alignment = TextAlignmentOptions.Center;
            stripesText.color = new Color(0.2f, 0.15f, 0.05f, 0.8f);
            stripesText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            slider.fillRect = fillRT;
            slider.handleRect = hanRT;
            slider.targetGraphic = hanImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = startVal;

            slider.onValueChanged.AddListener((val) => {
                action(val);
                GameOptionsManager.Instance.SaveSettings();
            });
        }

        private void CreateSelector(GameObject parent, string[] options, Func<string> getCurrentVal, Action<string> action, string goName = "SelectorRow")
        {
            var go = CreateUIObject(goName, parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 28f);

            var bgImg = go.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                bgImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_selector_bg", 180, 28, 4,
                    new Color(0.05f, 0.04f, 0.04f, 1f),
                    new Color(0.75f, 0.63f, 0.38f, 0.4f),
                    1
                );
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = _colorInputBg;
            }

            var txt = CreateText(go, getCurrentVal(), 12, TextAlignmentOptions.Center); // FontSize enlarged from 10 to 12
            var txtRT = txt.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(30, 0);
            txtRT.offsetMax = new Vector2(-30, 0);

            // Left arrow
            var leftBtnGO = CreateUIObject("btn_left", go.transform);
            var lRT = leftBtnGO.GetComponent<RectTransform>();
            lRT.anchorMin = new Vector2(0, 0.5f);
            lRT.anchorMax = new Vector2(0, 0.5f);
            lRT.pivot = new Vector2(0, 0.5f);
            lRT.anchoredPosition = new Vector2(5f, 0);
            lRT.sizeDelta = new Vector2(18f, 18f);
            var lImg = leftBtnGO.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                lImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_arrow_btn_l", 18, 18, 3,
                    new Color(0.18f, 0.12f, 0.10f, 1f),
                    new Color(0.75f, 0.63f, 0.38f, 0.5f),
                    1
                );
                lImg.color = Color.white;
            }
            else
            {
                lImg.color = _colorBtnNormal;
            }
            var lTxt = CreateText(leftBtnGO, "<", 11, TextAlignmentOptions.Center); // FontSize enlarged from 9 to 11
            var lBtn = leftBtnGO.AddComponent<Button>();

            // Right arrow
            var rightBtnGO = CreateUIObject("btn_right", go.transform);
            var rRT = rightBtnGO.GetComponent<RectTransform>();
            rRT.anchorMin = new Vector2(1, 0.5f);
            rRT.anchorMax = new Vector2(1, 0.5f);
            rRT.pivot = new Vector2(1, 0.5f);
            rRT.anchoredPosition = new Vector2(-5f, 0);
            rRT.sizeDelta = new Vector2(18f, 18f);
            var rImg = rightBtnGO.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                rImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_arrow_btn_r", 18, 18, 3,
                    new Color(0.18f, 0.12f, 0.10f, 1f),
                    new Color(0.75f, 0.63f, 0.38f, 0.5f),
                    1
                );
                rImg.color = Color.white;
            }
            else
            {
                rImg.color = _colorBtnNormal;
            }
            var rTxt = CreateText(rightBtnGO, ">", 11, TextAlignmentOptions.Center); // FontSize enlarged from 9 to 11
            var rBtn = rightBtnGO.AddComponent<Button>();

            Action updateVal = () => {
                txt.text = getCurrentVal();
            };

            lBtn.onClick.AddListener(() => {
                string cur = getCurrentVal();
                int idx = Array.IndexOf(options, cur);
                if (idx > 0)
                {
                    action(options[idx - 1]);
                    updateVal();
                    GameOptionsManager.Instance.SaveSettings();
                }
            });

            rBtn.onClick.AddListener(() => {
                string cur = getCurrentVal();
                int idx = Array.IndexOf(options, cur);
                if (idx >= 0 && idx < options.Length - 1)
                {
                    action(options[idx + 1]);
                    updateVal();
                    GameOptionsManager.Instance.SaveSettings();
                }
            });
        }

        private void CreateZoomSelector(GameObject parent, string goName = "selector_zoom")
        {
            var go = CreateUIObject(goName, parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 28f);

            var bgImg = go.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                bgImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_selector_bg", 180, 28, 4,
                    new Color(0.05f, 0.04f, 0.04f, 1f),
                    new Color(0.75f, 0.63f, 0.38f, 0.4f),
                    1
                );
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = _colorInputBg;
            }

            int currentVal = GameOptionsManager.Instance.Graphic_CameraZoom;
            var txt = CreateText(go, currentVal.ToString(), 12, TextAlignmentOptions.Center);
            var txtRT = txt.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(30, 0);
            txtRT.offsetMax = new Vector2(-30, 0);

            // Left "+" button (zooms in / approaches character)
            var leftBtnGO = CreateUIObject("btn_left", go.transform);
            var lRT = leftBtnGO.GetComponent<RectTransform>();
            lRT.anchorMin = new Vector2(0, 0.5f);
            lRT.anchorMax = new Vector2(0, 0.5f);
            lRT.pivot = new Vector2(0, 0.5f);
            lRT.anchoredPosition = new Vector2(5f, 0);
            lRT.sizeDelta = new Vector2(18f, 18f);
            var lImg = leftBtnGO.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                lImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_arrow_btn_l", 18, 18, 3,
                    new Color(0.18f, 0.12f, 0.10f, 1f),
                    new Color(0.75f, 0.63f, 0.38f, 0.5f),
                    1
                );
                lImg.color = Color.white;
            }
            else
            {
                lImg.color = _colorBtnNormal;
            }
            var lTxt = CreateText(leftBtnGO, "+", 11, TextAlignmentOptions.Center);
            var lBtn = leftBtnGO.AddComponent<Button>();

            // Right "-" button (zooms out / distances character)
            var rightBtnGO = CreateUIObject("btn_right", go.transform);
            var rRT = rightBtnGO.GetComponent<RectTransform>();
            rRT.anchorMin = new Vector2(1, 0.5f);
            rRT.anchorMax = new Vector2(1, 0.5f);
            rRT.pivot = new Vector2(1, 0.5f);
            rRT.anchoredPosition = new Vector2(-5f, 0);
            rRT.sizeDelta = new Vector2(18f, 18f);
            var rImg = rightBtnGO.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                rImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_arrow_btn_r", 18, 18, 3,
                    new Color(0.18f, 0.12f, 0.10f, 1f),
                    new Color(0.75f, 0.63f, 0.38f, 0.5f),
                    1
                );
                rImg.color = Color.white;
            }
            else
            {
                rImg.color = _colorBtnNormal;
            }
            var rTxt = CreateText(rightBtnGO, "-", 11, TextAlignmentOptions.Center);
            var rBtn = rightBtnGO.AddComponent<Button>();

            lBtn.onClick.AddListener(() => {
                int val = GameOptionsManager.Instance.Graphic_CameraZoom;
                if (val < 10) // Limit approach to +10 (minDistance = 3f)
                {
                    val++;
                    GameOptionsManager.Instance.Graphic_CameraZoom = val;
                    txt.text = val.ToString();
                    
                    // Apply zoom immediately to active CameraController
                    var cam = UnityEngine.Object.FindAnyObjectByType<EntropyOnline.Camera.CameraController>();
                    if (cam != null) cam.SetZoomLimit(val);
                    
                    GameOptionsManager.Instance.SaveSettings();
                }
            });

            rBtn.onClick.AddListener(() => {
                int val = GameOptionsManager.Instance.Graphic_CameraZoom;
                if (val > -1) // Limit zoom out to -1 (furthest distance is 10.6f)
                {
                    val--;
                    GameOptionsManager.Instance.Graphic_CameraZoom = val;
                    txt.text = val.ToString();
                    
                    // Apply zoom immediately to active CameraController
                    var cam = UnityEngine.Object.FindAnyObjectByType<EntropyOnline.Camera.CameraController>();
                    if (cam != null) cam.SetZoomLimit(val);
                    
                    GameOptionsManager.Instance.SaveSettings();
                }
            });
        }

        private void CreateInputField(GameObject parent, string defaultVal, Action<string> action, string goName = "InputField")
        {
            var go = CreateUIObject(goName, parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 28f);

            var img = go.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_input_bg", 180, 28, 4,
                    new Color(0.05f, 0.04f, 0.04f, 1f),
                    new Color(0.75f, 0.63f, 0.38f, 0.4f),
                    1
                );
                img.color = Color.white;
            }
            else
            {
                img.color = _colorInputBg;
            }

            var textGO = CreateUIObject("Text", go.transform);
            var txtRT = textGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(5, 3);
            txtRT.offsetMax = new Vector2(-5, -3);

            var txt = textGO.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 12; // FontSize enlarged from 10 to 12
            txt.color = Color.white;
            txt.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            var input = go.AddComponent<TMP_InputField>();
            input.textComponent = txt;
            input.text = defaultVal;

            input.onValueChanged.AddListener((val) => {
                action(val);
                GameOptionsManager.Instance.SaveSettings();
            });
        }

        private void CreateButton(GameObject parent, string text, Color color, Action onClick, string goName = "Button")
        {
            var go = CreateUIObject(goName, parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 30f);

            var img = go.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_btn_bg_" + text.Replace(" ", "_"), 180, 30, 4,
                    color * 0.8f, // Richer color
                    new Color(0.75f, 0.63f, 0.38f, 1f), // Gold border
                    1
                );
                img.color = Color.white;
            }
            else
            {
                img.color = color;
            }

            var txt = CreateText(go, text, 12, TextAlignmentOptions.Center); // FontSize enlarged from 10 to 12
            txt.color = Color.white;

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
        }

        private void CreateHelpBox(GameObject parent, string text)
        {
            var go = CreateUIObject("HelpBox", parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 50f);

            var img = go.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_helpbox_bg", 180, 50, 4,
                    new Color(0.12f, 0.10f, 0.09f, 1f),
                    new Color(0.75f, 0.63f, 0.38f, 0.3f),
                    1
                );
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.12f, 0.10f, 0.09f, 1f);
            }

            var txt = CreateText(go, text, 10, TextAlignmentOptions.Center); // FontSize enlarged from 8 to 10
            txt.color = Color.gray;
            var txtRT = txt.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(6, 6);
            txtRT.offsetMax = new Vector2(-6, -6);
        }

        // --- Interaction Logic ---

        private void SwitchToTab(int index)
        {
            _activePageIndex = index;

            // Highlight active tab button
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var img = _tabButtons[i].GetComponent<Image>();
                var txt = _tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (img != null)
                {
                    if (KOUIManager.Instance != null)
                    {
                        if (i == index)
                        {
                            img.sprite = KOUIManager.Instance.GetFadeGradientSprite(
                                "opt_sidebar_fade_115_30", 115, 30, Color.white, Color.clear, 0
                            );
                            img.color = new Color(0.6f, 0.48f, 0.22f, 0.35f); // Warm active fadeout highlight
                            if (txt != null) txt.color = new Color(0.95f, 0.85f, 0.35f, 1.0f); // Bright gold text
                        }
                        else
                        {
                            img.sprite = KOUIManager.Instance.GetFadeGradientSprite(
                                "opt_sidebar_fade_115_30", 115, 30, Color.white, Color.clear, 0
                            );
                            img.color = Color.clear; // Transparent inactive
                            if (txt != null) txt.color = new Color(0.75f, 0.70f, 0.65f, 1.0f); // Silver/grey text
                        }
                    }
                    else
                    {
                        img.color = (i == index) ? _colorBtnActive : _colorBtnNormal;
                    }
                }
            }

            // Set active page
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].SetActive(i == index);
            }
        }

        private void CloseMenu()
        {
            gameObject.SetActive(false);
        }

        private void ResetSettings()
        {
            ResetSettingsForTab(_activePageIndex); // Reset ONLY active tab settings
            RefreshAllValues();
            RefreshPrefabUIValues();
            SwitchToTab(_activePageIndex);
        }

        private void ResetSettingsForTab(int pageIndex)
        {
            var mgr = GameOptionsManager.Instance;
            switch (pageIndex)
            {
                case 0: // Looting
                    mgr.Loot_LowClass = true;
                    mgr.Loot_MiddleClass = true;
                    mgr.Loot_HighClass = true;
                    mgr.Loot_Potion = true;
                    mgr.Loot_SellPrice = 0;
                    break;
                case 1: // Cospre
                    mgr.Cospre_HideWing_Me = false;
                    mgr.Cospre_HideFairy_Me = false;
                    mgr.Cospre_HideCostumeArmor_Me = false;
                    mgr.Cospre_HideWing_Others = false;
                    mgr.Cospre_HideGloves_Others = false;
                    mgr.Cospre_HideFairy_Others = false;
                    mgr.Cospre_HideAllCostumes_Others = false;
                    break;
                case 2: // Effect
                    mgr.Effect_HideAllPlayers = false;
                    mgr.Effect_HideMinorFX = false;
                    mgr.Effect_HideHealFX = false;
                    mgr.Effect_HideWeaponFX = false;
                    mgr.Effect_HideMonsterFX = false;
                    mgr.Effect_HideTargetFX = false;
                    mgr.Effect_HideHandTrailFX = false;
                    mgr.Effect_HideCapeFX = false;
                    mgr.Effect_HideCastFX = false;
                    mgr.Effect_HideNovaFX = false;
                    mgr.Effect_CameraShakeStrength = 1.0f;
                    break;
                case 3: // Graphic
                    mgr.Graphic_FPS = 120;
                    mgr.Graphic_CameraZoom = 10;
                    mgr.Graphic_TextureQuality = 1;
                    mgr.Graphic_CameraFar = 1.0f;
                    mgr.Graphic_Quality = 0.5f;
                    break;
                case 4: // Graphic2
                    mgr.Graphic2_SkillAreaSens = 0.5f;
                    mgr.Graphic2_CameraSens = 0.5f;
                    mgr.Graphic2_ZButtonSize = 0.5f;
                    mgr.Graphic2_PartyUIScale = 0.5f;
                    mgr.Graphic2_SkillBarSize = 0.5f;
                    KOUIManager.Instance?.ApplyUIScalingAndMargins();
                    MobileSkillBar.Instance?.ApplyZButtonScale();
                    break;
                case 5: // Graphic3
                    mgr.Graphic3_PostExposure = 0.5f;
                    mgr.Graphic3_Contrast = 0.5f;
                    mgr.Graphic3_UIExpandWidth = 1.0f;
                    mgr.Graphic3_UIExpandHeight = 1.0f;
                    mgr.Graphic3_UIScale = 0.5f;
                    KOUIManager.Instance?.ApplyUIScalingAndMargins();
                    KOUIManager.Instance?.ApplyPostProcessingSettings();
                    break;
                case 6: // Block
                    mgr.Block_PartyRequests = false;
                    mgr.Block_TradeRequests = false;
                    break;
                case 7: // PK
                    mgr.PK_TargetPriorityPlayer = false;
                    mgr.PK_TargetPriorityMonster = true;
                    mgr.PK_ZFix = false;
                    mgr.PK_ShowHpMpBarHud = false;
                    mgr.PK_ComboHelper = false;
                    mgr.PK_ChangeZAndR = false;
                    mgr.PK_ExtraSkillCount = 0;
                    if (MobileSkillBar.Instance != null)
                    {
                        MobileSkillBar.Instance.ApplyZAndRLayout();
                        MobileSkillBar.Instance.ApplyExtraSkillsActiveState();
                        MobileSkillBar.Instance.ResetPositions();
                    }
                    break;
                case 8: // Sound
                    mgr.Sound_MuteAll = false;
                    mgr.Sound_MuteWalk = false;
                    mgr.Sound_Background = 0.5f;
                    mgr.Sound_Skill = 0.5f;
                    break;
                case 9: // PM Block
                    PlayerPrefs.SetString("Opt_PMBlockList", "");
                    mgr.LoadSettings();
                    break;
                case 10: // Hide
                    mgr.Hide_NamePlates = false;
                    mgr.Hide_FriendsNamePlates = false;
                    mgr.Hide_AllCapes = false;
                    mgr.Hide_WingMe = false;
                    mgr.Hide_TargetMark = false;
                    mgr.Hide_LeaderMark = false;
                    mgr.Hide_PlayerShadow = false;
                    mgr.Hide_UIKillAnim = false;
                    mgr.Hide_RedHitScreen = false;
                    mgr.Hide_GrayScreen = false;
                    mgr.Hide_DamageTextActive = true;
                    break;
                case 11: // Mod
                    mgr.Mod_DLCAccepted = false;
                    break;
                case 12: // Language
                    mgr.Lang_GameLanguage = 0;
                    mgr.Lang_NoticeLanguage = 0;
                    break;
            }
            mgr.SaveSettings();
        }

        private void RefreshAllValues()
        {
            if (_pageContentContainers.Count > 0)
            {
                for (int i = 0; i < _pageContentContainers.Count; i++)
                {
                    var container = _pageContentContainers[i];
                    if (container != null)
                    {
                        foreach (Transform child in container)
                        {
                            Destroy(child.gameObject);
                        }
                        BuildDynamicPageContent(i, container.gameObject);
                    }
                }
            }
        }

        private void ApplyThemeToPrefab()
        {
            if (KOUIManager.Instance == null) return;

            // 1. Root background
            var mainImg = GetComponent<Image>();
            if (mainImg != null)
            {
                mainImg.sprite = KOUIManager.Instance.GetSkillThemePanelBgSprite(
                    "game_options_main_bg", 360, 450, 0,
                    new Color(0.12f, 0.10f, 0.08f, 0.98f), // Dark brown top
                    new Color(0.04f, 0.04f, 0.04f, 0.98f), // Black bottom
                    new Color(0.6f, 0.48f, 0.22f, 0.9f),   // Amber gold border
                    2
                );
                mainImg.color = Color.white;
            }

            // 2. Content area background
            var contentImg = transform.Find("ContentArea")?.GetComponent<Image>();
            if (contentImg != null)
            {
                contentImg.sprite = KOUIManager.Instance.GetSkillThemePanelBgSprite(
                    "game_options_content_bg", 230, 395, 0,
                    new Color(0.06f, 0.05f, 0.04f, 0.98f),
                    new Color(0.02f, 0.02f, 0.02f, 0.98f),
                    new Color(0.48f, 0.38f, 0.22f, 0.5f),
                    1
                );
                contentImg.color = Color.white;
            }

            // 3. Tab buttons
            for (int i = 0; i < _tabNames.Length; i++)
            {
                var tabBtn = transform.Find($"Sidebar/Tab_{i}");
                if (tabBtn != null)
                {
                    var img = tabBtn.GetComponent<Image>();
                    if (img != null)
                    {
                        img.sprite = KOUIManager.Instance.GetFadeGradientSprite(
                            $"options_tab_fade_{i}", 100, 32,
                            _colorBtnNormal, _colorBtnNormal, 0
                        );
                        img.color = Color.white;
                    }
                }
            }

            // 4. Close and Reset buttons
            var closeImg = transform.Find("Header/btn_close")?.GetComponent<Image>();
            if (closeImg != null)
            {
                closeImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("opt_btn_close", 18, 18, 9, Color.clear, Color.clear, 0);
                closeImg.color = Color.white;
            }
            var resetImg = transform.Find("btn_reset")?.GetComponent<Image>();
            if (resetImg != null)
            {
                resetImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "opt_btn_reset", 244, 25, 4,
                    new Color(0.18f, 0.12f, 0.10f, 1f),
                    new Color(0.75f, 0.63f, 0.38f, 0.5f),
                    1
                );
                resetImg.color = Color.white;
            }

            // 5. Toggles
            var toggles = GetComponentsInChildren<Toggle>(true);
            foreach (var t in toggles)
            {
                var toggleImg = t.GetComponent<Image>();
                if (toggleImg != null)
                {
                    toggleImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_toggle_bg", 18, 18, 4,
                        new Color(0.05f, 0.04f, 0.04f, 1f),
                        new Color(0.75f, 0.63f, 0.38f, 0.5f),
                        1
                    );
                    toggleImg.color = Color.white;
                }
                var tickImg = t.graphic as Image;
                if (tickImg != null)
                {
                    tickImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_toggle_tick", 10, 10, 2,
                        new Color(0.95f, 0.85f, 0.35f, 1f),
                        Color.clear,
                        0
                    );
                    tickImg.color = Color.white;
                }
            }

            // 6. Sliders
            var sliders = GetComponentsInChildren<Slider>(true);
            foreach (var s in sliders)
            {
                var bgImg = s.transform.Find("Background")?.GetComponent<Image>();
                if (bgImg != null)
                {
                    Color barColor = new Color(0.45f, 0.35f, 0.15f, 0.8f);
                    bgImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_slider_bg_styled", 170, 4, 0,
                        barColor, barColor, 0
                    );
                    bgImg.color = Color.white;
                }

                var fillImg = s.fillRect?.GetComponent<Image>();
                if (fillImg != null)
                {
                    Color fillColor = new Color(0.75f, 0.63f, 0.38f, 0.9f);
                    fillImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_slider_fill_styled", 170, 4, 0,
                        fillColor, fillColor, 0
                    );
                    fillImg.color = Color.white;
                }

                var handleImg = s.handleRect?.GetComponent<Image>() ?? s.handleRect?.Find("Visual")?.GetComponent<Image>();
                if (handleImg != null)
                {
                    handleImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_slider_handle", 12, 12, 6,
                        new Color(0.95f, 0.85f, 0.35f, 1f),
                        new Color(0.45f, 0.35f, 0.15f, 0.5f),
                        1
                    );
                    handleImg.color = Color.white;
                }
            }

            // 7. General Image Styling recursively for Inputs, HelpBoxes, and Page Reset buttons
            var allImages = GetComponentsInChildren<Image>(true);
            foreach (var img in allImages)
            {
                string goName = img.gameObject.name;
                if (goName.Contains("HelpBox"))
                {
                    img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_helpbox_bg", 180, 50, 4,
                        new Color(0.12f, 0.10f, 0.09f, 1f),
                        new Color(0.75f, 0.63f, 0.38f, 0.3f),
                        1
                    );
                    img.color = Color.white;
                }
                else if (goName.StartsWith("input_"))
                {
                    img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_input_bg", 180, 28, 4,
                        new Color(0.05f, 0.04f, 0.04f, 1f),
                        new Color(0.75f, 0.63f, 0.38f, 0.4f),
                        1
                    );
                    img.color = Color.white;
                }
                else if (goName == "ResetButton")
                {
                    Color colorCloseMerch = new Color(0.4f, 0.15f, 0.15f, 1f); // Dark red close background
                    Color colorCloseMerchBorder = new Color(0.75f, 0.15f, 0.15f, 1f); // Bright red close border
                    img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_btn_reset_page", 244, 25, 4,
                        colorCloseMerch,
                        colorCloseMerchBorder,
                        1
                    );
                    img.color = Color.white;
                }
                else if (goName.StartsWith("Divider"))
                {
                    img.sprite = KOUIManager.Instance.GetSkillThemeFadingDividerSprite(
                        "opt_sidebar_" + goName, 95, 2,
                        new Color(0.6f, 0.48f, 0.22f, 0.35f)
                    );
                    img.color = Color.white;
                }
                else if (goName.StartsWith("selector_") || goName == "SelectorRow")
                {
                    img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_selector_bg", 180, 28, 4,
                        new Color(0.05f, 0.04f, 0.04f, 1f),
                        new Color(0.75f, 0.63f, 0.38f, 0.4f),
                        1
                    );
                    img.color = Color.white;
                }
                else if (goName == "btn_left")
                {
                    img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_arrow_btn_l", 18, 18, 3,
                        new Color(0.18f, 0.12f, 0.10f, 1f),
                        new Color(0.75f, 0.63f, 0.38f, 0.5f),
                        1
                    );
                    img.color = Color.white;
                }
                else if (goName == "btn_right")
                {
                    img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_arrow_btn_r", 18, 18, 3,
                        new Color(0.18f, 0.12f, 0.10f, 1f),
                        new Color(0.75f, 0.63f, 0.38f, 0.5f),
                        1
                    );
                    img.color = Color.white;
                }
                else if (goName.StartsWith("LangBtn_"))
                {
                    img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_lang_btn_" + goName, 64, 30, 4,
                        new Color(0.15f, 0.12f, 0.10f, 1f),
                        new Color(0.75f, 0.63f, 0.38f, 0.5f),
                        1
                    );
                    img.color = Color.white;
                }
                else if (goName.StartsWith("btn_"))
                {
                    img.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                        "opt_btn_bg_" + goName, 180, 30, 4,
                        new Color(0.15f, 0.12f, 0.10f, 1f),
                        new Color(0.75f, 0.63f, 0.38f, 0.5f),
                        1
                    );
                    img.color = Color.white;
                }
            }
        }

        private void RefreshCapesVisibility()
        {
            if (EntropyOnline.World.EntityManager.Instance != null)
            {
                EntropyOnline.World.EntityManager.Instance.RefreshAllCapesVisibility();
            }
            if (EntropyOnline.World.WorldBuilder.Instance != null)
            {
                EntropyOnline.World.WorldBuilder.Instance.RefreshLocalPlayerCloak();
            }
        }

        private void RefreshPrefabUIValues()
        {
            var mgr = GameOptionsManager.Instance;
            if (mgr == null) return;

            // Page 0: Looting
            SetToggleValue("toggle_low_class", mgr.Loot_LowClass);
            SetToggleValue("toggle_middle_class", mgr.Loot_MiddleClass);
            SetToggleValue("toggle_high_class", mgr.Loot_HighClass);
            SetToggleValue("toggle_potion", mgr.Loot_Potion);
            SetInputValue("input_sell_price", mgr.Loot_SellPrice.ToString());

            // Page 1: Cospre
            SetToggleValue("toggle_wing_me", mgr.Cospre_HideWing_Me);
            SetToggleValue("toggle_fairy_me", mgr.Cospre_HideFairy_Me);
            SetToggleValue("toggle_costume_me", mgr.Cospre_HideCostumeArmor_Me);
            SetToggleValue("toggle_wing_others", mgr.Cospre_HideWing_Others);
            SetToggleValue("toggle_gloves_others", mgr.Cospre_HideGloves_Others);
            SetToggleValue("toggle_fairy_others", mgr.Cospre_HideFairy_Others);
            SetToggleValue("toggle_costume_others", mgr.Cospre_HideAllCostumes_Others);

            // Page 2: Effect
            SetToggleValue("toggle_hide_all_players", mgr.Effect_HideAllPlayers);
            SetToggleValue("toggle_hide_minor", mgr.Effect_HideMinorFX);
            SetToggleValue("toggle_hide_heal", mgr.Effect_HideHealFX);
            SetToggleValue("toggle_hide_weapon", mgr.Effect_HideWeaponFX);
            SetToggleValue("toggle_hide_monster", mgr.Effect_HideMonsterFX);
            SetToggleValue("toggle_hide_target", mgr.Effect_HideTargetFX);
            SetToggleValue("toggle_hide_trail", mgr.Effect_HideHandTrailFX);
            SetToggleValue("toggle_hide_cape", mgr.Effect_HideCapeFX);
            SetToggleValue("toggle_hide_cast", mgr.Effect_HideCastFX);
            SetToggleValue("toggle_hide_nova", mgr.Effect_HideNovaFX);
            SetSliderValue("slider_camera_shake", mgr.Effect_CameraShakeStrength);

            // Page 3: Graphic
            SetSliderValue("slider_camera_far", mgr.Graphic_CameraFar);
            SetSliderValue("slider_quality", mgr.Graphic_Quality);

            // Page 4: Graphic2
            SetSliderValue("slider_skill_area", mgr.Graphic2_SkillAreaSens);
            SetSliderValue("slider_camera_sens", mgr.Graphic2_CameraSens);
            SetSliderValue("slider_z_size", mgr.Graphic2_ZButtonSize);
            SetSliderValue("slider_party_scale", mgr.Graphic2_PartyUIScale);
            SetSliderValue("slider_skill_bar", mgr.Graphic2_SkillBarSize);

            // Page 5: Graphic3
            SetSliderValue("slider_exposure", mgr.Graphic3_PostExposure);
            SetSliderValue("slider_contrast", mgr.Graphic3_Contrast);
            SetSliderValue("slider_ui_scale", mgr.Graphic3_UIScale);

            // Page 6: Block
            SetToggleValue("toggle_block_party", mgr.Block_PartyRequests);
            SetToggleValue("toggle_block_trade", mgr.Block_TradeRequests);

            // Page 7: PK
            SetToggleValue("toggle_priority_player", mgr.PK_TargetPriorityPlayer);
            SetToggleValue("toggle_priority_monster", mgr.PK_TargetPriorityMonster);
            SetToggleValue("toggle_z_fix", mgr.PK_ZFix);
            SetToggleValue("toggle_show_hud_bars", mgr.PK_ShowHpMpBarHud);
            SetToggleValue("toggle_combo_helper", mgr.PK_ComboHelper);
            SetToggleValue("toggle_change_z_r", mgr.PK_ChangeZAndR);
            SetSelectorValue("selector_extra_skills", mgr.PK_ExtraSkillCount.ToString());

            // Page 8: Sound
            SetSliderValue("slider_bg_sound", mgr.Sound_Background);
            SetSliderValue("slider_skill_sound", mgr.Sound_Skill);
        }

        private void UpdateMoveButtonUI(bool isEditMode)
        {
            UpdateSingleButtonUI(FindGameObjectInChildren(gameObject, "btn_move_skill_slots"), isEditMode);
            UpdateSingleButtonUI(GameObject.Find("btn_move_skill_slots"), isEditMode);
        }

        private void UpdateSingleButtonUI(GameObject btnObj, bool isEditMode)
        {
            if (btnObj == null) return;

            var txt = btnObj.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = isEditMode ? "Save Skill Slots" : "Move Skill Slots";
            }
            else
            {
                var tmp = btnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = isEditMode ? "Save Skill Slots" : "Move Skill Slots";
                }
            }

            var img = btnObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = isEditMode ? Color.green : Color.white;
            }
        }

        private void SetToggleValueSafe(string name, bool value)
        {
            SetToggleValue(name, value);
            if (name == "toggle_priority_player") SetToggleValue("toggle_target_priority_player", value);
            if (name == "toggle_priority_monster") SetToggleValue("toggle_target_priority_monster", value);
        }

        private void SetToggleValue(string goName, bool value)
        {
            var toggle = FindComponentInChildren<Toggle>(goName);
            if (toggle != null) toggle.SetIsOnWithoutNotify(value);
        }

        private void SetSliderValue(string goName, float value)
        {
            var slider = FindComponentInChildren<Slider>(goName);
            if (slider != null) slider.SetValueWithoutNotify(value);
        }

        private void SetInputValue(string goName, string value)
        {
            var input = FindComponentInChildren<TMP_InputField>(goName);
            if (input != null) input.SetTextWithoutNotify(value);
        }

        private void SetSelectorValue(string goName, string value)
        {
            var selectorGo = FindGameObjectInChildren(gameObject, goName);
            if (selectorGo == null) return;
            var textTrans = selectorGo.transform.Find("Text") ?? selectorGo.transform.Find("TextMeshProUGUI");
            if (textTrans != null)
            {
                var txt = textTrans.GetComponent<TextMeshProUGUI>();
                if (txt != null)
                {
                    txt.text = value;
                }
                else
                {
                    var legacyTxt = textTrans.GetComponent<Text>();
                    if (legacyTxt != null)
                    {
                        legacyTxt.text = value;
                    }
                }
            }
        }
    }
}
