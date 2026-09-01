using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO v1.298 Runtime Animation Builder
    /// 
    /// Joint keyframe verileri (N3ChrImporter) + animasyon tanımları (N3CPartImporter)
    /// kullanarak Unity AnimationClip'leri oluşturur.
    /// 
    /// KO animasyon sistemi (N3AnimKey.h DataGet):
    ///   - Her joint'in 4 animation key track'i var: pos, rot, scale, orient
    ///   - Keyframe'ler sabit sampling rate ile depolanır
    ///   - Frame → keyframe index formülü: index = frame * (samplingRate / 30.0)
    ///   - Frame aralıkları .n3anim dosyasında tanımlı (breath: 2-52, walk: 54-90, ...)
    ///   - Interpolasyon: fDelta = (frame - index * (30/sr)) / (30/sr)
    /// </summary>
    public static class N3AnimBuilder
    {
        /// <summary>
        /// .n3chr skeleton verisinden + .n3anim tanımlarından AnimationClip'ler oluşturur.
        /// </summary>
        /// <param name="chrData">Parse edilmiş karakter verisi (skeleton + joint keys)</param>
        /// <param name="animCtrl">Parse edilmiş animasyon tanımları</param>
        /// <param name="jointPaths">Her joint'in hierarchy path'i (bone0/bone1/bone2...)</param>
        /// <returns>İsimlendirilmiş AnimationClip listesi</returns>
        public static List<AnimationClip> BuildClips(
            N3ChrImporter.N3ChrData chrData,
            N3CPartImporter.AnimControlData animCtrl,
            Dictionary<int, string> jointPaths)
        {
            if (chrData?.RootJoint == null || animCtrl == null || animCtrl.Animations.Count == 0)
                return new List<AnimationClip>();

            // Tüm joint'leri flat listeye topla
            var allJoints = new List<N3ChrImporter.JointNode>();
            FlattenJoints(chrData.RootJoint, allJoints);

            var clips = new List<AnimationClip>();
            var usedNames = new HashSet<string>();

            // Diagnostic: ilk joint'in keyframe bilgisi
            var firstJoint = allJoints[0];

            int clipIdx = 0;
            foreach (var anim in animCtrl.Animations)
            {
                // KRITIK: Boş veya geçersiz animasyonları ATLAMA!
                // C++'da e_Ani enum değerleri .n3anim dosya sırasıyla birebir eşleşir.
                // Herhangi bir entry atlanırsa tüm sonraki index'ler kayar ve
                // yanlış animasyonlar oynar.
                if (string.IsNullOrEmpty(anim.Name) || anim.FrmEnd <= anim.FrmStart)
                {
                    // Placeholder clip ekle — index sırasını koru
                    var placeholder = new AnimationClip();
                    string placeholderName = $"_placeholder_{clipIdx}";
                    placeholder.name = placeholderName;
                    usedNames.Add(placeholderName);
                    placeholder.legacy = true;
                    clips.Add(placeholder);
                    clipIdx++;
                    continue;
                }

                var clip = BuildSingleClip(anim, allJoints, jointPaths);
                if (clip != null)
                {
                    string uniqueName = anim.Name;
                    if (usedNames.Contains(uniqueName))
                    {
                        uniqueName = uniqueName + "_" + clipIdx;
                    }
                    usedNames.Add(uniqueName);
                    clip.name = uniqueName;

                    clips.Add(clip);
                }
                else
                {
                    // BuildSingleClip null döndüyse de placeholder ekle
                    var placeholder = new AnimationClip();
                    string placeholderName = $"_placeholder_{clipIdx}";
                    placeholder.name = placeholderName;
                    usedNames.Add(placeholderName);
                    placeholder.legacy = true;
                    clips.Add(placeholder);
                }
                clipIdx++;
            }

            return clips;
        }

        /// <summary>
        /// Tek bir animasyon tanımından Unity AnimationClip oluşturur.
        /// </summary>
        private static AnimationClip BuildSingleClip(
            N3CPartImporter.AnimData anim,
            List<N3ChrImporter.JointNode> joints,
            Dictionary<int, string> jointPaths)
        {
            var clip = new AnimationClip();
            clip.name = anim.Name;
            clip.legacy = true; // Legacy animation for Animation component

            float duration = (anim.FrmEnd - anim.FrmStart) / anim.FrmPerSec;
            if (duration <= 0) return null;

            // WrapMode: Hareket animasyonları her zaman loop
            string lname = anim.Name.ToLower();
            bool isLooping = (anim.BlendFlags & 1) != 0 ||
                             lname.Contains("breath") || lname.Contains("walk") ||
                             lname.Contains("run") || lname.Contains("idle") ||
                             lname.Contains("stand") || lname.Contains("wait");
            clip.wrapMode = isLooping ? WrapMode.Loop : WrapMode.Once;

            int curvesAdded = 0;
            bool firstJointLogged = false;

            foreach (var joint in joints)
            {
                if (!jointPaths.TryGetValue(joint.Index, out string path))
                    continue;

                // ============================================
                // Position keyframes
                // ============================================
                if (joint.KeyPos != null && joint.KeyPos.Count > 0 && joint.KeyPos.Type == 0)
                {
                    var posKeys = ExtractVec3Keyframes(joint.KeyPos,
                        anim.FrmStart, anim.FrmEnd, anim.FrmPerSec);

                    if (posKeys.x.Length > 0)
                    {
                        clip.SetCurve(path, typeof(Transform), "localPosition.x",
                            new AnimationCurve(posKeys.x));
                        clip.SetCurve(path, typeof(Transform), "localPosition.y",
                            new AnimationCurve(posKeys.y));
                        clip.SetCurve(path, typeof(Transform), "localPosition.z",
                            new AnimationCurve(posKeys.z));
                        curvesAdded += 3;

                        if (!firstJointLogged)
                        {
                        }
                    }
                }

                // ============================================
                // Rotation keyframes (quaternion)
                // KO ReCalcMatrix: m_qRot * m_qOrient
                // Orient keyframe'ler varsa rotation ile birleştirilir
                // ============================================
                if (joint.KeyRot != null && joint.KeyRot.Count > 0 && joint.KeyRot.Type == 1)
                {
                    var rotKeys = ExtractQuatKeyframes(joint.KeyRot,
                        anim.FrmStart, anim.FrmEnd, anim.FrmPerSec);

                    // Orient key varsa rotation'a bake et (KO: m_qRot * m_qOrient)
                    if (joint.KeyOrient != null && joint.KeyOrient.Count > 0 && joint.KeyOrient.Type == 1)
                    {
                        var orientKeys = ExtractQuatKeyframes(joint.KeyOrient,
                            anim.FrmStart, anim.FrmEnd, anim.FrmPerSec);
                        
                        rotKeys = BakeOrientIntoRotation(rotKeys, orientKeys);
                    }

                    if (rotKeys.x.Length > 0)
                    {
                        clip.SetCurve(path, typeof(Transform), "localRotation.x",
                            new AnimationCurve(rotKeys.x));
                        clip.SetCurve(path, typeof(Transform), "localRotation.y",
                            new AnimationCurve(rotKeys.y));
                        clip.SetCurve(path, typeof(Transform), "localRotation.z",
                            new AnimationCurve(rotKeys.z));
                        clip.SetCurve(path, typeof(Transform), "localRotation.w",
                            new AnimationCurve(rotKeys.w));
                        curvesAdded += 4;

                        if (!firstJointLogged)
                        {
                            firstJointLogged = true;
                        }
                    }
                }

                // ============================================
                // Scale keyframes
                // ============================================
                if (joint.KeyScale != null && joint.KeyScale.Count > 0 && joint.KeyScale.Type == 0)
                {
                    var scaleKeys = ExtractVec3Keyframes(joint.KeyScale,
                        anim.FrmStart, anim.FrmEnd, anim.FrmPerSec);

                    if (scaleKeys.x.Length > 0)
                    {
                        clip.SetCurve(path, typeof(Transform), "localScale.x",
                            new AnimationCurve(scaleKeys.x));
                        clip.SetCurve(path, typeof(Transform), "localScale.y",
                            new AnimationCurve(scaleKeys.y));
                        clip.SetCurve(path, typeof(Transform), "localScale.z",
                            new AnimationCurve(scaleKeys.z));
                        curvesAdded += 3;
                    }
                }
            }

            if (curvesAdded == 0)
                return null;

            clip.EnsureQuaternionContinuity();
            return clip;
        }

        #region Keyframe Extraction

        /// <summary>
        /// Vector3 track'inden belirli frame aralığındaki keyframe'leri çıkarır.
        /// 
        /// KO N3AnimKey.h DataGet formülü:
        ///   float fD = 30.0f / samplingRate;
        ///   int nIndex = (int)(fFrm * (samplingRate / 30.0f));
        ///   float fDelta = (fFrm - nIndex * fD) / fD;
        ///
        /// Yani: frame → keyIndex = frame * sr / 30
        /// Unity time: (frame - frmStart) / frmPerSec
        /// </summary>
        private static (Keyframe[] x, Keyframe[] y, Keyframe[] z) ExtractVec3Keyframes(
            N3ChrImporter.AnimKeyData key,
            float frmStart, float frmEnd, float frmPerSec)
        {
            if (key.VectorKeys == null || key.VectorKeys.Length == 0)
                return (Array.Empty<Keyframe>(), Array.Empty<Keyframe>(), Array.Empty<Keyframe>());

            float sr = key.SamplingRate > 0 ? key.SamplingRate : 30f;

            // KO formülü: index = frame * (sr / 30)
            int startIdx = Mathf.FloorToInt(frmStart * (sr / 30.0f));
            int endIdx = Mathf.CeilToInt(frmEnd * (sr / 30.0f));

            startIdx = Mathf.Clamp(startIdx, 0, key.VectorKeys.Length - 1);
            endIdx = Mathf.Clamp(endIdx, startIdx, key.VectorKeys.Length - 1);

            int count = endIdx - startIdx + 1;
            if (count <= 0)
                return (Array.Empty<Keyframe>(), Array.Empty<Keyframe>(), Array.Empty<Keyframe>());

            var kfX = new Keyframe[count];
            var kfY = new Keyframe[count];
            var kfZ = new Keyframe[count];

            // KO: fD = 30 / sr (her keyframe arası frame sayısı)
            float fD = 30.0f / sr;

            for (int i = 0; i < count; i++)
            {
                int idx = startIdx + i;
                // Bu keyframe'in karşılık geldiği KO frame numarası
                float frame = idx * fD;
                // Unity time: frame'in animasyon başından itibaren saniye cinsinden konumu
                float time = (frame - frmStart) / frmPerSec;
                // Negatif zaman olmamalı
                if (time < 0) time = 0;

                var v = key.VectorKeys[idx];
                kfX[i] = new Keyframe(time, v.x);
                kfY[i] = new Keyframe(time, v.y);
                kfZ[i] = new Keyframe(time, v.z);
            }

            return (kfX, kfY, kfZ);
        }

        /// <summary>
        /// Quaternion track'inden belirli frame aralığındaki keyframe'leri çıkarır.
        /// Aynı KO formülü: index = frame * (sr/30)
        /// </summary>
        private static (Keyframe[] x, Keyframe[] y, Keyframe[] z, Keyframe[] w) ExtractQuatKeyframes(
            N3ChrImporter.AnimKeyData key,
            float frmStart, float frmEnd, float frmPerSec)
        {
            if (key.QuatKeys == null || key.QuatKeys.Length == 0)
                return (Array.Empty<Keyframe>(), Array.Empty<Keyframe>(),
                        Array.Empty<Keyframe>(), Array.Empty<Keyframe>());

            float sr = key.SamplingRate > 0 ? key.SamplingRate : 30f;

            // KO formülü: index = frame * (sr / 30)
            int startIdx = Mathf.FloorToInt(frmStart * (sr / 30.0f));
            int endIdx = Mathf.CeilToInt(frmEnd * (sr / 30.0f));

            startIdx = Mathf.Clamp(startIdx, 0, key.QuatKeys.Length - 1);
            endIdx = Mathf.Clamp(endIdx, startIdx, key.QuatKeys.Length - 1);

            int count = endIdx - startIdx + 1;
            if (count <= 0)
                return (Array.Empty<Keyframe>(), Array.Empty<Keyframe>(),
                        Array.Empty<Keyframe>(), Array.Empty<Keyframe>());

            var kfX = new Keyframe[count];
            var kfY = new Keyframe[count];
            var kfZ = new Keyframe[count];
            var kfW = new Keyframe[count];

            float fD = 30.0f / sr;

            for (int i = 0; i < count; i++)
            {
                int idx = startIdx + i;
                float frame = idx * fD;
                float time = (frame - frmStart) / frmPerSec;
                if (time < 0) time = 0;

                var q = key.QuatKeys[idx];
                kfX[i] = new Keyframe(time, q.x);
                kfY[i] = new Keyframe(time, q.y);
                kfZ[i] = new Keyframe(time, q.z);
                kfW[i] = new Keyframe(time, q.w);
            }

            return (kfX, kfY, kfZ, kfW);
        }

        /// <summary>
        /// KO ReCalcMatrix: rotation = qRot * qOrient
        /// Orient keyframe'leri rotation'a bake eder.
        /// Eğer orient keyframe sayısı farklıysa, en yakın index kullanılır.
        /// </summary>
        private static (Keyframe[] x, Keyframe[] y, Keyframe[] z, Keyframe[] w) BakeOrientIntoRotation(
            (Keyframe[] x, Keyframe[] y, Keyframe[] z, Keyframe[] w) rot,
            (Keyframe[] x, Keyframe[] y, Keyframe[] z, Keyframe[] w) orient)
        {
            int count = rot.x.Length;
            int orientCount = orient.x.Length;

            var kfX = new Keyframe[count];
            var kfY = new Keyframe[count];
            var kfZ = new Keyframe[count];
            var kfW = new Keyframe[count];

            for (int i = 0; i < count; i++)
            {
                float time = rot.x[i].time;
                var qRot = new Quaternion(rot.x[i].value, rot.y[i].value, rot.z[i].value, rot.w[i].value);

                // Orient index'ini rotation keyframe'iyle eşleştir
                int oi = orientCount > 0 ? Mathf.Clamp(i * orientCount / count, 0, orientCount - 1) : 0;
                
                Quaternion qOrient;
                if (orientCount > 0)
                    qOrient = new Quaternion(orient.x[oi].value, orient.y[oi].value, 
                                            orient.z[oi].value, orient.w[oi].value);
                else
                    qOrient = Quaternion.identity;

                // KO: m_Matrix = m_qRot * m_qOrient (quaternion multiplication)
                Quaternion combined = qRot * qOrient;

                kfX[i] = new Keyframe(time, combined.x);
                kfY[i] = new Keyframe(time, combined.y);
                kfZ[i] = new Keyframe(time, combined.z);
                kfW[i] = new Keyframe(time, combined.w);
            }

            return (kfX, kfY, kfZ, kfW);
        }

        #endregion

        #region Helpers

        /// <summary>Joint ağacını flat listeye dönüştürür (index sırasıyla).</summary>
        private static void FlattenJoints(
            N3ChrImporter.JointNode node,
            List<N3ChrImporter.JointNode> list)
        {
            list.Add(node);
            foreach (var child in node.Children)
                FlattenJoints(child, list);
        }

        /// <summary>
        /// Joint hierarchy'deki her joint için transform path hesaplar.
        /// Unity AnimationClip curve'leri Animation component'ın bulunduğu
        /// GameObject'e göre relative path ister.
        /// 
        /// Animation component root'ta (ör: "upc_el_rm_wa") olduğu için,
        /// joint path'leri root joint'ten başlamalı: "Hips", "Hips/Spine", vb.
        /// </summary>
        public static Dictionary<int, string> BuildJointPaths(
            N3ChrImporter.JointNode root, string rootBoneName = "")
        {
            var paths = new Dictionary<int, string>();
            BuildJointPathsRecursive(root, rootBoneName, paths);

            // Diagnostic: ilk birkaç path'i logla
            int logged = 0;
            foreach (var kvp in paths)
            {
                if (logged < 5)
                {
                    logged++;
                }
            }

            return paths;
        }

        private static void BuildJointPathsRecursive(
            N3ChrImporter.JointNode node, string parentPath,
            Dictionary<int, string> paths)
        {
            string nodeName = string.IsNullOrEmpty(node.Name) ? $"Bone_{node.Index}" : node.Name;
            string fullPath = string.IsNullOrEmpty(parentPath) ? nodeName : $"{parentPath}/{nodeName}";

            paths[node.Index] = fullPath;

            foreach (var child in node.Children)
            {
                BuildJointPathsRecursive(child, fullPath, paths);
            }
        }

        #endregion
    }
}
