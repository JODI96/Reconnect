namespace Reconnect.Contracts.Hubs
{
    /// <summary>SignalR room hub (live presence in a room): path, server methods and client events.</summary>
    public static class RoomHubContract
    {
        /// <summary>Relative to the API version group (backend).</summary>
        public const string RelativePath = "/hubs/room";

        /// <summary>Full path including the API version (clients).</summary>
        public const string Path = ApiRoutes.Version + RelativePath;

        /// <summary>Server methods the client can invoke.</summary>
        public static class Server
        {
            /// <summary>(Guid roomId) → RoomSnapshotDto. Leaves the previous room, if any.</summary>
            public const string JoinRoom = "JoinRoom";

            /// <summary>() → nothing.</summary>
            public const string LeaveRoom = "LeaveRoom";

            /// <summary>(int x, int z) → TilePosition actually used (clamped to the grid).</summary>
            public const string MoveTo = "MoveTo";

            /// <summary>
            /// (int item, int place) → bool. Sits down on a seat of the layout (<c>RoomSeats</c>); false when taken.
            /// Walking (MoveTo) stands up again.
            /// </summary>
            public const string Sit = "Sit";

            /// <summary>() → nothing. Stands up from the current seat.</summary>
            public const string StandUp = "StandUp";

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

            /// <summary>(string game) → BoardGameStateDto. Takes a free seat at a game for two (<c>BoardGames</c>).</summary>
            public const string BoardGameJoin = "BoardGameJoin";

            /// <summary>(string game, string move) → BoardGameStateDto. See <c>BoardGameStateDto</c> for the moves.</summary>
            public const string BoardGameMove = "BoardGameMove";

            /// <summary>(string game) → BoardGameStateDto. Clears the board and the seats.</summary>
            public const string BoardGameReset = "BoardGameReset";

            /// <summary>
            /// (Guid targetRoomId) → ElevatorResultDto. Rides to another floor of the same tower: arrives at once if
            /// there is room (and nobody waiting), otherwise joins the queue and rides up automatically.
            /// </summary>
            public const string RideElevator = "RideElevator";

            /// <summary>() → nothing. Leaves the lift queue.</summary>
            public const string LeaveQueue = "LeaveQueue";

            /// <summary>No payload – I saved a new look (profile); the room shows it to everyone (PlayerLookChanged).</summary>
            public const string RefreshLook = "RefreshLook";
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

            /// <summary>Payload: PlayerSeatDto – someone sat down or stood up (a move also stands up).</summary>
            public const string PlayerSeated = "PlayerSeated";

            /// <summary>Payload: RoomChatMessageDto</summary>
            public const string ChatMessage = "ChatMessage";

            /// <summary>Payload: EmoteDto</summary>
            public const string PlayerEmote = "PlayerEmote";

            /// <summary>Payload: TicTacToeStateDto</summary>
            public const string TicTacToeUpdated = "TicTacToeUpdated";

            /// <summary>Payload: QuizStateDto</summary>
            public const string QuizUpdated = "QuizUpdated";

            /// <summary>Payload: BoardGameStateDto (Connect Four, Memory, chess).</summary>
            public const string BoardGameUpdated = "BoardGameUpdated";

            /// <summary>Payload: QueueStatusDto – my place in the lift queue changed (Position 0 = left the queue).</summary>
            public const string QueueUpdated = "QueueUpdated";

            /// <summary>Payload: RoomSnapshotDto – it was my turn: the lift brought me to the floor I waited for.</summary>
            public const string ElevatorArrived = "ElevatorArrived";

            /// <summary>Payload: RoomLayoutChangedDto – the owner or an admin saved a new layout (build editor).</summary>
            public const string RoomLayoutChanged = "RoomLayoutChanged";

            /// <summary>Payload: PlayerLookDto – someone in the room changed their look (character creator).</summary>
            public const string PlayerLookChanged = "PlayerLookChanged";
        }
    }
}
