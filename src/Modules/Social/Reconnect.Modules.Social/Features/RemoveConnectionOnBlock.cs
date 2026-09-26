using Microsoft.EntityFrameworkCore;
using Reconnect.Modules.Safety.Public;
using Reconnect.Modules.Social.Domain;
using Reconnect.Modules.Social.Infrastructure;
using Reconnect.SharedKernel.Events;

namespace Reconnect.Modules.Social.Features;

/// <summary>
/// A block removes likes in both directions and the match (messages cascade),
/// so a later unblock does not restore the connection.
/// </summary>
internal sealed class RemoveConnectionOnBlock(SocialDbContext db) : IIntegrationEventHandler<UserBlocked>
{
    public async Task HandleAsync(UserBlocked integrationEvent, CancellationToken ct)
    {
        var (a, b) = (integrationEvent.BlockerId, integrationEvent.BlockedId);
        var (first, second) = Match.Order(a, b);

        await db.Matches.Where(m => m.User1Id == first && m.User2Id == second).ExecuteDeleteAsync(ct);
        await db.Likes
            .Where(l => (l.FromUserId == a && l.ToUserId == b) || (l.FromUserId == b && l.ToUserId == a))
            .ExecuteDeleteAsync(ct);
    }
}
