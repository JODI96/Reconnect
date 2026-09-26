using Reconnect.Contracts.Rooms;

namespace Reconnect.UnitTests.Rooms;

public sealed class RoomLayoutTests
{
    // Habbo-style room (walls north + east, door in the middle of the north wall), 10 × 8 m.
    private static readonly RoomLayoutContext Room = RoomZones.ContextFor("cafe", 10, 8, []);

    private static int Cells(float metres) => (int)Math.Round(metres / BuildGrid.CellSize);

    /// <summary>A floor item whose footprint starts at (x, z) metres.</summary>
    private static RoomItemDto At(string itemId, float x, float z, int quarter = 0)
    {
        var definition = ItemDefinitions.Find(itemId)!;
        var (width, depth) = RoomLayout.Size(definition, quarter);
        var (cx, cz) = RoomLayout.Centre(new CellRect(Cells(x), Cells(z), width, depth));
        return new RoomItemDto(itemId, new Vector3Dto(cx, 0f, cz), quarter * 90f);
    }

    /// <summary>A small thing with its centre at (x, z) metres (decor grid).</summary>
    private static RoomItemDto Decor(string itemId, float x, float z, int quarter = 0) =>
        new(itemId, new Vector3Dto(RoomLayout.SnapDecor(x), 0f, RoomLayout.SnapDecor(z)), quarter * 90f);

    /// <summary>Middle of a table's top (where small things go).</summary>
    private static (float X, float Z) TopOf(RoomItemDto table)
    {
        var top = RoomLayout.SurfaceArea(table, ItemDefinitions.Find(table.ItemId)!);
        return (top.CentreX, top.CentreZ);
    }

    private static IReadOnlyList<LayoutProblem> Check(params RoomItemDto[] items) => RoomLayout.Validate(Room, items);

    [Fact]
    public void The_catalog_knows_footprints_of_all_furniture()
    {
        Assert.True(ItemDefinitions.All.Count > 150);
        var table = ItemDefinitions.Find("table")!;
        Assert.Equal(ItemKind.Floor, table.Kind);
        Assert.True(table.HasSurface);
        Assert.True(table.SurfaceWidth > 1f && table.SurfaceDepth > 0.5f, "measured table top");
        Assert.Equal(ItemKind.Decor, ItemDefinitions.Find("laptop")!.Kind);
        Assert.Equal(ItemKind.Wall, ItemDefinitions.Find("ph-hanging_picture_frame_01")!.Kind);
        Assert.Equal(ItemKind.Rug, ItemDefinitions.Find("rugRectangle")!.Kind);
        Assert.All(ItemDefinitions.All, d => Assert.False(string.IsNullOrWhiteSpace(d.Name)));
    }

    [Fact]
    public void A_walking_tile_has_16_build_cells_and_tables_a_finer_decor_grid()
    {
        Assert.Equal(0.25f, BuildGrid.CellSize);
        Assert.Equal(16, BuildGrid.CellsPerTile * BuildGrid.CellsPerTile);
        Assert.Equal(BuildGrid.CellSize / 2f, BuildGrid.DecorStep);
    }

    [Fact]
    public void Turning_an_item_swaps_its_footprint()
    {
        var table = ItemDefinitions.Find("table")!;
        var straight = RoomLayout.Footprint(At("table", 1, 1), table);
        var turned = RoomLayout.Footprint(At("table", 1, 1, quarter: 1), table);

        Assert.Equal((table.Width, table.Depth), (straight.Width, straight.Depth));
        Assert.Equal((table.Depth, table.Width), (turned.Width, turned.Depth));
    }

    [Fact]
    public void A_tidy_room_is_valid()
    {
        var table = At("table", 2, 2);
        var (x, z) = TopOf(table);
        var problems = Check(
            table,
            At("chair", 2, 1),
            Decor("laptop", x, z),
            At("rugRectangle", 1, 1),
            At("ph-hanging_picture_frame_01", 1, 7.75f),
            At("ph-potted_plant_01", 9.5f, 0));

        Assert.Empty(problems);
    }

    [Fact]
    public void Furniture_may_not_overlap()
    {
        var problems = Check(At("table", 2, 2), At("loungeSofa", 2.5f, 2));

        var problem = Assert.Single(problems);
        Assert.Equal(1, problem.Index);
        Assert.Contains("überlappt", problem.Message);
    }

