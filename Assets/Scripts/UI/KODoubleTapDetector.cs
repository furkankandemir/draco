using UnityEngine;
using UnityEngine.EventSystems;
using System;

namespace EntropyOnline.UI
{
    /// <summary>
    /// C++ UIMSG_LIST_DBLCLK karşılığı — mobilde double-tap tespit eder.
    /// 
    /// C++ UIWarp.cpp satır 60-64:
    ///   else if (dwMsg & UIMSG_LIST_DBLCLK) {
    ///       CGameProcedure::s_pProcMain->MsgSend_Warp();
    ///       this->SetVisible(false);
    ///   }
    /// 
    /// Mobilde çift tıklama yoktur — bu component iki ardışık tap'ı algılayarak
    /// double-click davranışını simüle eder.
    /// </summary>
    public class KODoubleTapDetector : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>
        /// İki tap arasındaki maksimum süre (saniye).
        /// C++ UI'da çift tıklama algılama süresi ~0.3s'dir.
        /// </summary>
        public float doubleTapInterval = 0.3f;

        /// <summary>
        /// Çift tıklama algılandığında çağrılan callback.
        /// </summary>
        public Action onDoubleTap;

        private float _lastTapTime;

        public void OnPointerClick(PointerEventData eventData)
        {
            float currentTime = Time.unscaledTime;

            if (currentTime - _lastTapTime < doubleTapInterval)
            {
                // Double-tap algılandı
                onDoubleTap?.Invoke();
                _lastTapTime = 0f; // Reset — üçlü tap'ı engelle
            }
            else
            {
                _lastTapTime = currentTime;
            }
        }
    }
}
