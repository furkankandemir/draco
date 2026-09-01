 using System.Collections.Generic;
 using System.IO;
 using UnityEngine;
 using KOImport;
 using EntropyOnline.Import;
 using KO;
 
 namespace EntropyOnline.World
 {
     /// <summary>
     /// Knight Online birebir: CPlayerBase::PlugSet + CN3CPlug::RenderFX weapon glow.
     /// Silahların +7/+8 upgrade seviyelerindeki alev, buz, şimşek veya zehir parlama efektleri.
     /// </summary>
     public class KOWeaponGlow : MonoBehaviour
     {
         private const int LIMIT_FX_DAMAGE = 64;
         private const int ITEM_ATTRIB_UNIQUE = 4;
         private const int MAX_FXTAIL = 5; // C++ Warfare client MAX_FXTAIL
 
         private KOTableReader.TableItemExt _itemExt;
         private FxBundleData _mainBundle;
         private FxBundleData _tailBundle;
 
         private FxPartData _mainPartData;
 
         // 3-Pass Mesh Glow Overlay objects
         private GameObject _glowPass1;
         private GameObject _glowPass2;
         private GameObject _glowPass3;
 
         private Material _glowMaterial1;
         private Material _glowMaterial2;
         private Material _glowMaterial3;
 
         private float _elapsedLife;
         private int _lastTexIdx1 = -1;
         private int _lastTexIdx2 = -1;
         private int _lastTexIdx3 = -1;
 
         private Vector3 _normal0 = Vector3.up;
         private Bounds _weaponBounds;
         private Mesh _quadMesh;
 
         // Floating particles (Tail bundles)
         private readonly List<GameObject> _tailInstances = new();
         private readonly List<PartVisual> _tailVisualParts = new();
 
         private class CustomParticle
         {
             public GameObject Obj;
             public MeshRenderer Renderer;
             public Vector3 CreatePoint;
             public Vector3 LcPos;
             public Vector3 Velocity;
             public Vector3 Accel;
             public float Size;
             public float Life;
             public float CurrLife;
             public float DropY;
             public float DropVel;
             public int LastTexIdx = -1;
             public bool IsAlive;
         }
 
         private class PartVisual
         {
             public FxPartData Data;
             public GameObject Obj;
             public MeshRenderer Renderer;
             public int LastTexIdx = -1;
             public float CurrLife;
             public float SpawnTimer;
             public readonly List<CustomParticle> CustomParticles = new();
         }
 
         private void CleanUpExistingGlow()
         {
             if (_glowPass1 != null) { DestroyImmediate(_glowPass1); _glowPass1 = null; }
             if (_glowPass2 != null) { DestroyImmediate(_glowPass2); _glowPass2 = null; }
             if (_glowPass3 != null) { DestroyImmediate(_glowPass3); _glowPass3 = null; }
 
             if (_glowMaterial1 != null) { DestroyImmediate(_glowMaterial1); _glowMaterial1 = null; }
             if (_glowMaterial2 != null) { DestroyImmediate(_glowMaterial2); _glowMaterial2 = null; }
             if (_glowMaterial3 != null) { DestroyImmediate(_glowMaterial3); _glowMaterial3 = null; }
 
             foreach (var tail in _tailInstances)
             {
                 if (tail != null) DestroyImmediate(tail);
             }
             _tailInstances.Clear();
             _tailVisualParts.Clear();
 
             if (_quadMesh != null) { DestroyImmediate(_quadMesh); _quadMesh = null; }
 
             _mainBundle = null;
             _tailBundle = null;
             _mainPartData = null;
         }
 
         public void Initialize(KOTableReader.TableItemExt pItemExt)
         {
             CleanUpExistingGlow();
 
             _itemExt = pItemExt;
             if (_itemExt == null) return;
 
             int mainFxId = -1;
             int tailFxId = -1;
 
             int upgradeLevel = (int)(_itemExt.dwID % 100);
             bool isUnique = (_itemExt.byMagicOrRare == ITEM_ATTRIB_UNIQUE);
             bool shouldGlow = (isUnique && upgradeLevel >= 6) || (!isUnique && upgradeLevel >= 8);
 
 
             if (shouldGlow)
             {
                 if ((_itemExt.byMagicOrRare == ITEM_ATTRIB_UNIQUE && _itemExt.byDamageFire > 0)
                     || (_itemExt.byDamageFire >= LIMIT_FX_DAMAGE))
                 {
                     mainFxId = KOFXManager.FXID_SWORD_FIRE_MAIN; // 10021
                     tailFxId = KOFXManager.FXID_SWORD_FIRE_TAIL; // 10022
                 }
                 else if ((_itemExt.byMagicOrRare == ITEM_ATTRIB_UNIQUE && _itemExt.byDamageIce > 0)
                          || (_itemExt.byDamageIce >= LIMIT_FX_DAMAGE))
                 {
                     mainFxId = KOFXManager.FXID_SWORD_ICE_MAIN; // 10023
                     tailFxId = KOFXManager.FXID_SWORD_ICE_TAIL; // 10024
                 }
                 else if ((_itemExt.byMagicOrRare == ITEM_ATTRIB_UNIQUE && _itemExt.byDamageThuner > 0)
                          || (_itemExt.byDamageThuner >= LIMIT_FX_DAMAGE))
                 {
                     mainFxId = KOFXManager.FXID_SWORD_LIGHTNING_MAIN; // 10025
                     tailFxId = KOFXManager.FXID_SWORD_LIGHTNING_TAIL; // 10026
                 }
                 else if ((_itemExt.byMagicOrRare == ITEM_ATTRIB_UNIQUE && _itemExt.byDamagePoison > 0)
                          || (_itemExt.byDamagePoison >= LIMIT_FX_DAMAGE))
                 {
                     mainFxId = KOFXManager.FXID_SWORD_POISON_MAIN; // 10027
                     tailFxId = KOFXManager.FXID_SWORD_POISON_TAIL; // 10028
                 }
             }
 
             if (mainFxId == -1)
             {
                 enabled = false;
                 return;
             }
 
             enabled = true;
 
             // FX Table'dan .fxb yollarını çöz
             var mainEntry = FxTableParser.Find(mainFxId);
             var tailEntry = FxTableParser.Find(tailFxId);
 
             if (mainEntry != null && !string.IsNullOrEmpty(mainEntry.FileName))
             {
                 _mainBundle = FxBundleParser.Parse(mainEntry.FileName);
             }
 
             if (tailEntry != null && !string.IsNullOrEmpty(tailEntry.FileName))
             {
                 _tailBundle = FxBundleParser.Parse(tailEntry.FileName);
             }
 
             // C++: m_pFXPart = (CN3FXPartBillBoard*) m_pFXMainBundle->GetPart(0);
             if (_mainBundle != null && _mainBundle.Parts.Count > 0)
             {
                 foreach (var part in _mainBundle.Parts)
                 {
                     if (part.Type == FxPartType.Billboard)
                     {
                         _mainPartData = part;
                         break;
                     }
                 }
             }
 
             // Orijinal C++: m_pFXPart->m_fCurrLife = (float)(rand() % 1000) / 100.0f;
             _elapsedLife = UnityEngine.Random.Range(0f, 10f);
 
             CreateSharedQuadMesh();
             SetupWeaponMeshBounds();
             SetupMainGlowOverlay();
             SetupTailParticles();
         }
 
         private void CreateSharedQuadMesh()
         {
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
             _quadMesh.colors = new[]
             {
                 Color.white, Color.white,
                 Color.white, Color.white
             };
             _quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
             _quadMesh.RecalculateNormals();
         }
 
         private void SetupWeaponMeshBounds()
         {
             var mf = GetComponent<MeshFilter>();
             if (mf != null && mf.sharedMesh != null)
             {
                 var mesh = mf.sharedMesh;
                 _weaponBounds = mesh.bounds;
                 if (mesh.normals != null && mesh.normals.Length > 0)
                     _normal0 = mesh.normals[0];
             }
             else
             {
                 _weaponBounds = new Bounds(Vector3.zero, new Vector3(0.2f, 1.5f, 0.2f));
             }
         }
 
         private void SetupMainGlowOverlay()
         {
             if (_mainPartData == null) return;
 
             var parentMf = GetComponent<MeshFilter>();
             var parentMr = GetComponent<MeshRenderer>();
             if (parentMf == null || parentMf.sharedMesh == null) return;
 
             // Pass 1
             _glowPass1 = new GameObject("GlowPass_1");
             _glowPass1.transform.SetParent(transform, false);
             _glowPass1.transform.localScale = Vector3.one;
             _glowPass1.AddComponent<MeshFilter>().sharedMesh = parentMf.sharedMesh;
             var mr1 = _glowPass1.AddComponent<MeshRenderer>();
             mr1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
             mr1.receiveShadows = false;
             _glowMaterial1 = KOFXRenderer.CreateFxMaterial(_mainPartData.SrcBlend, _mainPartData.DestBlend, _mainPartData.RenderFlags);
             mr1.sharedMaterial = _glowMaterial1;
 
             // Pass 2
             _glowPass2 = new GameObject("GlowPass_2");
             _glowPass2.transform.SetParent(transform, false);
             _glowPass2.transform.localScale = Vector3.one;
             _glowPass2.AddComponent<MeshFilter>().sharedMesh = parentMf.sharedMesh;
             var mr2 = _glowPass2.AddComponent<MeshRenderer>();
             mr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
             mr2.receiveShadows = false;
             _glowMaterial2 = KOFXRenderer.CreateFxMaterial(_mainPartData.SrcBlend, _mainPartData.DestBlend, _mainPartData.RenderFlags);
             mr2.sharedMaterial = _glowMaterial2;
 
             // Pass 3
             _glowPass3 = new GameObject("GlowPass_3");
             _glowPass3.transform.SetParent(transform, false);
             _glowPass3.transform.localScale = Vector3.one;
             _glowPass3.AddComponent<MeshFilter>().sharedMesh = parentMf.sharedMesh;
             var mr3 = _glowPass3.AddComponent<MeshRenderer>();
             mr3.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
             mr3.receiveShadows = false;
             _glowMaterial3 = KOFXRenderer.CreateFxMaterial(_mainPartData.SrcBlend, _mainPartData.DestBlend, _mainPartData.RenderFlags);
             mr3.sharedMaterial = _glowMaterial3;
         }
 
         private void SetupTailParticles()
         {
             if (_tailBundle == null || _tailBundle.Parts.Count == 0) return;
 
             // CN3CPlug::InitFX / RenderFX: MAX_FXTAIL (5) adet kopyasını çıkarır.
             // bundle[0]'ı orjin olarak kullanıp, 1..4 arasını render eder.
             Vector3 min = _weaponBounds.min;
             Vector3 max = _weaponBounds.max;
             Vector3 interval = max - min;
 
             float targetScale = (interval.z + interval.y) * 0.7f;
             if (targetScale <= 0f) targetScale = 1.0f;
 
             for (int i = 1; i < MAX_FXTAIL; i++)
             {
                 var tailObj = new GameObject($"TailInstance_{i}");
                 tailObj.transform.SetParent(transform, false);
 
                 // C++ RenderFX: vTmp konumu (titremeyi/hızlı sıçramaları önlemek için başlangıçta bir kez sabitlenir)
                 Vector3 randLocalPos = new Vector3(
                     min.x + (interval.x * 0.25f) + (interval.x * UnityEngine.Random.Range(0f, 0.5f)),
                     min.y + (interval.y * 0.25f) + (interval.y * UnityEngine.Random.Range(0f, 0.5f)),
                     min.z + (interval.z * 0.25f) + (interval.z * UnityEngine.Random.Range(0f, 0.5f))
                 );
                 tailObj.transform.localPosition = randLocalPos;
 
                 _tailInstances.Add(tailObj);
 
                 foreach (var partData in _tailBundle.Parts)
                 {
                     var partObj = new GameObject($"Part_{partData.SlotIndex}_{partData.Type}");
                     partObj.transform.SetParent(tailObj.transform, false);
 
                     var vp = new PartVisual { Data = partData, Obj = partObj };
 
                     if (partData.Type == FxPartType.Billboard)
                     {
                         var mf = partObj.AddComponent<MeshFilter>();
                         mf.sharedMesh = _quadMesh;
 
                         var mr = partObj.AddComponent<MeshRenderer>();
                         mr.sharedMaterial = KOFXRenderer.CreateFxMaterial(partData.SrcBlend, partData.DestBlend, partData.RenderFlags);
                         mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                         mr.receiveShadows = false;
 
                         vp.Renderer = mr;
                     }
                     else if (partData.Type == FxPartType.Particle)
                     {
                         var pData = partData.ParticleData;
                         if (pData != null)
                         {
                             // Preallocate pData.NumParticles child particles
                             for (int pIdx = 0; pIdx < pData.NumParticles; pIdx++)
                             {
                                 var pObj = new GameObject($"Particle_{pIdx}");
                                 pObj.transform.SetParent(partObj.transform, false);
 
                                 var mf = pObj.AddComponent<MeshFilter>();
                                 mf.sharedMesh = _quadMesh;
 
                                 var mr = pObj.AddComponent<MeshRenderer>();
                                 mr.sharedMaterial = KOFXRenderer.CreateFxMaterial(partData.SrcBlend, partData.DestBlend, partData.RenderFlags);
                                 mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                                 mr.receiveShadows = false;
 
                                 pObj.SetActive(false);
 
                                 vp.CustomParticles.Add(new CustomParticle
                                 {
                                     Obj = pObj,
                                     Renderer = mr
                                 });
                             }
                         }
                     }
 
                     // İlk texture yükle
                     if (!string.IsNullOrEmpty(partData.TextureName) && partData.NumTextures > 0)
                     {
                         string texKey = partData.TextureName.Replace('\\', '/') + "0000.dxt";
                         var tex = KOFXManager.Instance?.Renderer?.LoadTexture(texKey);
                         if (tex != null)
                         {
                             if (vp.Renderer != null) vp.Renderer.sharedMaterial.mainTexture = tex;
                             else
                             {
                                 foreach (var cp in vp.CustomParticles)
                                 {
                                     if (cp.Renderer != null)
                                         cp.Renderer.sharedMaterial.mainTexture = tex;
                                 }
                             }
                         }
                     }
 
                     _tailVisualParts.Add(vp);
                 }
             }
         }
 
         private void Update()
         {
             if (EntropyOnline.UI.GameOptionsManager.Instance != null && EntropyOnline.UI.GameOptionsManager.Instance.Effect_HideWeaponFX)
             {
                 if (_glowPass1 != null && _glowPass1.activeSelf) _glowPass1.SetActive(false);
                 if (_glowPass2 != null && _glowPass2.activeSelf) _glowPass2.SetActive(false);
                 if (_glowPass3 != null && _glowPass3.activeSelf) _glowPass3.SetActive(false);
                 
                 foreach (var tail in _tailInstances)
                 {
                     if (tail != null && tail.activeSelf) tail.SetActive(false);
                 }
                 return;
             }
             else
             {
                 if (_glowPass1 != null && !_glowPass1.activeSelf) _glowPass1.SetActive(true);
                 if (_glowPass2 != null && !_glowPass2.activeSelf) _glowPass2.SetActive(true);
                 if (_glowPass3 != null && !_glowPass3.activeSelf) _glowPass3.SetActive(true);
                 
                 foreach (var tail in _tailInstances)
                 {
                     if (tail != null && !tail.activeSelf) tail.SetActive(true);
                 }
             }
 
             _elapsedLife += Time.deltaTime;
 
             UpdateMainGlowOverlay();
             UpdateTailParticles();
         }
 
         private void UpdateMainGlowOverlay()
         {
             if (_mainPartData == null) return;
 
             int totalFrames = _mainPartData.NumTextures;
             if (totalFrames <= 0) return;
 
             float fps = _mainPartData.TextureFPS > 0 ? _mainPartData.TextureFPS : 30f;
             int texIdx = Mathf.FloorToInt(_elapsedLife * fps) % totalFrames;
 
             // CN3CPlug::RenderFX fArg2 oscillation:
             // float fArg1 = m_pFXMainBundle->m_fLife * 1.2f;
             // float fArg2 = (0.07f * (fArg1 - (int)fArg1)) - 0.035f;
             float fArg1 = _elapsedLife * 1.2f;
             float fArg2 = (0.07f * (fArg1 - (int)fArg1)) - 0.035f;
 
             // Pass 1: Identity local transform
             if (_glowPass1 != null)
             {
                 _glowPass1.transform.localPosition = Vector3.zero;
                 _glowPass1.transform.localRotation = Quaternion.identity;
 
                 if (texIdx != _lastTexIdx1)
                 {
                     _lastTexIdx1 = texIdx;
                     string texPath = _mainPartData.TextureName.Replace('\\', '/') + texIdx.ToString("D4") + ".dxt";
                     var tex = KOFXManager.Instance?.Renderer?.LoadTexture(texPath);
                     if (tex != null) _glowMaterial1.mainTexture = tex;
                 }
             }
 
             // Pass 2: Rotated Y by -5 deg, offset along normal
             if (_glowPass2 != null)
             {
                 _glowPass2.transform.localPosition = _normal0 * fArg2;
                 _glowPass2.transform.localRotation = Quaternion.Euler(0, -5f, 0);
 
                 int texIdx2 = (texIdx + 1) % totalFrames;
                 if (texIdx2 != _lastTexIdx2)
                 {
                     _lastTexIdx2 = texIdx2;
                     string texPath = _mainPartData.TextureName.Replace('\\', '/') + texIdx2.ToString("D4") + ".dxt";
                     var tex = KOFXManager.Instance?.Renderer?.LoadTexture(texPath);
                     if (tex != null) _glowMaterial2.mainTexture = tex;
                 }
             }
 
             // Pass 3: Rotated Y by 5 deg, offset opposite normal
             if (_glowPass3 != null)
             {
                 _glowPass3.transform.localPosition = -_normal0 * fArg2;
                 _glowPass3.transform.localRotation = Quaternion.Euler(0, 5f, 0);
 
                 int texIdx3 = (texIdx + 2) % totalFrames;
                 if (texIdx3 != _lastTexIdx3)
                 {
                     _lastTexIdx3 = texIdx3;
                     string texPath = _mainPartData.TextureName.Replace('\\', '/') + texIdx3.ToString("D4") + ".dxt";
                     var tex = KOFXManager.Instance?.Renderer?.LoadTexture(texPath);
                     if (tex != null) _glowMaterial3.mainTexture = tex;
                 }
             }
         }
 
         private void UpdateTailParticles()
         {
             if (_tailInstances.Count == 0) return;
 
             // C++ RenderFX: vTmp konumu her kare yeniden hesaplanarak kıvılcımların namlu boyunca dağılması sağlanır
             Vector3 min = _weaponBounds.min;
             Vector3 max = _weaponBounds.max;
             Vector3 interval = max - min;
 
             float targetScale = (interval.z + interval.y) * 0.7f;
             if (targetScale <= 0f) targetScale = 1.0f;
 
             for (int i = 0; i < _tailInstances.Count; i++)
             {
                 var tailObj = _tailInstances[i];
                 if (tailObj != null)
                 {
                     Vector3 randLocalPos = new Vector3(
                         min.x + (interval.x * 0.25f) + (interval.x * UnityEngine.Random.Range(0f, 0.5f)),
                         min.y + (interval.y * 0.25f) + (interval.y * UnityEngine.Random.Range(0f, 0.5f)),
                         min.z + (interval.z * 0.25f) + (interval.z * UnityEngine.Random.Range(0f, 0.5f))
                     );
                     tailObj.transform.localPosition = randLocalPos;
                 }
             }
 
             // Billboard vb. part animasyonlarını güncelle
             var cam = UnityEngine.Camera.main;
             foreach (var vp in _tailVisualParts)
             {
                 vp.CurrLife += Time.deltaTime;
 
                 if (vp.Data.Type == FxPartType.Billboard && vp.Renderer != null)
                 {
                     if (cam != null)
                     {
                         vp.Obj.transform.LookAt(cam.transform.position);
 
                         if (vp.Data.RotVelocity.x != 0f)
                         {
                             float zAngle = vp.CurrLife * vp.Data.RotVelocity.x * Mathf.Rad2Deg;
                             vp.Obj.transform.Rotate(0, 0, zAngle, Space.Self);
                         }
                     }
 
                     // Billboard size
                     float scaleX = Mathf.Max(0.01f, vp.Data.BillboardData != null ? vp.Data.BillboardData.Width : 1f);
                     float scaleY = Mathf.Max(0.01f, vp.Data.BillboardData != null ? vp.Data.BillboardData.Height : 1f);
                     vp.Obj.transform.localScale = new Vector3(scaleX, scaleY, 1f);
 
                     // Texture animation
                     int totalFrames = vp.Data.NumTextures;
                     if (totalFrames > 0)
                     {
                         float fps = vp.Data.TextureFPS > 0 ? vp.Data.TextureFPS : 30f;
                         int texIdx = Mathf.FloorToInt(vp.CurrLife * fps) % totalFrames;
 
                         if (texIdx != vp.LastTexIdx)
                         {
                             vp.LastTexIdx = texIdx;
                             string texKey = vp.Data.TextureName.Replace('\\', '/') + texIdx.ToString("D4") + ".dxt";
                             var tex = KOFXManager.Instance?.Renderer?.LoadTexture(texKey);
                             if (tex != null)
                                 vp.Renderer.sharedMaterial.mainTexture = tex;
                         }
                     }
 
                     // Alpha
                     float alpha = 1f;
                     if (vp.Data.FadeIn > 0 && vp.CurrLife <= vp.Data.FadeIn)
                         alpha = vp.CurrLife / vp.Data.FadeIn;
 
                     vp.Renderer.sharedMaterial.color = new Color(1, 1, 1, alpha);
                 }
                 else if (vp.Data.Type == FxPartType.Particle)
                 {
                     var pData = vp.Data.ParticleData;
                     if (pData != null)
                     {
                         // 1. Spawning
                         float createDelay = pData.CreateDelay > 0 ? pData.CreateDelay : 0.1f;
                         vp.SpawnTimer += Time.deltaTime;
                         if (vp.SpawnTimer >= createDelay)
                         {
                             vp.SpawnTimer = 0f;
                             int spawned = 0;
                             for (int k = 0; k < vp.CustomParticles.Count; k++)
                             {
                                 var cp = vp.CustomParticles[k];
                                 if (!cp.IsAlive)
                                 {
                                     cp.IsAlive = true;
                                     cp.Life = UnityEngine.Random.Range(pData.ParticleLifeMin, pData.ParticleLifeMax);
                                     cp.CurrLife = 0f;
                                     cp.Size = UnityEngine.Random.Range(pData.ParticleSizeMin, pData.ParticleSizeMax) * targetScale;
 
                                     Vector3 minCreate = pData.MinCreateRange * targetScale;
                                     Vector3 maxCreate = pData.MaxCreateRange * targetScale;
                                     cp.LcPos = new Vector3(
                                         UnityEngine.Random.Range(minCreate.x, maxCreate.x),
                                         UnityEngine.Random.Range(minCreate.y, maxCreate.y),
                                         UnityEngine.Random.Range(minCreate.z, maxCreate.z)
                                     );
 
                                     Vector3 emitDir = pData.EmitDir.normalized;
                                     if (pData.EmitType == 1 && pData.EmitAngle > 0)
                                     {
                                         float angle = UnityEngine.Random.Range(-pData.EmitAngle / 2f, pData.EmitAngle / 2f);
                                         emitDir = Quaternion.Euler(0, 0, angle) * emitDir;
                                     }
 
                                     cp.Velocity = emitDir * pData.PtVelocity * targetScale;
                                     cp.Accel = emitDir * pData.PtAccel * targetScale;
                                     cp.DropY = 0f;
                                     cp.DropVel = 0f;
                                     cp.CreatePoint = vp.Obj.transform.position;
                                     cp.LastTexIdx = -1;
 
                                     cp.Obj.SetActive(true);
 
                                     spawned++;
                                     if (spawned >= pData.NumCreate)
                                         break;
                                 }
                             }
                         }
 
                         // 2. Updating & Rendering
                         foreach (var cp in vp.CustomParticles)
                         {
                             if (!cp.IsAlive) continue;
 
                             cp.CurrLife += Time.deltaTime;
                             if (cp.CurrLife >= cp.Life)
                             {
                                 cp.IsAlive = false;
                                 cp.Obj.SetActive(false);
                                 continue;
                             }
 
                             // Physics
                             cp.LcPos += cp.Velocity * Time.deltaTime;
                             cp.Velocity += cp.Accel * Time.deltaTime;
 
                             // Gravity drop
                             cp.DropVel += pData.PtGravity * Time.deltaTime;
                             cp.DropY += cp.DropVel * Time.deltaTime;
 
                             // Position
                             Vector3 wdPos = cp.CreatePoint + cp.LcPos;
                             wdPos.y -= cp.DropY;
                             cp.Obj.transform.position = wdPos;
 
                             // Billboard rotation to face camera
                             if (cam != null)
                             {
                                 cp.Obj.transform.LookAt(cam.transform.position);
                                 if (pData.PtRotVelocity != 0f)
                                 {
                                     float zAngle = cp.CurrLife * pData.PtRotVelocity * Mathf.Rad2Deg;
                                     cp.Obj.transform.Rotate(0, 0, zAngle, Space.Self);
                                 }
                             }
 
                             // Size
                             cp.Obj.transform.localScale = new Vector3(cp.Size, cp.Size, 1f);
 
                             // Texture animation
                             int totalFrames = vp.Data.NumTextures;
                             int texIdx = 0;
                             if (totalFrames > 1)
                             {
                                 float fps = vp.Data.TextureFPS > 0 ? vp.Data.TextureFPS : 30f;
                                 texIdx = Mathf.FloorToInt(cp.CurrLife * fps) % totalFrames;
                             }
 
                             if (texIdx != cp.LastTexIdx)
                             {
                                 cp.LastTexIdx = texIdx;
                                 string texKey = vp.Data.TextureName.Replace('\\', '/') + texIdx.ToString("D4") + ".dxt";
                                 var tex = KOFXManager.Instance?.Renderer?.LoadTexture(texKey);
                                 if (tex != null)
                                     cp.Renderer.sharedMaterial.mainTexture = tex;
                             }
 
                             // Color
                             Color col = Color.white;
                             if (pData.ChangeColor && pData.ColorKeys != null && pData.ColorKeys.Length > 0)
                             {
                                 int idx = Mathf.FloorToInt(cp.CurrLife * pData.ColorKeys.Length / cp.Life);
                                 if (idx >= pData.ColorKeys.Length) idx = pData.ColorKeys.Length - 1;
                                 uint argb = pData.ColorKeys[idx];
                                 float a = ((argb >> 24) & 0xFF) / 255f;
                                 float r = ((argb >> 16) & 0xFF) / 255f;
                                 float g = ((argb >> 8) & 0xFF) / 255f;
                                 float b = (argb & 0xFF) / 255f;
                                 col = new Color(r, g, b, a);
                             }
                             else
                             {
                                 float alpha = 1f;
                                 if (vp.Data.FadeIn > 0 && cp.CurrLife <= vp.Data.FadeIn)
                                     alpha = cp.CurrLife / vp.Data.FadeIn;
                                 col = new Color(1, 1, 1, alpha);
                             }
                             cp.Renderer.sharedMaterial.color = col;
                         }
                     }
                 }
             }
         }
 
         private void OnDestroy()
         {
             if (_glowPass1 != null) Destroy(_glowPass1);
             if (_glowPass2 != null) Destroy(_glowPass2);
             if (_glowPass3 != null) Destroy(_glowPass3);
 
             if (_glowMaterial1 != null) Destroy(_glowMaterial1);
             if (_glowMaterial2 != null) Destroy(_glowMaterial2);
             if (_glowMaterial3 != null) Destroy(_glowMaterial3);
 
             foreach (var tail in _tailInstances)
             {
                 if (tail != null) Destroy(tail);
             }
             _tailInstances.Clear();
             _tailVisualParts.Clear();
 
             if (_quadMesh != null) Destroy(_quadMesh);
         }
     }
 }