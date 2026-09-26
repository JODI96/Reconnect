using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Reconnect.Contracts;
using Reconnect.Contracts.RealEstate;
using Reconnect.Contracts.Wallet;
using Reconnect.Modules.RealEstate.Domain;
using Reconnect.Modules.RealEstate.Infrastructure;
using Reconnect.Modules.Rooms.Public;
using Reconnect.Modules.Wallet.Public;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.RealEstate.Features;

internal static class OfficeEndpoints
{
    public const int OfficeCapacity = 12;

    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(ApiRoutes.RealEstate.Path).WithTags("RealEstate").RequireAuthorization();
        group.MapGet("/buildings/{buildingId:guid}/units", List);
        group.MapPost("/units/{unitId:guid}/buy", Buy).RequireRateLimiting(RateLimitPolicies.Writes);
        group.MapPost("/units/{unitId:guid}/sell", Sell).RequireRateLimiting(RateLimitPolicies.Writes);
    }

    /// <summary>All offices of a building: available ones with price, and the caller's own (with their room).</summary>
    private static async Task<Ok<List<OfficeUnitDto>>> List(
        Guid buildingId, ClaimsPrincipal principal, RealEstateDbContext db, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var units = await db.Offices
            .Where(o => o.BuildingId == buildingId && (o.OwnerId == null || o.OwnerId == userId))
            .OrderBy(o => o.Floor).ThenBy(o => o.Name)
            .ToListAsync(ct);
        return TypedResults.Ok(units.Select(u => ToDto(u, userId)).ToList());
    }

    /// <summary>
    /// Pays and takes the office: money first (atomic, never below zero), then the office is claimed with a
    /// single "only if still free" update – if someone was faster, the money is refunded. Then the owner's
    /// room on that floor is created.
    /// </summary>
    private static async Task<Results<Ok<OfficeTransactionDto>, NotFound, Conflict<ProblemDetails>, ProblemHttpResult>> Buy(
        Guid unitId, ClaimsPrincipal principal, RealEstateDbContext db, IWallet wallet, IRoomProvisioning rooms,
        TimeProvider time, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var unit = await db.Offices.AsNoTracking().SingleOrDefaultAsync(o => o.Id == unitId, ct);
        if (unit is null)
        {
            return TypedResults.NotFound();
        }
        if (!unit.IsAvailable)
        {
            return Taken();
        }

        var price = new Chf(unit.PriceRappen);
        var reference = $"office:{unit.Id}";
        if (!await wallet.TryDebitAsync(userId, price, TransactionKinds.OfficePurchase, $"Kauf {unit.Name}, Prime Tower", reference, ct))
        {
            return TypedResults.Problem(title: "Nicht genug Guthaben", statusCode: StatusCodes.Status400BadRequest,
                detail: $"Das Büro kostet {price}, dein Guthaben reicht nicht.");
        }

        var claimed = await db.Offices
            .Where(o => o.Id == unitId && o.OwnerId == null)
            .ExecuteUpdateAsync(o => o
                .SetProperty(x => x.OwnerId, userId)
                .SetProperty(x => x.PurchasedAt, time.GetUtcNow()), ct);
        if (claimed == 0)
        {
            await wallet.CreditAsync(userId, price, TransactionKinds.OfficePurchase, $"Rückerstattung {unit.Name} (schon verkauft)", reference, ct);
            return Taken();
        }

        var roomId = await rooms.CreateOfficeAsync(userId, unit.BuildingId, unit.Floor, unit.Name, OfficeCapacity, ct);
        await db.Offices.Where(o => o.Id == unitId).ExecuteUpdateAsync(o => o.SetProperty(x => x.RoomId, roomId), ct);

        var updated = await db.Offices.AsNoTracking().SingleAsync(o => o.Id == unitId, ct);
        return TypedResults.Ok(new OfficeTransactionDto(ToDto(updated, userId), (await wallet.GetBalanceAsync(userId, ct)).Francs));
    }

    /// <summary>Sells the office back to the building for its purchase price (a market between users comes later).</summary>
    private static async Task<Results<Ok<OfficeTransactionDto>, NotFound>> Sell(
        Guid unitId, ClaimsPrincipal principal, RealEstateDbContext db, IWallet wallet, IRoomProvisioning rooms, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var unit = await db.Offices.AsNoTracking().SingleOrDefaultAsync(o => o.Id == unitId && o.OwnerId == userId, ct);
        if (unit is null)
        {
            return TypedResults.NotFound();
        }

        // Only one sale can win (double click, two devices): release first, then pay out.
        var released = await db.Offices
            .Where(o => o.Id == unitId && o.OwnerId == userId)
            .ExecuteUpdateAsync(o => o
                .SetProperty(x => x.OwnerId, (Guid?)null)
                .SetProperty(x => x.RoomId, (Guid?)null)
                .SetProperty(x => x.PurchasedAt, (DateTimeOffset?)null), ct);
        if (released == 0)
        {
            return TypedResults.NotFound();
        }

        await wallet.CreditAsync(userId, new Chf(unit.PriceRappen), TransactionKinds.OfficeSale, $"Verkauf {unit.Name}, Prime Tower", $"office:{unit.Id}", ct);
        if (unit.RoomId is { } roomId)
        {
            await rooms.DeleteRoomAsync(roomId, ct);
        }

        var updated = await db.Offices.AsNoTracking().SingleAsync(o => o.Id == unitId, ct);
        return TypedResults.Ok(new OfficeTransactionDto(ToDto(updated, userId), (await wallet.GetBalanceAsync(userId, ct)).Francs));
    }

    private static Conflict<ProblemDetails> Taken() =>
        TypedResults.Conflict(new ProblemDetails { Title = "Schon verkauft", Detail = "Dieses Büro hat inzwischen jemand anderes gekauft." });

    private static OfficeUnitDto ToDto(OfficeUnit unit, Guid viewerId)
    {
        var mine = unit.OwnerId == viewerId;
        return new OfficeUnitDto(unit.Id, unit.BuildingId, unit.Floor, unit.Name, unit.PriceRappen / 100m, unit.AreaSquareMeters,
            unit.IsAvailable, mine, mine ? unit.RoomId : null);
    }
}
