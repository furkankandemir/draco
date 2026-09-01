using System;

using UnityEngine;

using UnityEditor;

using System.IO;

using System.Collections.Generic;

using EntropyOnline.Import;

using EntropyOnline.World;



namespace EntropyOnline.Editor

{

    public class KOTerrainExporterWindow : EditorWindow

    {

        private short selectedZoneId = 21; // Default Moradon



        [MenuItem("Entropy Online/Terrain Exporter Tool", false, 30)]

        public static void ShowWindow()

        {

            GetWindow<KOTerrainExporterWindow>("KO Terrain Exporter");

        }



        private void OnGUI()

        {

            GUILayout.Label("Knight Online Terrain Exporter", EditorStyles.boldLabel);

            EditorGUILayout.Space();



            // List available zones

            var zones = KOZoneMapper.GetAllZones();

            var zoneOptions = new List<string>();

            var zoneIds = new List<short>();



            foreach (var kvp in zones)

            {

                zoneOptions.Add($"Zone {kvp.Key}: {kvp.Value.ZoneName} ({kvp.Value.GtdFile})");

                zoneIds.Add(kvp.Key);

            }



            int selectedIndex = zoneIds.IndexOf(selectedZoneId);

            if (selectedIndex < 0) selectedIndex = 0;



            selectedIndex = EditorGUILayout.Popup("Select Zone to Export", selectedIndex, zoneOptions.ToArray());

            selectedZoneId = zoneIds[selectedIndex];



            EditorGUILayout.Space();



            if (GUILayout.Button("Convert and Export Terrain to Assets", GUILayout.Height(40)))

            {

                ExportTerrain(selectedZoneId);

            }







            EditorGUILayout.Space();

            GUILayout.Label("Scene Objects (Editor Only)", EditorStyles.boldLabel);

            EditorGUILayout.Space();



            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Load & Place Objects in Scene", GUILayout.Height(30)))

            {

                PlaceObjectsInScene(selectedZoneId);

            }

            if (GUILayout.Button("Clear Objects from Scene", GUILayout.Height(30)))

            {

                ClearObjectsFromScene(selectedZoneId);

            }

            GUILayout.EndHorizontal();



            EditorGUILayout.Space();

            GUILayout.Label("Save Editor Scene Objects to Zone Asset", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox("Use the buttons below to save your new scene edits into the compiled zone asset.", MessageType.Info);



            if (GUILayout.Button("Auto-Detect and Append New Objects (Recommended)", GUILayout.Height(40)))

            {

                AutoDetectAndAppendNewObjects(selectedZoneId);

            }



            EditorGUILayout.Space();

            GUILayout.Label("Manual Serialization (Advanced)", EditorStyles.miniBoldLabel);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Append Selected", GUILayout.Height(30)))

            {

                SaveSelectedObjectsToZoneAsset(selectedZoneId, append: true);

            }

            if (GUILayout.Button("Overwrite with Selected", GUILayout.Height(30)))

            {

                SaveSelectedObjectsToZoneAsset(selectedZoneId, append: false);

            }

            GUILayout.EndHorizontal();



            EditorGUILayout.Space();

            if (GUILayout.Button("Debug GTD Tiles", GUILayout.Height(30)))

            {

                DebugGtdTiles(selectedZoneId);

            }

        }



        public void ExportTerrain(short zoneId, bool showDialog = true, bool instantiateInScene = true)

        {

            var zoneInfo = KOZoneMapper.GetZoneInfo(zoneId);

            if (zoneInfo == null)

            {

                Debug.LogError($"[EXPORTER] Zone info not found for zone {zoneId}");

                return;

            }



            string gtdPath = KOZoneMapper.GetGtdPath(zoneId);

            if (string.IsNullOrEmpty(gtdPath) || !KOBinaryProvider.Exists(gtdPath))

            {

                Debug.LogError($"[EXPORTER] GTD file not found: {gtdPath}");

                return;

            }



            Debug.Log($"[EXPORTER] Exporting Zone {zoneId}: {zoneInfo.ZoneName} from {gtdPath}...");



            // Create target folders if they don't exist

            if (!Directory.Exists("Assets/Resources/TerrainAssets"))

            {

                Directory.CreateDirectory("Assets/Resources/TerrainAssets");

                AssetDatabase.Refresh();

            }



            // 1. Parse GTD Data

            var gtdData = GtdTerrainImporter.Parse(gtdPath);

            if (gtdData == null)

            {

                Debug.LogError("[EXPORTER] Failed to parse GTD data.");

                return;

            }



            // 2. Create TerrainData

            TerrainData terrainData = GtdTerrainImporter.CreateTerrainData(gtdData);

            if (terrainData == null)

            {

                Debug.LogError("[EXPORTER] Failed to create TerrainData.");

                return;

            }



            // 3. Save TerrainData Asset

            string assetPath = $"Assets/Resources/TerrainAssets/Zone_{zoneId}.asset";

            AssetDatabase.CreateAsset(terrainData, assetPath);



            // 4. Validate tile texture data

            int mapSize = gtdData.MapSize;

            int maxTileIdx = gtdData.TileTextures.Count;

            int cellCount = mapSize - 1;



            if (gtdData.TileTextures == null || gtdData.TileTextures.Count == 0 ||

                gtdData.TileTexSources == null || gtdData.TileTexSources.Count == 0 ||

                gtdData.CellData == null)

            {

                if (showDialog)

                {

                    Debug.LogError("[EXPORTER] Missing tile texture data in GTD.");

                }

                else

                {

                    Debug.LogWarning($"[EXPORTER] Zone {zoneId} ({zoneInfo.ZoneName}) has no terrain tile textures in its GTD (likely a dungeon/indoor map). Skipping terrain creation.");

                }

                return;

            }



            // 5. Load colormap texture

            Texture2D colormapAsset = null;

            if (zoneInfo != null && !string.IsNullOrEmpty(zoneInfo.DxtFile))

            {

                string resourcePath = $"KOTextures/Zones/{Path.GetFileNameWithoutExtension(zoneInfo.DxtFile).ToLowerInvariant()}";

                colormapAsset = Resources.Load<Texture2D>(resourcePath);



                if (colormapAsset == null)

                {

                    string dxtPath = Path.Combine("Zones", zoneInfo.DxtFile);

                    colormapAsset = KOTextureProvider.Load(dxtPath, flipY: false);

                }

            }



            if (colormapAsset != null)

            {

                string colormapPath = AssetDatabase.GetAssetPath(colormapAsset);

                if (!string.IsNullOrEmpty(colormapPath))

                {

                    TextureImporter importer = AssetImporter.GetAtPath(colormapPath) as TextureImporter;

                    if (importer != null && !importer.isReadable)

                    {

                        importer.isReadable = true;

                        importer.SaveAndReimport();

                        colormapAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(colormapPath);

                    }

                }

            }

            else

            {

                colormapAsset = new Texture2D(256, 256);

                var colors = new Color[256 * 256];

                for (int c = 0; c < colors.Length; c++) colors[c] = new Color(0.2f, 0.4f, 0.2f);

                colormapAsset.SetPixels(colors);

                colormapAsset.Apply();

            }



            // 6. Load unique tile textures into cache

            var tileCache = new Dictionary<int, Texture2D>();

            for (int x = 0; x < mapSize; x++)

            {

                for (int z = 0; z < mapSize; z++)

                {

                    var cell = gtdData.CellData[x, z];

                    int idx = cell.Tex1Idx;

                    if (idx < 0 || idx >= maxTileIdx) continue;

                    if (tileCache.ContainsKey(idx)) continue;



                    var info = gtdData.TileTextures[idx];

                    if (info.SrcIdx >= 0 && info.SrcIdx < gtdData.TileTexSources.Count)

                    {

                        string gttRel = gtdData.TileTexSources[info.SrcIdx]

                            .Replace('/', Path.DirectorySeparatorChar)

                            .Replace('\\', Path.DirectorySeparatorChar);

                        string gttFull = Path.Combine("", gttRel);

                        Texture2D tex = GttTextureImporter.LoadTile(gttFull, info.TileIdx);

                        tex = LoadReplacementTile(gttRel, info.TileIdx, tex);

                        tileCache[idx] = tex;

                    }



                    int idx2 = cell.Tex2Idx;

                    if (idx2 >= 0 && idx2 < maxTileIdx && !tileCache.ContainsKey(idx2))

                    {

                        var info2 = gtdData.TileTextures[idx2];

                        if (info2.SrcIdx >= 0 && info2.SrcIdx < gtdData.TileTexSources.Count)

                        {

                            string gttRel2 = gtdData.TileTexSources[info2.SrcIdx]

                                .Replace('/', Path.DirectorySeparatorChar)

                                .Replace('\\', Path.DirectorySeparatorChar);

                            string gttFull2 = Path.Combine("", gttRel2);

                            Texture2D tex2 = GttTextureImporter.LoadTile(gttFull2, info2.TileIdx);

                            tex2 = LoadReplacementTile(gttRel2, info2.TileIdx, tex2);

                            tileCache[idx2] = tex2;

                        }

                    }

                }

            }



            // 7. Build composite texture

            int maxTexSize = Mathf.Min(8192, SystemInfo.maxTextureSize); // Use 8192 for Editor export to balance quality and file size

            int pixPerCell = Mathf.Max(1, maxTexSize / cellCount);

            int compositeSize = cellCount * pixPerCell;

            if (compositeSize > maxTexSize) { compositeSize = maxTexSize; pixPerCell = compositeSize / cellCount; }

            if (pixPerCell < 1) pixPerCell = 1;

            compositeSize = cellCount * pixPerCell;



            Debug.Log($"[EXPORTER] Generating composite texture: {compositeSize}x{compositeSize} ({pixPerCell}px/cell)...");



            var pixels = new Color32[compositeSize * compositeSize];

            Color32[] cmPixels = colormapAsset.GetPixels32();

            int cmW = colormapAsset.width;

            int cmH = colormapAsset.height;

            var fallbackColor = new Color32(80, 100, 60, 255);



            for (int py = 0; py < compositeSize; py++)

            {

                for (int px = 0; px < compositeSize; px++)

                {

                    if (cmPixels != null)

                    {

                        float u = (float)px / compositeSize;

                        float v = (float)py / compositeSize;

                        int cx2 = Mathf.Clamp((int)(u * cmW), 0, cmW - 1);

                        int cy2 = Mathf.Clamp((int)(v * cmH), 0, cmH - 1);

                        var c = cmPixels[cy2 * cmW + cx2];

                        c.a = 255;

                        pixels[py * compositeSize + px] = c;

                    }

                    else

                    {

                        pixels[py * compositeSize + px] = fallbackColor;

                    }

                }

            }



            // Direction mappings (LyTerrain.cpp:118-157 — _KNIGHT variant)

            float[,] tileDirU = new float[8, 4]

            {

                { 0f, 1f, 0f, 1f },  // 0: up

                { 0f, 0f, 1f, 1f },  // 1: right (90° CW)

                { 1f, 0f, 1f, 0f },  // 2: bottom (180°)

                { 1f, 1f, 0f, 0f },  // 3: left (270° CW)

                { 1f, 0f, 1f, 0f },  // 4: up_mirror

                { 0f, 0f, 1f, 1f },  // 5: right_mirror

                { 0f, 1f, 0f, 1f },  // 6: bottom_mirror

                { 1f, 1f, 0f, 0f },  // 7: left_mirror

            };

            float[,] tileDirV = new float[8, 4]

            {

                { 0f, 0f, 1f, 1f },  // 0: up

                { 1f, 0f, 1f, 0f },  // 1: right

                { 1f, 1f, 0f, 0f },  // 2: bottom

                { 0f, 1f, 0f, 1f },  // 3: left

                { 0f, 0f, 1f, 1f },  // 4: up_mirror

                { 0f, 1f, 0f, 1f },  // 5: right_mirror

                { 1f, 1f, 0f, 0f },  // 6: bottom_mirror

                { 1f, 0f, 1f, 0f },  // 7: left_mirror

            };



            // Apply overlays

            for (int cx = 0; cx < cellCount; cx++)

            {

                for (int cz = 0; cz < cellCount; cz++)

                {

                    var cell = gtdData.CellData[cx, cz];

                    int tex1Idx = cell.Tex1Idx;

                    if (tex1Idx < 0 || tex1Idx >= maxTileIdx) continue;



                    Texture2D tileTex = null;

                    tileCache.TryGetValue(tex1Idx, out tileTex);

                    if (tileTex == null) continue;



                    int tex1Dir = cell.Tex1Dir;

                    if (tex1Dir < 0 || tex1Dir > 7) tex1Dir = 0;



                    int tw = tileTex.width;

                    int th = tileTex.height;

                    var tilePixels = tileTex.GetPixels32();



                    int startX = cx * pixPerCell;

                    int startY = cz * pixPerCell;



                    float uLB = tileDirU[tex1Dir, 2], uRB = tileDirU[tex1Dir, 3];

                    float uLT = tileDirU[tex1Dir, 0], uRT = tileDirU[tex1Dir, 1];

                    float vLB = tileDirV[tex1Dir, 2], vRB = tileDirV[tex1Dir, 3];

                    float vLT = tileDirV[tex1Dir, 0], vRT = tileDirV[tex1Dir, 1];



                    // Tex2 ADD blending

                    int tex2Idx = cell.Tex2Idx;

                    int tex2Dir = cell.Tex2Dir;

                    Texture2D tileTex2 = null;

                    Color32[] tilePixels2 = null;

                    int tw2 = 0, th2 = 0;

                    bool hasTex2 = (tex2Idx >= 0 && tex2Idx < maxTileIdx);

                    if (hasTex2)

                    {

                        tileCache.TryGetValue(tex2Idx, out tileTex2);

                        if (tileTex2 != null)

                        {

                            tw2 = tileTex2.width;

                            th2 = tileTex2.height;

                            tilePixels2 = tileTex2.GetPixels32();

                            if (tex2Dir < 0 || tex2Dir > 7) tex2Dir = 0;

                        }

                        else hasTex2 = false;

                    }



                    float u2LB = 0, u2RB = 0, u2LT = 0, u2RT = 0;

                    float v2LB = 0, v2RB = 0, v2LT = 0, v2RT = 0;

                    if (hasTex2)

                    {

                        u2LB = tileDirU[tex2Dir, 2]; u2RB = tileDirU[tex2Dir, 3];

                        u2LT = tileDirU[tex2Dir, 0]; u2RT = tileDirU[tex2Dir, 1];

                        v2LB = tileDirV[tex2Dir, 2]; v2RB = tileDirV[tex2Dir, 3];

                        v2LT = tileDirV[tex2Dir, 0]; v2RT = tileDirV[tex2Dir, 1];

                    }



                    // For cells with IsTileFull = false (transition cells), we blend using the tile texture alpha channel.

                    // If IsTileFull = true, we overwrite (opacity = 100%).

                    bool isFull = cell.IsTileFull;



                    for (int py = 0; py < pixPerCell; py++)

                    {

                        float t = (float)py / pixPerCell;

                        for (int px = 0; px < pixPerCell; px++)

                        {

                            float s = (float)px / pixPerCell;



                            float u = (1f - s) * (1f - t) * uLB + s * (1f - t) * uRB

                                    + (1f - s) * t * uLT + s * t * uRT;

                            float v = (1f - s) * (1f - t) * vLB + s * (1f - t) * vRB

                                    + (1f - s) * t * vLT + s * t * vRT;



                            int txS = Mathf.Clamp((int)(u * tw), 0, tw - 1);

                            int tyS = Mathf.Clamp((int)(v * th), 0, th - 1);



                            int compIdx = (startY + py) * compositeSize + (startX + px);

                            int tileIdx1 = tyS * tw + txS;

                            if (compIdx < pixels.Length && tileIdx1 < tilePixels.Length)

                            {

                                var c1 = tilePixels[tileIdx1];

                                Color32 finalTileColor;



                                if (hasTex2)

                                {

                                    float u2 = (1f - s) * (1f - t) * u2LB + s * (1f - t) * u2RB

                                             + (1f - s) * t * u2LT + s * t * u2RT;

                                    float v2 = (1f - s) * (1f - t) * v2LB + s * (1f - t) * v2RB

                                             + (1f - s) * t * v2LT + s * t * v2RT;

                                    int tx2S = Mathf.Clamp((int)(u2 * tw2), 0, tw2 - 1);

                                    int ty2S = Mathf.Clamp((int)(v2 * th2), 0, th2 - 1);

                                    int tileIdx2 = ty2S * tw2 + tx2S;

                                    if (tileIdx2 < tilePixels2.Length)

                                    {

                                        var c2 = tilePixels2[tileIdx2];

                                        byte r = (byte)Mathf.Min(c1.r + c2.r, 255);

                                        byte g = (byte)Mathf.Min(c1.g + c2.g, 255);

                                        byte b = (byte)Mathf.Min(c1.b + c2.b, 255);

                                        finalTileColor = new Color32(r, g, b, c1.a);

                                    }

                                    else

                                    {

                                        finalTileColor = c1;

                                    }

                                }

                                else

                                {

                                    finalTileColor = c1;

                                }



                                float alpha = isFull ? 1.0f : (finalTileColor.a / 255f);

                                if (alpha > 0.001f)

                                {

                                    var bg = pixels[compIdx];

                                    byte r = (byte)(finalTileColor.r * alpha + bg.r * (1f - alpha));

                                    byte g = (byte)(finalTileColor.g * alpha + bg.g * (1f - alpha));

                                    byte b = (byte)(finalTileColor.b * alpha + bg.b * (1f - alpha));

                                    pixels[compIdx] = new Color32(r, g, b, 255);

                                }

                            }

                        }

                    }

                }

            }



            // Cleanup tileCache

            foreach (var kv in tileCache)

            {

                if (kv.Value != null) DestroyImmediate(kv.Value);

            }

            tileCache.Clear();



            // Save composite texture as PNG

            Texture2D compositeTex = new Texture2D(compositeSize, compositeSize, TextureFormat.RGBA32, false);

            compositeTex.SetPixels32(pixels);

            compositeTex.Apply();



            byte[] compositePngBytes = compositeTex.EncodeToPNG();

            string compositePngPath = $"Assets/Resources/TerrainAssets/Zone_{zoneId}_Composite.png";

            File.WriteAllBytes(compositePngPath, compositePngBytes);

            DestroyImmediate(compositeTex);



            AssetDatabase.ImportAsset(compositePngPath);



            // Configure composite texture import settings

            TextureImporter compositeImporter = AssetImporter.GetAtPath(compositePngPath) as TextureImporter;

            if (compositeImporter != null)

            {

                compositeImporter.wrapMode = TextureWrapMode.Clamp;

                compositeImporter.textureCompression = TextureImporterCompression.CompressedHQ;

                compositeImporter.maxTextureSize = compositeSize;

                compositeImporter.mipmapEnabled = true;

                compositeImporter.SaveAndReimport();

            }



            Texture2D compositeAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(compositePngPath);



            // Create TerrainLayer asset

            TerrainLayer compositeLayer = new TerrainLayer();

            compositeLayer.name = $"Zone_{zoneId}_Composite";

            compositeLayer.diffuseTexture = compositeAsset;

            compositeLayer.tileSize = new Vector2(gtdData.WorldSize, gtdData.WorldSize);

            compositeLayer.smoothness = 0f;

            compositeLayer.metallic = 0f;



            string compositeLayerPath = $"Assets/Resources/TerrainAssets/Zone_{zoneId}_Composite.terrainlayer";

            AssetDatabase.CreateAsset(compositeLayer, compositeLayerPath);



            terrainData.terrainLayers = new TerrainLayer[] { compositeLayer };



            // Paint the terrain automatically (100% weight for composite layer)

            terrainData.alphamapResolution = 512;

            terrainData.baseMapResolution = 4096;

            int alphaRes = terrainData.alphamapResolution;

            float[,,] alphamaps = new float[alphaRes, alphaRes, 1];

            for (int y = 0; y < alphaRes; y++)

            {

                for (int x = 0; x < alphaRes; x++)

                {

                    alphamaps[y, x, 0] = 1.0f;

                }

            }

            terrainData.SetAlphamaps(0, 0, alphamaps);



            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();



            // 7.5 Create & Save custom Terrain/Lit material template (saved to resource folder for runtime loading)

            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");

            if (terrainShader == null)

            {

                throw new System.InvalidOperationException("[EXPORTER] 'Universal Render Pipeline/Terrain/Lit' shader not found! " +

                    "Please ensure the URP package is installed, active, and there are no compiler errors.");

            }

            var terrainMat = new Material(terrainShader);

            

            // Zero out smoothness and metallic for all potential layers to eliminate sheen

            for (int layerIdx = 0; layerIdx < 4; layerIdx++)

            {

                if (terrainMat.HasProperty($"_Smoothness{layerIdx}"))

                    terrainMat.SetFloat($"_Smoothness{layerIdx}", 0f);

                if (terrainMat.HasProperty($"_Metallic{layerIdx}"))

                    terrainMat.SetFloat($"_Metallic{layerIdx}", 0f);

            }



            // Completely disable specular highlights and reflections to enforce absolute matte terrain

            if (terrainMat.HasProperty("_SpecularHighlights"))

                terrainMat.SetFloat("_SpecularHighlights", 0f);

            if (terrainMat.HasProperty("_EnvironmentReflections"))

                terrainMat.SetFloat("_EnvironmentReflections", 0f);

            terrainMat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");

            terrainMat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");



            string terrainMatPath = $"Assets/Resources/TerrainAssets/Zone_{zoneId}_Terrain_Mat.mat";

            AssetDatabase.CreateAsset(terrainMat, terrainMatPath);

            AssetDatabase.SaveAssets();



            // 8. Instantiate Terrain in active scene

            if (instantiateInScene)

            {

                string terrainGoName = $"KOTerrain_{zoneId}";

                var oldGo = GameObject.Find(terrainGoName);

                if (oldGo != null)

                {

                    DestroyImmediate(oldGo);

                }



                var terrainObj = Terrain.CreateTerrainGameObject(terrainData);

                terrainObj.name = terrainGoName;

                terrainObj.transform.position = new Vector3(0, GtdTerrainImporter.LastTerrainBaseY, 0);



                var collider = terrainObj.GetComponent<TerrainCollider>();

                if (collider != null) collider.terrainData = terrainData;



                // Make it static

                terrainObj.isStatic = true;



                var terrain = terrainObj.GetComponent<Terrain>();

                if (terrain != null)

                {

                    terrain.basemapDistance = 100000f;

                    terrain.drawInstanced = true;

                    terrain.heightmapPixelError = 5f;

                    terrain.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                    terrain.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>(terrainMatPath);

                }



                // Save the scene

                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);

                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);

            }



