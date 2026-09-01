using UnityEngine;
using UnityEngine.EventSystems;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO mobil adaptasyon: Item slot etkileşim yöneticisi.
    /// 
    /// C++ hover → single-tap = tooltip
    /// C++ click → double-tap = equip/unequip/use
    /// Warehouse: double-tap = warehouse↔inventory arası taşı
    /// 
    /// Bu component her inventory/warehouse slot GameObject'una eklenir.
    /// </summary>
    public class KOItemSlotHandler : MonoBehaviour, IPointerClickHandler
    {
        private const float DOUBLE_TAP_THRESHOLD = 0.4f; // saniye

        public enum SlotType 
        { 
            EquipSlot, 
            BagSlot, 
            WarehouseSlot, 
            WarehouseInvSlot, 
            ShopNpcSlot, 
            ShopInvSlot, 
            MerchantSetupSlot, 
            MerchantSetupInvSlot, 
            StallViewSlot,
            InspectEquipSlot,
            InspectBagSlot
        }

        public SlotType slotType;
        public int slotIndex;
        public InventoryItemData itemData;

        private float _lastTapTime;
        private static KOItemTooltip _tooltip;

        private void Awake()
        {
            // NOT: KOItemDragDrop burada otomatik eklenMEZ.
            // Inventory slot'larda KOItemDragHandler zaten drag-drop sağlıyor.
            // KOItemDragDrop sadece shop NPC slot'larına KOUIManager tarafından eklenir.
        }

        /// <summary>
        /// Tooltip singleton'ını bul/oluştur.
        /// </summary>
        private static KOItemTooltip GetTooltip()
        {
            if (_tooltip != null) return _tooltip;

            if (KOUIManager.Instance == null || KOUIManager.Instance.Canvas == null)
            {
                Debug.LogError("[KOItemSlotHandler] KOUIManager veya Canvas referansi bulunamadi! Tooltip olusturulamiyor.");
                return null;
            }

            Canvas canvas = KOUIManager.Instance.Canvas;

            _tooltip = canvas.GetComponentInChildren<KOItemTooltip>(true);
            if (_tooltip == null)
            {
                var go = new GameObject("KOItemTooltip");
                go.transform.SetParent(canvas.transform, false);
                _tooltip = go.AddComponent<KOItemTooltip>();
            }
            return _tooltip;
        }

        /// <summary>
        /// Tooltip'i gizle (panel dışına dokunma vb.)
        /// </summary>
        public static void HideTooltip()
        {
            if (_tooltip != null) _tooltip.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (KOInventory.Instance != null && KOInventory.Instance.IsSorting) return;
            if (itemData == null && tooltipItemDefId <= 0) return;

            // Tasarım özgünleştirme: Çoklu satış modu aktifken Shop envanter slotuna tek tıklama ile seçim yap
            if (slotType == SlotType.ShopInvSlot && 
                KOUIManager.Instance != null && 
                KOUIManager.Instance.IsMultipleSellingActive)
            {
                _lastTapTime = 0; // reset
                HideTooltip();
                HandleShopSell();
                return;
            }

            float now = Time.unscaledTime;
            float elapsed = now - _lastTapTime;

            if (elapsed < DOUBLE_TAP_THRESHOLD)
            {
                // === DOUBLE TAP → equip/unequip/use/warehouse transfer ===
                _lastTapTime = 0; // reset
                HideTooltip();
                HandleDoubleTap();
            }
            else
            {
                // === SINGLE TAP → tooltip göster ===
                _lastTapTime = now;
                HandleSingleTap(eventData.position);
            }
        }

        /// <summary>
        /// Single tap → CUIImageTooltipDlg::DisplayTooltipsEnable birebir
        /// </summary>
        private void HandleSingleTap(Vector2 screenPos)
        {
            var tooltip = GetTooltip();
            if (tooltip == null) return;

            if (itemData != null)
            {
                tooltip.Show(itemData, screenPos, tooltipShowPrice, tooltipIsBuy);
            }
            else if (tooltipItemDefId > 0)
            {
                // Shop/Warehouse/Loot — itemData yok, itemDefId var
                tooltip.ShowByItemId(tooltipItemDefId, screenPos, tooltipShowPrice, tooltipIsBuy);
            }
        }

        /// <summary>
        /// Double tap → equip/unequip/use/warehouse transfer
        /// C++ birebir: UIInventory.cpp satır 861-982 InvOpsSomething
        /// + UIWareHouseDlg.cpp ReceiveIconDrop
        /// Public for KOItemDragDrop access.
        /// </summary>
        public void HandleDoubleTap()
        {
            switch (slotType)
            {
                case SlotType.InspectEquipSlot:
                case SlotType.InspectBagSlot:
                    // Read-only inspection slots do nothing on double tap
                    break;

                case SlotType.EquipSlot:
                    // Equipped item → Unequip (Arm → Inv)
                    if (KOInventory.Instance != null)
                    {
                        KOInventory.Instance.UnequipItem(slotIndex);
                    }
                    break;

                case SlotType.BagSlot:
                    if (KOUIManager.Instance != null && KOUIManager.Instance.IsGemChestExchangeOpen)
                    {
                        if (KOGemChestExchangeManager.Instance != null)
                        {
                            KOGemChestExchangeManager.Instance.SetExchangeItem(slotIndex);
                        }
                        break;
                    }

                    if (KOUIManager.Instance != null && (KOUIManager.Instance.IsUpgradeUIOpen || KOUIManager.Instance.IsFastUpgradeUIOpen || KOUIManager.Instance.IsRingUpgradeOpen))
                    {
                        break;
                    }

                    bool blockSlotAction = false;
                    if (EntropyOnline.Trade.KOMerchantManager.Instance != null)
                    {
                        if (EntropyOnline.Trade.KOMerchantManager.Instance.IsSellingSetup)
                        {
                            foreach (var setupItem in EntropyOnline.Trade.KOMerchantManager.Instance.SellingSetupItems)
                            {
                                if (setupItem != null && !setupItem.IsEmpty && setupItem.InvPos == slotIndex)
                                {
                                    blockSlotAction = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (blockSlotAction)
                    {
                        break;
                    }

                    // KO protokolü: byAttachPoint >= 15 → consumable (potion, scroll, quest item)
                    // NOT: Eski IsConsumable (Type==4) kontrolü KO itemleri için çalışmaz
                    // çünkü byClass değerleri (95,97,98,255) hiçbir zaman 4 değildir.
                    bool isConsumable = false;
                    if (KOInventory.Instance != null)
                        isConsumable = KOInventory.Instance.IsConsumableItem(slotIndex);

                    if (isConsumable)
                    {
                        // Consumable → Use
                        // KO protokolünde consumable kullanımı, eşyayı harcayan (ExhaustItem)
                        // yetenek/skill'in tetiklenmesiyle (WIZ_MAGIC_PROCESS) yapılır.
                        if (itemData != null)
                        {
                            var skill = KOImport.SkillTableParser.FindByExhaustItem((uint)itemData.ItemDefId);
                            if (skill != null)
                            {
                                var magicMgr = EntropyOnline.Combat.KOMagicSkillManager.Instance;
                                var gm = EntropyOnline.Core.GameManager.Instance;
                                if (magicMgr != null && gm != null)
                                {
                                    magicMgr.MsgSend_MagicProcess((int)gm.CharacterId, skill);
                                }
                            }
                            else
                            {
                                // Skill bulunamadıysa (örneğin görev item'ı vb.), eski yoldan dene
                                if (itemData.ItemDefId == 800085000)
                                {
                                    // Pazar SC kullanımı: WIZ_MARKET_BBS packet (BBS_OPEN) göndeririz.
                                    // Sunucu bunu doğrulayıp scroll'u silecek ve bize BBS arayüzünü açtıracak.
                                    var netMgr = KONetworkManager.Instance;
                                    if (netMgr != null && netMgr.IsConnected)
                                    {
                                        using var pkt = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS);
                                        pkt.WriteByte(TradeBBSSub.N3_SP_TYPE_BBS_OPEN);
                                        pkt.WriteByte(0); // bbsKind = 0
                                        netMgr.SendPacket(pkt);
                                    }
                                }
                                else if (InventoryUI.Instance != null && itemData.InstanceId > 0)
                                {
                                    InventoryUI.Instance.SendUseItem(itemData.InstanceId);
                                }
                                else
                                {
                                    KOUIManager.Instance?.ShowToast("Item use is not supported.");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Non-consumable → Equip (Inv → Arm)
                        if (KOInventory.Instance != null)
                        {
                            KOInventory.Instance.EquipItem(slotIndex);
                        }
                    }
                    break;

                case SlotType.WarehouseSlot:
                    if (KOUIManager.Instance != null)
                    {
                        int destInv = KOUIManager.Instance.GetWareInvDestinationIndex(tooltipItemDefId, shopByContable);
                        KOUIManager.Instance.HandleWareWithdraw(slotIndex, destInv);
                    }
                    break;

                case SlotType.WarehouseInvSlot:
                    if (KOUIManager.Instance != null)
                    {
                        KOUIManager.Instance.HandleWareDeposit(slotIndex, -1);
                    }
                    break;

                case SlotType.ShopNpcSlot:
                    // C++ birebir: UITransactionDlg.cpp ReceiveIconDrop satır 938-1061
                    // NPC item → Buy
                    HandleShopBuy();
                    break;

                case SlotType.ShopInvSlot:
                    // C++ birebir: UITransactionDlg.cpp ReceiveIconDrop satır 1106-1124
                    // Inv item → Sell
                    HandleShopSell();
                    break;

                case SlotType.MerchantSetupSlot:
                    EntropyOnline.Trade.KOMerchantManager.Instance?.SendMerchantItemCancel((byte)slotIndex);
                    break;

                case SlotType.MerchantSetupInvSlot:
                    if (KOUIManager.Instance != null)
                        KOUIManager.Instance.HandleMerchantSetupInvDoubleTap(slotIndex);
                    break;

                case SlotType.StallViewSlot:
                    if (KOUIManager.Instance != null)
                        KOUIManager.Instance.HandleStallViewSlotDoubleTap(slotIndex);
                    break;
            }
        }

        // ===== Shop Buy/Sell =====

        public int shopNpcId;       // NPC ID
        public int shopTradeId;     // tradeID
        public byte shopByContable; // C++ byContable: 0=onlyone, 1=countable, 2=countable_small
        public int shopItemCount = 1; // miktar

        /// <summary>
        /// C++ birebir: UITransactionDlg.cpp ReceiveIconDrop satır 938-1061
        /// NPC item'a çift tıklama = satın alma.
        /// Countable item → miktar popup, Normal item → direkt buy.
        /// C++ satır 951-1002: countable ise s_pCountableItemEdit->Open()
        /// C++ satır 1005-1061: normal ise para kontrolü + SendToServerBuyMsg
        /// </summary>
        private void HandleShopBuy()
        {
            if (tooltipItemDefId <= 0) return;

            // C++ birebir satır 1059: iDestInviOrder = ilk boş veya eşleşen inv slot
            int destInvSlot = -1;
            if (KOUIManager.Instance != null)
                destInvSlot = KOUIManager.Instance.GetTradeInvDestinationIndex(tooltipItemDefId, shopByContable);
            if (destInvSlot < 0)
            {
                Debug.LogWarning("[SLOT] Shop buy: envanter dolu!");
                return;
            }

            // C++ byContable kontrolü (satır 951-952)
            if (shopByContable == 1 || shopByContable == 2)
            {
                // Countable item → miktar popup
                // C++ birebir: s_pCountableItemEdit->Open(UIWND_TRANSACTION, UIWND_DISTRICT_TRADE_NPC, false)
                KOUIManager.Instance?.ShowShopCountPopup(
                    isbuying: true, itemDefId: tooltipItemDefId,
                    slotIndex: destInvSlot, npcId: shopNpcId, tradeId: shopTradeId,
                    maxCount: shopByContable == 1 ? 9999 : 999);
            }
            else
            {
                // Normal item → direkt satın al
                // C++ birebir: SendToServerBuyMsg(itemID, iDestInviOrder, count) — satır 1061
                var shopUI = ShopUI.Instance;
                if (shopUI != null && KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.SetShopPendingInfo(true, tooltipItemDefId, destInvSlot, shopNpcId, shopTradeId, 1);
                    shopUI.BuyItem(shopTradeId, (byte)destInvSlot, (uint)tooltipItemDefId, 1);
                    KOUIManager.Instance.ApplyPendingTradeToInventory();
                    KOUIManager.Instance.RefreshShopInventory();
                }
            }
        }

        /// <summary>
        /// C++ birebir: UITransactionDlg.cpp ReceiveIconDrop satır 1106-1124
        /// Inv item'a çift tıklama (shop açıkken) = satma.
        /// Countable item → miktar popup
        /// Normal item → onay mesajı (IDS_TRANSACTION_OK_CANCEL_MESSAGE)
        ///   C++ satır 1119-1123: m_pUIMsgBoxOkCancel->ShowWindow → "Bu eşyayı satmak..."
        /// </summary>
        private void HandleShopSell()
        {
            if (tooltipItemDefId <= 0) return;

            // Tasarım özgünleştirme: Çoklu satış aktifse listeye ekle/çıkar
            if (KOUIManager.Instance != null && KOUIManager.Instance.IsMultipleSellingActive)
            {
                KOUIManager.Instance.ToggleMultipleSellSelection(slotIndex, tooltipItemDefId, shopItemCount);
                return;
            }

            if (shopByContable == 1 || shopByContable == 2)
            {
                if (shopItemCount == 1)
                {
                    KOUIManager.Instance?.ShowShopSellConfirm(
                        itemDefId: tooltipItemDefId, slotIndex: slotIndex,
                        npcId: shopNpcId, tradeId: shopTradeId, count: 1);
                }
                else
                {
                    // Countable item → miktar popup
                    KOUIManager.Instance?.ShowShopCountPopup(
                        isbuying: false, itemDefId: tooltipItemDefId,
                        slotIndex: slotIndex, npcId: shopNpcId, tradeId: shopTradeId,
                        maxCount: shopItemCount);
                }
            }
            else
            {
                // C++ birebir satır 1119-1123: onay mesajı
                // m_pUIMsgBoxOkCancel->ShowWindow(CHILD_UI_MSGBOX_OKCANCEL, this)
                // m_pUIMsgBoxOkCancel->SetText("Bu eşyayı satmak istediğinizden emin misiniz?")
                KOUIManager.Instance?.ShowShopSellConfirm(
                    itemDefId: tooltipItemDefId, slotIndex: slotIndex,
                    npcId: shopNpcId, tradeId: shopTradeId, count: shopItemCount);
            }
        }

        // ===== Warehouse taşıma paketleri =====

        /// <summary>
        /// C++ birebir: UIWareHouseDlg::SendToServerFromWareMsg (satır 1095-1108)
        /// Wire: WIZ_WAREHOUSE [N3_SP_WARE_GET_OUT=0x03] [itemId:uint32] [page:byte] [startpos:byte] [pos:byte] [count:int32]
        /// </summary>
        private void SendWarehouseToInv(int wareSlot, int itemId, int count)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null || !netMgr.IsConnected) return;

            uint serverItemId = EntropyOnline.Import.KOItemMapping.GetServerItemId((uint)itemId);

            const byte N3_SP_WARE_GET_OUT = 0x03;
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_WAREHOUSE);
            pkt.WriteByte(N3_SP_WARE_GET_OUT);
            pkt.WriteUInt32(serverItemId);
            pkt.WriteByte(0); // page (current page, 0 = first)
            pkt.WriteByte((byte)wareSlot); // startpos = warehouse slot
            pkt.WriteByte(0xFF); // pos = 0xFF → sunucu ilk boş inv slotuna koyar
            pkt.WriteInt32(count > 0 ? count : 1);

            netMgr.SendPacket(pkt);
        }

        /// <summary>
        /// C++ birebir: UIWareHouseDlg::SendToServerToWareMsg (satır 1080-1093)
        /// Wire: WIZ_WAREHOUSE [N3_SP_WARE_GET_IN=0x02] [itemId:uint32] [page:byte] [startpos:byte] [pos:byte] [count:int32]
        /// </summary>
        private void SendInvToWarehouse(int invSlot, int itemId, int count)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null || !netMgr.IsConnected) return;

            uint serverItemId = EntropyOnline.Import.KOItemMapping.GetServerItemId((uint)itemId);

            const byte N3_SP_WARE_GET_IN = 0x02;
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_WAREHOUSE);
            pkt.WriteByte(N3_SP_WARE_GET_IN);
            pkt.WriteUInt32(serverItemId);
            pkt.WriteByte(0); // page (current page, 0 = first)
            pkt.WriteByte((byte)invSlot); // startpos = inventory slot
            pkt.WriteByte(0xFF); // pos = 0xFF → sunucu ilk boş ware slotuna koyar
            pkt.WriteInt32(count > 0 ? count : 1);

            netMgr.SendPacket(pkt);
        }

        // ===== Shop/Warehouse/Loot tooltip desteği =====

        /// <summary>
        /// itemDefId bazlı tooltip-only slot handler.
        /// Shop/Warehouse/Loot panellerindeki item'lar için.
        /// </summary>
        public int tooltipItemDefId;
        public bool tooltipShowPrice;
        public bool tooltipIsBuy = true;
        public int warehouseItemCount = 1; // Warehouse taşıma için item miktarı

        /// <summary>
        /// itemData null ise (shop/warehouse/loot) itemDefId tabanlı tooltip gösterir.
        /// </summary>
        private void HandleSingleTapByItemId(Vector2 screenPos)
        {
            var tooltip = GetTooltip();
            if (tooltip == null) return;
            tooltip.ShowByItemId(tooltipItemDefId, screenPos, tooltipShowPrice, tooltipIsBuy);
        }
    }
}

