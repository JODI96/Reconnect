using System.Net;
using System.Net.Http.Json;
using Reconnect.Api.Tests.Infrastructure;
using Reconnect.Contracts;
using Reconnect.Contracts.Auth;

namespace Reconnect.Api.Tests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class DevAdminTests(ReconnectApiFactory factory)
{
    /// <summary>The dev admin must only exist in Development – even if the flag is switched on elsewhere.</summary>
    [Fact]
    public async Task Dev_admin_does_not_exist_outside_development()
    {
        var client = factory.WithWebHostBuilder(b => b.UseSetting("DevAdmin:Enabled", "true")).CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Login, new LoginRequest("Admin", "Admin"), TestUsers.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_accepts_user_name_as_well_as_email()
    {
        var user = await factory.RegisterAsync();   // registered users: user name = email

        var response = await factory.CreateClient().PostAsJsonAsync(ApiRoutes.Auth.Login,
            new LoginRequest(user.Email.ToUpperInvariant(), user.Password), TestUsers.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
