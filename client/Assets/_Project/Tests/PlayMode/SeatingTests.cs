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
    /// People sitting on every kind of seat of two furnished rooms (Kenney chairs and stools in the café, Poly Haven
    /// sofas, armchairs and bar chairs in the lobby): the hips must rest on the seat, facing away from the backrest.
    /// Renders client/Logs/seats-*.png. Needs the local backend.
    /// </summary>
    [Category("Integration")]
    public sealed class SeatingTests
    {
        private const string BaseUrl = "http://localhost:5191";

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Avatars_sit_on_every_kind_of_seat()
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

            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var target = new RenderTexture(900, 900, 24);
            view.Camera.targetTexture = target;
            foreach (var name in new[] { "Café Limmat", "Lobby" })
            {
                var detail = api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(list.Result.Value.Items.Single(r => r.Name == name).Id));
                yield return Wait(detail);
                var room = detail.Result.Value;

                // One person on the first place of each kind of seat in the room.
                var seats = room.Layout.Select((item, index) => (item.ItemId, Index: index))
                    .Where(s => RoomSeats.IsSeat(s.ItemId))
                    .GroupBy(s => s.ItemId)
                    .Select(g => g.First())
                    .ToList();
                Assert.IsNotEmpty(seats, name + " has seats");
                var players = seats.Select((s, i) => new RoomPlayerDto(Guid.NewGuid(), s.ItemId, new TilePosition(1, 1), new SeatDto(s.Index, 0))).ToList();
                // Every place of the first sofa or bench, to see several people side by side.
                var sofa = seats.FirstOrDefault(s => RoomSeats.PlacesFor(s.ItemId) > 1);
                var extra = sofa.ItemId == null ? new List<RoomPlayerDto>() : Enumerable.Range(1, RoomSeats.PlacesFor(sofa.ItemId) - 1)
                    .Select(place => new RoomPlayerDto(Guid.NewGuid(), sofa.ItemId + place, new TilePosition(1, 2), new SeatDto(sofa.Index, place))).ToList();
                var me = new RoomPlayerDto(Guid.NewGuid(), "Ich", new TilePosition(room.Width / 2, 1));
                view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, players.Concat(extra).Append(me).ToList()), me.UserId);
                for (var frame = 0; frame < 20; frame++)
                {
                    yield return null;
                }

                var problems = new List<string>();
                foreach (var (player, seat) in players.Zip(seats, (p, s) => (p, s)))
                {
                    var furniture = view.SeatFor(seat.Index);
                    Assert.IsNotNull(furniture, $"{seat.ItemId} became a seat");
                    var avatar = view.Avatar(player.UserId);
                    Assert.IsTrue(avatar.IsSeated, $"{seat.ItemId}: seated");
                    var hips = avatar.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips).position;
                    var seatPoint = view.transform.TransformPoint(furniture.Points[0]);
                    var offset = hips - seatPoint;
                    Debug.Log($"[Reconnect] {name} {seat.ItemId}: seat height {furniture.Points[0].y:0.00} m, hips offset {offset}, facing {furniture.Facings[0]:0}°");
                    if (new Vector2(offset.x, offset.z).magnitude > 0.15f || offset.y is < 0f or > 0.25f)
                    {
                        problems.Add($"{seat.ItemId}: hips {offset} from the seat point");
                    }
                }

                // A close-up of every kind of seat for a visual check.
                foreach (var seat in seats)
                {
                    Save(view.Camera, view.transform.TransformPoint(view.SeatFor(seat.Index).Points[0]),
                        $"seats-{(name == "Lobby" ? "lobby" : "cafe")}-{seat.ItemId}.png");
                }
                view.Hide();
                Assert.IsEmpty(problems, string.Join("\n", problems));
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

        private static void Save(Camera camera, Vector3 lookAt, string fileName)
        {
            camera.transform.position = lookAt + new Vector3(-2.2f, 2.6f, -2.2f);
            camera.transform.LookAt(lookAt);
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
