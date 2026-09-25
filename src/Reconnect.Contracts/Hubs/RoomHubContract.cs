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

            /// <summary>(string emote) → nothing. One of <c>Emotes.All</c>.</summary>
            public const string Emote = "Emote";

            /// <summary>() → TicTacToeStateDto. Takes the free seat (X first).</summary>
            public const string TicTacToeJoin = "TicTacToeJoin";

            /// <summary>(int cell 0–8) → TicTacToeStateDto.</summary>
            public const string TicTacToeMove = "TicTacToeMove";

            /// <summary>() → TicTacToeStateDto. Clears the board and seats.</summary>
            public const string TicTacToeReset = "TicTacToeReset";

            /// <summary>() → QuizStateDto. Starts a new round of questions for everyone in the room.</summary>
            public const string QuizStart = "QuizStart";

            /// <summary>(int answerIndex) → QuizStateDto. One answer per player and question.</summary>
            public const string QuizAnswer = "QuizAnswer";

            /// <summary>() → QuizStateDto. Reveals the answer, then moves to the next question.</summary>
            public const string QuizNext = "QuizNext";
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

            /// <summary>Payload: EmoteDto</summary>
            public const string PlayerEmote = "PlayerEmote";

            /// <summary>Payload: TicTacToeStateDto</summary>
            public const string TicTacToeUpdated = "TicTacToeUpdated";

            /// <summary>Payload: QuizStateDto</summary>
            public const string QuizUpdated = "QuizUpdated";
        }
    }
}
