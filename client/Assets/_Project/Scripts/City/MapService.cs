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
    /// Which city map to show. By default swisstopo (free, nothing is asked); with the switch "Karte: Google 3D" in the
    /// city menu (remembered per device) the backend decides whether this user gets Google Photorealistic 3D Tiles
    /// (Premium/Admin always, others a few sessions per month). That answer is kept for its validity (a Google session is
    /// billed once and lasts up to 3 h), so switching screens – or the switch back and forth – doesn't pay again. Falls
    /// back to swisstopo if the backend can't be asked.
    /// </summary>
    public sealed class MapService
    {
        private static readonly MapSessionDto Offline =
            new(MapProviders.Swisstopo, null, false, null, null, 5);

        /// <summary>swisstopo chosen: always the same instance, so the city doesn't reload when asked again.</summary>
        private static readonly MapSessionDto Swisstopo =
            new(MapProviders.Swisstopo, null, false, null, null, 60);

        private const string GooglePref = "map.google";

        private readonly ApiClient _api;
        private readonly bool _googleAllowed;
        private MapSessionDto _current;
        private DateTime _validUntilUtc;
        private Task<MapSessionDto> _pending;

        /// <param name="googleAllowed">False in automated tests: they never start a billed Google session.</param>
        public MapService(ApiClient api, bool googleAllowed = true)
        {
            _api = api;
            _googleAllowed = googleAllowed;
        }

        /// <summary>The switch in the city menu: photorealistic Google city wanted (default: off → swisstopo).</summary>
        public bool GoogleWanted => _googleAllowed && PlayerPrefs.GetInt(GooglePref, 0) == 1;

        public void SetGoogleWanted(bool wanted)
        {
            PlayerPrefs.SetInt(GooglePref, wanted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public Task<MapSessionDto> GetAsync(CancellationToken ct = default)
        {
            if (!GoogleWanted)
            {
                return Task.FromResult(Swisstopo);   // the backend isn't asked – nothing is counted
            }
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
