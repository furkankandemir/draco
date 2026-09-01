using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering.Universal;
using System.IO;
using System.Collections.Generic;

public class IntegrateNewAssets
{
    // =============================================
    // STEP 1: Move folders to NewMegaKits
    // =============================================
    [MenuItem("Entropy Online/Integrate Assets/Step 1 - Organize Folders")]
    static void Step1_OrganizeFolders()
    {
        Debug.Log("[INTEGRATE] Step 1: Organizing folders under NewMegaKits...");

        // Create NewMegaKits folder
        if (!AssetDatabase.IsValidFolder("Assets/NewMegaKits"))
            AssetDatabase.CreateFolder("Assets", "NewMegaKits");

        // Move StoneKeep
        MoveAssetFolder("Assets/StoneKeep", "Assets/NewMegaKits/MedievalStoneKeep");

        // Move MyRealMaterialsFree
        MoveAssetFolder("Assets/JailBreak/MyRealMaterialsFree", "Assets/NewMegaKits/RealMaterials");

        // Move Trees_WorldSpace_FREE
        MoveAssetFolder("Assets/Trees_WorldSpace_FREE", "Assets/NewMegaKits/WorldSpaceTrees");

        // Move Free Stylized Textures
        MoveAssetFolder("Assets/Game Buffs/Free Stylized Textures", "Assets/NewMegaKits/StylizedTextures");

        // Clean up empty parent folders
        CleanEmptyFolder("Assets/JailBreak");
        CleanEmptyFolder("Assets/Game Buffs");

        AssetDatabase.Refresh();
        Debug.Log("[INTEGRATE] Step 1 COMPLETE! Folders organized under Assets/NewMegaKits/");
    }

    // =============================================
    // STEP 2: Convert materials to URP
    // =============================================
    [MenuItem("Entropy Online/Integrate Assets/Step 2 - Convert Materials to URP")]
    static void Step2_ConvertMaterials()
    {
        Debug.Log("[INTEGRATE] Step 2: Converting materials to URP...");

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        Shader urpSimpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (urpLit == null)
        {
            Debug.LogError("[INTEGRATE] URP Lit shader not found!");
            return;
        }

        int converted = 0;

        // Convert MedievalStoneKeep materials
        converted += ConvertMaterialsInFolder("Assets/NewMegaKits/MedievalStoneKeep", urpLit);

        // Convert RealMaterials materials
        converted += ConvertMaterialsInFolder("Assets/NewMegaKits/RealMaterials", urpLit);

        // Convert WorldSpaceTrees SRP materials
        converted += ConvertTreeMaterials("Assets/NewMegaKits/WorldSpaceTrees", urpLit);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[INTEGRATE] Step 2 COMPLETE! Converted {converted} materials to URP.");
    }

    // =============================================
    // STEP 3: Create terrain layers for RealMaterials
    // =============================================
    [MenuItem("Entropy Online/Integrate Assets/Step 3 - Create Terrain Layers")]
    static void Step3_CreateTerrainLayers()
    {
        Debug.Log("[INTEGRATE] Step 3: Creating terrain layers for RealMaterials ground textures...");

        string[] groundSets = new string[]
        {
            "GroundMoistForestLeaves",
            "GroundWaterForestLeaves"
        };

        string texBasePath = "Assets/NewMegaKits/RealMaterials/Textures/Ground";
        string layerOutputPath = "Assets/NewMegaKits/RealMaterials/TerrainLayers";

        if (!AssetDatabase.IsValidFolder(layerOutputPath))
        {
            AssetDatabase.CreateFolder("Assets/NewMegaKits/RealMaterials", "TerrainLayers");
        }

        foreach (var setName in groundSets)
        {
            CreateTerrainLayerFromTextures(setName, texBasePath, layerOutputPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[INTEGRATE] Step 3 COMPLETE! Terrain layers created.");
    }

    // =============================================
    // ALL STEPS AT ONCE
    // =============================================
    [MenuItem("Entropy Online/Integrate Assets/Run ALL Steps")]
    static void RunAllSteps()
    {
        Step1_OrganizeFolders();
        Step2_ConvertMaterials();
        Step3_CreateTerrainLayers();
        Debug.Log("[INTEGRATE] === ALL INTEGRATION STEPS COMPLETE ===");
        EditorUtility.DisplayDialog("Integration Complete",
            "All 4 asset packages have been integrated!\n\n" +
            "• Folders organized under NewMegaKits\n" +
            "• Materials converted to URP\n" +
            "• Terrain layers created\n\n" +
            "Check Console for details.", "OK");
    }

    // =============================================
    // HELPER METHODS
    // =============================================

    static void MoveAssetFolder(string source, string destination)
    {
        if (!AssetDatabase.IsValidFolder(source))
        {
            Debug.LogWarning($"[INTEGRATE] Source folder not found: {source}");
            return;
        }

        if (AssetDatabase.IsValidFolder(destination))
        {
            Debug.LogWarning($"[INTEGRATE] Destination already exists: {destination}");
            return;
        }

        string result = AssetDatabase.MoveAsset(source, destination);
        if (string.IsNullOrEmpty(result))
        {
            Debug.Log($"[INTEGRATE] Moved: {source} → {destination}");
        }
        else
        {
            Debug.LogError($"[INTEGRATE] Failed to move {source}: {result}");
        }
    }

    static void CleanEmptyFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return;

        string fullPath = Path.Combine(Application.dataPath, folder.Replace("Assets/", ""));
        if (Directory.Exists(fullPath))
        {
            var files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);
            // Filter out .meta files
            var realFiles = new List<string>();
            foreach (var f in files)
            {
                if (!f.EndsWith(".meta")) realFiles.Add(f);
            }

            if (realFiles.Count == 0)
            {
                AssetDatabase.DeleteAsset(folder);
                Debug.Log($"[INTEGRATE] Cleaned empty folder: {folder}");
            }
        }
    }

