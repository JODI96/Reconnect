using Reconnect.Domain.Common;

namespace Reconnect.Domain.Social;

public sealed class Message
{
    public const int TextMaxLength = 2000;

    private Message() { }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Text { get; private set; } = "";
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>Callers must ensure the sender takes part in the match.</summary>
    public static Message Create(Match match, Guid senderId, string text, DateTimeOffset now)
    {
        if (!match.Involves(senderId))
        {
            throw new DomainException("Only participants of a match can send messages.");
        }

        text = text.Trim();
        if (text.Length is 0 or > TextMaxLength)
        {
            throw new DomainException($"Message must be 1-{TextMaxLength} characters.");
        }

        return new Message { Id = Guid.CreateVersion7(), MatchId = match.Id, SenderId = senderId, Text = text, SentAt = now };
    }
}
