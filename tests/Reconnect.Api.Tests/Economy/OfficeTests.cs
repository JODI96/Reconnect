using System.Net;
using Reconnect.Api.Tests.Infrastructure;
using Reconnect.Contracts;
using Reconnect.Contracts.RealEstate;
using Reconnect.Contracts.Rooms;
using Reconnect.Contracts.Wallet;
using Reconnect.Modules.City.Public;

namespace Reconnect.Api.Tests.Economy;

[Collection(ApiTestGroup.Name)]
public sealed class OfficeTests(ReconnectApiFactory factory)
{
    private const decimal StartingCapital = 10_000m;

    [Fact]
    public async Task New_users_start_with_ten_thousand_francs_once()
    {
        var user = await factory.RegisterAsync();

        var first = await GetWalletAsync(user);
        var second = await GetWalletAsync(user);

        Assert.Equal(StartingCapital, first.Balance);
        Assert.Equal(Currency.Chf, first.Currency);
        Assert.Equal(StartingCapital, second.Balance);
        Assert.Single(second.RecentTransactions, t => t.Kind == TransactionKinds.StartingCapital);
    }

    [Fact]
    public async Task Buying_an_office_pays_for_it_and_gives_you_a_room_on_that_floor()
    {
        var user = await factory.RegisterAsync();
        var office = (await ListAsync(user)).First(u => u.IsAvailable && u.Price <= StartingCapital);

        var response = await user.Client.PostAsync(ApiRoutes.RealEstate.Buy(office.Id), null);
        var bought = await response.ReadAsync<OfficeTransactionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(bought.Unit.IsMine);
        Assert.Equal(StartingCapital - office.Price, bought.Balance);
        Assert.Contains((await GetWalletAsync(user)).RecentTransactions, t => t.Kind == TransactionKinds.OfficePurchase && t.Amount == -office.Price);

        var tower = await GetTowerAsync(user);
        var myFloor = Assert.Single(tower.Floors, f => f.IsMine);
        Assert.Equal(office.Floor, myFloor.Floor);
        Assert.Equal(bought.Unit.RoomId, myFloor.RoomId);
        Assert.False(myFloor.IsPublic);

        // Nobody else can see or buy it any more.
        var other = await factory.RegisterAsync();
        Assert.DoesNotContain(await ListAsync(other), u => u.Id == office.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await other.Client.PostAsync(ApiRoutes.RealEstate.Buy(office.Id), null)).StatusCode);
        Assert.DoesNotContain((await GetTowerAsync(other)).Floors, f => f.RoomId == myFloor.RoomId);
    }

    [Fact]
    public async Task Selling_refunds_the_price_and_removes_the_room()
    {
        var user = await factory.RegisterAsync();
        var office = (await ListAsync(user)).First(u => u.IsAvailable && u.Price <= StartingCapital);
        var bought = await (await user.Client.PostAsync(ApiRoutes.RealEstate.Buy(office.Id), null)).ReadAsync<OfficeTransactionDto>();

        var sold = await (await user.Client.PostAsync(ApiRoutes.RealEstate.Sell(office.Id), null)).ReadAsync<OfficeTransactionDto>();

        Assert.Equal(StartingCapital, sold.Balance);
        Assert.True(sold.Unit.IsAvailable);
        Assert.DoesNotContain((await GetTowerAsync(user)).Floors, f => f.RoomId == bought.Unit.RoomId);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync(ApiRoutes.Rooms.ById(bought.Unit.RoomId!.Value))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.PostAsync(ApiRoutes.RealEstate.Sell(office.Id), null)).StatusCode);
    }

    [Fact]
    public async Task You_cannot_spend_more_than_you_have()
    {
        var user = await factory.RegisterAsync();
        var tooExpensive = (await ListAsync(user)).First(u => u.IsAvailable && u.Price > StartingCapital);

        var response = await user.Client.PostAsync(ApiRoutes.RealEstate.Buy(tooExpensive.Id), null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(StartingCapital, (await GetWalletAsync(user)).Balance);
    }

    [Fact]
    public async Task Two_buyers_at_the_same_time_only_one_gets_the_office_and_the_other_is_refunded()
    {
        var anna = await factory.RegisterAsync();
        var ben = await factory.RegisterAsync();
        var office = (await ListAsync(anna)).Last(u => u.IsAvailable && u.Price <= StartingCapital);
        await GetWalletAsync(anna);
        await GetWalletAsync(ben);

        var responses = await Task.WhenAll(
            anna.Client.PostAsync(ApiRoutes.RealEstate.Buy(office.Id), null),
            ben.Client.PostAsync(ApiRoutes.RealEstate.Buy(office.Id), null));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var balances = new[] { (await GetWalletAsync(anna)).Balance, (await GetWalletAsync(ben)).Balance };
        Assert.Contains(StartingCapital, balances);
        Assert.Contains(StartingCapital - office.Price, balances);
    }

    private static async Task<List<OfficeUnitDto>> ListAsync(TestUser user) =>
        await (await user.Client.GetAsync(ApiRoutes.RealEstate.Units(ZurichBuildings.PrimeTowerId))).ReadAsync<List<OfficeUnitDto>>();

    private static async Task<WalletDto> GetWalletAsync(TestUser user) =>
        await (await user.Client.GetAsync(ApiRoutes.Wallet.Me)).ReadAsync<WalletDto>();

    private static async Task<TowerDto> GetTowerAsync(TestUser user) =>
        await (await user.Client.GetAsync(ApiRoutes.Rooms.Tower(ZurichBuildings.PrimeTowerId))).ReadAsync<TowerDto>();
}