    static int ConvertMaterialsInFolder(string folderPath, Shader targetShader)
    {
        int count = 0;
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Skip if already URP
            if (mat.shader != null && mat.shader.name.Contains("Universal Render Pipeline"))
                continue;

            // Skip demo/plane materials
            if (mat.name == "DemoPlane" || mat.name == "Plane") continue;

            // Save old texture references before shader change
            Texture albedo = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Texture normalMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            Texture heightMap = mat.HasProperty("_ParallaxMap") ? mat.GetTexture("_ParallaxMap") : null;
            Texture occlusionMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
            Texture metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            Texture specularMap = mat.HasProperty("_SpecGlossMap") ? mat.GetTexture("_SpecGlossMap") : null;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

            // Change shader to URP Lit
            mat.shader = targetShader;

            // Re-apply textures
            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", color);

            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (occlusionMap != null)
                mat.SetTexture("_OcclusionMap", occlusionMap);

            if (metallicMap != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallicMap);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else if (specularMap != null)
            {
                // Use specular as metallic approximation
                mat.SetTexture("_MetallicGlossMap", specularMap);
            }

            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            if (heightMap != null)
                mat.SetTexture("_ParallaxMap", heightMap);

            EditorUtility.SetDirty(mat);
            count++;
            Debug.Log($"[INTEGRATE] Converted material: {mat.name} ({path})");
        }

        return count;
    }

    static int ConvertTreeMaterials(string folderPath, Shader urpLit)
    {
        int count = 0;

        // Find SRP materials only (skip HDRP)
        string srpFolder = folderPath + "/Materials/SRP";
        if (!AssetDatabase.IsValidFolder(srpFolder))
        {
            // Try without subfolder
            srpFolder = folderPath + "/Materials";
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });

        // Find URP Nature shaders
        Shader speedTreeLeaf = Shader.Find("Universal Render Pipeline/Nature/SpeedTree8");
        Shader urpLitShader = urpLit;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Skip HDRP materials
            if (path.Contains("HDRP")) continue;

            // Skip if already URP
            if (mat.shader != null && mat.shader.name.Contains("Universal Render Pipeline"))
                continue;

            // Save textures
            Texture albedo = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Texture normalMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;

            bool isLeaf = mat.name.Contains("Leaf") || mat.name.Contains("leaf");
            bool isBark = mat.name.Contains("Bark") || mat.name.Contains("bark");

            mat.shader = urpLitShader;

            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", color);

            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
            }

            // For leaf materials, enable alpha clipping
            if (isLeaf)
            {
                mat.SetFloat("_AlphaClip", 1);
                mat.SetFloat("_Cutoff", cutoff);
                mat.SetFloat("_Surface", 0); // Opaque with alpha test
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = 2450;
            }

            EditorUtility.SetDirty(mat);
            count++;
            Debug.Log($"[INTEGRATE] Converted tree material: {mat.name} ({path})");
        }

        return count;
    }

    static void CreateTerrainLayerFromTextures(string setName, string texBasePath, string outputPath)
    {
        // Find textures
        string albedoPath = FindTexture(texBasePath, setName, "Albedo");
        string normalPath = FindTexture(texBasePath, setName, "Normal");

        if (string.IsNullOrEmpty(albedoPath))
        {
            Debug.LogWarning($"[INTEGRATE] Albedo texture not found for {setName}");
            return;
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D normal = !string.IsNullOrEmpty(normalPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null;

        // Create terrain layer
        TerrainLayer layer = new TerrainLayer();
        layer.diffuseTexture = albedo;
        layer.normalMapTexture = normal;
        layer.tileSize = new Vector2(5, 5);
        layer.tileOffset = Vector2.zero;

        string layerPath = $"{outputPath}/{setName}.terrainlayer";
        AssetDatabase.CreateAsset(layer, layerPath);
        Debug.Log($"[INTEGRATE] Created terrain layer: {layerPath}");
    }

    static string FindTexture(string basePath, string setName, string mapType)
    {
        // Try common naming patterns
        string[] patterns = new string[]
        {
            $"{basePath}/{setName}_{mapType}",
            $"{basePath}/{setName}/{setName}_{mapType}",
        };

        string[] extensions = new string[] { ".tga", ".png", ".jpg" };

        foreach (var pattern in patterns)
        {
            foreach (var ext in extensions)
            {
                string fullPath = pattern + ext;
                if (File.Exists(Path.Combine(Application.dataPath, fullPath.Replace("Assets/", ""))))
                    return fullPath;
            }
        }

        // Fallback: search with FindAssets
        string[] guids = AssetDatabase.FindAssets(setName + "_" + mapType, new[] { basePath });
        if (guids.Length > 0)
            return AssetDatabase.GUIDToAssetPath(guids[0]);

        return null;
    }
}
