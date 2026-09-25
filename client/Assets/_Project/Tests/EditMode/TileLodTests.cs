using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reconnect.Client.City;
using UnityEngine;

namespace Reconnect.Client.Tests
{
    public sealed class TileLodTests
    {
        private const float PixelsPerRadian = 1920f / (2f * 0.5774f);   // 60° vertical FOV, 1920 px tall
        private static readonly GeoProjection Zurich = new(47.37785, 8.54018);

        [Test]
        public void Tile_parent_children_and_ancestor()
        {
            var hb = new TileId(16, 34322, 22949);

            Assert.AreEqual(new TileId(15, 17161, 11474), hb.Parent);
            Assert.AreEqual(4, hb.Children.Length);
            Assert.IsTrue(hb.Children.All(c => c.Zoom == 17 && c.Parent.Equals(hb)));
            Assert.AreEqual(new TileId(14, 8580, 5737), hb.AncestorAt(14));
        }

        [Test]
        public void Tile_bounds_contain_the_building_they_show()
        {
            var selector = new TileLodSelector(Zurich);
            var bounds = selector.WorldBounds(new TileId(16, 34322, 22949));

            Assert.IsTrue(bounds.Contains(Zurich.ToWorld(47.37785, 8.54018)), "HB is inside its own tile");
            Assert.AreEqual(bounds.size.x, bounds.size.z, bounds.size.x * 0.01f, "tiles are square in metres");
        }

        [Test]
        public void High_camera_uses_coarse_tiles()
        {
            var tiles = Select(cameraHeight: 3000f, maxZoom: 20);

            Assert.That(tiles.Max(t => t.Zoom), Is.LessThanOrEqualTo(16));
        }

        [Test]
        public void Street_level_camera_uses_sharpest_tiles()
        {
            // Pitched 60° with a 60° FOV the camera sees only ~70 m ahead from 40 m height.
            var tiles = Select(cameraHeight: 40f, maxZoom: 20);

            Assert.AreEqual(20, tiles.Max(t => t.Zoom), "10 cm/px right in front of the camera");
            Assert.Less(tiles.Count, 60, "only the visible tiles are selected");
        }

        [Test]
        public void Near_tiles_are_sharper_than_far_tiles()
        {
            var tiles = Select(cameraHeight: 300f, maxZoom: 20);

            Assert.GreaterOrEqual(tiles.Max(t => t.Zoom) - tiles.Min(t => t.Zoom), 1, "level of detail varies with distance");
            Assert.Less(tiles.Count, 200, "quadtree keeps the tile count manageable");
        }

        [Test]
        public void Max_zoom_is_respected_for_the_map_layer()
        {
            var tiles = Select(cameraHeight: 40f, maxZoom: 19);

            Assert.AreEqual(19, tiles.Max(t => t.Zoom));
        }

        [Test]
        public void Selected_tiles_never_overlap()
        {
            var tiles = Select(cameraHeight: 200f, maxZoom: 20);

            foreach (var tile in tiles)
            {
                Assert.IsFalse(tiles.Any(other => other.Zoom < tile.Zoom && tile.AncestorAt(other.Zoom).Equals(other)),
                    $"{tile} overlaps a coarser selected tile");
            }
        }

        /// <summary>Camera like in the app: portrait 1080×1920, 60° FOV, pitched 60° down, looking north at HB.</summary>
        private static List<TileId> Select(float cameraHeight, int maxZoom)
        {
            const float pitch = 60f;
            var position = new Vector3(0f, cameraHeight, -cameraHeight / Mathf.Tan(pitch * Mathf.Deg2Rad));
            var view = Matrix4x4.TRS(position, Quaternion.Euler(pitch, 0f, 0f), new Vector3(1f, 1f, -1f)).inverse;
            var projection = Matrix4x4.Perspective(60f, 1080f / 1920f, 1f, 20000f);
            var frustum = GeometryUtility.CalculateFrustumPlanes(projection * view);

            var selector = new TileLodSelector(Zurich);
            var (x, y) = WebMercatorTiles.ToTile(47.37785, 8.54018, 13);
            var root = new TileId(13, (int)x, (int)y);
            var result = new List<TileId>();
            selector.Select(new[] { root }, position, PixelsPerRadian, detail: 1f, maxZoom, frustum, result);
            return result;
        }
    }
}
