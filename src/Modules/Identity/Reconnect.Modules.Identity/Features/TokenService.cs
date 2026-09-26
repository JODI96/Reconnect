using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Reconnect.Contracts.Auth;
using Reconnect.Modules.Identity.Domain;
using Reconnect.Modules.Identity.Infrastructure;

namespace Reconnect.Modules.Identity.Features;

/// <summary>
/// Bound from the "Jwt" configuration section. <see cref="SigningKey"/> is never stored in
/// appsettings: locally it comes from the Aspire parameter "jwt-signing-key" (user secrets).
/// </summary>
internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    [Required, MinLength(32)]
    public string SigningKey { get; set; } = "";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);
}

/// <summary>Issues JWT access tokens and rotating refresh tokens.</summary>
internal sealed class TokenService(IdentityDbContext db, IOptions<JwtOptions> options, TimeProvider time)
{
    private readonly JwtOptions _options = options.Value;

    public async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var accessExpires = now.Add(_options.AccessTokenLifetime);
        var refreshExpires = now.Add(_options.RefreshTokenLifetime);

        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessExpires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(CreateSigningKey(_options.SigningKey), SecurityAlgorithms.HmacSha256),
        });

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(refreshToken),
            CreatedAt = now,
            ExpiresAt = refreshExpires,
        });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(user.Id, accessToken, accessExpires, refreshToken, refreshExpires);
    }

    /// <summary>Validates and revokes the given refresh token and issues a new pair. Null if invalid.</summary>
    public async Task<AuthResponse?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        var now = time.GetUtcNow();

        if (stored is null || !stored.IsActive(now))
        {
            return null;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null || (user.LockoutEnd is { } lockoutEnd && lockoutEnd > now))
        {
            return null;
        }

        stored.RevokedAt = now;
        return await IssueAsync(user, ct);
    }

    public static SymmetricSecurityKey CreateSigningKey(string key) => new(Encoding.UTF8.GetBytes(key));

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
