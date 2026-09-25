using System;
using System.IO;
using Reconnect.Client.City;
using Reconnect.Client.Core;
using Reconnect.Client.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

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
        private const string MaterialsDir = Root + "/Materials";
        private const string ScenePath = Root + "/Scenes/Main.unity";
        private const string ApiSettingsPath = SettingsDir + "/ApiSettings.asset";
        private const string CitySettingsPath = SettingsDir + "/CitySettings.asset";
        private const string UiCatalogPath = SettingsDir + "/UiCatalog.asset";
        private const string PanelSettingsPath = SettingsDir + "/PanelSettings.asset";
        private const string TileMaterialPath = MaterialsDir + "/MapTile.mat";
        private const string BuildingMaterialPath = MaterialsDir + "/Building.mat";
        private const string SelectedBuildingMaterialPath = MaterialsDir + "/BuildingSelected.mat";
        public const string BundleId = "ch.reconnect.app";

        [MenuItem("Reconnect/Setup Project")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);

            // 1) Create/update all assets and write them to disk.
            LoadOrCreate<ApiSettings>(ApiSettingsPath);
            LoadOrCreate<CitySettings>(CitySettingsPath);
            CreateUiCatalog();
            CreatePanelSettings();
            CreateCityMaterials();
            AssetDatabase.SaveAssets();

            // 2) Build the scene. It re-loads every asset by path: references held from step 1 can
            //    become invalid because Unity unloads unmodified assets while importing new ones.
            CreateMainScene();
            ConfigurePlayer();
            RemoveTemplateContent();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Reconnect] Project setup complete.");
        }

        private static void CreateUiCatalog()
        {
            var catalog = LoadOrCreate<UiCatalog>(UiCatalogPath);
            catalog.theme = Load<StyleSheet>(UiDir + "/Theme.uss");
            catalog.login = Load<VisualTreeAsset>(UiDir + "/Login.uxml");
            catalog.register = Load<VisualTreeAsset>(UiDir + "/Register.uxml");
            catalog.roomList = Load<VisualTreeAsset>(UiDir + "/RoomList.uxml");
            catalog.roomListItem = Load<VisualTreeAsset>(UiDir + "/RoomListItem.uxml");
            catalog.roomDetail = Load<VisualTreeAsset>(UiDir + "/RoomDetail.uxml");
            catalog.city = Load<VisualTreeAsset>(UiDir + "/City.uxml");
            EditorUtility.SetDirty(catalog);
        }

        private static void CreatePanelSettings()
        {
            var panel = LoadOrCreate<PanelSettings>(PanelSettingsPath);
            panel.themeStyleSheet = Load<ThemeStyleSheet>(UiDir + "/DefaultRuntimeTheme.tss");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 1920);   // portrait phone
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
        }

        private static void CreateCityMaterials()
        {
            Directory.CreateDirectory(MaterialsDir);
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var lit = Shader.Find("Universal Render Pipeline/Lit");

            var tile = LoadOrCreateMaterial(TileMaterialPath, unlit);
            tile.SetFloat("_ZWrite", 0f);   // tiles are layered by render queue (zoom), not by depth

            var building = LoadOrCreateMaterial(BuildingMaterialPath, lit);
            building.SetColor("_BaseColor", new Color32(255, 92, 138, 255));
            SetEmission(building, new Color(0.45f, 0.08f, 0.2f));

            var selected = LoadOrCreateMaterial(SelectedBuildingMaterialPath, lit);
            selected.SetColor("_BaseColor", new Color32(255, 214, 102, 255));
            SetEmission(selected, new Color(0.9f, 0.6f, 0.1f));

            foreach (var material in new[] { tile, building, selected })
            {
                EditorUtility.SetDirty(material);
            }
        }

        private static void SetEmission(Material material, Color color)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        private static void CreateMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(20, 18, 32, 255);
            var cameraController = cameraGo.AddComponent<CityCameraController>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Distant map edge fades into the background colour instead of ending abruptly.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 3500f;
            RenderSettings.fogEndDistance = 7500f;

            // UI Toolkit + new Input System: an EventSystem with the Input System module routes pointer input.
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetAsLastSibling();

            var cityGo = new GameObject("City");
            var cityView = cityGo.AddComponent<CityView>();
            Assign(cityView,
                ("cameraController", cameraController),
                ("tileMaterial", Load<Material>(TileMaterialPath)),
                ("buildingMaterial", Load<Material>(BuildingMaterialPath)),
                ("selectedBuildingMaterial", Load<Material>(SelectedBuildingMaterialPath)));

            var app = new GameObject("App");
            var document = app.AddComponent<UIDocument>();
            document.panelSettings = Load<PanelSettings>(PanelSettingsPath);
            var bootstrap = app.AddComponent<AppBootstrap>();
            Assign(bootstrap,
                ("apiSettings", Load<ApiSettings>(ApiSettingsPath)),
                ("citySettings", Load<CitySettings>(CitySettingsPath)),
                ("ui", Load<UiCatalog>(UiCatalogPath)),
                ("city", cityView));

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

        /// <summary>Sets private [SerializeField] references, as dragging them in the Inspector would.</summary>
        private static void Assign(Object target, params (string Field, Object Value)[] values)
        {
            var serialized = new SerializedObject(target);
            foreach (var (field, value) in values)
            {
                var property = serialized.FindProperty(field)
                    ?? throw new MissingFieldException(target.GetType().Name, field);
                if (value == null)   // Unity's == also catches destroyed/unloaded objects
                {
                    throw new InvalidOperationException($"{target.GetType().Name}.{field}: value is missing or was unloaded.");
                }
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
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
