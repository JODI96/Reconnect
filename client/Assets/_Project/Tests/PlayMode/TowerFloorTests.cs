using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Reconnect.Client.Networking;
using Reconnect.Client.Rooms;
using Reconnect.Contracts;
using Reconnect.Contracts.Auth;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// Renders every public floor of the Prime Tower (lobby, coworking, sky office, conference, sky lounge) into
    /// client/Logs/tower-&lt;floor&gt;.png and checks that every item has a model. Needs the local backend.
    /// </summary>
    [Category("Integration")]
    public sealed class TowerFloorTests
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
        public IEnumerator Every_tower_floor_renders_with_a_lift_and_all_models()
        {
            var api = new ApiClient(new UnityWebRequestTransport(10), BaseUrl);
            var login = api.PostAsync<AuthResponse>(ApiRoutes.Auth.Login, new LoginRequest("Admin", "Admin"));
            yield return Wait(login);
            if (!login.Result.IsSuccess)
            {
                Assert.Ignore("Backend with dev admin not running on " + BaseUrl + ".");
            }
            api.Tokens = new StaticToken(login.Result.Value.AccessToken);
            var tower = api.GetAsync<TowerDto>(ApiRoutes.Rooms.Tower(PrimeTowerId));
            yield return Wait(tower);
            var floors = tower.Result.Value.Floors.Where(f => f.IsPublic).ToList();
            Assert.AreEqual(5, floors.Count, "five public floors");

            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var target = new RenderTexture(1080, 1920, 24);
            view.Camera.targetTexture = target;
            foreach (var floor in floors)
            {
                var detail = api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(floor.RoomId));
                yield return Wait(detail);
                var room = detail.Result.Value;
                var players = Enumerable.Range(0, 4)
                    .Select(i => new RoomPlayerDto(Guid.NewGuid(), "Gast " + i, new TilePosition(room.Width / 2 - 2 + i, 2 + i % 2)))
                    .ToArray();
                view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, players), players[0].UserId);
                for (var frame = 0; frame < 30; frame++)
                {
                    yield return null;
                }

                CollectionAssert.IsEmpty(view.MissingItems, room.Name + ": every item id has a model");
                Assert.IsTrue(view.Stations.Any(s => s.GameId == RoomView.ElevatorStation), room.Name + " has a lift");
                Save(view.Camera, $"tower-{floor.Floor:00}-avatar.png");
                view.FrameWholeRoom();
                yield return null;
                Save(view.Camera, $"tower-{floor.Floor:00}.png");
                view.Hide();
            }
            view.Camera.targetTexture = null;
            target.Release();
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
