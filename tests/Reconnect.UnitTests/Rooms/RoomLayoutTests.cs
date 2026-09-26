using Reconnect.Contracts.Rooms;

namespace Reconnect.UnitTests.Rooms;

public sealed class RoomLayoutTests
{
    // Habbo-style room (walls north + east, door in the middle of the north wall), 10 × 8 m = 20 × 16 cells.
    private static readonly RoomLayoutContext Room = RoomZones.ContextFor("cafe", 10, 8, []);

    /// <summary>An item whose footprint starts at build cell (x, z).</summary>
    private static RoomItemDto At(string itemId, int x, int z, int quarter = 0)
    {
        var definition = ItemDefinitions.Find(itemId)!;
        var (width, depth) = RoomLayout.Size(definition, quarter);
        var (cx, cz) = RoomLayout.Centre(new CellRect(x, z, width, depth));
        return new RoomItemDto(itemId, new Vector3Dto(cx, 0f, cz), quarter * 90f);
    }

    private static IReadOnlyList<LayoutProblem> Check(params RoomItemDto[] items) => RoomLayout.Validate(Room, items);

    [Fact]
    public void The_catalog_knows_footprints_of_all_furniture()
    {
        Assert.True(ItemDefinitions.All.Count > 150);
        var table = ItemDefinitions.Find("table")!;
        Assert.Equal(ItemKind.Floor, table.Kind);
        Assert.True(table.HasSurface);
        Assert.Equal(ItemKind.Decor, ItemDefinitions.Find("laptop")!.Kind);
        Assert.Equal(ItemKind.Wall, ItemDefinitions.Find("ph-hanging_picture_frame_01")!.Kind);
        Assert.Equal(ItemKind.Rug, ItemDefinitions.Find("rugRectangle")!.Kind);
        Assert.All(ItemDefinitions.All, d => Assert.False(string.IsNullOrWhiteSpace(d.Name)));
    }

    [Fact]
    public void A_build_cell_is_a_quarter_of_a_walking_tile()
    {
        Assert.Equal(0.5f, BuildGrid.CellSize);
        Assert.Equal(4, BuildGrid.CellsPerTile * BuildGrid.CellsPerTile);
    }

    [Fact]
    public void Turning_an_item_swaps_its_footprint()
    {
        var table = ItemDefinitions.Find("table")!;
        var straight = RoomLayout.Footprint(At("table", 2, 2), table);
        var turned = RoomLayout.Footprint(At("table", 2, 2, quarter: 1), table);

        Assert.Equal((table.Width, table.Depth), (straight.Width, straight.Depth));
        Assert.Equal((table.Depth, table.Width), (turned.Width, turned.Depth));
    }

    [Fact]
    public void A_tidy_room_is_valid()
    {
        var problems = Check(
            At("table", 4, 4),
            At("chair", 4, 3),
            At("laptop", 5, 4),
            At("rugRectangle", 2, 2),
            At("ph-hanging_picture_frame_01", 2, 15),
            At("ph-potted_plant_01", 19, 0));

        Assert.Empty(problems);
    }

    [Fact]
    public void Furniture_may_not_overlap()
    {
        var problems = Check(At("table", 4, 4), At("loungeSofa", 5, 4));

        var problem = Assert.Single(problems);
        Assert.Equal(1, problem.Index);
        Assert.Contains("überlappt", problem.Message);
    }

    [Fact]
    public void Plants_and_small_things_need_a_surface()
    {
        var onFloor = Check(At("plantSmall1", 4, 4));
        var onSofa = Check(At("loungeSofa", 4, 4), At("plantSmall1", 5, 4));
        var onTable = Check(At("table", 4, 4), At("plantSmall1", 5, 4));

        Assert.Single(onFloor);
        Assert.Single(onSofa);
        Assert.Empty(onTable);
    }

    [Fact]
    public void Nothing_may_stick_out_of_the_room()
    {
        var problems = Check(At("ph-potted_plant_01", 19, 15), At("table", -1, 2));

        Assert.Equal(2, problems.Count);
        Assert.All(problems, p => Assert.Contains("ragt aus dem Raum", p.Message));
    }

    [Fact]
    public void Items_must_stand_on_the_grid_and_turn_in_quarter_steps()
    {
        var offGrid = new RoomItemDto("table", new Vector3Dto(3.13f, 0f, 3.37f), 0f);
        var slanted = At("chair", 10, 10) with { Rotation = 45f };

        var problems = Check(offGrid, slanted);

        Assert.Equal(2, problems.Count);
        Assert.Empty(Check(RoomLayout.Snap(offGrid)));
    }

    [Fact]
    public void The_door_stays_free()
    {
        var door = Assert.Single(Room.Reserved);

        var problems = Check(At("table", door.X, door.Z));

        Assert.Contains("Eingang", Assert.Single(problems).Message);
    }

    [Fact]
    public void Paintings_hang_on_walls_only()
    {
        Assert.Single(Check(At("ph-hanging_picture_frame_01", 6, 6)));
        Assert.Empty(Check(At("ph-hanging_picture_frame_01", 19, 6, quarter: 1)));
    }

    [Fact]
    public void Rugs_lie_under_furniture_and_ceiling_lamps_hang_above_it()
    {
        Assert.Empty(Check(At("rugRectangle", 3, 3), At("table", 4, 4), At("ph-Chandelier_01", 4, 4)));
    }

    [Fact]
    public void A_seat_nobody_can_reach_is_rejected()
    {
        // Chair in the corner, fenced in by shelves on the two open sides.
        var problems = Check(
            At("chair", 0, 0),
            At("bookcaseClosedWide", 0, 2),
            At("bookcaseClosed", 2, 0, quarter: 1));

        Assert.Contains(problems, p => p.Index == 0 && p.Message.Contains("zugestellt"));
    }

    [Fact]
    public void Pools_are_water_not_obstacles()
    {
        var pool = At("custom-pool-6x3", 2, 2);

        Assert.Empty(RoomLayout.BlockedTiles([pool]));
        Assert.Equal(18, RoomLayout.WaterTiles([pool]).Count);
    }

    [Fact]
    public void Furniture_blocks_the_walking_tiles_it_covers()
    {
        var blocked = RoomLayout.BlockedTiles([At("table", 4, 4), At("rugRectangle", 10, 10)]);

        Assert.Equal([(2, 2), (3, 2)], blocked.OrderBy(t => t).ToArray());
    }
}
