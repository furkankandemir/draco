using UnityEngine;
using UnityEditor;

namespace KO.Editor
{
    public class FixNamesOnce
    {
        [MenuItem("Tools/Terrain/Fix Internal Names")]
        public static void Fix()
        {
            TerrainData td = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Resources/TerrainAssets/Zone_12.asset");
            if (td != null)
            {
                td.name = "Zone_12";
                EditorUtility.SetDirty(td);
            }
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/TerrainAssets/Zone_12_Terrain_Mat.mat");
            if (mat != null)
            {
                mat.name = "Zone_12_Terrain_Mat";
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[NAME-FIX] Tamamlandı.");
        }
    }
}
