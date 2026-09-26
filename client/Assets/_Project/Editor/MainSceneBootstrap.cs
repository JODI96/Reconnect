using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reconnect.Client.Editor
{
    /// <summary>
    /// The app has a single entry scene. Pressing Play always starts Main.unity (even while another scene
    /// is open in the editor), and an empty "Untitled" scene after opening the project is replaced by Main.
    /// </summary>
    [InitializeOnLoad]
    public static class MainSceneBootstrap
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";

        static MainSceneBootstrap()
        {
            if (Application.isBatchMode)
            {
                return;   // tests and ProjectSetup manage scenes themselves
            }
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
            EditorApplication.delayCall += OpenMainIfEditorIsEmpty;
        }

        private static void OpenMainIfEditorIsEmpty()
        {
            var active = SceneManager.GetActiveScene();
            var isEmptyUntitled = SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path) && !active.isDirty;
            if (isEmptyUntitled && !EditorApplication.isPlayingOrWillChangePlaymode && AssetDatabase.AssetPathExists(MainScenePath))
            {
                EditorSceneManager.OpenScene(MainScenePath);
            }
        }
    }
}
