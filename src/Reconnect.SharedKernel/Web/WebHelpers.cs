using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Reconnect.SharedKernel.Domain;

namespace Reconnect.SharedKernel.Web;

public static class ClaimsPrincipalExtensions
{
    /// <summary>JWT claim names (RFC 7519) – tokens are validated with MapInboundClaims = false.</summary>
    public const string SubjectClaim = "sub";
    public const string EmailClaim = "email";
    public const string RoleClaim = "role";

    /// <summary>The authenticated user's id (JWT "sub" claim).</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(SubjectClaim), out var id)
            ? id
            : throw new InvalidOperationException("No authenticated user.");

    /// <summary>The authenticated user's email (JWT "email" claim).</summary>
    public static string GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(EmailClaim) ?? "";
}

/// <summary>Application roles (stored by Identity, carried as "role" claims in the access token).</summary>
public static class AppRoles
{
    public const string Admin = "Admin";

    /// <summary>Paying members (subscription). Set by a future billing module; admins count as premium too.</summary>
    public const string Premium = "Premium";

    public static bool IsPremium(this ClaimsPrincipal principal) => principal.IsInRole(Premium) || principal.IsInRole(Admin);
}

/// <summary>Names of the rate limiting policies (configured by the API host, used by module endpoints).</summary>
public static class RateLimitPolicies
{
    /// <summary>Login, registration, token refresh – per client IP.</summary>
    public const string Auth = "auth";

    /// <summary>Reports, likes, blocks and other writes that could be spammed – per user.</summary>
    public const string Writes = "writes";
}

public static class TimeProviderExtensions
{
    public static DateOnly GetUtcToday(this TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Clamps user input to sane values: page ≥ 1, 1 ≤ pageSize ≤ <see cref="MaxPageSize"/>.</summary>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize) =>
        (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}

public static class Validation
{
    /// <summary>A 400 validation problem for a single field.</summary>
    public static ValidationProblem Problem(string field, string error) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [error] });
}

/// <summary>Turns a violated business rule into a 400 ProblemDetails response.</summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Business rule violated",
                Detail = domainException.Message,
            },
        });
    }
}
