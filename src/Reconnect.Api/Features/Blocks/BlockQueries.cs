using Microsoft.EntityFrameworkCore;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Features.Blocks;

/// <summary>
/// Block rules shared by all features: a block works in both directions.
/// Every query that returns users or user content must use these helpers.
/// </summary>
public static class BlockQueries
{
    public static Task<bool> IsBlockedBetweenAsync(this ReconnectDbContext db, Guid userA, Guid userB, CancellationToken ct) =>
        db.Blocks.AnyAsync(b =>
            (b.BlockerId == userA && b.BlockedId == userB) ||
            (b.BlockerId == userB && b.BlockedId == userA), ct);

    /// <summary>All users the given user must not see (blocked by them or blocking them).</summary>
    public static IQueryable<Guid> HiddenUserIdsFor(this ReconnectDbContext db, Guid userId) =>
        db.Blocks.Where(b => b.BlockerId == userId).Select(b => b.BlockedId)
            .Concat(db.Blocks.Where(b => b.BlockedId == userId).Select(b => b.BlockerId));
}
