using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using EntropyOnline.Import;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class KOAssetConverterWindow : EditorWindow
{
    private GameObject selectedGo;
    private string meshName = "";
    private string detectedTexture = "";
    private short detectedZoneId = 21;

    [MenuItem("Entropy Online/KO Asset Converter Window")]
    public static void ShowWindow()
    {
        GetWindow<KOAssetConverterWindow>("KO Asset Converter");
    }

    [MenuItem("GameObject/Entropy Online/Convert KO Object to Unity Format", false, 10)]
    public static void ConvertSelectedObjectMenu()
    {
        var activeGo = Selection.activeGameObject;
        if (activeGo == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a GameObject in the Hierarchy first.", "OK");
            return;
        }

        var window = GetWindow<KOAssetConverterWindow>("KO Asset Converter");
        window.selectedGo = activeGo;
        window.AutoDetectDetails();
        window.RunConversion();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        OnSelectionChanged();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        if (Selection.activeGameObject != selectedGo)
        {
            selectedGo = Selection.activeGameObject;
            AutoDetectDetails();
            Repaint();
        }
    }

    private void AutoDetectDetails()
    {
        if (selectedGo == null)
        {
            meshName = "";
            detectedTexture = "";
            return;
        }

        // Clean mesh name from gameobject name
        // e.g. "obj_mora_centawall_fens.n3pmesh" or "obj_mora_centawall_fens"
        string rawName = selectedGo.name;
        meshName = rawName.Replace(".n3pmesh", "").Replace(".N3PMESH", "");
        
        // Remove part suffixes if any (e.g. Part_0, etc.)
        int underIndex = meshName.LastIndexOf('_');
        if (underIndex > 0 && int.TryParse(meshName.Substring(underIndex + 1), out _))
        {
            meshName = meshName.Substring(0, underIndex);
        }

        // Auto-detect zone ID from scene
        detectedZoneId = 21;
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            if (root.name.StartsWith("KOTerrain_"))
            {
                string idStr = root.name.Substring("KOTerrain_".Length);
                if (short.TryParse(idStr, out var id))
                {
                    detectedZoneId = id;
                    break;
                }
            }
        }

        // Scan OPD file to find the matching texture
        detectedTexture = "";
        var zoneInfo = KOZoneMapper.GetZoneInfo(detectedZoneId);
        if (zoneInfo != null && !string.IsNullOrEmpty(zoneInfo.OpdFile))
        {
            string opdPath = Path.Combine("Zones", zoneInfo.OpdFile);
            if (KOBinaryProvider.Exists(opdPath))
            {
                try
                {
                    var opdFull = N3ShapeParser.ParseFull(opdPath);
                    if (opdFull != null)
                    {
                        foreach (var shape in opdFull.Shapes)
                        {
                            if (shape.Parts != null)
                            {
                                foreach (var part in shape.Parts)
                                {
                                    if (!string.IsNullOrEmpty(part.MeshFileName))
                                    {
                                        string opdMeshName = Path.GetFileNameWithoutExtension(part.MeshFileName).ToLowerInvariant();
                                        if (opdMeshName.Contains(meshName.ToLowerInvariant()) || meshName.ToLowerInvariant().Contains(opdMeshName))
                                        {
                                            if (part.TextureFileNames != null && part.TextureFileNames.Count > 0)
                                            {
                                                detectedTexture = part.TextureFileNames[0].Replace('\\', '/');
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            if (!string.IsNullOrEmpty(detectedTexture)) break;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[CONVERTER] OPD parse warning during auto-detection: {ex.Message}");
                }
            }
        }

        // Fallback texture name if OPD search failed
        if (string.IsNullOrEmpty(detectedTexture))
        {
            detectedTexture = $"Object/{meshName}.dxt";
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Knight Online to Unity Asset Converter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        selectedGo = (GameObject)EditorGUILayout.ObjectField("Selected Object", selectedGo, typeof(GameObject), true);

        if (selectedGo == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject in the Hierarchy to convert it.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        meshName = EditorGUILayout.TextField("Mesh File Name (KO)", meshName);
        detectedTexture = EditorGUILayout.TextField("Texture File Name (KO)", detectedTexture);
        detectedZoneId = (short)EditorGUILayout.IntField("Zone ID", detectedZoneId);

        EditorGUILayout.Space();
        if (GUILayout.Button("Convert and Replace Object in Scene", GUILayout.Height(40)))
        {
            RunConversion();
        }
    }

    private void RunConversion()
    {
        if (selectedGo == null || string.IsNullOrEmpty(meshName))
        {
            EditorUtility.DisplayDialog("Error", "Selected object or mesh name is invalid.", "OK");
            return;
        }

        string meshSaveDir = "Assets/Resources/KOModels/Object/Meshes";
        string texSaveDir = "Assets/Resources/KOTextures/Object";
        string matSaveDir = "Assets/Resources/KOModels/Object/Materials";
        string prefabSaveDir = "Assets/Prefabs/Object";

        Directory.CreateDirectory(meshSaveDir);
        Directory.CreateDirectory(texSaveDir);
        Directory.CreateDirectory(matSaveDir);
        Directory.CreateDirectory(prefabSaveDir);

        string cleanMeshName = Path.GetFileNameWithoutExtension(meshName);
        string meshAssetPath = Path.Combine(meshSaveDir, $"{cleanMeshName}.asset");
        string textureAssetPath = "";

        Debug.Log($"[CONVERTER] Starting conversion for {cleanMeshName}...");

        // 1. Convert Mesh
        Mesh unityMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
        if (unityMesh == null)
        {
            string koMeshPath = $"Object/{cleanMeshName}.n3pmesh";
            if (!KOBinaryProvider.Exists(koMeshPath))
            {
                koMeshPath = $"Object/Meshes/{cleanMeshName}.n3pmesh";
            }

            if (KOBinaryProvider.Exists(koMeshPath))
            {
                var lodData = N3PMeshImporter.Load(koMeshPath);
                if (lodData != null)
                {
                    unityMesh = N3PMeshImporter.CreateUnityMesh(lodData);
                    if (unityMesh != null)
                    {
                        AssetDatabase.CreateAsset(unityMesh, meshAssetPath);
                        Debug.Log($"[CONVERTER] Converted and saved Mesh to: {meshAssetPath}");
                    }
                }
            }
        }

        if (unityMesh == null)
        {
            // If raw files don't exist, try to extract existing mesh from MeshFilter
            var mf = selectedGo.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                unityMesh = Instantiate(mf.sharedMesh);
                AssetDatabase.CreateAsset(unityMesh, meshAssetPath);
                Debug.Log($"[CONVERTER] Extracted and saved Mesh from MeshFilter to: {meshAssetPath}");
            }
        }

        if (unityMesh == null)
        {
            EditorUtility.DisplayDialog("Error", $"Could not find or convert mesh file for {cleanMeshName}.", "OK");
            return;
        }

        // 2. Convert Texture
        Texture2D loadedTex = null;
        if (!string.IsNullOrEmpty(detectedTexture))
        {
            string cleanTexName = Path.GetFileNameWithoutExtension(detectedTexture);
            textureAssetPath = Path.Combine(texSaveDir, $"{cleanTexName}.png");

            loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (loadedTex == null)
            {
                string koDxtPath = detectedTexture;
                if (!KOBinaryProvider.Exists(koDxtPath))
                {
                    koDxtPath = $"Object/{cleanTexName}.dxt";
                }

                if (KOBinaryProvider.Exists(koDxtPath))
                {
                    Texture2D tex = DxtTextureImporter.Load(koDxtPath, flipY: true);
                    if (tex != null)
                    {
                        byte[] pngBytes = tex.EncodeToPNG();
                        if (pngBytes != null)
                        {
                            File.WriteAllBytes(textureAssetPath, pngBytes);
                            AssetDatabase.ImportAsset(textureAssetPath);
                            loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
                            Debug.Log($"[CONVERTER] Converted and saved Texture to: {textureAssetPath}");
                        }
                        DestroyImmediate(tex);
                    }
                }
            }
        }

        // 3. Create/Retrieve Material
        string matAssetPath = Path.Combine(matSaveDir, $"{cleanMeshName}_Mat.mat");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            if (loadedTex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", loadedTex);
                else mat.mainTexture = loadedTex;
            }
            mat.color = Color.white;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(mat, matAssetPath);
            Debug.Log($"[CONVERTER] Created and saved Material to: {matAssetPath}");
        }
        else if (loadedTex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", loadedTex);
            else mat.mainTexture = loadedTex;
            EditorUtility.SetDirty(mat);
        }

        // 4. Update scene GameObject with native Mesh & Material
        var meshFilter = selectedGo.GetComponentInChildren<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = selectedGo.AddComponent<MeshFilter>();
        }
        meshFilter.sharedMesh = unityMesh;

        var meshRenderer = selectedGo.GetComponentInChildren<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = selectedGo.AddComponent<MeshRenderer>();
        }
        meshRenderer.sharedMaterial = mat;

        var meshCollider = selectedGo.GetComponentInChildren<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = selectedGo.AddComponent<MeshCollider>();
        }
        meshCollider.sharedMesh = unityMesh;

        // Clean names of children to match new native setup
        foreach (Transform child in selectedGo.transform)
        {
            if (child.name.ToLower().Contains(cleanMeshName.ToLower()))
            {
                var childMf = child.GetComponent<MeshFilter>();
                if (childMf != null) childMf.sharedMesh = unityMesh;
                var childMr = child.GetComponent<MeshRenderer>();
                if (childMr != null) childMr.sharedMaterial = mat;
                var childMc = child.GetComponent<MeshCollider>();
                if (childMc != null) childMc.sharedMesh = unityMesh;
            }
            if (child.name.ToLower().EndsWith(".n3pmesh"))
            {
                child.name = child.name.Substring(0, child.name.Length - 8);
            }
        }

        // Rename parent object to remove extension
        if (selectedGo.name.ToLower().EndsWith(".n3pmesh"))
        {
            selectedGo.name = selectedGo.name.Substring(0, selectedGo.name.Length - 8);
        }
        else
        {
            selectedGo.name = cleanMeshName;
        }

        // Save as Prefab Asset and connect it so it turns blue in the hierarchy
        string prefabPath = Path.Combine(prefabSaveDir, $"{selectedGo.name}.prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(selectedGo, prefabPath, InteractionMode.AutomatedAction);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Mark scene dirty
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[CONVERTER] ✅ Successfully converted {cleanMeshName} to standard Unity format and saved as Prefab!");
        EditorUtility.DisplayDialog("Success", 
            $"Successfully converted {cleanMeshName} to Unity format!\n\n" +
            $"• Mesh saved: {meshAssetPath}\n" +
            $"• Texture saved: {textureAssetPath}\n" +
            $"• Material saved: {matAssetPath}\n" +
            $"• Prefab saved: {prefabPath}", "OK");
    }
}
