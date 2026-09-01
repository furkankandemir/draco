using UnityEngine;

namespace EntropyOnline.World
{
    /// <summary>
    /// NPC/Monster entity veri taşıyıcısı.
    /// Raycast ile seçildiğinde bu component'tan bilgi alınır.
    /// Open-KO: CPlayerNPC sınıfının Unity karşılığı.
    /// </summary>
    public class KOEntity : MonoBehaviour
    {
        public int NpcId;           // K_NPCPOS.NpcID (SID)
        public string EntityName;   // K_NPC.strName / K_MONSTER.strName
        public bool IsNpc;          // true=NPC (actType>=100), false=Monster
        public int ActType;         // K_NPCPOS.ActType
        public int NpcByType;       // K_NPC.byType — Open-KO globals.h e_NpcType (21=Merchant, 28=TeleportGate, 31=Warehouse vb.)
        public int MaxHP;           // K_MONSTER.iHP (monster HP, NPC için 0)
        public int CurrentHP;       // Şu anki HP
        public byte Nation;         // Open-KO: m_InfoBase.eNation — 0=NATION_NOTSELECTED (wild monster)
        public bool IsObjectNpc;    // Haritadaki static shape'e bağlı nesne NPC'si mi (Magic Anvil vb.)

        /// <summary>
        /// Open-KO birebir: m_iDroppedItemID (PlayerBase.h:131)
        /// MsgRecv_ItemBundleDrop (GameProcMain.cpp:3552) ile set edilir.
        /// Oyuncu cesete tıklayınca bu ID ile WIZ_BUNDLE_OPEN_REQ gönderilir.
        /// 0 veya negatif = loot yok.
        /// </summary>
        public long DroppedItemID = 0;

        /// <summary>
        /// Sunucu tarafından atanan runtime InstanceId.
        /// S2C_SPAWN_MONSTER paketi geldiğinde NpcId (K_NPCPOS SID) ile eşleştirilerek set edilir.
        /// Saldırı paketlerinde bu ID kullanılmalı (sunucu GetMonster(instanceId) ile arar).
        /// Eşleşme olmamışsa -1 kalır.
        /// </summary>
        public long ServerInstanceId = -1;

        // =============================================
        // Open-KO birebir: Dying state & corpse timer
        // PlayerBase.h:25-26, PlayerBase.cpp:708-709, 761-762
        // =============================================

        /// <summary>
        /// Open-KO birebir: m_fTimeAfterDeath — PlayerBase.cpp:113
        /// 0 = canlı, > 0 = ölüm timer'ı aktif
        /// </summary>
        public float TimeAfterDeath { get; private set; } = 0f;

        /// <summary>Entity öldü mü?</summary>
        public bool IsDead => TimeAfterDeath > 0f;

        /// <summary>Cesedin ve kutunun yerde kalma süresi (30 saniye)</summary>
        private const float TIME_CORPSE_REMAIN = 30.0f;

        /// <summary>Cesedin ve kutunun şeffaflaşma (fade-out) süresi (10 saniye)</summary>
        private const float TIME_CORPSE_REMOVE = 10.0f;

        // Open-KO e_Ani — NPC vs Player dying animation index
        // NPC: ANI_NPC_DEAD0 = 10, ANI_NPC_DEAD1 = 11 (GameDef.h:293-294)
        // Player: ANI_DEAD_NEATLY = 8, ANI_DEAD_KNOCKDOWN = 9, ANI_DEAD_ROLL = 10 (GameDef.h:137-139)
        private const int ANI_NPC_DEAD0 = 10;
        private const int ANI_NPC_DEAD1 = 11;
        private const int ANI_DEAD_NEATLY = 8;
        private const int ANI_DEAD_KNOCKDOWN = 9;
        private const int ANI_DEAD_ROLL = 10;

        // Open-KO birebir: GameDef.h:342-359 — e_StateDying
        public enum StateDying : sbyte
        {
            PSD_DISJOINT      = 0,  // cpp:348 — 몸이 휙 돌아가서 죽기
            PSD_KNOCK_DOWN    = 1,  // cpp:351 — 뒤로 밀리며 죽기
            PSD_KEEP_POSITION = 2,  // cpp:354 — 제자리에서 죽기
            PSD_COUNT         = 3,
            PSD_UNKNOWN       = -1
        }

