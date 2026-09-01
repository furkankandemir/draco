using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MeshCombiner
{
    [MenuItem("Entropy Online/Combine Selected Meshes %#m")] // Ctrl+Shift+M
    public static void CombineSelectedMeshes()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length < 2)
        {
            EditorUtility.DisplayDialog("Hata", "En az 2 obje seçmelisin!", "Tamam");
            return;
        }

        // Tüm MeshFilter'ları topla
        List<MeshFilter> meshFilters = new List<MeshFilter>();
        foreach (var go in selected)
        {
            meshFilters.AddRange(go.GetComponentsInChildren<MeshFilter>());
        }

        if (meshFilters.Count == 0)
        {
            EditorUtility.DisplayDialog("Hata", "Seçili objelerde mesh bulunamadı!", "Tamam");
            return;
        }

        // Material'lere göre grupla (aynı material olanları birleştir)
        Dictionary<Material, List<CombineInstance>> materialGroups = new Dictionary<Material, List<CombineInstance>>();

        foreach (var mf in meshFilters)
        {
            Renderer renderer = mf.GetComponent<Renderer>();
            if (renderer == null || mf.sharedMesh == null) continue;

            Material mat = renderer.sharedMaterial;
            if (!materialGroups.ContainsKey(mat))
            {
                materialGroups[mat] = new List<CombineInstance>();
            }

            CombineInstance ci = new CombineInstance();
            ci.mesh = mf.sharedMesh;
            ci.transform = mf.transform.localToWorldMatrix;
            materialGroups[mat].Add(ci);
        }

        // Merkez pozisyonu hesapla
        Vector3 center = Vector3.zero;
        foreach (var go in selected)
        {
            center += go.transform.position;
        }
        center /= selected.Length;

        // Birleştirilmiş objeyi oluştur
        GameObject combined = new GameObject("Combined_Fence");
        combined.transform.position = center;
        combined.isStatic = true;

        Undo.RegisterCreatedObjectUndo(combined, "Combine Meshes");

        if (materialGroups.Count == 1)
        {
            // Tek material — basit birleştirme
            foreach (var kvp in materialGroups)
            {
                // Transform'ları merkeze göre ayarla
                List<CombineInstance> adjusted = new List<CombineInstance>();
                foreach (var ci in kvp.Value)
                {
                    CombineInstance newCi = ci;
                    Matrix4x4 offset = Matrix4x4.TRS(-center, Quaternion.identity, Vector3.one);
                    newCi.transform = offset * ci.transform;
                    adjusted.Add(newCi);
                }

                Mesh combinedMesh = new Mesh();
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                combinedMesh.CombineMeshes(adjusted.ToArray(), true, true);
                combinedMesh.RecalculateBounds();
                combinedMesh.RecalculateNormals();

                // Save combined mesh to asset database
                string folder = "Assets/Resources/TerrainAssets/CombinedMeshes";
                if (!System.IO.Directory.Exists(folder))
                {
                    System.IO.Directory.CreateDirectory(folder);
                }
                string meshPath = $"{folder}/Mesh_{System.Guid.NewGuid()}.asset";
                AssetDatabase.CreateAsset(combinedMesh, meshPath);
                AssetDatabase.SaveAssets();

                MeshFilter mf = combined.AddComponent<MeshFilter>();
                mf.sharedMesh = combinedMesh;

                MeshRenderer mr = combined.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kvp.Key;

                MeshCollider mc = combined.AddComponent<MeshCollider>();
                mc.sharedMesh = combinedMesh;
            }
        }
        else
        {
            // Birden fazla material — sub-mesh olarak birleştir
            List<Mesh> subMeshes = new List<Mesh>();
            List<Material> materials = new List<Material>();

            foreach (var kvp in materialGroups)
            {
                List<CombineInstance> adjusted = new List<CombineInstance>();
                foreach (var ci in kvp.Value)
                {
                    CombineInstance newCi = ci;
                    Matrix4x4 offset = Matrix4x4.TRS(-center, Quaternion.identity, Vector3.one);
                    newCi.transform = offset * ci.transform;
                    adjusted.Add(newCi);
                }

                Mesh subMesh = new Mesh();
                subMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                subMesh.CombineMeshes(adjusted.ToArray(), true, true);
                subMeshes.Add(subMesh);
                materials.Add(kvp.Key);
            }

            // Sub-mesh'leri birleştir
            CombineInstance[] finalCombine = new CombineInstance[subMeshes.Count];
            for (int i = 0; i < subMeshes.Count; i++)
            {
                finalCombine[i].mesh = subMeshes[i];
                finalCombine[i].transform = Matrix4x4.identity;
            }

            Mesh finalMesh = new Mesh();
            finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            finalMesh.CombineMeshes(finalCombine, false, true);
            finalMesh.RecalculateBounds();
            finalMesh.RecalculateNormals();

            // Save combined mesh to asset database
            string folder = "Assets/Resources/TerrainAssets/CombinedMeshes";
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }
            string meshPath = $"{folder}/Mesh_{System.Guid.NewGuid()}.asset";
            AssetDatabase.CreateAsset(finalMesh, meshPath);
            AssetDatabase.SaveAssets();

            MeshFilter mf = combined.AddComponent<MeshFilter>();
            mf.sharedMesh = finalMesh;

            MeshRenderer mr = combined.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials.ToArray();

            MeshCollider mc = combined.AddComponent<MeshCollider>();
            mc.sharedMesh = finalMesh;
        }

        // Orijinal objeleri devre dışı bırak
        foreach (var go in selected)
        {
            Undo.RecordObject(go, "Disable Original");
            go.SetActive(false);
        }

        Selection.activeGameObject = combined;

        int totalVerts = combined.GetComponent<MeshFilter>().sharedMesh.vertexCount;
        Debug.Log($"[MeshCombiner] {meshFilters.Count} mesh birleştirildi ve kaydedildi. Toplam vertex: {totalVerts}");
    }
}

