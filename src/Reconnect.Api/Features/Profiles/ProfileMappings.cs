using Reconnect.Contracts.Profiles;
using Reconnect.Domain.Profiles;

namespace Reconnect.Api.Features.Profiles;

public static class ProfileMappings
{
    public static ProfileDto ToDto(this Profile profile, DateOnly today) =>
        new(profile.UserId, profile.DisplayName, profile.AgeOn(today), profile.Bio, profile.IsVerified);
}
