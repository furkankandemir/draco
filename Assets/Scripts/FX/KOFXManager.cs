using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using EntropyOnline.Import;
using KOImport;
using EntropyOnline.World;

namespace KO
{
    /// <summary>
    /// Open-KO birebir: CN3FXMgr (N3FXMgr.h/cpp)
    /// FX Bundle yöneticisi — efekt tetikleme, yaşam döngüsü ve temizleme.
    ///
    /// Akış:
    ///   1. fx.tbl yüklenir → FXID → .fxb dosya yolu eşlemesi
    ///   2. TriggerBundle(FXID) → .fxb parse → FxBundleData → efekt instantiate
    ///   3. Template cache (m_OriginBundle) → duplicate (m_ListBundle)
    ///   4. Tick() her frame → yaşam süresi dolunca temizle
    ///
    /// Referans:
    ///   CN3FXMgr::TriggerBundle — N3FXMgr.cpp:44-148
    ///   CN3FXMgr::Tick — N3FXMgr.cpp:237-643
    ///   CN3FXMgr::Stop — N3FXMgr.cpp:153-191
    ///   CN3FXMgr::ClearAll — N3FXMgr.cpp:681-704
    /// </summary>
    public class KOFXManager : MonoBehaviour
    {
        public static KOFXManager Instance { get; private set; }
        public static float LastProjectileCastTime = -100f;
        public static Vector3 LastProjectileCastPos = Vector3.zero;

        public static float GetProjectileDelayForTarget(int targetId)
        {
            if (UnityEngine.Time.time - LastProjectileCastTime < 1.0f)
            {
                Vector3 targetPos = ResolveEntityFootPosition(targetId);
                if (targetPos != Vector3.zero && LastProjectileCastPos != Vector3.zero)
                {
                    float dist = Vector3.Distance(LastProjectileCastPos, targetPos);
                    return dist / 20.0f;
                }
            }
            return 0f;
        }

        /// <summary>
        /// Open-KO birebir: m_OriginBundle — template cache.
        /// Key = lowercase .fxb filename, Value = __FXBundleOrigin (N3FXMgr.h:18-30)
        /// N3FXMgr.h:44 m_OriginBundle map.
        /// </summary>
        private readonly Dictionary<string, FxBundleOrigin> _originBundle = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Open-KO birebir: m_ListBundle — aktif bundle instance'ları.
        /// N3FXMgr.cpp:43 m_ListBundle list.
        /// </summary>
        private readonly List<FxBundleInstance> _activeBundles = new();

        /// <summary>
        /// Open-KO birebir: m_fOriginLimitedTime = 60.0f
        /// N3FXMgr.cpp:19
        /// </summary>
        private const float ORIGIN_LIMITED_TIME = 60.0f;



        /// <summary>fx.tbl yüklendi mi?</summary>
        public bool IsReady => FxTableParser.IsLoaded;

        // === FXID sabitleri — GameDef.h:1348-1378 birebir ===
        public const int FXID_CLASS_CHANGE             = 603;
        public const int FXID_BLOOD                    = 10002;
        public const int FXID_LEVELUP_KARUS            = 10012;
        public const int FXID_LEVELUP_ELMORAD          = 10018;
        public const int FXID_REGEN_ELMORAD            = 10019;
        public const int FXID_REGEN_KARUS              = 10020;
        public const int FXID_SWORD_FIRE_MAIN          = 10021;
        public const int FXID_SWORD_FIRE_TAIL          = 10022;
        public const int FXID_SWORD_FIRE_TARGET        = 10031;
        public const int FXID_SWORD_ICE_MAIN           = 10023;
        public const int FXID_SWORD_ICE_TAIL           = 10024;
        public const int FXID_SWORD_ICE_TARGET         = 10032;
        public const int FXID_SWORD_LIGHTNING_MAIN     = 10025;
        public const int FXID_SWORD_LIGHTNING_TAIL     = 10026;
        public const int FXID_SWORD_LIGHTNING_TARGET   = 10033;
        public const int FXID_SWORD_POISON_MAIN        = 10027;
        public const int FXID_SWORD_POISON_TAIL        = 10028;
        public const int FXID_SWORD_POISON_TARGET      = 10034;
        public const int FXID_REGION_TARGET_EL_ROGUE   = 10035;
        public const int FXID_REGION_TARGET_EL_WIZARD  = 10036;
        public const int FXID_REGION_TARGET_EL_PRIEST  = 10037;
        public const int FXID_REGION_TARGET_KA_ROGUE   = 10038;
        public const int FXID_REGION_TARGET_KA_WIZARD  = 10039;
        public const int FXID_REGION_TARGET_KA_PRIEST  = 10040;
        public const int FXID_CLAN_RANK_1              = 10041;
        public const int FXID_WARP_KARUS               = 10046;
        public const int FXID_WARP_ELMORAD             = 10047;
        public const int FXID_REGION_POISON            = 10100;
        public const int FXID_TARGET_POINTER           = 30001;
        public const int FXID_ZONE_POINTER             = 30002;

        // === FX_BUNDLE_MOVE types — N3FXDef.h:64-73 birebir (e_FXBundleAct) ===
        public const int FX_BUNDLE_MOVE_DIR_FIXEDTARGET          = 0;
        public const int FX_BUNDLE_MOVE_DIR_FLEXABLETARGET       = 1;
        public const int FX_BUNDLE_MOVE_DIR_FLEXABLETARGET_RATIO = 2;
        public const int FX_BUNDLE_MOVE_CURVE_FIXEDTARGET        = 3;
        public const int FX_BUNDLE_MOVE_DIR_SLOW                 = 4;
        public const int FX_BUNDLE_REGION_POISON                 = 5;
        public const int FX_BUNDLE_MOVE_NONE                     = -1;

        /// <summary>FX visual renderer referansı</summary>
        private KOFXRenderer _renderer;

        /// <summary>Visual renderer getter</summary>
        public KOFXRenderer Renderer => _renderer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // KOFXRenderer aynı GameObject'e ekle
            _renderer = gameObject.AddComponent<KOFXRenderer>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// FX sistemi başlat — fx.tbl yükle.
        /// Open-KO: CGameBase::StaticMemberInit → s_pTbl_FXSource.LoadFromFile("Data\\fx.tbl")
        /// </summary>
        public void Initialize()
        {
            FxTableParser.Load("Data/fx.tbl");
        }

        /// <summary>
        /// Open-KO birebir: CN3FXMgr::TriggerBundle (target ID overload)
        /// N3FXMgr.cpp:44-95
        ///
        /// sourceId: efekti tetikleyen entity ID
        /// sourceJoint: kaynak joint index
        /// fxId: FX ID (ör: FXID_BLOOD)
        /// targetId: hedef entity ID
        /// targetJoint: hedef joint index
        /// idx: bundle index (default 0)
        /// moveType: hareket tipi (default FX_BUNDLE_MOVE_NONE)
        /// </summary>
        public void TriggerBundle(int sourceId, int sourceJoint, int fxId, int targetId, int targetJoint, int idx = 0, int moveType = FX_BUNDLE_MOVE_NONE)
        {
            // N3FXMgr.cpp:46-48
            var fxEntry = FxTableParser.Find(fxId);
            if (fxEntry == null)
            {
                Debug.LogWarning($"[KOFXManager] FXID {fxId} fx.tbl'de bulunamadı");
                return;
            }

            if (ShouldHideEffect(fxId, fxEntry, sourceId, targetId)) return;

            // Check for custom FX Override
            string effectName = Path.GetFileNameWithoutExtension(fxEntry.FileName).ToLowerInvariant();
            GameObject overridePrefab = Resources.Load<GameObject>($"FXOverride/{effectName}");

            string fxbKey = fxEntry.FileName.ToLowerInvariant().Replace('\\', '/');

            // Get or load template — N3FXMgr.cpp:53-94
            var origin = GetOrLoadOrigin(fxbKey, fxEntry.FileName);
            if (origin == null) return;

            // Create instance — N3FXMgr.cpp:59-70 (from cached) or 75-93 (from new)
            var instance = new FxBundleInstance
            {
                Data = origin.Bundle,
                CacheKey = fxbKey,
                FxId = fxId,
                Idx = idx,
                MoveType = moveType,
                SourceId = sourceId,
                SourceJoint = sourceJoint,
                TargetId = targetId,
                TargetJoint = targetJoint,
                SoundId = (int)fxEntry.SoundId,
                State = FxBundleState.Live,
                Life = 0f,
                MaxLife = origin.Bundle.Life,
                IsRegion = (sourceId != GetMyEntityId()),
                BundleVelocity = origin.Bundle.Velocity
            };

            // Open-KO birebir: CN3FXBundleGame::Trigger (N3FXBundleGame.cpp:34-126)
            // Kaynak pozisyonu çözümle — cpp:42-58 (Eğer geçerli bir kemik indeksi varsa o kemiğin pozisyonu alınır)
            instance.Pos = ResolveEntityJointPosition(sourceId, sourceJoint);
            
            // Set initial DestPos: cpp:60 — m_vDestPos = pSource->Position() + pSource->Direction();
            instance.DestPos = ResolveEntityFootPosition(sourceId) + ResolveEntityDirection(sourceId);

            // Hedef pozisyonu çözümle — cpp:38-41, 64-111
            // C++ birebir: target && target != source ise hedef pozisyona göre güncelle
            if (targetId >= 0 && targetId != sourceId)
            {
                Vector3 tgtPos = ResolveEntityDestPosition(targetId, targetJoint);
                if (tgtPos.sqrMagnitude > 0.001f)
                {
                    instance.DestPos = tgtPos;
                }
            }
            
            // cpp:113-117
            instance.Distance = (instance.DestPos - instance.Pos).magnitude;
            instance.Height = instance.Distance / 2.0f;
            
            if (targetId == sourceId || moveType == FX_BUNDLE_MOVE_NONE)
            {
                instance.Dir = ResolveEntityDirection(sourceId);
            }
            else
            {
                Vector3 rawDir = instance.DestPos - instance.Pos;
                instance.Dir = rawDir.sqrMagnitude > 0.001f ? rawDir.normalized : Vector3.forward;
            }

            // cpp:119-123 — bStatic ise position overload'a yönlendir
            if (origin.Bundle.IsStatic)
            {
                instance.IsRegion = true;
                instance.Pos = instance.DestPos;
            }

            // Open-KO birebir: CN3FXBundle::Trigger (N3FXBundle.cpp:490-496)
            // Her part'ı READY state'e al
            instance.PartInstances = new FxPartInstance[origin.Bundle.Parts.Count];
            for (int i = 0; i < origin.Bundle.Parts.Count; i++)
            {
                var partData = origin.Bundle.Parts[i];
                instance.PartInstances[i] = new FxPartInstance
                {
                    Data = partData,
                    State = FxPartState.Ready,  // N3FXBundle.cpp:494
                    CurrLife = 0f,
                    CurrVelocity = partData.Velocity,
                    CurrPos = partData.Position
                };
            }

            // C++ birebir: CN3FXBundle::Trigger → Init() (N3FXBundle.cpp:490-496)
            // Her part'ın CurrSizeX/Y değerlerini BillboardData'dan set et
            InitParts(instance);

            if (overridePrefab != null)
            {
                float customScale = 2.5f;


                if (targetId >= 0)
                {
                    customScale *= ResolveEntityScale(targetId);
                }

                Vector3 spawnPos = (targetId >= 0 && targetId != sourceId && instance.DestPos.sqrMagnitude > 0.001f) ? instance.DestPos : instance.Pos;
                float finalScale = customScale * overridePrefab.transform.localScale.x;
                GameObject effectInstance = UnityEngine.Object.Instantiate(overridePrefab, spawnPos, Quaternion.identity);
                SetupOverrideEffect(effectInstance, effectName, finalScale, instance.Idx);
                instance.OverrideInstance = effectInstance;
            }

            _activeBundles.Add(instance);

            // N3FXMgr.cpp:71 / 92 — pSrc->iNum++
            origin.RefCount++;
            origin.LimitedTime = 0f; // reset eviction timer

        }

