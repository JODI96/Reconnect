using System;
using System.Threading;
using System.Threading.Tasks;
using Reconnect.Client.Networking;
using Reconnect.Contracts;
using Reconnect.Contracts.Buildings;

namespace Reconnect.Client.City
{
    /// <summary>
    /// Asks the backend which city map this app session gets (Google Photorealistic 3D Tiles or swisstopo).
    /// The answer is kept for its validity (a Google session is billed once and lasts up to 3 h), so switching
    /// screens doesn't start – and pay for – a new session. Falls back to swisstopo if the backend can't be asked.
    /// </summary>
    public sealed class MapService
    {
        private static readonly MapSessionDto Offline =
            new(MapProviders.Swisstopo, null, false, null, null, 5);

#if UNITY_EDITOR
        /// <summary>
        /// Every Play in the editor would start a billed Google session. So the editor shows swisstopo
        /// unless the developer switches on "Reconnect > Google 3D im Editor" (per machine, EditorPrefs).
        /// </summary>
        public const string GoogleInEditorPref = "Reconnect.GoogleInEditor";

        private static readonly MapSessionDto EditorSwisstopo =
            new(MapProviders.Swisstopo, null, false, null, null, 1);

        private static bool GoogleAllowed => UnityEditor.EditorPrefs.GetBool(GoogleInEditorPref, false);
#else
        private static bool GoogleAllowed => true;
#endif

        private readonly ApiClient _api;
        private MapSessionDto _current;
        private DateTime _validUntilUtc;
        private Task<MapSessionDto> _pending;

        public MapService(ApiClient api)
        {
            _api = api;
        }

        public Task<MapSessionDto> GetAsync(CancellationToken ct = default)
        {
#if UNITY_EDITOR
            if (!GoogleAllowed)
            {
                return Task.FromResult(EditorSwisstopo);   // the backend isn't asked – nothing is counted
            }
#endif
            // Wall-clock time: on phones the app can sit in the background for hours.
            if (_current != null && DateTime.UtcNow < _validUntilUtc)
            {
                return Task.FromResult(_current);
            }
            // Callers asking at the same time share one request – each request may be a billed Google session.
            if (_pending == null || _pending.IsCompleted)
            {
                _pending = FetchAsync(ct);
            }
            return _pending;
        }

        private async Task<MapSessionDto> FetchAsync(CancellationToken ct)
        {
            try
            {
                var result = await _api.PostAsync<MapSessionDto>(ApiRoutes.Maps.Session, new { }, ct);
                _current = result.IsSuccess ? result.Value : Offline;
                _validUntilUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, _current.ValidForMinutes));
                return _current;
            }
            finally
            {
                _pending = null;
            }
        }

        /// <summary>After logout/login (another user, maybe Premium) the next call asks again.</summary>
        public void Reset()
        {
            _current = null;
            _pending = null;
        }
    }
}
