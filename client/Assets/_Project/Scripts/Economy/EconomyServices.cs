using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Reconnect.Client.Networking;
using Reconnect.Contracts;
using Reconnect.Contracts.RealEstate;
using Reconnect.Contracts.Rooms;
using Reconnect.Contracts.Wallet;

namespace Reconnect.Client.Economy
{
    /// <summary>Swiss formatting of in-game money: "CHF 10'000.00".</summary>
    public static class Money
    {
        private static readonly NumberFormatInfo Swiss = new() { NumberGroupSeparator = "'", NumberDecimalSeparator = "." };

        public static string Format(decimal francs) => "CHF " + francs.ToString("#,0.00", Swiss);
    }

    public sealed class WalletService
    {
        private readonly ApiClient _api;

        public WalletService(ApiClient api)
        {
            _api = api;
        }

        public Task<ApiResult<WalletDto>> GetAsync(CancellationToken ct = default) => _api.GetAsync<WalletDto>(ApiRoutes.Wallet.Me, ct);
    }

    public sealed class RealEstateService
    {
        private readonly ApiClient _api;

        public RealEstateService(ApiClient api)
        {
            _api = api;
        }

        public Task<ApiResult<List<OfficeUnitDto>>> GetOfficesAsync(Guid buildingId, CancellationToken ct = default) =>
            _api.GetAsync<List<OfficeUnitDto>>(ApiRoutes.RealEstate.Units(buildingId), ct);

        public Task<ApiResult<OfficeTransactionDto>> BuyAsync(Guid unitId, CancellationToken ct = default) =>
            _api.PostAsync<OfficeTransactionDto>(ApiRoutes.RealEstate.Buy(unitId), new { }, ct);

        public Task<ApiResult<OfficeTransactionDto>> SellAsync(Guid unitId, CancellationToken ct = default) =>
            _api.PostAsync<OfficeTransactionDto>(ApiRoutes.RealEstate.Sell(unitId), new { }, ct);
    }

    /// <summary>Floors of a tower building (Prime Tower) with live occupancy – for the lift panel.</summary>
    public sealed class TowerService
    {
        private readonly ApiClient _api;

        public TowerService(ApiClient api)
        {
            _api = api;
        }

        public Task<ApiResult<TowerDto>> GetAsync(Guid buildingId, CancellationToken ct = default) =>
            _api.GetAsync<TowerDto>(ApiRoutes.Rooms.Tower(buildingId), ct);
    }
}
