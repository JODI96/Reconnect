using System;
using System.Collections.Generic;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>Unity world units, Y is up.</summary>
    public sealed record Vector3Dto(float X, float Y, float Z);

    /// <param name="Rotation">Rotation around the Y axis in degrees.</param>
    public sealed record RoomItemDto(string ItemId, Vector3Dto Position, float Rotation);

    public sealed record RoomSummaryDto(
        Guid Id,
        string Name,
        Guid BuildingId,
        Guid OwnerId,
        string OwnerDisplayName,
        bool IsPublic,
        DateTimeOffset UpdatedAt);

    public sealed record RoomDto(
        Guid Id,
        string Name,
        Guid BuildingId,
        Guid OwnerId,
        string OwnerDisplayName,
        bool IsPublic,
        IReadOnlyList<RoomItemDto> Layout,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public sealed record CreateRoomRequest(Guid BuildingId, string Name, bool IsPublic);

    public sealed record UpdateRoomLayoutRequest(IReadOnlyList<RoomItemDto> Items);
}
