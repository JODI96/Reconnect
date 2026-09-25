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
    }

    public TestUser User { get; }
    public ConcurrentBag<RoomPlayerDto> Joined { get; } = [];
    public ConcurrentBag<Guid> Left { get; } = [];
    public ConcurrentBag<PlayerMovedDto> Moved { get; } = [];
    public ConcurrentBag<RoomChatMessageDto> Chat { get; } = [];

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

    public Task LeaveAsync() => _connection.InvokeAsync(RoomHubContract.Server.LeaveRoom);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    /// <summary>Events arrive asynchronously – poll until the condition holds or time runs out.</summary>
    public static async Task Eventually(Func<bool> condition, string because)
    {
        for (var i = 0; i < 50 && !condition(); i++)
        {
            await Task.Delay(100);
        }
        Assert.True(condition(), because);
    }
}
