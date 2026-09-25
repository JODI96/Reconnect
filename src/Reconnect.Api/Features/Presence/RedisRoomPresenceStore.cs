using System.Text.Json;
using StackExchange.Redis;

namespace Reconnect.Api.Features.Presence;

/// <summary>
/// Redis layout:
/// <list type="bullet">
/// <item><c>presence:room:{roomId}</c> – hash userId → entry (JSON)</item>
/// <item><c>presence:conn:{connectionId}</c> – "roomId|userId", expires after a day as a safety net</item>
/// </list>
/// </summary>
public sealed class RedisRoomPresenceStore(IConnectionMultiplexer redis) : IRoomPresenceStore
{
    private static readonly TimeSpan ConnectionTtl = TimeSpan.FromDays(1);

    private IDatabase Db => redis.GetDatabase();

    public async Task<IReadOnlyList<PresenceEntry>> GetPlayersAsync(Guid roomId)
    {
        var values = await Db.HashValuesAsync(RoomKey(roomId));
        return values.Select(v => JsonSerializer.Deserialize<PresenceEntry>(v.ToString())!).ToList();
    }

    public async Task<PresenceEntry?> AddAsync(PresenceEntry entry)
    {
        var previous = await GetAsync(entry.RoomId, entry.UserId);
        if (previous is not null && previous.ConnectionId != entry.ConnectionId)
        {
            await Db.KeyDeleteAsync(ConnectionKey(previous.ConnectionId));
        }

        await Db.HashSetAsync(RoomKey(entry.RoomId), entry.UserId.ToString(), JsonSerializer.Serialize(entry));
        await Db.StringSetAsync(ConnectionKey(entry.ConnectionId), $"{entry.RoomId}|{entry.UserId}", ConnectionTtl);
        return previous is not null && previous.ConnectionId != entry.ConnectionId ? previous : null;
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
        Db.HashSetAsync(RoomKey(entry.RoomId), entry.UserId.ToString(), JsonSerializer.Serialize(entry with { X = x, Z = z }));

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
