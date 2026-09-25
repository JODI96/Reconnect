using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>Renders a furnished room with avatars (no network) into client/Logs/room-preview.png.</summary>
    public sealed class RoomViewTests
    {
        private static readonly Guid Me = Guid.Parse("11111111-1111-7111-8111-111111111111");
        private static readonly Guid Other = Guid.Parse("22222222-2222-7222-8222-222222222222");

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            PlayerPrefs.DeleteKey("reconnect.refreshToken");
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Room_shows_players_and_they_walk_tile_by_tile()
        {
            var room = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            Assert.IsNotNull(room, "RoomView in scene");

            room.Show(Snapshot(), Me);
            var me = room.Avatar(Me);
            Assert.AreEqual(2, room.Avatars.Count);
            Assert.AreEqual(new Vector2Int(5, 5), me.Tile);

            room.MovePlayer(Me, new TilePosition(7, 2));
            var end = Time.realtimeSinceStartup + 10f;
            while (me.Tile != new Vector2Int(7, 2) && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
            Assert.AreEqual(new Vector2Int(7, 2), me.Tile, "walked to the target tile");

            Save(room.Camera, "room-preview.png");
            room.Hide();
        }

        private static RoomSnapshotDto Snapshot()
        {
            var layout = new[]
            {
                new RoomItemDto("loungeSofa", new Vector3Dto(5f, 0f, 9.3f), 180f),
                new RoomItemDto("tableCoffee", new Vector3Dto(5f, 0f, 7.8f), 0f),
                new RoomItemDto("rugRound", new Vector3Dto(5f, 0f, 7.8f), 0f),
                new RoomItemDto("pottedPlant", new Vector3Dto(9.4f, 0f, 9.4f), 0f),
                new RoomItemDto("bookcaseClosedWide", new Vector3Dto(9.6f, 0f, 5f), 270f),
                new RoomItemDto("lampRoundFloor", new Vector3Dto(3.2f, 0f, 9.4f), 0f),
                new RoomItemDto("unknownItem", new Vector3Dto(1.5f, 0f, 1.5f), 0f),
            };
            var dto = new RoomDto(Guid.NewGuid(), "Testraum", Guid.NewGuid(), Other, "Ben", true, layout,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "rooftop");
            return new RoomSnapshotDto(dto, RoomGrid.Width, RoomGrid.Depth, new[]
            {
                new RoomPlayerDto(Me, "Anna", new TilePosition(5, 5)),
                new RoomPlayerDto(Other, "Ben", new TilePosition(3, 3)),
            });
        }

        private static void Save(Camera camera, string fileName)
        {
            var target = new RenderTexture(1080, 1920, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            target.Release();

            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", fileName));
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log($"[Reconnect] Room view: {path}");
        }
    }
}
