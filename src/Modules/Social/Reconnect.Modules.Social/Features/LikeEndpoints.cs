using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Reconnect.Contracts;
using Reconnect.Contracts.Social;
using Reconnect.Modules.Profiles.Public;
using Reconnect.Modules.Safety.Public;
using Reconnect.Modules.Social.Domain;
using Reconnect.Modules.Social.Hubs;
using Reconnect.Modules.Social.Infrastructure;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.Social.Features;

internal static class LikeEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(ApiRoutes.Likes.Path).WithTags("Likes").RequireAuthorization();
        group.MapPost("/{userId:guid}", LikeUser).RequireRateLimiting(RateLimitPolicies.Writes);
    }

    /// <summary>
    /// Likes a user (idempotent). If the other user already liked the caller, a match is
    /// created and both are notified via SignalR. Blocked users cannot like each other (403).
    /// </summary>
    private static async Task<Results<Ok<LikeResponse>, NotFound, ForbidHttpResult>> LikeUser(
        Guid userId, ClaimsPrincipal principal, SocialDbContext db, IProfileDirectory profiles, IBlockQueries blocks,
        TimeProvider time, IHubContext<ChatHub, IChatClient> hub, CancellationToken ct)
    {
        var me = principal.GetUserId();
        if (!await profiles.ExistsAsync(userId, ct))
        {
            return TypedResults.NotFound();
        }
        if (await blocks.IsBlockedBetweenAsync(me, userId, ct))
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
        SocialDbContext db, Guid userA, Guid userB, DateTimeOffset now, CancellationToken ct)
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
    private static async Task<bool> SaveIgnoringDuplicateAsync(SocialDbContext db, CancellationToken ct)
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

internal static class MatchMappings
{
    /// <summary>Match from the perspective of <paramref name="viewerId"/>.</summary>
    public static MatchDto ToDto(this Match match, Guid viewerId) =>
        new(match.Id, match.OtherUser(viewerId), match.CreatedAt);
}
