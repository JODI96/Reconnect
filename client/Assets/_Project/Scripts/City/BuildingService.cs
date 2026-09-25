using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Reconnect.Client.Networking;
using Reconnect.Contracts;
using Reconnect.Contracts.Buildings;

namespace Reconnect.Client.City
{
    public sealed class BuildingService
    {
        private readonly ApiClient _api;

        public BuildingService(ApiClient api)
        {
            _api = api;
        }

        public Task<ApiResult<List<BuildingDto>>> GetNearbyAsync(double latitude, double longitude, float radiusMeters,
            CancellationToken ct = default) =>
            _api.GetAsync<List<BuildingDto>>(string.Format(CultureInfo.InvariantCulture,
                "{0}?lat={1}&lng={2}&radiusMeters={3}", ApiRoutes.Buildings.Nearby, latitude, longitude, radiusMeters), ct);
    }
}
