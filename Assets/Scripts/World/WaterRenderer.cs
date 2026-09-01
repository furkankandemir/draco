using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using EntropyOnline.Import;

namespace EntropyOnline.World
{
    /// <summary>
    /// CN3River + CN3Pond birebir Unity portu.
    /// 
    /// GTD'den parse edilen River/Pond mesh verilerini Unity Mesh'lere dönüştürür.
    /// Caustic texture animasyonu (32 frame @ 15 FPS) ve UV scroll uygular.
    /// </summary>
    [ExecuteAlways]
    public class WaterRenderer : MonoBehaviour
    {
        // Caustic texture animasyon sabitleri (C++ MAX_RIVER_TEX = MAX_POND_TEX = 32)
        private const int MAX_CAUSTIC_TEX = 32;
        private const float CAUSTIC_FPS = 15.0f;

        // River UV scroll hızı (CN3River::Tick — vDelta = 0.01f * deltaTime)
        private const float RIVER_UV_SPEED = 0.01f;

        private Texture2D[] _causticTextures;
        private float _texIndex = 0f;
        private float _riverUVOffset = 0f;

        // River mesh'ler
        private List<MeshFilter> _riverMeshFilters = new List<MeshFilter>();
        private List<MeshRenderer> _riverRenderers = new List<MeshRenderer>();

        // Pond mesh'ler
        private List<MeshFilter> _pondMeshFilters = new List<MeshFilter>();
        private List<MeshRenderer> _pondRenderers = new List<MeshRenderer>();

#if UNITY_EDITOR
        private double _lastEditorTime = 0;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                _lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
                UnityEditor.EditorApplication.update += EditorUpdate;
            }
        }

        private void OnDisable()
        {
            UnityEditor.EditorApplication.update -= EditorUpdate;
        }

        private void EditorUpdate()
        {
            if (Application.isPlaying) return;

            double time = UnityEditor.EditorApplication.timeSinceStartup;
            float dt = (float)(time - _lastEditorTime);
            _lastEditorTime = time;

            // Limit huge dt (e.g. if editor was suspended/paused)
            if (dt > 0.1f) dt = 0.1f;

            UpdateWater(dt);
        }
