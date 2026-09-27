namespace Reconnect.Modules.City.Public;

/// <summary>What other modules may ask about the city.</summary>
public interface ICityDirectory
{
    Task<bool> BuildingExistsAsync(Guid buildingId, CancellationToken ct);

    /// <summary>Outline of a building (latitude/longitude corners, not closed), or null if unknown.</summary>
    Task<IReadOnlyList<(double Latitude, double Longitude)>?> GetFootprintAsync(Guid buildingId, CancellationToken ct);
}

/// <summary>
/// Stable ids of the seeded sample buildings (development seed, tests, showcase rooms).
/// Replaced / extended by the swisstopo pipeline later. IDs must stay stable.
/// </summary>
public static class ZurichBuildings
{
    public static readonly Guid HauptbahnhofId = new("0199a000-0000-7000-8000-000000000001");
    public static readonly Guid GrossmuensterId = new("0199a000-0000-7000-8000-000000000002");
    public static readonly Guid PrimeTowerId = new("0199a000-0000-7000-8000-000000000003");
    public static readonly Guid EthHauptgebaeudeId = new("0199a000-0000-7000-8000-000000000004");
    public static readonly Guid OpernhausId = new("0199a000-0000-7000-8000-000000000005");
    public static readonly Guid LandesmuseumId = new("0199a000-0000-7000-8000-000000000006");
    public static readonly Guid KunsthausId = new("0199a000-0000-7000-8000-000000000007");
    public static readonly Guid SeebadUtoquaiId = new("0199a000-0000-7000-8000-000000000008");

    /// <summary>Prime Tower outline (OpenStreetMap way 47122541, © OpenStreetMap contributors, ODbL).</summary>
    public static readonly IReadOnlyList<(double Latitude, double Longitude)> PrimeTowerFootprint =
    [
        (47.3858168, 8.5172916), (47.3858427, 8.5173161), (47.3859997, 8.5174734), (47.3860325, 8.5174691),
        (47.3861503, 8.5174531), (47.3862611, 8.5174915), (47.3862929, 8.5175038), (47.3864032, 8.5172469),
        (47.3863787, 8.5172227), (47.3862219, 8.5170682), (47.3861908, 8.5170710), (47.3860271, 8.5170859),
        (47.3859049, 8.5170304), (47.3858759, 8.5170173),
    ];
}
