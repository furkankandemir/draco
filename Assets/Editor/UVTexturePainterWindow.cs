using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EntropyOnline.Import;

namespace EntropyOnline.Editor
{
    public class UVTexturePainterWindow : EditorWindow
    {
        [MenuItem("Window/KO Tools/UV Texture Painter")]
        public static void ShowWindow()
        {
            var window = GetWindow<UVTexturePainterWindow>("UV Texture Painter");
            window.minSize = new Vector2(1150, 600);
            window.Show();
        }

        // Target references
        private GameObject _targetGo;
        private Renderer _targetRenderer;
        private Mesh _targetMesh;
        private Texture2D _targetTexture;

        // Auto-find info status
        private string _meshSourceStatus = "None";

        // UI options
        private bool _showUVWireframe = true;
        private Color _wireframeColor = new Color(0.2f, 1.0f, 0.2f, 0.5f);

        // GUI layout helpers
        private Vector2 _scrollPosition;
        private Rect _textureRect;

        // 3D Local Preview Utility (Offline rendering)
        private PreviewRenderUtility _previewUtility;
        private Material _previewMaterial;
        private Vector2 _previewDrag = new Vector2(180f, 0f); // Default front view facing camera
        private float _previewZoom = 4.0f;
        private string _photoshopPath = "";

        private void OnEnable()
        {
            _photoshopPath = EditorPrefs.GetString("KO_PhotoshopPath", "");
            
            // Initialize 3D Preview Utility
            _previewUtility = new PreviewRenderUtility();
            _previewUtility.camera.fieldOfView = 30f;
            _previewUtility.camera.farClipPlane = 100f;
            _previewUtility.camera.nearClipPlane = 0.1f;
            _previewUtility.camera.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);
            _previewUtility.camera.clearFlags = CameraClearFlags.Color;

            // Auto-detect from selection
            OnSelectionChange();
        }

        private void OnDisable()
        {
            // Cleanup 3D Preview Utility
            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }

