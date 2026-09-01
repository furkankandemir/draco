using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace EntropyOnline.UI
{
    public class KOSkillBarSlotDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int SlotIndex;

        private int _draggedMagicNum;
        private Sprite _draggedIcon;
        private GameObject _dragCanvas;
        private Transform _dragIconTransform;

        private void OnDisable()
        {
            DestroyDragIcon();
            
            // Kaynak slot ikonunu normal yap (Eğer boşsa şeffaf yap)
            if (MobileSkillBar.Instance != null)
            {
                int magicNum = MobileSkillBar.Instance.GetSlotMagicNum(SlotIndex);
                var iconImg = MobileSkillBar.Instance.GetSlotIconImage(SlotIndex);
                if (iconImg != null)
                {
                    iconImg.color = magicNum > 0 ? Color.white : Color.clear;
                }
            }
        }

        private void OnDestroy()
        {
            DestroyDragIcon();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (MobileSkillBar.Instance == null) return;
            if (MobileSkillBar.Instance.IsEditMode)
            {
                return; // Let edit mode movement drag proceed
            }

            _draggedMagicNum = MobileSkillBar.Instance.GetSlotMagicNum(SlotIndex);
            if (_draggedMagicNum <= 0)
            {
                eventData.pointerDrag = null; // Sürüklemeyi iptal et
                return;
            }

            // Disable swap drag for AoE skills so they can be aimed by KOSkillAimHandler
            var pSkill = KOImport.SkillTableParser.Find(_draggedMagicNum);
            if (pSkill != null && (pSkill.Target == 10 || pSkill.Target == 11 || pSkill.Target == 12))
            {
                return;
            }

            _draggedIcon = KOItemIconLoader.LoadSkillIcon(_draggedMagicNum);

            // Kaynak slot ikonunu yarı saydam yap
            var iconImg = MobileSkillBar.Instance.GetSlotIconImage(SlotIndex);
            if (iconImg != null)
            {
                iconImg.color = new Color(1f, 1f, 1f, 0.4f);
            }

            CreateDragIcon(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (MobileSkillBar.Instance != null && MobileSkillBar.Instance.IsEditMode)
            {
                var rt = GetComponent<RectTransform>();
                var canvas = GetComponentInParent<Canvas>();
                if (rt != null && canvas != null)
                {
                    rt.anchoredPosition += eventData.delta / canvas.scaleFactor;
                }
                return;
            }

            if (_dragIconTransform != null)
            {
                _dragIconTransform.position = new Vector3(eventData.position.x, eventData.position.y, 0f);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (MobileSkillBar.Instance != null)
            {
                if (MobileSkillBar.Instance.IsEditMode)
                {
                    return;
                }

                int targetSlot = MobileSkillBar.Instance.GetSlotAtScreenPosition(eventData.position);
                if (targetSlot >= 0)
                {
                    // Hedef slota sürüklenirse: yer değiştir (swap)
                    MobileSkillBar.Instance.SwapSkillSlots(SlotIndex, targetSlot);
                }
                else
                {
                    // Dışarıya boşluğa bırakılırsa: slotu temizle
                    MobileSkillBar.Instance.ClearSkillSlot(SlotIndex);
                }
            }

            DestroyDragIcon();
        }

        private void CreateDragIcon(PointerEventData eventData)
        {
            DestroyDragIcon();

            var canvasObj = new GameObject("SkillBarDragCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            _dragCanvas = canvasObj;

            var imgObj = new GameObject("DragImage");
            imgObj.transform.SetParent(canvasObj.transform, false);

            var rt = imgObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(50f, 50f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = imgObj.AddComponent<Image>();
            if (_draggedIcon != null)
            {
                img.sprite = _draggedIcon;
                img.color = Color.white;
            }
            img.raycastTarget = false;

            _dragIconTransform = imgObj.transform;
            _dragIconTransform.position = new Vector3(eventData.position.x, eventData.position.y, 0f);
        }

        private void DestroyDragIcon()
        {
            if (_dragCanvas != null)
            {
                Destroy(_dragCanvas);
                _dragCanvas = null;
                _dragIconTransform = null;
            }

            if (MobileSkillBar.Instance != null)
            {
                MobileSkillBar.Instance.RefreshPage();
            }
        }
    }
}
