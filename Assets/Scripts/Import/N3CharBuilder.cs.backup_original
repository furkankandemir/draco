using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using EntropyOnline.World;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO v1.298 Karakter Model Builder
    /// 
    /// N3ChrImporter (skeleton) + N3CPartImporter (skinned mesh + bone weights)
    /// verilerini birleştirerek Unity SkinnedMeshRenderer oluşturur.
    /// 
    /// Pipeline:
    ///   1. .n3chr → skeleton joint hierarchy
    ///   2. .n3cpart → material + texture + skins referansı
    ///   3. .n3cskins → 4x LOD skinned mesh (vertex + normal + UV + bone weights)
    ///   4. Unity: SkinnedMeshRenderer + BoneWeight + bindposes
    /// </summary>
    public static class N3CharBuilder
    {
        /// <summary>
        /// .n3chr dosyasından tam karakter modeli oluşturur.
        /// </summary>
        /// <param name="chrPath">.n3chr dosya yolu</param>
                /// <returns>Karakter root GameObject (skeleton + skinned meshes)</returns>
        public static GameObject Build(string chrPath)
        {
            var chrData = N3ChrImporter.Load(chrPath);
            if (chrData == null) return null;

            string chrFileName = Path.GetFileName(chrPath);
            var root = new GameObject(chrData.Name ?? chrFileName);

            // ============================================
            // 1. Skeleton joint hierarchy oluştur
            // ============================================
            var jointTransforms = new List<Transform>();
            var jointNodes = new List<N3ChrImporter.JointNode>();

            if (chrData.RootJoint != null)
            {
                BuildJointTransforms(root.transform, chrData.RootJoint,
                    jointTransforms, jointNodes);
            }



            // ============================================
            /// <summary>Her part için skinned mesh oluştur
            // ============================================
            int partsLoaded = 0;
            int meshesCreated = 0;

            foreach (string partFileName in chrData.PartFileNames)
            {
                string partPath = FindAssetFile(partFileName);
                if (partPath == null) continue;

                var partData = N3CPartImporter.LoadCPart(partPath);
                if (partData == null) continue;
                partsLoaded++;

                // En iyi LOD'daki skin mesh'i al (LOD 0)
                var skinLOD = GetBestLOD(partData);
                if (skinLOD == null || skinLOD.FaceCount <= 0) continue;

                // Part GameObject
                var partObj = new GameObject(partData.Name ?? Path.GetFileNameWithoutExtension(partFileName));
                partObj.transform.SetParent(root.transform);
                partObj.transform.localPosition = Vector3.zero;
                partObj.transform.localRotation = Quaternion.identity;

                bool hasSkinning = (skinLOD.SkinVertices != null &&
                                    jointTransforms.Count > 0);

                if (hasSkinning)
                {
                    // Open-KO birebir: CN3Chr::Init + BuildMesh pipeline
                    // SkinnedMeshRenderer — doğrudan SkinLODData'dan
                    var smr = partObj.AddComponent<SkinnedMeshRenderer>();
                    var skinnedMesh = BuildSkinnedMeshFromSkin(skinLOD,
                        jointTransforms, root.transform);
                    smr.sharedMesh = skinnedMesh;
                    smr.bones = jointTransforms.ToArray();
                    smr.rootBone = jointTransforms.Count > 0 ? jointTransforms[0] : root.transform;
                    smr.material = BuildPartMaterial(partData);
                    smr.localBounds = skinnedMesh.bounds;
                    smr.updateWhenOffscreen = true;
                }
                else
                {
                    // Static mesh — bone weights yoksa MeshRenderer kullan
                    var mesh = N3CPartImporter.CreateUnityMesh(skinLOD);
                    if (mesh == null) continue;
                    var mf = partObj.AddComponent<MeshFilter>();
                    mf.mesh = mesh;
                    var mr = partObj.AddComponent<MeshRenderer>();
                    mr.material = BuildPartMaterial(partData);
                }

                meshesCreated++;
            }

            // Part yoksa veya yüklenemezse — Open-KO'da placeholder yoktur, sadece log
            if (meshesCreated == 0 && jointTransforms.Count == 0)
            {
                Debug.LogWarning($"[N3CharBuilder] Model yüklenemedi: mesh ve joint yok");
            }

            // ============================================
            // 2b. Plug'ları yükle (.n3cplug silahlar/ekipman)
            // Open-KO birebir: CN3Chr::Load → plugCount, PlugSet(i, szPlugFN)
            // C++ GameProcCharacterSelect.cpp:528-529:
            //   m_pChrs[iPosIndex]->PlugSet(0, szPlug0FN);
            //   m_pChrs[iPosIndex]->PlugSet(1, szPlug1FN);
            // .n3chr dosyasındaki PlugFileNames sırasıyla:
            //   index 0 → PLUG_POS_RIGHTHAND (sağ el)
            //   index 1 → PLUG_POS_LEFTHAND (sol el)
            // ============================================
            int plugsLoaded = 0;
            for (int i = 0; i < chrData.PlugFileNames.Count; i++)
            {
                string plugFN = chrData.PlugFileNames[i];
                if (string.IsNullOrEmpty(plugFN))
                    continue;
                
                // C++ birebir: PlugSet(plugIndex, plugFileName)
                // plugTag farklı olmalı ki her plug ayrı GameObject olsun
                string plugTag = $"PLUG_{i}";
                var plugObj = PlugSet(root, plugFN, -1, plugTag);
                if (plugObj != null)
                {
                    plugsLoaded++;
                }
            }

            // ============================================
            // 3. Animation yükle ve clip'leri bağla
            // ============================================
            int animCount = 0;

            if (chrData.RootJoint != null && !string.IsNullOrEmpty(chrData.AniCtrlFileName))
            {
                string animPath = FindAssetFile(chrData.AniCtrlFileName);
                if (animPath != null)
                {
                    var animCtrl = N3CPartImporter.LoadAnimControl(animPath);
                    if (animCtrl != null && animCtrl.Animations.Count > 0)
                    {
                        // Joint path'leri oluştur (root bone'un adı ile başlar)
                        var jointPaths = N3AnimBuilder.BuildJointPaths(chrData.RootJoint);

                        // AnimationClip'leri oluştur
                        var clips = N3AnimBuilder.BuildClips(chrData, animCtrl, jointPaths);

                        if (clips.Count > 0)
                        {
                            // Legacy Animation component ekle
                            var anim = root.AddComponent<Animation>();

                            foreach (var clip in clips)
                            {
                                anim.AddClip(clip, clip.name);
                            }

                            // İlk clip'i varsayılan yap
                            anim.clip = clips[0];
                            animCount = clips.Count;

                            // Open-KO birebir: CN3AnimControl::m_Datas sırasını koru
                            AddAnimRegistry(root, clips, animCtrl);
                        }
                    }
                }
            }


            return root;
        }

        /// <summary>
        /// Open-KO birebir: CGameProcCharacterSelect::AddChr (cpp:526-529)
        /// ChrSelect'te .n3chr dosyası YÜKLENMEZ — C++ ayrı ayrı set eder:
        ///   m_pChrs[i]->JointSet(szJointFN);    // skeleton
        ///   m_pChrs[i]->AniCtrlSet(szAniFN);    // animasyon
        ///   m_pChrs[i]->PlugSet(0, szPlug0FN);  // sağ el silah
        ///   m_pChrs[i]->PlugSet(1, szPlug1FN);  // sol el silah/sadak
        /// </summary>
        public static GameObject BuildChrSelect(
            string jointFN, string aniFN,
            string plug0FN, string plug1FN,
            int joint0Override = -1, int joint1Override = -1)
        {
            // 1. Joint dosyasını yükle (skeleton)
            string jointPath = FindAssetFile(jointFN);
            if (jointPath == null)
            {
                Debug.LogWarning($"[N3CharBuilder] Joint dosyası bulunamadı: {jointFN}");
                return null;
            }
            
            var rootJoint = N3ChrImporter.LoadJointFile(jointPath);
            if (rootJoint == null)
            {
                Debug.LogWarning($"[N3CharBuilder] Joint parse edilemedi: {jointFN}");
                return null;
            }
            
            string chrName = Path.GetFileNameWithoutExtension(jointFN);
            var root = new GameObject(chrName);
            
            // Joint hierarchy oluştur
            var jointTransforms = new List<Transform>();
            var jointNodes = new List<N3ChrImporter.JointNode>();
            BuildJointTransforms(root.transform, rootJoint, jointTransforms, jointNodes);
            

            
            // 2. Animasyon yükle
            int animCount = 0;
            if (!string.IsNullOrEmpty(aniFN))
            {
                string animPath = FindAssetFile(aniFN);
                if (animPath != null)
                {
                    var animCtrl = N3CPartImporter.LoadAnimControl(animPath);
                    if (animCtrl != null && animCtrl.Animations.Count > 0)
                    {
                        // ChrData oluştur (AnimBuilder için)
                        var chrData = new N3ChrImporter.N3ChrData();
                        chrData.RootJoint = rootJoint;
                        chrData.TotalJointCount = jointTransforms.Count;
                        
                        var jointPaths = N3AnimBuilder.BuildJointPaths(rootJoint);
                        var clips = N3AnimBuilder.BuildClips(chrData, animCtrl, jointPaths);
                        
                        if (clips.Count > 0)
                        {
                            var anim = root.AddComponent<Animation>();
                            anim.playAutomatically = true;
                            anim.animatePhysics = false;

                            foreach (var clip in clips)
                                anim.AddClip(clip, clip.name);

                            // breath (idle) animasyonunu bul — BuildWithExternalParts ile aynı mantık
                            AnimationClip defaultClip = null;
                            foreach (var clip in clips)
                            {
                                string lname = clip.name.ToLower();
                                if (lname == "breath") { defaultClip = clip; break; }
                            }
                            if (defaultClip == null)
                            {
                                foreach (var clip in clips)
                                {
                                    string lname = clip.name.ToLower();
                                    if (lname.Contains("idle") || lname.Contains("wait") || lname.Contains("stand"))
                                    { defaultClip = clip; break; }
                                }
                            }

                            anim.clip = defaultClip ?? clips[0];
                            anim.clip.wrapMode = WrapMode.ClampForever;
                            anim.wrapMode = WrapMode.ClampForever;
                            anim.Play(anim.clip.name);
                            animCount = clips.Count;
                            
                            AddAnimRegistry(root, clips, animCtrl);
                        }
                    }
                }
            }
            
            // 3. Plug'ları yükle — cpp:528-529
            int plugsLoaded = 0;
            if (!string.IsNullOrEmpty(plug0FN))
            {
                var p0 = PlugSet(root, plug0FN, joint0Override, "PLUG_0");
                if (p0 != null) plugsLoaded++;
            }
            if (!string.IsNullOrEmpty(plug1FN))
            {
                var p1 = PlugSet(root, plug1FN, joint1Override, "PLUG_1");
                if (p1 != null) plugsLoaded++;
            }
            
            
            return root;
        }

        /// <summary>
        /// Skeleton'lu karakter oluşturup harici part dosyalarını skinned mesh olarak ekler.
        /// ChrSelect n3chr dosyaları partCount=0 olduğu için bu metot kullanılır.
        /// </summary>
        public static GameObject BuildWithExternalParts(
            string chrPath, string[] externalPartPaths)
        {
            var chrData = N3ChrImporter.Load(chrPath);
            if (chrData == null)
            {
                Debug.LogWarning($"[N3CharBuilder] chrData null: {chrPath}");
                return null;
            }

            // N3ChrData'yı cache'le — üst gövde animasyonu (JointPartStarts) için gerekli.
            // Sadece henüz set edilmemişse cache'le — remote player çağrısı self player'ın
            // chrData'sını ezmemeli. WorldBuilder.ReplacePlayerModel ilk çağrıdır.
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null && gm.PlayerChrData == null)
                gm.PlayerChrData = chrData;

            string chrFileName = Path.GetFileName(chrPath);
            var root = new GameObject(chrData.Name ?? chrFileName);

            // 1. Skeleton joint hierarchy oluştur
            var jointTransforms = new List<Transform>();
            var jointNodes = new List<N3ChrImporter.JointNode>();

            if (chrData.RootJoint != null)
            {
                BuildJointTransforms(root.transform, chrData.RootJoint,
                    jointTransforms, jointNodes);
            }

            // 2. Harici part'ları skinned mesh olarak ekle
            int meshesCreated = 0;
            foreach (string partRelPath in externalPartPaths)
            {
                string partPath = FindAssetFile(partRelPath);
                if (partPath == null)
                {
                    Debug.LogWarning($"[N3CharBuilder] Part bulunamadı: {partRelPath}");
                    continue;
                }

                var partData = N3CPartImporter.LoadCPart(partPath);
                if (partData == null) continue;

                var skinLOD = GetBestLOD(partData);
                if (skinLOD == null || skinLOD.FaceCount <= 0) continue;

                string partName = partData.Name ?? Path.GetFileNameWithoutExtension(partRelPath);
                var partObj = new GameObject(partName);
                partObj.transform.SetParent(root.transform);
                partObj.transform.localPosition = Vector3.zero;
                partObj.transform.localRotation = Quaternion.identity;

                bool hasSkinning = (skinLOD.SkinVertices != null &&
                                    jointTransforms.Count > 0);


                if (hasSkinning)
                {
                    var smr = partObj.AddComponent<SkinnedMeshRenderer>();
                    var skinnedMesh = BuildSkinnedMeshFromSkin(skinLOD,
                        jointTransforms, root.transform);
                    smr.sharedMesh = skinnedMesh;
                    smr.bones = jointTransforms.ToArray();
                    smr.rootBone = jointTransforms[0];
                    smr.material = BuildPartMaterial(partData);
                    smr.localBounds = skinnedMesh.bounds;
                    smr.updateWhenOffscreen = true;
                }
                else
                {
                    var mesh = N3CPartImporter.CreateUnityMesh(skinLOD);
                    if (mesh == null) continue;
                    var mf = partObj.AddComponent<MeshFilter>();
                    mf.mesh = mesh;
                    var mr = partObj.AddComponent<MeshRenderer>();
                    mr.material = BuildPartMaterial(partData);
                }

                meshesCreated++;
            }

            // 3. Animation yükle — Chr/ dizinindeki tam anim dosyasını tercih et
            int animCount = 0;
            if (chrData.RootJoint != null && !string.IsNullOrEmpty(chrData.AniCtrlFileName))
            {
                string animFileName = chrData.AniCtrlFileName;
                string baseName = Path.GetFileNameWithoutExtension(animFileName);
                
                // Sınıf sonekini kaldır: upc_el_rm_wa → upc_el_rm
                string[] classSuffixes = { "_wa", "_rog", "_pri", "_wiz", "_ma" };
                foreach (string suffix in classSuffixes)
                {
                    if (baseName.EndsWith(suffix))
                    {
                        baseName = baseName.Substring(0, baseName.Length - suffix.Length);
                        break;
                    }
                }

                // Chr/ dizinindeki tam anim dosyasını ara (136 animasyon)
                string chrAnimPath = Path.Combine("Chr", baseName + ".n3anim");
                string animPath = null;

                if (KOBinaryProvider.Exists(chrAnimPath))
                {
                    animPath = chrAnimPath;
                }
                else
                {
                    // Fallback: orijinal referansı dene
                    animPath = FindAssetFile(animFileName);
                }

                if (animPath != null)
                {
                    var animCtrl = N3CPartImporter.LoadAnimControl(animPath);
                    if (animCtrl != null && animCtrl.Animations.Count > 0)
                    {
                        var jointPaths = N3AnimBuilder.BuildJointPaths(chrData.RootJoint);
                        
                        // Diagnostic: Joint hierarchy'yi doğrula
                        LogTransformHierarchy(root.transform, 0, 3);
                        
                        var clips = N3AnimBuilder.BuildClips(chrData, animCtrl, jointPaths);

                        if (clips.Count > 0)
                        {
                            var anim = root.AddComponent<Animation>();
                            anim.playAutomatically = true;
                            anim.animatePhysics = false;
                            
                            foreach (var clip in clips)
                                anim.AddClip(clip, clip.name);

                            // breath clip'i ara (idle animasyonu)
                            AnimationClip defaultClip = null;
                            foreach (var clip in clips)
                            {
                                string lname = clip.name.ToLower();
                                if (lname == "breath") { defaultClip = clip; break; }
                            }
                            if (defaultClip == null)
                            {
                                foreach (var clip in clips)
                                {
                                    string lname = clip.name.ToLower();
                                    if (lname.Contains("idle") || lname.Contains("wait") || lname.Contains("stand"))
                                    { defaultClip = clip; break; }
                                }
                            }
                            
                            anim.clip = defaultClip ?? clips[0];
                            anim.clip.wrapMode = WrapMode.Loop;
                            anim.wrapMode = WrapMode.Loop;
                            anim.Play(anim.clip.name);
                            animCount = clips.Count;

                            // Sıralı clip listesini GameManager'a kaydet
                            // KOAnimResolver bu listeyi kullanarak doğru index→clip eşlemesi yapar
                            if (gm != null)
                                gm.PlayerAnimClips = clips;

                            AddAnimRegistry(root, clips, animCtrl);

                            // Debug: Animation state — Warning seviyesi (Console'da görünsün)
                            Debug.LogWarning($"[N3CharBuilder-ANIM] isPlaying={anim.isPlaying}, " +
                                      $"clip='{anim.clip.name}', duration={anim.clip.length:F3}s, " +
                                      $"wrapMode={anim.clip.wrapMode}, legacy={anim.clip.legacy}");

                            // İlk birkaç clip adını logla
                            var names = new List<string>();
                            foreach (var clip in clips)
                            {
                                if (names.Count < 8) names.Add($"{clip.name}({clip.length:F2}s)");
                            }
                            Debug.LogWarning($"[N3CharBuilder-ANIM] Clips: {string.Join(", ", names)}... (toplam {animCount})");
                        }
                    }
                }
            }


            return root;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::InitChr (PlayerBase.cpp:2025-2055) — else dalı
        /// szChrFN boşsa:
        ///   m_Chr.JointSet(pTbl->szJointFN);   // .n3joint skeleton
        ///   m_Chr.AniCtrlSet(pTbl->szAniFN);   // .n3anim animasyonlar
        /// Bu metot .n3joint dosyasından skeleton oluşturur + .n3anim ile animasyonları ekler.
        /// Part'lar sonradan PartSet ile eklenir (BuildWithExternalParts gibi boş başlar).
        /// </summary>
        public static GameObject BuildWithJointAndAnim(
            string jointPath, string animFilePath)
        {
            if (jointPath == null || !KOBinaryProvider.Exists(jointPath))
            {
                Debug.LogWarning($"[N3CharBuilder] Joint dosyası bulunamadı: {jointPath}");
                return null;
            }

            // C++ birebir: m_Chr.JointSet(szJointFN) — skeleton'u .n3joint'ten yükle
            var chrData = N3ChrImporter.LoadJointOnly(jointPath);
            if (chrData == null)
            {
                Debug.LogWarning($"[N3CharBuilder] Joint parse edilemedi: {jointPath}");
                return null;
            }

            // PlayerChrData cache — sadece henüz set edilmemişse
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null && gm.PlayerChrData == null)
                gm.PlayerChrData = chrData;

            string jointFileName = Path.GetFileName(jointPath);
            var root = new GameObject(chrData.Name ?? jointFileName);

            // 1. Skeleton joint hierarchy
            var jointTransforms = new List<Transform>();
            var jointNodes = new List<N3ChrImporter.JointNode>();

            if (chrData.RootJoint != null)
            {
                BuildJointTransforms(root.transform, chrData.RootJoint,
                    jointTransforms, jointNodes);
            }


            // 2. C++ birebir: m_Chr.AniCtrlSet(szAniFN) — animasyonları yükle
            int animCount = 0;
            if (chrData.RootJoint != null && !string.IsNullOrEmpty(animFilePath))
            {
                string animPath = FindAssetFile(animFilePath);
                
                // Tam anim dosyasını Chr/ dizininde ara (BuildWithExternalParts ile aynı mantık)
                if (animPath == null)
                {
                    string baseName = Path.GetFileNameWithoutExtension(animFilePath);
                    string[] classSuffixes = { "_wa", "_rog", "_pri", "_wiz", "_ma" };
                    foreach (string suffix in classSuffixes)
                    {
                        if (baseName.EndsWith(suffix))
                        {
                            baseName = baseName.Substring(0, baseName.Length - suffix.Length);
                            break;
                        }
                    }
                    string chrAnimPath = Path.Combine("Chr", baseName + ".n3anim");
                    if (KOBinaryProvider.Exists(chrAnimPath))
                        animPath = chrAnimPath;
                }

                if (animPath != null)
                {
                    var animCtrl = N3CPartImporter.LoadAnimControl(animPath);
                    if (animCtrl != null && animCtrl.Animations.Count > 0)
                    {
                        var jointPaths = N3AnimBuilder.BuildJointPaths(chrData.RootJoint);
                        var clips = N3AnimBuilder.BuildClips(chrData, animCtrl, jointPaths);

                        if (clips.Count > 0)
                        {
                            var anim = root.AddComponent<Animation>();
                            anim.playAutomatically = true;
                            anim.animatePhysics = false;

                            foreach (var clip in clips)
                                anim.AddClip(clip, clip.name);

                            // breath clip'i ara (idle animasyonu)
                            AnimationClip defaultClip = null;
                            foreach (var clip in clips)
                            {
                                string lname = clip.name.ToLower();
                                if (lname == "breath") { defaultClip = clip; break; }
                            }
                            if (defaultClip == null)
                            {
                                foreach (var clip in clips)
                                {
                                    string lname = clip.name.ToLower();
                                    if (lname.Contains("idle") || lname.Contains("wait") || lname.Contains("stand"))
                                    { defaultClip = clip; break; }
                                }
                            }

                            anim.clip = defaultClip ?? clips[0];
                            anim.clip.wrapMode = WrapMode.Loop;
                            anim.wrapMode = WrapMode.Loop;
                            anim.Play(anim.clip.name);
                            animCount = clips.Count;

                            AddAnimRegistry(root, clips, animCtrl);
                        }
                    }
                }
            }


            return root;
        }
        /// NPC_Looks.tbl verilerinden NPC modeli oluşturur.
        /// Open-KO birebir: MsgRecv_NPCIn → s_pTbl_NPC_Looks.Find(PID) → InitChr(pLooks)
        /// .n3chr gerektirmez — doğrudan joint + anim + part dosyalarını kullanır.
        /// </summary>
        /// <param name="looks">NPC_Looks.tbl'den okunan NPC bilgisi</param>
                /// <returns>NPC root GameObject</returns>
        public static GameObject BuildFromLooks(
            KOImport.NpcLooksTblParser.NpcLooksEntry looks)
        {
            if (looks == null) return null;

            string overridePrefabPath = KONpcOverrideManager.GetOverridePrefabPath(looks.Id);
            if (!string.IsNullOrEmpty(overridePrefabPath))
            {
                GameObject prefab = Resources.Load<GameObject>(overridePrefabPath);
                if (prefab != null)
                {
                    GameObject overrideObj = UnityEngine.Object.Instantiate(prefab);
                    overrideObj.name = looks.Name ?? $"NPC_{looks.Id}_override";
                    
                    // Keep animator enabled for custom creatures
                    Animator animator = overrideObj.GetComponentInChildren<Animator>();
                    if (animator != null)
                    {
                        animator.enabled = true;
                    }

                    // Apply offset/scale overrides
                    KONpcOverrideManager.ApplyNpcOverrides(looks.Id, overrideObj);

                    return overrideObj;
                }
                else
                {
                    Debug.LogError($"[N3CharBuilder] NPC/Monster Override prefab found in config but missing in Resources: {overridePrefabPath}");
                }
            }

            // ChrFile varsa doğrudan Build() kullan
            if (!string.IsNullOrEmpty(looks.ChrFile))
            {
                string chrPath = FindAssetFile(looks.ChrFile);
                if (chrPath != null)
                    return Build(chrPath);
            }

            // Part-based NPC: joint + anim + parts ayrı ayrı
            string jointPath = FindAssetFile(looks.JointFile);
            if (jointPath == null)
            {
                Debug.LogWarning($"[N3CharBuilder] NPC joint bulunamadı: {looks.JointFile}");
                return null;
            }

            // Joint dosyasını N3ChrImporter ile oku (skeleton)
            // Joint dosyası .n3joint formatında — N3ChrImporter ile skeleton'u parse et
            var chrData = N3ChrImporter.LoadJointOnly(jointPath);
            if (chrData == null)
            {
                Debug.LogWarning($"[N3CharBuilder] NPC joint parse edilemedi: {jointPath}");
                return null;
            }

            string npcName = looks.Name ?? $"NPC_{looks.Id}";
            var root = new GameObject(npcName);

            // 1. Skeleton joint hierarchy
            var jointTransforms = new List<Transform>();
            var jointNodes = new List<N3ChrImporter.JointNode>();

            if (chrData.RootJoint != null)
            {
                BuildJointTransforms(root.transform, chrData.RootJoint,
                    jointTransforms, jointNodes);
            }

            // 2. Part mesh'lerini yükle
            int meshesCreated = 0;
            // Open-KO: PART_POS_UPPER(0), LOWER(1), FACE(2), HANDS(3), FEET(4), HAIR(5)
            for (int i = 0; i < 10; i++)
            {
                string partFile = (i < looks.PartFiles.Length) ? looks.PartFiles[i] : null;
                if (string.IsNullOrEmpty(partFile)) continue;

                string partPath = FindAssetFile(partFile);
                if (partPath == null) continue;

                var partData = N3CPartImporter.LoadCPart(partPath);
                if (partData == null) continue;

                var skinLOD = GetBestLOD(partData);
                if (skinLOD == null || skinLOD.FaceCount <= 0) continue;

                string partName = partData.Name ?? Path.GetFileNameWithoutExtension(partFile);
                var partObj = new GameObject(partName);
                partObj.transform.SetParent(root.transform);
                partObj.transform.localPosition = Vector3.zero;
                partObj.transform.localRotation = Quaternion.identity;

                bool hasSkinning = (skinLOD.SkinVertices != null && jointTransforms.Count > 0);

                if (hasSkinning)
                {
                    var smr = partObj.AddComponent<SkinnedMeshRenderer>();
                    var skinnedMesh = BuildSkinnedMeshFromSkin(skinLOD,
                        jointTransforms, root.transform);
                    smr.sharedMesh = skinnedMesh;
                    smr.bones = jointTransforms.ToArray();
                    smr.rootBone = jointTransforms[0];
                    smr.material = BuildPartMaterial(partData);
                    smr.localBounds = skinnedMesh.bounds;
                    smr.updateWhenOffscreen = true;
                }
                else
                {
                    var mesh = N3CPartImporter.CreateUnityMesh(skinLOD);
                    if (mesh == null) continue;
                    var mf = partObj.AddComponent<MeshFilter>();
                    mf.mesh = mesh;
                    var mr = partObj.AddComponent<MeshRenderer>();
                    mr.material = BuildPartMaterial(partData);
                }

                meshesCreated++;
            }

            // 3. Animation yükle
            int animCount = 0;
            if (chrData.RootJoint != null && !string.IsNullOrEmpty(looks.AniFile))
            {
                string animPath = FindAssetFile(looks.AniFile);
                if (animPath != null)
                {
                    var animCtrl = N3CPartImporter.LoadAnimControl(animPath);
                    if (animCtrl != null && animCtrl.Animations.Count > 0)
                    {
                        var jointPaths = N3AnimBuilder.BuildJointPaths(chrData.RootJoint);
                        var clips = N3AnimBuilder.BuildClips(chrData, animCtrl, jointPaths);

                        if (clips.Count > 0)
                        {
                            var anim = root.AddComponent<Animation>();
                            anim.playAutomatically = true;

                            foreach (var clip in clips)
                                anim.AddClip(clip, clip.name);

                            // breath (idle) animasyonunu bul
                            AnimationClip defaultClip = null;
                            foreach (var clip in clips)
                            {
                                string lname = clip.name.ToLower();
                                if (lname == "breath") { defaultClip = clip; break; }
                            }
                            if (defaultClip == null)
                            {
                                foreach (var clip in clips)
                                {
                                    string lname = clip.name.ToLower();
                                    if (lname.Contains("idle") || lname.Contains("wait") || lname.Contains("stand"))
                                    { defaultClip = clip; break; }
                                }
                            }

                            anim.clip = defaultClip ?? clips[0];
                            anim.clip.wrapMode = WrapMode.Loop;
                            anim.wrapMode = WrapMode.Loop;
                            anim.Play(anim.clip.name);
                            // Open-KO birebir: CN3AnimControl::m_Datas sırasını koru
                            // C++ AniCurSet(iAni) → m_pAniCtrlRef->DataGet(iAni)
                            AddAnimRegistry(root, clips, animCtrl);

                            animCount = clips.Count;
                        }
                    }
                }
            }

            // Part yoksa veya yüklenemezse — Open-KO'da placeholder yoktur, sadece log
            if (meshesCreated == 0)
            {
                Debug.LogWarning($"[N3CharBuilder] NPC model yüklenemedi: mesh yok (PID={looks.Id})");
            }


            return root;
        }

        #region Skeleton Building

        /// <summary>
        /// Joint ağacından flat Transform listesi oluşturur (bone index sırası korunur).
        /// 
        /// Open-KO birebir: CN3Joint::Tick(0) → ReCalcMatrix()
        ///   m_KeyPos.DataGet(0, m_vPos);
        ///   m_KeyRot.DataGet(0, m_qRot);
        ///   m_KeyOrient.DataGet(0, m_qOrient);
        ///   if (m_KeyOrient.Count() > 0)
        ///       m_Matrix = m_qRot * m_qOrient;
        ///   else
        ///       m_Matrix = m_qRot;
        ///   m_Matrix.PosSet(m_vPos);
        ///   if (m_pParent) m_Matrix *= m_pParent->m_Matrix;
        ///
        /// Bindpose hesaplaması için frame 0'daki position, rotation VE orient
        /// joint transform'larına uygulanmalıdır.
        /// </summary>
        private static void BuildJointTransforms(
            Transform parent,
            N3ChrImporter.JointNode joint,
            List<Transform> allJoints,
            List<N3ChrImporter.JointNode> allNodes)
        {
            var jointObj = new GameObject(
                string.IsNullOrEmpty(joint.Name) ? $"Bone_{joint.Index}" : joint.Name);
            var boneComp = jointObj.AddComponent<KOBone>();
            boneComp.Index = joint.Index;
            jointObj.transform.SetParent(parent);

            // Open-KO: Tick(0) → m_KeyPos.DataGet(0, m_vPos)
            // Frame 0'daki pozisyon (AnimKey varsa ilk keyframe, yoksa stored position)
            Vector3 pos = joint.Position;
            if (joint.KeyPos != null && joint.KeyPos.Count > 0 &&
                joint.KeyPos.VectorKeys != null && joint.KeyPos.VectorKeys.Length > 0)
            {
                pos = joint.KeyPos.VectorKeys[0];
            }
            jointObj.transform.localPosition = pos;

            // Open-KO: Tick(0) → m_KeyRot.DataGet(0, m_qRot)
            Quaternion rot = joint.Rotation;
            if (joint.KeyRot != null && joint.KeyRot.Count > 0 &&
                joint.KeyRot.QuatKeys != null && joint.KeyRot.QuatKeys.Length > 0)
            {
                rot = joint.KeyRot.QuatKeys[0];
            }

            // Open-KO: Tick(0) → m_KeyOrient.DataGet(0, m_qOrient)
            // ReCalcMatrix:365-368 — if (m_KeyOrient.Count() > 0) m_Matrix = m_qRot * m_qOrient
            if (joint.KeyOrient != null && joint.KeyOrient.Count > 0 &&
                joint.KeyOrient.QuatKeys != null && joint.KeyOrient.QuatKeys.Length > 0)
            {
                Quaternion orient = joint.KeyOrient.QuatKeys[0];
                rot = rot * orient; // N3Joint.cpp:366 birebir
            }

            // Sıfır quaternion kontrolü — geçersiz quaternion'u atla
            if (rot.w != 0 || rot.x != 0 || rot.y != 0 || rot.z != 0)
            {
                jointObj.transform.localRotation = rot;
            }

            // Cache startup Frame 0 values for procedural bindpose calculation
            boneComp.DefaultLocalPosition = pos;
            boneComp.DefaultLocalRotation = (rot.w != 0 || rot.x != 0 || rot.y != 0 || rot.z != 0) ? rot : Quaternion.identity;

            allJoints.Add(jointObj.transform);
            allNodes.Add(joint);

            foreach (var child in joint.Children)
            {
                BuildJointTransforms(jointObj.transform, child, allJoints, allNodes);
            }
        }

        #endregion

        #region Skinned Mesh Building

        /// <summary>
        /// Open-KO birebir: CN3Chr::Init() + CN3Chr::BuildMesh() pipeline.
        /// 
        /// SkinLODData'dan doğrudan indexed Unity skinned mesh oluşturur.
        /// Open-KO'da CN3Skin (CN3IMesh) şu yapıyı kullanır:
        ///   - Positions[nVC]: __VertexXyzNormal — position + normal
        ///   - VtxIndices[nFC*3]: uint16 — face vertex index'leri
        ///   - UVs[nUVC*2]: float — UV koordinatları
        ///   - UVIndices[nFC*3]: uint16 — face UV index'leri
        ///   - SkinVertices[nVC]: __VertexSkinned — vOrigin + nAffect + joints + weights
        /// 
        /// Unity indexed mesh'e dönüşüm:
        ///   Vertex + UV index çiftleri unique vertex'lere eşlenir.
        ///   Her unique vertex: SkinVertex.Origin (bind pose), normal, UV, BoneWeight taşır.
        /// 
        /// Bindpose: Open-KO N3Chr.cpp:2076 birebir:
        ///   m_MtxInverses[i] = m_JointRefs[i]->m_Matrix.Inverse()
        ///   Unity karşılığı: bones[i].worldToLocalMatrix * rootTransform.localToWorldMatrix
        /// </summary>
        private static Mesh BuildSkinnedMeshFromSkin(
            N3CPartImporter.SkinLODData skin,
            List<Transform> bones,
            Transform rootTransform)
        {
            if (skin == null || skin.FaceCount <= 0 || skin.VertexCount <= 0)
                return null;

            int triCount = skin.FaceCount * 3;
            bool hasUV = (skin.UVCount > 0 && skin.UVs != null && skin.UVIndices != null);

            // ============================================
            // Step 1: Unique vertex'leri oluştur
            // Open-KO'da vertex index ve UV index AYRI.
            // Unity'de unified vertex formatı gerekli.
            // (vtxIdx, uvIdx) çifti → unique vertex index
            // ============================================
            var vertexMap = new Dictionary<long, int>();
            var outPositions = new List<Vector3>();
            var outNormals = new List<Vector3>();
            var outUVs = new List<Vector2>();
            var outWeights = new List<BoneWeight>();
            var outIndices = new int[triCount];

            for (int i = 0; i < triCount; i++)
            {
                int vIdx = (skin.VtxIndices != null && i < skin.VtxIndices.Length)
                    ? skin.VtxIndices[i] : 0;
                int uvIdx = (hasUV && i < skin.UVIndices.Length)
                    ? skin.UVIndices[i] : -1;

                // Unique key: vertex index + UV index
                long key = ((long)vIdx << 32) | (uint)(uvIdx >= 0 ? uvIdx : 0);

                if (!vertexMap.TryGetValue(key, out int unifiedIdx))
                {
                    unifiedIdx = outPositions.Count;
                    vertexMap[key] = unifiedIdx;

                    // Position: SkinVertex.Origin (bind pose pozisyonu)
                    // Open-KO: CN3Chr::BuildMesh — vOrigin kullanılır
                    if (skin.SkinVertices != null && vIdx >= 0 && vIdx < skin.SkinVertices.Length)
                    {
                        outPositions.Add(skin.SkinVertices[vIdx].Origin);
                    }
                    else if (vIdx >= 0 && vIdx < skin.VertexCount)
                    {
                        outPositions.Add(skin.Positions[vIdx]);
                    }
                    else
                    {
                        outPositions.Add(Vector3.zero);
                    }

                    // Normal
                    if (vIdx >= 0 && vIdx < skin.VertexCount && skin.Normals != null)
                        outNormals.Add(skin.Normals[vIdx]);
                    else
                        outNormals.Add(Vector3.up);

                    // UV
                    // D3D→Unity: V koordinatı flip edilmeli.
                    // D3D: V=0 üstte, V=1 altta. Unity: V=0 altta, V=1 üstte.
                    // DxtTextureImporter flipY=true ile texture pixel'lerini flip ediyor,
                    // dolayısıyla UV V'sini de flip etmeliyiz (1-v).
                    if (hasUV && uvIdx >= 0 && uvIdx < skin.UVCount)
                        outUVs.Add(new Vector2(skin.UVs[uvIdx * 2], 1.0f - skin.UVs[uvIdx * 2 + 1]));
                    else
                        outUVs.Add(Vector2.zero);

                    // BoneWeight
                    if (skin.SkinVertices != null && vIdx >= 0 && vIdx < skin.SkinVertices.Length)
                        outWeights.Add(ConvertBoneWeight(skin.SkinVertices[vIdx], bones.Count));
                    else
                        outWeights.Add(DefaultBoneWeight());
                }

                outIndices[i] = unifiedIdx;
            }

            // ============================================
            // Step 2: Bind poses
            // Open-KO N3Chr.cpp:2071-2076 birebir:
            //   m_pRootJointRef->Tick(0);
            //   m_MtxInverses[i] = m_JointRefs[i]->m_Matrix.Inverse();
            // Unity: bones[i].worldToLocalMatrix — zaten inverse world matrix.
            // rootTransform.localToWorldMatrix ile çarparak model-space inverse elde ederiz.
            // ============================================
            // ============================================
            // Step 2: Bind poses (Procedural T-pose)
            // ============================================
            var bindPoses = new Matrix4x4[bones.Count];
            var modelMatrices = new Dictionary<int, Matrix4x4>();

            if (bones.Count > 0)
            {
                Transform hipsBone = null;
                foreach (var b in bones)
                {
                    var kb = b.GetComponent<KOBone>();
                    if (kb != null && kb.Index == 0)
                    {
                        hipsBone = b;
                        break;
                    }
                }
                if (hipsBone == null) hipsBone = bones[0];
                CalculateProceduralModelMatrices(hipsBone, Matrix4x4.identity, modelMatrices);
            }

            for (int i = 0; i < bones.Count; i++)
            {
                var kb = bones[i].GetComponent<KOBone>();
                if (kb != null && modelMatrices.TryGetValue(kb.Index, out Matrix4x4 modelMat))
                {
                    bindPoses[i] = modelMat.inverse;
                }
                else
                {
                    bindPoses[i] = bones[i].worldToLocalMatrix * rootTransform.localToWorldMatrix;
                }
            }

            // ============================================
            // Step 3: Unity Mesh oluştur
            // ============================================
            var mesh = new Mesh();
            mesh.name = "N3Skin";

            if (outPositions.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = outPositions.ToArray();
            mesh.normals = outNormals.ToArray();
            mesh.uv = outUVs.ToArray();
            mesh.triangles = outIndices;
            mesh.boneWeights = outWeights.ToArray();
            mesh.bindposes = bindPoses;
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Rest pose hiyerarşisini Unity matris önbelleğinden bağımsız, procedural olarak hesaplar.
        /// </summary>
        private static void CalculateProceduralModelMatrices(
            Transform current, Matrix4x4 parentMatrix, Dictionary<int, Matrix4x4> result)
        {
            var kb = current.GetComponent<KOBone>();
            if (kb == null) return;

            Matrix4x4 localMatrix = Matrix4x4.TRS(
                kb.DefaultLocalPosition, kb.DefaultLocalRotation, Vector3.one);
            
            Matrix4x4 modelMatrix = parentMatrix * localMatrix;
            result[kb.Index] = modelMatrix;

            for (int i = 0; i < current.childCount; i++)
            {
                CalculateProceduralModelMatrices(current.GetChild(i), modelMatrix, result);
            }
        }

        /// <summary>
        /// N3 SkinVertex → Unity BoneWeight dönüşümü.
        /// Unity max 4 bone destekler; KO'da nAffect>4 olabilir.
        /// </summary>
        private static BoneWeight ConvertBoneWeight(
            N3CPartImporter.SkinVertex sv, int maxBoneIndex)
        {
            var bw = new BoneWeight();

            if (sv == null || sv.AffectCount <= 0 || sv.JointIndices == null)
                return DefaultBoneWeight();

            // Collect all valid joints and weights
            var jointList = new List<(int index, float weight)>();
            for (int i = 0; i < sv.AffectCount; i++)
            {
                if (i >= sv.JointIndices.Length) break;
                
                int boneIdx = sv.JointIndices[i];
                float boneWeight = (sv.Weights != null && i < sv.Weights.Length) ? sv.Weights[i] : 0f;

                if (boneIdx >= 0 && boneIdx < maxBoneIndex && boneWeight > 0.0001f)
                {
                    jointList.Add((boneIdx, boneWeight));
                }
            }

            if (jointList.Count == 0)
                return DefaultBoneWeight();

            // Sort by weight descending so we prioritize the most influential bones
            jointList.Sort((a, b) => b.weight.CompareTo(a.weight));

            // Select up to 4 bones
            int count = Math.Min(jointList.Count, 4);

            // Normalize top 4 weights
            float totalWeight = 0f;
            for (int i = 0; i < count; i++)
            {
                totalWeight += jointList[i].weight;
            }

            if (totalWeight > 0.001f)
            {
                bw.boneIndex0 = jointList[0].index;
                bw.weight0 = jointList[0].weight / totalWeight;

                if (count >= 2)
                {
                    bw.boneIndex1 = jointList[1].index;
                    bw.weight1 = jointList[1].weight / totalWeight;
                }
                if (count >= 3)
                {
                    bw.boneIndex2 = jointList[2].index;
                    bw.weight2 = jointList[2].weight / totalWeight;
                }
                if (count >= 4)
                {
                    bw.boneIndex3 = jointList[3].index;
                    bw.weight3 = jointList[3].weight / totalWeight;
                }
            }
            else
            {
                bw.boneIndex0 = jointList[0].index;
                bw.weight0 = 1f;
            }

            return bw;
        }

        private static BoneWeight DefaultBoneWeight()
        {
            return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
        }

        #endregion

        #region Material Building

        /// <summary>
        /// Part'ın texture referansından Unity Material oluşturur.
        /// </summary>
        public static Material BuildPartMaterial(
            N3CPartImporter.CPartData part)
        {
            // === Resources.Load fallback: convert edilmiş texture'ı dene ===
            // Material'ı part'ın texture adından ara
            if (!string.IsNullOrEmpty(part.TextureFileName))
            {
                string texBaseName = Path.GetFileNameWithoutExtension(part.TextureFileName);
                // Önce convert edilmiş texture'ı KOTextures'tan dene
                string[] texSearchDirs = { "Chr", "Item", "ChrSelect", "Object", "DTex", "Misc" };
                Texture2D resTex = null;
                foreach (var dir in texSearchDirs)
                {
                    resTex = Resources.Load<Texture2D>($"KOTextures/{dir}/{texBaseName}");
                    if (resTex != null) break;
                }
                UnityEngine.Debug.Log($"[N3CharBuilder] BuildPartMaterial for part '{part.Name}' (texture: '{part.TextureFileName}'): resolved base name '{texBaseName}' to resource '{resTex?.name ?? "null"}'");
                if (resTex != null)
                {
                    // Convert edilmiş texture bulundu — material'ı bundan oluştur
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    var mat = new Material(shader);
                    if (mat.HasProperty("_BaseMap"))
                        mat.SetTexture("_BaseMap", resTex);
                    else if (mat.HasProperty("_Base_Map"))
                        mat.SetTexture("_Base_Map", resTex);
                    else
                        mat.mainTexture = resTex;
                     if (mat.HasProperty("_BaseColor"))
                         mat.SetColor("_BaseColor", Color.white);
                     else
                         mat.color = Color.white;

                      // Hair materials need Alpha Clipping to handle transparency cutouts correctly
                      if (texBaseName.ToLower().Contains("hair"))
                      {
                          if (mat.HasProperty("_AlphaClip"))
                          {
                              mat.SetFloat("_AlphaClip", 1f);
                              mat.EnableKeyword("_ALPHATEST_ON");
                          }
                          if (mat.HasProperty("_Cutoff"))
                          {
                              mat.SetFloat("_Cutoff", 0.05f); // Very low threshold to prevent holes in volumetric hair layers
                          }
                      }


                    if ((part.RenderFlags & 0x1) != 0)
                    {
                        if (mat.HasProperty("_Surface"))
                        {
                            mat.SetFloat("_Surface", 1);
                            mat.SetFloat("_Blend", 0);
                        }
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.renderQueue = 3000;
                    }
                    if ((part.RenderFlags & 0x4) != 0)
                    {
                        if (mat.HasProperty("_Cull"))
                            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    }
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
                    if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.35f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                    return mat;
                }
            }

            // === Fallback: DXT yükle ===
            Texture2D tex = null;

            if (!string.IsNullOrEmpty(part.TextureFileName))
            {
                string texPath = FindAssetFile(part.TextureFileName);
                if (texPath != null)
                    tex = KOTextureProvider.Load(texPath);
            }

            if (tex != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", tex);
                else if (mat.HasProperty("_Base_Map"))
                    mat.SetTexture("_Base_Map", tex);
                else
                    mat.mainTexture = tex;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", Color.white);
                else
                    mat.color = Color.white;

                // Alpha blending
                if ((part.RenderFlags & 0x1) != 0) // RF_ALPHABLENDING
                {
                    // URP transparent surface
                    if (mat.HasProperty("_Surface"))
                    {
                        mat.SetFloat("_Surface", 1); // Transparent
                        mat.SetFloat("_Blend", 0); // Alpha
                    }
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = 3000;
                }

                // Double-sided
                if ((part.RenderFlags & 0x4) != 0) // RF_DOUBLESIDED
                {
                    if (mat.HasProperty("_Cull"))
                        mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                }
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.35f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

                return mat;
            }

            // Diffuse renkli fallback
            Color diffuse = part.Diffuse;
            if (diffuse.r < 0.01f && diffuse.g < 0.01f && diffuse.b < 0.01f)
                diffuse = new Color(0.6f, 0.55f, 0.5f);
            return WorldBuilder.CreateMaterial(diffuse);
        }

        #endregion

        #region LOD Selection

        /// <summary>En iyi LOD seviyesini seç (vertex sayısı > 0 olan ilk LOD).</summary>
        public static N3CPartImporter.SkinLODData GetBestLOD(N3CPartImporter.CPartData part)
        {
            if (part.Skins == null) return null;

            for (int i = 0; i < 4; i++)
            {
                if (part.Skins.LODs[i] != null &&
                    part.Skins.LODs[i].FaceCount > 0 &&
                    part.Skins.LODs[i].VertexCount > 0)
                {
                    return part.Skins.LODs[i];
                }
            }
            return null;
        }

        #endregion

        #region File Resolution

        /// <summary>
        /// KO path'inden dosya bul. Birden fazla yerde arar.
        /// </summary>
        public static string FindAssetFile(string koPath)
        {
            if (string.IsNullOrEmpty(koPath)) return null;

            string normalized = koPath.Replace('\\', '/');

            // Resources/KOBinary/ altında var mı?
            if (KOBinaryProvider.Exists(normalized))
                return normalized;

            // Sadece dosya adı ile dene
            string fileNameOnly = Path.GetFileName(normalized);
            if (fileNameOnly != normalized && KOBinaryProvider.Exists(fileNameOnly))
                return fileNameOnly;

            return null;
        }

        #endregion

        #region Plug System (Equipment Visualization)

        /// <summary>
        /// Open-KO birebir: CPlayerBase::PlugSet (PlayerBase.cpp:1705-1850)
        /// + CN3ChrPartSet::PlugSet (N3Chr.cpp:1400-1420)
        /// 
        /// .n3cplug dosyasını yükler ve karakter skeleton'undaki uygun joint'e ekler.
        /// Plug'ın local transform'ı (position, rotation, scale) CPlugData'dan alınır.
        /// 
        /// Open-KO rendering pipeline:
        ///   mtx = m_Matrix (plug local) * mtxJoint (bone world) * mtxParent (chr world)
        ///   Unity'de: plug.transform.SetParent(joint) → local transform ayarla
        /// 
        /// ePlugPos: 0=RIGHTHAND, 1=LEFTHAND (Open-KO: e_PlugPosition)
        /// </summary>
        /// <param name="characterRoot">Karakter root GameObject (skeleton parent)</param>
        /// <param name="plugFilePath">.n3cplug dosya yolu</param>
        /// <param name="jointIndex">Override joint index (-1 ise plug dosyasındaki kullanılır)</param>
        /// <param name="plugTag">Plug'ı tanımlayan tag (örn: "PLUG_RH", "PLUG_LH")</param>
        /// <returns>Oluşturulan plug GameObject, null ise başarısız</returns>
        public static GameObject PlugSet(GameObject characterRoot, string plugFilePath,
            int jointIndex = -1, string plugTag = "PLUG")
        {
            if (characterRoot == null || string.IsNullOrEmpty(plugFilePath))
                return null;

            // ==========================================
            // [Araya Girme / Interception - Direct Prefab Loading]
            // If the item has a registered override prefab path (weapon/shield customization),
            // load and attach the Unity prefab directly, ignoring the legacy .n3cplug file.
            // ==========================================
            string plugBaseName = Path.GetFileNameWithoutExtension(plugFilePath);
            string overridePrefabPath = KOWeaponOverrideManager.GetOverridePrefabPath(plugBaseName);

            if (!string.IsNullOrEmpty(overridePrefabPath))
            {
                // Remove previous plug with the same tag
                PlugRemove(characterRoot, plugTag);

                GameObject prefab = Resources.Load<GameObject>(overridePrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[N3CharBuilder] Override prefab Resources altında bulunamadı: {overridePrefabPath}");
                    return null;
                }

                // Determine target joint (7 = Right Hand, 23 = Left Hand)
                int overrideTargetJoint = jointIndex;
                if (overrideTargetJoint < 0)
                {
                    if (plugTag == "PLUG_LH" || plugTag == "PLUG_1")
                    {
                        overrideTargetJoint = 23; // Left Hand
                    }
                    else
                    {
                        overrideTargetJoint = 7;  // Right Hand (Default fallback)
                    }
                }

                // Find joint transform from skeleton
                Transform overrideJointTransform = FindJointByIndex(characterRoot.transform, overrideTargetJoint);
                if (overrideJointTransform == null)
                {
                    Debug.LogWarning($"[N3CharBuilder] Joint bulunamadı: index={overrideTargetJoint} for override {plugFilePath}");
                    return null;
                }

                string overridePlugName = $"{plugTag}_{plugBaseName}_override";
                GameObject overridePlugObj = UnityEngine.Object.Instantiate(prefab);
                overridePlugObj.name = overridePlugName;
                overridePlugObj.transform.SetParent(overrideJointTransform);

                // Disable animator if present
                Animator[] animators = overridePlugObj.GetComponentsInChildren<Animator>();
                foreach (var animator in animators)
                {
                    animator.enabled = false;
                }

                // Add dummy MeshRenderer if missing (skeleton engine compatibility)
                if (overridePlugObj.GetComponent<MeshRenderer>() == null && overridePlugObj.GetComponent<SkinnedMeshRenderer>() == null)
                {
                    overridePlugObj.AddComponent<MeshRenderer>();
                }

                // Reset position, rotation, scale to defaults
                overridePlugObj.transform.localPosition = Vector3.zero;
                overridePlugObj.transform.localRotation = Quaternion.identity;
                overridePlugObj.transform.localScale = Vector3.one;

                // Apply custom weapon offsets, scales, and rotations
                KOWeaponOverrideManager.ApplyRelativeOffsets(plugBaseName, overridePlugObj.transform, plugTag);

                return overridePlugObj;
            }

            // If it's a weapon or shield (not a cloak/cape) and has no override, skip loading to prevent fallback
            if (!plugFilePath.ToLower().Contains("cloak") && plugTag != "PLUG_BACK")
            {
                Debug.LogWarning($"[N3CharBuilder] No custom weapon/shield prefab found for {plugFilePath}. Load skipped.");
                return null;
            }

            // ==========================================
            // Non-overridden legacy code begins here (e.g. for cloaks/capes):
            // ==========================================
            string fullPath = FindAssetFile(plugFilePath);
            // Önceki plug'ı kaldır (aynı tag ile)
            PlugRemove(characterRoot, plugTag);

            // .n3cplug dosyasını parse et
            if (fullPath == null)
            {
                Debug.LogWarning($"[N3CharBuilder] Plug dosyası bulunamadı: {plugFilePath}");
                return null;
            }

            var plugData = N3CPlugImporter.Load(fullPath);
            if (plugData == null)
            {
                Debug.LogWarning($"[N3CharBuilder] Plug data parsed null: {fullPath}");
                return null;
            }

            // Joint index belirle
            int targetJoint = jointIndex >= 0 ? jointIndex : plugData.JointIndex;

            // Skeleton'dan joint transform'ı bul
            Transform jointTransform = FindJointByIndex(characterRoot.transform, targetJoint);
            if (jointTransform == null)
            {
                Debug.LogWarning($"[N3CharBuilder] Joint bulunamadı: index={targetJoint} for {plugFilePath}");
                return null;
            }

            // PMesh yükle — önce convert edilmiş asset'i dene
            Mesh plugMesh = null;
            // (plugBaseName yukarıda tanımlandı)

            // === Resources.Load fallback: convert edilmiş mesh ===
            string[] meshSearchDirs = { "Item", "Chr", "ChrSelect" };
            foreach (var dir in meshSearchDirs)
            {
                plugMesh = Resources.Load<Mesh>($"KOModels/{dir}/Meshes/{plugBaseName}");
                if (plugMesh != null) break;
            }

            // === Fallback: parse ===
            if (plugMesh == null && !string.IsNullOrEmpty(plugData.MeshFileName))
            {
                string meshPath = FindAssetFile(plugData.MeshFileName);
                if (meshPath != null)
                {
                    var pmeshData = N3PMeshImporter.Load(meshPath);
                    plugMesh = N3PMeshImporter.CreateUnityMesh(pmeshData);
                }
            }

            if (plugMesh == null)
            {
                Debug.LogWarning($"[N3CharBuilder] Plug mesh yüklenemedi: {plugData.MeshFileName}");
                return null;
            }

            // Triangle winding order: N3PMesh DirectX CW = Unity CW, doğrudan kullanılır.
            // (Winding değiştirmeye gerek YOK — rotation doğru transpose edilince yüzler doğru kalır.)

            // Texture yükle — önce convert edilmiş texture'ı dene
            Texture2D plugTex = null;
            if (!string.IsNullOrEmpty(plugData.TextureFileName))
            {
                string texBaseName = Path.GetFileNameWithoutExtension(plugData.TextureFileName);
                string[] texSearchDirs = { "Chr", "Item", "ChrSelect", "Object", "DTex", "Misc" };
                foreach (var dir in texSearchDirs)
                {
                    plugTex = Resources.Load<Texture2D>($"KOTextures/{dir}/{texBaseName}");
                    if (plugTex != null) break;
                }

                // Fallback: DXT yükle
                if (plugTex == null)
                {
                    string texPath = FindAssetFile(plugData.TextureFileName);
                    if (texPath != null)
                        plugTex = KOTextureProvider.Load(texPath, flipY: true);
                }
            }

            // Plug GameObject oluştur
            string plugName = $"{plugTag}_{Path.GetFileNameWithoutExtension(plugFilePath)}";
            var plugObj = new GameObject(plugName);
            plugObj.tag = "Untagged"; // Unity default

            // Joint'e parent yap
            plugObj.transform.SetParent(jointTransform);

            // ============================================================
            // Open-KO birebir: CN3CPlugBase::ReCalcMatrix (N3Chr.cpp:329-334)
            //
            //   m_Matrix.Scale(m_vScale);              // Scale diagonal
            //   m_Matrix *= m_MtxRot;                  // Scale * Rot (row-major)
            //   m_Matrix.PosSet(m_vPosition * m_vScale) // translation = pos * scale → row 3
            //
            // DirectX convention: row-major, row-vector multiply → v' = v * M
            //   Transform order: v * Scale * Rot + (pos*scale)
            //     1) Scale vertex
            //     2) Rotate
            //     3) Translate
            //
            // Unity TRS: column-major, column-vector multiply → v' = T + R * S * v
            //   Transform order: same (scale, rotate, translate)
            //
            // Mapping:
            //   localScale    = m_vScale
            //   localPosition = m_vPosition * m_vScale
            //   localRotation = quaternion from TRANSPOSED m_MtxRot
            //                   (DirectX row-major → Unity column-major = transpose)
            //
            // Neden transpose?
            //   DirectX: v_row * R  (rotation satırlarda)
            //   Unity:   R * v_col  (rotation kolonlarda)
            //   R_unity = R_directx^T
            // ============================================================

            // Position: pos * scale (Open-KO PosSet birebir)
            Vector3 scale = plugData.Scale;
            plugObj.transform.localPosition = new Vector3(
                plugData.Position.x * scale.x,
                plugData.Position.y * scale.y,
                plugData.Position.z * scale.z
            );

            // Scale: doğrudan
            plugObj.transform.localScale = scale;

            // Rotation: DirectX row-major → Unity column-major = transpose
            var dxRot = plugData.RotationMatrix;
            var unityRotMtx = Matrix4x4.identity;
            unityRotMtx[0, 0] = dxRot[0, 0]; unityRotMtx[0, 1] = dxRot[1, 0]; unityRotMtx[0, 2] = dxRot[2, 0];
            unityRotMtx[1, 0] = dxRot[0, 1]; unityRotMtx[1, 1] = dxRot[1, 1]; unityRotMtx[1, 2] = dxRot[2, 1];
            unityRotMtx[2, 0] = dxRot[0, 2]; unityRotMtx[2, 1] = dxRot[1, 2]; unityRotMtx[2, 2] = dxRot[2, 2];
            plugObj.transform.localRotation = unityRotMtx.rotation;

            // Orijinal silaha/kalkana özel bağlı offset ve açıları uygula
            KOWeaponOverrideManager.ApplyRelativeOffsets(plugBaseName, plugObj.transform, plugTag);

            // MeshFilter + MeshRenderer ekle
            var mf = plugObj.AddComponent<MeshFilter>();
            mf.mesh = plugMesh;

            var mr = plugObj.AddComponent<MeshRenderer>();

            // Material oluştur
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);

            if (plugTex != null)
            {
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", plugTex);
                else if (mat.HasProperty("_Base_Map"))
                    mat.SetTexture("_Base_Map", plugTex);
                else
                    mat.mainTexture = plugTex;
            }

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            else
                mat.color = Color.white;

            // Alpha blending check
            if ((plugData.RenderFlags & 0x1) != 0) // RF_ALPHABLENDING
            {
                if (mat.HasProperty("_Surface"))
                {
                    mat.SetFloat("_Surface", 1);
                    mat.SetFloat("_Blend", 0);
                }
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }

            // Double-sided
            if ((plugData.RenderFlags & 0x4) != 0) // RF_DOUBLESIDED
            {
                if (mat.HasProperty("_Cull"))
                    mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            mr.material = mat;

            // ============================================================
            // Open-KO birebir: CN3CPlug trace (sword trail) — N3Chr.cpp:630-636
            // nTraceStep > 1 ise trail component ekle
            // Trail sadece saldırı animasyonu sırasında aktif olur (ileride)
            // ============================================================
            if (plugData.TraceStep > 1)
            {
                var trail = plugObj.AddComponent<KOWeaponTrail>();
                trail.Initialize(plugData.TraceStep, plugData.TraceColor,
                                 plugData.Trace0, plugData.Trace1);
            }


            return plugObj;
        }

        /// <summary>
        /// Belirtilen tag'e sahip plug'ı kaldırır.
        /// Open-KO: CN3ChrPartSet::PlugSet ile boş dosya adı verilince mevcut plug kaldırılır.
        /// </summary>
        public static void PlugRemove(GameObject characterRoot, string plugTag)
        {
            if (characterRoot == null) return;

            // Tüm child'larda tag ile eşleşen plug'ı bul ve kaldır
            var allTransforms = characterRoot.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith(plugTag + "_"))
                {
                    UnityEngine.Object.Destroy(t.gameObject);
                }
            }
        }

        /// <summary>
        /// Skeleton'daki joint'i flat index ile bulur.
        /// 
        /// BuildJointTransforms, joint'lere "Bone_{index}" veya gerçek isimlerini verir.
        /// İlk olarak "Bone_{index}" isimli transform'u arar.
        /// Bulunamazsa, tüm bone transform'larını BuildJointTransforms sırasıyla 
        /// (depth-first) toplar ve indeksle.
        /// </summary>
        private static Transform FindJointByIndex(Transform root, int targetIndex)
        {
            var bones = root.GetComponentsInChildren<KOBone>(true);
            foreach (var b in bones)
            {
                if (b.Index == targetIndex)
                    return b.transform;
            }

            // Fallback
            string boneName = $"Bone_{targetIndex}";
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name == boneName)
                    return t;
            }

            // Yöntem 2: Tüm bone transform'larını depth-first topla
            // BuildJointTransforms ile birebir aynı sırayı kullanır.
            // Part/Plug objeleri (renderer'lı) atlanır — sadece skeleton joint'leri sayılır.
            var bonesFallback = new List<Transform>();
            CollectBoneTransforms(root, bonesFallback);
            
            if (targetIndex >= 0 && targetIndex < bonesFallback.Count)
                return bonesFallback[targetIndex];

            return null;
        }

        /// <summary>
        /// BuildJointTransforms ile birebir aynı depth-first sırayla
        /// skeleton joint transform'larını toplar.
        /// Root'un direct child'larından başlar (part/plug objeleri atlanır).
        /// </summary>
        private static void CollectBoneTransforms(Transform parent, List<Transform> bones)
        {
            // Collect all KOBone components in the hierarchy (ignores plugs/meshes)
            var kobones = parent.GetComponentsInChildren<KOBone>(true);
            
            // Find max index to size the array
            int maxIdx = -1;
            foreach (var kb in kobones)
            {
                if (kb.Index > maxIdx) maxIdx = kb.Index;
            }

            if (maxIdx < 0) return;

            var sortedBones = new Transform[maxIdx + 1];
            foreach (var kb in kobones)
            {
                sortedBones[kb.Index] = kb.transform;
            }

            // Populate the bones list in order of their joint index
            foreach (var t in sortedBones)
            {
                if (t != null)
                {
                    bones.Add(t);
                }
            }
        }



        /// <summary>
        /// Public erişimli bone toplama metodu.
        /// PlayerController'ın FindUpperBodyBone() için kullanır.
        /// CollectBoneTransforms ile birebir aynı depth-first sırayı kullanır.
        /// </summary>
        public static void CollectBoneTransformsByName(Transform parent, List<Transform> bones)
        {
            CollectBoneTransforms(parent, bones);
        }

        #endregion

        #region Part Set/Remove — Open-KO birebir: CN3Chr::PartSet (N3Chr.cpp:2137-2151)

        // Open-KO: e_PartPosition (GameDef.h:361-371)
        public const int PART_POS_UPPER        = 0;
        public const int PART_POS_LOWER        = 1;
        public const int PART_POS_FACE         = 2;
        public const int PART_POS_HANDS        = 3;
        public const int PART_POS_FEET         = 4;
        public const int PART_POS_HAIR_HELMET  = 5;
        public const int PART_POS_COUNT        = 6;
        public const int PART_POS_UNKNOWN      = -1;

        public static string GetDefaultPartPath(int partIndex, byte eRace, int charClass, string fallbackPath)
        {
            bool isWarrior = (charClass == 101 || charClass == 105 || charClass == 106 ||
                              charClass == 201 || charClass == 205 || charClass == 206);
            bool isRogue = (charClass == 102 || charClass == 107 || charClass == 108 ||
                            charClass == 202 || charClass == 207 || charClass == 208);
            bool isPriest = (charClass == 104 || charClass == 111 || charClass == 112 ||
                             charClass == 204 || charClass == 211 || charClass == 212);
            bool isMage = (charClass == 103 || charClass == 109 || charClass == 110 ||
                           charClass == 203 || charClass == 209 || charClass == 210);

            // ==========================================
            // El Morad (Human) Side
            // ==========================================
            // Human Male (Race 12)
            if (eRace == 12)
            {
                if (isWarrior)
                {
                    if (partIndex == PART_POS_UPPER) return "Item/2_0212_10_0.n3cpart"; // Half Plate Pauldron
                    if (partIndex == PART_POS_LOWER) return "Item/2_0212_20_0.n3cpart"; // Half Plate Pads
                    if (partIndex == PART_POS_HANDS) return "Item/2_0212_40_0.n3cpart"; // Half Plate Gauntlet
                    if (partIndex == PART_POS_FEET)  return "Item/2_0212_50_0.n3cpart"; // Half Plate Boots
                }
                else if (isRogue)
                {
                    if (partIndex == PART_POS_UPPER) return "Item/2_4112_10_0.n3cpart"; // Rogue Shirt
                    if (partIndex == PART_POS_LOWER) return "Item/2_4112_20_0.n3cpart"; // Rogue Pads
                    if (partIndex == PART_POS_HANDS) return "Item/2_4112_40_0.n3cpart"; // Rogue Gloves
                    if (partIndex == PART_POS_FEET)  return "Item/2_4112_50_0.n3cpart"; // Rogue Shoes
                }
            }
            // Human Female (Race 13)
            else if (eRace == 13)
            {
                if (isPriest)
                {
                    if (partIndex == PART_POS_UPPER) return "Item/2_8113_10_0.n3cpart"; // Fabric Coat
                    if (partIndex == PART_POS_LOWER) return "Item/2_8113_20_0.n3cpart"; // Fabric Pants
                    if (partIndex == PART_POS_HANDS) return "Item/2_8113_40_0.n3cpart"; // Priest Gloves
                    if (partIndex == PART_POS_FEET)  return "Item/2_8113_50_0.n3cpart"; // Priest Shoes
                }
                else if (isMage)
                {
                    if (partIndex == PART_POS_UPPER) return "Item/2_6113_10_0.n3cpart"; // Mage Cotton Robe
                    if (partIndex == PART_POS_LOWER) return "Item/2_6113_20_0.n3cpart"; // Mage Cloth Pants
                    if (partIndex == PART_POS_HANDS) return "Item/2_6113_40_0.n3cpart"; // Mage Gloves
                    if (partIndex == PART_POS_FEET)  return "Item/2_6113_50_0.n3cpart"; // Mage Shoes
                }
            }
            // ==========================================
            // Karus Side
            // ==========================================
            // Karus Warrior Male (Arch Tuarek, Race 1)
            else if (eRace == 1)
            {
                if (isWarrior)
                {
                    if (partIndex == PART_POS_UPPER) return "Item/2_0201_10_0.n3cpart"; // Half Plate Pauldron
                    if (partIndex == PART_POS_LOWER) return "Item/2_0201_20_0.n3cpart"; // Half Plate Pads
                    if (partIndex == PART_POS_HANDS) return "Item/2_0201_40_0.n3cpart"; // Half Plate Gauntlet
                    if (partIndex == PART_POS_FEET)  return "Item/2_0201_50_0.n3cpart"; // Half Plate Boots
                }
            }
            // Karus Rogue Male (Tuarek, Race 2)
            else if (eRace == 2)
            {
                if (isRogue)
                {
                    if (partIndex == PART_POS_UPPER) return "Item/2_4102_10_0.n3cpart"; // Rogue Shirt
                    if (partIndex == PART_POS_LOWER) return "Item/2_4102_20_0.n3cpart"; // Rogue Pads
                    if (partIndex == PART_POS_HANDS) return "Item/2_4102_40_0.n3cpart"; // Rogue Gloves
                    if (partIndex == PART_POS_FEET)  return "Item/2_4102_50_0.n3cpart"; // Rogue Shoes
                }
            }
            // Karus Female Mage & Priest (Puri Tuarek, Race 4)
            else if (eRace == 4)
            {
                if (isPriest)
                {
                    if (partIndex == PART_POS_UPPER) return "Item/2_8104_10_0.n3cpart"; // Fabric Coat
                    if (partIndex == PART_POS_LOWER) return "Item/2_8104_20_0.n3cpart"; // Fabric Pants
                    if (partIndex == PART_POS_HANDS) return "Item/2_8104_40_0.n3cpart"; // Priest Gloves
                    if (partIndex == PART_POS_FEET)  return "Item/2_8104_50_0.n3cpart"; // Priest Shoes
                }
                else if (isMage)
                {
                    if (partIndex == PART_POS_UPPER) return "Item/2_6104_10_0.n3cpart"; // Mage Cotton Robe
                    if (partIndex == PART_POS_LOWER) return "Item/2_6104_20_0.n3cpart"; // Mage Cloth Pants
                    if (partIndex == PART_POS_HANDS) return "Item/2_6104_40_0.n3cpart"; // Mage Gloves
                    if (partIndex == PART_POS_FEET)  return "Item/2_6104_50_0.n3cpart"; // Mage Shoes
                }
            }
            return fallbackPath;
        }

        // ============================================================
        // Bind pose cache — Open-KO birebir: N3Chr.cpp:2071-2076
        //
        // C++'da bind pose hesabından ÖNCE m_pRootJointRef->Tick(0) ile
        // iskelet rest pose'a sıfırlanır. Unity portunda bu yapılmadığından,
        // runtime'da PartSet çağrıldığında kemikler animasyonlu pozisyonda
        // olabilir ve bind pose yanlış hesaplanır.
        //
        // Çözüm: İlk başarılı PartSet'teki bind pose'ları cache'le.
        // KO'da tüm part'lar aynı iskeleti paylaşır — bind pose hep aynıdır.
        // ============================================================
        private static Dictionary<(int, int), Matrix4x4[]> s_bindPoseCache = new Dictionary<(int, int), Matrix4x4[]>();

        /// <summary>
        /// Bind pose cache'ini temizler. Karakter modeli yeniden oluşturulduğunda çağrılmalı.
        /// </summary>
        public static void ClearBindPoseCache()
        {
            s_bindPoseCache.Clear();
        }

        /// <summary>
        /// Open-KO birebir: CN3Chr::PartSet (N3Chr.cpp:2137-2151)
        ///
        /// Belirtilen part index'teki skinned mesh'i yeni .n3cpart dosyasıyla değiştirir.
        ///
        /// CN3Chr::PartSet(int iIndex, const std::string& szFN):
        ///   if (m_Parts[iIndex]->FileName() == szFN) return;  // aynıysa dokunma
        ///   if (szFN.empty()) m_Parts[iIndex]->Release();      // boşsa kaldır
        ///   else m_Parts[iIndex]->LoadFromFile(szFN);           // yenisini yükle
        ///
        /// CPlayerBase::PartSet (PlayerBase.cpp:1852-1941):
        ///   UPPER için robe kontrolü yapılır.
        ///   szFN boşsa → varsayılan kıyafeti (pLooks->szPartFNs[ePos]) yükler.
        ///   Dolu ise → yeni part dosyasını yükler.
        /// </summary>
        /// <param name="characterRoot">Karakter root GameObject (skeleton + skinned meshes)</param>
        /// <param name="partIndex">Part pozisyonu (0-5: UPPER, LOWER, FACE, HANDS, FEET, HAIR)</param>
        /// <param name="partFilePath">KO relative .n3cpart dosya yolu (boş ise part kaldırılır)</param>
                /// <returns>Oluşturulan part GameObject, veya null</returns>
        public static GameObject PartSet(
            GameObject characterRoot, int partIndex, string partFilePath)
        {
            if (characterRoot == null) return null;
            if (partIndex < 0 || partIndex >= PART_POS_COUNT) return null;

            string partTag = $"PART_{partIndex}";

            // Mevcut part'ı kaldır (CN3Chr::PartSet → Release birebir)
            PartRemove(characterRoot, partIndex);

            // Open-KO birebir: szFN boşsa → part kaldırıldı, geri dön
            // CN3Chr::PartSet: if (szFN.empty()) m_Parts[iIndex]->Release();
            if (string.IsNullOrEmpty(partFilePath))
            {
                return null;
            }

            // .n3cpart dosyasını bul
            string partPath = FindAssetFile(partFilePath);
            if (partPath == null)
            {
                Debug.LogWarning($"[N3CharBuilder] PartSet: dosya bulunamadı: {partFilePath}");
                return null;
            }

            // CN3CPart::LoadFromFile birebir → N3CPartImporter.LoadCPart
            var partData = N3CPartImporter.LoadCPart(partPath);
            if (partData == null)
            {
                Debug.LogWarning($"[N3CharBuilder] PartSet: parse edilemedi: {partFilePath}");
                return null;
            }

            // En iyi LOD'u seç
            var skinLOD = GetBestLOD(partData);
            if (skinLOD == null || skinLOD.FaceCount <= 0)
            {
                Debug.LogWarning($"[N3CharBuilder] PartSet: LOD bulunamadı: {partFilePath}");
                return null;
            }

            // Bone transform'ları topla (BuildJointTransforms sırasıyla)
            var jointTransforms = new List<Transform>();
            CollectBoneTransforms(characterRoot.transform, jointTransforms);

            // Karakter root'unun doğrudan altındaki skeleton root'u da ekle
            // (ilk bone, Transform hierarchy'de ilk joint child'ıdır)
            if (jointTransforms.Count == 0)
            {
                Debug.LogWarning($"[N3CharBuilder] PartSet: bone transform bulunamadı");
                return null;
            }

            // Part GameObject oluştur
            string partName = $"{partTag}_{partData.Name ?? Path.GetFileNameWithoutExtension(partFilePath)}";
            var partObj = new GameObject(partName);
            partObj.transform.SetParent(characterRoot.transform);
            partObj.transform.localPosition = Vector3.zero;
            partObj.transform.localRotation = Quaternion.identity;

            // Skinned mesh oluştur — BuildFromLooks'taki akış birebir
            bool hasSkinning = (skinLOD.SkinVertices != null && jointTransforms.Count > 0);

            if (hasSkinning)
            {
                var smr = partObj.AddComponent<SkinnedMeshRenderer>();
                var skinnedMesh = BuildSkinnedMeshFromSkin(skinLOD,
                    jointTransforms, characterRoot.transform);

                smr.sharedMesh = skinnedMesh;
                smr.bones = jointTransforms.ToArray();
                smr.rootBone = jointTransforms[0];
                smr.material = BuildPartMaterial(partData);
                smr.localBounds = skinnedMesh.bounds;
                smr.updateWhenOffscreen = true;
            }
            else
            {
                var mesh = N3CPartImporter.CreateUnityMesh(skinLOD);
                if (mesh == null)
                {
                    UnityEngine.Object.Destroy(partObj);
                    return null;
                }
                var mf = partObj.AddComponent<MeshFilter>();
                mf.mesh = mesh;
                var mr = partObj.AddComponent<MeshRenderer>();
                mr.material = BuildPartMaterial(partData);
            }

            return partObj;
        }

        /// <summary>
        /// Belirtilen part index'teki skinned mesh'i kaldırır.
        /// Open-KO: CN3CPart::Release() birebir.
        /// </summary>
        public static void PartRemove(GameObject characterRoot, int partIndex)
        {
            if (characterRoot == null) return;

            string partTag = $"PART_{partIndex}";

            var allTransforms = characterRoot.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith(partTag + "_"))
                {
                    UnityEngine.Object.Destroy(t.gameObject);
                }
            }
        }

        #endregion

        #region Debug Helpers

        /// <summary>Transform hierarchy'yi debug logla (max derinlik sınırlı).</summary>
        private static void LogTransformHierarchy(Transform t, int depth, int maxDepth)
        {
            if (depth >= maxDepth) return;
            string indent = new string(' ', depth * 2);
            for (int i = 0; i < t.childCount && i < 5; i++)
            {
                LogTransformHierarchy(t.GetChild(i), depth + 1, maxDepth);
            }
        }

        #endregion

        #region InitFace / InitHair — Open-KO birebir: PlayerOther.cpp:172-201

        /// <summary>
        /// Open-KO birebir: CPlayerOther::InitFace (PlayerOther.cpp:172-184)
        ///
        /// C++ mantık:
        ///   pLooks = s_pTbl_UPC_Looks.Find(eRace);
        ///   if (pLooks && !pLooks->szPartFNs[PART_POS_FACE].empty()) {
        ///     _splitpath(pLooks->szPartFNs[PART_POS_FACE], nullptr, szDir, szFName, szExt);
        ///     szFN = fmt::format("{}{}{:02}{}", szDir, szFName, iFace, szExt);
        ///     PartSet(PART_POS_FACE, szFN, nullptr, nullptr);
        ///   }
        /// </summary>
        public static void InitFace(GameObject characterRoot,
            KOTableReader.TablePlayerLooks pLooks, int iFace)
        {
            if (characterRoot == null || pLooks == null) return;

            string baseFaceFN = pLooks.szPartFNs[PART_POS_FACE];
            if (string.IsNullOrEmpty(baseFaceFN)) return;

            // C++ birebir: _splitpath → dir, name, ext
            string dir = System.IO.Path.GetDirectoryName(baseFaceFN);
            string nameOnly = System.IO.Path.GetFileNameWithoutExtension(baseFaceFN);
            string ext = System.IO.Path.GetExtension(baseFaceFN);

            // C++ birebir: fmt::format("{}{}{:02}{}", szDir, szFName, iFace, szExt)
            // Windows path separator: backslash → need trailing separator
            if (!string.IsNullOrEmpty(dir) && !dir.EndsWith("\\") && !dir.EndsWith("/"))
                dir += "\\";

            string faceFN = $"{dir}{nameOnly}{iFace:D2}{ext}";

            PartSet(characterRoot, PART_POS_FACE, faceFN);
        }

        /// <summary>
        /// Open-KO birebir: CPlayerOther::InitHair (PlayerOther.cpp:187-201)
        ///
        /// C++ mantık:
        ///   pLooks = s_pTbl_UPC_Looks.Find(eRace);
        ///   if (pLooks && !pLooks->szPartFNs[PART_POS_HAIR_HELMET].empty()) {
        ///     _splitpath(pLooks->szPartFNs[PART_POS_HAIR_HELMET], ...);
        ///     szFN = fmt::format("{}{}{:02}{}", szDir, szFName, iHair, szExt);
        ///     PartSet(PART_POS_HAIR_HELMET, szFN, nullptr, nullptr);
        ///   } else {
        ///     m_Chr.PartSet(PART_POS_HAIR_HELMET, "");
        ///   }
        /// </summary>
        public static void InitHair(GameObject characterRoot,
            KOTableReader.TablePlayerLooks pLooks, int iHair)
        {
            if (characterRoot == null || pLooks == null) return;

            string baseHairFN = pLooks.szPartFNs[PART_POS_HAIR_HELMET];
            if (string.IsNullOrEmpty(baseHairFN))
            {
                // C++ birebir: else { m_Chr.PartSet(PART_POS_HAIR_HELMET, ""); }
                PartSet(characterRoot, PART_POS_HAIR_HELMET, "");
                return;
            }

            // C++ birebir: _splitpath → dir, name, ext
            string dir = System.IO.Path.GetDirectoryName(baseHairFN);
            string nameOnly = System.IO.Path.GetFileNameWithoutExtension(baseHairFN);
            string ext = System.IO.Path.GetExtension(baseHairFN);

            if (!string.IsNullOrEmpty(dir) && !dir.EndsWith("\\") && !dir.EndsWith("/"))
                dir += "\\";

            // Packed hair style decoding (e.g., 160 -> style 1, 50 -> style 0)
            int style = iHair >= 10 ? iHair / 100 : iHair;
            if (style < 0) style = 0;
            if (style > 9) style = 9;

            string hairFN = $"{dir}{nameOnly}{style:D2}{ext}";

            PartSet(characterRoot, PART_POS_HAIR_HELMET, hairFN);
        }

        private static void AddAnimRegistry(GameObject root, List<AnimationClip> clips, N3CPartImporter.AnimControlData animCtrl)
        {
            var registry = root.AddComponent<N3AnimClipRegistry>();
            registry.ClipNames = new string[clips.Count];
            registry.BlendTimes = new float[clips.Count];
            for (int ci = 0; ci < clips.Count; ci++)
            {
                registry.ClipNames[ci] = clips[ci].name;
                registry.BlendTimes[ci] = (ci < animCtrl.Animations.Count) ? animCtrl.Animations[ci].TimeBlend : 0.25f;
            }
        }

        public class KOBone : MonoBehaviour
        {
            public int Index;
            public Vector3 DefaultLocalPosition;
            public Quaternion DefaultLocalRotation;
        }

        #endregion
    }
}
