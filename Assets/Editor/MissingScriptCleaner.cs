using UnityEngine;
using UnityEditor;
using System.IO;

public class MissingScriptCleaner : EditorWindow
{
    private static readonly string[] TargetFolders = new string[]
    {
        "Assets/Hovl Studio/AAA Magic circles Vol 3",
        "Assets/Magic Circle Fx",
        "Assets/Piloto Studio",
        "Assets/VFX_Klaus"
    };

    [MenuItem("Tools/Antigravity/Remove Missing Scripts from Prefabs")]
    public static void RemoveMissingScripts()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", TargetFolders);
        int totalRemoved = 0;
        int affectedPrefabs = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            // Load prefab contents using the modern official PrefabUtility API
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            if (prefabRoot == null) continue;

            int removedInThisPrefab = CleanGameObject(prefabRoot);

            if (removedInThisPrefab > 0)
            {
                // Save the modified contents back to the prefab asset
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                affectedPrefabs++;
                totalRemoved += removedInThisPrefab;
                Debug.Log($"[Cleaner] Removed {removedInThisPrefab} missing script(s) from prefab: {path}");
            }
            
            // Unload contents to free memory
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        if (totalRemoved > 0)
        {
            AssetDatabase.Refresh();
            Debug.Log($"[Cleaner] Successfully removed {totalRemoved} missing script components across {affectedPrefabs} prefabs!");
        }
        else
        {
            Debug.Log("[Cleaner] No missing scripts found in prefabs. Everything is clean!");
        }
    }

    private static int CleanGameObject(GameObject go)
    {
        int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        
        foreach (Transform child in go.transform)
        {
            count += CleanGameObject(child.gameObject);
        }
        
        return count;
    }
}
