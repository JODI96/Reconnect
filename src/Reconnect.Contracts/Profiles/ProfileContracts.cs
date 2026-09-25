using System;

namespace Reconnect.Contracts.Profiles
{
    /// <summary>Profile as seen by other users (age instead of birth date).</summary>
    public sealed record ProfileDto(Guid UserId, string DisplayName, int Age, string? Bio, bool IsVerified);

    /// <summary>The caller's own profile.</summary>
    public sealed record MyProfileDto(Guid UserId, string Email, string DisplayName, DateTime BirthDate, string? Bio, bool IsVerified);

    public sealed record UpdateProfileRequest(string DisplayName, string? Bio);
}
