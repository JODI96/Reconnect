namespace Reconnect.Modules.Profiles.Public;

/// <summary>What other modules may ask about profiles.</summary>
public interface IProfileDirectory
{
    Task<bool> ExistsAsync(Guid userId, CancellationToken ct);

    Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken ct);

    /// <summary>Display names for many users at once (missing users are left out).</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken ct);
}
