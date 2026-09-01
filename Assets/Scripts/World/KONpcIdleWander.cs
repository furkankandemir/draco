using UnityEngine;

namespace EntropyOnline.World
{
    /// <summary>
    /// Open-KO birebir: CPlayerNPC::Tick() + MoveTo() (PlayerNPC.cpp:25-128)
    ///
    /// Sunucudan WIZ_NPC_MOVE geldiğinde:
    ///   1. MoveTo() → m_vPosFromServer set edilir, m_fMoveSpeedPerSec hesaplanır
    ///   2. Tick() → her frame m_vPosFromServer'a doğru m_fMoveSpeedPerSec hızıyla yürür
    ///
    /// C++ formül (PlayerNPC.cpp:120-127):
    ///   m_fMoveSpeedPerSec = fSpeed;
    ///   if (fSpeed != 0)
    ///     m_fMoveSpeedPerSec *= (distance / (fSpeed * PACKET_INTERVAL_MOVE)) * 0.85f;
    ///   // Bu formül: "paket aralığında hedefe varılacak hızı" hesaplar
    ///   // 0.85 çarpanı: biraz yavaşlatarak "duraksamayı" önler
    ///
    /// Client-side idle wander: sunucu paketi gelmediğinde spawn noktası çevresinde
    /// küçük görsel hareket simülasyonu (Open-KO'da yok — bizim ek).
    /// </summary>
    public class KONpcIdleWander : MonoBehaviour
    {
        [HideInInspector] public float wanderRadius = 4f;
        [HideInInspector] public float moveSpeed = 1.2f;
        [HideInInspector] public bool isNpc = false;

        // =============================================
        // C++ birebir alanlar — PlayerNPC.cpp
        // =============================================

        // PlayerNPC.cpp:18 — m_vPosFromServer
        private Vector3 _posFromServer;
        // PlayerNPC.cpp:113 — m_fMoveSpeedPerSec
        private float _moveSpeedPerSec;
        // GameDef.h:37 — PACKET_INTERVAL_MOVE = 1.5f
        private const float PACKET_INTERVAL_MOVE = 1.5f;

        // Spawn pozisyonu — wander merkezi
        private Vector3 _spawnPos;
        // Client-side wander timer
        private float _stateTimer;
        // Sunucu-driven hareket aktif mi?
        private bool _serverDriven;

        // Animation component
        private Animation _anim;
        private string _walkClip;
        private string _idleClip;
        private bool _animSearchDone;

        // Mecanim support for custom creatures
        private Animator _animator;
        private bool _isMecanim;

        private enum WanderState
        {
            Idle,
            Walking,
        }
        private WanderState _state = WanderState.Idle;

        private void Start()
        {
            _spawnPos = transform.position;
            _posFromServer = transform.position;

            if (isNpc)
            {
                enabled = false;
                return;
            }

            TryFindAnimation();
            _stateTimer = Random.Range(2f, 5f);
            _state = WanderState.Idle;
            _serverDriven = false;
        }

        private void TryFindAnimation()
        {
            if (_animSearchDone && (_anim != null || _animator != null)) return;

            _anim = GetComponentInChildren<Animation>();
            if (_anim == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator != null)
                {
                    _isMecanim = true;
                    _animSearchDone = true;
                }
                return;
            }

            _animSearchDone = true;

            foreach (AnimationState state in _anim)
            {
                string lname = state.name.ToLower();
                if (_walkClip == null && (lname == "walk" || lname.Contains("walk")))
                    _walkClip = state.name;
                if (_idleClip == null && (lname == "breath" || lname == "idle" ||
                                          lname.Contains("idle") || lname.Contains("wait") ||
                                          lname.Contains("stand")))
                    _idleClip = state.name;
            }
            if (_idleClip == null && _anim.clip != null)
                _idleClip = _anim.clip.name;

