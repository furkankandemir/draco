using UnityEngine;
using System.Collections.Generic;

namespace EntropyOnline.World
{
    /// <summary>
    /// CN3SPart::Tick() — Texture Animation birebir portu.
    /// Birden fazla texture'a sahip Part'lar arasında FPS bazlı geçiş yapar.
    ///
    /// C++ (N3Shape.cpp:99-106):
    ///   int iTC = m_TexRefs.size();
    ///   if (iTC > 1) // texture animation
    ///   {
    ///       m_fTexIndex += s_fSecPerFrm * m_fTexFPS;
    ///       if (m_fTexIndex >= iTC)
    ///           m_fTexIndex -= (iTC * m_fTexIndex) / iTC;
    ///   }
    ///
    /// C++ (N3Shape.cpp:186-191, Render):
    ///   int iTexIndex = (int) m_fTexIndex;
    ///   if (iTexIndex >= 0 && iTexIndex < iTC && m_TexRefs[iTexIndex])
    ///       lpTex = m_TexRefs[iTexIndex]->Get();
    /// </summary>
    public class TextureAnimPart : MonoBehaviour
    {
        private Texture2D[] _textures;
        private float _texFPS;
        private float _texIndex;
        private Renderer _renderer;
        private int _mainTexPropId;

        /// <summary>
        /// C++ CN3SPart constructor: m_fTexFPS = 10.0f (varsayılan)
        /// WorldBuilder tarafından çağrılır.
        /// </summary>
        public void Initialize(Texture2D[] textures, float texFPS)
        {
            _textures = textures;
            _texFPS = texFPS;
            _texIndex = 0f;
            _renderer = GetComponent<Renderer>();

            // URP veya Standard shader property ID
            if (_renderer != null && _renderer.material != null)
            {
                if (_renderer.material.HasProperty("_BaseMap"))
                    _mainTexPropId = Shader.PropertyToID("_BaseMap");
                else if (_renderer.material.HasProperty("_Base_Map"))
                    _mainTexPropId = Shader.PropertyToID("_Base_Map");
                else
                    _mainTexPropId = Shader.PropertyToID("_MainTex");
            }
        }

        private void Update()
        {
            if (_textures == null || _textures.Length <= 1 || _renderer == null)
                return;

            int iTC = _textures.Length;

            // C++ (N3Shape.cpp:102-105):
            // m_fTexIndex += CN3Base::s_fSecPerFrm * m_fTexFPS;
            _texIndex += Time.deltaTime * _texFPS;

            // C++ wrap logic: if (m_fTexIndex >= iTC) m_fTexIndex -= (iTC * m_fTexIndex) / iTC;
            // Bu aslında modulo: m_fTexIndex = m_fTexIndex % iTC (float modulo)
            if (_texIndex >= iTC)
                _texIndex -= Mathf.Floor(_texIndex / iTC) * iTC;

            // C++ (N3Shape.cpp:189-191): int iTexIndex = (int) m_fTexIndex;
            int iTexIndex = (int)_texIndex;
            if (iTexIndex >= 0 && iTexIndex < iTC && _textures[iTexIndex] != null)
            {
                _renderer.material.SetTexture(_mainTexPropId, _textures[iTexIndex]);
            }
        }
    }
}
