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
    private const float South = 0f;
    private const float West = 90f;
    private const float North = 180f;
    private const float East = 270f;

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
        foreach (var definition in Definitions())
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

    private static IEnumerable<Definition> Definitions() =>
    [
        new("Rooftop Lounge", ZurichBuildings.PrimeTowerId, RoomThemes.Rooftop, 18, 14, Rooftop()),
        new("ETH Bibliothek", ZurichBuildings.EthHauptgebaeudeId, RoomThemes.Library, 22, 16, Library()),
        new("Opern-Foyer", ZurichBuildings.OpernhausId, RoomThemes.Opera, 20, 14, OperaFoyer()),
        new("Café Limmat", ZurichBuildings.GrossmuensterId, RoomThemes.Cafe, 14, 10, Cafe()),
        new("Kunst-Atelier", ZurichBuildings.KunsthausId, RoomThemes.Atelier, 14, 12, Atelier()),
    ];

    /// <summary>Open roof terrace on the Prime Tower: pool, loungers, bar, fire pit, DJ corner.</summary>
    private static List<RoomItem> Rooftop()
    {
        var items = new List<RoomItem>
        {
            // Pool with loungers facing it and a parasol.
            Item("custom-pool", 6f, 10.5f),
            Item("custom-parasol", 9.9f, 7.7f),
            Item("tableCoffeeSquare", 5.75f, 7.6f),
            // Bar along the east edge, back bar with coffee machine and fridge.
            Item("kitchenFridgeLarge", 17.3f, 12.6f, West),
            Item("kitchenCoffeeMachine", 17.3f, 10.8f), Item("kitchenBlender", 17.3f, 9.2f),
            Item("pottedPlant", 17.3f, 6.6f),
            // Lounge in the south-west corner.
            Item("rugRound", 3.2f, 3.2f),
            Item("loungeSofa", 3.2f, 0.9f, North), Item("loungeSofa", 0.9f, 3.2f, East),
            Item("tableCoffeeGlass", 3.3f, 3.1f),
            Item("pottedPlant", 0.6f, 0.6f), Item("lampRoundFloor", 0.6f, 5.2f), Item("lampRoundFloor", 5.4f, 0.6f),
            // Fire pit with benches around it.
            Item("custom-firepit", 9f, 3.4f),
            Item("benchCushion", 9f, 1.9f, North), Item("benchCushion", 9f, 4.9f, South),
            Item("benchCushion", 7.5f, 3.4f, East), Item("benchCushion", 10.5f, 3.4f, West),
            // DJ corner in the north-east.
            Item("rugSquare", 13.8f, 11.9f),
            Item("desk", 13.8f, 13.0f, South), Item("laptop", 13.8f, 13.0f), Item("speakerSmall", 14.4f, 13.0f),
            Item("speaker", 12.3f, 13.2f), Item("speaker", 15.3f, 13.2f),
            // Tic-tac-toe table.
            Item("game-tictactoe", 14f, 4.4f), Item("chairCushion", 12.9f, 4.4f, East), Item("chairCushion", 15.1f, 4.4f, West),
            // Fairy lights along the north and west edge, planters along the railing.
            Item("custom-lightstring", 3f, 13.6f), Item("custom-lightstring", 7f, 13.6f), Item("custom-lightstring", 11f, 13.6f),
            Item("custom-lightstring", 0.4f, 10f, West), Item("custom-lightstring", 0.4f, 6.5f, West),
            Item("custom-planter", 1f, 13.3f), Item("custom-planter", 10.3f, 13.3f), Item("custom-planter", 0.7f, 8.3f, West),
            Item("custom-planter", 12f, 0.5f), Item("custom-planter", 16.5f, 0.5f),
        };

        // Four sun loungers facing the pool.
        items.AddRange(Row("loungeChairRelax", x: 3.6f, z: 7.6f, dx: 1.3f, dz: 0f, count: 2, North));
        items.AddRange(Row("loungeChairRelax", x: 7.1f, z: 7.6f, dx: 1.3f, dz: 0f, count: 2, North));
        // Bar counter with stools.
        items.AddRange(Row("kitchenBar", x: 15.9f, z: 7.6f, dx: 0f, dz: 0.87f, count: 5, West));
        items.AddRange(Row("stoolBar", x: 14.9f, z: 7.8f, dx: 0f, dz: 1.05f, count: 4, East));
        items.AddRange(Row("kitchenCabinet", x: 17.4f, z: 8.3f, dx: 0f, dz: 0.87f, count: 4, West));
        return items;
    }

    /// <summary>Reading room: shelf aisles, long reading tables with lamps, study desks, info desk, sofa corner.</summary>
    private static List<RoomItem> Library()
    {
        var items = new List<RoomItem>
        {
            // Columns along the central aisle and rugs in it.
            Item("custom-column", 9.4f, 12.2f), Item("custom-column", 9.4f, 8.6f),
            Item("custom-column", 12.6f, 12.2f), Item("custom-column", 12.6f, 8.6f),
            Item("rugRectangle", 11f, 13.6f, West), Item("rugRectangle", 11f, 10.4f, West),
            // Info desk with computer.
            Item("deskCorner", 11f, 5.8f, South), Item("computerScreen", 10.6f, 5.9f), Item("computerKeyboard", 10.6f, 5.5f),
            Item("chairDesk", 11.4f, 6.9f, South), Item("lampSquareTable", 11.6f, 5.6f),
            // Quiz TV on the east wall with benches in front.
            Item("game-quiz", 21.6f, 8f, West),
            Item("benchCushion", 19.6f, 7.4f, East), Item("benchCushion", 19.6f, 8.6f, East),
            // Sofa corner in the south-east.
            Item("rugRectangle", 17.2f, 2.5f),
            Item("loungeSofa", 17.2f, 1.0f, North), Item("loungeChair", 15.2f, 2.8f, East), Item("loungeChair", 19.2f, 2.8f, West),
            Item("tableCoffee", 17.2f, 2.8f), Item("books", 17f, 2.8f),
            Item("lampRoundFloor", 20.6f, 0.7f), Item("pottedPlant", 14.1f, 0.7f),
            // Plants in the corners.
            Item("pottedPlant", 0.6f, 15.4f), Item("pottedPlant", 21.4f, 15.4f), Item("pottedPlant", 0.6f, 0.6f),
        };

        // Wall shelves along the north wall (a gap in the middle for the door) and the east wall.
        items.AddRange(Row("bookcaseClosedWide", x: 1.4f, z: 15.7f, dx: 1.6f, dz: 0f, count: 5, South));
        items.AddRange(Row("bookcaseClosedWide", x: 13.4f, z: 15.7f, dx: 1.6f, dz: 0f, count: 5, South));
        items.AddRange(Row("bookcaseClosedWide", x: 21.7f, z: 14.2f, dx: 0f, dz: -1.6f, count: 3, West));
        items.AddRange(Row("bookcaseClosedWide", x: 21.7f, z: 4.9f, dx: 0f, dz: -1.6f, count: 2, West));

        // Freestanding back-to-back shelf rows forming aisles (north-west quadrant).
        foreach (var z in new[] { 12.6f, 9.6f })
        {
            items.AddRange(Row("bookcaseOpen", x: 1.2f, z: z + 0.26f, dx: 0.8f, dz: 0f, count: 8, South));
            items.AddRange(Row("bookcaseOpen", x: 1.2f, z: z - 0.26f, dx: 0.8f, dz: 0f, count: 8, North));
        }

        // Reading tables (north-east) with four chairs, a lamp and books/laptops on each.
        foreach (var (x, z) in new[] { (15f, 11.8f), (18.2f, 11.8f), (15f, 8.9f), (18.2f, 8.9f) })
        {
            items.Add(Item("table", x, z));
            items.Add(Item("lampSquareTable", x, z));
            items.Add(Item(x > 16 ? "laptop" : "books", x + 0.5f, z));
            items.Add(Item("books", x - 0.5f, z));
            items.Add(Item("chair", x - 0.45f, z + 0.8f, South));
            items.Add(Item("chair", x + 0.45f, z + 0.8f, South));
            items.Add(Item("chair", x - 0.45f, z - 0.8f, North));
            items.Add(Item("chair", x + 0.45f, z - 0.8f, North));
        }

        // Study desks (south-west) with desk chairs, laptops and lamps.
        foreach (var x in new[] { 2.2f, 4.4f, 6.6f })
        {
            items.Add(Item("desk", x, 5.2f, South));
            items.Add(Item("laptop", x, 5.2f));
            items.Add(Item("lampSquareTable", x + 0.5f, 5.2f));
            items.Add(Item("chairDesk", x, 4.3f, North));
        }
        return items;
    }

    /// <summary>Grand foyer: column rows, bar, seating groups, dance floor, cloakroom.</summary>
    private static List<RoomItem> OperaFoyer()
    {
        var items = new List<RoomItem>
        {
            // Seating group north-west.
            Item("rugRounded", 4f, 11.8f),
            Item("loungeDesignSofa", 4f, 13.4f, South), Item("tableCoffeeGlassSquare", 4f, 11.9f),
            Item("loungeDesignChair", 2f, 11.6f, East), Item("loungeDesignChair", 6f, 11.6f, West),
            // Seating group east.
            Item("rugRounded", 17.6f, 6.8f, West),
            Item("loungeDesignSofa", 19.4f, 6.8f, West), Item("tableCoffeeGlass", 17.8f, 6.8f),
            Item("loungeDesignChair", 17.6f, 4.9f, North), Item("loungeDesignChair", 17.6f, 8.7f, South),
            // Cloakroom near the entrance (west).
            Item("sideTable", 0.6f, 7.5f, East),
            Item("coatRackStanding", 0.6f, 5.8f), Item("coatRackStanding", 0.6f, 9.2f),
            // Tic-tac-toe table in the south-east.
            Item("game-tictactoe", 16.5f, 2.2f), Item("chairCushion", 15.4f, 2.2f, East), Item("chairCushion", 17.6f, 2.2f, West),
            // Plants in the corners, lamps at the walls.
            Item("pottedPlant", 0.6f, 13.4f), Item("pottedPlant", 19.4f, 13.4f), Item("pottedPlant", 0.6f, 0.6f), Item("pottedPlant", 19.4f, 0.6f),
            Item("lampRoundFloor", 8.4f, 13.5f), Item("lampRoundFloor", 19.5f, 10.6f), Item("lampRoundFloor", 19.5f, 3.2f),
            // Speakers at the dance floor.
            Item("speaker", 7.1f, 9.4f), Item("speaker", 12.9f, 9.4f), Item("speaker", 7.1f, 4.6f), Item("speaker", 12.9f, 4.6f),
        };

        // Two rows of columns.
        items.AddRange(Row("custom-column", x: 4f, z: 10.4f, dx: 4f, dz: 0f, count: 4));
        items.AddRange(Row("custom-column", x: 4f, z: 3.6f, dx: 4f, dz: 0f, count: 4));

        // Dance floor: 3×3 square rugs.
        foreach (var z in new[] { 5.2f, 7f, 8.8f })
        {
            items.AddRange(Row("rugSquare", x: 8.2f, z: z, dx: 1.8f, dz: 0f, count: 3));
        }

        // Bar along the north wall: back bar, counter, stools.
        items.AddRange(Row("kitchenCabinet", x: 11.4f, z: 13.5f, dx: 0.87f, dz: 0f, count: 7, South));
        items.Add(Item("kitchenCoffeeMachine", 12.3f, 13.5f));
        items.Add(Item("kitchenBlender", 14f, 13.5f));
        items.Add(Item("kitchenMicrowave", 15.8f, 13.5f));
        items.AddRange(Row("kitchenBar", x: 11.4f, z: 12.3f, dx: 0.87f, dz: 0f, count: 7, South));
        items.AddRange(Row("stoolBar", x: 11.6f, z: 11.4f, dx: 1.05f, dz: 0f, count: 6, North));
        return items;
    }

    /// <summary>Bistro: counter with back bar, bar stools, café tables, quiz TV.</summary>
    private static List<RoomItem> Cafe()
    {
        var items = new List<RoomItem>
        {
            Item("kitchenFridgeLarge", 5f, 9.5f, South),
            Item("kitchenCoffeeMachine", 6.9f, 9.5f), Item("kitchenCoffeeMachine", 7.8f, 9.5f), Item("toaster", 9.6f, 9.5f),
            Item("kitchenBlender", 11.4f, 9.5f),
            Item("game-quiz", 13.6f, 4.8f, West),
            Item("benchCushion", 11.8f, 4.2f, East), Item("benchCushion", 11.8f, 5.4f, East),
            Item("rugRectangle", 2.8f, 4.8f, West),
            Item("coatRackStanding", 0.5f, 0.5f),
            Item("pottedPlant", 0.5f, 9.4f), Item("pottedPlant", 13.5f, 9.4f), Item("pottedPlant", 13.5f, 0.5f),
            Item("lampRoundFloor", 0.5f, 4.8f), Item("lampRoundFloor", 10.8f, 0.5f),
        };

        // Back bar, upper cabinets on the wall, counter and stools.
        items.AddRange(Row("kitchenCabinet", x: 6.4f, z: 9.5f, dx: 0.87f, dz: 0f, count: 6, South));
        items.AddRange(Row("kitchenCabinetUpper", x: 6.4f, z: 9.75f, dx: 0.87f, dz: 0f, count: 6, South, y: 1.55f));
        items.AddRange(Row("kitchenBar", x: 6.4f, z: 8.1f, dx: 0.87f, dz: 0f, count: 6, South));
        items.AddRange(Row("stoolBar", x: 6.6f, z: 7.3f, dx: 1.05f, dz: 0f, count: 5, North));

        // Café tables with two chairs and a small plant each.
        foreach (var (x, z) in new[] { (1.8f, 7.6f), (1.8f, 4.8f), (1.8f, 2f), (4.9f, 5.4f), (4.9f, 2.4f), (8.2f, 4.4f), (8.2f, 1.6f) })
        {
            items.Add(Item("tableRound", x, z));
            items.Add(Item(z > 5 ? "plantSmall1" : "plantSmall3", x, z));
            items.Add(Item("chairRounded", x - 1f, z, East));
            items.Add(Item("chairRounded", x + 1f, z, West));
        }
        return items;
    }

    /// <summary>Studio: easels, work tables, shelves with boxes, sofa corner, tic-tac-toe.</summary>
    private static List<RoomItem> Atelier()
    {
        var items = new List<RoomItem>
        {
            Item("custom-easel", 2.2f, 8.4f), Item("custom-easel", 4.4f, 8.8f), Item("custom-easel", 6.6f, 8.4f),
            Item("chairModernCushion", 2.2f, 7.5f, North), Item("chairModernCushion", 4.4f, 7.9f, North), Item("chairModernCushion", 6.6f, 7.5f, North),
            Item("tableCross", 3.2f, 4.4f), Item("books", 2.8f, 4.4f), Item("cardboardBoxOpen", 3.6f, 4.4f),
            Item("tableCross", 6.4f, 4.4f), Item("laptop", 6.4f, 4.4f), Item("lampSquareTable", 6.9f, 4.4f),
            Item("chairModernFrameCushion", 3.2f, 3.4f, North), Item("chairModernFrameCushion", 6.4f, 3.4f, North),
            Item("rugSquare", 4.8f, 4.4f),
            // Sofa corner in the east.
            Item("rugRectangle", 11.8f, 9.4f, West),
            Item("loungeSofa", 13.2f, 9.4f, West), Item("loungeChair", 11.3f, 11f, South), Item("tableCoffee", 11.8f, 9.4f, West),
            Item("lampSquareFloor", 13.4f, 11.4f),
            // Computer desk and tic-tac-toe.
            Item("desk", 13.2f, 3f, West), Item("computerScreen", 13.2f, 3f), Item("chairDesk", 12.3f, 3f, East),
            Item("game-tictactoe", 9.8f, 4.6f), Item("chairCushion", 8.7f, 4.6f, East), Item("chairCushion", 10.9f, 4.6f, West),
            // Boxes, bear, plants.
            Item("cardboardBoxClosed", 13.4f, 0.5f), Item("cardboardBoxClosed", 12.9f, 0.5f), Item("cardboardBoxOpen", 13.2f, 1.1f),
            Item("bear", 0.6f, 5.8f, East),
            Item("pottedPlant", 0.5f, 11.4f), Item("pottedPlant", 0.5f, 0.5f), Item("lampSquareFloor", 0.5f, 3f),
        };

        // Shelves along the north wall with things on top.
        items.AddRange(Row("bookcaseOpen", x: 1f, z: 11.7f, dx: 0.8f, dz: 0f, count: 5, South));
        items.AddRange(Row("bookcaseOpenLow", x: 5.4f, z: 11.7f, dx: 0.8f, dz: 0f, count: 3, South));
        items.Add(Item("plantSmall2", 5.4f, 11.7f));
        items.Add(Item("radio", 6.2f, 11.7f));
        return items;
    }

    private static IEnumerable<RoomItem> Row(string itemId, float x, float z, float dx, float dz, int count,
        float rotation = South, float y = 0f) =>
        Enumerable.Range(0, count).Select(i => Item(itemId, x + dx * i, z + dz * i, rotation, y));

    private static RoomItem Item(string itemId, float x, float z, float rotation = South, float y = 0f) =>
        new() { ItemId = itemId, Position = new Position3 { X = x, Y = y, Z = z }, Rotation = rotation };
}
