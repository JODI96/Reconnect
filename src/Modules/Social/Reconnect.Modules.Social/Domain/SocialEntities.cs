using Reconnect.SharedKernel.Domain;

namespace Reconnect.Modules.Social.Domain;

internal sealed class Like
{
    private Like() { }

    public Guid Id { get; private set; }
    public Guid FromUserId { get; private set; }
    public Guid ToUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Like Create(Guid fromUserId, Guid toUserId, DateTimeOffset now)
    {
        if (fromUserId == toUserId)
        {
            throw new DomainException("You cannot like yourself.");
        }
        return new Like { Id = Guid.CreateVersion7(), FromUserId = fromUserId, ToUserId = toUserId, CreatedAt = now };
    }
}

/// <summary>
/// Created when two users like each other. The pair is stored ordered
/// (<see cref="User1Id"/> &lt; <see cref="User2Id"/>) so each pair exists only once.
/// </summary>
internal sealed class Match
{
    private Match() { }

    public Guid Id { get; private set; }
    public Guid User1Id { get; private set; }
    public Guid User2Id { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Match Create(Guid userA, Guid userB, DateTimeOffset now)
    {
        if (userA == userB)
        {
            throw new DomainException("A match needs two different users.");
        }
        var (first, second) = Order(userA, userB);
        return new Match { Id = Guid.CreateVersion7(), User1Id = first, User2Id = second, CreatedAt = now };
    }

    public static (Guid First, Guid Second) Order(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    public bool Involves(Guid userId) => User1Id == userId || User2Id == userId;

    public Guid OtherUser(Guid userId) => userId == User1Id ? User2Id : User1Id;
}

internal sealed class Message
{
    public const int TextMaxLength = 2000;

    private Message() { }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Text { get; private set; } = "";
    public DateTimeOffset SentAt { get; private set; }

    public static Message Create(Match match, Guid senderId, string text, DateTimeOffset now)
    {
        if (!match.Involves(senderId))
        {
            throw new DomainException("Only participants of a match can send messages.");
        }

        text = (text ?? "").Trim();
        if (text.Length is 0 or > TextMaxLength)
        {
            throw new DomainException($"Message must be 1-{TextMaxLength} characters.");
        }

        return new Message { Id = Guid.CreateVersion7(), MatchId = match.Id, SenderId = senderId, Text = text, SentAt = now };
    }
}
