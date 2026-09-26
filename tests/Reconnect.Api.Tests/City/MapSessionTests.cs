using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Reconnect.Api.Tests.Infrastructure;
using Reconnect.Contracts;
using Reconnect.Contracts.Buildings;

namespace Reconnect.Api.Tests.City;

[Collection(ApiTestGroup.Name)]
public sealed class MapSessionTests(ReconnectApiFactory factory)
{
    [Fact]
    public async Task Free_users_get_google_until_their_monthly_quota_is_used()
    {
        var user = await factory.RegisterAsync();

        var sessions = new List<MapSessionDto>();
        for (var i = 0; i <= ReconnectApiFactory.FreeGoogleSessions; i++)
        {
            sessions.Add(await (await user.Client.PostAsync(ApiRoutes.Maps.Session, null)).ReadAsync<MapSessionDto>());
        }

        Assert.All(sessions.Take(ReconnectApiFactory.FreeGoogleSessions), s =>
        {
            Assert.Equal(MapProviders.Google, s.Provider);
            Assert.Contains("key=" + ReconnectApiFactory.GoogleTestKey, s.GoogleTilesetUrl);
            Assert.False(s.IsPremium);
        });
        var last = sessions[^1];
        Assert.Equal(MapProviders.Swisstopo, last.Provider);
        Assert.Equal(MapFallbackReasons.FreeQuotaUsed, last.FallbackReason);
        Assert.Null(last.GoogleTilesetUrl);
        Assert.Equal(0, last.FreeGoogleSessionsLeft);
    }

    [Fact]
    public async Task Exhausted_budget_gives_free_users_swisstopo()
    {
        using var noBudget = factory.WithWebHostBuilder(b => b.UseSetting("Maps:MonthlyFreeGoogleSessionBudget", "0"));
        var client = await RegisterOnAsync(noBudget);

        var session = await (await client.PostAsync(ApiRoutes.Maps.Session, null)).ReadAsync<MapSessionDto>();

        Assert.Equal(MapProviders.Swisstopo, session.Provider);
        Assert.Equal(MapFallbackReasons.BudgetExhausted, session.FallbackReason);
    }

    [Fact]
    public async Task Without_a_google_key_everyone_gets_swisstopo()
    {
        using var noKey = factory.WithWebHostBuilder(b => b.UseSetting("Maps:Google:ApiKey", ""));
        var client = await RegisterOnAsync(noKey);

        var session = await (await client.PostAsync(ApiRoutes.Maps.Session, null)).ReadAsync<MapSessionDto>();

        Assert.Equal(MapProviders.Swisstopo, session.Provider);
        Assert.Equal(MapFallbackReasons.NotConfigured, session.FallbackReason);
    }

    [Fact]
    public async Task Map_sessions_require_login()
    {
        var response = await factory.CreateClient().PostAsync(ApiRoutes.Maps.Session, null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpClient> RegisterOnAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        var response = await client.PostAsync(ApiRoutes.Auth.Register,
            System.Net.Http.Json.JsonContent.Create(TestUsers.NewRegistration(), options: TestUsers.Json));
        var auth = await response.ReadAsync<Reconnect.Contracts.Auth.AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
