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
    /// Measures the real Prime Tower from swisstopo and shows lobby, coworking (12th) and Clouds (35th) at their
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
            Assert.That(tower.RoofY - tower.GroundY, Is.InRange(100f, 140f), "Prime Tower is 126 m high");
            Assert.That(tower.SizeX, Is.InRange(15f, 75f));
            Assert.That(tower.SizeZ, Is.InRange(15f, 75f));

            foreach (var storey in new[] { 0, 12, 35 })
            {
                var floor = floors.Result.Value.Floors.First(f => f.Floor == storey && f.IsPublic);
                var detail = api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(floor.RoomId));
                yield return Wait(detail, 15f);
                var room = detail.Result.Value;
                var me = Guid.NewGuid();
                city.ShowTowerCutaway(tower, storey);
                view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, new[]
                {
                    new RoomPlayerDto(me, "Anna", new TilePosition(room.Width / 2, 3)),
                    new RoomPlayerDto(Guid.NewGuid(), "Ben", new TilePosition(room.Width / 2 + 1, 4)),
                }), me, tower.RoomAnchor(storey, room.Width, room.Depth), tower.Yaw);

                Assert.AreEqual(tower.FloorAnchor(storey).y, view.transform.position.y, 0.01f, "floor at its real height");
                view.FrameWholeRoom();
                yield return WaitForCity(city);
                Save(view.Camera, $"tower-city-{storey:00}.png");
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
