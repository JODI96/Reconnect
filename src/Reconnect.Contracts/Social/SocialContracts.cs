using System;

namespace Reconnect.Contracts.Social
{
    public sealed record MatchDto(Guid Id, Guid OtherUserId, DateTimeOffset CreatedAt);

    /// <param name="Match">Set when this like completed a mutual like.</param>
    public sealed record LikeResponse(bool IsMatch, MatchDto? Match);

    public sealed record MessageDto(Guid Id, Guid MatchId, Guid SenderId, string Text, DateTimeOffset SentAt);
}
