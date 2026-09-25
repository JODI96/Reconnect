using System.ComponentModel.DataAnnotations;

namespace Reconnect.Api.Common.Auth;

/// <summary>
/// Bound from the "Jwt" configuration section. <see cref="SigningKey"/> is never stored in
/// appsettings: locally it comes from the Aspire parameter "jwt-signing-key" (user secrets).
/// </summary>
public sealed class JwtOptions
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
