using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Reconnect.Contracts;
using Reconnect.Contracts.Safety;
using Reconnect.Modules.Identity.Public;
using Reconnect.Modules.Safety.Domain;
using Reconnect.Modules.Safety.Infrastructure;
using Reconnect.Modules.Safety.Public;
using Reconnect.SharedKernel.Events;
using Reconnect.SharedKernel.Web;
using ReportReason = Reconnect.Modules.Safety.Domain.ReportReason;

namespace Reconnect.Modules.Safety.Features;

internal sealed class BlockQueries(SafetyDbContext db) : IBlockQueries
{
    public Task<bool> IsBlockedBetweenAsync(Guid userA, Guid userB, CancellationToken ct) =>
        db.Blocks.AnyAsync(b =>
            (b.BlockerId == userA && b.BlockedId == userB) ||
            (b.BlockerId == userB && b.BlockedId == userA), ct);

    public async Task<IReadOnlyCollection<Guid>> HiddenUserIdsAsync(Guid userId, CancellationToken ct) =>
        await db.Blocks.Where(b => b.BlockerId == userId).Select(b => b.BlockedId)
            .Concat(db.Blocks.Where(b => b.BlockedId == userId).Select(b => b.BlockerId))
            .Distinct()
            .ToListAsync(ct);
}

internal static class BlockEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(ApiRoutes.Blocks.Path).WithTags("Safety").RequireAuthorization();
        group.MapPost("/{userId:guid}", BlockUser).RequireRateLimiting(RateLimitPolicies.Writes);
        group.MapDelete("/{userId:guid}", UnblockUser);
    }

    /// <summary>Blocks a user (idempotent) and tells the other modules (likes and matches are removed).</summary>
    private static async Task<Results<NoContent, NotFound>> BlockUser(
        Guid userId, ClaimsPrincipal principal, SafetyDbContext db, IUserDirectory users, IEventBus events,
        TimeProvider time, CancellationToken ct)
    {
        var me = principal.GetUserId();
        if (!await users.ExistsAsync(userId, ct))
        {
            return TypedResults.NotFound();
        }

        if (!await db.Blocks.AnyAsync(b => b.BlockerId == me && b.BlockedId == userId, ct))
        {
            db.Blocks.Add(Block.Create(me, userId, time.GetUtcNow()));
            await db.SaveChangesAsync(ct);
        }

        await events.PublishAsync(new UserBlocked(me, userId), ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnblockUser(Guid userId, ClaimsPrincipal principal, SafetyDbContext db, CancellationToken ct)
    {
        var me = principal.GetUserId();
        await db.Blocks.Where(b => b.BlockerId == me && b.BlockedId == userId).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }
}

internal static class ReportEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(ApiRoutes.Reports.Path).WithTags("Safety").RequireAuthorization();
        group.MapPost("/", CreateReport).RequireRateLimiting(RateLimitPolicies.Writes);
    }

    /// <summary>Stores a report for moderation. Reports work even if the users blocked each other.</summary>
    private static async Task<Results<Created<ReportCreatedResponse>, NotFound>> CreateReport(
        CreateReportRequest request, ClaimsPrincipal principal, SafetyDbContext db, IUserDirectory users,
        TimeProvider time, CancellationToken ct)
    {
        if (!await users.ExistsAsync(request.ReportedUserId, ct))
        {
            return TypedResults.NotFound();
        }

        var report = Report.Create(principal.GetUserId(), request.ReportedUserId, (ReportReason)request.Reason,
            request.Comment, request.RoomId, request.MessageId, time.GetUtcNow());
        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created((string?)null, new ReportCreatedResponse(report.Id));
    }
}
