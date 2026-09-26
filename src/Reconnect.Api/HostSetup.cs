using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Api;

/// <summary>Aspire resource names (must match Reconnect.AppHost).</summary>
public static class ResourceNames
{
    public const string Redis = "redis";
    public const string Blobs = "blobs";
}

/// <summary>
/// Rate limiting policies used by the modules (<see cref="RateLimitPolicies"/>). Limits come from
/// the "RateLimiting" configuration section, so tests and later environments can tune them.
/// </summary>
public static class RateLimitingSetup
{
    public sealed class Options
    {
        /// <summary>Login/registration/refresh requests per client IP and minute.</summary>
        public int AuthPerMinute { get; set; } = 20;

        /// <summary>Rate-limited writes (likes, reports, blocks, new rooms) per user and minute.</summary>
        public int WritesPerMinute { get; set; } = 60;
    }

    public static IHostApplicationBuilder AddReconnectRateLimiting(this IHostApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection("RateLimiting").Get<Options>() ?? new Options();

        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => Window(options.AuthPerMinute)));

            limiter.AddPolicy(RateLimitPolicies.Writes, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimsPrincipalExtensions.SubjectClaim)
                    ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => Window(options.WritesPerMinute)));
        });

        return builder;
    }

    private static FixedWindowRateLimiterOptions Window(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };
}
