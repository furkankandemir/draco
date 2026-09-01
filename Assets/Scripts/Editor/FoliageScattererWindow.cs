using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using EntropyOnline.Import;
using EntropyOnline.World;

namespace EntropyOnline.Editor
{
    public class FoliageScattererWindow : EditorWindow
    {
        private short zoneId = 21; // Default Moradon
        private GameObject prefabToScatter;
        private string[] textureSources = new string[0];
        private int selectedTextureIndex = 0;
        
        private string textureFilter = "ngrass"; // Default grass filter
        private float densityPerCell = 0.5f; // Average count per cell
        private float minScale = 0.8f;
        private float maxScale = 1.2f;
        private bool randomYRotation = true;
        private string parentName = "Scattered_Foliage";

        private GtdTerrainImporter.GtdData loadedGtdData;
        private string loadedGtdPath;

        [MenuItem("Entropy Online/Foliage Scatterer Tool", false, 32)]
        public static void ShowWindow()
        {
            GetWindow<FoliageScattererWindow>("Foliage Scatterer");
        }

        private void OnGUI()
        {
            GUILayout.Label("Foliage Scatterer Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            zoneId = (short)EditorGUILayout.IntField("Zone ID", zoneId);
            prefabToScatter = (GameObject)EditorGUILayout.ObjectField("Prefab to Scatter", prefabToScatter, typeof(GameObject), false);
            parentName = EditorGUILayout.TextField("Parent Container Name", parentName);

            EditorGUILayout.Space();
            if (GUILayout.Button("Load Texture List from GTD file"))
            {
                LoadTextureList();
            }

            EditorGUILayout.Space();
            GUILayout.Label("Option 1: Scatter on Single Texture", EditorStyles.boldLabel);
            if (textureSources.Length > 0)
            {
                selectedTextureIndex = EditorGUILayout.Popup("Select Texture", selectedTextureIndex, textureSources);
                
                GUI.enabled = (prefabToScatter != null);
                if (GUILayout.Button("Scatter on Selected Single Texture"))
                {
                    ScatterFoliage(useFilter: false);
                }
                GUI.enabled = true;
            }
            else
            {
                EditorGUILayout.HelpBox("Load texture list from the GTD file first.", MessageType.Info);
            }

            EditorGUILayout.Space();
            GUILayout.Label("Option 2: Bulk Scatter by Keyword (Recommended)", EditorStyles.boldLabel);
            textureFilter = EditorGUILayout.TextField("Texture Name Filter (e.g. grass)", textureFilter);
            
            GUI.enabled = (prefabToScatter != null && textureSources.Length > 0 && !string.IsNullOrEmpty(textureFilter));
            if (GUILayout.Button("Scatter on All Textures matching Filter", GUILayout.Height(30)))
            {
                ScatterFoliage(useFilter: true);
            }
            GUI.enabled = true;

            EditorGUILayout.Space();
            GUILayout.Label("Scatter Settings", EditorStyles.boldLabel);
            densityPerCell = EditorGUILayout.Slider("Density per Cell", densityPerCell, 0.05f, 5f);
            
            EditorGUILayout.Space();
            GUILayout.Label("Randomization Settings", EditorStyles.boldLabel);
            minScale = EditorGUILayout.Slider("Min Scale", minScale, 0.1f, 5f);
            maxScale = EditorGUILayout.Slider("Max Scale", maxScale, 0.1f, 5f);
            randomYRotation = EditorGUILayout.Toggle("Random Y Rotation", randomYRotation);
        }

        private void LoadTextureList()
        {
            string gtdPath = KOZoneMapper.GetGtdPath(zoneId);
            if (string.IsNullOrEmpty(gtdPath) || !KOBinaryProvider.Exists(gtdPath))
            {
                EditorUtility.DisplayDialog("Error", $"GTD file not found: {gtdPath}", "OK");
                return;
            }

            loadedGtdData = GtdTerrainImporter.Parse(gtdPath);
            if (loadedGtdData == null || loadedGtdData.TileTexSources == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to parse GTD file or extract textures.", "OK");
                return;
            }

            loadedGtdPath = gtdPath;
            
            // Populate textureSources array
            List<string> options = new List<string>();
            for (int i = 0; i < loadedGtdData.TileTexSources.Count; i++)
            {
                options.Add($"[{i}] {loadedGtdData.TileTexSources[i]}");
            }
            textureSources = options.ToArray();
            selectedTextureIndex = 0;
            
            Debug.Log($"[SCATTER] Loaded {textureSources.Length} texture sources from {gtdPath}");
        }

        private void ScatterFoliage(bool useFilter)
        {
            if (loadedGtdData == null || loadedGtdPath == null)
            {
                LoadTextureList();
            }

            if (loadedGtdData == null) return;

            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain == null)
            {
                EditorUtility.DisplayDialog("Error", "No active Terrain found in the scene! Please make sure the terrain is loaded.", "OK");
                return;
            }

            GameObject parentContainer = GameObject.Find(parentName);
            if (parentContainer == null)
            {
                parentContainer = new GameObject(parentName);
                Undo.RegisterCreatedObjectUndo(parentContainer, "Create Parent Container");
            }

            HashSet<int> allowedSrcIndices = new HashSet<int>();
            string targetNameInfo = "";

            if (useFilter)
            {
                string filterLower = textureFilter.ToLowerInvariant();
                for (int i = 0; i < loadedGtdData.TileTexSources.Count; i++)
                {
                    if (loadedGtdData.TileTexSources[i].ToLowerInvariant().Contains(filterLower))
                    {
                        allowedSrcIndices.Add(i);
                    }
                }
                targetNameInfo = $"matching filter '{textureFilter}' ({allowedSrcIndices.Count} textures)";
            }
            else
            {
                allowedSrcIndices.Add(selectedTextureIndex);
                targetNameInfo = $"'{loadedGtdData.TileTexSources[selectedTextureIndex]}'";
            }

            if (allowedSrcIndices.Count == 0)
            {
                EditorUtility.DisplayDialog("Warning", "No textures match your filter keyword.", "OK");
                return;
            }

            int cellCount = loadedGtdData.MapSize - 1;
            const float TILE_SIZE = 4.0f;
            int scatteredCount = 0;
            
            // Collect matching cells
            List<Vector2Int> matchingCells = new List<Vector2Int>();
            for (int cx = 0; cx < cellCount; cx++)
            {
                for (int cz = 0; cz < cellCount; cz++)
                {
                    var cell = loadedGtdData.CellData[cx, cz];
                    bool cellMatches = false;
                    
                    if (cell.Tex1Idx >= 0 && cell.Tex1Idx < loadedGtdData.TileTextures.Count)
                    {
                        if (allowedSrcIndices.Contains(loadedGtdData.TileTextures[cell.Tex1Idx].SrcIdx))
                            cellMatches = true;
                    }
                    if (!cellMatches && cell.Tex2Idx >= 0 && cell.Tex2Idx < loadedGtdData.TileTextures.Count)
                    {
                        if (allowedSrcIndices.Contains(loadedGtdData.TileTextures[cell.Tex2Idx].SrcIdx))
                            cellMatches = true;
                    }

                    if (cellMatches)
                    {
                        matchingCells.Add(new Vector2Int(cx, cz));
                    }
                }
            }

            if (matchingCells.Count == 0)
            {
                EditorUtility.DisplayDialog("Warning", "No cells in the terrain use the specified texture(s).", "OK");
                return;
            }

            string confirmMsg = $"Found {matchingCells.Count} cells using texture(s) {targetNameInfo}.\n" +
                               $"Estimated spawn count: {Mathf.RoundToInt(matchingCells.Count * densityPerCell)} prefabs.\n\n" +
                               $"Do you want to scatter the foliage?";

            if (!EditorUtility.DisplayDialog("Scatter Foliage", confirmMsg, "Scatter", "Cancel"))
            {
                return;
            }

            // Begin batch undo group
            Undo.SetCurrentGroupName("Scatter Foliage by Texture");
            int group = Undo.GetCurrentGroup();

            foreach (var cellCoord in matchingCells)
            {
                int cx = cellCoord.x;
                int cz = cellCoord.y;

                float wx = cx * TILE_SIZE + TILE_SIZE * 0.5f;
                float wz = cz * TILE_SIZE + TILE_SIZE * 0.5f;

                // Determine how many to spawn in this cell
                int spawnCount = Mathf.FloorToInt(densityPerCell) + (Random.value < (densityPerCell % 1.0f) ? 1 : 0);

                for (int i = 0; i < spawnCount; i++)
                {
                    float offsetX = Random.Range(-TILE_SIZE * 0.5f, TILE_SIZE * 0.5f);
                    float offsetZ = Random.Range(-TILE_SIZE * 0.5f, TILE_SIZE * 0.5f);

                    float posX = wx + offsetX;
                    float posZ = wz + offsetZ;

                    // Sample terrain height
                    float posY = activeTerrain.SampleHeight(new Vector3(posX, 0, posZ)) + activeTerrain.transform.position.y;

                    // Instantiate
                    GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefabToScatter);
                    if (newObj != null)
                    {
                        newObj.transform.position = new Vector3(posX, posY, posZ);
                        
                        if (randomYRotation)
                        {
                            newObj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        }

                        float scaleFactor = Random.Range(minScale, maxScale);
                        newObj.transform.localScale = prefabToScatter.transform.localScale * scaleFactor;

                        newObj.transform.SetParent(parentContainer.transform);
                        Undo.RegisterCreatedObjectUndo(newObj, "Scatter Prefab");
                        scatteredCount++;
                    }
                }
            }

            Undo.CollapseUndoOperations(group);
            
            // Mark scene dirty
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log($"[SCATTER] Successfully scattered {scatteredCount} prefabs on textures {targetNameInfo}");
            EditorUtility.DisplayDialog("Success", $"Successfully scattered {scatteredCount} foliage prefabs onto the terrain!", "OK");
        }
    }
}
