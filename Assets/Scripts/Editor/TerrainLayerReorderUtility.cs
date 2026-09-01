using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace KO.Editor
{
    public class TerrainLayerReorderUtility : EditorWindow
    {
        [MenuItem("Tools/Terrain/Optimize and Reorder Layers")]
        public static void OptimizeAndReorderLayers()
        {
            Terrain terrain = Selection.activeGameObject?.GetComponent<Terrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select KOTerrain_12 in hierarchy!", "OK");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null) return;

            TerrainLayer[] originalLayers = terrainData.terrainLayers;
            int layerCount = originalLayers.Length;
            if (layerCount == 0) return;

            int w = terrainData.alphamapWidth;
            int h = terrainData.alphamapHeight;
            float[,,] oldAlphamaps = terrainData.GetAlphamaps(0, 0, w, h);

            // Step 1: Calculate total weight of each layer across the entire map
            float[] totalWeights = new float[layerCount];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int l = 0; l < layerCount; l++)
                    {
                        totalWeights[l] += oldAlphamaps[y, x, l];
                    }
                }
            }

            // Create a sorted list of indices by weight descending
            var sortedIndices = Enumerable.Range(0, layerCount)
                .OrderByDescending(i => totalWeights[i])
                .ToList();

            // Step 2: Build the analysis report
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Terrain Layer Analysis (Sorted by painted area):");
            sb.AppendLine("--------------------------------------------------");
            for (int i = 0; i < Mathf.Min(10, layerCount); i++)
            {
                int idx = sortedIndices[i];
                string layerName = originalLayers[idx] != null ? originalLayers[idx].name : "null";
                sb.AppendLine($"#{i + 1}: {layerName} (Paint Weight: {totalWeights[idx]:F1})");
            }
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine("\nDo you want to reorder these layers so the top layers are moved to the first 4 slots?");
            sb.AppendLine("This will make them render perfectly in Play Mode without lowering quality.");

            bool confirm = EditorUtility.DisplayDialog("Confirm Layer Optimization", 
                sb.ToString(), 
                "Yes, Optimize & Backup", "Cancel");

            if (!confirm) return;

            // Step 3: Perform backups first
            string terrainPath = AssetDatabase.GetAssetPath(terrainData);
            string terrainBackupPath = terrainPath + ".backup_reorder";
            if (AssetDatabase.CopyAsset(terrainPath, terrainBackupPath))
            {
                Debug.Log($"[OPTIMIZER] Created TerrainData backup at: {terrainBackupPath}");
            }

            string matPath = "Assets/Resources/TerrainAssets/Zone_12_Terrain_Mat.mat";
            Material terrainMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (terrainMat != null)
            {
                string matBackupPath = matPath + ".backup_reorder";
                AssetDatabase.DeleteAsset(matBackupPath); // Delete old backup if exists
                if (AssetDatabase.CopyAsset(matPath, matBackupPath))
                {
                    Debug.Log($"[OPTIMIZER] Created Material backup at: {matBackupPath}");
                }
            }

            // Step 4: Perform reordering
            TerrainLayer[] newLayers = new TerrainLayer[layerCount];
            float[,,] newAlphamaps = new float[h, w, layerCount];

            for (int i = 0; i < layerCount; i++)
            {
                int oldIdx = sortedIndices[i];
                newLayers[i] = originalLayers[oldIdx];
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int i = 0; i < layerCount; i++)
                    {
                        int oldIdx = sortedIndices[i];
                        newAlphamaps[y, x, i] = oldAlphamaps[y, x, oldIdx];
                    }
                }
            }

            // Apply changes
            Undo.RecordObject(terrainData, "Reorder Terrain Layers");
            terrainData.terrainLayers = newLayers;
            terrainData.SetAlphamaps(0, 0, newAlphamaps);
            EditorUtility.SetDirty(terrainData);

            // Step 5: Sync to material template
            if (terrainMat != null)
            {
                Undo.RecordObject(terrainMat, "Update Material Template after Reorder");
                // Clear all texture fields first
                for (int i = 0; i < 8; i++)
                {
                    terrainMat.SetTexture($"_Splat{i}", null);
                    terrainMat.SetTexture($"_Normal{i}", null);
                }
                
                // Bind new sorted layers (up to 8 layers)
                for (int i = 0; i < Mathf.Min(8, layerCount); i++)
                {
                    if (newLayers[i] != null)
                    {
                        terrainMat.SetTexture($"_Splat{i}", newLayers[i].diffuseTexture);
                        if (newLayers[i].normalMapTexture != null)
                        {
                            terrainMat.SetTexture($"_Normal{i}", newLayers[i].normalMapTexture);
                        }
                    }
                }
                terrainMat.SetFloat("_NumLayersCount", layerCount);
                EditorUtility.SetDirty(terrainMat);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", 
                "Successfully reordered layers!\n\nYour most used 4 layers are now in the first 4 slots. They will render beautifully in Play Mode.", 
                "OK");
        }
    }
}
