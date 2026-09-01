using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Import;
using System.Collections.Generic;
using KOImport;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Entropy Online â€” Game Event Handler + Data Binding
    /// 
    /// Bu sÄ±nÄ±f SADECE:
    /// - Sunucu event'lerini dinler (HP/MP/EXP gÃ¼ncelleme, level-up, hasar, vb.)
    /// - KOUIManager Ã¼zerinden KO UI progress bar'larÄ±nÄ± gÃ¼nceller
    /// - Floating text sistemi (combat feedback)
    /// 
    /// UI panelleri KOUIManager tarafÄ±ndan UIF dosyalarÄ±ndan yÃ¼klenir.
    /// Bu sÄ±nÄ±f asla kendi UI oluÅŸturmaz.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }
        public bool IsStealthActive { get; private set; }

        // Floating text sistemi
        private readonly List<FloatingText> _floatingTexts = new();
        private GameObject _floatingTextContainer;
        private Canvas _overlayCanvas;
        private Font _cachedArialFont;

        // Animasyon hedefleri
        private float _targetHpFill;
        private float _targetMpFill;
        private float _targetExpFill;

        // FPS ve Konum güncelleme zamanlayıcıları
        private float _fpsTimer = 0f;
        private int _fpsCount = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (Instance != this) return;
            CreateOverlayCanvas();
            SubscribeToEvents();
            RefreshAllBars();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            UpdateFloatingTexts();

            // --- C++ UIStateBar.cpp birebir: FPS ve Konum Güncellemeleri ---
            _fpsCount++;
            _fpsTimer += Time.deltaTime;
            if (_fpsTimer >= 0.5f)
            {
                float currentFps = _fpsCount / _fpsTimer;
                _fpsCount = 0;
                _fpsTimer = 0f;
                KOUIManager.Instance?.UpdateFPSText(currentFps);
            }

            if (GameManager.Instance != null && KOUIManager.Instance != null)
            {
                KOUIManager.Instance.UpdatePositionText(GameManager.Instance.PlayerPosX, GameManager.Instance.PlayerPosZ);
            }
        }

        // ============================
        // OVERLAY CANVAS (floating text iÃ§in)
        // ============================

        private void CreateOverlayCanvas()
        {
            var canvasObj = new GameObject("HUD_OverlayCanvas");
            canvasObj.transform.SetParent(transform, false);
            _overlayCanvas = canvasObj.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 200; // KO UI'nÄ±n Ã¼stÃ¼nde

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024, 768);
            scaler.matchWidthOrHeight = 1.0f;

            canvasObj.AddComponent<GraphicRaycaster>();

            _floatingTextContainer = new GameObject("FloatingTexts");
            _floatingTextContainer.transform.SetParent(canvasObj.transform, false);
        }

        // ============================
        // EVENT HANDLERs
        // ============================

        private void SubscribeToEvents()
        {
            KOPacketHandler.OnMyInfo += OnMyInfo_KO;
            KOPacketHandler.OnHPChange += OnHpChange_KO;
            KOPacketHandler.OnMSPChange += OnMSpChange_KO;
            KOPacketHandler.OnExpChange += OnExpChange_KO;
            KOPacketHandler.OnLevelChange += OnLevelChange_KO;
            KOPacketHandler.OnAttackResult += OnAttackResult_KO;
            KOPacketHandler.OnDead += OnDead_KO;
            KOPacketHandler.OnMagicProcess += OnMagicProcess_KO;
            KOPacketHandler.OnStateChange += OnStateChange_KO;
            KOPacketHandler.OnZoneAbility += OnZoneAbility_KO;
            KOPacketHandler.OnLoyaltyChange += OnLoyaltyChange_KO;
            KOPacketHandler.OnTargetHP += OnTargetHP_KO;
            // NOT: OnTradeNpc, OnNpcEvent, OnSkillData, OnParty, OnWarehouse, OnItemUpgrade
            // kendi UI sınıfları (ShopUI, KONpcInteractUI, KOSkillTreeManager vb.) tarafından
            // handle ediliyor — duplicate subscription gereksiz.
            KOPacketHandler.OnChat += OnChat_KO;
            KOPacketHandler.OnZoneChange += OnZoneChange_KO;
            KOPacketHandler.OnPointChange += OnPointChange_KO;
            KOPacketHandler.OnItemCountChange += OnItemCountChange_KO;
            KOPacketHandler.OnSkillPtChange += OnSkillPtChange_KO;
            // NOT: OnNpcSay ve OnSelectMsg QuestDialogUI tarafından handle ediliyor
        }

        private void UnsubscribeFromEvents()
        {
            KOPacketHandler.OnMyInfo -= OnMyInfo_KO;
            KOPacketHandler.OnHPChange -= OnHpChange_KO;
            KOPacketHandler.OnMSPChange -= OnMSpChange_KO;
            KOPacketHandler.OnExpChange -= OnExpChange_KO;
            KOPacketHandler.OnLevelChange -= OnLevelChange_KO;
            KOPacketHandler.OnAttackResult -= OnAttackResult_KO;
            KOPacketHandler.OnDead -= OnDead_KO;
            KOPacketHandler.OnMagicProcess -= OnMagicProcess_KO;
            KOPacketHandler.OnStateChange -= OnStateChange_KO;
            KOPacketHandler.OnZoneAbility -= OnZoneAbility_KO;
            KOPacketHandler.OnLoyaltyChange -= OnLoyaltyChange_KO;
            KOPacketHandler.OnTargetHP -= OnTargetHP_KO;
            KOPacketHandler.OnChat -= OnChat_KO;
            KOPacketHandler.OnZoneChange -= OnZoneChange_KO;
            KOPacketHandler.OnPointChange -= OnPointChange_KO;
            KOPacketHandler.OnItemCountChange -= OnItemCountChange_KO;
            KOPacketHandler.OnSkillPtChange -= OnSkillPtChange_KO;
        }

        // ============================
        // KO RAW DATA WRAPPERS
        // ============================

        private void OnMyInfo_KO(byte[] rawData)
        {
            // GameSceneController handles full MyInfo parse â€” HUD just refreshes bars
            RefreshAllBars();
        }

        private void OnHpChange_KO(short maxHp, short curHp)
        {
            OnHpChangeReceived(maxHp, curHp);
        }

        private void OnMSpChange_KO(short maxMp, short curMp)
        {
            OnMSpChangeReceived(maxMp, curMp);
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_MyInfo_EXP (GameProcMain.cpp:3696-3717)
        /// Wire: [opcode][totalExp: uint32]
        /// </summary>
        private void OnExpChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            uint totalExp = r.ReadUInt32();
            OnExpGained((int)totalExp);
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_MyInfo_LevelChange (GameProcMain.cpp:3719-3780)
        /// Wire: [opcode][id:int16][level:byte][bonusPt:byte][skillPt:byte][expNext:int32][exp:int32]
        ///        [maxHp:int16][hp:int16][maxMp:int16][mp:int16][maxWeight:int16][weight:int16]
        /// </summary>
        private void OnLevelChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            short id = r.ReadInt16();
            byte level = r.ReadByte();
            short bonusPt = r.ReadInt16();
            byte skillPt = r.ReadByte();
            int expNext = r.ReadInt32();
            int exp = r.ReadInt32();
            short maxHp = r.ReadInt16();
            short hp = r.ReadInt16();
            short maxMp = r.ReadInt16();
            short mp = r.ReadInt16();
            // maxWeight, weight â€” okunur ama HUD'da kullanÄ±lmaz
            r.ReadInt16(); // maxWeight
            r.ReadInt16(); // weight
            OnLevelUp(id, level, bonusPt, skillPt, expNext, exp, maxHp, hp, maxMp, mp);
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_Attack (GameProcMain.cpp:3213-3328)
        /// Wire: [opcode][type:byte][result:byte][attackerId:int16][targetId:int16]
        /// </summary>
        private void OnAttackResult_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte type = r.ReadByte();   // 0x01=physical, 0x02=magic
            byte result = r.ReadByte(); // 0x00=miss, 0x01=hit, 0x02=kill
            short attackerId = r.ReadInt16();
            short targetId = r.ReadInt16();
            // C++ satÄ±r 3298: 0x0 â†’ miss (damage=0), 0x2 â†’ kill
            int damage = (result == 0) ? 0 : 1; // gerÃ§ek damage HP_CHANGE'den gelir
            bool died = (result == 0x02);
            OnAttackResult(attackerId, targetId, targetId < 10000, damage, 0, died);
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_Dead (GameProcMain.cpp:3330-3364)
        /// Wire: [opcode][targetId:int16]
        /// </summary>
        private void OnDead_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            short targetId = r.ReadInt16();
            OnEntityDeath(targetId, targetId < 10000);
        }

        /// <summary>
        /// Open-KO birebir: MagicSkillMng.cpp MagicPacketRecv()
        /// Wire: [opcode][subOpcode:byte][magicId:uint32][sourceId:int16][targetId:int16]
        ///        [data0-5: int16Ã—6]
        /// subOpcode: 0x01=CASTING, 0x02=FLYING, 0x03=EFFECTING, 0x04=FAIL
        /// </summary>
        private void OnMagicProcess_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte subOp = r.ReadByte();

            // 0x05 (TYPE4BUFFTYPE) ve 0x06 (CANCEL) farklı wire format —
            // EntityManager.HandleMagicProcess_KO'da handle ediliyor.
            if (subOp == 0x05 || subOp == 0x06) return;

            uint magicId = r.ReadUInt32();
            short sourceId = r.ReadInt16();
            short targetId = r.ReadInt16();
            short d0 = r.ReadInt16(), d1 = r.ReadInt16(), d2 = r.ReadInt16();
            short d3 = r.ReadInt16(), d4 = r.ReadInt16(), d5 = r.ReadInt16();

            switch (subOp)
            {
                // packets.h birebir: MAGIC_CASTING=1, FLYING=2, EFFECTING=3, FAIL=4
                // Sunucu command echo ediyor (MagicProcess.cpp:532)
                case 0x01: OnMagicCasting((int)magicId, sourceId, targetId, d0, d1, d2, d3, d4, d5); break;
                case 0x02: OnMagicFlying((int)magicId, sourceId, targetId, d0, d1, d2, d3, d4, d5); break;
                case 0x03: OnMagicEffecting((int)magicId, sourceId, targetId, d0, d1, d2, d3, d4, d5); break;
                case 0x04:
                    // MagicSkillMng.cpp:1963 — Data[3] = fail reason
                    MagicFailReason failReason = (MagicFailReason)d3;
                    OnMagicFail((int)magicId, sourceId, targetId, failReason, d0, d1, d2, d4, d5);
                    break;
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_StateChange
        /// Wire: [opcode][userId:int16][type:byte][value:int32]
        /// type: 1=SIT, 2=PARTY, 3=ACTION, 5=VISIBLE, 7=GM
        /// </summary>
        private void OnStateChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            short userId = r.ReadInt16();
            byte type = r.ReadByte();
            int value = r.ReadInt32();

            var gm = GameManager.Instance;
            bool isMe = gm != null && (userId == (short)gm.CharacterId || userId == gm.CharacterId);            switch (type)
            {
                case 2: // e_SubPacket_State::N3_SP_STATE_CHANGE_RECRUIT_PARTY = 2
                    if (isMe && gm != null)
                    {
                        gm.RecruitParty = (value == 2);
                    }
                    
                    string szMsg = "";
                    if (value == 2) // N3_SP_STATE_CHANGE_RECRUIT_PARTY active
                    {
                        int level = 1;
                        if (isMe)
                        {
                            level = gm.Level;
                        }
                        else
                        {
                            var entityMgr = EntropyOnline.World.EntityManager.Instance;
                            var remotePlayer = entityMgr?.GetRemotePlayer(userId);
                            if (remotePlayer != null)
                            {
                                level = remotePlayer.Level;
                            }
                        }

                        int iLMin = Mathf.Min(level - 8, (int)(level / 1.5f));
                        if (iLMin < 1) iLMin = 1;
                        int iLMax = Mathf.Max(level + 8, (int)(level * 1.5f));
                        if (iLMax > 80) iLMax = 80;

                        szMsg = $"Seeking Party : Level {iLMin} ~ {iLMax}";
                    }

                    if (isMe)
                    {
                        var player = GameObject.FindGameObjectWithTag("Player");
                        if (player == null) player = GameObject.Find("Player");
                        if (player != null)
                        {
                            var fn = player.GetComponent<EntropyOnline.World.FloatingName>();
                            if (fn != null)
                            {
                                fn.SetInfoText(szMsg, new Color(0f, 1f, 0f, 1f));
                            }
                        }
                    }
                    else
                    {
                        var entityMgr = EntropyOnline.World.EntityManager.Instance;
                        if (entityMgr != null)
                        {
                            var remotePlayer = entityMgr.GetRemotePlayer(userId);
                            if (remotePlayer != null && remotePlayer.Root != null)
                            {
                                var fn = remotePlayer.Root.GetComponent<EntropyOnline.World.FloatingName>();
                                if (fn != null)
                                {
                                    fn.SetInfoText(szMsg, new Color(0f, 1f, 0f, 1f));
                                }
                            }
                        }
                    }
                    break;

                case 5: // e_SubPacket_State::N3_SP_STATE_CHANGE_VISIBLE = 5
                    bool invisible = (value > 0);
                    if (isMe)
                    {
                        IsStealthActive = invisible;
                        var player = GameObject.FindGameObjectWithTag("Player");
                        if (player == null) player = GameObject.Find("Player");
                        if (player != null)
                        {
                            SetActorTransparency(player, true, true, invisible);
                        }


                        if (KOUIManager.Instance != null)
                        {
                            if (!invisible)
                            {
                                KOUIManager.Instance.RemoveBuff(108645);
                                KOUIManager.Instance.RemoveBuff(208645);
                                KOUIManager.Instance.RemoveBuff(107645);
                                KOUIManager.Instance.RemoveBuff(207645);
                                KOUIManager.Instance.RemoveBuff(107715);
                                KOUIManager.Instance.RemoveBuff(207715);
                                KOUIManager.Instance.RemoveBuff(107735);
                                KOUIManager.Instance.RemoveBuff(207735);
                            }
                        }
                    }
                    else
                    {
                        var entityMgr = EntropyOnline.World.EntityManager.Instance;
                        if (entityMgr != null)
                        {
                            var remotePlayer = entityMgr.GetRemotePlayer(userId);
                            if (remotePlayer != null && remotePlayer.Root != null)
                            {
                                bool isSameNation = (gm != null && gm.Nation == remotePlayer.Nation);
                                SetActorTransparency(remotePlayer.Root, false, isSameNation, invisible);
                            }

                        }
                    }
                    break;
            }
        }

        private class OriginalMaterialState
        {
            public float surface;
            public float blend;
            public int srcBlend;
            public int dstBlend;
            public int zWrite;
            public int renderQueue;
            public bool isTransparentKeywordActive;
            public float colorAlpha = 1.0f;
            public float baseColorAlpha = 1.0f;

            // Shader caching to bypass asset pipeline locks on Cutout
            public Shader originalShader;
        }

        public void RefreshStealthTransparency(GameObject playerObj, bool isMe, bool isSameNation, bool active)
        {
            if (playerObj == null) return;
            SetActorTransparency(playerObj, isMe, isSameNation, active);
        }

        private readonly System.Collections.Generic.Dictionary<Material, OriginalMaterialState> _originalMaterialCache = 
            new System.Collections.Generic.Dictionary<Material, OriginalMaterialState>();

        private void SetActorTransparency(GameObject go, bool isMe, bool isSameNation, bool invisible)
        {
            if (go == null) return;

            // Keep track of stealth state in the visualizer for both local and friendly players
            var eqVis = go.GetComponent<EntropyOnline.World.KOEquipmentVisualizer>();
            if (eqVis == null) eqVis = go.GetComponentInParent<EntropyOnline.World.KOEquipmentVisualizer>();
            if (eqVis != null)
            {
                eqVis.IsStealthActive = invisible;
                eqVis.SetStealthHelmet(invisible);
            }

            // Enemy rogue becomes completely invisible (hide renderer), friendly rogue becomes transparent
            if (!isMe && !isSameNation)
            {
                bool canSeeStealthEnemy = false;
                var localPlayer = EntropyOnline.Character.PlayerController.Instance;
                if (localPlayer != null && localPlayer.CanSeeStealth)
                {
                    float distance = Vector3.Distance(localPlayer.transform.position, go.transform.position);
                    if (distance <= localPlayer.StealthDetectionRadius)
                    {
                        canSeeStealthEnemy = true;
                    }
                }

                if (!canSeeStealthEnemy)
                {
                    var renderers = go.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r != null) r.enabled = !invisible;
                    }
                    return;
                }
                else
                {
                    // Enforce renderer to be enabled so it can be drawn transparently
                    var renderers = go.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r != null) r.enabled = true;
                    }
                }
            }

            // Local player or friendly players become semi-transparent
            var meshRenderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in meshRenderers)
            {
                if (r == null) continue;

                // Unity Rule: Get copies of materials, modify, and assign back
                var mats = r.materials;
                if (mats == null || mats.Length == 0) continue;

                bool modified = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    if (mat == null) continue;

                    if (invisible)
                    {
                        // Cache original material properties before altering them
                        if (!_originalMaterialCache.ContainsKey(mat))
                        {
                            var state = new OriginalMaterialState
                            {
                                surface = mat.HasProperty("_Surface") ? mat.GetFloat("_Surface") : 0f,
                                blend = mat.HasProperty("_Blend") ? mat.GetFloat("_Blend") : 0f,
                                srcBlend = mat.HasProperty("_SrcBlend") ? mat.GetInt("_SrcBlend") : (int)UnityEngine.Rendering.BlendMode.One,
                                dstBlend = mat.HasProperty("_DstBlend") ? mat.GetInt("_DstBlend") : (int)UnityEngine.Rendering.BlendMode.Zero,
                                zWrite = mat.HasProperty("_ZWrite") ? mat.GetInt("_ZWrite") : 1,
                                renderQueue = mat.renderQueue,
                                isTransparentKeywordActive = mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),
                                colorAlpha = mat.HasProperty("_Color") ? mat.color.a : 1f,
                                baseColorAlpha = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor").a : 1f,
                                originalShader = mat.shader
                            };
                            _originalMaterialCache[mat] = state;
                        }

                        // If the material is already transparent (e.g. glowing weapon effects), skip changing its properties
                        if (_originalMaterialCache[mat].surface > 0.5f)
                        {
                            continue;
                        }



                        if (mat.HasProperty("_Color"))
                        {
                            Color col = mat.color;
                            col.a = 0.4f;
                            mat.color = col;
                        }
                        if (mat.HasProperty("_BaseColor"))
                        {
                            Color col = mat.GetColor("_BaseColor");
                            col.a = 0.4f;
                            mat.SetColor("_BaseColor", col);
                        }

                        // URP Transparency settings
                        mat.SetFloat("_Surface", 1); // 1: Transparent
                        mat.SetFloat("_Blend", 0);   // 0: Alpha Blend
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.renderQueue = 3000;
                        modified = true;
                    }
                    else
                    {
                        // Restore original material properties from cache
                        if (_originalMaterialCache.TryGetValue(mat, out var state))
                        {


                            if (mat.HasProperty("_Color"))
                            {
                                Color col = mat.color;
                                col.a = state.colorAlpha;
                                mat.color = col;
                            }
                            if (mat.HasProperty("_BaseColor"))
                            {
                                Color col = mat.GetColor("_BaseColor");
                                col.a = state.baseColorAlpha;
                                mat.SetColor("_BaseColor", col);
                            }

                            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", state.surface);
                            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", state.blend);
                            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", state.srcBlend);
                            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", state.dstBlend);
                            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", state.zWrite);

                            if (state.isTransparentKeywordActive)
                                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                            else
                                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");

                            mat.renderQueue = state.renderQueue;
                            modified = true;
                        }
                    }
                }

                if (modified)
                {
                    r.materials = mats; // Re-assign back to apply rendering changes
                }
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_ZoneAbilityChange
        /// Wire: [opcode][subOp:byte][canTradeOther:byte][abilityType:byte][canTalkOther:byte][tariff:int16]
        /// </summary>
        private void OnZoneAbility_KO(byte[] rawData)
        {
            // Open-KO birebir: GameProcMain.cpp:8043-8053
            var r = new KOPacketReader(rawData);
            byte subOp = r.ReadByte();                // cpp:8044 — ZONE_ABILITY_UPDATE check
            byte canTradeOtherNation = r.ReadByte();   // cpp:8046
            byte zoneAbilityType = r.ReadByte();       // cpp:8047
            byte canTalkOtherNation = r.ReadByte();    // cpp:8049
            short zoneTariff = r.ReadInt16();           // cpp:8050
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.CanTradeOtherNation = canTradeOtherNation != 0;
                gm.ZoneAbilityType = zoneAbilityType; // cpp:8049 — eZoneAbilityType
                gm.CanTalkOtherNation = canTalkOtherNation != 0;
                gm.ZoneTariff = zoneTariff;
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_MyInfo_RealmPoint (GameProcMain.cpp:3782-3810)
        /// Wire: [opcode][subOpcode:byte][loyalty:uint32][loyaltyMonthly:uint32]
        /// </summary>
        private void OnLoyaltyChange_KO(byte[] rawData)
        {
            // Open-KO birebir: GameProcMain.cpp:3782-3827
            var r = new KOPacketReader(rawData);
            byte subOp = r.ReadByte(); // LOYALTY_CHANGE_NATIONAL=1, LOYALTY_CHANGE_MANNER=2
            var gm = GameManager.Instance;
            if (subOp == 1) // LOYALTY_CHANGE_NATIONAL — cpp:3789
            {
                uint loyalty = r.ReadUInt32();        // cpp:3790
                uint loyaltyMonthly = r.ReadUInt32(); // cpp:3791
                if (gm != null)
                {
                    gm.Loyalty = (int)loyalty;
                    gm.LoyaltyMonthly = (int)loyaltyMonthly;
                }
            }
            else if (subOp == 2) // LOYALTY_CHANGE_MANNER — cpp:3810
            {
                uint manner = r.ReadUInt32(); // cpp:3811
                if (gm != null)
                    gm.MannerPoint = (int)manner;
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_TargetHP (GameProcMain.cpp:4258-4304)
        /// Wire: [opcode][targetId:int16][echo:byte][maxHp:int32][curHp:int32][maxExistHP:int16]
        /// </summary>
        private void OnTargetHP_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            short targetId = r.ReadInt16();
            byte echo = r.ReadByte();
            int maxHp = r.ReadInt32();
            int curHp = r.ReadInt32();
            short hpChange = r.ReadInt16();

            OnTargetHpReceived(targetId, echo, curHp, maxHp, hpChange);
        }

        /// <summary>
        /// WIZ_TRADE_NPC â€” NPC shop aÃ§Ä±lmasÄ±.
        /// Routing: ShopUI handles this via its own event subscription.
        /// </summary>
        private void OnTradeNpc_KO(byte[] rawData)
        {
            // ShopUI kendi event aboneliÄŸiyle handle eder
        }

        /// <summary>
        /// WIZ_NPC_EVENT â€” NPC etkileÅŸim olayÄ±.
        /// Routing: KONpcInteractUI handles this via its own event subscription.
        /// </summary>
        private void OnNpcEvent_KO(byte[] rawData)
        {
            // KONpcInteractUI kendi event aboneliÄŸiyle handle eder
        }

        /// <summary>
        /// WIZ_SKILLDATA â€” Skill bar verisi.
        /// Routing: KOSkillTreeManager handles this.
        /// </summary>
        private void OnSkillData_KO(byte[] rawData)
        {
            // KOSkillTreeManager kendi event aboneliÄŸiyle handle eder
        }

        /// <summary>
        /// WIZ_PARTY â€” Parti iÅŸlemleri.
        /// Routing: PartyUI handles this.
        /// </summary>
        private void OnParty_KO(byte[] rawData)
        {
            // PartyUI kendi event aboneliÄŸiyle handle eder
        }

        /// <summary>
        /// WIZ_WAREHOUSE â€” Depo iÅŸlemleri.
        /// Routing: KOWarehouseManager handles this.
        /// </summary>
        private void OnWarehouse_KO(byte[] rawData)
        {
            // KOWarehouseManager kendi event aboneliÄŸiyle handle eder
        }

        /// <summary>
        /// WIZ_ITEM_UPGRADE â€” Item upgrade sonucu.
        /// Routing: UpgradeUI handles this.
        /// </summary>
        private void OnItemUpgrade_KO(byte[] rawData)
        {
            // UpgradeUI kendi event aboneliÄŸiyle handle eder
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_Chat (GameProcMain.cpp:2199-2212)
        /// Wire: [opcode][chatMode:byte][nation:byte][senderId:int16][nameLen:byte][name:str][msgLen:int16][msg:str]
        /// </summary>
        private void OnChat_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte channel = r.ReadByte();       // cpp:2202 — e_ChatMode
            byte nation = r.ReadByte();        // cpp:2203 — e_Nation
            short senderId = r.ReadInt16();    // cpp:2204
            string senderName = r.ReadKOString1(); // cpp:2207-2208 — 1-byte prefix!
            string message = r.ReadKOString(); // cpp:2211-2212 â€” 2-byte prefix

            string myName = GameManager.Instance != null ? GameManager.Instance.CharacterName : "";
            bool isMePrivate = (channel == 2) && string.Equals(senderName, myName, System.StringComparison.OrdinalIgnoreCase);

            if (KOUIManager.Instance != null && !isMePrivate && channel != 2)
            {
                // C++ birebir: PacketDef.h enum e_ChatMode + GameProcMain.cpp:2233-2268
                // N3_CHAT_NORMAL=1, PRIVATE=2, PARTY=3, FORCE=4, SHOUT=5, CLAN=6, PUBLIC=7, WAR=8, TITLE=9
                uint color = channel switch
                {
                    1 => 0xffffffff, // N3_CHAT_NORMAL — beyaz (cpp:2236)
                    2 => 0xff80ffff, // N3_CHAT_PRIVATE — açık cyan (cpp:2240)
                    3 => 0xff00c0c0, // N3_CHAT_PARTY — teal (cpp:2245)
                    4 => 0xff00c0c0, // N3_CHAT_FORCE — teal (cpp:2244, aynı case)
                    5 => 0xfff86605, // N3_CHAT_SHOUT — turuncu (cpp:2251)
                    6 => 0xff00ff00, // N3_CHAT_CLAN — yeşil (cpp:2255)
                    7 => 0xffffff00, // N3_CHAT_PUBLIC — sarı (cpp:2262)
                    8 => 0xffffff00, // N3_CHAT_WAR — sarı (cpp:2259)
                    _ => 0xffffffff,
                };
                string formatted = string.IsNullOrEmpty(senderName)
                    ? message
                    : $"{senderName} : {message}"; // cpp:2217 format
                KOUIManager.Instance.AddChatMessage(channel, formatted);
            }

            if (channel == 2 && !isMePrivate && KOWhisperManager.Instance != null)
            {
                KOWhisperManager.Instance.ReceivePrivateMessage(senderName, message);
            }

            // C++ birebir: GameProcMain.cpp satır 2311-2319
            // Balon sadece N3_CHAT_NORMAL(1) ve N3_CHAT_SHOUT(5) kanallarında gösterilir (cpp:2318)
            // BalloonStringSet'e birleştirilmiş "isim : mesaj" formatı gönderilir (cpp:2217+2319)
            if (channel == 1 || channel == 5) // N3_CHAT_NORMAL(1) || N3_CHAT_SHOUT(5)
            {
                string szChat = string.IsNullOrEmpty(senderName)
                    ? message
                    : $"{senderName} : {message}"; // cpp:2217 birebir
                ShowChatBubbleOnEntity(senderId, szChat, channel);
            }
        }

        /// <summary>
        /// C++ birebir: CPlayerBase::m_pFontChat — mesajı karakterin üzerinde göster.
        /// Kendi karakterimiz veya diğer oyuncular üzerinde chat balonu.
        /// </summary>
        private void ShowChatBubbleOnEntity(short senderId, string message, byte channel)
        {
            var gm = GameManager.Instance;

            // C++ birebir: BalloonStringSet(szChat, crChat) â€” renk kanal bazlÄ±
            // Normal = 0xFFFFFFFF (beyaz), Shout = 0xFFF86605 (turuncu)
            Color balloonColor = (channel == 5)
                ? new Color(0.973f, 0.4f, 0.02f, 1f)  // Shout: #F86605
                : Color.white;                          // Normal: #FFFFFF

            // Kendi karakterimiz mi? — short/long karşılaştırma
            bool isMe = gm != null && (senderId == (short)gm.CharacterId || senderId == gm.CharacterId);

            if (isMe)
            {
                // Player objesinde FloatingName bul
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) player = GameObject.Find("Player");
                if (player != null)
                {
                    var fn = player.GetComponent<EntropyOnline.World.FloatingName>();
                    if (fn != null)
                    {
                        fn.ShowChatBubble(message, balloonColor);
                    }
                    else
                        Debug.LogWarning("[CHAT-BUBBLE] FloatingName component bulunamadı!");
                }
                else
                    Debug.LogWarning("[CHAT-BUBBLE] Player objesi bulunamadı!");
                return;
            }

            // Diğer oyuncu — EntityManager üzerinden bul
            var entityMgr = EntropyOnline.World.EntityManager.Instance;
            if (entityMgr != null)
            {
                var remotePlayer = entityMgr.GetRemotePlayer(senderId);
                if (remotePlayer != null && remotePlayer.Root != null)
                {
                    var fn = remotePlayer.Root.GetComponent<EntropyOnline.World.FloatingName>();
                    if (fn != null)
                        fn.ShowChatBubble(message, balloonColor);
                }
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_ZoneChange (GameProcMain.cpp:4968-5030)
        /// Wire: [opcode][subOp:byte] then if TELEPORT: [zone:byte][zoneSub:byte][x:uint16][z:uint16][y:int16][victoryNation:byte]
        /// </summary>
        private void OnZoneChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte subOp = r.ReadByte(); // cpp:4970 — ZONE_CHANGE_LOADING=1, ZONE_CHANGE_LOADED=2, ZONE_CHANGE_TELEPORT=3
            const byte ZONE_CHANGE_TELEPORT = 3;
            if (subOp == ZONE_CHANGE_TELEPORT) // ZONE_CHANGE_TELEPORT
            {
                byte zoneId = r.ReadByte();     // cpp:4972 — zone (byte, NOT int16!)
                byte zoneSub = r.ReadByte();     // cpp:4973 — alt bölge
                ushort x = r.ReadUInt16();       // cpp:4974
                ushort z = r.ReadUInt16();       // cpp:4975
                short y = r.ReadInt16();         // cpp:4976
                byte victoryNation = r.ReadByte(); // cpp:4977
            }
            // GameSceneController zone geçişini handle eder
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_MyInfo_PointChange (GameProcMain.cpp:3829-3882)
        /// Wire: [opcode][type:byte][value:int16][hpMax:int16][mspMax:int16][attack:int16][weightMax:uint16]
        /// IMPORTANT: value = ABSOLUTE (절대수치), NOT delta!
        /// </summary>
        private void OnPointChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte type = r.ReadByte();          // cpp:3831
            short value = r.ReadInt16();        // cpp:3832 — ABSOLUTE value!
            short hpMax = r.ReadInt16();        // cpp:3834 — iHPMax
            short mspMax = r.ReadInt16();       // cpp:3835 — iMSPMax
            short attack = r.ReadInt16();       // cpp:3836 — iAttack
            ushort weightMax = r.ReadUInt16();  // cpp:3837 — iWeightMax
            var gm = GameManager.Instance;
            if (gm != null)
            {
                // C++ birebir: cpp:3834-3837 — HP/MSP/Attack/Weight max güncelle
                gm.MaxHP = hpMax;
                gm.MaxMP = mspMax;
                gm.TotalHit = attack;
                gm.MaxWeight = (short)weightMax;

                // C++ birebir: cpp:3851-3875 — value MUTLAK değer olarak ATAR (SET)
                switch (type)
                {
                    case 1: gm.StatStr = value; break; // Strength — cpp:3853
                    case 2: gm.StatSta = value; break; // Stamina — cpp:3858
                    case 3: gm.StatDex = value; break; // Dexterity — cpp:3863
                    case 4: gm.StatInt = value; break; // Intelligence — cpp:3868
                    case 5: gm.StatCha = value; break; // MagicAttak — cpp:3873
                }
                // cpp:3879 — bonus point azalt
                if (type >= 1 && type <= 5) gm.StatPoints--;
            }
            RefreshAllBars();
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_ItemCountChange (GameProcMain.cpp:3587-3617)
        /// Wire: [totalCount:int16] [{ district:byte, index:byte, itemId:uint32,
        ///        count:uint32, newItem:byte, durability:uint16 } × totalCount]
        ///
        /// C++ satır 3602-3608: if (iNewItem == ITEM_COUNT_CHANGE_NEW)
        ///   → pItem = s_pTbl_Items_Basic.Find(iID / 1000 * 1000)
        ///   → MsgOutput(IDS_ITEM_RECEIVED + pItem->szName, 0xFFFFFF00)
        /// </summary>
        private void OnItemCountChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            short totalCount = r.ReadInt16();   // cpp:3589

            var inv = KOInventory.Instance;

            for (int i = 0; i < totalCount; i++)
            {
                byte district = r.ReadByte();       // cpp:3593
                byte index = r.ReadByte();           // cpp:3594
                uint itemId = r.ReadUInt32();         // cpp:3595
                uint count = r.ReadUInt32();          // cpp:3596
                byte newItem = r.ReadByte();          // cpp:3597 — 100 for new items
                ushort durability = r.ReadUInt16();    // cpp:3598

                // C++ birebir: cpp:3600 — m_pUIInventory->ItemCountChange(iDistrict, iIndex, iCount, iID, iDurability)
                if (inv != null)
                {
                    inv.ItemCountChange(district, index, itemId, (int)count, durability);
                }

                // C++ birebir: cpp:3602 — ITEM_COUNT_CHANGE_NEW = 100
                if (newItem == 100 && KOUIManager.Instance != null)
                {
                    // C++ birebir: cpp:3604-3608 — pItem->szName ile mesaj göster
                    // text_resources.h:562 IDS_ITEM_RECEIVED (7613) → "You received %s."
                    string itemName = GetItemName(itemId);
                    KOUIManager.Instance.AddMsgOutput(
                        $"You've received the {itemName} item.",
                        KOUIManager.D3DColorToUnity(0xFFFFFF00));
                }
            }

            // C++ birebir: Inventory UI güncelle
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.RefreshInventoryUI();
        }

        /// <summary>
        /// C++ birebir: s_pTbl_Items_Basic.Find(iID / 1000 * 1000)->szName
        /// </summary>
        private string GetItemName(uint itemId)
        {
            // C++ birebir: dwItemID / 1000 * 1000 → base item ID
            var basic = ItemDataManager.GetItemBasic((int)(itemId / 1000 * 1000));
            if (basic != null && !string.IsNullOrEmpty(basic.SzName))
                return basic.SzName;
            return $"Item #{itemId}";
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_NpcSay
        /// Wire: [opcode][npcId:int16][message:string2]
        /// Routing: QuestDialogUI handles display.
        /// </summary>
        private void OnNpcSay_KO(byte[] rawData)
        {
            // QuestDialogUI kendi event aboneliÄŸiyle handle eder
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_SelectMsg
        /// Wire: [opcode][count:byte][{optionText:string2}*count]
        /// Routing: QuestDialogUI handles display.
        /// </summary>
        private void OnSelectMsg_KO(byte[] rawData)
        {
            // QuestDialogUI kendi event aboneliÄŸiyle handle eder
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:5603-5611 MsgRecv_SkillChange
        /// Wire: [opcode][type:byte][value:byte]
        /// </summary>
        private void OnSkillPtChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte type = r.ReadByte();
            byte value = r.ReadByte();
            OnSkillPointResult(type, value);
        }

        private void OnMyInfo(long charId, string name, byte nation, byte race, byte charClass,
            short level, long exp, short str, short sta, short dex, short intel, short cha,
            short statPoints, short skillPoints, int currentHp, int maxHp, int currentMp, int maxMp,
            float posX, float posY, float posZ, long gold, byte authority)
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.CharacterId = charId;
                gm.CharacterName = name;
                gm.Nation = nation;
                gm.Race = race;
                gm.CharClass = charClass;
                gm.Level = level;
                gm.Experience = exp;
                gm.StatStr = str;
                gm.StatSta = sta;
                gm.StatDex = dex;
                gm.StatInt = intel;
                gm.StatCha = cha;
                gm.StatPoints = statPoints;
                gm.SkillPoints = skillPoints;
                gm.CurrentHp = currentHp;
                gm.MaxHp = maxHp;
                gm.CurrentMp = currentMp;
                gm.MaxMp = maxMp;
                gm.PlayerPosX = posX;
                gm.PlayerPosY = posY;
                gm.PlayerPosZ = posZ;
                gm.Gold = gold;
            }
            
            UpdateHpBar(currentHp, maxHp);
            UpdateMpBar(currentMp, maxMp);
            UpdateExpBar(exp, level);

            // C++ UIInventory::GoldUpdate birebir
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.UpdateGold(gold);

                // C++ UIVarious tÃ¼m Update metotlarÄ± birebir
                // C++ GameProcMain: iExpNext = GetNeedExp(level) â€” caller hesaplar
                long expMax = EntropyOnline.Shared.LevelExpTable.GetExpForLevel(level);
                KOUIManager.Instance.UpdateCharacterInfo(
                    name, level, str, sta, dex, intel, cha,
                    statPoints, currentHp, maxHp, currentMp, maxMp,
                    exp, expMax, skillPoints,
                    gm != null ? gm.Loyalty : 0,
                    gm != null ? gm.LoyaltyMonthly : 0);

                // C++ GameProcMain::InitZone() satÄ±r 4478: ilk giriÅŸte minimap yÃ¼kle
                string minimapFile = KOUIManager.GetMiniMapFileName(gm != null ? gm.CurrentZoneId : (short)21);
                KOUIManager.Instance.LoadMiniMap(minimapFile, 4096f, 4096f);

                // C++ GameProcMain::InitZone() satÄ±r 4483-4487 birebir:
                // float fZoom = 6.0f;
                // e_Class_Represent eCR = GetRepresentClass(eClass);
                // if (CLASS_REPRESENT_ROGUE == eCR) fZoom = 3.0f;
                // m_pUIStateBarAndMiniMap->ZoomSet(fZoom);
                float fZoom = 6.0f;
                if (gm != null && IsRepresentRogue(gm.CharClass))
                    fZoom = 3.0f; // 로그 계열은 맵이 좀 더 널리 자세히 보인다
                KOUIManager.Instance.SetMiniMapZoom(fZoom);
            }
        }

        private void OnHpChangeReceived(int maxHp, int currentHp)
        {
            var gm = GameManager.Instance;
            if (gm != null && KOUIManager.Instance != null)
            {
                // C++ satır 3630: int iHPChange = iHP - s_pPlayer->m_InfoBase.iHP;
                int hpDelta = currentHp - gm.CurrentHp;
                if (gm.CurrentHp > 0) // İlk sync'te (CurrentHp=0) sahte hasar mesajı gösterme
                {
                    if (hpDelta < 0)
                        KOUIManager.Instance.AddMsgOutput($"{-hpDelta} HP Damage", KOUIManager.D3DColorToUnity(0xffff3b3b));
                    else if (hpDelta > 0)
                        KOUIManager.Instance.AddMsgOutput($"{hpDelta} HP Recovered", KOUIManager.D3DColorToUnity(0xff6565ff));
                }

                // Root Cause Protection: Only overwrite MaxHp if the new maxHp is a valid value (> 1)
                // or if we don't have a valid MaxHp yet.
                if (maxHp > 1 || gm.MaxHp <= 1)
                {
                    gm.MaxHp = maxHp;
                }
                gm.CurrentHp = currentHp;
                if (KOPartyManager.Instance != null)
                {
                    KOPartyManager.Instance.UpdateLocalPlayerStats(gm.CurrentHp, gm.MaxHp, gm.CurrentMp, gm.MaxMp);
                }
            }

            // C++ satır 3647-3648: UpdateHP(iHP, iHPMax)
            UpdateHpBar(currentHp, gm != null ? gm.MaxHp : maxHp);
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_MyInfo_MSP (GameProcMain.cpp:3651-3694)
        /// Wire: [maxMp:int16] [curMp:int16]
        /// Warrior/Rogue → "SP", Mage/Priest → "MP" (C++ satır 3658-3662)
        /// </summary>
        private void OnMSpChangeReceived(int maxMp, int currentMp)
        {
            var gm = GameManager.Instance;
            if (gm != null && KOUIManager.Instance != null)
            {
                // C++ satır 3656: int iMSPChange = iMSP - s_pPlayer->m_InfoExt.iMSP;
                int mpDelta = currentMp - gm.CurrentMp;

                // C++ birebir: GetRepresentClass (GameBase.cpp:386-427)
                // switch(eClass) ile explicit enum eşleme — formül DEĞİL!
                bool bUseMP = true;
                byte charClass = gm.CharClass;
                // CLASS_REPRESENT_WARRIOR veya CLASS_REPRESENT_ROGUE → bUseMP = false (SP kullanır)
                switch (charClass)
                {
                    case 101: case 105: case 106: // KA_WARRIOR, KA_BERSERKER, KA_GUARDIAN
                    case 201: case 205: case 206: // EL_WARRIOR, EL_BLADE, EL_PROTECTOR
                    case 102: case 107: case 108: // KA_ROGUE, KA_HUNTER, KA_PENETRATOR
                    case 202: case 207: case 208: // EL_ROGUE, EL_RANGER, EL_ASSASSIN
                        bUseMP = false;
                        break;
                    // Wizard(103,109,110,203,209,210) ve Priest(104,111,112,204,211,212) → bUseMP = true
                }

                if (gm.CurrentMp > 0) // İlk sync'te (CurrentMp=0) sahte mesaj gösterme
                {
                    if (mpDelta < 0)
                    {
                        // C++ satır 3667-3672
                        string msg = bUseMP
                            ? $"{-mpDelta} MP Used"
                            : $"{-mpDelta} SP Used";
                        KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffff3b3b));
                    }
                    else if (mpDelta > 0)
                    {
                        // C++ satır 3674-3682
                        string msg = bUseMP
                            ? $"{mpDelta} MP Recovered"
                            : $"{mpDelta} SP Recovered";
                        KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xff6565ff));
                    }
                }

                // Root Cause Protection: Only overwrite MaxMp if the new maxMp is a valid value (> 1)
                // or if we don't have a valid MaxMp yet.
                if (maxMp > 1 || gm.MaxMp <= 1)
                {
                    gm.MaxMp = maxMp;
                }
                gm.CurrentMp = currentMp;
                if (KOPartyManager.Instance != null)
                {
                    KOPartyManager.Instance.UpdateLocalPlayerStats(gm.CurrentHp, gm.MaxHp, gm.CurrentMp, gm.MaxMp);
                }
            }

            // C++ satır 3687-3688: UpdateMSP(iMSP, iMSPMax)
            UpdateMpBar(currentMp, gm != null ? gm.MaxMp : maxMp);
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_MyInfo_EXP (GameProcMain.cpp:3696-3717)
        /// Wire: [totalExp: uint32]
        /// 
        /// C++ satÄ±r 3698-3701: iExp = read, iOldExp = m_InfoExt.iExp, m_InfoExt.iExp = iExp
        /// C++ satÄ±r 3702-3703: UpdateExp bar'larÄ±
        /// C++ satÄ±r 3705-3714: iLevelPrev == iLevel && iExp != iOldExp â†’ MsgOutput
        ///   - iExp > iOldExp â†’ IDS_MSG_FMT_EXP_GET (0xffffff00 sarÄ±)
        ///   - iExp < iOldExp â†’ IDS_MSG_FMT_EXP_LOST (0xffffff00 sarÄ±)
        /// C++ satÄ±r 3716: iLevelPrev = iLevel
        /// </summary>
        private void OnExpGained(int totalExp)
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                // C++ satÄ±r 3699: iOldExp = s_pPlayer->m_InfoExt.iExp
                long iOldExp = gm.Experience;
                
                // C++ satÄ±r 3701: s_pPlayer->m_InfoExt.iExp = iExp
                gm.Experience = totalExp;
                UpdateExpBar(totalExp, gm.Level);
                
                // C++ satır 3705-3714 birebir: MsgOutput EXP kazanım/kayıp mesajı
                // if (iLevelPrev == iLevel && iExp != iOldExp)
                // C++ birebir: seviye atlandığında EXP mesajı gösterilmez (level up ayrı paket)
                if (gm._levelPrev == gm.Level && totalExp != iOldExp && KOUIManager.Instance != null)
                {
                    if (totalExp > iOldExp)
                    {
                        // C++ birebir: text_resources.h:180 IDS_MSG_FMT_EXP_GET (3007)
                        // "Earned %d Experience Points"
                        long delta = totalExp - iOldExp;
                        KOUIManager.Instance.AddMsgOutput(
                            $"Earned {delta} Experience Points",
                            KOUIManager.D3DColorToUnity(0xffffff00));
                    }
                    else
                    {
                        // C++ birebir: text_resources.h:181 IDS_MSG_FMT_EXP_LOST (3008)
                        // "Lost %d Experience Points"
                        long delta = iOldExp - totalExp;
                        KOUIManager.Instance.AddMsgOutput(
                            $"Lost {delta} Experience Points",
                            KOUIManager.D3DColorToUnity(0xffffff00));
                    }
                }

                // C++ satır 3716: iLevelPrev = iLevel
                gm._levelPrev = gm.Level;
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_MyInfo_LevelChange (GameProcMain.cpp:3719-3780)
        /// Wire: charId + level + points + skillPoints + maxExp + exp + maxHp + hp + maxMp + mp
        /// 
        /// C++ satÄ±r 3723: if (iID == s_pPlayer->IDNumber()) â†’ kendi bilgilerimiz
        /// C++ satÄ±r 3763: else â†’ diÄŸer oyuncu level gÃ¼ncellemesi + FX
        /// </summary>
        private void OnLevelUp(long charId, byte level, short statPoints, byte skillPoints,
            int maxExp, int exp, short maxHp, short hp, short maxMp, short mp)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            
            // C++ satÄ±r 3723: if (iID == s_pPlayer->IDNumber())
            if (charId == gm.CharacterId)
            {
                // C++ satÄ±r 3728: iLevelPrev = pInfoBase->iLevel
                short prevLevel = gm.Level;
                
                // C++ satÄ±r 3729-3741: tÃ¼m stat'larÄ± gÃ¼ncelle
                gm.Level = level;
                gm.StatPoints = statPoints;
                gm.SkillPoints = skillPoints;
                gm.Experience = exp;
                gm.MaxHp = maxHp;
                gm.CurrentHp = hp;
                gm.MaxMp = maxMp;
                gm.CurrentMp = mp;
                
                // C++ satÄ±r 3746: UpdateAllStates
                UpdateHpBar(hp, maxHp);
                UpdateMpBar(mp, maxMp);
                // C++ satÄ±r 3748: UpdateExp(iExp, iExpNext, true)
                UpdateExpBar(exp, level);
                
                // C++ satÄ±r 3752-3753: m_pUISkillTreeDlg->m_iSkillInfo[0] = bExtraSkillPoint; InitIconUpdate();
                var skillMgr = KOSkillTreeManager.Instance;
                if (skillMgr != null)
                {
                    skillMgr.SkillInfo[0] = skillPoints;
                    skillMgr.InitIconUpdate();
                }
                
                // C++ satÄ±r 3755-3761 birebir: if (iLevel > iLevelPrev) â†’ LevelUp FX
                if (level > prevLevel)
                {
                    var fxMgr = KO.KOFXManager.Instance;
                    if (fxMgr != null)
                    {
                        int myId = (int)gm.CharacterId;
                        if (gm.Nation == 1) // NATION_KARUS
                            fxMgr.TriggerBundle(myId, -1, KO.KOFXManager.FXID_LEVELUP_KARUS, myId, -1);
                        else if (gm.Nation == 2) // NATION_ELMORAD
                            fxMgr.TriggerBundle(myId, -1, KO.KOFXManager.FXID_LEVELUP_ELMORAD, myId, -1);
                    }
                    
                    SpawnFloatingText($"â˜… LEVEL {level} â˜…", new Color(1f, 0.84f, 0f), 0f, true, 40);
                }
                
                // KOUIManager gÃ¼ncellemeleri
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.UpdateCharacterInfo(
                        gm.CharacterName, level, gm.StatStr, gm.StatSta, gm.StatDex, gm.StatInt, gm.StatCha,
                        statPoints, hp, maxHp, mp, maxMp,
                        exp, maxExp, skillPoints,
                        gm.Loyalty, gm.LoyaltyMonthly);
                }
            }
            else
            {
                // C++ satÄ±r 3763-3777 birebir: ë‹¤ë¥¸ ë„˜ì´ë‹¤.. (diÄŸer oyuncu)
                // CPlayerOther* pUPC = s_pOPMgr->UPCGetByID(iID, false);
                var entityMgr = EntropyOnline.World.EntityManager.Instance;
                if (entityMgr != null)
                {
                    var remotePlayer = entityMgr.GetRemotePlayer(charId);
                    if (remotePlayer != null)
                    {
                        // C++ satÄ±r 3768-3774: if (iLevel > pUPC->m_InfoBase.iLevel) â†’ FX
                        int prevOtherLevel = remotePlayer.Level;
                        if (level > prevOtherLevel)
                        {
                            var fxMgr = KO.KOFXManager.Instance;
                            if (fxMgr != null)
                            {
                                int otherId = (int)charId;
                                byte otherNation = remotePlayer.Nation;
                                if (otherNation == 1) // NATION_KARUS
                                    fxMgr.TriggerBundle(otherId, -1, KO.KOFXManager.FXID_LEVELUP_KARUS, otherId, -1);
                                else if (otherNation == 2) // NATION_ELMORAD
                                    fxMgr.TriggerBundle(otherId, -1, KO.KOFXManager.FXID_LEVELUP_ELMORAD, otherId, -1);
                            }
                        }
                        // C++ satÄ±r 3775: pUPC->m_InfoBase.iLevel = iLevel;
                        remotePlayer.Level = level;
                    }
                }
            }
        }

        private void OnAttackResult(long attackerId, long targetId, bool targetIsPlayer, int damage, int targetHp, bool targetDied)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (attackerId == gm.CharacterId)
            {
                // C++ GameProcMain.cpp MsgRecv_Attack satÄ±r 3298-3311 birebir:
                // if (0x0 == iResult) â†’ attack miss
                // MsgOutput(szMsg, 0xffffffff);
                if (damage == 0 && KOUIManager.Instance != null)
                {
                    string missTargetName = GetEntityName(targetId, targetIsPlayer);
                    KOUIManager.Instance.AddMsgOutput($"{missTargetName} Missed.", KOUIManager.D3DColorToUnity(0xffffffff));
                }

                if (damage > 0)
                    SpawnFloatingText($"-{damage}", new Color(1f, 1f, 0.3f), 0f, true);

                // C++ GameProcMain.cpp satır 6186-6220: UpdateUI_TargetBar
                // Bizim saldırımız → TargetBar'ı göster ve hedef ismini set et
                if (KOUIManager.Instance != null && targetId != gm.CharacterId)
                {
                    gm.TargetId = targetId;
                    string targetName = GetEntityName(targetId, targetIsPlayer);
                    KOUIManager.Instance.SetTargetInfo(targetName);
                    KOUIManager.Instance.ShowTargetBar(true);
                }
                // C++ birebir: MsgRecv_Attack result==0x02 (GameProcMain.cpp:3318-3319)
                // MessageBoxPost(IDS_REGENERATION) — MsgOutput DEĞİL
                // NOT: C++'ta ölüm mesajı Information paneline gönderilmez
            }
            else if (targetId == gm.CharacterId && targetIsPlayer)
                SpawnFloatingText($"-{damage}", new Color(1f, 0.2f, 0.2f), 0f, true);
        }

        private void OnEntityDeath(long entityId, bool isPlayer)
        {
            var gm = GameManager.Instance;
            if (gm != null && isPlayer && entityId == gm.CharacterId)
            {
                SpawnFloatingText("DEAD", new Color(1f, 0f, 0f), 0f, true, 36);
                // C++ birebir: MsgRecv_Dead (GameProcMain.cpp:3335-3343) — MsgOutput YOK, sadece MessageBox
            }

            // C++ UpdateUI_TargetBar() satır 6222-6224:
            // 죽은 캐릭터가 선택되었을때는 target bar를 그려주지 않는다.
            if (gm != null && entityId == gm.TargetId)
            {
                gm.TargetId = -1;
                if (KOUIManager.Instance != null)
                    KOUIManager.Instance.ShowTargetBar(false);
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_Effecting (MagicSkillMng.cpp:1866-1928)
        /// EFFECTING = FX efekt tetikleme. Damage/HP bilgisi bu pakette YOKTUR.
        /// Damage floating text → HP_CHANGE delta'dan türetilir.
        /// </summary>
        private void OnMagicEffecting(int magicId, long sourceId, long targetId,
            short data0, short data1, short data2, short data3, short data4, short data5)
        {
            // Open-KO birebir: MagicSkillMng.cpp:1884-1927

            // MagicSkillMng.cpp:1880-1882:
            // CPlayerBase* pPlayer = CharacterGetByID(iSourceID, false);
            // if (pPlayer == nullptr) return;
            var entityMgr = EntropyOnline.World.EntityManager.Instance;
            if (entityMgr != null)
            {
                bool sourceExists = entityMgr.GetMonster(sourceId) != null
                                 || entityMgr.GetRemotePlayer(sourceId) != null;
                // Kendi oyuncumuz da sourceId olabilir â€” o zaman her zaman mevcuttur
                if (!sourceExists && (GameManager.Instance == null || sourceId != GameManager.Instance.CharacterId))
                    return;
            }

            // MagicSkillMng.cpp:1884 â€” __TABLE_UPC_SKILL* pSkill = s_pTbl_Skill.Find(dwMagicID);
            var pSkill = KOImport.SkillTableParser.Find(magicId);
            if (pSkill == null) return;

            var fxMgr = KO.KOFXManager.Instance;
            if (fxMgr == null) return;

            int iSourceID = (int)sourceId;

            // MagicSkillMng.cpp:1897-1898 birebir:
            // s_pFX->Stop(iSourceID, iSourceID, pSkill->iSelfFX1, -1, true);
            // s_pFX->Stop(iSourceID, iSourceID, pSkill->iSelfFX1, -2, true);
            fxMgr.Stop(iSourceID, iSourceID, pSkill.SelfFX1, -1, true);
            fxMgr.Stop(iSourceID, iSourceID, pSkill.SelfFX1, -2, true);

            // ============================================================
            // C++ birebir: MagicSkillMng.cpp:1900-1916
            // Type1/Type4/Type3 effecting — buff delta'larını uygula
            // ============================================================
            var gm = GameManager.Instance;
            var magicMgr = EntropyOnline.Combat.KOMagicSkillManager.Instance;

            // cpp:1906-1910: Type4 buff (speed, attack, AC, vb.)
            bool isTargetLocalPlayer = (targetId == 0 || (gm != null && (targetId == (short)gm.CharacterId || targetId == gm.CharacterId)));
            if ((pSkill.FirstTableType == 4 || pSkill.SecondTableType == 4)
                && isTargetLocalPlayer
                && magicMgr != null)
            {
                magicMgr.EffectingType4(magicId);
            }

            // cpp:1912-1916: Type3 buff (DoT/HoT, stun, vb.)
            if ((pSkill.FirstTableType == 3 || pSkill.SecondTableType == 3)
                && isTargetLocalPlayer
                && magicMgr != null)
            {
                magicMgr.EffectingType3(magicId);
            }

            if (pSkill.TargetFX == 0)
                return;

            // MagicSkillMng.cpp:1921-1927 birebir:
            // if (iTargetID == -1)
            //     s_pFX->TriggerBundle(iSourceID, 0, pSkill->iTargetFX, vTargetPos);
            // else
            //     s_pFX->TriggerBundle(iSourceID, 0, pSkill->iTargetFX, iTargetID, pSkill->iTargetPart);
            if (pSkill.Target >= 10 && pSkill.Target <= 13)
            {
                var wb = EntropyOnline.World.WorldBuilder.Instance;
                float y = wb != null ? wb.GetTerrainHeight(data0, data2) : (float)data1;
                var vTargetPos = new UnityEngine.Vector3(data0, y, data2);
                fxMgr.TriggerAreaTargetFX(iSourceID, pSkill.TargetFX, vTargetPos, pSkill.Id);
            }
            else
            {
                if (targetId == -1)
                {
                    var wb = EntropyOnline.World.WorldBuilder.Instance;
                    float y = wb != null ? wb.GetTerrainHeight(data0, data2) : (float)data1;
                    var vTargetPos = new UnityEngine.Vector3(data0, y, data2);
                    fxMgr.TriggerBundle(iSourceID, 0, pSkill.TargetFX, vTargetPos);
                }
                else
                {
                    fxMgr.TriggerBundle(iSourceID, 0, pSkill.TargetFX, (int)targetId, pSkill.TargetPart);
                }
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_Casting (MagicSkillMng.cpp:1707-1792)
        /// BaÅŸka bir oyuncu/NPC cast baÅŸlattÄ±ÄŸÄ±nda cast FX tetikle.
        /// Kendi casting'imiz server'dan gelmez (cpp:1722-1723).
        /// </summary>
        private void OnMagicCasting(int magicId, long sourceId, long targetId,
            short data0, short data1, short data2, short data3, short data4, short data5)
        {
            // MagicSkillMng.cpp:1722-1723 birebir:
            // if (iSourceID < 0 || iSourceID == s_pPlayer->IDNumber()) return;
            var gm = GameManager.Instance;
            if (gm != null && sourceId == gm.CharacterId) return;
            if (sourceId < 0) return;

            // MagicSkillMng.cpp:1735-1737 â€” skill lookup
            var pSkill = KOImport.SkillTableParser.Find(magicId);
            if (pSkill == null) return;

            var fxMgr = KO.KOFXManager.Instance;
            if (fxMgr == null) return;

            int iSourceID = (int)sourceId;

            // MagicSkillMng.cpp:1766-1772 birebir:
            // int spart1 = pSkill->iSelfPart1 % 1000;
            // int spart2 = pSkill->iSelfPart1 / 1000;
            // spart2 = abs(spart2);
            // s_pFX->TriggerBundle(iSourceID, spart1, pSkill->iSelfFX1, iSourceID, spart1, -1);
            // if (spart2 != 0)
            //     s_pFX->TriggerBundle(iSourceID, spart2, pSkill->iSelfFX1, iSourceID, spart2, -2);
            int spart1 = pSkill.SelfPart1 % 1000;
            int spart2 = System.Math.Abs(pSkill.SelfPart1 / 1000);

            fxMgr.TriggerBundle(iSourceID, spart1, pSkill.SelfFX1, iSourceID, spart1, -1);
            if (spart2 != 0)
                fxMgr.TriggerBundle(iSourceID, spart2, pSkill.SelfFX1, iSourceID, spart2, -2);
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_Flying (MagicSkillMng.cpp:1794-1864)
        /// BaÅŸka bir oyuncu/NPC flying projectile spawn ettiÄŸinde FX tetikle.
        /// Kendi flying'imiz server'dan gelmez (cpp:1809).
        /// </summary>
        private void OnMagicFlying(int magicId, long sourceId, long targetId,
            short data0, short data1, short data2, short data3, short data4, short data5)
        {
            // MagicSkillMng.cpp:1809 birebir:
            // if (iSourceID < 0 || iSourceID == s_pPlayer->IDNumber()) return;
            var gm = GameManager.Instance;
            if (gm != null && sourceId == gm.CharacterId) return;
            if (sourceId < 0) return;

            // MagicSkillMng.cpp:1816-1818 â€” skill lookup
            var pSkill = KOImport.SkillTableParser.Find(magicId);
            if (pSkill == null) return;

            var fxMgr = KO.KOFXManager.Instance;
            if (fxMgr == null) return;

            int iSourceID = (int)sourceId;

            // MagicSkillMng.cpp:1838-1839 birebir:
            // s_pFX->Stop(iSourceID, iSourceID, pSkill->iSelfFX1, -1, true);
            // s_pFX->Stop(iSourceID, iSourceID, pSkill->iSelfFX1, -2, true);
            fxMgr.Stop(iSourceID, iSourceID, pSkill.SelfFX1, -1, true);
            fxMgr.Stop(iSourceID, iSourceID, pSkill.SelfFX1, -2, true);

            if (pSkill.FlyingFX == 0) return;

            // MagicSkillMng.cpp:1849 â€” spart1
            int spart1 = pSkill.SelfPart1 % 1000;

            // MagicSkillMng.cpp:1847-1863 birebir:
            // CPlayerBase* pTarget = CharacterGetByID(iTargetID, false);
            // if (pTarget == nullptr) {
            //     vTargetPos = pPlayer->Position() + pPlayer->Direction();
            //     TriggerBundle(..., vTargetPos, Data[3], FX_BUNDLE_MOVE_DIR_FIXEDTARGET);
            // } else {
            //     TriggerBundle(..., iTargetID, 0, Data[3], FX_BUNDLE_MOVE_DIR_FLEXABLETARGET);
            // }
            //
            // C++ CharacterGetByID(iTargetID, false) â€” hem player hem NPC/monster arar.
            // Biz EntityManager.GetMonster + GetRemotePlayer ile aynÄ± ÅŸeyi yapÄ±yoruz.
            var entityMgr = EntropyOnline.World.EntityManager.Instance;
            bool targetFound = false;
            if (targetId >= 0 && entityMgr != null)
            {
                var monster = entityMgr.GetMonster(targetId);
                if (monster != null && monster.Root != null)
                    targetFound = true;
                else
                {
                    var remotePlayer = entityMgr.GetRemotePlayer(targetId);
                    if (remotePlayer != null && remotePlayer.Root != null)
                        targetFound = true;
                }
            }

            if (targetFound)
            {
                // MagicSkillMng.cpp:1861-1862 â€” target var, FLEXABLETARGET
                fxMgr.TriggerBundle(iSourceID, spart1, pSkill.FlyingFX, (int)targetId, 0,
                    data3, KO.KOFXManager.FX_BUNDLE_MOVE_DIR_FLEXABLETARGET);
            }
            else
            {
                // MagicSkillMng.cpp:1853-1856 â€” target yok, FIXEDTARGET
                // C++ birebir: vTargetPos = pPlayer->Position() + pPlayer->Direction()
                // Kaynak oyuncunun pozisyonunu kullan
                UnityEngine.GameObject sourceGo = null;
                if (entityMgr != null)
                {
                    var srcMonster = entityMgr.GetMonster(sourceId);
                    if (srcMonster != null) sourceGo = srcMonster.Root;
                    else
                    {
                        var srcPlayer = entityMgr.GetRemotePlayer(sourceId);
                        if (srcPlayer != null) sourceGo = srcPlayer.Root;
                    }
                }
                if (sourceGo != null)
                {
                    var pos = sourceGo.transform.position;
                    var dir = sourceGo.transform.forward;
                    var vTargetPos = pos + dir;
                    fxMgr.TriggerBundle(iSourceID, spart1, pSkill.FlyingFX, vTargetPos,
                        data3, KO.KOFXManager.FX_BUNDLE_MOVE_DIR_FIXEDTARGET);
                }
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_Fail MsgOutput mesajlarÄ±.
        /// MagicSkillMng.cpp:1973 (AttackZero), 1992 (NoEffect), 2011 (Casting)
        /// TÃ¼m fail mesajlarÄ± 0xffff3b3b (kÄ±rmÄ±zÄ±) rengiyle gÃ¶sterilir.
        /// </summary>
        private void OnMagicFail(int magicId, long sourceId, long targetId, MagicFailReason reason,
            short data0, short data1, short data2, short data4, short data5)
        {
            // Open-KO birebir: MagicSkillMng.cpp:1947-1961

            // MagicSkillMng.cpp:1947-1949 â€” skill lookup
            var pSkill = KOImport.SkillTableParser.Find(magicId);

            // MagicSkillMng.cpp:1953-1961 birebir:
            // s_pFX->Stop(iSourceID, iSourceID, pSkill->iSelfFX1, -1, true);
            // s_pFX->Stop(iSourceID, iSourceID, pSkill->iSelfFX1, -2, true);
            var fxMgr = KO.KOFXManager.Instance;
            if (fxMgr != null && pSkill != null)
            {
                int iSourceID = (int)sourceId;
                fxMgr.Stop(iSourceID, iSourceID, pSkill.SelfFX1, -1, true);
                fxMgr.Stop(iSourceID, iSourceID, pSkill.SelfFX1, -2, true);
            }

            // Mesaj sadece kendi oyuncuya gÃ¶sterilir â€” MagicSkillMng.cpp:1968/1987/2006
            var gm = GameManager.Instance;
            if (gm == null || sourceId != gm.CharacterId) return;
            if (KOUIManager.Instance == null) return;

            // Open-KO birebir: tÃ¼m fail mesajlarÄ± 0xffff3b3b kÄ±rmÄ±zÄ±
            var failColor = KOUIManager.D3DColorToUnity(0xffff3b3b);

            // C++ birebir: Skill adıyla mesaj göster
            string skillName = pSkill?.Name ?? "Skill";

            switch (reason)
            {
                case MagicFailReason.AttackZero:
                    // C++ birebir: KOMagicSkillManager.cs içinde ekrana yazdırıldığı için burada duplicate edilmez.
                    break;
                case MagicFailReason.NoEffect:
                    // C++ birebir: KOMagicSkillManager.cs içinde ekrana yazdırıldığı için burada duplicate edilmez.
                    break;
                case MagicFailReason.Casting:
                    // C++ birebir: KOMagicSkillManager.cs içinde ekrana yazdırıldığı için burada duplicate edilmez.
                    break;
                case MagicFailReason.KillFlying:
                    // C++ birebir: KILLFLYING(-101) mesaj göstermez.
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_TargetHP (GameProcMain.cpp:4258-4304)
        /// 
        /// C++ satÄ±r 4277-4284: Benim hedefimse â†’ target bar gÃ¼ncelle
        /// C++ satÄ±r 4286-4290: Entity HP'sini gÃ¼ncelle (pTarget->m_InfoBase.iHP/iHPMax)
        /// C++ satÄ±r 4292-4302: HP change mesajÄ± gÃ¶ster
        ///   - kayÄ±p: IDS_MSG_FMT_TARGET_HP_LOST (0xffffffff beyaz)
        ///   - iyileÅŸme: IDS_MSG_FMT_TARGET_HP_RECOVER (0xff6565ff mavi)
        /// </summary>
        private void OnTargetHpReceived(long targetId, byte echo, int currentHp, int maxHp, short hpChange)
        {
            var gm = GameManager.Instance;
            
            // C++ satÄ±r 4266-4275: maxHp <= 0 â†’ log + return
            if (maxHp <= 0) return;

            // C++ satÄ±r 4277-4284: if (iID == s_pPlayer->m_iIDTarget)
            if (gm != null && targetId == gm.TargetId)
            {
                // C++ satÄ±r 4280-4282: byUpdateImmediately â†’ bUI
                // m_pUITargetBar->UpdateHP(iTargetHPCur, iTargetHPMax, bUI)
                
                // KOTargetSelector kendi HP bar'Ä±nÄ± gÃ¼ncelliyor (entity.CurrentHP Ã¼zerinden)
                // KOUIManager'daki UIF target bar'Ä± da gÃ¼ncelle
                if (KOUIManager.Instance != null)
                {
                    float ratio = (float)currentHp / maxHp;
                    KOUIManager.Instance.UpdateTargetHP(ratio);
                }
            }

            // C++ satır 4286-4290: Entity HP güncelleme
            // pTarget = s_pOPMgr->CharacterGetByID(iID, true);
            // pTarget->m_InfoBase.iHP = iTargetHPCur;
            // pTarget->m_InfoBase.iHPMax = iTargetHPMax;
            var entityMgr = EntropyOnline.World.EntityManager.Instance;
            if (entityMgr != null)
            {
                var monster = entityMgr.GetMonster(targetId);
                if (monster != null)
                {
                    // UpdateData hem MonsterEntity hem overhead bar'ı günceller
                    monster.UpdateData(currentHp, maxHp);

                    // KOEntity (WorldBuilder model) HP'sini de güncelle —
                    // KOTargetSelector.UpdateHPBar() KOEntity.CurrentHP okuyor
                    var koEntity = monster.Root?.GetComponent<EntropyOnline.World.KOEntity>();
                    if (koEntity != null)
                    {
                        koEntity.CurrentHP = currentHp;
                        koEntity.MaxHP = maxHp;
                    }
                }
                else
                {
                    var rp = entityMgr.GetRemotePlayer(targetId);
                    if (rp != null)
                    {
                        rp.UpdateHp(currentHp, maxHp);
                        if (rp.Entity != null)
                        {
                            rp.Entity.CurrentHp = currentHp;
                            rp.Entity.MaxHp = maxHp;
                        }
                    }
                }
            }

            // C++ satır 4292-4302: HP change mesajı
            if (hpChange != 0 && KOUIManager.Instance != null)
            {
                string targetName = GetEntityName(targetId, false);
                if (hpChange < 0)
                {
                    // C++ satır 4293-4296: IDS_MSG_FMT_TARGET_HP_LOST (3016) → "%s received %d damage"
                    KOUIManager.Instance.AddMsgOutput(
                        $"{targetName} received {-hpChange} damage",
                        KOUIManager.D3DColorToUnity(0xffffffff));
                }
                else
                {
                    // C++ satır 4298-4301: IDS_MSG_FMT_TARGET_HP_RECOVER (3017) → "%s received %d HP"
                    KOUIManager.Instance.AddMsgOutput(
                        $"{targetName} received {hpChange} HP",
                        KOUIManager.D3DColorToUnity(0xff6565ff));
                }
            }
        }

        /// <summary>
        /// Entity ismini EntityManager'dan alır.
        /// C++ referans: GameProcMain.cpp IDToName() satır 2842-2862
        /// </summary>
        private string GetEntityName(long entityId, bool isPlayer)
        {
            var gm = GameManager.Instance;
            if (gm != null && entityId == gm.CharacterId)
                return gm.CharacterName;

            // EntityManager'dan ara
            var em = EntropyOnline.World.EntityManager.Instance;
            if (em != null)
            {
                string name = em.GetEntityName(entityId);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            // Fallback: ID göster
            return isPlayer ? $"Player_{entityId}" : $"Monster_{entityId}";
        }

        private void OnSitStandReceived(long charId, bool isSitting)
        {
            var gm = GameManager.Instance;
            if (gm == null || charId != gm.CharacterId) return;
        }

        private void OnNpcEventReceived(int tradeId, byte eventType)
        {
            if (KOUIManager.Instance == null) return;
            KOUIManager.Instance.ShowNpcEvent(tradeId, eventType);
        }

        private void OnNpcShopDataReceived(int npcId, string npcName, int tradeId, ShopItemData[] items)
        {
            if (KOUIManager.Instance == null) return;
            KOUIManager.Instance.PopulateShopList(npcId, tradeId, items);
            KOUIManager.Instance.ShowTransaction(true);
        }



        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:1071-1073
        /// case WIZ_NPC_SAY: m_pUIQuestTalk->Open(pkt);
        /// </summary>
        private void OnNpcSayReceived(int eventIdUp, int eventIdOk, int[] messageIds)
        {
            if (KOUIManager.Instance == null) return;
            KOUIManager.Instance.ShowQuestTalk(eventIdUp, eventIdOk, messageIds);
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:1067-1069
        /// case WIZ_SELECT_MSG: m_pUIQuestMenu->Open(pkt);
        /// </summary>
        private void OnSelectMsgReceived(short npcId, int talkId, int[] menuTextIds)
        {
            if (KOUIManager.Instance == null) return;
            KOUIManager.Instance.ShowQuestMenu(npcId, talkId, menuTextIds);
        }

        /// <summary>
        /// Sunucudan gelen skill listesini iÅŸler.
        /// C++ referans: GameProcMain.cpp MsgRecv_MyInfo â†’ UISkillTreeDlg InitIconUpdate
        /// 1. GameManager.LearnedSkills cache'e yaz
        /// 2. KOUIManager.PopulateSkillTree ile UIF paneline render et
        /// </summary>
        private void OnSkillListReceived(int[] learnedSkills)
        {
            var gm = GameManager.Instance;
            if (gm != null)
                gm.LearnedSkills = learnedSkills;

            if (KOUIManager.Instance != null)
                KOUIManager.Instance.PopulateSkillTree(learnedSkills);

            // C++ UIHotKeyDlg.cpp InitIconUpdate() satÄ±r 420-445 birebir:
            // Ä°lk 8 Ã¶ÄŸrenilmiÅŸ skill'i MobileSkillBar slotlarÄ±na ikon olarak yÃ¼kle.
            // C++ szIconFN = fmt::format("UI\\skillicon_{:02}_{}.dxt", HD.iID % 100, HD.iID / 100);
            // C++ spSkill->pUIIcon->SetTex(szIconFN); SetUVRect(0, 0, 1, 1);
            if (MobileSkillBar.Instance != null)
            {
                // Sunucudan skillbar verisi gelmemişse varsayılan skilleri yükle
                if (!MobileSkillBar.Instance.HasServerData)
                {
                    int slotCount = Mathf.Min(learnedSkills.Length, 8); // MAX_SKILL_IN_HOTKEY = 8
                    for (int i = 0; i < slotCount; i++)
                    {
                        int magicNum = learnedSkills[i];
                        var icon = KOItemIconLoader.LoadSkillIcon(magicNum);
                        MobileSkillBar.Instance.SetSkillIcon(i, icon, magicNum);
                    }
                }
                else
                {
                    MobileSkillBar.Instance.RefreshPage();
                }
            }
        }

        /// <summary>
        /// Parti Ã¼ye listesi gÃ¼ncellemesi.
        /// Open-KO birebir: MsgRecv_PartyOrForce INSERT/REMOVE/DESTROY
        /// GameProcMain.cpp:5173-5260
        /// Veri akÄ±ÅŸÄ±: KOPacketHandler â†’ GameHUD â†’ PartyUI (KOPartyManager yÃ¶netir)
        /// PartyUI.HandlePartyUpdate() zaten KOPartyManager'Ä± Ã§aÄŸÄ±rÄ±yor,
        /// burada sadece yedek olarak KOUIManager'a da yÃ¶nlendiriyoruz.
        /// </summary>
        private void OnPartyUpdateReceived(long leaderId, PartyMemberData[] members)
        {
            // Open-KO birebir: KOPartyManager veri yÃ¶netimi yapÄ±yor,
            // PartyUI.HandlePartyUpdate() zaten event'e abone.
            // Yedek: KOUIManager doÄŸrudan gÃ¼ncelle (PartyUI Ã§alÄ±ÅŸmÄ±yorsa)
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.PopulatePartyList(leaderId, members);
        }

        /// <summary>
        /// Parti Ã¼yelerinin HP gÃ¼ncellemesi.
        /// Open-KO birebir: N3_SP_PARTY_OR_FORCE_HP_CHANGE
        /// GameProcMain.cpp:5262-5272
        /// </summary>
        private void OnPartyHpUpdateReceived(PartyHpData[] updates)
        {
            // Open-KO birebir: KOPartyManager.MemberHPChange() veriyi gÃ¼nceller,
            // PartyUI.HandlePartyHpUpdate() zaten event'e abone.
            // Yedek: KOUIManager doÄŸrudan gÃ¼ncelle
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.UpdatePartyMemberHP(updates);
        }

        /// <summary>
        /// Depo verisi alÄ±ndÄ±ÄŸÄ±nda Ã§aÄŸrÄ±lÄ±r.
        /// C++ referans: UIWareHouseDlg.cpp EnterWareHouseStateStart/End
        /// </summary>
        private void OnWarehouseDataReceived(long gold, WarehouseSlot[] items)
        {
            if (KOUIManager.Instance != null)
            {
                // C++ birebir: ShowWarehouse ÖNCE (ItemMoveFromInvToThis_Ware çağırır)
                // PopulateWarehouse SONRA (m_pMyWareInv'den okur)
                KOUIManager.Instance.ShowWarehouse(true);
                KOUIManager.Instance.PopulateWarehouse(gold, items);
            }
        }

        /// <summary>
        /// Upgrade sonuÃ§ event'i.
        /// C++ referans: GameProcMain.cpp:1055-1056
        /// case WIZ_ITEM_UPGRADE: m_pUIItemUpgrade->MsgRecv_ItemUpgrade(pkt)
        /// 
        /// TÃ¼m mantÄ±k artÄ±k KOItemUpgradeManager.MsgRecv_ItemUpgrade() iÃ§inde.
        /// UpgradeUI.HandleUpgradeResult() zaten KOItemUpgradeManager'a yÃ¶nlendiriyor.
        /// GameHUD sadece KOUIManager.HandleUpgradeResult stub'Ä±nÄ± Ã§aÄŸÄ±rÄ±r (UI gÃ¼ncelleme).
        /// </summary>
        private void OnUpgradeResultReceived(long instanceId, bool success, byte resultType, short newLevel, short newAtkMin, short newAtkMax, short newDef)
        {
            // MsgOutput mesajlarÄ± artÄ±k KOItemUpgradeManager.MsgRecv_ItemUpgrade() iÃ§inde
            // UpgradeUI.HandleUpgradeResult() zaten doÄŸru yÃ¶nlendirmeyi yapÄ±yor.
            // GameHUD sadece KOUIManager stub'Ä±nÄ± Ã§aÄŸÄ±rÄ±r.
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.HandleUpgradeResult(success, newLevel);
        }

        /// <summary>
        /// Takas isteÄŸi geldi.
        /// C++ referans: SubProcPerTrade.cpp ReceiveMsgPerTradeReq
        /// </summary>
        private void OnTradeIncomingReceived(long requesterId, string requesterName)
        {
            // C++ EnterWaitMyDecisionToPerTrade: Trade panelini aÃ§
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowPersonalTrade(true);
        }

        /// <summary>
        /// Takas tamamlandÄ±/iptal edildi.
        /// C++ referans: SubProcPerTrade.cpp PerTradeCompleteSuccess/Cancel
        /// </summary>
        private void OnTradeCompleteReceived(byte resultType, string message)
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.HandleTradeComplete(resultType, message);

            // C++ SubProcPerTrade.cpp birebir: takas sonucu MsgOutput
            if (KOUIManager.Instance != null && !string.IsNullOrEmpty(message))
            {
                // resultType: 0=iptal, 1=baÅŸarÄ±lÄ±, 2+=hata
                uint color = resultType == 1 ? 0xff00ff00u : 0xffff3b3bu;
                KOUIManager.Instance.AddMsgOutput(message, KOUIManager.D3DColorToUnity(color));
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_ZoneAbility (GameProcMain.cpp:8043-8053)
        /// s_pPlayer->m_InfoExt.eZoneAbilityType = byZoneAbilityType;
        /// s_pPlayer->m_InfoExt.bCanTradeWithOtherNation = bCanTradeWithOtherNation;
        /// s_pPlayer->m_InfoExt.bCanTalkToOtherNation = bCanTalkToOtherNation;
        /// s_pPlayer->m_InfoExt.sZoneTariff = sTariff;
        /// </summary>
        private void OnZoneAbilityReceived(byte canTradeOtherNation, byte zoneAbilityType, byte canTalkOtherNation, short tariff)
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                // C++ GameProcMain.cpp:8047-8050 birebir
                gm.ZoneAbilityType = zoneAbilityType;
                gm.CanTradeWithOtherNation = canTradeOtherNation != 0;
                gm.CanTalkToOtherNation = canTalkOtherNation != 0;
                gm.ZoneTariff = tariff;
            }
        }

        private void OnLoyaltyChangeReceived(int loyalty, int loyaltyMonthly)
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                // C++ GameProcMain.cpp MsgRecv_MyInfo_RealmPoint() satÄ±r 3792-3806 birebir:
                // int32_t iLoyaltyDelta = iLoyalty - s_pPlayer->m_InfoExt.iRealmPoint;
                // if (iLoyaltyDelta > 0) MsgOutput(szMsg, 0xffa2a0c8);
                // else MsgOutput(szMsg, 0xffff3b3b);
                if (KOUIManager.Instance != null)
                {
                    int loyaltyDelta = loyalty - gm.Loyalty;
                    if (loyaltyDelta > 0)
                        KOUIManager.Instance.AddMsgOutput($"Earned {loyaltyDelta} national points.", KOUIManager.D3DColorToUnity(0xffa2a0c8));
                    else if (loyaltyDelta < 0)
                        KOUIManager.Instance.AddMsgOutput($"Lost {-loyaltyDelta} national points.", KOUIManager.D3DColorToUnity(0xffff3b3b));
                }

                gm.Loyalty = loyalty;
                gm.LoyaltyMonthly = loyaltyMonthly;

                // C++ UIVarious::UpdateRealmPoint birebir
                if (KOUIManager.Instance != null)
                {
                    long expMax = EntropyOnline.Shared.LevelExpTable.GetExpForLevel(gm.Level);
                    KOUIManager.Instance.UpdateCharacterInfo(
                        gm.CharacterName, gm.Level, gm.StatStr, gm.StatSta,
                        gm.StatDex, gm.StatInt, gm.StatCha,
                        gm.StatPoints, gm.CurrentHp, gm.MaxHp,
                        gm.CurrentMp, gm.MaxMp, gm.Experience,
                        expMax, gm.SkillPoints, loyalty, loyaltyMonthly);
                }
            }
        }

        /// <summary>
        /// Sunucudan gelen chat mesajÄ±nÄ± KOUIManager'Ä±n chat paneline yÃ¶nlendir.
        /// C++ GameProcMain.cpp MsgRecv_Chat â†’ m_pUIChatDlg->AddChatMsg() birebir.
        /// 
        /// C++ satÄ±r 2289-2307: Chat scramble (í†µì—­ ì„œë¹„ìŠ¤)
        /// KarÅŸÄ± ulustan mesaj + bCanTalkToOtherNation==false + ikisi de GM deÄŸilse
        /// â†’ ':' sonrasÄ±ndan random karakterlerle bozulur.
        /// </summary>
        private void OnChatMessageReceived(byte channel, byte nation, long senderId, string senderName, string message)
        {
            var gm = GameManager.Instance;
            
            // C++ GameProcMain.cpp satÄ±r 2214-2217 birebir: szChat oluÅŸturma
            // if (szName.empty()) szChat = szMsg;
            // else szChat = szName + " : " + szMsg;
            string szChat;
            if (string.IsNullOrEmpty(senderName))
                szChat = message;
            else
                szChat = senderName + " : " + message;

            // C++ satÄ±r 2220-2223: N3_CHAT_CONTINUE_DELETE(12) â†’ DeleteContinueMsg
            if (channel == 12) // N3_CHAT_CONTINUE_DELETE
            {
                if (KOUIManager.Instance != null)
                    KOUIManager.Instance.SetContinueNotice("");
                return;
            }

            // C++ satÄ±r 2226-2229: N3_CHAT_TITLE_DELETE(10) â†’ SetNoticeTitle("", 0xffffffff)
            if (channel == 10) // N3_CHAT_TITLE_DELETE
            {
                if (KOUIManager.Instance != null)
                    KOUIManager.Instance.SetTitleNotice("");
                return;
            }

            // C++ satÄ±r 2269-2273: N3_CHAT_TITLE(9) â†’ SetNoticeTitle(szChat, crChat)
            if (channel == 9) // N3_CHAT_TITLE
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.SetTitleNotice(szChat);
                    KOUIManager.Instance.AddChatMessage(channel, szChat);
                }
                return;
            }

            // C++ satÄ±r 2276-2281: N3_CHAT_WAR(8) â†’ WarMessage ekrana yaz
            if (channel == 8) // N3_CHAT_WAR
            {
                if (KOUIManager.Instance != null)
                    KOUIManager.Instance.AddMsgOutput(szChat, KOUIManager.D3DColorToUnity(0xffffff00));
                return;
            }

            // C++ satÄ±r 2282-2287: N3_CHAT_CONTINUE(11) â†’ AddContinueMsg
            if (channel == 11) // N3_CHAT_CONTINUE
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.SetContinueNotice(szChat);
                    KOUIManager.Instance.AddChatMessage(channel, szChat);
                }
                return;
            }
            
            // C++ GameProcMain.cpp satÄ±r 2289-2307 birebir: í†µì—­ ì„œë¹„ìŠ¤ (chat scramble)
            // if (N3_CHAT_NORMAL == eCM || N3_CHAT_PRIVATE == eCM || N3_CHAT_SHOUT == eCM)
            if (gm != null && (channel == 1 || channel == 2 || channel == 5)) // Normal=1, Private=2, Shout=5
            {
                // C++ satÄ±r 2292: if (eNation != s_pPlayer->Nation() && !s_pPlayer->m_InfoExt.bCanTalkToOtherNation)
                if (nation != 0 && nation != gm.Nation && !gm.CanTalkToOtherNation)
                {
                    // C++ satÄ±r 2295-2296: GM kontrolÃ¼ â€” birebir
                    bool bIamManager = (0 == gm.Authority);
                    
                    // C++ satÄ±r 2294: CPlayerBase* pTalker = s_pOPMgr->UPCGetByID(iID, false);
                    bool bTalkerIsManager = false;
                    var entityMgr = EntropyOnline.World.EntityManager.Instance;
                    if (entityMgr != null)
                    {
                        var rp = entityMgr.GetRemotePlayer(senderId);
                        if (rp != null)
                        {
                            bTalkerIsManager = (0 == rp.Authority);
                        }
                    }
                    
                    // C++ satÄ±r 2299: if (!bIamManager && !bTalkerIsManager) â†’ scramble
                    if (!bIamManager && !bTalkerIsManager)
                    {
                        // C++ satÄ±r 2301-2305 birebir: szChat Ã¼zerinde ':' bul, sonrasÄ±nÄ± scramble
                        // szChat = szName + " : " + szMsg â†’ ':' HER ZAMAN bulunur
                        int colonIdx = szChat.IndexOf(':');
                        if (colonIdx >= 0)
                        {
                            char[] chars = szChat.ToCharArray();
                            var rng = new System.Random();
                            for (int i = colonIdx; i < chars.Length; i++)
                            {
                                chars[i] = (char)('!' + rng.Next(10)); // C++ birebir: '!' + rand() % 10
                            }
                            szChat = new string(chars);
                        }
                    }
                }
            }
            
            // C++ satÄ±r 2321: m_pUIChatDlg->AddChatMsg(eCM, szChat, crChat);
            // C++'da birleÅŸtirilmiÅŸ szChat doÄŸrudan UI'a gÃ¶nderilir
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.AddChatMessage(channel, szChat);
        }

        /// <summary>
        /// Zone deÄŸiÅŸiminde minimap yÃ¼kle.
        /// C++ GameProcMain.cpp InitZone() satÄ±r 4478-4480 birebir:
        /// float fWidth = ACT_WORLD->GetWidthByMeterWithTerrain();
        /// m_pUIStateBarAndMiniMap->LoadMap(pZoneData->szMiniMapFN, fWidth, fWidth);
        /// </summary>
        private void OnZoneChangeReceived(bool success, short zoneId, float spawnX, float spawnY, float spawnZ)
        {
            if (!success) return;
            if (KOUIManager.Instance == null) return;

            // C++ User.cpp:2769-2770: InitType3() + InitType4() â€” sunucu buff'larÄ± temizler
            // Client tarafÄ± bunu yansÄ±tÄ±r: tÃ¼m buff ikonlarÄ±nÄ± kaldÄ±r
            KOUIManager.Instance.ClearBuffs();

            // C++ __TABLE_ZONE::szMiniMapFN â†’ zone ID'ye gÃ¶re minimap dosya adÄ±
            string minimapFileName = KOUIManager.GetMiniMapFileName(zoneId);

            // C++ ACT_WORLD->GetWidthByMeterWithTerrain(): standart KO terrain boyutu
            const float DEFAULT_MAP_SIZE = 4096.0f;

            KOUIManager.Instance.LoadMiniMap(minimapFileName, DEFAULT_MAP_SIZE, DEFAULT_MAP_SIZE);

            // C++ GameProcMain::InitZone() satÄ±r 4483-4487 birebir:
            // Rogue class â†’ zoom 3.0f, diÄŸerleri â†’ zoom 6.0f
            var gm = GameManager.Instance;
            float fZoom = 6.0f;
            if (gm != null && IsRepresentRogue(gm.CharClass))
                fZoom = 3.0f;
            KOUIManager.Instance.SetMiniMapZoom(fZoom);
        }

        // ============================
        // BAR GÃœNCELLEME (KOUIManager Ã¼zerinden)
        // ============================

        private void RefreshAllBars()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            UpdateHpBar(gm.CurrentHp, gm.MaxHp);
            UpdateMpBar(gm.CurrentMp, gm.MaxMp);
            UpdateExpBar(gm.Experience, gm.Level);
        }

        private void UpdateHpBar(int current, int max)
        {
            if (max <= 0) max = 1;
            _targetHpFill = (float)current / max;
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.UpdateHP(_targetHpFill);
                // C++ UIStateBar.cpp UpdateHP() satÄ±r 305: m_pText_HP->SetString(fmt::format("{} / {}", iHP, iHPMax))
                KOUIManager.Instance.UpdateHPText(current, max);
            }
        }

        private void UpdateMpBar(int current, int max)
        {
            if (max <= 0) max = 1;
            _targetMpFill = (float)current / max;
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.UpdateMP(_targetMpFill);
                // C++ UIStateBar.cpp UpdateMSP() satÄ±r 283: m_pText_MP->SetString(fmt::format("{} / {}", iMSP, iMSPMax))
                KOUIManager.Instance.UpdateMPText(current, max);
            }
        }

        private void UpdateExpBar(long currentExp, short level)
        {
            long requiredExp = EntropyOnline.Shared.LevelExpTable.GetExpForLevel(level);
            if (requiredExp <= 0) requiredExp = 100;
            float ratio = Mathf.Clamp01((float)currentExp / requiredExp);
            _targetExpFill = ratio;
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.UpdateEXP(ratio);
                // C++ UIStateBar.cpp UpdateExp() satÄ±r 258: m_pText_Exp->SetString(fmt::format("{:.2f} %", iPercentage2))
                KOUIManager.Instance.UpdateExpText(currentExp, requiredExp);
            }
        }

        // ============================
        // FLOATING TEXT SÄ°STEMÄ°
        // ============================

        private void SpawnFloatingText(string text, Color color, float delay = 0f,
            bool isLarge = false, int fontSize = 0)
        {
            if (_floatingTextContainer == null || _overlayCanvas == null) return;

            var obj = new GameObject("FloatingText");
            obj.transform.SetParent(_floatingTextContainer.transform, false);

            var rt = obj.AddComponent<RectTransform>();
            float offsetX = Random.Range(-80f, 80f);
            rt.anchoredPosition = new Vector2(offsetX, 300f + delay * 100f);
            rt.sizeDelta = new Vector2(400, 60);

            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize > 0 ? fontSize : (isLarge ? 32 : 24);
            if (_cachedArialFont == null)
                _cachedArialFont = Font.CreateDynamicFontFromOSFont("Arial", 24);
            txt.font = _cachedArialFont;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;

            var shadow = obj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
            shadow.effectDistance = new Vector2(2, -2);

            _floatingTexts.Add(new FloatingText
            {
                TextComponent = txt,
                RectTransform = rt,
                StartTime = Time.time + delay,
                Duration = 1.5f,
                StartY = rt.anchoredPosition.y
            });
        }

        private void UpdateFloatingTexts()
        {
            for (int i = _floatingTexts.Count - 1; i >= 0; i--)
            {
                var ft = _floatingTexts[i];
                float elapsed = Time.time - ft.StartTime;

                if (elapsed < 0) continue;

                if (elapsed >= ft.Duration)
                {
                    Destroy(ft.TextComponent.gameObject);
                    _floatingTexts.RemoveAt(i);
                    continue;
                }

                float t = elapsed / ft.Duration;
                var pos = ft.RectTransform.anchoredPosition;
                pos.y = ft.StartY + t * 120f;
                ft.RectTransform.anchoredPosition = pos;

                if (t > 0.6f)
                {
                    float alpha = 1f - (t - 0.6f) / 0.4f;
                    var c = ft.TextComponent.color;
                    c.a = alpha;
                    ft.TextComponent.color = c;
                }

                float scale = t < 0.1f ? Mathf.Lerp(1.5f, 1f, t / 0.1f) : 1f;
                ft.RectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        // ============================
        // PUBLIC API
        // ============================

        public void ForceRefresh()
        {
            RefreshAllBars();
        }

        public void ShowFloatingText(string text, Color color)
        {
            SpawnFloatingText(text, color);
        }

        // ============================
        // BUFF/DEBUFF HANDLER'LARI
        // ============================

        /// <summary>
        /// Buff uygulandÄ±ÄŸÄ±nda ikon ekle.
        /// C++ GameProcMain â†’ UIStateBar::AddMagic(pSkill, fDuration)
        /// UIStateBar.cpp satÄ±r 729-763 birebir flow.
        /// </summary>
        private void OnBuffAppliedReceived(int magicNum, byte buffType, short duration, string skillName)
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.AddBuff(magicNum, buffType, duration, skillName);
        }

        /// <summary>
        /// Buff sÃ¼resi dolduÄŸunda stat delta'larÄ±nÄ± geri al + ikonu kaldÄ±r.
        /// C++ birebir: MsgRecv_BuffType (MagicSkillMng.cpp:2064-2151)
        /// MsgRecv_BuffType iÃ§inde hem delta revert hem DelMagic Ã§aÄŸrÄ±lÄ±r (cpp:2073-2074).
        /// AyrÄ±ca UI icon kaldÄ±rmaya gerek yok â€” MsgRecv_BuffType iÃ§inde yapÄ±lÄ±yor.
        /// </summary>
        private void OnBuffExpiredReceived(byte buffType)
        {
            // Open-KO birebir: MsgRecv_BuffType (MagicSkillMng.cpp:2064-2151)
            // cpp:2073-2074: pSkill = Find(it->second) â†’ DelMagic(pSkill) â†’ UI icon kaldÄ±rÄ±r
            // cpp:2075: m_ListBuffTypeID.erase(it) â†’ listeden sil
            // cpp:2078-2151: BuffType switch â†’ stat delta revert
            var magicMgr = EntropyOnline.Combat.KOMagicSkillManager.Instance;
            if (magicMgr != null)
                magicMgr.MsgRecv_BuffType(buffType);
        }

        /// <summary>
        /// C++ UIVarious::UpdateAllStates() satÄ±r 1694-1698 birebir:
        /// Stat deÄŸiÅŸimi sonrasÄ± tÃ¼m stat deÄŸerlerini gÃ¼ncelle.
        /// </summary>
        private void OnStatChangeResult(byte success, short str, short sta, short dex, short intel, short cha, short statPoints, long gold)
        {
            if (success == 0) return;
            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.StatStr = str;
            gm.StatSta = sta;
            gm.StatDex = dex;
            gm.StatInt = intel;
            gm.StatCha = cha;
            gm.StatPoints = statPoints;
            gm.Gold = gold;

            if (KOUIManager.Instance != null)
            {
                long expMax = EntropyOnline.Shared.LevelExpTable.GetExpForLevel(gm.Level);
                KOUIManager.Instance.UpdateCharacterInfo(
                    gm.CharacterName, gm.Level, str, sta, dex, intel, cha,
                    statPoints, gm.CurrentHp, gm.MaxHp, gm.CurrentMp, gm.MaxMp,
                    gm.Experience, expMax, gm.SkillPoints,
                    gm.Loyalty, gm.LoyaltyMonthly);
                KOUIManager.Instance.UpdateGold(gold);
            }
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:5603-5611 MsgRecv_SkillChange
        /// Bu paket sadece fail durumunda gelir â€” sunucu doÄŸru deÄŸeri zorla geri yazar.
        /// m_iSkillInfo[iType] = iValue   // sunucu deÄŸerini set et
        /// m_iSkillInfo[0]++              // serbest puanÄ± geri ver
        /// InitIconUpdate()              // skill tree ikonlarÄ±nÄ± yenile
        /// </summary>
        private void OnSkillPointResult(byte type, byte value)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Open-KO birebir: GameProcMain.cpp:5608-5610
            // KOSkillTreeManager varsa onu kullan (o zaten GameManager'Ä± da senkronize eder)
            var skillMgr = KOSkillTreeManager.Instance;
            if (skillMgr != null)
            {
                skillMgr.MsgRecv_SkillChange(type, value);
            }
            else
            {
                // Fallback: KOSkillTreeManager yoksa doÄŸrudan GameManager gÃ¼ncelle
                // cpp:5608: m_pUISkillTreeDlg->m_iSkillInfo[iType] = iValue;
                if (type >= 1 && type <= 8 && gm.SkillTreePoints != null)
                    gm.SkillTreePoints[type] = value;

                // cpp:5609: m_pUISkillTreeDlg->m_iSkillInfo[0]++;
                gm.SkillPoints++;
            }

            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.AddMsgOutput("Failed to allocate skill points.", KOUIManager.D3DColorToUnity(0xffff3b3b));
                KOUIManager.Instance.RefreshSkillTreeUI();
            }
        }

        /// <summary>
        /// C++ GameBase.cpp GetRepresentClass() satÄ±r 398-404 birebir:
        /// CLASS_KA_ROGUE/HUNTER/PENETRATOR/EL_ROGUE/RANGER/ASSASSIN â†’ CLASS_REPRESENT_ROGUE
        /// </summary>
        private static bool IsRepresentRogue(byte charClass)
        {
            return charClass switch
            {
                102 => true,  // CLASS_KA_ROGUE
                107 => true,  // CLASS_KA_HUNTER
                108 => true,  // CLASS_KA_PENETRATOR
                202 => true,  // CLASS_EL_ROGUE
                207 => true,  // CLASS_EL_RANGER
                208 => true,  // CLASS_EL_ASSASSIN
                _ => false
            };
        }
    }

    internal class FloatingText
    {
        public Text TextComponent;
        public RectTransform RectTransform;
        public float StartTime;
        public float Duration;
        public float StartY;
    }
}
