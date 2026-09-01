using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;
using EntropyOnline.UI;

public class CreateExitMenuPrefab
{
    [MenuItem("Tools/Create Modern Exit Menu")]
    public static void CreateExitMenu()
    {
        string modernUiDir = "Assets/Resources/ModernUI";
        if (!Directory.Exists(modernUiDir))
        {
            Directory.CreateDirectory(modernUiDir);
        }

        // 1. Create root GameObject
        GameObject root = new GameObject("co_ExitMenu_us", typeof(RectTransform));
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(200f, 183f);
        rootRt.anchoredPosition = Vector2.zero;

        root.AddComponent<CanvasRenderer>();
        root.AddComponent<Image>();

        // Add Scale Independent component
        root.AddComponent<KOUIScaleIndependent>();

        // 2. Create ButtonContainer
        GameObject container = new GameObject("ButtonContainer", typeof(RectTransform));
        container.transform.SetParent(root.transform, false);
        RectTransform containerRt = container.GetComponent<RectTransform>();
        containerRt.anchorMin = new Vector2(0.5f, 0.5f);
        containerRt.anchorMax = new Vector2(0.5f, 0.5f);
        containerRt.pivot = new Vector2(0.5f, 0.5f);
        containerRt.sizeDelta = new Vector2(180f, 163f);
        containerRt.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = container.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        // Helper to add clean button
        System.Action<string, string> addBtn = (name, label) => {
            GameObject btnObj = new GameObject(name, typeof(RectTransform));
            btnObj.transform.SetParent(container.transform, false);
            btnObj.AddComponent<CanvasRenderer>();
            btnObj.AddComponent<Image>();
            btnObj.AddComponent<Button>();

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 180f;
            le.preferredHeight = 37f;

            // Add Text child
            GameObject txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
            txtRt.anchoredPosition = Vector2.zero;

            Text t = txtObj.AddComponent<Text>();
            t.text = label;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white; // White text color
            t.fontSize = 12;
            t.fontStyle = FontStyle.Bold;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.font = font;
        };

        // Create the 4 buttons
        addBtn("btn_select_server", "SELECT SERVER");
        addBtn("btn_option", "OPTION");
        addBtn("btn_exit", "EXIT");
        addBtn("btn_cancel", "CANCEL");

        // 3. Save prefab
        string savePath = $"{modernUiDir}/co_ExitMenu_us.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Debug.Log("[EXITMENU] Clean standalone exit menu prefab generated at " + savePath);

        // 4. Cleanup temporary scene object
        GameObject.DestroyImmediate(root);
    }
}
