using UnityEngine;
using UnityEditor;

namespace EntropyOnline.Editor
{
    public class FencePlacerWindow : EditorWindow
    {
        public enum AlignmentAxis { X_Axis, Z_Axis }
        public enum PlacementMode { SelectedObjects, SceneClick }

        private GameObject fencePrefab;
        private AlignmentAxis alignmentAxis = AlignmentAxis.Z_Axis;
        private PlacementMode placementMode = PlacementMode.SceneClick;
        
        // Spacing settings
        private bool autoCalculateSpacing = true;
        private float manualSpacing = 2.0f;
        private float spacingOffset = 0.0f; // Offset to overlap or gap

        // Auto-align settings
        private bool snapToTerrain = true;
        private bool tiltWithSlope = true;
        private LayerMask terrainLayer = ~0;

        // Container setting
        private string parentContainerName = "Fences_Container";

        // SceneClick points
        private Vector3 startPoint;
        private Vector3 endPoint;
        private bool hasStartPoint = false;
        private bool hasEndPoint = false;
        private bool isSelectingStart = false;
        private bool isSelectingEnd = false;

        [MenuItem("Entropy Online/Fence & Wall Placer", false, 32)]
        public static void ShowWindow()
        {
            GetWindow<FencePlacerWindow>("Fence Placer");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            // Title
            GUILayout.Space(10);
            GUILayout.Label("Fence & Wall Placer Tool", new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            });
            GUILayout.Space(10);

            // Prefab selection
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Asset Settings", EditorStyles.boldLabel);
            fencePrefab = (GameObject)EditorGUILayout.ObjectField("Fence Prefab", fencePrefab, typeof(GameObject), false);
            alignmentAxis = (AlignmentAxis)EditorGUILayout.EnumPopup("Alignment Axis", alignmentAxis);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Spacing Settings
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Spacing & Positioning", EditorStyles.boldLabel);
            autoCalculateSpacing = EditorGUILayout.Toggle("Auto Calculate Spacing", autoCalculateSpacing);
            if (!autoCalculateSpacing)
            {
                manualSpacing = EditorGUILayout.FloatField("Manual Spacing (m)", manualSpacing);
            }
            else
            {
                spacingOffset = EditorGUILayout.Slider("Overlap / Gap Offset", spacingOffset, -2.0f, 2.0f);
            }
            
            snapToTerrain = EditorGUILayout.Toggle("Snap to Terrain/Ground", snapToTerrain);
            if (snapToTerrain)
            {
                tiltWithSlope = EditorGUILayout.Toggle("Tilt with Slope", tiltWithSlope);
            }
            parentContainerName = EditorGUILayout.TextField("Parent Container", parentContainerName);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Placement Mode Selection
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Placement Mode", EditorStyles.boldLabel);
            placementMode = (PlacementMode)EditorGUILayout.EnumPopup("Mode", placementMode);
            
            if (placementMode == PlacementMode.SelectedObjects)
            {
                EditorGUILayout.HelpBox("Select two GameObjects in the hierarchy representing the Start and End points.", MessageType.Info);
                if (GUILayout.Button("Place Fence Between Selection", GUILayout.Height(35)))
                {
                    PlaceBetweenSelection();
                }
            }
            else // SceneClick
            {
                EditorGUILayout.HelpBox("Use the buttons below to pick Start and End points in the Scene View by clicking on any surface.", MessageType.Info);
                
                EditorGUILayout.BeginHorizontal();
                
                GUI.backgroundColor = isSelectingStart ? Color.yellow : (hasStartPoint ? Color.green : Color.white);
                if (GUILayout.Button(isSelectingStart ? "Click in Scene..." : (hasStartPoint ? "Start Point Set ✔" : "Set Start Point")))
                {
                    isSelectingStart = true;
                    isSelectingEnd = false;
                }
                
                GUI.backgroundColor = isSelectingEnd ? Color.yellow : (hasEndPoint ? Color.green : Color.white);
                if (GUILayout.Button(isSelectingEnd ? "Click in Scene..." : (hasEndPoint ? "End Point Set ✔" : "Set End Point")))
                {
                    isSelectingEnd = true;
                    isSelectingStart = false;
                }
                EditorGUILayout.EndHorizontal();
                
                GUI.backgroundColor = Color.white;

                if (hasStartPoint)
                {
                    EditorGUILayout.LabelField("Start Point:", startPoint.ToString());
                }
                if (hasEndPoint)
                {
                    EditorGUILayout.LabelField("End Point:", endPoint.ToString());
                }

                if (hasStartPoint && hasEndPoint)
                {
                    GUILayout.Space(10);
                    GUI.backgroundColor = Color.cyan;
                    if (GUILayout.Button("Place Fence", GUILayout.Height(40)))
                    {
                        PlaceFence(startPoint, endPoint);
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!isSelectingStart && !isSelectingEnd)
            {
                // Draw preview line if we have both points
                if (hasStartPoint && hasEndPoint && placementMode == PlacementMode.SceneClick)
                {
                    Handles.color = Color.cyan;
                    Handles.DrawDottedLine(startPoint, endPoint, 4f);
                    Handles.DrawWireDisc(startPoint, Vector3.up, 0.5f);
                    Handles.DrawWireDisc(endPoint, Vector3.up, 0.5f);
                }
                return;
            }

            // Prevent default selection box in Scene View
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(hit.point, hit.normal, 0.5f);
                sceneView.Repaint();

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (isSelectingStart)
                    {
                        startPoint = hit.point;
                        hasStartPoint = true;
                        isSelectingStart = false;
                    }
                    else if (isSelectingEnd)
                    {
                        endPoint = hit.point;
                        hasEndPoint = true;
                        isSelectingEnd = false;
                    }
                    e.Use();
                }
            }
        }

