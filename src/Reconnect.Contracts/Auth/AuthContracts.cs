using System;
using System.Collections.Generic;

namespace Reconnect.Contracts.Auth
{
    /// <param name="BirthDate">Only the date part is used. Users must be at least 18.</param>
    public sealed record RegisterRequest(string Email, string Password, string DisplayName, DateTime BirthDate);

    public sealed record LoginRequest(string Email, string Password);

    public sealed record RefreshRequest(string RefreshToken);

    /// <param name="Roles">Roles of the account (e.g. "Admin": may build in every room).</param>
    public sealed record AuthResponse(
        Guid UserId,
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshTokenExpiresAt,
        IReadOnlyList<string>? Roles = null);
}
