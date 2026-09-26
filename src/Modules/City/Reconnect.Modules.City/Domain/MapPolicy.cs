using Reconnect.Contracts.Buildings;

namespace Reconnect.Modules.City.Domain;

/// <summary>
/// "Maps" configuration. The Google key never lives in appsettings or code: locally it comes from the
/// Aspire parameter "google-maps-api-key" (AppHost user secrets), in production from the secret store.
/// </summary>
internal sealed class MapOptions
{
    public const string SectionName = "Maps";

    public GoogleOptions Google { get; set; } = new();

    /// <summary>Photorealistic sessions per free user and calendar month.</summary>
    public int FreeGoogleSessionsPerMonth { get; set; } = 10;

    /// <summary>Upper limit for all free users together per month (cost cap). Premium is not limited.</summary>
    public int MonthlyFreeGoogleSessionBudget { get; set; } = 900;

    /// <summary>A Google root tileset session is valid for 3 h; the client reuses the answer a bit less long.</summary>
    public int SessionMinutes { get; set; } = 170;

    public sealed class GoogleOptions
    {
        public string? ApiKey { get; set; }
        public string TilesetUrl { get; set; } = "https://tile.googleapis.com/v1/3dtiles/root.json";
    }
}

/// <summary>Decides which map an app session gets. Pure function – the counting happens in the endpoint.</summary>
internal static class MapPolicy
{
    public sealed record Decision(string Provider, int? FreeSessionsLeft, string? FallbackReason);

    /// <param name="userSessionsThisMonth">Google sessions this user already had this month.</param>
    /// <param name="freeSessionsThisMonth">Google sessions of all free users this month.</param>
    public static Decision Decide(MapOptions options, bool isPremium, int userSessionsThisMonth, int freeSessionsThisMonth)
    {
        if (string.IsNullOrWhiteSpace(options.Google.ApiKey))
        {
            return new(MapProviders.Swisstopo, null, MapFallbackReasons.NotConfigured);
        }
        if (isPremium)
        {
            return new(MapProviders.Google, null, null);
        }

        var left = Math.Max(0, options.FreeGoogleSessionsPerMonth - userSessionsThisMonth);
        if (left == 0)
        {
            return new(MapProviders.Swisstopo, 0, MapFallbackReasons.FreeQuotaUsed);
        }
        if (freeSessionsThisMonth >= options.MonthlyFreeGoogleSessionBudget)
        {
            return new(MapProviders.Swisstopo, left, MapFallbackReasons.BudgetExhausted);
        }
        return new(MapProviders.Google, left - 1, null);   // this session uses one
    }
}

/// <summary>Google map sessions per user and month (for the free quota and the cost cap).</summary>
internal sealed class MapUsage
{
    public Guid UserId { get; set; }

    /// <summary>First day of the calendar month (UTC).</summary>
    public DateOnly Month { get; set; }

    public bool IsPremium { get; set; }
    public int GoogleSessions { get; set; }

    public static DateOnly MonthOf(DateOnly day) => new(day.Year, day.Month, 1);
}