        // Open-KO e_Ani — Struck animation indices (GameDef.h:133-135, 289-291)
        private const int ANI_STRUCK0 = 4;
        private const int ANI_NPC_STRUCK0 = 6;

        // Open-KO e_Ani — NPC Attack animation indices (GameDef.h:287-288)
        private const int ANI_NPC_ATTACK0 = 4;
        private const int ANI_NPC_ATTACK1 = 5;

        // =============================================
        // Open-KO birebir: DurationColor sistemi
        // PlayerBase.h:109-111, PlayerBase.cpp:432-437, 556-587
        // =============================================

        /// <summary>Open-KO birebir: m_cvDuration — PlayerBase.h:109</summary>
        private Color _cvDuration = Color.white;

        /// <summary>Open-KO birebir: m_fDurationColorTime — PlayerBase.h:111</summary>
        private float _fDurationColorTime = 0f;

        /// <summary>Open-KO birebir: m_fDurationColorTimeCur — PlayerBase.h:110</summary>
        private float _fDurationColorTimeCur = 0f;

        /// <summary>Open-KO birebir: m_eStateDying — PlayerBase.h</summary>
        private StateDying _eStateDying = StateDying.PSD_UNKNOWN;

        /// <summary>Open-KO birebir: PSD_KEEP_POSITION — ölüm anındaki Y pozisyonu</summary>
        private float _deathPositionY = 0f;

        /// <summary>Orijinal materyal renkleri (TickDurationColor geri dönüş için)</summary>
        private Color[] _originalColors = null;
        private Renderer[] _cachedRenderers = null;

        public Color GetTargetColor()
        {
            if (IsNpc)
                return new Color(0.065f, 0.39f, 1f, 1f); // Mavi (Dost NPC'ler)
            else
                return new Color(1f, 0.376f, 0.376f, 1f); // Kırmızı (Düşman Yaratıklar)
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::ActionDying (PlayerBase.cpp:1172-1203)
        ///
        /// C++ imza: void ActionDying(e_StateDying eSD, const __Vector3& vDir)
        /// 1. ActionMove(PSM_STOP)
        /// 2. Action(PSA_DYING, false)
        /// 3. eSD'ye göre animasyon seç:
        ///    PSD_DISJOINT:      NPC→ANI_NPC_DEAD1(11), Player→ANI_DEAD_ROLL(10)
        ///    PSD_KNOCK_DOWN:    NPC→ANI_NPC_DEAD1(11), Player→ANI_DEAD_KNOCKDOWN(9)
        ///    PSD_KEEP_POSITION: NPC→ANI_NPC_DEAD0(10), Player→ANI_DEAD_NEATLY(8)
        /// 4. AniCurSet(eAni, true, FLT_MIN, 0, true)
        /// </summary>
        public void ActionDying(StateDying eSD = StateDying.PSD_KEEP_POSITION)
        {
            if (IsDead) return;

            TimeAfterDeath = 0.1f;                // cpp:1174 — m_fTimeAfterDeath = 0.1f
            _deathPositionY = transform.position.y;
            _eStateDying = eSD;                   // cpp:1176 — m_eStateDying = eSD

            // cpp:1174 — ActionMove(PSM_STOP)
            var wander = GetComponent<KONpcIdleWander>();
            if (wander != null) wander.enabled = false;

            // cpp:1179-1200 — e_StateDying'e göre animasyon index seçimi
            // KOEntity sadece NPC/Monster/Gate için kullanılır — C++'da hepsi RACE_NPC
            // Player ölümü PlayerController.Action(PSA_DYING) tarafından handle edilir
            bool isNpc = true; // KOEntity = her zaman RACE_NPC
            int eAni = ANI_NPC_DEAD0;  // cpp:1197 — NPC varsayılan

            if (eSD == StateDying.PSD_DISJOINT)           // cpp:1180-1186
            {
                eAni = isNpc ? ANI_NPC_DEAD1              // cpp:1183
                             : ANI_DEAD_ROLL;              // cpp:1185
            }
            else if (eSD == StateDying.PSD_KNOCK_DOWN)    // cpp:1187-1193
            {
                eAni = isNpc ? ANI_NPC_DEAD1              // cpp:1190
                             : ANI_DEAD_KNOCKDOWN;         // cpp:1192
            }
            else                                           // cpp:1194-1200 — PSD_KEEP_POSITION
            {
                eAni = isNpc ? ANI_NPC_DEAD0              // cpp:1197
                             : ANI_DEAD_NEATLY;            // cpp:1199
            }

            // cpp:1202 — AniCurSet(eAni, true, FLT_MIN, 0, true)
            bool animPlayed = PlayAnimByIndex(eAni, WrapMode.ClampForever, 0.2f);

            if (!animPlayed)
            {
                Debug.LogWarning($"[ENTITY] {EntityName} — death anim bulunamadı (eSD={eSD}, eAni={eAni}), fallback devrilme");
                StartCoroutine(FallbackDeathAnimation());
            }

        }

        /// <summary>Animasyon bulunamazsa basit yana devrilme efekti.</summary>
        private System.Collections.IEnumerator FallbackDeathAnimation()
        {
            Quaternion startRot = transform.rotation;
            float elapsed = 0f;
            float duration = 0.8f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.rotation = startRot * Quaternion.Euler(0, 0, t * 90f);
                yield return null;
            }
        }

