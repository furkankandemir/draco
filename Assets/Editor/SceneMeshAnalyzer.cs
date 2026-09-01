using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class SceneMeshAnalyzer : EditorWindow
{
    private Vector2 scrollPos;
    private List<MeshGroupInfo> meshGroups = new List<MeshGroupInfo>();
    private bool analyzed = false;
    private int totalTris, totalVerts, totalObjects, totalUniqueTypes;
    private string sortBy = "total";

    private class MeshGroupInfo
    {
        public string meshName;
        public string sourceFBX;
        public int trisPerInstance;
        public int verticesPerInstance;
        public int instanceCount;
        public int totalTris;
        public Mesh sharedMesh;
        public List<GameObject> instances = new List<GameObject>();
    }

    [MenuItem("Entropy Online/Analyze Scene Meshes", false, 200)]
    public static void ShowWindow()
    {
        var w = GetWindow<SceneMeshAnalyzer>("Scene Mesh Analyzer");
        w.minSize = new Vector2(750, 500);
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        var header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        GUILayout.Label("Scene Mesh Analyzer", header);
        GUILayout.Space(5);

        EditorGUILayout.HelpBox("Aktif sahnedeki tüm mesh'leri türlerine göre gruplar ve poligon sayısına göre listeler.", MessageType.Info);
        GUILayout.Space(5);

        var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fixedHeight = 30 };
        if (GUILayout.Button("Sahneyi Analiz Et", btnStyle))
        {
            AnalyzeScene();
        }

        if (!analyzed || meshGroups.Count == 0)
        {
            if (analyzed) EditorGUILayout.HelpBox("Sahnede mesh bulunamadı.", MessageType.Warning);
            return;
        }

        GUILayout.Space(5);
        EditorGUILayout.LabelField($"Toplam: {totalObjects:N0} obje  |  {totalUniqueTypes} benzersiz mesh türü  |  {totalTris:N0} üçgen  |  {totalVerts:N0} vertex");
        GUILayout.Space(5);

        // Sort buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Toggle(sortBy == "total", "Toplam Üçgene Göre", EditorStyles.toolbarButton))
        { if (sortBy != "total") { sortBy = "total"; SortList(); } }
        if (GUILayout.Toggle(sortBy == "per", "Birim Üçgene Göre", EditorStyles.toolbarButton))
        { if (sortBy != "per") { sortBy = "per"; SortList(); } }
        if (GUILayout.Toggle(sortBy == "count", "Adet'e Göre", EditorStyles.toolbarButton))
        { if (sortBy != "count") { sortBy = "count"; SortList(); } }
        if (GUILayout.Toggle(sortBy == "name", "İsme Göre", EditorStyles.toolbarButton))
        { if (sortBy != "name") { sortBy = "name"; SortList(); } }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Header row
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("#", EditorStyles.miniLabel, GUILayout.Width(28));
        GUILayout.Label("Mesh İsmi", EditorStyles.miniLabel, GUILayout.Width(210));
        GUILayout.Label("Üçgen/1", EditorStyles.miniLabel, GUILayout.Width(70));
        GUILayout.Label("Adet", EditorStyles.miniLabel, GUILayout.Width(50));
        GUILayout.Label("Toplam Üçgen", EditorStyles.miniLabel, GUILayout.Width(95));
        GUILayout.Label("Kaynak", EditorStyles.miniLabel, GUILayout.MinWidth(150));
        GUILayout.Label("", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < meshGroups.Count; i++)
        {
            var info = meshGroups[i];

            // Color code by total tri count
            Color bgColor;
            if (info.totalTris > 500000) bgColor = new Color(0.9f, 0.3f, 0.3f, 0.15f);
            else if (info.totalTris > 100000) bgColor = new Color(0.9f, 0.7f, 0.2f, 0.15f);
            else if (info.totalTris > 50000) bgColor = new Color(0.9f, 0.9f, 0.3f, 0.1f);
            else bgColor = Color.clear;

            Rect r = EditorGUILayout.BeginHorizontal();
            if (bgColor != Color.clear) EditorGUI.DrawRect(r, bgColor);

            GUILayout.Label((i + 1).ToString(), GUILayout.Width(28));
            GUILayout.Label(info.meshName, GUILayout.Width(210));
            GUILayout.Label(info.trisPerInstance.ToString("N0"), GUILayout.Width(70));

            // Adet - bold if many
            var countStyle = info.instanceCount > 50 ? EditorStyles.boldLabel : EditorStyles.label;
            GUILayout.Label(info.instanceCount.ToString(), countStyle, GUILayout.Width(50));

            GUILayout.Label(info.totalTris.ToString("N0"), GUILayout.Width(95));
            GUILayout.Label(info.sourceFBX, EditorStyles.miniLabel, GUILayout.MinWidth(150));

            if (GUILayout.Button("Birini Seç", GUILayout.Width(75)))
            {
                if (info.instances.Count > 0 && info.instances[0] != null)
                {
                    Selection.activeGameObject = info.instances[0];
                    EditorGUIUtility.PingObject(info.instances[0]);
                    SceneView.FrameLastActiveSceneView();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        // Export button
        GUILayout.Space(5);
        if (GUILayout.Button("Sonuçları Console'a Yazdır"))
        {
            ExportToConsole();
        }
    }

    private void AnalyzeScene()
    {
        meshGroups.Clear();
        totalTris = 0; totalVerts = 0; totalObjects = 0;

        var meshMap = new Dictionary<Mesh, MeshGroupInfo>();

        // MeshFilter'lar
        var allMeshFilters = FindObjectsByType<MeshFilter>();
        foreach (var mf in allMeshFilters)
        {
            if (mf.sharedMesh == null) continue;
            Mesh mesh = mf.sharedMesh;
            int tris = mesh.triangles.Length / 3;

            if (!meshMap.ContainsKey(mesh))
            {
                string sourcePath = AssetDatabase.GetAssetPath(mesh);
                meshMap[mesh] = new MeshGroupInfo
                {
                    meshName = mesh.name,
                    sourceFBX = System.IO.Path.GetFileName(sourcePath),
                    trisPerInstance = tris,
                    verticesPerInstance = mesh.vertexCount,
                    instanceCount = 0,
                    totalTris = 0,
                    sharedMesh = mesh,
                    instances = new List<GameObject>()
                };
            }

            meshMap[mesh].instanceCount++;
            meshMap[mesh].totalTris = meshMap[mesh].trisPerInstance * meshMap[mesh].instanceCount;
            meshMap[mesh].instances.Add(mf.gameObject);

            totalTris += tris;
            totalVerts += mesh.vertexCount;
            totalObjects++;
        }

        // SkinnedMeshRenderer'lar
        var allSkinned = FindObjectsByType<SkinnedMeshRenderer>();
        foreach (var smr in allSkinned)
        {
            if (smr.sharedMesh == null) continue;
            Mesh mesh = smr.sharedMesh;
            int tris = mesh.triangles.Length / 3;

            if (!meshMap.ContainsKey(mesh))
            {
                string sourcePath = AssetDatabase.GetAssetPath(mesh);
                meshMap[mesh] = new MeshGroupInfo
                {
                    meshName = mesh.name,
                    sourceFBX = System.IO.Path.GetFileName(sourcePath),
                    trisPerInstance = tris,
                    verticesPerInstance = mesh.vertexCount,
                    instanceCount = 0,
                    totalTris = 0,
                    sharedMesh = mesh,
                    instances = new List<GameObject>()
                };
            }

            meshMap[mesh].instanceCount++;
            meshMap[mesh].totalTris = meshMap[mesh].trisPerInstance * meshMap[mesh].instanceCount;
            meshMap[mesh].instances.Add(smr.gameObject);

            totalTris += tris;
            totalVerts += mesh.vertexCount;
            totalObjects++;
        }

        meshGroups = meshMap.Values.ToList();
        totalUniqueTypes = meshGroups.Count;
        SortList();
        analyzed = true;
        Repaint();
    }

    private void SortList()
    {
        switch (sortBy)
        {
            case "total": meshGroups = meshGroups.OrderByDescending(m => m.totalTris).ToList(); break;
            case "per": meshGroups = meshGroups.OrderByDescending(m => m.trisPerInstance).ToList(); break;
            case "count": meshGroups = meshGroups.OrderByDescending(m => m.instanceCount).ToList(); break;
            case "name": meshGroups = meshGroups.OrderBy(m => m.meshName).ToList(); break;
        }
    }

    private void ExportToConsole()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== SCENE MESH ANALYSIS ===");
        sb.AppendLine($"Toplam: {totalObjects:N0} obje | {totalUniqueTypes} benzersiz tür | {totalTris:N0} üçgen | {totalVerts:N0} vertex");
        sb.AppendLine($"{"#",-4} {"Mesh İsmi",-40} {"Üçgen/1",10} {"Adet",6} {"Toplam",12}  {"Kaynak"}");
        sb.AppendLine(new string('-', 100));

        for (int i = 0; i < meshGroups.Count; i++)
        {
            var info = meshGroups[i];
            string flag = info.totalTris > 500000 ? " [!!!]" : info.totalTris > 100000 ? " [!!]" : info.totalTris > 50000 ? " [!]" : "";
            sb.AppendLine($"{i + 1,-4} {info.meshName,-40} {info.trisPerInstance,10:N0} {info.instanceCount,6} {info.totalTris,12:N0}  {info.sourceFBX}{flag}");
        }

        Debug.Log(sb.ToString());
    }
}
