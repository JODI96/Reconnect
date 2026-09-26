using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reconnect.Modules.City.Public;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Contracts.Rooms;
using Reconnect.Modules.Rooms.Hubs;
using static Reconnect.Modules.Rooms.Infrastructure.Seeding.ShowcaseRooms;

namespace Reconnect.Modules.Rooms.Infrastructure.Seeding;

/// <summary>
/// The public floors of the Prime Tower (all environments – they are part of the game, not dev data).
/// Real building: 36 storeys, 126 m; lobby with 10 m high walls of green Aosta serpentine, conference centre on
/// the 34th, a restaurant and bar on the 35th floor, offices in between (some can be bought – RealEstate module).
/// Names are our own: real brand names (e.g. of the restaurant) are not used, see CLAUDE.md.
/// Idempotent: floors are matched by storey and updated on every start.
/// </summary>
internal static class PrimeTowerFloors
{
    internal sealed record Floor(int Storey, string Name, string Theme, int Width, int Depth, int Capacity, List<RoomItem> Layout);

    internal static IEnumerable<Floor> Floors() =>
    [
        new(0, "Lobby", RoomThemes.Lobby, 30, 20, 80, Lobby()),
        new(12, "Coworking", RoomThemes.Coworking, 22, 16, 20, Coworking()),
        new(24, "Sky Office", RoomThemes.Coworking, 22, 16, 20, SkyOffice()),
        new(34, "Konferenzzentrum", RoomThemes.Conference, 24, 16, 25, Conference()),
        new(35, "Sky Lounge", RoomThemes.SkyLounge, 22, 16, 30, SkyLounge()),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<RoomsDbContext>();
        var owner = TowerOwners.PrimeTower;
        var building = ZurichBuildings.PrimeTowerId;
        foreach (var floor in Floors())
        {
            var room = await db.Rooms.SingleOrDefaultAsync(r => r.OwnerId == owner && r.BuildingId == building && r.Floor == floor.Storey, ct);
            if (room is null)
            {
                room = Room.Create(owner, building, floor.Name, isPublic: true, floor.Theme);
                db.Rooms.Add(room);
            }
            room.Rename(floor.Name);
            room.ChangeTheme(floor.Theme);
            room.Resize(floor.Width, floor.Depth);
            room.PlaceOnFloor(floor.Storey, floor.Capacity);
            room.ReplaceLayout(floor.Layout);
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Ground floor (30 × 20 m): serpentine wall with two lift banks (low-rise and high-rise group, as in the
    /// real tower), access gates in front of them, reception, lobby café at the glass and two waiting lounges –
    /// the lobby is where people wait for the lift queue.
    /// </summary>
    private static List<RoomItem> Lobby() =>
    [
        Cell("custom-elevator", 16, 35, 0),   // Lift
        Cell("custom-elevator", 34, 35, 0),   // Lift
        Cell("ph-Ottoman_01", 27, 38, 180),   // Hocker
        Cell("ph-Ottoman_01", 31, 38, 180),   // Hocker
        Cell("custom-turnstiles", 24, 27, 0),   // Drehkreuze
        Cell("custom-queuelane", 24, 20, 0),   // Absperrung
        Cell("custom-reception", 8, 24, 0),   // Empfang
        Cell("ph-GreenChair_01", 10, 27, 180),   // Samtstuhl
        Cell("ph-GreenChair_01", 13, 27, 180),   // Samtstuhl
        Cell("ph-Shelf_01", 6, 31, 180),   // Metallregal
        Cell("ph-Shelf_01", 8, 31, 180),   // Metallregal
        Cell("custom-ruground", 44, 29, 0),   // Runder Teppich
        Cell("custom-hologram", 46, 31, 0),   // Hologramm
        Cell("ph-side_table_tall_01", 24, 6, 180),   // Hoher Beistelltisch
        Cell("ph-marble_bust_01", 24, 6, 180),   // Marmorbüste
        Cell("ph-side_table_tall_01", 36, 6, 180),   // Hoher Beistelltisch
        Cell("ph-marble_bust_01", 36, 6, 180),   // Marmorbüste
        Cell("custom-rug", 6, 8, 0),   // Teppich
        Cell("ph-sofa_02", 8, 12, 180),   // Chesterfield-Sofa
        Cell("ph-sofa_02", 8, 6, 0),   // Chesterfield-Sofa
        Cell("ph-modern_coffee_table_01", 10, 8, 180),   // Moderner Couchtisch
        Cell("ph-tea_set_01", 10, 9, 270),   // Teeservice
        Cell("ph-mid_century_lounge_chair", 4, 9, 90),   // Lounge Chair
        Cell("ph-mid_century_lounge_chair", 14, 9, 270),   // Lounge Chair
        Cell("custom-ruground", 46, 7, 0),   // Runder Teppich
        Cell("ph-coffee_table_round_01", 48, 8, 180),   // Runder Couchtisch
        Cell("ph-ceramic_vase_01", 48, 10, 180),   // Vase
        Cell("ph-modern_arm_chair_01", 45, 9, 90),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 51, 9, 270),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 48, 6, 0),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 48, 12, 180),   // Moderner Sessel
        Cell("custom-skybar", 53, 19, 90),   // Bar
        Cell("ph-pachira_aquatica_01", 1, 37, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 57, 37, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 57, 1, 180),   // Glückskastanie
        Cell("ph-potted_plant_02", 1, 2, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 14, 38, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 44, 38, 180),   // Zimmerpflanze
        Cell("custom-planter", 58, 14, 90),   // Pflanztrog
        Cell("custom-planter", 58, 33, 90),   // Pflanztrog
        Cell("custom-planter", 16, 1, 0),   // Pflanztrog
        Cell("custom-planter", 40, 1, 0),   // Pflanztrog
        Cell("ph-bar_chair_round_01", 52, 20, 90),   // Barstuhl
        Cell("ph-bar_chair_round_01", 52, 23, 90),   // Barstuhl
        Cell("ph-bar_chair_round_01", 52, 25, 90),   // Barstuhl
        Cell("ph-bar_chair_round_01", 52, 28, 90),   // Barstuhl
    ];

