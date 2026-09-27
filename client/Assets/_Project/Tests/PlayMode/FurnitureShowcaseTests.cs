using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Reconnect.Client.PlayModeTests
{
    /// <summary>
    /// Our own furniture in a furnished sample room – lounge, dining, bar, work corner – rendered from the game camera
    /// (client/Logs/furniture-*.png) to judge the look. Every piece must be built (nothing missing). No backend needed.
    /// </summary>
    public sealed class FurnitureShowcaseTests
    {
        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
        }

        private static RoomItemDto At(string id, float x, float z, float rotation = 0f, string colours = null) =>
            new(id, new Vector3Dto(x, 0f, z), rotation, colours);

        [UnityTest]
        public IEnumerator A_furnished_room_looks_right_from_the_game_camera()
        {
            var layout = new List<RoomItemDto>
            {
                // Lounge.
                At("custom-zone-carpet-4x3", 4f, 7f, 0f, "oat"),
                At("custom-sofa-box-3", 4f, 8.3f, 0f, "sage/brass"),
                At("custom-armchair-round", 2.2f, 7f, 270f, "terracotta"),
                At("custom-armchair-round", 5.8f, 7f, 90f, "terracotta"),
                At("custom-coffeetable-oval", 4f, 7f),
                At("custom-vase-bowl", 4f, 7f, 0f, "black"),
                At("custom-floorlamp-arc", 6.3f, 8.6f),
                At("custom-plant-tall-tapered", 1.4f, 8.9f),
                At("custom-sidetable-round", 2.2f, 8.4f),
                At("custom-tablelamp-dome", 2.2f, 8.4f),
                // Dining.
                At("custom-zone-wood-6x4", 11f, 7.5f, 0f, "smoked-oak"),
                At("custom-table-legs-240", 11f, 7.5f),
                At("custom-chair-classic", 10.2f, 8.3f, 0f), At("custom-chair-classic", 11f, 8.3f, 0f), At("custom-chair-classic", 11.8f, 8.3f, 0f),
                At("custom-chair-classic", 10.2f, 6.7f, 180f), At("custom-chair-classic", 11f, 6.7f, 180f), At("custom-chair-classic", 11.8f, 6.7f, 180f),
                At("custom-pendantlamp-linear", 11f, 7.5f),
                At("custom-vase-round", 10.5f, 7.5f, 0f, "sage"),
                At("custom-candles", 11.5f, 7.5f),
                // Bar corner.
                At("custom-hightable", 13f, 2.5f, 0f, "black-marble/brass"),
                At("custom-barstool-back", 13f, 1.9f, 180f), At("custom-barstool-back", 13f, 3.1f, 0f),
                At("custom-pendantlamp-globe", 13f, 2.5f),
                // Along the back wall.
                At("custom-sideboard-180", 4f, 11.6f, 0f, "walnut/brass"),
                At("custom-vase-tall", 3.5f, 11.6f),
                At("custom-books", 4.6f, 11.6f, 20f, "terracotta"),
                At("custom-shelf-grid", 10f, 11.6f),
                At("custom-plant-leafy-cylinder", 7.2f, 11.4f, 0f, "travertine"),
                // Work corner.
                At("custom-desk-140", 3f, 2.5f, 180f),
                At("custom-chair-shell", 3f, 3.2f, 0f, "warm-white/oak"),
                At("custom-acoustic-curved", 3f, 1.2f, 180f, "petrol"),
                At("custom-tablelamp-mushroom", 3.5f, 2.3f, 0f, "mustard/brass"),
                // Sofa corner.
                At("custom-sofa-low-corner", 8.5f, 3f, 0f, "charcoal/black"),
                At("custom-coffeetable-round", 8.2f, 1.6f),
                At("custom-rugmodern-round-200", 13f, 6f, 0f, "stone/charcoal"),
                At("custom-lounge-shell", 13f, 6f, 45f),
            };
            const int width = 16, depth = 12;
            var room = new RoomDto(Guid.NewGuid(), "Möbelprobe", Guid.NewGuid(), Guid.NewGuid(), "Test", true, layout,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "office", width, depth);
            var me = new RoomPlayerDto(Guid.NewGuid(), "Anna", new TilePosition(7, 5));
            var ben = new RoomPlayerDto(Guid.NewGuid(), "Ben", new TilePosition(8, 5));

            var view = UnityEngine.Object.FindFirstObjectByType<RoomView>();
            var target = new RenderTexture(1080, 1920, 24);
            view.Camera.targetTexture = target;
            view.Show(new RoomSnapshotDto(room, width, depth, new[] { me, ben }), me.UserId);
            Assert.IsEmpty(view.MissingItems, "every piece is built");

            foreach (var (name, focus, viewWidth) in new[]
                     {
                         ("room", new Vector2(8f, 6f), 30f),
                         ("lounge", new Vector2(4f, 7.5f), 7f),
                         ("dining", new Vector2(11f, 7.5f), 7f),
                         ("corner", new Vector2(9f, 3f), 8f),
                     })
            {
                view.LookAt(focus, viewWidth);
                for (var frame = 0; frame < 10; frame++)
                {
                    yield return null;
                }
                Save(view.Camera, $"furniture-{name}.png");
            }
            view.Hide();
            view.Camera.targetTexture = null;
            target.Release();
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
    }
}
