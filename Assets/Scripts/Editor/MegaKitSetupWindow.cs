using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace EntropyOnline.Editor
{
    public class MegaKitSetupWindow : EditorWindow
    {
        [MenuItem("Entropy Online/Setup MegaKits Tool", false, 33)]
        public static void ShowWindow()
        {
            GetWindow<MegaKitSetupWindow>("MegaKit Setup Tool");
        }

        private void OnGUI()
        {
            GUILayout.Label("MegaKit Setup & Prefab Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("This tool scans Assets/NewMegaKits/ and automatically:\n" +
                                    "1. Generates URP Lit materials for each model slot.\n" +
                                    "2. Matches and maps BaseColor and Normal textures.\n" +
                                    "3. Configures normal map import settings.\n" +
                                    "4. Creates ready-to-use Prefabs in a 'Prefabs' folder.", MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Scan and Setup All MegaKits", GUILayout.Height(45)))
            {
                SetupMegaKits();
            }
        }

        private void SetupMegaKits()
        {
            string rootPath = "Assets/NewMegaKits";
            if (!Directory.Exists(rootPath))
            {
                EditorUtility.DisplayDialog("Error", $"Folder not found: {rootPath}\nPlease copy the folders first.", "OK");
                return;
            }

            string[] subfolders = Directory.GetDirectories(rootPath);
            int folderCount = 0;

            foreach (var folder in subfolders)
            {
                string folderName = Path.GetFileName(folder);
                Debug.Log($"[MEGAKIT] Processing folder: {folderName}...");

                // Create Materials and Prefabs folders if they don't exist
                string matFolder = Path.Combine(folder, "Materials");
                string prefabFolder = Path.Combine(folder, "Prefabs");

                if (!Directory.Exists(matFolder)) Directory.CreateDirectory(matFolder);
                if (!Directory.Exists(prefabFolder)) Directory.CreateDirectory(prefabFolder);

                AssetDatabase.Refresh();

                // 1. Scan and index all textures in this folder
                string[] allFiles = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                List<string> textures = new List<string>();
                List<string> fbxFiles = new List<string>();

                foreach (var file in allFiles)
                {
                    string ext = Path.GetExtension(file).ToLower();
                    string assetPath = file.Replace('\\', '/');

                    if (ext == ".png" || ext == ".jpg" || ext == ".tga")
                    {
                        if (!assetPath.Contains("/Materials/") && !assetPath.Contains("/Prefabs/"))
                        {
                            textures.Add(assetPath);
                        }
                    }
                    else if (ext == ".fbx")
                    {
                        fbxFiles.Add(assetPath);
                    }
                }

                if (fbxFiles.Count == 0)
                {
                    Debug.LogWarning($"[MEGAKIT] No FBX files found in {folderName}");
                    continue;
                }

                // 2. Setup materials and create prefabs for each FBX
                int prefabCount = 0;
                float progress = 0f;

                for (int i = 0; i < fbxFiles.Count; i++)
                {
                    string fbxPath = fbxFiles[i];
                    progress = (float)i / fbxFiles.Count;
                    EditorUtility.DisplayProgressBar($"Setting up {folderName}", $"Processing {Path.GetFileName(fbxPath)}...", progress);

                    GameObject fbxGo = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                    if (fbxGo == null) continue;

                    GameObject tempInstance = PrefabUtility.InstantiatePrefab(fbxGo) as GameObject;
                    if (tempInstance == null) continue;

                    var renderers = tempInstance.GetComponentsInChildren<Renderer>(true);
                    foreach (var renderer in renderers)
                    {
                        Material[] sharedMaterials = renderer.sharedMaterials;
                        Material[] newMaterials = new Material[sharedMaterials.Length];

                        for (int m = 0; m < sharedMaterials.Length; m++)
                        {
                            Material origMat = sharedMaterials[m];
                            string matName = origMat != null ? origMat.name : "DefaultMaterial";

                            // Search for existing material or create new
                            string matAssetPath = Path.Combine(matFolder, matName + ".mat").Replace('\\', '/');
                            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);

                            if (mat == null)
                            {
                                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                                mat.SetFloat("_Smoothness", 0f);
                                mat.SetFloat("_Metallic", 0f);

                                // Map textures
                                string cleanMatName = CleanName(matName);
                                string baseTexPath = FindTexture(textures, cleanMatName, isNormal: false);
                                string normalTexPath = FindTexture(textures, cleanMatName, isNormal: true);

                                if (!string.IsNullOrEmpty(baseTexPath))
                                {
                                    Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexPath);
                                    if (baseTex != null)
                                    {
                                        mat.SetTexture("_BaseMap", baseTex);
                                    }
                                }

                                if (!string.IsNullOrEmpty(normalTexPath))
                                {
                                    MarkAsNormalMap(normalTexPath);
                                    Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexPath);
                                    if (normalTex != null)
                                    {
                                        mat.SetTexture("_BumpMap", normalTex);
                                        mat.EnableKeyword("_NORMALMAP");
                                    }
                                }

                                AssetDatabase.CreateAsset(mat, matAssetPath);
                                Debug.Log($"[MEGAKIT] Created Material: {matAssetPath}");
                            }

                            newMaterials[m] = mat;
                        }

                        renderer.sharedMaterials = newMaterials;
                    }

                    // Save as Prefab
                    string prefabPath = Path.Combine(prefabFolder, fbxGo.name + ".prefab").Replace('\\', '/');
                    PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabPath);
                    DestroyImmediate(tempInstance);
                    prefabCount++;
                }

                EditorUtility.ClearProgressBar();
                folderCount++;
                Debug.Log($"[MEGAKIT] Finished {folderName}: Generated {prefabCount} prefabs.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success", $"Successfully processed {folderCount} MegaKits!\n" +
                                                  "Check the 'Prefabs' and 'Materials' folders inside each MegaKit directory.", "OK");
        }

        private string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.ToLowerInvariant()
                       .Replace("m_", "")
                       .Replace("t_", "")
                       .Replace("_basecolor", "")
                       .Replace("_normal", "")
                       .Replace("_diffuse", "")
                       .Replace("_orm", "")
                       .Replace("_roughness", "")
                       .Replace(" ", "")
                       .Replace("_", "")
                       .Replace("-", "");
        }

        private string FindTexture(List<string> textures, string cleanMatName, bool isNormal)
        {
            string bestMatch = null;
            int bestScore = 0;

            foreach (var texPath in textures)
            {
                string texFileName = Path.GetFileNameWithoutExtension(texPath);
                string cleanTexName = CleanName(texFileName);
                bool texIsNormal = cleanTexName.Contains("normal") || texFileName.ToLowerInvariant().EndsWith("_n") || texFileName.ToLowerInvariant().Contains("_normal");

                if (isNormal != texIsNormal) continue;

                // Check if they match
                if (cleanTexName == cleanMatName || cleanTexName.Contains(cleanMatName) || cleanMatName.Contains(cleanTexName))
                {
                    int score = 0;
                    if (cleanTexName == cleanMatName) score = 100;
                    else score = Mathf.Max(cleanTexName.Length, cleanMatName.Length);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMatch = texPath;
                    }
                }
            }

            return bestMatch;
        }

        private void MarkAsNormalMap(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }
    }
}
