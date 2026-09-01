using UnityEngine;
using UnityEditor;
using System.IO;

namespace EntropyOnline.Editor
{
    public class ThroneMaterialSetup
    {
        [MenuItem("Tools/Setup Throne Materials")]
        public static void Setup()
        {
            string baseDir = "Assets/CharacterSelectModels";
            
            // 1. Process Orc Throne (Karus)
            SetupThrone(
                Path.Combine(baseDir, "orc_throne"),
                "OrcThrone_Mat",
                "Meshy_AI_Inferno_Throne_0810003630_texture.png",
                null, // Normal map yok
                null, // Metallic map yok
                null  // Emission map yok
            );

            // 1b. Process Orc Throne 2 (Karus alternative)
            SetupThrone(
                Path.Combine(baseDir, "orc_throne2"),
                "OrcThrone2_Mat",
                "Meshy_AI_Infernal_Throne_0809224909_texture.png",
                "Meshy_AI_Infernal_Throne_0809224909_texture_normal.png",
                "Meshy_AI_Infernal_Throne_0809224909_texture_metallic_roughness.png",
                "Meshy_AI_Infernal_Throne_0809224909_texture_emit.png"
            );

            // 2. Process Human Throne (Human)
            SetupThrone(
                Path.Combine(baseDir, "human_throne"),
                "HumanThrone_Mat",
                "Meshy_AI_The_Crimson_Throne_0810003113_texture.png",
                "Meshy_AI_The_Crimson_Throne_0810003113_texture_normal.png",
                "Meshy_AI_The_Crimson_Throne_0810003113_texture_metallic_roughness.png",
                "Meshy_AI_The_Crimson_Throne_0810003113_texture_emission.png"
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Setup Complete", "Throne materials and texture optimizations have been successfully created/applied!", "OK");
            Debug.Log("[ThroneSetup] Materials and textures setup complete!");
        }

        private static void SetupThrone(string dir, string matName, string diffTex, string normTex, string metTex, string emitTex)
        {
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"[ThroneSetup] Directory not found: {dir}");
                return;
            }

            // Create or Load Material
            string matPath = Path.Combine(dir, matName + ".mat");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            // Setup Diffuse (Base Map)
            if (!string.IsNullOrEmpty(diffTex))
            {
                string texPath = Path.Combine(dir, diffTex);
                OptimizeTexture(texPath, false);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTexture("_MainTex", tex);
                }
            }

            // Setup Normal
            if (!string.IsNullOrEmpty(normTex))
            {
                string texPath = Path.Combine(dir, normTex);
                OptimizeTexture(texPath, true);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex != null)
                {
                    mat.SetTexture("_BumpMap", tex);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            // Setup Metallic/Roughness
            if (!string.IsNullOrEmpty(metTex))
            {
                string texPath = Path.Combine(dir, metTex);
                OptimizeTexture(texPath, false);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex != null)
                {
                    mat.SetTexture("_MetallicGlossMap", tex);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetFloat("_Metallic", 1.0f);
                    mat.SetFloat("_Smoothness", 0.5f);
                }
            }

            // Setup Emission
            if (!string.IsNullOrEmpty(emitTex))
            {
                string texPath = Path.Combine(dir, emitTex);
                OptimizeTexture(texPath, false);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex != null)
                {
                    mat.SetTexture("_EmissionMap", tex);
                    mat.SetColor("_EmissionColor", Color.white);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
            }

            EditorUtility.SetDirty(mat);
        }

        private static void OptimizeTexture(string path, bool isNormal)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool changed = false;
            
            // Set normal map type
            if (isNormal && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            
            // Limit max size to 2048
            if (importer.maxTextureSize > 2048)
            {
                importer.maxTextureSize = 2048;
                changed = true;
            }
            
            // Enable crunch compression for size optimization
            if (!importer.crunchedCompression)
            {
                importer.crunchedCompression = true;
                importer.compressionQuality = 90;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }
}
