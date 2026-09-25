using System;

namespace Reconnect.Contracts.Buildings
{
    /// <param name="DistanceMeters">Distance to the queried point (only set for nearby queries).</param>
    public sealed record BuildingDto(Guid Id, string Name, string Address, double Latitude, double Longitude, double? DistanceMeters);
}
