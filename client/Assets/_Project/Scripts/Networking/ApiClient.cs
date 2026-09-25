using System.Threading;
using System.Threading.Tasks;

namespace Reconnect.Client.Networking
{
    /// <summary>Supplies the current access token and can renew it (implemented by AuthService).</summary>
    public interface IAccessTokenProvider
    {
        string AccessToken { get; }

        /// <summary>Tries to get a new access token with the refresh token. False if the session is gone.</summary>
        Task<bool> TryRefreshAsync(CancellationToken ct);
    }

    /// <summary>
    /// Typed JSON calls against the Reconnect API. Adds the bearer token and, on a 401,
    /// refreshes the session once and retries the request.
    /// </summary>
    public sealed class ApiClient
    {
        private readonly IHttpTransport _transport;
        private readonly string _baseUrl;

        public ApiClient(IHttpTransport transport, string baseUrl)
        {
            _transport = transport;
            _baseUrl = baseUrl.TrimEnd('/');
        }

        /// <summary>Set by the composition root once the AuthService exists.</summary>
        public IAccessTokenProvider Tokens { get; set; }

        public Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken ct = default) =>
            SendAsync<T>("GET", path, null, ct);

        public Task<ApiResult<T>> PostAsync<T>(string path, object body, CancellationToken ct = default) =>
            SendAsync<T>("POST", path, body, ct);

        public Task<ApiResult<T>> PutAsync<T>(string path, object body, CancellationToken ct = default) =>
            SendAsync<T>("PUT", path, body, ct);

        private async Task<ApiResult<T>> SendAsync<T>(string method, string path, object body, CancellationToken ct)
        {
            var json = body == null ? null : Json.Serialize(body);
            var response = await _transport.SendAsync(new HttpRequest(method, _baseUrl + path, json, Tokens?.AccessToken), ct);

            if (response.StatusCode == 401 && Tokens?.AccessToken != null && await Tokens.TryRefreshAsync(ct))
            {
                response = await _transport.SendAsync(new HttpRequest(method, _baseUrl + path, json, Tokens.AccessToken), ct);
            }

            if (!response.IsSuccess)
            {
                return ApiResult<T>.Failure(ApiError.FromResponse(response), response.StatusCode);
            }

            var value = string.IsNullOrEmpty(response.Body) ? default : Json.Deserialize<T>(response.Body);
            return ApiResult<T>.Success(value, response.StatusCode);
        }
    }
}
