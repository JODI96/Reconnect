using System;
using System.Threading;
using System.Threading.Tasks;
using Reconnect.Client.Networking;
using Reconnect.Contracts;
using Reconnect.Contracts.Auth;

namespace Reconnect.Client.Auth
{
    /// <summary>Holds the current session (tokens) and talks to the /auth endpoints.</summary>
    public sealed class AuthService : IAccessTokenProvider
    {
        private readonly ApiClient _api;
        private readonly ITokenStore _store;
        private AuthResponse _session;
        private Task<bool> _refreshInFlight;

        public AuthService(ApiClient api, ITokenStore store)
        {
            _api = api;
            _store = store;
        }

        /// <summary>Raised after login, register, restore and logout.</summary>
        public event Action SessionChanged;

        public bool IsLoggedIn => _session != null;
        public Guid? UserId => _session?.UserId;
        public string AccessToken => _session?.AccessToken;

        public async Task<ApiResult<AuthResponse>> LoginAsync(string email, string password, CancellationToken ct = default)
        {
            var result = await _api.PostAsync<AuthResponse>(ApiRoutes.Auth.Login, new LoginRequest(email.Trim(), password), ct);
            if (result.IsSuccess)
            {
                SetSession(result.Value);
            }
            return result;
        }

        public async Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            var result = await _api.PostAsync<AuthResponse>(ApiRoutes.Auth.Register, request, ct);
            if (result.IsSuccess)
            {
                SetSession(result.Value);
            }
            return result;
        }

        /// <summary>On app start: log in silently with the stored refresh token, if any.</summary>
        public async Task<bool> TryRestoreSessionAsync(CancellationToken ct = default)
        {
            var stored = _store.LoadRefreshToken();
            return stored != null && await RefreshWithAsync(stored, ct);
        }

        public Task<bool> TryRefreshAsync(CancellationToken ct)
        {
            if (_session == null)
            {
                return Task.FromResult(false);
            }

            // Several requests may hit 401 at once – refresh tokens are single-use, so share one refresh.
            return _refreshInFlight ??= RefreshOnceAsync(_session.RefreshToken, ct);
        }

        public void Logout()
        {
            _session = null;
            _store.Clear();
            SessionChanged?.Invoke();
        }

        private async Task<bool> RefreshOnceAsync(string refreshToken, CancellationToken ct)
        {
            try
            {
                return await RefreshWithAsync(refreshToken, ct);
            }
            finally
            {
                _refreshInFlight = null;
            }
        }

        private async Task<bool> RefreshWithAsync(string refreshToken, CancellationToken ct)
        {
            // Send without an access token so a 401 here doesn't trigger another refresh.
            var previous = _session;
            _session = null;
            var result = await _api.PostAsync<AuthResponse>(ApiRoutes.Auth.Refresh, new RefreshRequest(refreshToken), ct);

            if (result.IsSuccess)
            {
                SetSession(result.Value);
                return true;
            }

            if (result.StatusCode == 401)
            {
                Logout();   // refresh token expired or revoked
            }
            else
            {
                _session = previous;   // network problem: keep the session, try again later
            }
            return false;
        }

        private void SetSession(AuthResponse session)
        {
            _session = session;
            _store.SaveRefreshToken(session.RefreshToken);
            SessionChanged?.Invoke();
        }
    }
}
