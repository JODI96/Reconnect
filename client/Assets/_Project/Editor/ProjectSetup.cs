using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CesiumForUnity;
using Reconnect.Client.City;
using Reconnect.Client.Core;
using Reconnect.Client.Rooms;
using Reconnect.Client.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor.Animations;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        private const string MarkerMaterialPath = MaterialsDir + "/Marker.mat";
        private const string SelectedMarkerMaterialPath = MaterialsDir + "/MarkerSelected.mat";
        private const string SkyMaterialPath = MaterialsDir + "/Sky.mat";
        private const string RoomFloorMaterialPath = MaterialsDir + "/RoomFloor.mat";
        private const string RoomFloorAltMaterialPath = MaterialsDir + "/RoomFloorAlt.mat";
        private const string RoomWallMaterialPath = MaterialsDir + "/RoomWall.mat";
        private const string RoomItemMaterialPath = MaterialsDir + "/RoomItem.mat";
        private const string AvatarMaterialPath = MaterialsDir + "/Avatar.mat";
        private const string GlassMaterialPath = MaterialsDir + "/Glass.mat";
        private const string WaterMaterialPath = MaterialsDir + "/Water.mat";
        private const string BuildingsMaterialPath = MaterialsDir + "/Buildings.mat";
        private const string ItemCatalogPath = SettingsDir + "/ItemCatalog.asset";
        private const string BuildIconsPath = SettingsDir + "/BuildIcons.asset";
        private const string FloorMaterialsPath = SettingsDir + "/FloorMaterials.asset";
        private const string FloorMaterialsDir = MaterialsDir + "/Floors";
        private const string TexturesDir = "Assets/ThirdParty/PolyHaven/Textures";

        /// <summary>Theme → Poly Haven texture (tools/fetch_textures.py) and how many metres one repeat covers.</summary>
        private static readonly (string Theme, string Texture, float MetresPerTile, float Smoothness)[] Floors =
        {
            ("lobby", "marble_01", 2.5f, 1f),
            ("coworking", "rectangular_parquet", 1.6f, 0.8f),
            ("office", "herringbone_parquet", 1.4f, 0.85f),
            ("conference", "poly_wool_herringbone", 1.2f, 0.4f),
            ("skylounge", "granite_tile", 2.5f, 1f),
        };
        private const string AvatarCatalogPath = SettingsDir + "/AvatarCatalog.asset";
        private const string MusicCatalogPath = SettingsDir + "/MusicCatalog.asset";
        private const string MusicDir = "Assets/ThirdParty/Music";
        private const string PostProcessingPath = SettingsDir + "/PostProcessing.asset";
        private const string AnimationDir = Root + "/Animation";
        private const string AvatarControllerPath = AnimationDir + "/Avatar.controller";
        private const string KenneyFurnitureDir = "Assets/ThirdParty/Kenney/Furniture";
        private const string PolyHavenDir = "Assets/ThirdParty/PolyHaven";

        // swisstopo (OGD, commercial use allowed with attribution "© swisstopo").
        private const string TerrainUrl = "https://3d.geo.admin.ch/ch.swisstopo.terrain.3d/v1/layer.json";
        private const string BuildingsUrl = "https://3d.geo.admin.ch/ch.swisstopo.swissbuildings3d.3d/v1/tileset.json";
        // Cesium counts {y} from the south; swisstopo's XYZ tiles count from the north → {reverseY}.
        private const string AerialUrl = "https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.swissimage/default/current/3857/{z}/{x}/{reverseY}.jpeg";
        private const string MapUrl = "https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.pixelkarte-farbe/default/current/3857/{z}/{x}/{reverseY}.jpeg";
        public const string BundleId = "ch.reconnect.app";

        [MenuItem("Reconnect/Setup Project")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);

            // 1) Create/update all assets and write them to disk.
            EnsureLayer(CityView.DataOnlyLayerName);
            LoadOrCreate<ApiSettings>(ApiSettingsPath);
            LoadOrCreate<CitySettings>(CitySettingsPath);
            CreateUiCatalog();
            CreatePanelSettings();
            CreateCityMaterials();
            CreateItemCatalog();
            CreateBuildCatalog();
            CreateFloorMaterials();
            CreateAvatarCatalog();
            CreateMusicCatalog();
            CreatePostProcessing();
            AssetDatabase.SaveAssets();

            // 2) Build the scene. It re-loads every asset by path: references held from step 1 can
            //    become invalid because Unity unloads unmodified assets while importing new ones.
            CreateMainScene();
            ConfigurePlayer();
            ConfigureRendering();
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
            catalog.room = Load<VisualTreeAsset>(UiDir + "/Room.uxml");
            catalog.offices = Load<VisualTreeAsset>(UiDir + "/Offices.uxml");
            EditorUtility.SetDirty(catalog);
        }

        private static void CreatePanelSettings()
        {
            var panel = LoadOrCreate<PanelSettings>(PanelSettingsPath);
            panel.themeStyleSheet = Load<ThemeStyleSheet>(UiDir + "/DefaultRuntimeTheme.tss");
            // Sizes in USS are points/dp: ScreenNavigator sets the scale per device density at runtime.
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = 1f;
            EditorUtility.SetDirty(panel);
        }

        private static void CreateCityMaterials()
        {
            Directory.CreateDirectory(MaterialsDir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");

            var marker = LoadOrCreateMaterial(MarkerMaterialPath, lit);
            marker.SetColor("_BaseColor", new Color32(255, 92, 138, 255));
            SetEmission(marker, new Color(0.9f, 0.15f, 0.4f));

            var selected = LoadOrCreateMaterial(SelectedMarkerMaterialPath, lit);
            selected.SetColor("_BaseColor", new Color32(255, 214, 102, 255));
            SetEmission(selected, new Color(1f, 0.7f, 0.15f));

            var sky = LoadOrCreateMaterial(SkyMaterialPath, Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.03f);
            sky.SetFloat("_AtmosphereThickness", 0.9f);
            sky.SetFloat("_Exposure", 1.2f);

            var roomMaterials = new[]
            {
                Colored(RoomFloorMaterialPath, lit, new Color32(214, 170, 120, 255)),
                Colored(RoomFloorAltMaterialPath, lit, new Color32(196, 150, 104, 255)),
                Colored(RoomWallMaterialPath, lit, new Color32(236, 226, 214, 255)),
                Colored(RoomItemMaterialPath, lit, new Color32(120, 170, 220, 255)),
                Colored(AvatarMaterialPath, lit, Color.white),
            };

            var glass = Transparent(LoadOrCreateMaterial(GlassMaterialPath, lit), new Color(0.75f, 0.9f, 1f, 0.28f), 0.95f);
            var water = Transparent(LoadOrCreateMaterial(WaterMaterialPath, lit), new Color(0.2f, 0.75f, 0.95f, 0.55f), 0.97f);

            // swissBUILDINGS3D comes untextured with raw classification colours (red roofs, yellow walls).
            // A plain lit material replaces them: a calm, light "architectural model" look; the sun shapes the volumes.
            var buildings = Colored(BuildingsMaterialPath, lit, new Color32(226, 222, 214, 255));
            buildings.SetFloat("_Smoothness", 0.15f);

            foreach (var material in roomMaterials.Concat(new[] { marker, selected, sky, glass, water, buildings }))
            {
                EditorUtility.SetDirty(material);
            }
        }

        /// <summary>
        /// ItemId = model file name, e.g. "loungeSofa" (Kenney Furniture Kit, CC0), plus "ph-" + name for the
        /// realistic Poly Haven models (CC0, glTF via glTFast, already in metres – see tools/fetch_polyhaven.py).
        /// </summary>
        /// <summary>The Kenney kit is not to one scale: these models get their real height (metres) instead of ×0.2.</summary>
        private static readonly Dictionary<string, float> KenneyRealHeights = new()
        {
            ["laptop"] = 0.24f, ["trashcan"] = 0.6f, ["toilet"] = 0.8f, ["shower"] = 2.2f, ["speakerSmall"] = 0.35f,
            ["radio"] = 0.22f, ["tableCross"] = 0.75f, ["tableCrossCloth"] = 0.75f, ["bathtub"] = 0.6f,
        };

        private static float RealSizeScale(GameObject model, float baseScale, float height)
        {
            var instance = (GameObject)Object.Instantiate(model);
            instance.transform.localScale = Vector3.one * baseScale;
            var measured = BuildCatalogGenerator.Bounds(instance).size.y;
            Object.DestroyImmediate(instance);
            return measured > 0.01f ? baseScale * height / measured : baseScale;
        }

        private static void CreateItemCatalog()
        {
            var catalog = LoadOrCreate<ItemCatalog>(ItemCatalogPath);
            catalog.modelScale = 0.2f;   // ≈ real-life size (table 65 cm, door 2 m)
            var kenney = AssetDatabase.FindAssets("t:Model", new[] { KenneyFurnitureDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path)
                .Select(path => new ItemCatalog.Entry
                {
                    itemId = Path.GetFileNameWithoutExtension(path),
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(path),
                });
            var polyHaven = Directory.Exists(PolyHavenDir)
                ? Directory.GetFiles(PolyHavenDir, "*.gltf", SearchOption.AllDirectories)
                    .Select(path => path.Replace('\\', '/'))
                    .OrderBy(path => path)
                    .Select(path => new ItemCatalog.Entry
                    {
                        itemId = "ph-" + Path.GetFileNameWithoutExtension(path),
                        model = AssetDatabase.LoadAssetAtPath<GameObject>(path),
                        scale = 1f,
                    })
                : Enumerable.Empty<ItemCatalog.Entry>();
            catalog.items = kenney.Concat(polyHaven).ToList();
            foreach (var entry in catalog.items.Where(e => e.model != null && KenneyRealHeights.ContainsKey(e.itemId)))
            {
                entry.scale = RealSizeScale(entry.model, catalog.modelScale, KenneyRealHeights[entry.itemId]);
            }

            var missing = catalog.items.Where(e => e.model == null).Select(e => e.itemId).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException("Models not imported: " + string.Join(", ", missing));
            }
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Footprints of all buildable items → Contracts (ItemCatalogData.cs); then <c>dotnet build</c>.</summary>
        private static void CreateBuildCatalog()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var custom = new CustomItems(material, material, material, RoomTheme.For("default"));
            var icons = LoadOrCreate<BuildIconCatalog>(BuildIconsPath);
            icons.items = BuildCatalogGenerator.Generate(Load<ItemCatalog>(ItemCatalogPath), custom);
            EditorUtility.SetDirty(icons);
        }

        /// <summary>Real floors (URP Lit with colour, normal and smoothness maps) for the tower themes.</summary>
        private static void CreateFloorMaterials()
        {
            Directory.CreateDirectory(FloorMaterialsDir);
            var catalog = LoadOrCreate<FloorMaterialCatalog>(FloorMaterialsPath);
            catalog.entries.Clear();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var (theme, texture, metresPerTile, smoothness) in Floors)
            {
                var folder = $"{TexturesDir}/{texture}";
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"[Reconnect] Floor texture {texture} missing – run tools/fetch_textures.py");
                    continue;
                }
                ConfigureTexture($"{folder}/{texture}_normal.jpg", normal: true, linear: true);
                ConfigureTexture($"{folder}/{texture}_smooth.png", normal: false, linear: true);
                ConfigureTexture($"{folder}/{texture}_color.jpg", normal: false, linear: false);

                var path = $"{FloorMaterialsDir}/{theme}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit);
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = lit;
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{texture}_color.jpg"));
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{texture}_normal.jpg"));
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{texture}_smooth.png"));
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_SmoothnessTextureChannel", 0f);   // smoothness from the metallic map's alpha
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", 0f);
                EditorUtility.SetDirty(material);
                catalog.entries.Add(new FloorMaterialCatalog.Entry { theme = theme, material = material, metresPerTile = metresPerTile });
            }
            EditorUtility.SetDirty(catalog);
        }

        private static void ConfigureTexture(string path, bool normal, bool linear)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !linear;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Ambient occlusion also on phones (switchable, "Grafik: Hoch"): a copy of the PC renderer's SSAO feature on the
        /// mobile renderer.
        /// </summary>
        private static void AddMobileAmbientOcclusion()
        {
            var pc = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset");
            if (pc == null || mobile == null || mobile.rendererFeatures.Exists(f => f != null && f.GetType().Name.Contains("AmbientOcclusion")))
            {
                return;
            }
            var source = pc.rendererFeatures.Find(f => f != null && f.GetType().Name.Contains("AmbientOcclusion"));
            if (source == null)
            {
                return;
            }
            var copy = Object.Instantiate(source);
            copy.name = "SSAO";
            AssetDatabase.AddObjectToAsset(copy, mobile);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(copy, out _, out long localId);
            var data = new SerializedObject(mobile);
            var features = data.FindProperty("m_RendererFeatures");
            var map = data.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = copy;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mobile);
            AssetDatabase.SaveAssets();
        }

        /// <summary>One CC0 track per room theme: file name = theme id (tools/fetch_music.py), "default" for the rest.</summary>
        private static void CreateMusicCatalog()
        {
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { MusicDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToDictionary(path => Path.GetFileNameWithoutExtension(path), AssetDatabase.LoadAssetAtPath<AudioClip>);
            var catalog = LoadOrCreate<MusicCatalog>(MusicCatalogPath);
            catalog.entries = clips.Where(c => c.Key != "default")
                .OrderBy(c => c.Key)
                .Select(c => new MusicCatalog.Entry { theme = c.Key, clip = c.Value })
                .ToArray();
            catalog.fallback = clips.TryGetValue("default", out var fallback) ? fallback : null;
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>Realistic MakeHuman figures with one Humanoid animator (see <see cref="AvatarSetup"/>).</summary>
        private static void CreateAvatarCatalog() => AvatarSetup.Build(AvatarCatalogPath, AvatarControllerPath);

        /// <summary>Warm, slightly punchy grading with soft bloom on lamps and emissive markers.</summary>
        private static void CreatePostProcessing()
        {
            var profile = LoadOrCreate<VolumeProfile>(PostProcessingPath);
            foreach (var component in profile.components.ToList())
            {
                Object.DestroyImmediate(component, true);
            }
            profile.components.Clear();

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.45f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.65f);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(12f);
            color.saturation.Override(14f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);

            foreach (var component in profile.components)
            {
                component.name = component.GetType().Name;
                if (!AssetDatabase.IsSubAsset(component))
                {
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
            }
            EditorUtility.SetDirty(profile);
        }

        /// <summary>Switches a URP Lit material to alpha-blended transparency.</summary>
        private static Material Transparent(Material material, Color color, float smoothness)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static Material Colored(string path, Shader shader, Color color)
        {
            var material = LoadOrCreateMaterial(path, shader);
            material.SetColor("_BaseColor", color);
            return material;
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
            cameraGo.AddComponent<AudioListener>();   // without one Unity plays no sound at all (room music)
            camera.clearFlags = CameraClearFlags.Skybox;
            var cameraController = cameraGo.AddComponent<CityCameraController>();
            var cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Load<VolumeProfile>(PostProcessingPath);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            RenderSettings.skybox = Load<Material>(SkyMaterialPath);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            // Light haze towards the horizon, like looking over a real city.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.75f, 0.82f, 0.9f);
            RenderSettings.fogStartDistance = 2500f;
            RenderSettings.fogEndDistance = 30000f;

            // UI Toolkit + new Input System: an EventSystem with the Input System module routes pointer input.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // Cesium: georeference (origin set at runtime from CitySettings) with swisstopo tilesets below it.
            var georeference = new GameObject("CesiumGeoreference").AddComponent<CesiumGeoreference>();

            var terrain = CreateTileset(georeference.transform, "Terrain (swissALTI3D)", TerrainUrl);
            var aerial = terrain.gameObject.AddComponent<CesiumUrlTemplateRasterOverlay>();
            ConfigureOverlay(aerial, AerialUrl, maximumLevel: 20);
            var map = terrain.gameObject.AddComponent<CesiumUrlTemplateRasterOverlay>();
            ConfigureOverlay(map, MapUrl, maximumLevel: 19);

            var buildings = CreateTileset(georeference.transform, "Buildings (swissBUILDINGS3D)", BuildingsUrl);
            buildings.opaqueMaterial = Load<Material>(BuildingsMaterialPath);

            // Google Photorealistic 3D Tiles: URL (with the key) comes from the backend per session, so the
            // tileset starts empty and disabled. Google's terms require its logo and credits on screen.
            var google = CreateTileset(georeference.transform, "Photorealistic (Google)", "");
            google.showCreditsOnScreen = true;
            google.createPhysicsMeshes = false;   // collisions and heights come from swisstopo
            google.gameObject.SetActive(false);

            var cityGo = new GameObject("City");
            var cityView = cityGo.AddComponent<CityView>();
            Assign(cityView,
                ("georeference", georeference),
                ("terrain", terrain),
                ("buildings", buildings),
                ("googleTiles", google),
                ("aerialOverlay", aerial),
                ("mapOverlay", map),
                ("cameraController", cameraController),
                ("markerMaterial", Load<Material>(MarkerMaterialPath)),
                ("selectedMarkerMaterial", Load<Material>(SelectedMarkerMaterialPath)));

            var roomView = new GameObject("Room").AddComponent<RoomView>();
            Assign(roomView,
                ("roomCamera", camera),
                ("floorMaterial", Load<Material>(RoomFloorMaterialPath)),
                ("wallMaterial", Load<Material>(RoomWallMaterialPath)),
                ("itemMaterial", Load<Material>(RoomItemMaterialPath)),
                ("avatarMaterial", Load<Material>(AvatarMaterialPath)),
                ("glassMaterial", Load<Material>(GlassMaterialPath)),
                ("waterMaterial", Load<Material>(WaterMaterialPath)),
                ("itemCatalog", Load<ItemCatalog>(ItemCatalogPath)),
                ("buildIcons", Load<BuildIconCatalog>(BuildIconsPath)),
                ("floorMaterials", Load<FloorMaterialCatalog>(FloorMaterialsPath)),
                ("avatarCatalog", Load<AvatarCatalog>(AvatarCatalogPath)));
            roomView.gameObject.AddComponent<AudioSource>();
            Assign(roomView.gameObject.AddComponent<RoomMusic>(), ("catalog", Load<MusicCatalog>(MusicCatalogPath)));

            var app = new GameObject("App");
            var document = app.AddComponent<UIDocument>();
            document.panelSettings = Load<PanelSettings>(PanelSettingsPath);
            var bootstrap = app.AddComponent<AppBootstrap>();
            Assign(bootstrap,
                ("apiSettings", Load<ApiSettings>(ApiSettingsPath)),
                ("citySettings", Load<CitySettings>(CitySettingsPath)),
                ("ui", Load<UiCatalog>(UiCatalogPath)),
                ("city", cityView),
                ("room", roomView));
            var rendererList = new SerializedObject(bootstrap).FindProperty("renderers");
            var rendererPaths = new[] { "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/PC_Renderer.asset" };
            rendererList.arraySize = rendererPaths.Length;
            for (var i = 0; i < rendererPaths.Length; i++)
            {
                rendererList.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPaths[i]);
            }
            rendererList.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>Adds a user layer (Project Settings > Tags and Layers) if it doesn't exist yet.</summary>
        private static void EnsureLayer(string name)
        {
            if (LayerMask.NameToLayer(name) >= 0)
            {
                return;
            }
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (var i = 8; i < layers.arraySize; i++)   // 0–7 are Unity's built-in layers
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
            throw new InvalidOperationException("No free user layer for " + name);
        }

        private static Cesium3DTileset CreateTileset(Transform parent, string name, string url)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tileset = go.AddComponent<Cesium3DTileset>();
            tileset.tilesetSource = CesiumDataSource.FromUrl;
            tileset.url = url;
            tileset.createPhysicsMeshes = true;   // camera collision, panning on roofs/hills, height sampling
            tileset.showCreditsOnScreen = false;  // our UI shows "© swisstopo"
            return tileset;
        }

        private static void ConfigureOverlay(CesiumUrlTemplateRasterOverlay overlay, string url, int maximumLevel)
        {
            overlay.templateUrl = url;
            overlay.projection = CesiumUrlTemplateRasterOverlayProjection.WebMercator;
            overlay.minimumLevel = 0;
            overlay.maximumLevel = maximumLevel;
            overlay.tileWidth = 256;
            overlay.tileHeight = 256;
        }

        /// <summary>
        /// GPU Resident Drawer: Unity draws the many furniture meshes of a room instanced on the GPU instead of one
        /// draw call each (needs Forward+, which both renderers use). Big win on phones in full rooms.
        /// </summary>
        private static void ConfigureRendering()
        {
            AddMobileAmbientOcclusion();
            foreach (var path in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null)
                {
                    continue;
                }
                var settings = new SerializedObject(asset);
                settings.FindProperty("m_GPUResidentDrawerMode").intValue = 1;   // GPUResidentDrawerMode.InstancedDrawing
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            // The drawer needs the BatchRendererGroup shader variants in builds.
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            graphics.FindProperty("m_BrgStripping").intValue = 2;   // keep all
            graphics.ApplyModifiedPropertiesWithoutUndo();
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

            // Phones only in portrait: the UI is laid out for it, rotating would break it.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

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
