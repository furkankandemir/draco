using UnityEngine;
using EntropyOnline.Network;
using EntropyOnline.Core;
using EntropyOnline.World;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Target info handler'ı.
    /// UI artık KOUIManager tarafından co_targetbar_us.uif'den yükleniyor.
    /// EntityManager'dan hedef HP güncellemesi alır.
    /// </summary>
    public class TargetInfoUI : MonoBehaviour
    {
        public static TargetInfoUI Instance { get; private set; }

        private long _currentTargetId = -1;
        private int _currentHp;
        private int _currentMaxHp;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// EntityManager'dan çağrılır — hedef HP güncellemesi.
        /// </summary>
        public void UpdateTargetHp(long targetId, int hp, int maxHp)
        {
            _currentTargetId = targetId;
            _currentHp = hp;
            _currentMaxHp = maxHp;

            // KOTargetSelector kendi HP bar'ını güncelliyor (CreateTargetBarUI → UpdateHPBar)
            // KOEntity.CurrentHP/MaxHP KOTargetSelector.UpdateHPBar()'dan okunuyor.
            // GameHUD.OnTargetHpReceived() entity HP'yi MonsterEntity üzerinden de günceller.
            // Burada ek olarak KOEntity üzerinden de güncelle (3D overhead HP bar).
            var selector = KOTargetSelector.Instance;
            if (selector != null && selector.CurrentTarget != null &&
                selector.CurrentTarget.ServerInstanceId == targetId)
            {
                selector.CurrentTarget.CurrentHP = hp;
                selector.CurrentTarget.MaxHP = maxHp;
            }
        }

        /// <summary>
        /// Hedef seçimi kaldırıldığında çağrılır.
        /// </summary>
        public void ClearTarget()
        {
            _currentTargetId = -1;
            _currentHp = 0;
            _currentMaxHp = 0;

            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowTargetBar(false);
        }

        public long CurrentTargetId => _currentTargetId;
        public int CurrentHp => _currentHp;
        public int CurrentMaxHp => _currentMaxHp;
    }
}
