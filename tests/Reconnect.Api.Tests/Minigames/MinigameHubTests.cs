using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Reconnect.Api.Tests.Infrastructure;
using Reconnect.Contracts;
using Reconnect.Contracts.Rooms;
using Reconnect.Infrastructure.Persistence.Seed;

namespace Reconnect.Api.Tests.Minigames;

[Collection(ApiTestGroup.Name)]
public sealed class MinigameHubTests(ReconnectApiFactory factory)
{
    [Fact]
    public async Task Two_players_play_tic_tac_toe_and_the_room_sees_the_result()
    {
        var (roomId, anna, ben) = await TwoPlayersInRoomAsync();

        await anna.TicTacToeJoinAsync();
        var started = await ben.TicTacToeJoinAsync();
        Assert.Equal(GameStatus.Playing, started.Status);
        Assert.Equal(anna.User.Id, started.Turn);

        await anna.TicTacToeMoveAsync(0);
        await Assert.ThrowsAsync<HubException>(() => anna.TicTacToeMoveAsync(1));   // not her turn
        await ben.TicTacToeMoveAsync(3);
        await anna.TicTacToeMoveAsync(1);
        await ben.TicTacToeMoveAsync(4);
        var final = await anna.TicTacToeMoveAsync(2);

        Assert.Equal(GameStatus.Won, final.Status);
        Assert.Equal(anna.User.Id, final.Winner);
        await RoomHubClient.Eventually(() => ben.TicTacToe.Any(s => s.Status == GameStatus.Won), "Ben sees that Anna won");

        // Someone entering later sees the board in the snapshot.
        await using var carl = await RoomHubClient.ConnectAsync(factory, await factory.RegisterAsync("Carl"));
        var snapshot = await carl.JoinAsync(roomId);
        Assert.Equal("XXXOO....", snapshot.TicTacToe!.Board);
    }

    [Fact]
    public async Task Quiz_round_with_answers_reveal_and_scores()
    {
        var (_, anna, ben) = await TwoPlayersInRoomAsync();

        var question = await anna.QuizStartAsync();
        Assert.Equal(GameStatus.Question, question.Status);
        Assert.Equal(4, question.Answers.Count);
        Assert.Null(question.CorrectIndex);
        await RoomHubClient.Eventually(() => ben.Quiz.Any(q => q.Status == GameStatus.Question), "Ben gets the question");

        await anna.QuizAnswerAsync(0);
        await ben.QuizAnswerAsync(1);
        var revealed = await ben.QuizNextAsync();

        Assert.Equal(GameStatus.Reveal, revealed.Status);
        Assert.NotNull(revealed.CorrectIndex);
        Assert.Equal(2, revealed.Answered.Count);
        var winnerPoints = revealed.CorrectIndex is 0 or 1 ? 2 : 0;
        Assert.Equal(winnerPoints, revealed.Scores.Max(s => s.Points));
    }

    [Fact]
    public async Task Emotes_are_shown_to_others_and_validated()
    {
        var (_, anna, ben) = await TwoPlayersInRoomAsync();

        await anna.EmoteAsync(Emotes.Wave);
        await Assert.ThrowsAsync<HubException>(() => anna.EmoteAsync("moonwalk"));

        await RoomHubClient.Eventually(() => ben.EmotesSeen.Any(e => e.UserId == anna.User.Id && e.Emote == Emotes.Wave), "Ben sees Anna wave");
    }

    [Fact]
    public async Task Leaving_the_room_frees_the_tic_tac_toe_seat()
    {
        var (_, anna, ben) = await TwoPlayersInRoomAsync();
        await anna.TicTacToeJoinAsync();
        await ben.TicTacToeJoinAsync();

        await ben.LeaveAsync();

        await RoomHubClient.Eventually(() => anna.TicTacToe.Any(s => s.Status == GameStatus.Waiting && s.PlayerX == null), "the table is free again");
    }

    private async Task<(Guid RoomId, RoomHubClient Anna, RoomHubClient Ben)> TwoPlayersInRoomAsync()
    {
        var annaUser = await factory.RegisterAsync("Anna");
        var benUser = await factory.RegisterAsync("Ben");
        var room = await (await annaUser.Client.PostAsJsonAsync(ApiRoutes.Rooms.Group,
            new CreateRoomRequest(ZurichBuildings.EthHauptgebaeudeId, "Spielzimmer", IsPublic: true, "library"), TestUsers.Json)).ReadAsync<RoomDto>();

        var anna = await RoomHubClient.ConnectAsync(factory, annaUser);
        var ben = await RoomHubClient.ConnectAsync(factory, benUser);
        await anna.JoinAsync(room.Id);
        await ben.JoinAsync(room.Id);
        return (room.Id, anna, ben);
    }
}
