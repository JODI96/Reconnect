using Reconnect.Contracts.Rooms;

namespace Reconnect.UnitTests.Rooms;

public sealed class ItemColoursTests
{
    [Fact]
    public void Our_furniture_has_colour_zones_with_defaults_of_the_right_kind()
    {
        Assert.NotEmpty(FurnitureFamilies.All);
        foreach (var family in FurnitureFamilies.All)
        {
            var zones = ItemColours.ZonesFor(family.Id);
            Assert.True(zones.Count > 0, family.Id + " has colour zones");
            Assert.All(zones, z => Assert.Contains(z.Default, ItemColours.Swatches[z.Kind]));
        }
        Assert.Empty(ItemColours.ZonesFor("ph-sofa_02"));
    }

    [Fact]
    public void Only_swatches_of_the_zones_kind_are_allowed()
    {
        const string sofa = "custom-sofa-box-3";   // Bezug (fabric), Füsse (metal)

        Assert.Null(ItemColours.Problem(sofa, "sage/brass"));
        Assert.Null(ItemColours.Problem(sofa, "sage"));
        Assert.NotNull(ItemColours.Problem(sofa, "walnut/brass"));        // wood isn't a fabric
        Assert.NotNull(ItemColours.Problem(sofa, "sage/brass/walnut"));   // only two zones
        Assert.NotNull(ItemColours.Problem("ph-sofa_02", "sage"));        // can't be coloured
    }

    [Fact]
    public void Defaults_are_stored_as_nothing_and_choices_resolve_per_zone()
    {
        const string sofa = "custom-sofa-box-3";
        var defaults = ItemColours.ZonesFor(sofa).Select(z => z.Default).ToArray();

        Assert.Null(ItemColours.Join(sofa, defaults));
        Assert.Equal("petrol/" + defaults[1], ItemColours.Join(sofa, ["petrol", defaults[1]]));
        Assert.Equal(["petrol", defaults[1]], ItemColours.Resolve(sofa, "petrol"));
    }

    [Fact]
    public void The_build_rules_reject_a_wrong_colour()
    {
        var room = RoomZones.ContextFor("cafe", 10, 8, []);
        var pouf = new RoomItemDto("custom-pouf-round", new Vector3Dto(5f, 0f, 4f), 0f, "chrome");

        var problem = Assert.Single(RoomLayout.Validate(room, [pouf]));
        Assert.Contains("chrome", problem.Message);
    }

    [Fact]
    public void Every_piece_of_our_furniture_is_in_the_build_catalog()
    {
        var missing = FurnitureFamilies.Ids.Where(id => ItemDefinitions.Find(id) == null).ToList();

        Assert.True(missing.Count == 0, "Run Setup Project to regenerate the catalog – missing: " + string.Join(", ", missing));
        Assert.All(FurnitureFamilies.All.Where(f => f.Surface), f => Assert.True(ItemDefinitions.Find(f.Id)!.HasSurface, f.Id + " has a top"));
    }

    [Fact]
    public void Seats_of_our_furniture_are_known_to_the_server()
    {
        Assert.Equal(3, RoomSeats.PlacesFor("custom-sofa-round-3"));
        Assert.Equal(4, RoomSeats.PlacesFor("custom-sofa-low-corner"));
        Assert.Equal(1, RoomSeats.PlacesFor("custom-chair-shell"));
        Assert.Equal(0, RoomSeats.PlacesFor("custom-table-sled-240"));
    }
}
