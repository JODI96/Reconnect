namespace Reconnect.Modules.Rooms.Domain;

/// <summary>Furniture of a freshly bought office (14 × 10 m) – the owner rearranges it later in the room editor.</summary>
internal static class StarterOffice
{
    public static List<RoomItem> Layout() =>
    [
        Item("custom-elevator", 7f, 9.2f, 0f),
        Item("ph-sofa_02", 3f, 7.8f, 180f),
        Item("ph-modern_coffee_table_01", 3f, 6.4f, 180f),
        Item("ph-modern_arm_chair_01", 10.5f, 3f, 90f),
        Item("ph-side_table_01", 10.5f, 4.2f, 180f),
        Item("ph-potted_plant_02", 0.8f, 0.8f, 180f),
        Item("ph-pachira_aquatica_01", 13.2f, 9.2f, 180f),
    ];

    private static RoomItem Item(string itemId, float x, float z, float rotation) =>
        new() { ItemId = itemId, Position = new Position3 { X = x, Z = z }, Rotation = rotation };
}
