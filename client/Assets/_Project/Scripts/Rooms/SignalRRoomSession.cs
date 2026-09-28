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
        public event Action<EmoteDto> EmoteReceived;
        public event Action<PlayerSeatDto> PlayerSeated;
        public event Action<TicTacToeStateDto> TicTacToeUpdated;
        public event Action<BoardGameStateDto> BoardGameUpdated;
        public event Action<QuizStateDto> QuizUpdated;
        public event Action<QueueStatusDto> QueueUpdated;
        public event Action<RoomSnapshotDto> ElevatorArrived;
        public event Action<RoomLayoutChangedDto> LayoutChanged;
        public event Action<PlayerLookDto> LookChanged;
        public event Action<string> Disconnected;

        public async Task<RoomSnapshotDto> JoinAsync(Guid roomId, CancellationToken ct)
        {
            await EnsureConnectedAsync(ct);
            return await _connection.InvokeAsync<RoomSnapshotDto>(RoomHubContract.Server.JoinRoom, roomId);
        }

        public Task RefreshLookAsync() => _connection.InvokeAsync(RoomHubContract.Server.RefreshLook);

        public Task<TilePosition> MoveToAsync(TilePosition tile) =>
            _connection.InvokeAsync<TilePosition>(RoomHubContract.Server.MoveTo, tile.X, tile.Z);

        public Task SayAsync(string text) => _connection.InvokeAsync(RoomHubContract.Server.Say, text);

        public Task EmoteAsync(string emote) => _connection.InvokeAsync(RoomHubContract.Server.Emote, emote);

        public Task<bool> SitAsync(SeatDto seat) => _connection.InvokeAsync<bool>(RoomHubContract.Server.Sit, seat.Item, seat.Place);

        public Task StandUpAsync() => _connection.InvokeAsync(RoomHubContract.Server.StandUp);

        public Task<TicTacToeStateDto> TicTacToeJoinAsync() => _connection.InvokeAsync<TicTacToeStateDto>(RoomHubContract.Server.TicTacToeJoin);

        public Task<TicTacToeStateDto> TicTacToeMoveAsync(int cell) => _connection.InvokeAsync<TicTacToeStateDto>(RoomHubContract.Server.TicTacToeMove, cell);

        public Task<TicTacToeStateDto> TicTacToeResetAsync() => _connection.InvokeAsync<TicTacToeStateDto>(RoomHubContract.Server.TicTacToeReset);

        public Task<BoardGameStateDto> BoardGameJoinAsync(string game) =>
            _connection.InvokeAsync<BoardGameStateDto>(RoomHubContract.Server.BoardGameJoin, game);

        public Task<BoardGameStateDto> BoardGameMoveAsync(string game, string move) =>
            _connection.InvokeAsync<BoardGameStateDto>(RoomHubContract.Server.BoardGameMove, game, move);

        public Task<BoardGameStateDto> BoardGameResetAsync(string game) =>
            _connection.InvokeAsync<BoardGameStateDto>(RoomHubContract.Server.BoardGameReset, game);

        public Task<QuizStateDto> QuizStartAsync() => _connection.InvokeAsync<QuizStateDto>(RoomHubContract.Server.QuizStart);

        public Task<QuizStateDto> QuizAnswerAsync(int answerIndex) => _connection.InvokeAsync<QuizStateDto>(RoomHubContract.Server.QuizAnswer, answerIndex);

        public Task<QuizStateDto> QuizNextAsync() => _connection.InvokeAsync<QuizStateDto>(RoomHubContract.Server.QuizNext);

        public Task<ElevatorResultDto> RideElevatorAsync(Guid targetRoomId) =>
            _connection.InvokeAsync<ElevatorResultDto>(RoomHubContract.Server.RideElevator, targetRoomId);

        public async Task LeaveQueueAsync()
        {
            if (_connection?.IsConnected == true)
            {
                await _connection.InvokeAsync(RoomHubContract.Server.LeaveQueue);
            }
        }

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
            connection.On<EmoteDto>(RoomHubContract.Client.PlayerEmote, e => EmoteReceived?.Invoke(e));
            connection.On<PlayerSeatDto>(RoomHubContract.Client.PlayerSeated, s => PlayerSeated?.Invoke(s));
            connection.On<TicTacToeStateDto>(RoomHubContract.Client.TicTacToeUpdated, t => TicTacToeUpdated?.Invoke(t));
            connection.On<BoardGameStateDto>(RoomHubContract.Client.BoardGameUpdated, b => BoardGameUpdated?.Invoke(b));
            connection.On<QuizStateDto>(RoomHubContract.Client.QuizUpdated, q => QuizUpdated?.Invoke(q));
            connection.On<QueueStatusDto>(RoomHubContract.Client.QueueUpdated, q => QueueUpdated?.Invoke(q));
            connection.On<RoomSnapshotDto>(RoomHubContract.Client.ElevatorArrived, s => ElevatorArrived?.Invoke(s));
            connection.On<RoomLayoutChangedDto>(RoomHubContract.Client.RoomLayoutChanged, l => LayoutChanged?.Invoke(l));
            connection.On<PlayerLookDto>(RoomHubContract.Client.PlayerLookChanged, l => LookChanged?.Invoke(l));
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
