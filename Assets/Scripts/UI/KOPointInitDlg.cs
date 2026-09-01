using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Network.KO;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUIPointInitDlg (UIPointInitDlg.h/cpp)
    /// UIF: co_change_bill_us.uif / prefab: co_change_bill_us.prefab
    /// </summary>
    public class KOPointInitDlg : MonoBehaviour
    {
        // C++: m_pBtn_Ok, m_pBtn_Cancel, m_pText_NeedGold
        private Button _btnOk;
        private Button _btnCancel;
        private Text _textNeedGold;

        // C++: m_bAllpoint
        private bool _bAllpoint;

        /// <summary>
        /// C++: Load() — UI elemanlarını bağlar
        /// </summary>
        public void Init()
        {
            _btnOk = KOUIRenderer.FindChildButton(transform, "btn_ok");
            _btnCancel = KOUIRenderer.FindChildButton(transform, "btn_cancel");
            _textNeedGold = KOUIRenderer.FindChildText(transform, "string_gold");

            if (_btnOk != null)
            {
                _btnOk.onClick.RemoveAllListeners();
                _btnOk.onClick.AddListener(OnOkClick);
            }

            if (_btnCancel != null)
            {
                _btnCancel.onClick.RemoveAllListeners();
                _btnCancel.onClick.AddListener(Close);
            }
        }

        /// <summary>
        /// C++: InitDlg(bool bAllpoint, int iGold)
        /// </summary>
        public void InitDlg(bool bAllpoint, int iGold)
        {
            _bAllpoint = bAllpoint;

            if (_textNeedGold != null)
            {
                _textNeedGold.text = iGold.ToString("N0");
            }
        }

        private void OnOkClick()
        {
            Close();
            PushOkButton();
        }

        /// <summary>
        /// C++: Close()
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.ClosePointInit();
            }
        }

        /// <summary>
        /// C++: PushOkButton()
        /// Sends resetting packet to the server.
        /// </summary>
        public void PushOkButton()
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_CLASS_CHANGE);
                if (_bAllpoint)
                {
                    pkt.WriteByte(0x03); // CLASS_RESET_STAT_REQ
                }
                else
                {
                    pkt.WriteByte(0x04); // CLASS_RESET_SKILL_REQ
                }
                netMgr.SendPacket(pkt);
            }
        }
    }
}