            if (_previewMaterial != null)
            {
                DestroyImmediate(_previewMaterial);
                _previewMaterial = null;
            }
        }

        private void OnInspectorUpdate()
        {
            // Automatically repaint the window to show live updates when texture assets are re-imported in Unity
            Repaint();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject != null && Selection.activeGameObject != _targetGo)
            {
                _targetGo = Selection.activeGameObject;
                _targetRenderer = _targetGo.GetComponentInChildren<Renderer>();
                
                // Get Mesh
                var mf = _targetGo.GetComponentInChildren<MeshFilter>();
                if (mf != null)
                {
                    _targetMesh = mf.sharedMesh;
                    _meshSourceStatus = $"Selected GameObject ({_targetGo.name})";
                }
                else
                {
                    var smr = _targetGo.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null)
                    {
                        _targetMesh = smr.sharedMesh;
                        _meshSourceStatus = $"Selected GameObject Skinned Mesh ({_targetGo.name})";
                    }
                }

                // Get Texture
                if (_targetRenderer != null && _targetRenderer.sharedMaterial != null)
                {
                    _targetTexture = _targetRenderer.sharedMaterial.mainTexture as Texture2D;
                }
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // 1. LEFT PANEL: Controls (300px width)
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            DrawControlPanel();
            EditorGUILayout.EndVertical();

            // 2. CENTER PANEL: 2D Texture Preview (520px width)
            EditorGUILayout.BeginVertical(GUILayout.Width(520));
            DrawCanvasPanel();
            EditorGUILayout.EndVertical();

            // 3. RIGHT PANEL: 3D Preview (Flexible/Remaining area)
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            Draw3DPreviewPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawControlPanel()
        {
            GUILayout.Space(10);
            GUILayout.Label("Live Photoshop Viewer", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // Target Selection info
            EditorGUI.BeginChangeCheck();
            _targetGo = (GameObject)EditorGUILayout.ObjectField("Target Object (Optional)", _targetGo, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck() && _targetGo != null)
            {
                _targetRenderer = _targetGo.GetComponentInChildren<Renderer>();
                var mf = _targetGo.GetComponentInChildren<MeshFilter>();
                _targetMesh = mf != null ? mf.sharedMesh : null;
                if (_targetMesh == null)
                {
                    var smr = _targetGo.GetComponentInChildren<SkinnedMeshRenderer>();
                    _targetMesh = smr != null ? smr.sharedMesh : null;
                }

                if (_targetRenderer != null && _targetRenderer.sharedMaterial != null)
                {
                    _targetTexture = _targetRenderer.sharedMaterial.mainTexture as Texture2D;
                    _meshSourceStatus = $"Selected GameObject ({_targetGo.name})";
                }
            }

            EditorGUI.BeginChangeCheck();
            _targetTexture = (Texture2D)EditorGUILayout.ObjectField("Target Texture (PNG)", _targetTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck() && _targetTexture != null)
            {
                AutoFindMeshForTexture();
            }

            GUILayout.Space(5);
            // Mesh Source status info
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Mesh Source:", EditorStyles.miniBoldLabel, GUILayout.Width(80));
            GUILayout.Label(_meshSourceStatus, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (_targetTexture != null && GUILayout.Button("Auto-Find Mesh for Texture"))
            {
                AutoFindMeshForTexture();
            }

            GUILayout.Space(15);
            GUILayout.Label("Photoshop Integration Settings", EditorStyles.boldLabel);
            
            // Photoshop path selection
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _photoshopPath = EditorGUILayout.TextField("Photoshop Path", _photoshopPath);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString("KO_PhotoshopPath", _photoshopPath);
            }
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFilePanel("Select Photoshop.exe", "C:\\Program Files", "exe");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    _photoshopPath = selectedPath;
                    EditorPrefs.SetString("KO_PhotoshopPath", _photoshopPath);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_targetTexture != null)
            {
                GUILayout.Space(5);
                if (GUILayout.Button("Open Texture in Photoshop", GUILayout.Height(35)))
                {
                    string assetPath = AssetDatabase.GetAssetPath(_targetTexture);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
                        if (!string.IsNullOrEmpty(_photoshopPath) && File.Exists(_photoshopPath))
                        {
                            System.Diagnostics.Process.Start(_photoshopPath, $"\"{fullPath}\"");
                            Debug.Log($"[TexturePainter] Opened {assetPath} in Photoshop: {_photoshopPath}");
                        }
                        else
                        {
                            EditorUtility.OpenWithDefaultApp(assetPath);
                            Debug.Log($"[TexturePainter] Opened {assetPath} in default app.");
                        }
                    }
                }

                if (_targetMesh != null)
                {
                    GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
                    if (GUILayout.Button("Export UV Layout (Transparent PNG)", GUILayout.Height(35)))
                    {
                        ExportUVLayoutPNG();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    EditorGUILayout.HelpBox("Select or Auto-Find a Mesh to enable UV Layout Export.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select a texture to enable Photoshop tools.", MessageType.Info);
            }

            GUILayout.Space(15);
            GUILayout.Label("Overlay Settings", EditorStyles.boldLabel);
            _showUVWireframe = EditorGUILayout.Toggle("Show UV Wireframe", _showUVWireframe);
            _wireframeColor = EditorGUILayout.ColorField("Wireframe Color", _wireframeColor);

            GUILayout.Space(15);
            GUILayout.Label("3D Preview Settings", EditorStyles.boldLabel);
            _previewZoom = EditorGUILayout.Slider("Zoom Out / Distance", _previewZoom, 1.0f, 10.0f);

            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox("How to use:\n1. Select your target texture.\n2. Export the UV Layout.\n3. Open in Photoshop and place the UV layout as a top layer.\n4. Paint and Save in Photoshop.\n5. Alt+Tab back to Unity to see updates instantly on the 3D model on the right!", MessageType.Info);
            GUILayout.Space(10);
        }

        private void AutoFindMeshForTexture()
        {
            if (_targetTexture == null) return;

            string textureName = _targetTexture.name;

            // 1. Search Active Scene for any Renderer referencing this texture
#if UNITY_2023_1_OR_NEWER
            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
#else
            var renderers = FindObjectsOfType<Renderer>(true);
#endif
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture == _targetTexture)
                {
                    _targetGo = renderer.gameObject;
                    _targetRenderer = renderer;

                    var mf = _targetGo.GetComponent<MeshFilter>();
                    _targetMesh = mf != null ? mf.sharedMesh : null;
                    if (_targetMesh == null)
                    {
                        var smr = _targetGo.GetComponent<SkinnedMeshRenderer>();
                        _targetMesh = smr != null ? smr.sharedMesh : null;
                    }

                    if (_targetMesh != null)
                    {
                        _meshSourceStatus = $"Auto-Found in Scene ({_targetGo.name})";
                        Repaint();
                        Debug.Log($"[TexturePainter] Auto-found matching GameObject in active scene: {_targetGo.name}");
                        return;
                    }
                }
            }

            // 2. Search Knight Online binary database (.n3cpart or .n3cplug) matching the texture name
            Mesh binaryMesh = TryAutoFindMeshFromBinary(textureName);
            if (binaryMesh != null)
            {
                _targetMesh = binaryMesh;
                _meshSourceStatus = $"Auto-Loaded from Binary KO Files";
                Repaint();
                Debug.Log($"[TexturePainter] Auto-loaded corresponding Mesh from KO binaries for: {textureName}");
                return;
            }

            _meshSourceStatus = "Mesh not found (3D preview disabled)";
            _targetMesh = null;
        }

        private Mesh TryAutoFindMeshFromBinary(string textureName)
        {
            if (string.IsNullOrEmpty(textureName)) return null;

            // 1. Try to find CPart file (.n3cpart) matching texture name (armors/clothes)
            string partFN = $"Item/{textureName}.n3cpart";
            string partPath = N3CharBuilder.FindAssetFile(partFN);
            if (partPath == null)
            {
                partPath = N3CharBuilder.FindAssetFile($"{textureName}.n3cpart");
            }

            if (partPath != null)
            {
                var partData = N3CPartImporter.LoadCPart(partPath);
                if (partData != null && partData.Skins != null && partData.Skins.LODs != null)
                {
                    var skinLOD = partData.Skins.LODs[0];
                    if (skinLOD != null && skinLOD.FaceCount > 0)
                    {
                        return N3CPartImporter.CreateUnityMesh(skinLOD);
                    }
                }
            }

            // 2. Try to find CPlug file (.n3cplug) matching texture name (weapons/shields)
            string plugFN = $"Item/{textureName}.n3cplug";
            string plugPath = N3CharBuilder.FindAssetFile(plugFN);
            if (plugPath == null)
            {
                plugPath = N3CharBuilder.FindAssetFile($"{textureName}.n3cplug");
            }

            if (plugPath != null)
            {
                var plugData = N3CPlugImporter.Load(plugPath);
                if (plugData != null && !string.IsNullOrEmpty(plugData.MeshFileName))
                {
                    string meshPath = N3CharBuilder.FindAssetFile(plugData.MeshFileName);
                    if (meshPath != null)
                    {
                        var pmeshData = N3PMeshImporter.Load(meshPath);
                        if (pmeshData != null)
                        {
                            return N3PMeshImporter.CreateUnityMesh(pmeshData);
                        }
                    }
                }
            }

            return null;
        }

        private void DrawCanvasPanel()
        {
            if (_targetTexture == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Please select a Target Texture to display.", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Draw Texture area (keep 1:1 aspect ratio or fit to 512x512)
            float canvasSize = 512f;
            GUILayout.Box("", GUILayout.Width(canvasSize), GUILayout.Height(canvasSize));
            _textureRect = GUILayoutUtility.GetLastRect();

            // Render texture directly from original asset
            GUI.DrawTexture(_textureRect, _targetTexture, ScaleMode.StretchToFill);

            // Render UV Wireframe overlay
            if (_showUVWireframe && _targetMesh != null)
            {
                DrawUVWireframe(_textureRect, _targetMesh);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawUVWireframe(Rect rect, Mesh mesh)
        {
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;

            if (uvs == null || uvs.Length == 0 || triangles == null || triangles.Length == 0) return;

            // Use GL/Handles to draw lines over GUI
            Handles.BeginGUI();
            Handles.color = _wireframeColor;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (triangles[i] >= uvs.Length || triangles[i + 1] >= uvs.Length || triangles[i + 2] >= uvs.Length)
                    continue;

                Vector2 uv0 = uvs[triangles[i]];
                Vector2 uv1 = uvs[triangles[i + 1]];
                Vector2 uv2 = uvs[triangles[i + 2]];

                // Map normalized UV coordinates (0..1) to GUI rect pixels
                Vector2 p0 = new Vector2(rect.x + uv0.x * rect.width, rect.y + (1f - uv0.y) * rect.height);
                Vector2 p1 = new Vector2(rect.x + uv1.x * rect.width, rect.y + (1f - uv1.y) * rect.height);
                Vector2 p2 = new Vector2(rect.x + uv2.x * rect.width, rect.y + (1f - uv2.y) * rect.height);

                Handles.DrawLine(p0, p1);
                Handles.DrawLine(p1, p2);
                Handles.DrawLine(p2, p0);
            }

            Handles.color = Color.white;
            Handles.EndGUI();
        }

        private void Draw3DPreviewPanel()
        {
            GUILayout.Space(10);
            GUILayout.Label("3D Local Preview (Offline - Drag to Rotate)", EditorStyles.boldLabel);
            GUILayout.Space(5);

            if (_previewUtility != null && _targetMesh != null && _targetTexture != null)
            {
                // Ensure we have a preview material
                if (_previewMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    _previewMaterial = new Material(shader);
                    
                    if (_previewMaterial.HasProperty("_Smoothness")) _previewMaterial.SetFloat("_Smoothness", 0f);
                    if (_previewMaterial.HasProperty("_Glossiness")) _previewMaterial.SetFloat("_Glossiness", 0f);
                    if (_previewMaterial.HasProperty("_Metallic")) _previewMaterial.SetFloat("_Metallic", 0f);
                }

                // Feed texture directly from the original asset
                if (_previewMaterial.HasProperty("_BaseMap"))
                    _previewMaterial.SetTexture("_BaseMap", _targetTexture);
                else if (_previewMaterial.HasProperty("_Base_Map"))
                    _previewMaterial.SetTexture("_Base_Map", _targetTexture);
                else
                    _previewMaterial.mainTexture = _targetTexture;

                // Obtain rectangular area for rendering
                float pSize = position.height - 60f;
                if (pSize < 200f) pSize = 200f;
                Rect pRect = GUILayoutUtility.GetRect(pSize, pSize, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                _previewUtility.BeginPreview(pRect, GUIStyle.none);

                // Configure camera around mesh bounds
                Bounds bounds = _targetMesh.bounds;
                float size = bounds.extents.magnitude;
                if (size <= 0f) size = 1f;

                _previewUtility.camera.transform.position = bounds.center + new Vector3(0, 0, -size * _previewZoom);
                _previewUtility.camera.transform.LookAt(bounds.center);

                // Lights settings
                _previewUtility.lights[0].intensity = 1.2f;
                _previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0);
                _previewUtility.lights[1].intensity = 0.5f;
                _previewUtility.lights[1].transform.rotation = Quaternion.Euler(-40f, -40f, 0);

                // Model orientation
                Quaternion meshRotation = Quaternion.Euler(_previewDrag.y, _previewDrag.x, 0);

                // Render inside local preview scene
                _previewUtility.DrawMesh(_targetMesh, Vector3.zero, meshRotation, _previewMaterial, 0);
                _previewUtility.camera.Render();

                Texture resultRender = _previewUtility.EndPreview();
                GUI.DrawTexture(pRect, resultRender, ScaleMode.StretchToFill, false);

                // Orbit camera drag and scroll wheel zoom handling
                Event currentEvent = Event.current;
                if (pRect.Contains(currentEvent.mousePosition))
                {
                    if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
                    {
                        _previewDrag += currentEvent.delta * 0.5f;
                        _previewDrag.y = Mathf.Clamp(_previewDrag.y, -80f, 80f);
                        Repaint();
                        currentEvent.Use();
                    }
                    else if (currentEvent.type == EventType.ScrollWheel)
                    {
                        // Scroll down (positive delta) zooms out, scroll up (negative delta) zooms in
                        _previewZoom += currentEvent.delta.y * 0.15f;
                        _previewZoom = Mathf.Clamp(_previewZoom, 1.0f, 10.0f);
                        Repaint();
                        currentEvent.Use();
                    }
                }
            }
            else
            {
                GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                GUILayout.FlexibleSpace();
                GUILayout.Label("No Mesh or Texture Loaded.\nSelect an Object or Texture to load a 3D Preview.", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
            }
        }

        private void ExportUVLayoutPNG()
        {
            if (_targetTexture == null || _targetMesh == null) return;

            string texAssetPath = AssetDatabase.GetAssetPath(_targetTexture);
            if (string.IsNullOrEmpty(texAssetPath)) return;

            string directory = Path.GetDirectoryName(texAssetPath);
            string texName = Path.GetFileNameWithoutExtension(texAssetPath);
            string uvAssetPath = Path.Combine(directory, $"{texName}_UV.png");
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), uvAssetPath);

            int width = _targetTexture.width;
            int height = _targetTexture.height;

            // Create a transparent texture
            Texture2D uvTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] clearPixels = new Color[width * height];
            for (int i = 0; i < clearPixels.Length; i++)
            {
                clearPixels[i] = Color.clear;
            }
            uvTex.SetPixels(clearPixels);

            // Get UVs and triangles
            Vector2[] uvs = _targetMesh.uv;
            int[] triangles = _targetMesh.triangles;

            if (uvs != null && triangles != null && uvs.Length > 0 && triangles.Length > 0)
            {
                // Draw all triangle edges
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    if (triangles[i] >= uvs.Length || triangles[i + 1] >= uvs.Length || triangles[i + 2] >= uvs.Length)
                        continue;

                    Vector2 uv0 = uvs[triangles[i]];
                    Vector2 uv1 = uvs[triangles[i + 1]];
                    Vector2 uv2 = uvs[triangles[i + 2]];

                    Vector2 p0 = new Vector2(uv0.x * width, uv0.y * height);
                    Vector2 p1 = new Vector2(uv1.x * width, uv1.y * height);
                    Vector2 p2 = new Vector2(uv2.x * width, uv2.y * height);

                    DrawLineOnTexture(uvTex, p0, p1, _wireframeColor);
                    DrawLineOnTexture(uvTex, p1, p2, _wireframeColor);
                    DrawLineOnTexture(uvTex, p2, p0, _wireframeColor);
                }
            }

            uvTex.Apply();

            try
            {
                byte[] bytes = uvTex.EncodeToPNG();
                File.WriteAllBytes(fullPath, bytes);
                DestroyImmediate(uvTex);

                AssetDatabase.ImportAsset(uvAssetPath, ImportAssetOptions.ForceUpdate);
                
                // Show in project window
                var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(uvAssetPath);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                }

                EditorUtility.DisplayDialog("UV Export Success", $"UV Layout exported successfully as a transparent PNG!\n\nFile: {uvAssetPath}\n\nDrag this layer into Photoshop as the top layer.", "OK");
                Debug.Log($"[TexturePainter] UV Layout exported to: {uvAssetPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TexturePainter] Error exporting UV layout: {e.Message}");
            }
        }

        private void DrawLineOnTexture(Texture2D tex, Vector2 p0, Vector2 p1, Color color)
        {
            int x0 = Mathf.RoundToInt(p0.x);
            int y0 = Mathf.RoundToInt(p0.y);
            int x1 = Mathf.RoundToInt(p1.x);
            int y1 = Mathf.RoundToInt(p1.y);

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                if (x0 >= 0 && x0 < tex.width && y0 >= 0 && y0 < tex.height)
                {
                    tex.SetPixel(x0, y0, color);
                }

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }
    }
}
