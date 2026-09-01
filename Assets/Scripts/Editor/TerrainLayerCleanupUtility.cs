using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace KO.Editor
{
    public class TerrainLayerCleanupUtility : EditorWindow
    {
        [MenuItem("Tools/Terrain/Dump Layer Info")]
        public static void DumpLayerInfo()
        {
            Terrain terrain = Selection.activeGameObject?.GetComponent<Terrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select KOTerrain_12 in hierarchy!", "OK");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null) return;

            TerrainLayer[] layers = terrainData.terrainLayers;
            List<string> lines = new List<string>();
            for (int i = 0; i < layers.Length; i++)
            {
                var l = layers[i];
                if (l != null)
                {
                    string path = AssetDatabase.GetAssetPath(l);
                    string texName = l.diffuseTexture != null ? l.diffuseTexture.name : "null";
                    string texPath = l.diffuseTexture != null ? AssetDatabase.GetAssetPath(l.diffuseTexture) : "null";
                    lines.Add($"Index {i}: LayerName='{l.name}', LayerPath='{path}', TextureName='{texName}', TexturePath='{texPath}'");
                }
                else
                {
                    lines.Add($"Index {i}: NULL");
                }
            }

            System.IO.File.WriteAllLines(@"C:\_dev\knightonline-mobil\layer_info.txt", lines.ToArray());
            EditorUtility.DisplayDialog("Dumped", "Successfully dumped layer info to C:\\_dev\\knightonline-mobil\\layer_info.txt", "OK");
        }

        [MenuItem("Tools/Terrain/Bake Painted Terrain to Composite")]
        public static void BakeTerrainToComposite()
        {
            Terrain terrain = Selection.activeGameObject?.GetComponent<Terrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select KOTerrain_12 in hierarchy!", "OK");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null) return;

            bool confirm = EditorUtility.DisplayDialog("Confirm Bake", 
                "This will bake your current custom painted terrain directly into a single-layer composite (Zone_12_Composite.png).\n\nThis will preserve all your painted grass, snow, and mountains, and make them render perfectly in Play Mode.\n\nDo you want to proceed?", 
                "Yes, Bake", "Cancel");

            if (!confirm) return;

            TerrainLayer[] layers = terrainData.terrainLayers;
            int layerCount = layers.Length;
            if (layerCount == 0) return;

            int w = terrainData.alphamapWidth;
            int h = terrainData.alphamapHeight;
            float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, w, h);

            Texture2D[] textures = new Texture2D[layerCount];
            for (int i = 0; i < layerCount; i++)
            {
                textures[i] = layers[i]?.diffuseTexture;
            }

            // Step 1: Make all textures readable automatically before baking loop
            for (int i = 0; i < layerCount; i++)
            {
                if (textures[i] != null)
                {
                    string texPath = AssetDatabase.GetAssetPath(textures[i]);
                    if (!string.IsNullOrEmpty(texPath))
                    {
                        TextureImporter texImporter = AssetImporter.GetAtPath(texPath) as TextureImporter;
                        if (texImporter != null && !texImporter.isReadable)
                        {
                            texImporter.isReadable = true;
                            texImporter.SaveAndReimport();
                        }
                    }
                }
            }

            int texSize = 4096; // 4096 is safe, fast, and high quality
            Texture2D compositeTex = new Texture2D(texSize, texSize, TextureFormat.RGB24, false);
            Color[] pixels = new Color[texSize * texSize];

            float terrainSizeX = terrainData.size.x;
            float terrainSizeZ = terrainData.size.z;

            // Step 2: Bake pixels (all textures are guaranteed readable now)
            for (int y = 0; y < texSize; y++)
            {
                float v = (float)y / texSize;
                int mapY = Mathf.Clamp(Mathf.FloorToInt(v * h), 0, h - 1);

                for (int x = 0; x < texSize; x++)
                {
                    float u = (float)x / texSize;
                    int mapX = Mathf.Clamp(Mathf.FloorToInt(u * w), 0, w - 1);

                    Color blendedColor = Color.black;
                    float totalWeight = 0f;

                    for (int l = 0; l < layerCount; l++)
                    {
                        float weight = alphamaps[mapY, mapX, l];
                        if (weight > 0.0001f && textures[l] != null)
                        {
                            float tileSizeX = layers[l].tileSize.x;
                            float tileSizeY = layers[l].tileSize.y;

                            float worldX = u * terrainSizeX;
                            float worldZ = v * terrainSizeZ;

                            float texU = (worldX / tileSizeX) % 1.0f;
                            if (texU < 0) texU += 1.0f;
                            float texV = (worldZ / tileSizeY) % 1.0f;
                            if (texV < 0) texV += 1.0f;

                            Color texColor = textures[l].GetPixelBilinear(texU, texV);
                            blendedColor += texColor * weight;
                            totalWeight += weight;
                        }
                    }

                    if (totalWeight > 0.0001f)
                    {
                        pixels[y * texSize + x] = blendedColor / totalWeight;
                    }
                    else
                    {
                        pixels[y * texSize + x] = Color.gray;
                    }
                }
            }

            compositeTex.SetPixels(pixels);
            compositeTex.Apply();

            // Save composite PNG
            byte[] pngBytes = compositeTex.EncodeToPNG();
            string compositePngPath = "Assets/Resources/TerrainAssets/Zone_12_Composite.png";
            string absolutePngPath = System.IO.Path.Combine(Application.dataPath, "Resources/TerrainAssets/Zone_12_Composite.png");
            
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(absolutePngPath));
            System.IO.File.WriteAllBytes(absolutePngPath, pngBytes);
            DestroyImmediate(compositeTex);

            AssetDatabase.ImportAsset(compositePngPath);

            // Set import settings
            TextureImporter importer = AssetImporter.GetAtPath(compositePngPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = texSize;
                importer.SaveAndReimport();
            }

            // Create/Update TerrainLayer
            string compositeLayerPath = "Assets/Resources/TerrainAssets/Zone_12_Composite.terrainlayer";
            TerrainLayer compositeLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(compositeLayerPath);
            if (compositeLayer == null)
            {
                compositeLayer = new TerrainLayer();
                AssetDatabase.CreateAsset(compositeLayer, compositeLayerPath);
            }
            
            Texture2D compositeAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(compositePngPath);
            compositeLayer.name = "Zone_12_Composite";
            compositeLayer.diffuseTexture = compositeAsset;
            compositeLayer.tileSize = new Vector2(terrainSizeX, terrainSizeZ);
            compositeLayer.smoothness = 0f;
            compositeLayer.metallic = 0f;
            EditorUtility.SetDirty(compositeLayer);

            // Update TerrainData to use ONLY composite layer
            Undo.RecordObject(terrainData, "Bake Terrain to Composite");
            terrainData.terrainLayers = new TerrainLayer[] { compositeLayer };

            // Fill alphamap with 1.0
            float[,,] newAlphamaps = new float[h, w, 1];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    newAlphamaps[y, x, 0] = 1f;
                }
            }
            terrainData.SetAlphamaps(0, 0, newAlphamaps);
            EditorUtility.SetDirty(terrainData);

            // Update Material Template
            string matPath = "Assets/Resources/TerrainAssets/Zone_12_Terrain_Mat.mat";
            Material terrainMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (terrainMat != null)
            {
                Undo.RecordObject(terrainMat, "Update Material Template");
                terrainMat.SetTexture("_Splat0", compositeAsset);
                for (int i = 1; i < 8; i++)
                {
                    terrainMat.SetTexture($"_Splat{i}", null);
                    terrainMat.SetTexture($"_Normal{i}", null);
                }
                terrainMat.SetFloat("_NumLayersCount", 1f);
                EditorUtility.SetDirty(terrainMat);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Bake Success", "Successfully baked your custom painted terrain!\n\nAll your custom painted layers are now baked into Zone_12_Composite.png, and the terrain is set to single-layer rendering. The mountains will render perfectly in Play Mode!", "OK");
        }

        [MenuItem("Tools/Terrain/Clean Unused Layers")]
        public static void CleanUnusedLayers()
        {
            Terrain terrain = Selection.activeGameObject?.GetComponent<Terrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select the KOTerrain_12 GameObject in the hierarchy first!", "OK");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null) return;

            TerrainLayer[] originalLayers = terrainData.terrainLayers;
            if (originalLayers == null || originalLayers.Length == 0) return;

            int w = terrainData.alphamapWidth;
            int h = terrainData.alphamapHeight;
            float[,,] oldAlphamaps = terrainData.GetAlphamaps(0, 0, w, h);
            int layerCount = originalLayers.Length;

            bool[] isUsed = new bool[layerCount];
            int forceRemovedCount = 0;

            for (int l = 0; l < layerCount; l++)
            {
                TerrainLayer layer = originalLayers[l];
                if (layer == null) continue;

                string assetPath = AssetDatabase.GetAssetPath(layer);
                bool isOriginalKO = string.IsNullOrEmpty(assetPath) || 
                                    assetPath.Contains("Resources/TerrainAssets") || 
                                    assetPath.Contains("TerrainAssets");

                if (isOriginalKO)
                {
                    isUsed[l] = false;
                    forceRemovedCount++;
                }
                else
                {
                    bool hasSignificantWeight = false;
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            if (oldAlphamaps[y, x, l] > 0.05f)
                            {
                                hasSignificantWeight = true;
                                break;
                            }
                        }
                        if (hasSignificantWeight) break;
                    }
                    isUsed[l] = hasSignificantWeight;
                }
            }

            List<TerrainLayer> keptLayers = new List<TerrainLayer>();
            Dictionary<int, int> oldToNewIndex = new Dictionary<int, int>();

            for (int l = 0; l < layerCount; l++)
            {
                if (isUsed[l])
                {
                    keptLayers.Add(originalLayers[l]);
                    oldToNewIndex[l] = keptLayers.Count - 1;
                }
            }

            if (keptLayers.Count == 0)
            {
                keptLayers.Add(originalLayers[0]);
                oldToNewIndex[0] = 0;
                isUsed[0] = true;
            }

            int removedCount = layerCount - keptLayers.Count;
            if (removedCount == 0)
            {
                EditorUtility.DisplayDialog("Info", "All layers are custom and significantly used. No layers were removed.", "OK");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog("Confirm Cleanup", 
                $"Found {keptLayers.Count} custom layers (with significant paint).\nWe will remove {removedCount} layers (including {forceRemovedCount} original KO textures).\n\nDo you want to proceed and clean up this Terrain's palette?", 
                "Yes, Clean Up", "Cancel");

            if (!confirm) return;

            Undo.RecordObject(terrainData, "Clean Unused Terrain Layers");

            int newLayerCount = keptLayers.Count;
            float[,,] newAlphamaps = new float[h, w, newLayerCount];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int l = 0; l < layerCount; l++)
                    {
                        if (isUsed[l])
                        {
                            int newIdx = oldToNewIndex[l];
                            float val = oldAlphamaps[y, x, l];
                            newAlphamaps[y, x, newIdx] = val;
                            sum += val;
                        }
                    }

                    if (sum > 0.0001f)
                    {
                        for (int nl = 0; nl < newLayerCount; nl++)
                        {
                            newAlphamaps[y, x, nl] /= sum;
                        }
                    }
                    else
                    {
                        newAlphamaps[y, x, 0] = 1f;
                        for (int nl = 1; nl < newLayerCount; nl++)
                        {
                            newAlphamaps[y, x, nl] = 0f;
                        }
                    }
                }
            }

            terrainData.terrainLayers = keptLayers.ToArray();
            terrainData.SetAlphamaps(0, 0, newAlphamaps);

            EditorUtility.SetDirty(terrainData);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Success", 
                $"Successfully cleaned up the terrain!\n\n- Kept: {newLayerCount} custom layers\n- Removed: {removedCount} layers total ({forceRemovedCount} original KO textures completely purged).", 
                "OK");
        }

        [MenuItem("Tools/Terrain/Fix & Sync All Terrain Layers")]
        public static void FixTerrainLayersAndMaterials()
        {
            Terrain terrain = Selection.activeGameObject?.GetComponent<Terrain>() ?? Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Error", "No active Terrain found! Please select your Terrain (e.g. KOTerrain_12) in hierarchy.", "OK");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
            {
                EditorUtility.DisplayDialog("Error", "Terrain does not have a TerrainData assigned!", "OK");
                return;
            }

            // Step 1: Backup TerrainData and Material
            string terrainPath = AssetDatabase.GetAssetPath(terrainData);
            if (!string.IsNullOrEmpty(terrainPath))
            {
                string terrainBackup = terrainPath + ".backup_fix_layers";
                AssetDatabase.DeleteAsset(terrainBackup);
                AssetDatabase.CopyAsset(terrainPath, terrainBackup);
            }

            Material terrainMat = terrain.materialTemplate;
            if (terrainMat == null)
            {
                string defaultMatPath = "Assets/Resources/TerrainAssets/Zone_12_Terrain_Mat.mat";
                terrainMat = AssetDatabase.LoadAssetAtPath<Material>(defaultMatPath);
            }

            if (terrainMat != null)
            {
                string matPath = AssetDatabase.GetAssetPath(terrainMat);
                if (!string.IsNullOrEmpty(matPath))
                {
                    string matBackup = matPath + ".backup_fix_layers";
                    AssetDatabase.DeleteAsset(matBackup);
                    AssetDatabase.CopyAsset(matPath, matBackup);
                }
            }

            TerrainLayer[] originalLayers = terrainData.terrainLayers;
            int oldLayerCount = originalLayers != null ? originalLayers.Length : 0;
            int w = terrainData.alphamapWidth;
            int h = terrainData.alphamapHeight;
            float[,,] oldAlphamaps = (oldLayerCount > 0 && w > 0 && h > 0) ? terrainData.GetAlphamaps(0, 0, w, h) : null;

            // Step 2: Build clean unique list of layers
            List<TerrainLayer> cleanLayers = new List<TerrainLayer>();
            HashSet<Texture2D> seenTextures = new HashSet<Texture2D>();
            HashSet<string> seenNames = new HashSet<string>();
            Dictionary<int, int> oldToNewMapping = new Dictionary<int, int>();

            for (int i = 0; i < oldLayerCount; i++)
            {
                TerrainLayer layer = originalLayers[i];
                if (layer == null) continue;

                Texture2D diff = layer.diffuseTexture;
                // Check if valid
                if (diff == null || diff.name.EndsWith("_S") || diff.name.EndsWith("_mask"))
                {
                    Debug.LogWarning($"[TERRAIN] Skipping broken/black layer: {layer.name}");
                    continue;
                }

                // If composite layer (index 0) or unique diffuse
                if (cleanLayers.Count == 0 || (!seenTextures.Contains(diff) && !seenNames.Contains(layer.name)))
                {
                    cleanLayers.Add(layer);
                    seenTextures.Add(diff);
                    seenNames.Add(layer.name);
                    oldToNewMapping[i] = cleanLayers.Count - 1;
                }
                else
                {
                    // Map duplicate to existing layer
                    int existingIdx = cleanLayers.FindIndex(l => l.diffuseTexture == diff || l.name == layer.name);
                    if (existingIdx >= 0)
                    {
                        oldToNewMapping[i] = existingIdx;
                    }
                }
            }

            if (cleanLayers.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No valid TerrainLayers found to keep!", "OK");
                return;
            }

            int newLayerCount = cleanLayers.Count;

            // Step 3: Remap Alphamaps cleanly
            Undo.RecordObject(terrainData, "Fix Terrain Layers & Alphamaps");
            if (oldAlphamaps != null)
            {
                float[,,] newAlphamaps = new float[h, w, newLayerCount];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float sum = 0f;
                        for (int oldIdx = 0; oldIdx < oldLayerCount; oldIdx++)
                        {
                            if (oldToNewMapping.TryGetValue(oldIdx, out int newIdx))
                            {
                                float val = oldAlphamaps[y, x, oldIdx];
                                newAlphamaps[y, x, newIdx] += val;
                                sum += val;
                            }
                        }

                        if (sum > 0.0001f)
                        {
                            for (int l = 0; l < newLayerCount; l++)
                            {
                                newAlphamaps[y, x, l] /= sum;
                            }
                        }
                        else
                        {
                            newAlphamaps[y, x, 0] = 1f;
                            for (int l = 1; l < newLayerCount; l++)
                            {
                                newAlphamaps[y, x, l] = 0f;
                            }
                        }
                    }
                }
                terrainData.terrainLayers = cleanLayers.ToArray();
                terrainData.SetAlphamaps(0, 0, newAlphamaps);
            }
            else
            {
                terrainData.terrainLayers = cleanLayers.ToArray();
            }

            // Step 4: Sync to Material Template
            if (terrainMat != null)
            {
                Undo.RecordObject(terrainMat, "Sync Material Template Layers");
                terrainMat.SetFloat("_NumLayersCount", Mathf.Min(8, newLayerCount));

                for (int i = 0; i < 8; i++)
                {
                    if (i < newLayerCount && cleanLayers[i] != null)
                    {
                        terrainMat.SetTexture($"_Splat{i}", cleanLayers[i].diffuseTexture);
                        terrainMat.SetTexture($"_Normal{i}", cleanLayers[i].normalMapTexture);
                    }
                    else
                    {
                        terrainMat.SetTexture($"_Splat{i}", null);
                        terrainMat.SetTexture($"_Normal{i}", null);
                    }
                }
                EditorUtility.SetDirty(terrainMat);
            }

            EditorUtility.SetDirty(terrainData);
            AssetDatabase.SaveAssets();
            terrain.Flush();

            Debug.Log($"[TERRAIN] Fixed and synchronized terrain layers. Clean Layers: {newLayerCount} (Old: {oldLayerCount})");
            EditorUtility.DisplayDialog("Success", 
                $"Terrain layers fixed & synchronized!\n\n- Active unique layers: {newLayerCount}\n- Broken/duplicate layers removed.\n- Material splat slots updated.\n\nYou can now paint with any layer without texture mismatch!", 
                "OK");
        }
    }
}
