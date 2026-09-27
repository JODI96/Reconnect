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
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// The dev admin's own storey of the Prime Tower (furnished as the penthouse, see ResidenceDesigns) at its real height in
    /// the city: an overview from all four sides and a close look at every part of the flat → client/Logs/penthouse-*.png.
    /// Needs the local backend with the admin owning a storey, and internet (RECONNECT_TEST_GOOGLE=1: photorealistic city).
    /// </summary>
    [Category("Integration")]
    public sealed class PenthouseTests
    {
        private const string BaseUrl = "http://localhost:5191";
        private static readonly Guid PrimeTowerId = Guid.Parse("0199a000-0000-7000-8000-000000000003");

        /// <summary>Parts of the flat in room metres (see ResidenceDesigns.Penthouse).</summary>
        private static readonly (string Name, Vector2 Focus)[] Parts =
        {
            ("pool", new Vector2(47f, 9f)), ("spa", new Vector2(49f, 17f)), ("gym", new Vector2(50f, 25f)),
            ("living", new Vector2(31f, 9f)), ("kitchen", new Vector2(30f, 26.5f)), ("bath", new Vector2(5.5f, 13f)),
            ("dressing", new Vector2(11.5f, 11.5f)), ("bedroom", new Vector2(7.5f, 24f)), ("office", new Vector2(18.5f, 24f)),
            ("gaming", new Vector2(19f, 8.5f)), ("hall", new Vector2(19f, 15.5f)),
        };

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            var end = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator The_penthouse_in_the_tower()
        {
            var api = new ApiClient(new UnityWebRequestTransport(10), BaseUrl);
            var login = api.PostAsync<AuthResponse>(ApiRoutes.Auth.Login, new LoginRequest("Admin", "Admin"));
            yield return Wait(login, 15f);
            if (!login.Result.IsSuccess)
            {
                Assert.Ignore("Backend with dev admin not running on " + BaseUrl + ".");
            }
            api.Tokens = new StaticToken(login.Result.Value.AccessToken);
            var floors = api.GetAsync<TowerDto>(ApiRoutes.Rooms.Tower(PrimeTowerId));
            yield return Wait(floors, 15f);
            var mine = floors.Result.Value.Floors.FirstOrDefault(f => f.IsMine);
            if (mine == null)
            {
                Assert.Ignore("The dev admin owns no storey of the Prime Tower.");
            }
            var detail = api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(mine.RoomId));
            yield return Wait(detail, 15f);
            var room = detail.Result.Value;

            var city = UnityEngine.Object.FindFirstObjectByType<CityView>();
            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var target = new RenderTexture(1080, 1920, 24);
            view.Camera.targetTexture = target;
            if (Environment.GetEnvironmentVariable("RECONNECT_TEST_GOOGLE") == "1")
            {
                var session = api.PostAsync<MapSessionDto>(ApiRoutes.Maps.Session, new { });
                yield return Wait(session, 15f);
                city.ApplyMap(session.Result.Value);
            }
            var nearby = api.GetAsync<System.Collections.Generic.List<BuildingDto>>(ApiRoutes.Buildings.Nearby + "?lat=47.38622&lng=8.51733&radiusMeters=50");
            yield return Wait(nearby, 15f);
            city.ShowBuildings(nearby.Result.Value);
            city.SetVisible(true);
            for (var i = 0; i < 300 && (city.RoofPosition(PrimeTowerId)?.y ?? 0f) < 50f; i++)
            {
                yield return null;
            }
            city.ShowAsBackdrop();
            var measure = city.GetTowerAsync(PrimeTowerId);
            yield return Wait(measure, 60f);
            var tower = measure.Result;
            Assert.IsNotNull(tower, "tower measured");

            city.ShowTowerCutaway(tower, mine.Floor);
            var (centre, yaw) = city.StoreyPlacement(tower, room, room.Width, room.Depth);
            var me = Guid.NewGuid();
            view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, new[] { new RoomPlayerDto(me, "Admin", new TilePosition(30, 12)) }), me, centre, yaw);
            Assert.IsEmpty(view.MissingItems, "every item of the flat is built: " + string.Join(", ", view.MissingItems));

            view.LookAt(new Vector2(room.Width / 2f, room.Depth / 2f), 22f);
            for (var turn = 0; turn < 4; turn++)
            {
                yield return Settle(city);
                Save(view.Camera, $"penthouse-overview{turn}.png");
                if (turn == 0)
                {
                    // Budget like the crowded lobby (CrowdPerformanceTests, normal graphics): set-pass calls for the frame with the
                    // city, triangles for the flat's own furniture. Batches are logged (with URP's SRP batcher they are cheap).
                    Debug.Log($"[Reconnect] Penthouse with the city: {UnityEditor.UnityStats.batches} batches, " +
                              $"{UnityEditor.UnityStats.setPassCalls} set-pass calls, {UnityEditor.UnityStats.triangles} triangles");
                    Assert.LessOrEqual(UnityEditor.UnityStats.setPassCalls, 80, "set-pass calls");
                    var (renderers, triangles) = FlatCost(view);
                    Assert.LessOrEqual(triangles, 400000, "triangles of the flat's furniture");
                    Assert.LessOrEqual(renderers, 1000, "renderers of the flat's furniture");
                }
                view.RotateView(1);
            }
            foreach (var (name, focus) in Parts)
            {
                view.LookAt(focus, 12f);
                yield return Settle(city);
                Save(view.Camera, $"penthouse-{name}.png");
            }

            view.Hide();
            city.HideTowerCutaway();
            view.Camera.targetTexture = null;
            target.Release();
        }

        /// <summary>Renderers and triangles per kind of item, heaviest first (to see what to slim down); returns the totals.</summary>
        private static (int Renderers, int Triangles) FlatCost(RoomView view)
        {
            var costs = new System.Collections.Generic.Dictionary<string, (int Renderers, int Triangles)>();
            foreach (var renderer in view.GetComponentsInChildren<MeshRenderer>())
            {
                var item = renderer.transform;
                while (item.parent != null && item.parent.name != "Furniture")
                {
                    item = item.parent;
                }
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                var triangles = mesh != null ? (int)(mesh.GetIndexCount(0) / 3) : 0;
                costs.TryGetValue(item.name, out var c);
                costs[item.name] = (c.Renderers + 1, c.Triangles + triangles);
            }
            Debug.Log($"[Reconnect] Flat total: {costs.Values.Sum(c => c.Renderers)} renderers, {costs.Values.Sum(c => c.Triangles)} triangles");
            Debug.Log("[Reconnect] Most renderers: " + string.Join(", ", costs.OrderByDescending(c => c.Value.Renderers).Take(15)
                .Select(c => $"{c.Key} {c.Value.Renderers}r/{c.Value.Triangles}t")));
            Debug.Log("[Reconnect] Most triangles: " + string.Join(", ", costs.OrderByDescending(c => c.Value.Triangles).Take(15)
                .Select(c => $"{c.Key} {c.Value.Renderers}r/{c.Value.Triangles}t")));
            return (costs.Values.Sum(c => c.Renderers), costs.Values.Sum(c => c.Triangles));
        }

        private static IEnumerator Settle(CityView city)
        {
            for (var frame = 0; frame < 40; frame++)
            {
                yield return null;   // the view moves smoothly
            }
            var end = Time.realtimeSinceStartup + 120f;
            while (city.LoadProgress < 99.9f && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            for (var i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Wait(Task task, float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (!task.IsCompleted)
            {
                Assert.Less(Time.realtimeSinceStartup, end, "timed out");
                yield return null;
            }
            if (task.IsFaulted)
            {
                throw task.Exception!.InnerException!;
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
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", fileName)), image.EncodeToPNG());
        }

        private sealed class StaticToken : IAccessTokenProvider
        {
            public StaticToken(string token) => AccessToken = token;
            public string AccessToken { get; }
            public Task<bool> TryRefreshAsync(System.Threading.CancellationToken ct) => Task.FromResult(false);
        }
    }
}
