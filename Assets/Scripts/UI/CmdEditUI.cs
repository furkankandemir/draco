using UnityEngine;
using UnityEngine.UI;

namespace EntropyOnline.UI
{
    public class CmdEditUI : MonoBehaviour
    {
        private Text _textTitle;
        private InputField _editBox;
        private GameObject _btnOk;
        private GameObject _btnCancel;

        private void Awake()
        {
            // Find references by their C++ names
            _textTitle = transform.Find("Text_cmd")?.GetComponent<Text>();
            _btnOk = transform.Find("btn_ok")?.gameObject;
            _btnCancel = transform.Find("btn_cancel")?.gameObject;
            
            var editTransform = transform.Find("edit_cmd");
            if (editTransform != null)
            {
                _editBox = editTransform.GetComponent<InputField>();
                if (_editBox == null)
                    _editBox = editTransform.gameObject.AddComponent<InputField>();
            }

            // Bind buttons
            if (_btnOk != null)
            {
                var btn = _btnOk.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(OnOkClicked);
            }

            if (_btnCancel != null)
            {
                var btn = _btnCancel.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(OnCancelClicked);
            }
        }

        public void Open(string title)
        {
            gameObject.SetActive(true);
            if (_textTitle != null)
            {
                _textTitle.text = title;
            }

            if (_editBox != null)
            {
                _editBox.text = "";
                _editBox.ActivateInputField();
                _editBox.Select();
            }
        }

        private void OnOkClicked()
        {
            string arg = _editBox != null ? _editBox.text.Trim() : "";
            string cmdName = _textTitle != null ? _textTitle.text : "";

            if (!string.IsNullOrEmpty(cmdName))
            {
                // Construct command like "/PM name"
                string fullCommand = "/" + cmdName + " " + arg;
                KOUIManager.Instance.ParseChattingCommand(fullCommand);
            }

            Close();
        }

        private void OnCancelClicked()
        {
            Close();
        }

        private void Close()
        {
            if (_editBox != null)
            {
                _editBox.DeactivateInputField();
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            // Support Enter key inside input field to trigger OK
            if (gameObject.activeSelf && _editBox != null && _editBox.isFocused)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                {
                    OnOkClicked();
                }
            }

            // Escape closes edit window
            if (gameObject.activeSelf && kb.escapeKey.wasPressedThisFrame)
            {
                OnCancelClicked();
            }
        }
    }
}
