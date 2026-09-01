using UnityEngine;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.UI;
using EntropyOnline.Import;

namespace EntropyOnline.Trade
{
    /// <summary>
    /// Open-KO birebir: CSubProcPerTrade (SubProcPerTrade.cpp)
    /// 
    /// State machine:
    ///   PER_TRADE_STATE_NONE                                  = 0  (idle)
    ///   PER_TRADE_STATE_WAIT_FOR_REQ                          = 1  (REQ gönderildi, yanıt bekleniyor)
    ///   PER_TRADE_STATE_WAIT_FOR_MY_DECISION_AGREE_OR_DISAGREE= 2  (REQ alındı, kabul/red bekleniyor)
    ///   PER_TRADE_STATE_NORMAL                                = 3  (takas penceresi açık, item eklenebilir)
    ///   PER_TRARE_STATE_EDITTING                              = 4  (gold/count edit açık)
    ///   PER_TRADE_STATE_MY_TRADE_DECISION_DONE                = 5  (onayladım, karşı bekleniyor)
    /// </summary>
    public class KOTradeManager : MonoBehaviour
    {
        public static KOTradeManager Instance { get; private set; }

        // Open-KO birebir: e_PerTradeState (SubProcPerTrade.h:20-27)
        public enum PerTradeState
        {
            None = 0,
            WaitForReq = 1,
            WaitForMyDecision = 2,
            Normal = 3,
            Editing = 4,
            MyDecisionDone = 5
        }

        // Open-KO birebir: EXCHANGE sub-opcode sabitleri — PacketDef.h:78-85
        private const byte EXCHANGE_REQ = 0x01;
        private const byte EXCHANGE_AGREE = 0x02;
        private const byte EXCHANGE_ADD = 0x03;
        private const byte EXCHANGE_OTHERADD = 0x04;
        private const byte EXCHANGE_DECIDE = 0x05;
        private const byte EXCHANGE_OTHERDECIDE = 0x06;
        private const byte EXCHANGE_DONE = 0x07;
        private const byte EXCHANGE_CANCEL = 0x08;

        // Open-KO birebir: ITEM_GOLD (globals.h: ITEM_NOAH = 900000000)
        public const int ITEM_GOLD = 900000000;
        public const int MAX_ITEM_PER_TRADE = 12;

        // State
        public PerTradeState State { get; private set; } = PerTradeState.None;
        public short OtherId { get; private set; } = -1;

        // Gold backup (Open-KO: m_iGoldOffsetBackup)
        private int _goldOffsetBackup;

        // ================================================
        // COUNTABLE ITEM ADD STATE
        // Open-KO birebir: CUIPerTradeDlg'daki backup field'lar
        //   s_sRecoveryJobInfo.UIWndSourceStart.iOrder → _pendingCountableInvPos
        //   s_sRecoveryJobInfo.UIWndSourceEnd.iOrder   → _pendingCountableTradeSlot
        //   m_iBackupiOrder[i]                         → _pendingCountableInvPos
        //   m_iBackupiCount                            → _pendingCountableBackupCount
        // ================================================
        private int _pendingCountableInvPos = -1;
        private int _pendingCountableTradeSlot = -1;
        private int _pendingCountableItemId;
        private int _pendingCountableMaxCount;  // max = envanter stack count
        private int _pendingCountableBackupCount; // C++ m_iBackupiCount — fail recovery için

        // Item add türü backup — C++ m_ePerTradeItemKindBackup
        private enum PerTradeItemKind { None, Money, Other }
        private PerTradeItemKind _perTradeItemKindBackup = PerTradeItemKind.None;


        // ================================================
        // TRADE SLOT TRACKING
        // Open-KO birebir: CUIPerTradeDlg::m_pPerTradeMy[MAX_ITEM_PER_TRADE]
        //                   CUIPerTradeDlg::m_pPerTradeOther[MAX_ITEM_PER_TRADE]
        // ================================================

        /// <summary>Benim eklediğim item'lar (UI slot dizisi)</summary>
        public TradeSlotItem[] MySlots { get; private set; } = new TradeSlotItem[MAX_ITEM_PER_TRADE];

        /// <summary>Karşının eklediği item'lar (UI slot dizisi)</summary>
        public TradeSlotItem[] OtherSlots { get; private set; } = new TradeSlotItem[MAX_ITEM_PER_TRADE];

        /// <summary>Benim takas penceresindeki gold miktarı</summary>
        public int MyTradeGold { get; private set; }

        /// <summary>Karşının takas penceresindeki gold miktarı</summary>
        public int OtherTradeGold { get; private set; }

        // Pending add tracking — server yanıtı gelene kadar ne eklendiğini tut
        private int _pendingAddItemId;
        private int _pendingAddCount;
        private short _pendingAddDurability;
        private byte _pendingAddInvPos;

        // ================================================
        // UNITY LIFECYCLE
        // ================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            KOPacketHandler.OnExchange += HandleExchange_KO;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnExchange -= HandleExchange_KO;
        }

