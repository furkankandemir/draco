using UnityEngine;
using EntropyOnline.Character;

namespace EntropyOnline.World
{
    /// <summary>
    /// Uzak oyuncu entity component'i.
    /// Raycast ile hedef seçimi (TargetInfoUI) için kullanılır.
    /// MonsterEntity'nin oyuncu karşılığı.
    ///
    /// Open-KO birebir: CPlayerOther (PlayerOther.h/cpp)
    /// State machine + animasyon alanları PlayerBase'den miras alır.
    /// </summary>
    public class RemotePlayerEntity : MonoBehaviour
    {
        public long CharId { get; private set; }
        public string PlayerName { get; private set; }
        public byte Nation { get; private set; }
        public byte CharClass { get; private set; }
        public short Level { get; private set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }

        // =============================================
        // Open-KO birebir: CPlayerBase silah bilgisi
        // PlayerBase.h:154-155 — m_pItemPlugBasics[PLUG_POS_RIGHTHAND/LEFTHAND]
        // ItemClass_RightHand() / ItemClass_LeftHand() çağrıları için.
        // =============================================
        private KOItemClass _eICR = KOItemClass.ITEM_CLASS_UNKNOWN;
        private KOItemClass _eICL = KOItemClass.ITEM_CLASS_UNKNOWN;
        private float _fWeightR = 0f;
        public bool IsAlive => _eState != PlayerStateAction.PSA_DEATH && _eState != PlayerStateAction.PSA_DYING;

        /// <summary>Open-KO birebir: CPlayerBase::ItemClass_RightHand() (PlayerBase.h:222)</summary>
        public KOItemClass ItemClass_RightHand => _eICR;
        /// <summary>Open-KO birebir: CPlayerBase::ItemClass_LeftHand() (PlayerBase.h:230)</summary>
        public KOItemClass ItemClass_LeftHand => _eICL;

        // =============================================
        // Open-KO birebir: CPlayerBase state machine
        // PlayerBase.h satır 96-99, 130-131
        // =============================================

        /// <summary>Open-KO birebir: PlayerBase.h:96 — e_StateAction m_eState</summary>
        private PlayerStateAction _eState = PlayerStateAction.PSA_BASIC;

        /// <summary>Open-KO birebir: PlayerBase.h:97 — e_StateAction m_eStateNext</summary>
        private PlayerStateAction _eStateNext = PlayerStateAction.PSA_BASIC;

        /// <summary>Open-KO birebir: PlayerBase.h:131 — int m_iMagicAni</summary>
        private int _iMagicAni = 0;

        /// <summary>Open-KO birebir: PlayerBase.h:130 — float m_fCastFreezeTime</summary>
        private float _fCastFreezeTime = 0f;

        /// <summary>Mevcut state — Open-KO: PlayerBase.h:193</summary>
        public PlayerStateAction State() => _eState;

        // Animation resolver — e_Ani index → clip name
        private KOAnimResolver _animResolver;
        private Animation _charAnim;
        private string _currentAnim;

        // Open-KO birebir: PlayerBase.h:85-86 — Animation Deque
        private readonly System.Collections.Generic.Queue<KOAni> _animationDeque = new();
        #pragma warning disable CS0414 // Open-KO parity: üst gövde anim blend'de okunacak
        private bool _bAnimationChanged = false;
        #pragma warning restore CS0414

