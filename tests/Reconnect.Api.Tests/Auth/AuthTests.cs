using System.Net;
using System.Net.Http.Json;
using Reconnect.Api.Tests.Infrastructure;
using Reconnect.Contracts;
using Reconnect.Contracts.Auth;
using Reconnect.Contracts.Profiles;

namespace Reconnect.Api.Tests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class AuthTests(ReconnectApiFactory factory)
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    public async Task Register_adult_returns_tokens_and_creates_profile()
    {
        var user = await factory.RegisterAsync("Anna");

        Assert.NotEmpty(user.Auth.AccessToken);
        Assert.NotEmpty(user.Auth.RefreshToken);

        var me = await (await user.Client.GetAsync(ApiRoutes.Profiles.Me)).ReadAsync<MyProfileDto>();
        Assert.Equal(user.Id, me.UserId);
        Assert.Equal("Anna", me.DisplayName);
        Assert.Equal(user.Email, me.Email);
    }

    [Fact]
    public async Task Register_under_18_is_rejected()
    {
        var seventeen = DateTime.UtcNow.Date.AddYears(-18).AddDays(1);
        var response = await _anonymous.PostAsJsonAsync(ApiRoutes.Auth.Register,
            TestUsers.NewRegistration(seventeen), TestUsers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_exactly_18_today_is_allowed()
    {
        var eighteenToday = DateTime.UtcNow.Date.AddYears(-18);
        var response = await _anonymous.PostAsJsonAsync(ApiRoutes.Auth.Register,
            TestUsers.NewRegistration(eighteenToday), TestUsers.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_duplicate_email_is_rejected()
    {
        var existing = await factory.RegisterAsync();
        var response = await _anonymous.PostAsJsonAsync(ApiRoutes.Auth.Register,
            TestUsers.NewRegistration() with { Email = existing.Email }, TestUsers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_correct_password_returns_tokens()
    {
        var user = await factory.RegisterAsync();

        var response = await _anonymous.PostAsJsonAsync(ApiRoutes.Auth.Login,
            new LoginRequest(user.Email, user.Password), TestUsers.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(user.Id, (await response.ReadAsync<AuthResponse>()).UserId);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var user = await factory.RegisterAsync();

        var response = await _anonymous.PostAsJsonAsync(ApiRoutes.Auth.Login,
            new LoginRequest(user.Email, "wrong-password-1"), TestUsers.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_old_token_cannot_be_reused()
    {
        var user = await factory.RegisterAsync();

        var first = await _anonymous.PostAsJsonAsync(ApiRoutes.Auth.Refresh,
            new RefreshRequest(user.Auth.RefreshToken), TestUsers.Json);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotEqual(user.Auth.RefreshToken, (await first.ReadAsync<AuthResponse>()).RefreshToken);

        var reuse = await _anonymous.PostAsJsonAsync(ApiRoutes.Auth.Refresh,
            new RefreshRequest(user.Auth.RefreshToken), TestUsers.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_without_token_is_unauthorized()
    {
        var response = await _anonymous.GetAsync(ApiRoutes.Profiles.Me);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
