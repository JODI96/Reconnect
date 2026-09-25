using Reconnect.Domain.Common;

namespace Reconnect.Domain.Social;

public sealed class Like
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
