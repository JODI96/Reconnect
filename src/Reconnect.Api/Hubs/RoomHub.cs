using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Reconnect.Api.Common.Auth;
using Reconnect.Api.Features.Blocks;
using Reconnect.Api.Features.Minigames;
using Reconnect.Api.Features.Presence;
using Reconnect.Api.Features.Rooms;
using Reconnect.Contracts.Rooms;
using Reconnect.Infrastructure.Persistence;

namespace Reconnect.Api.Hubs;

/// <summary>Events pushed to clients in a room. Method names = <c>RoomHubContract.Client</c>.</summary>
public interface IRoomClient
{
    Task PlayerJoined(RoomPlayerDto player);
    Task PlayerLeft(Guid userId);
    Task PlayerMoved(PlayerMovedDto move);
    Task ChatMessage(RoomChatMessageDto message);
    Task PlayerEmote(EmoteDto emote);
    Task TicTacToeUpdated(TicTacToeStateDto state);
    Task QuizUpdated(QuizStateDto state);
}

/// <summary>
/// Live presence in a room (Habbo style): join, walk to a tile, speak in bubbles.
/// Clients only send intentions ("walk to 3/5"); the server validates and broadcasts, and every
/// client animates the walk itself. Blocked users never see or hear each other.
/// The client talks to this through IRoomSession, so a Photon implementation can replace it later.
/// </summary>
[Authorize]
public sealed class RoomHub(ReconnectDbContext db, IRoomPresenceStore presence, IRoomGameStore games, TimeProvider time)
    : Hub<IRoomClient>
{
    public async Task<RoomSnapshotDto> JoinRoom(Guid roomId)
    {
        var userId = Context.User!.GetUserId();
        var ct = Context.ConnectionAborted;
        await LeaveCurrentRoomAsync();

        var room = await db.QueryRoomDto(db.VisibleRooms(userId).Where(r => r.Id == roomId), ct)
            ?? throw new HubException("Room not found.");

        var hidden = await HiddenUsersAsync(userId, ct);
        var others = (await presence.GetPlayersAsync(roomId)).Where(p => p.UserId != userId).ToList();
        if (others.Count >= RoomGrid.MaxPlayers)
        {
            throw new HubException("Room is full.");
        }

        var displayName = await db.Profiles.Where(p => p.UserId == userId).Select(p => p.DisplayName).SingleAsync(ct);
        var (x, z) = FindFreeTile(others, room.Width, room.Depth);
        var me = new PresenceEntry(roomId, userId, Context.ConnectionId, displayName, x, z, room.Width, room.Depth);

        var replaced = await presence.AddAsync(me);
        if (replaced is not null)
        {
            // Same user joined from another connection: that one is no longer in the room.
            await Clients.Client(replaced.ConnectionId).PlayerLeft(userId);
        }

        var visibleOthers = others.Where(p => !hidden.Contains(p.UserId)).ToList();
        await Clients.Clients(visibleOthers.Select(p => p.ConnectionId).ToList()).PlayerJoined(ToDto(me));

        var ticTacToe = await games.GetAsync<TicTacToeGame>(roomId, TicTacToeId);
        var quiz = await games.GetAsync<QuizGame>(roomId, QuizId);
        return new RoomSnapshotDto(room, room.Width, room.Depth,
            visibleOthers.Select(ToDto).Append(ToDto(me)).ToList(),
            (ticTacToe ?? new TicTacToeGame()).ToDto(), (quiz ?? new QuizGame()).ToDto());
    }

    public Task LeaveRoom() => LeaveCurrentRoomAsync();

    public async Task<TilePosition> MoveTo(int x, int z)
    {
        var me = await CurrentEntryAsync();
        x = Math.Clamp(x, 0, me.Width - 1);
        z = Math.Clamp(z, 0, me.Depth - 1);
        await presence.UpdateTileAsync(me, x, z);

        var tile = new TilePosition(x, z);
        await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false)).PlayerMoved(new PlayerMovedDto(me.UserId, tile));
        return tile;
    }

    public async Task Say(string text)
    {
        text = (text ?? "").Trim();
        if (text.Length is 0 or > RoomGrid.MaxChatLength)
        {
            throw new HubException($"Message must be 1-{RoomGrid.MaxChatLength} characters.");
        }

        var me = await CurrentEntryAsync();
        var message = new RoomChatMessageDto(me.UserId, me.DisplayName, text, time.GetUtcNow());
        await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: true)).ChatMessage(message);
    }

    public async Task Emote(string emote)
    {
        if (!Emotes.All.Contains(emote))
        {
            throw new HubException("Unknown emote.");
        }
        var me = await CurrentEntryAsync();
        await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false)).PlayerEmote(new EmoteDto(me.UserId, emote));
    }

    public Task<TicTacToeStateDto> TicTacToeJoin() => UpdateTicTacToeAsync((game, me) => game.Join(me.UserId, me.DisplayName));

    public Task<TicTacToeStateDto> TicTacToeMove(int cell) => UpdateTicTacToeAsync((game, me) => game.Move(me.UserId, cell));

    public Task<TicTacToeStateDto> TicTacToeReset() => UpdateTicTacToeAsync((game, _) => game.Reset());

    public Task<QuizStateDto> QuizStart() => UpdateQuizAsync((game, _) => game.Start(Random.Shared));

    public Task<QuizStateDto> QuizAnswer(int answerIndex) =>
        UpdateQuizAsync((game, me) => game.Answer(me.UserId, me.DisplayName, answerIndex));

    public Task<QuizStateDto> QuizNext() => UpdateQuizAsync((game, _) => game.Next());

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await LeaveCurrentRoomAsync();
        await base.OnDisconnectedAsync(exception);
    }

    private async Task LeaveCurrentRoomAsync()
    {
        var me = await presence.RemoveByConnectionAsync(Context.ConnectionId);
        if (me is not null)
        {
            // Leaving the room gives up the seat at the tic-tac-toe table.
            var freed = false;
            var table = await games.UpdateAsync<TicTacToeGame>(me.RoomId, TicTacToeId, game => freed = game.Leave(me.UserId));
            if (freed)
            {
                await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false, CancellationToken.None)).TicTacToeUpdated(table.ToDto());
            }

            // Don't use ConnectionAborted here: it is already cancelled when the client disconnected.
            await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false, CancellationToken.None)).PlayerLeft(me.UserId);
        }
    }

    private const string TicTacToeId = "tictactoe";
    private const string QuizId = "quiz";

    private async Task<TicTacToeStateDto> UpdateTicTacToeAsync(Action<TicTacToeGame, PresenceEntry> action)
    {
        var me = await CurrentEntryAsync();
        var state = (await UpdateGameAsync<TicTacToeGame>(me, TicTacToeId, game => action(game, me))).ToDto();
        await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false)).TicTacToeUpdated(state);
        return state;
    }

    private async Task<QuizStateDto> UpdateQuizAsync(Action<QuizGame, PresenceEntry> action)
    {
        var me = await CurrentEntryAsync();
        var state = (await UpdateGameAsync<QuizGame>(me, QuizId, game => action(game, me))).ToDto();
        await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false)).QuizUpdated(state);
        return state;
    }

    private async Task<T> UpdateGameAsync<T>(PresenceEntry me, string gameId, Action<T> action) where T : class, new()
    {
        try
        {
            return await games.UpdateAsync(me.RoomId, gameId, action);
        }
        catch (MinigameException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    private async Task<PresenceEntry> CurrentEntryAsync() =>
        await presence.GetByConnectionAsync(Context.ConnectionId) ?? throw new HubException("Join a room first.");

    /// <summary>Connections in the same room that may see <paramref name="sender"/> (no block in either direction).</summary>
    private async Task<List<string>> VisibleConnectionsAsync(PresenceEntry sender, bool includeSelf, CancellationToken? ct = null)
    {
        var hidden = await HiddenUsersAsync(sender.UserId, ct ?? Context.ConnectionAborted);
        return (await presence.GetPlayersAsync(sender.RoomId))
            .Where(p => (includeSelf || p.UserId != sender.UserId) && !hidden.Contains(p.UserId))
            .Select(p => p.ConnectionId)
            .ToList();
    }

    private async Task<HashSet<Guid>> HiddenUsersAsync(Guid userId, CancellationToken ct) =>
        (await db.HiddenUserIdsFor(userId).ToListAsync(ct)).ToHashSet();

    /// <summary>First free tile, spiralling outwards from the front middle of the room (where you "come in").</summary>
    private static (int X, int Z) FindFreeTile(IReadOnlyCollection<PresenceEntry> occupied, int width, int depth)
    {
        var taken = occupied.Select(p => (p.X, p.Z)).ToHashSet();
        var (cx, cz) = (width / 2, Math.Min(2, depth - 1));
        for (var radius = 0; radius < Math.Max(width, depth); radius++)
        for (var dx = -radius; dx <= radius; dx++)
        for (var dz = -radius; dz <= radius; dz++)
        {
            var (x, z) = (cx + dx, cz + dz);
            if (x >= 0 && x < width && z >= 0 && z < depth && !taken.Contains((x, z)))
            {
                return (x, z);
            }
        }
        return (cx, cz);   // full grid – stand on top of each other
    }

    private static RoomPlayerDto ToDto(PresenceEntry entry) =>
        new(entry.UserId, entry.DisplayName, new TilePosition(entry.X, entry.Z));
}
