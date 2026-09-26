using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Reconnect.Contracts;
using Reconnect.Contracts.Common;
using Reconnect.Contracts.Rooms;
using Reconnect.Modules.City.Public;
using Reconnect.Modules.Profiles.Public;
using Reconnect.Modules.Rooms.Domain;
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
            .Select(r => new RoomSummaryDto(r.Id, r.Name, r.BuildingId, r.OwnerId, names.GetValueOrDefault(r.OwnerId, ""), r.IsPublic, r.UpdatedAt))
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

        var room = Room.Create(principal.GetUserId(), request.BuildingId, request.Name, request.IsPublic, request.Theme);
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created(ApiRoutes.Rooms.ById(room.Id), await reader.ToDtoAsync(room, ct));
    }

    /// <summary>Replaces the whole layout. Only the owner may do this (403 otherwise).</summary>
    private static async Task<Results<Ok<RoomDto>, NotFound, ForbidHttpResult>> UpdateLayout(
        Guid id, UpdateRoomLayoutRequest request, ClaimsPrincipal principal, RoomsDbContext db, RoomReader reader,
        CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var room = await (await reader.VisibleRoomsAsync(userId, ct)).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (room is null)
        {
            return TypedResults.NotFound();
        }
        if (!room.IsOwnedBy(userId))
        {
            return TypedResults.Forbid();
        }

        room.ReplaceLayout(request.Items.Select(RoomMappings.ToDomain));
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(await reader.ToDtoAsync(room, ct));
    }
}
