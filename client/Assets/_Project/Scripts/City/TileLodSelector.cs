using System.Collections.Generic;
using UnityEngine;

namespace Reconnect.Client.City
{
    /// <summary>
    /// Chooses which map tiles to show (quadtree level of detail): a tile is split into its four
    /// children while it would appear larger on screen than its 256 px texture can resolve.
    /// Result: sharp tiles near the camera, coarse tiles towards the horizon. Pure logic – testable.
    /// </summary>
    public sealed class TileLodSelector
    {
        public const int TilePixels = 256;

        private readonly GeoProjection _projection;
        private readonly Dictionary<TileId, Bounds> _boundsCache = new();

        public TileLodSelector(GeoProjection projection)
        {
            _projection = projection;
        }

        /// <summary>World-space rectangle of a tile on the ground (y = 0, 1 m thick for frustum tests).</summary>
        public Bounds WorldBounds(TileId tile)
        {
            if (_boundsCache.TryGetValue(tile, out var cached))
            {
                return cached;
            }

            var (northLat, westLon) = WebMercatorTiles.ToGeo(tile.X, tile.Y, tile.Zoom);
            var (southLat, eastLon) = WebMercatorTiles.ToGeo(tile.X + 1, tile.Y + 1, tile.Zoom);
            var northWest = _projection.ToWorld(northLat, westLon);
            var southEast = _projection.ToWorld(southLat, eastLon);

            var bounds = new Bounds(
                new Vector3((northWest.x + southEast.x) / 2f, 0f, (northWest.z + southEast.z) / 2f),
                new Vector3(southEast.x - northWest.x, 1f, northWest.z - southEast.z));
            _boundsCache[tile] = bounds;
            return bounds;
        }

        /// <param name="pixelsPerRadian">Screen height / (2·tan(fov/2)) – converts angular size to pixels.</param>
        /// <param name="detail">1 = one texel per screen pixel; &lt; 1 loads fewer, blurrier tiles.</param>
        /// <param name="frustum">Camera frustum planes; tiles outside are skipped. Null = no culling (tests).</param>
        public void Select(IEnumerable<TileId> roots, Vector3 cameraPosition, float pixelsPerRadian, float detail,
            int maxZoom, Plane[] frustum, List<TileId> result)
        {
            result.Clear();
            foreach (var root in roots)
            {
                Visit(root, cameraPosition, pixelsPerRadian * detail / TilePixels, maxZoom, frustum, result);
            }
        }

        private void Visit(TileId tile, Vector3 camera, float scale, int maxZoom, Plane[] frustum, List<TileId> result)
        {
            var bounds = WorldBounds(tile);
            if (frustum != null && !GeometryUtility.TestPlanesAABB(frustum, bounds))
            {
                return;
            }

            var distance = Mathf.Max(1f, Vector3.Distance(camera, bounds.ClosestPoint(camera)));
            var screenSizeInTiles = bounds.size.x / distance * scale;   // > 1: texture would be magnified
            if (tile.Zoom < maxZoom && screenSizeInTiles > 1f)
            {
                foreach (var child in tile.Children)
                {
                    Visit(child, camera, scale, maxZoom, frustum, result);
                }
                return;
            }

            result.Add(tile);
        }
    }
}
