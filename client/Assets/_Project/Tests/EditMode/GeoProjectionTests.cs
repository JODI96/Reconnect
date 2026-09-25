using NUnit.Framework;
using Reconnect.Client.City;
using UnityEngine;

namespace Reconnect.Client.Tests
{
    public sealed class GeoProjectionTests
    {
        [Test]
        public void Zurich_HB_is_in_swisstopo_tile_34322_22949_at_zoom_16()
        {
            // Verified against https://wmts.geo.admin.ch/1.0.0/ch.swisstopo.swissimage/default/current/3857/16/34322/22949.jpeg
            var (x, y) = WebMercatorTiles.ToTile(47.37785, 8.54018, 16);

            Assert.AreEqual(34322, (int)x);
            Assert.AreEqual(22949, (int)y);
        }

        [Test]
        public void Tile_math_roundtrips()
        {
            var (x, y) = WebMercatorTiles.ToTile(47.37785, 8.54018, 16);
            var (lat, lon) = WebMercatorTiles.ToGeo(x, y, 16);

            Assert.AreEqual(47.37785, lat, 1e-9);
            Assert.AreEqual(8.54018, lon, 1e-9);
        }

        [Test]
        public void Origin_maps_to_world_zero_and_axes_point_east_and_north()
        {
            var projection = new GeoProjection(47.37785, 8.54018);

            Assert.AreEqual(Vector3.zero, projection.ToWorld(47.37785, 8.54018));
            Assert.Greater(projection.ToWorld(47.37785, 8.55).x, 0f, "east is +X");
            Assert.Greater(projection.ToWorld(47.39, 8.54018).z, 0f, "north is +Z");
        }

        [Test]
        public void Distances_are_in_metres()
        {
            var projection = new GeoProjection(47.37785, 8.54018);

            // HB → Prime Tower is about 2.0 km (checked on map.geo.admin.ch).
            var distance = projection.ToWorld(47.38622, 8.51733).magnitude;
            Assert.That(distance, Is.InRange(1900f, 2050f));

            // 0.01° latitude ≈ 1112 m
            Assert.AreEqual(1111.3f, projection.ToWorld(47.38785, 8.54018).z, 1f);
        }

        [Test]
        public void World_to_geo_roundtrips()
        {
            var projection = new GeoProjection(47.37785, 8.54018);
            var world = projection.ToWorld(47.36490, 8.54671);

            var (lat, lon) = projection.ToGeo(world);

            Assert.AreEqual(47.36490, lat, 1e-6);
            Assert.AreEqual(8.54671, lon, 1e-6);
        }
    }
}
