using NetTopologySuite.Geometries;

namespace Reconnect.Domain.Buildings;

/// <summary>A real building in Zurich that acts as an entrance to user rooms.</summary>
public sealed class Building
{
    /// <summary>WGS84 – all geometries use this spatial reference.</summary>
    public const int Srid = 4326;

    private Building() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Address { get; private set; } = "";

    /// <summary>Entrance / centre point. X = longitude, Y = latitude.</summary>
    public Point Location { get; private set; } = null!;

    /// <summary>Optional ground plan (filled from swisstopo data in phase 6).</summary>
    public Polygon? Footprint { get; private set; }

    public static Building Create(Guid id, string name, string address, double latitude, double longitude, Polygon? footprint = null) =>
        new()
        {
            Id = id,
            Name = name,
            Address = address,
            Location = CreatePoint(latitude, longitude),
            Footprint = footprint,
        };

    public static Point CreatePoint(double latitude, double longitude) =>
        new(longitude, latitude) { SRID = Srid };
}
