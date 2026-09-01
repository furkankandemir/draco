#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

public static class iOSPostProcessBuild
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        UnityEngine.Debug.Log("[iOSPostProcess] Configuring 1024x1024 App Icon in: " + pathToBuiltProject);

        string[] possibleXcassets = new string[]
        {
            Path.Combine(pathToBuiltProject, "Unity-iPhone", "Images.xcassets", "AppIcon.appiconset"),
            Path.Combine(pathToBuiltProject, "Images.xcassets", "AppIcon.appiconset"),
            Path.Combine(pathToBuiltProject, "Unity-iPhone", "Assets.xcassets", "AppIcon.appiconset")
        };

        string iconSource = Path.Combine(Directory.GetCurrentDirectory(), "BuildScripts", "AppIcon1024.png");
        if (!File.Exists(iconSource))
        {
            iconSource = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Textures", "AppIcon.png");
        }

        if (!File.Exists(iconSource))
        {
            UnityEngine.Debug.LogWarning("[iOSPostProcess] AppIcon1024.png not found!");
            return;
        }

        foreach (var xcassetsPath in possibleXcassets)
        {
            if (Directory.Exists(xcassetsPath))
            {
                string targetIcon = Path.Combine(xcassetsPath, "AppIcon-1024.png");
                File.Copy(iconSource, targetIcon, true);
                UnityEngine.Debug.Log("[iOSPostProcess] Copied AppIcon-1024.png to: " + targetIcon);

                string contentsJson = Path.Combine(xcassetsPath, "Contents.json");
                if (File.Exists(contentsJson))
                {
                    string json = File.ReadAllText(contentsJson);
                    if (!json.Contains("1024x1024"))
                    {
                        string iconEntry = "{\n      \"size\" : \"1024x1024\",\n      \"idiom\" : \"ios-marketing\",\n      \"filename\" : \"AppIcon-1024.png\",\n      \"scale\" : \"1x\"\n    },";
                        json = json.Replace("\"images\" : [", "\"images\" : [\n    " + iconEntry);
                        File.WriteAllText(contentsJson, json);
                        UnityEngine.Debug.Log("[iOSPostProcess] Successfully updated Contents.json with 1024x1024 icon entry!");
                    }
                }
            }
        }
    }
}
#endif
