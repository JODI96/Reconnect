using System;
using NUnit.Framework;
using Reconnect.Client.Auth;
using Reconnect.Client.Networking;
using Reconnect.Contracts.Auth;
using Reconnect.Contracts.Profiles;

namespace Reconnect.Client.Tests
{
    public sealed class ApiClientTests
    {
        private const string BaseUrl = "http://api.test";

        [Test]
        public void Login_stores_session_and_sends_bearer_token_afterwards()
        {
            var transport = new FakeTransport(req => req.Url.EndsWith("/auth/login")
                ? Ok(AuthJson("access-1", "refresh-1"))
                : Ok(ProfileJson));
            var (api, auth, store) = Create(transport);

            var login = FakeTransport.Run(auth.LoginAsync("anna@example.com", "pw"));
            var profile = FakeTransport.Run(api.GetAsync<MyProfileDto>("/profiles/me"));

            Assert.IsTrue(login.IsSuccess);
            Assert.IsTrue(auth.IsLoggedIn);
            Assert.AreEqual("refresh-1", store.Token);
            Assert.IsTrue(profile.IsSuccess);
            Assert.AreEqual("access-1", transport.Requests[1].BearerToken);
            Assert.AreEqual(BaseUrl + "/profiles/me", transport.Requests[1].Url);
        }

        [Test]
        public void Expired_access_token_is_refreshed_once_and_request_retried()
        {
            var transport = new FakeTransport(req =>
            {
                if (req.Url.EndsWith("/auth/login")) return Ok(AuthJson("old", "refresh-1"));
                if (req.Url.EndsWith("/auth/refresh")) return Ok(AuthJson("new", "refresh-2"));
                return req.BearerToken == "new" ? Ok(ProfileJson) : new HttpResponse(401, "");
            });
            var (api, auth, store) = Create(transport);
            FakeTransport.Run(auth.LoginAsync("anna@example.com", "pw"));

            var profile = FakeTransport.Run(api.GetAsync<MyProfileDto>("/profiles/me"));

            Assert.IsTrue(profile.IsSuccess);
            Assert.AreEqual("Anna", profile.Value.DisplayName);
            Assert.AreEqual("refresh-2", store.Token);
            Assert.IsNull(transport.Requests.Find(r => r.Url.EndsWith("/auth/refresh")).BearerToken);
        }

        [Test]
        public void Rejected_refresh_token_logs_out()
        {
            var transport = new FakeTransport(req => req.Url.EndsWith("/auth/login")
                ? Ok(AuthJson("old", "refresh-1"))
                : new HttpResponse(401, ""));
            var (api, auth, store) = Create(transport);
            FakeTransport.Run(auth.LoginAsync("anna@example.com", "pw"));

            var profile = FakeTransport.Run(api.GetAsync<MyProfileDto>("/profiles/me"));

            Assert.IsFalse(profile.IsSuccess);
            Assert.AreEqual(401, profile.StatusCode);
            Assert.IsFalse(auth.IsLoggedIn);
            Assert.IsNull(store.Token);
        }

        [Test]
        public void Validation_problem_is_exposed_as_field_errors()
        {
            const string problem = "{\"title\":\"One or more validation errors occurred.\",\"status\":400," +
                                   "\"errors\":{\"BirthDate\":[\"You must be at least 18 years old.\"]}}";
            var (_, auth, _) = Create(new FakeTransport(_ => new HttpResponse(400, problem)));

            var result = FakeTransport.Run(auth.RegisterAsync(
                new RegisterRequest("kid@example.com", "Passw0rd!", "Kid", new DateTime(2012, 1, 1))));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("You must be at least 18 years old.", result.Error.ToDisplayString());
            Assert.IsFalse(auth.IsLoggedIn);
        }

        [Test]
        public void Network_error_gives_readable_message()
        {
            var (api, _, _) = Create(new FakeTransport(_ => new HttpResponse(0, null, "Cannot connect to destination host")));

            var result = FakeTransport.Run(api.GetAsync<MyProfileDto>("/profiles/me"));

            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("Server nicht erreichbar", result.Error.Message);
        }

        private static (ApiClient Api, AuthService Auth, MemoryTokenStore Store) Create(FakeTransport transport)
        {
            var api = new ApiClient(transport, BaseUrl + "/");
            var store = new MemoryTokenStore();
            var auth = new AuthService(api, store);
            api.Tokens = auth;
            return (api, auth, store);
        }

        private static HttpResponse Ok(string body) => new(200, body);

        private static string AuthJson(string access, string refresh) =>
            "{\"userId\":\"01a0d8b8-25e5-7754-a60f-ec33d41caf0b\",\"accessToken\":\"" + access + "\"," +
            "\"accessTokenExpiresAt\":\"2026-09-25T13:15:00+00:00\",\"refreshToken\":\"" + refresh + "\"," +
            "\"refreshTokenExpiresAt\":\"2026-10-25T13:00:00+00:00\"}";

        private const string ProfileJson =
            "{\"userId\":\"01a0d8b8-25e5-7754-a60f-ec33d41caf0b\",\"email\":\"anna@example.com\",\"displayName\":\"Anna\"," +
            "\"birthDate\":\"1995-05-17T00:00:00\",\"isVerified\":false}";

        private sealed class MemoryTokenStore : ITokenStore
        {
            public string Token { get; private set; }
            public string LoadRefreshToken() => Token;
            public void SaveRefreshToken(string refreshToken) => Token = refreshToken;
            public void Clear() => Token = null;
        }
    }
}
