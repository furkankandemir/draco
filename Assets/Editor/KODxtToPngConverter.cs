using UnityEngine;
using UnityEditor;
using System.IO;
using EntropyOnline.Import;

namespace EntropyOnline.Editor
{
    public class KODxtToPngConverter : EditorWindow
    {
        [MenuItem("Window/KO Tools/Convert KOBinary DXT to PNG")]
        public static void ShowWindow()
        {
            GetWindow<KODxtToPngConverter>("DXT to PNG Converter");
        }

        private string _targetFolder = "Item"; // Default subfolder to scan
        private bool _overwriteExisting = false;

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("KOBinary DXT to PNG Texture Converter", EditorStyles.boldLabel);
            GUILayout.Label("Converts raw DXT binary files (_dxt.bytes) to standard Unity PNG textures.", EditorStyles.miniLabel);
            GUILayout.Space(10);

            _targetFolder = EditorGUILayout.TextField("KOBinary Folder (e.g. Item)", _targetFolder);
            _overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing PNGs", _overwriteExisting);

            GUILayout.Space(20);

            if (GUILayout.Button("Convert Folder", GUILayout.Height(40)))
            {
                RunConversion();
            }
        }

        private void RunConversion()
        {
            string sourceDir = Path.Combine(Application.dataPath, "Resources/KOBinary", _targetFolder);
            string destDir = Path.Combine(Application.dataPath, "Resources/KOTextures", _targetFolder);

            if (!Directory.Exists(sourceDir))
            {
                EditorUtility.DisplayDialog("Error", $"Source directory does not exist:\n{sourceDir}", "OK");
                return;
            }

            Directory.CreateDirectory(destDir);

            string[] files = Directory.GetFiles(sourceDir, "*_dxt.bytes");
            int total = files.Length;
            int converted = 0;
            int skipped = 0;

            for (int i = 0; i < total; i++)
            {
                string file = files[i];
                string baseName = Path.GetFileNameWithoutExtension(file); // e.g. "2_6104_10_0_dxt"
                
                // Remove the "_dxt" suffix to get original texture name
                if (baseName.EndsWith("_dxt"))
                {
                    baseName = baseName.Substring(0, baseName.Length - 4);
                }

                string pngName = baseName + ".png";
                string pngSavePath = Path.Combine(destDir, pngName);
                string pngAssetPath = $"Assets/Resources/KOTextures/{_targetFolder}/{pngName}";

                // Progress Bar
                EditorUtility.DisplayProgressBar("Converting DXT to PNG", $"Processing {baseName} ({i + 1}/{total})", (float)i / total);

                if (!_overwriteExisting && File.Exists(pngSavePath))
                {
                    skipped++;
                    continue;
                }

                string dxtVirtualPath = $"{_targetFolder}/{baseName}.dxt";
                Texture2D tex = DxtTextureImporter.Load(dxtVirtualPath, flipY: true);

                if (tex != null)
                {
                    byte[] pngBytes = tex.EncodeToPNG();
                    if (pngBytes != null)
                    {
                        File.WriteAllBytes(pngSavePath, pngBytes);
                        converted++;
                    }
                    DestroyImmediate(tex);
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Conversion Complete", 
                $"Successfully processed DXT textures from KOBinary/{_targetFolder}:\n\n" +
                $"• Converted: {converted}\n" +
                $"• Skipped (Already Exist): {skipped}\n" +
                $"• Total Scanned: {total}", "OK");
        }
    }
}
