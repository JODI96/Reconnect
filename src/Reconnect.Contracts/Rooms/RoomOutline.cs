using System;
using System.Collections.Generic;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>A corner of a room's floor outline, in room metres (X, Z).</summary>
    public sealed record RoomPointDto(float X, float Z);

    /// <summary>
    /// Where a room lies on the globe: the geographic position of its local origin (corner 0, 0) and the yaw (degrees,
    /// Unity: clockwise around up, 0 = room +X points east) of its axes. Tower floors use it to sit exactly in the real
    /// building.
    /// </summary>
    public sealed record GeoAnchorDto(double Latitude, double Longitude, float Yaw);

    /// <summary>Plane geometry for room outlines (polygons in room metres, any orientation, may be concave).</summary>
    public static class RoomOutline
    {
        /// <summary>Point in polygon (even–odd rule).</summary>
        public static bool Contains(IReadOnlyList<RoomPointDto> outline, float x, float z)
        {
            var inside = false;
            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                var a = outline[i];
                var b = outline[j];
                if ((a.Z > z) != (b.Z > z) && x < (b.X - a.X) * (z - a.Z) / (b.Z - a.Z) + a.X)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>Shortest distance from a point to the outline's edges (the facade).</summary>
        public static float DistanceToEdge(IReadOnlyList<RoomPointDto> outline, float x, float z)
        {
            var best = float.MaxValue;
            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                best = Math.Min(best, DistanceToSegment(x, z, outline[j], outline[i]));
            }
            return best;
        }

        /// <summary>Area in square metres.</summary>
        public static float Area(IReadOnlyList<RoomPointDto> outline)
        {
            var sum = 0f;
            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                sum += outline[j].X * outline[i].Z - outline[i].X * outline[j].Z;
            }
            return Math.Abs(sum) / 2f;
        }

        private static float DistanceToSegment(float x, float z, RoomPointDto a, RoomPointDto b)
        {
            var dx = b.X - a.X;
            var dz = b.Z - a.Z;
            var lengthSquared = dx * dx + dz * dz;
            var t = lengthSquared < 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, ((x - a.X) * dx + (z - a.Z) * dz) / lengthSquared));
            var px = a.X + t * dx - x;
            var pz = a.Z + t * dz - z;
            return (float)Math.Sqrt(px * px + pz * pz);
        }
    }
}
