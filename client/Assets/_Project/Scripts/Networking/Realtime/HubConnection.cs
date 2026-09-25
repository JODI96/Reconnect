using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Reconnect.Client.Networking.Realtime
{
    /// <summary>
    /// Minimal SignalR client (JSON protocol over WebSockets, no negotiation) – enough for our hubs
    /// and free of the ~15 extra DLLs the official client needs (IL2CPP-friendly).
    /// Must be used from the Unity main thread: awaits resume there, so event handlers do too.
    /// </summary>
    public sealed class HubConnection : IDisposable
    {
        private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);

        private readonly Uri _url;
        private readonly Dictionary<string, Action<JToken>> _handlers = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JToken>> _pending = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private ClientWebSocket _socket;
        private CancellationTokenSource _lifetime;
        private int _nextInvocationId;

        /// <param name="hubUrl">http(s)://host/hubs/room – converted to ws(s).</param>
        /// <param name="accessToken">JWT, sent as query parameter (browsers/WebSockets can't set headers).</param>
        public HubConnection(string hubUrl, string accessToken)
        {
            var builder = new UriBuilder(hubUrl);
            builder.Scheme = builder.Scheme == "https" ? "wss" : "ws";
            builder.Query = "access_token=" + Uri.EscapeDataString(accessToken);
            _url = builder.Uri;
        }

        /// <summary>Raised once when the connection ends (argument: error message or null).</summary>
        public event Action<string> Closed;

        public bool IsConnected => _socket?.State == WebSocketState.Open;

        /// <summary>Registers a handler for a server event with one argument.</summary>
        public void On<T>(string method, Action<T> handler) =>
            _handlers[method] = argument => handler(argument.ToObject<T>(JsonSerializer.Create(Json.Settings)));

        public async Task ConnectAsync(CancellationToken ct)
        {
            _lifetime = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            await _socket.ConnectAsync(_url, ct);
            await SendRawAsync(HubProtocol.Handshake, ct);

            // The first message is the handshake response: "{}" or {"error": "..."}.
            var buffer = new StringBuilder();
            var handshake = (await ReceiveMessagesAsync(buffer, ct))[0];
            var error = JObject.Parse(handshake)["error"]?.ToString();
            if (error != null)
            {
                throw new InvalidOperationException("SignalR handshake failed: " + error);
            }

            _ = ReceiveLoopAsync(buffer, _lifetime.Token);
            _ = PingLoopAsync(_lifetime.Token);
        }

        public async Task<T> InvokeAsync<T>(string method, params object[] arguments)
        {
            var result = await InvokeCoreAsync(method, arguments);
            return result == null || result.Type == JTokenType.Null
                ? default
                : result.ToObject<T>(JsonSerializer.Create(Json.Settings));
        }

        public Task InvokeAsync(string method, params object[] arguments) => InvokeCoreAsync(method, arguments);

        public void Dispose()
        {
            _lifetime?.Cancel();
            if (_socket != null && _socket.State == WebSocketState.Open)
            {
                // Fire and forget: tell the server we're leaving so it cleans up immediately.
                _ = _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            FailPending("Connection closed.");
        }

        private async Task<JToken> InvokeCoreAsync(string method, object[] arguments)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            var id = Interlocked.Increment(ref _nextInvocationId).ToString();
            var completion = new TaskCompletionSource<JToken>();
            _pending[id] = completion;
            await SendRawAsync(HubProtocol.InvocationMessage(id, method, arguments), _lifetime.Token);
            return await completion.Task;
        }

        private async Task ReceiveLoopAsync(StringBuilder buffer, CancellationToken ct)
        {
            string closeReason = null;
            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    foreach (var message in await ReceiveMessagesAsync(buffer, ct))
                    {
                        closeReason = Dispatch(JObject.Parse(message)) ?? closeReason;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Disposed.
            }
            catch (Exception ex)
            {
                closeReason = ex.Message;
            }

            FailPending(closeReason ?? "Connection closed.");
            Closed?.Invoke(closeReason);
        }

        /// <summary>Handles one message. Returns an error text if the server closed the connection.</summary>
        private string Dispatch(JObject message)
        {
            switch ((int?)message["type"])
            {
                case HubProtocol.Invocation:
                    var target = (string)message["target"];
                    var arguments = (JArray)message["arguments"];
                    if (target != null && _handlers.TryGetValue(target, out var handler))
                    {
                        try
                        {
                            handler(arguments != null && arguments.Count > 0 ? arguments[0] : JValue.CreateNull());
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);   // a broken handler must not kill the connection
                        }
                    }
                    return null;

                case HubProtocol.Completion:
                    if (_pending.TryRemove((string)message["invocationId"], out var completion))
                    {
                        var error = (string)message["error"];
                        if (error != null)
                        {
                            completion.TrySetException(new HubException(error));
                        }
                        else
                        {
                            completion.TrySetResult(message["result"]);
                        }
                    }
                    return null;

                case HubProtocol.Close:
                    _lifetime.Cancel();
                    return (string)message["error"] ?? "Server closed the connection.";

                default:
                    return null;   // Ping etc.
            }
        }

        private async Task PingLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(PingInterval, ct);
                    if (IsConnected)
                    {
                        await SendRawAsync(HubProtocol.PingMessage, ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Disposed.
            }
            catch (WebSocketException)
            {
                // Connection lost – the receive loop reports it.
            }
        }

        private async Task<List<string>> ReceiveMessagesAsync(StringBuilder buffer, CancellationToken ct)
        {
            var chunk = new byte[8192];
            while (true)
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(chunk), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new WebSocketException("Server closed the WebSocket.");
                }

                buffer.Append(Encoding.UTF8.GetString(chunk, 0, result.Count));
                var messages = HubProtocol.ExtractMessages(buffer);
                if (messages.Count > 0)
                {
                    return messages;
                }
            }
        }

        private async Task SendRawAsync(string text, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await _sendLock.WaitAsync(ct);
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private void FailPending(string reason)
        {
            foreach (var id in _pending.Keys)
            {
                if (_pending.TryRemove(id, out var completion))
                {
                    completion.TrySetException(new HubException(reason));
                }
            }
        }
    }

    /// <summary>Error reported by the hub (e.g. "Room not found.").</summary>
    public sealed class HubException : Exception
    {
        private const string ServerPrefix = "HubException: ";

        public HubException(string message) : base(Clean(message))
        {
        }

        /// <summary>The server wraps messages: "An unexpected error occurred invoking 'X' on the server. HubException: Room not found."</summary>
        private static string Clean(string message)
        {
            var index = message?.IndexOf(ServerPrefix, StringComparison.Ordinal) ?? -1;
            return index >= 0 ? message.Substring(index + ServerPrefix.Length) : message;
        }
    }
}
