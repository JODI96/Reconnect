using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Reconnect.Contracts.Auth;
using Reconnect.Infrastructure.Identity;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Common.Auth;

/// <summary>Issues JWT access tokens and rotating refresh tokens.</summary>
public sealed class TokenService(ReconnectDbContext db, IOptions<JwtOptions> options, TimeProvider time)
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
