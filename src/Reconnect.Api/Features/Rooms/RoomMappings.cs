using Reconnect.Contracts.Rooms;
using Reconnect.Domain.Rooms;

namespace Reconnect.Api.Features.Rooms;

public static class RoomMappings
{
    public static RoomDto ToDto(this Room room, string ownerDisplayName) =>
        new(room.Id, room.Name, room.BuildingId, room.OwnerId, ownerDisplayName, room.IsPublic,
            room.Layout.Select(ToDto).ToList(), room.CreatedAt, room.UpdatedAt, room.Theme, room.Width, room.Depth);

    public static RoomItemDto ToDto(this RoomItem item) =>
        new(item.ItemId, new Vector3Dto(item.Position.X, item.Position.Y, item.Position.Z), item.Rotation);

    public static RoomItem ToDomain(RoomItemDto dto) => new()
    {
        ItemId = dto.ItemId,
        Position = new Position3 { X = dto.Position.X, Y = dto.Position.Y, Z = dto.Position.Z },
        Rotation = dto.Rotation,
    };
}
