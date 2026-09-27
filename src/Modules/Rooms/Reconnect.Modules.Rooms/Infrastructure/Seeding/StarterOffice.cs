using Reconnect.Modules.Rooms.Domain;

namespace Reconnect.Modules.Rooms.Infrastructure.Seeding;

/// <summary>Furniture of a freshly bought office storey – the owner rearranges it later in the build editor.</summary>
internal static class StarterOffice
{
    /// <summary>Reception, a desk island, a lounge and plants on the real floor plan – the rest is the owner's.</summary>
    public static List<RoomItem> Layout(TowerFloorPlan plan) => TowerFloorDesigns.StarterOffice(plan);
}
