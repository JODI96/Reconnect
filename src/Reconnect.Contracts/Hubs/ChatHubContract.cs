namespace Reconnect.Contracts.Hubs
{
    /// <summary>SignalR chat hub: path, server methods and client events.</summary>
    public static class ChatHubContract
    {
        /// <summary>Relative to the API version group (backend).</summary>
        public const string RelativePath = "/hubs/chat";

        /// <summary>Full path including the API version (clients).</summary>
        public const string Path = ApiRoutes.Version + RelativePath;

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
