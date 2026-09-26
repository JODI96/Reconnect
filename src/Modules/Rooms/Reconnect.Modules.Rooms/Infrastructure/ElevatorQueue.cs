using StackExchange.Redis;

namespace Reconnect.Modules.Rooms.Infrastructure;

/// <summary>
/// People waiting in the lift for a full floor, first come first served. A user waits for one floor at a
/// time (queueing again moves them). Redis layout:
/// <list type="bullet">
/// <item><c>elevator:queue:{roomId}</c> – sorted set userId, score = time of queueing</item>
/// <item><c>elevator:user:{userId}</c> – "roomId|connectionId" (whom to bring up when it's their turn)</item>
/// </list>
/// </summary>
internal interface IElevatorQueue
{
    /// <returns>Position (1 = next).</returns>
    Task<int> EnqueueAsync(Guid roomId, Guid userId, string connectionId, DateTimeOffset now);

    Task<int> PositionAsync(Guid roomId, Guid userId);

    Task<int> LengthAsync(Guid roomId);

    Task<Guid?> PeekAsync(Guid roomId);

    Task<IReadOnlyList<Guid>> MembersAsync(Guid roomId);

    Task<(Guid RoomId, string ConnectionId)?> GetForUserAsync(Guid userId);

    /// <returns>The floor the user was waiting for, if any.</returns>
    Task<Guid?> RemoveAsync(Guid userId);
}

internal sealed class RedisElevatorQueue(IConnectionMultiplexer redis) : IElevatorQueue
{
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(6);

    private IDatabase Db => redis.GetDatabase();

    public async Task<int> EnqueueAsync(Guid roomId, Guid userId, string connectionId, DateTimeOffset now)
    {
        var current = await GetForUserAsync(userId);
        if (current is { } waiting && waiting.RoomId != roomId)
        {
            await Db.SortedSetRemoveAsync(QueueKey(waiting.RoomId), userId.ToString());
        }

        // NX: queueing again for the same floor keeps the original place.
        await Db.SortedSetAddAsync(QueueKey(roomId), userId.ToString(), now.ToUnixTimeMilliseconds(), SortedSetWhen.NotExists);
        await Db.KeyExpireAsync(QueueKey(roomId), Expiry);
        await Db.StringSetAsync(UserKey(userId), $"{roomId}|{connectionId}", Expiry);
        return await PositionAsync(roomId, userId);
    }

    public async Task<int> PositionAsync(Guid roomId, Guid userId) =>
        await Db.SortedSetRankAsync(QueueKey(roomId), userId.ToString()) is { } rank ? (int)rank + 1 : 0;

    public async Task<int> LengthAsync(Guid roomId) => (int)await Db.SortedSetLengthAsync(QueueKey(roomId));

    public async Task<Guid?> PeekAsync(Guid roomId)
    {
        var first = await Db.SortedSetRangeByRankAsync(QueueKey(roomId), 0, 0);
        return first.Length == 0 ? null : Guid.Parse(first[0].ToString());
    }

    public async Task<IReadOnlyList<Guid>> MembersAsync(Guid roomId) =>
        (await Db.SortedSetRangeByRankAsync(QueueKey(roomId))).Select(v => Guid.Parse(v.ToString())).ToList();

    public async Task<(Guid RoomId, string ConnectionId)?> GetForUserAsync(Guid userId)
    {
        var value = await Db.StringGetAsync(UserKey(userId));
        if (value.IsNullOrEmpty)
        {
            return null;
        }
        var parts = value.ToString().Split('|', 2);
        return (Guid.Parse(parts[0]), parts[1]);
    }

    public async Task<Guid?> RemoveAsync(Guid userId)
    {
        var current = await GetForUserAsync(userId);
        await Db.KeyDeleteAsync(UserKey(userId));
        if (current is not { } waiting)
        {
            return null;
        }
        await Db.SortedSetRemoveAsync(QueueKey(waiting.RoomId), userId.ToString());
        return waiting.RoomId;
    }

    private static string QueueKey(Guid roomId) => $"elevator:queue:{roomId}";
    private static string UserKey(Guid userId) => $"elevator:user:{userId}";
}
