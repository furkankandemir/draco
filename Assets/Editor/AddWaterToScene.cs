using UnityEngine;
using UnityEditor;

public class AddWaterToScene
{
    [MenuItem("Entropy Online/Add Water Plane to Scene")]
    public static void AddWater()
    {
        // Su mesh'ini bul
        GameObject waterMesh = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Polyart/PolyartStudio/SharedResources/Meshes/SM_WaterPlane.fbx");

        if (waterMesh == null)
        {
            Debug.LogError("[AddWater] SM_WaterPlane.fbx bulunamadı!");
            return;
        }

        // Material bul
        Material waterMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Polyart/PolyartStudio/DreamscapeCastle/Materials/Water/MI_Water_Lake.mat");

        if (waterMat == null)
        {
            waterMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Polyart/PolyartStudio/SharedResources/Materials/Water/MI_Ocean.mat");
        }

        // Sahneye ekle
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(waterMesh);
        instance.name = "Water_Lake";

        // Terrain'i bul ve merkeze yerleştir
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            Vector3 center = terrainPos + new Vector3(terrainSize.x / 2f, 0, terrainSize.z / 2f);

            // Su seviyesini terrain yüksekliğinin biraz altına ayarla
            center.y = terrainPos.y + 5f;
            instance.transform.position = center;
        }
        else
        {
            // Scene view kamerasının baktığı yere koy
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                instance.transform.position = sceneView.pivot;
            }
        }

        // Uygun boyut
        instance.transform.localScale = new Vector3(0.3f, 1f, 0.3f);

        // Material ata
        if (waterMat != null)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = waterMat;
                r.sharedMaterials = mats;
            }
        }

        Undo.RegisterCreatedObjectUndo(instance, "Add Water");
        Selection.activeGameObject = instance;
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log("[AddWater] Su düzlemi sahneye eklendi. Scale ve Position'ı istediğin gibi ayarla.");
    }
}
