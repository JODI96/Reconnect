using System.Collections;
using System.Reflection;
using CesiumForUnity;
using NUnit.Framework;
using Reconnect.Client.City;
using Reconnect.Contracts.Buildings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// Map switching (Google Photorealistic 3D Tiles ↔ swisstopo) and the height alignment it relies on.
    /// Streams swisstopo (needs internet); never starts a (billed) Google session.
    /// </summary>
    [Category("Integration")]
    public sealed class CityMapTests
    {
        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            // The app starts too: with a saved login it opens the city screen, which asks the backend for
            // the map and applies it. Let that settle so it doesn't overwrite what the test sets up.
            var end = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Google tiles are lowered by the geoid height via their Transform; this only works if Cesium
        /// positions tiles relative to the tileset's Transform. Checked with swisstopo (no key needed).
        /// </summary>
        [UnityTest]
        public IEnumerator Moving_a_tileset_moves_its_tiles()
        {
            var city = Object.FindFirstObjectByType<CityView>();
            city.SetVisible(true);
            yield return WaitForTiles(city);
            var before = city.SurfaceHeightAt(Vector3.zero);
            Assert.IsNotNull(before, "ground at Zürich HB loaded");

            const float lift = 25f;
            foreach (var tileset in new[] { Tileset(city, "terrain"), Tileset(city, "buildings") })
            {
                tileset.transform.localPosition += Vector3.up * lift;
            }
            yield return null;
            Physics.SyncTransforms();

            Assert.AreEqual(before.Value + lift, city.SurfaceHeightAt(Vector3.zero) ?? 0f, 0.5f);
        }

        [UnityTest]
        public IEnumerator Google_session_hides_swisstopo_but_keeps_it_for_roof_heights()
        {
            LogAssert.ignoreFailingMessages = true;   // the fake Google URL fails to load – that's fine here
            var city = Object.FindFirstObjectByType<CityView>();
            city.SetVisible(true);
            var dataLayer = LayerMask.NameToLayer(CityView.DataOnlyLayerName);
            Assert.GreaterOrEqual(dataLayer, 0, "layer exists (Setup Project)");

            city.ApplyMap(new MapSessionDto(MapProviders.Google, "https://example.invalid/root.json?key=test", false, 3, null, 170));
            yield return null;
            Assert.IsTrue(city.IsGoogle);
            Assert.IsTrue(Tileset(city, "googleTiles").gameObject.activeInHierarchy);
            Assert.AreEqual(dataLayer, Tileset(city, "buildings").gameObject.layer);
            Assert.AreEqual(0, city.Camera.cullingMask & (1 << dataLayer), "swisstopo not drawn");
            Assert.Less(Tileset(city, "googleTiles").transform.localPosition.y, -40f, "Google lowered by the geoid height");

            city.ApplyMap(new MapSessionDto(MapProviders.Swisstopo, null, false, 0, MapFallbackReasons.FreeQuotaUsed, 170));
            yield return null;
            Assert.IsFalse(city.IsGoogle);
            Assert.IsFalse(Tileset(city, "googleTiles").gameObject.activeSelf);
            Assert.AreEqual(0, Tileset(city, "buildings").gameObject.layer);
            Assert.AreEqual(0, Tileset(city, "terrain").gameObject.layer, "swisstopo drawn again");
        }

        private static Cesium3DTileset Tileset(CityView city, string field) =>
            (Cesium3DTileset)typeof(CityView).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(city);

        private static IEnumerator WaitForTiles(CityView city)
        {
            var end = Time.realtimeSinceStartup + 120f;
            yield return null;   // let Cesium select tiles for the camera first (progress is 100 % before that)
            yield return null;
            while (city.LoadProgress < 99.9f && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            for (var i = 0; i < 5; i++)
            {
                yield return null;
            }
        }
    }
}