        private int _lastAreaSkillId = -1;
        private Vector3 _lastAreaSkillPos = Vector3.zero;
        private float _lastAreaSkillTime = 0f;

        public void TriggerAreaTargetFX(int sourceId, int fxId, Vector3 targetPos, int skillId)
        {
            if (fxId <= 0) return;

            float now = UnityEngine.Time.time;
            if (_lastAreaSkillId == skillId 
                && Vector3.SqrMagnitude(_lastAreaSkillPos - targetPos) < 1.0f 
                && now - _lastAreaSkillTime < 2.0f)
            {
                return;
            }

            _lastAreaSkillId = skillId;
            _lastAreaSkillPos = targetPos;
            _lastAreaSkillTime = now;

            TriggerBundle(sourceId, 0, fxId, targetPos);
        }

        /// <summary>
        /// Open-KO birebir: CN3FXMgr::TriggerBundle (target position overload)
        /// </summary>
        public void TriggerBundle(int sourceId, int sourceJoint, int fxId, Vector3 targetPos, int idx = 0, int moveType = FX_BUNDLE_MOVE_NONE)
        {
            var fxEntry = FxTableParser.Find(fxId);
            if (fxEntry == null) return;

            if (ShouldHideEffect(fxId, fxEntry, sourceId, -1)) return;

            // Check for custom FX Override
            string effectName = Path.GetFileNameWithoutExtension(fxEntry.FileName).ToLowerInvariant();
            GameObject overridePrefab = Resources.Load<GameObject>($"FXOverride/{effectName}");

            string fxbKey = fxEntry.FileName.ToLowerInvariant().Replace('\\', '/');
            var origin = GetOrLoadOrigin(fxbKey, fxEntry.FileName);
            if (origin == null) return;

            var instance = new FxBundleInstance
            {
                Data = origin.Bundle,
                CacheKey = fxbKey,
                FxId = fxId,
                Idx = idx,
                MoveType = moveType,
                SourceId = sourceId,
                SourceJoint = sourceJoint,
                TargetId = -1,
                TargetJoint = 0,
                SoundId = (int)fxEntry.SoundId,
                State = FxBundleState.Live,
                Life = 0f,
                MaxLife = origin.Bundle.Life,
                IsRegion = (sourceId != GetMyEntityId()),
                BundleVelocity = origin.Bundle.Velocity
            };

            // Open-KO birebir: CN3FXBundleGame::Trigger pos overload (N3FXBundleGame.cpp:128-160)
            // Kaynak pozisyonu çözümle — cpp:132-150 (Eğer geçerli bir kemik indeksi varsa o kemiğin pozisyonu alınır)
            instance.Pos = ResolveEntityJointPosition(sourceId, sourceJoint);
            instance.DestPos = targetPos;                    // cpp:152
            // cpp:154-158
            instance.Distance = (instance.DestPos - instance.Pos).magnitude;
            instance.Height = instance.Distance / 2.0f;
            Vector3 rawDir = instance.DestPos - instance.Pos;
            if (origin.Bundle.IsStatic || moveType == FX_BUNDLE_MOVE_NONE)
            {
                // C++ N3FXBundleGame.cpp:276 — m_vDir.y = 0.0f; m_vDir.Normalize();
                rawDir.y = 0f;
                instance.Pos = instance.DestPos;
            }
            instance.Dir = rawDir.sqrMagnitude > 0.001f ? rawDir.normalized : Vector3.forward;

            // Open-KO birebir: CN3FXBundle::Trigger (N3FXBundle.cpp:490-496)
            instance.PartInstances = new FxPartInstance[origin.Bundle.Parts.Count];
            for (int i = 0; i < origin.Bundle.Parts.Count; i++)
            {
                var partData = origin.Bundle.Parts[i];
                instance.PartInstances[i] = new FxPartInstance
                {
                    Data = partData,
                    State = FxPartState.Ready,
                    CurrLife = 0f,
                    CurrVelocity = partData.Velocity,
                    CurrPos = partData.Position
                };
            }

            // C++ birebir: CN3FXBundle::Trigger → Init() (N3FXBundle.cpp:490-496)
            InitParts(instance);

            if (overridePrefab != null)
            {
                float customScale = 2.5f;
                if (moveType != FX_BUNDLE_MOVE_NONE)
                {
                    if (effectName == "fire_flying_1" || effectName == "lighting_flying_1" || effectName == "ice_flying_1") customScale = 1.5f;
                    else if (effectName == "fire_flying_2" || effectName == "ice_flying_2") customScale = 2.0f;
                    else if (effectName == "fire_flying_3" || effectName == "ice_flying_3" || effectName == "lighting_flying_2") customScale = 1.0f;

                    GameObject effectInstance = UnityEngine.Object.Instantiate(overridePrefab, instance.Pos, Quaternion.identity);
                    SetupOverrideEffect(effectInstance, effectName, customScale, instance.Idx);
                    instance.OverrideInstance = effectInstance;
                }
                else // moveType == FX_BUNDLE_MOVE_NONE
                {
                    Quaternion spawnRot = Quaternion.identity;
                    var hits = Physics.RaycastAll(new Vector3(targetPos.x, targetPos.y + 10.0f, targetPos.z), Vector3.down, 20.0f);
                    foreach (var hit in hits)
                    {
                        if (hit.collider is TerrainCollider || hit.collider.gameObject.name.Contains("Terrain") || hit.collider.gameObject.name.Contains("TerrainObj"))
                        {
                            spawnRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                            break;
                        }
                    }
                    Vector3 finalTargetPos = targetPos + (spawnRot * Vector3.up * 0.05f);
                    float finalScale = customScale * overridePrefab.transform.localScale.x;

                    GameObject effectInstance = UnityEngine.Object.Instantiate(overridePrefab, finalTargetPos, spawnRot);
                    SetupOverrideEffect(effectInstance, effectName, finalScale, idx);
                    instance.OverrideInstance = effectInstance;
                }
            }

            _activeBundles.Add(instance);

            // N3FXMgr.cpp:125 / 145 — pSrc->iNum++
            origin.RefCount++;
            origin.LimitedTime = 0f;
        }

        /// <summary>
        /// Open-KO birebir: CN3FXMgr::Stop
        /// N3FXMgr.cpp:153-191
        /// </summary>
        public void Stop(int sourceId, int targetId, int fxId = -1, int idx = 0, bool immediately = false)
        {
            for (int i = _activeBundles.Count - 1; i >= 0; i--)
            {
                var bundle = _activeBundles[i];
                if (fxId < 0)
                {
                    // N3FXMgr.cpp:167-168
                    if (bundle.SourceId == sourceId && (idx == -1 || idx == -2 || bundle.Idx == idx))
                        StopBundle(bundle, immediately);
                }
                else
                {
                    // N3FXMgr.cpp:185-186
                    if (bundle.SourceId == sourceId && bundle.FxId == fxId && (idx == -1 || idx == -2 || bundle.Idx == idx))
                        StopBundle(bundle, immediately);
                }
            }
        }

        /// <summary>
        /// Open-KO birebir: CN3FXMgr::ClearAll
        /// N3FXMgr.cpp:681-704
        /// </summary>
        public void ClearAll()
        {
            _activeBundles.Clear();
            _originBundle.Clear();
        }

        /// <summary>
        /// Open-KO birebir: CN3FXMgr::StopMine
        /// N3FXMgr.cpp:215-232
        /// Kendi oyuncunun sourceId'sine sahip tüm bundle'ları durdurur.
        /// UIDead.cpp:263 — regen öncesi çağrılır.
        /// </summary>
        public void StopMine(int myId)
        {
            // N3FXMgr.cpp:217-231
            for (int i = _activeBundles.Count - 1; i >= 0; i--)
            {
                var bundle = _activeBundles[i];
                // N3FXMgr.cpp:227-228
                if (bundle.SourceId == myId)
                    StopBundle(bundle, true); // immediately = true
            }
        }

        /// <summary>
        /// Open-KO birebir: CN3FXMgr::SetBundlePos
        /// N3FXMgr.cpp:196-210
        /// Region efektinin hedef pozisyonunu günceller.
        /// </summary>
        public void SetBundlePos(int fxId, int idx, Vector3 pos)
        {
            // N3FXMgr.cpp:198-209
            foreach (var bundle in _activeBundles)
            {
                if (bundle.FxId == fxId && bundle.Idx == idx)
                {
                    bundle.DestPos = pos; // cpp:204
                    return;
                }
            }
        }

        // ========================================================
        // Open-KO birebir: CMagicSkillMng::m_MySelf (idx → magicID map)
        // C++'da bu dictionary CMagicSkillMng sınıfında. Unity'de
        // KOMagicSkillManager'a delege ediyoruz (tek kaynak).
        // ========================================================

        /// <summary>Open-KO birebir: CMagicSkillMng::AddIdx — KOMagicSkillManager'a delege</summary>
        public int AddIdx(uint magicId, int iNum = 1)
        {
            var mgr = EntropyOnline.Combat.KOMagicSkillManager.Instance;
            if (mgr == null) return 0;
            return mgr.AddIdx((int)magicId, iNum);
        }

        /// <summary>Open-KO birebir: CMagicSkillMng::RemoveIdx — KOMagicSkillManager'a delege</summary>
        public void RemoveIdx(int idx)
        {
            var mgr = EntropyOnline.Combat.KOMagicSkillManager.Instance;
            mgr?.RemoveIdx(idx);
        }

        /// <summary>Open-KO birebir: CMagicSkillMng::GetMagicID — KOMagicSkillManager'a delege</summary>
        public uint GetMagicID(int idx)
        {
            var mgr = EntropyOnline.Combat.KOMagicSkillManager.Instance;
            if (mgr == null) return 0;
            return (uint)mgr.GetMagicID(idx);
        }

        // C++ paket sabitleri — packets.h, PacketDef.h
        private const byte WIZ_MAGIC_PROCESS_OPCODE = 0x31; // packets.h:58
        private const byte N3_SP_MAGIC_EFFECTING = 0x03;     // PacketDef.h:93 birebir
        private const byte N3_SP_MAGIC_FAIL = 0x04;           // PacketDef.h:94 birebir
        private const short SKILLMAGIC_FAIL_KILLFLYING = -101; // packets.h:460

