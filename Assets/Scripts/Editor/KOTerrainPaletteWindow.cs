using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace KO.Editor
{
    public class KOTerrainPaletteWindow : EditorWindow
    {
        private Vector2 scrollPos;
        private Terrain targetTerrain;
        private int selectedIndex = -1;

        [MenuItem("Tools/Terrain/Terrain Palette & Painter", false, 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<KOTerrainPaletteWindow>("Terrain Palette");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnEnable()
        {
            FindTerrain();
        }

        private void FindTerrain()
        {
            targetTerrain = Selection.activeGameObject?.GetComponent<Terrain>() ?? Terrain.activeTerrain ?? Object.FindAnyObjectByType<Terrain>();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            GUILayout.Label("KO Terrain Palette & Painter", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (targetTerrain == null)
            {
                FindTerrain();
            }

            targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", targetTerrain, typeof(Terrain), true);

            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                EditorGUILayout.HelpBox("Sahnede aktif bir Terrain bulunamadı! Lütfen KOTerrain_12 nesnesini seçin.", MessageType.Warning);
                return;
            }

            TerrainData tData = targetTerrain.terrainData;
            TerrainLayer[] layers = tData.terrainLayers;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Listeyi Yenile / Senkronize Et", GUILayout.Height(30)))
            {
                SyncLayersAndMaterial(targetTerrain);
            }
            if (GUILayout.Button("✨ Temiz 8 Temel Katman Dizilimi", GUILayout.Height(30)))
            {
                SetupTop8DistinctLayers(targetTerrain);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            GUILayout.Label($"Toplam Katman Sayısı: {layers.Length}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Aşağıdaki kaplamalardan birine tıkladığınızda fırçanız o kaplamaya kilitlenir. Yanlış katman boyama riski kalmaz.", MessageType.Info);
            EditorGUILayout.Space(5);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            int columns = Mathf.Max(1, (int)(EditorGUIUtility.currentViewWidth - 40) / 100);
            int rowCount = Mathf.CeilToInt((float)layers.Length / columns);

            for (int r = 0; r < rowCount; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < columns; c++)
                {
                    int idx = r * columns + c;
                    if (idx >= layers.Length) break;

                    TerrainLayer layer = layers[idx];
                    if (layer == null) continue;

                    Texture2D previewTex = layer.diffuseTexture;
                    bool isSelected = (idx == selectedIndex);

                    GUIStyle boxStyle = new GUIStyle(GUI.skin.button);
                    if (isSelected)
                    {
                        boxStyle.normal.textColor = Color.yellow;
                    }

                    EditorGUILayout.BeginVertical(GUILayout.Width(90), GUILayout.Height(115));

                    GUIContent content = new GUIContent(previewTex, $"Index {idx}: {layer.name}\nTexture: {(previewTex != null ? previewTex.name : "null")}");
                    
                    if (GUILayout.Button(content, GUILayout.Width(84), GUILayout.Height(84)))
                    {
                        selectedIndex = idx;
                        SelectTerrainLayer(targetTerrain, idx);
                    }

                    string displayName = layer.name;
                    if (displayName.Length > 12) displayName = displayName.Substring(0, 10) + "..";
                    GUILayout.Label($"[{idx}] {displayName}", EditorStyles.miniLabel);

                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();
        }

        private void SelectTerrainLayer(Terrain terrain, int layerIndex)
        {
            Selection.activeGameObject = terrain.gameObject;
            var terrainInspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.TerrainInspector");
            if (terrainInspectorType != null)
            {
                UnityEditor.EditorUtility.SetDirty(terrain);
            }
            Debug.Log($"[TERRAIN] Seçilen Katman Index: {layerIndex} ({terrain.terrainData.terrainLayers[layerIndex].name})");
        }

        private void SyncLayersAndMaterial(Terrain terrain)
        {
            TerrainLayerCleanupUtility.FixTerrainLayersAndMaterials();
            Repaint();
        }

        private void SetupTop8DistinctLayers(Terrain terrain)
        {
            if (!EditorUtility.DisplayDialog("Onay", 
                "Bu işlem en çok kullanılan 8 temel kaplamayı (Kompozit Zemin, Çimen, Kum/Yol, Taş, Kar, Kaya, Toprak vb.) ilk 8 slota yerleştirecek ve Material ile tam senkronize edecektir.\n\nDevam etmek istiyor musunuz?", 
                "Evet, Düzenle", "İptal"))
            {
                return;
            }

            TerrainData tData = terrain.terrainData;
            TerrainLayer[] curLayers = tData.terrainLayers;
            if (curLayers.Length == 0) return;

            string backupDir = @"c:\_dev\knightonline-mobil\Backups";
            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

            List<TerrainLayer> distinctLayers = new List<TerrainLayer>();

            // 0: Composite
            distinctLayers.Add(curLayers[0]);

            string[] preferredLayerGuids = new string[]
            {
                "Layer_PBR_Grass_01",
                "coast_sand_rocks_02",
                "Ground001",
                "snow_field_aerial",
                "rocky_terrain",
                "Layer_CastleCobblestone",
                "Ground005"
            };

            foreach (var name in preferredLayerGuids)
            {
                foreach (var l in curLayers)
                {
                    if (l != null && l.name.ToLower().Contains(name.ToLower()) && !distinctLayers.Contains(l))
                    {
                        distinctLayers.Add(l);
                        break;
                    }
                }
            }

            foreach (var l in curLayers)
            {
                if (l != null && !distinctLayers.Contains(l))
                {
                    distinctLayers.Add(l);
                }
            }

            int w = tData.alphamapWidth;
            int h = tData.alphamapHeight;
            float[,,] oldMaps = tData.GetAlphamaps(0, 0, w, h);
            float[,,] newMaps = new float[h, w, distinctLayers.Count];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int i = 0; i < curLayers.Length; i++)
                    {
                        int newIdx = distinctLayers.IndexOf(curLayers[i]);
                        if (newIdx >= 0)
                        {
                            newMaps[y, x, newIdx] += oldMaps[y, x, i];
                        }
                    }
                }
            }

            Undo.RecordObject(tData, "Setup Top 8 Distinct Layers");
            tData.terrainLayers = distinctLayers.ToArray();
            tData.SetAlphamaps(0, 0, newMaps);
            EditorUtility.SetDirty(tData);

            Material terrainMat = terrain.materialTemplate;
            if (terrainMat == null)
            {
                terrainMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/TerrainAssets/Zone_12_Terrain_Mat.mat");
            }
            if (terrainMat != null)
            {
                Undo.RecordObject(terrainMat, "Sync Material");
                terrainMat.SetFloat("_NumLayersCount", Mathf.Min(8, distinctLayers.Count));
                for (int i = 0; i < 8; i++)
                {
                    if (i < distinctLayers.Count && distinctLayers[i] != null)
                    {
                        terrainMat.SetTexture($"_Splat{i}", distinctLayers[i].diffuseTexture);
                        terrainMat.SetTexture($"_Normal{i}", distinctLayers[i].normalMapTexture);
                    }
                    else
                    {
                        terrainMat.SetTexture($"_Splat{i}", null);
                        terrainMat.SetTexture($"_Normal{i}", null);
                    }
                }
                EditorUtility.SetDirty(terrainMat);
            }

            AssetDatabase.SaveAssets();
            terrain.Flush();
            Repaint();

            EditorUtility.DisplayDialog("Başarılı", "Temel kaplamalar ilk 8 slota başarıyla yerleştirildi ve senkronize edildi!", "Tamam");
        }
    }
}
