using System;
using System.Collections.Generic;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>Unity world units, Y is up.</summary>
    public sealed record Vector3Dto(float X, float Y, float Z);

    /// <param name="Rotation">Rotation around the Y axis in degrees.</param>
    /// <param name="Colours">Chosen swatch per colour zone ("sage/walnut", see <see cref="ItemColours"/>); null = defaults.</param>
    public sealed record RoomItemDto(string ItemId, Vector3Dto Position, float Rotation, string Colours = null);

    public sealed record RoomSummaryDto(
        Guid Id,
        string Name,
        Guid BuildingId,
        Guid OwnerId,
        string OwnerDisplayName,
        bool IsPublic,
        DateTimeOffset UpdatedAt,
        int? Floor = null);

    public sealed record RoomDto(
        Guid Id,
        string Name,
        Guid BuildingId,
        Guid OwnerId,
        string OwnerDisplayName,
        bool IsPublic,
        IReadOnlyList<RoomItemDto> Layout,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        string Theme,
        int Width,
        int Depth,
        int? Floor = null,
        int Capacity = RoomGrid.MaxPlayers,
        IReadOnlyList<RoomPointDto>? Outline = null,
        GeoAnchorDto? Anchor = null);

    /// <param name="Theme">Optional: cozy (default), rooftop, cafe, atelier, opera, library, skylounge, lobby, coworking, conference.</param>
    public sealed record CreateRoomRequest(Guid BuildingId, string Name, bool IsPublic, string? Theme = null);

    /// <summary>
    /// Replaces the whole layout (owner or admin). Every item must follow the build rules (<see cref="RoomLayout"/>);
    /// otherwise 400 with one error per broken rule, keyed "items[index]".
    /// </summary>
    public sealed record UpdateRoomLayoutRequest(IReadOnlyList<RoomItemDto> Items);

    /// <summary>Hub event: the room was rebuilt (everyone stands up, seats are addressed by the new layout).</summary>
    public sealed record RoomLayoutChangedDto(Guid RoomId, IReadOnlyList<RoomItemDto> Layout);
}