        /// <summary>
        /// Open-KO birebir: CN3FXMgr::Tick collision (N3FXMgr.cpp:295-639)
        /// </summary>
        private void TickCollision(FxBundleInstance bundle, float dt)
        {
            // cpp:295 — sadece hareket eden ve canlı
            if (bundle.MoveType == FX_BUNDLE_MOVE_NONE || bundle.State != FxBundleState.Live)
                return;

            int myId = GetMyEntityId();
            if (myId < 0) return;

            // cpp:297-301 — source UPC/NPC yoksa ve biz de değilsek → dur
            bool sourceIsUPC = ResolveEntityPosition(bundle.SourceId).sqrMagnitude > 0.001f;
            if (!sourceIsUPC && myId != bundle.SourceId)
            {
                StopBundle(bundle, false);
                return;
            }

            // cpp:303-307 — dwToMe belirleme
            // dwToMe==1: benim attığım
            // dwToMe==2: bana atılan VE source UPC olarak bulunamıyor (NPC source)
            uint dwToMe = 0;
            if (myId == bundle.SourceId)
                dwToMe = 1;
            else if (myId == bundle.TargetId && !IsUPC(bundle.SourceId)) // cpp:306
                dwToMe = 2;

            if (dwToMe == 0) return;

            bool bCol = false;
            Vector3 vCol = Vector3.zero;

            // cpp:319-366 — dwToMe==2: bana atılan, kendi bounding box ile çarpışma
            if (dwToMe == 2 && !bundle.IsRegion)
            {
                Vector3 myPos = ResolveEntityPosition(myId);
                float distToMe = (bundle.Pos - myPos).magnitude;
                if (distToMe < 16.0f) // cpp:319
                {
                    // cpp:321-322 — s_pPlayer->CheckCollisionByBox(m_vPos, m_vPos + m_vDir * m_fVelocity * s_fSecPerFrm)
                    Vector3 v0 = bundle.Pos;
                    Vector3 v1 = bundle.Pos + bundle.Dir * bundle.BundleVelocity * dt;
                    if (CheckCollisionByBox(myId, v0, v1, out vCol))
                    {
                        bundle.Pos = vCol;               // cpp:325
                        StopBundle(bundle, false);        // cpp:326
                        uint iMagicID = GetMagicID(bundle.Idx); // cpp:327
                        SendEffectingPacket(iMagicID, bundle.SourceId, myId, bundle.Idx);
                        SendFailPacket(iMagicID, bundle.SourceId, myId, vCol, bundle.Idx);
                        return; // cpp:364 break
                    }
                }
            }

            // cpp:368-422 — UPC (diğer oyuncular) ile çarpışma
            if (!bCol && !bundle.IsRegion)
            {
                var em = EntropyOnline.World.EntityManager.Instance;
                if (em != null)
                {
                    foreach (var kvp in em.GetAllRemotePlayers())
                    {
                        var pUPC = kvp.Value;
                        if (pUPC.Root == null) continue;
                        
                        // Ölü remote player'lar ile çarpışmayı engelle
                        var rpe = pUPC.Root.GetComponent<RemotePlayerEntity>();
                        if (rpe != null && !rpe.IsAlive)
                            continue;

                        // cpp:371 — dwToMe==1 && !IsHostileTarget → skip
                        if (dwToMe == 1 && !IsHostileTarget(pUPC.Nation))
                            continue;

                        // cpp:374 — pUPC->Position() — offset yok, C++ birebir
                        Vector3 upcPos = pUPC.Root.transform.position;
                        float distUpc = (bundle.Pos - upcPos).magnitude;
                        if (distUpc > 16.0f) continue; // cpp:374-375

                        // cpp:377-378 — pUPC->CheckCollisionByBox
                        Vector3 v0 = bundle.Pos;
                        Vector3 v1 = bundle.Pos + bundle.Dir * bundle.BundleVelocity * dt;
                        if (CheckCollisionByBox((int)pUPC.CharId, v0, v1, out vCol))
                        {
                            bCol = true;              // cpp:380
                            bundle.Pos = vCol;        // cpp:381
                            StopBundle(bundle, false); // cpp:382
                            uint iMagicID = GetMagicID(bundle.Idx);
                            int upcId = (int)pUPC.CharId;
                            SendEffectingPacket(iMagicID, bundle.SourceId, upcId, bundle.Idx);
                            SendFailPacket(iMagicID, bundle.SourceId, upcId, vCol, bundle.Idx);
                            break; // cpp:420
                        }
                    }
                }
            }

            // cpp:424-542 — NPC ile çarpışma
            if (!bCol && !bundle.IsRegion)
            {
                Vector3 vNext = bundle.Pos + bundle.Dir * (bundle.BundleVelocity * dt * 1.2f); // cpp:426
                float fDistTmp = bundle.BundleVelocity * dt * 1.2f; // cpp:451
                var koEntities = FindObjectsByType<KOEntity>(FindObjectsInactive.Exclude);
                foreach (var npc in koEntities)
                {
                    // Ölü monster/NPC'ler ile çarpışmayı engelle
                    if (npc.IsDead)
                        continue;

                    // cpp:436-437 — dwToMe==1 && !IsHostileTarget → skip
                    if (dwToMe == 1 && !IsHostileTargetNPC(npc))
                        continue;

                    // cpp:442 — pNPC->Position() — offset yok, C++ birebir
                    Vector3 npcPos = npc.transform.position;
                    float distNpc = (bundle.Pos - npcPos).magnitude;
                    if (distNpc > 16.0f) continue; // cpp:442

                    // cpp:445-496 — bounding box merkezi ile mesafe kontrolü
                    {
                        Vector3 vDestPos = npcPos;
                        var npcRenderer = npc.GetComponentInChildren<Renderer>();
                        if (npcRenderer != null)
                            vDestPos = npcRenderer.bounds.center;
                        else
                            vDestPos = npcPos + Vector3.up * 0.9f; // fallback gövde merkezi

                        float distToCenter = (bundle.Pos - vDestPos).magnitude;

                        // NPC'nin bounding radius'u — C++ birebir: pNPC->Radius()
                        float npcRadius = 1.0f;
                        if (npcRenderer != null)
                            npcRadius = Mathf.Max(npcRenderer.bounds.extents.x, npcRenderer.bounds.extents.z);
                        var capsule = npc.GetComponent<CapsuleCollider>();
                        if (capsule != null)
                            npcRadius = Mathf.Max(npcRadius, capsule.radius * npc.transform.localScale.x);

                        if (distToCenter <= fDistTmp + npcRadius)
                        {
                            bCol = true;
                            vCol = vDestPos;
                            StopBundle(bundle, false);
                            uint iMagicID = GetMagicID(bundle.Idx);
                            SendEffectingPacket(iMagicID, bundle.SourceId, (int)npc.ServerInstanceId, bundle.Idx);
                            SendFailPacket(iMagicID, bundle.SourceId, (int)npc.ServerInstanceId, vCol, bundle.Idx);
                            break;
                        }
                    }
                }
            }

            // cpp:545-592 — Shape (dünya objesi) ile çarpışma
            if (!bCol)
            {
                Vector3 shapeStart = bundle.Pos;
                Vector3 shapeEnd = bundle.Pos + bundle.Dir * bundle.BundleVelocity * dt;
                if (Physics.Linecast(shapeStart, shapeEnd, out RaycastHit shapeHit))
                {
                    // Unity adaptasyonu: C++'ta entity'ler ayrı sistemde,
                    // Unity'de aynı fizik sahnesinde olduğu için entity collider'lar hariç tutuluyor.
                    bool isEntity = shapeHit.collider.GetComponentInParent<KOEntity>() != null
                        || shapeHit.collider.GetComponentInParent<EntropyOnline.Character.PlayerController>() != null;
                    if (!isEntity)
                    {
                        bCol = true;                  // cpp:551
                        vCol = shapeHit.point;
                        bundle.Pos = vCol;            // cpp:552
                        StopBundle(bundle, false);    // cpp:554
                        uint iMagicID = GetMagicID(bundle.Idx);
                        // cpp:564 — targetId = -1 (dünya objesi)
                        if (!bundle.IsRegion)
                        {
                            SendEffectingPacketWithPos(iMagicID, bundle.SourceId, -1, vCol, bundle.Idx);
                            SendFailPacket(iMagicID, bundle.SourceId, -1, vCol, bundle.Idx);
                        }
                    }
                }
            }

            // cpp:593-638 — Terrain ile çarpışma
            // Open-KO birebir: CN3Terrain::CheckCollision (N3Terrain.cpp:1820-1845)
            // fHeight1 = vPos.y - GetHeight(vPos.x, vPos.z)
            // vNextPos = vPos + (vDir * (fVelocity * s_fSecPerFrm))
            // fHeight2 = vNextPos.y - GetHeight(vNextPos.x, vNextPos.z)
            // Collision if: fHeight1 <= 0 OR fHeight1*fHeight2 sign change (geçiş)
            // C++ NOT: N3FXMgr.cpp:598 — bCol set edilmiyor ("last instance so it's not used")
            if (!bCol)
            {
                var terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    // cpp:1823 — vDir.Normalize() (C++ normalizes in-place)
                    Vector3 dirNorm = bundle.Dir.normalized;
                    // cpp:1825 — fHeight1 = vPos.y - GetHeight(vPos.x, vPos.z)
                    float terrainYCur = terrain.SampleHeight(bundle.Pos) + terrain.transform.position.y;
                    float fHeight1 = bundle.Pos.y - terrainYCur;

                    // cpp:1826 — vNextPos = vPos + (vDir * (fVelocity * s_fSecPerFrm))
                    Vector3 vNextPos = bundle.Pos + dirNorm * (bundle.BundleVelocity * dt);
                    // cpp:1827 — fHeight2 = vNextPos.y - GetHeight(vNextPos.x, vNextPos.z)
                    float terrainYNext = terrain.SampleHeight(vNextPos) + terrain.transform.position.y;
                    float fHeight2 = vNextPos.y - terrainYNext;

                    bool terrainCol = false;
                    // cpp:1829-1834 — zaten altındaysa
                    if (fHeight1 <= 0)
                    {
                        terrainCol = true;
                        // cpp:1831-1832
                        vCol = new Vector3(bundle.Pos.x, terrainYCur + 0.1f, bundle.Pos.z);
                    }
                    // cpp:1836-1844 — işaret değişimi (üstten alta geçiyor)
                    else if (fHeight1 * fHeight2 <= 0) // sign change = crossing terrain
                    {
                        terrainCol = true;
                        // cpp:1842-1843
                        vCol = new Vector3(bundle.Pos.x, terrainYCur + 0.1f, bundle.Pos.z);
                    }

                    if (terrainCol)
                    {
                        bundle.Pos = vCol;            // cpp:599
                        StopBundle(bundle, false);    // cpp:600
                        uint iMagicID = GetMagicID(bundle.Idx);
                        // cpp:610 — targetId = -1 (terrain)
                        if (!bundle.IsRegion)
                            SendEffectingPacketWithPos(iMagicID, bundle.SourceId, -1, vCol, bundle.Idx);
                    }
                }
            }
        }

