using UnityEngine;
using UnityEditor;
using System.IO;

public class MaterialURPConverter : EditorWindow
{
    private static readonly string[] TargetFolders = new string[]
    {
        "Assets/Hovl Studio/AAA Magic circles Vol 3",
        "Assets/Magic Circle Fx",
        "Assets/Piloto Studio",
        "Assets/VFX_Klaus"
    };

    [MenuItem("Tools/Antigravity/Convert New Materials to URP")]
    public static void ConvertMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", TargetFolders);
        int convertedCount = 0;

        Shader urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (urpParticleShader == null)
        {
            Debug.LogError("[Converter] Universal Render Pipeline/Particles/Unlit shader not found! Make sure URP is active.");
            return;
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            Shader currentShader = mat.shader;
            
            // If the shader is legacy/built-in
            if (currentShader != null && !currentShader.name.StartsWith("Universal Render Pipeline/") && !currentShader.name.StartsWith("Shader Graphs/"))
            {
                // Backup main texture and color
                Texture mainTex = mat.mainTexture;
                Color mainColor = mat.color;

                // Set URP shader
                mat.shader = urpParticleShader;

                // Re-apply properties to URP map structure
                if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                mat.SetColor("_BaseColor", mainColor);

                // Determine Blend Mode (Additive vs Alpha)
                string searchKey = (mat.name + "_" + currentShader.name).ToLower();
                bool isAdditive = searchKey.Contains("add") || searchKey.Contains("glow") || searchKey.Contains("spark") || searchKey.Contains("fire") || searchKey.Contains("light");

                mat.SetFloat("_Surface", 1); // 1 = Transparent

                if (isAdditive)
                {
                    mat.SetFloat("_Blend", 1); // 1 = Additive
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                }
                else
                {
                    mat.SetFloat("_Blend", 0); // 0 = Alpha Blend
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }

                // Disable depth write for transparency
                mat.SetInt("_ZWrite", 0);

                EditorUtility.SetDirty(mat);
                convertedCount++;
                Debug.Log($"[Converter] Converted material: {path} (Additive: {isAdditive})");
            }
        }

        if (convertedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Converter] Converted {convertedCount} materials to URP successfully!");
        }
        else
        {
            Debug.Log("[Converter] No materials needed conversion in targeted folders.");
        }
    }
}
