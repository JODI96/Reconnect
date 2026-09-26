using Reconnect.Contracts.Buildings;
using Reconnect.Modules.City.Domain;

namespace Reconnect.UnitTests;

public sealed class MapPolicyTests
{
    private static MapOptions Options(string? key = "key", int free = 3, int budget = 100) => new()
    {
        Google = { ApiKey = key },
        FreeGoogleSessionsPerMonth = free,
        MonthlyFreeGoogleSessionBudget = budget,
    };

    [Fact]
    public void Without_a_key_everyone_gets_swisstopo()
    {
        var decision = MapPolicy.Decide(Options(key: null), isPremium: true, 0, 0);

        Assert.Equal(MapProviders.Swisstopo, decision.Provider);
        Assert.Equal(MapFallbackReasons.NotConfigured, decision.FallbackReason);
    }

    [Fact]
    public void Premium_always_gets_google_even_over_quota_and_budget()
    {
        var decision = MapPolicy.Decide(Options(free: 1, budget: 0), isPremium: true, userSessionsThisMonth: 50, freeSessionsThisMonth: 1000);

        Assert.Equal(MapProviders.Google, decision.Provider);
        Assert.Null(decision.FreeSessionsLeft);
    }

    [Theory]
    [InlineData(0, MapProviders.Google, 2)]
    [InlineData(2, MapProviders.Google, 0)]
    [InlineData(3, MapProviders.Swisstopo, 0)]
    public void Free_users_get_a_monthly_quota(int used, string provider, int left)
    {
        var decision = MapPolicy.Decide(Options(free: 3), isPremium: false, used, 0);

        Assert.Equal(provider, decision.Provider);
        Assert.Equal(left, decision.FreeSessionsLeft);
        Assert.Equal(provider == MapProviders.Swisstopo ? MapFallbackReasons.FreeQuotaUsed : null, decision.FallbackReason);
    }

    [Fact]
    public void Exhausted_budget_switches_free_users_to_swisstopo()
    {
        var decision = MapPolicy.Decide(Options(budget: 10), isPremium: false, 0, freeSessionsThisMonth: 10);

        Assert.Equal(MapProviders.Swisstopo, decision.Provider);
        Assert.Equal(MapFallbackReasons.BudgetExhausted, decision.FallbackReason);
        Assert.Equal(3, decision.FreeSessionsLeft);
    }
}
