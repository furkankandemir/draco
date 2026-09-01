using UnityEngine;
using UnityEngine.UI;

namespace EntropyOnline.Import
{
    /// <summary>
    /// C++ CN3UIImage animate birebir (N3UIImage.cpp:150-191)
    /// UISTYLE_IMAGE_ANIMATE (0x00010000) flag'li Image'lar için.
    /// Child Image'lar arasında frame frame döngü yapar.
    /// </summary>
    public class KOAnimatedImage : MonoBehaviour
    {
        public RawImage[] Frames;
        public float FrameRate = 30f; // m_fAnimFrame

        private float _currentFrame;
        private int _lastIndex = -1;

        private void Update()
        {
            if (Frames == null || Frames.Length == 0) return;

            // cpp:156 — m_fCurAnimFrame += s_fSecPerFrm * m_fAnimFrame
            _currentFrame += Time.deltaTime * FrameRate;

            // cpp:157-158 — wrap around
            while (_currentFrame >= Frames.Length)
                _currentFrame -= Frames.Length;

            int idx = (int)_currentFrame;
            if (idx == _lastIndex) return;

            // cpp:172 — sadece aktif frame'i göster
            for (int i = 0; i < Frames.Length; i++)
            {
                if (Frames[i] != null)
                    Frames[i].enabled = (i == idx);
            }
            _lastIndex = idx;
        }
    }
}