    /// <summary>Coworking (22 × 16 m): two desk islands, glass meeting room, café counter, lounge, lift core in the middle.</summary>
    private static List<RoomItem> Coworking() =>
    [
        Cell("custom-elevator", 17, 16, 0),   // Lift
        Cell("ph-dining_table", 6, 24, 180),   // Esstisch
        Cell("ph-dining_chair_02", 6, 23, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 9, 23, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 6, 27, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 9, 27, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 4, 25, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 11, 25, 270),   // Esszimmerstuhl
        Cell("custom-divider", 14, 22, 90),   // Raumteiler
        Cell("custom-divider", 5, 21, 0),   // Raumteiler
        Cell("custom-openkitchen", 32, 29, 0),   // Offene Küche
        Cell("ph-side_table_tall_01", 33, 25, 180),   // Hoher Beistelltisch
        Cell("ph-side_table_tall_01", 38, 25, 180),   // Hoher Beistelltisch
        Cell("ph-bar_chair_round_01", 33, 24, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 38, 24, 0),   // Barstuhl
        Cell("custom-rug", 37, 12, 90),   // Teppich
        Cell("ph-sofa_03", 41, 12, 270),   // Ledersofa
        Cell("ph-coffee_table_round_01", 38, 14, 180),   // Runder Couchtisch
        Cell("ph-modern_arm_chair_01", 35, 13, 90),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 35, 16, 90),   // Moderner Sessel
        Cell("game-tictactoe", 30, 20, 0),   // Tic-Tac-Toe-Tisch
        Cell("ph-dining_chair_02", 29, 20, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 33, 20, 270),   // Esszimmerstuhl
        Cell("ph-Shelf_01", 16, 31, 180),   // Metallregal
        Cell("ph-Shelf_01", 18, 31, 180),   // Metallregal
        Cell("ph-pachira_aquatica_01", 1, 29, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 41, 1, 180),   // Glückskastanie
        Cell("ph-potted_plant_02", 1, 1, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 41, 30, 180),   // Zimmerpflanze
        Cell("ph-metal_office_desk", 3, 4, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 4, 3, 0),   // Esszimmerstuhl
        Cell("ph-desk_lamp_arm_01", 6, 5, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 3, 6, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 4, 9, 180),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 7, 4, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 8, 3, 0),   // Esszimmerstuhl
        Cell("ph-desk_lamp_arm_01", 10, 5, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 7, 6, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 8, 9, 180),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 11, 4, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 13, 3, 0),   // Esszimmerstuhl
        Cell("ph-desk_lamp_arm_01", 14, 5, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 11, 6, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 13, 9, 180),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 29, 4, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 30, 3, 0),   // Esszimmerstuhl
        Cell("ph-desk_lamp_arm_01", 32, 5, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 29, 6, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 30, 9, 180),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 33, 4, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 34, 3, 0),   // Esszimmerstuhl
        Cell("ph-desk_lamp_arm_01", 36, 5, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 33, 6, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 34, 9, 180),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 37, 4, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 39, 3, 0),   // Esszimmerstuhl
        Cell("ph-desk_lamp_arm_01", 40, 5, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 37, 6, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 39, 9, 180),   // Esszimmerstuhl
    ];

    /// <summary>Sky Office (22 × 16 m): executive desks, boardroom table, lounge, quiz screen, sideboard, viewer.</summary>
    private static List<RoomItem> SkyOffice() =>
    [
        Cell("custom-elevator", 17, 16, 0),   // Lift
        Cell("ph-metal_office_desk", 5, 9, 90),   // Bürotisch
        Cell("ph-ArmChair_01", 3, 10, 90),   // Ohrensessel
        Cell("ph-desk_lamp_arm_01", 6, 12, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 5, 17, 90),   // Bürotisch
        Cell("ph-ArmChair_01", 3, 18, 90),   // Ohrensessel
        Cell("ph-desk_lamp_arm_01", 6, 20, 180),   // Schreibtischlampe
        Cell("ph-Shelf_01", 0, 8, 90),   // Metallregal
        Cell("ph-Shelf_01", 0, 14, 90),   // Metallregal
        Cell("ph-Shelf_01", 0, 20, 90),   // Metallregal
        Cell("ph-dining_table", 11, 25, 180),   // Esstisch
        Cell("ph-GreenChair_01", 12, 24, 0),   // Samtstuhl
        Cell("ph-GreenChair_01", 14, 24, 0),   // Samtstuhl
        Cell("ph-GreenChair_01", 12, 28, 180),   // Samtstuhl
        Cell("ph-GreenChair_01", 14, 28, 180),   // Samtstuhl
        Cell("ph-GreenChair_01", 10, 26, 90),   // Samtstuhl
        Cell("ph-GreenChair_01", 16, 26, 270),   // Samtstuhl
        Cell("custom-rug", 12, 4, 0),   // Teppich
        Cell("ph-sofa_02", 14, 2, 0),   // Chesterfield-Sofa
        Cell("ph-modern_coffee_table_01", 16, 4, 180),   // Moderner Couchtisch
        Cell("ph-tea_set_01", 16, 5, 270),   // Teeservice
        Cell("ph-mid_century_lounge_chair", 11, 6, 90),   // Lounge Chair
        Cell("ph-mid_century_lounge_chair", 19, 6, 270),   // Lounge Chair
        Cell("game-quiz", 42, 8, 90),   // Quiz-TV
        Cell("ph-ArmChair_01", 36, 7, 90),   // Ohrensessel
        Cell("ph-ArmChair_01", 36, 11, 90),   // Ohrensessel
        Cell("ph-side_table_01", 36, 10, 180),   // Beistelltisch rund
        Cell("game-tictactoe", 30, 24, 0),   // Tic-Tac-Toe-Tisch
        Cell("ph-dining_chair_02", 29, 25, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 33, 25, 270),   // Esszimmerstuhl
        Cell("ph-modern_wooden_cabinet", 32, 30, 180),   // Sideboard
        Cell("ph-ceramic_vase_01", 33, 30, 180),   // Vase
        Cell("ph-tea_set_01", 35, 30, 180),   // Teeservice
        Cell("custom-telescope", 42, 21, 90),   // Fernrohr
        Cell("ph-pachira_aquatica_01", 1, 29, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 41, 29, 180),   // Glückskastanie
        Cell("ph-potted_plant_02", 41, 1, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 25, 1, 180),   // Zimmerpflanze
        Cell("custom-ruground", 30, 3, 0),   // Runder Teppich
        Cell("ph-sofa_03", 30, 2, 0),   // Ledersofa
        Cell("ph-coffee_table_round_01", 32, 4, 180),   // Runder Couchtisch
        Cell("ph-ceramic_vase_01", 32, 6, 180),   // Vase
        Cell("ph-GreenChair_01", 29, 7, 90),   // Samtstuhl
        Cell("ph-GreenChair_01", 35, 7, 270),   // Samtstuhl
    ];

    /// <summary>Konferenzzentrum (24 × 16 m): hall with stage and presentation wall, boardroom behind glass, foyer.</summary>
    private static List<RoomItem> Conference() =>
    [
        Cell("custom-elevator", 39, 11, 90),   // Lift
        Cell("custom-screenwall", 10, 26, 0),   // Videowand
        Cell("ph-dining_table", 34, 26, 180),   // Esstisch
        Cell("ph-GreenChair_01", 34, 25, 0),   // Samtstuhl
        Cell("ph-GreenChair_01", 37, 25, 0),   // Samtstuhl
        Cell("ph-GreenChair_01", 34, 29, 180),   // Samtstuhl
        Cell("ph-GreenChair_01", 37, 29, 180),   // Samtstuhl
        Cell("ph-GreenChair_01", 32, 27, 90),   // Samtstuhl
        Cell("ph-GreenChair_01", 39, 27, 270),   // Samtstuhl
        Cell("custom-divider", 30, 25, 90),   // Raumteiler
        Cell("custom-divider", 35, 23, 0),   // Raumteiler
        Cell("ph-side_table_tall_01", 30, 6, 180),   // Hoher Beistelltisch
        Cell("ph-side_table_tall_01", 36, 6, 180),   // Hoher Beistelltisch
        Cell("ph-bar_chair_round_01", 30, 4, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 36, 4, 0),   // Barstuhl
        Cell("ph-modern_wooden_cabinet", 46, 4, 270),   // Sideboard
        Cell("ph-tea_set_01", 46, 5, 270),   // Teeservice
        Cell("ph-pachira_aquatica_01", 1, 29, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 45, 1, 180),   // Glückskastanie
        Cell("ph-potted_plant_02", 1, 1, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 45, 30, 180),   // Zimmerpflanze
        Cell("ph-dining_chair_02", 6, 23, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 8, 23, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 10, 23, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 12, 23, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 18, 23, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 20, 23, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 22, 23, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 24, 23, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 6, 19, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 8, 19, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 10, 19, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 12, 19, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 18, 19, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 20, 19, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 22, 19, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 24, 19, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 6, 15, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 8, 15, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 10, 15, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 12, 15, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 18, 15, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 20, 15, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 22, 15, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 24, 15, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 6, 11, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 8, 11, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 10, 11, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 12, 11, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 18, 11, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 20, 11, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 22, 11, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 24, 11, 0),   // Esszimmerstuhl
    ];

    /// <summary>
    /// Sky Lounge, 35th floor: modelled on the real top-floor restaurant and bar: cocktail bar with a
    /// back bar along the north facade, open kitchen with a bistro in front, restaurant tables, a sunset
    /// lounge facing the Uetliberg (west), a Prive with the quiz screen behind a glass partition, a DJ booth,
    /// panorama viewers at the glass and the lift core in the middle.
    /// Glass facade, luminous "sky" ceiling and LED lines come from the skylounge theme (client).
    /// Furniture: realistic Poly Haven models ("ph-*") plus custom pieces built by the client.
    /// </summary>
    private static List<RoomItem> SkyLounge() =>
    [
        Cell("custom-backbar", 27, 30, 0),   // Rückbuffet
        Cell("custom-skybar", 27, 26, 0),   // Bar
        Cell("custom-pendant", 28, 26, 0),   // Hängeleuchten
        Cell("custom-pendant", 34, 26, 0),   // Hängeleuchten
        Cell("custom-openkitchen", 41, 14, 90),   // Offene Küche
        Cell("ph-side_table_tall_01", 35, 16, 180),   // Hoher Beistelltisch
        Cell("ph-side_table_tall_01", 35, 20, 180),   // Hoher Beistelltisch
        Cell("ph-bar_chair_round_01", 33, 16, 90),   // Barstuhl
        Cell("ph-bar_chair_round_01", 37, 16, 270),   // Barstuhl
        Cell("ph-bar_chair_round_01", 33, 20, 90),   // Barstuhl
        Cell("ph-bar_chair_round_01", 37, 20, 270),   // Barstuhl
        Cell("custom-rug", 4, 24, 0),   // Teppich
        Cell("ph-sofa_02", 5, 29, 180),   // Chesterfield-Sofa
        Cell("ph-mid_century_lounge_chair", 2, 25, 90),   // Lounge Chair
        Cell("ph-mid_century_lounge_chair", 11, 25, 270),   // Lounge Chair
        Cell("ph-modern_coffee_table_01", 7, 25, 180),   // Moderner Couchtisch
        Cell("ph-tea_set_01", 7, 26, 270),   // Teeservice
        Cell("ph-Ottoman_01", 6, 22, 180),   // Hocker
        Cell("custom-pendant", 7, 26, 0),   // Hängeleuchten
        Cell("ph-side_table_tall_01", 1, 21, 180),   // Hoher Beistelltisch
        Cell("ph-marble_bust_01", 1, 21, 180),   // Marmorbüste
        Cell("custom-rug", 2, 10, 90),   // Teppich
        Cell("ph-sofa_03", 1, 11, 90),   // Ledersofa
        Cell("ph-coffee_table_round_01", 3, 12, 180),   // Runder Couchtisch
        Cell("ph-modern_arm_chair_01", 7, 11, 270),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 7, 15, 270),   // Moderner Sessel
        Cell("ph-potted_plant_02", 1, 18, 180),   // Zimmerpflanze
        Cell("custom-djbooth", 17, 29, 0),   // DJ-Pult
        Cell("custom-ledstrip", 16, 27, 0),   // LED-Band
        Cell("custom-elevator", 17, 16, 0),   // Lift
        Cell("game-tictactoe", 11, 17, 0),   // Tic-Tac-Toe-Tisch
        Cell("ph-dining_chair_02", 10, 18, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 14, 18, 270),   // Esszimmerstuhl
        Cell("custom-divider", 30, 3, 90),   // Raumteiler
        Cell("custom-rug", 34, 2, 90),   // Teppich
        Cell("game-quiz", 41, 4, 90),   // Quiz-TV
        Cell("ph-modern_arm_chair_01", 35, 3, 90),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 35, 7, 90),   // Moderner Sessel
        Cell("ph-side_table_01", 35, 6, 180),   // Beistelltisch rund
        Cell("ph-ceramic_vase_01", 35, 6, 180),   // Vase
        Cell("custom-telescope", 14, 30, 0),   // Fernrohr
        Cell("custom-telescope", 1, 8, 270),   // Fernrohr
        Cell("custom-telescope", 42, 11, 90),   // Fernrohr
        Cell("ph-pachira_aquatica_01", 1, 29, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 41, 29, 180),   // Glückskastanie
        Cell("ph-potted_plant_02", 1, 1, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 41, 1, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 24, 30, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 27, 1, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 38, 12, 180),   // Zimmerpflanze
        Cell("ph-bar_chair_round_01", 28, 25, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 30, 25, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 32, 25, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 34, 25, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 36, 25, 0),   // Barstuhl
        Cell("ph-round_wooden_table_01", 3, 3, 180),   // Runder Holztisch
        Cell("ph-dining_chair_02", 2, 4, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 6, 4, 270),   // Esszimmerstuhl
        Cell("custom-pendant", 4, 4, 0),   // Hängeleuchten
        Cell("ph-round_wooden_table_01", 9, 3, 180),   // Runder Holztisch
        Cell("ph-dining_chair_02", 8, 4, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 12, 4, 270),   // Esszimmerstuhl
        Cell("custom-pendant", 10, 4, 0),   // Hängeleuchten
        Cell("ph-round_wooden_table_01", 15, 3, 180),   // Runder Holztisch
        Cell("ph-dining_chair_02", 14, 4, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 18, 4, 270),   // Esszimmerstuhl
        Cell("custom-pendant", 16, 4, 0),   // Hängeleuchten
    ];

}