        /// <summary>
        /// Respawn geldiğinde ölüm durumunu tamamen sıfırla.
        /// Open-KO: CNpc::Init() — respawn'da tüm state yeniden başlatılır.
        /// HandleSpawnMonster'dan çağrılır (aynı InstanceId ile S2C_SPAWN_MONSTER geldiğinde).
        /// </summary>
        public void ResetDeath()
        {
            TimeAfterDeath = 0f;
            DroppedItemID = 0;

            // Materyal alpha'sını ve opaklığını geri yükle (fade out yapılmışsa)
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                var mats = r.materials;
                bool modified = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var mat = mats[i];
                    string colorProp = mat.HasProperty("_BaseColor") ? "_BaseColor" : (mat.HasProperty("_Color") ? "_Color" : null);
                    if (colorProp != null)
                    {
                        Color c = mat.GetColor(colorProp);
                        c.a = 1f;
                        mat.SetColor(colorProp, c);

                        // URP ve Standard opaque moduna geri dön
                        if (mat.HasProperty("_Surface"))
                        {
                            mat.SetFloat("_Surface", 0); // 0 = Opaque
                            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                            mat.DisableKeyword("_ALPHABLEND_ON");
                        }
                        else
                        {
                            mat.SetFloat("_Mode", 0); // Opaque
                            mat.DisableKeyword("_ALPHABLEND_ON");
                        }
                        modified = true;
                    }
                }
                if (modified)
                {
                    r.materials = mats;
                }
            }

