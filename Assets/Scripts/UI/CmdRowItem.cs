using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace EntropyOnline.UI
{
    public class CmdRowItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public int index;
        public string text;
        public string tooltip;
        public System.Action<int> onClick;
        public System.Action<int> onDoubleClick;

        private Text _textComp;
        private Color _normalColor = Color.white;
        private Color _selectedColor = Color.yellow;
        private bool _selected;

        public void Init(string itemText, string itemTooltip, int itemIndex, Font font, Color normalColor)
        {
            index = itemIndex;
            text = itemText;
            tooltip = itemTooltip;
            _normalColor = normalColor;

            _textComp = gameObject.GetComponent<Text>();
            if (_textComp == null)
                _textComp = gameObject.AddComponent<Text>();

            _textComp.text = itemText;
            _textComp.font = font;
            _textComp.fontSize = 12;
            _textComp.alignment = TextAnchor.MiddleLeft;
            _textComp.color = _normalColor;

            // Ensure rect bounds are correct
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            
            // Add layout element
            var layout = gameObject.GetComponent<LayoutElement>();
            if (layout == null)
                layout = gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 20;

            // Add DoubleTapDetector
            var dblTap = gameObject.GetComponent<KODoubleTapDetector>();
            if (dblTap == null)
                dblTap = gameObject.AddComponent<KODoubleTapDetector>();
            dblTap.onDoubleTap = () => onDoubleClick?.Invoke(index);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (_textComp != null)
            {
                _textComp.color = _selected ? _selectedColor : _normalColor;
                _textComp.fontStyle = _selected ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            onClick?.Invoke(index);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(tooltip))
            {
                KOUIManager.Instance.ShowTooltip(tooltip, new Color32(144, 238, 144, 255));
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            KOUIManager.Instance.HideTooltip();
        }
    }
}
