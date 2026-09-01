using UnityEngine;

using UnityEngine.EventSystems;

using UnityEngine.UI;



namespace EntropyOnline.UI

{

    /// <summary>

    /// Open-KO birebir: Icon Drag & Drop mekanizması — mobil adaptasyon.

    /// 

    /// C++ referans: N3UIWndBase.h satır 162 (IconRestore)

    ///               UIInventory.cpp satır 566-572 (UI_STATE_ICON_MOVING + mouse tracking)

    ///               UIInventory.cpp satır 1139-1220 (ReceiveIconDrop)

    ///               UIManager.cpp satır 349 (ReceiveIconDrop dispatcher)

    ///               UIInventory.cpp satır 577-588 (SendInvMsg)

    /// 

    /// Open-KO'da:

    ///   Sol tıklama → icon seçilir (s_sSelectedIconInfo) → UI_STATE_ICON_MOVING

    ///   Fare cursor'unda icon takip eder

    ///   Hedef slot'a bırakılır → ReceiveIconDrop → SendInvMsg

    ///

    /// Mobilde:

    ///   Uzun basma (0.3s) → ghost icon oluşturulur

    ///   Parmakla sürükleme → ghost icon takip eder

    ///   Hedef slot'a bırakma → ReceiveIconDrop → SendInvMsg

    ///

    /// Her inventory slot'a bu component eklenir (KOUIManager tarafından).

    /// </summary>

    public class KOItemDragHandler : MonoBehaviour,

        IBeginDragHandler, IDragHandler, IEndDragHandler,

        IPointerDownHandler, IPointerUpHandler

