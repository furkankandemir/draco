using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EntropyOnline.Editor
{
    /// <summary>
    /// Unity Editöründe play moduna geçildiğinde oyunun her zaman LoginScene'den başlamasını sağlar.
    /// Bu sayede hangi sahne açık olursa olsun (ColonyZone, Eslant vb.) play'e basıldığında otomatik olarak giriş ekranı yüklenir.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeStartSceneSetter
    {
        static PlayModeStartSceneSetter()
        {
            string scenePath = "Assets/Scenes/LoginScene.unity";
            var loginScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (loginScene != null)
            {
                if (EditorSceneManager.playModeStartScene != loginScene)
                {
                    EditorSceneManager.playModeStartScene = loginScene;
                    Debug.Log($"[PLAYMODE] Play Mode Başlangıç Sahnesi Ayarlandı: {scenePath}");
                }
            }
            else
            {
                Debug.LogWarning($"[PLAYMODE] LoginScene şu yolda bulunamadı: {scenePath}. Başlangıç sahnesi ayarlanamadı.");
            }
        }
    }
}
