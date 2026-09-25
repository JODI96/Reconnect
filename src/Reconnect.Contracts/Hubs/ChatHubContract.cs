namespace Reconnect.Contracts.Hubs
{
    /// <summary>SignalR chat hub: path, server methods and client events.</summary>
    public static class ChatHubContract
    {
        public const string Path = "/hubs/chat";

        /// <summary>Server methods the client can invoke.</summary>
        public static class Server
        {
            /// <summary>(Guid matchId, string text) → MessageDto</summary>
            public const string SendMessage = "SendMessage";
        }

        /// <summary>Events the server pushes to the client.</summary>
        public static class Client
        {
            /// <summary>Payload: MessageDto</summary>
            public const string ReceiveMessage = "ReceiveMessage";

            /// <summary>Payload: MatchDto</summary>
            public const string MatchCreated = "MatchCreated";
        }
    }
}
