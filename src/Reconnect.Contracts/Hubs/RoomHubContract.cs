namespace Reconnect.Contracts.Hubs
{
    /// <summary>SignalR room hub (live presence in a room): path, server methods and client events.</summary>
    public static class RoomHubContract
    {
        public const string Path = "/hubs/room";

        /// <summary>Server methods the client can invoke.</summary>
        public static class Server
        {
            /// <summary>(Guid roomId) → RoomSnapshotDto. Leaves the previous room, if any.</summary>
            public const string JoinRoom = "JoinRoom";

            /// <summary>() → nothing.</summary>
            public const string LeaveRoom = "LeaveRoom";

            /// <summary>(int x, int z) → TilePosition actually used (clamped to the grid).</summary>
            public const string MoveTo = "MoveTo";

            /// <summary>(string text) → nothing. Max RoomGrid.MaxChatLength characters.</summary>
            public const string Say = "Say";
        }

        /// <summary>Events the server pushes to clients in the same room (never between blocked users).</summary>
        public static class Client
        {
            /// <summary>Payload: RoomPlayerDto</summary>
            public const string PlayerJoined = "PlayerJoined";

            /// <summary>Payload: Guid userId</summary>
            public const string PlayerLeft = "PlayerLeft";

            /// <summary>Payload: PlayerMovedDto</summary>
            public const string PlayerMoved = "PlayerMoved";

            /// <summary>Payload: RoomChatMessageDto</summary>
            public const string ChatMessage = "ChatMessage";
        }
    }
}
