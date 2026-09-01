using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using EntropyOnline.Network.KO;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUITradeBBSEditDlg (UITradeBBSEditDlg.h/cpp)
    /// UIF: co_saleboardmemo_us.uif
    /// </summary>
    public class KOTradeBBSEditDlg : MonoBehaviour
    {
        public static KOTradeBBSEditDlg Instance { get; private set; }

        private InputField _editTitle;
        private InputField _editPrice;
        private InputField _editExplanation;
        private Button _btnOk;
        private Button _btnCancel;

        public byte BBSKind { get; set; } = TradeBBSKind.N3_SP_TRADE_BBS_SELL;

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

            // C++ birebir: edit_name, edit_price, edit_memo
            var editNameObj = KOUIRenderer.FindChildByID(t, "edit_name");
            if (editNameObj != null)
            {
                _editTitle = editNameObj.GetComponent<InputField>();
                if (_editTitle == null)
                {
                    _editTitle = editNameObj.gameObject.AddComponent<InputField>();
                    var txt = KOUIRenderer.FindChildText(editNameObj, "");
                    _editTitle.textComponent = txt;
                }
                _editTitle.characterLimit = 40;
            }

            var editPriceObj = KOUIRenderer.FindChildByID(t, "edit_price");
            if (editPriceObj != null)
            {
                _editPrice = editPriceObj.GetComponent<InputField>();
                if (_editPrice == null)
                {
                    _editPrice = editPriceObj.gameObject.AddComponent<InputField>();
                    var txt = KOUIRenderer.FindChildText(editPriceObj, "");
                    _editPrice.textComponent = txt;
                }
                _editPrice.contentType = InputField.ContentType.IntegerNumber;
                _editPrice.characterLimit = 10;
            }

            var editMemoObj = KOUIRenderer.FindChildByID(t, "edit_memo");
            if (editMemoObj != null)
            {
                _editExplanation = editMemoObj.GetComponent<InputField>();
                if (_editExplanation == null)
                {
                    _editExplanation = editMemoObj.gameObject.AddComponent<InputField>();
                    var txt = KOUIRenderer.FindChildText(editMemoObj, "");
                    _editExplanation.textComponent = txt;
                }
                _editExplanation.characterLimit = 120;
            }

            // Buttons: btn_ok, btn_cancel
            _btnOk = KOUIRenderer.FindChildButton(t, "btn_ok");
            _btnCancel = KOUIRenderer.FindChildButton(t, "btn_cancel");

            if (_btnOk != null) _btnOk.onClick.AddListener(OnBtnOkClick);
            if (_btnCancel != null) _btnCancel.onClick.AddListener(() => SetVisible(false));
        }

        public void Show(byte bbsKind)
        {
            BBSKind = bbsKind;
            if (_editTitle != null) _editTitle.text = "";
            if (_editPrice != null) _editPrice.text = "";
            if (_editExplanation != null) _editExplanation.text = "";
            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (!visible)
            {
                if (_editTitle != null) _editTitle.DeactivateInputField();
                if (_editPrice != null) _editPrice.DeactivateInputField();
                if (_editExplanation != null) _editExplanation.DeactivateInputField();
            }
        }

        private void OnBtnOkClick()
        {
            string title = _editTitle != null ? _editTitle.text : "";
            string priceStr = _editPrice != null ? _editPrice.text : "";
            string memo = _editExplanation != null ? _editExplanation.text : "";

            int price = 0;
            int.TryParse(priceStr, out price);

            if (string.IsNullOrWhiteSpace(title))
            {
                KOUIManager.Instance?.AddMsgOutput("Title cannot be empty.", KOUIManager.D3DColorToUnity(0xffff0000));
                return;
            }

            MsgSend_Register(title, memo, price);
            SetVisible(false);
        }

        /// <summary>
        /// C++ birebir: MsgSend_Register (UITradeSellBBS.cpp:379-413)
        /// Wire: WIZ_MARKET_BBS [N3_SP_TYPE_REGISTER=0x01] [byBBSKind:byte] [title_len:int16] [title:str] [explanation_len:int16] [explanation:str] [price:uint32]
        /// </summary>
        private void MsgSend_Register(string title, string explanation, int price)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS);
                pkt.WriteByte(TradeBBSSub.N3_SP_TYPE_REGISTER);
                pkt.WriteByte(BBSKind);
                pkt.WriteKOString(title);
                pkt.WriteKOString(explanation);
                pkt.WriteInt32(price);
                netMgr.SendPacket(pkt);
            }
        }
    }
}
