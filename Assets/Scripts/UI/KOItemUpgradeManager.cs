using System;
using System.Collections.Generic;
using UnityEngine;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO v1.298: UIItemUpgrade.cpp birebir veri/mantık katmanı portu.
    /// 
    /// C++ karşılığı: UIItemUpgrade.h/cpp — 1352 satır
    /// Bu sınıf UI rendering YAPMAZ — sadece state ve logic yönetir.
    /// KOUIManager ve GameHUD bu sınıfı kullanarak UI'ı günceller.
    /// 
    /// Sabitler:
    ///   ANVIL_REQ_MAX = 9            (globals.h:549)
    ///   MAX_ITEM_INVENTORY = 28      (globals.h — HAVE_MAX)
    ///   ITEM_ATTRIB_UNIQUE = 4       (GameDef.h:389)
    ///   ITEM_ATTRIB_UPGRADE = 5      (GameDef.h:390)
    ///   ITEM_EFFECT2_ITEM_UPGRADE_REQ = 255 (GameDef.h:398)
    ///   ITEM_CLASS_CONSUMABLE = 255  (globals.h:180)
    /// </summary>
    public class KOItemUpgradeManager : MonoBehaviour, IKOUpgradeManager
    {
        public static KOItemUpgradeManager Instance { get; private set; }

        // ==========================================
        // C++ sabitleri — birebir
        // ==========================================

        /// <summary>Open-KO: ANVIL_REQ_MAX = 9 (globals.h:549)</summary>
        public const int ANVIL_REQ_MAX = 9;

        /// <summary>Open-KO: MAX_ITEM_INVENTORY = HAVE_MAX = 28</summary>
        public const int MAX_ITEM_INVENTORY = 28;

        // Open-KO: e_ItemAttrib (GameDef.h:383-392)
        private const int ITEM_ATTRIB_UNIQUE = 4;
        private const int ITEM_ATTRIB_UPGRADE = 5;

        public static readonly Dictionary<int, int> UNIQUE_BASE_TO_PLUS_ONE = new Dictionary<int, int>
        {
            { 119101101, 119101201 }, // Dark Vane
            { 120510255, 120510281 }, // Tooth
            { 120550256, 120550291 }, // Sword of Beast
            { 125350258, 125350271 }, // Sword of the Dead
            { 129101102, 129101211 }, // Hanguk Sword
            { 130450256, 130450271 }, // Gigantic Axe (1H)
            { 130450259, 130450271 }, // Gigantic Axe (1H)
            { 130650255, 130650261 }, // Javana Axe (1H)
            { 130650258, 130650261 }, // Javana Axe (1H)
            { 135750255, 135750261 }, // Javana Axe (2H)
            { 135750258, 135750261 }, // Javana Axe (2H)
            { 135750256, 135750271 }, // Gigantic Axe (2H)
            { 135750259, 135750271 }, // Gigantic Axe (2H)
            { 149101103, 149101221 }, // Hell Breaker
            { 155550255, 155550261 }, // Scorpion Side
            { 160450255, 160450271 }, // Chitin Bow
            { 160450256, 160450281 }, // Scorpion Bow
            { 169010257, 169010261 }, // Centaur Bow
            { 169101104, 169101231 }, // Enion Bow
            { 179101110, 179101291 }, // Defender of the Lord
            { 180110251, 180110271 }, // Lobo Staff
            { 180110252, 180110281 }, // Lupus staff
            { 180110253, 180110291 }, // Lycaon staff
            { 189101106, 189101301 }, // Wreath of Erenion
            { 189201107, 189201261 }, // Glacier Erenion
            { 189301108, 189301271 }, // Lightning Erenion
            { 189401109, 189401281 }, // Ron's Staff
            { 190250251, 190250271 }, // Lobo hammer
            { 190250252, 190250281 }, // Lupus hammer
            { 190250253, 190250291 }, // Lycaon hammer
            { 190250255, 190250301 }, // Skull hammer
            { 190610256, 190610311 }, // Dragon tooth hammer
            { 199101105, 199101241 }  // Smite Hammer
        };

        // Open-KO: e_ItemPosition (GameDef.h:973-994)
        private const int ITEM_POS_DUAL = 0;
        private const int ITEM_POS_RIGHTHAND = 1;
        private const int ITEM_POS_LEFTHAND = 2;
        private const int ITEM_POS_TWOHANDRIGHT = 3;
        private const int ITEM_POS_TWOHANDLEFT = 4;
        private const int ITEM_POS_UPPER = 5;
        private const int ITEM_POS_LOWER = 6;
        private const int ITEM_POS_HEAD = 7;
        private const int ITEM_POS_GLOVES = 8;
        private const int ITEM_POS_SHOES = 9;

        // Open-KO: e_ItemUpgradeResult (packets.h:632-641)
        public const byte ITEM_UPGRADE_RESULT_FAILED = 0;
        public const byte ITEM_UPGRADE_RESULT_SUCCEEDED = 1;
        public const byte ITEM_UPGRADE_RESULT_TRADING = 2;
        public const byte ITEM_UPGRADE_RESULT_NEED_COINS = 3;
        public const byte ITEM_UPGRADE_RESULT_NO_MATCH = 4;
        public const byte ITEM_UPGRADE_RESULT_ITEM_RENTED = 5;

        // ==========================================
        // C++ state fields — UIItemUpgrade.h birebir
        // ==========================================

        /// <summary>C++ m_iUpgradeItemSlotInvPos (UIItemUpgrade.h:62)</summary>
        private int _upgradeItemSlotInvPos = -1;

        /// <summary>C++ m_iRequirementSlotInvPos[ANVIL_REQ_MAX] (UIItemUpgrade.h:61)</summary>
        private readonly int[] _requirementSlotInvPos = new int[ANVIL_REQ_MAX];

        /// <summary>C++ m_bUpgradeInProgress (UIItemUpgrade.h:33)</summary>
        private bool _upgradeInProgress = false;

        /// <summary>C++ m_bUpgradeSucceeded (UIItemUpgrade.h:32)</summary>
        private bool _upgradeSucceeded = false;

        /// <summary>C++ m_iNpcID (UIItemUpgrade.h:34)</summary>
        private int _npcId = 0;

        private struct ReqItem
        {
            public int ID;
            public byte Pos;
        }

        // ==========================================
        // Public property'ler (UI okuma için)
        // ==========================================

        public int UpgradeItemSlotInvPos => _upgradeItemSlotInvPos;
        public bool IsUpgradeInProgress
        {
            get => _upgradeInProgress;
            set => _upgradeInProgress = value;
        }
        public bool IsUpgradeSucceeded => _upgradeSucceeded;
        public bool IsPreviewActive { get; set; } = false;
        public int PreviewResultItemId { get; set; } = 0;
        public int NpcId => _npcId;

        /// <summary>Requirement slot pozisyonlarını kopyalar (UI render için).</summary>
        public void GetRequirementSlotPositions(int[] dst)
        {
            if (dst == null || dst.Length < ANVIL_REQ_MAX) return;
            Array.Copy(_requirementSlotInvPos, dst, ANVIL_REQ_MAX);
        }

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

        /// <summary>
        /// C++ UIItemUpgrade.cpp:285-288 — SetNpcID(iNpcID)
        /// NPC Event'ten upgrade paneli açılırken çağrılır.
        /// </summary>
        public void SetNpcID(int npcId)
        {
            _npcId = npcId;
        }

        // ==========================================
        // IsAllowedUpgradeItem — cpp:757-784 birebir
        // ==========================================

        /// <summary>
        /// Open-KO birebir: UIItemUpgrade.cpp:757-784 — IsAllowedUpgradeItem()
        /// 
        /// Bir eşyanın upgrade slot'una konulabilir olup olmadığını kontrol eder.
        /// Kurallar:
        ///   1. Upgrade slot zaten dolu ise false (cpp:759-760)
        ///   2. AttachPoint silah/zırh pozisyonlarından biri olmalı (cpp:765-781)
        ///   3. byMagicOrRare = ITEM_ATTRIB_UNIQUE(4) veya ITEM_ATTRIB_UPGRADE(5) olmalı (cpp:783-784)
        /// </summary>
        public bool IsAllowedUpgradeItem(InventoryItemData item, bool ignoreSlotCheck = false)
        {
            // C++ satır 759-760: m_iUpgradeItemSlotInvPos != -1 → zaten dolu
            if (!ignoreSlotCheck && _upgradeItemSlotInvPos != -1)
                return false;

            if (item == null || item.IsEquipped || item.SlotType == 1)
                return false;

            // Open-KO C++ referansı ile birebir eşleme için yerel .tbl tablosundan sorgulayalım:
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

            // C++ satır 765-781: byAttachPoint switch — sadece silah/zırh pozisyonları
            byte attachPoint = (basic != null) ? basic.byAttachPoint : item.AttachPoint;
            switch (attachPoint)
            {
                case ITEM_POS_DUAL:
                case ITEM_POS_RIGHTHAND:
                case ITEM_POS_LEFTHAND:
                case ITEM_POS_TWOHANDRIGHT:
                case ITEM_POS_TWOHANDLEFT:
                case ITEM_POS_SHOES:
                case ITEM_POS_GLOVES:
                case ITEM_POS_HEAD:
                case ITEM_POS_LOWER:
                case ITEM_POS_UPPER:
                    break;

                default:
                    return false;
            }

            // C++ satır 783-784: e_ItemAttrib check
            if (ext != null)
            {
                int magicOrRare = ext.byMagicOrRare;
                return magicOrRare == ITEM_ATTRIB_UNIQUE || magicOrRare == ITEM_ATTRIB_UPGRADE;
            }

            // Fallback (eğer extension tablosu yüklenmediyse):
            return true;
        }

        // ==========================================
        // IsValidRequirementItem — cpp:1129-1161 birebir
        // ==========================================

        /// <summary>
        /// Open-KO birebir: UIItemUpgrade.cpp:1129-1161 — IsValidRequirementItem()
        /// 
        /// Bir eşyanın malzeme slotuna konulabilir olup olmadığını kontrol eder.
        /// Kurallar:
        ///   1. Upgrade sürüyorsa false (cpp:1131-1132)
        ///   2. dwEffectID2 == ITEM_EFFECT2_ITEM_UPGRADE_REQ(255) olmalı (cpp:1134-1135)
        ///   3. Aynı tipten (consumable/scroll) birden fazla yerleştirilemez (cpp:1152-1159)
        /// </summary>
        public bool IsValidRequirementItem(InventoryItemData item)
        {

            
            // C++ satır 1131-1132
            if (_upgradeInProgress)
            {
                return false;
            }

            if (item == null)
                return false;

            // Open-KO C++ referansı ile birebir eşleme için yerel .tbl tablosundan sorgulayalım:
            KOTableReader.TableItemBasic basic = null;
            if (KOInventory.s_pTbl_Items_Basic != null)
            {
                basic = KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, item.ItemDefId);
            }

            // C++ satır 1134-1135: dwEffectID2 == ITEM_EFFECT2_ITEM_UPGRADE_REQ (255)
            if (basic != null)
            {
                if (basic.dwEffectID2 != 255) // ITEM_EFFECT2_ITEM_UPGRADE_REQ
                {
                    return false;
                }
            }
            else
            {
                // Fallback (tablo yüklenmemişse):
                if (item.AttachPoint < 15)
                {
                    return false;
                }
            }

            // C++ satır 1137-1159: Aynı class'tan birden fazla yerleştirme kontrolü
            // Consumable ise bHasConsumable, scroll ise bHasScroll
            bool hasConsumable = false;
            bool hasScroll = false;

            for (int i = 0; i < ANVIL_REQ_MAX; i++)
            {
                int order = _requirementSlotInvPos[i];
                if (order < 0)
                    continue;

                var existingItem = FindInventoryItemBySlot(order);
                if (existingItem == null) continue;

                // C++ satır 1146-1149: ITEM_CLASS_CONSUMABLE = 255
                bool isExistingConsumable = false;
                KOTableReader.TableItemBasic existingBasic = null;
                if (KOInventory.s_pTbl_Items_Basic != null)
                {
                    existingBasic = KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, existingItem.ItemDefId);
                }

                if (existingBasic != null)
                {
                    isExistingConsumable = (existingBasic.byClass == 255);
                }
                else
                {
                    isExistingConsumable = (existingItem.AttachPoint >= 15);
                }

                if (isExistingConsumable)
                    hasConsumable = true;
                else
                    hasScroll = true;
            }

            // C++ satır 1152-1153: Zaten consumable varsa ikinci consumable yasak
            bool isCurrentConsumable = false;
            if (basic != null)
            {
                isCurrentConsumable = (basic.byClass == 255);
            }
            else
            {
                isCurrentConsumable = (item.AttachPoint >= 15);
            }

            if (hasConsumable && isCurrentConsumable)
            {
                return false;
            }

            // C++ satır 1155-1156: Zaten scroll varsa consumable olmayan yasak
            if (hasScroll && !isCurrentConsumable)
            {
                return false;
            }

            // C++ satır 1158-1159: İkisi de zaten var → yasak
            if (hasConsumable && hasScroll)
            {
                return false;
            }

            return true;
        }

        // ==========================================
        // SetUpgradeItem — cpp:349-361 (ReceiveIconDrop upgrade slot)
        // ==========================================

        /// <summary>
        /// Open-KO birebir: UIItemUpgrade.cpp:349-361 — upgrade slot'a eşya yerleştir.
        /// Right-click: cpp:1255-1264 — HandleInventoryIconRightClick upgrade kısmı.
        /// </summary>
        public bool SetUpgradeItem(int invPos)
        {
            if (invPos < 0 || invPos >= MAX_ITEM_INVENTORY)
                return false;

            var item = FindInventoryItemBySlot(invPos);
            if (item == null || !IsAllowedUpgradeItem(item))
                return false;

            // C++ satır 359: m_iUpgradeItemSlotInvPos = m_iItemBeingDraggedSourcePos
            _upgradeItemSlotInvPos = invPos;
            return true;
        }

        // ==========================================
        // SetRequirementItem — cpp:1300-1314 (SetRequirementItemSlot)
        // ==========================================

        /// <summary>
        /// Open-KO birebir: UIItemUpgrade.cpp:1300-1314 — SetRequirementItemSlot()
        /// Right-click: cpp:1267-1288 — HandleInventoryIconRightClick requirement kısmı.
        /// </summary>
        public bool SetRequirementItem(int invPos, int slotIndex = -1)
        {
            if (invPos < 0 || invPos >= MAX_ITEM_INVENTORY)
            {
                return false;
            }

            var item = FindInventoryItemBySlot(invPos);
            if (item == null)
            {
                return false;
            }
            
            if (!IsValidRequirementItem(item))
            {
                return false;
            }

            // C++ satır 1271-1280: Boş slot bul
            if (slotIndex < 0)
            {
                for (int i = 0; i < ANVIL_REQ_MAX; i++)
                {
                    if (_requirementSlotInvPos[i] == -1)
                    {
                        slotIndex = i;
                        break;
                    }
                }
            }

            if (slotIndex < 0 || slotIndex >= ANVIL_REQ_MAX)
                return false;

            if (_requirementSlotInvPos[slotIndex] != -1)
                return false;

            // C++ satır 1313-1314
            _requirementSlotInvPos[slotIndex] = invPos;
            return true;
        }

        // ==========================================
        // ResetUpgradeInventory — cpp:710-754 birebir
        // ==========================================

        /// <summary>
        /// Open-KO birebir: UIItemUpgrade.cpp:710-754 — ResetUpgradeInventory()
        /// Upgrade ve requirement slotlarını temizler, tüm state'i sıfırlar.
        /// </summary>
        public void ResetUpgradeInventory()
        {
            // C++ satır 712-723: Upgrade slot'u sıfırla
            _upgradeItemSlotInvPos = -1;

            // C++ satır 725-750: Requirement slotlarını sıfırla
            for (int i = 0; i < ANVIL_REQ_MAX; i++)
            {
                _requirementSlotInvPos[i] = -1;
            }

            // C++ satır 752-753
            _upgradeSucceeded = false;
            _upgradeInProgress = false;

            IsPreviewActive = false;
            PreviewResultItemId = 0;
        }

        // ==========================================
        // SendToServerUpgradeMsg — cpp:787-833 birebir
        // ==========================================

        /// <summary>
        /// Open-KO birebir: UIItemUpgrade.cpp:787-833 — SendToServerUpgradeMsg()
        /// 
        /// Sunucuya upgrade isteği gönderir.
        /// Mobil wire format (UpgradeProcessor ile uyumlu):
        ///   [originInstanceId: int64] [originSlotIndex: byte]
        ///   [reqItemId[0..7]: int32] [reqItemSlotIndex[0..7]: byte] × 8
        /// 
        /// C++ wire format (referans — bizde kullanılmıyor):
        ///   [WIZ_ITEM_UPGRADE: byte] [ITEM_UPGRADE_PROCESS: byte] [npcId: int16]
        ///   [originItemId: int32] [originPos: byte]
        ///   [reqItemId[0..8]: int32] [reqItemPos[0..8]: byte] × 9
        /// </summary>
        public void SendToServerUpgradeMsg()
        {
            if (_upgradeInProgress) return;

            // Deactivate preview mode when a real upgrade starts
            IsPreviewActive = false;
            PreviewResultItemId = 0;

            // C++ satır 789-791: Upgrade slot doğrulama
            if (_upgradeItemSlotInvPos < 0 || _upgradeItemSlotInvPos >= MAX_ITEM_INVENTORY)
                return;

            var upgradeItem = FindInventoryItemBySlot(_upgradeItemSlotInvPos);
            if (upgradeItem == null)
                return;

            // C++ satır 803: m_bUpgradeInProgress = true
            _upgradeInProgress = true;

            // Open-KO birebir: WIZ_ITEM_UPGRADE
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_ITEM_UPGRADE);

            // C++: sub-opcode byte (ITEM_UPGRADE_PROCESS = 2)
            pkt.WriteByte(2); 

            // C++: npcId (short)
            pkt.WriteInt16((short)_npcId);

            // C++: originItemId (int32) and originPos (byte)
            pkt.WriteInt32(upgradeItem.ItemDefId);
            pkt.WriteByte((byte)_upgradeItemSlotInvPos);

            // C++ satır 814-830: Requirement items
            var reqList = new System.Collections.Generic.List<ReqItem>();

            for (int i = 0; i < ANVIL_REQ_MAX; i++)
            {
                int order = _requirementSlotInvPos[i];
                if (order >= 0 && order < MAX_ITEM_INVENTORY)
                {
                    var reqItem = FindInventoryItemBySlot(order);
                    if (reqItem != null)
                    {
                        reqList.Add(new ReqItem { ID = reqItem.ItemDefId, Pos = (byte)order });
                    }
                }
            }

            // Fill empty slots with ID = 0, Pos = 255
            while (reqList.Count < ANVIL_REQ_MAX)
            {
                reqList.Add(new ReqItem { ID = 0, Pos = 255 });
            }

            // C++ satır 823-824: Sort by ID descending
            reqList.Sort((a, b) => b.ID.CompareTo(a.ID));

            // Write 9 pairs of reqItemId (int32) + reqItemPos (byte)
            for (int i = 0; i < ANVIL_REQ_MAX; i++)
            {
                pkt.WriteInt32(reqList[i].ID);
                pkt.WriteByte(reqList[i].Pos);
            }

            KONetworkManager.Instance?.SendPacket(pkt);
        }

        // ==========================================
        // HandleRawUpgradePacket — UpgradeUI.cs'den çağrılır
        // ==========================================

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:8009-8041 — MsgRecv_ItemUpgrade raw dispatch
        /// 
        /// UpgradeUI tarafından çağrılır — ham paket verisinden upgrade sonucunu parse eder
        /// ve MsgRecv_ItemUpgrade'e yönlendirir.
        /// 
        /// Wire: [subOpcode:byte (zaten okunmuş)][result:byte][itemId:int32][pos:byte]
        ///       [reqItemId[0..8]:int32][reqItemPos[0..8]:byte] × 9
        /// </summary>
        public void HandleRawUpgradePacket(byte subOpcode, byte[] rawData)
        {
            // subOpcode zaten UpgradeUI tarafından okundu — rawData'dan tekrar parse et
            var r = new KOPacketReader(rawData);
            r.ReadByte(); // subOpcode (zaten biliniyor — skip)

            byte resultType = r.ReadByte();
            bool success = resultType == ITEM_UPGRADE_RESULT_SUCCEEDED;

            int upgradedItemId = 0;
            byte originPos = 0;
            int[] reqItemIds = new int[ANVIL_REQ_MAX];
            byte[] reqItemPoss = new byte[ANVIL_REQ_MAX];

            // Success or burn failure contains the full response payload
            if (resultType == ITEM_UPGRADE_RESULT_SUCCEEDED || resultType == ITEM_UPGRADE_RESULT_FAILED)
            {
                if (r.Remaining >= 5) // Item ID (4) + Pos (1)
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

            // Local inventory slot updates matching the C++ client behavior
            var inv = KOInventory.Instance;
            if (inv != null)
            {
                if (resultType == ITEM_UPGRADE_RESULT_SUCCEEDED || resultType == ITEM_UPGRADE_RESULT_FAILED)
                {
                    // 1. Consume/decrement requirement items in base inventory
                    for (int i = 0; i < ANVIL_REQ_MAX; i++)
                    {
                        byte reqPos = reqItemPoss[i];
                        if (reqPos >= 0 && reqPos < MAX_ITEM_INVENTORY)
                        {
                            var reqSlot = inv.m_pMyInvWnd[reqPos];
                            if (reqSlot != null && reqSlot.itemId == reqItemIds[i])
                            {
                                reqSlot.count--;
                                if (reqSlot.count <= 0)
                                {
                                    inv.m_pMyInvWnd[reqPos] = null;
                                }
                            }
                        }
                    }

                    // 2. Update origin upgrade item slot
                    if (originPos >= 0 && originPos < MAX_ITEM_INVENTORY)
                    {
                        if (resultType == ITEM_UPGRADE_RESULT_FAILED)
                        {
                            // Burnt - remove from inventory
                            inv.m_pMyInvWnd[originPos] = null;
                        }
                        else if (resultType == ITEM_UPGRADE_RESULT_SUCCEEDED)
                        {
                            // Succeeded - update ID to upgradedItemId
                            var originSlot = inv.m_pMyInvWnd[originPos];
                            if (originSlot != null)
                            {
                                originSlot.itemId = upgradedItemId;
                                originSlot.count = 1;
                                
                                // Fetch and update item definition properties
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

                // Force refresh UI so inventory bag slots update visual state
                KOUIManager.Instance?.RefreshInventoryUI();
                KOUIManager.Instance?.PopulateUpgradeInventory();
            }

            // Dispatch to standard MsgRecv_ItemUpgrade
            MsgRecv_ItemUpgrade(0, success, resultType, 0, 0, 0, 0);
        }

        // ==========================================
        // MsgRecv_ItemUpgrade — cpp:835-1041 birebir
        // ==========================================

        /// <summary>
        /// Open-KO birebir: UIItemUpgrade.cpp:835-1041 — MsgRecv_ItemUpgrade()
        /// 
        /// Sunucudan gelen upgrade sonucunu işler.
        /// 
        /// Mobil wire format (UpgradeProcessor.SendUpgradeResult ile uyumlu):
        ///   [instanceId: int64] [success: bool] [resultType: byte]
        ///   [newLevel: int16] [newAtkMin: int16] [newAtkMax: int16] [newDef: int16]
        /// 
        /// C++ wire format (referans — bizde kullanılmıyor):
        ///   [result: byte]
        ///   [itemId: int32] [pos: byte]
        ///   [reqItemId[0..8]: int32] [reqItemPos[0..8]: byte] × 9
        /// </summary>
        public void MsgRecv_ItemUpgrade(long instanceId, bool success, byte resultType,
            short newLevel, short newAtkMin, short newAtkMax, short newDef)
        {
            // C++ satır 863: CancelIconDrop — mobilde gerekli değil (D&D yok)

            // C++ satır 866-907: Başarı veya başarısız → malzeme tüketimi
            // Local updates are done in HandleRawUpgradePacket.

            switch (resultType)
            {
                case ITEM_UPGRADE_RESULT_FAILED:
                    // C++ satır 909-937: Eşya yandı

                    // C++ satır 931-932: Animasyon state
                    _upgradeSucceeded = false;
                    _upgradeInProgress = true;

                    // Reset slots
                    _upgradeItemSlotInvPos = -1;
                    for (int i = 0; i < ANVIL_REQ_MAX; i++)
                        _requirementSlotInvPos[i] = -1;

                    // C++ satır 935-936: MsgOutput(IDS_ITEM_UPGRADE_FAILED, D3DCOLOR_XRGB(255, 0, 255))
                    if (KOUIManager.Instance != null)
                    {
                        KOUIManager.Instance.AddMsgOutput("Item upgrade failed.",
                            KOUIManager.D3DColorToUnity(0xffff00ff));
                        KOUIManager.Instance.PlayUpgradeAnimation(false);
                    }
                    break;

                case ITEM_UPGRADE_RESULT_SUCCEEDED:
                    // C++ satır 938-1005: Başarılı upgrade

                    // C++ satır 945: m_bUpgradeSucceeded = true
                    _upgradeSucceeded = true;
                    _upgradeInProgress = true;

                    // Clear requirement slots (materials consumed)
                    for (int i = 0; i < ANVIL_REQ_MAX; i++)
                        _requirementSlotInvPos[i] = -1;

                    // C++ satır 951-952: MsgOutput(IDS_ITEM_UPGRADE_SUCCEEDED, D3DCOLOR_XRGB(128, 128, 255))
                    if (KOUIManager.Instance != null)
                    {
                        KOUIManager.Instance.AddMsgOutput($"Item upgrade succeeded! +{newLevel}",
                            KOUIManager.D3DColorToUnity(0xff8080ff));
                        KOUIManager.Instance.PlayUpgradeAnimation(true);
                    }
                    break;

                case ITEM_UPGRADE_RESULT_TRADING:
                    // C++ satır 1006-1016: Trade sırasında upgrade engeli

                    // C++ satır 1010-1011: state reset
                    _upgradeInProgress = false;
                    ResetUpgradeInventory();

                    // C++ satır 1014-1015: MsgOutput(IDS_ITEM_UPGRADE_CANNOT_PERFORM, D3DCOLOR_XRGB(255, 0, 255))
                    if (KOUIManager.Instance != null)
                        KOUIManager.Instance.AddMsgOutput("Cannot perform item upgrade.",
                            KOUIManager.D3DColorToUnity(0xffff00ff));
                    break;

                case ITEM_UPGRADE_RESULT_NEED_COINS:
                    // C++ satır 1017-1027

                    _upgradeInProgress = false;
                    ResetUpgradeInventory();

                    // C++ satır 1025-1026: D3DCOLOR_XRGB(255, 0, 255)
                    if (KOUIManager.Instance != null)
                        KOUIManager.Instance.AddMsgOutput("You don't have enough Coins.",
                            KOUIManager.D3DColorToUnity(0xffff00ff));
                    break;

                case ITEM_UPGRADE_RESULT_NO_MATCH:
                    // C++ satır 1028-1038

                    _upgradeInProgress = false;
                    ResetUpgradeInventory();

                    // C++ satır 1036-1037: D3DCOLOR_XRGB(255, 0, 255)
                    if (KOUIManager.Instance != null)
                        KOUIManager.Instance.AddMsgOutput("The items required for upgrade does not match.",
                            KOUIManager.D3DColorToUnity(0xffff00ff));
                    break;

                default:
                    Debug.LogWarning($"[UPGRADE] Bilinmeyen resultType: {resultType}");
                    _upgradeInProgress = false;
                    ResetUpgradeInventory();
                    break;
            }

            // C++ satır 1040: GoldUpdate()
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.UpdateUpgradeGold();
        }

        // ==========================================
        // Yardımcı metotlar
        // ==========================================

        /// <summary>Tüm slotları başlangıç durumuna getirir.</summary>
        private void ResetAllSlots()
        {
            _upgradeItemSlotInvPos = -1;
            _upgradeInProgress = false;
            _upgradeSucceeded = false;
            _npcId = 0;
            for (int i = 0; i < ANVIL_REQ_MAX; i++)
                _requirementSlotInvPos[i] = -1;
        }

        /// <summary>
        /// Envanterde belirtilen SlotIndex'teki eşyayı bulur.
        /// C++ karşılığı: m_pMyUpgradeInv[i] — CopyInventoryItems ile doldurulan kopya envanter.
        /// Mobil: GameManager.Inventory referansı üzerinden doğrudan okuma.
        /// </summary>
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

        /// <summary>
        /// Belirli bir requirement slot'unun pozisyonunu döndürür.
        /// -1 = boş slot.
        /// </summary>
        public int GetRequirementSlotPos(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= ANVIL_REQ_MAX)
                return -1;
            return _requirementSlotInvPos[slotIndex];
        }

        /// <summary>
        /// Dolu olan requirement slot sayısını döndürür.
        /// </summary>
        public int GetFilledRequirementSlotCount()
        {
            int count = 0;
            for (int i = 0; i < ANVIL_REQ_MAX; i++)
            {
                if (_requirementSlotInvPos[i] >= 0)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Removes an item from the requirement slot.
        /// </summary>
        public void RemoveRequirementItem(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < ANVIL_REQ_MAX)
            {
                _requirementSlotInvPos[slotIndex] = -1;
            }
        }

        /// <summary>
        /// Calculates the upgraded item preview ID if possible.
        /// </summary>
        public bool RequestUpgradePreview(out int previewResultId)
        {
            previewResultId = 0;
            if (_upgradeItemSlotInvPos < 0) return false;

            var upgradeItem = FindInventoryItemBySlot(_upgradeItemSlotInvPos);
            if (upgradeItem == null || upgradeItem.ItemDefId == 0) return false;

            // Check if the item is upgradeable
            if (!IsAllowedUpgradeItem(upgradeItem, ignoreSlotCheck: true)) return false;

            KOTableReader.TableItemBasic basic = null;
            KOTableReader.TableItemExt ext = null;
            if (KOInventory.s_pTbl_Items_Basic != null)
            {
                basic = KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, upgradeItem.ItemDefId);
                if (basic != null && KOInventory.s_pTbl_Items_Exts != null)
                {
                    ext = KOTableReader.FindItemExt(KOInventory.s_pTbl_Items_Exts, basic.byExtIndex, upgradeItem.ItemDefId);
                }
            }

            int currentLevel = GetItemUpgradeLevel(upgradeItem.ItemDefId, basic, ext);
            if (currentLevel >= 9) return false; // Maximum upgrade level reached

            // Standard upgrade progression: itemId + 1
            int candidateId = upgradeItem.ItemDefId + 1;
            if (ext != null && ext.byMagicOrRare == 4 && currentLevel == 0) // Unique +0 item
            {
                candidateId = GetUniqueNextLevelId(upgradeItem.ItemDefId);
            }

            // Verify if candidateId exists in the items table
            if (basic != null && KOInventory.s_pTbl_Items_Exts != null)
            {
                var candExt = KOTableReader.FindItemExt(KOInventory.s_pTbl_Items_Exts, basic.byExtIndex, candidateId);
                if (candExt != null)
                {
                    previewResultId = candidateId;
                    return true;
                }
            }

            return false;
        }

        private int GetItemUpgradeLevel(int itemDefId, KOTableReader.TableItemBasic basic, KOTableReader.TableItemExt ext)
        {
            if (basic == null) return 0;
            bool isUnique = (ext != null && ext.byMagicOrRare == 4);
            if (isUnique)
            {
                if (UNIQUE_BASE_TO_PLUS_ONE.ContainsKey(itemDefId))
                {
                    return 0;
                }
                else
                {
                    int level = itemDefId % 10;
                    return (level == 0) ? 10 : level;
                }
            }
            return itemDefId % 10;
        }

        private int GetUniqueNextLevelId(int itemId)
        {
            if (UNIQUE_BASE_TO_PLUS_ONE.TryGetValue(itemId, out int plusOneId))
            {
                return plusOneId;
            }
            return itemId + 1;
        }

        /// <summary>
        /// Calculates the upgrade success rate (0-100%) dynamically based on slot contents.
        /// Returns -1f if no rate should be displayed.
        /// </summary>
        public float CalculateUpgradeRate()
        {
            if (_upgradeItemSlotInvPos < 0) return -1f;

            var upgradeItem = FindInventoryItemBySlot(_upgradeItemSlotInvPos);
            if (upgradeItem == null || upgradeItem.ItemDefId == 0) return -1f;

            // Find scroll and Trina in requirement slots
            int scrollId = 0;
            bool hasTrina = false;

            for (int i = 0; i < ANVIL_REQ_MAX; i++)
            {
                int order = _requirementSlotInvPos[i];
                if (order < 0) continue;

                var reqItem = FindInventoryItemBySlot(order);
                if (reqItem == null) continue;

                if (reqItem.ItemDefId / 1000000 == 379)
                {
                    scrollId = reqItem.ItemDefId;
                }
                else if (reqItem.ItemDefId == 700002000)
                {
                    hasTrina = true;
                }
            }

            // No scroll placed -> don't show any rate
            if (scrollId == 0) return -1f;

            KOTableReader.TableItemBasic basic = null;
            KOTableReader.TableItemExt ext = null;
            if (KOInventory.s_pTbl_Items_Basic != null)
            {
                basic = KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, upgradeItem.ItemDefId);
                if (basic != null && KOInventory.s_pTbl_Items_Exts != null)
                {
                    ext = KOTableReader.FindItemExt(KOInventory.s_pTbl_Items_Exts, basic.byExtIndex, upgradeItem.ItemDefId);
                }
            }

            int currentLevel = GetItemUpgradeLevel(upgradeItem.ItemDefId, basic, ext);

            if (scrollId == 379021000 || scrollId == 379221000 || scrollId == 379205000) // Blessed, Low Class, or Middle Class Upgrade Scroll
            {
                if (hasTrina)
                {
                    switch (currentLevel)
                    {
                        case 1: return 100f;
                        case 2: return 100f;
                        case 3: return 100f;
                        case 4: return 100f;
                        case 5: return 70f;
                        case 6: return 40f;
                        case 7: return 15f;
                        case 8: return 6f;
                        case 9: return 3f;
                        default: return 0f;
                    }
                }
                else
                {
                    switch (currentLevel)
                    {
                        case 1: return 100f;
                        case 2: return 100f;
                        case 3: return 70f;
                        case 4: return 70f;
                        case 5: return 60f;
                        case 6: return 30f;
                        case 7: return 5f;
                        case 8: return 2f;
                        case 9: return 1f;
                        default: return 0f;
                    }
                }
            }
            else if (scrollId == 379016000) // Standard Upgrade Scroll
            {
                switch (currentLevel)
                {
                    case 1: return 95f;
                    case 2: return 50f;
                    case 3: return 40f;
                    case 4: return 35f;
                    default: return 0f;
                }
            }
            else if (scrollId == 379025000) // Blessed Elemental Scroll
            {
                return 100f;
            }
            else if (scrollId == 379022000 || scrollId == 379023000 || scrollId == 379024000 || 
                     (scrollId >= 379030000 && scrollId <= 379033000)) // Blessed Enchant, Reduce, Immune Scrolls
            {
                return 95f;
            }
            else // Normal Enchant, Reduce, Elemental, or Immune Scrolls (e.g. 379017000, 379222000 etc.)
            {
                return 30f;
            }
        }

        /// <summary>
        /// Calculates the required coins (Noah) dynamically based on the scroll placed in requirement slots.
        /// Returns -1 if no scroll is placed.
        /// </summary>
        public int CalculateUpgradeCost()
        {
            if (_upgradeItemSlotInvPos < 0) return -1;

            var upgradeItem = FindInventoryItemBySlot(_upgradeItemSlotInvPos);
            if (upgradeItem == null || upgradeItem.ItemDefId == 0) return -1;

            // Find scroll ID in requirement slots
            int scrollId = 0;
            for (int i = 0; i < ANVIL_REQ_MAX; i++)
            {
                int order = _requirementSlotInvPos[i];
                if (order < 0) continue;

                var reqItem = FindInventoryItemBySlot(order);
                if (reqItem == null) continue;

                if (reqItem.ItemDefId / 1000000 == 379)
                {
                    scrollId = reqItem.ItemDefId;
                }
            }

            if (scrollId == 0) return -1;

            // Mapped cost based on scroll ID in ITEM_UPGRADE table
            if (scrollId == 379021000 || scrollId == 379079000 || scrollId == 379152000)
            {
                return 240000; // Blessed Upgrade Scroll
            }
            else if (scrollId == 379025000 || 
                     scrollId == 379034000 || scrollId == 379035000 ||
                     (scrollId >= 379138000 && scrollId <= 379141000) ||
                     (scrollId >= 379215000 && scrollId <= 379220000) ||
                     (scrollId >= 379230000 && scrollId <= 379235000))
            {
                return 500000; // Blessed Elemental Scroll & Dispell Scrolls
            }
            else if (scrollId == 379159000)
            {
                return 2000000; // Rebirth Scroll
            }

            return 0; // Other scrolls (Low Class, Middle Class, Standard, etc.)
        }
    }
}
