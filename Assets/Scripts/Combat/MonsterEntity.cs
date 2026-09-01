using UnityEngine;

namespace EntropyOnline.Combat
{
    /// <summary>
    /// Entropy Online — Yaratık Entity (Client Tarafı)
    /// 
    /// Sahnedeki yaratıkları temsil eder.
    /// Sunucudan gelen S2C_SPAWN_MONSTER paketi ile oluşturulur.
    /// </summary>
    public class MonsterEntity : MonoBehaviour
    {
        public long InstanceId { get; set; }
        public int DefinitionId { get; set; }
        public string MonsterName { get; set; }
        public short Level { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }

        public bool IsAlive => CurrentHp > 0;
        public float HpPercent => MaxHp > 0 ? (float)CurrentHp / MaxHp : 0f;

        /// <summary>
        /// Sunucudan gelen hasar sonucunu uygular.
        /// </summary>
        public void ApplyDamage(int damage, int remainingHp)
        {
            CurrentHp = remainingHp;

            if (CurrentHp <= 0)
            {
                OnDeath();
            }
        }

        private void OnDeath()
        {
            // Open-KO birebir: Ölüm lifecycle'ı KOEntity.ActionDying() + TimeAfterDeath timer
            // ile yönetilir. Entity burada gizlenmez — corpse 90 saniye kalır.
            // gameObject.SetActive(false) → YAPMA! Ceset etkileşimi (loot) için entity aktif kalmalı.
        }

        /// <summary>
        /// Yaratık bilgilerini başlat.
        /// </summary>
        public void Initialize(long instanceId, int defId, string name, short level, int hp, int maxHp)
        {
            InstanceId = instanceId;
            DefinitionId = defId;
            MonsterName = name;
            Level = level;
            CurrentHp = hp;
            MaxHp = maxHp;
            gameObject.name = $"Monster_{name}_{instanceId}";
        }
    }
}
