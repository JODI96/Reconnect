using System;
using System.Threading;
using System.Threading.Tasks;
using Reconnect.Client.Networking;
using Reconnect.Contracts;
using Reconnect.Contracts.Buildings;
using UnityEngine;

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

        private readonly ApiClient _api;
        private MapSessionDto _current;
        private float _validUntil;

        public MapService(ApiClient api)
        {
            _api = api;
        }

        public async Task<MapSessionDto> GetAsync(CancellationToken ct = default)
        {
            if (_current != null && Time.realtimeSinceStartup < _validUntil)
            {
                return _current;
            }

            var result = await _api.PostAsync<MapSessionDto>(ApiRoutes.Maps.Session, new { }, ct);
            _current = result.IsSuccess ? result.Value : Offline;
            _validUntil = Time.realtimeSinceStartup + Math.Max(1, _current.ValidForMinutes) * 60f;
            return _current;
        }

        /// <summary>After logout/login (another user, maybe Premium) the next call asks again.</summary>
        public void Reset() => _current = null;
    }
}
