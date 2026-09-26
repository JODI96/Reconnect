using System.Collections.Concurrent;
using System.Text.Json;
using StackExchange.Redis;

namespace Reconnect.Modules.Rooms.Infrastructure;

/// <summary>Game state per room in Redis (<c>game:{roomId}:{gameId}</c>, expires after 6 h of inactivity).</summary>
internal interface IRoomGameStore
{
    Task<T?> GetAsync<T>(Guid roomId, string gameId) where T : class;

    /// <summary>Loads (or creates) the state, applies <paramref name="update"/> and saves it – serialised per room.</summary>
    Task<T> UpdateAsync<T>(Guid roomId, string gameId, Action<T> update) where T : class, new();
}

internal sealed class RedisRoomGameStore(IConnectionMultiplexer redis) : IRoomGameStore
{
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(6);

    // Moves of two players can arrive at the same moment: one update per game at a time.
    // Note: in-process lock – with several API instances this needs a Redis lock (e.g. LockTakeAsync).
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<T?> GetAsync<T>(Guid roomId, string gameId) where T : class
    {
        var value = await redis.GetDatabase().StringGetAsync(Key(roomId, gameId));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(value.ToString());
    }

    public async Task<T> UpdateAsync<T>(Guid roomId, string gameId, Action<T> update) where T : class, new()
    {
        var key = Key(roomId, gameId);
        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            var state = await GetAsync<T>(roomId, gameId) ?? new T();
            update(state);
            await redis.GetDatabase().StringSetAsync(key, JsonSerializer.Serialize(state), Expiry);
            return state;
        }
        finally
        {
            gate.Release();
        }
    }

    private static string Key(Guid roomId, string gameId) => $"game:{roomId}:{gameId}";
}
