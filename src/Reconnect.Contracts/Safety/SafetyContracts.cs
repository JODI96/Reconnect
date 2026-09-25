using System;

namespace Reconnect.Contracts.Safety
{
    /// <summary>Serialized as string (e.g. "Harassment").</summary>
    public enum ReportReason
    {
        Spam = 0,
        Harassment = 1,
        InappropriateContent = 2,
        FakeProfile = 3,
        Underage = 4,
        Other = 5,
    }

    /// <param name="RoomId">Optional context: room where it happened.</param>
    /// <param name="MessageId">Optional context: offending chat message.</param>
    public sealed record CreateReportRequest(
        Guid ReportedUserId,
        ReportReason Reason,
        string? Comment,
        Guid? RoomId,
        Guid? MessageId);

    public sealed record ReportCreatedResponse(Guid Id);
}
