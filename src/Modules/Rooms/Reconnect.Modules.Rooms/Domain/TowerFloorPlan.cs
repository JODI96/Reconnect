using Reconnect.Contracts.Rooms;

namespace Reconnect.Modules.Rooms.Domain;

/// <summary>
/// A tower storey as a room: the real building outline (from the map) in room metres. The room's X axis runs along the
/// building's long side (smallest enclosing rectangle), so the floor fits the tower exactly and the slanted facades stay
/// slanted. The geo anchor puts the room's corner (0, 0) on the globe for the 3D city.
/// </summary>
internal sealed record TowerFloorPlan(int Width, int Depth, IReadOnlyList<RoomPointDto> Outline, GeoAnchorDto Anchor)
{
    private const double EarthRadius = 6371008.8;

    /// <summary>Middle of the floor in room metres (where the core goes).</summary>
    public (float X, float Z) Centre
    {
        get
        {
            // Centroid of the polygon.
            double area = 0, cx = 0, cz = 0;
            for (int i = 0, j = Outline.Count - 1; i < Outline.Count; j = i++)
            {
                var cross = Outline[j].X * Outline[i].Z - Outline[i].X * Outline[j].Z;
                area += cross;
                cx += (Outline[j].X + Outline[i].X) * cross;
                cz += (Outline[j].Z + Outline[i].Z) * cross;
            }
            area /= 2;
            return ((float)(cx / (6 * area)), (float)(cz / (6 * area)));
        }
    }

    public static TowerFloorPlan FromFootprint(IReadOnlyList<(double Latitude, double Longitude)> footprint)
    {
        if (footprint.Count < 3)
        {
            throw new ArgumentException("A footprint needs at least three corners.", nameof(footprint));
        }
        var lat0 = footprint.Average(p => p.Latitude);
        var lon0 = footprint.Average(p => p.Longitude);
        var metresPerDegreeLat = Math.PI / 180 * EarthRadius;
        var metresPerDegreeLon = metresPerDegreeLat * Math.Cos(lat0 * Math.PI / 180);
        // East / north metres around the middle.
        var points = footprint.Select(p => ((p.Longitude - lon0) * metresPerDegreeLon, (p.Latitude - lat0) * metresPerDegreeLat)).ToList();

        // Smallest enclosing rectangle: one of its sides lies on an edge of the outline.
        (double Area, double Angle) best = (double.MaxValue, 0);
        for (var i = 0; i < points.Count; i++)
        {
            var (x1, z1) = points[i];
            var (x2, z2) = points[(i + 1) % points.Count];
            var angle = Math.Atan2(z2 - z1, x2 - x1);
            var (width, depth, _, _) = Extent(points, angle);
            if (width * depth < best.Area)
            {
                best = (width * depth, width >= depth ? angle : angle + Math.PI / 2);
            }
        }
        var (w, d, minX, minZ) = Extent(points, best.Angle);

        // Corner (0, 0) of the room back to the globe: turn by +angle, then metres → degrees.
        var cos = Math.Cos(best.Angle);
        var sin = Math.Sin(best.Angle);
        var east = minX * cos - minZ * sin;
        var north = minX * sin + minZ * cos;
        var anchor = new GeoAnchorDto(lat0 + north / metresPerDegreeLat, lon0 + east / metresPerDegreeLon, (float)(-best.Angle * 180 / Math.PI));

        var outline = points
            .Select(p => Rotate(p, -best.Angle))
            .Select(p => new RoomPointDto((float)(p.X - minX), (float)(p.Z - minZ)))
            .ToList();
        return new TowerFloorPlan((int)Math.Ceiling(w), (int)Math.Ceiling(d), outline, anchor);
    }

    private static (double Width, double Depth, double MinX, double MinZ) Extent(List<(double X, double Z)> points, double angle)
    {
        var turned = points.Select(p => Rotate(p, -angle)).ToList();
        var minX = turned.Min(p => p.X);
        var minZ = turned.Min(p => p.Z);
        return (turned.Max(p => p.X) - minX, turned.Max(p => p.Z) - minZ, minX, minZ);
    }

    private static (double X, double Z) Rotate((double X, double Z) p, double angle) =>
        (p.X * Math.Cos(angle) - p.Z * Math.Sin(angle), p.X * Math.Sin(angle) + p.Z * Math.Cos(angle));
}
