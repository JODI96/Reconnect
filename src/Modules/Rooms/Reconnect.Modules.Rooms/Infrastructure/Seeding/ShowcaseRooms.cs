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

    internal sealed record Definition(string Name, Guid BuildingId, string Theme, int Width, int Depth, List<RoomItem> Layout);

    private static readonly Guid[] ExclusiveBuildings = [ZurichBuildings.PrimeTowerId];

    internal static IEnumerable<Definition> Definitions() =>
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
    private static List<RoomItem> Seebad() =>
    [
        Cell("custom-pool-12x6", 10, 10, 0),   // Pool gross
        Cell("custom-skybar", 33, 27, 0),   // Bar
        Cell("ph-bar_chair_round_01", 35, 26, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 38, 26, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 40, 26, 0),   // Barstuhl
        Cell("ph-outdoor_table_chair_set_01", 4, 23, 180),   // Gartentisch mit Stühlen
        Cell("ph-outdoor_table_chair_set_01", 4, 14, 180),   // Gartentisch mit Stühlen
        Cell("custom-parasol", 4, 20, 0),   // Sonnenschirm
        Cell("ph-planter_box_01", 0, 30, 180),   // Pflanzkasten
        Cell("ph-planter_box_02", 8, 30, 180),   // Pflanzkasten gross
        Cell("ph-planter_box_01", 28, 30, 180),   // Pflanzkasten
        Cell("ph-planter_box_02", 41, 1, 180),   // Pflanzkasten gross
        Cell("ph-planter_box_01", 0, 1, 180),   // Pflanzkasten
        Cell("ph-potted_plant_02", 42, 16, 180),   // Zimmerpflanze
        Cell("ph-pachira_aquatica_01", 41, 8, 180),   // Glückskastanie
        Cell("ph-street_lamp_02", 1, 8, 180),   // Laterne
        Cell("ph-street_lamp_02", 42, 21, 180),   // Laterne
        Cell("custom-lightstring", 18, 16, 0),   // Lichterkette
        Cell("game-tictactoe", 4, 6, 0),   // Tic-Tac-Toe-Tisch
        Cell("chairCushion", 3, 7, 270),   // Polsterstuhl
        Cell("chairCushion", 8, 7, 90),   // Polsterstuhl
        // Sun deck: pairs of loungers sharing a parasol, 4 m between the parasols so the shades don't overlap.
        Cell("custom-lounger", 12, 3, 180),   // Sonnenliege
        Cell("custom-parasol", 14, 4, 0),   // Sonnenschirm
        Cell("custom-lounger", 16, 3, 180),   // Sonnenliege
        Cell("custom-lounger", 20, 3, 180),   // Sonnenliege
        Cell("custom-parasol", 22, 4, 0),   // Sonnenschirm
        Cell("custom-lounger", 24, 3, 180),   // Sonnenliege
        Cell("custom-lounger", 28, 3, 180),   // Sonnenliege
        Cell("custom-parasol", 30, 4, 0),   // Sonnenschirm
        Cell("custom-lounger", 32, 3, 180),   // Sonnenliege
        Cell("custom-lounger", 14, 25, 0),   // Sonnenliege
        Cell("custom-parasol", 16, 26, 0),   // Sonnenschirm
        Cell("custom-lounger", 18, 25, 0),   // Sonnenliege
        Cell("custom-lounger", 24, 25, 0),   // Sonnenliege
        Cell("custom-parasol", 26, 26, 0),   // Sonnenschirm
        Cell("custom-lounger", 28, 25, 0),   // Sonnenliege
    ];

    /// <summary>
    /// Reading room: worn wooden bookcases along the walls and in aisles, long reading tables with lamps, books and a
    /// chess game, study desks, info desk, a grandfather clock, sofa corner and the quiz corner.
    /// </summary>
    private static List<RoomItem> Library() =>
    [
        Cell("custom-column", 18, 23, 0),   // Säule
        Cell("custom-column", 18, 16, 0),   // Säule
        Cell("custom-column", 24, 23, 0),   // Säule
        Cell("custom-column", 24, 16, 0),   // Säule
        Cell("custom-rug", 20, 24, 90),   // Teppich
        Cell("custom-rug", 20, 17, 90),   // Teppich
        Cell("ph-metal_office_desk", 20, 11, 180),   // Bürotisch
        Cell("computerScreen", 20, 11, 0),   // Bildschirm
        Cell("computerKeyboard", 21, 12, 0),   // Tastatur
        Cell("ph-GreenChair_01", 22, 13, 180),   // Samtstuhl
        Cell("ph-desk_lamp_arm_01", 23, 11, 180),   // Schreibtischlampe
        Cell("ph-vintage_grandfather_clock_01", 16, 30, 180),   // Standuhr
        Cell("game-quiz", 43, 14, 90),   // Quiz-TV
        Cell("ph-painted_wooden_bench", 39, 14, 90),   // Holzbank bemalt
        Cell("ph-painted_wooden_bench", 39, 16, 90),   // Holzbank bemalt
        Cell("custom-rug", 31, 2, 0),   // Teppich
        Cell("ph-Sofa_01", 33, 2, 0),   // Klassisches Sofa
        Cell("ph-ArmChair_01", 29, 5, 90),   // Ohrensessel
        Cell("ph-ArmChair_01", 37, 5, 270),   // Ohrensessel
        Cell("ph-CoffeeTable_01", 33, 5, 180),   // Couchtisch klassisch
        Cell("ph-book_encyclopedia_set_01", 34, 5, 180),   // Lexikon
        Cell("ph-mantel_clock_01", 33, 5, 180),   // Kaminuhr
        Cell("ph-potted_plant_02", 41, 1, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 27, 1, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_01", 1, 30, 180),   // Grosse Pflanze
        Cell("ph-potted_plant_01", 42, 30, 180),   // Grosse Pflanze
        Cell("ph-potted_plant_02", 0, 1, 180),   // Zimmerpflanze
        Cell("ph-wooden_bookshelf_worn", 1, 31, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 4, 31, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 7, 31, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 10, 31, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 12, 30, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 27, 31, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 30, 31, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 33, 31, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 36, 31, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 38, 30, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 43, 27, 270),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 43, 24, 270),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 43, 21, 270),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 43, 8, 270),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 42, 6, 270),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 1, 25, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 4, 25, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 7, 25, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 10, 25, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 12, 24, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 1, 24, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 4, 24, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 7, 24, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 10, 23, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 13, 23, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 1, 19, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 4, 19, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 7, 19, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 10, 19, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 12, 18, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 1, 18, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 4, 18, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 7, 18, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 10, 17, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 13, 17, 0),   // Altes Bücherregal
        Cell("ph-WoodenTable_01", 28, 23, 180),   // Langer Holztisch
        Cell("ph-desk_lamp_arm_01", 31, 23, 180),   // Schreibtischlampe
        Cell("ph-chess_set", 29, 23, 180),   // Schachspiel
        Cell("books", 30, 23, 0),   // Bücher
        Cell("ph-dining_chair_02", 29, 25, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 30, 25, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 29, 22, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 30, 22, 0),   // Esszimmerstuhl
        Cell("ph-WoodenTable_01", 34, 23, 180),   // Langer Holztisch
        Cell("ph-desk_lamp_arm_01", 37, 23, 180),   // Schreibtischlampe
        Cell("ph-book_encyclopedia_set_01", 35, 23, 180),   // Lexikon
        Cell("laptop", 36, 23, 0),   // Laptop
        Cell("ph-dining_chair_02", 35, 25, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 37, 25, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 35, 22, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 37, 22, 0),   // Esszimmerstuhl
        Cell("ph-WoodenTable_01", 28, 17, 180),   // Langer Holztisch
        Cell("ph-desk_lamp_arm_01", 31, 17, 180),   // Schreibtischlampe
        Cell("ph-chess_set", 29, 17, 180),   // Schachspiel
        Cell("books", 30, 17, 0),   // Bücher
        Cell("ph-dining_chair_02", 29, 19, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 30, 19, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 29, 16, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 30, 16, 0),   // Esszimmerstuhl
        Cell("ph-WoodenTable_01", 34, 17, 180),   // Langer Holztisch
        Cell("ph-desk_lamp_arm_01", 37, 17, 180),   // Schreibtischlampe
        Cell("ph-book_encyclopedia_set_01", 35, 17, 180),   // Lexikon
        Cell("laptop", 36, 17, 0),   // Laptop
        Cell("ph-dining_chair_02", 35, 19, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 37, 19, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 35, 16, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 37, 16, 0),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 2, 9, 180),   // Bürotisch
        Cell("laptop", 4, 10, 0),   // Laptop
        Cell("ph-desk_lamp_arm_01", 5, 10, 180),   // Schreibtischlampe
        Cell("ph-GreenChair_01", 4, 8, 0),   // Samtstuhl
        Cell("ph-metal_office_desk", 7, 9, 180),   // Bürotisch
        Cell("laptop", 9, 10, 0),   // Laptop
        Cell("ph-desk_lamp_arm_01", 10, 10, 180),   // Schreibtischlampe
        Cell("ph-GreenChair_01", 9, 8, 0),   // Samtstuhl
        Cell("ph-metal_office_desk", 12, 9, 180),   // Bürotisch
        Cell("laptop", 14, 10, 0),   // Laptop
        Cell("ph-desk_lamp_arm_01", 15, 10, 180),   // Schreibtischlampe
        Cell("ph-GreenChair_01", 14, 8, 0),   // Samtstuhl
    ];

    /// <summary>
    /// Grand foyer: column rows, crystal chandeliers, a bar with candle holders, classic seating groups, a marble bust,
    /// paintings on the east wall, dance floor and cloakroom.
    /// </summary>
    private static List<RoomItem> OperaFoyer() =>
    [
        Cell("custom-ruground", 5, 21, 0),   // Runder Teppich
        Cell("ph-Sofa_01", 6, 26, 180),   // Klassisches Sofa
        Cell("ph-CoffeeTable_01", 6, 23, 180),   // Couchtisch klassisch
        Cell("ph-brass_candleholders", 7, 23, 180),   // Kerzenständer
        Cell("ph-ArmChair_01", 3, 22, 90),   // Ohrensessel
        Cell("ph-ArmChair_01", 11, 22, 270),   // Ohrensessel
        Cell("custom-ruground", 32, 11, 90),   // Runder Teppich
        Cell("ph-Sofa_01", 38, 12, 270),   // Klassisches Sofa
        Cell("ph-CoffeeTable_01", 35, 12, 270),   // Couchtisch klassisch
        Cell("ph-antique_ceramic_vase_01", 35, 13, 180),   // Antike Vase
        Cell("ph-ArmChair_01", 34, 9, 0),   // Ohrensessel
        Cell("ph-ArmChair_01", 34, 16, 180),   // Ohrensessel
        Cell("ph-side_table_tall_01", 1, 14, 90),   // Hoher Beistelltisch
        Cell("ph-marble_bust_01", 1, 14, 90),   // Marmorbüste
        Cell("coatRackStanding", 1, 11, 0),   // Garderobe
        Cell("coatRackStanding", 1, 18, 0),   // Garderobe
        Cell("game-tictactoe", 32, 3, 0),   // Tic-Tac-Toe-Tisch
        Cell("ph-dining_chair_02", 30, 4, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 35, 4, 270),   // Esszimmerstuhl
        Cell("ph-potted_plant_01", 1, 26, 180),   // Grosse Pflanze
        Cell("ph-potted_plant_01", 38, 26, 180),   // Grosse Pflanze
        Cell("ph-potted_plant_02", 0, 1, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 38, 1, 180),   // Zimmerpflanze
        Cell("ph-vintage_grandfather_clock_01", 19, 26, 180),   // Standuhr
        Cell("ph-hanging_picture_frame_01", 39, 22, 270),   // Gemälde hoch
        Cell("ph-hanging_picture_frame_02", 39, 5, 270),   // Gemälde quer
        Cell("speaker", 14, 18, 0),   // Lautsprecher
        Cell("speaker", 25, 18, 0),   // Lautsprecher
        Cell("speaker", 14, 9, 0),   // Lautsprecher
        Cell("speaker", 25, 9, 0),   // Lautsprecher
        Cell("ph-Chandelier_01", 19, 13, 180),   // Kronleuchter
        Cell("ph-Chandelier_01", 7, 23, 180),   // Kronleuchter
        Cell("ph-Chandelier_01", 34, 13, 180),   // Kronleuchter
        Cell("custom-column", 7, 18, 0),   // Säule
        Cell("custom-column", 15, 18, 0),   // Säule
        Cell("custom-column", 23, 18, 0),   // Säule
        Cell("custom-column", 31, 18, 0),   // Säule
        Cell("custom-column", 7, 6, 0),   // Säule
        Cell("custom-column", 15, 6, 0),   // Säule
        Cell("custom-column", 23, 6, 0),   // Säule
        Cell("custom-column", 31, 6, 0),   // Säule
        Cell("rugSquare", 14, 8, 0),   // Rug Square
        Cell("rugSquare", 18, 8, 0),   // Rug Square
        Cell("rugSquare", 22, 8, 0),   // Rug Square
        Cell("rugSquare", 14, 12, 0),   // Rug Square
        Cell("rugSquare", 18, 12, 0),   // Rug Square
        Cell("rugSquare", 22, 12, 0),   // Rug Square
        Cell("rugSquare", 14, 16, 0),   // Rug Square
        Cell("rugSquare", 18, 16, 0),   // Rug Square
        Cell("rugSquare", 22, 16, 0),   // Rug Square
        // Foyer bar right of the entrance: back bar, 1 m behind the counter, counter with stools (none in front of the column).
        Cell("kitchenCabinet", 24, 26, 0),   // Küchenschrank
        Cell("kitchenCabinet", 26, 26, 0),   // Küchenschrank
        Cell("kitchenCabinet", 28, 26, 0),   // Küchenschrank
        Cell("kitchenCabinet", 30, 26, 0),   // Küchenschrank
        Cell("kitchenCabinet", 32, 26, 0),   // Küchenschrank
        Cell("kitchenCoffeeMachine", 24, 26, 0),   // Kaffeemaschine
        Cell("ph-brass_candleholders", 26, 26, 180),   // Kerzenständer
        Cell("ph-ceramic_vase_03", 31, 26, 180),   // Vase hell
        Cell("kitchenBar", 24, 23, 0),   // Theke
        Cell("kitchenBar", 26, 23, 0),   // Theke
        Cell("kitchenBar", 28, 23, 0),   // Theke
        Cell("kitchenBar", 30, 23, 0),   // Theke
        Cell("kitchenBar", 32, 23, 0),   // Theke
        Cell("ph-brass_candleholders", 28, 23, 180),   // Kerzenständer
        Cell("ph-bar_chair_round_01", 24, 21, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 26, 21, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 28, 21, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 32, 21, 0),   // Barstuhl
    ];

    /// <summary>Bistro: counter with back bar and bar chairs, marble bistro tables, bench corner, coffee cart, menu board, quiz TV.</summary>
    private static List<RoomItem> Cafe() =>
    [
        // Counter: kitchen along the north wall right of the entrance, 1 m for the barista, bar with four stools.
        Cell("kitchenCabinet", 16, 18, 0),   // Küchenschrank
        Cell("kitchenCabinet", 18, 18, 0),   // Küchenschrank
        Cell("kitchenCabinet", 20, 18, 0),   // Küchenschrank
        Cell("kitchenCabinet", 22, 18, 0),   // Küchenschrank
        Cell("kitchenFridgeLarge", 24, 18, 0),   // Kühlschrank
        Cell("kitchenCoffeeMachine", 16, 18, 0),   // Kaffeemaschine
        Cell("toaster", 19, 18, 0),   // Toaster
        Cell("ph-ceramic_vase_03", 22, 18, 180),   // Vase hell
        Cell("kitchenCabinetUpper", 16, 19, 0),   // Hängeschrank
        Cell("kitchenCabinetUpper", 18, 19, 0),   // Hängeschrank
        Cell("kitchenCabinetUpper", 20, 19, 0),   // Hängeschrank
        Cell("kitchenCabinetUpper", 22, 19, 0),   // Hängeschrank
        Cell("kitchenBar", 16, 15, 0),   // Theke
        Cell("kitchenBar", 18, 15, 0),   // Theke
        Cell("kitchenBar", 20, 15, 0),   // Theke
        Cell("kitchenBar", 22, 15, 0),   // Theke
        Cell("ph-brass_candleholders", 20, 15, 180),   // Kerzenständer
        Cell("ph-bar_chair_round_01", 16, 13, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 18, 13, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 20, 13, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 22, 13, 0),   // Barstuhl
        Cell("game-quiz", 27, 8, 90),   // Quiz-TV
        Cell("ph-painted_wooden_bench", 23, 7, 90),   // Holzbank bemalt
        Cell("ph-painted_wooden_bench", 23, 10, 90),   // Holzbank bemalt
        Cell("custom-rug", 3, 6, 90),   // Teppich
        Cell("coatRackStanding", 0, 0, 0),   // Garderobe
        Cell("ph-CoffeeCart_01", 4, 17, 180),   // Kaffeewagen
        Cell("ph-standing_chalkboard_01", 22, 0, 180),   // Menütafel
        Cell("ph-wooden_display_shelves_01", 26, 16, 270),   // Vitrinenregal
        Cell("ph-tea_set_01", 26, 16, 180),   // Teeservice
        Cell("ph-potted_plant_02", 0, 18, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 26, 18, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 26, 0, 180),   // Zimmerpflanze
        Cell("lampRoundFloor", 0, 9, 0),   // Stehlampe
        Cell("lampRoundFloor", 20, 0, 0),   // Stehlampe
        Cell("ph-hanging_picture_frame_03", 27, 4, 270),   // Kleines Bild
        Cell("ph-gallinera_table", 3, 14, 180),   // Bistrotisch
        Cell("ph-ceramic_vase_03", 3, 14, 180),   // Vase hell
        Cell("ph-gallinera_chair", 2, 14, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 4, 13, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 3, 9, 180),   // Bistrotisch
        Cell("ph-antique_ceramic_vase_01", 3, 9, 180),   // Antike Vase
        Cell("ph-gallinera_chair", 2, 9, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 4, 8, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 3, 4, 180),   // Bistrotisch
        Cell("ph-antique_ceramic_vase_01", 3, 4, 180),   // Antike Vase
        Cell("ph-gallinera_chair", 2, 4, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 4, 3, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 9, 10, 180),   // Bistrotisch
        Cell("ph-ceramic_vase_03", 9, 10, 180),   // Vase hell
        Cell("ph-gallinera_chair", 8, 10, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 11, 10, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 9, 4, 180),   // Bistrotisch
        Cell("ph-antique_ceramic_vase_01", 9, 4, 180),   // Antike Vase
        Cell("ph-gallinera_chair", 8, 4, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 11, 4, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 15, 8, 180),   // Bistrotisch
        Cell("ph-antique_ceramic_vase_01", 16, 8, 180),   // Antike Vase
        Cell("ph-gallinera_chair", 14, 8, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 17, 8, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 15, 3, 180),   // Bistrotisch
        Cell("ph-antique_ceramic_vase_01", 16, 3, 180),   // Antike Vase
        Cell("ph-gallinera_chair", 14, 3, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 17, 3, 270),   // Bistrostuhl
    ];

    /// <summary>Studio: easels, work tables, a bronze sculpture, paintings, shelves, sofa corner, computer desk, tic-tac-toe.</summary>
    private static List<RoomItem> Atelier() =>
    [
        Cell("custom-easel", 3, 16, 0),   // Staffelei
        Cell("custom-easel", 8, 17, 0),   // Staffelei
        Cell("custom-easel", 12, 16, 0),   // Staffelei
        Cell("ph-dining_chair_02", 4, 14, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 8, 15, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 13, 14, 0),   // Esszimmerstuhl
        Cell("ph-WoodenTable_01", 4, 8, 180),   // Langer Holztisch
        Cell("books", 5, 8, 0),   // Bücher
        Cell("ph-ceramic_vase_03", 7, 8, 180),   // Vase hell
        Cell("ph-WoodenTable_01", 11, 8, 180),   // Langer Holztisch
        Cell("laptop", 12, 8, 0),   // Laptop
        Cell("ph-desk_lamp_arm_01", 14, 8, 180),   // Schreibtischlampe
        Cell("ph-dining_chair_02", 6, 6, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 12, 6, 0),   // Esszimmerstuhl
        Cell("custom-rug", 6, 6, 0),   // Teppich
        Cell("ph-side_table_tall_01", 18, 15, 180),   // Hoher Beistelltisch
        Cell("custom-rug", 21, 15, 90),   // Teppich
        Cell("ph-sofa_03", 25, 16, 270),   // Ledersofa
        Cell("ph-mid_century_lounge_chair", 22, 21, 180),   // Lounge Chair
        Cell("ph-modern_coffee_table_01", 22, 18, 270),   // Moderner Couchtisch
        Cell("ph-standing_picture_frame_01", 23, 18, 180),   // Bilderrahmen
        Cell("ph-metal_office_desk", 25, 4, 270),   // Bürotisch
        Cell("computerScreen", 25, 6, 0),   // Bildschirm
        Cell("ph-GreenChair_01", 23, 6, 90),   // Samtstuhl
        Cell("game-tictactoe", 18, 8, 0),   // Tic-Tac-Toe-Tisch
        Cell("ph-dining_chair_02", 17, 9, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 21, 9, 270),   // Esszimmerstuhl
        Cell("ph-hanging_picture_frame_01", 27, 12, 270),   // Gemälde hoch
        Cell("ph-hanging_picture_frame_02", 27, 1, 270),   // Gemälde quer
        Cell("ph-potted_plant_01", 0, 22, 180),   // Grosse Pflanze
        Cell("ph-potted_plant_02", 0, 0, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 26, 0, 180),   // Zimmerpflanze
        Cell("ph-Shelf_01", 1, 23, 180),   // Metallregal
        Cell("ph-Shelf_01", 3, 23, 180),   // Metallregal
        Cell("ph-Shelf_01", 5, 23, 180),   // Metallregal
        Cell("ph-Shelf_01", 8, 23, 180),   // Metallregal
        Cell("ph-wooden_display_shelves_01", 11, 22, 180),   // Vitrinenregal
        Cell("ph-wooden_display_shelves_01", 16, 22, 180),   // Vitrinenregal
        Cell("ph-ceramic_vase_03", 11, 23, 180),   // Vase hell
        Cell("radio", 16, 23, 0),   // Radio
    ];

    /// <summary>
    /// Item placed on the build grid like the build editor does: footprint starts at 50 cm cell (x, z), rotation
    /// 0/90/180/270 (Kenney models face -Z at 0, Poly Haven models +Z). Layouts follow <c>RoomLayout</c>'s rules.
    /// </summary>
    internal static RoomItem Cell(string itemId, int x, int z, float rotation = 0f) => RoomItem.AtCell(itemId, x, z, rotation);
}
