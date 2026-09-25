using Reconnect.Domain.Buildings;

namespace Reconnect.Infrastructure.Persistence.Seed;

/// <summary>
/// Sample buildings for development (part of the migration via HasData).
/// Replaced / extended by the swisstopo pipeline in phase 6. IDs must stay stable.
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

    public static IReadOnlyList<Building> All =>
    [
        Building.Create(HauptbahnhofId, "Zürich HB", "Bahnhofplatz 1, 8001 Zürich", 47.37785, 8.54018),
        Building.Create(GrossmuensterId, "Grossmünster", "Grossmünsterplatz, 8001 Zürich", 47.37011, 8.54411),
        Building.Create(PrimeTowerId, "Prime Tower", "Hardstrasse 201, 8005 Zürich", 47.38622, 8.51733),
        Building.Create(EthHauptgebaeudeId, "ETH Hauptgebäude", "Rämistrasse 101, 8092 Zürich", 47.37635, 8.54798),
        Building.Create(OpernhausId, "Opernhaus Zürich", "Falkenstrasse 1, 8008 Zürich", 47.36490, 8.54671),
        Building.Create(LandesmuseumId, "Landesmuseum", "Museumstrasse 2, 8001 Zürich", 47.37926, 8.54063),
        Building.Create(KunsthausId, "Kunsthaus Zürich", "Heimplatz 1, 8001 Zürich", 47.37036, 8.54826),
    ];
}
