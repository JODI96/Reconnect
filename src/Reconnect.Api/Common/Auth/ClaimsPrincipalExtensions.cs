using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Reconnect.Api.Common.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The authenticated user's id (JWT "sub" claim).</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new InvalidOperationException("No authenticated user.");
}
