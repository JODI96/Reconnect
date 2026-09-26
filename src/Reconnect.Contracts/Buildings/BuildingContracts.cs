using System;
using System.Collections.Generic;

namespace Reconnect.Contracts.Buildings
{
    public sealed record GeoPointDto(double Latitude, double Longitude);

    /// <param name="DistanceMeters">Distance to the queried point (only set for nearby queries).</param>
    /// <param name="Footprint">Ground plan outline (not closed), if known – e.g. for towers with storeys.
    /// Source: OpenStreetMap (© OpenStreetMap contributors, ODbL).</param>
    public sealed record BuildingDto(
        Guid Id, string Name, string Address, double Latitude, double Longitude, double? DistanceMeters,
        IReadOnlyList<GeoPointDto>? Footprint = null);
}
