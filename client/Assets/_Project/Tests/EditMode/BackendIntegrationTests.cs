using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Reconnect.Client.Auth;
using Reconnect.Client.Networking;
using Reconnect.Client.Rooms;
using Reconnect.Contracts;
using Reconnect.Contracts.Auth;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Reconnect.Client.Tests
{
    /// <summary>
    /// End-to-end against a locally running backend (dotnet run --project src/Reconnect.AppHost).
    /// Uses the real UnityWebRequest transport. Skipped automatically if the backend is not reachable.
    /// </summary>
    [Category("Integration")]
    public sealed class BackendIntegrationTests
    {
        private const string BaseUrl = "http://localhost:5191";
        private static readonly Guid PrimeTowerId = Guid.Parse("0199a000-0000-7000-8000-000000000003");

        [UnityTest]
        public IEnumerator Register_create_room_and_see_it_in_room_list()
        {
            var api = new ApiClient(new UnityWebRequestTransport(10), BaseUrl);
            var auth = new AuthService(api, new InMemoryTokenStore());
            api.Tokens = auth;
            var rooms = new RoomService(api);

            var register = auth.RegisterAsync(new RegisterRequest(
                $"unity-{Guid.NewGuid():N}@test.local", "Passw0rd!Secure", "Unity Tester", new DateTime(1995, 5, 17)));
            yield return Wait(register);
            if (register.Result.StatusCode == 0)
            {
                Assert.Ignore("Backend not running on " + BaseUrl + " – start the AppHost to run this test.");
            }
            Assert.IsTrue(register.Result.IsSuccess, register.Result.Error?.ToDisplayString());
            Assert.IsTrue(auth.IsLoggedIn);

            var create = api.PostAsync<RoomDto>(ApiRoutes.Rooms.Group, new CreateRoomRequest(PrimeTowerId, "Unity Lounge", true));
            yield return Wait(create);
            Assert.IsTrue(create.Result.IsSuccess, create.Result.Error?.ToDisplayString());

            var list = rooms.GetRoomsAsync(1);
            yield return Wait(list);
            Assert.IsTrue(list.Result.IsSuccess, list.Result.Error?.ToDisplayString());
            Assert.IsTrue(list.Result.Value.Items.Count > 0);
            Assert.AreEqual(create.Result.Value.Id, list.Result.Value.Items[0].Id, "newest room comes first");

            var detail = rooms.GetRoomAsync(create.Result.Value.Id);
            yield return Wait(detail);
            Assert.AreEqual("Unity Lounge", detail.Result.Value.Name);
            Assert.AreEqual("Unity Tester", detail.Result.Value.OwnerDisplayName);
        }

        [UnityTest]
        public IEnumerator Wrong_password_is_rejected_with_401()
        {
            var api = new ApiClient(new UnityWebRequestTransport(10), BaseUrl);
            var auth = new AuthService(api, new InMemoryTokenStore());
            api.Tokens = auth;

            var login = auth.LoginAsync("nobody@test.local", "wrong-password");
            yield return Wait(login);
            if (login.Result.StatusCode == 0)
            {
                Assert.Ignore("Backend not running on " + BaseUrl + ".");
            }

            Assert.AreEqual(401, login.Result.StatusCode);
            Assert.IsFalse(auth.IsLoggedIn);
        }

        private static IEnumerator Wait(Task task)
        {
            yield return new WaitUntil(() => task.IsCompleted);
            if (task.IsFaulted)
            {
                throw task.Exception!.GetBaseException();
            }
        }

        private sealed class InMemoryTokenStore : ITokenStore
        {
            private string _token;
            public string LoadRefreshToken() => _token;
            public void SaveRefreshToken(string refreshToken) => _token = refreshToken;
            public void Clear() => _token = null;
        }
    }
}
