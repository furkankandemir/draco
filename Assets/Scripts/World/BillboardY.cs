using UnityEngine;

namespace EntropyOnline.World
{
    /// <summary>
    /// CN3SPart::Tick() — RF_BOARD_Y (0x8) birebir portu.
    /// Y ekseni etrafında kameraya döner (ağaç yaprakları, tabelalar vb.)
    ///
    /// C++ (N3Shape.cpp:108-128):
    ///   __Vector3 vPos = m_vPivot * mtxParent;
    ///   __Vector3 vDir = s_CameraData.vEye - vPos;
    ///   if (vDir.x > 0)
    ///       m_Matrix.RotationY(-atan(vDir.z / vDir.x) - PI/2);
    ///   else
    ///       m_Matrix.RotationY(-atan(vDir.z / vDir.x) + PI/2);
    /// </summary>
    public class BillboardY : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 vDir = cam.transform.position - transform.position;

            // C++ birebir: Y ekseni etrafında kameraya dön
            if (Mathf.Abs(vDir.x) > 0.001f || Mathf.Abs(vDir.z) > 0.001f)
            {
                float angle;
                if (vDir.x > 0f)
                    angle = -Mathf.Atan(vDir.z / vDir.x) - Mathf.PI * 0.5f;
                else
                    angle = -Mathf.Atan(vDir.z / vDir.x) + Mathf.PI * 0.5f;

                transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            }
        }
    }
}
