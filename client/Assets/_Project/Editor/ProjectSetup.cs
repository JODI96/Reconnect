using System.IO;
using Reconnect.Client.Core;
using Reconnect.Client.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Reconnect.Client.Editor
{
    /// <summary>
    /// Creates/updates the project's assets, main scene and player settings in code, so the
    /// setup is reproducible and reviewable in Git. Safe to run repeatedly.
    /// Menu: Reconnect → Setup Project. Batch mode: -executeMethod Reconnect.Client.Editor.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        private const string Root = "Assets/_Project";
        private const string SettingsDir = Root + "/Settings";
        private const string UiDir = Root + "/UI";
        private const string ScenePath = Root + "/Scenes/Main.unity";
        public const string BundleId = "ch.reconnect.app";

        [MenuItem("Reconnect/Setup Project")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);

            var apiSettings = LoadOrCreate<ApiSettings>(SettingsDir + "/ApiSettings.asset");
            var catalog = CreateUiCatalog();
            var panelSettings = CreatePanelSettings();
            CreateMainScene(apiSettings, catalog, panelSettings);
            ConfigurePlayer();
            RemoveTemplateContent();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Reconnect] Project setup complete.");
        }

        private static UiCatalog CreateUiCatalog()
        {
            var catalog = LoadOrCreate<UiCatalog>(SettingsDir + "/UiCatalog.asset");
            catalog.theme = Load<StyleSheet>(UiDir + "/Theme.uss");
            catalog.login = Load<VisualTreeAsset>(UiDir + "/Login.uxml");
            catalog.register = Load<VisualTreeAsset>(UiDir + "/Register.uxml");
            catalog.roomList = Load<VisualTreeAsset>(UiDir + "/RoomList.uxml");
            catalog.roomListItem = Load<VisualTreeAsset>(UiDir + "/RoomListItem.uxml");
            catalog.roomDetail = Load<VisualTreeAsset>(UiDir + "/RoomDetail.uxml");
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static PanelSettings CreatePanelSettings()
        {
            var panel = LoadOrCreate<PanelSettings>(SettingsDir + "/PanelSettings.asset");
            panel.themeStyleSheet = Load<ThemeStyleSheet>(UiDir + "/DefaultRuntimeTheme.tss");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 1920);   // portrait phone
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        private static void CreateMainScene(ApiSettings apiSettings, UiCatalog catalog, PanelSettings panelSettings)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(20, 18, 32, 255);

            var app = new GameObject("App");
            var document = app.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var bootstrap = app.AddComponent<AppBootstrap>();

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("apiSettings").objectReferenceValue = apiSettings;
            serialized.FindProperty("ui").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Reconnect";
            PlayerSettings.productName = "Reconnect";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);

            // Local backend runs on plain HTTP; allow it only in development builds.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Store requirements: IL2CPP + 64 bit on Android.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        private static void RemoveTemplateContent()
        {
            foreach (var path in new[] { "Assets/Readme.asset", "Assets/TutorialInfo", "Assets/Scenes/SampleScene.unity" })
            {
                // AssetPathExists also works for assets whose script type no longer exists.
                if (AssetDatabase.AssetPathExists(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
            if (AssetDatabase.IsValidFolder("Assets/Scenes") && AssetDatabase.FindAssets("", new[] { "Assets/Scenes" }).Length == 0)
            {
                AssetDatabase.DeleteAsset("Assets/Scenes");
            }
        }

        private static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException($"Asset missing: {path}");

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }
    }
}
