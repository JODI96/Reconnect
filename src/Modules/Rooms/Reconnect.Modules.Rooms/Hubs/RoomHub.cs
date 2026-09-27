using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Reconnect.Contracts.Rooms;
using Reconnect.Modules.Profiles.Public;
using Reconnect.Modules.Rooms.Domain.Minigames;
using Reconnect.Modules.Rooms.Features;
using Reconnect.Modules.Rooms.Infrastructure;
using Reconnect.Modules.Safety.Public;
using Reconnect.SharedKernel.Web;

namespace Reconnect.Modules.Rooms.Hubs;

/// <summary>
/// Events pushed to clients in a room. Method names = <c>RoomHubContract.Client</c>.
/// Public because SignalR generates a typed proxy for it.
/// </summary>
public interface IRoomClient
{
    Task PlayerJoined(RoomPlayerDto player);
    Task PlayerLeft(Guid userId);
    Task PlayerMoved(PlayerMovedDto move);
    Task ChatMessage(RoomChatMessageDto message);
    Task PlayerEmote(EmoteDto emote);
    Task PlayerSeated(PlayerSeatDto seat);
    Task TicTacToeUpdated(TicTacToeStateDto state);
    Task QuizUpdated(QuizStateDto state);
    Task BoardGameUpdated(BoardGameStateDto state);
    Task QueueUpdated(QueueStatusDto status);
    Task ElevatorArrived(RoomSnapshotDto snapshot);
    Task RoomLayoutChanged(RoomLayoutChangedDto change);
}

