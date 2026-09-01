using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    public class KOFastUpgradeManager : MonoBehaviour, IKOUpgradeManager
    {
        public static KOFastUpgradeManager Instance { get; private set; }

        // Constants
        public const int MAX_ITEM_INVENTORY = 44; // 28 bag + 14 equip + 2 spare
        public const int ANVIL_REQ_MAX = 9;

        // Opcodes & Result codes matching C++ client
        public const byte ITEM_UPGRADE_RESULT_FAILED = 0;
        public const byte ITEM_UPGRADE_RESULT_SUCCEEDED = 1;
        public const byte ITEM_UPGRADE_RESULT_TRADING = 2;
        public const byte ITEM_UPGRADE_RESULT_NO_COINS = 3;
        public const byte ITEM_UPGRADE_RESULT_NO_MATCH = 4;
        public const byte ITEM_UPGRADE_RESULT_ITEM_RENTED = 5;

        // ==========================================
        // 18-Slot State Fields
        // ==========================================
        public const int FAST_ANVIL_SLOT_MAX = 18;
        private readonly int[] _upgradeItemSlotsInvPos = new int[FAST_ANVIL_SLOT_MAX];

        // Keep these for IKOUpgradeManager compatibility
        private int _upgradeItemSlotInvPos => (_upgradeItemSlotsInvPos.Length > 0) ? _upgradeItemSlotsInvPos[0] : -1;
        private readonly int[] _requirementSlotInvPos = new int[ANVIL_REQ_MAX];

        private bool _upgradeInProgress = false;
        private bool _upgradeSucceeded = false;
        private int _npcId = 0;

        private int _pendingUpgradePacketsCount = 0;
        private bool _batchHasSuccess = false;



        private struct DeferredInventoryUpdate
        {
            public int originPos;
            public byte resultType;
            public int upgradedItemId;
        }
        private readonly List<DeferredInventoryUpdate> _deferredUpdates = new List<DeferredInventoryUpdate>();

        // ==========================================
        // Public properties (IKOUpgradeManager compatibility)
        // ==========================================
        public int UpgradeItemSlotInvPos => _upgradeItemSlotInvPos;
        public bool IsUpgradeInProgress
        {
            get => _upgradeInProgress;
            set => _upgradeInProgress = value;
        }
        public bool IsUpgradeSucceeded
        {
            get => _upgradeSucceeded;
            set => _upgradeSucceeded = value;
        }
        public bool IsPreviewActive { get; set; } = false;
        public int PreviewResultItemId { get; set; } = 0;
        public int NpcId => _npcId;

        // ==========================================
        // Lifecycle
        // ==========================================
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ResetAllSlots();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetNpcID(int npcId)
        {
            _npcId = npcId;
        }

        // ==========================================
        // 18-Slot Management Methods
        // ==========================================
        public int GetUpgradeItemSlotPos(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < FAST_ANVIL_SLOT_MAX)
                return _upgradeItemSlotsInvPos[slotIndex];
            return -1;
        }

        public bool IsItemPlaced(int invPos)
        {
            for (int i = 0; i < FAST_ANVIL_SLOT_MAX; i++)
            {
                if (_upgradeItemSlotsInvPos[i] == invPos)
                    return true;
            }
            return false;
        }

        public void RemoveUpgradeItem(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < FAST_ANVIL_SLOT_MAX)
            {
                _upgradeItemSlotsInvPos[slotIndex] = -1;
            }
        }

        public bool SetUpgradeItem(int invPos)
        {
            return SetUpgradeItem(invPos, -1);
        }

        public bool SetUpgradeItem(int invPos, int slotIndex = -1)
        {
            if (invPos < 0 || invPos >= MAX_ITEM_INVENTORY)
                return false;

            var item = FindInventoryItemBySlot(invPos);
            if (item == null || !IsAllowedUpgradeItem(item, ignoreSlotCheck: true))
                return false;

            // Envanter slotu panelde sadece bir kez bulunabilir, mükerrerliği engelle
            for (int i = 0; i < FAST_ANVIL_SLOT_MAX; i++)
            {
                if (_upgradeItemSlotsInvPos[i] == invPos)
                {
                    _upgradeItemSlotsInvPos[i] = -1;
                }
            }

            if (slotIndex >= 0 && slotIndex < FAST_ANVIL_SLOT_MAX)
            {
                _upgradeItemSlotsInvPos[slotIndex] = invPos;
                return true;
            }
            else
            {
                // İlk boş slotu bul
                for (int i = 0; i < FAST_ANVIL_SLOT_MAX; i++)
                {
                    if (_upgradeItemSlotsInvPos[i] == -1)
                    {
                        _upgradeItemSlotsInvPos[i] = invPos;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool SetRequirementItem(int invPos, int slotIndex = -1)
        {
            // Fast upgrade does not use requirement slots
            return false;
        }

        public void RemoveRequirementItem(int slotIndex)
        {
            // Do nothing
        }

        public int GetRequirementSlotPos(int slotIndex)
        {
            return -1;
        }

        // ==========================================
        // Cost & Class Calculations
        // ==========================================
        public int GetItemClass(InventoryItemData item)
        {
            if (item == null) return 1;

            KOTableReader.TableItemBasic basic = null;
            KOTableReader.TableItemExt ext = null;
            if (KOInventory.s_pTbl_Items_Basic != null)
            {
                basic = KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, item.ItemDefId);
                if (basic != null && KOInventory.s_pTbl_Items_Exts != null)
                {
                    ext = KOTableReader.FindItemExt(KOInventory.s_pTbl_Items_Exts, basic.byExtIndex, item.ItemDefId);
                }
            }

            if (basic == null || ext == null) return 1;

            int iItemGrade = System.Math.Min(3, basic.byGrade + ext.bySoulBind);
            int level = (int)(ext.dwID % 10);
            if (iItemGrade == 1 && level >= 7) // ITEM_GRADE_LOW_CLASS = 1
                iItemGrade = 2; // ITEM_GRADE_MIDDLE_CLASS = 2
            else if (iItemGrade == 2 && level >= 7)
                iItemGrade = 3; // ITEM_GRADE_HIGH_CLASS = 3

            return iItemGrade;
        }

        public int GetItemUpgradeCost(InventoryItemData item)
        {
            int itemClass = GetItemClass(item);
            if (itemClass == 1) // Low Class
                return 20000;
            if (itemClass == 2) // Middle Class
                return 200000;
            if (itemClass == 3) // High Class
                return 2700000;
            return 20000;
        }

        public int CalculateUpgradeCost()
        {
            int totalCost = 0;
            bool hasItems = false;
            for (int i = 0; i < FAST_ANVIL_SLOT_MAX; i++)
            {
                int invPos = _upgradeItemSlotsInvPos[i];
                if (invPos >= 0 && invPos < MAX_ITEM_INVENTORY)
                {
                    var item = FindInventoryItemBySlot(invPos);
                    if (item != null && item.ItemDefId != 0)
                    {
                        totalCost += GetItemUpgradeCost(item);
                        hasItems = true;
                    }
                }
            }
            return hasItems ? totalCost : 0;
        }

        public float CalculateUpgradeRate()
        {
            // Do not show rate for fast upgrade panel
            return -1f;
        }

        // ==========================================
        // Reset Methods
        // ==========================================
        private void ResetAllSlots()
        {
            _upgradeInProgress = false;
            _upgradeSucceeded = false;
            _npcId = 0;
            for (int i = 0; i < FAST_ANVIL_SLOT_MAX; i++)
                _upgradeItemSlotsInvPos[i] = -1;
            for (int i = 0; i < ANVIL_REQ_MAX; i++)
                _requirementSlotInvPos[i] = -1;
        }

        public void ResetUpgradeInventory()
        {
            for (int i = 0; i < FAST_ANVIL_SLOT_MAX; i++)
            {
                _upgradeItemSlotsInvPos[i] = -1;
            }
            for (int i = 0; i < ANVIL_REQ_MAX; i++)
            {
                _requirementSlotInvPos[i] = -1;
            }
            _upgradeSucceeded = false;
            _upgradeInProgress = false;
            IsPreviewActive = false;
            PreviewResultItemId = 0;
            _pendingUpgradePacketsCount = 0;
            _batchHasSuccess = false;
            _deferredUpdates.Clear();

        }

        // ==========================================
        // Item Allowed Checks
        // ==========================================
        public bool IsAllowedUpgradeItem(InventoryItemData item, bool ignoreSlotCheck = false)
        {
            if (item == null || item.IsEquipped || item.SlotType == 1)
                return false;

            // Yerel .tbl tablosundan sorgulayalım:
            KOTableReader.TableItemBasic basic = null;
            if (KOInventory.s_pTbl_Items_Basic != null)
            {
                basic = KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, item.ItemDefId);
            }

            // Sadece silah/zırh pozisyonları
            byte attachPoint = (basic != null) ? basic.byAttachPoint : item.AttachPoint;
            switch (attachPoint)
            {
                case 0:  // ITEM_POS_DUAL
                case 1:  // ITEM_POS_RIGHTHAND
                case 2:  // ITEM_POS_LEFTHAND
                case 3:  // ITEM_POS_TWOHANDRIGHT
                case 4:  // ITEM_POS_TWOHANDLEFT
                case 5:  // ITEM_POS_UPPER
                case 6:  // ITEM_POS_LOWER
                case 7:  // ITEM_POS_HEAD
                case 8:  // ITEM_POS_GLOVES
                case 9:  // ITEM_POS_SHOES
                    break;
                default:
                    return false;
            }

            // Sihirli veya nadir olmalı
            KOTableReader.TableItemExt ext = null;
            if (basic != null && KOInventory.s_pTbl_Items_Exts != null)
            {
                ext = KOTableReader.FindItemExt(KOInventory.s_pTbl_Items_Exts, basic.byExtIndex, item.ItemDefId);
            }

            byte magicOrRare = (ext != null) ? ext.byMagicOrRare : item.Type;
            if (magicOrRare != 4 && magicOrRare != 5) // ITEM_ATTRIB_UNIQUE (4) veya ITEM_ATTRIB_UPGRADE (5)
                return false;

            // Hızlı upgrade en fazla +5 seviyesine kadar yükseltmeye izin verir (+5 olan eşyalar daha fazla yükseltilemez)
            int currentLevel = item.ItemDefId % 10;
            if (basic != null && ext != null && ext.byMagicOrRare == 4) // Unique item
            {
                if (KOItemUpgradeManager.UNIQUE_BASE_TO_PLUS_ONE.ContainsKey(item.ItemDefId))
                {
                    currentLevel = 0;
                }
                else
                {
                    int level = item.ItemDefId % 10;
                    currentLevel = (level == 0) ? 10 : level;
                }
            }
            if (currentLevel >= 5)
                return false;

            return true;
        }

        // ==========================================
        // Network Upgrade Trigger
        // ==========================================
        public void SendToServerUpgradeMsg()
        {
            if (_upgradeInProgress) return;

            IsPreviewActive = false;
            PreviewResultItemId = 0;
            _pendingUpgradePacketsCount = 0;
            _batchHasSuccess = false;

            // Maliyet kontrolü
            int totalCost = CalculateUpgradeCost();
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm == null || gm.Gold < totalCost)
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.AddMsgOutput("Not enough coins for upgrade.",
                        KOUIManager.D3DColorToUnity(0xffff00ff));
                }
                return;
            }

            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;

            // Her bir slot için ayrı paket gönder
            for (int i = 0; i < FAST_ANVIL_SLOT_MAX; i++)
            {
                int invPos = _upgradeItemSlotsInvPos[i];
                if (invPos >= 0 && invPos < MAX_ITEM_INVENTORY)
                {
                    var upgradeItem = FindInventoryItemBySlot(invPos);
                    if (upgradeItem != null && upgradeItem.ItemDefId != 0)
                    {
                        _pendingUpgradePacketsCount++;

                        using var pkt = new KOPacketWriter(WizOpcode.WIZ_ITEM_UPGRADE);
                        pkt.WriteByte(12); // sub-opcode (ITEM_UPGRADE_FAST = 12)
                        pkt.WriteInt16((short)_npcId);

                        // Origin item id ve envanter pozisyonu
                        pkt.WriteInt32(upgradeItem.ItemDefId);
                        pkt.WriteByte((byte)invPos);

                        // 1. Scroll ID ve pozisyon (0 ve 255 olarak gönderiliyor, veritabanındaki nReqItem1=0 satırı ile eşleşmesi için)
                        pkt.WriteInt32(0);
                        pkt.WriteByte(255);

                        // Diğer 8 slot boş
                        for (int r = 1; r < 9; r++)
                        {
                            pkt.WriteInt32(0);
                            pkt.WriteByte(255);
                        }

                        netMgr.SendPacket(pkt);
                    }
                }
            }
            if (_pendingUpgradePacketsCount > 0)
            {
                _upgradeInProgress = true;
            }
        }

        public bool RequestUpgradePreview(out int previewResultId)
        {
            previewResultId = 0;
            return false;
        }

        // ==========================================
        // Incoming Packet Response Handlers
        // ==========================================
        public void HandleRawUpgradePacket(byte subOpcode, byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            r.ReadByte(); // subOpcode skip

            byte resultType = r.ReadByte();
            bool success = resultType == ITEM_UPGRADE_RESULT_SUCCEEDED;

            int upgradedItemId = 0;
            byte originPos = 0;
            int[] reqItemIds = new int[ANVIL_REQ_MAX];
            byte[] reqItemPoss = new byte[ANVIL_REQ_MAX];

            if (resultType == ITEM_UPGRADE_RESULT_SUCCEEDED || resultType == ITEM_UPGRADE_RESULT_FAILED)
            {
                if (r.Remaining >= 5)
                {
                    upgradedItemId = r.ReadInt32();
                    originPos = r.ReadByte();
                }
                for (int i = 0; i < ANVIL_REQ_MAX; i++)
                {
                    if (r.Remaining >= 5)
                    {
                        reqItemIds[i] = r.ReadInt32();
                        reqItemPoss[i] = r.ReadByte();
                    }
                    else
                    {
                        reqItemPoss[i] = 255;
                    }
                }
            }

            var inv = KOInventory.Instance;
            if (inv != null)
            {
                if (resultType == ITEM_UPGRADE_RESULT_SUCCEEDED || resultType == ITEM_UPGRADE_RESULT_FAILED)
                {
                    _deferredUpdates.Add(new DeferredInventoryUpdate
                    {
                        originPos = originPos,
                        resultType = resultType,
                        upgradedItemId = upgradedItemId
                    });
                    
                    if (resultType == ITEM_UPGRADE_RESULT_SUCCEEDED)
                    {
                        _batchHasSuccess = true;
                    }
                }
            }

            // Decrement pending packets count and trigger animation upon completion
            if (_pendingUpgradePacketsCount > 0)
            {
                _pendingUpgradePacketsCount--;
                if (_pendingUpgradePacketsCount == 0)
                {
                    MsgRecv_ItemUpgrade(0, _batchHasSuccess, _batchHasSuccess ? ITEM_UPGRADE_RESULT_SUCCEEDED : ITEM_UPGRADE_RESULT_FAILED, 0, 0, 0, 0);
                }
            }
            else
            {
                MsgRecv_ItemUpgrade(0, success, resultType, 0, 0, 0, 0);
            }

        }

        public void ApplyDeferredInventoryUpdates()
        {
            var inv = KOInventory.Instance;
            if (inv == null)
            {
                _deferredUpdates.Clear();
                return;
            }

            foreach (var update in _deferredUpdates)
            {
                int originPos = update.originPos;
                byte resultType = update.resultType;
                int upgradedItemId = update.upgradedItemId;

                if (originPos >= 0 && originPos < MAX_ITEM_INVENTORY)
                {
                    if (resultType == ITEM_UPGRADE_RESULT_FAILED)
                    {
                        // Burnt - remove from inventory
                        inv.m_pMyInvWnd[originPos] = null;

                        // Hızlı upgrade slotlarından da kaldır
                        for (int s = 0; s < FAST_ANVIL_SLOT_MAX; s++)
                        {
                            if (_upgradeItemSlotsInvPos[s] == originPos)
                            {
                                _upgradeItemSlotsInvPos[s] = -1;
                            }
                        }
                    }
                    else if (resultType == ITEM_UPGRADE_RESULT_SUCCEEDED)
                    {
                        // Succeeded - update ID to upgradedItemId
                        var originSlot = inv.m_pMyInvWnd[originPos];
                        if (originSlot != null)
                        {
                            originSlot.itemId = upgradedItemId;
                            originSlot.count = 1;
                            
                            KOTableReader.TableItemBasic basic = null;
                            if (KOInventory.s_pTbl_Items_Basic != null && KOInventory.s_pTbl_Items_Basic.TryGetValue((uint)upgradedItemId / 1000 * 1000, out basic))
                            {
                                originSlot.pItemBasic = basic;
                                originSlot.iconFN = basic.dwIDIcon.ToString();
                                if (originSlot.serverData != null)
                                {
                                    originSlot.serverData.ItemDefId = upgradedItemId;
                                    originSlot.serverData.IconId = originSlot.iconFN;
                                    originSlot.serverData.Durability = (short)basic.siMaxDurability;
                                }
                            }
                            originSlot.durability = (basic != null ? basic.siMaxDurability : 0);
                        }
                    }
                }
            }

            _deferredUpdates.Clear();

            // Refresh UI components
            KOUIManager.Instance?.RefreshInventoryUI();
            KOUIManager.Instance?.PopulateUpgradeInventory();
        }

        public void MsgRecv_ItemUpgrade(long instanceId, bool success, byte resultType,
            short newLevel, short newAtkMin, short newAtkMax, short newDef)
        {
            switch (resultType)
            {
                case ITEM_UPGRADE_RESULT_FAILED:
                    _upgradeSucceeded = false;
                    // _upgradeInProgress sıfırlamasını animasyon sonrasına (KOUIManager'a) bırakıyoruz
                    
                    if (KOUIManager.Instance != null)
                    {
                        KOUIManager.Instance.AddMsgOutput("Item upgrade failed.",
                            KOUIManager.D3DColorToUnity(0xffff00ff));
                        KOUIManager.Instance.PlayUpgradeAnimation(false);
                    }
                    break;

                case ITEM_UPGRADE_RESULT_SUCCEEDED:
                    _upgradeSucceeded = true;
                    // _upgradeInProgress sıfırlamasını animasyon sonrasına (KOUIManager'a) bırakıyoruz
                    
                    if (KOUIManager.Instance != null)
                    {
                        KOUIManager.Instance.AddMsgOutput("Item upgrade succeeded.",
                            KOUIManager.D3DColorToUnity(0xff00ff00));
                        KOUIManager.Instance.PlayUpgradeAnimation(true);
                    }
                    break;

                case ITEM_UPGRADE_RESULT_NO_COINS:
                    _upgradeInProgress = false;
                    if (KOUIManager.Instance != null)
                    {
                        KOUIManager.Instance.AddMsgOutput("Not enough coins.",
                            KOUIManager.D3DColorToUnity(0xffff00ff));
                    }
                    break;

                case ITEM_UPGRADE_RESULT_NO_MATCH:
                default:
                    _upgradeInProgress = false;
                    if (KOUIManager.Instance != null)
                    {
                        KOUIManager.Instance.AddMsgOutput("Items required for upgrade do not match.",
                            KOUIManager.D3DColorToUnity(0xffff00ff));
                    }
                    break;
            }
        }

        private InventoryItemData FindInventoryItemBySlot(int slotIndex)
        {
            if (KOInventory.Instance == null) return null;
            if (slotIndex < 0 || slotIndex >= MAX_ITEM_INVENTORY) return null;
            var slot = KOInventory.Instance.m_pMyInvWnd[slotIndex];
            if (slot == null || slot.itemId == 0) return null;

            var item = slot.serverData;
            if (item == null)
            {
                item = new InventoryItemData
                {
                    ItemDefId = slot.itemId,
                    SlotType = 0,
                    SlotIndex = (byte)slotIndex,
                    StackCount = (short)slot.count,
                    Durability = (short)slot.durability,
                    AttachPoint = (byte)slot.attachPoint,
                    Type = (byte)slot.itemClass
                };
            }
            return item;
        }
    }
}
