using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// Every seat of the build catalog, turned all four ways: whoever sits down looks the way the chair faces (back to
    /// the backrest). Models whose measured backrest contradicts their catalog front are listed and rendered
    /// (client/Logs/seat-direction-*.png) for a look. No backend needed.
    /// </summary>
    public sealed class SeatDirectionTests
    {
        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator People_sit_facing_the_way_every_seat_faces()
        {
            var seatIds = ItemDefinitions.All.Where(d => d.Seats > 0 && d.Kind == ItemKind.Floor).Select(d => d.Id).OrderBy(id => id).ToList();
            Assert.IsNotEmpty(seatIds);

            // A big empty room with every seat in a grid, each turned by 0/90/180/270 and a crooked 37° in turn.
            var angles = new[] { 0f, 90f, 180f, 270f, 37f };
            const int spacing = 4;
            var perRow = 10;
            var width = perRow * spacing + 2;
            var depth = ((seatIds.Count * angles.Length + perRow - 1) / perRow) * spacing + 2;
            var layout = new List<RoomItemDto>();
            for (var i = 0; i < seatIds.Count * angles.Length; i++)
            {
                var id = seatIds[i / angles.Length];
                var rotation = angles[i % angles.Length];
                var definition = ItemDefinitions.Find(id);
                var tileX = (i % perRow) * spacing + 2;
                var tileZ = (i / perRow) * spacing + 2;
                if (!RoomLayout.IsQuarterTurn(rotation))
                {
                    layout.Add(new RoomItemDto(id, new Vector3Dto(tileX, 0f, tileZ), rotation));   // turned: centre on the fine grid
                    continue;
                }
                var quarter = RoomLayout.Quarter(rotation);
                var (w, d) = RoomLayout.Size(definition, quarter);
                var cellX = tileX * BuildGrid.CellsPerTile - w / 2;
                var cellZ = tileZ * BuildGrid.CellsPerTile - d / 2;
                var (x, z) = RoomLayout.Centre(new CellRect(cellX, cellZ, w, d));
                layout.Add(new RoomItemDto(id, new Vector3Dto(x, 0f, z), quarter * 90f));
            }
            var room = new RoomDto(Guid.NewGuid(), "Sitzprobe", Guid.NewGuid(), Guid.NewGuid(), "Test", true, layout,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "cozy", width, depth);
            var players = layout.Select((item, index) => new RoomPlayerDto(Guid.NewGuid(), item.ItemId, new TilePosition(0, 0), new SeatDto(index, 0))).ToList();
            var me = new RoomPlayerDto(Guid.NewGuid(), "Ich", new TilePosition(0, 0));

            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var target = new RenderTexture(700, 700, 24);
            view.Camera.targetTexture = target;
            view.Show(new RoomSnapshotDto(room, width, depth, players.Append(me).ToList()), me.UserId);
            for (var frame = 0; frame < 15; frame++)
            {
                yield return null;
            }

            var wrong = new List<string>();
            var suspicious = new HashSet<string>();
            for (var index = 0; index < layout.Count; index++)
            {
                var item = layout[index];
                var seat = view.SeatFor(index);
                Assert.IsNotNull(seat, item.ItemId + " is a seat");
                var avatar = view.Avatar(players[index].UserId);
                Assert.IsTrue(avatar.IsSeated, item.ItemId + " seated");
                if (seat.MeasuredBackDisagrees)
                {
                    suspicious.Add(item.ItemId);
                }
                if (!seat.HasBackrest)
                {
                    continue;   // stools turn to counters
                }
                var (fx, fz) = RoomLayout.FrontVector(item.ItemId, item.Rotation);
                var expected = view.transform.TransformDirection(new Vector3(fx, 0f, fz));
                var looking = avatar.transform.forward;
                looking.y = 0f;
                if (Vector3.Angle(expected, looking) > 25f)
                {
                    wrong.Add($"{item.ItemId} at {item.Rotation:0}°: looks {Vector3.Angle(expected, looking):0}° away from the front");
                }
            }

            // A look at every seat whose shape contradicts its front, and one overview.
            foreach (var id in suspicious)
            {
                var index = layout.FindIndex(i => i.ItemId == id);
                Save(view.Camera, view.transform.TransformPoint(view.SeatFor(index).Points[0]), $"seat-direction-{id}.png");
            }
            Debug.Log("[Reconnect] Seats whose measured backrest contradicts the catalog front: " + string.Join(", ", suspicious));
            view.Hide();
            view.Camera.targetTexture = null;
            target.Release();
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        [UnityTest]
        public IEnumerator At_a_bar_everyone_on_a_stool_faces_the_counter()
        {
            // Counter along x, stools right in front of it (south), deliberately turned the wrong way.
            var stools = ItemDefinitions.All.Where(d => d.Seats > 0 && (d.Id.Contains("stool") || d.Id.Contains("bar_chair"))).Select(d => d.Id).ToList();
            var layout = new List<RoomItemDto>();
            var bar = ItemDefinitions.Find("kitchenBar");
            for (var i = 0; i < stools.Count; i++)
            {
                var barCells = new CellRect(4 + i * bar.Width, 16, bar.Width, bar.Depth);
                var (bx, bz) = RoomLayout.Centre(barCells);
                layout.Add(new RoomItemDto("kitchenBar", new Vector3Dto(bx, 0f, bz), 0f));
                var stool = ItemDefinitions.Find(stools[i]);
                var stoolCells = new CellRect(barCells.X, barCells.Z - stool.Depth, stool.Width, stool.Depth);
                var (sx, sz) = RoomLayout.Centre(stoolCells);
                layout.Add(new RoomItemDto(stools[i], new Vector3Dto(sx, 0f, sz), 0f));
            }
            var width = 4 + stools.Count * bar.Width / BuildGrid.CellsPerTile + 4;
            var room = new RoomDto(Guid.NewGuid(), "Bar", Guid.NewGuid(), Guid.NewGuid(), "Test", true, layout,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "cozy", width, 10);
            var players = Enumerable.Range(0, stools.Count)
                .Select(i => new RoomPlayerDto(Guid.NewGuid(), stools[i], new TilePosition(0, 0), new SeatDto(i * 2 + 1, 0))).ToList();
            var me = new RoomPlayerDto(Guid.NewGuid(), "Ich", new TilePosition(0, 0));

            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            view.Show(new RoomSnapshotDto(room, width, 10, players.Append(me).ToList()), me.UserId);
            for (var frame = 0; frame < 15; frame++)
            {
                yield return null;
            }

            var wrong = new List<string>();
            for (var i = 0; i < stools.Count; i++)
            {
                var seat = view.SeatFor(i * 2 + 1);
                var looking = view.Avatar(players[i].UserId).transform.forward;
                if (!seat.HasBackrest && Vector3.Angle(view.transform.forward, new Vector3(looking.x, 0f, looking.z)) > 45f)
                {
                    wrong.Add($"{stools[i]} sits with its back to the counter");
                }
            }
            view.Hide();
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        private static void Save(Camera camera, Vector3 lookAt, string fileName)
        {
            camera.transform.position = lookAt + new Vector3(-1.8f, 2.2f, -1.8f);
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
    }
}
