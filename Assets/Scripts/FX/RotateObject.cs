using UnityEngine;

namespace EntropyOnline.FX
{
    public class RotateObject : MonoBehaviour
    {
        public float rotationSpeed = 120f;

        void Update()
        {
            // Rotate around the local Y axis
            transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}
