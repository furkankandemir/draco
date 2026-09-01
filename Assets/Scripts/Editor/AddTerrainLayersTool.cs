using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace EntropyOnline.Editor
{
    public class AddTerrainLayersTool : EditorWindow
    {
        [MenuItem("Entropy Online/Add MegaKit Terrain Layers", false, 34)]
        public static void ShowWindow()
        {
            GetWindow<AddTerrainLayersTool>("MegaKit Terrain Layers");
        }

        private void OnGUI()
        {
            GUILayout.Label("Araziye Zemin Kaplamaları Ekle (Terrain Layers)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("Bu araç, projedeki tüm özel boyama kaplamalarını (indirdiğiniz tüm paketleri) aktif Terrain nesnesine ekler. Orijinal oyun kaplamalarını ve otomatik üretilen katmanları eklemez.", MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Zemin Kaplamalarını Ekle", GUILayout.Height(40)))
            {
                AddTerrainLayers();
            }
        }

        private void AddTerrainLayers()
        {
            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain == null || activeTerrain.terrainData == null)
            {
                EditorUtility.DisplayDialog("Hata", "Sahnede aktif bir Terrain (Arazi) bulunamadı! Lütfen sahnenin yüklü olduğundan emin olun.", "Tamam");
                return;
            }

            List<TerrainLayer> targetLayers = new List<TerrainLayer>();
            HashSet<Texture2D> addedTextures = new HashSet<Texture2D>();

            // Projedeki tum TerrainLayer dosyalarini bul
            string[] allGuids = AssetDatabase.FindAssets("t:TerrainLayer");
            foreach (var guid in allGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string lowerPath = path.ToLower().Replace('\\', '/');

                // Haric tutulacak klasorler/dosyalar (Orijinal oyun ve otomatik uretilenler)
                if (lowerPath.Contains("/_terrainautoupgrade") || 
                    lowerPath.Contains("composite") || 
                    lowerPath.Contains("colormap") ||
                    lowerPath.Contains("/terrain/terrain_") ||
                    (lowerPath.Contains("/resources/") && !lowerPath.Contains("polyhaventextures") && !lowerPath.Contains("deserttextures")))
                {
                    continue;
                }

                TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer != null && layer.diffuseTexture != null)
                {
                    // Specular veya mask texture olanlari atla
                    if (layer.diffuseTexture.name.EndsWith("_S") || layer.diffuseTexture.name.EndsWith("_mask"))
                        continue;

                    if (!addedTextures.Contains(layer.diffuseTexture))
                    {
                        targetLayers.Add(layer);
                        addedTextures.Add(layer.diffuseTexture);
                    }
                }
            }

            TerrainData terrainData = activeTerrain.terrainData;
            List<TerrainLayer> newLayersList = new List<TerrainLayer>();

            // Arazinin ilk kaplamasini (Kompozit referans haritasi) koru
            if (terrainData.terrainLayers.Length > 0 && terrainData.terrainLayers[0] != null)
            {
                newLayersList.Add(terrainData.terrainLayers[0]);
                if (terrainData.terrainLayers[0].diffuseTexture != null)
                    addedTextures.Add(terrainData.terrainLayers[0].diffuseTexture);
            }

            // Indirilen ozel kaplamalari ekle
            foreach (var layer in targetLayers)
            {
                if (layer != null && !newLayersList.Contains(layer))
                {
                    newLayersList.Add(layer);
                }
            }

            // Arazinin zemin kaplamalarini guncelle
            Undo.RecordObject(terrainData, "Add MegaKit Terrain Layers");
            terrainData.terrainLayers = newLayersList.ToArray();
            EditorUtility.SetDirty(terrainData);

            // Material sablonunu senkronize et
            Material terrainMat = activeTerrain.materialTemplate;
            if (terrainMat == null)
            {
                string defaultMatPath = "Assets/Resources/TerrainAssets/Zone_12_Terrain_Mat.mat";
                terrainMat = AssetDatabase.LoadAssetAtPath<Material>(defaultMatPath);
            }

            if (terrainMat != null)
            {
                Undo.RecordObject(terrainMat, "Sync Material Layers");
                terrainMat.SetFloat("_NumLayersCount", Mathf.Min(8, newLayersList.Count));
                for (int i = 0; i < 8; i++)
                {
                    if (i < newLayersList.Count && newLayersList[i] != null)
                    {
                        terrainMat.SetTexture($"_Splat{i}", newLayersList[i].diffuseTexture);
                        terrainMat.SetTexture($"_Normal{i}", newLayersList[i].normalMapTexture);
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
            AssetDatabase.Refresh();
            activeTerrain.Flush();

            Debug.Log($"[TERRAIN] Setup completed. Active layers count: {newLayersList.Count} (Added custom: {targetLayers.Count})");
            EditorUtility.DisplayDialog("Basarili", $"Basariyla {targetLayers.Count} adet ozel yuksek kaliteli kaplama Terrain'e eklendi ve Material ile senkronize edildi!\nHatali/siyah dokular ve mukerrer kayitlar temizlendi.", "Tamam");
        }
    }
}