            if (showDialog)

            {

                EditorUtility.DisplayDialog("Success", $"Zone {zoneId} ({zoneInfo.ZoneName}) exported successfully!\n" +

                    $"The terrain composite texture has been generated and applied successfully!", "OK");

            }

        }



        public void ExportAllTerrains()

        {

            var zones = KOZoneMapper.GetAllZones();

            int count = 0;

            int total = zones.Count;

            

            Debug.Log($"[EXPORTER] Starting bulk export of all mapped terrains...");

            

            foreach (var kvp in zones)

            {

                short zoneId = kvp.Key;

                string gtdPath = KOZoneMapper.GetGtdPath(zoneId);

                

                if (string.IsNullOrEmpty(gtdPath) || !KOBinaryProvider.Exists(gtdPath))

                {

                    Debug.LogWarning($"[EXPORTER] Skipping Zone {zoneId} ({kvp.Value.ZoneName}) because GTD file is missing: {gtdPath}");

                    continue;

                }

                

                try

                {

                    EditorUtility.DisplayProgressBar("Exporting All Terrains", $"Exporting Zone {zoneId} ({kvp.Value.ZoneName})...", (float)count / total);

                    ExportTerrain(zoneId, showDialog: false, instantiateInScene: false);

                    count++;

                }

                catch (System.Exception ex)

                {

                    Debug.LogError($"[EXPORTER] Failed to export terrain for Zone {zoneId} ({kvp.Value.ZoneName}): {ex.Message}");

                }

            }

            

            EditorUtility.ClearProgressBar();

            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();

            

            Debug.Log($"[EXPORTER] Bulk export completed! Successfully exported {count}/{total} terrains.");

            EditorUtility.DisplayDialog("Bulk Export", $"Successfully exported {count}/{total} terrains to assets!\nThey are configured as fully matte URP terrain materials.", "OK");

        }



