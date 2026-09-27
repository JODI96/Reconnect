using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
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
    /// Measures the real Prime Tower from swisstopo and shows lobby, coworking (12th) and the sky lounge (35th) at their
    /// real height in the cut-away tower, with the streamed city around → client/Logs/tower-city-*.png.
    /// swisstopo only (no Google session). Needs the local backend and internet.
    /// </summary>
    [Category("Integration")]
    public sealed class TowerInCityTests
    {
        private const string BaseUrl = "http://localhost:5191";
        private static readonly Guid PrimeTowerId = Guid.Parse("0199a000-0000-7000-8000-000000000003");

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
        [Timeout(900000)]   // streams the city for every storey and view
        public IEnumerator Tower_floors_stand_at_their_real_height_in_the_cut_away_tower()
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

            var city = UnityEngine.Object.FindFirstObjectByType<CityView>();
            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var target = new RenderTexture(1080, 1920, 24);
            view.Camera.targetTexture = target;
            var nearby = api.GetAsync<System.Collections.Generic.List<BuildingDto>>(ApiRoutes.Buildings.Nearby + "?lat=47.38622&lng=8.51733&radiusMeters=50");
            yield return Wait(nearby, 15f);
            Assert.IsNotNull(nearby.Result.Value.Single(b => b.Id == PrimeTowerId).Footprint, "Prime Tower has a ground plan");
            if (Environment.GetEnvironmentVariable("RECONNECT_TEST_GOOGLE") == "1")
            {
                // Opt-in (a Google session is billed): the same views over the photorealistic city.
                var session = api.PostAsync<MapSessionDto>(ApiRoutes.Maps.Session, new { });
                yield return Wait(session, 15f);
                city.ApplyMap(session.Result.Value);
            }
            city.ShowBuildings(nearby.Result.Value);
            city.SetVisible(true);
            for (var i = 0; i < 300 && (city.RoofPosition(PrimeTowerId)?.y ?? 0f) < 50f; i++)
            {
                yield return null;
            }
            city.ShowAsBackdrop();

            // Like the room screen entering the lobby: right after measuring, cut the tower out and hide the city – in
            // the same continuation (this crashed natively when it ran inside the tileset's Update).
            var measure = city.GetTowerAsync(PrimeTowerId).ContinueWith(t =>
            {
                if (t.Result != null)
                {
                    city.ShowTowerCutaway(t.Result, 0);
                    city.SetVisible(false);
                }
                return t.Result;
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.FromCurrentSynchronizationContext());
            yield return Wait(measure, 60f);
            var tower = measure.Result;
            Assert.IsNotNull(tower, "tower measured");
            for (var frame = 0; frame < 10; frame++)
            {
                yield return null;
            }
            city.HideTowerCutaway();
            city.ShowAsBackdrop();
            Assert.That(tower.RoofY - tower.GroundY, Is.InRange(100f, 140f), "Prime Tower is 126 m high");
            Assert.That(tower.SizeX, Is.InRange(15f, 75f));
            Assert.That(tower.SizeZ, Is.InRange(15f, 75f));

            foreach (var storey in new[] { 0, 12, 24, 35 })
            {
                var floor = floors.Result.Value.Floors.First(f => f.Floor == storey && f.IsPublic);
                var detail = api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(floor.RoomId));
                yield return Wait(detail, 15f);
                var room = detail.Result.Value;
                var me = Guid.NewGuid();
                city.ShowAsBackdrop();
                city.ShowTowerCutaway(tower, storey);
                if (RoomView.HidesCity(storey))
                {
                    city.SetVisible(false);   // as the room screen does: low storeys float in black
                }
                var (centre, yaw) = city.StoreyPlacement(tower, room, room.Width, room.Depth);
                view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, new[]
                {
                    new RoomPlayerDto(me, "Anna", new TilePosition(room.Width / 2, 3)),
                    new RoomPlayerDto(Guid.NewGuid(), "Ben", new TilePosition(room.Width / 2 + 1, 4)),
                }), me, centre, yaw);

                Assert.AreEqual(tower.FloorAnchor(storey).y, view.transform.position.y, 0.01f, "floor at its real height");
                // The storey's glass stands on the tower's facade: every outline corner of the room lies on the tower's
                // outline (a few cm of rounding allowed), so no floor plate shows outside and nothing overhangs.
                Assert.IsNotNull(room.Outline, "tower storeys have the real outline");
                var worst = room.Outline.Max(p =>
                {
                    var world = view.transform.TransformPoint(new Vector3(p.X, 0f, p.Z));
                    return DistanceToOutline(tower.Outline, new Vector2(world.x, world.z));
                });
                Debug.Log($"[Reconnect] Storey {storey}: room outline is at most {worst:0.00} m off the tower's outline");
                Assert.Less(worst, 0.25f, "room outline on the tower's facade");
                view.FrameWholeRoom();
                yield return WaitForCity(city, storey);
                Save(view.Camera, $"tower-city-{storey:00}.png");
                // What a player sees: close to the people, turned all four ways.
                view.LookAt(new Vector2(room.Width / 2f, room.Depth / 2f), 22f);
                for (var turn = 0; turn < 4; turn++)
                {
                    for (var frame = 0; frame < 40; frame++)
                    {
                        yield return null;   // the view turns smoothly
                    }
                    yield return WaitForCity(city, storey);
                    Save(view.Camera, $"tower-city-{storey:00}-view{turn}.png");
                    view.RotateView(1);
                }
                view.Hide();
            }

            city.HideTowerCutaway();
            view.Camera.targetTexture = null;
            target.Release();
        }

        private static float DistanceToOutline(System.Collections.Generic.IReadOnlyList<Vector2> outline, Vector2 point)
        {
            var best = float.MaxValue;
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                var t = Mathf.Clamp01(Vector2.Dot(point - a, b - a) / Mathf.Max(1e-6f, (b - a).sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(point, a + (b - a) * t));
            }
            return best;
        }

        private static IEnumerator WaitForCity(CityView city, int storey = -1)
        {
            yield return null;
            yield return null;
            if (RoomView.HidesCity(storey))
            {
                yield break;   // no city to stream
            }
            var end = Time.realtimeSinceStartup + 180f;
            while (city.LoadProgress < 99.9f && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            for (var i = 0; i < 15; i++)
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