        /// <summary>KO wrapper — WIZ_EXCHANGE</summary>
        private void HandleExchange_KO(byte[] rawData)
        {
            // C++ birebir: MsgRecv_PerTrade dispatch — SubProcPerTrade.cpp
            // Wire: [opcode][sub:byte][...]
            var r = new KOPacketReader(rawData);
            byte sub = r.ReadByte();

            switch (sub)
            {
                case EXCHANGE_REQ:
                {
                    // Wire: [requesterId:int16]
                    short requesterId = r.ReadInt16();
                    ReceiveMsgPerTradeReq(requesterId);
                    break;
                }
                case EXCHANGE_AGREE:
                {
                    // Wire: [result:int16] (0x01=accept, else=reject)
                    short result = r.ReadInt16();
                    ReceiveMsgPerTradeAgree(result);
                    break;
                }
                case EXCHANGE_ADD:
                {
                    // Wire: [result:byte]
                    byte result = r.ReadByte();
                    ReceiveMsgPerTradeAdd(result);
                    break;
                }
                case EXCHANGE_OTHERADD:
                {
                    // Wire: [itemId:int32][count:int32][durability:int16]
                    int itemId       = r.ReadInt32();
                    int count        = r.ReadInt32();
                    short durability = r.ReadInt16();
                    ReceiveMsgPerTradeOtherAdd(itemId, count, durability);
                    break;
                }
                case EXCHANGE_DECIDE:
                {
                    // Client-side confirm decision (sent to server, server doesn't echo back)
                    break;
                }
                case EXCHANGE_OTHERDECIDE:
                {
                    // Other player confirmed
                    ReceiveMsgPerTradeOtherDecide();
                    break;
                }
                case EXCHANGE_DONE:
                {
                    byte result = r.ReadByte();
                    if (result == 0x01)
                    {
                        int totalGold = r.ReadInt32();
                        short itemCount = r.ReadInt16();
                        var items = new TradeResultItem[itemCount];
                        for (int i = 0; i < itemCount; i++)
                        {
                            items[i] = new TradeResultItem
                            {
                                Pos = r.ReadByte(),
                                ItemId = r.ReadInt32(),
                                Count = r.ReadInt16(),
                                Durability = r.ReadInt16()
                            };
                        }
                        ReceiveMsgPerTradeDone(true, totalGold, items);
                    }
                    else
                    {
                        ReceiveMsgPerTradeDone(false, 0, null);
                    }
                    break;
                }
                case EXCHANGE_CANCEL:
                {
                    ReceiveMsgPerTradeCancel();
                    break;
                }
                default:
                {
                    Debug.LogWarning($"[TRADE] Unknown WIZ_EXCHANGE sub-opcode: 0x{sub:X2}");
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================
        // C→S PAKET GÖNDERİM (Open-KO birebir)
        // ================================================

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:121-134 — EnterWaitMsgFromServerStatePerTradeReq
        /// Takas isteği gönder (hedefe tıkla).
        /// Wire: [C2S_TRADE_REQUEST] [destShortId: short]
        /// </summary>
        public void SendExchangeReq(short targetShortId)
        {
            if (State != PerTradeState.None)
            {
                Debug.LogWarning("[TRADE] Zaten bir takas işlemi aktif.");
                return;
            }

            OtherId = targetShortId;
            State = PerTradeState.WaitForReq;

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_EXCHANGE);
            pkt.WriteByte(EXCHANGE_REQ);
            pkt.WriteInt16(targetShortId);
            KONetworkManager.Instance?.SendPacket(pkt);

            // Open-KO: SecureCodeBegin()
            SecureCodeBegin();
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:429-442 — ProcessProceed(PER_TRADE_RESULT_MY_AGREE)
        /// Takas isteğini kabul et.
        /// Wire: [C2S_TRADE_RESPONSE] [0x01]
        /// </summary>
        public void SendExchangeAgree()
        {
            if (State != PerTradeState.WaitForMyDecision)
            {
                Debug.LogWarning("[TRADE] Kabul edilecek takas isteği yok.");
                return;
            }

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_EXCHANGE);
            pkt.WriteByte(EXCHANGE_AGREE);
            pkt.WriteByte(0x01); // kabul
            KONetworkManager.Instance?.SendPacket(pkt);

            // Open-KO: PerTradeCoreStart() — SubProcPerTrade.cpp:456-465
            PerTradeCoreStart();
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:400-410 — LeavePerTradeState(PER_TRADE_RESULT_MY_DISAGREE)
        /// Takas isteğini reddet.
        /// Wire: [C2S_TRADE_RESPONSE] [0x00]
        /// </summary>
        public void SendExchangeDisagree()
        {
            if (State != PerTradeState.WaitForMyDecision)
            {
                Debug.LogWarning("[TRADE] Reddedilecek takas isteği yok.");
                return;
            }

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_EXCHANGE);
            pkt.WriteByte(EXCHANGE_AGREE);
            pkt.WriteByte(0x00); // ret
            KONetworkManager.Instance?.SendPacket(pkt);

            FinalizePerTrade();
        }


        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:480-488 — RequestItemCountEdit
        /// btn_gold tıklandığında gold edit penceresi aç.
        /// State: Normal → Editing
        /// </summary>
        public void RequestItemCountEdit()
        {
            // SubProcPerTrade.cpp:482-483
            if (State != PerTradeState.Normal)
            {
                return;
            }

            // SubProcPerTrade.cpp:484
            State = PerTradeState.Editing;

            // SubProcPerTrade.cpp:486 — m_pUITradeEditDlg->Open(true)
            // SubProcPerTrade.cpp:487 — m_pUIPerTradeDlg->PlayGoldSound()
            OnGoldEditOpen?.Invoke();
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:490-552 — ItemCountEditOK
        /// Gold edit onaylandığında:
        /// 1) Mevcut trade gold'u oku
        /// 2) Edit pencereden miktar al
        /// 3) Validasyon (miktar ≤ 0 → return false, miktar > myMoney → return false)
        /// 4) Gold düşür: iMyMoney -= iGoldOffset
        /// 5) Paket gönder: WIZ_EXCHANGE + EXCHANGE_ADD + 0xFF + dwGold + iGoldOffset
        /// 6) State = Normal, pencere kapat, PlayGoldSound
        /// Returns: true = başarılı (pencere kapatılabilir), false = validasyon fail (pencere açık kalmalı)
        /// </summary>
        public bool ItemCountEditOK(int iGoldOffset)
        {
            // SubProcPerTrade.cpp:507
            _goldOffsetBackup = iGoldOffset;

            // SubProcPerTrade.cpp:510 — mevcut gold al
            int iMyMoney = GameManager.Instance != null ? (int)GameManager.Instance.Gold : 0;

            // SubProcPerTrade.cpp:512-515 — validasyon
            // C++ birebir: return ederse Close() çağrılmaz — popup açık kalır
            if (iGoldOffset <= 0)
                return false;
            if (iGoldOffset > iMyMoney)
                return false;

            // SubProcPerTrade.cpp:518-519 — gold düşür
            iMyMoney -= iGoldOffset;
            if (GameManager.Instance != null)
                GameManager.Instance.Gold = iMyMoney;

            // SubProcPerTrade.cpp:527 — trade gold artır (client-side)
            MyTradeGold += iGoldOffset;

            // SubProcPerTrade.cpp:548 — m_ePerTradeItemKindBackup = PER_TRADE_ITEM_MONEY
            _perTradeItemKindBackup = PerTradeItemKind.Money;

            // SubProcPerTrade.cpp:530-541 — paket gönder
            _pendingAddItemId = ITEM_GOLD;
            _pendingAddCount = iGoldOffset;

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_EXCHANGE);
            pkt.WriteByte(EXCHANGE_ADD);
            pkt.WriteByte(0xFF); // pos = 0xFF → gold (SubProcPerTrade.cpp:537)
            pkt.WriteInt32(ITEM_GOLD); // SubProcPerTrade.cpp:538 — dwGold
            pkt.WriteInt32(iGoldOffset); // SubProcPerTrade.cpp:539
            KONetworkManager.Instance?.SendPacket(pkt);

            // SubProcPerTrade.cpp:543-549 — temizle, kapat
            State = PerTradeState.Normal;

            // SubProcPerTrade.cpp:551 — PlayGoldSound + UI bildir
            OnGoldEditConfirmed?.Invoke(iGoldOffset);

            return true;
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:554-563 — ItemCountEditCancel
        /// Gold edit iptal — state Normal'e geri dön, pencere kapat.
        /// </summary>
        public void ItemCountEditCancel()
        {
            // SubProcPerTrade.cpp:559
            State = PerTradeState.Normal;

            // SubProcPerTrade.cpp:562 — PlayGoldSound
            OnGoldEditCancelled?.Invoke();
        }

        // ================================================
        // COUNTABLE ITEM ADD — C++ UIPerTradeDlg.cpp birebir
        // ================================================

        /// <summary>
        /// Open-KO birebir: UIPerTradeDlg::ReceiveIconDrop satır 569-570
        /// m_ePerTradeItemKindBackup = PER_TRADE_ITEM_OTHER (veya PER_TRADE_ITEM_MONEY)
        /// Countable/non-countable dallanmasından ÖNCE çağrılır.
        /// </summary>
        public void SetPerTradeItemKindBackup(bool isOther)
        {
            _perTradeItemKindBackup = isOther ? PerTradeItemKind.Other : PerTradeItemKind.Money;
        }

        /// <summary>
        /// Open-KO birebir: UIPerTradeDlg::ReceiveIconDrop satır 695-696
        /// s_sRecoveryJobInfo.UIWndSourceEnd.iOrder = i (non-countable)
        /// m_iBackupiOrder[i] = srcOrder
        /// Fail recovery için trade slot index'i kaydet.
        /// </summary>
        public void SetPendingNonCountableAdd(int srcInvPos, int destTradeSlot)
        {
            _pendingCountableInvPos = srcInvPos;
            _pendingCountableTradeSlot = destTradeSlot;
        }

        /// <summary>
        /// Open-KO birebir: UIPerTradeDlg::ReceiveIconDrop satır 569-624
        /// s_sRecoveryJobInfo set + m_iBackupiOrder set.
        /// KOTradeDropTarget.OnDrop'tan çağrılır.
        /// </summary>
        public void SetPendingCountableAdd(int srcInvPos, int destTradeSlot, int itemId, int maxCount)
        {
            _pendingCountableInvPos = srcInvPos;
            _pendingCountableTradeSlot = destTradeSlot;
            _pendingCountableItemId = itemId;
            _pendingCountableMaxCount = maxCount;
            _perTradeItemKindBackup = PerTradeItemKind.Other;
        }

        /// <summary>
        /// Open-KO birebir: UIPerTradeDlg::ReceiveIconDrop satır 666-667
        /// s_pCountableItemEdit->Open(UIWND_PER_TRADE, UIWND_DISTRICT_PER_TRADE_MY, false)
        /// State: Normal (C++ satır 666: s_bWaitFromServer = false — countable edit sırasında bekleme yok)
        /// </summary>
        public void RequestItemCountEditForItem()
        {
            // C++ satır 666: s_bWaitFromServer = false — countable item edit açılırken wait yok
            // C++ satır 667: s_pCountableItemEdit->Open(false) — false = item count (gold değil)
            OnItemCountEditOpen?.Invoke(_pendingCountableMaxCount);
        }

        /// <summary>
        /// Open-KO birebir: UIPerTradeDlg::ItemCountOK() (UIPerTradeDlg.cpp:406-475)
        /// Adet popup onaylandığında çağrılır.
        /// 
        /// 1) Validasyon: count ≤ 0 → return false, count > stackCount → return false
        /// 2) Stack düşür (envanter tarafı)
        /// 3) Trade slot'a count ekle
        /// 4) Paket gönder: SendToServerItemAddMsg(invPos, itemID, count)
        /// 5) Popup kapat
        /// 
        /// Returns: true=başarılı, false=validasyon fail (popup açık kalır)
        /// </summary>
        public bool CountableItemCountOK(int count)
        {
            if (count <= 0)
                return false;

            var koInv = EntropyOnline.UI.KOInventory.Instance;
            if (koInv == null || _pendingCountableInvPos < 0 || _pendingCountableInvPos >= 28)
                return false;

            var slotItem = koInv.m_pMyInvWnd[_pendingCountableInvPos];
            if (slotItem == null || slotItem.IsEmpty)
                return false;

            var srcItem = slotItem.serverData;
            if (srcItem == null)
                return false;

            if (count > srcItem.StackCount)
                return false;

            _pendingCountableBackupCount = count;

            if (MySlots[_pendingCountableTradeSlot] == null || MySlots[_pendingCountableTradeSlot].ItemId == 0)
            {
                MySlots[_pendingCountableTradeSlot] = new TradeSlotItem
                {
                    ItemId = _pendingCountableItemId,
                    Count = count,
                    Durability = srcItem.Durability,
                    OriginalInvSlot = _pendingCountableInvPos
                };
            }
            else
            {
                MySlots[_pendingCountableTradeSlot].Count += count;
            }

            // Decrease/remove from inventory immediately
            if (slotItem.count <= count)
            {
                koInv.m_pMyInvWnd[_pendingCountableInvPos] = null;
            }
            else
            {
                slotItem.count -= count;
                if (slotItem.serverData != null)
                {
                    slotItem.serverData.StackCount = (short)slotItem.count;
                }
            }
            KOUIManager.Instance?.RefreshInventoryUI();

            _pendingAddItemId = _pendingCountableItemId;
            _pendingAddCount = count;
            _pendingAddInvPos = (byte)_pendingCountableInvPos;
            _pendingAddDurability = srcItem.Durability;

            SendExchangeAddItem((byte)_pendingCountableInvPos, _pendingCountableItemId, count);

            return true;
        }

        /// <summary>
        /// Open-KO birebir: UIPerTradeDlg::ItemCountCancel() (UIPerTradeDlg.cpp:477-516)
        /// Adet popup iptal — count=0 olan trade slot'ları temizle.
        /// </summary>
        public void CountableItemCountCancel()
        {
            // C++ satır 483-507: count==0 olan countable trade slot'ları temizle
            for (int i = 0; i < MAX_ITEM_PER_TRADE; i++)
            {
                if (MySlots[i] != null && MySlots[i].Count == 0)
                {
                    MySlots[i] = null;
                }
            }

            // C++ satır 511-513: temizle
            _pendingCountableInvPos = -1;
            _pendingCountableTradeSlot = -1;

            // C++ satır 515: s_pCountableItemEdit->Close()
            OnItemCountEditCancelled?.Invoke();
        }

        /// <summary>
        /// LEGACY wrapper — eski SendExchangeAddGold arayüzü (ItemCountEditOK'a delegasyon).
        /// Open-KO birebir: SubProcPerTrade.cpp:530-541
        /// </summary>
        public void SendExchangeAddGold(int amount)
        {
            if (State != PerTradeState.Normal && State != PerTradeState.Editing)
            {
                Debug.LogWarning("[TRADE] Gold eklenemez — takas normal durumda değil.");
                return;
            }

            // Editing state'e geçirip onay akışına yönlendir
            if (State == PerTradeState.Normal)
                State = PerTradeState.Editing;

            ItemCountEditOK(amount);
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp item drag-drop case
        /// Takasa item ekle.
        /// Wire: [C2S_TRADE_ADD_ITEM] [invPos: byte] [itemId: int] [count: int]
        /// </summary>
        public void SendExchangeAddItem(byte invPos, int itemId, int count)
        {
            if (State != PerTradeState.Normal && State != PerTradeState.Editing)
            {
                Debug.LogWarning("[TRADE] Item eklenemez — takas normal durumda değil.");
                return;
            }

            _pendingAddItemId = itemId;
            _pendingAddCount = count;
            _pendingAddInvPos = invPos;

            // C++ birebir: spItem->iDurability — envanterdeki item'ın durability'sini oku
            _pendingAddDurability = 0;
            var koInv = EntropyOnline.UI.KOInventory.Instance;
            var slotItem = (koInv != null && invPos < 28) ? koInv.m_pMyInvWnd[invPos] : null;
            if (slotItem != null && !slotItem.IsEmpty && slotItem.serverData != null)
                _pendingAddDurability = (short)slotItem.serverData.Durability;

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_EXCHANGE);
            pkt.WriteByte(EXCHANGE_ADD);
            pkt.WriteByte(invPos);
            pkt.WriteInt32(itemId);
            pkt.WriteInt32(count);
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:567-586 — PerTradeMyDecision
        /// Takası onayla (kilit).
        /// Wire: [C2S_TRADE_CONFIRM]
        /// </summary>
        public void SendExchangeDecide()
        {
            if (State != PerTradeState.Normal)
            {
                Debug.LogWarning("[TRADE] Onaylanamaz — takas normal durumda değil.");
                return;
            }

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_EXCHANGE);
            pkt.WriteByte(EXCHANGE_DECIDE);
            KONetworkManager.Instance?.SendPacket(pkt);

            // Open-KO: SecureJobStuffByMyDecision() — SubProcPerTrade.cpp:588-598
            State = PerTradeState.MyDecisionDone;

            // UI'a bildir — onay butonu devre dışı
            OnMyDecisionDone?.Invoke();
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:389-398 — LeavePerTradeState(PER_TRADE_RESULT_MY_CANCEL)
        /// Takası iptal et.
        /// C++: WIZ_EXCHANGE + EXCHANGE_CANCEL (0x08) gönderir.
        /// Wire: [C2S_TRADE_CANCEL] (parametresiz)
        /// </summary>
        public void SendExchangeCancel()
        {
            if (State == PerTradeState.None) return;

            // Open-KO birebir: WIZ_EXCHANGE + EXCHANGE_CANCEL
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_EXCHANGE);
            pkt.WriteByte(EXCHANGE_CANCEL);
            KONetworkManager.Instance?.SendPacket(pkt);

            PerTradeCompleteCancel();
            FinalizePerTrade();
        }

        // ================================================
        // S→C MESAJ ALIMI (Open-KO birebir: MsgRecv_PerTrade dispatch)
        // ================================================

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:609-612 — ReceiveMsgPerTradeReq
        /// Birisi bana takas isteği gönderdi.
        /// </summary>
        private void ReceiveMsgPerTradeReq(short requesterId)
        {
            // Auto-decline if Block Trade Requests option is active
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.Block_TradeRequests)
            {
                SendExchangeDisagree();
                if (KOUIManager.Instance != null)
                {
                    string requesterName = "";
                    if (EntropyOnline.World.EntityManager.Instance != null)
                    {
                        requesterName = EntropyOnline.World.EntityManager.Instance.GetEntityName(requesterId) ?? $"Player_{requesterId}";
                    }
                    else
                    {
                        requesterName = $"Player_{requesterId}";
                    }

                    string rawMsg = Services.StringTableService.Get(6070); // "%s takas teklifini reddetti."
                    string formattedMsg = rawMsg.Replace("%s", requesterName);
                    KOUIManager.Instance.AddMsgOutput(formattedMsg, KOUIManager.D3DColorToUnity(0xffffff00));
                }
                return;
            }

            OtherId = requesterId;
            State = PerTradeState.WaitForMyDecision;

            // Open-KO: SecureCodeBegin()
            SecureCodeBegin();

            // UI'a bildir — "X kişisi takas istiyor, kabul et?"
            OnTradeRequestReceived?.Invoke(requesterId);
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:614-629 — ReceiveMsgPerTradeAgree
        /// Karşı taraf kabul/ret etti.
        /// </summary>
        private void ReceiveMsgPerTradeAgree(short result)
        {
            if (result == 0x01)
            {
                // Open-KO: ProcessProceed(PER_TRADE_RESULT_OTHER_AGREE) — satır 444-450
                PerTradeCoreStart();
            }
            else
            {
                // Open-KO: LeavePerTradeState(PER_TRADE_RESULT_OTHER_DISAGREE) — satır 412-418
                FinalizePerTrade();
                OnTradeRejected?.Invoke();
            }
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:631-793 — ReceiveMsgPerTradeAdd
        /// Kendi ekleme sonucu (başarı/başarısız).
        /// </summary>
        private void ReceiveMsgPerTradeAdd(byte result)
        {
            if (result == 0x01)
            {
                OnMyAddSuccess?.Invoke();
            }
            else
            {
                switch (_perTradeItemKindBackup)
                {
                    case PerTradeItemKind.Money:
                    {
                        if (_goldOffsetBackup > 0 && GameManager.Instance != null)
                            GameManager.Instance.Gold += _goldOffsetBackup;

                        MyTradeGold -= _goldOffsetBackup;
                        if (MyTradeGold < 0) MyTradeGold = 0;

                        break;
                    }
                    case PerTradeItemKind.Other:
                    {
                        int tradeSlot = _pendingCountableTradeSlot >= 0
                            ? _pendingCountableTradeSlot
                            : FindMySlotByItemId(_pendingAddItemId);

                        if (tradeSlot >= 0 && tradeSlot < MAX_ITEM_PER_TRADE && MySlots[tradeSlot] != null)
                        {
                            var tradeItem = MySlots[tradeSlot];
                            RestoreTradedItemToInventory(tradeItem);
                            MySlots[tradeSlot] = null;
                            KOUIManager.Instance?.RefreshInventoryUI();
                        }
                        break;
                    }
                }

                OnMyAddFail?.Invoke(_goldOffsetBackup);
                _goldOffsetBackup = 0;
            }

            _pendingAddItemId = 0;
            _pendingAddCount = 0;
            _pendingAddDurability = 0;
            _pendingAddInvPos = 0;
            _pendingCountableInvPos = -1;
            _pendingCountableTradeSlot = -1;
            _pendingCountableBackupCount = 0;
        }

        private void RestoreTradedItemToInventory(TradeSlotItem tradeItem)
        {
            if (tradeItem == null || tradeItem.ItemId <= 0 || tradeItem.OriginalInvSlot < 0 || tradeItem.OriginalInvSlot >= 28) return;

            var koInv = EntropyOnline.UI.KOInventory.Instance;
            if (koInv == null) return;

            var existing = koInv.m_pMyInvWnd[tradeItem.OriginalInvSlot];
            if (existing != null && !existing.IsEmpty && existing.itemId == tradeItem.ItemId)
            {
                existing.count += tradeItem.Count;
                if (existing.count > 9999) existing.count = 9999;
                if (existing.serverData != null)
                    existing.serverData.StackCount = (short)existing.count;
            }
            else
            {
                KOTableReader.TableItemBasic basic = null;
                if (KOInventory.s_pTbl_Items_Basic != null)
                    KOInventory.s_pTbl_Items_Basic.TryGetValue((uint)tradeItem.ItemId / 1000 * 1000, out basic);

                koInv.m_pMyInvWnd[tradeItem.OriginalInvSlot] = new KOInventory.ItemSlot
                {
                    itemId = tradeItem.ItemId,
                    count = tradeItem.Count,
                    durability = tradeItem.Durability,
                    pItemBasic = basic,
                    iconFN = basic?.dwIDIcon.ToString()
                };
                koInv.m_pMyInvWnd[tradeItem.OriginalInvSlot].serverData = new InventoryItemData
                {
                    ItemDefId = tradeItem.ItemId,
                    StackCount = (short)tradeItem.Count,
                    Durability = (short)tradeItem.Durability,
                    SlotType = 0,
                    SlotIndex = (byte)tradeItem.OriginalInvSlot,
                    IconId = koInv.m_pMyInvWnd[tradeItem.OriginalInvSlot].iconFN ?? "",
                    AttachPoint = (byte)koInv.m_pMyInvWnd[tradeItem.OriginalInvSlot].attachPoint,
                    Type = (byte)koInv.m_pMyInvWnd[tradeItem.OriginalInvSlot].itemClass
                };
            }
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:796-957 — ReceiveMsgPerTradeOtherAdd
        /// Karşı taraf item/gold ekledi — slot dizisinde takip et.
        /// </summary>
        private void ReceiveMsgPerTradeOtherAdd(int itemId, int count, short durability)
        {
            // Open-KO birebir: SubProcPerTrade.cpp:801-813 — gold case
            if (itemId == ITEM_GOLD)
            {
                OtherTradeGold += count;
            }
            else
            {
                // Open-KO birebir: SubProcPerTrade.cpp:832-955 — item case
                // Countable item: mevcut slot'ta aynı item varsa count ekle
                int destSlot = -1;
                for (int i = 0; i < MAX_ITEM_PER_TRADE; i++)
                {
                    if (OtherSlots[i] != null && OtherSlots[i].ItemId == itemId)
                    {
                        OtherSlots[i].Count += count;
                        destSlot = i;
                        break;
                    }
                }

                // Bulamazsa boş slot'a ekle
                if (destSlot < 0)
                {
                    for (int i = 0; i < MAX_ITEM_PER_TRADE; i++)
                    {
                        if (OtherSlots[i] == null)
                        {
                            OtherSlots[i] = new TradeSlotItem
                            {
                                ItemId = itemId,
                                Count = count,
                                Durability = durability
                            };
                            destSlot = i;
                            break;
                        }
                    }
                }

                if (destSlot < 0)
                {
                    Debug.LogWarning("[TRADE] OtherAdd: boş slot bulunamadı!");
                    return;
                }
            }

            OnOtherItemAdded?.Invoke(itemId, count, durability);
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:600-605 — PerTradeOtherDecision
        /// Karşı taraf onayladı — UI'da karşı tarafın onay butonu disable.
        /// </summary>
        private void ReceiveMsgPerTradeOtherDecide()
        {
            OnOtherDecisionDone?.Invoke();
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_PerTrade EXCHANGE_DONE case
        /// Takas tamamlandı veya başarısız oldu.
        /// </summary>
        private void ReceiveMsgPerTradeDone(bool success, int gold, TradeResultItem[] items)
        {
            if (success)
            {
                // Open-KO: PerTradeCompleteSuccess() — satır 223-262
                PerTradeCompleteSuccess(gold, items);
            }
            else
            {
                // Open-KO: PerTradeCompleteCancel() — satır 264-376
                PerTradeCompleteCancel();
            }

            FinalizePerTrade();
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_PerTrade EXCHANGE_CANCEL case
        /// Karşı taraf veya sunucu iptal etti.
        /// </summary>
        private void ReceiveMsgPerTradeCancel()
        {
            PerTradeCompleteCancel();
            FinalizePerTrade();
        }

        // ================================================
        // HELPERS
        // ================================================

        /// <summary>MySlots'ta belirtilen itemId'ye sahip slot indeksini bul.</summary>
        private int FindMySlotByItemId(int itemId)
        {
            for (int i = 0; i < MAX_ITEM_PER_TRADE; i++)
            {
                if (MySlots[i] != null && MySlots[i].ItemId == itemId)
                    return i;
            }
            return -1;
        }

        // ================================================
        // INTERNAL STATE MANAGEMENT
        // ================================================

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:165-202 — SecureCodeBegin
        /// Takas başladığında güvenlik önlemleri.
        /// </summary>
        private void SecureCodeBegin()
        {
            // Open-KO:
            // 1. NPC mağaza açıksa kapat
            // 2. Hareket ediyorsa durdur
            // 3. İcon manager pencerelerini kapat
            // 4. Input kilitle
            // 5. Takas panelindeki değerleri sıfırla
            // 6. Onay butonlarını normal yap
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:456-465 — PerTradeCoreStart
        /// Takas penceresi aç, state=Normal.
        /// </summary>
        private void PerTradeCoreStart()
        {
            State = PerTradeState.Normal;

            // Slot dizilerini sıfırla
            ResetSlots();

            OnTradeWindowOpen?.Invoke();
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:964-1070
        /// DoneSuccessBegin (gold set) + DoneItemMove (envanter slot güncelleme) + DoneSuccessEnd
        /// </summary>
        private void PerTradeCompleteSuccess(int totalGold, TradeResultItem[] items)
        {
            // Open-KO birebir: SubProcPerTrade.cpp:964-968 — DoneSuccessBegin
            // s_pPlayer->m_InfoExt.iGold = iTotalGold
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Gold = totalGold;
            }

            // Open-KO birebir: SubProcPerTrade.cpp:970-1063 — DoneItemMove
            // Her item için envanterdeki bItemPos slotuna yerleştir
            var koInv = EntropyOnline.UI.KOInventory.Instance;
            if (koInv != null)
            {
                // 2. Add received items
                foreach (var resultItem in items)
                {
                    byte pos = resultItem.Pos;
                    if (pos >= 28) continue;

                    var existingSlot = koInv.m_pMyInvWnd[pos];
                    if (existingSlot != null && !existingSlot.IsEmpty && existingSlot.itemId == resultItem.ItemId)
                    {
                        // Aynı item — countable: count ekle
                        existingSlot.count += resultItem.Count;
                        if (existingSlot.count > 9999) existingSlot.count = 9999;
                        if (existingSlot.serverData != null)
                            existingSlot.serverData.StackCount = (short)existingSlot.count;
                    }
                    else
                    {
                        // Yeni item veya farklı item — üzerine yaz
                        KOTableReader.TableItemBasic basic = null;
                        if (KOInventory.s_pTbl_Items_Basic != null)
                            KOInventory.s_pTbl_Items_Basic.TryGetValue((uint)resultItem.ItemId / 1000 * 1000, out basic);

                        koInv.m_pMyInvWnd[pos] = new KOInventory.ItemSlot
                        {
                            itemId = resultItem.ItemId,
                            count = resultItem.Count,
                            durability = resultItem.Durability,
                            pItemBasic = basic,
                            iconFN = basic?.dwIDIcon.ToString()
                        };
                        koInv.m_pMyInvWnd[pos].serverData = new InventoryItemData
                        {
                            ItemDefId = resultItem.ItemId,
                            StackCount = (short)resultItem.Count,
                            Durability = (short)resultItem.Durability,
                            SlotType = 0,
                            SlotIndex = pos,
                            IconId = koInv.m_pMyInvWnd[pos].iconFN ?? "",
                            AttachPoint = (byte)koInv.m_pMyInvWnd[pos].attachPoint,
                            Type = (byte)koInv.m_pMyInvWnd[pos].itemClass
                        };
                    }
                }
                // Refresh both the UI and GameManager's cache
                KOUIManager.Instance?.RefreshInventoryUI();
            }

            OnTradeSuccess?.Invoke(totalGold, items);
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:264-376 — PerTradeCompleteCancel
        /// Takas iptal — gold ve item'ları envantere geri al.
        /// C++ client: gold'u m_InfoExt.iGold'a geri ekler, item'ları da envanter slotlarına geri koyar.
        /// Sunucu zaten ExchangeCancel ile gold/item'ı geri verdi — burada sadece client cache'i güncelle.
        /// </summary>
        private void PerTradeCompleteCancel()
        {
            if ((int)State >= (int)PerTradeState.Normal && GameManager.Instance != null)
            {
                // Open-KO birebir: SubProcPerTrade.cpp:272-283 — gold geri ver
                GameManager.Instance.Gold += MyTradeGold;

                // Restore all items from MySlots back to inventory
                var koInv = EntropyOnline.UI.KOInventory.Instance;
                if (koInv != null)
                {
                    for (int i = 0; i < MAX_ITEM_PER_TRADE; i++)
                    {
                        var tradeItem = MySlots[i];
                        if (tradeItem != null)
                        {
                            RestoreTradedItemToInventory(tradeItem);
                        }
                    }
                    KOUIManager.Instance?.RefreshInventoryUI();
                }
            }

            OnTradeCancelled?.Invoke();
        }

        /// <summary>
        /// Open-KO birebir: SubProcPerTrade.cpp:206-221 — FinalizePerTrade
        /// State temizle, pencereyi kapat.
        /// </summary>
        private void FinalizePerTrade()
        {
            State = PerTradeState.None;
            OtherId = -1;
            _goldOffsetBackup = 0;

            // Slot/gold temizle
            ResetSlots();

            OnTradeWindowClose?.Invoke();
        }

        /// <summary>Slot dizilerini ve gold'ları sıfırla.</summary>
        private void ResetSlots()
        {
            for (int i = 0; i < MAX_ITEM_PER_TRADE; i++)
            {
                MySlots[i] = null;
                OtherSlots[i] = null;
            }
            MyTradeGold = 0;
            OtherTradeGold = 0;
        }

        // ================================================
        // PUBLIC QUERIES
        // ================================================

        public bool IsInTrade => State != PerTradeState.None;

        // ================================================
        // EVENTS (UI bağlama için)
        // ================================================

        /// <summary>Takas isteği alındı — UI: "X kişisi takas istiyor" popup göster.</summary>
        public event System.Action<short> OnTradeRequestReceived;

        /// <summary>Karşı taraf reddetti — UI: mesaj göster.</summary>
        public event System.Action OnTradeRejected;

        /// <summary>Takas penceresi açılmalı.</summary>
        public event System.Action OnTradeWindowOpen;

        /// <summary>Takas penceresi kapanmalı.</summary>
        public event System.Action OnTradeWindowClose;

        /// <summary>Kendi item ekleme başarılı — UI: item'ı "benim" panelde göster.</summary>
        public event System.Action OnMyAddSuccess;

        /// <summary>Kendi item ekleme başarısız — UI: gold/item geri al. Param: goldOffset.</summary>
        public event System.Action<int> OnMyAddFail;

        /// <summary>Karşı taraf item ekledi — UI: karşı panelde göster.</summary>
        public event System.Action<int, int, short> OnOtherItemAdded;

        /// <summary>Ben onayladım — UI: benim onay butonu disable.</summary>
        public event System.Action OnMyDecisionDone;

        /// <summary>Karşı taraf onayladı — UI: karşının onay butonu disable.</summary>
        public event System.Action OnOtherDecisionDone;

        /// <summary>Takas başarılı — UI: envanter yenile. Param: gold, items.</summary>
        public event System.Action<int, TradeResultItem[]> OnTradeSuccess;

        /// <summary>Takas iptal — UI: gold/item geri yükle.</summary>
        public event System.Action OnTradeCancelled;

        /// <summary>Gold edit penceresi açılmalı — Open-KO: UITradeEditDlg::Open(true)</summary>
        public event System.Action OnGoldEditOpen;

        /// <summary>Gold edit onaylandı — miktar. UI: gold göstergelerini güncelle.</summary>
        public event System.Action<int> OnGoldEditConfirmed;

        /// <summary>Gold edit iptal edildi — UI: pencere kapat.</summary>
        public event System.Action OnGoldEditCancelled;

        /// <summary>
        /// Countable item adet popup açılmalı — Open-KO: s_pCountableItemEdit->Open(false)
        /// Param: maxCount (envanterdeki stack miktarı)
        /// </summary>
        public event System.Action<int> OnItemCountEditOpen;

        /// <summary>Countable item adet popup iptal — Open-KO: UIPerTradeDlg::ItemCountCancel()</summary>
        public event System.Action OnItemCountEditCancelled;
    }

    /// <summary>
    /// Open-KO birebir: __IconItemSkill karşılığı — trade slotundaki item bilgisi.
    /// Hem MySlots hem OtherSlots dizilerinde kullanılır.
    /// </summary>
    public class TradeSlotItem
    {
        public int ItemId;
        public int Count;
        public short Durability;

        /// <summary>İtem'ın geldiği envanter slot indeksi (benim slotlar için).</summary>
        public int OriginalInvSlot = -1;
    }
}