        private void PlaceBetweenSelection()
        {
            if (Selection.gameObjects.Length != 2)
            {
                EditorUtility.DisplayDialog("Error", "Please select exactly two GameObjects in the Hierarchy.", "OK");
                return;
            }

            Vector3 pA = Selection.gameObjects[0].transform.position;
            Vector3 pB = Selection.gameObjects[1].transform.position;
            PlaceFence(pA, pB);
        }

        private void PlaceFence(Vector3 pA, Vector3 pB)
        {
            if (fencePrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Fence Prefab first.", "OK");
                return;
            }

            // Create parent container if not exists
            GameObject parent = GameObject.Find(parentContainerName);
            if (parent == null)
            {
                parent = new GameObject(parentContainerName);
                Undo.RegisterCreatedObjectUndo(parent, "Create Parent Container");
            }

            // Calculate Spacing
            float spacing = manualSpacing;
            if (autoCalculateSpacing)
            {
                spacing = CalculatePrefabLength();
                spacing += spacingOffset;
            }

            if (spacing <= 0.01f) spacing = 1.0f;

            // Placement calculations
            float distance = Vector3.Distance(pA, pB);
            int count = Mathf.FloorToInt(distance / spacing);

            if (count == 0)
            {
                EditorUtility.DisplayDialog("Warning", "The distance is smaller than the spacing of a single fence segment.", "OK");
                return;
            }

            Vector3 direction = (pB - pA).normalized;

            // Start placing
            for (int i = 0; i < count; i++)
            {
                float segmentStartDist = i * spacing;
                float segmentEndDist = (i + 1) * spacing;
                
                Vector3 localStart = pA + direction * segmentStartDist;
                Vector3 localEnd = pA + direction * segmentEndDist;

                if (snapToTerrain)
                {
                    localStart = SnapPointToTerrain(localStart);
                    localEnd = SnapPointToTerrain(localEnd);
                }

                // Middle point where segment is placed
                Vector3 midPoint = (localStart + localEnd) * 0.5f;

                GameObject segment = (GameObject)PrefabUtility.InstantiatePrefab(fencePrefab);
                if (segment != null)
                {
                    segment.transform.position = midPoint;

                    // Align rotation
                    Vector3 segmentDir = (localEnd - localStart).normalized;
                    if (!tiltWithSlope)
                    {
                        segmentDir.y = 0;
                        segmentDir.Normalize();
                    }

                    Quaternion rot;
                    if (alignmentAxis == AlignmentAxis.Z_Axis)
                    {
                        rot = Quaternion.LookRotation(segmentDir);
                    }
                    else
                    {
                        rot = Quaternion.LookRotation(segmentDir) * Quaternion.Euler(0, -90, 0);
                    }

                    segment.transform.rotation = rot;
                    segment.transform.SetParent(parent.transform);

                    Undo.RegisterCreatedObjectUndo(segment, "Place Fence Segment");
                }
            }

            Debug.Log($"Placed {count} fence segments successfully.");
        }

        private float CalculatePrefabLength()
        {
            MeshFilter[] meshFilters = fencePrefab.GetComponentsInChildren<MeshFilter>();
            if (meshFilters.Length == 0) return 2.0f;

            Bounds bounds = new Bounds();
            bool initialized = false;

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                {
                    if (!initialized)
                    {
                        bounds = mf.sharedMesh.bounds;
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(mf.sharedMesh.bounds);
                    }
                }
            }

            if (!initialized) return 2.0f;

            Vector3 size = bounds.size;
            Vector3 scale = fencePrefab.transform.localScale;

            return (alignmentAxis == AlignmentAxis.X_Axis) ? size.x * scale.x : size.z * scale.z;
        }

        private Vector3 SnapPointToTerrain(Vector3 point)
        {
            RaycastHit hit;
            // Raycast down from above the point
            if (Physics.Raycast(new Ray(point + Vector3.up * 50f, Vector3.down), out hit, 100f, terrainLayer))
            {
                return hit.point;
            }
            return point;
        }
    }
}
