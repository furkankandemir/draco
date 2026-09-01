using UnityEngine;

namespace EntropyOnline.UI
{
    public class KOUIAreaOriginalPosition : MonoBehaviour
    {
        [SerializeField]
        private Vector2 _pos;
        public Vector2 Position { get { return _pos; } set { _pos = value; } }
    }
}
