using UnityEngine;
using EntropyOnline.World;
using EntropyOnline.UI;

namespace EntropyOnline.World
{
    /// <summary>
    /// Spawns when a monster dies and drops items.
    /// Manages the loot box's BundleID, CorpseEntity reference, and self-cleanup.
    /// </summary>
    public class KOLootBox : MonoBehaviour
    {
        public long BundleID { get; set; }
        public KOEntity CorpseEntity { get; set; }

        private bool _hasOpened = false;

        private void Update()
        {
            // If the monster corpse has faded out or been destroyed, despawn this loot box too
            if (CorpseEntity == null)
            {
                if (LootDropUI.Instance != null)
                {
                    LootDropUI.Instance.RemoveLootBoxFromTracking(BundleID);
                }
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Animates the chest lid opening smoothly.
        /// </summary>
        public void OpenChest()
        {
            if (_hasOpened) return;
            _hasOpened = true;

            StartCoroutine(AnimateOpen());
        }

        private System.Collections.IEnumerator AnimateOpen()
        {
            Transform lid = transform.Find("SM_Chest_Top");
            if (lid == null) yield break;

            Quaternion startRot = Quaternion.Euler(-90f, 90f, 0f);
            Quaternion targetRot = Quaternion.Euler(-225f, 90f, 0f); // Swing lid open backward by 135 degrees

            float elapsed = 0f;
            float duration = 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Cubic easing (smooth step)
                t = t * t * (3f - 2f * t);

                lid.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            lid.localRotation = targetRot;
        }
    }
}
