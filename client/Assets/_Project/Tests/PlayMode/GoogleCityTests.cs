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
    /// Renders the photorealistic city (Google) and Prime Tower storeys in the cut-away tower into
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
            var nearby = api.GetAsync<System.Collections.Generic.List<BuildingDto>>(ApiRoutes.Buildings.Nearby + "?lat=47.38622&lng=8.51733&radiusMeters=50");
            yield return Wait(nearby);
            Assert.IsNotNull(nearby.Result.Value.Single(b => b.Id == PrimeTowerId).Footprint, "Prime Tower has a ground plan");
            city.ShowBuildings(nearby.Result.Value);
            city.SetVisible(true);

            // Overview over the old town, then an oblique street view.
            city.CameraController.Orbit(city.ToUnity(47.3712, 8.5410, 408), distance: 1400f, pitch: 45f, yaw: 160f);
            yield return WaitForCity(city);
            Save(city.Camera, "google-city.png");
            city.CameraController.Orbit(city.ToUnity(47.36970, 8.53920, 409), distance: 350f, pitch: 30f, yaw: 160f);
            yield return WaitForCity(city);
            Save(city.Camera, "google-street.png");

            // Tower storeys at their real height: the real (Google) tower is clipped out, our cut-away model replaces it.
            var floors = api.GetAsync<TowerDto>(ApiRoutes.Rooms.Tower(PrimeTowerId));
            yield return Wait(floors);
            for (var i = 0; i < 300 && (city.RoofPosition(PrimeTowerId)?.y ?? 0f) < 50f; i++)
            {
                yield return null;
            }
            city.ShowAsBackdrop();
            var measure = city.GetTowerAsync(PrimeTowerId);
            var measureEnd = Time.realtimeSinceStartup + 60f;
            while (!measure.IsCompleted && Time.realtimeSinceStartup < measureEnd)
            {
                yield return null;
            }
            var tower = measure.Result;
            Assert.IsNotNull(tower, "tower measured");

            // Check view: the ground plan (OpenStreetMap) as a glowing frame on the real Google tower.
            var frame = new GameObject("Outline Debug");
            var glow = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            glow.SetColor("_BaseColor", Color.magenta);
            for (var i = 0; i < tower.Outline.Count; i++)
            {
                var a = tower.Outline[i];
                var b = tower.Outline[(i + 1) % tower.Outline.Count];
                var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                edge.transform.SetParent(frame.transform, false);
                edge.transform.position = new Vector3((a.x + b.x) / 2f, tower.RoofY + 1f, (a.y + b.y) / 2f);
                edge.transform.rotation = Quaternion.LookRotation(new Vector3(b.x - a.x, 0f, b.y - a.y));
                edge.transform.localScale = new Vector3(0.6f, 0.6f, Vector2.Distance(a, b));
                edge.GetComponent<Renderer>().sharedMaterial = glow;
            }
            city.SetVisible(true);
            city.CameraController.Orbit(new Vector3(tower.Center.x, tower.RoofY, tower.Center.z), distance: 260f, pitch: 70f, yaw: 0f);
            yield return WaitForCity(city);
            Save(city.Camera, "google-outline.png");
            city.ShowTowerCutaway(tower, 24);
            yield return WaitForCity(city);
            var settleTop = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < settleTop)
            {
                yield return null;
            }
            Save(city.Camera, "google-outline-cut.png");
            city.CameraController.Orbit(new Vector3(tower.Center.x, tower.GroundY + 60f, tower.Center.z), distance: 320f, pitch: 25f, yaw: 200f);
            yield return WaitForCity(city);
            Save(city.Camera, "google-outline-side.png");
            city.HideTowerCutaway();
            UnityEngine.Object.Destroy(frame);
            city.ShowAsBackdrop();

            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            foreach (var storey in new[] { 0, 24, 35 })
            {
                var floor = floors.Result.Value.Floors.First(f => f.Floor == storey && f.IsPublic);
                var detail = api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(floor.RoomId));
                yield return Wait(detail);
                var room = detail.Result.Value;
                var me = Guid.NewGuid();
                city.ShowTowerCutaway(tower, storey);
                view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, new[]
                {
                    new RoomPlayerDto(me, "Anna", new TilePosition(room.Width / 2, 3)),
                    new RoomPlayerDto(Guid.NewGuid(), "Ben", new TilePosition(room.Width / 2 + 1, 4)),
                }), me, tower.RoomAnchor(storey, room.Width, room.Depth), tower.Yaw);
                view.FrameWholeRoom();
                yield return WaitForCity(city);
                var settle = Time.realtimeSinceStartup + 6f;   // clipping is rasterised per tile, asynchronously
                while (Time.realtimeSinceStartup < settle)
                {
                    yield return null;
                }
                yield return WaitForCity(city);
                Save(view.Camera, $"google-tower-{storey:00}.png");
                view.Hide();
            }
            city.HideTowerCutaway();

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
