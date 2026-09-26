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
using Reconnect.Contracts.Common;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// Loads the four showcase rooms from the running local backend (as the dev admin) and renders
    /// each into client/Logs/showcase-&lt;n&gt;.png. Skipped if the backend is not running.
    /// </summary>
    [Category("Integration")]
    public sealed class ShowcaseRoomTests
    {
        private const string BaseUrl = "http://localhost:5191";

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Showcase_rooms_render_with_furniture_and_players()
        {
            var api = new ApiClient(new UnityWebRequestTransport(10), BaseUrl);
            var login = api.PostAsync<AuthResponse>(ApiRoutes.Auth.Login, new LoginRequest("Admin", "Admin"));
            yield return Wait(login);
            if (!login.Result.IsSuccess)
            {
                Assert.Ignore("Backend with dev admin not running on " + BaseUrl + ".");
            }
            var tokens = new StaticToken(login.Result.Value.AccessToken);
            api.Tokens = tokens;

            var list = api.GetAsync<PagedResponse<RoomSummaryDto>>(ApiRoutes.Rooms.Group + "?pageSize=50");
            yield return Wait(list);
            var showcase = list.Result.Value.Items.Where(r => r.OwnerDisplayName == "Admin" && r.Name != "Test").OrderBy(r => r.Name).ToList();
            Assert.AreEqual(4, showcase.Count, "four showcase rooms (the sky lounge moved into the Prime Tower, 35th floor)");

            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            // Render into a portrait phone target from the start, so the camera frames the room for it.
            var target = new RenderTexture(1080, 1920, 24);
            view.Camera.targetTexture = target;
            var number = 0;
            foreach (var summary in showcase)
            {
                var detail = api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(summary.Id));
                yield return Wait(detail);

                var room = detail.Result.Value;
                var players = new[]
                {
                    new RoomPlayerDto(Guid.NewGuid(), "Anna", new TilePosition(room.Width / 2, 2)),
                    new RoomPlayerDto(Guid.NewGuid(), "Ben", new TilePosition(room.Width / 2 + 2, 3)),
                    new RoomPlayerDto(Guid.NewGuid(), "Chiara", new TilePosition(room.Width / 2 - 2, 4)),
                };
                view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, players), players[0].UserId);
                for (var frame = 0; frame < 30; frame++)
                {
                    yield return null;   // let animations settle
                }

                Assert.Greater(detail.Result.Value.Layout.Count, 25, "room is richly furnished");
                CollectionAssert.IsEmpty(view.MissingItems, room.Name + ": every item id has a model");
                number++;
                Save(view.Camera, $"showcase-{number}-avatar.png", room.Name);   // start view, zoomed on me
                view.FrameWholeRoom();
                yield return null;
                Save(view.Camera, $"showcase-{number}.png", room.Name);
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

        private static void Save(Camera camera, string fileName, string roomName)
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
            Debug.Log($"[Reconnect] Showcase {roomName}: {path}");
        }

        private sealed class StaticToken : IAccessTokenProvider
        {
            public StaticToken(string token) => AccessToken = token;
            public string AccessToken { get; }
            public Task<bool> TryRefreshAsync(System.Threading.CancellationToken ct) => Task.FromResult(false);
        }
    }
}
