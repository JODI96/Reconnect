using Microsoft.EntityFrameworkCore;
using Reconnect.Contracts.Rooms;
using Reconnect.Modules.Profiles.Public;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Modules.Rooms.Infrastructure;
using Reconnect.Modules.Safety.Public;

namespace Reconnect.Modules.Rooms.Features;

/// <summary>Visibility rules and DTO building for rooms, shared by the REST endpoints and the room hub.</summary>
internal sealed class RoomReader(RoomsDbContext db, IBlockQueries blocks, IProfileDirectory profiles)
{
    /// <summary>Rooms the user may see: own rooms and public rooms of users not blocked in either direction.</summary>
    public async Task<IQueryable<Room>> VisibleRoomsAsync(Guid userId, CancellationToken ct)
    {
        var hiddenUserIds = (await blocks.HiddenUserIdsAsync(userId, ct)).ToList();
        return db.Rooms.Where(r => r.OwnerId == userId || (r.IsPublic && !hiddenUserIds.Contains(r.OwnerId)));
    }

    public async Task<RoomDto?> FindVisibleAsync(Guid userId, Guid roomId, CancellationToken ct)
    {
        var room = await (await VisibleRoomsAsync(userId, ct)).SingleOrDefaultAsync(r => r.Id == roomId, ct);
        return room is null ? null : await ToDtoAsync(room, ct);
    }

    public async Task<RoomDto> ToDtoAsync(Room room, CancellationToken ct) =>
        room.ToDto(await profiles.GetDisplayNameAsync(room.OwnerId, ct) ?? "");
}

internal static class RoomMappings
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
