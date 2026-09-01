using UnityEngine;
using UnityEngine.EventSystems;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Envanter 3D karakter modelini mouse/dokunma sürüklemesiyle döndüren yardımcı bileşen.
    /// </summary>
    public class KOInventoryChrRotate : MonoBehaviour, IDragHandler
    {
        [Tooltip("Döndürülecek hedef transform (3D karakter modeli)")]
        public Transform targetTransform;

        [Tooltip("Döndürme hassasiyeti")]
        public float rotationSpeed = 0.6f;

        public void OnDrag(PointerEventData eventData)
        {
            if (targetTransform != null)
            {
                // Sürükleme yönünün tersine döndürme (doğal döndürme hissi için)
                targetTransform.Rotate(Vector3.up, -eventData.delta.x * rotationSpeed, Space.Self);
            }
        }
    }
}
