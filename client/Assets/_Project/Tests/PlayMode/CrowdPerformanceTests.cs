using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Reconnect.Client.Core;
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
    /// Phone budget for the worst case: the Prime Tower lobby (whole storey, ~1580 m²) full with 150 avatars, zoomed out as
    /// far as players can over the middle of the floor (the tilted camera then sees nearly the whole storey), portrait at
    /// phone resolution, for both graphics settings. With URP's SRP Batcher draw calls are cheap and set-pass calls (shader/material
    /// switches) are what costs; the limits keep mid-range phones at 30+ fps.
    /// Renders client/Logs/crowd-lobby.png. Needs the local backend.
    /// </summary>
    [Category("Integration")]
    public sealed class CrowdPerformanceTests
    {
        private const string BaseUrl = "http://localhost:5191";
        private const int People = 150;
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
        public IEnumerator Lobby_with_150_people_stays_within_the_phone_budget()
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

            // People spread over the free floor (inside the outline, not in furniture).
            var blocked = RoomLayout.BlockedTiles(room.Layout);
            blocked.UnionWith(RoomLayout.OutsideTiles(RoomZones.ContextFor(room.Theme, room.Width, room.Depth, room.Layout, room.Outline)));
            var random = new System.Random(7);
            var free = Enumerable.Range(0, room.Width * room.Depth)
                .Select(i => (X: i % room.Width, Z: i / room.Width))
                .Where(t => !blocked.Contains(t))
                .OrderBy(_ => random.Next())
                .Take(People)
                .ToArray();
            Assert.AreEqual(People, free.Length, "free tiles");
            var players = free
                .Select((t, i) => new RoomPlayerDto(Guid.NewGuid(), "Gast " + i, new TilePosition(t.X, t.Z)))
                .ToArray();
            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var graphics = UnityEngine.Object.FindFirstObjectByType<AppBootstrap>().Graphics;
            var wasHigh = graphics.High;
            var target = new RenderTexture(1080, 1920, 24);
            view.Camera.targetTexture = target;
            view.Show(new RoomSnapshotDto(room, room.Width, room.Depth, players), players[0].UserId);
            // Furthest a player can zoom out, over the middle of the storey (most people and furniture in view).
            view.ZoomOutFully(new Vector2(room.Width / 2f, room.Depth / 2f));

            var results = new System.Collections.Generic.List<(bool High, int Batches, int SetPasses, int Triangles)>();
            foreach (var high in new[] { false, true })
            {
                graphics.Set(high);
                for (var frame = 0; frame < 30; frame++)
                {
                    yield return null;
                }
                view.Camera.Render();
#if UNITY_EDITOR
                results.Add((high, UnityEditor.UnityStats.batches, UnityEditor.UnityStats.setPassCalls, UnityEditor.UnityStats.triangles));
                Debug.Log($"[Reconnect] Lobby with {People} people, graphics {(high ? "high" : "normal")}: {UnityEditor.UnityStats.batches} batches, " +
                          $"{UnityEditor.UnityStats.setPassCalls} set-pass calls, {UnityEditor.UnityStats.triangles} triangles");
#endif
            }
#if UNITY_EDITOR
            // What costs the most, for when the budget breaks: draw calls and triangles per kind of item.
            var drawn = view.GetComponentsInChildren<Renderer>()
                .Where(r => r.isVisible && r.enabled)
                .GroupBy(r => TopItem(view.transform, r.transform))
                .Select(g => (Name: g.Key.name, Draws: g.Sum(r => r.sharedMaterials.Length), Triangles: g.Sum(r => r is MeshRenderer ? Triangles(r.GetComponent<MeshFilter>().sharedMesh) : 0)))
                .GroupBy(x => x.Name.StartsWith("Avatar") ? "Avatars" : x.Name)
                .Select(g => (Name: $"{g.Key} x{g.Count()}", Draws: g.Sum(x => x.Draws), Triangles: g.Sum(x => x.Triangles)))
                .OrderByDescending(x => x.Draws)
                .Take(25);
            Debug.Log("[Reconnect] Draws by item: " + string.Join(" | ", drawn.Select(d => $"{d.Name} {d.Draws}/{d.Triangles}")));
#endif
            Save(view.Camera, "crowd-lobby.png");
            graphics.Set(wasHigh);
            view.Hide();
            view.Camera.targetTexture = null;
            target.Release();

#if UNITY_EDITOR
            if (results.Any(r => r.Batches == 0))
            {
                Assert.Inconclusive("Render statistics not available in this run.");
            }
            foreach (var (high, batches, setPasses, triangles) in results)
            {
                // "Hoch" (phones with 5.5 GB+) adds ambient occlusion, which draws everything once more into a depth pre-pass:
                // cheap on the GPU (depth only, same shader state), so it gets twice the draw-call and triangle budget.
                var factor = high ? 2 : 1;
                var label = high ? "graphics high" : "graphics normal";
                Assert.LessOrEqual(batches, MaxBatches * factor, "draw calls, " + label);
                Assert.LessOrEqual(setPasses, MaxSetPassCalls, "set-pass calls, " + label);
                Assert.LessOrEqual(triangles, MaxTriangles * factor, "triangles, " + label);
            }
#endif
        }

        private static Transform TopItem(Transform root, Transform t)
        {
            for (var x = t; x != null && x != root; x = x.parent)
            {
                if (x.parent != null && x.parent.name == "Furniture")
                {
                    return x;
                }
            }
            while (t.parent != null && t.parent != root && t.parent.parent != root)
            {
                t = t.parent;
            }
            return t;
        }

        /// <summary>Triangle count without reading the mesh (works for meshes without Read/Write).</summary>
        private static int Triangles(Mesh mesh)
        {
            long indices = 0;
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                indices += mesh.GetIndexCount(i);
            }
            return (int)(indices / 3);
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
