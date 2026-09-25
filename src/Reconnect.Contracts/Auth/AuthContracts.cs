using System;

namespace Reconnect.Contracts.Auth
{
    /// <param name="BirthDate">Only the date part is used. Users must be at least 18.</param>
    public sealed record RegisterRequest(string Email, string Password, string DisplayName, DateTime BirthDate);

    public sealed record LoginRequest(string Email, string Password);

    public sealed record RefreshRequest(string RefreshToken);

    public sealed record AuthResponse(
        Guid UserId,
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshTokenExpiresAt);
}
