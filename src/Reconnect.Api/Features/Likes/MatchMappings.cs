using Reconnect.Contracts.Social;
using Reconnect.Domain.Social;

namespace Reconnect.Api.Features.Likes;

public static class MatchMappings
{
    /// <summary>Match from the perspective of <paramref name="viewerId"/>.</summary>
    public static MatchDto ToDto(this Match match, Guid viewerId) =>
        new(match.Id, match.OtherUser(viewerId), match.CreatedAt);
}
