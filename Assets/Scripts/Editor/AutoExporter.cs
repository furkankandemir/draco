using UnityEditor;
using UnityEngine;

namespace EntropyOnline.Editor
{
    public static class AutoExporter
    {
        [MenuItem("Entropy Online/Auto Export and Place Objects", false, 150)]
        public static void Run()
        {
            Debug.Log("[AUTO-EXPORT] Starting Auto Export...");
            
            // 1. Create instance of exporter window
            var window = ScriptableObject.CreateInstance<KOTerrainExporterWindow>();
            
            // 2. Export terrain for Zone 21
            window.ExportTerrain(21);
            
            // 3. Place objects in scene for Zone 21
            window.PlaceObjectsInScene(21);
            
            Debug.Log("[AUTO-EXPORT] Auto Export and Placement Completed Successfully!");
        }

        [MenuItem("Entropy Online/Auto Export All Terrains (Bulk)", false, 151)]
        public static void RunBulk()
        {
            Debug.Log("[AUTO-EXPORT] Starting Bulk Auto Export...");
            
            // 1. Create instance of exporter window
            var window = ScriptableObject.CreateInstance<KOTerrainExporterWindow>();
            
            // 2. Run bulk export for all zones
            window.ExportAllTerrains();
            
            Debug.Log("[AUTO-EXPORT] Bulk Auto Export Completed Successfully!");
        }
    }
}
