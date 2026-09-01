using UnityEditor;
using UnityEngine;
using System.IO;

namespace EntropyOnline.Editor
{
    public static class ShaderRecompiler
    {
        [MenuItem("Draco Tools/Force Recompile All Shaders")]
        public static void RecompileAllShaders()
        {
            string[] shadergraphs = Directory.GetFiles(Application.dataPath, "*.shadergraph", SearchOption.AllDirectories);
            string[] subgraphs = Directory.GetFiles(Application.dataPath, "*.shadersubgraph", SearchOption.AllDirectories);
            string[] shaders = Directory.GetFiles(Application.dataPath, "*.shader", SearchOption.AllDirectories);

            int count = 0;
            
            // Loop through all shadergraphs
            foreach (var file in shadergraphs)
            {
                string relativePath = "Assets" + file.Replace(Application.dataPath, "").Replace('\\', '/');
                AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
                count++;
            }
            
            // Loop through all subgraphs
            foreach (var file in subgraphs)
            {
                string relativePath = "Assets" + file.Replace(Application.dataPath, "").Replace('\\', '/');
                AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
                count++;
            }
            
            // Loop through all shaders
            foreach (var file in shaders)
            {
                string relativePath = "Assets" + file.Replace(Application.dataPath, "").Replace('\\', '/');
                AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
                count++;
            }

            // Force update asset database
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            
            Debug.Log($"[ShaderRecompiler] Successfully recompiled {count} shader files.");
            EditorUtility.DisplayDialog("Shader Recompile", $"Successfully recompiled {count} shaders and shadergraphs! All pink shader issues should now be fixed.", "OK");
        }
    }
}
