using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class SetupLowPolyFences
{
    [MenuItem("Entropy Online/Setup Low Poly Fences Materials")]
    public static void Setup()
    {
        string modelsFolder = "Assets/LowPolyFences";
        string materialsFolder = "Assets/LowPolyFences/Materials";

        if (!AssetDatabase.IsValidFolder(materialsFolder))
        {
            AssetDatabase.CreateFolder(modelsFolder, "Materials");
        }

        // URP Lit Shader
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[SetupLowPolyFences] URP Lit shader bulunamadı!");
            return;
        }

        // Tüm FBX dosyalarını bul
        string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { modelsFolder });
        HashSet<string> processedMaterials = new HashSet<string>();
        int totalCreated = 0;

        // 1. Adım: FBX'lerden material isimlerini topla ve URP materyalleri oluştur
        foreach (string guid in fbxGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            
            foreach (Object subAsset in subAssets)
            {
                if (subAsset is Material mat)
                {
                    string matName = mat.name;
                    if (processedMaterials.Contains(matName))
                        continue;
                    processedMaterials.Add(matName);

                    string matPath = $"{materialsFolder}/{matName}.mat";

                    // Zaten varsa kontrol et
                    Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (existingMat != null)
                    {
                        // Shader'ı URP Lit değilse güncelle
                        if (existingMat.shader != urpLit)
                        {
                            existingMat.shader = urpLit;
                            EditorUtility.SetDirty(existingMat);
                            totalCreated++;
                        }
                        continue;
                    }

                    // Yeni URP Lit material oluştur
                    Material newMat = new Material(urpLit);
                    newMat.name = matName;

                    // Orijinal material'den texture'ları kopyala
                    if (mat.HasProperty("_MainTex") && mat.mainTexture != null)
                    {
                        newMat.SetTexture("_BaseMap", mat.mainTexture);
                        newMat.SetTexture("_MainTex", mat.mainTexture);
                    }
                    if (mat.HasProperty("_Color"))
                    {
                        newMat.SetColor("_BaseColor", mat.GetColor("_Color"));
                        newMat.SetColor("_Color", mat.GetColor("_Color"));
                    }
                    if (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null)
                    {
                        newMat.SetTexture("_BumpMap", mat.GetTexture("_BumpMap"));
                    }

                    AssetDatabase.CreateAsset(newMat, matPath);
                    totalCreated++;
                    Debug.Log($"[SetupLowPolyFences] Material oluşturuldu: {matName}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 2. Adım: FBX Importer ayarlarını güncelle - Material remap yap
        int remappedCount = 0;
        foreach (string guid in fbxGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            // Material import modunu ayarla
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            // Gömülü materyalleri Materials klasöründeki URP materyalleriyle eşleştir
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            bool didRemap = false;

            foreach (Object subAsset in subAssets)
            {
                if (subAsset is Material embeddedMat)
                {
                    string matPath = $"{materialsFolder}/{embeddedMat.name}.mat";
                    Material urpMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (urpMat != null)
                    {
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embeddedMat), urpMat);
                        didRemap = true;
                    }
                }
            }

            if (didRemap)
            {
                importer.SaveAndReimport();
                remappedCount++;
            }
        }

        Debug.Log($"[SetupLowPolyFences] Tamamlandı! {totalCreated} material oluşturuldu/güncellendi, {remappedCount} model remapped.");
        EditorUtility.DisplayDialog("Tamamlandı", $"{totalCreated} URP material oluşturuldu.\n{remappedCount} FBX modeli güncellendi.\n\nArtık beyaz görünme sorunu çözüldü!", "Tamam");
    }

    [MenuItem("Entropy Online/Create Low Poly Fences Prefabs")]
    public static void CreatePrefabs()
    {
        string modelsFolder = "Assets/LowPolyFences";
        string prefabsFolder = "Assets/LowPolyFences/Prefabs";


        if (!AssetDatabase.IsValidFolder(prefabsFolder))
        {
            AssetDatabase.CreateFolder(modelsFolder, "Prefabs");
        }

        string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { modelsFolder });
        int createdCount = 0;
        int skippedCount = 0;

        foreach (string guid in fbxGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string prefabPath = $"{prefabsFolder}/{fileName}.prefab";

            // Zaten varsa atla
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                skippedCount++;
                continue;
            }

            // FBX'i yükle
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbxAsset == null) continue;

            // Sahneye geçici instance oluştur
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);

            // Static yap (performans için)
            instance.isStatic = true;

            // MeshCollider ekle (snap to ground ve tıklama için)
            MeshFilter[] meshFilters = instance.GetComponentsInChildren<MeshFilter>();
            foreach (var mf in meshFilters)
            {
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                {
                    MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }

            // Prefab olarak kaydet
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            createdCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SetupLowPolyFences] {createdCount} prefab oluşturuldu, {skippedCount} zaten mevcuttu.");
        EditorUtility.DisplayDialog("Prefab Oluşturma Tamamlandı",
            $"{createdCount} yeni prefab oluşturuldu.\n{skippedCount} zaten mevcuttu.\n\nKonum: Assets/LowPolyFences/Prefabs/", "Tamam");
    }
}
