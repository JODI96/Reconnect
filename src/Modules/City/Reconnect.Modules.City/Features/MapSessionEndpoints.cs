using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reconnect.Contracts;
using Reconnect.Contracts.Buildings;
using Reconnect.Modules.City.Domain;
using Reconnect.Modules.City.Infrastructure;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.City.Features;

internal static class MapSessionEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup(ApiRoutes.Maps.Path).WithTags("Maps").RequireAuthorization();
        group.MapPost("/session", StartSession).RequireRateLimiting(RateLimitPolicies.Writes);
    }

    /// <summary>
    /// Called once per app session before the city is shown. Premium users always get Google
    /// Photorealistic 3D Tiles; free users get a few per month, then (or when the monthly budget is used
    /// up) swisstopo. Every Google answer counts as one billed Google session.
    /// </summary>
    private static async Task<Ok<MapSessionDto>> StartSession(
        ClaimsPrincipal principal, CityDbContext db, IOptions<MapOptions> options, TimeProvider time, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var isPremium = principal.IsPremium();
        var month = MapUsage.MonthOf(time.GetUtcToday());

        var userSessions = await db.MapUsage
            .Where(u => u.UserId == userId && u.Month == month)
            .Select(u => u.GoogleSessions)
            .SingleOrDefaultAsync(ct);
        var freeSessions = await db.MapUsage
            .Where(u => u.Month == month && !u.IsPremium)
            .SumAsync(u => u.GoogleSessions, ct);

        var settings = options.Value;
        var decision = MapPolicy.Decide(settings, isPremium, userSessions, freeSessions);
        string? tilesetUrl = null;
        if (decision.Provider == MapProviders.Google)
        {
            // Atomic upsert, so parallel app starts can't lose a count.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO city.map_usage (user_id, month, is_premium, google_sessions) VALUES ({userId}, {month}, {isPremium}, 1)
                ON CONFLICT (user_id, month) DO UPDATE
                SET google_sessions = map_usage.google_sessions + 1, is_premium = excluded.is_premium
                """, ct);
            tilesetUrl = $"{settings.Google.TilesetUrl}?key={Uri.EscapeDataString(settings.Google.ApiKey!)}";
        }

        return TypedResults.Ok(new MapSessionDto(decision.Provider, tilesetUrl, isPremium, decision.FreeSessionsLeft,
            decision.FallbackReason, settings.SessionMinutes));
    }
}
