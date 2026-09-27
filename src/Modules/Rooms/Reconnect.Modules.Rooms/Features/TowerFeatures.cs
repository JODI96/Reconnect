using Reconnect.Modules.Rooms.Infrastructure.Seeding;
using Reconnect.Modules.City.Public;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Reconnect.Contracts;
using Reconnect.Contracts.Rooms;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Modules.Rooms.Infrastructure;
using Reconnect.Modules.Rooms.Public;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.Rooms.Features;

internal static class TowerEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(ApiRoutes.Rooms.Path).WithTags("Rooms").RequireAuthorization();
        group.MapGet("/tower/{buildingId:guid}", GetTower);
    }

    /// <summary>
    /// The storeys of a building the caller may enter – public floors and their own offices – with live
    /// occupancy and lift queue length (for the lift panel). Empty for buildings without floors.
    /// </summary>
    private static async Task<Ok<TowerDto>> GetTower(
        Guid buildingId, ClaimsPrincipal principal, RoomReader reader, IRoomPresenceStore presence, IElevatorQueue queue,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var rooms = await (await reader.VisibleRoomsAsync(userId, ct))
            .Where(r => r.BuildingId == buildingId && r.Floor != null)
            .OrderBy(r => r.Floor).ThenBy(r => r.Name)
            .ToListAsync(ct);

        var floors = new List<TowerFloorDto>(rooms.Count);
        foreach (var room in rooms)
        {
            floors.Add(new TowerFloorDto(room.Id, room.Floor!.Value, room.Name, room.Theme, room.Capacity,
                await presence.CountAsync(room.Id), await queue.LengthAsync(room.Id), room.IsPublic, room.IsOwnedBy(userId)));
        }
        return TypedResults.Ok(new TowerDto(buildingId, floors));
    }
}

internal sealed class RoomProvisioning(RoomsDbContext db, ICityDirectory city) : IRoomProvisioning
{
    /// <summary>People in a whole office storey at once.</summary>
    public const int OfficeCapacity = 60;

    /// <summary>A bought office is a whole storey with the real floor plan of the building.</summary>
    public async Task<Guid> CreateOfficeAsync(Guid ownerId, Guid buildingId, int floor, string name, int capacity, CancellationToken ct)
    {
        var footprint = await city.GetFootprintAsync(buildingId, ct);
        var plan = footprint is { Count: >= 3 } ? TowerFloorPlan.FromFootprint(footprint) : TowerFurnishing.PrimeTower;
        var room = Room.Create(ownerId, buildingId, name, isPublic: false, RoomThemes.Coworking);
        room.PlaceOnFloor(floor, capacity);
        room.ShapeAs(plan);
        room.ReplaceLayout(StarterOffice.Layout(plan));
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);
        return room.Id;
    }

    public Task DeleteRoomAsync(Guid roomId, CancellationToken ct) =>
        db.Rooms.Where(r => r.Id == roomId).ExecuteDeleteAsync(ct);
}
