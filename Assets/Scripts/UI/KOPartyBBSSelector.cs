using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Import;
using EntropyOnline.Network.KO;

namespace EntropyOnline.UI
{
    public class KOPartyBBSSelector : MonoBehaviour
    {
        public static KOPartyBBSSelector Instance { get; private set; }

        private Button _btnOk;
        private Button _btnCancel;
        private Button _btnWarrior;
        private Button _btnRogue;
        private Button _btnMage;
        private Button _btnPriest;
        private InputField _editMessage;

        private bool _warriorActive = true;
        private bool _rogueActive = true;
        private bool _mageActive = true;
        private bool _priestActive = true;

        private void Awake()
        {
            Instance = this;
            BindElements();
        }

        private void BindElements()
        {
            var t = transform;

            _btnOk = KOUIRenderer.FindChildButton(t, "btn_ok");
            _btnCancel = KOUIRenderer.FindChildButton(t, "btn_cencel"); // Typo in UIF/Prefab
            _btnWarrior = KOUIRenderer.FindChildButton(t, "btn_warrior");
            _btnRogue = KOUIRenderer.FindChildButton(t, "btn_rogue");
            _btnMage = KOUIRenderer.FindChildButton(t, "btn_mage");
            _btnPriest = KOUIRenderer.FindChildButton(t, "btn_priest");

            // Message edit
            var editTrans = KOUIRenderer.FindChildByID(t, "edit_message");
            if (editTrans != null)
            {
                _editMessage = editTrans.GetComponent<InputField>();
            }

            // Bind listeners
            if (_btnOk != null)
            {
                _btnOk.onClick.AddListener(OnConfirm);
                SetButtonTransitions(_btnOk);
            }

            if (_btnCancel != null)
            {
                _btnCancel.onClick.AddListener(OnClose);
                SetButtonTransitions(_btnCancel);
            }

            if (_btnWarrior != null)
            {
                _btnWarrior.onClick.AddListener(() => ToggleClass(ref _warriorActive, _btnWarrior));
                UpdateBtnState(_warriorActive, _btnWarrior);
            }

            if (_btnRogue != null)
            {
                _btnRogue.onClick.AddListener(() => ToggleClass(ref _rogueActive, _btnRogue));
                UpdateBtnState(_rogueActive, _btnRogue);
            }

            if (_btnMage != null)
            {
                _btnMage.onClick.AddListener(() => ToggleClass(ref _mageActive, _btnMage));
                UpdateBtnState(_mageActive, _btnMage);
            }

            if (_btnPriest != null)
            {
                _btnPriest.onClick.AddListener(() => ToggleClass(ref _priestActive, _btnPriest));
                UpdateBtnState(_priestActive, _btnPriest);
            }
        }

        private void ToggleClass(ref bool state, Button btn)
        {
            state = !state;
            UpdateBtnState(state, btn);
        }

        private void UpdateBtnState(bool state, Button btn)
        {
            if (btn != null && btn.targetGraphic != null)
            {
                // Active/selected = normal color, inactive = grayed out
                btn.targetGraphic.color = state ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1.0f);
            }
        }

        private void OnConfirm()
        {
            // Send WIZ_PARTY_BBS register packet (sub-opcode 1)
            var netMgr = KONetworkManager.Instance;
            if (netMgr != null)
            {
                using var packet = new KOPacketWriter(WizOpcode.WIZ_PARTY_BBS);
                packet.WriteByte(1); // sub-opcode: PARTY_BBS_REGISTER

                // Optional: C++ client sends byKind, we can send it or keep it simple.
                // We'll write 0 (SEEKING_PARTY) since that's standard for individual registering.
                packet.WriteByte(0); 

                netMgr.SendPacket(packet);
            }

            gameObject.SetActive(false);
        }

        private void OnClose()
        {
            gameObject.SetActive(false);
        }

        private void SetButtonTransitions(Button btn)
        {
            if (btn == null) return;
            if (btn.gameObject.GetComponent<UIButtonScaleFeedback>() == null)
            {
                btn.gameObject.AddComponent<UIButtonScaleFeedback>();
            }
        }
    }
}
