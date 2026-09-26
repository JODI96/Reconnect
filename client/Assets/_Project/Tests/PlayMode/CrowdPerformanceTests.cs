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
    /// Phone budget for the worst case: the Prime Tower lobby (largest floor) full with 80 avatars, whole room in view,
    /// portrait at phone resolution. With URP's SRP Batcher draw calls are cheap and set-pass calls (shader/material
    /// switches) are what costs; the limits keep mid-range phones at 30+ fps.
    /// Renders client/Logs/crowd-lobby.png. Needs the local backend.
    /// </summary>
    [Category("Integration")]
    public sealed class CrowdPerformanceTests
    {
        private const string BaseUrl = "http://localhost:5191";
        private const int People = 80;
        private const int MaxBatches = 400;
        private const int MaxSetPassCalls = 80;
        private const int MaxTriangles = 400_000;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Lobby_with_80_people_stays_within_the_phone_budget()
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
            var detail = api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(list.Result.Value.Items.Single(r => r.Name == "Lobby").Id));
            yield return Wait(detail);
            var room = detail.Result.Value;

            // 80 people spread over the hall.
            var random = new System.Random(7);
            var players = Enumerable.Range(0, People)
                .Select(i => new RoomPlayerDto(Guid.NewGuid(), "Gast " + i, new TilePosition(random.Next(1, room.Width - 1), random.Next(1, room.Depth - 1))))
                .ToArray();
            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var target = new RenderTexture(1080, 1920, 24);
            view.Camera.targetTexture = target;
#if UNITY_EDITOR
            // The empty room first: the avatars' share is the difference.
            view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, players.Take(1).ToArray()), players[0].UserId);
            view.FrameWholeRoom();
            for (var frame = 0; frame < 5; frame++)
            {
                yield return null;
            }
            view.Camera.Render();
            Debug.Log($"[Reconnect] Lobby empty: {UnityEditor.UnityStats.batches} batches, {UnityEditor.UnityStats.triangles} triangles, shadow casters {UnityEditor.UnityStats.shadowCasters}");
            var heavy = view.GetComponentsInChildren<MeshFilter>()
                .Where(f => f.sharedMesh != null)
                .GroupBy(f => f.sharedMesh.name)
                .Select(g => (Name: g.Key, Count: g.Count(), Triangles: g.Sum(f => f.sharedMesh.triangles.Length / 3)))
                .OrderByDescending(x => x.Triangles)
                .Take(15);
            Debug.Log("[Reconnect] Heaviest meshes: " + string.Join(" | ", heavy.Select(h => $"{h.Name} x{h.Count} = {h.Triangles}")));
            Debug.Log($"[Reconnect] Room renderers: {view.GetComponentsInChildren<Renderer>().Length}, materials: {view.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct().Count()}");
            view.Hide();
#endif
            view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, players), players[0].UserId);
            view.FrameWholeRoom();
            for (var frame = 0; frame < 30; frame++)
            {
                yield return null;
            }

            view.Camera.Render();
#if UNITY_EDITOR
            var batches = UnityEditor.UnityStats.batches;
            var triangles = UnityEditor.UnityStats.triangles;
            var setPasses = UnityEditor.UnityStats.setPassCalls;
            Debug.Log($"[Reconnect] Lobby with {People} people: {batches} batches, {setPasses} set-pass calls, {triangles} triangles, shadow casters {UnityEditor.UnityStats.shadowCasters}");
            var lodCounts = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None)
                .Select(g => g.GetLODs().Select((lod, i) => (lod, i)).FirstOrDefault(x => x.lod.renderers.Any(r => r != null && r.isVisible)).i);
            Debug.Log("[Reconnect] Avatar LODs visible: " + string.Join(",", lodCounts.GroupBy(i => i).OrderBy(g => g.Key).Select(g => $"LOD{g.Key}={g.Count()}")));
#endif
            Save(view.Camera, "crowd-lobby.png");
            view.Hide();
            view.Camera.targetTexture = null;
            target.Release();

#if UNITY_EDITOR
            if (batches == 0)
            {
                Assert.Inconclusive("Render statistics not available in this run.");
            }
            Assert.LessOrEqual(batches, MaxBatches, "draw calls");
            Assert.LessOrEqual(setPasses, MaxSetPassCalls, "set-pass calls");
            Assert.LessOrEqual(triangles, MaxTriangles, "triangles");
#endif
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