        public void Initialize(long charId, string name, byte nation, byte charClass, short level, int currentHp, int maxHp)
        {
            CharId = charId;
            PlayerName = name;
            Nation = nation;
            CharClass = charClass;
            Level = level;
            CurrentHp = currentHp;
            MaxHp = maxHp;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::ItemClass_RightHand / ItemClass_LeftHand
        /// Spawn sırasında itemIds[6] (RIGHTHAND) ve itemIds[7] (LEFTHAND) Item_Basic.byClass'ından alınır.
        /// JudgeAnimationBreath silah tipine göre idle animasyon seçimi için gerekli.
        /// </summary>
        public void SetWeaponInfo(KOItemClass eICR, KOItemClass eICL, float fWeightR)
        {
            _eICR = eICR;
            _eICL = eICL;
            _fWeightR = fWeightR;
        }

        /// <summary>
        /// C++ GameBase.cpp GetTextByClass() birebir — KOTextHelper'a yönlendirir.
        /// KRİTİK: charClass % 100 YAPILMAZ — tam eClass değeri kullanılır.
        /// </summary>
        public string GetClassName() => EntropyOnline.Core.KOTextHelper.GetTextByClass(CharClass);

        /// <summary>
        /// C++ GameBase.cpp GetTextByNation() birebir — KOTextHelper'a yönlendirir.
        /// </summary>
        public string GetNationName() => EntropyOnline.Core.KOTextHelper.GetTextByNation(Nation);

        // =============================================
        // ACTION STATE MACHINE — Open-KO birebir
        // PlayerBase.cpp satır 940-1112
        // =============================================

        /// <summary>
        /// Open-KO birebir: CPlayerBase::Action (PlayerBase.cpp:940-1112)
        /// Uzak oyuncu versiyonu — animasyon tetikleme.
        ///
        /// PSA_SPELLMAGIC case'i:
        ///   m_eStateNext = PSA_BASIC
        ///   fFreezeTime = m_fCastFreezeTime
        ///   eAni = (e_Ani)(m_iMagicAni)
        ///   bOnceAndFreeze = true
        /// </summary>
        public bool Action(PlayerStateAction eState)
        {
            // C++ satır 967: State Table kontrol
            if (!KOStateTable.CanTransition(_eState, eState))
                return false;

            PlayerStateAction eStatePrev = _eState;
            _eStateNext = _eState = eState;

            KOAni eAni = KOAni.ANI_UNKNOWN;
            KOAni eAniToRestore = KOAni.ANI_UNKNOWN;
            bool bOnceAndFreeze = false;

            switch (eState)
            {
                case PlayerStateAction.PSA_BASIC:
                    if (eStatePrev == PlayerStateAction.PSA_SITDOWN)
                    {
                        eAni = KOAni.ANI_STANDUP;
                        eAniToRestore = KOAnimJudge.JudgeAnimationBreath(false, false, _eICR, _eICL, _fWeightR);
                        bOnceAndFreeze = true;
                    }
                    else
                    {
                        eAni = KOAnimJudge.JudgeAnimationBreath(false, false, _eICR, _eICL, _fWeightR);
                    }
                    break;

                case PlayerStateAction.PSA_SITDOWN:
                    eAni = KOAni.ANI_SITDOWN;
                    eAniToRestore = KOAni.ANI_SITDOWN_BREATH;
                    bOnceAndFreeze = true;
                    break;

                case PlayerStateAction.PSA_SPELLMAGIC:
                    _eStateNext = PlayerStateAction.PSA_BASIC;  // cpp:1076
                    eAni = (KOAni)_iMagicAni;                   // cpp:1078
                    bOnceAndFreeze = true;                       // cpp:1079
                    break;

                case PlayerStateAction.PSA_DYING:
                    _eStateNext = PlayerStateAction.PSA_DEATH;
                    return true;

                case PlayerStateAction.PSA_DEATH:
                    _eStateNext = PlayerStateAction.PSA_DEATH;
                    return true;

                default:
                    return true;
            }

            // cpp:1095-1097: AnimationClear + AnimationAdd(eAniToRestore)
            AnimationClear();
            if (eAniToRestore != KOAni.ANI_UNKNOWN)
                AnimationAdd(eAniToRestore, false);

            // cpp:1101-1104: AniCurSet
            if (eAni != KOAni.ANI_UNKNOWN)
            {
                EnsureAnimResolverInit();
                string clipName = _animResolver?.GetClipName(eAni);
                if (!string.IsNullOrEmpty(clipName))
                {
                    PlayRemoteAnim(clipName, bOnceAndFreeze);
                }
                else
                {
                    Debug.LogWarning($"[REMOTE-ACTION] {PlayerName}: Clip bulunamadı: eAni={eAni}({(int)eAni})");
                }
            }

            return true;
        }

        /// <summary>
        /// Casting başlatma — uzak oyuncu.
        /// Open-KO birebir: MsgRecv_Casting (MagicSkillMng.cpp:1774-1788)
        ///   pPlayer->ActionMove(PSM_STOP)
        ///   pPlayer->m_iMagicAni = pSkill->iSelfAnimID1
        ///   pPlayer->m_fCastFreezeTime = 10.0f
        ///   pPlayer->Action(PSA_SPELLMAGIC, false, pTargetPlayer)
        /// </summary>
        public void ActionSpellMagic(int iSelfAnimID1, float fFreezeTime = 10.0f)
        {
            _iMagicAni = iSelfAnimID1;          // cpp:1777
            _fCastFreezeTime = fFreezeTime;      // cpp:1787
            Action(PlayerStateAction.PSA_SPELLMAGIC);  // cpp:1788
        }

        /// <summary>
        /// Effecting animasyonu — uzak oyuncu.
        /// Open-KO birebir: MsgRecv_Effecting (MagicSkillMng.cpp:1890-1895)
        ///   pPlayer->m_iMagicAni = pSkill->iSelfAnimID2
        ///   pPlayer->m_fCastFreezeTime = 0.0f
        ///   pPlayer->Action(PSA_SPELLMAGIC, false)
        /// </summary>
        public void ActionSpellEffecting(int iSelfAnimID2)
        {
            // C++ birebir: MagicSkillMng.cpp:1892
            //   pPlayer->m_iMagicAni = pSkill->iSelfAnimID2;
            //   pPlayer->m_fCastFreezeTime = 0.0f;
            //   pPlayer->Action(PSA_SPELLMAGIC, false);
            _iMagicAni = iSelfAnimID2;           // cpp:1892
            _fCastFreezeTime = 0f;                // cpp:1893
            Action(PlayerStateAction.PSA_SPELLMAGIC);  // cpp:1894
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::Action PSA_ATTACK case (PlayerBase.cpp:1005-1030)
        /// Uzak oyuncu saldırı animasyonu.
        ///
        /// C++ satır 1005-1010:
        ///   eAni = JudgeAnimationAttack(...)
        ///   eAniToRestore = JudgeAnimationBreath()
        ///   eStateNext = PSA_BASIC
        /// </summary>
        public void ActionAttack()
        {
            PlayerStateAction eStatePrev = _eState;
            _eState = PlayerStateAction.PSA_ATTACK;
            _eStateNext = PlayerStateAction.PSA_BASIC;

            // C++ birebir: eAni = JudgeAnimationAttack() — silah tipine göre atak animasyonu seç
            KOAni eAni = KOAnimJudge.JudgeAnimationAttack(false, true, _eICR, _eICL, _fWeightR);

            EnsureAnimResolverInit();
            string clipName = _animResolver?.GetClipName(eAni);
            if (!string.IsNullOrEmpty(clipName))
            {
                PlayRemoteAnim(clipName, true);
            }
            else
            {
                // Fallback: "attack" ismiyle dene
                PlayRemoteAnim("attack", true);
                Debug.LogWarning($"[REMOTE-ACTION] {PlayerName}: Attack clip bulunamadı: eAni={eAni}({(int)eAni}), fallback='attack'");
            }
        }

        /// <summary>
        /// Casting iptal — idle'a dön.
        /// Open-KO birebir: MsgRecv_Fail sonrası Action(PSA_BASIC)
        /// </summary>
        public void ActionCancelCasting()
        {
            _eState = PlayerStateAction.PSA_BASIC;
            _eStateNext = PlayerStateAction.PSA_BASIC;
            PlayRemoteAnim("breath");
        }

        // =============================================
        // Animasyon yardımcıları
        // =============================================

        /// <summary>
        /// KOAnimResolver'ı lazy initialize et.
        /// </summary>
        private void EnsureAnimResolverInit()
        {
            if (_animResolver == null)
                _animResolver = new KOAnimResolver();

            if (_animResolver.ClipCount == 0)
            {
                var anim = GetComponentInChildren<Animation>();
                var registry = GetComponentInChildren<EntropyOnline.Import.N3AnimClipRegistry>();
                if (registry != null && registry.ClipNames != null && registry.ClipNames.Length > 0)
                {
                    _animResolver.Initialize(registry.ClipNames, anim);
                }
                else if (anim != null)
                {
                    Debug.LogWarning($"[EnsureAnimResolver-Remote] FALLBACK: AnimationState foreach kullanılıyor — sıra GARANTİSİZ! anim='{anim.gameObject.name}'");
                    _animResolver.Initialize(anim);
                }
            }
        }

        /// <summary>
        /// Uzak oyuncunun animasyonunu oynatır.
        ///
        /// Open-KO birebir: CN3Chr::AniCurSet(e_Ani eAni, bool bOnceAndFreeze, float fBlendTime, float fFreezeTime)
        ///   bOnceAndFreeze=true → WrapMode.ClampForever (son frame'de don)
        ///   bOnceAndFreeze=false → WrapMode.Loop veya CrossFade
        /// </summary>
        private void PlayRemoteAnim(string animName, bool bOnceAndFreeze = false)
        {
            if (!bOnceAndFreeze && _currentAnim == animName) return;

            if (_charAnim == null)
            {
                _charAnim = GetComponentInChildren<Animation>();
                if (_charAnim == null) return;
            }

            // Case-insensitive clip arama — .n3anim isimleri mixed-case olabilir
            string resolvedName = FindClipCaseInsensitive(animName);
            if (resolvedName == null) return;

            if (bOnceAndFreeze)
            {
                // Open-KO birebir: AniCurSet(eAni, true, fBlendTime, fFreezeTime)
                // Animasyonu bir kez oynat, son frame'de dondur
                var clip = _charAnim.GetClip(resolvedName);
                if (clip != null)
                    clip.wrapMode = WrapMode.ClampForever;  // Son frame'de don
                var state = _charAnim[resolvedName];
                if (state != null)
                    state.wrapMode = WrapMode.ClampForever;
                _charAnim.Stop(resolvedName);
                _charAnim.CrossFade(resolvedName, 0.2f);
            }
            else
            {
                // Normal: looping CrossFade
                var clip = _charAnim.GetClip(resolvedName);
                if (clip != null)
                    clip.wrapMode = WrapMode.Loop;
                var state = _charAnim[resolvedName];
                if (state != null)
                    state.wrapMode = WrapMode.Loop;
                _charAnim.CrossFade(resolvedName, 0.2f);
            }
            _currentAnim = animName;
        }

        /// <summary>
        /// Case-insensitive klip arama.
        /// .n3anim dosyalarında isimler mixed-case: "SitDown", "StandUp" vb.
        /// </summary>
        private string FindClipCaseInsensitive(string animName)
        {
            if (_charAnim == null) return null;

            // Önce exact match dene
            if (_charAnim.GetClip(animName) != null)
                return animName;

            // Case-insensitive arama
            string lower = animName.ToLowerInvariant();
            foreach (AnimationState state in _charAnim)
            {
                if (state.name.ToLowerInvariant() == lower)
                    return state.name;
            }

            return null;
        }

        // =============================================
        // ANIMATION DEQUE — Open-KO birebir
        // PlayerBase.cpp:535-553, 2083-2093
        // =============================================

        private void Update()
        {
            TickAnimation();
        }

        /// <summary>
        /// Open-KO birebir: PlayerBase::AnimationAdd (PlayerBase.cpp:2083-2093)
        /// </summary>
        public void AnimationAdd(KOAni eAni, bool bImmediately)
        {
            if (bImmediately)
            {
                AnimationClear();
                EnsureAnimResolverInit();
                string clipName = _animResolver?.GetClipName(eAni);
                if (!string.IsNullOrEmpty(clipName))
                    PlayRemoteAnim(clipName);
            }
            else
            {
                _animationDeque.Enqueue(eAni);
            }
        }

        /// <summary>
        /// Open-KO birebir: PlayerBase::AnimationClear (PlayerBase.h:268-271)
        /// </summary>
        public void AnimationClear()
        {
            _animationDeque.Clear();
        }

        /// <summary>
        /// Open-KO birebir: PlayerBase::TickAnimation (PlayerBase.cpp:535-553)
        /// </summary>
        private void TickAnimation()
        {
            _bAnimationChanged = false;
            if (!IsAnimEnd()) return;
            _bAnimationChanged = true;

            if (_animationDeque.Count == 0)
            {
                Action(_eStateNext);
            }
            else
            {
                KOAni eAniToSet = _animationDeque.Dequeue();
                EnsureAnimResolverInit();
                string clipName = _animResolver?.GetClipName(eAniToSet);
                if (!string.IsNullOrEmpty(clipName))
                    PlayRemoteAnim(clipName);
            }
        }

        /// <summary>
        /// Open-KO birebir: CN3Chr::IsAnimEnd()
        /// </summary>
        private bool IsAnimEnd()
        {
            if (_charAnim == null)
            {
                _charAnim = GetComponentInChildren<Animation>();
                if (_charAnim == null) return false;
            }

            if (!_charAnim.isPlaying) return true;
            if (string.IsNullOrEmpty(_currentAnim)) return false;

            string resolvedName = FindClipCaseInsensitive(_currentAnim);
            if (resolvedName == null) return false;

            AnimationState state = _charAnim[resolvedName];
            if (state == null) return false;

            if (state.wrapMode == WrapMode.Loop || state.wrapMode == WrapMode.PingPong)
                return false;

            return state.normalizedTime >= 1.0f;
        }
    }
}

