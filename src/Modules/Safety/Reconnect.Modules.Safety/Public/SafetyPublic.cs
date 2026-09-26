using Reconnect.SharedKernel.Events;

namespace Reconnect.Modules.Safety.Public;

/// <summary>
/// Block rules for all modules: a block works in both directions. Every query that returns users
/// or user content must respect it.
/// </summary>
public interface IBlockQueries
{
    Task<bool> IsBlockedBetweenAsync(Guid userA, Guid userB, CancellationToken ct);

    /// <summary>All users the given user must not see (blocked by them or blocking them).</summary>
    Task<IReadOnlyCollection<Guid>> HiddenUserIdsAsync(Guid userId, CancellationToken ct);
}

/// <summary><paramref name="BlockerId"/> blocked <paramref name="BlockedId"/> – e.g. Social removes likes and the match.</summary>
public sealed record UserBlocked(Guid BlockerId, Guid BlockedId) : IIntegrationEvent;
