namespace Reconnect.Api.Features.Presence;

/// <summary>A user standing in a room, reachable via one SignalR connection.</summary>
public sealed record PresenceEntry(Guid RoomId, Guid UserId, string ConnectionId, string DisplayName, int X, int Z);

/// <summary>
/// Who is in which room right now. Lives in Redis so that several API instances (and restarts)
/// share it. One entry per user and room – joining again from another device replaces the entry.
/// </summary>
public interface IRoomPresenceStore
{
    Task<IReadOnlyList<PresenceEntry>> GetPlayersAsync(Guid roomId);

    /// <summary>Adds or replaces the user's entry. Returns the replaced entry (other connection), if any.</summary>
    Task<PresenceEntry?> AddAsync(PresenceEntry entry);

    Task<PresenceEntry?> GetByConnectionAsync(string connectionId);

    Task UpdateTileAsync(PresenceEntry entry, int x, int z);

    /// <summary>Removes the connection's entry (if it is still the user's current one) and returns it.</summary>
    Task<PresenceEntry?> RemoveByConnectionAsync(string connectionId);
}