        private static void SendEffectingPacketWithPos(uint magicId, int sourceId, int targetId, Vector3 vCol, int idx)
        {
            var net = EntropyOnline.Network.KO.KONetworkManager.Instance;
            if (net == null) return;

            // Open-KO birebir: WIZ_MAGIC_PROCESS + N3_SP_MAGIC_EFFECTING with collision position
            using var pkt = new EntropyOnline.Network.KO.KOPacketWriter(
                EntropyOnline.Network.KO.WizOpcode.WIZ_MAGIC_PROCESS);
            pkt.WriteByte(N3_SP_MAGIC_EFFECTING);
            pkt.WriteUInt32(magicId);
            pkt.WriteInt16((short)sourceId);
            pkt.WriteInt16((short)targetId);
            pkt.WriteInt16((short)vCol.x); // data0
            pkt.WriteInt16((short)vCol.y); // data1
            pkt.WriteInt16((short)vCol.z); // data2
            pkt.WriteInt16((short)idx);
            pkt.WriteInt16(0);
            pkt.WriteInt16(0);
            net.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: N3FXMgr.cpp:331-345 — EFFECTING paketi
        /// Paket: WIZ_MAGIC_PROCESS(byte) + N3_SP_MAGIC_EFFECTING(byte) + magicID(dword)
        ///        + sourceID(short) + targetID(short) + data0-2(3x short=0) + idx(short) + 0 + 0
        /// </summary>
        private static void SendEffectingPacket(uint magicId, int sourceId, int targetId, int idx)
        {
            var net = EntropyOnline.Network.KO.KONetworkManager.Instance;
            if (net == null) return;

            // Open-KO birebir: WIZ_MAGIC_PROCESS + N3_SP_MAGIC_EFFECTING
            using var pkt = new EntropyOnline.Network.KO.KOPacketWriter(
                EntropyOnline.Network.KO.WizOpcode.WIZ_MAGIC_PROCESS);
            pkt.WriteByte(N3_SP_MAGIC_EFFECTING);        // cpp:332
            pkt.WriteUInt32(magicId);                      // cpp:333
            pkt.WriteInt16((short)sourceId);               // cpp:334
            pkt.WriteInt16((short)targetId);               // cpp:335
            pkt.WriteInt16(0);                             // cpp:337 data0
            pkt.WriteInt16(0);                             // cpp:338 data1
            pkt.WriteInt16(0);                             // cpp:339 data2
            pkt.WriteInt16((short)idx);                    // cpp:341
            pkt.WriteInt16(0);                             // cpp:342
            pkt.WriteInt16(0);                             // cpp:343
            net.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: N3FXMgr.cpp:348-362 — FAIL paketi (KILLFLYING)
        /// Paket: WIZ_MAGIC_PROCESS(byte) + N3_SP_MAGIC_FAIL(byte) + magicID(dword)
        ///        + sourceID(short) + targetID(short) + vCol.x/y/z(3x short)
        ///        + SKILLMAGIC_FAIL_KILLFLYING(short) + idx(short) + 0
        /// </summary>
        private static void SendFailPacket(uint magicId, int sourceId, int targetId, Vector3 vCol, int idx)
        {
            var net = EntropyOnline.Network.KO.KONetworkManager.Instance;
            if (net == null) return;

            // Open-KO birebir: WIZ_MAGIC_PROCESS + N3_SP_MAGIC_FAIL
            using var pkt = new EntropyOnline.Network.KO.KOPacketWriter(
                EntropyOnline.Network.KO.WizOpcode.WIZ_MAGIC_PROCESS);
            pkt.WriteByte(N3_SP_MAGIC_FAIL);                      // cpp:349
            pkt.WriteUInt32(magicId);                              // cpp:350
            pkt.WriteInt16((short)sourceId);                       // cpp:351
            pkt.WriteInt16((short)targetId);                       // cpp:352
            pkt.WriteInt16((short)vCol.x);                         // cpp:354
            pkt.WriteInt16((short)vCol.y);                         // cpp:355
            pkt.WriteInt16((short)vCol.z);                         // cpp:356
            pkt.WriteInt16(SKILLMAGIC_FAIL_KILLFLYING);            // cpp:358
            pkt.WriteInt16((short)idx);                            // cpp:359
            pkt.WriteInt16(0);                                     // cpp:360
            net.SendPacket(pkt);

        }

        private static void WriteDword(byte[] buf, ref int off, uint v)
        { BitConverter.GetBytes(v).CopyTo(buf, off); off += 4; }

        private static void WriteShort(byte[] buf, ref int off, short v)
        { BitConverter.GetBytes(v).CopyTo(buf, off); off += 2; }

        /// <summary>
        /// C++ s_pOPMgr->UPCGetByID karşılığı — entity UPC (oyuncu) mu kontrol.
        /// </summary>
        private static bool IsUPC(int entityId)
        {
            var em = EntropyOnline.World.EntityManager.Instance;
            if (em == null) return false;
            var rp = em.GetRemotePlayer(entityId);
            return rp != null && rp.Root != null;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::IsHostileTarget (PlayerBase.cpp:2532-2642)
        /// Zone ability type'a göre düşmanlık kontrolü — tüm switch case'leri birebir.
        ///
        /// Zone ability enum (globals.h:482-506):
        ///   0=NEUTRAL, 1=PVP, 2=SPECTATOR, 3=SIEGE_TYPE_1,
        ///   4=SIEGE_TYPE_2, 5=SIEGE_TYPE_3, 6=SIEGE_DISABLED,
        ///   7=CAITHAROS_ARENA, 8=PVP_NEUTRAL_NPCS
        /// </summary>
        // Zone ability constants — globals.h:482-506 birebir
        private const byte ZONE_ABILITY_NEUTRAL          = 0;
        private const byte ZONE_ABILITY_PVP              = 1;
        private const byte ZONE_ABILITY_SPECTATOR        = 2;
        private const byte ZONE_ABILITY_SIEGE_TYPE_1     = 3;
        private const byte ZONE_ABILITY_SIEGE_TYPE_2     = 4;
        private const byte ZONE_ABILITY_SIEGE_TYPE_3     = 5;
        private const byte ZONE_ABILITY_SIEGE_DISABLED   = 6;
        private const byte ZONE_ABILITY_CAITHAROS_ARENA  = 7;
        private const byte ZONE_ABILITY_PVP_NEUTRAL_NPCS = 8;

        private static bool IsHostileTarget(byte targetNation)
        {
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm == null) return true;

            byte myNation = gm.Nation;

            // cpp:2540-2541 — AUTHORITY_LIMITED_MANAGER kontrolü (şu an GM sistemi yok, atlanıyor)

            // cpp:2545 — switch(GetCurrentZoneAbilityType())
            byte zoneAbility = gm.ZoneAbilityType;

            switch (zoneAbility)
            {
                case ZONE_ABILITY_SPECTATOR: // cpp:2547-2551
                    // cpp:2548-2549 — NPC && Nation != NOTSELECTED → false
                    // (Bu UPC versiyonu, NPC kontrolü IsHostileTargetNPC'de)
                    return true;

                case ZONE_ABILITY_NEUTRAL: // cpp:2553-2570
                    // cpp:2557-2558 — her ikisi de UPC ise → false
                    // (Bu fonksiyon UPC hedefler için, ikisi de UPC)
                    return false;

                case ZONE_ABILITY_PVP: // cpp:2572-2576
                    // cpp:2576 — return Nation() != rhs->Nation()
                    return myNation != targetNation;

                case ZONE_ABILITY_PVP_NEUTRAL_NPCS: // cpp:2578-2596
                    // cpp:2579-2580 — aynı ulus → false
                    if (myNation == targetNation) return false;
                    return true;

                case ZONE_ABILITY_SIEGE_TYPE_1: // cpp:2598-2608
                    // cpp:2605-2606 — aynı knights → false
                    // (Knights bilgisi henüz yok, default true)
                    return true;

                case ZONE_ABILITY_SIEGE_TYPE_2: // cpp:2610-2625
                    // cpp:2614-2622 — alliance/knights kontrolü
                    // (Alliance bilgisi henüz yok, default true)
                    return true;

                case ZONE_ABILITY_SIEGE_TYPE_3: // cpp:2627-2628
                    return true;

                case ZONE_ABILITY_SIEGE_DISABLED: // cpp:2630-2638
                case ZONE_ABILITY_CAITHAROS_ARENA:
                    // cpp:2635-2636 — aynı knights → false
                    // (Knights bilgisi henüz yok, default true)
                    return true;

                default:
                    break;
            }

            // cpp:2641 — default: return true
            return true;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::IsHostileTarget for NPC (PlayerBase.cpp:2532-2642)
        /// NPC hedef düşmanlık kontrolü — zone ability type'a göre.
        /// </summary>
        private static bool IsHostileTargetNPC(KOEntity npc)
        {
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm == null) return true;

            byte myNation = gm.Nation;
            byte npcNation = npc.Nation;
            byte zoneAbility = gm.ZoneAbilityType;

            switch (zoneAbility)
            {
                case ZONE_ABILITY_SPECTATOR: // cpp:2548-2549
                    // NPC && Nation != NATION_NOTSELECTED → false (dost NPC)
                    if (npcNation != 0) return false;
                    return true;

                case ZONE_ABILITY_NEUTRAL: // cpp:2553-2570
                    // cpp:2560-2564 — her ikisi de nation seçmişse → false (dost)
                    if (myNation != 0 && npcNation != 0) return false;
                    // cpp:2565-2568 — rhs nation seçmişse → false
                    if (myNation == 0 && npcNation != 0) return false;
                    return true;

                case ZONE_ABILITY_PVP: // cpp:2576
                    return myNation != npcNation;

                case ZONE_ABILITY_PVP_NEUTRAL_NPCS: // cpp:2578-2596
                    if (myNation == npcNation) return false;
                    // cpp:2582-2593 — NPC nation kontrolü
                    if (myNation != 0 && npcNation != 0) return false;
                    return true;

                case ZONE_ABILITY_SIEGE_TYPE_1: // cpp:2598-2603
                    // cpp:2599-2602 — NPC, knights yok, nation seçilmiş → false
                    if (npcNation != 0) return false;
                    return true;

                case ZONE_ABILITY_SIEGE_TYPE_2: // cpp:2611-2612
                    // cpp:2611 — NPC, knights yok, nation seçilmiş → false
                    if (npcNation != 0) return false;
                    return true;

                case ZONE_ABILITY_SIEGE_TYPE_3: // cpp:2628
                    return true;

                case ZONE_ABILITY_SIEGE_DISABLED: // cpp:2630-2633
                case ZONE_ABILITY_CAITHAROS_ARENA:
                    // cpp:2632-2633 — NPC && Nation != NOTSELECTED → false
                    if (npcNation != 0) return false;
                    return true;

                default:
                    break;
            }

            return true;
        }

        /// <summary>
        /// C++ s_pPlayer->IDNumber() karşılığı.
        /// </summary>
        private static int GetMyEntityId()
        {
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm == null) return -1;
            return (int)gm.CharacterId;
        }

        private static bool IsMyEntity(int entityId)
        {
            if (entityId < 0) return false;
            int myId = GetMyEntityId();
            return entityId == 0 || (myId >= 0 && entityId == myId);
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::CheckCollisionByBox (PlayerBase.cpp:1618-1624)
        ///
        /// C++ akış:
        ///   CN3VMesh* pvMesh = m_Chr.CollisionMesh();
        ///   if (pvMesh == nullptr) return false;
        ///   return pvMesh->CheckCollision(m_Chr.m_Matrix, v0, v1, pVCol, pVNormal);
        ///
        /// CN3Chr::RegenerateCollisionMesh (N3Chr.cpp:2089-2095):
        ///   m_pMeshCollision->CreateCube(m_vMin, m_vMax);
        ///
        /// Collision mesh, entity'nin bounding box'ından (vMin,vMax) oluşturulan bir küptür.
        /// Unity'de entity'nin Renderer.bounds AABB'sinden aynı küpü oluşturup
        /// N3VMesh.CheckCollision ile test ediyoruz.
        /// </summary>
        private static bool CheckCollisionByBox(int entityId, Vector3 v0, Vector3 v1, out Vector3 vCol)
        {
            vCol = Vector3.zero;

            // Entity'nin Transform + Bounds'unu bul
            Transform entityTrans = null;
            Bounds entityBounds = default;
            bool hasBounds = false;

            // Kendi oyuncumuz mu?
            if (IsMyEntity(entityId))
            {
                var pc = FindAnyObjectByType<EntropyOnline.Character.PlayerController>();
                if (pc != null)
                {
                    entityTrans = pc.transform;
                    var renderer = pc.GetComponentInChildren<Renderer>();
                    if (renderer != null) { entityBounds = renderer.bounds; hasBounds = true; }
                }
            }
            else
            {
                // UPC (remote player)
                var em = EntropyOnline.World.EntityManager.Instance;
                if (em != null)
                {
                    var rp = em.GetRemotePlayer(entityId);
                    if (rp != null && rp.Root != null)
                    {
                        entityTrans = rp.Root.transform;
                        if (rp.Renderer != null) { entityBounds = rp.Renderer.bounds; hasBounds = true; }
                    }
                }

                // NPC/Monster (KOEntity)
                if (entityTrans == null)
                {
                    var koEntities = FindObjectsByType<KOEntity>(FindObjectsInactive.Exclude);
                    foreach (var ent in koEntities)
                    {
                        if (ent.ServerInstanceId == entityId)
                        {
                            entityTrans = ent.transform;
                            var renderer = ent.GetComponentInChildren<Renderer>();
                            if (renderer != null) { entityBounds = renderer.bounds; hasBounds = true; }
                            break;
                        }
                    }
                }
            }

            // C++ davranışı: mesh yoksa false (PlayerBase.cpp:1621-1622)
            if (entityTrans == null || !hasBounds)
                return false;

            // CN3Chr::RegenerateCollisionMesh — CreateCube(vMin, vMax) birebir
            // Bounds.min/max local space olmalı — C++ m_vMin/m_vMax local space
            Vector3 localMin = entityTrans.InverseTransformPoint(entityBounds.min);
            Vector3 localMax = entityTrans.InverseTransformPoint(entityBounds.max);

            var collisionMesh = new KOImport.N3VMesh();
            collisionMesh.CreateCube(localMin, localMax);

            // C++ m_Chr.m_Matrix = entity world matrix
            Matrix4x4 mtxWorld = entityTrans.localToWorldMatrix;

            return collisionMesh.CheckCollision(mtxWorld, v0, v1, out vCol);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // ========================================================
            // Open-KO birebir: CN3FXMgr::Tick — PART 1: Template eviction
            // N3FXMgr.cpp:239-257
            // iNum <= 0 olan template'lerde fLimitedTime biriktirilir.
            // fLimitedTime > m_fOriginLimitedTime (60s) olunca template silinir.
            // ========================================================
            _evictionRemoveList.Clear();
            foreach (var kvp in _originBundle)
            {
                var origin = kvp.Value;
                if (origin.RefCount <= 0)
                {
                    // N3FXMgr.cpp:245
                    origin.LimitedTime += dt;
                    // N3FXMgr.cpp:246-254
                    if (origin.LimitedTime > ORIGIN_LIMITED_TIME)
                    {
                        _evictionRemoveList.Add(kvp.Key);
                    }
                }
            }
            for (int i = 0; i < _evictionRemoveList.Count; i++)
            {
                _originBundle.Remove(_evictionRemoveList[i]);
            }

            // ========================================================
            // Open-KO birebir: CN3FXMgr::Tick — PART 2: Active bundle lifecycle
            // N3FXMgr.cpp:259-280
            // ========================================================
            for (int i = _activeBundles.Count - 1; i >= 0; i--)
            {
                var bundle = _activeBundles[i];

                // N3FXMgr.cpp:269-280 — dead bundle cleanup + iNum--
                if (bundle.State == FxBundleState.Dead)
                {
                    if (bundle.OverrideInstance != null)
                    {
                        UnityEngine.Object.Destroy(bundle.OverrideInstance);
                        bundle.OverrideInstance = null;
                    }
                    // N3FXMgr.cpp:271-276 — pSrc->iNum--
                    if (bundle.CacheKey != null && _originBundle.TryGetValue(bundle.CacheKey, out var origin))
                    {
                        origin.RefCount--;
                    }
                    _activeBundles.RemoveAt(i);
                    continue;
                }

                bundle.Life += dt;

                // Open-KO birebir: N3FXBundleGame::Tick (N3FXBundleGame.cpp:162-333)
                // Hareket mantığı — lifecycle check'ten ÖNCE çalışır (cpp:169-307 → 310-318)
                TickBundleMovement(bundle, dt);

                if (bundle.OverrideInstance != null)
                {
                    bundle.OverrideInstance.transform.position = bundle.Pos;
                }

                // Open-KO birebir: N3FXMgr::Tick collision (N3FXMgr.cpp:295-639)
                // Hareket sonrası çarpışma testi — lifecycle check'ten ÖNCE
                TickCollision(bundle, dt);

                // Open-KO birebir: N3FXBundleGame.cpp:310-318 — lifecycle check
                if (bundle.State == FxBundleState.Live || bundle.State == FxBundleState.Dying)
                {
                    bool isDead = false;
                    if (bundle.OverrideInstance != null)
                    {
                        ParticleSystem[] pss = bundle.OverrideInstance.GetComponentsInChildren<ParticleSystem>();
                        bool anyActive = false;
                        foreach (var ps in pss)
                        {
                            if (ps.IsAlive(true))
                            {
                                anyActive = true;
                                break;
                            }
                        }
                        isDead = (bundle.Life > 0.2f && !anyActive) || bundle.State == FxBundleState.Dying || (bundle.MaxLife > 0f && bundle.Life > bundle.MaxLife);
                    }
                    else
                    {
                        isDead = CheckAllPartsDead(bundle) || (bundle.MaxLife > 0f && bundle.Life > bundle.MaxLife);
                    }

                    if (isDead)
                    {
                        bundle.State = FxBundleState.Dead;
                        if (bundle.OverrideInstance != null)
                        {
                            UnityEngine.Object.Destroy(bundle.OverrideInstance);
                            bundle.OverrideInstance = null;
                        }
                        // N3FXMgr.cpp:271-276 — pSrc->iNum--
                        if (bundle.CacheKey != null && _originBundle.TryGetValue(bundle.CacheKey, out var origin))
                        {
                            origin.RefCount--;
                        }
                        // N3FXBundle.cpp:441 — Init() resets parts
                        InitParts(bundle);
                        _activeBundles.RemoveAt(i);
                        continue;
                    }
                }

                // Open-KO birebir: N3FXBundleGame.cpp:320-330 — part-level tick
                TickParts(bundle, dt);
            }

            // Visual rendering — Unity adaptasyonu
            if (_renderer != null)
                _renderer.UpdateVisuals(_activeBundles);
        }

        /// <summary>Eviction için geçici liste (GC-free)</summary>
        private readonly List<string> _evictionRemoveList = new();

        /// <summary>
        /// Open-KO birebir: m_OriginBundle cache lookup + lazy load.
        /// N3FXMgr.cpp:53-94 (exists check) / 73-93 (new load + insert)
        /// </summary>
        private FxBundleOrigin GetOrLoadOrigin(string cacheKey, string rawFileName)
        {
            // N3FXMgr.cpp:53 — itOrigin = m_OriginBundle.find(strTmp)
            if (_originBundle.TryGetValue(cacheKey, out var existing))
            {
                existing.LimitedTime = 0f; // reset eviction timer on reuse
                return existing;
            }

            FxBundleData data = null;

            // === Resources.Load fallback: KOFx/ altındaki .bytes dosyasını dene ===
            string fxbBaseName = Path.GetFileNameWithoutExtension(rawFileName);
            var textAsset = Resources.Load<TextAsset>($"KOFx/{fxbBaseName}");
            if (textAsset != null)
            {
                data = FxBundleParser.ParseFromBytes(textAsset.bytes, fxbBaseName);
                Resources.UnloadAsset(textAsset);
            }

            if (data == null)
            {
                string effectName = fxbBaseName.ToLowerInvariant();
                GameObject overridePrefab = Resources.Load<GameObject>($"FXOverride/{effectName}");
                if (overridePrefab != null)
                {
                    data = new FxBundleData
                    {
                        Life = 8.0f, // 8 saniye yaşam süresi (efektin tamamlanması için yeterli)
                        IsStatic = false,
                        Parts = new List<FxPartData>()
                    };
                }
                else
                {
                    Debug.LogWarning($"[KOFXManager] FXB not found in Resources: {rawFileName}. Convert with KO Tools.");
                }
            }

            if (data == null) return null;

            // N3FXMgr.cpp:93 — m_OriginBundle.insert(...)
            var origin = new FxBundleOrigin { Bundle = data, RefCount = 0, LimitedTime = 0f };
            _originBundle[cacheKey] = origin;
            return origin;
        }

        private void StopBundle(FxBundleInstance bundle, bool immediately)
        {
            // Open-KO birebir: N3FXBundle.cpp:519-539
            if (bundle.State == FxBundleState.Dead) return;

            if (!immediately)
            {
                bundle.State = FxBundleState.Dying;
                // N3FXBundle.cpp:527-533 — her part'a Stop()
                if (bundle.PartInstances != null)
                {
                    foreach (var part in bundle.PartInstances)
                    {
                        if (part != null)
                            StopPart(part);
                    }
                }
            }
            else
            {
                bundle.State = FxBundleState.Dead;
                if (bundle.OverrideInstance != null)
                {
                    UnityEngine.Object.Destroy(bundle.OverrideInstance);
                    bundle.OverrideInstance = null;
                }
                if (bundle.PartInstances != null)
                {
                    foreach (var part in bundle.PartInstances)
                    {
                        if (part != null)
                            part.State = FxPartState.Dead;
                    }
                }
                InitParts(bundle);
            }
        }

        /// <summary>
        /// Open-KO birebir: N3FXBundle.cpp:546-557 — CheckAllPartsDead
        /// Tüm part'lar DEAD ise true döner.
        /// </summary>
        private static bool CheckAllPartsDead(FxBundleInstance bundle)
        {
            if (bundle.PartInstances == null || bundle.PartInstances.Length == 0)
                return true;

            foreach (var part in bundle.PartInstances)
            {
                if (part != null && part.State != FxPartState.Dead)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Open-KO birebir: N3FXBundle.cpp:446-457 — her part'ın Tick'i
        /// + N3FXPartBase.cpp:430-451 — part lifecycle
        /// </summary>
        private static void TickParts(FxBundleInstance bundle, float dt)
        {
            if (bundle.PartInstances == null) return;

            foreach (var part in bundle.PartInstances)
            {
                if (part == null) continue;

                // N3FXBundle.cpp:450-454 — startTime check + Start()
                if (part.Data.StartTime <= bundle.Life && part.State == FxPartState.Ready)
                {
                    // C++ birebir: N3FXPartBase::Start() (N3FXPartBase.cpp:410-413)
                    // Start() → Init() çağrır → CurrSizeX/Y set edilir
                    InitPart(part);
                    part.State = FxPartState.Live;
                }

                // N3FXPartBase.cpp:430-451 — Tick()
                TickPart(part, dt);
            }
        }

        /// <summary>
        /// Open-KO birebir: CN3FXPartBase::Tick (N3FXPartBase.cpp:430-451)
        /// </summary>
        private static void TickPart(FxPartInstance part, float dt)
        {
            // N3FXPartBase.cpp:432-433
            if (part.State == FxPartState.Dead || part.State == FxPartState.Ready)
                return;

            // N3FXPartBase.cpp:435
            part.CurrLife += dt;

            // N3FXPartBase.cpp:436-440 — life check → Stop()
            if (part.Data.Life > 0f && part.State == FxPartState.Live)
            {
                if (part.CurrLife >= (part.Data.Life + part.Data.FadeIn))
                    StopPart(part);
            }

            // N3FXPartBase.cpp:441-449 — dying → dead check
            if (part.State == FxPartState.Dying)
            {
                // N3FXPartBase.cpp:443-448 — IsDead() → DEAD + Init()
                if (IsPartDead(part))
                {
                    part.State = FxPartState.Dead;
                    InitPart(part);
                    return;
                }
            }

            // Physics update — N3FXPartBase velocity/acceleration
            // N3FXPartBillBoard.cpp:323-324
            part.CurrVelocity += part.Data.Acceleration * dt;
            part.CurrPos += part.CurrVelocity * dt;

            // N3FXPartBillBoard.cpp:295-298 — Texture animation index
            // C++ birebir: non-loop modda clamp YOK — Render'da m_iTexIdx >= m_iNumTex ise skip edilir
            if (part.Data.NumTextures > 0 && part.Data.TextureFPS > 0)
            {
                bool texLoop = part.Data.BillboardData?.TexLoop
                            ?? part.Data.BottomBoardData?.TexLoop
                            ?? false;
                if (texLoop)
                    part.TexIdx = (int)(part.CurrLife * part.Data.TextureFPS) % part.Data.NumTextures;
                else
                    part.TexIdx = (int)(part.CurrLife * part.Data.TextureFPS); // cpp:298 birebir — clamp yok
            }

            // N3FXPartBillBoard.cpp:326-330 — Scale velocity/acceleration (Billboard)
            var bbExtra = part.Data.BillboardData;
            if (bbExtra != null)
            {
                part.CurrScaleVelX += bbExtra.ScaleAccelX * dt;
                part.CurrScaleVelY += bbExtra.ScaleAccelY * dt;
            }
            // N3FXPartBottomBoard — Scale velocity (BottomBoard — accel yok, sadece vel)
            else if (part.Data.BottomBoardData != null)
            {
                // BottomBoard CurrScaleVel is constant (no accel in C++)
            }
            part.CurrSizeX += part.CurrScaleVelX * dt;
            part.CurrSizeY += part.CurrScaleVelY * dt;
        }

        /// <summary>
        /// Open-KO birebir: CN3FXPartBase::Stop (N3FXPartBase.cpp:421-425)
        /// </summary>
        private static void StopPart(FxPartInstance part)
        {
            part.State = FxPartState.Dying;
            // N3FXPartBase.cpp:424 — m_fCurrLife = m_fLife + m_fFadeIn
            part.CurrLife = part.Data.Life + part.Data.FadeIn;
        }

        /// <summary>
        /// Open-KO birebir: CN3FXPartBase::IsDead (N3FXPartBase.cpp:456-459)
        /// Base implementation returns true. Derived classes override.
        /// Part fadeOut süresi kontrolü.
        /// </summary>
        private static bool IsPartDead(FxPartInstance part)
        {
            // N3FXPartBase.cpp:458 — base returns true
            // Derived part'lar override eder, fadeOut süresi bitene kadar yaşar
            if (part.Data.FadeOut > 0f)
                return part.CurrLife >= (part.Data.Life + part.Data.FadeIn + part.Data.FadeOut);
            return true;
        }

        /// <summary>
        /// Open-KO birebir: CN3FXPartBase::Init (N3FXPartBase.cpp:400-405)
        /// </summary>
        private static void InitPart(FxPartInstance part)
        {
            part.CurrLife = 0f;
            part.CurrVelocity = part.Data.Velocity;
            part.CurrPos = part.Data.Position;
            part.TexIdx = 0;
            // C++ N3FXPartBillBoard::Init — m_fCurrSizeX/Y = m_fSizeX/Y
            var bb = part.Data.BillboardData;
            if (bb != null)
            {
                part.CurrSizeX = bb.Width;
                part.CurrSizeY = bb.Height;
                part.CurrScaleVelX = bb.ScaleVelX;
                part.CurrScaleVelY = bb.ScaleVelY;
            }
            // C++ N3FXPartBottomBoard::Init — m_fCurrSizeX/Z = m_fSizeX/Z
            var btm = part.Data.BottomBoardData;
            if (btm != null)
            {
                part.CurrSizeX = btm.Width;
                part.CurrSizeY = btm.Height;
                part.CurrScaleVelX = btm.ScaleVelX;
                part.CurrScaleVelY = btm.ScaleVelY;
            }
            // C++ CN3FXPartMesh::Start (N3FXPartMesh.cpp:201) birebir:
            // m_vCurrScaleVel = m_vScaleVel
            var mesh = part.Data.MeshData;
            if (mesh != null)
            {
                part.CurrScaleVelX = mesh.ScaleVel.x;
                part.CurrScaleVelY = mesh.ScaleVel.y;
                part.CurrScaleVelZ = mesh.ScaleVel.z;
            }
        }

        /// <summary>
        /// Open-KO birebir: CN3FXBundle::Init (N3FXBundle.cpp:286-301) — tüm part'ları reset
        /// </summary>
        private static void InitParts(FxBundleInstance bundle)
        {
            if (bundle.PartInstances == null) return;
            foreach (var part in bundle.PartInstances)
            {
                if (part != null)
                    InitPart(part);
            }
        }

        /// <summary>Aktif bundle sayısı (debug)</summary>
        public int ActiveBundleCount => _activeBundles.Count;

        /// <summary>Template cache boyutu (debug)</summary>
        public int TemplateCacheCount => _originBundle.Count;

        /// <summary>
        /// Open-KO birebir: CN3FXBundleGame::Tick (N3FXBundleGame.cpp:162-333)
        /// Bundle hareket mantığı — move type'a göre pozisyon güncelleme.
        /// </summary>
        private void TickBundleMovement(FxBundleInstance bundle, float dt)
        {
            if (bundle.State != FxBundleState.Live) return;

            // N3FXBundleGame.cpp:173-201 — target pozisyonunu güncelle (region değilse)
            // C++ birebir: targetJoint bilgisi korunmalı (cpp:184-200)
            if (!bundle.IsRegion && bundle.TargetId >= 0)
            {
                Vector3 tgtPos = ResolveEntityDestPosition(bundle.TargetId, bundle.TargetJoint);
                if (tgtPos.sqrMagnitude > 0.001f)
                    bundle.DestPos = tgtPos;
            }

            // N3FXBundleGame.cpp:203-307 — move type switch
            switch (bundle.MoveType)
            {
                case FX_BUNDLE_MOVE_CURVE_FIXEDTARGET: // cpp:205-216
                {
                    Vector3 moved = bundle.Dir * dt * bundle.BundleVelocity;
                    bundle.Pos.x += moved.x;
                    bundle.Pos.z += moved.z;

                    float fAng = 0f;
                    if (bundle.Distance > 0.001f)
                    {
                        float remaining = (bundle.DestPos - bundle.Pos).magnitude;
                        fAng = Mathf.PI * (bundle.Distance - remaining) / bundle.Distance;
                    }
                    bundle.Pos.y = Mathf.Sin(fAng) * bundle.Height;
                    break;
                }

                case FX_BUNDLE_MOVE_DIR_SLOW:         // cpp:218-221 (fallthrough)
                case FX_BUNDLE_MOVE_DIR_FIXEDTARGET:  // cpp:219-221
                    bundle.Pos += bundle.Dir * dt * bundle.BundleVelocity;
                    break;

                case FX_BUNDLE_MOVE_DIR_FLEXABLETARGET_RATIO: // cpp:223-234 (fallthrough)
                case FX_BUNDLE_MOVE_DIR_FLEXABLETARGET:       // cpp:235-271
                {
                    // cpp:263-264 — yön güncelle
                    Vector3 newDir = bundle.DestPos - bundle.Pos;
                    if (newDir.sqrMagnitude > 0.001f)
                        bundle.Dir = newDir.normalized;
                    // cpp:266
                    bundle.Pos += bundle.Dir * dt * bundle.BundleVelocity;
                    break;
                }

                case FX_BUNDLE_MOVE_NONE: // cpp:273-290
                {
                    // cpp:278 — m_vPos = m_vDestPos
                    bundle.Pos = bundle.DestPos;
                    
                    // Open-KO: bağlı efektlerde kaynak yönünü her kare takip et (rotasyon senkronu)
                    if (bundle.SourceId >= 0)
                    {
                        bundle.Dir = ResolveEntityDirection(bundle.SourceId);
                    }
                    
                    // cpp:276-277 — m_vDir.y = 0, normalize
                    bundle.Dir.y = 0;
                    if (bundle.Dir.sqrMagnitude > 0.001f)
                        bundle.Dir = bundle.Dir.normalized;
                    break;
                }

                case FX_BUNDLE_REGION_POISON: // cpp:292-303
                {
                    // cpp:294-301 — kamera yönünde, near plane*3 mesafede
                    var cam = global::UnityEngine.Camera.main;
                    if (cam != null)
                    {
                        Vector3 eyeDir = (cam.transform.forward).normalized;
                        bundle.Dir = eyeDir;
                        bundle.Pos = cam.transform.position + eyeDir * cam.nearClipPlane * 3f;
                    }
                    break;
                }
            }
        }

        public static Vector3 ResolveEntityFootPosition(int entityId)
        {
            if (entityId < 0) return Vector3.zero;

            if (IsMyEntity(entityId))
            {
                var pc = FindAnyObjectByType<EntropyOnline.Character.PlayerController>();
                if (pc != null)
                    return pc.transform.position;
            }

            var em = EntropyOnline.World.EntityManager.Instance;
            if (em != null)
            {
                var rp = em.GetRemotePlayer(entityId);
                if (rp != null && rp.Root != null)
                    return rp.Root.transform.position;

                var mv = em.GetMonster(entityId);
                if (mv != null && mv.Root != null)
                    return mv.Root.transform.position;
            }

            var koEntities = FindObjectsByType<KOEntity>(FindObjectsInactive.Exclude);
            foreach (var e in koEntities)
            {
                if (e.ServerInstanceId == entityId)
                    return e.transform.position;
            }

            return Vector3.zero;
        }

        private static Vector3 ResolveEntityDirection(int entityId)
        {
            if (entityId < 0) return Vector3.forward;

            if (IsMyEntity(entityId))
            {
                var pc = FindAnyObjectByType<EntropyOnline.Character.PlayerController>();
                if (pc != null)
                    return pc.transform.forward;
            }

            var em = EntropyOnline.World.EntityManager.Instance;
            if (em != null)
            {
                var rp = em.GetRemotePlayer(entityId);
                if (rp != null && rp.Root != null)
                    return rp.Root.transform.forward;

                var mv = em.GetMonster(entityId);
                if (mv != null && mv.Root != null)
                    return mv.Root.transform.forward;
            }

            var koEntities = FindObjectsByType<KOEntity>(FindObjectsInactive.Exclude);
            foreach (var e in koEntities)
            {
                if (e.ServerInstanceId == entityId)
                    return e.transform.forward;
            }

            return Vector3.forward;
        }

        /// <summary>
        /// Open-KO birebir: CN3FXBundleGame::Trigger / Tick joint pozisyonu çözümleme (JointPosGet karşılığı)
        /// İlgili entity'nin iskeletindeki KOBone veya Bone_X adlı transform'ın dünya koordinatını döndürür.
        /// </summary>
        public static Vector3 ResolveEntityJointPosition(int entityId, int jointIndex)
        {
            if (entityId < 0) return Vector3.zero;
            if (jointIndex == 0) return ResolveEntityPosition(entityId);
            if (jointIndex < 0) return ResolveEntityFootPosition(entityId);

            GameObject rootObj = null;

            // 1. Kendi oyuncumuz (PlayerController)
            if (IsMyEntity(entityId))
            {
                var pc = FindAnyObjectByType<EntropyOnline.Character.PlayerController>();
                if (pc != null) rootObj = pc.gameObject;
            }
            // 2. EntityManager - remote player / monster
            else
            {
                var em = EntropyOnline.World.EntityManager.Instance;
                if (em != null)
                {
                    var rp = em.GetRemotePlayer(entityId);
                    if (rp != null) rootObj = rp.Root;
                    else
                    {
                        var mv = em.GetMonster(entityId);
                        if (mv != null) rootObj = mv.Root;
                    }
                }
            }

            // 3. KOEntity (WorldBuilder NPC)
            if (rootObj == null)
            {
                var koEntities = FindObjectsByType<KOEntity>(FindObjectsInactive.Exclude);
                foreach (var e in koEntities)
                {
                    if (e.ServerInstanceId == entityId)
                    {
                        rootObj = e.gameObject;
                        break;
                    }
                }
            }

            if (rootObj != null)
            {
                // KOBone bileşeninden jointIndex'e göre kemik bul
                var bones = rootObj.GetComponentsInChildren<EntropyOnline.Import.N3CharBuilder.KOBone>(true);
                foreach (var b in bones)
                {
                    if (b.Index == jointIndex)
                        return b.transform.position;
                }

                // Fallback: Bone_X ismiyle transform bul
                string boneName = $"Bone_{jointIndex}";
                var allTransforms = rootObj.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (t.name == boneName)
                        return t.position;
                }
            }

            // Kemik bulunamazsa varsayılan göğsün ortasına düş
            return ResolveEntityPosition(entityId);
        }

        public static Transform ResolveEntityJointTransform(int entityId, int jointIndex)
        {
            if (entityId < 0) return null;

            GameObject rootObj = null;

            // 1. Kendi oyuncumuz (PlayerController)
            if (IsMyEntity(entityId))
            {
                var pc = FindAnyObjectByType<EntropyOnline.Character.PlayerController>();
                if (pc != null) rootObj = pc.gameObject;
            }
            // 2. EntityManager - remote player / monster
            else
            {
                var em = EntropyOnline.World.EntityManager.Instance;
                if (em != null)
                {
                    var rp = em.GetRemotePlayer(entityId);
                    if (rp != null) rootObj = rp.Root;
                    else
                    {
                        var mv = em.GetMonster(entityId);
                        if (mv != null) rootObj = mv.Root;
                    }
                }
            }

            // 3. KOEntity (WorldBuilder NPC)
            if (rootObj == null)
            {
                var koEntities = FindObjectsByType<KOEntity>(FindObjectsInactive.Exclude);
                foreach (var e in koEntities)
                {
                    if (e.ServerInstanceId == entityId)
                    {
                        rootObj = e.gameObject;
                        break;
                    }
                }
            }

            if (rootObj != null)
            {
                if (jointIndex < 0) return rootObj.transform;

                // KOBone bileşeninden jointIndex'e göre kemik bul
                var bones = rootObj.GetComponentsInChildren<EntropyOnline.Import.N3CharBuilder.KOBone>(true);
                foreach (var b in bones)
                {
                    if (b.Index == jointIndex)
                        return b.transform;
                }

                // Fallback: Bone_X ismiyle transform bul
                string boneName = $"Bone_{jointIndex}";
                var allTransforms = rootObj.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (t.name == boneName)
                        return t;
                }

                return rootObj.transform;
            }

            return null;
        }

        public static float ResolveEntityScale(int entityId)
        {
            if (entityId < 0) return 1.0f;

            if (IsMyEntity(entityId))
            {
                var pc = FindAnyObjectByType<EntropyOnline.Character.PlayerController>();
                if (pc != null) return pc.transform.localScale.y;
            }

            var em = EntropyOnline.World.EntityManager.Instance;
            if (em != null)
            {
                var rp = em.GetRemotePlayer(entityId);
                if (rp != null && rp.Root != null) return rp.Root.transform.localScale.y;

                var mv = em.GetMonster(entityId);
                if (mv != null && mv.Root != null) return mv.Root.transform.localScale.y;
            }

            var koEntities = FindObjectsByType<KOEntity>(FindObjectsInactive.Exclude);
            foreach (var e in koEntities)
            {
                if (e.ServerInstanceId == entityId) return e.transform.localScale.y;
            }

            return 1.0f;
        }

        /// <summary>
        /// Open-KO birebir: entity pozisyonu — CPlayerBase::Position() / CPlayerNPC::Position()
        /// C++ Position() = m_Chr.Pos() — offset YOK, doğrudan entity transform pozisyonu.
        /// </summary>
        public static Vector3 ResolveEntityPosition(int entityId)
        {
            if (entityId < 0) return Vector3.zero;

            // 1. Kendi oyuncumuz (PlayerController)
            // C++ birebir: CN3FXBundleGame::Trigger (cpp:42-58)
            //   pSource->m_pShapeExtraRef → vMin + ((vMax - vMin) * 0.5f) (bounding box merkezi)
            //   else → JointPosGet(joint, m_vPos) (kemik pozisyonu)
            //   fallback → Position()
            // Unity adaptasyonu: Renderer.bounds.center = bounding box merkezi (birebir eşdeğer)
            //   fallback: Position + Height/2 (gövde ortası)
            if (IsMyEntity(entityId))
            {
                var pc = FindAnyObjectByType<EntropyOnline.Character.PlayerController>();
                if (pc != null)
                {
                    // C++ birebir: m_pShapeExtraRef → sabit bounding box merkezi (animasyonla DEĞİŞMEZ)
                    // Unity: CharacterController.center sabit — animasyonla kaymaz
                    var cc = pc.GetComponent<CharacterController>();
                    if (cc != null)
                        return pc.transform.position + cc.center;
                    // Fallback: Position + Height * 0.5f
                    return pc.transform.position + Vector3.up * 0.9f;
                }
            }

            // 2. EntityManager — remote player / monster
            var em = EntropyOnline.World.EntityManager.Instance;
            if (em != null)
            {
                var rp = em.GetRemotePlayer(entityId);
                if (rp != null && rp.Root != null)
                {
                    var cc = rp.Root.GetComponent<CharacterController>();
                    if (cc != null)
                        return rp.Root.transform.position + cc.center;
                    return rp.Root.transform.position + Vector3.up * 0.9f;
                }

                var mv = em.GetMonster(entityId);
                if (mv != null && mv.Root != null)
                {
                    var col = mv.Root.GetComponent<CapsuleCollider>();
                    if (col != null)
                        return mv.Root.transform.position + Vector3.up * (col.height * 0.5f);
                    return mv.Root.transform.position + Vector3.up * 0.9f;
                }
            }

            // 3. KOEntity (WorldBuilder spawn NPC/monster)
            var koEntities = FindObjectsByType<KOEntity>(FindObjectsInactive.Exclude);
            foreach (var e in koEntities)
            {
                if (e.ServerInstanceId == entityId)
                {
                    // C++ birebir: vMin + ((vMax - vMin) * 0.5f)
                    var renderer = e.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                        return renderer.bounds.center;
                    // Fallback: CapsuleCollider bounds center
                    var col = e.GetComponent<CapsuleCollider>();
                    if (col != null)
                        return e.transform.position + Vector3.up * (col.height * 0.5f);
                    return e.transform.position + Vector3.up * 0.9f;
                }
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Open-KO birebir: CN3FXBundleGame::Trigger hedef pozisyonu (N3FXBundleGame.cpp:84-107)
        /// C++'da pTarget->Min()/Max() tüm karakterin bounding box'ı.
        /// targetJoint == -1 → center(X,Z) + vMin.y (ayak hizası) — buff efektleri
        /// targetJoint >  -1 → bounding box merkezi (gövde ortası) — saldırı efektleri
        /// </summary>
        public static Vector3 ResolveEntityDestPosition(int entityId, int targetJoint)
        {
            if (entityId < 0) return Vector3.zero;

            // C++'da pTarget->Min()/Max() = tüm CN3Chr bounding box
            // Unity'de CharacterController/CapsuleCollider = tüm karakter capsule'ü (birebir eşdeğer)
            // Renderer KULLANMA — tek mesh part'ı döndürür (el, kol vs.)

            Vector3 footPos = Vector3.zero;   // vMin.y — ayak hizası
            Vector3 centerPos = Vector3.zero;  // (vMin+vMax)*0.5 — gövde ortası
            bool found = false;

            // 1. Kendi oyuncumuz
            if (IsMyEntity(entityId))
            {
                var pc = FindAnyObjectByType<EntropyOnline.Character.PlayerController>();
                if (pc != null)
                {
                    var cc = pc.GetComponent<CharacterController>();
                    if (cc != null)
                    {
                        footPos = pc.transform.position;                    // ayak hizası
                        centerPos = pc.transform.position + cc.center;      // gövde ortası
                        found = true;
                    }
                    else
                    {
                        footPos = pc.transform.position;
                        centerPos = pc.transform.position + Vector3.up * 0.9f;
                        found = true;
                    }
                }
            }

            // 2. Remote player
            if (!found)
            {
                var em = EntropyOnline.World.EntityManager.Instance;
                if (em != null)
                {
                    var rp = em.GetRemotePlayer(entityId);
                    if (rp != null && rp.Root != null)
                    {
                        footPos = rp.Root.transform.position;
                        var cc = rp.Root.GetComponent<CharacterController>();
                        centerPos = cc != null
                            ? rp.Root.transform.position + cc.center
                            : rp.Root.transform.position + Vector3.up * 0.9f;
                        found = true;
                    }

                    if (!found)
                    {
                        var mv = em.GetMonster(entityId);
                        if (mv != null && mv.Root != null)
                        {
                            footPos = mv.Root.transform.position;
                            var col = mv.Root.GetComponent<CapsuleCollider>();
                            centerPos = col != null
                                ? mv.Root.transform.position + Vector3.up * (col.height * 0.5f)
                                : mv.Root.transform.position + Vector3.up * 0.9f;
                            found = true;
                        }
                    }
                }
            }

            // 3. KOEntity (WorldBuilder NPC)
            if (!found)
            {
                var koEntities = FindObjectsByType<KOEntity>(FindObjectsInactive.Exclude);
                foreach (var e in koEntities)
                {
                    if (e.ServerInstanceId == entityId)
                    {
                        footPos = e.transform.position;
                        var col = e.GetComponent<CapsuleCollider>();
                        centerPos = col != null
                            ? e.transform.position + Vector3.up * (col.height * 0.5f)
                            : e.transform.position + Vector3.up * 0.9f;
                        found = true;
                        break;
                    }
                }
            }

            if (!found) return Vector3.zero;

            // C++: m_pShapeExtraRef olan nesneler (Canavarlar/NPC'ler) her zaman gövde merkezine (chest) hedeflenir.
            var entityManager = EntropyOnline.World.EntityManager.Instance;
            bool isMonsterOrNPC = (entityManager != null && entityManager.GetMonster(entityId) != null);
            if (!isMonsterOrNPC)
            {
                var koEntities = FindObjectsByType<KOEntity>(FindObjectsInactive.Exclude);
                foreach (var e in koEntities)
                {
                    if (e.ServerInstanceId == entityId)
                    {
                        isMonsterOrNPC = true;
                        break;
                    }
                }
            }

            if (isMonsterOrNPC)
                return centerPos;

            // C++ birebir: N3FXBundleGame.cpp:91-97
            // targetJoint == -1 → ayak hizası (vMin.y) — buff efektleri (Swift, Heal vs.)
            if (targetJoint == -1)
                return footPos;

            // C++ birebir: N3FXBundleGame.cpp:100-106
            // targetJoint > -1 → gövde ortası — saldırı efektleri (Öncelikle gerçek kemik koordinatını bulmaya çalışır)
            if (targetJoint >= 0)
            {
                Vector3 jointPos = ResolveEntityJointPosition(entityId, targetJoint);
                if (jointPos != Vector3.zero && jointPos != ResolveEntityPosition(entityId))
                    return jointPos;
            }

            return centerPos;
        }
        
        /// <summary>
        /// Open-KO birebir: N3FXMgr.cpp:329-362 — collision detected → KILLFLYING gönder.
        /// C++ Tick() collision handler 2 paket gönderir:
        ///   1. WIZ_MAGIC_PROCESS + EFFECTING (server hasar hesaplasın)
        ///   2. WIZ_MAGIC_PROCESS + FAIL(KILLFLYING) (region echo — projectile durdur)
        ///
        /// Biz tek C2S_MAGIC_FLYING_RESULT paketi gönderiyoruz — server echo eder.
        /// </summary>
        /// <param name="magicId">GetMagicID(idx) sonucu</param>
        /// <param name="sourceId">Projectile kaynağı</param>
        /// <param name="targetId">Çarpılan hedef (-1 = arazi)</param>
        /// <param name="collisionPos">Çarpışma pozisyonu</param>
        /// <param name="idx">Bundle idx</param>
        public void SendFlyingResult(int magicId, int sourceId, int targetId, Vector3 collisionPos, int idx)
        {
            var netMgr = EntropyOnline.Network.KO.KONetworkManager.Instance;
            if (netMgr == null || !netMgr.IsConnected) return;
            
            // Open-KO birebir: WIZ_MAGIC_PROCESS + N3_SP_MAGIC_FAIL (flying result)
            using var pkt = new EntropyOnline.Network.KO.KOPacketWriter(
                EntropyOnline.Network.KO.WizOpcode.WIZ_MAGIC_PROCESS);
            pkt.WriteByte(N3_SP_MAGIC_FAIL);                          // FAIL sub-opcode
            pkt.WriteUInt32((uint)magicId);                            // magicId
            pkt.WriteInt16((short)sourceId);                           // sourceId (short in C++)
            pkt.WriteInt16((short)targetId);                           // targetId (short in C++)
            pkt.WriteInt16((short)collisionPos.x);                     // data0
            pkt.WriteInt16((short)collisionPos.y);                     // data1
            pkt.WriteInt16((short)collisionPos.z);                     // data2
            pkt.WriteInt16(SKILLMAGIC_FAIL_KILLFLYING);                // data3 = -101
            pkt.WriteInt16((short)idx);                                // data4
            pkt.WriteInt16(0);                                         // data5
            netMgr.SendPacket(pkt);
            
        }

        private bool IsMinorHealingFX(int fxId)
        {
            if (fxId <= 0) return false;
            
            if (KOImport.SkillTableParser.IsLoaded)
            {
                // Karus/Elmorad Archer/Assassin Level 45 Minor Healing
                int[] minorSkillIds = new int[] { 107705, 207705, 108705, 208705 };
                foreach (int skillId in minorSkillIds)
                {
                    var skill = KOImport.SkillTableParser.Find(skillId);
                    if (skill != null)
                    {
                        if (skill.SelfFX1 == fxId || skill.TargetFX == fxId || skill.FlyingFX == fxId)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool ShouldHideEffect(int fxId, KOImport.FxTableEntry fxEntry, int sourceId, int targetId)
        {
            if (EntropyOnline.UI.GameOptionsManager.Instance == null) return false;
            
            string fileName = fxEntry != null ? fxEntry.FileName.ToLowerInvariant() : "";
            string effectName = fxEntry != null ? fxEntry.Name.ToLowerInvariant() : "";
            
            // 1. Hide Minor FX
            if (EntropyOnline.UI.GameOptionsManager.Instance.Effect_HideMinorFX)
            {
                if (fileName.Contains("minor") || effectName.Contains("minor") || IsMinorHealingFX(fxId))
                {
                    return true;
                }
            }
            
            // 2. Hide All Heal FX
            if (EntropyOnline.UI.GameOptionsManager.Instance.Effect_HideHealFX)
            {
                if (fileName.Contains("heal") || fileName.Contains("recovery") || fileName.Contains("regen") ||
                    effectName.Contains("heal") || effectName.Contains("recovery") || effectName.Contains("regen"))
                {
                    return true;
                }
            }
            
            // 3. Hide Monster FX
            if (EntropyOnline.UI.GameOptionsManager.Instance.Effect_HideMonsterFX)
            {
                if (EntropyOnline.World.EntityManager.Instance != null && 
                    EntropyOnline.World.EntityManager.Instance.GetMonster(sourceId) != null)
                {
                    return true;
                }
            }
            
            // 4. Hide Target FX
            if (EntropyOnline.UI.GameOptionsManager.Instance.Effect_HideTargetFX)
            {
                if (fxId == FXID_TARGET_POINTER || fxId == FXID_ZONE_POINTER || 
                    fileName.Contains("target") || fileName.Contains("pointer") ||
                    effectName.Contains("target") || effectName.Contains("pointer"))
                {
                    return true;
                }
            }
            
            // 5. Hide All Cast FX
            if (EntropyOnline.UI.GameOptionsManager.Instance.Effect_HideCastFX)
            {
                if (fileName.Contains("cast") || fileName.Contains("buff") || fileName.Contains("debuff") ||
                    fileName.Contains("cure") || fileName.Contains("status") ||
                    effectName.Contains("cast") || effectName.Contains("buff") || effectName.Contains("debuff") ||
                    effectName.Contains("cure") || effectName.Contains("status"))
                {
                    return true;
                }
            }
            
            // 6. Hide All Nova FX
            if (EntropyOnline.UI.GameOptionsManager.Instance.Effect_HideNovaFX)
            {
                if (fileName.Contains("nova") || fileName.Contains("meteor") || fileName.Contains("blizzard") ||
                    fileName.Contains("thunder") || fileName.Contains("tempest") || fileName.Contains("area") ||
                    (fxEntry != null && fxEntry.AOE > 0))
                {
                    return true;
                }
            }
            
            return false;
        }

        private void SetupOverrideEffect(GameObject effectInstance, string effectName, float customScale, int idx = 0)
        {
            effectInstance.transform.localScale = new Vector3(customScale, customScale, customScale);
            
            // Disable looping only for hit/explosion/projectile effects (idx >= 0)
            if (idx >= 0)
            {
                ParticleSystem[] particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particleSystems)
                {
                    var main = ps.main;
                    main.loop = false;
                }
            }
            
            var hsEffectSounds = effectInstance.GetComponentsInChildren<HS_EffectSound>();
            
            // Disable looping and playOnAwake on all AudioSource components so they only play once via script
            AudioSource[] audioSources = effectInstance.GetComponentsInChildren<AudioSource>();
            foreach (var audio in audioSources)
            {
                audio.loop = false;
                audio.playOnAwake = false;
                
                // If this is a generic effect (like Vefects) that doesn't use Hovl's custom delayed sound script,
                // play the audio immediately upon instantiation so it doesn't get delayed.
                if (hsEffectSounds.Length == 0)
                {
                    audio.Play();
                }
            }
            
            // Hovl Studio script audio trigger fix
            foreach (var es in hsEffectSounds)
            {
                es.Repeating = false;
                es.Invoke("RepeatSound", es.StartTime);
            }
        }

        private System.Collections.IEnumerator TriggerBundleDelayed(float delay, GameObject overridePrefab, Vector3 spawnPos, Quaternion spawnRot, string effectName, float customScale)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            GameObject effectInstance = UnityEngine.Object.Instantiate(overridePrefab, spawnPos, spawnRot);
            SetupOverrideEffect(effectInstance, effectName, customScale);
        }
    }

    /// <summary>
    /// Open-KO birebir: e_FXBundleState (N3FXDef.h:56-61)
    /// </summary>
    public enum FxBundleState
    {
        Dead  = 0, // FX_BUNDLE_STATE_DEAD
        Dying = 1, // FX_BUNDLE_STATE_DYING
        Live  = 2  // FX_BUNDLE_STATE_LIVE
    }

    /// <summary>
    /// Open-KO birebir: CN3FXBundleGame aktif instance.
    /// N3FXMgr.cpp:59-70 — runtime bundle data.
    /// </summary>
    public class FxBundleInstance
    {
        /// <summary>Parsed template data</summary>
        public FxBundleData Data;

        /// <summary>Cache key for origin lookup (N3FXMgr.cpp:271 — pBundle->FileName())</summary>
        public string CacheKey;

        /// <summary>Open-KO: m_iID — FX ID</summary>
        public int FxId;

        /// <summary>Open-KO: m_iIdx — bundle index</summary>
        public int Idx;

        /// <summary>Open-KO: m_iMoveType — hareket tipi</summary>
        public int MoveType;

        /// <summary>Open-KO: m_iSourceID</summary>
        public int SourceId;

        /// <summary>Open-KO: m_iSourceJoint</summary>
        public int SourceJoint;

        /// <summary>Open-KO: m_iTargetID</summary>
        public int TargetId;

        /// <summary>Open-KO: m_iTargetJoint</summary>
        public int TargetJoint;

        /// <summary>Sound ID from fx.tbl</summary>
        public int SoundId;

        /// <summary>Open-KO: m_dwState — bundle state</summary>
        public FxBundleState State;

        /// <summary>Open-KO: m_fLife — elapsed time</summary>
        public float Life;

        /// <summary>Open-KO: m_fLife0 — max lifetime</summary>
        public float MaxLife;

        /// <summary>Open-KO: m_pPart[] — part runtime instances (N3FXBundle.cpp:490-496)</summary>
        public FxPartInstance[] PartInstances;

        /// <summary>Unity specific: custom overridden visual prefab instance</summary>
        public GameObject OverrideInstance;

        // === N3FXBundleGame runtime movement fields ===

        /// <summary>Open-KO: m_vPos — bundle dünya pozisyonu (N3FXBundleGame.cpp:49,55)</summary>
        public Vector3 Pos;

        /// <summary>Open-KO: m_vDestPos — hedef pozisyon (N3FXBundleGame.cpp:60,82)</summary>
        public Vector3 DestPos;

        /// <summary>Open-KO: m_vDir — hareket yönü (N3FXBundleGame.cpp:116-117)</summary>
        public Vector3 Dir;

        /// <summary>Open-KO: m_fDistance — kaynak-hedef mesafesi (N3FXBundleGame.cpp:113)</summary>
        public float Distance;

        /// <summary>Open-KO: m_fHeight — arc yüksekliği = distance/2 (N3FXBundleGame.cpp:114)</summary>
        public float Height;

        /// <summary>Open-KO: m_bRegion — pozisyon overload kullanılıyor mu (N3FXBundleGame.cpp:36,130)</summary>
        public bool IsRegion;

        /// <summary>Open-KO: m_fVelocity — bundle hareket hızı (N3FXBundle.h)</summary>
        public float BundleVelocity;

        /// <summary>Open-KO: target entity scale for DependScale bundles (N3FXBundleGame.cpp:66-75)</summary>
        public float TargetScale = 1.0f;
    }

    /// <summary>
    /// Open-KO birebir: e_FXPartState (N3FXDef.h:47-53)
    /// </summary>
    public enum FxPartState : byte
    {
        Dead  = 0, // FX_PART_STATE_DEAD
        Dying = 1, // FX_PART_STATE_DYING
        Live  = 2, // FX_PART_STATE_LIVE
        Ready = 3  // FX_PART_STATE_READY
    }

    /// <summary>
    /// Open-KO birebir: CN3FXPartBase runtime state.
    /// N3FXPartBase.h:36-41 — m_fCurrLife, m_vCurrVelocity, m_vCurrPos, m_dwState
    /// </summary>
    public class FxPartInstance
    {
        /// <summary>Parsed part data (template)</summary>
        public FxPartData Data;

        /// <summary>Open-KO: m_dwState — part state (N3FXPartBase.h:40)</summary>
        public FxPartState State;

        /// <summary>Open-KO: m_fCurrLife — elapsed time (N3FXPartBase.h:36)</summary>
        public float CurrLife;

        /// <summary>Open-KO: m_vCurrVelocity (N3FXPartBase.h:37)</summary>
        public Vector3 CurrVelocity;

        /// <summary>Open-KO: m_vCurrPos (N3FXPartBase.h:38)</summary>
        public Vector3 CurrPos;

        /// <summary>Open-KO: m_iTexIdx — current texture frame (N3FXPartBillBoard.cpp:296)</summary>
        public int TexIdx;

        /// <summary>Open-KO: m_fCurrSizeX (N3FXPartBillBoard.cpp:329)</summary>
        public float CurrSizeX;

        /// <summary>Open-KO: m_fCurrSizeY (N3FXPartBillBoard.cpp:330)</summary>
        public float CurrSizeY;

        /// <summary>Open-KO: m_fCurrScaleVelX (N3FXPartBillBoard.cpp:326)</summary>
        public float CurrScaleVelX;

        /// <summary>Open-KO: m_fCurrScaleVelY (N3FXPartBillBoard.cpp:327)</summary>
        public float CurrScaleVelY;

        /// <summary>Open-KO: m_fCurrScaleVelZ — BottomBoard Z scale vel (N3FXPartBottomBoard.cpp:303)</summary>
        public float CurrScaleVelZ;
    }

    /// <summary>
    /// Open-KO birebir: __FXBundleOrigin (N3FXMgr.h:18-30)
    /// Template cache entry with reference counting and eviction timer.
    /// </summary>
    public class FxBundleOrigin
    {
        /// <summary>Open-KO: pBundle — parsed bundle template data</summary>
        public FxBundleData Bundle;

        /// <summary>Open-KO: iNum — aktif referans sayısı</summary>
        public int RefCount;

        /// <summary>Open-KO: fLimitedTime — iNum<=0 olduğundan beri geçen süre</summary>
        public float LimitedTime;
    }
}