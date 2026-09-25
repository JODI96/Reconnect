using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reconnect.Client.Networking;

namespace Reconnect.Client.Tests
{
    /// <summary>Records requests and answers them from a handler – completes synchronously.</summary>
    public sealed class FakeTransport : IHttpTransport
    {
        private readonly Func<HttpRequest, HttpResponse> _handler;

        public FakeTransport(Func<HttpRequest, HttpResponse> handler)
        {
            _handler = handler;
        }

        public List<HttpRequest> Requests { get; } = new();

        public Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(_handler(request));
        }

        /// <summary>The fake completes synchronously, so blocking here cannot deadlock.</summary>
        public static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();
    }
}
