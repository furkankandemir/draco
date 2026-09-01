using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using EntropyOnline.Network.KO;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUITradeBBSSelector (UITradeBBSSelector.h/cpp)
    /// UIF: co_saleboardselection_us.uif
    /// </summary>
    public class KOTradeBBSSelector : MonoBehaviour
    {
        public static KOTradeBBSSelector Instance { get; private set; }

        private Button _btnBBSSell;
        private Button _btnBBSBuy;
        private Button _btnBBSCancel;

        private void Awake()
        {
            Instance = this;
            BindElements();
        }

        private void Update()
        {
            // ESC ile kapat (C++ OnKeyPress DIK_ESCAPE birebir)
            if (gameObject.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetVisible(false);
            }
        }

        private void BindElements()
        {
            var t = transform;
            _btnBBSSell = KOUIRenderer.FindChildButton(t, "btn_sell");
            _btnBBSBuy = KOUIRenderer.FindChildButton(t, "btn_buy");
            _btnBBSCancel = KOUIRenderer.FindChildButton(t, "btn_cancel");

            if (_btnBBSSell != null)
                _btnBBSSell.onClick.AddListener(OnBtnSellClick);
            if (_btnBBSBuy != null)
                _btnBBSBuy.onClick.AddListener(OnBtnBuyClick);
            if (_btnBBSCancel != null)
                _btnBBSCancel.onClick.AddListener(() => SetVisible(false));
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void OnBtnSellClick()
        {
            MsgSend_OpenTradeSellBBS();
            SetVisible(false);
        }

        private void OnBtnBuyClick()
        {
            MsgSend_OpenTradeBuyBBS();
            SetVisible(false);
        }

        /// <summary>
        /// C++ birebir: MsgSend_OpenTradeSellBBS (UITradeBBSSelector.cpp:64-73)
        /// Wire: WIZ_MARKET_BBS [N3_SP_TYPE_BBS_OPEN=0x04] [N3_SP_TRADE_BBS_SELL=0x02]
        /// </summary>
        public void MsgSend_OpenTradeSellBBS()
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS);
                pkt.WriteByte(TradeBBSSub.N3_SP_TYPE_BBS_OPEN);
                pkt.WriteByte(TradeBBSKind.N3_SP_TRADE_BBS_SELL);
                netMgr.SendPacket(pkt);
            }
        }

        /// <summary>
        /// C++ birebir: MsgSend_OpenTradeBuyBBS (UITradeBBSSelector.cpp:75-84)
        /// Wire: WIZ_MARKET_BBS [N3_SP_TYPE_BBS_OPEN=0x04] [N3_SP_TRADE_BBS_BUY=0x01]
        /// </summary>
        public void MsgSend_OpenTradeBuyBBS()
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS);
                pkt.WriteByte(TradeBBSSub.N3_SP_TYPE_BBS_OPEN);
                pkt.WriteByte(TradeBBSKind.N3_SP_TRADE_BBS_BUY);
                netMgr.SendPacket(pkt);
            }
        }
    }
}
