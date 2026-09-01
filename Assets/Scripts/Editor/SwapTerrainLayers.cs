using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace KO.Editor
{
    public class SwapTerrainLayers
    {
        [MenuItem("Tools/Terrain/Swap Ground003 with TL_fwOF_RockSurf_02")]
        public static void ExecuteSwap()
        {
            Terrain terrain = Selection.activeGameObject?.GetComponent<Terrain>() ?? Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
            TerrainData tData = terrain != null ? terrain.terrainData : AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Resources/TerrainAssets/Zone_12.asset");

            if (tData == null)
            {
                EditorUtility.DisplayDialog("Error", "Zone_12 terrain data bulunamadı!", "Tamam");
                return;
            }

            TerrainLayer[] layers = tData.terrainLayers;
            int w = tData.alphamapWidth;
            int h = tData.alphamapHeight;
            float[,,] maps = tData.GetAlphamaps(0, 0, w, h);

            // Find destination index (TL_fwOF_RockSurf_02)
            int dstIdx = -1;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null && layers[i].name.Contains("RockSurf_02"))
                {
                    dstIdx = i;
                    break;
                }
            }

            if (dstIdx == -1)
            {
                EditorUtility.DisplayDialog("Hata", "TL_fwOF_RockSurf_02 katmanı arazide bulunamadı!", "Tamam");
                return;
            }

            // Find all source layer indices that match Ground003 (by name or diffuse texture)
            List<int> srcIndices = new List<int>();
            for (int i = 0; i < layers.Length; i++)
            {
                if (i == dstIdx) continue;
                TerrainLayer l = layers[i];
                if (l != null)
                {
                    if (l.name.Equals("Ground003", System.StringComparison.OrdinalIgnoreCase) || 
                        (l.diffuseTexture != null && l.diffuseTexture.name.Equals("Ground003", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        srcIndices.Add(i);
                    }
                }
            }

            if (srcIndices.Count == 0)
            {
                EditorUtility.DisplayDialog("Bilgi", "Ground003 katmanı bulunamadı.", "Tamam");
                return;
            }

            int modifiedPixels = 0;
            float totalTransferredWeight = 0f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float transferSum = 0f;
                    foreach (int srcIdx in srcIndices)
                    {
                        float val = maps[y, x, srcIdx];
                        if (val > 0.0001f)
                        {
                            transferSum += val;
                            maps[y, x, srcIdx] = 0f;
                        }
                    }

                    if (transferSum > 0f)
                    {
                        maps[y, x, dstIdx] += transferSum;
                        totalTransferredWeight += transferSum;
                        modifiedPixels++;
                    }
                }
            }

            Undo.RecordObject(tData, "Swap Ground003 to RockSurf_02");
            tData.SetAlphamaps(0, 0, maps);
            EditorUtility.SetDirty(tData);
            AssetDatabase.SaveAssets();

            if (terrain != null) terrain.Flush();

            Debug.Log($"[TERRAIN-SWAP] Ground003 katmanları (İndeksler: {string.Join(", ", srcIndices)}) -> TL_fwOF_RockSurf_02 (İndeks {dstIdx}) aktarıldı. Toplam ağırlık: {totalTransferredWeight:F1}, Değişen piksel: {modifiedPixels}");
            EditorUtility.DisplayDialog("İşlem Başarılı", 
                $"Ground003 ile boyanmış tüm alanlar ({modifiedPixels} piksel, {totalTransferredWeight:F1} ağırlık) başarıyla TL_fwOF_RockSurf_02 katmanına aktarıldı!", 
                "Tamam");
        }
    }
}
