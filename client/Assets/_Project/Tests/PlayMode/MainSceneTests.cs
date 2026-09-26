using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Reconnect.Client.City;
using Reconnect.Contracts.Buildings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>Starts the real Main scene. Any error logged during the test fails it (Unity Test Framework default).</summary>
    public sealed class MainSceneTests
    {
        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            PlayerPrefs.DeleteKey("reconnect.refreshToken");   // start logged out
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator App_starts_without_errors_and_shows_login()
        {
            // Session restore finishes immediately without a stored token.
            yield return WaitFor(() => Root().Q("login") != null, 10f);

            Assert.IsNotNull(Root().Q<Button>("login"), "login screen visible");
        }

        /// <summary>The room in the scene has the pictures for every item of the build catalog.</summary>
        [UnityTest]
        public IEnumerator Build_catalog_has_a_picture_for_every_item()
        {
            yield return null;
            var room = UnityEngine.Object.FindFirstObjectByType<Reconnect.Client.Rooms.RoomView>(FindObjectsInactive.Include);
            Assert.IsNotNull(room, "RoomView in scene");
            Assert.IsNotNull(room.BuildIcons, "RoomView.buildIcons is set in Main.unity (run Setup Project and commit the scene)");
            foreach (var item in Reconnect.Contracts.Rooms.ItemDefinitions.All)
            {
                Assert.IsNotNull(room.BuildIcons.Find(item.Id), "picture for " + item.Id);
            }
        }

        /// <summary>
        /// Streams the real swisstopo 3D city (terrain + aerial + swissBUILDINGS3D) via Cesium and
        /// renders three views into client/Logs: city-preview.png (overview), city-street.png
        /// (Paradeplatz, oblique) and city-closeup.png (Grossmünster). Needs internet.
        /// </summary>
        [UnityTest, Category("Integration")]
        public IEnumerator City_streams_swisstopo_3d_and_renders_views()
        {
            var city = UnityEngine.Object.FindFirstObjectByType<CityView>();
            Assert.IsNotNull(city, "CityView in scene");
            yield return null;   // AppBootstrap.Start has initialized the city

            // Render into a phone-sized target, so level of detail matches a real 1080×1920 screen.
            var target = new RenderTexture(1080, 1920, 24);
            city.Camera.targetTexture = target;

            city.ShowBuildings(new[]
            {
                new BuildingDto(Guid.NewGuid(), "Zürich HB", "Bahnhofplatz 1", 47.37785, 8.54018, null),
                new BuildingDto(Guid.NewGuid(), "Prime Tower", "Hardstrasse 201", 47.38622, 8.51733, null),
                new BuildingDto(Guid.NewGuid(), "Opernhaus", "Falkenstrasse 1", 47.36490, 8.54671, null),
                new BuildingDto(Guid.NewGuid(), "ETH", "Rämistrasse 101", 47.37635, 8.54798, null),
            });
            city.SetVisible(true);

            yield return WaitForCity(city);
            Assert.AreEqual(4, city.Markers.Count);
            Save(city, "city-preview.png");

            // Oblique view over Paradeplatz towards the lake.
            var paradeplatz = city.ToUnity(47.36970, 8.53920, 409);
            city.CameraController.Orbit(paradeplatz, distance: 450f, pitch: 32f, yaw: 160f);
            yield return WaitForCity(city);
            Save(city, "city-street.png");

            // Close-up: Grossmünster from the Limmat.
            var grossmuenster = city.ToUnity(47.37011, 8.54411, 407);
            city.CameraController.Orbit(grossmuenster, distance: 160f, pitch: 18f, yaw: 250f);
            yield return WaitForCity(city);
            Save(city, "city-closeup.png");

            foreach (var marker in city.Markers)
            {
                Debug.Log($"[Reconnect] Marker {marker.Building.Name} at y = {marker.transform.position.y:0.0} m");
            }

            city.Camera.targetTexture = null;
            target.Release();
        }

        private static IEnumerator WaitForCity(CityView city)
        {
            yield return null;   // let Cesium select tiles for the new camera position
            yield return null;
            yield return WaitFor(() => city.LoadProgress >= 99.9f, 240f);
            for (var i = 0; i < 10; i++)
            {
                yield return null;   // a few frames for raster overlays to be applied
            }
        }

        private static void Save(CityView city, string fileName)
        {
            var path = SaveCameraImage(city.Camera, fileName);
            Debug.Log($"[Reconnect] City view: {path} (camera y = {city.Camera.transform.position.y:0} m)");
        }

        private static VisualElement Root() =>
            UnityEngine.Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            var end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end)
                {
                    Assert.Fail($"Condition not met within {timeoutSeconds}s.");
                }
                yield return null;
            }
        }

        /// <summary>Renders the camera (which draws into its target texture) and writes client/Logs/&lt;fileName&gt;.</summary>
        private static string SaveCameraImage(Camera camera, string fileName)
        {
            var target = camera.targetTexture;
            camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;

            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            return path;
        }
    }
}
