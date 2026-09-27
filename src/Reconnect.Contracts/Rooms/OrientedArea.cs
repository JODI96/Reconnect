using System;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>
    /// A rectangle in metres turned by <see cref="Rotation"/> degrees around its centre, the way Unity turns an item
    /// around Y: its local X axis points to (cos r, −sin r), its local Z axis to (sin r, cos r) in room X/Z.
    /// </summary>
    public readonly struct OrientedArea
    {
        public OrientedArea(float centreX, float centreZ, float halfX, float halfZ, float rotation)
        {
            CentreX = centreX;
            CentreZ = centreZ;
            HalfX = halfX;
            HalfZ = halfZ;
            Rotation = rotation;
        }

        public float CentreX { get; }
        public float CentreZ { get; }

        /// <summary>Half the size along the rectangle's own X / Z axis.</summary>
        public float HalfX { get; }
        public float HalfZ { get; }
        public float Rotation { get; }

        private double Radians => Rotation * Math.PI / 180.0;

        /// <summary>The rectangle's own X axis in room coordinates.</summary>
        public (float X, float Z) AxisX => ((float)Math.Cos(Radians), (float)-Math.Sin(Radians));

        /// <summary>The rectangle's own Z axis in room coordinates.</summary>
        public (float X, float Z) AxisZ => ((float)Math.Sin(Radians), (float)Math.Cos(Radians));

        /// <summary>The four corners in room coordinates.</summary>
        public (float X, float Z)[] Corners()
        {
            var (ax, az) = AxisX;
            var (bx, bz) = AxisZ;
            var result = new (float, float)[4];
            var i = 0;
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                result[i++] = (CentreX + ax * HalfX * sx + bx * HalfZ * sz, CentreZ + az * HalfX * sx + bz * HalfZ * sz);
            }
            return result;
        }

        /// <summary>Axis-aligned bounds.</summary>
        public Area Bounds
        {
            get
            {
                var (ax, az) = AxisX;
                var (bx, bz) = AxisZ;
                var ex = Math.Abs(ax) * HalfX + Math.Abs(bx) * HalfZ;
                var ez = Math.Abs(az) * HalfX + Math.Abs(bz) * HalfZ;
                return new Area(CentreX - ex, CentreZ - ez, CentreX + ex, CentreZ + ez);
            }
        }

        /// <summary>The same rectangle, <paramref name="by"/> metres larger on every side (negative: smaller).</summary>
        public OrientedArea Grown(float by) =>
            new OrientedArea(CentreX, CentreZ, Math.Max(0f, HalfX + by), Math.Max(0f, HalfZ + by), Rotation);

        public bool Contains(float x, float z, float tolerance = 0f)
        {
            // 0.1 mm for float noise of the turned axes (things exactly at the edge count as inside).
            var (lx, lz) = ToLocal(x, z);
            return Math.Abs(lx) <= HalfX + tolerance + 1e-4f && Math.Abs(lz) <= HalfZ + tolerance + 1e-4f;
        }

        /// <summary>The other rectangle lies completely inside this one (all its corners).</summary>
        public bool Contains(OrientedArea other, float tolerance = 0f)
        {
            foreach (var (x, z) in other.Corners())
            {
                if (!Contains(x, z, tolerance))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The rectangles overlap by more than <paramref name="margin"/> (separating axis test).</summary>
        public bool Overlaps(OrientedArea other, float margin = 0f)
        {
            foreach (var axis in new[] { AxisX, AxisZ, other.AxisX, other.AxisZ })
            {
                var (minA, maxA) = Project(this, axis);
                var (minB, maxB) = Project(other, axis);
                if (maxA - margin <= minB || maxB - margin <= minA)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Room point → the rectangle's own coordinates (centre = 0, 0).</summary>
        public (float X, float Z) ToLocal(float x, float z)
        {
            var dx = x - CentreX;
            var dz = z - CentreZ;
            var (ax, az) = AxisX;
            var (bx, bz) = AxisZ;
            return (dx * ax + dz * az, dx * bx + dz * bz);
        }

        /// <summary>The rectangle's own coordinates → room point.</summary>
        public (float X, float Z) ToRoom(float localX, float localZ)
        {
            var (ax, az) = AxisX;
            var (bx, bz) = AxisZ;
            return (CentreX + ax * localX + bx * localZ, CentreZ + az * localX + bz * localZ);
        }

        private static (float Min, float Max) Project(OrientedArea area, (float X, float Z) axis)
        {
            var min = float.MaxValue;
            var max = float.MinValue;
            foreach (var (x, z) in area.Corners())
            {
                var d = x * axis.X + z * axis.Z;
                min = Math.Min(min, d);
                max = Math.Max(max, d);
            }
            return (min, max);
        }
    }
}
