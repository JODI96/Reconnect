using Reconnect.Contracts.Rooms;
using Reconnect.Modules.City.Public;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Modules.Rooms.Features;

namespace Reconnect.Modules.Rooms.Infrastructure.Seeding;

/// <summary>Tower storeys: the real floor plan, the concrete core in the middle, furniture fitted to the outline.</summary>
internal static class TowerFurnishing
{
    /// <summary>Every storey of the Prime Tower has the outline of the building (OpenStreetMap).</summary>
    public static readonly TowerFloorPlan PrimeTower = TowerFloorPlan.FromFootprint(ZurichBuildings.PrimeTowerFootprint);

    /// <summary>The core with the lifts, centred on the floor, long side along the building.</summary>
    public static RoomItem Core(TowerFloorPlan plan)
    {
        var definition = ItemDefinitions.Find(RoomZones.CoreItem)
            ?? throw new InvalidOperationException("custom-core is missing from the build catalog (run Setup Project).");
        var (cx, cz) = plan.Centre;
        var cells = RoomLayout.Footprint(definition, cx, cz, 0);
        var (x, z) = RoomLayout.Centre(cells);
        return new RoomItem { ItemId = RoomZones.CoreItem, Position = new Position3 { X = x, Z = z }, Rotation = 0f };
    }

    /// <summary>
    /// Core plus furniture moved by (dx, dz) metres, then put onto the grid inside the outline (nearest free legal
    /// place for everything, small things onto tables).
    /// </summary>
    public static List<RoomItem> Fit(TowerFloorPlan plan, string theme, IEnumerable<RoomItem> furniture, float dx = 0f, float dz = 0f)
    {
        var items = new List<RoomItemDto> { Core(plan).ToDto() };
        items.AddRange(furniture
            .Where(i => !RoomZones.IsLift(i.ItemId))
            .Select(i => new RoomItemDto(i.ItemId, new Vector3Dto(i.Position.X + dx, 0f, i.Position.Z + dz), i.Rotation)));
        return RoomLayoutFixer.Legalize(theme, plan.Width, plan.Depth, items, outline: plan.Outline)
            .Select(RoomMappings.ToDomain)
            .ToList();
    }
}
