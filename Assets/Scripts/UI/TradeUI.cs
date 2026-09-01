using UnityEngine;
using EntropyOnline.Network;
using EntropyOnline.Trade;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUIPerTradeDlg (UIPerTradeDlg.cpp) — UI katmanı.
    /// KOTradeManager event'lerine subscribe olarak takas penceresi yönetir.
    /// 
    /// NOT: Gerçek UI widget'ları (slot, gold text, onay butonları) henüz
    /// KOUIManager.ShowPersonalTrade() tarafından yönetiliyor. Bu sınıf
    /// KOTradeManager ile UI arasındaki köprüdür.
    /// </summary>
    public class TradeUI : MonoBehaviour
    {
        public static TradeUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            if (KOTradeManager.Instance == null) return;

            KOTradeManager.Instance.OnTradeRequestReceived += HandleTradeRequest;
            KOTradeManager.Instance.OnTradeRejected += HandleTradeRejected;
            KOTradeManager.Instance.OnTradeWindowOpen += HandleTradeWindowOpen;
            KOTradeManager.Instance.OnTradeWindowClose += HandleTradeWindowClose;
            KOTradeManager.Instance.OnMyAddSuccess += HandleMyAddSuccess;
            KOTradeManager.Instance.OnMyAddFail += HandleMyAddFail;
            KOTradeManager.Instance.OnOtherItemAdded += HandleOtherItemAdded;
            KOTradeManager.Instance.OnMyDecisionDone += HandleMyDecisionDone;
            KOTradeManager.Instance.OnOtherDecisionDone += HandleOtherDecisionDone;
            KOTradeManager.Instance.OnTradeSuccess += HandleTradeSuccess;
            KOTradeManager.Instance.OnTradeCancelled += HandleTradeCancelled;
            KOTradeManager.Instance.OnGoldEditOpen += HandleGoldEditOpen;
            KOTradeManager.Instance.OnGoldEditConfirmed += HandleGoldEditConfirmed;
            KOTradeManager.Instance.OnGoldEditCancelled += HandleGoldEditCancelled;
            KOTradeManager.Instance.OnItemCountEditOpen += HandleItemCountEditOpen;
            KOTradeManager.Instance.OnItemCountEditCancelled += HandleItemCountEditCancelled;
        }

        private void OnDisable()
        {
            if (KOTradeManager.Instance == null) return;

            KOTradeManager.Instance.OnTradeRequestReceived -= HandleTradeRequest;
            KOTradeManager.Instance.OnTradeRejected -= HandleTradeRejected;
            KOTradeManager.Instance.OnTradeWindowOpen -= HandleTradeWindowOpen;
            KOTradeManager.Instance.OnTradeWindowClose -= HandleTradeWindowClose;
            KOTradeManager.Instance.OnMyAddSuccess -= HandleMyAddSuccess;
            KOTradeManager.Instance.OnMyAddFail -= HandleMyAddFail;
            KOTradeManager.Instance.OnOtherItemAdded -= HandleOtherItemAdded;
            KOTradeManager.Instance.OnMyDecisionDone -= HandleMyDecisionDone;
            KOTradeManager.Instance.OnOtherDecisionDone -= HandleOtherDecisionDone;
            KOTradeManager.Instance.OnTradeSuccess -= HandleTradeSuccess;
            KOTradeManager.Instance.OnTradeCancelled -= HandleTradeCancelled;
            KOTradeManager.Instance.OnGoldEditOpen -= HandleGoldEditOpen;
            KOTradeManager.Instance.OnGoldEditConfirmed -= HandleGoldEditConfirmed;
            KOTradeManager.Instance.OnGoldEditCancelled -= HandleGoldEditCancelled;
            KOTradeManager.Instance.OnItemCountEditOpen -= HandleItemCountEditOpen;
            KOTradeManager.Instance.OnItemCountEditCancelled -= HandleItemCountEditCancelled;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================
        // EVENT HANDLERS
        // ================================================

        /// <summary>
        /// Open-KO: EnterWaitMyDecisionToPerTrade — MessageBox "X kişisi takas istiyor"
        /// </summary>
        private void HandleTradeRequest(short requesterId)
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

            string szMsg = $"{requesterName} has requested a trade. Do you accept?";

            if (KOMessageBox.Instance != null)
            {
                KOMessageBox.Instance.ShowYesNo(
                    szMsg, "",
                    MsgBoxBehavior.BEHAVIOR_PERSONAL_TRADE_PERMIT,
                    onYes: () =>
                    {
                        // Open-KO birebir: UIMessageBox.cpp:144-146
                        // case BEHAVIOR_PERSONAL_TRADE_PERMIT:
                        //   pProcMain->m_pSubProcPerTrade->ProcessProceed(PER_TRADE_RESULT_MY_AGREE);
                        KOTradeManager.Instance?.SendExchangeAgree();
                    },
                    onNo: () =>
                    {
                        // Open-KO birebir: UIMessageBox.cpp:193-195
                        // case BEHAVIOR_PERSONAL_TRADE_PERMIT:
                        //   pProcMain->m_pSubProcPerTrade->LeavePerTradeState(PER_TRADE_RESULT_MY_DISAGREE);
                        KOTradeManager.Instance?.SendExchangeDisagree();
                    },
                    countdownDuration: 30,
                    forceFixedCenter: true,
                    autoClickOnTimeout: true
                );
            }
            else
            {
                // Fallback: KOMessageBox yoksa otomatik kabul (C++ zaten dialog zorunlu)
                Debug.LogWarning("[TRADE-UI] KOMessageBox yüklenmemiş — otomatik kabul");
                KOTradeManager.Instance?.SendExchangeAgree();
            }
        }

        /// <summary>Open-KO: LeavePerTradeState(PER_TRADE_RESULT_OTHER_DISAGREE)</summary>
        private void HandleTradeRejected()
        {
            // C++ birebir: SubProcPerTrade.cpp:412-418 — LeavePerTradeState(PER_TRADE_RESULT_OTHER_DISAGREE)
            // MsgOutput(IDS_OTHER_PER_TRADE_ID_NO, 0xffff3b3b)
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.AddMsgOutput("Trade request refused.",
                    KOUIManager.D3DColorToUnity(0xffff3b3b));
        }

        /// <summary>Open-KO: PerTradeCoreStart — takas penceresi aç</summary>
        private void HandleTradeWindowOpen()
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowPersonalTrade(true);
        }

        /// <summary>Open-KO: FinalizePerTrade — takas penceresi kapat</summary>
        private void HandleTradeWindowClose()
        {
            if (KOUIManager.Instance != null)
            {
                // C++ SubProcPerTrade.cpp:216-220: FinalizePerTrade → m_pUITradeEditDlg->Close()
                KOUIManager.Instance.HideCountableEdit();
                KOUIManager.Instance.ShowPersonalTrade(false);
            }
            else
            {
                Debug.LogWarning("[TRADE-UI] KOUIManager.Instance is null!");
            }
        }

        /// <summary>Open-KO: ReceiveMsgPerTradeAdd success</summary>
        private void HandleMyAddSuccess()
        {
            RefreshMyPanel();
        }

        /// <summary>Open-KO: ReceiveMsgPerTradeAdd fail — gold/item geri al</summary>
        private void HandleMyAddFail(int goldOffset)
        {
            // Sunucu gold'u geri almadı — client cache'i gold zaten düşürülmemişti
            // (sunucu reddetti = gold düşürülmedi)
        }

        /// <summary>Open-KO: MsgRecv_PerTrade EXCHANGE_OTHERADD — karşı taraf item ekledi</summary>
        private void HandleOtherItemAdded(int itemId, int count, short durability)
        {
            RefreshOtherPanel();
        }

        /// <summary>Open-KO: SecureJobStuffByMyDecision — benim onay butonu disable</summary>
        private void HandleMyDecisionDone()
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.SetTradeConfirmButtonState(isMine: true, enabled: false);
        }

        /// <summary>Open-KO: PerTradeOtherDecision — karşının onay butonu disable</summary>
        private void HandleOtherDecisionDone()
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.SetTradeConfirmButtonState(isMine: false, enabled: false);
        }

        /// <summary>Open-KO: PerTradeCompleteSuccess — takas başarılı</summary>
        private void HandleTradeSuccess(int gold, TradeResultItem[] items)
        {
            // Envanter UI'ı yenile — gold ve item'lar zaten KOTradeManager'da güncellendi
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.RefreshInventoryUI();
        }

        /// <summary>Open-KO: PerTradeCompleteCancel — takas iptal</summary>
        private void HandleTradeCancelled()
        {
            // Gold geri alındı (KOTradeManager'da) — envanter UI'ı yenile
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.RefreshInventoryUI();
                KOUIManager.Instance.HideCountableEdit();
            }
            else
            {
                Debug.LogWarning("[TRADE-UI] KOUIManager.Instance is null during Cancel!");
            }
        }

        // ================================================
        // GOLD EDIT EVENT HANDLERS
        // Open-KO birebir: SubProcPerTrade.cpp:480-563 UI aktarımı
        // ================================================

        /// <summary>
        /// Open-KO: SubProcPerTrade.cpp:486 — m_pUITradeEditDlg->Open(true)
        /// Gold edit penceresi aç.
        /// </summary>
        private void HandleGoldEditOpen()
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowCountableEdit(true);
        }

        /// <summary>
        /// Open-KO: SubProcPerTrade.cpp:551 — ItemCountEditOK sonrası
        /// Gold display güncelle + envanter gold güncelle.
        /// </summary>
        private void HandleGoldEditConfirmed(int goldAmount)
        {
            RefreshMyPanel();
        }

        /// <summary>
        /// Open-KO: SubProcPerTrade.cpp:562 — ItemCountEditCancel
        /// Gold edit iptal.
        /// </summary>
        private void HandleGoldEditCancelled()
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.HideCountableEdit();
        }

        /// <summary>
        /// Open-KO birebir: UIPerTradeDlg.cpp:667 — s_pCountableItemEdit->Open(false)
        /// Countable item adet popup açılıyor.
        /// </summary>
        private void HandleItemCountEditOpen(int maxCount)
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowItemCountPopup(maxCount);
        }

        /// <summary>
        /// Open-KO birebir: UIPerTradeDlg.cpp:477-516 — ItemCountCancel()
        /// Countable item adet popup iptal.
        /// </summary>
        private void HandleItemCountEditCancelled()
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.HideItemCountPopup();
        }

        // ================================================
        // PANEL REFRESH
        // Open-KO birebir: CUIPerTradeDlg panellerini günceller
        // ================================================

        /// <summary>
        /// Benim takas slotlarımı ve gold'umu KOUIManager'a yansıt.
        /// </summary>
        private void RefreshMyPanel()
        {
            if (KOTradeManager.Instance == null || KOUIManager.Instance == null) return;

            KOUIManager.Instance.UpdateTradeGoldDisplay(
                isMine: true, KOTradeManager.Instance.MyTradeGold);

            for (int i = 0; i < KOTradeManager.MAX_ITEM_PER_TRADE; i++)
            {
                var slot = KOTradeManager.Instance.MySlots[i];
                if (slot != null)
                    KOUIManager.Instance.UpdateTradeSlot(isMine: true, i, slot.ItemId, slot.Count, slot.Durability);
                else
                    KOUIManager.Instance.ClearTradeSlot(isMine: true, i);
            }
        }

        /// <summary>
        /// Karşının takas slotlarını ve gold'unu KOUIManager'a yansıt.
        /// </summary>
        private void RefreshOtherPanel()
        {
            if (KOTradeManager.Instance == null || KOUIManager.Instance == null) return;

            KOUIManager.Instance.UpdateTradeGoldDisplay(
                isMine: false, KOTradeManager.Instance.OtherTradeGold);

            for (int i = 0; i < KOTradeManager.MAX_ITEM_PER_TRADE; i++)
            {
                var slot = KOTradeManager.Instance.OtherSlots[i];
                if (slot != null)
                    KOUIManager.Instance.UpdateTradeSlot(isMine: false, i, slot.ItemId, slot.Count, slot.Durability);
                else
                    KOUIManager.Instance.ClearTradeSlot(isMine: false, i);
            }
        }

        // ================================================
        // PUBLIC API (UI butonlarından çağrılır)
        // ================================================

        /// <summary>Kabul butonuna basıldı.</summary>
        public void OnAcceptButtonClicked()
        {
            KOTradeManager.Instance?.SendExchangeAgree();
        }

        /// <summary>Ret butonuna basıldı.</summary>
        public void OnRejectButtonClicked()
        {
            KOTradeManager.Instance?.SendExchangeDisagree();
        }

        /// <summary>Onay (kilit) butonuna basıldı.</summary>
        public void OnConfirmButtonClicked()
        {
            KOTradeManager.Instance?.SendExchangeDecide();
        }

        /// <summary>İptal butonuna basıldı.</summary>
        public void OnCancelButtonClicked()
        {
            KOTradeManager.Instance?.SendExchangeCancel();
        }
    }
}
