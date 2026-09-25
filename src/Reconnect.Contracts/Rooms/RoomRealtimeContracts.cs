using System;
using System.Collections.Generic;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>Room floor as a grid of 1 m tiles (Habbo style). Fixed size until rooms get their own size.</summary>
    public static class RoomGrid
    {
        public const int Width = 10;
        public const int Depth = 10;
        public const int MaxPlayers = 25;
        public const int MaxChatLength = 200;
    }

    /// <param name="X">Column, 0 … Width-1 (Unity +X).</param>
    /// <param name="Z">Row, 0 … Depth-1 (Unity +Z).</param>
    public sealed record TilePosition(int X, int Z);

    public sealed record RoomPlayerDto(Guid UserId, string DisplayName, TilePosition Tile);

    /// <summary>Everything a client needs when entering: the room and who is already there (including the caller).</summary>
    public sealed record RoomSnapshotDto(RoomDto Room, int Width, int Depth, IReadOnlyList<RoomPlayerDto> Players);

    public sealed record PlayerMovedDto(Guid UserId, TilePosition Tile);

    public sealed record RoomChatMessageDto(Guid UserId, string DisplayName, string Text, DateTimeOffset SentAt);
}
