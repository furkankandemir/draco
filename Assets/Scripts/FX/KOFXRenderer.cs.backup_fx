using System.Collections.Generic;
using System.IO;
using UnityEngine;
using KOImport;
using EntropyOnline.Import;

namespace KO
{
    /// <summary>
    /// Open-KO birebir: CN3FXBundle::Render + CN3FXPartBillBoard::Render
    /// Unity adaptasyonu — DirectX DrawPrimitiveUP → Unity MeshRenderer + Material
    ///
    /// Her aktif FxBundleInstance için bir root GameObject oluşturur.
    /// Her FxPartInstance için child GameObject + visual component ekler.
    ///
    /// Referans:
    ///   N3FXBundle.cpp:464-477 — Render() loop
    ///   N3FXPartBillBoard.cpp:376-658 — Billboard Render()
    ///   N3FXPartBillBoard.cpp:290-358 — Billboard Tick() (texture index, color, size)
    /// </summary>
    public class KOFXRenderer : MonoBehaviour
    {
        private Camera _mainCamera;
        private Camera GetMainCamera()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;
            return _mainCamera;
        }

        private Terrain _activeTerrain;
        private Terrain GetActiveTerrain()
        {
            if (_activeTerrain == null)
                _activeTerrain = Terrain.activeTerrain;
            return _activeTerrain;
        }

        // Bundle instance → visual root mapping
        private readonly Dictionary<FxBundleInstance, FxVisualRoot> _visuals = new();
        private readonly List<FxBundleInstance> _removeList = new();


        // N3FXShape cache — C++ s_MngFXShape birebir (N3FXPartMesh.cpp:232)
        private readonly Dictionary<string, N3ShapeParser.N3ShapeData> _shapeCache = new(System.StringComparer.OrdinalIgnoreCase);

        // Shared quad mesh for billboards (C++ m_vUnit quad: N3FXPartBillBoard.cpp:146-149)
        private Mesh _quadMesh;



        // Texture cache — C++ CN3Base::s_MngTex (aynı texture'ı tekrar yükleme)
        private readonly Dictionary<string, Texture2D> _texCache = new(System.StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            CreateSharedAssets();
        }



        /// <summary>
        /// KOFXManager.Update() sonrasında çağrılır — aktif bundle'ları render et.
        /// Open-KO birebir: N3FXBundle.cpp:464-477 — Render() loop
        /// </summary>
        public void UpdateVisuals(List<FxBundleInstance> activeBundles)
        {
            // Yeni bundle'lar için visual oluştur
            foreach (var bundle in activeBundles)
            {
                if (!_visuals.ContainsKey(bundle))
                    CreateVisual(bundle);
            }

            // Mevcut visual'ları güncelle
            _removeList.Clear();
            foreach (var kvp in _visuals)
            {
                var bundle = kvp.Key;
                var visual = kvp.Value;

                // N3FXBundle.cpp:466-467 — dead ise render etme
                if (bundle.State == FxBundleState.Dead || !activeBundles.Contains(bundle))
                {
                    if (visual.Root != null)
                    {
                        Destroy(visual.Root);
                    }
                    _removeList.Add(bundle);
                    continue;
                }

                UpdateVisual(bundle, visual);
            }

            foreach (var key in _removeList)
                _visuals.Remove(key);
        }

        private void CreateVisual(FxBundleInstance bundle)
        {
            int fxId = bundle.FxId;
            FxVisualRoot visual = null;



            // Fallback: instantiate new
            var root = new GameObject($"FX_{bundle.FxId}");
            root.transform.position = GetBundleWorldPos(bundle);
            if (bundle.Dir != Vector3.zero)
            {
                root.transform.rotation = Quaternion.LookRotation(bundle.Dir);
            }

            if (bundle.OverrideInstance != null)
            {
                bundle.OverrideInstance.transform.SetParent(root.transform, true);
                bundle.OverrideInstance.transform.localPosition = Vector3.zero;
                bundle.OverrideInstance.transform.localRotation = Quaternion.identity;
            }

            visual = new FxVisualRoot { Root = root, PartVisuals = new FxPartVisual[bundle.PartInstances.Length] };

            if (bundle.OverrideInstance == null)
            {
                for (int i = 0; i < bundle.PartInstances.Length; i++)
                {
                    var part = bundle.PartInstances[i];
                    if (part == null) continue;

                    // N3FXBundle.cpp:469-476 — her part render
                    visual.PartVisuals[i] = CreatePartVisual(root.transform, part, i, bundle);
                }
            }

            _visuals[bundle] = visual;
        }

        private FxPartVisual CreatePartVisual(Transform parent, FxPartInstance part, int index, FxBundleInstance bundle)
        {
            var partObj = new GameObject($"Part_{index}_{part.Data.Type}");
            
            // Set initial world position directly first to avoid (0,0,0) sync lag
            Vector3 absPartPos = Rotate2AbsolutePos(part.CurrPos, bundle.Dir);
            partObj.transform.position = parent.position + absPartPos;
            partObj.transform.rotation = parent.rotation;

            partObj.transform.SetParent(parent, true);

            partObj.SetActive(false);

            var visual = new FxPartVisual { Obj = partObj };

            switch (part.Data.Type)
            {
                case FxPartType.Billboard:
                    SetupBillboard(partObj, part, visual);
                    break;
                case FxPartType.Particle:
                    SetupParticleSystem(partObj, part, visual, bundle);
                    break;
                case FxPartType.BottomBoard:
                    SetupBottomBoard(partObj, part, visual);
                    break;
                case FxPartType.Mesh:
                    // Open-KO birebir: CN3FXPartMesh::Load (N3FXPartMesh.cpp:226-303)
                    // N3FXShape yükle, her shape part için ayrı child mesh oluştur
                    SetupMesh(partObj, part, visual);
                    break;
            }

            // C++ N3FXPartBase.cpp:573-580 birebir — texture yükleme
            // Format: textureName + "0000" + ".dxt" (m_pTexName + frame index)
            LoadAndApplyTexture(part, visual);

            return visual;
        }

