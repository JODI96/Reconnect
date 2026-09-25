using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reconnect.Contracts;
using Reconnect.Contracts.Auth;

namespace Reconnect.Api.Tests.Infrastructure;

/// <summary>A registered user with an HttpClient that sends its access token.</summary>
public sealed record TestUser(Guid Id, string Email, string Password, HttpClient Client, AuthResponse Auth);

public static class TestUsers
{
    public const string DefaultPassword = "Passw0rd!Secure";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@test.local";

    public static RegisterRequest NewRegistration(DateTime? birthDate = null) =>
        new(UniqueEmail(), DefaultPassword, "Test User", birthDate ?? new DateTime(1995, 5, 17));

    public static async Task<TestUser> RegisterAsync(this ReconnectApiFactory factory, string displayName = "Test User")
    {
        var request = NewRegistration() with { DisplayName = displayName };
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Auth.Register, request, Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new TestUser(auth.UserId, request.Email, request.Password, client, auth);
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}
