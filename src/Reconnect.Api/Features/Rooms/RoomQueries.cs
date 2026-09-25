using Microsoft.EntityFrameworkCore;
using Reconnect.Api.Features.Blocks;
using Reconnect.Contracts.Rooms;
using Reconnect.Domain.Rooms;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Features.Rooms;

public static class RoomQueries
{
    /// <summary>Rooms the user may see: own rooms and public rooms of users not blocked in either direction.</summary>
    public static IQueryable<Room> VisibleRooms(this ReconnectDbContext db, Guid userId)
    {
        var hiddenUserIds = db.HiddenUserIdsFor(userId);
        return db.Rooms.Where(r => r.OwnerId == userId || (r.IsPublic && !hiddenUserIds.Contains(r.OwnerId)));
    }

    public static async Task<RoomDto?> QueryRoomDto(this ReconnectDbContext db, IQueryable<Room> rooms, CancellationToken ct)
    {
        var row = await rooms
            .Join(db.Profiles, r => r.OwnerId, o => o.UserId, (r, o) => new { Room = r, OwnerName = o.DisplayName })
            .SingleOrDefaultAsync(ct);

        return row?.Room.ToDto(row.OwnerName);
    }
}
