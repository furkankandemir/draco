using UnityEngine;
using UnityEditor;
using System.Linq;

namespace EntropyOnline.Editor
{
    public class WaterIntegrator : EditorWindow
    {
        [MenuItem("Entropy Online/Integrate Water System", false, 40)]
        public static void IntegrateWater()
        {
            // 1. Yeni su prefabını yükle
            string newWaterPrefabPath = "Assets/IgniteCoders/Simple Water Shader/Prefabs/WaterBlock_50m.prefab";
            GameObject newWaterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newWaterPrefabPath);
            
            if (newWaterPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", $"New Water Prefab not found at path:\n{newWaterPrefabPath}\nPlease make sure the package was imported correctly.", "OK");
                return;
            }

            // 2. Sahnede eski su objesini (WaterSystem vb.) bul
            GameObject oldWaterGo = GameObject.Find("WaterSystem");
            if (oldWaterGo == null)
            {
                // Eğer yoksa isminde water geçen ama block/system içermeyen bir şey ara (eski mantık fallback)
                var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
                foreach (var go in allObjects)
                {
                    string nameLower = go.name.ToLowerInvariant();
                    if (nameLower.Contains("water") && !nameLower.Contains("system") && !nameLower.Contains("block"))
                    {
                        oldWaterGo = go;
                        break;
                    }
                    else if (nameLower.Contains("plane_medium"))
                    {
                        if (go.transform.parent != null)
                            oldWaterGo = go.transform.parent.gameObject;
                        else
                            oldWaterGo = go;
                        break;
                    }
                }
            }

            Vector3 targetPosition = new Vector3(512f, -0.5f, 512f); // Default center
            Quaternion targetRotation = Quaternion.identity;
            Vector3 targetScale = new Vector3(21f, 1f, 21f); // Default scale (El Morad/Eslant fits with ~1000m scale, prefab is 50m so 21 * 50 = 1050m)

            if (oldWaterGo != null)
            {
                targetPosition = oldWaterGo.transform.position;
                targetRotation = oldWaterGo.transform.rotation;
                // Eğer eski objenin scale'i 1,1,1 ise water block için 21f yapalım (tüm haritayı kaplasın diye)
                if (oldWaterGo.transform.localScale.sqrMagnitude < 4.0f)
                {
                    targetScale = new Vector3(21f, 1f, 21f); 
                }
                else
                {
                    targetScale = oldWaterGo.transform.localScale;
                }
                Debug.Log($"[WATER INTEGRATOR] Found existing water: '{oldWaterGo.name}' at {targetPosition}. Replacing it.");
            }
            else
            {
                Debug.Log("[WATER INTEGRATOR] No existing water found in active scene. Placing at default position.");
            }

            // 3. Prefab'ı sahneye instantiate et
            GameObject instantiatedWater = (GameObject)PrefabUtility.InstantiatePrefab(newWaterPrefab);
            if (instantiatedWater == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to instantiate the new water prefab.", "OK");
                return;
            }

            Undo.RegisterCreatedObjectUndo(instantiatedWater, "Integrate New Water");

            // Eslant/ElMorad farkına göre isim verelim
            var activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            instantiatedWater.name = $"WaterBlock_50m_{activeSceneName}";
            instantiatedWater.transform.position = targetPosition;
            instantiatedWater.transform.rotation = targetRotation;
            instantiatedWater.transform.localScale = targetScale;
            
            // Layer'ı "Water" yapalım ki Unity kamerası, ışıklar ve raycast'ler su olduğunu bilsin
            int waterLayer = LayerMask.NameToLayer("Water");
            if (waterLayer != -1)
            {
                instantiatedWater.layer = waterLayer;
                foreach (Transform childTrans in instantiatedWater.transform)
                {
                    childTrans.gameObject.layer = waterLayer;
                }
            }

            // 4. Üzerinde yürümesini engellemek için tüm collider bileşenlerini temizle/kapat
            var colliders = instantiatedWater.GetComponentsInChildren<Collider>(true);
            int disabledColliders = 0;
            foreach (var col in colliders)
            {
                Undo.DestroyObjectImmediate(col);
                disabledColliders++;
            }
            Debug.Log($"[WATER INTEGRATOR] Removed {disabledColliders} collider(s) from the new water prefab to prevent walking on it.");

            // 5. Eski suyu sahneden sil
            if (oldWaterGo != null)
            {
                Undo.DestroyObjectImmediate(oldWaterGo);
            }

            // 6. Sahneyi kirli (dirty) olarak işaretle ki kaydedilsin
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);

            string message = "New Water integrated successfully!\n\n" +
                             $"Position: {targetPosition}\n" +
                             $"Scale: {targetScale}\n" +
                             $"Colliders Removed: {disabledColliders}\n\n" +
                             "Lütfen sahnedeki yüksekliği (Y) ve boyutu (Scale) ihtiyacınıza göre ayarlayın.\n" +
                             "İşleminiz bittiğinde, nesneyi kalıcı olarak kaydetmek için 'KO Terrain Exporter' -> 'Auto-Detect and Append New Objects' butonunu kullanabilirsiniz.";
            
            EditorUtility.DisplayDialog("Success", message, "OK");
        }
    }
}
