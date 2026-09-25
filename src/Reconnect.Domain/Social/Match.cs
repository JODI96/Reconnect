namespace Reconnect.Domain.Social;

/// <summary>
/// Created when two users like each other. The pair is stored ordered
/// (<see cref="User1Id"/> &lt; <see cref="User2Id"/>) so each pair exists only once.
/// </summary>
public sealed class Match
{
    private Match() { }

    public Guid Id { get; private set; }
    public Guid User1Id { get; private set; }
    public Guid User2Id { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Match Create(Guid userA, Guid userB, DateTimeOffset now)
    {
        var (first, second) = Order(userA, userB);
        return new Match { Id = Guid.CreateVersion7(), User1Id = first, User2Id = second, CreatedAt = now };
    }

    public static (Guid First, Guid Second) Order(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    public bool Involves(Guid userId) => User1Id == userId || User2Id == userId;

    public Guid OtherUser(Guid userId) => userId == User1Id ? User2Id : User1Id;
}
