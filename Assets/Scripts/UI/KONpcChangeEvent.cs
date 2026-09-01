using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Network.KO;
using EntropyOnline.Core;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUINPCChangeEvent (UINPCChangeEvent.h/cpp)
    /// UIF: co_change_us.uif / prefab: co_change_us.prefab
    /// </summary>
    public class KONpcChangeEvent : MonoBehaviour
    {
        // C++: m_pBtn_Repoint0, m_pBtn_Repoint1, m_pBtn_Close
        private Button _btnRepoint0;
        private Button _btnRepoint1;
        private Button _btnClose;

        // C++: m_bSendedAllPoint
        private bool _bSendedAllPoint;

        /// <summary>
        /// C++: Load() — UI elemanlarını bağlar
        /// </summary>
        public void Init()
        {
            _btnRepoint0 = KOUIRenderer.FindChildButton(transform, "Btn_repoint0");
            _btnRepoint1 = KOUIRenderer.FindChildButton(transform, "Btn_repoint1");
            _btnClose = KOUIRenderer.FindChildButton(transform, "btn_close"); // Prefab names are lowercase btn_close

            // Fallback lookup if not found
            if (_btnClose == null)
            {
                _btnClose = KOUIRenderer.FindChildButton(transform, "Btn_close");
            }

            if (_btnRepoint0 != null)
            {
                _btnRepoint0.onClick.RemoveAllListeners();
                _btnRepoint0.onClick.AddListener(OnRepoint0Click);
            }

            if (_btnRepoint1 != null)
            {
                _btnRepoint1.onClick.RemoveAllListeners();
                _btnRepoint1.onClick.AddListener(OnRepoint1Click);
            }

            if (_btnClose != null)
            {
                _btnClose.onClick.RemoveAllListeners();
                _btnClose.onClick.AddListener(Close);
            }
        }

        private void OnRepoint0Click()
        {
            // C++: Repoint0 click -> checks HasAnyItemInSlot()
            var inv = KOInventory.Instance;
            if (inv == null)
            {
                Close();
                return;
            }

            if (!inv.HasAnyItemInSlot())
            {
                PointChangePriceQuery(true);
            }
            else
            {
                Close();
                if (KOUIManager.Instance != null)
                {
                    // C++: IDS_MSG_HASITEMINSLOT = 6112
                    string msg = Services.StringTableService.Get(6112);
                    KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffff3b3b));
                }
            }
        }

        private void OnRepoint1Click()
        {
            PointChangePriceQuery(false);
        }

        /// <summary>
        /// C++: Close()
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.CloseNpcChange();
            }
        }

        /// <summary>
        /// C++: PointChangePriceQuery(bool bAllPoint)
        /// Sends query to the server for the price.
        /// </summary>
        public void PointChangePriceQuery(bool bAllPoint)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_CLASS_CHANGE);
                pkt.WriteByte(0x05); // CLASS_RESET_COST_REQ
                pkt.WriteByte((byte)(bAllPoint ? 1 : 2));
                netMgr.SendPacket(pkt);

                _bSendedAllPoint = bAllPoint;
            }
        }

        public bool GetSendedAllPoint() => _bSendedAllPoint;
    }
}
