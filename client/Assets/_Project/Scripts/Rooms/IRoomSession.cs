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

        /// <summary>Connection lost (argument: reason). The room is left implicitly.</summary>
        event Action<string> Disconnected;

        /// <summary>Enters the room (leaving the previous one). Returns everyone present, including me.</summary>
        Task<RoomSnapshotDto> JoinAsync(Guid roomId, CancellationToken ct);

        /// <summary>Walks to a tile. Returns the tile the server accepted (clamped to the grid).</summary>
        Task<TilePosition> MoveToAsync(TilePosition tile);

        Task SayAsync(string text);

        Task LeaveAsync();
    }
}
