using UnityEngine;
using UnityEditor;
using EntropyOnline.Import;

namespace EntropyOnline.Editor
{
    public class KOZoneViewerWindow : EditorWindow
    {
        private int _zoneId = 201;

        [MenuItem("Entropy Online/Zone Viewer Tool", false, 25)]
        public static void ShowWindow()
        {
            GetWindow<KOZoneViewerWindow>("Zone Viewer");
        }

        private void OnGUI()
        {
            GUILayout.Label("KO Zone Viewer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select a Zone ID and click 'Load Zone Preview' to build it directly in the scene view. Click 'Clear Zone Preview' to remove the preview object.", MessageType.Info);
            
            _zoneId = EditorGUILayout.IntField("Zone ID", _zoneId);

            GUILayout.Space(10);

            if (GUILayout.Button("Load Zone Preview", GUILayout.Height(30)))
            {
                LoadZonePreview(_zoneId);
            }

            if (GUILayout.Button("Clear Zone Preview", GUILayout.Height(30)))
            {
                ClearZonePreview();
            }
        }

        private void LoadZonePreview(int zoneId)
        {
            ClearZonePreview();

            string assetPath = $"Assets/Resources/KOZones/zone_{zoneId}.asset";
            KOZoneAsset za = AssetDatabase.LoadAssetAtPath<KOZoneAsset>(assetPath);

            if (za == null)
            {
                EditorUtility.DisplayDialog("Error", $"Zone asset not found at:\n{assetPath}", "OK");
                return;
            }

            GameObject root = new GameObject($"Zone_{zoneId}_Preview");
            Undo.RegisterCreatedObjectUndo(root, "Create Zone Preview");

            // 1. Load Terrain
            if (za.terrainData != null)
            {
                GameObject terrainObj = Terrain.CreateTerrainGameObject(za.terrainData);
                terrainObj.name = "Terrain";
                terrainObj.transform.SetParent(root.transform);
                terrainObj.transform.position = new Vector3(0, za.terrainBaseY, 0);
                
                // Add TerrainCollider
                var tc = terrainObj.GetComponent<TerrainCollider>();
                if (tc == null)
                {
                    tc = terrainObj.AddComponent<TerrainCollider>();
                }
                tc.terrainData = za.terrainData;
            }

            // 2. Load Shapes (meshes like walls, towers, buildings)
            if (za.shapes != null && za.shapes.Length > 0)
            {
                GameObject objectsParent = new GameObject("Objects");
                objectsParent.transform.SetParent(root.transform);

                foreach (var shape in za.shapes)
                {
                    if (shape.position.sqrMagnitude < 0.001f) continue;

                    GameObject shapeObj = new GameObject(string.IsNullOrEmpty(shape.name) ? "Shape" : shape.name);
                    shapeObj.transform.SetParent(objectsParent.transform);
                    shapeObj.transform.position = shape.position;
                    shapeObj.transform.rotation = shape.rotation;
                    shapeObj.transform.localScale = shape.scale;

                    if (shape.parts != null)
                    {
                        foreach (var part in shape.parts)
                        {
                            if (part.mesh == null) continue;

                            GameObject partObj = new GameObject(part.mesh.name);
                            partObj.transform.SetParent(shapeObj.transform);
                            partObj.transform.localPosition = part.pivot;
                            partObj.transform.localRotation = Quaternion.identity;
                            partObj.transform.localScale = Vector3.one;

                            var mf = partObj.AddComponent<MeshFilter>();
                            mf.sharedMesh = part.mesh;

                            var mr = partObj.AddComponent<MeshRenderer>();
                            mr.sharedMaterial = part.material != null 
                                ? part.material 
                                : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                            
                            // Add a MeshCollider for easy clicking/snapping in editor
                            partObj.AddComponent<MeshCollider>().sharedMesh = part.mesh;
                        }
                    }
                }
            }

            // 3. Load Water ponds
            if (za.ponds != null && za.ponds.Length > 0)
            {
                GameObject pondsParent = new GameObject("Ponds");
                pondsParent.transform.SetParent(root.transform);
                
                foreach (var pond in za.ponds)
                {
                    if (pond.mesh == null) continue;
                    GameObject pondObj = new GameObject("Pond");
                    pondObj.transform.SetParent(pondsParent.transform);
                    pondObj.transform.localPosition = Vector3.zero;
                    pondObj.transform.localRotation = Quaternion.identity;
                    pondObj.transform.localScale = Vector3.one;

                    var mf = pondObj.AddComponent<MeshFilter>();
                    mf.sharedMesh = pond.mesh;
                    
                    var mr = pondObj.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = pond.material;
                }
            }

            // Focus Scene view on the town center coordinates
            Vector3 targetFocus = new Vector3(622f, 20f, 911f); // El Morad town center
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAt(targetFocus, Quaternion.Euler(30f, 45f, 0f), 100f);
            }

            EditorUtility.DisplayDialog("Success", $"Zone {zoneId} loaded successfully!\nLook at the Scene View.", "OK");
        }

        private void ClearZonePreview()
        {
            GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            int count = 0;
            foreach (var go in allObjects)
            {
                if (go != null && go.name.StartsWith("Zone_") && go.name.EndsWith("_Preview"))
                {
                    DestroyImmediate(go);
                    count++;
                }
            }
            if (count > 0)
            {
                Debug.Log($"[ZONE VIEWER] Cleared {count} zone preview(s).");
            }
        }
    }
}
