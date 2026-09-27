using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Reconnect.Contracts;
using Reconnect.Contracts.Common;
using Reconnect.Contracts.Rooms;
using Reconnect.Modules.City.Public;
using Reconnect.Modules.Profiles.Public;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Modules.Rooms.Hubs;
using Reconnect.Modules.Rooms.Infrastructure;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.Rooms.Features;

internal static class RoomEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(ApiRoutes.Rooms.Path).WithTags("Rooms").RequireAuthorization();

        group.MapGet("/", List);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create).RequireRateLimiting(RateLimitPolicies.Writes);
        group.MapPut("/{id:guid}/layout", UpdateLayout);
    }

    /// <summary>Public rooms plus the caller's own rooms; rooms of blocked users are hidden.</summary>
    private static async Task<Ok<PagedResponse<RoomSummaryDto>>> List(
        Guid? buildingId, int? page, int? pageSize, ClaimsPrincipal principal, RoomReader reader,
        IProfileDirectory profiles, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, pageSize);

        var query = await reader.VisibleRoomsAsync(principal.GetUserId(), ct);
        if (buildingId is not null)
        {
            query = query.Where(r => r.BuildingId == buildingId);
        }

        var total = await query.CountAsync(ct);
        var rooms = await query
            .OrderByDescending(r => r.UpdatedAt)
            .ThenBy(r => r.Id)
            .Skip((p - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        // Owner names come from the Profiles module – one batched call per page.
        var names = await profiles.GetDisplayNamesAsync(rooms.Select(r => r.OwnerId), ct);
        var items = rooms
            .Select(r => new RoomSummaryDto(r.Id, r.Name, r.BuildingId, r.OwnerId,
                TowerOwners.NameOf(r.OwnerId) ?? names.GetValueOrDefault(r.OwnerId, ""), r.IsPublic, r.UpdatedAt, r.Floor))
            .ToList();

        return TypedResults.Ok(new PagedResponse<RoomSummaryDto>(items, p, size, total));
    }

    private static async Task<Results<Ok<RoomDto>, NotFound>> GetById(
        Guid id, ClaimsPrincipal principal, RoomReader reader, CancellationToken ct)
    {
        var dto = await reader.FindVisibleAsync(principal.GetUserId(), id, ct);
        return dto is null ? TypedResults.NotFound() : TypedResults.Ok(dto);
    }

    private static async Task<Results<Created<RoomDto>, ValidationProblem>> Create(
        CreateRoomRequest request, ClaimsPrincipal principal, RoomsDbContext db, RoomReader reader,
        ICityDirectory city, CancellationToken ct)
    {
        if (!await city.BuildingExistsAsync(request.BuildingId, ct))
        {
            return Validation.Problem(nameof(request.BuildingId), "Building does not exist.");
        }
        if (await db.Rooms.AnyAsync(r => r.BuildingId == request.BuildingId && r.Floor != null, ct))
        {
            // Towers have floors and offices for sale instead of free rooms.
            return Validation.Problem(nameof(request.BuildingId), "In diesem Gebäude kann man Büros kaufen, aber keine Räume anlegen.");
        }

        var room = Room.Create(principal.GetUserId(), request.BuildingId, request.Name, request.IsPublic, request.Theme);
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created(ApiRoutes.Rooms.ById(room.Id), await reader.ToDtoAsync(room, ct));
    }

    /// <summary>
    /// Replaces the whole layout (build editor). The owner may build in their room, admins in every room (403 otherwise).
    /// Every item must follow the build rules – 400 with one error per broken rule. Everyone in the room gets the new
    /// layout live and stands up (seats are addressed by layout index).
    /// </summary>
    private static async Task<Results<Ok<RoomDto>, NotFound, ForbidHttpResult, ValidationProblem>> UpdateLayout(
        Guid id, UpdateRoomLayoutRequest request, ClaimsPrincipal principal, RoomsDbContext db, RoomReader reader,
        IRoomPresenceStore presence, IHubContext<RoomHub, IRoomClient> hub, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var isAdmin = principal.IsInRole(AppRoles.Admin);
        var rooms = isAdmin ? db.Rooms : await reader.VisibleRoomsAsync(userId, ct);
        var room = await rooms.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (room is null)
        {
            return TypedResults.NotFound();
        }
        if (!room.IsOwnedBy(userId) && !isAdmin)
        {
            return TypedResults.Forbid();
        }

        var items = request.Items ?? [];
        var problems = RoomLayout.Validate(room.BuildContext(items), items).ToList();
        // The core of a tower floor belongs to the building: it stays exactly where it is (its colour may change).
        var fixedBefore = room.Layout.Select(RoomMappings.ToDto).Where(i => RoomZones.IsFixed(i.ItemId)).ToList();
        foreach (var part in fixedBefore.Where(part => !items.Any(i => i.ItemId == part.ItemId && i.Position == part.Position && i.Rotation == part.Rotation)))
        {
            problems.Add(new LayoutProblem(-1, $"„{ItemDefinitions.Find(part.ItemId)?.Name ?? part.ItemId}“ gehört zum Gebäude und bleibt, wo es ist."));
        }
        if (items.Count(i => RoomZones.IsFixed(i.ItemId)) > fixedBefore.Count)
        {
            problems.Add(new LayoutProblem(-1, "Einen Gebäudekern kann man nicht bauen."));
        }
        if (problems.Count > 0)
        {
            return TypedResults.ValidationProblem(problems
                .GroupBy(p => p.Index < 0 ? "items" : $"items[{p.Index}]")
                .ToDictionary(g => g.Key, g => g.Select(p => p.Message).ToArray()));
        }

        room.ReplaceLayout(items.Select(RoomMappings.ToDomain));
        await db.SaveChangesAsync(ct);

        var players = await presence.GetPlayersAsync(room.Id);
        foreach (var player in players.Where(p => p.Seat is not null))
        {
            await presence.UpdateSeatAsync(player, null);
        }
        var dto = await reader.ToDtoAsync(room, ct);
        await hub.Clients.Clients(players.Select(p => p.ConnectionId).ToList())
            .RoomLayoutChanged(new RoomLayoutChangedDto(room.Id, dto.Layout));

        return TypedResults.Ok(dto);
    }
}