#endif

        public void Initialize(GtdTerrainImporter.GtdData gtd, short zoneId = -1)
        {
            if (gtd == null) return;

            // Caustic texture'ları yükle (misc/river/caust00.dxt - caust31.dxt)
            LoadCausticTextures();

            // River mesh'leri oluştur
            for (int i = 0; i < gtd.Rivers.Count; i++)
                CreateRiverMesh(gtd.Rivers[i], zoneId, i);

            // Pond mesh'leri oluştur
            for (int i = 0; i < gtd.Ponds.Count; i++)
                CreatePondMesh(gtd.Ponds[i], zoneId, i);

        }

        public void InitializeFromConverted(KOZoneAsset za)
        {
            LoadCausticTextures();

            foreach (Transform child in transform)
            {
                var mf = child.GetComponent<MeshFilter>();
                var mr = child.GetComponent<MeshRenderer>();
                if (mf == null || mr == null) continue;

                if (child.name.StartsWith("River"))
                {
                    _riverMeshFilters.Add(mf);
                    _riverRenderers.Add(mr);
                }
                else if (child.name.StartsWith("Pond"))
                {
                    _pondMeshFilters.Add(mf);
                    _pondRenderers.Add(mr);
                }
            }

        }

        private void LoadCausticTextures()
        {
            _causticTextures = new Texture2D[MAX_CAUSTIC_TEX];
            for (int i = 0; i < MAX_CAUSTIC_TEX; i++)
            {
                string caustPath = $"misc/river/caust{i:D2}.dxt";
                _causticTextures[i] = KOTextureProvider.Load(caustPath, flipY: false);
            }
        }

        private void CreateRiverMesh(GtdTerrainImporter.RiverMeshData riverData, short zoneId, int index)
        {
            if (riverData.VertexCount <= 0 || riverData.Vertices == null) return;

            var go = new GameObject($"River_{index}");
            go.transform.SetParent(transform, false);

            var mesh = new Mesh();
            mesh.name = $"River_{index}";

            int vc = riverData.VertexCount;
            var positions = new Vector3[vc];
            var normals = new Vector3[vc];
            var colors = new Color32[vc];
            var uvs = new Vector2[vc];
            var uvs2 = new Vector2[vc];

            for (int i = 0; i < vc; i++)
            {
                var v = riverData.Vertices[i];
                positions[i] = new Vector3(v.x, v.y, v.z);
                normals[i] = new Vector3(v.nx, v.ny, v.nz);
                colors[i] = new Color32(
                    (byte)((v.diffuse >> 16) & 0xFF), // R
                    (byte)((v.diffuse >> 8) & 0xFF),  // G
                    (byte)(v.diffuse & 0xFF),          // B
                    (byte)((v.diffuse >> 24) & 0xFF)); // A
                uvs[i] = new Vector2(v.u, v.v);
                uvs2[i] = new Vector2(v.u2, v.v2);
            }

            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.colors32 = colors;
            mesh.uv = uvs;
            mesh.uv2 = uvs2;

            int segmentCount = vc / 4;
            var indices = new int[segmentCount * 18];
            int[] wIndex = { 4, 0, 1, 4, 1, 5, 5, 1, 2, 5, 2, 6, 6, 2, 3, 6, 3, 7 };
            for (int s = 0; s < segmentCount - 1; s++)
            {
                for (int j = 0; j < 18; j++)
                    indices[s * 18 + j] = wIndex[j] + s * 4;
            }

            mesh.triangles = indices;
            mesh.RecalculateBounds();

#if UNITY_EDITOR
            mesh = SaveMeshAsset(mesh, zoneId, "River", index);
#endif

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            var mat = CreateWaterMaterial(riverData.TextureName, true);
#if UNITY_EDITOR
            mat = SaveMaterialAsset(mat, zoneId, "River", index);
#endif
            mr.sharedMaterial = mat;

            _riverMeshFilters.Add(mf);
            _riverRenderers.Add(mr);
        }

        private void CreatePondMesh(GtdTerrainImporter.PondMeshData pondData, short zoneId, int index)
        {
            if (pondData.VertexCount <= 0 || pondData.Vertices == null) return;

            var go = new GameObject($"Pond_{index}");
            go.transform.SetParent(transform, false);

            var mesh = new Mesh();
            mesh.name = $"Pond_{index}";

            int vc = pondData.VertexCount;
            var positions = new Vector3[vc];
            var normals = new Vector3[vc];
            var colors = new Color32[vc];
            var uvs = new Vector2[vc];
            var uvs2 = new Vector2[vc];

            for (int i = 0; i < vc; i++)
            {
                var v = pondData.Vertices[i];
                positions[i] = new Vector3(v.x, v.y, v.z);
                normals[i] = new Vector3(v.nx, v.ny, v.nz);
                colors[i] = new Color32(
                    (byte)((v.diffuse >> 16) & 0xFF),
                    (byte)((v.diffuse >> 8) & 0xFF),
                    (byte)(v.diffuse & 0xFF),
                    (byte)((v.diffuse >> 24) & 0xFF));
                uvs[i] = new Vector2(v.u, v.v);
                uvs2[i] = new Vector2(v.u2, v.v2);
            }

            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.colors32 = colors;
            mesh.uv = uvs;
            mesh.uv2 = uvs2;

            int iWidth = pondData.WidthVertex;
            int iHeight = pondData.HeightVertex;
            var indices = new List<int>();

            for (int j = 0; j < iHeight - 1; j++)
            {
                for (int k = 0; k < iWidth - 1; k++)
                {
                    int x = j * iWidth + k;
                    int y = (j + 1) * iWidth + k;

                    indices.Add(x);
                    indices.Add(x + 1);
                    indices.Add(y);
                    indices.Add(y);
                    indices.Add(x + 1);
                    indices.Add(y + 1);
                }
            }

            mesh.triangles = indices.ToArray();
            mesh.RecalculateBounds();

#if UNITY_EDITOR
            mesh = SaveMeshAsset(mesh, zoneId, "Pond", index);
#endif

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            var mat = CreateWaterMaterial(pondData.TextureName, false);
#if UNITY_EDITOR
            mat = SaveMaterialAsset(mat, zoneId, "Pond", index);
#endif
            mr.sharedMaterial = mat;

            _pondMeshFilters.Add(mf);
            _pondRenderers.Add(mr);
        }

        private Material CreateWaterMaterial(string waveTex, bool isRiver)
        {
            var shader = Shader.Find("KO/Water");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            var mat = new Material(shader);

            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0.0f);
            
            if (mat.HasProperty("_ZTest"))
                mat.SetFloat("_ZTest", 4.0f); // LEqual

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.4f, 0.6f, 0.8f, 0.55f));

            Texture2D causticTex = (_causticTextures != null && _causticTextures.Length > 0) ? _causticTextures[0] : null;
            if (causticTex != null)
            {
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", causticTex);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", causticTex);
                if (mat.HasProperty("_Base_Map")) mat.SetTexture("_Base_Map", causticTex);
            }

            string waveName = waveTex;
            if (string.IsNullOrEmpty(waveName))
                waveName = "ka_water.dxt";

            string waveFullPath = $"misc/river/{waveName}";
            var tex = KOTextureProvider.Load(waveFullPath, flipY: false);
            if (tex == null && waveName != "ka_water.dxt")
            {
                tex = KOTextureProvider.Load("misc/river/ka_water.dxt", flipY: false);
            }

            if (tex != null)
            {
                if (mat.HasProperty("_WaveTex"))
                    mat.SetTexture("_WaveTex", tex);
                if (mat.HasProperty("_DetailAlbedoMap"))
                    mat.SetTexture("_DetailAlbedoMap", tex);
            }

            return mat;
        }

