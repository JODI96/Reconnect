using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Reconnect.Client.Networking.Realtime
{
    /// <summary>
    /// The SignalR JSON hub protocol (https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/docs/specs/HubProtocol.md):
    /// JSON messages terminated by the record separator 0x1E. Pure string handling – unit tested.
    /// </summary>
    public static class HubProtocol
    {
        public const char RecordSeparator = '\u001e';

        public const int Invocation = 1;
        public const int Completion = 3;
        public const int Ping = 6;
        public const int Close = 7;

        public static string Handshake => "{\"protocol\":\"json\",\"version\":1}" + RecordSeparator;

        public static string PingMessage => "{\"type\":6}" + RecordSeparator;

        public static string InvocationMessage(string invocationId, string target, object[] arguments)
        {
            var message = new JObject
            {
                ["type"] = Invocation,
                ["target"] = target,
                ["arguments"] = JArray.FromObject(arguments, JsonSerializer.Create(Json.Settings)),
            };
            if (invocationId != null)
            {
                message["invocationId"] = invocationId;
            }
            return message.ToString(Formatting.None) + RecordSeparator;
        }

        /// <summary>
        /// Removes all complete messages from <paramref name="buffer"/>; an incomplete tail stays for the next frame.
        /// </summary>
        public static List<string> ExtractMessages(StringBuilder buffer)
        {
            var messages = new List<string>();
            var text = buffer.ToString();
            var start = 0;
            int end;
            while ((end = text.IndexOf(RecordSeparator, start)) >= 0)
            {
                if (end > start)
                {
                    messages.Add(text.Substring(start, end - start));
                }
                start = end + 1;
            }
            buffer.Remove(0, start);
            return messages;
        }
    }
}
