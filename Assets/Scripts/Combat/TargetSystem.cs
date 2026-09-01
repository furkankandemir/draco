using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Core;
using EntropyOnline.Input;

namespace EntropyOnline.Combat
{
    /// <summary>
    /// Entropy Online — Hedef Seçme ve Kilitleme Sistemi
    /// 
    /// Klasik MMORPG'lerdeki "Z tuşu" veya hedefe tıklama mantığı.
    /// Mobilde: Düşmana dokunarak hedef al.
    /// Editor'de: Sol tık ile hedef al.
    /// 
    /// Hedef seçildiğinde:
    /// 1. Sunucuya C2S_TARGET_SELECT gönderilir
    /// 2. Hedefin üstünde HP barı gösterilir
    /// 3. "R" saldırı butonu aktifleşir
    /// </summary>
    public class TargetSystem : MonoBehaviour
    {
        public static TargetSystem Instance { get; private set; }

        [Header("Hedef Bilgileri")]
        public Transform CurrentTarget { get; private set; }
        public long CurrentTargetId { get; private set; } = -1;
        public bool TargetIsPlayer { get; private set; }
        public bool HasTarget => CurrentTarget != null;

        [Header("Ayarlar")]
        [SerializeField] private float maxTargetRange = 30f;
        [SerializeField] private LayerMask targetableLayers;

        // Hedef seçildiğinde tetiklenir
        public event System.Action<Transform, long, bool> OnTargetChanged;
        public event System.Action OnTargetLost;

        private global::UnityEngine.Camera _mainCam;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _mainCam = global::UnityEngine.Camera.main;
        }

        private void Update()
        {
            HandleTargetInput();
            ValidateTarget();
        }

