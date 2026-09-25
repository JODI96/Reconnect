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
    /// Puts the Rooftop Lounge on the real Prime Tower roof in the streamed 3D city and renders
    /// client/Logs/rooftop-city.png. Needs the local backend and internet.
    /// </summary>
    [Category("Integration")]
    public sealed class RooftopCityTests
    {
        private const string BaseUrl = "http://localhost:5191";
        private static readonly Guid PrimeTowerId = Guid.Parse("0199a000-0000-7000-8000-000000000003");

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rooftop_stands_on_the_prime_tower_with_the_city_around()
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
            var detail = api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(list.Result.Value.Items.Single(r => r.Name == "Rooftop Lounge").Id));
            yield return Wait(detail);
            var room = detail.Result.Value;

            var city = UnityEngine.Object.FindFirstObjectByType<CityView>();
            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var target = new RenderTexture(1080, 1920, 24);
            view.Camera.targetTexture = target;

            // The city places a marker on the real Prime Tower roof (height sampled from swissBUILDINGS3D).
            city.ShowBuildings(new[] { new BuildingDto(PrimeTowerId, "Prime Tower", "Hardstrasse 201", 47.38622, 8.51733, null) });
            city.SetVisible(true);
            for (var i = 0; i < 300 && (city.RoofPosition(PrimeTowerId)?.y ?? 0f) < 50f; i++)
            {
                yield return null;
            }
            Assert.Greater(city.RoofPosition(PrimeTowerId)?.y ?? 0f, 80f, "roof height sampled (Prime Tower ≈ 126 m)");

            city.ShowAsBackdrop();
            var anchorTask = city.RoofAnchorAsync(PrimeTowerId, room.Width + 1f, room.Depth + 1f);
            yield return Wait(anchorTask);
            var roof = anchorTask.Result;
            Assert.IsNotNull(roof);
            var me = Guid.NewGuid();
            view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, new[]
            {
                new RoomPlayerDto(me, "Anna", new TilePosition(9, 6)),
                new RoomPlayerDto(Guid.NewGuid(), "Ben", new TilePosition(11, 7)),
            }), me, roof);
            view.FrameWholeRoom();

            var end = Time.realtimeSinceStartup + 180f;
            yield return null;
            while (city.LoadProgress < 99.9f && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            for (var i = 0; i < 10; i++)
            {
                yield return null;
            }
            Save(view.Camera, "rooftop-city.png");
            Debug.Log($"[Reconnect] Rooftop on roof at y = {roof.Value.y:0.0} m");

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
