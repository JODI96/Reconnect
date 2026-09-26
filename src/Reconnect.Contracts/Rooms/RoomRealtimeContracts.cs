using System;
using System.Collections.Generic;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>Room floor as a grid of 1 m tiles (Habbo style). Width/Depth are the defaults – each room has its own size.</summary>
    public static class RoomGrid
    {
        public const int Width = 10;
        public const int Depth = 10;
        /// <summary>Default capacity of a room; tower floors have their own.</summary>
        public const int MaxPlayers = 25;
        public const int MaxChatLength = 200;
    }

    /// <param name="X">Column, 0 … Width-1 (Unity +X).</param>
    /// <param name="Z">Row, 0 … Depth-1 (Unity +Z).</param>
    public sealed record TilePosition(int X, int Z);

    /// <param name="Tile">Where the player stands (or stood before sitting down).</param>
    /// <param name="Seat">The seat the player sits on, if any (see <see cref="RoomSeats"/>).</param>
    public sealed record RoomPlayerDto(Guid UserId, string DisplayName, TilePosition Tile, SeatDto? Seat = null);

    /// <summary>Everything a client needs when entering: the room, who is already there (including the caller) and running games.</summary>
    public sealed record RoomSnapshotDto(
        RoomDto Room,
        int Width,
        int Depth,
        IReadOnlyList<RoomPlayerDto> Players,
        TicTacToeStateDto? TicTacToe = null,
        QuizStateDto? Quiz = null);

    public sealed record PlayerMovedDto(Guid UserId, TilePosition Tile);

    /// <summary>A floor of a tower (public floor or my own office) with live occupancy.</summary>
    /// <param name="Floor">0 = ground floor (lobby).</param>
    /// <param name="QueueLength">People waiting in the lift for this floor.</param>
    public sealed record TowerFloorDto(
        Guid RoomId, int Floor, string Name, string Theme, int Capacity, int Occupancy, int QueueLength, bool IsPublic, bool IsMine);

    public sealed record TowerDto(Guid BuildingId, IReadOnlyList<TowerFloorDto> Floors);

    public static class ElevatorStatus
    {
        /// <summary>You are on the target floor (snapshot included).</summary>
        public const string Arrived = "arrived";

        /// <summary>The floor is full: you wait in the queue and ride up automatically when it's your turn.</summary>
        public const string Queued = "queued";
    }

    public sealed record ElevatorResultDto(string Status, RoomSnapshotDto? Snapshot, QueueStatusDto? Queue);

    /// <param name="Position">1 = next. 0 = not queued (any more).</param>
    public sealed record QueueStatusDto(Guid RoomId, int Floor, string FloorName, int Position);

    public sealed record RoomChatMessageDto(Guid UserId, string DisplayName, string Text, DateTimeOffset SentAt);
}
