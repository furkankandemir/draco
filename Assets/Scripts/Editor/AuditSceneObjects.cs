using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EntropyOnline.Import;

namespace EntropyOnline.Editor
{
    public static class AuditSceneObjects
    {
        [MenuItem("Entropy Online/Audit Scene Objects", false, 40)]
        public static void RunAudit()
        {
            short zoneId = 21;
            string assetPath = $"Assets/Resources/KOZones/zone_{zoneId}.asset";
            string backupPath = $"Assets/Resources/KOZones/zone_{zoneId}_backup.asset";

            KOZoneAsset zoneAsset = AssetDatabase.LoadAssetAtPath<KOZoneAsset>(assetPath);
            KOZoneAsset backupAsset = AssetDatabase.LoadAssetAtPath<KOZoneAsset>(backupPath);

            Debug.Log($"=== MORADON SCENE OBJECT AUDIT ===");
            Debug.Log($"Active Zone Asset Shapes: {(zoneAsset != null ? zoneAsset.shapes.Length : 0)}");
            Debug.Log($"Backup Zone Asset Shapes: {(backupAsset != null ? backupAsset.shapes.Length : 0)}");

            GameObject koObjectsGo = null;
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root.name == $"KOObjects_{zoneId}" || root.name == "KOObjects")
                {
                    koObjectsGo = root;
                    break;
                }
            }

            if (koObjectsGo == null)
            {
                var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in allGos)
                {
                    if (go.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene() && 
                        (go.name == $"KOObjects_{zoneId}" || go.name == "KOObjects"))
                    {
                        koObjectsGo = go;
                        break;
                    }
                }
            }

            if (koObjectsGo == null)
            {
                Debug.LogError("KOObjects parent group not found in scene!");
                return;
            }

            var scenePositions = new List<Vector3>();
            var childTransforms = koObjectsGo.GetComponentsInChildren<Transform>(true);
            foreach (var t in childTransforms)
            {
                if (t != koObjectsGo.transform && t.parent == koObjectsGo.transform)
                {
                    scenePositions.Add(t.position);
                }
            }

            Debug.Log($"Total shapes under KOObjects in scene: {scenePositions.Count}");

            if (backupAsset != null && backupAsset.shapes != null)
            {
                int missingCount = 0;
                foreach (var shape in backupAsset.shapes)
                {
                    bool found = false;
                    foreach (var pos in scenePositions)
                    {
                        if (Vector3.Distance(shape.position, pos) < 0.1f)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        missingCount++;
                        if (missingCount <= 10)
                        {
                            Debug.Log($"Missing Shape: {shape.name} at position {shape.position}");
                        }
                    }
                }
                Debug.Log($"Total Missing/Deleted Shapes in Scene compared to Backup: {missingCount}");
            }
        }
    }
}
