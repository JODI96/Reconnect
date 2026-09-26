using Microsoft.AspNetCore.Identity;

namespace Reconnect.Modules.Identity.Infrastructure;

/// <summary>Login account (email + password). Public profile data lives in the Profiles module, same id.</summary>
internal sealed class AppUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Stored refresh token. Only the SHA-256 hash is persisted; tokens are rotated on every use.</summary>
internal sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
