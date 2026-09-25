using System;
using System.Threading;
using System.Threading.Tasks;
using Reconnect.Client.Networking;
using Reconnect.Client.Networking.Realtime;
using Reconnect.Contracts.Hubs;
using Reconnect.Contracts.Rooms;

namespace Reconnect.Client.Rooms
{
    /// <summary><see cref="IRoomSession"/> over our backend's SignalR RoomHub. Connects on first join.</summary>
    public sealed class SignalRRoomSession : IRoomSession
    {
        private readonly string _hubUrl;
        private readonly IAccessTokenProvider _tokens;
        private HubConnection _connection;

        public SignalRRoomSession(string apiBaseUrl, IAccessTokenProvider tokens)
        {
            _hubUrl = apiBaseUrl.TrimEnd('/') + RoomHubContract.Path;
            _tokens = tokens;
        }

        public event Action<RoomPlayerDto> PlayerJoined;
        public event Action<Guid> PlayerLeft;
        public event Action<PlayerMovedDto> PlayerMoved;
        public event Action<RoomChatMessageDto> ChatReceived;
        public event Action<string> Disconnected;

        public async Task<RoomSnapshotDto> JoinAsync(Guid roomId, CancellationToken ct)
        {
            await EnsureConnectedAsync(ct);
            return await _connection.InvokeAsync<RoomSnapshotDto>(RoomHubContract.Server.JoinRoom, roomId);
        }

        public Task<TilePosition> MoveToAsync(TilePosition tile) =>
            _connection.InvokeAsync<TilePosition>(RoomHubContract.Server.MoveTo, tile.X, tile.Z);

        public Task SayAsync(string text) => _connection.InvokeAsync(RoomHubContract.Server.Say, text);

        public async Task LeaveAsync()
        {
            if (_connection?.IsConnected == true)
            {
                await _connection.InvokeAsync(RoomHubContract.Server.LeaveRoom);
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
            _connection = null;
        }

        private async Task EnsureConnectedAsync(CancellationToken ct)
        {
            if (_connection?.IsConnected == true)
            {
                return;
            }

            // A fresh access token: the JWT is only checked when the WebSocket opens.
            if (!await _tokens.TryRefreshAsync(ct) && _tokens.AccessToken == null)
            {
                throw new InvalidOperationException("Not logged in.");
            }

            _connection?.Dispose();
            var connection = new HubConnection(_hubUrl, _tokens.AccessToken);
            connection.On<RoomPlayerDto>(RoomHubContract.Client.PlayerJoined, p => PlayerJoined?.Invoke(p));
            connection.On<Guid>(RoomHubContract.Client.PlayerLeft, id => PlayerLeft?.Invoke(id));
            connection.On<PlayerMovedDto>(RoomHubContract.Client.PlayerMoved, m => PlayerMoved?.Invoke(m));
            connection.On<RoomChatMessageDto>(RoomHubContract.Client.ChatMessage, m => ChatReceived?.Invoke(m));
            connection.Closed += reason =>
            {
                if (_connection == connection)
                {
                    Disconnected?.Invoke(reason);
                }
            };

            await connection.ConnectAsync(ct);
            _connection = connection;
        }
    }
}
