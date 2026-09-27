using System;
using System.Threading;
using System.Threading.Tasks;
using Reconnect.Contracts.Rooms;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Live presence in a room: join, walk, talk, and hear about the others.
    /// Transport-agnostic on purpose – today <see cref="SignalRRoomSession"/> (our own backend),
    /// later possibly Photon Fusion. The room scene and UI only know this interface.
    /// Events are raised on the Unity main thread.
    /// </summary>
    public interface IRoomSession : IDisposable
    {
        event Action<RoomPlayerDto> PlayerJoined;
        event Action<Guid> PlayerLeft;
        event Action<PlayerMovedDto> PlayerMoved;
        event Action<RoomChatMessageDto> ChatReceived;
        event Action<EmoteDto> EmoteReceived;

        /// <summary>Someone sat down (Seat) or stood up (Seat null). A move stands up as well.</summary>
        event Action<PlayerSeatDto> PlayerSeated;
        event Action<TicTacToeStateDto> TicTacToeUpdated;

        /// <summary>A game for two changed (Connect Four, Memory, chess).</summary>
        event Action<BoardGameStateDto> BoardGameUpdated;
        event Action<QuizStateDto> QuizUpdated;

        /// <summary>My place in the lift queue changed (Position 0 = no longer waiting).</summary>
        event Action<QueueStatusDto> QueueUpdated;

        /// <summary>It was my turn: the lift brought me to the floor I waited for (I left the previous room).</summary>
        event Action<RoomSnapshotDto> ElevatorArrived;

        /// <summary>Connection lost (argument: reason). The room is left implicitly.</summary>
        /// <summary>The owner or an admin saved a new layout (everyone stands up).</summary>
        event Action<RoomLayoutChangedDto> LayoutChanged;

        event Action<string> Disconnected;

        /// <summary>Enters the room (leaving the previous one). Returns everyone present, including me.</summary>
        Task<RoomSnapshotDto> JoinAsync(Guid roomId, CancellationToken ct);

        /// <summary>Walks to a tile. Returns the tile the server accepted (clamped to the grid).</summary>
        Task<TilePosition> MoveToAsync(TilePosition tile);

        Task SayAsync(string text);

        /// <summary>One of <c>Emotes.All</c>; shown to the others (play it locally yourself).</summary>
        Task EmoteAsync(string emote);

        /// <summary>Sits down on a seat of the layout; false when someone else sits there.</summary>
        Task<bool> SitAsync(SeatDto seat);

        Task StandUpAsync();

        Task<TicTacToeStateDto> TicTacToeJoinAsync();
        Task<TicTacToeStateDto> TicTacToeMoveAsync(int cell);
        Task<TicTacToeStateDto> TicTacToeResetAsync();

        Task<BoardGameStateDto> BoardGameJoinAsync(string game);
        Task<BoardGameStateDto> BoardGameMoveAsync(string game, string move);
        Task<BoardGameStateDto> BoardGameResetAsync(string game);

        Task<QuizStateDto> QuizStartAsync();
        Task<QuizStateDto> QuizAnswerAsync(int answerIndex);
        Task<QuizStateDto> QuizNextAsync();

        /// <summary>Takes the lift to another floor of the building: arrives (snapshot) or queues when it's full.</summary>
        Task<ElevatorResultDto> RideElevatorAsync(Guid targetRoomId);

        Task LeaveQueueAsync();

        Task LeaveAsync();
    }
}