        /// <summary>
        /// Billboard setup — C++ N3FXPartBillBoard::CreateVB (satır 140-159)
        /// Quad mesh + additive material
        /// </summary>
        private void SetupBillboard(GameObject obj, FxPartInstance part, FxPartVisual visual)
        {
            var mf = obj.AddComponent<MeshFilter>();
            mf.sharedMesh = _quadMesh;

            var mr = obj.AddComponent<MeshRenderer>();
            // C++ N3FXPartBase.cpp:38-39 — per-part srcBlend/destBlend
            mr.sharedMaterial = CreatePartMaterial(part.Data);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            visual.Renderer = mr;

            // Başlangıçta gizle — part READY state'de
            obj.SetActive(false);
        }

        /// <summary>
        /// Particle setup — C++ CN3FXPartParticles kullanarak Unity ParticleSystem
        /// </summary>
        private void SetupParticleSystem(GameObject obj, FxPartInstance part, FxPartVisual visual, FxBundleInstance bundle)
        {
            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
                       var pData = part.Data.ParticleData;
            if (pData != null)
            {
                float targetScale = bundle.Data.DependScale ? bundle.TargetScale : 1.0f;

                // C++ N3FXPartParticles — particle count, life, size
                main.maxParticles = pData.NumParticles;
                main.startLifetime = new ParticleSystem.MinMaxCurve(pData.ParticleLifeMin, pData.ParticleLifeMax);
                main.startSize = new ParticleSystem.MinMaxCurve(pData.ParticleSizeMin * targetScale, pData.ParticleSizeMax * targetScale);
                // C++ birebir: N3FXPartParticles.cpp:1017 — pParticle->m_vVelocity = vDir * m_fPtVelocity
                // C++'da hız BİR KEZ atanır, cone spread ile dağıtılır, sonra yerçekimi yavaşlatır.
                // VoL yaklaşımı ÇALIŞMAZ çünkü cone spread yok — tüm partiküller aynı yöne gider.
                // Cone shape + startSpeed = C++ emit cone + initial velocity birebir karşılığı.
                main.startSpeed = pData.PtVelocity * targetScale;
                // C++ birebir: N3FXParticle.cpp:134-135 — yerçekimi ayrı m_fDropY ile uygulanır
                main.gravityModifier = pData.PtGravity / 9.81f;

                // Open-KO birebir: C++'ta tüm partiküller dünya koordinatlarında simüle edilir (World space).
                // Ancak sabit efektlerde (MoveType == FX_BUNDLE_MOVE_NONE) Unity'nin ilk frame (0,0,0) konumunda 
                // partikül kaçırma hatasını önlemek için Local space kullanıyoruz.
                if (bundle.MoveType == KOFXManager.FX_BUNDLE_MOVE_NONE)
                {
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                }
                else
                {
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                }

                // Emission
                var emission = ps.emission;
                emission.enabled = true;
                if (pData.CreateDelay > 0)
                    emission.rateOverTime = pData.NumCreate / pData.CreateDelay;
                else
                    emission.rateOverTime = pData.NumCreate;

                // Shape — Cone shape = C++ emit cone (N3FXPartParticles.cpp:945-956)
                // C++'da partiküller EmitAngle açısıyla bir koni içinde rastgele dağıtılır,
                // sonra EmitDir yönüne döndürülür. Unity Cone shape bunu doğal olarak yapar.
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;

                // C++ EmitAngle = tam koni açısı, Unity cone angle = yarı açı
                // EmitType==1 (SPREAD) → cone açısı EmitAngle'dan gelir
                // EmitAngle == 0 → küçük varsayılan açı (tamamen paralel partiküller gerçekçi değil)
                float halfAngle = pData.EmitAngle > 0 ? pData.EmitAngle / 2f : 5f;
                shape.angle = halfAngle;

                // CreateRange — cone radius + position offset ile yaklaşık karşılık
                Vector3 rangeSize = pData.MaxCreateRange - pData.MinCreateRange;
                float avgRange = (Mathf.Abs(rangeSize.x) + Mathf.Abs(rangeSize.z)) * 0.5f;
                shape.radius = (avgRange > 0.001f ? avgRange * 0.5f : 0.1f) * targetScale;
                shape.radiusThickness = 1f;
                shape.position = (pData.MinCreateRange + pData.MaxCreateRange) * 0.5f * targetScale;

                // Cone yönünü EmitDir'e döndür → startSpeed bu yönde fırlatır
                // C++ birebir: N3FXPartParticles.cpp:1000-1014 — vDir, vDirEmit yönüne döndürülür
                if (pData.EmitDir.sqrMagnitude > 0.001f)
                {
                    Quaternion emitRot = Quaternion.FromToRotation(Vector3.forward, pData.EmitDir.normalized);
                    shape.rotation = emitRot.eulerAngles;
                }
            }

            // C++ N3FXPartBase.cpp:38-39 — per-part blend mode
            var psr = obj.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = CreatePartMaterial(part.Data);
            psr.renderMode = ParticleSystemRenderMode.Billboard;

            visual.ParticleSystem = ps;
            obj.SetActive(false);
        }

