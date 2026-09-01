using System.Collections.Generic;
using UnityEngine;

namespace EntropyOnline.World
{
    /// <summary>
    /// NPC, Loot Box ve Merchant etkileşim panellerinin/butonlarının
    /// yakınlık durumunu koordine ederek her zaman sadece en yakındaki tek bir butonun
    /// aktif olmasını sağlayan merkezi arayüz kayıt sistemi.
    /// </summary>
    public static class KOProximityInteractRegistry
    {
        public interface IProximityInteractable
        {
            Transform GetTransform();
            float GetCurrentDistance();
            void SetCurrentDistance(float dist);
            void SetVisible(bool visible);
            bool IsInRange();
        }

        private static readonly List<IProximityInteractable> _interactables = new List<IProximityInteractable>();

        public static void Register(IProximityInteractable interactable)
        {
            if (interactable == null) return;
            if (!_interactables.Contains(interactable))
            {
                _interactables.Add(interactable);
            }
        }

        public static void Unregister(IProximityInteractable interactable)
        {
            if (interactable == null) return;
            _interactables.Remove(interactable);
        }

        public static void UpdateRegistry()
        {
            IProximityInteractable closest = null;
            float minDistance = float.MaxValue;

            // Listeyi sondan başa doğru tarayarak silinen nesneleri güvenle temizliyoruz
            for (int i = _interactables.Count - 1; i >= 0; i--)
            {
                var item = _interactables[i];
                if (item == null || item.GetTransform() == null)
                {
                    _interactables.RemoveAt(i);
                    continue;
                }

                if (item.IsInRange())
                {
                    float dist = item.GetCurrentDistance();
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closest = item;
                    }
                    else if (Mathf.Approximately(dist, minDistance) && closest != null)
                    {
                        // Eşitlik durumlarında titremeyi önlemek için HashCode bazında öncelik veriyoruz
                        if (item.GetTransform().GetHashCode() < closest.GetTransform().GetHashCode())
                        {
                            closest = item;
                        }
                    }
                }
            }

            // Görünürlük durumlarını uygula
            for (int i = 0; i < _interactables.Count; i++)
            {
                var item = _interactables[i];
                if (item != null)
                {
                    item.SetVisible(item == closest);
                }
            }
        }
    }
}
