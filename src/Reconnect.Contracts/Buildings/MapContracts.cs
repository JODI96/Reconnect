namespace Reconnect.Contracts.Buildings
{
    /// <summary>City map sources. The backend decides per user and app session.</summary>
    public static class MapProviders
    {
        /// <summary>Google Photorealistic 3D Tiles – photorealistic, costs per session.</summary>
        public const string Google = "google";

        /// <summary>swisstopo terrain, aerial image and untextured buildings – free.</summary>
        public const string Swisstopo = "swisstopo";
    }

    /// <summary>Why a user gets swisstopo instead of Google (null when Google is granted).</summary>
    public static class MapFallbackReasons
    {
        /// <summary>No Google key configured on the server.</summary>
        public const string NotConfigured = "not-configured";

        /// <summary>The free monthly Google sessions of this user are used up (Premium gets unlimited).</summary>
        public const string FreeQuotaUsed = "free-quota-used";

        /// <summary>The monthly budget for free users is exhausted.</summary>
        public const string BudgetExhausted = "budget-exhausted";
    }

    /// <param name="Provider">See <see cref="MapProviders"/>.</param>
    /// <param name="GoogleTilesetUrl">Root tileset URL including the API key (only for Google).</param>
    /// <param name="FreeGoogleSessionsLeft">Remaining free Google sessions this month (null for Premium).</param>
    /// <param name="FallbackReason">See <see cref="MapFallbackReasons"/> (null for Google).</param>
    /// <param name="ValidForMinutes">Reuse this answer for so long (a Google session lasts up to 3 h).</param>
    public sealed record MapSessionDto(
        string Provider,
        string? GoogleTilesetUrl,
        bool IsPremium,
        int? FreeGoogleSessionsLeft,
        string? FallbackReason,
        int ValidForMinutes);
}
