using System.Threading;
using System.Threading.Tasks;

namespace Reconnect.Client.Networking
{
    /// <summary>Raw HTTP. Implemented with UnityWebRequest in the app and faked in tests.</summary>
    public interface IHttpTransport
    {
        Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken ct);
    }

    public sealed class HttpRequest
    {
        public HttpRequest(string method, string url, string jsonBody = null, string bearerToken = null)
        {
            Method = method;
            Url = url;
            JsonBody = jsonBody;
            BearerToken = bearerToken;
        }

        public string Method { get; }
        public string Url { get; }
        public string JsonBody { get; }
        public string BearerToken { get; }
    }

    public sealed class HttpResponse
    {
        public HttpResponse(long statusCode, string body, string networkError = null)
        {
            StatusCode = statusCode;
            Body = body;
            NetworkError = networkError;
        }

        /// <summary>0 when the server was not reached.</summary>
        public long StatusCode { get; }
        public string Body { get; }

        /// <summary>Set when the request never got an HTTP response (offline, timeout, DNS…).</summary>
        public string NetworkError { get; }

        public bool IsSuccess => NetworkError == null && StatusCode >= 200 && StatusCode < 300;
    }
}