        public void ClearObjectsFromScene(short zoneId)

        {

            string parentName = $"KOObjects_{zoneId}";

            var parentGo = GameObject.Find(parentName);

            if (parentGo != null)

            {

                DestroyImmediate(parentGo);

                Debug.Log($"[EXPORTER] Cleared objects for Zone {zoneId} from scene.");

            }

            else

            {

                var oldKo = GameObject.Find("KOObjects");

                if (oldKo != null) DestroyImmediate(oldKo);

            }

        }



        public void PlaceObjectsInScene(short zoneId)

        {

            // Clear existing first

            ClearObjectsFromScene(zoneId);



            var zoneInfo = KOZoneMapper.GetZoneInfo(zoneId);

            if (zoneInfo == null)

            {

                Debug.LogError($"[EXPORTER] Zone info not found for zone {zoneId}");

                return;

            }



            string opdPath = Path.Combine("Zones", zoneInfo.OpdFile);

            if (!KOBinaryProvider.Exists(opdPath))

            {

                Debug.LogError($"[EXPORTER] OPD file not found: {opdPath}");

                return;

            }



            Debug.Log($"[EXPORTER] Parsing OPD for Zone {zoneId}: {opdPath}...");

            N3ShapeParser.OpdFullData opdFull = null;

            try

            {

                opdFull = N3ShapeParser.ParseFull(opdPath);

            }

            catch (System.Exception ex)

            {

                Debug.LogError($"[EXPORTER] OPD parse exception: {ex.Message}");

                return;

            }



            if (opdFull == null || opdFull.Shapes.Count == 0)

            {

                Debug.LogError($"[EXPORTER] OPD file empty or parse failed.");

                return;

            }



            GameObject parentGo = new GameObject($"KOObjects_{zoneId}");



            // Load and instantiate Water System (Rivers/Ponds) in the Editor

            string gtdPath = KOZoneMapper.GetGtdPath(zoneId);

            if (!string.IsNullOrEmpty(gtdPath) && KOBinaryProvider.Exists(gtdPath))

            {

                try

                {

                    var gtdData = GtdTerrainImporter.Parse(gtdPath);

                    if (gtdData != null && (gtdData.Rivers.Count > 0 || gtdData.Ponds.Count > 0))

                    {

                        var waterObj = new GameObject("WaterSystem");

                        waterObj.transform.SetParent(parentGo.transform, false);

                        var waterRenderer = waterObj.AddComponent<WaterRenderer>();

                        waterRenderer.Initialize(gtdData, zoneId);

                        Debug.Log($"[EXPORTER] Successfully placed WaterSystem in Editor scene (Rivers={gtdData.Rivers.Count}, Ponds={gtdData.Ponds.Count}).");

                    }

                }

                catch (System.Exception ex)

                {

                    Debug.LogWarning($"[EXPORTER] Failed to load water system for Zone {zoneId}: {ex.Message}");

                }

            }



            int placed = 0;



            foreach (var shape in opdFull.Shapes)

            {

                var t = shape.Transform;

                if (t == null) continue;

                if (t.Position.sqrMagnitude < 0.001f) continue;



                bool isKarusGate = zoneId == 1 && !string.IsNullOrEmpty(t.Name) && t.Name == "obj_ka_transmark00";

                bool isKarusGatePart = zoneId == 1 && !string.IsNullOrEmpty(t.Name) && t.Name.StartsWith("obj_ka_transmark") && !isKarusGate;



                if (isKarusGatePart)

                {

                    // Eski kapının tabelasını ve diğer parçalarını editor sahnesinde de yükleme (pembe tabela kalıntısını önler)

                    placed++;

                    continue;

                }



                var shapeObj = new GameObject(string.IsNullOrEmpty(t.Name) ? $"Shape_{placed}" : t.Name);

                shapeObj.transform.SetParent(parentGo.transform);

                shapeObj.transform.position = t.Position;

                if (t.Rotation.w != 0 || t.Rotation.x != 0 || t.Rotation.y != 0 || t.Rotation.z != 0)

                {

                    shapeObj.transform.rotation = t.Rotation;

                }

                shapeObj.transform.localScale = t.Scale;



                if (isKarusGate)

                {

                    // Editor sahnesinde de Karus kapılarını yeni portal prefab'ı ile göster

                    var gatePrefab = Resources.Load<GameObject>("Prefabs/Meshy_AI_Portal_of_the_Enchant_0627204643_texture");

                    if (gatePrefab != null)

                    {

                        var portalInst = Instantiate(gatePrefab, shapeObj.transform);

                        portalInst.transform.localPosition = Vector3.zero;

                        

                        // Rotasyonu ve ölçeği (%20 artış) runtime ile aynı şekilde ayarla

                        portalInst.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * gatePrefab.transform.localRotation;

                        portalInst.transform.localScale = gatePrefab.transform.localScale * 1.20f;

                    }

                }

                else if (shape.Parts != null && shape.Parts.Count > 0)

                {

                    foreach (var part in shape.Parts)

                    {

                        CreateEditorPartObject(shapeObj.transform, part, placed);

                    }

                }

                else

                {

                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

                    cube.name = "NoPartPlaceholder";

                    cube.transform.SetParent(shapeObj.transform);

                    cube.transform.localPosition = Vector3.zero;

                    cube.transform.localScale = Vector3.one * 2f;

                    cube.GetComponent<Renderer>().sharedMaterial = GetOrCreateFallbackMaterial(Color.gray);

                }



                placed++;

            }



            Debug.Log($"[EXPORTER] Successfully placed {placed} objects in scene under {parentGo.name}!");

            

            /*

            // Configure scene lighting and reflections in Editor to prevent specular reflection on terrain/objects

            // Skip changing the directional light and ambient overrides if DemoLighting exists in the scene

            bool hasDemoLighting = GameObject.Find("DemoLighting") != null;

            if (!hasDemoLighting)

            {

                var editorDirLight = GameObject.Find("Directional Light");

                if (editorDirLight != null && (editorDirLight.transform.parent == null || editorDirLight.transform.parent.name != "DemoLighting"))

                {

                    var lightComp = editorDirLight.GetComponent<Light>();

                    if (lightComp != null)

                    {

                        lightComp.intensity = 0f; // Disable the intensity to avoid specular highlights

                        lightComp.shadows = LightShadows.None;

                    }

                }

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

                RenderSettings.ambientLight = Color.white;

                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;

                RenderSettings.customReflectionTexture = null;

                RenderSettings.reflectionIntensity = 0f;

            }

            else

            {

                Debug.Log("[EXPORTER] 'DemoLighting' detected in scene. Skipping lighting and ambient override.");

            }

            */

            // Mark scene dirty so it can be saved

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);

        }



        private void CreateEditorPartObject(Transform parent, N3ShapeParser.N3PartData part, int shapeIdx)

        {

            string partName = string.IsNullOrEmpty(part.MeshFileName)

                ? $"Part_{shapeIdx}" : Path.GetFileName(part.MeshFileName);



            Color diffuse = part.Material != null ? part.Material.Diffuse : Color.gray;

            if (diffuse.r < 0.01f && diffuse.g < 0.01f && diffuse.b < 0.01f)

                diffuse = new Color(0.5f, 0.48f, 0.45f);



            Mesh unityMesh = null;

            if (!string.IsNullOrEmpty(part.MeshFileName))

            {

                string meshBaseName = Path.GetFileNameWithoutExtension(part.MeshFileName);

                unityMesh = Resources.Load<Mesh>($"KOModels/Object/Meshes/{meshBaseName}");



                if (unityMesh == null)

                {

                    string meshRelPath = part.MeshFileName.Replace('\\', '/');

                    string meshFullPath = Path.Combine("", meshRelPath);



                    if (!KOBinaryProvider.Exists(meshFullPath))

                    {

                        string meshName = Path.GetFileName(meshRelPath);

                        string altPath = Path.Combine("", "Object", meshName);

                        if (KOBinaryProvider.Exists(altPath))

                            meshFullPath = altPath;

                    }



                    if (KOBinaryProvider.Exists(meshFullPath))

                    {

                        var lodMeshData = N3PMeshImporter.Load(meshFullPath);

                        if (lodMeshData != null)

                        {

                            unityMesh = N3PMeshImporter.CreateUnityMesh(lodMeshData);

                        }

                    }

                }

            }



            GameObject partObj;

            if (unityMesh != null)

            {

                partObj = new GameObject(partName);

                var mf = partObj.AddComponent<MeshFilter>();

                mf.sharedMesh = unityMesh;

                var mr = partObj.AddComponent<MeshRenderer>();

                mr.sharedMaterial = CreateEditorMeshMaterial(part, diffuse);



                // Editörde seçilip silinebilmesi için MeshCollider ekle

                var mc = partObj.AddComponent<MeshCollider>();

                mc.sharedMesh = unityMesh;

            }

            else

            {

                partObj = GameObject.CreatePrimitive(PrimitiveType.Cube);

                partObj.name = partName;

                partObj.transform.localScale = Vector3.one * 2f;

                partObj.GetComponent<Renderer>().sharedMaterial = GetOrCreateFallbackMaterial(diffuse);

            }



            partObj.transform.SetParent(parent, false);

            partObj.transform.localPosition = part.Pivot;

            partObj.transform.localRotation = Quaternion.identity;

            partObj.transform.localScale = Vector3.one;

        }



        private Material CreateEditorMeshMaterial(N3ShapeParser.N3PartData part, Color diffuse)

        {

            Texture2D tex = null;

            if (part.TextureFileNames != null && part.TextureFileNames.Count > 0)

            {

                string texRelPath = part.TextureFileNames[0].Replace('\\', '/');

                if (!string.IsNullOrEmpty(texRelPath))

                {

                    tex = KOTextureProvider.Load(texRelPath, flipY: false);

                }

            }



            if (tex != null)

            {

                var shader = Shader.Find("Universal Render Pipeline/Lit");

                if (shader == null) shader = Shader.Find("Standard");

                var mat = new Material(shader);

                if (mat.HasProperty("_BaseMap"))

                    mat.SetTexture("_BaseMap", tex);

                else if (mat.HasProperty("_Base_Map"))

                    mat.SetTexture("_Base_Map", tex);

                else

                    mat.mainTexture = tex;



                if (mat.HasProperty("_BaseColor"))

                    mat.SetColor("_BaseColor", Color.white);

                else

                    mat.color = Color.white;



                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);

                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);

                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);



                // C++ __Material render flags (My_3DStruct.h:78-89)

                uint renderFlags = part.Material != null ? part.Material.RenderFlags : 0;



                bool hasAlphaBlend = (renderFlags & 0x1) != 0;

                bool hasDiffuseAlpha = (renderFlags & 0x80) != 0;

                bool noZWrite = (renderFlags & 0x100) != 0;

                bool noZBuffer = (renderFlags & 0x400) != 0;



                uint srcBlend = part.Material != null ? part.Material.SrcBlend : 0;

                uint destBlend = part.Material != null ? part.Material.DestBlend : 0;



                bool isAdditiveBlend = hasAlphaBlend && (

                    (srcBlend == 2 && destBlend == 2) ||   // ONE + ONE

                    (srcBlend == 5 && destBlend == 2) ||   // SRCALPHA + ONE

                    (srcBlend == 2 && destBlend == 6));     // ONE + INVSRCALPHA



                if (isAdditiveBlend)

                {

                    mat.SetFloat("_Surface", 1); // Transparent

                    mat.SetOverrideTag("RenderType", "Transparent");

                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);

                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);

                    mat.SetFloat("_ZWrite", 0);

                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                    mat.DisableKeyword("_ALPHATEST_ON");

                    if (mat.HasProperty("_AlphaClip"))

                        mat.SetFloat("_AlphaClip", 0);

                    if (mat.HasProperty("_Cull"))

                        mat.SetFloat("_Cull", 0); // Cull Off

                }

                else if (hasAlphaBlend || hasDiffuseAlpha)

                {

                    mat.SetFloat("_Surface", 1);

                    mat.SetOverrideTag("RenderType", "Transparent");

                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

                    mat.SetFloat("_ZWrite", 0);

                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                    if (mat.HasProperty("_Cull"))

                        mat.SetFloat("_Cull", 0); // Cull Off

                }

                else

                {

                    bool texHasAlpha = HasTransparentPixels(tex);

                    if (texHasAlpha && mat.HasProperty("_AlphaClip"))

                    {

                        mat.SetFloat("_AlphaClip", 1);

                        mat.SetFloat("_Cutoff", 0.53f);

                        mat.SetOverrideTag("RenderType", "TransparentCutout");

                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

                        mat.EnableKeyword("_ALPHATEST_ON");

                    }

                }



                if (noZWrite || noZBuffer)

                {

                    mat.SetFloat("_ZWrite", 0);

                }



                bool needsDoubleSided = (renderFlags & 0x1) != 0 || (renderFlags & 0x8) != 0;

                if (needsDoubleSided)

                {

                    if (mat.HasProperty("_Cull"))

                        mat.SetFloat("_Cull", 0); // Cull Off

                }



                if ((renderFlags & 0x4) != 0)

                {

                    if (mat.HasProperty("_Cull"))

                        mat.SetFloat("_Cull", 0); // Cull Off

                }



                if ((renderFlags & 0x10) != 0 && tex != null)

                {

                    tex.filterMode = FilterMode.Point;

                }



                if ((renderFlags & 0x40) != 0)

                {

                    var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");

                    if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");

                    if (unlitShader != null)

                    {

                        var unlitMat = new Material(unlitShader);

                        if (unlitMat.HasProperty("_BaseMap"))

                            unlitMat.SetTexture("_BaseMap", tex);

                        else if (unlitMat.HasProperty("_Base_Map"))

                            unlitMat.SetTexture("_Base_Map", tex);

                        else

                            unlitMat.mainTexture = tex;

                        if (unlitMat.HasProperty("_BaseColor"))

                            unlitMat.SetColor("_BaseColor", Color.white);



                        if (isAdditiveBlend)

                        {

                            unlitMat.SetFloat("_Surface", 1);

                            unlitMat.SetOverrideTag("RenderType", "Transparent");

                            unlitMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                            unlitMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);

                            unlitMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);

                            unlitMat.SetFloat("_ZWrite", 0);

                            unlitMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                            if (unlitMat.HasProperty("_Cull"))

                                unlitMat.SetFloat("_Cull", 0);

                        }

                        else if (hasAlphaBlend || hasDiffuseAlpha)

                        {

                            unlitMat.SetFloat("_Surface", 1);

                            unlitMat.SetOverrideTag("RenderType", "Transparent");

                            unlitMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                            unlitMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

                            unlitMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

                            unlitMat.SetFloat("_ZWrite", 0);

                            unlitMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                            if (unlitMat.HasProperty("_Cull"))

                                unlitMat.SetFloat("_Cull", 0);

                        }

                        else if (unlitMat.HasProperty("_AlphaClip"))

                        {

                            unlitMat.SetFloat("_AlphaClip", 1);

                            unlitMat.SetFloat("_Cutoff", 0.5f);

                            unlitMat.SetOverrideTag("RenderType", "TransparentCutout");

                            unlitMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

                            unlitMat.EnableKeyword("_ALPHATEST_ON");

                        }



                        if ((renderFlags & 0x4) != 0 || hasAlphaBlend)

                        {

                            if (unlitMat.HasProperty("_Cull"))

                                unlitMat.SetFloat("_Cull", 0);

                        }



                        return unlitMat;

                    }

                }



                if ((renderFlags & 0x80) != 0)

                {

                    if (mat.HasProperty("_Surface"))

                        mat.SetFloat("_Surface", 1);

                    mat.SetOverrideTag("RenderType", "Transparent");

                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                    if (mat.HasProperty("_Blend"))

                        mat.SetFloat("_Blend", 1);

                }



                if ((renderFlags & 0x100) != 0)

                {

                    if (mat.HasProperty("_ZWrite"))

                        mat.SetFloat("_ZWrite", 0);

                }



                if ((renderFlags & 0x200) != 0 && tex != null)

                {

                    tex.wrapMode = TextureWrapMode.Clamp;

                }



                if ((renderFlags & 0x400) != 0)

                {

                    if (mat.HasProperty("_ZTest"))

                        mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);

                }



                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);

                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);

                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);



                return mat;

            }

            else

            {

                return CreateMaterial(diffuse);

            }

        }



        private static bool HasTransparentPixels(Texture2D tex)

        {

            if (tex == null) return false;



            string path = AssetDatabase.GetAssetPath(tex);

            if (!string.IsNullOrEmpty(path))

            {

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null)

                {

                    return importer.DoesSourceTextureHaveAlpha();

                }

            }

            

            try

            {

                var pixels = tex.GetPixels32();

                int step = Mathf.Max(1, pixels.Length / 256);

                for (int i = 0; i < pixels.Length; i += step)

                {

                    if (pixels[i].a < 250)

                        return true;

                }

                return false;

            }

            catch

            {

                string name = tex.name.ToLowerInvariant();

                if (name.Contains("leaf") || name.Contains("tree") || name.Contains("bush") || 

                    name.Contains("grass") || name.Contains("flower") || name.Contains("plant") || 

                    name.Contains("branch") || name.Contains("bark") || name.Contains("wood") || 

                    name.Contains("ivy"))

                {

                    return true;

                }

                

                var fmt = tex.format;

                if (fmt == TextureFormat.DXT5 || 

                    fmt == TextureFormat.RGBA32 || fmt == TextureFormat.ARGB32)

                {

                    return true;

                }

                return false;

            }

        }



        public static Material CreateMaterial(Color color)

        {

            var shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null) shader = Shader.Find("Standard");

            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);

            if (mat.HasProperty("_BaseColor"))

                mat.SetColor("_BaseColor", color);

            else

                mat.color = color;



            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);

            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);



            return mat;

        }



        private Material GetOrCreateFallbackMaterial(Color color)

        {

            return CreateMaterial(color);

        }



        private Texture2D LoadReplacementTile(string gttRel, int tileIdx, Texture2D originalTex)

        {

            if (originalTex == null) return null;



            string fileName = Path.GetFileNameWithoutExtension(gttRel).ToLowerInvariant();

            

            string specificPath = $"TerrainReplacements/{fileName}_{tileIdx}";

            Texture2D replacement = Resources.Load<Texture2D>(specificPath);



            if (replacement == null)

            {

                string archivePath = $"TerrainReplacements/{fileName}";

                replacement = Resources.Load<Texture2D>(archivePath);

            }



            if (replacement != null)

            {

                try

                {

                    string assetPath = AssetDatabase.GetAssetPath(replacement);

                    if (!string.IsNullOrEmpty(assetPath))

                    {

                        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                        if (importer != null && !importer.isReadable)

                        {

                            importer.isReadable = true;

                            importer.SaveAndReimport();

                            replacement = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

                        }

                    }



                    int w = originalTex.width;

                    int h = originalTex.height;

                    Texture2D copy = new Texture2D(w, h, TextureFormat.RGBA32, false);

                    Color32[] origPixels = originalTex.GetPixels32();

                    Color32[] replPixels = replacement.GetPixels32();

                    Color32[] finalPixels = new Color32[w * h];



                    int repW = replacement.width;

                    int repH = replacement.height;



                    for (int i = 0; i < w * h; i++)

                    {

                        int x = i % w;

                        int y = i / w;



                        float u = (float)x / (w - 1);

                        float v = (float)y / (h - 1);



                        int rx = Mathf.Clamp((int)(u * (repW - 1)), 0, repW - 1);

                        int ry = Mathf.Clamp((int)(v * (repH - 1)), 0, repH - 1);



                        Color32 cRepl = replPixels[ry * repW + rx];

                        Color32 cOrig = origPixels[i];



                        finalPixels[i] = new Color32(cRepl.r, cRepl.g, cRepl.b, cOrig.a);

                    }



                    copy.SetPixels32(finalPixels);

                    copy.Apply();

                    

                    DestroyImmediate(originalTex);

                    return copy;

                }

                catch (Exception ex)

                {

                    Debug.LogWarning($"[EXPORTER] Failed to load replacement tile for {fileName}_{tileIdx}: {ex.Message}");

                }

            }



            return originalTex;

        }



        private void DebugGtdTiles(short zoneId)

        {

            var zoneInfo = KOZoneMapper.GetZoneInfo(zoneId);

            if (zoneInfo == null) return;

            string gtdPath = KOZoneMapper.GetGtdPath(zoneId);

            var gtdData = GtdTerrainImporter.Parse(gtdPath);

            if (gtdData == null) return;

            

            int cellCount = gtdData.MapSize - 1;

            int maxTileIdx = gtdData.TileTextures.Count;

            

            var tileUsage = new Dictionary<int, int>();

            var fullCount = new Dictionary<int, int>();

            var nonFullCount = new Dictionary<int, int>();

            

            for (int x = 0; x < cellCount; x++)

            {

                for (int z = 0; z < cellCount; z++)

                {

                    var cell = gtdData.CellData[x, z];

                    int idx = cell.Tex1Idx;

                    if (!tileUsage.ContainsKey(idx))

                    {

                        tileUsage[idx] = 0;

                        fullCount[idx] = 0;

                        nonFullCount[idx] = 0;

                    }

                    tileUsage[idx]++;

                    if (cell.IsTileFull) fullCount[idx]++;

                    else nonFullCount[idx]++;

                }

            }

            

            var sortedTiles = new List<int>(tileUsage.Keys);

            sortedTiles.Sort((a, b) => tileUsage[b].CompareTo(tileUsage[a]));

            

            Debug.Log($"=== GTD TILES FOR ZONE {zoneId} ===");

            Debug.Log($"Total TileTex in GTD: {gtdData.TileTextures.Count}");

            Debug.Log($"Total TexSrc in GTD: {gtdData.TileTexSources.Count}");

            

            for (int i = 0; i < Mathf.Min(sortedTiles.Count, 30); i++)

            {

                int idx = sortedTiles[i];

                string mapping = "No Mapping";

                if (idx >= 0 && idx < gtdData.TileTextures.Count)

                {

                    var info = gtdData.TileTextures[idx];

                    string src = (info.SrcIdx >= 0 && info.SrcIdx < gtdData.TileTexSources.Count) 

                        ? gtdData.TileTexSources[info.SrcIdx] : "Unknown";

                    mapping = $"Src={info.SrcIdx} ({src}) TileIdx={info.TileIdx}";

                }

                Debug.Log($"Rank {i+1}: TileIdx {idx} | Total={tileUsage[idx]} (Full={fullCount[idx]}, NonFull={nonFullCount[idx]}) | {mapping}");

            }

        }



        /// <summary>

        /// Multi-submesh mesh'lerden (ağaçlar gibi) sadece gövde submesh'lerini çıkarır.

        /// Yaprak/dal material'lerini (alpha/transparent) hariç tutar.

        /// Editörde mesh readable olduğu için burada çalışır.

        /// </summary>

        private Mesh ExtractTrunkColliderMesh(Mesh originalMesh, Material[] materials)

        {

            if (originalMesh == null || !originalMesh.isReadable)

            {

                Debug.Log($"[COLLIDER] {originalMesh?.name ?? "null"} → isReadable={originalMesh?.isReadable}, ATLA");

                return null;

            }

            if (originalMesh.subMeshCount <= 1) return null;

            if (materials == null || materials.Length <= 1) return null;



            Debug.Log($"[COLLIDER] {originalMesh.name}: {originalMesh.subMeshCount} submesh, {materials.Length} material");



            var trunkTriangles = new List<int>();

            for (int si = 0; si < originalMesh.subMeshCount; si++)

            {

                bool isLeafOrBranch = false;

                if (si < materials.Length && materials[si] != null)

                {

                    var m = materials[si];

                    // Alpha cutout veya transparent = yaprak/dal

                    bool hasAlphaClip = m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0;

                    bool isTransparent = m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0;

                    string renderType = m.GetTag("RenderType", false);

                    string matName = m.name.ToLowerInvariant();

                    isLeafOrBranch = hasAlphaClip || isTransparent

                        || renderType == "TransparentCutout" || renderType == "Transparent"

                        || matName.Contains("leaf") || matName.Contains("branch")

                        || matName.Contains("leaves");



                    Debug.Log($"[COLLIDER]   submesh[{si}] mat='{m.name}' → isLeaf={isLeafOrBranch} (alphaClip={hasAlphaClip}, transparent={isTransparent}, renderType='{renderType}')");

                }

                if (!isLeafOrBranch)

                {

                    trunkTriangles.AddRange(originalMesh.GetTriangles(si));

                }

            }



            if (trunkTriangles.Count == 0 || trunkTriangles.Count == originalMesh.triangles.Length)

            {

                Debug.Log($"[COLLIDER] {originalMesh.name} → ayrıştırma gereksiz (trunk={trunkTriangles.Count}, total={originalMesh.triangles.Length})");

                return null;

            }



            var trunkMesh = new Mesh();

            trunkMesh.name = originalMesh.name + "_TrunkCollider";

            trunkMesh.vertices = originalMesh.vertices;

            trunkMesh.normals = originalMesh.normals;

            trunkMesh.triangles = trunkTriangles.ToArray();

            trunkMesh.RecalculateBounds();

            Debug.Log($"[COLLIDER] ✅ {originalMesh.name} → TrunkCollider oluşturuldu ({trunkMesh.triangles.Length/3} tri, orijinal={originalMesh.triangles.Length/3})");

            return trunkMesh;

        }



        private string GetMaterialTextureName(Material mat)

        {

            if (mat == null) return "";

            if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null)

                return mat.GetTexture("_BaseMap").name;

            if (mat.HasProperty("_Base_Map") && mat.GetTexture("_Base_Map") != null)

                return mat.GetTexture("_Base_Map").name;

            if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null)

                return mat.GetTexture("_MainTex").name;

            return "";

        }



        private void SaveSelectedObjectsToZoneAsset(short zoneId, bool append)

        {

            GameObject[] selectedObjects = Selection.gameObjects;

            if (selectedObjects == null || selectedObjects.Length == 0)

            {

                EditorUtility.DisplayDialog("Error", "Please select at least one GameObject in the Hierarchy to export.", "OK");

                return;

            }



            string assetPath = $"Assets/Resources/KOZones/zone_{zoneId}.asset";

            KOZoneAsset zoneAsset = AssetDatabase.LoadAssetAtPath<KOZoneAsset>(assetPath);

            if (zoneAsset == null)

            {

                EditorUtility.DisplayDialog("Error", $"Zone asset not found at: {assetPath}\nPlease export the terrain first to create the asset.", "OK");

                return;

            }



            // Orijinal dosyayı korumak için ilk işlemden önce yedek oluştur

            string backupPath = $"Assets/Resources/KOZones/zone_{zoneId}_backup.asset";

            if (AssetDatabase.LoadAssetAtPath<KOZoneAsset>(backupPath) == null)

            {

                if (AssetDatabase.CopyAsset(assetPath, backupPath))

                {

                    Debug.Log($"[EXPORTER] Orijinal yedek oluşturuldu: {backupPath}");

                }

            }



            var renderers = new List<Renderer>();

            foreach (var rootGo in selectedObjects)

            {

                foreach (var r in rootGo.GetComponentsInChildren<Renderer>(true))

                {

                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

                    if (ShouldSkipLOD(r)) continue;

                    renderers.Add(r);

                }

            }



            if (renderers.Count == 0)

            {

                EditorUtility.DisplayDialog("Error", "No MeshRenderers or SkinnedMeshRenderers found in the selected GameObjects or their children.", "OK");

                return;

            }



            var newShapes = new List<KOShapeEntry>();

            int shapeCount = 0;

            var processedEventParents = new HashSet<GameObject>();

            foreach (var renderer in renderers)

            {

                Mesh sharedMesh = null;

                if (renderer is MeshRenderer mr)

                {

                    var filter = mr.GetComponent<MeshFilter>();

                    if (filter != null) sharedMesh = filter.sharedMesh;

                }

                else if (renderer is SkinnedMeshRenderer smr)

                {

                    sharedMesh = smr.sharedMesh;

                }

                if (sharedMesh == null) continue;



                var go = renderer.gameObject;

                

                // Find parentGo by walking up until hitting KOObjects or root

                var parentGo = go;

                var curr = go.transform;

                while (curr.parent != null)

                {

                    string pName = curr.parent.name;

                    if (pName == $"KOObjects_{zoneId}" || pName == "KOObjects" || 

                        pName == "WaterSystem" || pName == "Foliage" || pName == "Grass")

                    {

                        break;

                    }

                    parentGo = curr.parent.gameObject;

                    curr = curr.parent;

                }



                var we = parentGo.GetComponentInChildren<KOWorldEvent>(true);

                if (we != null)

                {

                    if (processedEventParents.Contains(parentGo)) continue;

                    processedEventParents.Add(parentGo);

                }



                var shape = new KOShapeEntry();

                shape.name = (we != null) ? parentGo.name : go.name;

                shape.position = (we != null) ? parentGo.transform.position : go.transform.position;

                shape.rotation = (we != null) ? parentGo.transform.rotation : go.transform.rotation;

                shape.scale = (we != null) ? parentGo.transform.lossyScale : go.transform.lossyScale;

                

                if (we != null)

                {

                    shape.eventID = we.EventID;

                    shape.eventType = we.EventType;

                    shape.npcID = we.NPC_ID;

                }

                else

                {

                    shape.eventID = 0;

                    shape.eventType = 0;

                    shape.npcID = 0;

                }

                shape.shapeType = 0;

                shape.npcStatus = 0;

                shape.belong = 0;

                shape.isCustom = true;



                var part = new KOPartEntry();

                part.mesh = sharedMesh;

                part.material = renderer.sharedMaterial;

                part.materials = renderer.sharedMaterials;



                string newTextureName = GetMaterialTextureName(renderer.sharedMaterial);

                if (!string.IsNullOrEmpty(newTextureName))

                {

                    part.textureName = newTextureName;

                }

                if (renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)

                {

                    part.textureNames = new string[renderer.sharedMaterials.Length];

                    for (int m = 0; m < renderer.sharedMaterials.Length; m++)

                    {

                        string newName = GetMaterialTextureName(renderer.sharedMaterials[m]);

                        if (!string.IsNullOrEmpty(newName))

                        {

                            part.textureNames[m] = newName;

                        }

                    }

                }



                // Multi-submesh mesh'ler için gövde-only collider mesh çıkar

                part.colliderMesh = ExtractTrunkColliderMesh(sharedMesh, renderer.sharedMaterials);



                part.pivot = Vector3.zero;

                part.texFPS = 0f;

                part.animTextures = Array.Empty<Texture2D>();

                

                uint renderFlags = 0;

                if (renderer.sharedMaterial != null)

                {

                    var mat = renderer.sharedMaterial;

                    bool isTransparent = false;

                    if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") == 1f)

                    {

                        isTransparent = true;

                    }

                    else if (mat.shader != null && mat.shader.name.ToLowerInvariant().Contains("transparent"))

                    {

                        isTransparent = true;

                    }

                    else if (mat.renderQueue >= 3000)

                    {

                        isTransparent = true;

                    }



                    if (isTransparent)

                    {

                        renderFlags |= 0x1;

                    }

                }

                part.renderFlags = renderFlags;

                part.srcBlend = 0;

                part.destBlend = 0;



                shape.parts = new KOPartEntry[] { part };

                newShapes.Add(shape);

                shapeCount++;

            }



            if (newShapes.Count == 0)

            {

                EditorUtility.DisplayDialog("Error", "No valid meshes could be serialized from selection.", "OK");

                return;

            }



            Undo.RecordObject(zoneAsset, "Update Zone Shapes");

            if (append)

            {

                var existingShapes = new List<KOShapeEntry>(zoneAsset.shapes ?? Array.Empty<KOShapeEntry>());

                existingShapes.AddRange(newShapes);

                zoneAsset.shapes = existingShapes.ToArray();

            }

            else

            {

                zoneAsset.shapes = newShapes.ToArray();

            }



            EditorUtility.SetDirty(zoneAsset);



            // colliderMesh ve combined mesh'leri ScriptableObject'e sub-asset olarak kaydet

            string zoneAssetPath = AssetDatabase.GetAssetPath(zoneAsset);

            foreach (var shape in newShapes)

            {

                if (shape.parts == null) continue;

                foreach (var part in shape.parts)

                {

                    if (part.mesh != null)

                    {

                        string meshPath = AssetDatabase.GetAssetPath(part.mesh);

                        if (string.IsNullOrEmpty(meshPath)) // In-memory/combined mesh

                        {

                            Mesh clonedMesh = Instantiate(part.mesh);

                            clonedMesh.name = $"GenMesh_{shape.name.Replace("(Clone)", "").Trim()}_{part.mesh.name}";

                            

                            // Remove existing sub-asset with same name

                            var existing = AssetDatabase.LoadAllAssetsAtPath(zoneAssetPath);

                            foreach (var sub in existing)

                            {

                                if (sub is Mesh m && m.name == clonedMesh.name)

                                {

                                    AssetDatabase.RemoveObjectFromAsset(sub);

                                }

                            }

                            AssetDatabase.AddObjectToAsset(clonedMesh, zoneAsset);

                            part.mesh = clonedMesh; // Assign persistent sub-asset reference

                        }

                    }



                    if (part.colliderMesh != null)

                    {

                        // Aynı isimde eski sub-asset varsa kaldır

                        var existing = AssetDatabase.LoadAllAssetsAtPath(zoneAssetPath);

                        foreach (var sub in existing)

                        {

                            if (sub is Mesh m && m.name == part.colliderMesh.name)

                            {

                                AssetDatabase.RemoveObjectFromAsset(sub);

                            }

                        }

                        AssetDatabase.AddObjectToAsset(part.colliderMesh, zoneAsset);

                    }

                }

            }



            SyncTerrainMaterialLayers(zoneId, zoneAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Successfully serialized {shapeCount} objects to {assetPath}!\n(Mode: {(append ? "Append" : "Overwrite")})", "OK");

        }



        private void CleanupEmptyParentObjects(short zoneId)

        {

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



            if (koObjectsGo == null) return;



            var childrenToDelete = new List<GameObject>();

            var childTransforms = koObjectsGo.GetComponentsInChildren<Transform>(true);

            foreach (var t in childTransforms)

            {

                if (t != koObjectsGo.transform && t.parent == koObjectsGo.transform)

                {

                    // Sadece altındaki çocuk sayısı (childCount) sıfır olan, yani gerçekten bomboş parent'ları siliyoruz.

                    // Birleştirilmiş (combined) veya deaktif orijinal objeleri korumak için çocukları olan parent'lara dokunmuyoruz.

                    if (t.childCount == 0)

                    {

                        childrenToDelete.Add(t.gameObject);

                    }

                }

            }



            if (childrenToDelete.Count > 0)

            {

                Debug.Log($"[EXPORTER] Cleaning up {childrenToDelete.Count} empty parent GameObjects under {koObjectsGo.name}.");

                foreach (var go in childrenToDelete)

                {

                    Undo.DestroyObjectImmediate(go);

                }

                

                // Mark scene dirty

                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);

            }

        }



        private void AutoDetectAndAppendNewObjects(short zoneId)

        {

            // Sahnede içi boş kalmış parent nesneleri temizle

            CleanupEmptyParentObjects(zoneId);



            var zoneInfo = KOZoneMapper.GetZoneInfo(zoneId);

            string zoneName = zoneInfo != null ? zoneInfo.ZoneName : $"Zone {zoneId}";



            string assetPath = $"Assets/Resources/KOZones/zone_{zoneId}.asset";

            KOZoneAsset zoneAsset = AssetDatabase.LoadAssetAtPath<KOZoneAsset>(assetPath);

            if (zoneAsset == null)

            {

                EditorUtility.DisplayDialog("Error", $"Zone asset not found at: {assetPath}\nPlease export the terrain first to create the asset.", "OK");

                return;

            }



            string backupPath = $"Assets/Resources/KOZones/zone_{zoneId}_backup.asset";

            KOZoneAsset compareAsset = AssetDatabase.LoadAssetAtPath<KOZoneAsset>(backupPath);

            if (compareAsset == null)

            {

                if (AssetDatabase.CopyAsset(assetPath, backupPath))

                {

                    Debug.Log($"[EXPORTER] Original backup created successfully at: {backupPath}");

                    compareAsset = AssetDatabase.LoadAssetAtPath<KOZoneAsset>(backupPath);

                }

            }



            if (compareAsset == null)

            {

                compareAsset = zoneAsset;

            }



            // Sahnede silinen veya deaktif edilen orijinal objeleri tespit et (inaktif olanlar dahil ara)

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



            var sceneChildren = new List<Transform>();

            bool checkDeletions = true; // Always check deletions based on scene objects

            

            // Find all renderers in the scene (including disabled ones) to check if shapes exist

            var allSceneRenderers = GameObject.FindObjectsByType<Renderer>(FindObjectsInactive.Include);

            foreach (var r in allSceneRenderers)

            {

                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

                if (r.GetComponent<Terrain>() != null) continue;

                if (r.GetComponent<BillboardY>() != null) continue; // Skip billboards

                if (ShouldSkipLOD(r)) continue;

                sceneChildren.Add(r.transform);

            }

            Debug.Log($"[EXPORTER] Sahnede toplam {sceneChildren.Count} renderable nesne bulundu (deletions check için kullanılacak).");



            // Build spatial grid for scene children to avoid nested loops (O(N^2))

            var sceneGrid = new Dictionary<Vector2Int, List<Transform>>();

            foreach (var t in sceneChildren)

            {

                Vector2Int cell = new Vector2Int(Mathf.FloorToInt(t.position.x / 2.0f), Mathf.FloorToInt(t.position.z / 2.0f));

                if (!sceneGrid.TryGetValue(cell, out var list))

                {

                    list = new List<Transform>();

                    sceneGrid[cell] = list;

                }

                list.Add(t);

            }



            var originalKeys = new HashSet<string>();

            var finalShapes = new List<KOShapeEntry>();

            int deletedCount = 0;

            var deletedPositions = new List<UnityEngine.Vector3>();



            if (compareAsset.shapes != null)

            {

                foreach (var shape in compareAsset.shapes)

                {

                    // Skip existing LODs in the backup asset to clean them up!

                    string sName = (shape.name ?? "").ToLowerInvariant();

                    string mName = (shape.parts != null && shape.parts.Length > 0 && shape.parts[0].mesh != null) ? shape.parts[0].mesh.name.ToLowerInvariant() : "";

                    if (sName.Contains("lod1") || sName.Contains("lod2") || sName.Contains("lod3") || sName.Contains("lod4") || sName.Contains("lod5") ||

                        mName.Contains("lod1") || mName.Contains("lod2") || mName.Contains("lod3") || mName.Contains("lod4") || mName.Contains("lod5"))

                    {

                        deletedCount++;

                        continue;

                    }



                    Transform matchingSceneGo = null;

                    if (checkDeletions)

                    {

                        bool isDeleted = true;

                        float minDistance = 0.2f;



                        // Query 9 cells around shape position

                        Vector2Int shapeCell = new Vector2Int(Mathf.FloorToInt(shape.position.x / 2.0f), Mathf.FloorToInt(shape.position.z / 2.0f));

                        bool foundMatch = false;



                        for (int dx = -1; dx <= 1 && !foundMatch; dx++)

                        {

                            for (int dz = -1; dz <= 1 && !foundMatch; dz++)

                            {

                                Vector2Int neighborCell = new Vector2Int(shapeCell.x + dx, shapeCell.y + dz);

                                if (sceneGrid.TryGetValue(neighborCell, out var cellTransforms))

                                {

                                    foreach (var t in cellTransforms)

                                    {

                                        float dist = Vector3.Distance(shape.position, t.position);

                                        Transform checkTarget = t;

                                        

                                        // Eğer child/part ise parent pozisyonunu kontrol et

                                        if (dist >= minDistance && t.parent != null && t.parent != koObjectsGo?.transform)

                                        {

                                            float pDist = Vector3.Distance(shape.position, t.parent.position);

                                            if (pDist < minDistance)

                                            {

                                                dist = pDist;

                                                checkTarget = t.parent;

                                            }

                                        }



                                        if (dist < minDistance)

                                        {

                                            // Sadece en az bir aktif MeshRenderer'ı kalan nesneleri "var" kabul et.

                                            var childRenderers = checkTarget.GetComponentsInChildren<MeshRenderer>(true);

                                            bool hasActiveRenderer = false;

                                            if (childRenderers.Length == 0)

                                            {

                                                var r = checkTarget.GetComponent<MeshRenderer>();

                                                if (r != null && r.gameObject.activeInHierarchy && r.enabled)

                                                {

                                                    hasActiveRenderer = true;

                                                }

                                            }

                                            else

                                            {

                                                foreach (var r in childRenderers)

                                                {

                                                    if (r.gameObject.activeInHierarchy && r.enabled)

                                                    {

                                                        hasActiveRenderer = true;

                                                        break;

                                                    }

                                                }

                                            }



                                            if (hasActiveRenderer)

                                            {

                                                isDeleted = false;

                                                matchingSceneGo = checkTarget;

                                                foundMatch = true;

                                                break;

                                            }

                                        }

                                    }

                                }

                            }

                        }



                        if (isDeleted)

                        {

                            deletedCount++;

                            deletedPositions.Add(shape.position);

                            continue; // Tamamen silinmiş obje

                        }

                    }



                    if (matchingSceneGo != null)

                    {

                        // Sahnede düzenlenmiş (kaplaması değişmiş, taşınmış vb.) halini güncelle

                        shape.position = matchingSceneGo.position;

                        shape.rotation = matchingSceneGo.rotation;

                        shape.scale = matchingSceneGo.lossyScale;



                        var we = matchingSceneGo.GetComponentInChildren<KOWorldEvent>(true);

                        if (we != null)

                        {

                            shape.eventID = we.EventID;

                            shape.eventType = we.EventType;

                            shape.npcID = we.NPC_ID;

                        }

                        else

                        {

                            shape.eventID = 0;

                            shape.eventType = 0;

                            shape.npcID = 0;

                        }



                        var rawRenderers = matchingSceneGo.GetComponentsInChildren<Renderer>(true);

                        var filteredRenderers = new List<Renderer>();

                        foreach (var r in rawRenderers)

                        {

                            if (r is MeshRenderer || r is SkinnedMeshRenderer)

                            {

                                filteredRenderers.Add(r);

                            }

                        }

                        var renderers = filteredRenderers.ToArray();

                        if (renderers.Length > 0)

                        {

                            var updatedParts = new List<KOPartEntry>();

                            for (int ri = 0; ri < renderers.Length; ri++)

                            {

                                var renderer = renderers[ri];

                                Mesh sharedMesh = null;

                                if (renderer is MeshRenderer mr)

                                {

                                    var filter = mr.GetComponent<MeshFilter>();

                                    if (filter != null) sharedMesh = filter.sharedMesh;

                                }

                                else if (renderer is SkinnedMeshRenderer smr)

                                {

                                    sharedMesh = smr.sharedMesh;

                                }

                                if (sharedMesh == null) continue;



                                KOPartEntry partEntry = null;

                                string oldTextureName = "";

                                string[] oldTextureNames = null;



                                if (shape.parts != null && ri < shape.parts.Length)

                                {

                                    partEntry = shape.parts[ri];

                                    oldTextureName = partEntry.textureName;

                                    oldTextureNames = partEntry.textureNames;

                                    partEntry.pivot = matchingSceneGo.InverseTransformPoint(renderer.transform.position); // Update pivot to match editor position relative to root

                                }

                                else

                                {

                                    partEntry = new KOPartEntry();

                                    partEntry.pivot = matchingSceneGo.InverseTransformPoint(renderer.transform.position);

                                    partEntry.texFPS = 0f;

                                    partEntry.animTextures = Array.Empty<Texture2D>();

                                }



                                partEntry.mesh = sharedMesh;

                                partEntry.material = renderer.sharedMaterial;

                                partEntry.materials = renderer.sharedMaterials;



                                string newTextureName = GetMaterialTextureName(renderer.sharedMaterial);

                                if (!string.IsNullOrEmpty(newTextureName))

                                {

                                    partEntry.textureName = newTextureName;

                                }

                                else if (string.IsNullOrEmpty(partEntry.textureName))

                                {

                                    partEntry.textureName = oldTextureName;

                                }



                                if (renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)

                                {

                                    string[] newNames = new string[renderer.sharedMaterials.Length];

                                    for (int m = 0; m < renderer.sharedMaterials.Length; m++)

                                    {

                                        string newName = GetMaterialTextureName(renderer.sharedMaterials[m]);

                                        if (!string.IsNullOrEmpty(newName))

                                        {

                                            newNames[m] = newName;

                                        }

                                        else if (oldTextureNames != null && m < oldTextureNames.Length && !string.IsNullOrEmpty(oldTextureNames[m]))

                                        {

                                            newNames[m] = oldTextureNames[m];

                                        }

                                        else

                                        {

                                            newNames[m] = "";

                                        }

                                    }

                                    partEntry.textureNames = newNames;

                                }



                                uint renderFlags = partEntry.renderFlags;

                                if (renderer.sharedMaterial != null)

                                {

                                    var mat = renderer.sharedMaterial;

                                    bool isTransparent = false;

                                    if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") == 1f)

                                        isTransparent = true;

                                    else if (mat.shader != null && mat.shader.name.ToLowerInvariant().Contains("transparent"))

                                        isTransparent = true;

                                    else if (mat.renderQueue >= 3000)

                                        isTransparent = true;



                                    if (isTransparent)

                                        renderFlags |= 0x1;

                                    else

                                        renderFlags &= 0xFFFFFFFE;

                                }

                                partEntry.renderFlags = renderFlags;



                                updatedParts.Add(partEntry);

                            }

                            if (updatedParts.Count > 0)

                            {

                                shape.parts = updatedParts.ToArray();

                            }

                        }

                    }



                    finalShapes.Add(shape);



                    if (shape.parts == null || shape.parts.Length == 0) continue;

                    string meshName = shape.parts[0].mesh != null ? shape.parts[0].mesh.name : "";

                    string key = $"{shape.position.x:F1}_{shape.position.z:F1}_{meshName}";

                    originalKeys.Add(key);

                }

            }



            // Build shape grid for comparison to avoid O(N^2) search

            var shapeGrid = new Dictionary<Vector2Int, List<KOShapeEntry>>();

            if (compareAsset.shapes != null)

            {

                foreach (var shape in compareAsset.shapes)

                {

                    Vector2Int cell = new Vector2Int(Mathf.FloorToInt(shape.position.x / 2.0f), Mathf.FloorToInt(shape.position.z / 2.0f));

                    if (!shapeGrid.TryGetValue(cell, out var list))

                    {

                        list = new List<KOShapeEntry>();

                        shapeGrid[cell] = list;

                    }

                    list.Add(shape);

                }

            }



            // Sahnede yeni eklenen objeleri tespit et (pasif/LOD meshler dahil)

            var rawAllRenderers = GameObject.FindObjectsByType<Renderer>(FindObjectsInactive.Include);

            var allRenderers = new List<Renderer>();

            foreach (var r in rawAllRenderers)

            {

                if (r is MeshRenderer || r is SkinnedMeshRenderer)

                {

                    allRenderers.Add(r);

                }

            }

            var newShapes = new List<KOShapeEntry>();

            int newObjectsCount = 0;

            var processedEventParents = new HashSet<GameObject>();



            foreach (var renderer in allRenderers)

            {

                if (renderer.GetComponent<Terrain>() != null)

                    continue;

                if (ShouldSkipLOD(renderer))

                    continue;



                if (renderer.gameObject.name.Contains("Directional Light") || renderer.gameObject.name.Contains("Main Camera"))

                    continue;



                Mesh sharedMesh = null;

                if (renderer is MeshRenderer mr)

                {

                    var filter = mr.GetComponent<MeshFilter>();

                    if (filter != null) sharedMesh = filter.sharedMesh;

                }

                else if (renderer is SkinnedMeshRenderer smr)

                {

                    sharedMesh = smr.sharedMesh;

                }

                if (sharedMesh == null) continue;



                var go = renderer.gameObject;

                

                // Find parentGo by walking up until hitting KOObjects or root

                var parentGo = go;

                var curr = go.transform;

                while (curr.parent != null)

                {

                    string pName = curr.parent.name;

                    if (pName == $"KOObjects_{zoneId}" || pName == "KOObjects" || 

                        pName == "WaterSystem" || pName == "Foliage" || pName == "Grass")

                    {

                        break;

                    }

                    parentGo = curr.parent.gameObject;

                    curr = curr.parent;

                }

                

                var we = parentGo.GetComponentInChildren<KOWorldEvent>(true);

                if (we != null)

                {

                    if (processedEventParents.Contains(parentGo)) continue;

                    processedEventParents.Add(parentGo);

                }



                string meshName = sharedMesh.name;

                Vector3 checkPos = (we != null) ? parentGo.transform.position : go.transform.position;

                string key = $"{checkPos.x:F1}_{checkPos.z:F1}_{meshName}";



                bool isNew = !originalKeys.Contains(key);

                if (isNew && compareAsset.shapes != null)

                {

                    Vector2Int parentCell = new Vector2Int(Mathf.FloorToInt(checkPos.x / 2.0f), Mathf.FloorToInt(checkPos.z / 2.0f));

                    bool foundMatch = false;



                    for (int dx = -1; dx <= 1 && !foundMatch; dx++)

                    {

                        for (int dz = -1; dz <= 1 && !foundMatch; dz++)

                        {

                            Vector2Int neighborCell = new Vector2Int(parentCell.x + dx, parentCell.y + dz);

                            if (shapeGrid.TryGetValue(neighborCell, out var cellShapes))

                            {

                                foreach (var origShape in cellShapes)

                                {

                                    if (Vector3.Distance(origShape.position, checkPos) < 0.15f)

                                    {

                                        string origMesh = (origShape.parts != null && origShape.parts.Length > 0 && origShape.parts[0].mesh != null) ? origShape.parts[0].mesh.name : "";

                                        if (origMesh == meshName)

                                        {

                                            isNew = false;

                                            foundMatch = true;

                                            break;

                                        }

                                    }

                                }

                            }

                        }

                    }

                }



                if (isNew)

                {

                    var shape = new KOShapeEntry();

                    shape.name = (we != null) ? parentGo.name : go.name;

                    shape.position = (we != null) ? parentGo.transform.position : go.transform.position;

                    shape.rotation = (we != null) ? parentGo.transform.rotation : go.transform.rotation;

                    shape.scale = (we != null) ? parentGo.transform.lossyScale : go.transform.lossyScale;

                    

                    if (we != null)

                    {

                        shape.eventID = we.EventID;

                        shape.eventType = we.EventType;

                        shape.npcID = we.NPC_ID;

                    }

                    else

                    {

                        shape.eventID = 0;

                        shape.eventType = 0;

                        shape.npcID = 0;

                    }

                    shape.shapeType = 0;

                    shape.npcStatus = 0;

                    shape.belong = 0;

                    shape.isCustom = true;



                    var part = new KOPartEntry();

                    part.mesh = sharedMesh;

                    part.material = renderer.sharedMaterial;

                    part.materials = renderer.sharedMaterials;



                    string newTextureName = GetMaterialTextureName(renderer.sharedMaterial);

                    if (!string.IsNullOrEmpty(newTextureName))

                    {

                        part.textureName = newTextureName;

                    }

                    if (renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0)

                    {

                        part.textureNames = new string[renderer.sharedMaterials.Length];

                        for (int m = 0; m < renderer.sharedMaterials.Length; m++)

                        {

                            string newName = GetMaterialTextureName(renderer.sharedMaterials[m]);

                            if (!string.IsNullOrEmpty(newName))

                            {

                                part.textureNames[m] = newName;

                            }

                        }

                    }



                    // Multi-submesh mesh'ler için gövde-only collider mesh çıkar

                    part.colliderMesh = ExtractTrunkColliderMesh(sharedMesh, renderer.sharedMaterials);



                    part.pivot = Vector3.zero;

                    part.texFPS = 0f;

                    part.animTextures = Array.Empty<Texture2D>();

                    

                    uint renderFlags = 0;

                    if (renderer.sharedMaterial != null)

                    {

                        var mat = renderer.sharedMaterial;

                        bool isTransparent = false;

                        if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") == 1f)

                        {

                            isTransparent = true;

                        }

                        else if (mat.shader != null && mat.shader.name.ToLowerInvariant().Contains("transparent"))

                        {

                            isTransparent = true;

                        }

                        else if (mat.renderQueue >= 3000)

                        {

                            isTransparent = true;

                        }



                        if (isTransparent)

                        {

                            renderFlags |= 0x1;

                        }

                    }

                    part.renderFlags = renderFlags;

                    part.srcBlend = 0;

                    part.destBlend = 0;



                    shape.parts = new KOPartEntry[] { part };

                    newShapes.Add(shape);

                    newObjectsCount++;

                }

            }



            if (newObjectsCount == 0 && deletedCount == 0)

            {

                EditorUtility.DisplayDialog("No Changes", "No changes detected (no new objects and no deleted objects in the scene).", "OK");

                return;

            }



            string confirmMsg = $"Detected changes in the scene:\n" +

                              $"- {newObjectsCount} new/custom objects to add\n" +

                              $"- {deletedCount} deleted/disabled objects to remove from original map\n\n" +

                              $"Do you want to save these changes to the {zoneName} zone asset?";



            if (!EditorUtility.DisplayDialog("Export Changes", confirmMsg, "Save Changes", "Cancel"))

            {

                return;

            }



            if (AssetDatabase.LoadAssetAtPath<KOZoneAsset>(backupPath) == null)

            {

                AssetDatabase.CopyAsset(assetPath, backupPath);

            }



            Undo.RecordObject(zoneAsset, "Sync Scene Changes");



            // Orijinal kalan objelerle yeni objeleri birleştir

            finalShapes.AddRange(newShapes);



            // Tekrar eden mükerrer kayıtları temizle (collision filtrelemeyi bozmaması için)

            var uniqueShapes = new List<KOShapeEntry>();

            var uniqueGrid = new Dictionary<Vector2Int, List<KOShapeEntry>>();

            int duplicatesRemoved = 0;



            foreach (var shape in finalShapes)

            {

                bool isDuplicate = false;

                Vector2Int shapeCell = new Vector2Int(Mathf.FloorToInt(shape.position.x / 2.0f), Mathf.FloorToInt(shape.position.z / 2.0f));



                for (int dx = -1; dx <= 1 && !isDuplicate; dx++)

                {

                    for (int dz = -1; dz <= 1 && !isDuplicate; dz++)

                    {

                        Vector2Int neighborCell = new Vector2Int(shapeCell.x + dx, shapeCell.y + dz);

                        if (uniqueGrid.TryGetValue(neighborCell, out var cellShapes))

                        {

                            foreach (var unique in cellShapes)

                            {

                                if (Vector3.Distance(shape.position, unique.position) < 0.05f)

                                {

                                    string shapeMesh = (shape.parts != null && shape.parts.Length > 0 && shape.parts[0].mesh != null) ? shape.parts[0].mesh.name : "";

                                    string uniqueMesh = (unique.parts != null && unique.parts.Length > 0 && unique.parts[0].mesh != null) ? unique.parts[0].mesh.name : "";

                                    if (shapeMesh == uniqueMesh && shape.name == unique.name)

                                    {

                                        isDuplicate = true;

                                        break;

                                    }

                                }

                            }

                        }

                    }

                }



                if (!isDuplicate)

                {

                    uniqueShapes.Add(shape);

                    if (!uniqueGrid.TryGetValue(shapeCell, out var list))

                    {

                        list = new List<KOShapeEntry>();

                        uniqueGrid[shapeCell] = list;

                    }

                    list.Add(shape);

                }

                else

                {

                    duplicatesRemoved++;

                }

            }



            if (duplicatesRemoved > 0)

            {

                Debug.Log($"[EXPORTER] Cleaned {duplicatesRemoved} duplicate shape entries.");

            }

            zoneAsset.shapes = uniqueShapes.ToArray();



            // Tüm shape'lerin colliderMesh'ini güncelle (eski export'larda eksik olanlar dahil)

            int colliderUpdated = 0;

            foreach (var shape in zoneAsset.shapes)

            {

                if (!shape.isCustom || shape.parts == null) continue;

                foreach (var part in shape.parts)

                {

                    if (part.colliderMesh == null && part.mesh != null && part.mesh.subMeshCount > 1 && part.materials != null && part.materials.Length > 1)

                    {

                        part.colliderMesh = ExtractTrunkColliderMesh(part.mesh, part.materials);

                        if (part.colliderMesh != null) colliderUpdated++;

                    }

                }

            }

            if (colliderUpdated > 0)

            {

                Debug.Log($"[EXPORTER] {colliderUpdated} mevcut shape'e colliderMesh eklendi.");

            }







            // Clear rivers/ponds if WaterSystem is deleted/inactive in the editor scene

            var waterSysGo = GameObject.Find("WaterSystem");

            if (waterSysGo == null || !waterSysGo.activeInHierarchy)

            {

                if ((zoneAsset.rivers != null && zoneAsset.rivers.Length > 0) || (zoneAsset.ponds != null && zoneAsset.ponds.Length > 0))

                {

                    zoneAsset.rivers = Array.Empty<KOWaterEntry>();

                    zoneAsset.ponds = Array.Empty<KOWaterEntry>();

                    Debug.Log("[EXPORTER] Cleared rivers and ponds because WaterSystem is missing or inactive in the scene.");

                }

            }



            EditorUtility.SetDirty(zoneAsset);



            // colliderMesh ve combined mesh'leri ScriptableObject'e sub-asset olarak kaydet (tüm shape'ler)

            string autoDetectAssetPath = AssetDatabase.GetAssetPath(zoneAsset);

            foreach (var shape in zoneAsset.shapes)

            {

                if (shape.parts == null) continue;

                foreach (var part in shape.parts)

                {

                    if (part.mesh != null)

                    {

                        string meshPath = AssetDatabase.GetAssetPath(part.mesh);

                        if (string.IsNullOrEmpty(meshPath)) // In-memory/combined mesh

                        {

                            Mesh clonedMesh = Instantiate(part.mesh);

                            clonedMesh.name = $"GenMesh_{shape.name.Replace("(Clone)", "").Trim()}_{part.mesh.name}";

                            

                            // Remove existing sub-asset with same name

                            var existing = AssetDatabase.LoadAllAssetsAtPath(autoDetectAssetPath);

                            foreach (var sub in existing)

                            {

                                if (sub is Mesh m && m.name == clonedMesh.name)

                                {

                                    AssetDatabase.RemoveObjectFromAsset(sub);

                                }

                            }

                            AssetDatabase.AddObjectToAsset(clonedMesh, zoneAsset);

                            part.mesh = clonedMesh; // Assign persistent sub-asset reference

                        }

                    }



                    if (part.colliderMesh != null)

                    {

                        var existing = AssetDatabase.LoadAllAssetsAtPath(autoDetectAssetPath);

                        foreach (var sub in existing)

                        {

                            if (sub is Mesh m && m.name == part.colliderMesh.name)

                            {

                                AssetDatabase.RemoveObjectFromAsset(sub);

                            }

                        }

                        AssetDatabase.AddObjectToAsset(part.colliderMesh, zoneAsset);

                    }

                }

            }



            SyncTerrainMaterialLayers(zoneId, zoneAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Successfully updated {zoneName} asset!\nAdded {newObjectsCount} objects, removed {deletedCount} objects.", "OK");

        }



        private static bool ShouldSkipLOD(Renderer renderer)

        {

            if (renderer == null) return false;



            string goName = renderer.gameObject.name.ToLowerInvariant();

            

            Mesh sharedMesh = null;

            if (renderer is MeshRenderer mr)

            {

                var filter = mr.GetComponent<MeshFilter>();

                if (filter != null) sharedMesh = filter.sharedMesh;

            }

            else if (renderer is SkinnedMeshRenderer smr)

            {

                sharedMesh = smr.sharedMesh;

            }

            string meshName = (sharedMesh != null ? sharedMesh.name : "").ToLowerInvariant();



            if (goName.Contains("lod1") || goName.Contains("lod2") || goName.Contains("lod3") || goName.Contains("lod4") || goName.Contains("lod5") ||

                meshName.Contains("lod1") || meshName.Contains("lod2") || meshName.Contains("lod3") || meshName.Contains("lod4") || meshName.Contains("lod5"))

            {

                return true;

            }

            

            var lodGroup = renderer.GetComponentInParent<LODGroup>();

            if (lodGroup != null)

            {

                var lods = lodGroup.GetLODs();

                if (lods.Length > 0)

                {

                    bool isInLOD0 = false;

                    foreach (var r in lods[0].renderers)

                    {

                        if (r == renderer)

                        {

                            isInLOD0 = true;

                            break;

                        }

                    }

                    if (!isInLOD0)

                    {

                        return true;

                    }

                }

            }

            

            return false;

        }

    
        private void SyncTerrainMaterialLayers(short zoneId, KOZoneAsset zoneAsset)
        {
            if (zoneAsset == null || zoneAsset.terrainData == null) return;
            
            string terrainMatPath = $"Assets/Resources/TerrainAssets/Zone_{zoneId}_Terrain_Mat.mat";
            Material terrainMat = AssetDatabase.LoadAssetAtPath<Material>(terrainMatPath);
            if (terrainMat != null)
            {
                var terrainLayers = zoneAsset.terrainData.terrainLayers;
                if (terrainLayers != null && terrainLayers.Length > 0)
                {
                    Undo.RecordObject(terrainMat, "Sync Terrain Material Layers");
                    int layerCount = terrainLayers.Length;
                    terrainMat.SetFloat("_NumLayersCount", layerCount);
                    
                    // Clear texture slots first
                    for (int i = 0; i < 8; i++)
                    {
                        terrainMat.SetTexture($"_Splat{i}", null);
                        terrainMat.SetTexture($"_Normal{i}", null);
                    }
                    
                    // Bind textures
                    for (int i = 0; i < layerCount; i++)
                    {
                        var layer = terrainLayers[i];
                        if (layer != null)
                        {
                            if (layer.diffuseTexture != null)
                            {
                                terrainMat.SetTexture($"_Splat{i}", layer.diffuseTexture);
                            }
                            if (layer.normalMapTexture != null)
                            {
                                terrainMat.SetTexture($"_Normal{i}", layer.normalMapTexture);
                            }
                        }
                    }
                    EditorUtility.SetDirty(terrainMat);
                    Debug.Log($"[EXPORTER] Automatically synced terrain material template on disk: {terrainMatPath} (Layers: {layerCount})");
                }
            }
        }
    }

}