            // Walk bulunamadıysa run dene
            if (_walkClip == null)
            {
                foreach (AnimationState state in _anim)
                {
                    string lname = state.name.ToLower();
                    if (lname == "run" || lname.Contains("run"))
                    {
                        _walkClip = state.name;
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            if (!_animSearchDone || (_anim == null && _animator == null))
                TryFindAnimation();

            float dt = Time.deltaTime;

            // =============================================
            // C++ birebir: CPlayerNPC::Tick() (PlayerNPC.cpp:25-101)
            // Sunucu-driven hareket — m_vPosFromServer'a doğru yürü
            // =============================================
            if (_serverDriven)
            {
                TickServerDriven(dt);
                return;
            }

            // =============================================
            // Client-side fallback idle wander
            // Sunucu paketi gelmediğinde küçük görsel hareket
            // =============================================
            switch (_state)
            {
                case WanderState.Idle:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                    {
                        Vector2 rnd = Random.insideUnitCircle * wanderRadius;
                        _posFromServer = _spawnPos + new Vector3(rnd.x, 0, rnd.y);
                        _moveSpeedPerSec = moveSpeed;

                        Vector3 dir = _posFromServer - transform.position;
                        dir.y = 0;
                        if (dir.sqrMagnitude > 0.01f)
                            transform.rotation = Quaternion.LookRotation(dir.normalized);

                        PlayWalkAnimation();

                        _state = WanderState.Walking;
                    }
                    break;

                case WanderState.Walking:
                    TickMovement(dt);
                    break;
            }
        }

        /// <summary>
        /// C++ birebir: CPlayerNPC::Tick() satır 41-91
        /// m_vPosFromServer'a doğru m_fMoveSpeedPerSec hızıyla yürür.
        /// </summary>
        private void TickServerDriven(float dt)
        {
            TickMovement(dt);
        }

        /// <summary>
        /// C++ birebir: PlayerNPC.cpp:42-91 — hareket mantığı
        /// Her frame çağrılır, hedefe doğru yürür veya varınca durur.
        /// </summary>
        private void TickMovement(float dt)
        {
            Vector3 vPos = transform.position;
            Vector3 vTarget = _posFromServer;

            // C++ satır 42: if (m_vPosFromServer.x != vPos.x || m_vPosFromServer.z != vPos.z)
            Vector3 vOffset = new Vector3(vTarget.x - vPos.x, 0, vTarget.z - vPos.z);
            float fDist = vOffset.magnitude;

            if (fDist < 0.05f)
            {
                // Hedefe vardı
                if (_state == WanderState.Walking || _serverDriven)
                {
                    // C++ satır 62: this->ActionMove(PSM_STOP)
                    PlayIdleAnimation();

                    if (_serverDriven)
                    {
                        // Sunucu durdurdu — bir sonraki paketi bekle
                        // _serverDriven kalır (yeni paket gelecek)
                    }
                    else
                    {
                        _stateTimer = Random.Range(3f, 7f);
                    }
                    _state = WanderState.Idle;
                }
                return;
            }

            // C++ satır 53: vDir.Normalize()
            Vector3 vDir = vOffset.normalized;

            // C++ satır 55: fSpeedAbsolute
            float fSpeedAbsolute = Mathf.Abs(_moveSpeedPerSec);
            if (fSpeedAbsolute < 0.01f) fSpeedAbsolute = moveSpeed; // fallback

            // C++ satır 57: if (fDist < fSpeedAbsolute * s_fSecPerFrm) — hedefe çok yakın
            if (fDist < fSpeedAbsolute * dt + 0.05f)
            {
                // C++ satır 59-60: vPos = m_vPosFromServer
                Vector3 arrivedPos = vPos;
                arrivedPos.x = vTarget.x;
                arrivedPos.z = vTarget.z;
                arrivedPos.y = GetTerrainY(arrivedPos.x, arrivedPos.z);
                transform.position = arrivedPos;

                // C++ satır 62: ActionMove(PSM_STOP)
                PlayIdleAnimation();

                if (!_serverDriven)
                {
                    _stateTimer = Random.Range(3f, 7f);
                    _state = WanderState.Idle;
                }
                // serverDriven ise: bir sonraki WIZ_NPC_MOVE'u bekliyoruz
            }
            else
            {
                // C++ satır 66-67: yönü hesapla ve dön
                if (vDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(vDir);

                // C++ satır 78-79: walk/run animasyon seçimi
                bool needWalkAnim = (_state != WanderState.Walking);
                if (_isMecanim && _animator != null)
                {
                    var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                    if (!stateInfo.IsName("Walk") && !stateInfo.IsName("Attack") && !stateInfo.IsName("Struck"))
                    {
                        needWalkAnim = true;
                    }
                }
                else if (_anim != null)
                {
                    if (!_anim.IsPlaying(_walkClip) && !_anim.IsPlaying("attack") && !_anim.IsPlaying("struck"))
                    {
                        needWalkAnim = true;
                    }
                }

                if (needWalkAnim)
                {
                    PlayWalkAnimation();
                }
                _state = WanderState.Walking;

                // C++ satır 81: vPos += vDir * (fSpeedAbsolute * s_fSecPerFrm)
                Vector3 newPos = vPos + vDir * (fSpeedAbsolute * dt);
                newPos.y = GetTerrainY(newPos.x, newPos.z);
                transform.position = newPos;
            }
        }

        /// <summary>
        /// Open-KO birebir: CPlayerNPC::MoveTo() (PlayerNPC.cpp:103-128)
        /// Sunucudan WIZ_NPC_MOVE geldiğinde çağrılır.
        ///
        /// C++ formül (satır 120-127):
        ///   m_fMoveSpeedPerSec = fSpeed;
        ///   if (fSpeed != 0)
        ///     m_fMoveSpeedPerSec *= (distance / (fSpeed * PACKET_INTERVAL_MOVE)) * 0.85f;
        ///
        /// Bu formül sunucudan gelen fSpeed değerini, client'ın hedefe
        /// PACKET_INTERVAL_MOVE (1.5 sn) içinde varabilecegi gerçek hıza çevirir.
        /// 0.85 çarpanı: biraz yavaşlatarak "duraksamayı" önler.
        /// </summary>
        public void SetMoveTarget(float targetX, float targetZ, float fSpeed)
        {
            if (!enabled) enabled = true;

            _serverDriven = true;
            _posFromServer = new Vector3(targetX, transform.position.y, targetZ);

            // C++ satır 115-118
            Vector3 vPos = transform.position;
            vPos.y = 0;
            Vector3 vPosS = new Vector3(targetX, 0, targetZ);
            float distance = (vPosS - vPos).magnitude;

            // C++ satır 113: m_fMoveSpeedPerSec = fSpeed
            _moveSpeedPerSec = fSpeed;

            // C++ satır 120-125: hız boşlama — distance / (fSpeed * PACKET_INTERVAL) * 0.85
            if (fSpeed > 0.001f)
            {
                _moveSpeedPerSec *= (distance / (fSpeed * PACKET_INTERVAL_MOVE)) * 0.85f;
            }
            else if (distance > 0.1f)
            {
                // fSpeed=0 ama mesafe var — fallback (C++ satır 124)
                _moveSpeedPerSec = (distance / (0.001f * PACKET_INTERVAL_MOVE)) * 0.85f;
            }

            // C++ satır 126-127: negatif hız → ters yön
            // (Bizde kullanılmıyor — NPC geri yürümez)

            if (!_animSearchDone || (_anim == null && _animator == null))
                TryFindAnimation();

            // C++ satır 78: PSM_RUN animasyonu
            PlayWalkAnimation();

            _state = WanderState.Walking;
        }

        /// <summary>
        /// Sunucudan speed=0 — NPC durdurulur.
        /// C++ birebir: MoveTo(x,y,z,0,0) → iMoveMode==0 → return (PlayerNPC.cpp:110-111)
        /// Ama biz pozisyonu set ediyoruz (speed=0 paketinde final pozisyon gelir).
        /// </summary>
        public void StopMovement()
        {
            _state = WanderState.Idle;
            _moveSpeedPerSec = 0;
            _stateTimer = Random.Range(3f, 7f);
            _serverDriven = false;

            if (!_animSearchDone || (_anim == null && _animator == null))
                TryFindAnimation();

            PlayIdleAnimation();
        }

        private void PlayWalkAnimation()
        {
            if (_anim != null && _walkClip != null)
            {
                _anim.CrossFade(_walkClip, 0.2f);
            }
            else if (_isMecanim && _animator != null)
            {
                if (HasAnimatorParameter(_animator, "AnimIndex"))
                {
                    _animator.SetInteger("AnimIndex", 1);
                }
                
                if (HasAnimatorState(_animator, "Walk"))
                {
                    if (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Walk") && !_animator.IsInTransition(0))
                    {
                        _animator.CrossFadeInFixedTime("Walk", 0.2f);
                    }
                }
            }
        }

        private void PlayIdleAnimation()
        {
            if (_anim != null && _idleClip != null)
            {
                _anim.CrossFade(_idleClip, 0.2f);
            }
            else if (_isMecanim && _animator != null)
            {
                if (HasAnimatorParameter(_animator, "AnimIndex"))
                {
                    _animator.SetInteger("AnimIndex", 0);
                }
                
                if (HasAnimatorState(_animator, "Idle"))
                {
                    if (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !_animator.IsInTransition(0))
                    {
                        _animator.CrossFadeInFixedTime("Idle", 0.2f);
                    }
                }
            }
        }

        /// <summary>
        /// C++ birebir: PlayerNPC.cpp:105,109-110
        /// MoveTo(fPosX, fPosY, fPosZ, fSpeed, iMoveMode=0)
        ///   m_vPosFromServer.Set(fPosX, fPosY, fPosZ);
        ///   if (iMoveMode == 0) return;
        /// Sadece hedef pozisyonu güncellenir, hız ve state DEĞİŞMEZ.
        /// NPC eski hızla yeni hedefe doğru yürümeye devam eder.
        /// </summary>
        public void UpdateTargetOnly(float targetX, float targetZ)
        {
            _posFromServer = new Vector3(targetX, transform.position.y, targetZ);
            // C++ birebir: m_fMoveSpeedPerSec değişmez, state değişmez
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

        /// <summary>
        /// Terrain yüksekliğini örnekle — her frame güncellenir, yere gömülme önlenir.
        /// </summary>
        private float GetTerrainY(float x, float z)
        {
            if (WorldBuilder.Instance != null)
                return WorldBuilder.Instance.GetTerrainHeight(x, z);
            return transform.position.y;
        }
    }
}
