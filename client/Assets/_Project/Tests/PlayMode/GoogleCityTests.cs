using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Reconnect.Client.City;
using Reconnect.Client.Networking;
using Reconnect.Client.Rooms;
using Reconnect.Contracts;
using Reconnect.Contracts.Auth;
using Reconnect.Contracts.Buildings;
using Reconnect.Contracts.Common;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// Renders the photorealistic city (Google) and the Rooftop Lounge on the Prime Tower into
    /// client/Logs/google-*.png. EXPLICIT: starts one billed Google session, so it only runs when asked for
    /// (-testFilter GoogleCityTests). Needs the local backend with a Google key and the dev admin (Premium).
    /// </summary>
    [Explicit("Starts a billed Google Photorealistic 3D Tiles session")]
    [Category("Integration")]
    public sealed class GoogleCityTests
    {
        private const string BaseUrl = "http://localhost:5191";
        private static readonly Guid PrimeTowerId = Guid.Parse("0199a000-0000-7000-8000-000000000003");

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            var end = Time.realtimeSinceStartup + 3f;   // let the app's own start-up settle (see CityMapTests)
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Photorealistic_city_and_rooftop_on_the_prime_tower()
        {
            var api = new ApiClient(new UnityWebRequestTransport(10), BaseUrl);
            var login = api.PostAsync<AuthResponse>(ApiRoutes.Auth.Login, new LoginRequest("Admin", "Admin"));
            yield return Wait(login);
            if (!login.Result.IsSuccess)
            {
                Assert.Ignore("Backend with dev admin not running on " + BaseUrl + ".");
            }
            api.Tokens = new StaticToken(login.Result.Value.AccessToken);

            var session = api.PostAsync<MapSessionDto>(ApiRoutes.Maps.Session, new { });
            yield return Wait(session);
            Assert.AreEqual(MapProviders.Google, session.Result.Value.Provider, "backend grants Google (key configured, admin = Premium)");

            var city = UnityEngine.Object.FindFirstObjectByType<CityView>();
            var target = new RenderTexture(1080, 1920, 24);
            city.Camera.targetTexture = target;
            city.ApplyMap(session.Result.Value);
            city.ShowBuildings(new[] { new BuildingDto(PrimeTowerId, "Prime Tower", "Hardstrasse 201", 47.38622, 8.51733, null) });
            city.SetVisible(true);

            // Overview over the old town, then an oblique street view.
            city.CameraController.Orbit(city.ToUnity(47.3712, 8.5410, 408), distance: 1400f, pitch: 45f, yaw: 160f);
            yield return WaitForCity(city);
            Save(city.Camera, "google-city.png");
            city.CameraController.Orbit(city.ToUnity(47.36970, 8.53920, 409), distance: 350f, pitch: 30f, yaw: 160f);
            yield return WaitForCity(city);
            Save(city.Camera, "google-street.png");

            // Rooftop Lounge on the real roof (heights from swisstopo, Google lowered by the geoid height).
            var list = api.GetAsync<PagedResponse<RoomSummaryDto>>(ApiRoutes.Rooms.Group + "?pageSize=50");
            yield return Wait(list);
            var detail = api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(list.Result.Value.Items.Single(r => r.Name == "Clouds").Id));
            yield return Wait(detail);
            var room = detail.Result.Value;
            for (var i = 0; i < 300 && (city.RoofPosition(PrimeTowerId)?.y ?? 0f) < 50f; i++)
            {
                yield return null;
            }
            city.ShowAsBackdrop();
            var anchorTask = city.RoofAnchorAsync(PrimeTowerId, room.Width + 1f, room.Depth + 1f);
            yield return Wait(anchorTask);
            Assert.IsNotNull(anchorTask.Result, "roof found");

            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var me = Guid.NewGuid();
            view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, new[]
            {
                new RoomPlayerDto(me, "Anna", new TilePosition(9, 6)),
                new RoomPlayerDto(Guid.NewGuid(), "Ben", new TilePosition(11, 7)),
            }), me, anchorTask.Result);
            view.FrameWholeRoom();
            yield return WaitForCity(city);
            Save(view.Camera, "google-rooftop.png");

            view.Hide();
            view.Camera.targetTexture = null;
            target.Release();
        }

        private static IEnumerator WaitForCity(CityView city)
        {
            yield return null;
            yield return null;
            var end = Time.realtimeSinceStartup + 180f;
            while (city.LoadProgress < 99.9f && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            for (var i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Wait(Task task)
        {
            var end = Time.realtimeSinceStartup + 15f;
            while (!task.IsCompleted)
            {
                Assert.Less(Time.realtimeSinceStartup, end, "timed out");
                yield return null;
            }
        }

        private static void Save(Camera camera, string fileName)
        {
            var target = camera.targetTexture;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", fileName));
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log($"[Reconnect] Google view: {path}");
        }

        private sealed class StaticToken : IAccessTokenProvider
        {
            public StaticToken(string token) => AccessToken = token;
            public string AccessToken { get; }
            public Task<bool> TryRefreshAsync(System.Threading.CancellationToken ct) => Task.FromResult(false);
        }
    }
}