/// <summary>
/// Live presence in a room (Habbo style): join, walk to a tile, speak in bubbles.
/// Clients only send intentions ("walk to 3/5"); the server validates and broadcasts, and every
/// client animates the walk itself. Blocked users never see or hear each other.
/// The client talks to this through IRoomSession, so a Photon implementation can replace it later.
/// Towers: every room has a capacity (checked atomically in Redis). The lift moves people between the floors
/// of a building; a full floor has a first-come-first-served queue and whoever is next rides up automatically
/// as soon as someone leaves.
/// </summary>
[Authorize]
internal sealed class RoomHub(
    RoomReader rooms, IBlockQueries blocks, IProfileDirectory profiles,
    IRoomPresenceStore presence, IRoomGameStore games, IElevatorQueue queue, TimeProvider time)
    : Hub<IRoomClient>
{
    public async Task<RoomSnapshotDto> JoinRoom(Guid roomId)
    {
        var userId = Context.User!.GetUserId();
        var ct = Context.ConnectionAborted;
        await LeaveCurrentRoomAsync();

        var room = await rooms.FindVisibleAsync(userId, roomId, ct)
            ?? throw new HubException("Room not found.");
        if (await queue.PeekAsync(roomId) is { } next && next != userId)
        {
            throw new HubException("Dieser Stock ist voll – stell dich im Lift an.");
        }

        var snapshot = await EnterAsync(room, userId, Context.ConnectionId, byElevator: false, ct)
            ?? throw new HubException("Dieser Raum ist voll.");
        await queue.RemoveAsync(userId);
        return snapshot;
    }

    public Task LeaveRoom() => LeaveCurrentRoomAsync();

    /// <summary>
    /// Takes the lift to another floor of the current building. Arrives at once when there is space and
    /// nobody is waiting; otherwise the caller queues (and stays where they are until it's their turn).
    /// </summary>
    public async Task<ElevatorResultDto> RideElevator(Guid targetRoomId)
    {
        var ct = Context.ConnectionAborted;
        var me = await CurrentEntryAsync();
        var current = await rooms.FindVisibleAsync(me.UserId, me.RoomId, ct) ?? throw new HubException("Room not found.");
        var target = await rooms.FindVisibleAsync(me.UserId, targetRoomId, ct) ?? throw new HubException("Floor not found.");
        if (target.BuildingId != current.BuildingId || target.Floor is null || current.Floor is null)
        {
            throw new HubException("Der Lift fährt nur zu Stockwerken in diesem Gebäude.");
        }
        if (target.Id == current.Id)
        {
            throw new HubException("Du bist schon auf diesem Stock.");
        }

        var next = await queue.PeekAsync(target.Id);
        if (next is null || next == me.UserId)
        {
            var snapshot = await EnterAsync(target, me.UserId, me.ConnectionId, byElevator: true, ct);
            if (snapshot is not null)
            {
                await queue.RemoveAsync(me.UserId);
                await LeftRoomAsync(me, promote: true);
                return new ElevatorResultDto(ElevatorStatus.Arrived, snapshot, null);
            }
        }

        var position = await queue.EnqueueAsync(target.Id, me.UserId, me.ConnectionId, time.GetUtcNow());
        return new ElevatorResultDto(ElevatorStatus.Queued, null, new QueueStatusDto(target.Id, target.Floor.Value, target.Name, position));
    }

    public async Task LeaveQueue()
    {
        if (await queue.RemoveAsync(Context.User!.GetUserId()) is { } roomId)
        {
            await NotifyQueueAsync(roomId);
        }
    }

    public async Task<TilePosition> MoveTo(int x, int z)
    {
        var me = await CurrentEntryAsync();
        x = Math.Clamp(x, 0, me.Width - 1);
        z = Math.Clamp(z, 0, me.Depth - 1);
        await presence.UpdateTileAsync(me, x, z);   // walking also stands up

        var tile = new TilePosition(x, z);
        await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false)).PlayerMoved(new PlayerMovedDto(me.UserId, tile));
        return tile;
    }

    /// <summary>
    /// Sits down on place <paramref name="place"/> of layout item <paramref name="item"/> – if that item is a seat
    /// (<see cref="RoomSeats"/>) and nobody else sits there. Returns false when the place is taken.
    /// </summary>
    public async Task<bool> Sit(int item, int place)
    {
        var me = await CurrentEntryAsync();
        var room = await rooms.FindVisibleAsync(me.UserId, me.RoomId, Context.ConnectionAborted)
            ?? throw new HubException("Room not found.");
        if (item < 0 || item >= room.Layout.Count || place < 0 || place >= RoomSeats.PlacesFor(room.Layout[item].ItemId))
        {
            throw new HubException("Hier kann man nicht sitzen.");
        }

        var seat = new SeatDto(item, place);
        var players = await presence.GetPlayersAsync(me.RoomId);
        if (players.Any(p => p.UserId != me.UserId && p.Seat == seat))
        {
            return false;
        }
        await presence.UpdateSeatAsync(me, seat);
        await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false)).PlayerSeated(new PlayerSeatDto(me.UserId, seat));
        return true;
    }

    public async Task StandUp()
    {
        var me = await CurrentEntryAsync();
        if (me.Seat is null)
        {
            return;
        }
        await presence.UpdateSeatAsync(me, null);
        await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false)).PlayerSeated(new PlayerSeatDto(me.UserId, null));
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

    public Task<BoardGameStateDto> BoardGameJoin(string game) =>
        UpdateBoardGameAsync(game, (board, me) => board.Join(me.UserId, me.DisplayName));

    public Task<BoardGameStateDto> BoardGameMove(string game, string move) =>
        UpdateBoardGameAsync(game, (board, me) => board.Move(me.UserId, move));

    public Task<BoardGameStateDto> BoardGameReset(string game) => UpdateBoardGameAsync(game, (board, _) => board.Reset());

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Whoever goes offline gives up their place in the lift queue.
        var userId = Context.User!.GetUserId();
        if (await queue.GetForUserAsync(userId) is { } waiting && waiting.ConnectionId == Context.ConnectionId
            && await queue.RemoveAsync(userId) is { } roomId)
        {
            await NotifyQueueAsync(roomId);
        }
        await LeaveCurrentRoomAsync();
        await base.OnDisconnectedAsync(exception);
    }

    private async Task LeaveCurrentRoomAsync()
    {
        var me = await presence.RemoveByConnectionAsync(Context.ConnectionId);
        if (me is not null)
        {
            await LeftRoomAsync(me, promote: false);
            await PromoteAsync(me.RoomId);
        }
    }

    /// <summary>
    /// Adds the user to the room if it has space (atomic capacity check) and tells the others.
    /// Returns the snapshot for the user, or null when the room is full.
    /// </summary>
    private async Task<RoomSnapshotDto?> EnterAsync(RoomDto room, Guid userId, string connectionId, bool byElevator, CancellationToken ct)
    {
        var hidden = await HiddenUsersAsync(userId, ct);
        var others = (await presence.GetPlayersAsync(room.Id)).Where(p => p.UserId != userId).ToList();
        var displayName = await profiles.GetDisplayNameAsync(userId, ct) ?? throw new HubException("Profile not found.");
        var blocked = RoomLayout.BlockedTiles(room.Layout);
        blocked.UnionWith(RoomLayout.OutsideTiles(RoomZones.ContextFor(room.Theme, room.Width, room.Depth, room.Layout, room.Outline)));
        var (x, z) = FindFreeTile(others, blocked, room.Width, room.Depth, byElevator || room.Floor != null ? ElevatorLanding(room) : null);
        var me = new PresenceEntry(room.Id, userId, connectionId, displayName, x, z, room.Width, room.Depth);

        var (added, replaced) = await presence.TryAddAsync(me, room.Capacity);
        if (!added)
        {
            return null;
        }
        if (replaced is not null)
        {
            // Same user joined from another connection: that one is no longer in the room.
            await Clients.Client(replaced.ConnectionId).PlayerLeft(userId);
        }

        var visibleOthers = others.Where(p => !hidden.Contains(p.UserId)).ToList();
        await Clients.Clients(visibleOthers.Select(p => p.ConnectionId).ToList()).PlayerJoined(ToDto(me));

        var ticTacToe = await games.GetAsync<TicTacToeGame>(room.Id, TicTacToeId);
        var quiz = await games.GetAsync<QuizGame>(room.Id, QuizId);
        var boardGames = new List<BoardGameStateDto>
        {
            ((await games.GetAsync<ConnectFourGame>(room.Id, BoardGames.ConnectFour)) ?? new ConnectFourGame()).ToDto(),
            ((await games.GetAsync<MemoryGame>(room.Id, BoardGames.Memory)) ?? new MemoryGame()).ToDto(),
            ((await games.GetAsync<ChessGame>(room.Id, BoardGames.Chess)) ?? new ChessGame()).ToDto(),
        };
        return new RoomSnapshotDto(room, room.Width, room.Depth,
            visibleOthers.Select(ToDto).Append(ToDto(me)).ToList(),
            (ticTacToe ?? new TicTacToeGame()).ToDto(), (quiz ?? new QuizGame()).ToDto(), boardGames);
    }

    /// <summary>Everything that happens in a room someone left (on foot, by lift or by going offline).</summary>
    /// <param name="promote">Also let the next person from the lift queue in (the caller didn't already).</param>
    private async Task LeftRoomAsync(PresenceEntry me, bool promote)
    {
        if (promote)
        {
            await presence.RemoveFromRoomAsync(me.RoomId, me.UserId);
        }

        // Leaving the room gives up the seat at the tic-tac-toe table.
        var freed = false;
        var table = await games.UpdateAsync<TicTacToeGame>(me.RoomId, TicTacToeId, game => freed = game.Leave(me.UserId));
        if (freed)
        {
            await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false, CancellationToken.None)).TicTacToeUpdated(table.ToDto());
        }
        // … and at the games for two.
        foreach (var game in BoardGames.All)
        {
            if (await LeaveBoardGameAsync(me, game) is { } state)
            {
                await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false, CancellationToken.None)).BoardGameUpdated(state);
            }
        }

        // Don't use ConnectionAborted here: it is already cancelled when the client disconnected.
        await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false, CancellationToken.None)).PlayerLeft(me.UserId);

        if (promote)
        {
            await PromoteAsync(me.RoomId);
        }
    }

    /// <summary>
    /// A place became free on a floor: the lift brings the next people from its queue up (as many as fit).
    /// Rooms they leave for it may in turn have waiting people – handled iteratively.
    /// </summary>
    private async Task PromoteAsync(Guid roomId)
    {
        var ct = CancellationToken.None;   // may run while the caller disconnects
        var pending = new Queue<Guid>([roomId]);
        var rounds = 0;
        while (pending.TryDequeue(out var freedRoom) && rounds++ < 50)
        {
            while (await queue.PeekAsync(freedRoom) is { } next)
            {
                if (await queue.GetForUserAsync(next) is not { } waiting || waiting.RoomId != freedRoom)
                {
                    await queue.RemoveAsync(next);
                    continue;
                }
                var room = await rooms.FindVisibleAsync(next, freedRoom, ct);
                if (room is null)
                {
                    await queue.RemoveAsync(next);   // e.g. blocked by the owner meanwhile
                    continue;
                }

                var before = await presence.GetByConnectionAsync(waiting.ConnectionId);
                var snapshot = await EnterAsync(room, next, waiting.ConnectionId, byElevator: true, ct);
                if (snapshot is null)
                {
                    break;   // still full (someone else took the place)
                }
                await queue.RemoveAsync(next);
                if (before is not null && before.RoomId != freedRoom)
                {
                    await LeftRoomAsync(before, promote: false);
                    await presence.RemoveFromRoomAsync(before.RoomId, before.UserId);
                    pending.Enqueue(before.RoomId);
                }
                await Clients.Client(waiting.ConnectionId).ElevatorArrived(snapshot);
            }
            await NotifyQueueAsync(freedRoom);
        }
    }

    /// <summary>Tells everyone waiting for a floor their current place.</summary>
    private async Task NotifyQueueAsync(Guid roomId)
    {
        var members = await queue.MembersAsync(roomId);
        if (members.Count == 0)
        {
            return;
        }
        var room = await rooms.FindVisibleAsync(members[0], roomId, CancellationToken.None);
        for (var i = 0; i < members.Count; i++)
        {
            if (await queue.GetForUserAsync(members[i]) is { } waiting)
            {
                await Clients.Client(waiting.ConnectionId)
                    .QueueUpdated(new QueueStatusDto(roomId, room?.Floor ?? 0, room?.Name ?? "", i + 1));
            }
        }
    }

    /// <summary>Tile in front of the lift bank (where the doors open), if the room has one.</summary>
    private static (int X, int Z)? ElevatorLanding(RoomDto room)
    {
        var lift = room.Layout.FirstOrDefault(i => RoomZones.IsLift(i.ItemId));
        if (lift is null)
        {
            return null;
        }
        var (x, z, _, _) = RoomZones.Landings(lift).First();
        return (x, z);
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

    private async Task<BoardGameStateDto> UpdateBoardGameAsync(string game, Action<TwoPlayerGame, PresenceEntry> action)
    {
        var me = await CurrentEntryAsync();
        var state = game switch
        {
            BoardGames.ConnectFour => (await UpdateGameAsync<ConnectFourGame>(me, game, g => action(g, me))).ToDto(),
            BoardGames.Memory => (await UpdateGameAsync<MemoryGame>(me, game, g => action(g, me))).ToDto(),
            BoardGames.Chess => (await UpdateGameAsync<ChessGame>(me, game, g => action(g, me))).ToDto(),
            _ => throw new HubException("Unknown game."),
        };
        await Clients.Clients(await VisibleConnectionsAsync(me, includeSelf: false)).BoardGameUpdated(state);
        return state;
    }

    /// <summary>Frees the seat of someone who left; the new state if they were playing, else null.</summary>
    private async Task<BoardGameStateDto?> LeaveBoardGameAsync(PresenceEntry me, string game)
    {
        var freed = false;
        BoardGameStateDto state = game switch
        {
            BoardGames.ConnectFour => (await games.UpdateAsync<ConnectFourGame>(me.RoomId, game, g => freed = g.Leave(me.UserId))).ToDto(),
            BoardGames.Memory => (await games.UpdateAsync<MemoryGame>(me.RoomId, game, g => freed = g.Leave(me.UserId))).ToDto(),
            _ => (await games.UpdateAsync<ChessGame>(me.RoomId, game, g => freed = g.Leave(me.UserId))).ToDto(),
        };
        return freed ? state : null;
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
        (await blocks.HiddenUserIdsAsync(userId, ct)).ToHashSet();

    /// <summary>
    /// First free tile, spiralling outwards from <paramref name="start"/> – by default the front middle of the
    /// room (where you "come in"), for lift arrivals the landing in front of the lift.
    /// </summary>
    /// <summary>Nearest tile to <paramref name="start"/> that has neither a person nor furniture on it.</summary>
    private static (int X, int Z) FindFreeTile(IReadOnlyCollection<PresenceEntry> occupied, HashSet<(int X, int Z)> blocked,
        int width, int depth, (int X, int Z)? start = null)
    {
        var taken = occupied.Select(p => (p.X, p.Z)).ToHashSet();
        taken.UnionWith(blocked);
        var (cx, cz) = start is { } s
            ? (Math.Clamp(s.X, 0, width - 1), Math.Clamp(s.Z, 0, depth - 1))
            : (width / 2, Math.Min(2, depth - 1));
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
        new(entry.UserId, entry.DisplayName, new TilePosition(entry.X, entry.Z), entry.Seat);
}
