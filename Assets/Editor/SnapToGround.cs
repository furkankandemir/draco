using UnityEngine;
using UnityEditor;

public class SnapToGround
{
    [MenuItem("Entropy Online/Snap Selected to Ground %g")] // %g = Ctrl + G kısayolu
    public static void SnapSelectedToGround()
    {
        var selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            return;
        }

        Undo.RecordObjects(Selection.transforms, "Snap to Ground");

        foreach (var go in selectedObjects)
        {
            // Kendine çarpıp yukarı fırlamaması için objenin ve çocuklarının collider'larını geçici olarak kapatıyoruz
            var colliders = go.GetComponentsInChildren<Collider>();
            var originalStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                originalStates[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }

            // Objeyi yukarıdan aşağıya doğru ışın (raycast) göndererek zemini bulmaya çalışıyoruz
            Ray ray = new Ray(go.transform.position + Vector3.up * 50f, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, 5000f);
            bool hitSomething = false;

            // Işının çarptığı tüm nesneler içinden TerrainCollider olanı arıyoruz
            foreach (var hit in hits)
            {
                if (hit.collider is TerrainCollider)
                {
                    Debug.Log($"[SnapToGround] Obj: {go.name} -> TerrainCollider bulundu, Y: {hit.point.y}");
                    go.transform.position = hit.point;
                    hitSomething = true;
                    break;
                }
            }

            // Collider'ları eski durumlarına geri getiriyoruz
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = originalStates[i];
            }

            // Eğer TerrainCollider'a çarpmadıysa aktif Terrain yüksekliğini SampleHeight ile alıp oraya oturtuyoruz
            if (!hitSomething)
            {
                Terrain terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    float y = terrain.SampleHeight(go.transform.position) + terrain.transform.position.y;
                    Debug.Log($"[SnapToGround] Obj: {go.name} -> TerrainCollider bulunamadı. Aktif Terrain SampleHeight kullanılıyor. Y: {y}");
                    go.transform.position = new Vector3(go.transform.position.x, y, go.transform.position.z);
                }
                else
                {
                    Debug.LogWarning($"[SnapToGround] Obj: {go.name} -> Sahnede aktif bir Terrain bulunamadı!");
                }
            }
        }
    }
}