    {

        // Open-KO birebir: s_sSelectedIconInfo karşılığı

        // UIWndDistrict: UIWND_DISTRICT_INVENTORY_SLOT (1) veya UIWND_DISTRICT_INVENTORY_INV (2)

        public enum SlotDistrict

        {

            EquipSlot = 1,  // UIWND_DISTRICT_INVENTORY_SLOT

            BagSlot = 2     // UIWND_DISTRICT_INVENTORY_INV

        }



        [Header("Slot Bilgisi")]

        public SlotDistrict district;

        public int slotIndex;

        // Ghost icon (Open-KO'daki sürüklenen icon — cursor'da takip eden kopya)
        private static GameObject _ghostIcon;
        private static KOItemDragHandler _dragSource;
        private static Canvas _rootCanvas;

        /// <summary>
        /// Open-KO birebir: s_sSelectedIconInfo — aktif sürüklenen item bilgisi.
        /// KOTradeDropTarget.OnDrop'ta drag kaynağını belirlemek için kullanılır.
        /// </summary>
        public static KOItemDragHandler CurrentDragSource => _dragSource;

        // Long press kontrolü
        private float _pointerDownTime;
        private const float LONG_PRESS_DURATION = 0.3f;

        // Kaynak item bilgisi
        private Image _slotImage;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _slotImage = GetComponent<Image>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnDisable()
        {
            if (_dragSource == this)
            {
                DestroyGhostIcon();
                _dragSource = null;
            }
        }

        private void OnDestroy()
        {
            if (_dragSource == this)
            {
                DestroyGhostIcon();
                _dragSource = null;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (KOInventory.Instance != null && KOInventory.Instance.IsSorting) return;
            _pointerDownTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
        }

        // =============================================
        // Open-KO birebir: Icon kaldırma (UI_STATE_ICON_MOVING başlangıcı)
        // C++ UIInventory.cpp satır 566-572
        // =============================================
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (KOInventory.Instance != null && KOInventory.Instance.IsSorting) return;

            // Open-KO: Slot boşsa drag başlatma
            if (!HasItem()) return;

            // Block dragging if the item is already registered in setup
            bool blockDrag = false;
            if (EntropyOnline.Trade.KOMerchantManager.Instance != null)
            {
                if (EntropyOnline.Trade.KOMerchantManager.Instance.IsSellingSetup && district == SlotDistrict.BagSlot)
                {
                    foreach (var setupItem in EntropyOnline.Trade.KOMerchantManager.Instance.SellingSetupItems)
                    {
                        if (setupItem != null && !setupItem.IsEmpty && setupItem.InvPos == slotIndex)
                        {
                            blockDrag = true;
                            break;
                        }
                    }
                }
            }

            if (blockDrag)
            {
                eventData.pointerDrag = null; // Cancel Unity drag
                return;
            }

            // Open-KO birebir: s_sSelectedIconInfo set et
            _dragSource = this;

            // Open-KO birebir: Ghost icon oluştur (cursor'da takip eden kopya)
            // C++ satır 570-571: pItemSelect->pUIIcon->SetRegion(GetSampleRect())
            CreateGhostIcon(eventData);

            _canvasGroup.alpha = 0f;

            _canvasGroup.blocksRaycasts = false;






            int itemId = 0;

            if (district == SlotDistrict.EquipSlot)

            {

                var inventoryItem = KOInventory.Instance != null ? KOInventory.Instance.m_pMySlot[slotIndex] : null;

                if (inventoryItem != null) itemId = inventoryItem.itemId;

            }

            else

            {

                var inventoryItem = KOInventory.Instance != null ? KOInventory.Instance.m_pMyInvWnd[slotIndex] : null;

                if (inventoryItem != null) itemId = inventoryItem.itemId;

            }



            if (itemId > 0)

            {

                var aaSettings = KOMobileAutoAttackSettingsUI.Instance;

                if (aaSettings != null && aaSettings.gameObject.activeInHierarchy)

                {

                    aaSettings.HighlightSlotsForItem(itemId);

                }

            }

        }



        // =============================================

        // Open-KO birebir: Icon fare pozisyonuna taşı

        // C++ UIInventory.cpp satır 566-572 (MouseProc → icon güncelleme)

        // =============================================

        public void OnDrag(PointerEventData eventData)

        {

            if (_ghostIcon == null) return;



            // Ghost icon'u parmak pozisyonuna taşı

            UpdateGhostPosition(eventData.position);

        }



        // =============================================

        // Open-KO birebir: ReceiveIconDrop

        // C++ UIInventory.cpp satır 1139-1220

        // UIManager.cpp satır 349: ReceiveIconDrop dispatcher

        // =============================================

        public void OnEndDrag(PointerEventData eventData)

        {

            if (_dragSource != this) return;



            // Ghost icon'u yok et

            DestroyGhostIcon();



            // Open-KO birebir: IconRestore — kaynak slot'u geri yükle

            // C++ UIInventory.cpp satır 1443-1476

            _canvasGroup.alpha = 1f;

            _canvasGroup.blocksRaycasts = true;



            // Auto Attack Settings check

            var aaSettings = KOMobileAutoAttackSettingsUI.Instance;

            bool handledInAA = false;



            if (aaSettings != null && aaSettings.gameObject.activeInHierarchy)

            {

                int aaItemSlotIndex = aaSettings.GetItemSlotAtScreenPosition(eventData.position);

                if (aaItemSlotIndex >= 0)

                {

                    var src = _dragSource;

                    if (src != null && src.HasItem())

                    {

                        int itemId = 0;

                        if (src.district == SlotDistrict.EquipSlot)

                        {

                            var inventoryItem = KOInventory.Instance.m_pMySlot[src.slotIndex];

                            if (inventoryItem != null) itemId = inventoryItem.itemId;

                        }

                        else

                        {

                            var inventoryItem = KOInventory.Instance.m_pMyInvWnd[src.slotIndex];

                            if (inventoryItem != null) itemId = inventoryItem.itemId;

                        }



                        if (itemId > 0)

                        {

                            Sprite itemIcon = null;

                            var childImages = src.GetComponentsInChildren<Image>();

                            foreach (var childImg in childImages)

                            {

                                if (childImg != src._slotImage && childImg.sprite != null)

                                {

                                    itemIcon = childImg.sprite;

                                    break;

                                }

                            }

                            

                            if (itemIcon == null && src._slotImage != null)

                            {

                                itemIcon = src._slotImage.sprite;

                            }

                            

                            if (itemIcon != null)

                            {

                                if (aaSettings.ValidateAndDropItem(aaItemSlotIndex, itemId, itemIcon))

                                {


                                    handledInAA = true;

                                }

                            }

                        }

                    }

                }

                else

                {

                    // Check if dropped on Attack slots (0-11) to show warning toast

                    int aaSkillSlotIndex = aaSettings.GetSlotAtScreenPosition(eventData.position);

                    if (aaSkillSlotIndex >= 0 && aaSkillSlotIndex < 12)

                    {

                        KOUIManager.Instance?.ShowToast("Only active attack or debuff skills can be placed in these slots.");

                    }

                }

            }



            if (aaSettings != null)

            {

                aaSettings.ResetSlotHighlights();

            }



            if (handledInAA)

            {

                _dragSource = null;

                return;

            }



            // Mobile Skill Bar check

            var skillBar = MobileSkillBar.Instance;

            if (skillBar != null && skillBar.gameObject.activeInHierarchy)

            {

                int slotIndex = skillBar.GetSlotAtScreenPosition(eventData.position);

                if (slotIndex >= 0)

                {

                    var src = _dragSource;

                    if (src != null && src.HasItem())

                    {

                        int itemId = 0;

                        if (src.district == SlotDistrict.EquipSlot)

                        {

                            var inventoryItem = KOInventory.Instance.m_pMySlot[src.slotIndex];

                            if (inventoryItem != null) itemId = inventoryItem.itemId;

                        }

                        else

                        {

                            var inventoryItem = KOInventory.Instance.m_pMyInvWnd[src.slotIndex];

                            if (inventoryItem != null) itemId = inventoryItem.itemId;

                        }



                        if (itemId > 0)

                        {

                            var skill = KOImport.SkillTableParser.FindByExhaustItem((uint)itemId);

                            if (skill != null)

                            {

                                Sprite itemIcon = null;

                                var childImages = src.GetComponentsInChildren<Image>();

                                foreach (var childImg in childImages)

                                {

                                    if (childImg != src._slotImage && childImg.sprite != null)

                                    {

                                        itemIcon = childImg.sprite;

                                        break;

                                    }

                                }

                                if (itemIcon == null && src._slotImage != null)

                                    itemIcon = src._slotImage.sprite;



                                skillBar.SetSkillIcon(slotIndex, itemIcon, skill.Id);


                                _dragSource = null;

                                return;

                            }

                            else

                            {

                                KOUIManager.Instance?.ShowToast("This item cannot be added to the shortcut bar.");

                            }

                        }

                    }

                }

            }



            // =============================================

            // Open-KO birebir: m_pArea_Destroy->IsIn(ptCur.x, ptCur.y)

            // C++ UIInventory.cpp satır 1180-1217

            // Item'ı "area_samma" (kırmızı çöp kutusu) üzerine bırakma kontrolü

            // =============================================

            if (IsDropOnDestroyZone(eventData))

            {

                var src = _dragSource;

                bool isEquip = src.district == SlotDistrict.EquipSlot;

                int idx = src.slotIndex;

                _dragSource = null;



                // C++ birebir: m_bDestoyDlgAlive = true → onay diyaloğu göster

                // UIInventory.cpp satır 1182, 2678-2709, 2853-2876

                if (KOMessageBox.Instance != null)
                {
                    KOMessageBox.Instance.ShowYesNo(
                        "Would you like to destroy this item?", // C++ IDS_ITEM_DESTROY_CONFIRM
                        "",                                     // title
                        MsgBoxBehavior.BEHAVIOR_NOTHING,         // behavior
                        () =>
                        {
                            // C++ birebir: ItemDestroyOK() — onay → SendItemDestroy
                            if (KOInventory.Instance != null)
                            {
                                KOInventory.Instance.SendItemDestroy(isEquip, idx);
                            }
                        },
                        () =>
                        {
                            // C++ birebir: ItemDestroyCancel() — iptal → IconRestore
                        },
                        KOUIManager.Instance?.InventoryPanel // envanter panelini callerPanel olarak belirle
                    );
                }

                else

                {

                    Debug.LogWarning("[DRAG] KOMessageBox yüklenmemiş — destroy onay gösterilemiyor");

                }

                return;

            }



            // Hedef upgrade slot'unu bul (raycast)

            var upgradeTarget = FindUpgradeDropTarget(eventData);

            if (upgradeTarget != null)

            {

                // Upgrade/Requirement slot'una bırakıldı

                upgradeTarget.OnDrop(eventData);

            }

            else

            {

                // Hedef slot'u bul (raycast)

                var dropTarget = FindDropTarget(eventData);

                if (dropTarget != null && dropTarget != this)

                {

                    // Open-KO birebir: ReceiveIconDrop → SendInvMsg

                    ProcessDrop(_dragSource, dropTarget);

                }

                else

                {

                    // Open-KO birebir: Aynı slot'a bırakma veya boşluğa bırakma → IconRestore


                }

            }



            _dragSource = null;

        }



        // =============================================

        // Open-KO birebir: ReceiveIconDrop → CheckIconDropIfSuccessSendToServer → SendInvMsg

        // C++ UIInventory.cpp satır 1139-1220

        // =============================================

        private void ProcessDrop(KOItemDragHandler source, KOItemDragHandler target)

        {

            if (KOInventory.Instance == null) return;



            // C++ birebir: UIPerTradeDlg.cpp:352-377 — trade açıkken envanter item'ları

            // trade dialog'a taşınır (ItemMoveFromInvToThis). Envanter penceresi boş kalır,

            // dolayısıyla envanter→envanter taşıma doğal olarak engellenir.

            // Bizde item'lar yerinde kaldığından, trade Normal/Editing state'inde

            // envanter→envanter taşımayı açıkça engelliyoruz.

            if (EntropyOnline.Trade.KOTradeManager.Instance != null &&

                EntropyOnline.Trade.KOTradeManager.Instance.State != EntropyOnline.Trade.KOTradeManager.PerTradeState.None)

            {


                return;

            }



            byte bDir;

            int srcPos = source.slotIndex;

            int destPos = target.slotIndex;

            int itemId;



            // Open-KO birebir: 4 yön — UIInventory.cpp CheckIconDropIfSuccessSendToServer satır 792-1021

            if (source.district == SlotDistrict.EquipSlot && target.district == SlotDistrict.EquipSlot)

            {

                // Arm → Arm (0x04) — C++ satır 792-860

                bDir = KOInventory.ITEM_MOVE_ARM_TO_ARM;

                var item = KOInventory.Instance.m_pMySlot[srcPos];

                if (item == null || item.IsEmpty) return;

                itemId = item.itemId;



                // C++ satır 798, 824: IsValidPosFromArmToArm(iDestiOrder)

                if (!KOInventory.Instance.IsValidPosFromArmToArm(srcPos, destPos))

                {

                    Debug.LogWarning($"[DRAG] Arm→Arm: geçersiz slot ({srcPos}→{destPos}), taşıma iptal");

                    return;

                }

            }

            else if (source.district == SlotDistrict.EquipSlot && target.district == SlotDistrict.BagSlot)

            {

                // Arm → Inv (0x02) — C++ satır 862-916

                bDir = KOInventory.ITEM_MOVE_ARM_TO_INV;

                var item = KOInventory.Instance.m_pMySlot[srcPos];

                if (item == null || item.IsEmpty) return;

                itemId = item.itemId;



                // C++ satır 865: if (!m_pMyInvWnd[iDestiOrder]) → direkt gönder

                // C++ satır 884-906: else → ilk boş slot bul

                var targetSlot = KOInventory.Instance.m_pMyInvWnd[destPos];

                if (targetSlot != null && !targetSlot.IsEmpty)

                {

                    int freeIdx = KOInventory.Instance.GetInvDestinationIndex();

                    if (freeIdx < 0)

                    {

                        Debug.LogWarning("[DRAG] Arm→Inv: bag dolu, taşıma iptal");

                        return;

                    }

                    destPos = freeIdx;

                }

            }

            else if (source.district == SlotDistrict.BagSlot && target.district == SlotDistrict.EquipSlot)

            {

                // Inv → Arm (0x01) — C++ satır 917-982

                bDir = KOInventory.ITEM_MOVE_INV_TO_ARM;

                var item = KOInventory.Instance.m_pMyInvWnd[srcPos];

                if (item == null || item.IsEmpty) return;

                itemId = item.itemId;



                // C++ satır 924, 949: IsValidPosFromInvToArm(iDestiOrder)

                if (!KOInventory.Instance.IsValidPosFromInvToArm(srcPos, destPos))

                {

                    Debug.LogWarning($"[DRAG] Inv→Arm: geçersiz slot ({srcPos}→{destPos}), attachPoint uyumsuz");

                    return;

                }



                // C++ satır 1997: IsValidRaceAndClass(pItem, pItemExt)

                if (!KOInventory.Instance.IsValidRaceAndClass(itemId))

                {

                    Debug.LogWarning($"[DRAG] Inv→Arm: race/class uyumsuz (item={itemId})");

                    return;

                }

            }

            else if (source.district == SlotDistrict.BagSlot && target.district == SlotDistrict.BagSlot)

            {

                // Inv → Inv (0x03) — C++ satır 983-1021

                bDir = KOInventory.ITEM_MOVE_INV_TO_INV;

                var item = KOInventory.Instance.m_pMyInvWnd[srcPos];

                if (item == null || item.IsEmpty) return;

                itemId = item.itemId;

            }

            else

            {

                return;

            }






            // Open-KO birebir: SendInvMsg (UIInventory.cpp satır 577-588)

            KOInventory.Instance.SendInvMsg(bDir, itemId, srcPos, destPos);

        }



        // =============================================

        // Ghost Icon Yönetimi

        // Open-KO'da icon kaldırıldığında cursor'da takip eden kopya

        // =============================================



        private void CreateGhostIcon(PointerEventData eventData)

        {

            if (_ghostIcon != null)

                Destroy(_ghostIcon);



            // Dedicated ScreenSpaceOverlay canvas — her zaman en üstte

            var canvasObj = new GameObject("GhostDragCanvas");

            var ghostCanvas = canvasObj.AddComponent<Canvas>();

            ghostCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            ghostCanvas.sortingOrder = 30000;

            // CanvasScaler EKLENMİYOR — ScreenSpaceOverlay'de 1:1 pixel koordinatları kullanılır



            _ghostIcon = canvasObj;



            // İçine Image ekle

            var imgObj = new GameObject("GhostImage");

            imgObj.transform.SetParent(canvasObj.transform, false);



            var rt = imgObj.AddComponent<RectTransform>();

            // Slot'un ekrandaki gerçek piksel boyutunu al

            var srcRt = GetComponent<RectTransform>();

            float w = srcRt.rect.width * srcRt.lossyScale.x;

            float h = srcRt.rect.height * srcRt.lossyScale.y;

            rt.sizeDelta = new Vector2(w, h);

            rt.anchorMin = new Vector2(0.5f, 0.5f);

            rt.anchorMax = new Vector2(0.5f, 0.5f);

            rt.pivot = new Vector2(0.5f, 0.5f);



            var img = imgObj.AddComponent<Image>();

            Sprite itemSprite = null;



            // Child Image'lardan item ikonunu bul (ilk sprite'lı child)

            var childImages = GetComponentsInChildren<Image>();

            foreach (var childImg in childImages)

            {

                if (childImg != _slotImage && childImg.sprite != null)

                {

                    itemSprite = childImg.sprite;

                    break;

                }

            }

            if (itemSprite == null && _slotImage != null)

                itemSprite = _slotImage.sprite;



            if (itemSprite != null)

            {

                img.sprite = itemSprite;

                img.color = new Color(1f, 1f, 1f, 1f);

            }

            else

            {

                img.color = new Color(0.5f, 0.8f, 1f, 0.6f);

            }



            img.raycastTarget = false;



            // Başlangıç pozisyonu

            UpdateGhostPosition(eventData.position);




        }



        /// <summary>

        /// Ghost icon'u ekran pozisyonuna taşı.

        /// ScreenSpaceOverlay canvas'ta transform.position = ekran pikseli doğrudan çalışır.

        /// </summary>

        private static void UpdateGhostPosition(Vector2 screenPos)

        {

            if (_ghostIcon == null) return;

            if (_ghostIcon.transform.childCount == 0) return;



            // ScreenSpaceOverlay canvas'ta child'ın world position = ekran pikseli

            _ghostIcon.transform.GetChild(0).position = new Vector3(screenPos.x, screenPos.y, 0f);

        }



        private void DestroyGhostIcon()

        {

            if (_ghostIcon != null)

            {

                Destroy(_ghostIcon);

                _ghostIcon = null;

            }

        }



        // =============================================

        // Yardımcı Fonksiyonlar

        // =============================================



        /// <summary>

        /// Bu slot'ta item var mı?

        /// </summary>

        private bool HasItem()

        {

            if (KOInventory.Instance == null) return false;



            if (district == SlotDistrict.EquipSlot)

            {

                var item = KOInventory.Instance.m_pMySlot[slotIndex];

                return item != null && !item.IsEmpty;

            }

            else

            {

                if (slotIndex < 0 || slotIndex >= KOInventory.MAX_ITEM_INVENTORY) return false;

                var item = KOInventory.Instance.m_pMyInvWnd[slotIndex];

                return item != null && !item.IsEmpty;

            }

        }



        /// <summary>

        /// Drop hedefini bul (EventSystem raycast ile).

        /// Open-KO birebir: UIManager.cpp satır 347-351 — ReceiveIconDrop dispatcher

        /// </summary>

        private KOItemDragHandler FindDropTarget(PointerEventData eventData)

        {

            var results = new System.Collections.Generic.List<RaycastResult>();

            EventSystem.current.RaycastAll(eventData, results);



            foreach (var result in results)

            {

                var handler = result.gameObject.GetComponent<KOItemDragHandler>();

                if (handler != null && handler != this)

                    return handler;

            }

            return null;

        }



        /// <summary>

        /// Upgrade drop target hedefini bul (EventSystem raycast ile).

        /// </summary>

        private KOUpgradeDropTarget FindUpgradeDropTarget(PointerEventData eventData)

        {

            var results = new System.Collections.Generic.List<RaycastResult>();

            EventSystem.current.RaycastAll(eventData, results);


            foreach (var result in results)

            {

                var target = result.gameObject.GetComponent<KOUpgradeDropTarget>();


                if (target != null)

                    return target;

            }

            return null;

        }



        /// <summary>

        /// Dialog'u inventory panelinin ortasına taşır.

        /// GetWorldCorners ile gerçek ekran pozisyonunu alıp doğrudan yerleştirir.

        /// </summary>

        private static void PositionDialogOverInventory()
        {
            // Update loop within KOUIManager handles positioning dynamically in Canvas space
        }



        /// <summary>

        /// Open-KO birebir: m_pArea_Destroy->IsIn(ptCur.x, ptCur.y)

        /// C++ UIInventory.cpp satır 1180

        /// 

        /// UIF'te "area_samma" adlı area — envanterdeki kırmızı çöp kutusu.

        /// Drop pozisyonunun bu area'nın RectTransform sınırları içinde

        /// olup olmadığını kontrol eder.

        /// </summary>

        private bool IsDropOnDestroyZone(PointerEventData eventData)

        {

            // Inventory panel root'u bul

            var uiMgr = KOUIManager.Instance;

            if (uiMgr == null) return false;



            // _uiInventory field'ına doğrudan erişemiyoruz — hierarchy'den bul

            // C++ birebir: m_pArea_Destroy = GetChildByID<CN3UIArea>("area_samma")

            // UIF'te "area_samma" adlı area — envanterdeki kırmızı çöp kutusu

            var areaSamma = FindAreaSammaInScene();

            if (areaSamma == null) return false;



            // C++ birebir: m_pArea_Destroy->IsIn(ptCur.x, ptCur.y)

            // RectTransform bazlı hit test — raycast gerekmez

            Canvas canvas = areaSamma.GetComponentInParent<Canvas>();

            UnityEngine.Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)

                ? canvas.worldCamera : null;



            if (RectTransformUtility.RectangleContainsScreenPoint(areaSamma, eventData.position, cam))

            {


                return true;

            }



            return false;

        }



        /// <summary>

        /// "area_samma" GameObject'ini inventory panelinde bul.

        /// Sonucu cache'le — her frame aramak pahalı.

        /// </summary>

        private static RectTransform _cachedAreaSamma;

        private static bool _areaSammaSearched;



        private static RectTransform FindAreaSammaInScene()

        {

            if (_areaSammaSearched && _cachedAreaSamma != null)

                return _cachedAreaSamma;



            // Scene'deki tüm KOUIArea'lar arasında "area_samma" adlı olanı bul

            var allAreas = Object.FindObjectsByType<EntropyOnline.Import.KOUIArea>(FindObjectsInactive.Include);

            foreach (var area in allAreas)

            {

                if (area.gameObject.name == "area_samma")

                {

                    _cachedAreaSamma = area.GetComponent<RectTransform>();

                    _areaSammaSearched = true;


                    return _cachedAreaSamma;

                }

            }



            _areaSammaSearched = true;

            return null;

        }

    }

}

