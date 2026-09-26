using Reconnect.SharedKernel.Domain;

namespace Reconnect.Modules.Safety.Domain;

/// <summary>A block hides both users from each other in both directions.</summary>
internal sealed class Block
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

internal enum ReportReason
{
    Spam = 0,
    Harassment = 1,
    InappropriateContent = 2,
    FakeProfile = 3,
    Underage = 4,
    Other = 5,
}

internal enum ReportStatus
{
    Open = 0,
    InReview = 1,
    Resolved = 2,
    Dismissed = 3,
}

/// <summary>
/// User report for moderation. Room/message ids are context references into other modules
/// (no foreign keys across modules) – moderation looks them up.
/// </summary>
internal sealed class Report
{
    public const int CommentMaxLength = 1000;

    private Report() { }

    public Guid Id { get; private set; }
    public Guid ReporterId { get; private set; }
    public Guid ReportedUserId { get; private set; }
    public ReportReason Reason { get; private set; }
    public string? Comment { get; private set; }
    public Guid? RoomId { get; private set; }
    public Guid? MessageId { get; private set; }
    public ReportStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Report Create(Guid reporterId, Guid reportedUserId, ReportReason reason, string? comment,
        Guid? roomId, Guid? messageId, DateTimeOffset now)
    {
        if (reporterId == reportedUserId)
        {
            throw new DomainException("You cannot report yourself.");
        }
        if (!Enum.IsDefined(reason))
        {
            throw new DomainException("Unknown report reason.");
        }
        if (comment is { Length: > CommentMaxLength })
        {
            throw new DomainException($"Comment must be at most {CommentMaxLength} characters.");
        }

        return new Report
        {
            Id = Guid.CreateVersion7(),
            ReporterId = reporterId,
            ReportedUserId = reportedUserId,
            Reason = reason,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            RoomId = roomId,
            MessageId = messageId,
            Status = ReportStatus.Open,
            CreatedAt = now,
        };
    }
}
