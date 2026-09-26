using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reconnect.Modules.City.Public;
using Reconnect.Modules.Identity.Public;
using Reconnect.Modules.Rooms.Domain;

namespace Reconnect.Modules.Rooms.Infrastructure.Seeding;

/// <summary>
/// DEVELOPMENT ONLY: five fully furnished public rooms owned by the dev admin, so the app has
/// something to explore. Runs after the dev admin exists; re-running updates size, layout and theme.
/// Coordinates in metres: (0,0) is the front corner the camera looks from, back walls at z = depth
/// and x = width. Item ids = Kenney Furniture Kit models (client scale 0.2 ≈ real-life size),
/// "custom-*" are built by the client (pool, columns …), "game-*" are minigame stations.
/// Small items (laptops, lamps, coffee machines) placed on furniture are stacked by the client.
/// </summary>
internal static partial class ShowcaseRooms
{
    // Kenney models face -Z ("south", towards the camera side) at rotation 0.
    internal const float South = 0f;
    internal const float West = 90f;
    internal const float North = 180f;
    internal const float East = 270f;
    private const float PolyHavenForward = 180f;

    public static async Task SeedIfEnabledAsync(IServiceProvider services, CancellationToken ct)
    {
        var environment = services.GetRequiredService<IHostEnvironment>();
        var configuration = services.GetRequiredService<IConfiguration>();
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("Showcase:Enabled"))
        {
            return;
        }

        var adminName = configuration["DevAdmin:UserName"] ?? "Admin";
        var adminId = await services.GetRequiredService<IUserDirectory>().FindIdByUserNameAsync(adminName, ct);
        if (adminId is not { } ownerId)
        {
            return;   // DevAdmin disabled – nobody to own the rooms
        }

        var db = services.GetRequiredService<RoomsDbContext>();
        var definitions = Definitions().ToList();

        // Showcase buildings that should only contain their showcase room (Prime Tower = the sky lounge):
        // remove other rooms of the dev admin there, e.g. renamed or test rooms from earlier versions.
        var names = definitions.Select(d => d.Name).ToList();
        await db.Rooms
            .Where(r => r.OwnerId == ownerId && ExclusiveBuildings.Contains(r.BuildingId) && !names.Contains(r.Name))
            .ExecuteDeleteAsync(ct);

