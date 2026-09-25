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

        public Task<ApiResult<PagedResponse<RoomSummaryDto>>> GetRoomsAsync(int page, CancellationToken ct = default) =>
            _api.GetAsync<PagedResponse<RoomSummaryDto>>($"{ApiRoutes.Rooms.Group}?page={page}&pageSize={PageSize}", ct);

        public Task<ApiResult<RoomDto>> GetRoomAsync(Guid id, CancellationToken ct = default) =>
            _api.GetAsync<RoomDto>(ApiRoutes.Rooms.ById(id), ct);
    }
}
