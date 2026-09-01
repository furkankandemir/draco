using UnityEngine;
using UnityEngine.EventSystems;
using EntropyOnline.Combat;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Handles mobile-style Drag-to-Aim logic for AoE skills in the Skill Bar.
    /// When pressed, if the skill is an AoE/Region targeted skill, dragging it
    /// acts as a virtual aiming joystick that repositions the target circle.
    /// </summary>
    public class KOSkillAimHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IBeginDragHandler, IEndDragHandler
    {
        public int SlotIndex;

        public void OnBeginDrag(PointerEventData eventData) 
        {
            Debug.Log($"[AIM-HANDLER] OnBeginDrag called for slot {SlotIndex}");
        }
        public void OnEndDrag(PointerEventData eventData) 
        {
            Debug.Log($"[AIM-HANDLER] OnEndDrag called for slot {SlotIndex}");
        }

        private Vector2 _startPosition;
        private bool _isAiming;
        private Vector3 _currentTargetPos;
        private float _maxDragDistance = 100f; // Max pixel offset on UI
        private float _cancelThreshold = 0.8f; // y > Screen.height * 0.8f is cancellation region

        public void OnPointerDown(PointerEventData eventData)
        {
            if (MobileSkillBar.Instance == null || MobileSkillBar.Instance.IsEditMode)
                return;

            int magicNum = MobileSkillBar.Instance.GetSlotMagicNum(SlotIndex);
            Debug.Log($"[AIM-HANDLER] OnPointerDown for slot {SlotIndex}, magicNum={magicNum}");
            if (magicNum <= 0)
                return;

            var pSkill = KOImport.SkillTableParser.Find(magicNum);
            if (pSkill == null)
                return;

            // Check if it's an AoE/Region target skill (Target = 10, 11, 12)
            bool isAoE = pSkill.Target == 10 || pSkill.Target == 11 || pSkill.Target == 12;
            Debug.Log($"[AIM-HANDLER] Skill={pSkill.Name}, TargetType={pSkill.Target}, isAoE={isAoE}");

            if (isAoE)
            {
                _startPosition = eventData.position;
                _isAiming = true;

                // Set default starting position in case of quick tap (5m in front of player)
                var pc = EntropyOnline.Character.PlayerController.Instance;
                if (pc != null)
                {
                    _currentTargetPos = pc.transform.position + pc.transform.forward * 5f;
                    Vector3 rayStart = _currentTargetPos + Vector3.up * 10f;
                    if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 30f))
                    {
                        _currentTargetPos = hit.point;
                    }
                }

                // Call OnSkillPressed to trigger the targeting start event
                MobileSkillBar.Instance.OnSkillPressedDirectly(SlotIndex);

                if (RegionTargetIndicator.Instance != null)
                {
                    RegionTargetIndicator.Instance.ManualPositionOverride = true;
                    Debug.Log($"[AIM-HANDLER] RegionTargetIndicator.Instance found. Override set to true.");
                }
                else
                {
                    Debug.LogWarning($"[AIM-HANDLER] RegionTargetIndicator.Instance is NULL!");
                }
            }
            else
            {
                // Non-AoE skill: Trigger normally on press
                MobileSkillBar.Instance.OnSkillPressedDirectly(SlotIndex);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            Debug.Log($"[AIM-HANDLER] OnDrag called, _isAiming={_isAiming}, delta={eventData.delta}");
            if (!_isAiming)
                return;

            Vector2 dragOffset = eventData.position - _startPosition;
            float dragDist = Mathf.Min(dragOffset.magnitude, _maxDragDistance);
            Vector2 dragDir = dragOffset.normalized;
            float dragRatio = dragDist / _maxDragDistance;

            var mainCam = global::UnityEngine.Camera.main;
            var pc = EntropyOnline.Character.PlayerController.Instance;
            if (mainCam != null && pc != null)
            {
                // Translate UI drag space to camera horizontal viewport space
                Vector3 camForward = mainCam.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = mainCam.transform.right;
                camRight.y = 0f;
                camRight.Normalize();

                // 3D world direction relative to camera
                Vector3 worldDir = (camForward * dragDir.y) + (camRight * dragDir.x);
                if (worldDir.sqrMagnitude > 0.01f)
                    worldDir.Normalize();

                // Get max casting distance from the skill table
                float maxRange = 10f; // Default fallback
                int magicNum = MobileSkillBar.Instance.GetSlotMagicNum(SlotIndex);
                var pSkill = KOImport.SkillTableParser.Find(magicNum);
                if (pSkill != null && pSkill.ValidDist > 0)
                {
                    maxRange = pSkill.ValidDist;
                }

                // Project target coordinate
                Vector3 targetPos = pc.transform.position + worldDir * (dragRatio * maxRange);

                // Snap to ground layer (ignore player's own collider to prevent visual height jumps)
                Vector3 rayStart = targetPos + Vector3.up * 10f;
                RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 30f);
                foreach (var h in hits)
                {
                    if (h.collider.GetComponentInParent<EntropyOnline.Character.PlayerController>() == null)
                    {
                        targetPos = h.point;
                        break;
                    }
                }

                _currentTargetPos = targetPos;
                Debug.Log($"[AIM-HANDLER] DragOffset={dragOffset}, targetPos={_currentTargetPos}");

                if (RegionTargetIndicator.Instance != null)
                {
                    RegionTargetIndicator.Instance.ManualPositionOverride = true;
                    RegionTargetIndicator.Instance.UpdateIndicatorPosition(_currentTargetPos + Vector3.up * 0.1f);

                    // Dragging to the top 20% of screen cancels cast
                    bool isCancelled = (eventData.position.y > Screen.height * _cancelThreshold);
                    if (isCancelled)
                    {
                        // Red tint for cancel state
                        RegionTargetIndicator.Instance.SetIndicatorColor(new Color(1f, 0f, 0f, 0.60f));
                    }
                    else
                    {
                        // Default semi-transparent red
                        RegionTargetIndicator.Instance.SetIndicatorColor(new Color(1f, 0.3f, 0.3f, 0.35f));
                    }
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Debug.Log($"[AIM-HANDLER] OnPointerUp called, _isAiming={_isAiming}");
            if (!_isAiming)
                return;

            _isAiming = false;

            if (RegionTargetIndicator.Instance != null)
            {
                RegionTargetIndicator.Instance.ManualPositionOverride = false;
                RegionTargetIndicator.Instance.SetIndicatorColor(new Color(1f, 0.3f, 0.3f, 0.35f)); // Reset color
            }

            var magicMgr = KOMagicSkillManager.Instance;
            if (magicMgr != null)
            {
                bool isCancelled = (eventData.position.y > Screen.height * _cancelThreshold);
                Debug.Log($"[AIM-HANDLER] Cast confirmed. Pos={_currentTargetPos}, Cancelled={isCancelled}");
                if (isCancelled)
                {
                    magicMgr.CancelRegionTargeting();
                }
                else
                {
                    magicMgr.ConfirmRegionTarget(_currentTargetPos);
                }
            }
        }
    }
}