        foreach (var definition in definitions)
        {
            var room = await db.Rooms.SingleOrDefaultAsync(r => r.OwnerId == ownerId && r.Name == definition.Name, ct);
            if (room is null)
            {
                room = Room.Create(ownerId, definition.BuildingId, definition.Name, isPublic: true, definition.Theme);
                db.Rooms.Add(room);
            }
            room.ChangeTheme(definition.Theme);
            room.Resize(definition.Width, definition.Depth);
            room.ReplaceLayout(definition.Layout);
        }
        await db.SaveChangesAsync(ct);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ShowcaseRooms));
        LogSeeded(logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "DEV ONLY: showcase rooms are up to date.")]
    private static partial void LogSeeded(ILogger logger);

    private sealed record Definition(string Name, Guid BuildingId, string Theme, int Width, int Depth, List<RoomItem> Layout);

    private static readonly Guid[] ExclusiveBuildings = [ZurichBuildings.PrimeTowerId];

    private static IEnumerable<Definition> Definitions() =>
    [
        new("ETH Bibliothek", ZurichBuildings.EthHauptgebaeudeId, RoomThemes.Library, 22, 16, Library()),
        new("Opern-Foyer", ZurichBuildings.OpernhausId, RoomThemes.Opera, 20, 14, OperaFoyer()),
        new("Café Limmat", ZurichBuildings.GrossmuensterId, RoomThemes.Cafe, 14, 10, Cafe()),
        new("Kunst-Atelier", ZurichBuildings.KunsthausId, RoomThemes.Atelier, 14, 12, Atelier()),
        new("Seebad Utoquai", ZurichBuildings.SeebadUtoquaiId, RoomThemes.Pool, 22, 16, Seebad()),
    ];

    /// <summary>
    /// Open-air bath on the lake: a 12 × 6 m pool one can swim in, sun loungers with parasols on both sides, a kiosk
    /// bar with stools, outdoor tables and planters, string lights over the deck.
    /// </summary>
    private static List<RoomItem> Seebad()
    {
        var items = new List<RoomItem>
        {
            Item("custom-pool-12x6", 11f, 8f),
            // Kiosk bar in the north-east corner.
            Item("custom-skybar", 19f, 14.2f, South),
            Ph("bar_chair_round_01", 17.8f, 13f, North), Ph("bar_chair_round_01", 19f, 13f, North), Ph("bar_chair_round_01", 20.2f, 13f, North),
            // Tables in the shade on the west side.
            Ph("outdoor_table_chair_set_01", 2.4f, 12.5f), Ph("outdoor_table_chair_set_01", 2.4f, 8f),
            Item("custom-parasol", 2.4f, 10.3f),
            Ph("planter_box_01", 0.7f, 15.2f), Ph("planter_box_02", 4.6f, 15.2f), Ph("planter_box_01", 14.6f, 15.2f),
            Ph("planter_box_02", 21.2f, 0.8f), Ph("planter_box_01", 0.7f, 0.8f),
            Ph("potted_plant_04", 21.3f, 8f), Ph("pachira_aquatica_01", 21.2f, 4.4f),
            Ph("street_lamp_02", 0.6f, 4.4f), Ph("street_lamp_02", 21.4f, 11.2f),
            Item("custom-lightstring", 11f, 8f),
            Item("game-tictactoe", 3f, 3.6f), Item("chairCushion", 1.9f, 3.6f, East), Item("chairCushion", 4.1f, 3.6f, West),
        };
        // Sun loungers facing the pool, parasols between them.
        for (var i = 0; i < 5; i++)
        {
            var x = 6.4f + i * 2.3f;
            items.Add(Item("custom-lounger", x, 2.4f, North));
            if (i < 4)
            {
                items.Add(Item("custom-parasol", x + 1.15f, 2.2f));
            }
        }
        for (var i = 0; i < 4; i++)
        {
            items.Add(Item("custom-lounger", 7.6f + i * 2.3f, 13.4f, South));
        }
        return items;
    }

    /// <summary>
    /// Reading room: worn wooden bookcases along the walls and in aisles, long reading tables with lamps, books and a
    /// chess game, study desks, info desk, a grandfather clock, sofa corner and the quiz corner.
    /// </summary>
    private static List<RoomItem> Library()
    {
        var items = new List<RoomItem>
        {
            // Columns along the central aisle and rugs in it.
            Item("custom-column", 9.4f, 12.2f), Item("custom-column", 9.4f, 8.6f),
            Item("custom-column", 12.6f, 12.2f), Item("custom-column", 12.6f, 8.6f),
            Item("custom-rug", 11f, 13.6f, West), Item("custom-rug", 11f, 10.4f, West),
            // Info desk with computer.
            Ph("metal_office_desk", 11f, 5.8f, South), Item("computerScreen", 10.6f, 5.9f), Item("computerKeyboard", 10.6f, 5.5f),
            Ph("GreenChair_01", 11f, 6.9f, South), Ph("desk_lamp_arm_01", 11.7f, 5.7f),
            Ph("vintage_grandfather_clock_01", 8.2f, 15.5f, South),
            // Quiz TV on the east wall with benches in front.
            Item("game-quiz", 21.6f, 8f, West),
            Ph("painted_wooden_bench", 19.6f, 7.3f, East), Ph("painted_wooden_bench", 19.6f, 8.7f, East),
            // Sofa corner in the south-east.
            Item("custom-rug", 17.2f, 2.5f),
            Ph("Sofa_01", 17.2f, 1.0f, North), Ph("ArmChair_01", 15.2f, 2.8f, East), Ph("ArmChair_01", 19.2f, 2.8f, West),
            Ph("CoffeeTable_01", 17.2f, 2.8f), Ph("book_encyclopedia_set_01", 17.3f, 2.8f), Ph("mantel_clock_01", 16.8f, 2.8f),
            Ph("potted_plant_02", 20.8f, 0.7f), Ph("potted_plant_02", 14.1f, 0.7f),
            // Plants in the corners.
            Ph("potted_plant_01", 0.6f, 15.3f), Ph("potted_plant_01", 21.3f, 15.3f), Ph("potted_plant_04", 0.6f, 0.6f),
        };

        // Bookcases along the north wall (a gap in the middle for the door) and the east wall.
        items.AddRange(Row("ph-wooden_bookshelf_worn", x: 1.3f, z: 15.65f, dx: 1.4f, dz: 0f, count: 5, PolyHaven(South)));
        items.AddRange(Row("ph-wooden_bookshelf_worn", x: 14.3f, z: 15.65f, dx: 1.4f, dz: 0f, count: 5, PolyHaven(South)));
        items.AddRange(Row("ph-wooden_bookshelf_worn", x: 21.65f, z: 14.2f, dx: 0f, dz: -1.4f, count: 3, PolyHaven(West)));
        items.AddRange(Row("ph-wooden_bookshelf_worn", x: 21.65f, z: 4.9f, dx: 0f, dz: -1.4f, count: 2, PolyHaven(West)));

        // Freestanding back-to-back bookcase rows forming aisles (north-west quadrant).
        foreach (var z in new[] { 12.6f, 9.6f })
        {
            items.AddRange(Row("ph-wooden_bookshelf_worn", x: 1.3f, z: z + 0.3f, dx: 1.4f, dz: 0f, count: 5, PolyHaven(South)));
            items.AddRange(Row("ph-wooden_bookshelf_worn", x: 1.3f, z: z - 0.3f, dx: 1.4f, dz: 0f, count: 5, PolyHaven(North)));
        }

        // Reading tables (north-east) with four chairs, a lamp and books / a chess game on each.
        var tables = new[] { (15f, 11.8f), (18.2f, 11.8f), (15f, 8.9f), (18.2f, 8.9f) };
        for (var i = 0; i < tables.Length; i++)
        {
            var (x, z) = tables[i];
            items.Add(Ph("WoodenTable_01", x, z));
            items.Add(Ph("desk_lamp_arm_01", x + 0.6f, z));
            items.Add(i % 2 == 0 ? Ph("chess_set", x - 0.25f, z) : Ph("book_encyclopedia_set_01", x - 0.3f, z));
            items.Add(Item(x > 16 ? "laptop" : "books", x + 0.1f, z + 0.1f));
            items.Add(Ph("dining_chair_02", x - 0.45f, z + 0.75f, South));
            items.Add(Ph("dining_chair_02", x + 0.45f, z + 0.75f, South));
            items.Add(Ph("dining_chair_02", x - 0.45f, z - 0.75f, North));
            items.Add(Ph("dining_chair_02", x + 0.45f, z - 0.75f, North));
        }

        // Study desks (south-west) with desk chairs, laptops and lamps.
        foreach (var x in new[] { 2.2f, 4.6f, 7f })
        {
            items.Add(Ph("metal_office_desk", x, 5.2f, South));
            items.Add(Item("laptop", x, 5.2f));
            items.Add(Ph("desk_lamp_arm_01", x + 0.6f, 5.2f));
            items.Add(Ph("GreenChair_01", x, 4.2f, North));
        }
        return items;
    }

    /// <summary>
    /// Grand foyer: column rows, crystal chandeliers, a bar with candle holders, classic seating groups, a marble bust,
    /// paintings on the east wall, dance floor and cloakroom.
    /// </summary>
    private static List<RoomItem> OperaFoyer()
    {
        var items = new List<RoomItem>
        {
            // Seating group north-west.
            Item("custom-ruground", 4f, 11.8f),
            Ph("Sofa_01", 4f, 13.3f, South), Ph("CoffeeTable_01", 4f, 11.9f), Ph("brass_candleholders", 4f, 11.9f),
            Ph("ArmChair_01", 2f, 11.6f, East), Ph("ArmChair_01", 6f, 11.6f, West),
            // Seating group east.
            Item("custom-ruground", 17.6f, 6.8f, West),
            Ph("Sofa_01", 19.3f, 6.8f, West), Ph("CoffeeTable_01", 17.8f, 6.8f, West), Ph("antique_ceramic_vase_01", 17.8f, 6.8f),
            Ph("ArmChair_01", 17.6f, 4.9f, North), Ph("ArmChair_01", 17.6f, 8.7f, South),
            // Cloakroom near the entrance (west) and a marble bust on a side table.
            Ph("side_table_tall_01", 0.6f, 7.5f, East), Ph("marble_bust_01", 0.6f, 7.5f, East),
            Item("coatRackStanding", 0.6f, 5.8f), Item("coatRackStanding", 0.6f, 9.2f),
            // Tic-tac-toe table in the south-east.
            Item("game-tictactoe", 16.5f, 2.2f), Ph("dining_chair_02", 15.4f, 2.2f, East), Ph("dining_chair_02", 17.6f, 2.2f, West),
            // Plants in the corners, a grandfather clock by the bar.
            Ph("potted_plant_01", 0.6f, 13.3f), Ph("potted_plant_01", 19.3f, 13.3f), Ph("potted_plant_02", 0.6f, 0.6f), Ph("potted_plant_02", 19.4f, 0.6f),
            Ph("vintage_grandfather_clock_01", 9.6f, 13.5f, South),
            // Paintings on the east wall.
            Ph("hanging_picture_frame_01", 19.95f, 11.2f, West, y: 1.1f), Ph("hanging_picture_frame_02", 19.95f, 2.8f, West, y: 1.3f),
            // Speakers at the dance floor.
            Item("speaker", 7.1f, 9.4f), Item("speaker", 12.9f, 9.4f), Item("speaker", 7.1f, 4.6f), Item("speaker", 12.9f, 4.6f),
            // Crystal chandeliers over the dance floor and the seating groups.
            Ph("Chandelier_01", 10f, 7f, y: 1.75f), Ph("Chandelier_01", 4f, 12f, y: 1.75f), Ph("Chandelier_01", 17.6f, 6.8f, y: 1.75f),
        };

        // Two rows of columns.
        items.AddRange(Row("custom-column", x: 4f, z: 10.4f, dx: 4f, dz: 0f, count: 4));
        items.AddRange(Row("custom-column", x: 4f, z: 3.6f, dx: 4f, dz: 0f, count: 4));

        // Dance floor: 3×3 square rugs.
        foreach (var z in new[] { 5.2f, 7f, 8.8f })
        {
            items.AddRange(Row("rugSquare", x: 8.2f, z: z, dx: 1.8f, dz: 0f, count: 3));
        }

        // Bar along the north wall: back bar, counter with candles, bar chairs.
        items.AddRange(Row("kitchenCabinet", x: 11.4f, z: 13.5f, dx: 0.87f, dz: 0f, count: 7, South));
        items.Add(Item("kitchenCoffeeMachine", 12.3f, 13.5f));
        items.Add(Ph("brass_candleholders", 14f, 13.5f));
        items.Add(Ph("ceramic_vase_03", 15.8f, 13.5f));
        items.AddRange(Row("kitchenBar", x: 11.4f, z: 12.3f, dx: 0.87f, dz: 0f, count: 7, South));
        items.Add(Ph("brass_candleholders", 13.2f, 12.3f));
        items.AddRange(Row("ph-bar_chair_round_01", x: 11.6f, z: 11.4f, dx: 1.05f, dz: 0f, count: 6, PolyHaven(North)));
        return items;
    }

    /// <summary>Bistro: counter with back bar and bar chairs, marble bistro tables, bench corner, coffee cart, menu board, quiz TV.</summary>
    private static List<RoomItem> Cafe()
    {
        var items = new List<RoomItem>
        {
            Item("kitchenFridgeLarge", 5f, 9.5f, South),
            Item("kitchenCoffeeMachine", 6.9f, 9.5f), Item("kitchenCoffeeMachine", 7.8f, 9.5f), Item("toaster", 9.6f, 9.5f),
            Ph("ceramic_vase_03", 11.4f, 9.5f),
            Item("game-quiz", 13.6f, 4.8f, West),
            Ph("painted_wooden_bench", 11.9f, 4.1f, East), Ph("painted_wooden_bench", 11.9f, 5.5f, East),
            Item("custom-rug", 2.8f, 4.8f, West),
            Item("coatRackStanding", 0.5f, 0.5f),
            Ph("CoffeeCart_01", 3.2f, 9.2f, South),
            Ph("standing_chalkboard_01", 11.6f, 0.6f, South),
            Ph("wooden_display_shelves_01", 13.6f, 8.2f, West), Ph("tea_set_01", 13.6f, 8.2f),
            Ph("potted_plant_02", 0.5f, 9.4f), Ph("potted_plant_04", 13.5f, 9.4f), Ph("potted_plant_02", 13.5f, 0.5f),
            Item("lampRoundFloor", 0.5f, 4.8f), Item("lampRoundFloor", 10.3f, 0.5f),
            Ph("hanging_picture_frame_03", 13.95f, 2.4f, West, y: 1.3f),
        };

        // Back bar, upper cabinets on the wall, counter and bar chairs.
        items.AddRange(Row("kitchenCabinet", x: 6.4f, z: 9.5f, dx: 0.87f, dz: 0f, count: 6, South));
        items.AddRange(Row("kitchenCabinetUpper", x: 6.4f, z: 9.75f, dx: 0.87f, dz: 0f, count: 6, South, y: 1.55f));
        items.AddRange(Row("kitchenBar", x: 6.4f, z: 8.1f, dx: 0.87f, dz: 0f, count: 6, South));
        items.AddRange(Row("ph-bar_chair_round_01", x: 6.6f, z: 7.2f, dx: 1.05f, dz: 0f, count: 5, PolyHaven(North)));

        // Bistro tables with two chairs and a small vase each.
        foreach (var (x, z) in new[] { (1.8f, 7.4f), (1.8f, 4.8f), (1.8f, 2f), (4.9f, 5.4f), (4.9f, 2.4f), (8.2f, 4.4f), (8.2f, 1.6f) })
        {
            items.Add(Ph("gallinera_table", x, z));
            items.Add(Ph(z > 5 ? "ceramic_vase_03" : "antique_ceramic_vase_01", x, z));
            items.Add(Ph("gallinera_chair", x - 0.7f, z, East));
            items.Add(Ph("gallinera_chair", x + 0.7f, z, West));
        }
        return items;
    }

    /// <summary>Studio: easels, work tables, a bronze sculpture, paintings, shelves, sofa corner, computer desk, tic-tac-toe.</summary>
    private static List<RoomItem> Atelier()
    {
        var items = new List<RoomItem>
        {
            Item("custom-easel", 2.2f, 8.4f), Item("custom-easel", 4.4f, 8.8f), Item("custom-easel", 6.6f, 8.4f),
            Ph("dining_chair_02", 2.2f, 7.5f, North), Ph("dining_chair_02", 4.4f, 7.9f, North), Ph("dining_chair_02", 6.6f, 7.5f, North),
            Ph("WoodenTable_01", 3.2f, 4.4f), Item("books", 2.8f, 4.4f), Ph("ceramic_vase_03", 3.7f, 4.4f),
            Ph("WoodenTable_01", 6.4f, 4.4f), Item("laptop", 6.2f, 4.4f), Ph("desk_lamp_arm_01", 7f, 4.4f),
            Ph("dining_chair_02", 3.2f, 3.5f, North), Ph("dining_chair_02", 6.4f, 3.5f, North),
            Item("custom-rug", 4.8f, 4.4f),
            // Sculpture on a plinth in the middle of the studio.
            Ph("side_table_tall_01", 9.4f, 7.6f), Ph("bronze_ray_statue", 9.4f, 7.6f),
            // Sofa corner in the east.
            Item("custom-rug", 11.8f, 9.4f, West),
            Ph("sofa_03", 13f, 9.4f, West), Ph("mid_century_lounge_chair", 11.3f, 11f, South), Ph("modern_coffee_table_01", 11.8f, 9.4f, West),
            Ph("standing_picture_frame_01", 11.8f, 9.4f),
            // Computer desk and tic-tac-toe.
            Ph("metal_office_desk", 13f, 3f, West), Item("computerScreen", 13.2f, 3f), Ph("GreenChair_01", 11.9f, 3f, East),
            Item("game-tictactoe", 9.8f, 4.6f), Ph("dining_chair_02", 8.7f, 4.6f, East), Ph("dining_chair_02", 10.9f, 4.6f, West),
            // Paintings on the east wall, plants.
            Ph("hanging_picture_frame_01", 13.95f, 6.4f, West, y: 1.1f), Ph("hanging_picture_frame_02", 13.95f, 1.2f, West, y: 1.3f),
            Ph("potted_plant_01", 0.5f, 11.3f), Ph("potted_plant_04", 0.5f, 0.5f), Ph("potted_plant_02", 13.4f, 0.5f),
        };

        // Shelves along the north wall with things on top.
        items.AddRange(Row("ph-Shelf_01", x: 1.1f, z: 11.8f, dx: 1.05f, dz: 0f, count: 4, PolyHaven(South)));
        items.AddRange(Row("ph-wooden_display_shelves_01", x: 5.9f, z: 11.75f, dx: 1.15f, dz: 0f, count: 2, PolyHaven(South)));
        items.Add(Ph("ceramic_vase_03", 5.9f, 11.75f));
        items.Add(Item("radio", 7.05f, 11.75f));
        return items;
    }

    internal static IEnumerable<RoomItem> Row(string itemId, float x, float z, float dx, float dz, int count,
        float rotation = South, float y = 0f) =>
        Enumerable.Range(0, count).Select(i => Item(itemId, x + dx * i, z + dz * i, rotation, y));

    /// <summary>
    /// Realistic Poly Haven model (client id "ph-" + name). <paramref name="facing"/> uses the same
    /// convention as Kenney models; Poly Haven models face +Z, hence the extra half turn.
    /// </summary>
    internal static RoomItem Ph(string model, float x, float z, float facing = South, float y = 0f) =>
        Item("ph-" + model, x, z, PolyHaven(facing), y);

    /// <summary>Rotation for a Poly Haven model that should face <paramref name="facing"/> (they face +Z, Kenney -Z).</summary>
    internal static float PolyHaven(float facing) => (facing + PolyHavenForward) % 360f;

    internal static RoomItem Item(string itemId, float x, float z, float rotation = South, float y = 0f) =>
        new() { ItemId = itemId, Position = new Position3 { X = x, Y = y, Z = z }, Rotation = rotation };
}
