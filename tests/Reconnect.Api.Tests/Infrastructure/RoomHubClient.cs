using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Reconnect.Contracts.Hubs;
using Reconnect.Contracts.Rooms;

namespace Reconnect.Api.Tests.Infrastructure;

/// <summary>A test user connected to the room hub, recording every event it receives.</summary>
public sealed class RoomHubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;

    private RoomHubClient(HubConnection connection, TestUser user)
    {
        _connection = connection;
        User = user;
        connection.On<RoomPlayerDto>(RoomHubContract.Client.PlayerJoined, p => Joined.Add(p));
        connection.On<Guid>(RoomHubContract.Client.PlayerLeft, id => Left.Add(id));
        connection.On<PlayerMovedDto>(RoomHubContract.Client.PlayerMoved, m => Moved.Add(m));
        connection.On<RoomChatMessageDto>(RoomHubContract.Client.ChatMessage, m => Chat.Add(m));
        connection.On<EmoteDto>(RoomHubContract.Client.PlayerEmote, e => EmotesSeen.Add(e));
        connection.On<PlayerSeatDto>(RoomHubContract.Client.PlayerSeated, s => Seats.Enqueue(s));
        connection.On<TicTacToeStateDto>(RoomHubContract.Client.TicTacToeUpdated, t => TicTacToe.Enqueue(t));
        connection.On<BoardGameStateDto>(RoomHubContract.Client.BoardGameUpdated, b => BoardGames.Enqueue(b));
        connection.On<QuizStateDto>(RoomHubContract.Client.QuizUpdated, q => Quiz.Enqueue(q));
        connection.On<QueueStatusDto>(RoomHubContract.Client.QueueUpdated, q => QueueUpdates.Enqueue(q));
        connection.On<RoomSnapshotDto>(RoomHubContract.Client.ElevatorArrived, s => Arrivals.Enqueue(s));
        connection.On<RoomLayoutChangedDto>(RoomHubContract.Client.RoomLayoutChanged, l => LayoutChanges.Enqueue(l));
        connection.On<PlayerLookDto>(RoomHubContract.Client.PlayerLookChanged, l => Looks.Enqueue(l));
    }

    public TestUser User { get; }
    public ConcurrentBag<RoomPlayerDto> Joined { get; } = [];
    public ConcurrentBag<Guid> Left { get; } = [];
    public ConcurrentBag<PlayerMovedDto> Moved { get; } = [];
    public ConcurrentBag<RoomChatMessageDto> Chat { get; } = [];
    public ConcurrentBag<EmoteDto> EmotesSeen { get; } = [];
    public ConcurrentQueue<PlayerSeatDto> Seats { get; } = new();
    public ConcurrentQueue<RoomLayoutChangedDto> LayoutChanges { get; } = new();

    public ConcurrentQueue<PlayerLookDto> Looks { get; } = new();
    public ConcurrentQueue<TicTacToeStateDto> TicTacToe { get; } = new();
    public ConcurrentQueue<BoardGameStateDto> BoardGames { get; } = new();
    public ConcurrentQueue<QuizStateDto> Quiz { get; } = new();
    public ConcurrentQueue<QueueStatusDto> QueueUpdates { get; } = new();
    public ConcurrentQueue<RoomSnapshotDto> Arrivals { get; } = new();

    public static async Task<RoomHubClient> ConnectAsync(ReconnectApiFactory factory, TestUser user)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, RoomHubContract.Path.TrimStart('/')), options =>
            {
                options.Transports = HttpTransportType.LongPolling;   // TestServer has no real sockets
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(user.Auth.AccessToken);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        await connection.StartAsync();
        return new RoomHubClient(connection, user);
    }

    public Task<RoomSnapshotDto> JoinAsync(Guid roomId) =>
        _connection.InvokeAsync<RoomSnapshotDto>(RoomHubContract.Server.JoinRoom, roomId);

    public Task<TilePosition> MoveToAsync(int x, int z) =>
        _connection.InvokeAsync<TilePosition>(RoomHubContract.Server.MoveTo, x, z);

    public Task SayAsync(string text) => _connection.InvokeAsync(RoomHubContract.Server.Say, text);

    public Task<bool> SitAsync(int item, int place) => _connection.InvokeAsync<bool>(RoomHubContract.Server.Sit, item, place);

    public Task StandUpAsync() => _connection.InvokeAsync(RoomHubContract.Server.StandUp);

    public Task LeaveAsync() => _connection.InvokeAsync(RoomHubContract.Server.LeaveRoom);

    public Task EmoteAsync(string emote) => _connection.InvokeAsync(RoomHubContract.Server.Emote, emote);

    public Task<BoardGameStateDto> BoardGameJoinAsync(string game) => _connection.InvokeAsync<BoardGameStateDto>(RoomHubContract.Server.BoardGameJoin, game);

    public Task<BoardGameStateDto> BoardGameMoveAsync(string game, string move) =>
        _connection.InvokeAsync<BoardGameStateDto>(RoomHubContract.Server.BoardGameMove, game, move);

    public Task<TicTacToeStateDto> TicTacToeJoinAsync() => _connection.InvokeAsync<TicTacToeStateDto>(RoomHubContract.Server.TicTacToeJoin);

    public Task<TicTacToeStateDto> TicTacToeMoveAsync(int cell) => _connection.InvokeAsync<TicTacToeStateDto>(RoomHubContract.Server.TicTacToeMove, cell);

    public Task<QuizStateDto> QuizStartAsync() => _connection.InvokeAsync<QuizStateDto>(RoomHubContract.Server.QuizStart);

    public Task<QuizStateDto> QuizAnswerAsync(int index) => _connection.InvokeAsync<QuizStateDto>(RoomHubContract.Server.QuizAnswer, index);

    public Task<QuizStateDto> QuizNextAsync() => _connection.InvokeAsync<QuizStateDto>(RoomHubContract.Server.QuizNext);

    public Task<ElevatorResultDto> RideElevatorAsync(Guid targetRoomId) =>
        _connection.InvokeAsync<ElevatorResultDto>(RoomHubContract.Server.RideElevator, targetRoomId);

    public Task LeaveQueueAsync() => _connection.InvokeAsync(RoomHubContract.Server.LeaveQueue);

    public Task RefreshLookAsync() => _connection.InvokeAsync(RoomHubContract.Server.RefreshLook);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    /// <summary>Events arrive asynchronously â€“ poll until the condition holds or time runs out.</summary>
    public static async Task Eventually(Func<bool> condition, string because)
    {
        for (var i = 0; i < 50 && !condition(); i++)
        {
            await Task.Delay(100);
        }
        Assert.True(condition(), because);
    }
}
