using System.Text.Json;
using Reconnect.Contracts.Rooms;
using StackExchange.Redis;

namespace Reconnect.Modules.Rooms.Infrastructure;

/// <summary>A user standing in a room, reachable via one SignalR connection.</summary>
/// <param name="Width">Room size, kept with the entry so moves can be validated without a database call.</param>
/// <param name="Seat">The seat the user sits on (cleared by walking).</param>
/// <param name="Look">The avatar's look (character creator), sent with the player.</param>
internal sealed record PresenceEntry(
    Guid RoomId, Guid UserId, string ConnectionId, string DisplayName, int X, int Z, int Width, int Depth, SeatDto? Seat = null,
    Contracts.Avatars.AvatarLookDto? Look = null);

/// <summary>
/// Who is in which room right now. Lives in Redis so that several API instances (and restarts)
/// share it. One entry per user and room – joining again from another device replaces the entry.
/// </summary>
internal interface IRoomPresenceStore
{
    Task<IReadOnlyList<PresenceEntry>> GetPlayersAsync(Guid roomId);

    Task<int> CountAsync(Guid roomId);

    /// <summary>
    /// Adds (or replaces) the user's entry if the room has space – checked and written atomically, so
    /// parallel joins can never exceed <paramref name="capacity"/>. A user already in the room always fits.
    /// </summary>
    /// <returns>Added = false when full; Replaced = the user's entry from another connection, if any.</returns>
    Task<(bool Added, PresenceEntry? Replaced)> TryAddAsync(PresenceEntry entry, int capacity);

    Task<PresenceEntry?> GetByConnectionAsync(string connectionId);

    /// <summary>Moves the user to a tile (standing up from a seat).</summary>
    Task UpdateTileAsync(PresenceEntry entry, int x, int z);

    /// <summary>Sits the user down (or stands up with null).</summary>
    Task UpdateSeatAsync(PresenceEntry entry, SeatDto? seat);

    /// <summary>The user changed their look while in the room.</summary>
    Task UpdateLookAsync(PresenceEntry entry, Contracts.Avatars.AvatarLookDto? look);

    /// <summary>Removes the user from a room they left by taking the lift (the connection now belongs to the new room).</summary>
    Task RemoveFromRoomAsync(Guid roomId, Guid userId);

    /// <summary>Removes the connection's entry (if it is still the user's current one) and returns it.</summary>
    Task<PresenceEntry?> RemoveByConnectionAsync(string connectionId);
}

/// <summary>
/// Redis layout:
/// <list type="bullet">
/// <item><c>presence:room:{roomId}</c> – hash userId → entry (JSON)</item>
/// <item><c>presence:conn:{connectionId}</c> – "roomId|userId", expires after a day as a safety net</item>
/// </list>
/// </summary>
internal sealed class RedisRoomPresenceStore(IConnectionMultiplexer redis) : IRoomPresenceStore
{
    private static readonly TimeSpan ConnectionTtl = TimeSpan.FromDays(1);

    // Capacity check and write in one step (Redis runs scripts atomically).
    private const string TryAddScript = """
        if redis.call('HEXISTS', KEYS[1], ARGV[1]) == 1 or redis.call('HLEN', KEYS[1]) < tonumber(ARGV[3]) then
            redis.call('HSET', KEYS[1], ARGV[1], ARGV[2])
            return 1
        end
        return 0
        """;

    private IDatabase Db => redis.GetDatabase();

    public async Task<IReadOnlyList<PresenceEntry>> GetPlayersAsync(Guid roomId)
    {
        var values = await Db.HashValuesAsync(RoomKey(roomId));
        return values.Select(v => JsonSerializer.Deserialize<PresenceEntry>(v.ToString())!).ToList();
    }

    public async Task<int> CountAsync(Guid roomId) => (int)await Db.HashLengthAsync(RoomKey(roomId));

    public async Task<(bool Added, PresenceEntry? Replaced)> TryAddAsync(PresenceEntry entry, int capacity)
    {
        var previous = await GetAsync(entry.RoomId, entry.UserId);
        var result = await Db.ScriptEvaluateAsync(TryAddScript,
            [RoomKey(entry.RoomId)],
            [entry.UserId.ToString(), JsonSerializer.Serialize(entry), capacity]);
        if ((int)result != 1)
        {
            return (false, null);
        }

        var replaced = previous is not null && previous.ConnectionId != entry.ConnectionId ? previous : null;
        if (replaced is not null)
        {
            await Db.KeyDeleteAsync(ConnectionKey(replaced.ConnectionId));
        }
        await Db.StringSetAsync(ConnectionKey(entry.ConnectionId), $"{entry.RoomId}|{entry.UserId}", ConnectionTtl);
        return (true, replaced);
    }

    public async Task<PresenceEntry?> GetByConnectionAsync(string connectionId)
    {
        var mapping = await Db.StringGetAsync(ConnectionKey(connectionId));
        if (mapping.IsNullOrEmpty)
        {
            return null;
        }

        var parts = mapping.ToString().Split('|');
        var entry = await GetAsync(Guid.Parse(parts[0]), Guid.Parse(parts[1]));
        return entry?.ConnectionId == connectionId ? entry : null;
    }

    public Task UpdateTileAsync(PresenceEntry entry, int x, int z) =>
        Db.HashSetAsync(RoomKey(entry.RoomId), entry.UserId.ToString(), JsonSerializer.Serialize(entry with { X = x, Z = z, Seat = null }));

    public Task UpdateSeatAsync(PresenceEntry entry, SeatDto? seat) =>
        Db.HashSetAsync(RoomKey(entry.RoomId), entry.UserId.ToString(), JsonSerializer.Serialize(entry with { Seat = seat }));

    public Task UpdateLookAsync(PresenceEntry entry, Contracts.Avatars.AvatarLookDto? look) =>
        Db.HashSetAsync(RoomKey(entry.RoomId), entry.UserId.ToString(), JsonSerializer.Serialize(entry with { Look = look }));

    public Task RemoveFromRoomAsync(Guid roomId, Guid userId) => Db.HashDeleteAsync(RoomKey(roomId), userId.ToString());

    public async Task<PresenceEntry?> RemoveByConnectionAsync(string connectionId)
    {
        var entry = await GetByConnectionAsync(connectionId);
        await Db.KeyDeleteAsync(ConnectionKey(connectionId));
        if (entry is not null)
        {
            await Db.HashDeleteAsync(RoomKey(entry.RoomId), entry.UserId.ToString());
        }
        return entry;
    }

    private async Task<PresenceEntry?> GetAsync(Guid roomId, Guid userId)
    {
        var value = await Db.HashGetAsync(RoomKey(roomId), userId.ToString());
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<PresenceEntry>(value.ToString());
    }

    private static string RoomKey(Guid roomId) => $"presence:room:{roomId}";
    private static string ConnectionKey(string connectionId) => $"presence:conn:{connectionId}";
}
