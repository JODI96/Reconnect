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
        public IEnumerator Everyone_on_a_sofa_sits_on_its_own_cushion()
        {
            // Every seat with several places, straight and turned by a crooked 37°, all places taken.
            var ids = ItemDefinitions.All.Where(d => d.Seats > 1 && d.Kind == ItemKind.Floor).Select(d => d.Id).OrderBy(id => id).ToList();
            var angles = new[] { 0f, 37f };
            const int spacing = 5;
            const int perRow = 8;
            var width = perRow * spacing + 2;
            var depth = ((ids.Count * angles.Length + perRow - 1) / perRow) * spacing + 2;
            var layout = new List<RoomItemDto>();
            for (var i = 0; i < ids.Count * angles.Length; i++)
            {
                var tileX = (i % perRow) * spacing + 3;
                var tileZ = (i / perRow) * spacing + 3;
                layout.Add(new RoomItemDto(ids[i / angles.Length], new Vector3Dto(tileX, 0f, tileZ), angles[i % angles.Length]));
            }
            var players = new List<RoomPlayerDto>();
            for (var index = 0; index < layout.Count; index++)
            {
                for (var place = 0; place < ItemDefinitions.Find(layout[index].ItemId).Seats; place++)
                {
                    players.Add(new RoomPlayerDto(Guid.NewGuid(), layout[index].ItemId, new TilePosition(0, 0), new SeatDto(index, place)));
                }
            }
            var room = new RoomDto(Guid.NewGuid(), "Sofaprobe", Guid.NewGuid(), Guid.NewGuid(), "Test", true, layout,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "cozy", width, depth);
            var me = new RoomPlayerDto(Guid.NewGuid(), "Ich", new TilePosition(0, 0));

            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var target = new RenderTexture(700, 700, 24);
            view.Camera.targetTexture = target;
            view.Show(new RoomSnapshotDto(room, width, depth, players.Append(me).ToList()), me.UserId);
            for (var frame = 0; frame < 120; frame++)
            {
                yield return null;   // everyone settled in the sitting pose
            }

            // Each place lies on the piece (inside its footprint, at seat height) and no two people share a spot.
            var wrong = new List<string>();
            var shots = new List<(Vector3 LookAt, string File)>();
            for (var index = 0; index < layout.Count; index++)
            {
                var item = layout[index];
                var definition = ItemDefinitions.Find(item.ItemId);
                var seat = view.SeatFor(index);
                var shape = RoomLayout.Shape(item, definition);
                var centre = view.transform.TransformPoint(new Vector3(shape.CentreX, 0f, shape.CentreZ));
                var toItem = Quaternion.Inverse(view.transform.rotation * Quaternion.Euler(0f, item.Rotation, 0f));
                var spots = new List<Vector3>();
                for (var place = 0; place < seat.Points.Count; place++)
                {
                    var local = toItem * (view.transform.TransformPoint(seat.Points[place]) - centre);
                    if (Mathf.Abs(local.x) > definition.SizeX / 2f - 0.05f || Mathf.Abs(local.z) > definition.SizeZ / 2f - 0.05f
                        || local.y < 0.25f || local.y > 0.8f)
                    {
                        wrong.Add($"{item.ItemId} at {item.Rotation:0}°, place {place}: not on the seat ({local.x:0.00}, {local.y:0.00}, {local.z:0.00})");
                    }
                    if (spots.Any(other => Vector2.Distance(new Vector2(other.x, other.z), new Vector2(local.x, local.z)) < 0.45f))
                    {
                        wrong.Add($"{item.ItemId} at {item.Rotation:0}°, place {place}: on someone's lap");
                    }
                    spots.Add(local);
                }
                if (item.Rotation > 1f && (item.ItemId.Contains("corner") || item.ItemId.Contains("sofa-box") || item.ItemId.Contains("banquette-160")))
                {
                    shots.Add((view.transform.TransformPoint(seat.Points[0]), $"sofa-seats-{item.ItemId}.png"));
                }
            }
            foreach (var (lookAt, file) in shots)
            {
                // Avatars off screen don't update their pose (animator culling): look first, let them settle, then save.
                Aim(view.Camera, lookAt);
                for (var frame = 0; frame < 5; frame++)
                {
                    yield return null;
                    Aim(view.Camera, lookAt);
                }
                Save(view.Camera, lookAt, file);
            }
            // Where everyone's hips ended up relative to their place (should be a little above it).
            var sunk = new List<string>();
            for (var p = 0; p < players.Count; p++)
            {
                var avatar = view.Avatar(players[p].UserId);
                var hips = avatar.GetComponentInChildren<Animator>()?.GetBoneTransform(HumanBodyBones.Hips);
                var seatDto = players[p].Seat;
                var point = view.transform.TransformPoint(view.SeatFor(seatDto.Item).Points[seatDto.Place]);
                if (hips != null && Mathf.Abs(hips.position.y - point.y - 0.1f) > 0.08f)
                {
                    sunk.Add($"{layout[seatDto.Item].ItemId} {layout[seatDto.Item].Rotation:0}° place {seatDto.Place}: hips {hips.position.y - point.y:0.00} above the seat, seated {avatar.IsSeated}");
                }
            }
            Debug.Log("[Reconnect] Hips off the seat: " + sunk.Count + "\n" + string.Join("\n", sunk));
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

        private static void Aim(Camera camera, Vector3 lookAt)
        {
            camera.transform.position = lookAt + new Vector3(-1.8f, 2.2f, -1.8f);
            camera.transform.LookAt(lookAt);
        }

        private static void Save(Camera camera, Vector3 lookAt, string fileName)
        {
            Aim(camera, lookAt);
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
