namespace Reconnect.Modules.Rooms.Domain;

/// <summary>A placed furniture item. <see cref="ItemId"/> references the client-side item catalogue.</summary>
internal sealed class RoomItem
{
    public const int ItemIdMaxLength = 64;

    public required string ItemId { get; init; }
    public required Position3 Position { get; init; }

    /// <summary>Rotation around the Y axis in degrees.</summary>
    public float Rotation { get; init; }
}

/// <summary>Position inside a room in Unity world units (Y is up).</summary>
internal sealed class Position3
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
}