#if UNITY_EDITOR
        private Mesh SaveMeshAsset(Mesh mesh, short zoneId, string type, int index)
        {
            if (Application.isPlaying || zoneId < 0) return mesh;

            string folderPath = "Assets/Resources/TerrainAssets/Water";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
                UnityEditor.AssetDatabase.Refresh();
            }

            string assetName = $"Zone_{zoneId}_{type}_{index}";
            string assetPath = $"{folderPath}/{assetName}.asset";
            mesh.name = assetName;
            
            Mesh existingMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existingMesh != null)
            {
                existingMesh.Clear();
                UnityEditor.EditorUtility.CopySerialized(mesh, existingMesh);
                UnityEditor.AssetDatabase.SaveAssets();
                DestroyImmediate(mesh);
                return existingMesh;
            }
            else
            {
                UnityEditor.AssetDatabase.CreateAsset(mesh, assetPath);
                UnityEditor.AssetDatabase.SaveAssets();
                return mesh;
            }
        }

        private Material SaveMaterialAsset(Material mat, short zoneId, string type, int index)
        {
            if (Application.isPlaying || zoneId < 0) return mat;

            string folderPath = "Assets/Resources/TerrainAssets/Water";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
                UnityEditor.AssetDatabase.Refresh();
            }

            string assetName = $"Zone_{zoneId}_{type}_{index}_Mat";
            string assetPath = $"{folderPath}/{assetName}.mat";
            mat.name = assetName;
            
            Material existingMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existingMat != null)
            {
                existingMat.shader = mat.shader; // Force update shader reference!
                UnityEditor.EditorUtility.CopySerialized(mat, existingMat);
                UnityEditor.AssetDatabase.SaveAssets();
                DestroyImmediate(mat);
                return existingMat;
            }
            else
            {
                UnityEditor.AssetDatabase.CreateAsset(mat, assetPath);
                UnityEditor.AssetDatabase.SaveAssets();
                return mat;
            }
        }
#endif

        private void Update()
        {
            if (Application.isPlaying)
            {
                UpdateWater(Time.deltaTime);
            }
        }

        private void UpdateWater(float deltaTime)
        {
            if (_causticTextures == null || _causticTextures.Length == 0) return;

            _texIndex += deltaTime * CAUSTIC_FPS;
            if (_texIndex >= _causticTextures.Length)
                _texIndex = 0f;

            int texIdx = Mathf.Clamp((int)_texIndex, 0, _causticTextures.Length - 1);
            var causticTex = _causticTextures[texIdx];
            if (causticTex == null) return;

            // Apply caustic frames (Stage 0)
            foreach (var mr in _riverRenderers)
            {
                if (mr != null)
                {
                    var mat = mr.sharedMaterial;
                    if (mat != null)
                    {
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", causticTex);
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", causticTex);
                        if (mat.HasProperty("_Base_Map")) mat.SetTexture("_Base_Map", causticTex);
                    }
                }
            }

            foreach (var mr in _pondRenderers)
            {
                if (mr != null)
                {
                    var mat = mr.sharedMaterial;
                    if (mat != null)
                    {
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", causticTex);
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", causticTex);
                        if (mat.HasProperty("_Base_Map")) mat.SetTexture("_Base_Map", causticTex);
                    }
                }
            }

            // Scroll UV (Stage 1 / 0) on shader side
            _riverUVOffset += RIVER_UV_SPEED * deltaTime;
            if (_riverUVOffset >= 1f) _riverUVOffset -= 1f;

            var uvOffsetVector = new Vector4(0f, _riverUVOffset, 0f, 0f);

            foreach (var mr in _riverRenderers)
            {
                if (mr != null)
                {
                    var mat = mr.sharedMaterial;
                    if (mat != null && mat.HasProperty("_UVOffset"))
                    {
                        mat.SetVector("_UVOffset", uvOffsetVector);
                    }
                }
            }
        }
    }
}
