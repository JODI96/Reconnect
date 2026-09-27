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
        Cell("custom-pool-12x6", 20, 20, 0),   // Pool gross
        Cell("custom-skybar", 66, 54, 0),   // Bar
        Cell("ph-bar_chair_round_01", 70, 52, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 76, 52, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 80, 52, 0),   // Barstuhl
        Cell("ph-outdoor_table_chair_set_01", 8, 46, 180),   // Gartentisch mit Stühlen
        Cell("ph-outdoor_table_chair_set_01", 8, 28, 180),   // Gartentisch mit Stühlen
        Cell("custom-parasol", 8, 40, 0),   // Sonnenschirm
        Cell("ph-planter_box_01", 0, 60, 180),   // Pflanzkasten
        Cell("ph-planter_box_02", 16, 60, 180),   // Pflanzkasten gross
        Cell("ph-planter_box_01", 56, 60, 180),   // Pflanzkasten
        Cell("ph-planter_box_02", 82, 2, 180),   // Pflanzkasten gross
        Cell("ph-planter_box_01", 0, 2, 180),   // Pflanzkasten
        Cell("ph-potted_plant_02", 84, 32, 180),   // Zimmerpflanze
        Cell("ph-pachira_aquatica_01", 82, 16, 180),   // Glückskastanie
        Cell("ph-street_lamp_02", 2, 16, 180),   // Laterne
        Cell("ph-street_lamp_02", 84, 42, 180),   // Laterne
        Cell("custom-lightstring", 36, 32, 0),   // Lichterkette
        Cell("game-tictactoe", 8, 12, 0),   // Tic-Tac-Toe-Tisch
        Cell("chairCushion", 6, 14, 270),   // Polsterstuhl
        Cell("chairCushion", 16, 14, 90),   // Polsterstuhl
        Cell("custom-lounger", 24, 6, 180),   // Sonnenliege
        Cell("custom-parasol", 28, 8, 0),   // Sonnenschirm
        Cell("custom-lounger", 32, 6, 180),   // Sonnenliege
        Cell("custom-lounger", 40, 6, 180),   // Sonnenliege
        Cell("custom-parasol", 44, 8, 0),   // Sonnenschirm
        Cell("custom-lounger", 48, 6, 180),   // Sonnenliege
        Cell("custom-lounger", 56, 6, 180),   // Sonnenliege
        Cell("custom-parasol", 60, 8, 0),   // Sonnenschirm
        Cell("custom-lounger", 64, 6, 180),   // Sonnenliege
        Cell("custom-lounger", 28, 50, 0),   // Sonnenliege
        Cell("custom-parasol", 32, 52, 0),   // Sonnenschirm
        Cell("custom-lounger", 36, 50, 0),   // Sonnenliege
        Cell("custom-lounger", 48, 50, 0),   // Sonnenliege
        Cell("custom-parasol", 52, 52, 0),   // Sonnenschirm
        Cell("custom-lounger", 56, 50, 0),   // Sonnenliege
    ];

    /// <summary>
    /// Reading room: worn wooden bookcases along the walls and in aisles, long reading tables with lamps, books and a
    /// chess game, study desks, info desk, a grandfather clock, sofa corner and the quiz corner.
    /// </summary>
    private static List<RoomItem> Library() =>
    [
        Cell("custom-column", 36, 46, 0),   // Säule
        Cell("custom-column", 36, 32, 0),   // Säule
        Cell("custom-column", 48, 46, 0),   // Säule
        Cell("custom-column", 48, 32, 0),   // Säule
        Cell("custom-rug", 40, 48, 90),   // Teppich
        Cell("custom-rug", 40, 34, 90),   // Teppich
        Cell("ph-metal_office_desk", 40, 22, 180),   // Bürotisch
        At("computerScreen", 10.5f, 5.75f, 0),   // Bildschirm
        At("computerKeyboard", 10.75f, 6.25f, 0),   // Tastatur
        Cell("ph-GreenChair_01", 44, 26, 180),   // Samtstuhl
        At("ph-desk_lamp_arm_01", 11.75f, 5.875f, 180),   // Schreibtischlampe
        Cell("ph-vintage_grandfather_clock_01", 32, 60, 180),   // Standuhr
        Cell("game-quiz", 86, 28, 90),   // Quiz-TV
        Cell("ph-painted_wooden_bench", 78, 28, 90),   // Holzbank bemalt
        Cell("ph-painted_wooden_bench", 78, 33, 90),   // Holzbank bemalt
        Cell("custom-rug", 62, 4, 0),   // Teppich
        Cell("ph-Sofa_01", 66, 4, 0),   // Klassisches Sofa
        Cell("ph-ArmChair_01", 58, 10, 90),   // Ohrensessel
        Cell("ph-ArmChair_01", 72, 10, 270),   // Ohrensessel
        Cell("ph-CoffeeTable_01", 66, 10, 180),   // Couchtisch klassisch
        At("ph-book_encyclopedia_set_01", 17.25f, 2.75f, 180),   // Lexikon
        At("ph-mantel_clock_01", 16.75f, 2.75f, 180),   // Kaminuhr
        Cell("ph-potted_plant_02", 82, 2, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 54, 2, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_01", 2, 60, 180),   // Grosse Pflanze
        Cell("ph-potted_plant_01", 84, 60, 180),   // Grosse Pflanze
        Cell("ph-potted_plant_02", 0, 2, 180),   // Zimmerpflanze
        Cell("ph-wooden_bookshelf_worn", 4, 62, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 8, 60, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 14, 62, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 20, 62, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 24, 60, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 54, 62, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 60, 62, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 66, 62, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 72, 62, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 76, 60, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 86, 54, 270),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 86, 48, 270),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 86, 42, 270),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 86, 16, 270),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 84, 12, 270),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 2, 50, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 8, 50, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 14, 50, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 20, 50, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 24, 48, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 2, 48, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 8, 48, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 14, 48, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 20, 46, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 26, 46, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 2, 38, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 8, 38, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 14, 38, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 20, 38, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 24, 36, 180),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 2, 36, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 8, 36, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 14, 36, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 20, 34, 0),   // Altes Bücherregal
        Cell("ph-wooden_bookshelf_worn", 26, 34, 0),   // Altes Bücherregal
        Cell("ph-WoodenTable_01", 56, 46, 180),   // Langer Holztisch
        At("ph-desk_lamp_arm_01", 15.625f, 11.875f, 180),   // Schreibtischlampe
        At("ph-chess_set", 14.75f, 11.875f, 180),   // Schachspiel
        At("books", 15.25f, 11.75f, 0),   // Bücher
        Cell("ph-dining_chair_02", 58, 49, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 60, 49, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 58, 44, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 60, 44, 0),   // Esszimmerstuhl
        Cell("ph-WoodenTable_01", 68, 46, 180),   // Langer Holztisch
        At("ph-desk_lamp_arm_01", 18.625f, 11.875f, 180),   // Schreibtischlampe
        At("ph-book_encyclopedia_set_01", 17.75f, 11.75f, 180),   // Lexikon
        At("laptop", 18.25f, 11.75f, 0),   // Laptop
        Cell("ph-dining_chair_02", 70, 49, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 74, 49, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 70, 44, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 74, 44, 0),   // Esszimmerstuhl
        Cell("ph-WoodenTable_01", 56, 34, 180),   // Langer Holztisch
        At("ph-desk_lamp_arm_01", 15.625f, 8.875f, 180),   // Schreibtischlampe
        At("ph-chess_set", 14.75f, 8.875f, 180),   // Schachspiel
        At("books", 15.25f, 8.75f, 0),   // Bücher
        Cell("ph-dining_chair_02", 58, 37, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 60, 37, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 58, 32, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 60, 32, 0),   // Esszimmerstuhl
        Cell("ph-WoodenTable_01", 68, 34, 180),   // Langer Holztisch
        At("ph-desk_lamp_arm_01", 18.625f, 8.875f, 180),   // Schreibtischlampe
        At("ph-book_encyclopedia_set_01", 17.75f, 8.75f, 180),   // Lexikon
        At("laptop", 18.25f, 8.75f, 0),   // Laptop
        Cell("ph-dining_chair_02", 70, 37, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 74, 37, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 70, 32, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 74, 32, 0),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 4, 18, 180),   // Bürotisch
        At("laptop", 2.25f, 5.25f, 0),   // Laptop
        At("ph-desk_lamp_arm_01", 2.75f, 5.125f, 180),   // Schreibtischlampe
        Cell("ph-GreenChair_01", 8, 15, 0),   // Samtstuhl
        Cell("ph-metal_office_desk", 14, 18, 180),   // Bürotisch
        At("laptop", 4.75f, 5.25f, 0),   // Laptop
        At("ph-desk_lamp_arm_01", 5.25f, 5.125f, 180),   // Schreibtischlampe
        Cell("ph-GreenChair_01", 18, 15, 0),   // Samtstuhl
        Cell("ph-metal_office_desk", 24, 18, 180),   // Bürotisch
        At("laptop", 7.25f, 5.25f, 0),   // Laptop
        At("ph-desk_lamp_arm_01", 7.75f, 5.125f, 180),   // Schreibtischlampe
        Cell("ph-GreenChair_01", 28, 15, 0),   // Samtstuhl
    ];

    /// <summary>
    /// Grand foyer: column rows, crystal chandeliers, a bar with candle holders, classic seating groups, a marble bust,
    /// paintings on the east wall, dance floor and cloakroom.
    /// </summary>
    private static List<RoomItem> OperaFoyer() =>
    [
        Cell("custom-ruground", 10, 42, 0),   // Runder Teppich
        Cell("ph-Sofa_01", 12, 52, 180),   // Klassisches Sofa
        Cell("ph-CoffeeTable_01", 12, 46, 180),   // Couchtisch klassisch
        At("ph-brass_candleholders", 4f, 11.75f, 180),   // Kerzenständer
        Cell("ph-ArmChair_01", 9, 46, 90),   // Ohrensessel
        Cell("ph-ArmChair_01", 18, 46, 270),   // Ohrensessel
        Cell("custom-ruground", 64, 22, 90),   // Runder Teppich
        Cell("ph-Sofa_01", 76, 24, 270),   // Klassisches Sofa
        Cell("ph-CoffeeTable_01", 70, 24, 270),   // Couchtisch klassisch
        At("ph-antique_ceramic_vase_01", 17.75f, 6.75f, 180),   // Antike Vase
        Cell("ph-ArmChair_01", 70, 21, 0),   // Ohrensessel
        Cell("ph-ArmChair_01", 70, 30, 180),   // Ohrensessel
        Cell("ph-side_table_tall_01", 2, 28, 90),   // Hoher Beistelltisch
        At("ph-marble_bust_01", 0.75f, 7.25f, 90),   // Marmorbüste
        Cell("coatRackStanding", 2, 22, 0),   // Garderobe
        Cell("coatRackStanding", 2, 36, 0),   // Garderobe
        Cell("game-tictactoe", 64, 6, 0),   // Tic-Tac-Toe-Tisch
        Cell("ph-dining_chair_02", 60, 8, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 71, 8, 270),   // Esszimmerstuhl
        Cell("ph-potted_plant_01", 2, 52, 180),   // Grosse Pflanze
        Cell("ph-potted_plant_01", 76, 52, 180),   // Grosse Pflanze
        Cell("ph-potted_plant_02", 0, 2, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 76, 2, 180),   // Zimmerpflanze
        Cell("ph-vintage_grandfather_clock_01", 37, 52, 180),   // Standuhr
        Cell("ph-hanging_picture_frame_01", 79, 44, 270),   // Gemälde hoch
        Cell("ph-hanging_picture_frame_02", 79, 10, 270),   // Gemälde quer
        Cell("speaker", 28, 36, 0),   // Lautsprecher
        Cell("speaker", 50, 36, 0),   // Lautsprecher
        Cell("speaker", 28, 18, 0),   // Lautsprecher
        Cell("speaker", 50, 18, 0),   // Lautsprecher
        Cell("ph-Chandelier_01", 38, 26, 180),   // Kronleuchter
        Cell("ph-Chandelier_01", 14, 46, 180),   // Kronleuchter
        Cell("ph-Chandelier_01", 68, 26, 180),   // Kronleuchter
        Cell("custom-column", 14, 36, 0),   // Säule
        Cell("custom-column", 30, 36, 0),   // Säule
        Cell("custom-column", 46, 36, 0),   // Säule
        Cell("custom-column", 62, 36, 0),   // Säule
        Cell("custom-column", 14, 12, 0),   // Säule
        Cell("custom-column", 30, 12, 0),   // Säule
        Cell("custom-column", 46, 12, 0),   // Säule
        Cell("custom-column", 62, 12, 0),   // Säule
        Cell("rugSquare", 28, 16, 0),   // Rug Square
        Cell("rugSquare", 36, 16, 0),   // Rug Square
        Cell("rugSquare", 44, 16, 0),   // Rug Square
        Cell("rugSquare", 28, 24, 0),   // Rug Square
        Cell("rugSquare", 36, 24, 0),   // Rug Square
        Cell("rugSquare", 44, 24, 0),   // Rug Square
        Cell("rugSquare", 28, 32, 0),   // Rug Square
        Cell("rugSquare", 36, 32, 0),   // Rug Square
        Cell("rugSquare", 44, 32, 0),   // Rug Square
        Cell("kitchenCabinet", 48, 52, 0),   // Küchenschrank
        Cell("kitchenCabinet", 52, 52, 0),   // Küchenschrank
        Cell("kitchenCabinet", 56, 52, 0),   // Küchenschrank
        Cell("kitchenCabinet", 60, 52, 0),   // Küchenschrank
        Cell("kitchenCabinet", 64, 52, 0),   // Küchenschrank
        At("kitchenCoffeeMachine", 12.375f, 13.375f, 0),   // Kaffeemaschine
        At("ph-brass_candleholders", 13.5f, 13.25f, 180),   // Kerzenständer
        At("ph-ceramic_vase_03", 15.75f, 13.25f, 180),   // Vase hell
        Cell("kitchenBar", 48, 46, 0),   // Theke
        Cell("kitchenBar", 52, 46, 0),   // Theke
        Cell("kitchenBar", 56, 46, 0),   // Theke
        Cell("kitchenBar", 60, 46, 0),   // Theke
        Cell("kitchenBar", 64, 46, 0),   // Theke
        At("ph-brass_candleholders", 14.5f, 11.75f, 180),   // Kerzenständer
        Cell("ph-bar_chair_round_01", 49, 44, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 53, 44, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 57, 44, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 65, 44, 0),   // Barstuhl
    ];

    /// <summary>Bistro: counter with back bar and bar chairs, marble bistro tables, bench corner, coffee cart, menu board, quiz TV.</summary>
    private static List<RoomItem> Cafe() =>
    [
        Cell("kitchenCabinet", 32, 36, 0),   // Küchenschrank
        Cell("kitchenCabinet", 36, 36, 0),   // Küchenschrank
        Cell("kitchenCabinet", 40, 36, 0),   // Küchenschrank
        Cell("kitchenCabinet", 44, 36, 0),   // Küchenschrank
        Cell("kitchenFridgeLarge", 48, 36, 0),   // Kühlschrank
        At("kitchenCoffeeMachine", 8.375f, 9.375f, 0),   // Kaffeemaschine
        At("toaster", 9.75f, 9.25f, 0),   // Toaster
        At("ph-ceramic_vase_03", 11.25f, 9.25f, 180),   // Vase hell
        Cell("kitchenCabinetUpper", 32, 39, 0),   // Hängeschrank
        Cell("kitchenCabinetUpper", 36, 39, 0),   // Hängeschrank
        Cell("kitchenCabinetUpper", 40, 39, 0),   // Hängeschrank
        Cell("kitchenCabinetUpper", 44, 39, 0),   // Hängeschrank
        Cell("kitchenBar", 32, 30, 0),   // Theke
        Cell("kitchenBar", 36, 30, 0),   // Theke
        Cell("kitchenBar", 40, 30, 0),   // Theke
        Cell("kitchenBar", 44, 30, 0),   // Theke
        At("ph-brass_candleholders", 10.5f, 7.75f, 180),   // Kerzenständer
        Cell("ph-bar_chair_round_01", 33, 28, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 37, 28, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 41, 28, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 45, 28, 0),   // Barstuhl
        Cell("game-quiz", 54, 16, 90),   // Quiz-TV
        Cell("ph-painted_wooden_bench", 46, 14, 90),   // Holzbank bemalt
        Cell("ph-painted_wooden_bench", 46, 20, 90),   // Holzbank bemalt
        Cell("custom-rug", 6, 12, 90),   // Teppich
        Cell("coatRackStanding", 0, 0, 0),   // Garderobe
        Cell("ph-CoffeeCart_01", 8, 34, 180),   // Kaffeewagen
        Cell("ph-standing_chalkboard_01", 44, 0, 180),   // Menütafel
        Cell("ph-wooden_display_shelves_01", 52, 32, 270),   // Vitrinenregal
        Cell("ph-potted_plant_02", 0, 36, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 52, 36, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 52, 0, 180),   // Zimmerpflanze
        Cell("lampRoundFloor", 0, 18, 0),   // Stehlampe
        Cell("lampRoundFloor", 40, 0, 0),   // Stehlampe
        Cell("ph-hanging_picture_frame_03", 55, 8, 270),   // Kleines Bild
        Cell("ph-gallinera_table", 6, 28, 180),   // Bistrotisch
        At("ph-ceramic_vase_03", 1.75f, 7.25f, 180),   // Vase hell
        Cell("ph-gallinera_chair", 3, 28, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 9, 28, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 6, 18, 180),   // Bistrotisch
        At("ph-antique_ceramic_vase_01", 1.75f, 4.75f, 180),   // Antike Vase
        Cell("ph-gallinera_chair", 3, 18, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 9, 18, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 6, 8, 180),   // Bistrotisch
        At("ph-antique_ceramic_vase_01", 1.75f, 2.25f, 180),   // Antike Vase
        Cell("ph-gallinera_chair", 3, 8, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 9, 8, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 18, 20, 180),   // Bistrotisch
        At("ph-ceramic_vase_03", 4.75f, 5.25f, 180),   // Vase hell
        Cell("ph-gallinera_chair", 15, 20, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 21, 20, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 18, 8, 180),   // Bistrotisch
        At("ph-antique_ceramic_vase_01", 4.75f, 2.25f, 180),   // Antike Vase
        Cell("ph-gallinera_chair", 15, 8, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 21, 8, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 30, 16, 180),   // Bistrotisch
        At("ph-antique_ceramic_vase_01", 8.125f, 4.25f, 180),   // Antike Vase
        Cell("ph-gallinera_chair", 27, 16, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 33, 16, 270),   // Bistrostuhl
        Cell("ph-gallinera_table", 30, 6, 180),   // Bistrotisch
        At("ph-antique_ceramic_vase_01", 8.125f, 1.75f, 180),   // Antike Vase
        Cell("ph-gallinera_chair", 27, 6, 90),   // Bistrostuhl
        Cell("ph-gallinera_chair", 33, 6, 270),   // Bistrostuhl
    ];

    /// <summary>Studio: easels, work tables, a bronze sculpture, paintings, shelves, sofa corner, computer desk, tic-tac-toe.</summary>
    private static List<RoomItem> Atelier() =>
    [
        Cell("custom-easel", 6, 32, 0),   // Staffelei
        Cell("custom-easel", 16, 34, 0),   // Staffelei
        Cell("custom-easel", 24, 32, 0),   // Staffelei
        Cell("ph-dining_chair_02", 8, 28, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 16, 30, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 26, 28, 0),   // Esszimmerstuhl
        Cell("ph-WoodenTable_01", 8, 16, 180),   // Langer Holztisch
        At("books", 2.75f, 4.25f, 0),   // Bücher
        At("ph-ceramic_vase_03", 3.625f, 4.25f, 180),   // Vase hell
        Cell("ph-WoodenTable_01", 22, 16, 180),   // Langer Holztisch
        At("laptop", 6.25f, 4.25f, 0),   // Laptop
        At("ph-desk_lamp_arm_01", 7.125f, 4.375f, 180),   // Schreibtischlampe
        Cell("ph-dining_chair_02", 12, 14, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 24, 14, 0),   // Esszimmerstuhl
        Cell("custom-rug", 12, 12, 0),   // Teppich
        Cell("ph-side_table_tall_01", 36, 30, 180),   // Hoher Beistelltisch
        Cell("custom-rug", 42, 30, 90),   // Teppich
        Cell("ph-sofa_03", 50, 32, 270),   // Ledersofa
        Cell("ph-mid_century_lounge_chair", 44, 42, 180),   // Lounge Chair
        Cell("ph-modern_coffee_table_01", 44, 36, 270),   // Moderner Couchtisch
        At("ph-standing_picture_frame_01", 11.75f, 9.25f, 180),   // Bilderrahmen
        Cell("ph-metal_office_desk", 50, 8, 270),   // Bürotisch
        At("computerScreen", 13f, 3.25f, 270),   // Bildschirm
        Cell("ph-GreenChair_01", 47, 12, 90),   // Samtstuhl
        Cell("game-tictactoe", 36, 16, 0),   // Tic-Tac-Toe-Tisch
        Cell("ph-dining_chair_02", 34, 18, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 43, 18, 270),   // Esszimmerstuhl
        Cell("ph-hanging_picture_frame_01", 55, 24, 270),   // Gemälde hoch
        Cell("ph-hanging_picture_frame_02", 55, 2, 270),   // Gemälde quer
        Cell("ph-potted_plant_01", 0, 44, 180),   // Grosse Pflanze
        Cell("ph-potted_plant_02", 0, 0, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 52, 0, 180),   // Zimmerpflanze
        Cell("ph-Shelf_01", 2, 46, 180),   // Metallregal
        Cell("ph-Shelf_01", 6, 46, 180),   // Metallregal
        Cell("ph-Shelf_01", 10, 46, 180),   // Metallregal
        Cell("ph-Shelf_01", 16, 46, 180),   // Metallregal
        Cell("ph-wooden_display_shelves_01", 22, 44, 180),   // Vitrinenregal
        Cell("ph-wooden_display_shelves_01", 32, 44, 180),   // Vitrinenregal
        At("ph-ceramic_vase_03", 5.75f, 11.75f, 180),   // Vase hell
        At("radio", 8.25f, 11.75f, 0),   // Radio
    ];

    /// <summary>
    /// Item placed on the build grid like the build editor does: footprint starts at 50 cm cell (x, z), rotation
    /// 0/90/180/270 (Kenney models face -Z at 0, Poly Haven models +Z). Layouts follow <c>RoomLayout</c>'s rules.
    /// </summary>
    internal static RoomItem Cell(string itemId, int x, int z, float rotation = 0f) => RoomItem.AtCell(itemId, x, z, rotation);

    /// <summary>Small thing on a table: centre in metres on the 12.5 cm decor grid.</summary>
    internal static RoomItem At(string itemId, float x, float z, float rotation = 0f) => RoomItem.At(itemId, x, z, rotation);
}
