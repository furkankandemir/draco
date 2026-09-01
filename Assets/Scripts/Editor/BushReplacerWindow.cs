using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace EntropyOnline.Editor
{
    public class BushReplacerWindow : EditorWindow
    {
        private string targetName = "obj_co_dumbul08y";
        private float scaleMultiplier = 1.0f;
        private string prefabPath = "Assets/Holotna/Mountain/Prefabs/Bush01.prefab";

        [MenuItem("Entropy Online/Bush Replacer Tool", false, 35)]
        public static void ShowWindow()
        {
            GetWindow<BushReplacerWindow>("Bush Replacer");
        }

        private void OnGUI()
        {
            GUILayout.Label("Bush Replacer Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            targetName = EditorGUILayout.TextField("Target Object Name", targetName);
            prefabPath = EditorGUILayout.TextField("Replacement Prefab Path", prefabPath);
            scaleMultiplier = EditorGUILayout.FloatField("Scale Multiplier", scaleMultiplier);

            EditorGUILayout.Space();

            if (GUILayout.Button("Find and Replace in Scene", GUILayout.Height(45)))
            {
                ReplaceBushes();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Move Custom Objects to KOTerrain", GUILayout.Height(35)))
            {
                MoveCustomObjectsToKOTerrain();
            }
        }

        private void ReplaceBushes()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[BUSH REPLACER] Prefab not found at path: {prefabPath}");
                EditorUtility.DisplayDialog("Error", $"Prefab not found at: {prefabPath}", "OK");
                return;
            }

            // Find all GameObjects in the active scene containing targetName
            var allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
            var targets = new List<GameObject>();

            foreach (var go in allObjects)
            {
                if (go != null && go.name.Contains(targetName))
                {
                    targets.Add(go);
                }
            }

            if (targets.Count == 0)
            {
                Debug.LogWarning($"[BUSH REPLACER] No game objects named '{targetName}' found in the scene.");
                EditorUtility.DisplayDialog("Info", $"No game objects named '{targetName}' found in the scene.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int replacedCount = 0;

            foreach (var oldGo in targets)
            {
                Vector3 originalPos = oldGo.transform.position;
                Vector3 originalScale = oldGo.transform.localScale;
                Transform parent = oldGo.transform.parent;

                // Instantiate the prefab as a prefab instance in the scene
                GameObject newGo = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (newGo == null) continue;

                // Set properties
                newGo.transform.position = originalPos;
                newGo.transform.parent = parent;
                newGo.transform.localScale = originalScale * scaleMultiplier;

                // Random Y rotation
                float randomY = Random.Range(0f, 360f);
                newGo.transform.rotation = Quaternion.Euler(0f, randomY, 0f);

                // Register creation for Undo
                Undo.RegisterCreatedObjectUndo(newGo, "Replace Bush");

                // Destroy old object with Undo
                Undo.DestroyObjectImmediate(oldGo);
                replacedCount++;
            }

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Debug.Log($"[BUSH REPLACER] Successfully replaced {replacedCount} bushes.");
            EditorUtility.DisplayDialog("Success", $"Successfully replaced {replacedCount} bushes in the scene with Bush01!", "OK");
        }

        [MenuItem("Entropy Online/Move Custom Objects to KOTerrain", false, 36)]
        public static void MoveCustomObjectsToKOTerrain()
        {
            Terrain activeTerrain = GameObject.FindAnyObjectByType<Terrain>();
            if (activeTerrain == null)
            {
                EditorUtility.DisplayDialog("Error", "No active Terrain found in the scene!", "OK");
                return;
            }

            GameObject terrainGo = activeTerrain.gameObject;
            string terrainName = terrainGo.name;

            // Find all GameObjects in the scene
            var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            int movedCount = 0;

            Undo.IncrementCurrentGroup();

            foreach (var go in allObjects)
            {
                if (go != null && go.transform.parent != null && go.transform.parent.name.StartsWith("KOObjects"))
                {
                    // If name does not start with obj_ (original naming)
                    if (!go.name.StartsWith("obj_", System.StringComparison.OrdinalIgnoreCase))
                    {
                        Undo.SetTransformParent(go.transform, terrainGo.transform, "Move to KOTerrain");
                        movedCount++;
                    }
                }
            }

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Debug.Log($"[BUSH REPLACER] Moved {movedCount} custom objects under {terrainName}.");
            EditorUtility.DisplayDialog("Success", $"Successfully moved {movedCount} custom objects under {terrainName}!", "OK");
        }
    }
}