        private void HandleTargetInput()
        {
            bool clicked = false;
            Vector2 screenPos = Vector2.zero;
            int activePointerId = -1;

            // 1. Yeni Input System Dokunmatik Girişleri
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.wasPressedThisFrame)
                    {
                        clicked = true;
                        screenPos = touch.position.ReadValue();
                        activePointerId = touch.touchId.ReadValue();
                        break;
                    }
                }
            }

            // 2. Mouse / Editör Tıklama Girişi
            if (!clicked && Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    clicked = true;
                    screenPos = Mouse.current.position.ReadValue();
                    activePointerId = -1;
                }
            }

            if (!clicked)
                return;

            // UI engelleme kontrolünü gerçekleştir
            if (EventSystem.current != null)
            {
                if (IsPointerOverUIOtherThanJoystick(activePointerId, screenPos))
                    return;
            }

            // ================================================================
            // Region targeting mode — AoE skill yer seçimi
            // Open-KO'da mouse cursor ile zone pointer aktifken tıklama
            // Mobilde: IsRegionTargeting aktifken dokunma → zemine raycast
            // ================================================================
            var magicMgr = KOMagicSkillManager.Instance;
            if (magicMgr != null && magicMgr.IsRegionTargeting)
            {
                HandleRegionTargetInput();
                return;
            }

            if (_mainCam != null)
            {
                TrySelectTarget(screenPos);
            }
        }

        /// <summary>
        /// Region targeting mode — zemine dokunarak AoE skill hedef noktası seç.
        /// Open-KO'da mouse LButtonUp → m_dwRegionMagicState = 2.
        /// Mobilde: dokunma → terrain raycast → ConfirmRegionTarget(worldPos).
        /// </summary>
        private void HandleRegionTargetInput()
        {
            if (RegionTargetIndicator.Instance != null && RegionTargetIndicator.Instance.ManualPositionOverride)
            {
                return;
            }

            var mouse = Mouse.current;
            bool clicked = false;
            Vector2 screenPos = Vector2.zero;

            // Mobil — dokunuş
            if (Touchscreen.current != null)
            {
                var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
                foreach (var touch in touches)
                {
                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        clicked = true;
                        screenPos = touch.screenPosition;
                        break;
                    }
                }
            }

            // Editor — sol tık
            if (!clicked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                clicked = true;
                screenPos = mouse.position.ReadValue();
            }

            if (!clicked || _mainCam == null) return;

            // Zemine raycast — terrain/ground layer'a çarp
            Ray ray = _mainCam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                // Entity'ye değil zemine tıklanmış olmalı
                // MonsterEntity veya RemotePlayerEntity yoksa → zemin
                var monster = hit.collider.GetComponent<MonsterEntity>();
                if (monster == null) monster = hit.collider.GetComponentInParent<MonsterEntity>();
                var rpe = hit.collider.GetComponent<World.RemotePlayerEntity>();
                if (rpe == null) rpe = hit.collider.GetComponentInParent<World.RemotePlayerEntity>();

                Vector3 worldPos = hit.point;

                // Entity'ye tıklandıysa bile pozisyonunu kullan (entity altındaki zemin)
                var magicMgr = KOMagicSkillManager.Instance;
                if (magicMgr != null)
                {
                    magicMgr.ConfirmRegionTarget(worldPos);
                }
            }
        }

        private void TrySelectTarget(Vector2 screenPos)
        {
            Ray ray = _mainCam.ScreenPointToRay(screenPos);
            bool hitTargetable = false;

            RaycastHit[] hits = Physics.RaycastAll(ray, maxTargetRange * 2, ~0, QueryTriggerInteraction.Collide);
            if (hits.Length > 0)
            {
                // Yakından uzağa sıralayarak en öndeki yaratığı seç
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var hit in hits)
                {
                    var hitObj = hit.collider.gameObject;

                    // MonsterEntity bileşenini ara
                    var monster = hitObj.GetComponent<MonsterEntity>();
                    if (monster == null)
                        monster = hitObj.GetComponentInParent<MonsterEntity>();

                    if (monster != null && monster.IsAlive)
                    {
                        SetTarget(monster.gameObject.transform, monster.InstanceId, false);
                        hitTargetable = true;
                        break;
                    }
                }
            }

            if (!hitTargetable)
            {
                // Boşa tıklandıysa hedefi bırak (Joystick üzerinde sürükleme yapılıyorsa hedef korunur)
                if (!IsPointerOverJoystick(screenPos))
                {
                    ClearTarget();
                }
            }
        }

        public void SetTarget(Transform target, long targetId, bool isPlayer)
        {
            CurrentTarget = target;
            CurrentTargetId = targetId;
            TargetIsPlayer = isPlayer;

            // Sync with GameManager so other systems (like MobileSkillBar) know the target
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null)
            {
                gm.TargetId = targetId;
                gm.TargetIsPlayer = isPlayer;
            }

            // Open-KO birebir: WIZ_TARGET_HP request
            if (KONetworkManager.Instance != null)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_TARGET_HP);
                pkt.WriteInt16((short)targetId);
                pkt.WriteByte((byte)(isPlayer ? 0 : 1)); // 0=user, 1=npc
                KONetworkManager.Instance.SendPacket(pkt);
            }

            OnTargetChanged?.Invoke(target, targetId, isPlayer);
        }

        public void ClearTarget()
        {
            if (CurrentTarget == null) return;

            CurrentTarget = null;
            CurrentTargetId = -1;
            TargetIsPlayer = false;

            // Open-KO birebir: m_iIDTarget = -1 — GameManager ile senkronize
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null)
            {
                gm.TargetId = -1;
                gm.TargetIsPlayer = false;
            }

            OnTargetLost?.Invoke();
        }

        private void ValidateTarget()
        {
            if (CurrentTarget == null) return;

            // Hedef yok olduysa veya çok uzaktaysa → bırak
            if (!CurrentTarget.gameObject.activeInHierarchy)
            {
                ClearTarget();
                return;
            }

            float dist = Vector3.Distance(transform.position, CurrentTarget.position);
            if (dist > maxTargetRange)
            {
                ClearTarget();
            }
        }

        private bool IsPointerOverUIOtherThanJoystick(int pointerId = -1, Vector2? position = null)
        {
            if (EventSystem.current == null) return false;

            var eventData = new PointerEventData(EventSystem.current);
            eventData.pointerId = pointerId;

            if (position.HasValue)
            {
                eventData.position = position.Value;
            }
            else
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    eventData.position = mouse.position.ReadValue();
                }
            }

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            if (results.Count > 0)
            {
                var topmost = results[0].gameObject;
                if (topmost != null)
                {
                    // En üstteki nesne joystick veya joystick'in bir parçası/konteyneri değilse hedef seçmeyi engelle
                    if (topmost.GetComponent<VirtualJoystick>() == null && 
                        topmost.GetComponentInParent<VirtualJoystick>() == null &&
                        topmost.GetComponentInChildren<VirtualJoystick>() == null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsPointerOverJoystick(Vector2 position)
        {
            if (EventSystem.current == null) return false;

            var eventData = new PointerEventData(EventSystem.current);
            eventData.position = position;

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var r in results)
            {
                if (r.gameObject != null)
                {
                    if (r.gameObject.GetComponentInParent<VirtualJoystick>() != null || 
                        r.gameObject.GetComponentInChildren<VirtualJoystick>() != null)
                    {
                        return true; // Joystick üzerinde dokunuş yapıldı
                    }
                }
            }
            return false;
        }
    }
}
