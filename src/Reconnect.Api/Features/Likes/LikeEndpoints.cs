using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Reconnect.Api.Common.Auth;
using Reconnect.Api.Common.Endpoints;
using Reconnect.Api.Features.Blocks;
using Reconnect.Api.Hubs;
using Reconnect.Contracts;
using Reconnect.Contracts.Social;
using Reconnect.Domain.Social;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Features.Likes;

public sealed class LikeEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.Likes.Group).WithTags("Likes").RequireAuthorization();

        group.MapPost("/{userId:guid}", LikeUser);
    }

    /// <summary>
    /// Likes a user (idempotent). If the other user already liked the caller, a match is
    /// created and both are notified via SignalR. Blocked users cannot like each other (403).
    /// </summary>
    private static async Task<Results<Ok<LikeResponse>, NotFound, ForbidHttpResult>> LikeUser(
        Guid userId, ClaimsPrincipal principal, ReconnectDbContext db, TimeProvider time,
        IHubContext<ChatHub, IChatClient> hub, CancellationToken ct)
    {
        var me = principal.GetUserId();
        if (!await db.Profiles.AnyAsync(p => p.UserId == userId, ct))
        {
            return TypedResults.NotFound();
        }
        if (await db.IsBlockedBetweenAsync(me, userId, ct))
        {
            return TypedResults.Forbid();
        }

        if (!await db.Likes.AnyAsync(l => l.FromUserId == me && l.ToUserId == userId, ct))
        {
            db.Likes.Add(Like.Create(me, userId, time.GetUtcNow()));
            await SaveIgnoringDuplicateAsync(db, ct);
        }

        var likedBack = await db.Likes.AnyAsync(l => l.FromUserId == userId && l.ToUserId == me, ct);
        if (!likedBack)
        {
            return TypedResults.Ok(new LikeResponse(IsMatch: false, Match: null));
        }

        var (match, isNew) = await GetOrCreateMatchAsync(db, me, userId, time.GetUtcNow(), ct);
        if (isNew)
        {
            await hub.Clients.User(me.ToString()).MatchCreated(match.ToDto(me));
            await hub.Clients.User(userId.ToString()).MatchCreated(match.ToDto(userId));
        }

        return TypedResults.Ok(new LikeResponse(IsMatch: true, match.ToDto(me)));
    }

    private static async Task<(Match Match, bool IsNew)> GetOrCreateMatchAsync(
        ReconnectDbContext db, Guid userA, Guid userB, DateTimeOffset now, CancellationToken ct)
    {
        var (first, second) = Match.Order(userA, userB);
        var existing = await db.Matches.SingleOrDefaultAsync(m => m.User1Id == first && m.User2Id == second, ct);
        if (existing is not null)
        {
            return (existing, false);
        }

        var match = Match.Create(userA, userB, now);
        db.Matches.Add(match);
        if (await SaveIgnoringDuplicateAsync(db, ct))
        {
            return (match, true);
        }

        // Both users liked at the same moment and the other request created the match first.
        return (await db.Matches.SingleAsync(m => m.User1Id == first && m.User2Id == second, ct), false);
    }

    /// <summary>Saves; returns false (and detaches pending inserts) on a unique-index violation.</summary>
    private static async Task<bool> SaveIgnoringDuplicateAsync(ReconnectDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