        /// <summary>
        /// BottomBoard setup — C++ N3FXPartBottomBoard — ground quad
        /// </summary>
        private void SetupBottomBoard(GameObject obj, FxPartInstance part, FxPartVisual visual)
        {
            var mf = obj.AddComponent<MeshFilter>();
            mf.sharedMesh = _quadMesh;

            var mr = obj.AddComponent<MeshRenderer>();
            // C++ N3FXPartBase.cpp:38-39 — per-part srcBlend/destBlend
            mr.sharedMaterial = CreatePartMaterial(part.Data);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Ground-aligned: X rotasyonu 90 derece
            obj.transform.localRotation = Quaternion.Euler(90, 0, 0);

            visual.Renderer = mr;
            obj.SetActive(false);
        }

        /// <summary>
        /// Her frame visual güncelle.
        /// N3FXBundle.cpp:464-477 + N3FXPartBillBoard.cpp:290-358
        /// </summary>
        private void UpdateVisual(FxBundleInstance bundle, FxVisualRoot visual)
        {
            // Bundle pozisyonu — C++ m_vPos
            visual.Root.transform.position = GetBundleWorldPos(bundle);
            if (bundle.Dir != Vector3.zero)
            {
                visual.Root.transform.rotation = Quaternion.LookRotation(bundle.Dir);
            }

            if (!visual.Root.activeSelf)
                visual.Root.SetActive(true);

            for (int i = 0; i < bundle.PartInstances.Length; i++)
            {
                var part = bundle.PartInstances[i];
                var pv = visual.PartVisuals[i];
                if (part == null || pv == null || pv.Obj == null) continue;

                // N3FXBundle.cpp:471-472 — state != DEAD && state != READY → render
                bool shouldRender = part.State != FxPartState.Dead && part.State != FxPartState.Ready;
                if (bundle.OverrideInstance != null)
                {
                    shouldRender = false; // Suppress legacy visual parts rendering
                }
                if (shouldRender)
                {
                    // C++ birebir: Rotate2AbsolutePos (N3FXPartBillBoard.cpp:675-704)
                    // Part CurrPos'u bundle dir yönüne göre döndür
                    Vector3 absPartPos = Rotate2AbsolutePos(part.CurrPos, bundle.Dir);
                    pv.Obj.transform.localPosition = absPartPos;
                }

                if (pv.Obj.activeSelf != shouldRender)
                    pv.Obj.SetActive(shouldRender);

                if (!shouldRender) continue;

                if (part.Data.Type == FxPartType.Billboard)
                    UpdateBillboard(part, pv, bundle);
                else if (part.Data.Type == FxPartType.Particle)
                    UpdateParticle(part, pv, bundle);
                else if (part.Data.Type == FxPartType.BottomBoard)
                    UpdateBottomBoard(part, pv, bundle);
                else if (part.Data.Type == FxPartType.Mesh)
                    UpdateMesh(part, pv, bundle);
            }
        }

        /// <summary>
        /// Billboard update — N3FXPartBillBoard.cpp:290-358 (Tick) + 376-658 (Render)
        /// </summary>
        private void UpdateBillboard(FxPartInstance part, FxPartVisual pv, FxBundleInstance bundle)
        {
            // C++ N3FXPartBillBoard::Render:378 — texIdx >= numTex → skip
            if (part.TexIdx >= part.Data.NumTextures)
            {
                pv.Obj.SetActive(false);
                return;
            }

            var cam = GetMainCamera();
            if (cam == null) return;

            // C++ N3FXPartBillBoard::Render:388-393 — ViewInverse (kameraya bak)
            // Unity: LookAt + Z-axis rotation
            pv.Obj.transform.LookAt(cam.transform.position);

            // C++ N3FXPartBillBoard::Render:384 — Z-axis rotation: RotationZ(currLife * rotVelocity.x)
            if (part.Data.RotVelocity.x != 0f)
            {
                float zAngle = part.CurrLife * part.Data.RotVelocity.x * Mathf.Rad2Deg;
                pv.Obj.transform.Rotate(0, 0, zAngle, Space.Self);
            }

            // C++ N3FXPartBillBoard::Render:397-406 — radius offset (kameraya doğru kaydır)
            float radius = part.Data.BillboardData != null ? part.Data.BillboardData.Radius : 0f;
            if (radius > 0.001f)
            {
                Vector3 toCamera = cam.transform.position - pv.Obj.transform.position;
                if (toCamera.magnitude <= radius)
                {
                    // cpp:400 — near plane'e kaydır
                    Vector3 camFwd = (cam.transform.forward).normalized;
                    pv.Obj.transform.position += camFwd * (cam.nearClipPlane + 0.1f);
                }
                else
                {
                    // cpp:404-405 — radius kadar kameraya doğru
                    pv.Obj.transform.position += toCamera.normalized * radius;
                }
            }

            // Size — C++ m_fCurrSizeX/Y (satır 329-330)
            float finalScaleX = part.CurrSizeX;
            float finalScaleY = part.CurrSizeY;
            if (bundle.Data.DependScale)
            {
                finalScaleX *= bundle.TargetScale;
                finalScaleY *= bundle.TargetScale;
            }
            
            pv.Obj.transform.localScale = new Vector3(
                Mathf.Max(0.01f, finalScaleX),
                Mathf.Max(0.01f, finalScaleY),
                1f);

            // Alpha/Color — C++ m_dwCurrColor (satır 300-320)
            if (pv.Renderer != null)
            {
                float alpha = ComputePartAlpha(part);
                var mat = pv.Renderer.sharedMaterial;
                mat.color = new Color(1, 1, 1, alpha);

                // Texture animation
                if (part.Data.NumTextures > 1 && part.TexIdx != pv.LastTexIdx)
                {
                    string texKey = part.Data.TextureName.Replace('\\', '/') +
                                    part.TexIdx.ToString("D4") + ".dxt";
                    var tex = LoadTexture(texKey);
                    if (tex != null)
                        mat.mainTexture = tex;
                    pv.LastTexIdx = part.TexIdx;
                }
            }
        }

