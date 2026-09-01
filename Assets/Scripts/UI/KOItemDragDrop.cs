using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EntropyOnline.UI
{
    /// <summary>
    /// C++ birebir: UIMSG_ICON_DOWN_FIRST → drag başlar
    /// UIMSG_ICON_DOWN → sürükleme devam eder (ikon fareyi takip eder)
    /// UIMSG_ICON_UP → BroadcastIconDropMsg() → ReceiveIconDrop()
    /// 
    /// Bu component her item slot'una eklenir ve drag-drop ile alım/satım sağlar.
    /// N3UIWndBase::BroadcastIconDropMsg birebir.
    /// </summary>
    public class KOItemDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private KOItemSlotHandler _slotHandler;
        
        // Drag sırasında oluşturulan geçici ikon — dedicated ScreenSpaceOverlay canvas
        // KOItemDragHandler ile aynı yöntem
        private static GameObject _dragIcon;
        private static Transform _dragIconImage;
        
        // Drag başlangıç bilgileri
        private static KOItemDragDrop _currentDrag;
        
        /// <summary>Sürükleme devam ediyor mu?</summary>
        public static bool IsDragging => _currentDrag != null;
        
        /// <summary>Sürüklenen slot handler</summary>
        public static KOItemSlotHandler DragSource => _currentDrag?._slotHandler;

        private void Awake()
        {
            _slotHandler = GetComponent<KOItemSlotHandler>();
        }

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

        /// <summary>
        /// C++ birebir: UIMSG_ICON_DOWN_FIRST — drag başlangıcı
        /// UITransactionDlg.cpp satır 1493-1508
        /// s_sSelectedIconInfo doldurulur, ikon sürükleme başlar.
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_slotHandler == null) return;
            
            // Boş slot → drag başlamaz
            if (_slotHandler.itemData == null && _slotHandler.tooltipItemDefId <= 0) return;
            
            _currentDrag = this;
            
            // Drag ikonu oluştur — KOItemDragHandler ile aynı yöntem:
            // Dedicated ScreenSpaceOverlay canvas — her zaman en üstte
            CreateDragIcon(eventData);
            
        }

        /// <summary>
        /// C++ birebir: UIMSG_ICON_DOWN — sürükleme devam eder
        /// İkon fareyi/parmağı takip eder.
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            UpdateDragPosition(eventData.position);
        }

        /// <summary>
        /// C++ birebir: UIMSG_ICON_UP → BroadcastIconDropMsg → ReceiveIconDrop
        /// UITransactionDlg.cpp satır 873-1186
        /// </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (_currentDrag != this) return;
            
            // Drag ikonu temizle
            DestroyDragIcon();
            
            // Önce Auto Attack Settings paneline sürüklenip bırakılma durumunu kontrol et
            var aaSettings = KOMobileAutoAttackSettingsUI.Instance;
            if (aaSettings != null && aaSettings.gameObject.activeInHierarchy)
            {
                int aaItemSlotIndex = aaSettings.GetItemSlotAtScreenPosition(eventData.position);
                if (aaItemSlotIndex >= 0)
                {
                    aaSettings.SetItemSlot(aaItemSlotIndex, _slotHandler);
                    _currentDrag = null;
                    return;
                }
            }

            // Drop hedefini bul
            KOTradeDropTarget tradeDropTarget = null;
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var r in results)
            {
                tradeDropTarget = r.gameObject.GetComponent<KOTradeDropTarget>() ?? r.gameObject.GetComponentInParent<KOTradeDropTarget>();
                if (tradeDropTarget != null) break;
            }

            if (tradeDropTarget != null)
            {
                // Unity's EventSystem automatically triggers IDropHandler.OnDrop on KOTradeDropTarget.
                // Manual invocation here caused the drop to be processed twice, duplicating items.
            }
            else
            {
                var dropTarget = FindDropTarget(eventData);
                if (dropTarget != null && dropTarget != _slotHandler)
                {
                    HandleIconDrop(_slotHandler, dropTarget);
                }
                else if (_slotHandler != null && 
                         _slotHandler.slotType == KOItemSlotHandler.SlotType.ShopInvSlot &&
                         _slotHandler.tooltipItemDefId > 0)
                {
                    // C++ birebir: UITransactionDlg::ReceiveIconDrop satır 1106-1124
                    // C++'da ShopInvSlot'tan sürüklenen ikon NPC area'sına bırakılınca
                    // area bazlı kontrol yapılır — spesifik slot'a çarpması gerekmez.
                    // Drop hedefi bulunamazsa (boş NPC alanı) → yine sell tetiklenir.
                    HandleSellDrop(_slotHandler);
                }
                else
                {
                    // C++ birebir: IconRestore() — hiçbir yere bırakılmadı
                }
            }
            
            _currentDrag = null;
        }

        /// <summary>
        /// EventData'dan drop hedefini bul — raycast ile.
        /// </summary>
        private KOItemSlotHandler FindDropTarget(PointerEventData eventData)
        {
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            
            foreach (var result in results)
            {
                var handler = result.gameObject.GetComponent<KOItemSlotHandler>();
                if (handler != null) return handler;
                
                // Parent'ta ara (icon image → slot parent)
                handler = result.gameObject.GetComponentInParent<KOItemSlotHandler>();
                if (handler != null) return handler;
            }
            return null;
        }

        /// <summary>
        /// C++ birebir: UITransactionDlg::ReceiveIconDrop (satır 873-1186)
        /// Kaynak ve hedef slot tipine göre alım/satım/taşıma işlemi.
        /// </summary>
        private void HandleIconDrop(KOItemSlotHandler source, KOItemSlotHandler dest)
        {
            
            // C++ birebir area type kontrolü:
            // TRADE_NPC → TRADE_MY = BUY
            // TRADE_MY → TRADE_NPC = SELL
            // TRADE_MY → TRADE_MY = MOVE (inv içi taşıma)
            
            if (source.slotType == KOItemSlotHandler.SlotType.ShopNpcSlot &&
                dest.slotType == KOItemSlotHandler.SlotType.ShopInvSlot)
            {
                // NPC → MY = BUY
                // C++ birebir: UITransactionDlg.cpp satır 938-1061
                HandleBuyDrop(source, dest.slotIndex);
            }
            else if (source.slotType == KOItemSlotHandler.SlotType.ShopInvSlot &&
                     dest.slotType == KOItemSlotHandler.SlotType.ShopNpcSlot)
            {
                // MY → NPC = SELL
                // C++ birebir: UITransactionDlg.cpp satır 1106-1124
                HandleSellDrop(source);
            }
            else if (source.slotType == KOItemSlotHandler.SlotType.BagSlot &&
                     dest.slotType == KOItemSlotHandler.SlotType.BagSlot)
            {
                // Bag → Bag = envanter içi taşıma
                HandleInvMoveDrop(source, dest);
            }
            else if (source.slotType == KOItemSlotHandler.SlotType.BagSlot &&
                     dest.slotType == KOItemSlotHandler.SlotType.EquipSlot)
            {
                // Bag → Equip = equip
                if (KOInventory.Instance != null)
                    KOInventory.Instance.EquipItem(source.slotIndex);
            }
            else if (source.slotType == KOItemSlotHandler.SlotType.EquipSlot &&
                     dest.slotType == KOItemSlotHandler.SlotType.BagSlot)
            {
                // Equip → Bag = unequip
                if (KOInventory.Instance != null)
                    KOInventory.Instance.UnequipItem(source.slotIndex);
            }
            else if (source.slotType == KOItemSlotHandler.SlotType.WarehouseInvSlot &&
                     (dest.slotType == KOItemSlotHandler.SlotType.WarehouseSlot))
            {
                // C++ birebir: UIWareHouseDlg.cpp ReceiveIconDrop satır 866-1010
                // UIWND_DISTRICT_TRADE_MY → UIWND_DISTRICT_TRADE_NPC = Deposit
                if (KOUIManager.Instance != null)
                    KOUIManager.Instance.HandleWareDeposit(source.slotIndex, dest.slotIndex);
            }
            else if (source.slotType == KOItemSlotHandler.SlotType.WarehouseSlot &&
                     (dest.slotType == KOItemSlotHandler.SlotType.WarehouseInvSlot))
            {
                // C++ birebir: UIWareHouseDlg.cpp ReceiveIconDrop satır 711-827
                // UIWND_DISTRICT_TRADE_NPC → UIWND_DISTRICT_TRADE_MY = Withdraw
                if (KOUIManager.Instance != null)
                    KOUIManager.Instance.HandleWareWithdraw(source.slotIndex, dest.slotIndex);
            }
            else if (source.slotType == KOItemSlotHandler.SlotType.WarehouseSlot &&
                     dest.slotType == KOItemSlotHandler.SlotType.WarehouseSlot)
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.HandleWareToWareMove(source.slotIndex, dest.slotIndex);
                }
            }
            else if (source.slotType == KOItemSlotHandler.SlotType.WarehouseInvSlot &&
                     dest.slotType == KOItemSlotHandler.SlotType.WarehouseInvSlot)
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.HandleWareInvToInvMove(source.slotIndex, dest.slotIndex);
                }
            }
            else if (source.slotType == KOItemSlotHandler.SlotType.StallViewSlot &&
                     dest.slotType == KOItemSlotHandler.SlotType.BagSlot)
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.HandleStallViewSlotDragDrop(source.slotIndex, dest.slotIndex);
                }
            }
            else
            {
            }
        }

        /// <summary>
        /// C++ birebir: NPC → MY area drag-drop = BUY
        /// UITransactionDlg.cpp satır 938-1061
        /// </summary>
        private void HandleBuyDrop(KOItemSlotHandler source, int destSlotIndex)
        {
            if (source.tooltipItemDefId <= 0) return;
            
            // Hedef slot'u kullan; eğer dolu ise ilk boş slot'u bul
            int destInvSlot = destSlotIndex;
            if (KOUIManager.Instance != null)
            {
                // Sayılı (stackable) itemlar için envanterde zaten var olan slotu bulalım
                if (source.shopByContable == 1 || source.shopByContable == 2)
                {
                    int existingSlot = KOUIManager.Instance.GetTradeInvDestinationIndex(source.tooltipItemDefId, source.shopByContable);
                    if (existingSlot >= 0)
                    {
                        destInvSlot = existingSlot;
                    }
                }
                else
                {
                    // Sayılı olmayan itemlar için hedef slotun m_pMyTradeInv'deki durumuna bakalım (m_pMyInvWnd yerine!)
                    var existing = KOUIManager.Instance.GetTradeInvItem(destSlotIndex);
                    if (existing != null)
                    {
                        destInvSlot = KOUIManager.Instance.GetTradeInvDestinationIndex(source.tooltipItemDefId, source.shopByContable);
                    }
                }
            }
            if (destInvSlot < 0)
            {
                Debug.LogWarning("[DRAG] Buy: envanter dolu!");
                return;
            }
            
            // C++ byContable kontrolü
            if (source.shopByContable == 1 || source.shopByContable == 2)
            {
                // Countable → miktar popup
                KOUIManager.Instance?.ShowShopCountPopup(
                    isbuying: true, itemDefId: source.tooltipItemDefId,
                    slotIndex: destInvSlot, npcId: source.shopNpcId,
                    tradeId: source.shopTradeId,
                    maxCount: source.shopByContable == 1 ? 9999 : 999);
            }
            else
            {
                // Normal → direkt buy — C++ birebir: UITransactionDlg.cpp:1061-1096
                var shopUI = ShopUI.Instance;
                if (shopUI != null && KOUIManager.Instance != null)
                {
                    // C++ birebir: s_sRecoveryJobInfo kaydet
                    KOUIManager.Instance.SetShopPendingInfo(true, source.tooltipItemDefId,
                        destInvSlot, source.shopNpcId, source.shopTradeId, 1);

                    // C++ birebir: cpp:1061 — SendToServerBuyMsg ÖNCE
                    shopUI.BuyItem(source.shopTradeId, (byte)destInvSlot,
                        (uint)source.tooltipItemDefId, 1);

                    // C++ birebir: cpp:1072-1096 — lokal ekleme SONRA
                    KOUIManager.Instance.ApplyPendingTradeToInventory();
                    KOUIManager.Instance.RefreshShopInventory();
                }
            }
        }

        /// <summary>
        /// C++ birebir: MY → NPC area drag-drop = SELL
        /// UITransactionDlg.cpp satır 1106-1124
        /// </summary>
        private void HandleSellDrop(KOItemSlotHandler source)
        {
            if (source.tooltipItemDefId <= 0) return;
            
            if (source.shopByContable == 1 || source.shopByContable == 2)
            {
                if (source.shopItemCount == 1)
                {
                    KOUIManager.Instance?.ShowShopSellConfirm(
                        itemDefId: source.tooltipItemDefId, slotIndex: source.slotIndex,
                        npcId: source.shopNpcId, tradeId: source.shopTradeId,
                        count: 1);
                }
                else
                {
                    // Countable → miktar popup
                    KOUIManager.Instance?.ShowShopCountPopup(
                        isbuying: false, itemDefId: source.tooltipItemDefId,
                        slotIndex: source.slotIndex, npcId: source.shopNpcId,
                        tradeId: source.shopTradeId,
                        maxCount: source.shopItemCount);
                }
            }
            else
            {
                // Normal → onay mesajı
                KOUIManager.Instance?.ShowShopSellConfirm(
                    itemDefId: source.tooltipItemDefId, slotIndex: source.slotIndex,
                    npcId: source.shopNpcId, tradeId: source.shopTradeId,
                    count: source.shopItemCount);
            }
        }

        /// <summary>
        /// C++ birebir: MY → MY area drag-drop = MOVE
        /// UITransactionDlg.cpp SendToServerMoveMsg
        /// </summary>
        private void HandleInvMoveDrop(KOItemSlotHandler source, KOItemSlotHandler dest)
        {
            if (KOInventory.Instance == null) return;
            
            var srcSlot = KOInventory.Instance.m_pMyInvWnd[source.slotIndex];
            if (srcSlot == null || srcSlot.IsEmpty) return;
            
            // C++ birebir: SendInvMsg(ITEM_MOVE_INV_TO_INV, itemID, srcPos, destPos)
            KOInventory.Instance.SendInvMsg(
                KOInventory.ITEM_MOVE_INV_TO_INV,
                srcSlot.itemId,
                source.slotIndex,
                dest.slotIndex);
            
        }

        // =============================================
        // Drag Icon — KOItemDragHandler ile aynı yöntem
        // Dedicated ScreenSpaceOverlay canvas + doğrudan piksel koordinatları
        // =============================================

        private void CreateDragIcon(PointerEventData eventData)
        {
            DestroyDragIcon();

            // Dedicated ScreenSpaceOverlay canvas — her zaman en üstte
            var canvasObj = new GameObject("ShopDragCanvas");
            var dragCanvas = canvasObj.AddComponent<Canvas>();
            dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            dragCanvas.sortingOrder = 30000;
            // CanvasScaler EKLENMİYOR — 1:1 pixel koordinatları

            _dragIcon = canvasObj;

            // İçine Image ekle
            var imgObj = new GameObject("DragImage");
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
            // Slot'taki item ikonu (child Icon nesnesinde olabilir)
            Image sourceImg = null;
            var iconTr = transform.Find("Icon");
            if (iconTr != null)
            {
                sourceImg = iconTr.GetComponent<Image>();
            }
            if (sourceImg == null)
            {
                sourceImg = GetComponent<Image>();
            }

            if (sourceImg != null && sourceImg.sprite != null)
            {
                img.sprite = sourceImg.sprite;
                img.color = Color.white; // C++ birebir: opak
            }
            else
            {
                img.color = new Color(0.5f, 0.8f, 1f, 0.6f);
            }
            img.raycastTarget = false;

            _dragIconImage = imgObj.transform;

            // Başlangıç pozisyonu
            UpdateDragPosition(eventData.position);
        }

        /// <summary>
        /// ScreenSpaceOverlay canvas'ta transform.position = ekran pikseli doğrudan çalışır.
        /// </summary>
        private static void UpdateDragPosition(Vector2 screenPos)
        {
            if (_dragIconImage == null) return;
            _dragIconImage.position = new Vector3(screenPos.x, screenPos.y, 0f);
        }

        private static void DestroyDragIcon()
        {
            if (_dragIcon != null)
            {
                Destroy(_dragIcon);
                _dragIcon = null;
                _dragIconImage = null;
            }
        }
    }
}
