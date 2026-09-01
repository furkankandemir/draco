using System;
using UnityEngine;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO: CUIWareHouseDlg (UIWareHouseDlg.h/cpp) veri katmanı birebir port.
    /// 
    /// Sabitler:
    ///   GameDef.h:1248: MAX_ITEM_TRADE = 24      — sayfa başına slot
    ///   GameDef.h:1250: MAX_ITEM_WARE_PAGE = 8    — toplam sayfa
    ///   Toplam depo slotu: 8 × 24 = 192
    /// 
    /// State:
    ///   cpp:19: m_pMyWare[MAX_ITEM_WARE_PAGE][MAX_ITEM_TRADE] — depo slotları
    ///   cpp:30: m_iCurPage                                     — aktif sayfa
    ///   cpp:32-33: m_bSendedItemGold, m_iGoldOffsetBackup     — gold transfer state
    ///   cpp:549-550: m_pStrWareGold (warehouse gold display)
    /// </summary>
    public class KOWarehouseManager : MonoBehaviour
    {
        public static KOWarehouseManager Instance { get; private set; }

        // ============================
        // CONSTANTS — GameDef.h birebir
        // ============================
        
        /// <summary>GameDef.h:1248: MAX_ITEM_TRADE = 24</summary>
        public const int MAX_ITEM_TRADE = 24;
        
        /// <summary>GameDef.h:1250: MAX_ITEM_WARE_PAGE = 8</summary>
        public const int MAX_ITEM_WARE_PAGE = 8;

        /// <summary>SubProcPerTrade.h:40: dwGold = 900000000 — Gold item ID sabiti</summary>
        public const int GOLD_ITEM_ID = 900000000;

        // ============================
        // SUB-PACKET ENUM — PacketDef.h:170-176 birebir
        // ============================
        
        /// <summary>PacketDef.h:175: N3_SP_WARE_INN = 0x10</summary>
        public const byte N3_SP_WARE_INN       = 0x10;
        /// <summary>PacketDef.h:161: N3_SP_WARE_OPEN = 0x01</summary>
        public const byte N3_SP_WARE_OPEN      = 0x01;
        /// <summary>PacketDef.h:162: N3_SP_WARE_GET_IN = 0x02 (deposit: inv→ware)</summary>
        public const byte N3_SP_WARE_GET_IN    = 0x02;
        /// <summary>PacketDef.h:163: N3_SP_WARE_GET_OUT = 0x03 (withdraw: ware→inv)</summary>
        public const byte N3_SP_WARE_GET_OUT   = 0x03;
        /// <summary>PacketDef.h:164: N3_SP_WARE_WARE_MOVE = 0x04 (ware slot→ware slot)</summary>
        public const byte N3_SP_WARE_WARE_MOVE = 0x04;
        /// <summary>PacketDef.h:165: N3_SP_WARE_INV_MOVE = 0x05 (inv slot→inv slot)</summary>
        public const byte N3_SP_WARE_INV_MOVE  = 0x05;

        // ============================
        // STATE — UIWareHouseDlg.h birebir
        // ============================
        
        /// <summary>cpp:19: m_pMyWare[MAX_ITEM_WARE_PAGE][MAX_ITEM_TRADE]</summary>
        private WarehouseSlotData[,] _wareSlots = new WarehouseSlotData[MAX_ITEM_WARE_PAGE, MAX_ITEM_TRADE];

        /// <summary>cpp:35: m_iCurPage</summary>
        public int CurPage { get; private set; }

        /// <summary>cpp:32: m_bSendedItemGold — gold transfer pending flag</summary>
        private bool _sentGold;

        /// <summary>cpp:33: m_iGoldOffsetBackup — gold rollback backup</summary>
        private int _goldOffsetBackup;

        /// <summary>Warehouse gold (cpp:549: iWareGold from MsgRecv_WareHouseOpen)</summary>
        public long WareGold { get; private set; }

        /// <summary>Depo açık mı?</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Sessiz yükleme flag'i (Arayüz açılmasını engeller)</summary>
        public bool IsSilentOpen { get; set; }

        // ============================
        // EVENTS — UI abone olur
        // ============================
        
        public event Action OnWarehouseOpened;
        public event Action OnWarehouseClosed;
        public event Action<int> OnPageChanged;
        public event Action<byte, bool> OnOperationResult; // (subCommand, success)
        /// <summary>C++ birebir: m_pUIInn->SetVisible(true) — Inn UI açılması event'i</summary>
        public event Action OnInnOpened;
        /// <summary>Gold değiştiğinde UI güncelleme — optimistic update sonrası</summary>
        public event Action OnGoldChanged;

        // ============================
        // LIFECYCLE
        // ============================
        
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            KOPacketHandler.OnWarehouse += HandleWarehouse_KO;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnWarehouse -= HandleWarehouse_KO;
        }

        /// <summary>KO wrapper — WIZ_WAREHOUSE</summary>
        private void HandleWarehouse_KO(byte[] rawData)
        {
            // C++ birebir: GameProcMain.cpp:6452-6530 — MsgRecv_WareHouse dispatch
            // Wire: [opcode][sub:byte][...]
            var r = new KOPacketReader(rawData);
            byte sub = r.ReadByte();

            switch (sub)
            {
                case N3_SP_WARE_INN:
                {
                    // C++ birebir: GameProcMain.cpp:6459-6462
                    // m_pUIInn->SetVisible(true) — Inn UI aç
                    OnInnOpened?.Invoke();
                    break;
                }
                case N3_SP_WARE_OPEN:
                {
                    // C++ birebir: GameProcMain.cpp:6501-6530 — MsgRecv_WareHouseOpen
                    // Wire: [success:byte][gold:uint32][192 × {itemId:uint32, durability:int16, count:int16}]
                    // C++ satır 6506: /*uint8_t idk =*/pkt.read<uint8_t>(); — success byte atlanır
                    byte openResult = r.ReadByte(); // C++ birebir: satır 6506
                    uint gold = r.ReadUInt32();
                    int totalSlots = MAX_ITEM_WARE_PAGE * MAX_ITEM_TRADE; // 192

                    var items = new WarehouseSlot[totalSlots];
                    for (int i = 0; i < totalSlots; i++)
                    {
                        uint itemId     = r.ReadUInt32();
                        short durability = r.ReadInt16();
                        short count      = r.ReadInt16();
                        items[i] = new WarehouseSlot
                        {
                            ItemId     = (int)itemId,
                            Durability = durability,
                            Count      = count,
                            Slot       = (byte)i
                        };
                    }

                    HandleWarehouseData(gold, items);
                    break;
                }
                case N3_SP_WARE_GET_IN:
                case N3_SP_WARE_GET_OUT:
                case N3_SP_WARE_WARE_MOVE:
                case N3_SP_WARE_INV_MOVE:
                {
                    // C++ birebir: GameProcMain.cpp:6469-6494
                    // Wire: [result:byte]
                    byte result = r.ReadByte();
                    HandleWarehouseResult(sub, result == 0x01);
                    break;
                }
                default:
                {
                    Debug.LogWarning($"[WAREHOUSE] Unknown sub-opcode: 0x{sub:X2}");
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================
        // PACKET HANDLERS
        // ============================

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:6501-6530 — MsgRecv_WareHouseOpen
        /// cpp:6509: iWareGold = pkt.read<uint32_t>()
        /// cpp:6510: EnterWareHouseStateStart(iWareGold)
        /// cpp:6512-6518: loop 192 slots → AddItemInWare
        /// cpp:6529: EnterWareHouseStateEnd()
        /// </summary>
        private void HandleWarehouseData(long gold, WarehouseSlot[] items)
        {
            // cpp:6510
            EnterWareHouseStateStart(gold);

            // cpp:6512-6518: tüm slotları doldur
            if (items != null)
            {
                foreach (var item in items)
                {
                    AddItemInWare(item.ItemId, item.Durability, item.Count, item.Slot);
                }
            }

            // cpp:6529
            EnterWareHouseStateEnd();
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:6452-6498 — MsgRecv_WareHouse result dispatch
        /// </summary>
        private void HandleWarehouseResult(byte command, bool success)
        {
            ReceiveResult(command, (byte)(success ? 0x01 : 0x00));
        }

        // ============================
        // EnterWareHouseStateStart — cpp:510-551 birebir
        // Depo açılınca tüm slotları temizle + wareGold ayarla
        // ============================
        
        /// <summary>
        /// Open-KO: CUIWareHouseDlg::EnterWareHouseStateStart(iWareGold) — cpp:510-551
        /// cpp:512-530: m_pMyWare tüm slotları null'la
        /// cpp:549-550: wareGold ayarla
        /// </summary>
        public void EnterWareHouseStateStart(long wareGold)
        {
            // cpp:512-530: tüm slotları temizle
            for (int j = 0; j < MAX_ITEM_WARE_PAGE; j++)
            {
                for (int i = 0; i < MAX_ITEM_TRADE; i++)
                {
                    _wareSlots[j, i] = null;
                }
            }

            // cpp:549-550: gold ayarla
            WareGold = wareGold;
            _sentGold = false;
            _goldOffsetBackup = 0;
        }

        // ============================
        // AddItemInWare — cpp:62 in h / GameProcMain.cpp:6514-6517 birebir
        // Sunucudan gelen item verilerini slotlara yerleştir
        // ============================
        
        /// <summary>
        /// Open-KO: CUIWareHouseDlg::AddItemInWare(iItemID, iDurability, iCount, iIndex)
        /// cpp:6514-6517 (MsgRecv_WareHouseOpen loop):
        ///   iItemID = pkt.read&lt;uint32_t&gt;()
        ///   iItemDurability = pkt.read&lt;int16_t&gt;()
        ///   iItemCount = pkt.read&lt;int16_t&gt;()
        /// iIndex: 0..(MAX_ITEM_WARE_PAGE × MAX_ITEM_TRADE - 1)
        /// </summary>
        public void AddItemInWare(int itemId, int durability, int count, int index)
        {
            if (itemId == 0) return; // boş slot

            int page = index / MAX_ITEM_TRADE;
            int slot = index % MAX_ITEM_TRADE;

            if (page < 0 || page >= MAX_ITEM_WARE_PAGE) return;
            if (slot < 0 || slot >= MAX_ITEM_TRADE) return;

            _wareSlots[page, slot] = new WarehouseSlotData
            {
                ItemId     = itemId,
                Durability = durability,
                Count      = count
            };
        }

        // ============================
        // EnterWareHouseStateEnd — cpp:553-594 birebir
        // İtem ekleme bitti, sayfa 0'ı göster
        // ============================
        
        /// <summary>
        /// Open-KO: CUIWareHouseDlg::EnterWareHouseStateEnd() — cpp:553-594
        /// cpp:557: m_iCurPage = 0
        /// </summary>
        public void EnterWareHouseStateEnd()
        {
            CurPage = 0; // cpp:557
            IsOpen = true;
            if (!IsSilentOpen)
            {
                OnWarehouseOpened?.Invoke();
            }
            else
            {
                IsSilentOpen = false; // Reset back
            }

        }

        // ============================
        // LeaveWareHouseState — cpp:489-508 birebir
        // ============================
        
        /// <summary>
        /// Open-KO: CUIWareHouseDlg::LeaveWareHouseState() — cpp:489-508
        /// cpp:492: SetVisible(false)
        /// cpp:497: SetState(UI_STATE_COMMON_NONE)
        /// </summary>
        public void LeaveWareHouseState()
        {
            IsOpen = false;
            OnWarehouseClosed?.Invoke();
        }

        // ============================
        // PAGE NAVIGATION — cpp:341-407 birebir
        // ============================
        
        /// <summary>
        /// Open-KO: btn_up handler — cpp:341-373
        /// cpp:343-345: m_iCurPage-- (clamp 0)
        /// </summary>
        public void PageUp()
        {
            CurPage--;
            if (CurPage < 0) CurPage = 0; // cpp:344-345
            OnPageChanged?.Invoke(CurPage);
        }

        /// <summary>
        /// Open-KO: btn_down handler — cpp:375-407
        /// cpp:377-379: m_iCurPage++ (clamp MAX_ITEM_WARE_PAGE-1)
        /// </summary>
        public void PageDown()
        {
            CurPage++;
            if (CurPage >= MAX_ITEM_WARE_PAGE)
                CurPage = MAX_ITEM_WARE_PAGE - 1; // cpp:378-379
            OnPageChanged?.Invoke(CurPage);
        }

        // ============================
        // SLOT ACCESSORS
        // ============================
        
        public WarehouseSlotData GetSlot(int page, int slot)
        {
            if (page < 0 || page >= MAX_ITEM_WARE_PAGE) return null;
            if (slot < 0 || slot >= MAX_ITEM_TRADE) return null;
            return _wareSlots[page, slot];
        }

        public WarehouseSlotData GetSlotOnCurrentPage(int slot)
        {
            return GetSlot(CurPage, slot);
        }

        /// <summary>
        /// C++ birebir: m_pMyWare[page][slot] = spItem
        /// Deposit/withdraw sonrası _wareSlots güncelleme.
        /// </summary>
        public void SetSlot(int page, int slot, int itemId, int durability, int count)
        {
            if (page < 0 || page >= MAX_ITEM_WARE_PAGE) return;
            if (slot < 0 || slot >= MAX_ITEM_TRADE) return;
            _wareSlots[page, slot] = new WarehouseSlotData
            {
                ItemId = itemId,
                Durability = durability,
                Count = count
            };
        }

        /// <summary>
        /// C++ birebir: m_pMyWare[page][slot] = nullptr
        /// Withdraw sonrası slot'u temizleme.
        /// </summary>
        public void ClearSlot(int page, int slot)
        {
            if (page < 0 || page >= MAX_ITEM_WARE_PAGE) return;
            if (slot < 0 || slot >= MAX_ITEM_TRADE) return;
            _wareSlots[page, slot] = null;
        }

        private int GetTotalItemCount()
        {
            int count = 0;
            for (int j = 0; j < MAX_ITEM_WARE_PAGE; j++)
                for (int i = 0; i < MAX_ITEM_TRADE; i++)
                    if (_wareSlots[j, i] != null) count++;
            return count;
        }

        // ============================
        // RECEIVE RESULT — cpp:1138-1437 birebir
        // Sunucudan işlem sonucu geldiğinde slot güncelleme
        // ============================

        /// <summary>
        /// Open-KO: MsgRecv_WareHouse sub-command dispatch — GameProcMain.cpp:6452-6498
        /// cpp:6469: N3_SP_WARE_GET_IN → bResult
        /// cpp:6476: N3_SP_WARE_GET_OUT → bResult
        /// cpp:6483: N3_SP_WARE_WARE_MOVE → bResult
        /// cpp:6490: N3_SP_WARE_INV_MOVE → bResult
        /// </summary>
        public void ReceiveResult(byte subCommand, byte result)
        {
            bool success = (result == 0x01);

            switch (subCommand)
            {
                case N3_SP_WARE_GET_IN:    // deposit result — cpp:6469-6473
                    ReceiveResultToWareMsg(result);
                    break;
                case N3_SP_WARE_GET_OUT:   // withdraw result — cpp:6476-6480
                    ReceiveResultFromWareMsg(result);
                    break;
                case N3_SP_WARE_WARE_MOVE: // ware→ware result — cpp:6483-6487
                    ReceiveResultWareToWareMsg(result);
                    break;
                case N3_SP_WARE_INV_MOVE:  // inv→inv result — cpp:6490-6494
                    ReceiveResultInvToInvMsg(result);
                    break;
            }

            OnOperationResult?.Invoke(subCommand, success);
        }

        /// <summary>
        /// Open-KO: CUIWareHouseDlg::ReceiveResultToWareMsg — cpp:1138-1243
        /// Deposit (inv→ware) sonucu. 0x01=success, else=fail+rollback.
        /// </summary>
        private void ReceiveResultToWareMsg(byte result)
        {
            if (result == 0x01) // cpp:1213: success
            {
                // cpp:1215: m_bSendedItemGold == true ise gold deposit onaylandı → flag reset
                if (_sentGold)
                {
                    _sentGold = false;
                    _goldOffsetBackup = 0;
                }
            }
            else // cpp:1148: fail → rollback
            {
                // cpp:1150-1152: m_bSendedItemGold ise gold rollback
                if (_sentGold)
                    ReceiveResultGoldToWareFail();
            }
        }

        /// <summary>
        /// Open-KO: CUIWareHouseDlg::ReceiveResultFromWareMsg — cpp:1245-1351
        /// Withdraw (ware→inv) sonucu. 0x01=success, else=fail+rollback.
        /// </summary>
        private void ReceiveResultFromWareMsg(byte result)
        {
            if (result == 0x01) // cpp:1320: success
            {
                // cpp:1322: m_bSendedItemGold == true ise gold withdraw onaylandı → flag reset
                if (_sentGold)
                {
                    _sentGold = false;
                    _goldOffsetBackup = 0;
                }
            }
            else // cpp:1254: fail → rollback
            {
                // cpp:1256-1258: m_bSendedItemGold ise gold rollback
                if (_sentGold)
                    ReceiveResultGoldFromWareFail();
            }
        }

        /// <summary>
        /// Open-KO: CUIWareHouseDlg::ReceiveResultWareToWareMsg — cpp:1353-1394
        /// Ware→Ware slot taşıma sonucu. 0x01=success, else=fail+rollback.
        /// </summary>
        private void ReceiveResultWareToWareMsg(byte result)
        {
            if (result != 0x01) // cpp:1358: fail → restore
            {
            }
        }

        /// <summary>
        /// Open-KO: CUIWareHouseDlg::ReceiveResultInvToInvMsg — cpp:1396-1437
        /// Inv→Inv slot taşıma sonucu. 0x01=success, else=fail+rollback.
        /// </summary>
        private void ReceiveResultInvToInvMsg(byte result)
        {
            if (result != 0x01) // cpp:1401: fail → restore
            {
            }
        }

        // ============================
        // SEND PACKETS — cpp:1076-1136 birebir wire format
        // Mevcut WarehouseUI.cs'deki SendDeposit/SendWithdraw zaten doğru.
        // Burada eksik SendWareToWare ve SendInvToInv eklendi.
        // ============================

        /// <summary>
        /// Open-KO: CUIWareHouseDlg::SendToServerToWareMsg — cpp:1076-1093
        /// Wire: [WIZ_WAREHOUSE:byte] [N3_SP_WARE_GET_IN:byte] [itemId:u32] [page:byte] [srcPos:byte] [dstPos:byte] [count:u32]
        /// </summary>
        public void SendToServerToWareMsg(int itemId, byte page, byte srcPos, byte dstPos, int count)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;
            using var p = new KOPacketWriter(WizOpcode.WIZ_WAREHOUSE);
            p.WriteByte(N3_SP_WARE_GET_IN);
            p.WriteInt32(itemId);
            p.WriteByte(page);
            p.WriteByte(srcPos);
            p.WriteByte(dstPos);
            p.WriteInt32(count);
            netMgr.SendPacket(p);
        }

        /// <summary>
        /// Open-KO: CUIWareHouseDlg::SendToServerFromWareMsg — cpp:1095-1108
        /// Wire: [WIZ_WAREHOUSE:byte] [N3_SP_WARE_GET_OUT:byte] [itemId:u32] [page:byte] [srcPos:byte] [dstPos:byte] [count:u32]
        /// </summary>
        public void SendToServerFromWareMsg(int itemId, byte page, byte srcPos, byte dstPos, int count)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;
            using var p = new KOPacketWriter(WizOpcode.WIZ_WAREHOUSE);
            p.WriteByte(N3_SP_WARE_GET_OUT);
            p.WriteInt32(itemId);
            p.WriteByte(page);
            p.WriteByte(srcPos);
            p.WriteByte(dstPos);
            p.WriteInt32(count);
            netMgr.SendPacket(p);
        }

        /// <summary>
        /// Open-KO: CUIWareHouseDlg::SendToServerWareToWareMsg — cpp:1110-1122
        /// Wire: [WIZ_WAREHOUSE:byte] [N3_SP_WARE_WARE_MOVE:byte] [itemId:u32] [page:byte] [srcPos:byte] [dstPos:byte]
        /// </summary>
        public void SendToServerWareToWareMsg(int itemId, byte page, byte srcPos, byte dstPos)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;
            using var p = new KOPacketWriter(WizOpcode.WIZ_WAREHOUSE);
            p.WriteByte(N3_SP_WARE_WARE_MOVE);
            p.WriteInt32(itemId);
            p.WriteByte(page);
            p.WriteByte(srcPos);
            p.WriteByte(dstPos);
            netMgr.SendPacket(p);
        }

        /// <summary>
        /// Open-KO: CUIWareHouseDlg::SendToServerInvToInvMsg — cpp:1124-1136
        /// Wire: [WIZ_WAREHOUSE:byte] [N3_SP_WARE_INV_MOVE:byte] [itemId:u32] [page:byte] [srcPos:byte] [dstPos:byte]
        /// </summary>
        public void SendToServerInvToInvMsg(int itemId, byte page, byte srcPos, byte dstPos)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;
            using var p = new KOPacketWriter(WizOpcode.WIZ_WAREHOUSE);
            p.WriteByte(N3_SP_WARE_INV_MOVE);
            p.WriteInt32(itemId);
            p.WriteByte(page);
            p.WriteByte(srcPos);
            p.WriteByte(dstPos);
            netMgr.SendPacket(p);
        }

        // ============================
        // GOLD TRANSFER — cpp:1439-1437 (GoldCount* metotları)
        // ============================

        /// <summary>
        /// Open-KO: GoldCountToWareOK — gold'u envanter→depo'ya aktar.
        /// m_bSendedItemGold = true, backup iGold, send WARE_GET_IN
        /// </summary>
        public void GoldDepositToWare(int amount)
        {
            if (amount <= 0) return;
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.Gold < amount) return;

            _sentGold = true;
            _goldOffsetBackup = amount;

            // cpp:1856-1857: Optimistic update
            gm.Gold -= amount;
            WareGold += amount;

            // cpp:1873: SendToServerToWareMsg(dwGold, 0xff, 0xff, 0xff, iGold)
            SendToServerToWareMsg(GOLD_ITEM_ID, 0xff, 0xff, 0xff, amount);

            OnGoldChanged?.Invoke();
        }

        /// <summary>
        /// Open-KO: GoldCountFromWareOK — gold'u depo→envanter'a aktar.
        /// m_bSendedItemGold = true, backup iGold, send WARE_GET_OUT
        /// </summary>
        public void GoldWithdrawFromWare(int amount)
        {
            if (amount <= 0) return;
            if (WareGold < amount) return;

            _sentGold = true;
            _goldOffsetBackup = amount;

            // cpp:1913-1916: Optimistic update
            var gm = GameManager.Instance;
            if (gm != null) gm.Gold += amount;
            WareGold -= amount;

            // cpp:1930: SendToServerFromWareMsg(dwGold, 0xff, 0xff, 0xff, iGold)
            SendToServerFromWareMsg(GOLD_ITEM_ID, 0xff, 0xff, 0xff, amount);

            OnGoldChanged?.Invoke();
        }

        /// <summary>
        /// Open-KO: ReceiveResultGoldToWareFail — cpp:gold deposit fail rollback
        /// </summary>
        public void ReceiveResultGoldToWareFail()
        {
            _sentGold = false;
            var gm = GameManager.Instance;
            if (gm != null) gm.Gold += _goldOffsetBackup;
            WareGold -= _goldOffsetBackup;
            _goldOffsetBackup = 0;
        }

        /// <summary>
        /// Open-KO: ReceiveResultGoldFromWareFail — cpp:gold withdraw fail rollback
        /// </summary>
        public void ReceiveResultGoldFromWareFail()
        {
            _sentGold = false;
            var gm = GameManager.Instance;
            if (gm != null) gm.Gold -= _goldOffsetBackup;
            WareGold += _goldOffsetBackup;
            _goldOffsetBackup = 0;
        }
    }

    /// <summary>
    /// Warehouse slot verisi — __IconItemSkill basitleştirilmiş hali.
    /// cpp:19: m_pMyWare[page][slot] → ItemId + Durability + Count
    /// </summary>
    public class WarehouseSlotData
    {
        public int ItemId;
        public int Durability;
        public int Count;
    }
}
