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

        /// <summary>
        /// Loads the real swisstopo tiles around Zürich HB, places the building blocks and renders
        /// the camera into client/Logs/city-preview.png. Needs internet; skipped otherwise.
        /// </summary>
        [UnityTest, Category("Integration")]
        public IEnumerator City_renders_aerial_tiles_and_buildings()
        {
            var city = UnityEngine.Object.FindFirstObjectByType<CityView>();
            Assert.IsNotNull(city, "CityView in scene");
            yield return null;   // AppBootstrap.Start has initialized the city

            city.ShowBuildings(new[]
            {
                new BuildingDto(Guid.NewGuid(), "Zürich HB", "Bahnhofplatz 1", 47.37785, 8.54018, null),
                new BuildingDto(Guid.NewGuid(), "Prime Tower", "Hardstrasse 201", 47.38622, 8.51733, null),
                new BuildingDto(Guid.NewGuid(), "Opernhaus", "Falkenstrasse 1", 47.36490, 8.54671, null),
                new BuildingDto(Guid.NewGuid(), "ETH", "Rämistrasse 101", 47.37635, 8.54798, null),
            });
            city.SetVisible(true);

            yield return WaitFor(() => city.PendingTiles == 0, 90f);
            var tiles = city.transform.Find("Tiles").childCount;
            if (tiles == 0)
            {
                Assert.Ignore("No map tiles loaded – offline?");
            }

            Assert.AreEqual(4, city.Markers.Count);
            Assert.Greater(tiles, 20, "ground covered with tiles");

            var file = SaveCameraImage(city.Camera, 1080, 1920);
            Debug.Log($"[Reconnect] City preview: {file} ({tiles} tiles)");
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

        private static string SaveCameraImage(Camera camera, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();

            camera.targetTexture = previous;
            RenderTexture.active = null;

            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "city-preview.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            target.Release();
            return path;
        }
    }
}
