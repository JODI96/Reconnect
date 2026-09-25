using Reconnect.Domain.Common;

namespace Reconnect.Domain.Safety;

/// <summary>A block hides both users from each other in both directions.</summary>
public sealed class Block
{
    private Block() { }

    public Guid Id { get; private set; }
    public Guid BlockerId { get; private set; }
    public Guid BlockedId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Block Create(Guid blockerId, Guid blockedId, DateTimeOffset now)
    {
        if (blockerId == blockedId)
        {
            throw new DomainException("You cannot block yourself.");
        }
        return new Block { Id = Guid.CreateVersion7(), BlockerId = blockerId, BlockedId = blockedId, CreatedAt = now };
    }
}
