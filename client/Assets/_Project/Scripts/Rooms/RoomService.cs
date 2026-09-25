using System;
using System.Threading;
using System.Threading.Tasks;
using Reconnect.Client.Networking;
using Reconnect.Contracts;
using Reconnect.Contracts.Common;
using Reconnect.Contracts.Rooms;

namespace Reconnect.Client.Rooms
{
    public sealed class RoomService
    {
        public const int PageSize = 20;

        private readonly ApiClient _api;

        public RoomService(ApiClient api)
        {
            _api = api;
        }

        /// <param name="buildingId">Only rooms in this building, or all visible rooms if null.</param>
        public Task<ApiResult<PagedResponse<RoomSummaryDto>>> GetRoomsAsync(int page, Guid? buildingId = null,
            CancellationToken ct = default)
        {
            var path = $"{ApiRoutes.Rooms.Group}?page={page}&pageSize={PageSize}";
            if (buildingId != null)
            {
                path += "&buildingId=" + buildingId.Value;
            }
            return _api.GetAsync<PagedResponse<RoomSummaryDto>>(path, ct);
        }

        public Task<ApiResult<RoomDto>> GetRoomAsync(Guid id, CancellationToken ct = default) =>
            _api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(id), ct);

        public Task<ApiResult<RoomDto>> CreateRoomAsync(Guid buildingId, string name, bool isPublic, CancellationToken ct = default) =>
            _api.PostAsync<RoomDto>(ApiRoutes.Rooms.Group, new CreateRoomRequest(buildingId, name.Trim(), isPublic), ct);
    }
}
