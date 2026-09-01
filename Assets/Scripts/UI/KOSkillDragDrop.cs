// ====================================================================
// KOSkillDragDrop.cs — SkillTree → MobileSkillBar sürükle-bırak
//
// SkillTreeUI'daki her skill ikonuna eklenir.
// Öğrenilmiş skill ikonları sürüklenip MobileSkillBar slotlarına
// bırakılarak skill atanabilir.
//
// C++ referans: UISkillTreeDlg.cpp ReceiveMessage — UIMSG_ICON_DOWN_FIRST
//   → UIHotKeyDlg::SetReceiveSelectedSkill (cpp:594-624)
// ====================================================================
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EntropyOnline.UI
{
    /// <summary>
    /// SkillTree skill ikonlarına eklenen drag handler.
    /// Sürükle → MobileSkillBar slotuna bırak → skill ata.
    /// </summary>
    public class KOSkillDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // Skill bilgileri — SkillTreeUI tarafından set edilir
        private int _magicNum;
        private Sprite _skillIcon;
        private bool _hasLevel;

        // Drag sırasında oluşturulan geçici ikon
        private static GameObject _dragCanvas;
        private static Transform _dragIconTransform;
        private static KOSkillDragDrop _currentDrag;

        private void OnDisable()
        {
            if (_currentDrag == this)
            {
                DestroyDragIcon();
                _currentDrag = null;
            }
        }

        private void OnDestroy()
        {
            if (_currentDrag == this)
            {
                DestroyDragIcon();
                _currentDrag = null;
            }
        }

        /// <summary>Skill bilgilerini güncelle — SkillTreeUI.RefreshSkillSlots'ta çağrılır.</summary>
        public void SetSkillData(int magicNum, Sprite icon, bool hasLevel)
        {
            _magicNum = magicNum;
            _skillIcon = icon;
            _hasLevel = hasLevel;
        }

        // ============================
        // IBeginDragHandler
        // ============================

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Öğrenilmemiş skill sürüklenemez
            if (!_hasLevel || _magicNum <= 0)
            {
                eventData.pointerDrag = null; // Drag'ı iptal et
                return;
            }

            _currentDrag = this;
            CreateDragIcon(eventData);

            var aaSettings = KOMobileAutoAttackSettingsUI.Instance;
            if (aaSettings != null && aaSettings.gameObject.activeInHierarchy)
            {
                aaSettings.HighlightSlotsForSkill(_magicNum);
            }
        }

        // ============================
        // IDragHandler
        // ============================

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragIconTransform != null)
                _dragIconTransform.position = new Vector3(eventData.position.x, eventData.position.y, 0f);
        }

        // ============================
        // IEndDragHandler
        // ============================

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_currentDrag != this) return;

            var aaSettings = KOMobileAutoAttackSettingsUI.Instance;
            bool handledInAA = false;

            if (aaSettings != null && aaSettings.gameObject.activeInHierarchy)
            {
                int aaSlotIndex = aaSettings.GetSlotAtScreenPosition(eventData.position);
                if (aaSlotIndex >= 0)
                {
                    if (aaSettings.ValidateAndDropSkill(aaSlotIndex, _magicNum, _skillIcon))
                    {
                        handledInAA = true;
                    }
                }
                else
                {
                    // Check if dropped on HP/MP potion slots to show English warning toasts
                    int aaItemSlotIndex = aaSettings.GetItemSlotAtScreenPosition(eventData.position);
                    if (aaItemSlotIndex == 24)
                    {
                        KOUIManager.Instance?.ShowToast("Only HP recovery potions can be placed in this slot.");
                    }
                    else if (aaItemSlotIndex == 25)
                    {
                        KOUIManager.Instance?.ShowToast("Only MP recovery potions can be placed in this slot.");
                    }
                }
            }

            if (aaSettings != null)
            {
                aaSettings.ResetSlotHighlights();
            }

            if (handledInAA)
            {
                DestroyDragIcon();
                _currentDrag = null;
                return;
            }

            // MobileSkillBar'daki en yakın slotu bul
            var skillBar = MobileSkillBar.Instance;
            if (skillBar != null)
            {
                int slotIndex = skillBar.GetSlotAtScreenPosition(eventData.position);
                if (slotIndex >= 0)
                {
                    // C++ birebir: UIHotKeyDlg::SetReceiveSelectedSkill (cpp:594-624)
                    skillBar.SetSkillIcon(slotIndex, _skillIcon, _magicNum);
                }
                else
                {
                }
            }

            DestroyDragIcon();
            _currentDrag = null;
        }

        // ============================
        // Drag Icon — dedicated ScreenSpaceOverlay canvas
        // ============================

        private void CreateDragIcon(PointerEventData eventData)
        {
            DestroyDragIcon();

            // ScreenSpaceOverlay canvas — her zaman en üstte
            var canvasObj = new GameObject("SkillDragCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            _dragCanvas = canvasObj;

            // Skill ikonu
            var imgObj = new GameObject("DragImage");
            imgObj.transform.SetParent(canvasObj.transform, false);

            var rt = imgObj.AddComponent<RectTransform>();

            // Kaynak ikonun ekrandaki gerçek piksel boyutunu al
            var srcRT = GetComponent<RectTransform>();
            float w = srcRT.rect.width * srcRT.lossyScale.x;
            float h = srcRT.rect.height * srcRT.lossyScale.y;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = imgObj.AddComponent<Image>();
            if (_skillIcon != null)
            {
                img.sprite = _skillIcon;
                img.color = Color.white; // %100 opacity — aynı görünüm
            }
            else
            {
                img.color = new Color(0.5f, 0.8f, 1f, 0.6f);
            }
            img.raycastTarget = false;

            _dragIconTransform = imgObj.transform;
            _dragIconTransform.position = new Vector3(eventData.position.x, eventData.position.y, 0f);
        }

        private static void DestroyDragIcon()
        {
            if (_dragCanvas != null)
            {
                Destroy(_dragCanvas);
                _dragCanvas = null;
                _dragIconTransform = null;
            }
        }
    }
}