            // DurationColor state sıfırla
            _fDurationColorTime = 0f;
            _fDurationColorTimeCur = 0f;
            _cachedRenderers = null;
            _originalColors = null;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerOtherMgr::CorpseRemove (PlayerOtherMgr.cpp:440-451)
        ///   if (pCorpse->m_fTimeAfterDeath >= TIME_CORPSE_REMAIN - TIME_CORPSE_REMOVE) return;
        ///   if (bRemoveImmediately)
        ///     pCorpse->m_fTimeAfterDeath = TIME_CORPSE_REMAIN;           // hemen kaldır
        ///   else
        ///     pCorpse->m_fTimeAfterDeath = TIME_CORPSE_REMAIN - TIME_CORPSE_REMOVE; // fade başlat
        ///
        /// Çağıran: MsgSend_RequestItemBundleOpen (GameProcMain.cpp:1620)
        ///   s_pOPMgr->CorpseRemove(pCorpse, false); // 점점 투명하게 없앤다..
        /// </summary>
        public void CorpseRemove(bool bRemoveImmediately)
        {
            // Open-KO birebir: cpp:444-445
            if (TimeAfterDeath >= TIME_CORPSE_REMAIN - TIME_CORPSE_REMOVE)
                return;

            if (bRemoveImmediately)
            {
                // Open-KO birebir: cpp:448
                TimeAfterDeath = TIME_CORPSE_REMAIN;
            }
            else
            {
                // Open-KO birebir: cpp:450 — fade out başlat
                TimeAfterDeath = TIME_CORPSE_REMAIN - TIME_CORPSE_REMOVE;
            }

        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::Action(PSA_STRUCK, false) — PlayerBase.cpp:1043-1050
        ///   case PSA_STRUCK:
        ///     m_eStateNext = PSA_BASIC;
        ///     eAni = this->JudgeAnimationStruck();
        ///     if (!bNPC) bNeedUpperAnimationOnly = true; // NPC değilse sadece üst gövde
        ///
        /// Tetikleme: ProcessAttack — PlayerBase.cpp:1368
        ///   pTarget->Action(PSA_STRUCK, false);
        /// </summary>
        public void ActionStruck()
        {
            if (IsDead) return; // Ölüyse struck oynamaz

            // Open-KO birebir: JudgeAnimationStruck() — PlayerBase.cpp:1589-1595
            // NPC:    (e_Ani)(ANI_NPC_STRUCK0 + rand() % 3) → 6,7,8
            // Player: (e_Ani)(ANI_STRUCK0 + rand() % 3) → 4,5,6
            int struckBase = IsNpc ? ANI_NPC_STRUCK0 : ANI_STRUCK0;
            int struckAniIndex = struckBase + Random.Range(0, 3);

            // Open-KO birebir: AniCurSet(eAni) — sıralı clip index ile oyna
            PlayAnimByIndex(struckAniIndex, WrapMode.Once, 0.15f);
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::Action(PSA_GUARD, false) — PlayerBase.cpp:1032-1041
        ///   case PSA_GUARD:
        ///     m_eStateNext = PSA_BASIC;
        ///     eAni = this->JudgeAnimationGuard();
        ///     bNeedUpperAnimationOnly = true;
        ///     fFreezeTime = 1.5f;
        ///
        /// Tetikleme: MsgRecv_Attack (result==0) — GameProcMain.cpp:3297-3305
        ///   pTarget->Action(PSA_GUARD, false);
        /// </summary>
        public void ActionGuard()
        {
            if (IsDead) return;

            // Open-KO birebir: JudgeAnimationGuard() — PlayerBase.cpp:1597-1603
            // NPC:    ANI_NPC_GUARD0 = 9 (GameDef.h:292)
            // Player: ANI_GUARD0 = 7 (GameDef.h:136)
            int guardAniIndex = IsNpc ? 9 : 7;

            // Open-KO birebir: AniCurSet(ANI_NPC_GUARD) — sıralı clip index ile oyna
            PlayAnimByIndex(guardAniIndex, WrapMode.Once, 0.15f);
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::Action(PSA_ATTACK, false) — PlayerBase.cpp:1005-1030
        ///   case PSA_ATTACK:
        ///     eAni = JudgeAnimationAttack();
        ///     AniCurSet(eAni);
        ///
        /// JudgeAnimationAttack (PlayerBase.cpp:1411-1418):
        ///   if (RACE_NPC) eAni = ANI_NPC_ATTACK0 + rand()%2 → index 4 veya 5
        ///
        /// Tetikleme: MsgRecv_Attack — GameProcMain.cpp:3272-3275
        ///   pAttacker->Action(PSA_ATTACK, false, pTarget);
        /// </summary>
        public void ActionAttack()
        {
            if (IsDead) return;

            // C++ birebir: JudgeAnimationAttack() — PlayerBase.cpp:1415-1418
            // NPC: (e_Ani)(ANI_NPC_ATTACK0 + rand() % 2) → index 4 veya 5
            int attackAniIndex = ANI_NPC_ATTACK0 + Random.Range(0, 2);

            bool played = PlayAnimByIndex(attackAniIndex, WrapMode.Once, 0.15f);
            if (played)
            {

                // C++ birebir: CPlayerNPC::Tick (PlayerNPC.cpp:93-98)
                // Attack anim bitince PSA_BASIC'e (idle) dön
                StartCoroutine(ReturnToIdleAfterAttack(attackAniIndex));
            }
        }

        /// <summary>
        /// C++ birebir: CPlayerNPC::Tick — attack anim bitince idle'a dön
        /// </summary>
        private System.Collections.IEnumerator ReturnToIdleAfterAttack(int attackAniIndex)
        {
            // Clip süresini al
            float clipLength = 1.0f; // fallback
            var anim = GetComponentInChildren<Animation>();
            if (anim != null)
            {
                string clipName = GetClipNameByIndex(attackAniIndex);
                if (clipName != null && anim[clipName] != null)
                    clipLength = anim[clipName].length;
            }

            yield return new WaitForSeconds(clipLength);

            // Ölmediyse idle'a dön — C++ birebir: PSA_BASIC
            if (!IsDead)
            {
                PlayAnimByIndex(0, WrapMode.Loop, 0.2f); // ANI_NPC_BREATH = 0

                // C++ birebir: state → PSA_BASIC sonrası NPC hareket edebilir
                var wander = GetComponent<KONpcIdleWander>();
                if (wander != null) wander.enabled = true;
            }
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::DurationColorSet — PlayerBase.cpp:432-437
        ///   m_fDurationColorTime = fDurationTime;
        ///   m_fDurationColorTimeCur = 0;
        ///   m_cvDuration = color;
        ///
        /// Tetikleme: ProcessAttack — PlayerBase.cpp:1358-1359
        ///   D3DCOLORVALUE crHit = { 1.0f, 0.2f, 0.2f, 1.0f };
        ///   pTarget->DurationColorSet(crHit, 0.3f);
        /// </summary>
        public void DurationColorSet(Color color, float fDurationTime)
        {
            // Open-KO birebir: PlayerBase.cpp:434-436
            _fDurationColorTime = fDurationTime;
            _fDurationColorTimeCur = 0f;
            _cvDuration = color;

            // Renderer cache + orijinal renk kaydet (ilk çağrıda)
            if (_cachedRenderers == null)
            {
                _cachedRenderers = GetComponentsInChildren<Renderer>();
                _originalColors = new Color[_cachedRenderers.Length];
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    _originalColors[i] = _cachedRenderers[i].material.color;
                }
            }
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::TickDurationColor — PlayerBase.cpp:556-587
        ///   if (m_fDurationColorTime <= 0) return;
        ///   if (m_fDurationColorTimeCur > m_fDurationColorTime)
        ///     → orijinal renge geri dön
        ///   else
        ///     → fD = cur / total → lerp(orijinal * fD + duration * (1-fD))
        ///   m_fDurationColorTimeCur += s_fSecPerFrm;
        /// </summary>
        private void TickDurationColor()
        {
            // Open-KO birebir: PlayerBase.cpp:558
            if (_fDurationColorTime <= 0f) return;
            if (_cachedRenderers == null) return;

            // Open-KO birebir: PlayerBase.cpp:561-568
            if (_fDurationColorTimeCur > _fDurationColorTime)
            {
                _fDurationColorTime = 0f;
                _fDurationColorTimeCur = 0f;

                // Open-KO: pPart->m_Mtl = pPart->m_MtlOrg — orijinal renge dön
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    if (_cachedRenderers[i] != null)
                        _cachedRenderers[i].material.color = _originalColors[i];
                }
            }
            else
            {
                // Open-KO birebir: PlayerBase.cpp:571-582
                // fD = m_fDurationColorTimeCur / m_fDurationColorTime
                // pPart->m_Mtl.Ambient.r = pPart->m_MtlOrg.Ambient.r * fD + m_cvDuration.r * (1.0f - fD)
                float fD = _fDurationColorTimeCur / _fDurationColorTime;
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    if (_cachedRenderers[i] == null) continue;
                    Color orig = _originalColors[i];
                    Color blended = new Color(
                        orig.r * fD + _cvDuration.r * (1f - fD),
                        orig.g * fD + _cvDuration.g * (1f - fD),
                        orig.b * fD + _cvDuration.b * (1f - fD),
                        orig.a  // alpha dokunma
                    );
                    _cachedRenderers[i].material.color = blended;
                }
            }

            // Open-KO birebir: PlayerBase.cpp:586
            _fDurationColorTimeCur += Time.deltaTime;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::Tick() — PlayerBase.cpp:708-709
        /// m_fTimeAfterDeath += s_fSecPerFrm — corpse timer tick
        ///
        /// PlayerBase.cpp:761-762 — fade out:
        /// if (m_fTimeAfterDeath > TIME_CORPSE_REMAIN - TIME_CORPSE_REMOVE)
        ///   fFactorToApply = (TIME_CORPSE_REMAIN - m_fTimeAfterDeath) / TIME_CORPSE_REMOVE
        /// </summary>
        private void Update()
        {
            // Open-KO birebir: CPlayerBase::Tick — TickDurationColor her frame çağrılır
            // PlayerBase.cpp:556-587 — ölü olsa bile çalışır (renk geri dönüşü)
            TickDurationColor();

            if (TimeAfterDeath <= 0f) return;

            // Open-KO birebir: m_fTimeAfterDeath += s_fSecPerFrm
            TimeAfterDeath += Time.deltaTime;

            // Open-KO birebir: PSD_KEEP_POSITION — ceset terrain altına düşmesin
            // Ölüm animasyonu root motion ile Y'yi değiştirebilir → sabitle
            Vector3 pos = transform.position;
            if (pos.y < _deathPositionY - 0.05f)
            {
                transform.position = new Vector3(pos.x, _deathPositionY, pos.z);
            }

            // Fade out — Open-KO: PlayerBase.cpp:761-762
            float fadeStart = TIME_CORPSE_REMAIN - TIME_CORPSE_REMOVE;
            if (TimeAfterDeath > fadeStart)
            {
                float alpha = (TIME_CORPSE_REMAIN - TimeAfterDeath) / TIME_CORPSE_REMOVE;
                alpha = Mathf.Clamp01(alpha);

                // Tüm renderer'ların alpha'sını düşür
                var renderers = GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    var mats = r.materials;
                    bool modified = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var mat = mats[i];
                        string colorProp = mat.HasProperty("_BaseColor") ? "_BaseColor" : (mat.HasProperty("_Color") ? "_Color" : null);
                        if (colorProp != null)
                        {
                            Color c = mat.GetColor(colorProp);
                            c.a = alpha;
                            mat.SetColor(colorProp, c);

                            // Render mode → Transparent
                            if (alpha < 0.99f)
                            {
                                if (mat.HasProperty("_Surface"))
                                {
                                    mat.SetFloat("_Surface", 1); // 1 = Transparent
                                    if (mat.HasProperty("_Blend"))
                                        mat.SetFloat("_Blend", 0); // 0 = Alpha
                                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                                    mat.SetInt("_ZWrite", 0);
                                    mat.DisableKeyword("_ALPHATEST_ON");
                                    mat.EnableKeyword("_ALPHABLEND_ON");
                                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                                }
                                else
                                {
                                    mat.SetFloat("_Mode", 3); // Transparent
                                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                                    mat.SetInt("_ZWrite", 0);
                                    mat.DisableKeyword("_ALPHATEST_ON");
                                    mat.EnableKeyword("_ALPHABLEND_ON");
                                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                                    mat.renderQueue = 3000;
                                }
                            }
                            modified = true;
                        }
                    }
                    if (modified)
                    {
                        r.materials = mats;
                    }
                }
            }

            // Ceset süresi doldu — kaldır
            // Open-KO: PlayerOtherMgr.cpp:138 — if (pCorpse->m_fTimeAfterDeath >= TIME_CORPSE_REMAIN)
            if (TimeAfterDeath >= TIME_CORPSE_REMAIN)
            {
                Destroy(gameObject);
            }
        }
        // =============================================
        // Animation Helpers — Open-KO birebir: AniCurSet(int iAni)
        // e_Ani enum index → clip adı çevirisi (sıralı dizi ile)
        // =============================================

        /// <summary>
        /// Open-KO birebir: CN3AnimControl::DataGet(iAni) karşılığı.
        /// N3AnimClipRegistry'den sıralı clip adı alır, yoksa fallback olarak
        /// Animation component üzerinde foreach ile arar (sıra garantisiz!).
        /// </summary>
        private bool PlayAnimByIndex(int eAniIndex, WrapMode wrapMode, float blendTime)
        {
            var anim = GetComponentInChildren<Animation>();
            if (anim == null)
            {
                // Mecanim Animator controller bridge for overridden prefabs
                var animator = GetComponentInChildren<Animator>();
                if (animator != null && animator.isActiveAndEnabled)
                {
                    if (HasAnimatorParameter(animator, "AnimIndex"))
                    {
                        animator.SetInteger("AnimIndex", eAniIndex);
                    }
                    
                    string stateName = MapLegacyAniIndexToStateName(eAniIndex);
                    if (!string.IsNullOrEmpty(stateName))
                    {
                        if (HasAnimatorState(animator, stateName))
                        {
                            if (!animator.GetCurrentAnimatorStateInfo(0).IsName(stateName) && !animator.IsInTransition(0))
                            {
                                animator.CrossFadeInFixedTime(stateName, blendTime);
                            }
                        }
                    }
                    return true;
                }
                return false;
            }

            string clipName = GetClipNameByIndex(eAniIndex);
            if (clipName != null && anim[clipName] != null)
            {
                anim[clipName].wrapMode = wrapMode;
                float actualBlend = GetBlendTimeByIndex(eAniIndex, blendTime);
                anim.CrossFade(clipName, actualBlend);
                return true;
            }

            // Fallback: foreach ile dene (sıra garantisiz — son çare)
            int clipIdx = 0;
            foreach (AnimationState state in anim)
            {
                if (clipIdx == eAniIndex)
                {
                    state.wrapMode = wrapMode;
                    float actualBlend = GetBlendTimeByIndex(eAniIndex, blendTime);
                    anim.CrossFade(state.name, actualBlend);
                    return true;
                }
                clipIdx++;
            }
            return false;
        }

        private string MapLegacyAniIndexToStateName(int eAniIndex)
        {
            switch (eAniIndex)
            {
                case 0: return "Idle";
                case 1:
                case 2:
                case 3: return "Walk";
                case 4:
                case 5: return "Attack";
                case 6:
                case 7:
                case 8: return "Struck";
                case 10:
                case 11: return "Die";
                default: return null;
            }
        }

        /// <summary>
        /// Open-KO birebir: CN3AnimControl::DataGet(iAni)
        /// N3AnimClipRegistry component'ından index ile clip adı alır.
        /// </summary>
        private string GetClipNameByIndex(int eAniIndex)
        {
            // N3AnimClipRegistry model root'a attach edilir (N3CharBuilder tarafından)
            // KOEntity root'un child'ı olan model'de aranır
            var registry = GetComponentInChildren<EntropyOnline.Import.N3AnimClipRegistry>();
            if (registry != null && registry.ClipNames != null &&
                eAniIndex >= 0 && eAniIndex < registry.ClipNames.Length)
                return registry.ClipNames[eAniIndex];
            return null;
        }

        /// <summary>
        /// N3AnimClipRegistry'den index ile animasyonun orijinal geçiş süresini (fTimeBlend) alır.
        /// Bulamazsa varsayılan geçiş süresini döner.
        /// </summary>
        private float GetBlendTimeByIndex(int eAniIndex, float defaultBlend = 0.25f)
        {
            var registry = GetComponentInChildren<EntropyOnline.Import.N3AnimClipRegistry>();
            if (registry != null && registry.BlendTimes != null &&
                eAniIndex >= 0 && eAniIndex < registry.BlendTimes.Length)
            {
                float bt = registry.BlendTimes[eAniIndex];
                return bt >= 0f ? bt : defaultBlend;
            }
            return defaultBlend;
        }

        private bool HasAnimatorParameter(Animator animator, string paramName)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            foreach (var param in animator.parameters)
            {
                if (param.name == paramName) return true;
            }
            return false;
        }

        private bool HasAnimatorState(Animator animator, string stateName, int layerIndex = 0)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (layerIndex < 0 || layerIndex >= animator.layerCount) return false;
            return animator.HasState(layerIndex, Animator.StringToHash(stateName));
        }

        private void OnDestroy()
        {
            if (EntropyOnline.World.EntityManager.Instance != null && ServerInstanceId != -1)
            {
                EntropyOnline.World.EntityManager.Instance.RemoveMonsterFromDictionary(ServerInstanceId);
            }
        }
    }
}

