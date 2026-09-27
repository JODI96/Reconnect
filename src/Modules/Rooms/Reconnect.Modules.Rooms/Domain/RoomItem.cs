using Reconnect.Contracts.Rooms;

namespace Reconnect.Modules.Rooms.Domain;

/// <summary>A placed furniture item. <see cref="ItemId"/> references the client-side item catalogue.</summary>
internal sealed class RoomItem
{
    public const int ItemIdMaxLength = 64;

    public required string ItemId { get; init; }
    public required Position3 Position { get; init; }

    /// <summary>Rotation around the Y axis in degrees.</summary>
    public float Rotation { get; init; }

    /// <summary>
    /// An item placed like the build editor does: its footprint starts at build cell (<paramref name="x"/>,
    /// <paramref name="z"/>) – 50 cm cells, see <see cref="RoomLayout"/> – turned by <paramref name="rotation"/> (0/90/180/270).
    /// </summary>
    /// <summary>A small thing on a table: its centre in metres on the finer decor grid (<see cref="BuildGrid.DecorStep"/>).</summary>
    public static RoomItem At(string itemId, float x, float z, float rotation = 0f) =>
        new() { ItemId = itemId, Position = new Position3 { X = x, Z = z }, Rotation = rotation };

    public static RoomItem AtCell(string itemId, int x, int z, float rotation = 0f)
    {
        var definition = ItemDefinitions.Find(itemId) ?? throw new ArgumentException($"Unknown item {itemId}.", nameof(itemId));
        var (width, depth) = RoomLayout.Size(definition, RoomLayout.Quarter(rotation));
        var (cx, cz) = RoomLayout.Centre(new CellRect(x, z, width, depth));
        return new RoomItem { ItemId = itemId, Position = new Position3 { X = cx, Z = cz }, Rotation = rotation };
    }
}

/// <summary>Position inside a room in Unity world units (Y is up).</summary>
internal sealed class Position3
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
}

/// <summary>A corner of a room's floor outline (metres).</summary>
internal sealed class FloorPoint
{
    public float X { get; init; }
    public float Z { get; init; }
}
