using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Reconnect.Api.Tests.Infrastructure;
using Reconnect.Contracts;
using Reconnect.Contracts.Rooms;
using Reconnect.Infrastructure.Persistence.Seed;

namespace Reconnect.Api.Tests.Rooms;

[Collection(ApiTestGroup.Name)]
public sealed class RoomHubTests(ReconnectApiFactory factory)
{
    [Fact]
    public async Task Players_in_the_same_room_see_each_other_move_and_chat()
    {
        var (room, anna, ben) = await RoomWithTwoUsersAsync();
        await using var annaHub = await RoomHubClient.ConnectAsync(factory, anna);
        await using var benHub = await RoomHubClient.ConnectAsync(factory, ben);

        var annaSnapshot = await annaHub.JoinAsync(room.Id);
        Assert.Equal(room.Name, annaSnapshot.Room.Name);
        Assert.Equal(RoomGrid.Width, annaSnapshot.Width);
        Assert.Single(annaSnapshot.Players);

        var benSnapshot = await benHub.JoinAsync(room.Id);
        Assert.Equal(2, benSnapshot.Players.Count);
        Assert.Contains(benSnapshot.Players, p => p.UserId == anna.Id);
        await RoomHubClient.Eventually(() => annaHub.Joined.Any(p => p.UserId == ben.Id), "Anna is told that Ben joined");

        var tile = await benHub.MoveToAsync(3, 7);
        Assert.Equal(new TilePosition(3, 7), tile);
        await RoomHubClient.Eventually(() => annaHub.Moved.Any(m => m.UserId == ben.Id && m.Tile == tile), "Anna sees Ben walk");

        await benHub.SayAsync("Hoi Anna!");
        await RoomHubClient.Eventually(() => annaHub.Chat.Any(m => m.Text == "Hoi Anna!" && m.DisplayName == "Ben"), "Anna hears Ben");
        await RoomHubClient.Eventually(() => benHub.Chat.Any(m => m.Text == "Hoi Anna!"), "Ben sees his own bubble");

        await benHub.LeaveAsync();
        await RoomHubClient.Eventually(() => annaHub.Left.Contains(ben.Id), "Anna is told that Ben left");
    }

    [Fact]
    public async Task Moves_are_clamped_to_the_room_grid()
    {
        var (room, anna, _) = await RoomWithTwoUsersAsync();
        await using var hub = await RoomHubClient.ConnectAsync(factory, anna);
        await hub.JoinAsync(room.Id);

        var tile = await hub.MoveToAsync(99, -5);

        Assert.Equal(new TilePosition(RoomGrid.Width - 1, 0), tile);
    }

    [Fact]
    public async Task Disconnecting_removes_the_player_from_the_room()
    {
        var (room, anna, ben) = await RoomWithTwoUsersAsync();
        await using var annaHub = await RoomHubClient.ConnectAsync(factory, anna);
        await annaHub.JoinAsync(room.Id);

        var benHub = await RoomHubClient.ConnectAsync(factory, ben);
        await benHub.JoinAsync(room.Id);
        await benHub.DisposeAsync();   // app closed / connection lost

        await RoomHubClient.Eventually(() => annaHub.Left.Contains(ben.Id), "Anna is told that Ben is gone");
        await using var carlHub = await RoomHubClient.ConnectAsync(factory, await factory.RegisterAsync("Carl"));
        var snapshot = await carlHub.JoinAsync(room.Id);
        Assert.DoesNotContain(snapshot.Players, p => p.UserId == ben.Id);
    }

    [Fact]
    public async Task Blocked_users_neither_see_nor_hear_each_other_in_a_room()
    {
        // Room of a third person: a blocked user couldn't even see a room owned by the blocker.
        var (room, _, _) = await RoomWithTwoUsersAsync();
        var anna = await factory.RegisterAsync("Anna");
        var ben = await factory.RegisterAsync("Ben");
        await anna.Client.PostAsync(ApiRoutes.Blocks.ForUser(ben.Id), null);
        await using var annaHub = await RoomHubClient.ConnectAsync(factory, anna);
        await using var benHub = await RoomHubClient.ConnectAsync(factory, ben);
        await annaHub.JoinAsync(room.Id);

        var benSnapshot = await benHub.JoinAsync(room.Id);
        await benHub.SayAsync("Hallo?");
        await benHub.MoveToAsync(1, 1);
        await annaHub.SayAsync("Ich sehe dich nicht");
        await Task.Delay(500);   // give the server time to (not) deliver

        Assert.DoesNotContain(benSnapshot.Players, p => p.UserId == anna.Id);
        Assert.DoesNotContain(annaHub.Joined, p => p.UserId == ben.Id);
        Assert.DoesNotContain(annaHub.Chat, m => m.UserId == ben.Id);
        Assert.DoesNotContain(annaHub.Moved, m => m.UserId == ben.Id);
        Assert.DoesNotContain(benHub.Chat, m => m.UserId == anna.Id);
    }

    [Fact]
    public async Task Room_of_a_user_who_blocked_me_cannot_be_joined()
    {
        var (room, owner, visitor) = await RoomWithTwoUsersAsync();
        await owner.Client.PostAsync(ApiRoutes.Blocks.ForUser(visitor.Id), null);
        await using var hub = await RoomHubClient.ConnectAsync(factory, visitor);

        await Assert.ThrowsAsync<HubException>(() => hub.JoinAsync(room.Id));
    }

    [Fact]
    public async Task Private_room_of_someone_else_cannot_be_joined()
    {
        var owner = await factory.RegisterAsync();
        var visitor = await factory.RegisterAsync();
        var room = await (await owner.Client.PostAsJsonAsync(ApiRoutes.Rooms.Group,
            new CreateRoomRequest(ZurichBuildings.KunsthausId, "Privat", IsPublic: false), TestUsers.Json)).ReadAsync<RoomDto>();
        await using var hub = await RoomHubClient.ConnectAsync(factory, visitor);

        var error = await Assert.ThrowsAsync<HubException>(() => hub.JoinAsync(room.Id));

        Assert.Contains("Room not found", error.Message);
    }

    [Fact]
    public async Task Chat_messages_are_validated()
    {
        var (room, anna, _) = await RoomWithTwoUsersAsync();
        await using var hub = await RoomHubClient.ConnectAsync(factory, anna);
        await hub.JoinAsync(room.Id);

        await Assert.ThrowsAsync<HubException>(() => hub.SayAsync("   "));
        await Assert.ThrowsAsync<HubException>(() => hub.SayAsync(new string('x', RoomGrid.MaxChatLength + 1)));
    }

    private async Task<(RoomDto Room, TestUser Anna, TestUser Ben)> RoomWithTwoUsersAsync()
    {
        var anna = await factory.RegisterAsync("Anna");
        var ben = await factory.RegisterAsync("Ben");
        var room = await (await anna.Client.PostAsJsonAsync(ApiRoutes.Rooms.Group,
            new CreateRoomRequest(ZurichBuildings.OpernhausId, "Foyer", IsPublic: true), TestUsers.Json)).ReadAsync<RoomDto>();
        return (room, anna, ben);
    }
}