    [Fact]
    public void Small_things_must_stand_fully_on_a_table_top()
    {
        var table = At("table", 2, 2);
        var (x, z) = TopOf(table);
        var top = RoomLayout.SurfaceArea(table, ItemDefinitions.Find("table")!);

        Assert.Single(Check(Decor("plantSmall1", 6, 6)));                                // on the floor
        Assert.Single(Check(At("loungeSofa", 2, 2), Decor("plantSmall1", 2.5f, 2.5f)));   // on a sofa
        Assert.Empty(Check(table, Decor("plantSmall1", x, z)));                          // on the table
        Assert.Single(Check(table, Decor("laptop", top.MaxX, z)));                       // half off the edge
    }

    [Fact]
    public void Small_things_may_not_stand_in_each_other()
    {
        var table = At("table", 2, 2);
        var (x, z) = TopOf(table);

        Assert.Single(Check(table, Decor("laptop", x, z), Decor("plantSmall1", x, z)));
    }

    [Fact]
    public void Nothing_may_stick_out_of_the_room()
    {
        var problems = Check(At("ph-potted_plant_01", 9.9f, 7.9f), At("table", -0.5f, 2));

        Assert.Equal(2, problems.Count);
        Assert.All(problems, p => Assert.Contains("ragt aus dem Raum", p.Message));
    }

    [Fact]
    public void Items_must_stand_on_the_grid_and_turn_in_quarter_steps()
    {
        var offGrid = new RoomItemDto("table", new Vector3Dto(3.13f, 0f, 3.37f), 0f);
        var slanted = At("chair", 5, 5) with { Rotation = 45f };

        var problems = Check(offGrid, slanted);

        Assert.Equal(2, problems.Count);
        Assert.Empty(Check(RoomLayout.Snap(offGrid)));
    }

    [Fact]
    public void The_door_stays_free()
    {
        var door = Assert.Single(Room.Reserved);

        var problems = Check(At("table", door.X * BuildGrid.CellSize, door.Z * BuildGrid.CellSize));

        Assert.Contains("Eingang", Assert.Single(problems).Message);
    }

    [Fact]
    public void Paintings_hang_on_walls_only()
    {
        Assert.Single(Check(At("ph-hanging_picture_frame_01", 3, 3)));
        Assert.Empty(Check(At("ph-hanging_picture_frame_01", 9.75f, 3, quarter: 1)));
    }

    [Fact]
    public void Rugs_lie_under_furniture_and_ceiling_lamps_hang_above_it()
    {
        Assert.Empty(Check(At("rugRectangle", 1.5f, 1.5f), At("table", 2, 2), At("ph-Chandelier_01", 2, 2)));
    }

    [Fact]
    public void A_seat_nobody_can_reach_is_rejected()
    {
        // Chair in the corner, fenced in by shelves on the two open sides.
        var problems = Check(
            At("chair", 0, 0),
            At("bookcaseClosedWide", 0, 1),
            At("bookcaseClosed", 1, 0, quarter: 1));

        Assert.Contains(problems, p => p.Index == 0 && p.Message.Contains("zugestellt"));
    }

    [Fact]
    public void Pools_are_water_not_obstacles()
    {
        var pool = At("custom-pool-6x3", 1, 1);

        Assert.Empty(RoomLayout.BlockedTiles([pool]));
        Assert.Equal(18, RoomLayout.WaterTiles([pool]).Count);
    }

    [Fact]
    public void Furniture_blocks_the_walking_tiles_it_covers()
    {
        var blocked = RoomLayout.BlockedTiles([At("table", 2, 2), At("rugRectangle", 5, 5)]);

        Assert.Equal([(2, 2), (3, 2)], blocked.OrderBy(t => t).ToArray());
    }

    [Fact]
    public void Chairs_are_moved_up_to_their_table_and_lined_up_at_small_tables()
    {
        // Kenney chairs face -Z: this one stands north of a small table, 50 cm away and shifted to the side.
        var table = At("sideTable", 2, 2);
        var chair = At("chair", 2.25f, 3.25f);

        var fixedItems = RoomLayoutFixer.Legalize("cafe", 10, 8, [table, chair]);

        var tableCells = RoomLayout.Footprint(fixedItems[0], ItemDefinitions.Find("sideTable")!);
        var chairCells = RoomLayout.Footprint(fixedItems[1], ItemDefinitions.Find("chair")!);
        Assert.Equal(tableCells.ZMax, chairCells.Z);   // right at the table's edge
        Assert.Equal(tableCells.X + (tableCells.Width - chairCells.Width) / 2, chairCells.X);   // in the middle of its side
    }
}