        private void UpdateParticle(FxPartInstance part, FxPartVisual pv, FxBundleInstance bundle)
        {
            if (pv.ParticleSystem != null)
            {
                if (!pv.ParticleStarted && part.State == FxPartState.Live)
                {
                    // Force immediate transform propagation to prevent Unity's C++ particle engine 
                    // from using stale (0,0,0) world matrix in the first frame.
                    var forceRecalc = pv.Obj.transform.position;

                    pv.ParticleSystem.Play();
                    pv.ParticleStarted = true;
                }
                if (part.State == FxPartState.Dying)
                {
                    pv.ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        /// <summary>
        /// BottomBoard update — C++ N3FXPartBottomBoard::Tick/Render
        /// Ground-aligned quad: scale + alpha + texture animation
        /// </summary>
        private void UpdateBottomBoard(FxPartInstance part, FxPartVisual pv, FxBundleInstance bundle)
        {
            // C++ N3FXPartBottomBoard::Render:374 — texIdx >= numTex → skip
            if (part.TexIdx >= part.Data.NumTextures)
            {
                pv.Obj.SetActive(false);
                return;
            }

            // C++ N3FXPartBottomBoard.cpp:297-299 — Y-axis rotation
            float yAngle = part.CurrLife * part.Data.RotVelocity.y * Mathf.Rad2Deg;
            // BottomBoard ground-aligned: X=90 + Y rotation
            pv.Obj.transform.localRotation = Quaternion.Euler(90, yAngle, 0);

            // C++ BottomBoard: m_fCurrSizeX, m_fCurrSizeZ (X ve Z ekseni)
            pv.Obj.transform.localScale = new Vector3(
                Mathf.Max(0.01f, part.CurrSizeX),
                Mathf.Max(0.01f, part.CurrSizeY),
                1f);

            // C++ N3FXPartBottomBoard.cpp:326,346 — terrain height + gap
            var terrain = GetActiveTerrain();
            if (terrain != null)
            {
                Vector3 pos = pv.Obj.transform.position;
                float gap = part.Data.BottomBoardData != null ? part.Data.BottomBoardData.Gap : 0f;
                pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + gap;
                pv.Obj.transform.position = pos;
            }

            // Alpha + texture animation
            if (pv.Renderer != null)
            {
                float alpha = ComputePartAlpha(part);
                var mat = pv.Renderer.sharedMaterial;
                mat.color = new Color(1, 1, 1, alpha);

                if (part.Data.NumTextures > 1 && part.TexIdx != pv.LastTexIdx)
                {
                    string texKey = part.Data.TextureName.Replace('\\', '/') +
                                    part.TexIdx.ToString("D4") + ".dxt";
                    var tex = LoadTexture(texKey);
                    if (tex != null)
                        mat.mainTexture = tex;
                    pv.LastTexIdx = part.TexIdx;
                }
            }
        }

        /// <summary>
        /// Open-KO birebir: CN3FXPartMesh setup — N3FXShape yükle, mesh oluştur.
        /// CN3FXPartMesh::Load (N3FXPartMesh.cpp:226-303)
        /// </summary>
        private void SetupMesh(GameObject obj, FxPartInstance part, FxPartVisual visual)
        {
            var meshExtra = part.Data.MeshData;
            if (meshExtra == null || string.IsNullOrEmpty(meshExtra.MeshFileName))
            {
                obj.SetActive(false);
                return;
            }

            // Open-KO birebir: s_MngFXShape.Get(szShapeFileName) — N3FXPartMesh.cpp:232
            string shapeKey = meshExtra.MeshFileName.Replace('\\', '/').ToLowerInvariant();
            if (!_shapeCache.TryGetValue(shapeKey, out var shapeData))
            {
                // KOBinaryProvider üzerinden yükle (Resources/KOBinary/FX/)
                shapeData = N3ShapeParser.ParseShapeFile(meshExtra.MeshFileName);
                _shapeCache[shapeKey] = shapeData; // null dahil cache'le
            }

            if (shapeData == null || shapeData.Parts == null || shapeData.Parts.Count == 0)
            {
                obj.SetActive(false);
                return;
            }

            visual.ShapeData = shapeData;
            visual.MeshRenderers = new List<MeshRenderer>();

            // C++ CN3FXShape::Tick (N3FXShape.cpp:349) birebir hizalama:
            // Shape'in local transformu (baked position/rotation/scale) için bir ShapeRoot oluşturuyoruz.
            // Bu sayede silah glow'u/efekti mesh boyunca düzgün yayılır, tek noktada toplanmaz.
            var shapeRoot = new GameObject("ShapeRoot");
            visual.ShapeRoot = shapeRoot;
            shapeRoot.transform.SetParent(obj.transform, false);
            
            // FX mesh parçaları için shape parent transform pozisyon/rotasyonu yok sayılır.
            // Orijinal KO'da bu değerler sadece World/Terrain nesneleri için geçerlidir, FX'lerde çift ofsete sebep olur.
            shapeRoot.transform.localPosition = Vector3.zero;
            shapeRoot.transform.localRotation = Quaternion.identity;
            if (shapeData.Transform != null)
            {
                shapeRoot.transform.localScale = shapeData.Transform.Scale;
            }
            else
            {
                shapeRoot.transform.localScale = Vector3.one;
            }

            for (int sp = 0; sp < shapeData.Parts.Count; sp++)
            {
                var shapePart = shapeData.Parts[sp];
                if (string.IsNullOrEmpty(shapePart.MeshFileName)) continue;

                // KOBinaryProvider üzerinden mesh yükle
                var pmeshData = N3PMeshImporter.Load(shapePart.MeshFileName);
                if (pmeshData == null) continue;

                var unityMesh = N3PMeshImporter.CreateUnityMesh(pmeshData);
                if (unityMesh == null) continue;

                var meshObj = new GameObject($"MeshPart_{sp}");
                meshObj.transform.SetParent(shapeRoot.transform, false);
                
                // C++ CN3FXSPart::Tick (N3FXShape.cpp:94-96) birebir hizalama:
                // Sadece part'ın kendi pivot değeri yerel pozisyon olarak atanır.
                meshObj.transform.localRotation = Quaternion.identity;
                meshObj.transform.localScale = Vector3.one;
                meshObj.transform.localPosition = shapePart.Pivot;

                var mf = meshObj.AddComponent<MeshFilter>();
                mf.sharedMesh = unityMesh;

                var mr = meshObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = CreatePartMaterial(part.Data);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                // Texture yükle
                if (shapePart.TextureFileNames != null && shapePart.TextureFileNames.Count > 0)
                {
                    string texFileName = shapePart.TextureFileNames[0];
                    if (!string.IsNullOrEmpty(texFileName))
                    {
                        var tex = LoadTexture(texFileName.Replace('\\', '/'));
                        if (tex != null)
                            mr.sharedMaterial.mainTexture = tex;
                    }
                }

                visual.MeshRenderers.Add(mr);
            }

            obj.SetActive(false);
        }

        /// <summary>
        /// Open-KO birebir: CN3FXPartMesh::Tick + Render (N3FXPartMesh.cpp:388-721)
        /// Pozisyon, rotation, scale, alpha güncelleme.
        /// </summary>
        private void UpdateMesh(FxPartInstance part, FxPartVisual pv, FxBundleInstance bundle)
        {
            if (pv.MeshRenderers == null || pv.MeshRenderers.Count == 0) return;

            // Alpha — CN3FXPartMesh::Tick:396-442
            float alpha = ComputePartAlpha(part);

            // Scale — CN3FXPartMesh::Scaling (N3FXPartMesh.cpp:614-635) birebir
            // C++ satır 616: m_vCurrScaleVel += m_vScaleAccel * m_fCurrLife  (KÜMÜLATİF +=)
            // C++ satır 617: vScale = m_vCurrScaleVel * m_fCurrLife
            // C++ satır 618: vScale += m_vUnitScale
            var meshExtra = part.Data.MeshData;
            if (meshExtra != null)
            {
                // C++ birebir: m_vCurrScaleVel += m_vScaleAccel * m_fCurrLife
                // FxPartInstance.CurrScaleVelX/Y/Z = C++ m_vCurrScaleVel
                // Delta time yaklaşımı: her frame s_fSecPerFrm ile çarpılır
                // Ama C++ m_fCurrLife (toplam yaşam süresi) kullanıyor
                float dt = Time.deltaTime;
                part.CurrScaleVelX += meshExtra.ScaleAccel.x * part.CurrLife;
                part.CurrScaleVelY += meshExtra.ScaleAccel.y * part.CurrLife;
                part.CurrScaleVelZ += meshExtra.ScaleAccel.z * part.CurrLife;

                // C++ satır 617-618: vScale = currScaleVel * currLife + unitScale
                Vector3 vScale = new Vector3(
                    part.CurrScaleVelX * part.CurrLife + meshExtra.UnitScale.x,
                    part.CurrScaleVelY * part.CurrLife + meshExtra.UnitScale.y,
                    part.CurrScaleVelZ * part.CurrLife + meshExtra.UnitScale.z
                );

                // C++ satır 620-621: dependScale
                if (bundle.Data.DependScale)
                {
                    vScale *= bundle.TargetScale;
                }

                // C++ satır 623-628: clamp to zero
                vScale.x = Mathf.Max(0f, vScale.x);
                vScale.y = Mathf.Max(0f, vScale.y);
                vScale.z = Mathf.Max(0f, vScale.z);

                pv.Obj.transform.localScale = vScale;
            }

            // Rotation — CN3FXPartMesh::Rotate (N3FXPartMesh.cpp:543-573) birebir
            {
                // C++ satır 550: m_pShape->m_mtxParent.Rotation(m_vRotVelocity * m_fCurrLife)
                // C++ Rotation(x,y,z) = Rz * Ry * Rx (row-major, Matrix44.inl:185-208)
                // Column-major karşılığı: Rx * Ry * Rz
                // Unity Quaternion.Euler ZXY sırası kullanır — C++ ile UYUŞMAZ
                // Bu yüzden C++ birebir: Qx * Qy * Qz (column-major XYZ extrinsic) yapıyoruz
                Vector3 rotAngle = part.Data.RotVelocity * part.CurrLife;
                Quaternion localRot =
                    Quaternion.AngleAxis(rotAngle.x * Mathf.Rad2Deg, Vector3.right)
                  * Quaternion.AngleAxis(rotAngle.y * Mathf.Rad2Deg, Vector3.up)
                  * Quaternion.AngleAxis(rotAngle.z * Mathf.Rad2Deg, Vector3.forward);

                // Satır 552-572: mesh yönü (m_vDir = Z-forward) ile bundle yönü hizalama.
                // C++ birebir: m_vDir (Vector3.forward) ile m_pRefBundle->m_vDir (bundle.Dir) arasındaki dönüşüm
                Quaternion dirRot = bundle.Dir.sqrMagnitude > 0.001f ? Quaternion.FromToRotation(Vector3.forward, bundle.Dir) : Quaternion.identity;

                // C++ satır 572: m_pShape->m_mtxParent *= mtx
                // C++ row-major: localRot * dirRot → Unity column-major: dirRot * localRot
                pv.Obj.transform.rotation = dirRot * localRot;
            }

            // Shape animasyon karesi — C++ CN3FXPartMesh::Tick (N3FXPartMesh.cpp:444-457 + CN3Transform::TickAnimationKey)
            if (pv.ShapeRoot != null && pv.ShapeData?.Transform != null)
            {
                var t = pv.ShapeData.Transform;
                if (t.KeyPos != null)
                {
                    float fps = meshExtra != null && meshExtra.MeshFPS > 0f ? meshExtra.MeshFPS : (t.KeyPos.SamplingRate > 0f ? t.KeyPos.SamplingRate : 30f);
                    float animFrame = part.CurrLife * fps;
                    if (t.KeyPos.SampleVector(animFrame, out Vector3 animPos))
                    {
                        pv.ShapeRoot.transform.localPosition = animPos;
                    }
                }
            }

            // Material alpha update
            foreach (var mr in pv.MeshRenderers)
            {
                if (mr != null)
                    mr.sharedMaterial.color = new Color(1, 1, 1, alpha);
            }
        }

        /// <summary>
        /// C++ N3FXPartBillBoard.cpp:300-320 birebir — fade in/out alpha
        /// </summary>
        private static float ComputePartAlpha(FxPartInstance part)
        {
            // Satır 300-304 — fade in
            if (part.Data.FadeIn > 0 && part.CurrLife <= part.Data.FadeIn)
                return part.CurrLife / part.Data.FadeIn;

            // Satır 308-320 — fade out (dying state)
            if (part.State == FxPartState.Dying && part.Data.FadeOut > 0)
            {
                float totalLife = part.Data.FadeIn + part.Data.Life + part.Data.FadeOut;
                if (part.CurrLife >= totalLife) return 0f;
                return (totalLife - part.CurrLife) / part.Data.FadeOut;
            }

            return 1f;
        }

        /// <summary>
        /// Open-KO birebir: N3FXBundleGame m_vPos — bundle dünya pozisyonu.
        /// CN3FXBundleGame::Tick tarafından her frame güncellenir.
        /// </summary>
        private Vector3 GetBundleWorldPos(FxBundleInstance bundle)
        {
            return bundle.Pos;
        }

        /// <summary>
        /// C++ birebir: CN3FXPartBillBoard::Rotate2AbsolutePos (N3FXPartBillBoard.cpp:675-704)
        /// Part'ın relatif pozisyonunu bundle'ın m_vDir yönüne göre quaternion ile döndürür.
        /// </summary>
        private static Vector3 Rotate2AbsolutePos(Vector3 relativePos, Vector3 bundleDir)
        {
            if (relativePos.sqrMagnitude < 0.0001f) return relativePos;

            Vector3 axisZ = Vector3.forward; // (0,0,1)
            Vector3 dirNorm = bundleDir.sqrMagnitude > 0.001f ? bundleDir.normalized : axisZ;

            // cpp:683 — cross(axisZ, dir)
            Vector3 dirAxis = Vector3.Cross(axisZ, dirNorm);

            // cpp:685-690 — truncate to 4 decimal — C++ birebir: (int)(x*10000) sıfıra doğru keser
            dirAxis.x = (int)(dirAxis.x * 10000f) / 10000f;
            dirAxis.y = (int)(dirAxis.y * 10000f) / 10000f;
            dirAxis.z = (int)(dirAxis.z * 10000f) / 10000f;

            // cpp:692-693 — zero axis fallback (C++ birebir: exact == 0.0f check)
            if (dirAxis.x == 0f && dirAxis.y == 0f && dirAxis.z == 0f)
                dirAxis = Vector3.up;

            // cpp:695 — angle = acos(dot(axisZ, dir))
            float dot = Vector3.Dot(axisZ, dirNorm);
            dot = Mathf.Clamp(dot, -1f, 1f);
            float angle = Mathf.Acos(dot);

            // cpp:697-701 — quaternion rotation
            Quaternion rot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, dirAxis.normalized);
            return rot * relativePos;
        }

        private void CreateSharedAssets()
        {
            // Quad mesh — C++ m_vUnit (N3FXPartBillBoard.cpp:146-149)
            _quadMesh = new Mesh();
            _quadMesh.vertices = new[]
            {
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f, -0.5f, 0f)
            };
            _quadMesh.uv = new[]
            {
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(1, 0), new Vector2(0, 0)
            };
            // C++ birebir: FVF_XYZCOLORT1 — vertex color (m_dwCurrColor)
            // C++ N3FXPartBillBoard::Render:567 → m_dwCurrColor = 0xFFFFFFFF (beyaz, tam opak)
            _quadMesh.colors = new[]
            {
                Color.white, Color.white,
                Color.white, Color.white
            };
            _quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _quadMesh.RecalculateNormals();
        }

        /// <summary>
        /// Open-KO birebir: N3FXPartBase.cpp:38-39 — per-part blend mode material oluştur.
        /// C++ D3DBLEND enum: 1=ZERO, 2=ONE, 5=SRCALPHA, 6=INVSRCALPHA
        /// C++ N3FXPartBase.cpp:49-69 — RenderFlags: RF_ALPHABLENDING=0x1, RF_NOTZWRITE=0x100, RF_NOTZBUFFER=0x400
        /// </summary>
        private Material CreatePartMaterial(FxPartData partData)
        {
            return CreateFxMaterial(partData.SrcBlend, partData.DestBlend, partData.RenderFlags);
        }

        // C++ D3DBLEND enum (d3d8types.h) birebir
        private const uint D3DBLEND_ZERO         = 1;
        private const uint D3DBLEND_ONE          = 2;
        private const uint D3DBLEND_SRCCOLOR     = 3;
        private const uint D3DBLEND_INVSRCCOLOR  = 4;
        private const uint D3DBLEND_SRCALPHA     = 5;
        private const uint D3DBLEND_INVSRCALPHA  = 6;

        // C++ RenderFlags (N3FXPartBase.cpp:57-68) birebir
        private const uint RF_ALPHABLENDING = 0x1;
        private const uint RF_NOTUSEFOG     = 0x2;
        private const uint RF_DOUBLESIDED   = 0x4;
        private const uint RF_NOTUSELIGHT   = 0x40;
        private const uint RF_DIFFUSEALPHA  = 0x80;
        private const uint RF_NOTZWRITE     = 0x100;
        private const uint RF_NOTZBUFFER    = 0x400;

        /// <summary>
        /// C++ D3DBLEND → Unity blend mode birebir eşleme.
        /// </summary>
        private static UnityEngine.Rendering.BlendMode D3DBlendToUnity(uint d3dBlend)
        {
            switch (d3dBlend)
            {
                case D3DBLEND_ZERO:        return UnityEngine.Rendering.BlendMode.Zero;
                case D3DBLEND_ONE:         return UnityEngine.Rendering.BlendMode.One;
                case D3DBLEND_SRCCOLOR:    return UnityEngine.Rendering.BlendMode.SrcColor;
                case D3DBLEND_INVSRCCOLOR: return UnityEngine.Rendering.BlendMode.OneMinusSrcColor;
                case D3DBLEND_SRCALPHA:    return UnityEngine.Rendering.BlendMode.SrcAlpha;
                case D3DBLEND_INVSRCALPHA: return UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                default:                   return UnityEngine.Rendering.BlendMode.One;
            }
        }

        /// <summary>
        /// D3DBLEND + RenderFlags → Unity material birebir.
        /// C++ AlphaPrimitiveManager::Render (N3AlphaPrimitiveManager.cpp:70-198)
        /// Per-part render flag'ları material property olarak uygulanır.
        /// </summary>
        public static Material CreateFxMaterial(uint srcBlend, uint destBlend, uint renderFlags = 0)
        {
            // C++ birebir: N3FXPartBase.cpp:38-39 — default m_dwSrcBlend = D3DBLEND_ONE, m_dwDestBlend = D3DBLEND_ONE
            // Parse'ta 0 kalan değerler C++ default additive blend olmalı
            if (srcBlend == 0) srcBlend = D3DBLEND_ONE;
            if (destBlend == 0) destBlend = D3DBLEND_ONE;

            // C++ birebir: N3AlphaPrimitiveManager::Render satır 135-142
            // Her primitive kendi dwBlendSrc/dwBlendDest ile render edilir.
            // Unity'de bunu per-material _SrcBlend/_DstBlend property ile sağlıyoruz.
            Shader shader = Shader.Find("KO/FX/Generic");

            // Fallback
            if (shader == null)
            {
                Debug.LogWarning($"[KOFXRenderer] KO/FX/Generic shader bulunamadı, fallback kullanılıyor.");
                shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
            }

            var mat = new Material(shader);
            mat.renderQueue = 3100;

            // C++ birebir: per-part D3DBLEND → Unity BlendMode
            mat.SetInt("_SrcBlend", (int)D3DBlendToUnity(srcBlend));
            mat.SetInt("_DstBlend", (int)D3DBlendToUnity(destBlend));

            // C++ AlphaPrimitiveManager::Render satır 76-79: RF_DOUBLESIDED
            // RF_DOUBLESIDED (0x4) → D3DCULL_NONE, yoksa → D3DCULL_CCW
            // N3FXPartBase.cpp:543-546 birebir
            if ((renderFlags & RF_DOUBLESIDED) == 0)
            {
                // C++ D3DCULL_CCW = Back face culling
                mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
            }
            // else: shader default Cull Off (D3DCULL_NONE)

            return mat;
        }

        /// <summary>
        /// C++ N3FXPartBase.cpp:573-580 birebir — texture yükleme.
        /// Format: m_pTexName + "0000" + ".dxt"
        /// İlk frame (index 0) yüklenir ve material'e atanır.
        /// </summary>
        private void LoadAndApplyTexture(FxPartInstance part, FxPartVisual visual)
        {
            if (string.IsNullOrEmpty(part.Data.TextureName)) return;
            if (part.Data.NumTextures <= 0) return;

            // C++ N3FXPartBase.cpp:578 — FileName = fmt::format("{}{:04}.dxt", m_pTexName, 0)
            string texKey = part.Data.TextureName.Replace('\\', '/') + "0000.dxt";
            var tex = LoadTexture(texKey);
            if (tex == null) return;

            if (visual.Renderer != null)
                visual.Renderer.sharedMaterial.mainTexture = tex;
            else if (visual.ParticleSystem != null)
            {
                var psr = visual.Obj.GetComponent<ParticleSystemRenderer>();
                if (psr != null) psr.sharedMaterial.mainTexture = tex;
            }
        }

        /// <summary>
        /// DXT texture yükle (cache'li).
        /// C++ CN3Base::s_MngTex.Get(FileName) karşılığı.
        /// </summary>
        public Texture2D LoadTexture(string relPath)
        {
            if (_texCache.TryGetValue(relPath, out var cached))
                return cached;

            string baseName = Path.GetFileNameWithoutExtension(relPath);
            Texture2D tex = null;

            // Resources/KOTextures/ ana dizinlerden ara
            string[] searchDirs = { "fx", "Object", "Chr", "Item", "Misc", "DTex" };
            foreach (var dir in searchDirs)
            {
                tex = Resources.Load<Texture2D>($"KOTextures/{dir}/{baseName}");
                if (tex != null) break;
            }

            // FX alt dizinleri ara (billboard/, ground/, object/, particle/)
            // Dosya yapısı: KOTextures/fx/{subdir}/{texGroup}/{texFrame}.png
            // Örnek: KOTextures/fx/object/0807sprint_m/0807sprint_m0000.png
            if (tex == null)
            {
                string[] fxSubDirs = { "billboard", "ground", "object", "particle", "arrow", "javelin" };
                // Texture grup adını bul: baseName'den son 4 rakamı (frame no) çıkar
                // Örnek: "0807sprint_m0000" → grup = "0807sprint_m"
                string groupName = baseName;
                if (baseName.Length > 4)
                {
                    string lastFour = baseName.Substring(baseName.Length - 4);
                    if (int.TryParse(lastFour, out _))
                        groupName = baseName.Substring(0, baseName.Length - 4);
                }

                foreach (var sub in fxSubDirs)
                {
                    tex = Resources.Load<Texture2D>($"KOTextures/fx/{sub}/{groupName}/{baseName}");
                    if (tex != null) break;
                }
            }

            // relPath'teki dizin yapısını doğrudan dene
            // Örnek: relPath = "FX/Billboard/0714blow_target0000.dxt"
            // → KOTextures/FX/Billboard/0714blow_target/0714blow_target0000
            if (tex == null && relPath.Contains("/"))
            {
                // relPath'ten dizin + dosya adını çıkar
                string cleanPath = relPath.Replace(".dxt", "").Replace(".DXT", "");
                // Önce doğrudan dene
                tex = Resources.Load<Texture2D>($"KOTextures/{cleanPath}");

                // Yoksa lowercase dene
                if (tex == null)
                    tex = Resources.Load<Texture2D>($"KOTextures/{cleanPath.ToLowerInvariant()}");

                // Yoksa grup dizini ekle
                if (tex == null)
                {
                    string dirPart = Path.GetDirectoryName(cleanPath)?.Replace('\\', '/') ?? "";
                    string fileBase = Path.GetFileNameWithoutExtension(cleanPath);
                    string grp = fileBase;
                    if (grp.Length > 4 && int.TryParse(grp.Substring(grp.Length - 4), out _))
                        grp = grp.Substring(0, grp.Length - 4);
                    tex = Resources.Load<Texture2D>($"KOTextures/{dirPart}/{grp}/{fileBase}");
                    if (tex == null)
                        tex = Resources.Load<Texture2D>($"KOTextures/{dirPart.ToLowerInvariant()}/{grp}/{fileBase}");
                }
            }

            // Fallback: KOTextureProvider
            if (tex == null)
                tex = KOTextureProvider.Load(relPath);

            if (tex != null)
                _texCache[relPath] = tex;
            else
                Debug.LogWarning($"[KOFXRenderer] Texture NOT FOUND: relPath='{relPath}' baseName='{baseName}'");

            return tex;
        }

        private void OnDestroy()
        {
            foreach (var kvp in _visuals)
            {
                if (kvp.Value.Root != null)
                    Destroy(kvp.Value.Root);
            }
            _visuals.Clear();


            _shapeCache.Clear();
        }
    }

    // Visual data classes
    public class FxVisualRoot
    {
        public GameObject Root;
        public FxPartVisual[] PartVisuals;
    }

    public class FxPartVisual
    {
        public GameObject Obj;
        public GameObject ShapeRoot;
        public EntropyOnline.Import.N3ShapeParser.N3ShapeData ShapeData;
        public MeshRenderer Renderer;
        public ParticleSystem ParticleSystem;
        public bool ParticleStarted;
        /// <summary>Son uygulanan texture index — texture animation takibi</summary>
        public int LastTexIdx = -1;
        /// <summary>Open-KO: CN3FXShape part mesh renderers — N3FXPartMesh multi-part</summary>
        public List<MeshRenderer> MeshRenderers;
    }
}
