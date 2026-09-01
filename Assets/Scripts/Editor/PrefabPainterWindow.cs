using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace EntropyOnline.Editor
{
    public class PrefabPainterWindow : EditorWindow
    {
        private GameObject prefabToPaint;
        private float brushRadius = 5f;
        private float brushDensity = 3f; // Spawn count per click/drag step
        private float minScale = 0.8f;
        private float maxScale = 1.2f;
        private bool randomYRotation = true;
        private bool alignToNormal = false;
        private string parentName = "Painted_Grass";

        private bool isPainting = false;
        private GameObject parentContainer;

        [MenuItem("Entropy Online/Prefab Painter Tool", false, 31)]
        public static void ShowWindow()
        {
            GetWindow<PrefabPainterWindow>("Prefab Painter");
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
            GUILayout.Label("Prefab Painter Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            prefabToPaint = (GameObject)EditorGUILayout.ObjectField("Prefab to Paint", prefabToPaint, typeof(GameObject), false);
            parentName = EditorGUILayout.TextField("Parent Container Name", parentName);
            
            EditorGUILayout.Space();
            GUILayout.Label("Brush Settings", EditorStyles.boldLabel);
            brushRadius = EditorGUILayout.Slider("Brush Radius", brushRadius, 1f, 50f);
            brushDensity = EditorGUILayout.Slider("Brush Density", brushDensity, 1f, 20f);
            
            EditorGUILayout.Space();
            GUILayout.Label("Randomization Settings", EditorStyles.boldLabel);
            minScale = EditorGUILayout.Slider("Min Scale", minScale, 0.1f, 5f);
            maxScale = EditorGUILayout.Slider("Max Scale", maxScale, 0.1f, 5f);
            randomYRotation = EditorGUILayout.Toggle("Random Y Rotation", randomYRotation);
            alignToNormal = EditorGUILayout.Toggle("Align to Normal", alignToNormal);

            EditorGUILayout.Space();

            GUI.backgroundColor = isPainting ? Color.green : Color.red;
            if (GUILayout.Button(isPainting ? "PAINTING ACTIVE (Click in Scene to Paint, Hold Shift to Erase)" : "PAINTING INACTIVE (Click to Activate)", GUILayout.Height(40)))
            {
                isPainting = !isPainting;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("How to use:\n1. Select a Prefab (e.g. Grass or Flower).\n2. Click 'Activate Painting'.\n3. Hover mouse in Scene View.\n4. Left Click & Drag to paint.\n5. Hold Shift + Click/Drag to erase painted prefabs.\n6. Toggle painting off when done.", MessageType.Info);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!isPainting || prefabToPaint == null) return;

            // Prevent selecting objects while painting
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // Draw brush preview
                Handles.color = e.shift ? Color.red : Color.green;
                Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);
                Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.1f);
                Handles.DrawSolidDisc(hit.point, hit.normal, brushRadius);
                
                // Redraw scene view to show the handle smoothly
                sceneView.Repaint();

                // Paint or Erase on click or drag
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    if (e.shift)
                    {
                        ErasePrefabs(hit.point);
                    }
                    else
                    {
                        PaintPrefabs(hit.point, hit.normal);
                    }
                    e.Use();
                }
            }
        }

        private void PaintPrefabs(Vector3 center, Vector3 normal)
        {
            if (parentContainer == null || parentContainer.name != parentName)
            {
                parentContainer = GameObject.Find(parentName);
                if (parentContainer == null)
                {
                    parentContainer = new GameObject(parentName);
                    Undo.RegisterCreatedObjectUndo(parentContainer, "Create Parent Container");
                }
            }

            int spawnCount = Mathf.RoundToInt(brushDensity);
            for (int i = 0; i < spawnCount; i++)
            {
                // Generate random position within circle
                Vector2 randomPoint = Random.insideUnitCircle * brushRadius;
                Vector3 spawnPos = center + new Vector3(randomPoint.x, 0f, randomPoint.y);

                // Align to terrain height if raycast hits
                Ray ray = new Ray(spawnPos + Vector3.up * 50f, Vector3.down);
                RaycastHit hit;
                Vector3 finalPos = spawnPos;
                Vector3 spawnNormal = normal;

                if (Physics.Raycast(ray, out hit, 100f))
                {
                    finalPos = hit.point;
                    spawnNormal = hit.normal;
                }

                // Instantiate prefab
                GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefabToPaint);
                if (newObj != null)
                {
                    newObj.transform.position = finalPos;
                    
                    // Rotation
                    if (alignToNormal)
                    {
                        newObj.transform.rotation = Quaternion.FromToRotation(Vector3.up, spawnNormal);
                        if (randomYRotation)
                        {
                            newObj.transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.Self);
                        }
                    }
                    else if (randomYRotation)
                    {
                        newObj.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    }

                    // Scale
                    float scaleFactor = Random.Range(minScale, maxScale);
                    newObj.transform.localScale = prefabToPaint.transform.localScale * scaleFactor;

                    // Parent
                    newObj.transform.SetParent(parentContainer.transform);

                    Undo.RegisterCreatedObjectUndo(newObj, "Paint Prefab");
                }
            }
        }

        private void ErasePrefabs(Vector3 center)
        {
            if (parentContainer == null)
            {
                parentContainer = GameObject.Find(parentName);
                if (parentContainer == null) return;
            }

            // Find all children under the parent container
            List<Transform> children = new List<Transform>();
            foreach (Transform child in parentContainer.transform)
            {
                children.Add(child);
            }

            foreach (Transform child in children)
            {
                if (Vector3.Distance(child.position, center) <= brushRadius)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }
    }
}
