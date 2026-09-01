using UnityEngine;
using EntropyOnline.Core;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Eski Clan UI stub'ı — KnightsUI.cs artık tam UI implementasyonunu içerir.
    /// Bu sınıf geriye uyumluluk için korunur, iç mantık KnightsUI'a delegasyon yapar.
    /// Yeni kod KnightsUI.cs'i kullanmalıdır.
    /// </summary>
    public class ClanUI : MonoBehaviour
    {
        public static ClanUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Initialize()
        {
            // KnightsUI.cs tüm işi yapar
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
