using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EntropyOnline.UI
{
    public class GameOptionsManager : MonoBehaviour
    {
        private static GameOptionsManager _instance;
        public static GameOptionsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("GameOptionsManager");
                    _instance = go.AddComponent<GameOptionsManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // --- 1. Looting Option ---
        public bool Loot_LowClass { get; set; } = true;
        public bool Loot_MiddleClass { get; set; } = true;
        public bool Loot_HighClass { get; set; } = true;
        public bool Loot_Potion { get; set; } = true;
        public int Loot_SellPrice { get; set; } = 0;

        // --- 2. Cospre Option ---
        public bool Cospre_HideWing_Me { get; set; } = false;
        public bool Cospre_HideFairy_Me { get; set; } = false;
        public bool Cospre_HideCostumeArmor_Me { get; set; } = false;
        public bool Cospre_HideWing_Others { get; set; } = false;
        public bool Cospre_HideGloves_Others { get; set; } = false;
        public bool Cospre_HideFairy_Others { get; set; } = false;
        public bool Cospre_HideAllCostumes_Others { get; set; } = false;

        // --- 3. Effect Option ---
        public bool Effect_HideAllPlayers { get; set; } = false;
        public bool Effect_HideMinorFX { get; set; } = false;
        public bool Effect_HideHealFX { get; set; } = false;
        public bool Effect_HideWeaponFX { get; set; } = false;
        public bool Effect_HideMonsterFX { get; set; } = false;
        public bool Effect_HideTargetFX { get; set; } = false;
        public bool Effect_HideHandTrailFX { get; set; } = false;
        public bool Effect_HideCapeFX { get; set; } = false;
        public bool Effect_HideCastFX { get; set; } = false;
        public bool Effect_HideNovaFX { get; set; } = false;
        public float Effect_CameraShakeStrength { get; set; } = 1.0f;

        // --- 4. Graphic Option ---
        public int Graphic_FPS { get; set; } = 120;
        public int Graphic_CameraZoom { get; set; } = 0;
        public int Graphic_TextureQuality { get; set; } = 1; // 0: High, 1: Medium, 2: Low
        public float Graphic_CameraFar { get; set; } = 1.0f;
        public float Graphic_Quality { get; set; } = 0.5f;

        // --- 5. Graphic Option2 ---
        public float Graphic2_SkillAreaSens { get; set; } = 0.5f;
        public float Graphic2_CameraSens { get; set; } = 0.5f;
        public float Graphic2_ZButtonSize { get; set; } = 0.5f;
        public float Graphic2_PartyUIScale { get; set; } = 0.5f;
        public float Graphic2_SkillBarSize { get; set; } = 0.5f;

        // --- 6. Graphic Option3 ---
        public float Graphic3_PostExposure { get; set; } = 0.5f;
        public float Graphic3_Contrast { get; set; } = 0.5f;
        public float Graphic3_UIExpandWidth { get; set; } = 1.0f;
        public float Graphic3_UIExpandHeight { get; set; } = 1.0f;
        public float Graphic3_UIScale { get; set; } = 0.5f;

        // --- 7. Block Options ---
        public bool Block_PartyRequests { get; set; } = false;
        public bool Block_TradeRequests { get; set; } = false;

        // --- 8. PK Zone Option ---
        public bool PK_TargetPriorityPlayer { get; set; } = false;
        public bool PK_TargetPriorityMonster { get; set; } = true;
        public bool PK_ZFix { get; set; } = false;
        public bool PK_ShowHpMpBarHud { get; set; } = false;
        public bool PK_ComboHelper { get; set; } = false;
        public bool PK_ChangeZAndR { get; set; } = false;
        public int PK_ExtraSkillCount { get; set; } = 0;

        // --- 9. Sound Option ---
        public bool Sound_MuteAll { get; set; } = false;
        public bool Sound_MuteWalk { get; set; } = false;
        public float Sound_Background { get; set; } = 0.5f;
        public float Sound_Skill { get; set; } = 0.5f;

        // --- 10. PM Block ---
        private HashSet<string> _pmBlockList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public IEnumerable<string> PMBlockList => _pmBlockList;
        private HashSet<string> _playersWhoBlockedMe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // --- 11. Hide Option ---
        public bool Hide_NamePlates { get; set; } = false;
        public bool Hide_FriendsNamePlates { get; set; } = false;
        public bool Hide_AllCapes { get; set; } = false;
        public bool Hide_WingMe { get; set; } = false;
        public bool Hide_TargetMark { get; set; } = false;
        public bool Hide_LeaderMark { get; set; } = false;
        public bool Hide_PlayerShadow { get; set; } = false;
        public bool Hide_UIKillAnim { get; set; } = false;
        public bool Hide_RedHitScreen { get; set; } = false;
        public bool Hide_GrayScreen { get; set; } = false;
        public bool Hide_DamageTextActive { get; set; } = true;

        // --- 12. Mod Option ---
        public bool Mod_DLCAccepted { get; set; } = false;

        // --- 13. Language Settings ---
        public int Lang_GameLanguage { get; set; } = 0; // 0: English, 1: Español, 2: Türkçe
        public int Lang_NoticeLanguage { get; set; } = 0; // 0: English, 1: Español, 2: Türkçe

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }

        public void LoadSettings()
        {
            Loot_LowClass = PlayerPrefs.GetInt("Opt_Loot_LowClass", 1) == 1;
            Loot_MiddleClass = PlayerPrefs.GetInt("Opt_Loot_MiddleClass", 1) == 1;
            Loot_HighClass = PlayerPrefs.GetInt("Opt_Loot_HighClass", 1) == 1;
            Loot_Potion = PlayerPrefs.GetInt("Opt_Loot_Potion", 1) == 1;
            Loot_SellPrice = PlayerPrefs.GetInt("Opt_Loot_SellPrice", 0);

            Cospre_HideWing_Me = PlayerPrefs.GetInt("Opt_Cospre_HideWing_Me", 0) == 1;
            Cospre_HideFairy_Me = PlayerPrefs.GetInt("Opt_Cospre_HideFairy_Me", 0) == 1;
            Cospre_HideCostumeArmor_Me = PlayerPrefs.GetInt("Opt_Cospre_HideCostumeArmor_Me", 0) == 1;
            Cospre_HideWing_Others = PlayerPrefs.GetInt("Opt_Cospre_HideWing_Others", 0) == 1;
            Cospre_HideGloves_Others = PlayerPrefs.GetInt("Opt_Cospre_HideGloves_Others", 0) == 1;
            Cospre_HideFairy_Others = PlayerPrefs.GetInt("Opt_Cospre_HideFairy_Others", 0) == 1;
            Cospre_HideAllCostumes_Others = PlayerPrefs.GetInt("Opt_Cospre_HideAllCostumes_Others", 0) == 1;

            Effect_HideAllPlayers = PlayerPrefs.GetInt("Opt_Effect_HideAllPlayers", 0) == 1;
            Effect_HideMinorFX = PlayerPrefs.GetInt("Opt_Effect_HideMinorFX", 0) == 1;
            Effect_HideHealFX = PlayerPrefs.GetInt("Opt_Effect_HideHealFX", 0) == 1;
            Effect_HideWeaponFX = PlayerPrefs.GetInt("Opt_Effect_HideWeaponFX", 0) == 1;
            Effect_HideMonsterFX = PlayerPrefs.GetInt("Opt_Effect_HideMonsterFX", 0) == 1;
            Effect_HideTargetFX = PlayerPrefs.GetInt("Opt_Effect_HideTargetFX", 0) == 1;
            Effect_HideHandTrailFX = PlayerPrefs.GetInt("Opt_Effect_HideHandTrailFX", 0) == 1;
            Effect_HideCapeFX = PlayerPrefs.GetInt("Opt_Effect_HideCapeFX", 0) == 1;
            Effect_HideCastFX = PlayerPrefs.GetInt("Opt_Effect_HideCastFX", 0) == 1;
            Effect_HideNovaFX = PlayerPrefs.GetInt("Opt_Effect_HideNovaFX", 0) == 1;
            Effect_CameraShakeStrength = PlayerPrefs.GetFloat("Opt_Effect_CameraShakeStrength", 1.0f);

            Graphic_FPS = PlayerPrefs.GetInt("Opt_Graphic_FPS", 120);
            Graphic_CameraZoom = Mathf.Clamp(PlayerPrefs.GetInt("Opt_Graphic_CameraZoom", 0), -1, 10);
            Graphic_TextureQuality = PlayerPrefs.GetInt("Opt_Graphic_TextureQuality", 1);
            Graphic_CameraFar = PlayerPrefs.GetFloat("Opt_Graphic_CameraFar", 1.0f);
            Graphic_Quality = PlayerPrefs.GetFloat("Opt_Graphic_Quality", 0.5f);

            Graphic2_SkillAreaSens = PlayerPrefs.GetFloat("Opt_Graphic2_SkillAreaSens", 0.5f);
            Graphic2_CameraSens = PlayerPrefs.GetFloat("Opt_Graphic2_CameraSens", 0.5f);
            Graphic2_ZButtonSize = PlayerPrefs.GetFloat("Opt_Graphic2_ZButtonSize", 0.5f);
            Graphic2_PartyUIScale = PlayerPrefs.GetFloat("Opt_Graphic2_PartyUIScale", 0.5f);
            Graphic2_SkillBarSize = PlayerPrefs.GetFloat("Opt_Graphic2_SkillBarSize", 0.5f);

            Graphic3_PostExposure = PlayerPrefs.GetFloat("Opt_Graphic3_PostExposure", 0.5f);
            Graphic3_Contrast = PlayerPrefs.GetFloat("Opt_Graphic3_Contrast", 0.5f);
            Graphic3_UIExpandWidth = PlayerPrefs.GetFloat("Opt_Graphic3_UIExpandWidth", 1.0f);
            Graphic3_UIExpandHeight = PlayerPrefs.GetFloat("Opt_Graphic3_UIExpandHeight", 1.0f);
            Graphic3_UIScale = PlayerPrefs.GetFloat("Opt_Graphic3_UIScale", 0.5f);

            Block_PartyRequests = PlayerPrefs.GetInt("Opt_Block_PartyRequests", 0) == 1;
            Block_TradeRequests = PlayerPrefs.GetInt("Opt_Block_TradeRequests", 0) == 1;

            PK_TargetPriorityPlayer = PlayerPrefs.GetInt("Opt_PK_TargetPriorityPlayer", 0) == 1;
            PK_TargetPriorityMonster = PlayerPrefs.GetInt("Opt_PK_TargetPriorityMonster", 1) == 1;
            PK_ZFix = PlayerPrefs.GetInt("Opt_PK_ZFix", 0) == 1;
            PK_ShowHpMpBarHud = PlayerPrefs.GetInt("Opt_PK_ShowHpMpBarHud", 0) == 1;
            PK_ComboHelper = PlayerPrefs.GetInt("Opt_PK_ComboHelper", 0) == 1;
            PK_ChangeZAndR = PlayerPrefs.GetInt("Opt_PK_ChangeZAndR", 0) == 1;
            PK_ExtraSkillCount = PlayerPrefs.GetInt("Opt_PK_ExtraSkillCount", 0);

            Sound_MuteAll = PlayerPrefs.GetInt("Opt_Sound_MuteAll", 0) == 1;
            Sound_MuteWalk = PlayerPrefs.GetInt("Opt_Sound_MuteWalk", 0) == 1;
            Sound_Background = PlayerPrefs.GetFloat("Opt_Sound_Background", 0.5f);
            Sound_Skill = PlayerPrefs.GetFloat("Opt_Sound_Skill", 0.5f);



            Hide_NamePlates = PlayerPrefs.GetInt("Opt_Hide_NamePlates", 0) == 1;
            Hide_FriendsNamePlates = PlayerPrefs.GetInt("Opt_Hide_FriendsNamePlates", 0) == 1;
            Hide_AllCapes = PlayerPrefs.GetInt("Opt_Hide_AllCapes", 0) == 1;
            Hide_WingMe = PlayerPrefs.GetInt("Opt_Hide_WingMe", 0) == 1;
            Hide_TargetMark = PlayerPrefs.GetInt("Opt_Hide_TargetMark", 0) == 1;
            Hide_LeaderMark = PlayerPrefs.GetInt("Opt_Hide_LeaderMark", 0) == 1;
            Hide_PlayerShadow = PlayerPrefs.GetInt("Opt_Hide_PlayerShadow", 0) == 1;
            Hide_UIKillAnim = PlayerPrefs.GetInt("Opt_Hide_UIKillAnim", 0) == 1;
            Hide_RedHitScreen = PlayerPrefs.GetInt("Opt_Hide_RedHitScreen", 0) == 1;
            Hide_GrayScreen = PlayerPrefs.GetInt("Opt_Hide_GrayScreen", 0) == 1;
            Hide_DamageTextActive = PlayerPrefs.GetInt("Opt_Hide_DamageTextActive", 1) == 1;

            Mod_DLCAccepted = PlayerPrefs.GetInt("Opt_Mod_DLCAccepted", 0) == 1;

            Lang_GameLanguage = PlayerPrefs.GetInt("Opt_Lang_GameLanguage", 0);
            Lang_NoticeLanguage = PlayerPrefs.GetInt("Opt_Lang_NoticeLanguage", 0);

            ApplySettings();
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetInt("Opt_Loot_LowClass", Loot_LowClass ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Loot_MiddleClass", Loot_MiddleClass ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Loot_HighClass", Loot_HighClass ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Loot_Potion", Loot_Potion ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Loot_SellPrice", Loot_SellPrice);

            PlayerPrefs.SetInt("Opt_Cospre_HideWing_Me", Cospre_HideWing_Me ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Cospre_HideFairy_Me", Cospre_HideFairy_Me ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Cospre_HideCostumeArmor_Me", Cospre_HideCostumeArmor_Me ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Cospre_HideWing_Others", Cospre_HideWing_Others ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Cospre_HideGloves_Others", Cospre_HideGloves_Others ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Cospre_HideFairy_Others", Cospre_HideFairy_Others ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Cospre_HideAllCostumes_Others", Cospre_HideAllCostumes_Others ? 1 : 0);

            PlayerPrefs.SetInt("Opt_Effect_HideAllPlayers", Effect_HideAllPlayers ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Effect_HideMinorFX", Effect_HideMinorFX ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Effect_HideHealFX", Effect_HideHealFX ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Effect_HideWeaponFX", Effect_HideWeaponFX ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Effect_HideMonsterFX", Effect_HideMonsterFX ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Effect_HideTargetFX", Effect_HideTargetFX ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Effect_HideHandTrailFX", Effect_HideHandTrailFX ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Effect_HideCapeFX", Effect_HideCapeFX ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Effect_HideCastFX", Effect_HideCastFX ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Effect_HideNovaFX", Effect_HideNovaFX ? 1 : 0);
            PlayerPrefs.SetFloat("Opt_Effect_CameraShakeStrength", Effect_CameraShakeStrength);

            PlayerPrefs.SetInt("Opt_Graphic_FPS", Graphic_FPS);
            PlayerPrefs.SetInt("Opt_Graphic_CameraZoom", Graphic_CameraZoom);
            PlayerPrefs.SetInt("Opt_Graphic_TextureQuality", Graphic_TextureQuality);
            PlayerPrefs.SetFloat("Opt_Graphic_CameraFar", Graphic_CameraFar);
            PlayerPrefs.SetFloat("Opt_Graphic_Quality", Graphic_Quality);

            PlayerPrefs.SetFloat("Opt_Graphic2_SkillAreaSens", Graphic2_SkillAreaSens);
            PlayerPrefs.SetFloat("Opt_Graphic2_CameraSens", Graphic2_CameraSens);
            PlayerPrefs.SetFloat("Opt_Graphic2_ZButtonSize", Graphic2_ZButtonSize);
            PlayerPrefs.SetFloat("Opt_Graphic2_PartyUIScale", Graphic2_PartyUIScale);
            PlayerPrefs.SetFloat("Opt_Graphic2_SkillBarSize", Graphic2_SkillBarSize);

            PlayerPrefs.SetFloat("Opt_Graphic3_PostExposure", Graphic3_PostExposure);
            PlayerPrefs.SetFloat("Opt_Graphic3_Contrast", Graphic3_Contrast);
            PlayerPrefs.SetFloat("Opt_Graphic3_UIExpandWidth", Graphic3_UIExpandWidth);
            PlayerPrefs.SetFloat("Opt_Graphic3_UIExpandHeight", Graphic3_UIExpandHeight);
            PlayerPrefs.SetFloat("Opt_Graphic3_UIScale", Graphic3_UIScale);

            PlayerPrefs.SetInt("Opt_Block_PartyRequests", Block_PartyRequests ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Block_TradeRequests", Block_TradeRequests ? 1 : 0);

            PlayerPrefs.SetInt("Opt_PK_TargetPriorityPlayer", PK_TargetPriorityPlayer ? 1 : 0);
            PlayerPrefs.SetInt("Opt_PK_TargetPriorityMonster", PK_TargetPriorityMonster ? 1 : 0);
            PlayerPrefs.SetInt("Opt_PK_ZFix", PK_ZFix ? 1 : 0);
            PlayerPrefs.SetInt("Opt_PK_ShowHpMpBarHud", PK_ShowHpMpBarHud ? 1 : 0);
            PlayerPrefs.SetInt("Opt_PK_ComboHelper", PK_ComboHelper ? 1 : 0);
            PlayerPrefs.SetInt("Opt_PK_ChangeZAndR", PK_ChangeZAndR ? 1 : 0);
            PlayerPrefs.SetInt("Opt_PK_ExtraSkillCount", PK_ExtraSkillCount);

            PlayerPrefs.SetInt("Opt_Sound_MuteAll", Sound_MuteAll ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Sound_MuteWalk", Sound_MuteWalk ? 1 : 0);
            PlayerPrefs.SetFloat("Opt_Sound_Background", Sound_Background);
            PlayerPrefs.SetFloat("Opt_Sound_Skill", Sound_Skill);



            PlayerPrefs.SetInt("Opt_Hide_NamePlates", Hide_NamePlates ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Hide_FriendsNamePlates", Hide_FriendsNamePlates ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Hide_AllCapes", Hide_AllCapes ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Hide_WingMe", Hide_WingMe ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Hide_TargetMark", Hide_TargetMark ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Hide_LeaderMark", Hide_LeaderMark ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Hide_PlayerShadow", Hide_PlayerShadow ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Hide_UIKillAnim", Hide_UIKillAnim ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Hide_RedHitScreen", Hide_RedHitScreen ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Hide_GrayScreen", Hide_GrayScreen ? 1 : 0);
            PlayerPrefs.SetInt("Opt_Hide_DamageTextActive", Hide_DamageTextActive ? 1 : 0);

            PlayerPrefs.SetInt("Opt_Mod_DLCAccepted", Mod_DLCAccepted ? 1 : 0);

            PlayerPrefs.SetInt("Opt_Lang_GameLanguage", Lang_GameLanguage);
            PlayerPrefs.SetInt("Opt_Lang_NoticeLanguage", Lang_NoticeLanguage);

            PlayerPrefs.Save();
            ApplySettings();
        }

        public void ApplySettings()
        {
            // FPS limit
            Application.targetFrameRate = Graphic_FPS;

            // Texture Quality (0: High/Full resolution, 1: Half, 2: Quarter)
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(Graphic_TextureQuality, 0, 3);

            // General Quality Settings (Render Scale and MSAA)
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null)
            {
                if (Graphic_Quality <= 0.25f) // Low
                {
                    urpAsset.renderScale = 0.7f;
                    urpAsset.msaaSampleCount = 1;
                }
                else if (Graphic_Quality <= 0.50f) // Medium
                {
                    urpAsset.renderScale = 1.0f;
                    urpAsset.msaaSampleCount = 2;
                }
                else if (Graphic_Quality <= 0.75f) // High
                {
                    urpAsset.renderScale = 2.0f;
                    urpAsset.msaaSampleCount = 4;
                }
                else // Ultra
                {
                    urpAsset.renderScale = 3.0f;
                    urpAsset.msaaSampleCount = 8;
                }
            }

            // Camera Far Clip (Draw distance: 50m to 300m) with Soft Edge Fog
            var mainCam = UnityEngine.Camera.main;
            if (mainCam != null)
            {
                float farDistance = Mathf.Lerp(50f, 300f, Graphic_CameraFar);
                mainCam.farClipPlane = farDistance;

                // Fog only starts at 85% of view distance to keep nearby view crystal clear, while hiding pop-in
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = farDistance * 0.85f;
                RenderSettings.fogEndDistance = farDistance;
            }

            // Sound volumes
            if (Sound_MuteAll)
            {
                AudioListener.volume = 0f;
            }
            else
            {
                // Simple mapping: use background volume as global listener volume for now
                AudioListener.volume = Sound_Background;
            }

            // Other settings will be polled dynamically by the game modules
            // e.g., PlayerController checking GameOptionsManager.Instance.Loot_LowClass
        }

        public void AddBlockedPlayer(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return;
            playerName = playerName.Trim();
            _pmBlockList.Add(playerName);
        }

        public void RemoveBlockedPlayer(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return;
            playerName = playerName.Trim();
            _pmBlockList.Remove(playerName);
        }

        public bool IsPlayerBlocked(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return false;
            return _pmBlockList.Contains(playerName.Trim());
        }

        public void AddPlayerWhoBlockedMe(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return;
            playerName = playerName.Trim();
            _playersWhoBlockedMe.Add(playerName);
        }

        public void RemovePlayerWhoBlockedMe(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return;
            playerName = playerName.Trim();
            _playersWhoBlockedMe.Remove(playerName);
        }

        public bool IsPlayerBlockingMe(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return false;
            return _playersWhoBlockedMe.Contains(playerName.Trim());
        }

        public void ResetToDefault()
        {
            Loot_LowClass = true;
            Loot_MiddleClass = true;
            Loot_HighClass = true;
            Loot_Potion = true;
            Loot_SellPrice = 0;

            Cospre_HideWing_Me = false;
            Cospre_HideFairy_Me = false;
            Cospre_HideCostumeArmor_Me = false;
            Cospre_HideWing_Others = false;
            Cospre_HideGloves_Others = false;
            Cospre_HideFairy_Others = false;
            Cospre_HideAllCostumes_Others = false;

            Effect_HideAllPlayers = false;
            Effect_HideMinorFX = false;
            Effect_HideHealFX = false;
            Effect_HideWeaponFX = false;
            Effect_HideMonsterFX = false;
            Effect_HideTargetFX = false;
            Effect_HideHandTrailFX = false;
            Effect_HideCapeFX = false;
            Effect_HideCastFX = false;
            Effect_HideNovaFX = false;
            Effect_CameraShakeStrength = 1.0f;

            Graphic_FPS = 120;
            Graphic_CameraZoom = 0;
            Graphic_TextureQuality = 1;
            Graphic_CameraFar = 1.0f;
            Graphic_Quality = 0.5f;

            Graphic2_SkillAreaSens = 0.5f;
            Graphic2_CameraSens = 0.5f;
            Graphic2_ZButtonSize = 0.5f;
            Graphic2_PartyUIScale = 0.5f;
            Graphic2_SkillBarSize = 0.5f;

            Graphic3_PostExposure = 0.5f;
            Graphic3_Contrast = 0.5f;
            Graphic3_UIExpandWidth = 1.0f;
            Graphic3_UIExpandHeight = 1.0f;
            Graphic3_UIScale = 0.5f;

            Block_PartyRequests = false;
            Block_TradeRequests = false;

            PK_TargetPriorityPlayer = false;
            PK_TargetPriorityMonster = true;
            PK_ZFix = false;
            PK_ShowHpMpBarHud = false;
            PK_ComboHelper = false;
            PK_ChangeZAndR = false;
            PK_ExtraSkillCount = 0;

            Sound_MuteAll = false;
            Sound_MuteWalk = false;
            Sound_Background = 0.5f;
            Sound_Skill = 0.5f;

            _pmBlockList.Clear();

            Hide_NamePlates = false;
            Hide_FriendsNamePlates = false;
            Hide_AllCapes = false;
            Hide_WingMe = false;
            Hide_TargetMark = false;
            Hide_LeaderMark = false;
            Hide_PlayerShadow = false;
            Hide_UIKillAnim = false;
            Hide_RedHitScreen = false;
            Hide_GrayScreen = false;
            Hide_DamageTextActive = true;

            Mod_DLCAccepted = false;

            Lang_GameLanguage = 0;
            Lang_NoticeLanguage = 0;

            SaveSettings();
        }
    }
}
