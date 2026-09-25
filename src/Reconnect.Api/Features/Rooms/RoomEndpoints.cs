using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Reconnect.Api.Common;
using Reconnect.Api.Common.Auth;
using Reconnect.Api.Common.Endpoints;
using Reconnect.Api.Features.Blocks;
using Reconnect.Contracts;
using Reconnect.Contracts.Common;
using Reconnect.Contracts.Rooms;
using Reconnect.Domain.Rooms;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Features.Rooms;

public sealed class RoomEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Rooms.Group).WithTags("Rooms").RequireAuthorization();

        group.MapGet("/", List);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}/layout", UpdateLayout);
    }

    /// <summary>Public rooms plus the caller's own rooms; rooms of blocked users are hidden.</summary>
    private static async Task<Ok<PagedResponse<RoomSummaryDto>>> List(
        Guid? buildingId, int? page, int? pageSize, ClaimsPrincipal principal, ReconnectDbContext db, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var (p, size) = Paging.Normalize(page, pageSize);

        var query = db.VisibleRooms(userId);
        if (buildingId is not null)
        {
            query = query.Where(r => r.BuildingId == buildingId);
        }

        var total = await query.CountAsync(ct);
        // Sort/page on entity columns first; EF can't order by members of a constructed DTO.
        var items = await query
            .OrderByDescending(r => r.UpdatedAt)
            .ThenBy(r => r.Id)
            .Skip((p - 1) * size)
            .Take(size)
            .Join(db.Profiles, r => r.OwnerId, o => o.UserId, (r, o) => new { Room = r, OwnerName = o.DisplayName })
            .OrderByDescending(x => x.Room.UpdatedAt)
            .ThenBy(x => x.Room.Id)
            .Select(x => new RoomSummaryDto(
                x.Room.Id, x.Room.Name, x.Room.BuildingId, x.Room.OwnerId, x.OwnerName, x.Room.IsPublic, x.Room.UpdatedAt))
            .ToListAsync(ct);

        return TypedResults.Ok(new PagedResponse<RoomSummaryDto>(items, p, size, total));
    }

    private static async Task<Results<Ok<RoomDto>, NotFound>> GetById(
        Guid id, ClaimsPrincipal principal, ReconnectDbContext db, CancellationToken ct)
    {
        var dto = await db.QueryRoomDto(db.VisibleRooms(principal.GetUserId()).Where(r => r.Id == id), ct);
        return dto is null ? TypedResults.NotFound() : TypedResults.Ok(dto);
    }

    private static async Task<Results<Created<RoomDto>, ValidationProblem>> Create(
        CreateRoomRequest request, ClaimsPrincipal principal, ReconnectDbContext db, CancellationToken ct)
    {
        if (!await db.Buildings.AnyAsync(b => b.Id == request.BuildingId, ct))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.BuildingId)] = ["Building does not exist."],
            });
        }

        var room = Room.Create(principal.GetUserId(), request.BuildingId, request.Name, request.IsPublic);
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);

        var dto = await db.QueryRoomDto(db.Rooms.Where(r => r.Id == room.Id), ct);
        return TypedResults.Created(ApiRoutes.Rooms.ById(room.Id), dto);
    }

    /// <summary>Replaces the whole layout. Only the owner may do this (403 otherwise).</summary>
    private static async Task<Results<Ok<RoomDto>, NotFound, ForbidHttpResult>> UpdateLayout(
        Guid id, UpdateRoomLayoutRequest request, ClaimsPrincipal principal, ReconnectDbContext db, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var room = await db.VisibleRooms(userId).SingleOrDefaultAsync(r => r.Id == id, ct);
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

        return TypedResults.Ok((await db.QueryRoomDto(db.Rooms.Where(r => r.Id == id), ct))!);
    }
}
