namespace Reconnect.Modules.Rooms.Domain;

/// <summary>Furniture of a freshly bought office (14 × 10 m) – the owner rearranges it later in the build editor.</summary>
internal static class StarterOffice
{
    public static List<RoomItem> Layout() =>
    [
        Cell("custom-elevator", 18, 29, 0),   // Lift
        Cell("ph-sofa_02", 8, 30, 180),   // Chesterfield-Sofa
        Cell("ph-modern_coffee_table_01", 12, 22, 180),   // Moderner Couchtisch
        Cell("ph-modern_arm_chair_01", 40, 10, 90),   // Moderner Sessel
        Cell("ph-side_table_01", 40, 16, 180),   // Beistelltisch rund
        Cell("ph-potted_plant_02", 2, 2, 180),   // Zimmerpflanze
        Cell("ph-pachira_aquatica_01", 50, 34, 180),   // Glückskastanie
    ];

    private static RoomItem Cell(string itemId, int x, int z, float rotation) => RoomItem.AtCell(itemId, x, z, rotation);

    private static RoomItem At(string itemId, float x, float z, float rotation) => RoomItem.At(itemId, x, z, rotation);
}
