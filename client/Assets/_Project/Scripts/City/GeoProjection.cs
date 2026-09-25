using System;
using UnityEngine;

namespace Reconnect.Client.City
{
    /// <summary>
    /// Converts WGS84 coordinates to Unity world space around a fixed origin:
    /// X = east, Z = north, Y = up, 1 unit = 1 metre. The local tangent-plane approximation
    /// is accurate to well below a metre over a few kilometres – enough for a city quarter.
    /// Phase 6 (swisstopo LV95 data) will replace this with a proper LV95 → local transform.
    /// </summary>
    public sealed class GeoProjection
    {
        private const double MetersPerDegreeLat = 111_132.954;
        private const double EarthRadius = 6_378_137.0;

        private readonly double _metersPerDegreeLon;

        public GeoProjection(double originLatitude, double originLongitude)
        {
            OriginLatitude = originLatitude;
            OriginLongitude = originLongitude;
            _metersPerDegreeLon = Math.PI / 180.0 * EarthRadius * Math.Cos(originLatitude * Math.PI / 180.0);
        }

        public double OriginLatitude { get; }
        public double OriginLongitude { get; }

        public Vector3 ToWorld(double latitude, double longitude) => new(
            (float)((longitude - OriginLongitude) * _metersPerDegreeLon),
            0f,
            (float)((latitude - OriginLatitude) * MetersPerDegreeLat));

        public (double Latitude, double Longitude) ToGeo(Vector3 world) => (
            OriginLatitude + world.z / MetersPerDegreeLat,
            OriginLongitude + world.x / _metersPerDegreeLon);
    }

    /// <summary>Web-Mercator (EPSG:3857) tile math, as used by the swisstopo WMTS "3857" tile matrix.</summary>
    public static class WebMercatorTiles
    {
        /// <summary>Fractional tile coordinates; floor them to get the tile index.</summary>
        public static (double X, double Y) ToTile(double latitude, double longitude, int zoom)
        {
            var n = 1 << zoom;
            var latRad = latitude * Math.PI / 180.0;
            var x = (longitude + 180.0) / 360.0 * n;
            var y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
            return (x, y);
        }

        /// <summary>Latitude/longitude of a tile corner (x, y may be fractional; y grows southwards).</summary>
        public static (double Latitude, double Longitude) ToGeo(double x, double y, int zoom)
        {
            var n = 1 << zoom;
            var longitude = x / n * 360.0 - 180.0;
            var latitude = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * y / n))) * 180.0 / Math.PI;
            return (latitude, longitude);
        }
    }
}
