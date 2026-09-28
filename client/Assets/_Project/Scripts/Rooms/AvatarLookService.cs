using System.Threading;
using System.Threading.Tasks;
using Reconnect.Client.Networking;
using Reconnect.Contracts;
using Reconnect.Contracts.Avatars;

namespace Reconnect.Client.Rooms
{
    /// <summary>My avatar's look in the profile (character creator). No look yet = a null value.</summary>
    public sealed class AvatarLookService
    {
        private readonly ApiClient _api;

        public AvatarLookService(ApiClient api)
        {
            _api = api;
        }

        public Task<ApiResult<AvatarLookDto>> GetMineAsync(CancellationToken ct = default) => _api.GetAsync<AvatarLookDto>(ApiRoutes.Profiles.MyLook, ct);

        public Task<ApiResult<AvatarLookDto>> SaveAsync(AvatarLookDto look, CancellationToken ct = default) =>
            _api.PutAsync<AvatarLookDto>(ApiRoutes.Profiles.MyLook, look, ct);
    }
}
