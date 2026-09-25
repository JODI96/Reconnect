using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
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
    /// Two real players meet in a room over the running local backend (SignalR RoomHub).
    /// Skipped automatically if the backend is not running.
    /// </summary>
    [Category("Integration")]
    public sealed class RoomSessionIntegrationTests
    {
        private const string BaseUrl = "http://localhost:5191";
        private static readonly Guid KunsthausId = Guid.Parse("0199a000-0000-7000-8000-000000000007");

        [UnityTest]
        public IEnumerator Two_players_see_each_other_walk_and_talk()
        {
            var anna = Player();
            var ben = Player();
            var registerAnna = Register(anna, "Anna");
            yield return Wait(registerAnna);
            if (registerAnna.Result.StatusCode == 0)
            {
                Assert.Ignore("Backend not running on " + BaseUrl + ".");
            }
            yield return Wait(Register(ben, "Ben"));

            var room = anna.Api.PostAsync<RoomDto>(ApiRoutes.Rooms.Group, new CreateRoomRequest(KunsthausId, "Atelier", true));
            yield return Wait(room);

            var benJoined = new List<RoomPlayerDto>();
            var benMoved = new List<PlayerMovedDto>();
            var annaHeard = new List<RoomChatMessageDto>();
            anna.Session.PlayerJoined += benJoined.Add;
            anna.Session.PlayerMoved += benMoved.Add;
            anna.Session.ChatReceived += annaHeard.Add;

            var annaJoin = anna.Session.JoinAsync(room.Result.Value.Id, CancellationToken.None);
            yield return Wait(annaJoin);
            var benJoin = ben.Session.JoinAsync(room.Result.Value.Id, CancellationToken.None);
            yield return Wait(benJoin);

            Assert.AreEqual(2, benJoin.Result.Players.Count, "Ben sees Anna and himself");
            yield return WaitUntil(() => benJoined.Count == 1, "Anna is told that Ben joined");

            yield return Wait(ben.Session.MoveToAsync(new TilePosition(2, 8)));
            yield return WaitUntil(() => benMoved.Exists(m => m.Tile.X == 2 && m.Tile.Z == 8), "Anna sees Ben walk");

            yield return Wait(ben.Session.SayAsync("Hoi zäme!"));
            yield return WaitUntil(() => annaHeard.Exists(m => m.Text == "Hoi zäme!" && m.DisplayName == "Ben"), "Anna hears Ben");

            anna.Session.Dispose();
            ben.Session.Dispose();
        }

        private static (ApiClient Api, AuthService Auth, IRoomSession Session) Player()
        {
            var api = new ApiClient(new UnityWebRequestTransport(10), BaseUrl);
            var auth = new AuthService(api, new MemoryStore());
            api.Tokens = auth;
            return (api, auth, new SignalRRoomSession(BaseUrl, auth));
        }

        private static Task<ApiResult<AuthResponse>> Register((ApiClient Api, AuthService Auth, IRoomSession Session) player, string name) =>
            player.Auth.RegisterAsync(new RegisterRequest($"unity-{Guid.NewGuid():N}@test.local", "Passw0rd!Secure", name, new DateTime(1994, 3, 2)));

        private static IEnumerator Wait(Task task)
        {
            var end = Time.realtimeSinceStartup + 15f;
            while (!task.IsCompleted)
            {
                Assert.Less(Time.realtimeSinceStartup, end, "timed out");
                yield return null;
            }
            if (task.IsFaulted)
            {
                throw task.Exception!.GetBaseException();
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string because)
        {
            var end = Time.realtimeSinceStartup + 10f;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, end, because);
                yield return null;
            }
        }

        private sealed class MemoryStore : ITokenStore
        {
            private string _token;
            public string LoadRefreshToken() => _token;
            public void SaveRefreshToken(string refreshToken) => _token = refreshToken;
            public void Clear() => _token = null;
        }
    }
}
