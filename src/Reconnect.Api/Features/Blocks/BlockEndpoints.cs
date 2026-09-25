using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Reconnect.Api.Common.Auth;
using Reconnect.Api.Common.Endpoints;
using Reconnect.Contracts;
using Reconnect.Domain.Social;
using Reconnect.Domain.Safety;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Features.Blocks;

public sealed class BlockEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Blocks.Group).WithTags("Safety").RequireAuthorization();

        group.MapPost("/{userId:guid}", BlockUser);
        group.MapDelete("/{userId:guid}", UnblockUser);
    }

    /// <summary>
    /// Blocks a user (idempotent). Removes likes in both directions and an existing match
    /// (including its messages), so a later unblock does not restore the connection.
    /// </summary>
    private static async Task<Results<NoContent, NotFound>> BlockUser(
        Guid userId, ClaimsPrincipal principal, ReconnectDbContext db, TimeProvider time, CancellationToken ct)
    {
        var me = principal.GetUserId();
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
        {
            return TypedResults.NotFound();
        }

        if (!await db.Blocks.AnyAsync(b => b.BlockerId == me && b.BlockedId == userId, ct))
        {
            db.Blocks.Add(Block.Create(me, userId, time.GetUtcNow()));
            await db.SaveChangesAsync(ct);
        }

        var (first, second) = Match.Order(me, userId);
        await db.Matches.Where(m => m.User1Id == first && m.User2Id == second).ExecuteDeleteAsync(ct);
        await db.Likes
            .Where(l => (l.FromUserId == me && l.ToUserId == userId) || (l.FromUserId == userId && l.ToUserId == me))
            .ExecuteDeleteAsync(ct);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnblockUser(
        Guid userId, ClaimsPrincipal principal, ReconnectDbContext db, CancellationToken ct)
    {
        var me = principal.GetUserId();
        await db.Blocks.Where(b => b.BlockerId == me && b.BlockedId == userId).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }
}
