using System;
using System.Collections;
using System.Collections.Generic;
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
    /// The lake bath (Seebad Utoquai): the pool is sunk into the deck, its tiles are water; people in it swim with the
    /// head just above the surface, one walks from the deck into the pool, one lies on a sun lounger.
    /// Renders client/Logs/pool.png and pool-close.png. Needs the local backend.
    /// </summary>
    [Category("Integration")]
    public sealed class PoolTests
    {
        private const string BaseUrl = "http://localhost:5191";

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator People_swim_in_the_pool_and_lie_on_loungers()
        {
            var api = new ApiClient(new UnityWebRequestTransport(10), BaseUrl);
            var login = api.PostAsync<AuthResponse>(ApiRoutes.Auth.Login, new LoginRequest("Admin", "Admin"));
            yield return Wait(login);
            if (!login.Result.IsSuccess)
            {
                Assert.Ignore("Backend with dev admin not running on " + BaseUrl + ".");
            }
            api.Tokens = new StaticToken(login.Result.Value.AccessToken);
            var list = api.GetAsync<PagedResponse<RoomSummaryDto>>(ApiRoutes.Rooms.Group + "?pageSize=50");
            yield return Wait(list);
            var detail = api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(list.Result.Value.Items.Single(r => r.Name == "Seebad Utoquai").Id));
            yield return Wait(detail);
            var room = detail.Result.Value;
            var lounger = room.Layout.Select((item, index) => (item.ItemId, index)).First(i => i.ItemId == "custom-lounger").index;

            var swimmers = new[] { new Vector2Int(8, 7), new Vector2Int(12, 9), new Vector2Int(15, 6) };
            var players = swimmers.Select((tile, i) => new RoomPlayerDto(Guid.NewGuid(), "Schwimmer " + i, new TilePosition(tile.x, tile.y))).ToList();
            var walker = new RoomPlayerDto(Guid.NewGuid(), "Springt rein", new TilePosition(10, 3));
            var sunbather = new RoomPlayerDto(Guid.NewGuid(), "Sonnt sich", new TilePosition(6, 3), new SeatDto(lounger, 0));
            var me = new RoomPlayerDto(Guid.NewGuid(), "Ich", new TilePosition(3, 6));
            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var target = new RenderTexture(1080, 1920, 24);
            view.Camera.targetTexture = target;
            view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, players.Append(walker).Append(sunbather).Append(me).ToList()), me.UserId);
            view.MovePlayer(walker.UserId, new TilePosition(10, 7));   // from the deck into the pool
            var end = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }

            Assert.IsTrue(view.IsWater(new Vector2Int(10, 8)), "pool tiles are water");
            Assert.IsFalse(view.IsWater(new Vector2Int(10, 3)), "the deck is not");
            foreach (var swimmer in players.Append(walker))
            {
                var avatar = view.Avatar(swimmer.UserId);
                Assert.IsTrue(avatar.IsSwimming, swimmer.DisplayName + " swims");
                var head = view.transform.InverseTransformPoint(avatar.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head).position).y;
                Debug.Log($"[Reconnect] {swimmer.DisplayName}: head at {head:0.00} m (water {CustomItems.WaterLevel})");
                Assert.That(head, Is.InRange(CustomItems.WaterLevel, CustomItems.WaterLevel + 0.35f), swimmer.DisplayName + ": head just above the water");
            }
            Assert.IsTrue(view.Avatar(sunbather.UserId).IsSeated, "on the lounger");

            view.FrameWholeRoom();
            Save(view.Camera, "pool.png");
            var focus = view.transform.TransformPoint(new Vector3(10f, 0f, 6f));
            view.Camera.transform.position = focus + new Vector3(-4f, 5f, -5f);
            view.Camera.transform.LookAt(focus);
            Save(view.Camera, "pool-close.png");
            view.Hide();
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
