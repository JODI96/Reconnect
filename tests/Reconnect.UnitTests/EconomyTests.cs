using Reconnect.Modules.RealEstate.Domain;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Modules.Wallet.Public;
using Reconnect.SharedKernel.Domain;

namespace Reconnect.UnitTests;

public sealed class EconomyTests
{
    [Theory]
    [InlineData(10_000, 1_000_000)]
    [InlineData(0.05, 5)]
    [InlineData(12.345, 1235)]   // rounded to whole Rappen
    public void Francs_are_stored_as_whole_rappen(decimal francs, long rappen)
    {
        var amount = Chf.FromFrancs(francs);

        Assert.Equal(rappen, amount.Rappen);
        Assert.Equal(rappen / 100m, amount.Francs);
    }

    [Fact]
    public void Chf_amounts_add_up_exactly()
    {
        Assert.Equal(Chf.FromFrancs(0.3m), Chf.FromFrancs(0.1m) + Chf.FromFrancs(0.2m));
        Assert.Equal(Chf.Zero, Chf.FromFrancs(5m) - Chf.FromFrancs(5m));
    }

    [Fact]
    public void Prime_tower_offers_offices_only_on_office_floors_with_rising_prices()
    {
        var offer = PrimeTowerOffices.Offer().ToList();

        Assert.DoesNotContain(offer, o => PrimeTowerOffices.PublicFloors.Contains(o.Floor));
        Assert.Equal(offer.Count, offer.Select(o => o.Id).Distinct().Count());
        Assert.All(offer, o => Assert.InRange(o.Floor, 2, 33));
        var north = offer.Where(o => o.Name.Contains("Nord")).OrderBy(o => o.Floor).Select(o => o.PriceRappen).ToList();
        Assert.Equal(north.Order(), north);   // higher = more expensive
        Assert.Contains(offer, o => o.PriceRappen <= Chf.FromFrancs(10_000m).Rappen);   // affordable with the starting capital
        Assert.Contains(offer, o => o.PriceRappen > Chf.FromFrancs(10_000m).Rappen);    // and some to save up for
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, Room.MaxCapacity + 1)]
    public void Room_floor_and_capacity_are_validated(int floor, int capacity)
    {
        var room = Room.Create(Guid.NewGuid(), Guid.NewGuid(), "Stock", isPublic: true);

        Assert.Throws<DomainException>(() => room.PlaceOnFloor(floor, capacity));
    }

    [Fact]
    public void Room_can_be_placed_on_a_floor()
    {
        var room = Room.Create(Guid.NewGuid(), Guid.NewGuid(), "Lobby", isPublic: true);

        room.PlaceOnFloor(0, 80);

        Assert.Equal(0, room.Floor);
        Assert.Equal(80, room.Capacity);
    }
}
