namespace Reconnect.Modules.Rooms.Domain;

/// <summary>Furniture of a freshly bought office (14 × 10 m) – the owner rearranges it later in the build editor.</summary>
internal static class StarterOffice
{
    public static List<RoomItem> Layout() =>
    [
        Cell("custom-elevator", 9, 15, 0),   // Lift
        Cell("ph-sofa_02", 4, 15, 180),   // Chesterfield-Sofa
        Cell("ph-modern_coffee_table_01", 6, 11, 180),   // Moderner Couchtisch
        Cell("ph-modern_arm_chair_01", 20, 5, 90),   // Moderner Sessel
        Cell("ph-side_table_01", 20, 8, 180),   // Beistelltisch rund
        Cell("ph-potted_plant_02", 1, 1, 180),   // Zimmerpflanze
        Cell("ph-pachira_aquatica_01", 25, 17, 180),   // Glückskastanie
    ];

    private static RoomItem Cell(string itemId, int x, int z, float rotation) => RoomItem.AtCell(itemId, x, z, rotation);
}
