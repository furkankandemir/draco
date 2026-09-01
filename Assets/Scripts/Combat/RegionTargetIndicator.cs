using UnityEngine;
using EntropyOnline.Core;

namespace EntropyOnline.Combat
{
    /// <summary>
    /// Region Targeting ground indicator — AoE skill yer seçimi sırasında
    /// zeminde hedefleme dairesi gösterir.
    ///
    /// Open-KO'da mouse cursor'ın altında "zone pointer" dairesi gösterilir (CFX_Zone).
    /// Mobilde: Dokunma noktasının altında daire — parmak basılı tutulursa takip eder,
    /// bırakılınca o pozisyon onaylanır.
    ///
    /// Kullanım: GameManager veya TargetSystem'a attach et.
    /// KOMagicSkillManager.OnRegionTargetingStart/OnRegionTargetingCancel'e subscribe olur.
    /// </summary>
    public class RegionTargetIndicator : MonoBehaviour
    {
        public static RegionTargetIndicator Instance { get; private set; }
        public bool ManualPositionOverride { get; set; }

        private GameObject _indicator;
        private float _radius = 3f;
        private bool _active;
        private global::UnityEngine.Camera _mainCam;
        private bool _isCustomPrefab;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _mainCam = global::UnityEngine.Camera.main;

            var magicMgr = KOMagicSkillManager.Instance;
            if (magicMgr != null)
            {
                magicMgr.OnRegionTargetingStart += OnRegionStart;
                magicMgr.OnRegionTargetingCancel += OnRegionCancel;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            var magicMgr = KOMagicSkillManager.Instance;
            if (magicMgr != null)
            {
                magicMgr.OnRegionTargetingStart -= OnRegionStart;
                magicMgr.OnRegionTargetingCancel -= OnRegionCancel;
            }

            if (_indicator != null)
                Destroy(_indicator);
        }

        private void OnRegionStart(float radius, string skillName)
        {
            _radius = Mathf.Max(radius, 1f);
            _active = true;

            if (_indicator == null)
                CreateIndicator();

            // Reset color to default in case it was changed during last aim cancel
            if (!_isCustomPrefab)
            {
                SetIndicatorColor(new Color(1f, 0.3f, 0.3f, 0.35f));
            }
            _indicator.SetActive(true);

            // Başlangıç pozisyonu = oyuncunun önü
            var pc = EntropyOnline.Character.PlayerController.Instance;
            if (pc != null)
            {
                Vector3 startPos = pc.transform.position + pc.transform.forward * 5f;
                UpdateMesh(startPos, _radius, Vector3.up);
            }
        }

        private void OnRegionCancel()
        {
            _active = false;
            ManualPositionOverride = false;
            if (_indicator != null)
                _indicator.SetActive(false);
        }

        public void SetIndicatorColor(Color color)
        {
            if (_indicator != null && !_isCustomPrefab)
            {
                var mr = _indicator.GetComponent<MeshRenderer>();
                if (mr != null && mr.material != null)
                {
                    mr.material.color = color;
                }
            }
        }

        public void UpdateIndicatorPosition(Vector3 position)
        {
            UpdateMesh(position, _radius);
        }

        private float GetTerrainHeightAt(float x, float z)
        {
            var wb = World.WorldBuilder.Instance;
            if (wb != null)
            {
                float h = wb.GetTerrainHeight(x, z);
                if (h > -900f) return h;
            }

            // Raycast fallback if WorldBuilder is not loaded
            Vector3 rayStart = new Vector3(x, 100f, z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f))
            {
                return hit.point.y;
            }
            return 0f;
        }

        private void UpdateMesh(Vector3 centerPos, float radius, Vector3 normal = default)
        {
            if (_indicator == null) return;

            var filter = _indicator.GetComponent<MeshFilter>();
            if (filter == null || filter.mesh == null) return;

            var mesh = filter.mesh;
            int segments = 32;
            int vertCount = segments + 1;
            var verts = new Vector3[vertCount];

            // Snap center to terrain height
            float centerY_proc = GetTerrainHeightAt(centerPos.x, centerPos.z);
            verts[0] = new Vector3(centerPos.x, centerY_proc + 0.25f, centerPos.z);

            // Snap outer vertices to terrain heights
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = centerPos.x + Mathf.Cos(angle) * radius;
                float z = centerPos.z + Mathf.Sin(angle) * radius;
                float y = GetTerrainHeightAt(x, z);

                verts[i + 1] = new Vector3(x, y + 0.25f, z);
            }

            mesh.vertices = verts;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        private void Update()
        {
            if (!_active || _indicator == null || _mainCam == null) return;
            if (ManualPositionOverride) return; // Skip raycast if we are aiming with joystick

            // Dokunma/mouse pozisyonunu takip et
            Vector2 screenPos = Vector2.zero;
            bool hasInput = false;

            // Editor
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                screenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                hasInput = true;
            }

            // Mobil — aktif dokunuş
            if (UnityEngine.InputSystem.Touchscreen.current != null)
            {
                var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
                if (touches.Count > 0)
                {
                    screenPos = touches[0].screenPosition;
                    hasInput = true;
                }
            }

            if (!hasInput) return;

            // Ekran pozisyonundan zemine raycast
            Ray ray = _mainCam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                UpdateMesh(hit.point, _radius, hit.normal);
            }
        }

        /// <summary>
        /// Prosedürel olarak basit bir daire mesh oluşturur ve varsa custom prefab'ın materyalini üzerine giydirir.
        /// </summary>
        private void CreateIndicator()
        {
            _indicator = new GameObject("RegionTargetIndicator");
            _indicator.transform.position = Vector3.zero;
            _indicator.transform.rotation = Quaternion.identity;
            _indicator.transform.localScale = Vector3.one;

            var meshFilter = _indicator.AddComponent<MeshFilter>();
            var meshRenderer = _indicator.AddComponent<MeshRenderer>();

            // Custom prefab'dan materyal çekmeyi dene
            GameObject customPrefab = Resources.Load<GameObject>("FXOverride/region_target_indicator");
            Material customMat = null;
            if (customPrefab != null)
            {
                var r = customPrefab.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    customMat = r.sharedMaterial;
                }
            }

            if (customMat != null)
            {
                meshRenderer.material = customMat;
                _isCustomPrefab = true;
            }
            else
            {
                // Fallback — yarı saydam kırmızı
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(1f, 0.3f, 0.3f, 0.35f);
                meshRenderer.material = mat;
                _isCustomPrefab = false;
            }

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            // Daire mesh — disk (32 segment)
            meshFilter.mesh = CreateCircleMesh(32);
        }

        private Mesh CreateCircleMesh(int segments)
        {
            var mesh = new Mesh();
            mesh.name = "CircleIndicator";
            mesh.MarkDynamic();

            int vertCount = segments + 1; // merkez + çevre
            var verts = new Vector3[vertCount];
            var tris = new int[segments * 3];
            var uvs = new Vector2[vertCount];

            // Merkez vertex
            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            // Çevre vertices
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * 0.5f; // Initial skeleton layout
                float z = Mathf.Sin(angle) * 0.5f;

                verts[i + 1] = new Vector3(x, 0, z);
                uvs[i + 1] = new Vector2(x + 0.5f, z + 0.5f);
            }

            // Üçgenler (fan)
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % segments + 1;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
