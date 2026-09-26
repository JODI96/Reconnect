using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Reconnect.Api.Tests.Infrastructure;
using Reconnect.Contracts;
using Reconnect.Contracts.Auth;

namespace Reconnect.Api.Tests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class RateLimitTests(ReconnectApiFactory factory)
{
    [Fact]
    public async Task Too_many_login_attempts_are_rejected_with_429()
    {
        const int limit = 3;
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:AuthPerMinute", limit.ToString()));
        var client = limited.CreateClient();
        var login = new LoginRequest("nobody@test.local", "wrong-password");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i <= limit; i++)
        {
            statuses.Add((await client.PostAsJsonAsync(ApiRoutes.Auth.Login, login, TestUsers.Json)).StatusCode);
        }

        Assert.All(statuses.Take(limit), s => Assert.Equal(HttpStatusCode.Unauthorized, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task Api_routes_are_versioned()
    {
        var client = factory.CreateClient();

        var unversioned = await client.PostAsJsonAsync("/auth/login", new LoginRequest("a@b.c", "x"), TestUsers.Json);
        Assert.Equal(HttpStatusCode.NotFound, unversioned.StatusCode);
        Assert.StartsWith("/v1/", ApiRoutes.Auth.Login, StringComparison.Ordinal);
    }
}
