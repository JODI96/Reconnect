namespace Reconnect.Modules.City.Public;

/// <summary>What other modules may ask about the city.</summary>
public interface ICityDirectory
{
    Task<bool> BuildingExistsAsync(Guid buildingId, CancellationToken ct);
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
}
