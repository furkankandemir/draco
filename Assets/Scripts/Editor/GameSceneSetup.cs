using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace EntropyOnline.Editor
{
    /// <summary>
    /// Entropy Online — Sahne Temizleme Aracı
    /// Sahnelerdeki gereksiz UI elementlerini temizler.
    /// </summary>
    public static class GameSceneSetup
    {
        [MenuItem("Entropy Online/Sahneleri Temizle", false, 21)]
        public static void CleanAllScenes()
        {
            CleanScene("Assets/Scenes/GameScene.unity");
            CleanScene("Assets/Scenes/GameScene_Guvende.unity");
        }

        private static void CleanScene(string path)
        {
            if (!System.IO.File.Exists(path)) return;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            
            string[] namesToDestroy = { "TargetPanel", "CombatUI", "HPBar_BG", "MPBar_BG" };
            bool dirty = false;
            foreach (var name in namesToDestroy)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                    dirty = true;
                    Debug.Log($"Destroyed {name} in {path}");
                }
            }

            var gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas != null)
            {
                for (int i = gameCanvas.transform.childCount - 1; i >= 0; i--)
                {
                    var child = gameCanvas.transform.GetChild(i).gameObject;
                    if (System.Array.IndexOf(namesToDestroy, child.name) >= 0)
                    {
                        Object.DestroyImmediate(child);
                        dirty = true;
                        Debug.Log($"Destroyed canvas child {child.name} in {path}");
                    }
                }
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
    }
}
