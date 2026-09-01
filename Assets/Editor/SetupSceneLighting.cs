using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class SetupSceneLighting
{
    [MenuItem("Entropy Online/Lighting/Apply Standard Lighting to Scene")]
    public static void ApplyStandardLighting()
    {
        Debug.Log("[LIGHTING] Applying standard lighting and post-processing setup to the active scene...");

        // 1. Post Processing Volume
        var existingPP = GameObject.Find("Kingdom_PP_Balanced");
        if (existingPP == null)
        {
            // Try loading from Assets path first
            var ppProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Resources/Settings/Kingdom_PP_Balanced.asset");
            if (ppProfile == null)
            {
                ppProfile = Resources.Load<VolumeProfile>("Settings/Kingdom_PP_Balanced");
            }

            if (ppProfile != null)
            {
                var ppGO = new GameObject("Kingdom_PP_Balanced");
                var vol = ppGO.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.weight = 1f;
                vol.priority = 1;
                vol.sharedProfile = ppProfile;
                Undo.RegisterCreatedObjectUndo(ppGO, "Create Kingdom_PP_Balanced");
                Debug.Log("[LIGHTING] Created Kingdom_PP_Balanced Volume in scene.");
            }
            else
            {
                Debug.LogWarning("[LIGHTING] Kingdom_PP_Balanced VolumeProfile asset not found in Resources/Settings!");
            }
        }
        else
        {
            Debug.Log("[LIGHTING] Kingdom_PP_Balanced already exists in scene.");
        }

        // 2. DemoLighting Prefab
        var existingDemo = GameObject.Find("DemoLighting");
        if (existingDemo == null)
        {
            var demoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/DemoLighting.prefab");
            if (demoPrefab == null)
            {
                demoPrefab = Resources.Load<GameObject>("Prefabs/DemoLighting");
            }

            if (demoPrefab != null)
            {
                // Instantiate as prefab instance to keep connection
                var demoGO = (GameObject)PrefabUtility.InstantiatePrefab(demoPrefab);
                demoGO.name = "DemoLighting";
                demoGO.transform.position = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(demoGO, "Create DemoLighting");
                Debug.Log("[LIGHTING] Instantiated DemoLighting prefab in scene.");
            }
            else
            {
                Debug.LogWarning("[LIGHTING] DemoLighting prefab not found in Resources/Prefabs!");
            }
        }
        else
        {
            Debug.Log("[LIGHTING] DemoLighting already exists in scene.");
        }

        // 3. Render Settings (Ambient, Reflections, Fog)
        
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = new Color(0.9411765f, 0.9411765f, 0.9607843f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.fog = false;

        // Mark scene dirty so it prompts to save
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        
        Debug.Log("[LIGHTING] ✅ Standard lighting applied successfully! Save your scene (Ctrl+S) to persist changes.");
        EditorUtility.DisplayDialog("Scene Lighting Setup", 
            "Standard lighting and post-processing applied successfully!\n\n" +
            "Make sure to save your scene (Ctrl+S).", "OK");
    }
}
