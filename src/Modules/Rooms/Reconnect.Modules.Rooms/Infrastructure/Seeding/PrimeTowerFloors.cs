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
        Cell("custom-elevator", 32, 69, 0),   // Lift
        Cell("custom-elevator", 68, 69, 0),   // Lift
        Cell("ph-Ottoman_01", 54, 76, 180),   // Hocker
        Cell("ph-Ottoman_01", 62, 76, 180),   // Hocker
        Cell("custom-turnstiles", 48, 54, 0),   // Drehkreuze
        Cell("custom-queuelane", 48, 40, 0),   // Absperrung
        Cell("custom-reception", 16, 48, 0),   // Empfang
        Cell("ph-GreenChair_01", 20, 53, 180),   // Samtstuhl
        Cell("ph-GreenChair_01", 26, 53, 180),   // Samtstuhl
        Cell("ph-Shelf_01", 12, 62, 180),   // Metallregal
        Cell("ph-Shelf_01", 16, 62, 180),   // Metallregal
        Cell("custom-ruground", 88, 58, 0),   // Runder Teppich
        Cell("custom-hologram", 92, 62, 0),   // Hologramm
        Cell("ph-side_table_tall_01", 48, 12, 180),   // Hoher Beistelltisch
        At("ph-marble_bust_01", 12.25f, 3.25f, 180),   // Marmorbüste
        Cell("ph-side_table_tall_01", 72, 12, 180),   // Hoher Beistelltisch
        At("ph-marble_bust_01", 18.25f, 3.25f, 180),   // Marmorbüste
        Cell("custom-rug", 12, 16, 0),   // Teppich
        Cell("ph-sofa_02", 16, 24, 180),   // Chesterfield-Sofa
        Cell("ph-sofa_02", 16, 12, 0),   // Chesterfield-Sofa
        Cell("ph-modern_coffee_table_01", 20, 16, 180),   // Moderner Couchtisch
        At("ph-tea_set_01", 5.375f, 4.75f, 270),   // Teeservice
        Cell("ph-mid_century_lounge_chair", 8, 18, 90),   // Lounge Chair
        Cell("ph-mid_century_lounge_chair", 28, 18, 270),   // Lounge Chair
        Cell("custom-ruground", 92, 14, 0),   // Runder Teppich
        Cell("ph-coffee_table_round_01", 96, 16, 180),   // Runder Couchtisch
        At("ph-ceramic_vase_01", 24.375f, 4.875f, 180),   // Vase
        Cell("ph-modern_arm_chair_01", 92, 18, 90),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 101, 18, 270),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 96, 12, 0),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 96, 21, 180),   // Moderner Sessel
        Cell("custom-skybar", 106, 38, 90),   // Bar
        Cell("ph-pachira_aquatica_01", 2, 74, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 114, 74, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 114, 2, 180),   // Glückskastanie
        Cell("ph-potted_plant_02", 2, 4, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 28, 76, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 88, 76, 180),   // Zimmerpflanze
        Cell("custom-planter", 116, 28, 90),   // Pflanztrog
        Cell("custom-planter", 116, 66, 90),   // Pflanztrog
        Cell("custom-planter", 32, 2, 0),   // Pflanztrog
        Cell("custom-planter", 80, 2, 0),   // Pflanztrog
        Cell("ph-bar_chair_round_01", 104, 40, 90),   // Barstuhl
        Cell("ph-bar_chair_round_01", 104, 46, 90),   // Barstuhl
        Cell("ph-bar_chair_round_01", 104, 50, 90),   // Barstuhl
        Cell("ph-bar_chair_round_01", 104, 56, 90),   // Barstuhl
    ];

    /// <summary>Coworking (22 × 16 m): two desk islands, glass meeting room, café counter, lounge, lift core in the middle.</summary>
    private static List<RoomItem> Coworking() =>
    [
        Cell("custom-elevator", 34, 32, 0),   // Lift
        Cell("ph-dining_table", 12, 48, 180),   // Esstisch
        Cell("ph-dining_chair_02", 12, 46, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 18, 46, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 12, 54, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 18, 54, 180),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 10, 50, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 21, 50, 270),   // Esszimmerstuhl
        Cell("custom-divider", 28, 44, 90),   // Raumteiler
        Cell("custom-divider", 10, 42, 0),   // Raumteiler
        Cell("custom-openkitchen", 64, 58, 0),   // Offene Küche
        Cell("ph-side_table_tall_01", 66, 50, 180),   // Hoher Beistelltisch
        Cell("ph-side_table_tall_01", 76, 50, 180),   // Hoher Beistelltisch
        Cell("ph-bar_chair_round_01", 66, 48, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 76, 48, 0),   // Barstuhl
        Cell("custom-rug", 74, 24, 90),   // Teppich
        Cell("ph-sofa_03", 82, 24, 270),   // Ledersofa
        Cell("ph-coffee_table_round_01", 76, 28, 180),   // Runder Couchtisch
        Cell("ph-modern_arm_chair_01", 72, 26, 90),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 72, 32, 90),   // Moderner Sessel
        Cell("game-tictactoe", 60, 40, 0),   // Tic-Tac-Toe-Tisch
        Cell("ph-dining_chair_02", 58, 40, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 67, 40, 270),   // Esszimmerstuhl
        Cell("ph-Shelf_01", 32, 62, 180),   // Metallregal
        Cell("ph-Shelf_01", 36, 62, 180),   // Metallregal
        Cell("ph-pachira_aquatica_01", 2, 58, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 82, 2, 180),   // Glückskastanie
        Cell("ph-potted_plant_02", 2, 2, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 82, 60, 180),   // Zimmerpflanze
        Cell("ph-metal_office_desk", 6, 8, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 8, 6, 0),   // Esszimmerstuhl
        At("ph-desk_lamp_arm_01", 3.25f, 2.625f, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 6, 12, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 8, 16, 180),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 14, 8, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 16, 6, 0),   // Esszimmerstuhl
        At("ph-desk_lamp_arm_01", 5.25f, 2.625f, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 14, 12, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 16, 16, 180),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 22, 8, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 26, 6, 0),   // Esszimmerstuhl
        At("ph-desk_lamp_arm_01", 7.25f, 2.625f, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 22, 12, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 26, 16, 180),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 58, 8, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 60, 6, 0),   // Esszimmerstuhl
        At("ph-desk_lamp_arm_01", 16.25f, 2.625f, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 58, 12, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 60, 16, 180),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 66, 8, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 68, 6, 0),   // Esszimmerstuhl
        At("ph-desk_lamp_arm_01", 18.25f, 2.625f, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 66, 12, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 68, 16, 180),   // Esszimmerstuhl
        Cell("ph-metal_office_desk", 74, 8, 180),   // Bürotisch
        Cell("ph-dining_chair_02", 78, 6, 0),   // Esszimmerstuhl
        At("ph-desk_lamp_arm_01", 20.25f, 2.625f, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 74, 12, 0),   // Bürotisch
        Cell("ph-dining_chair_02", 78, 16, 180),   // Esszimmerstuhl
    ];

    /// <summary>Sky Office (22 × 16 m): executive desks, boardroom table, lounge, quiz screen, sideboard, viewer.</summary>
    private static List<RoomItem> SkyOffice() =>
    [
        Cell("custom-elevator", 34, 32, 0),   // Lift
        Cell("ph-metal_office_desk", 10, 18, 90),   // Bürotisch
        Cell("ph-ArmChair_01", 7, 20, 90),   // Ohrensessel
        At("ph-desk_lamp_arm_01", 3.25f, 6.125f, 180),   // Schreibtischlampe
        Cell("ph-metal_office_desk", 10, 34, 90),   // Bürotisch
        Cell("ph-ArmChair_01", 7, 36, 90),   // Ohrensessel
        At("ph-desk_lamp_arm_01", 3.25f, 10.125f, 180),   // Schreibtischlampe
        Cell("ph-Shelf_01", 0, 16, 90),   // Metallregal
        Cell("ph-Shelf_01", 0, 28, 90),   // Metallregal
        Cell("ph-Shelf_01", 0, 40, 90),   // Metallregal
        Cell("ph-dining_table", 22, 50, 180),   // Esstisch
        Cell("ph-GreenChair_01", 24, 47, 0),   // Samtstuhl
        Cell("ph-GreenChair_01", 28, 47, 0),   // Samtstuhl
        Cell("ph-GreenChair_01", 24, 56, 180),   // Samtstuhl
        Cell("ph-GreenChair_01", 28, 56, 180),   // Samtstuhl
        Cell("ph-GreenChair_01", 19, 52, 90),   // Samtstuhl
        Cell("ph-GreenChair_01", 31, 52, 270),   // Samtstuhl
        Cell("custom-rug", 24, 8, 0),   // Teppich
        Cell("ph-sofa_02", 28, 4, 0),   // Chesterfield-Sofa
        Cell("ph-modern_coffee_table_01", 32, 8, 180),   // Moderner Couchtisch
        At("ph-tea_set_01", 8.375f, 2.75f, 270),   // Teeservice
        Cell("ph-mid_century_lounge_chair", 22, 12, 90),   // Lounge Chair
        Cell("ph-mid_century_lounge_chair", 38, 12, 270),   // Lounge Chair
        Cell("game-quiz", 84, 16, 90),   // Quiz-TV
        Cell("ph-ArmChair_01", 72, 14, 90),   // Ohrensessel
        Cell("ph-ArmChair_01", 72, 22, 90),   // Ohrensessel
        Cell("ph-side_table_01", 72, 20, 180),   // Beistelltisch rund
        Cell("game-tictactoe", 60, 48, 0),   // Tic-Tac-Toe-Tisch
        Cell("ph-dining_chair_02", 58, 50, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 67, 50, 270),   // Esszimmerstuhl
        Cell("ph-modern_wooden_cabinet", 64, 60, 180),   // Sideboard
        At("ph-ceramic_vase_01", 16.75f, 15.25f, 180),   // Vase
        At("ph-tea_set_01", 18f, 15.25f, 180),   // Teeservice
        Cell("custom-telescope", 84, 42, 90),   // Fernrohr
        Cell("ph-pachira_aquatica_01", 2, 58, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 82, 58, 180),   // Glückskastanie
        Cell("ph-potted_plant_02", 82, 2, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 50, 2, 180),   // Zimmerpflanze
        Cell("custom-ruground", 60, 6, 0),   // Runder Teppich
        Cell("ph-sofa_03", 60, 4, 0),   // Ledersofa
        Cell("ph-coffee_table_round_01", 64, 8, 180),   // Runder Couchtisch
        At("ph-ceramic_vase_01", 16.375f, 2.875f, 180),   // Vase
        Cell("ph-GreenChair_01", 61, 12, 90),   // Samtstuhl
        Cell("ph-GreenChair_01", 69, 12, 270),   // Samtstuhl
    ];

    /// <summary>Konferenzzentrum (24 × 16 m): hall with stage and presentation wall, boardroom behind glass, foyer.</summary>
    private static List<RoomItem> Conference() =>
    [
        Cell("custom-elevator", 78, 22, 90),   // Lift
        Cell("custom-screenwall", 20, 52, 0),   // Videowand
        Cell("ph-dining_table", 68, 52, 180),   // Esstisch
        Cell("ph-GreenChair_01", 68, 49, 0),   // Samtstuhl
        Cell("ph-GreenChair_01", 74, 49, 0),   // Samtstuhl
        Cell("ph-GreenChair_01", 68, 58, 180),   // Samtstuhl
        Cell("ph-GreenChair_01", 74, 58, 180),   // Samtstuhl
        Cell("ph-GreenChair_01", 65, 54, 90),   // Samtstuhl
        Cell("ph-GreenChair_01", 77, 54, 270),   // Samtstuhl
        Cell("custom-divider", 60, 46, 90),   // Raumteiler
        Cell("ph-side_table_tall_01", 60, 12, 180),   // Hoher Beistelltisch
        Cell("ph-side_table_tall_01", 72, 12, 180),   // Hoher Beistelltisch
        Cell("ph-bar_chair_round_01", 60, 10, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 72, 10, 0),   // Barstuhl
        Cell("ph-modern_wooden_cabinet", 92, 8, 270),   // Sideboard
        At("ph-tea_set_01", 23.25f, 3f, 270),   // Teeservice
        Cell("ph-pachira_aquatica_01", 2, 58, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 90, 2, 180),   // Glückskastanie
        Cell("ph-potted_plant_02", 2, 2, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 90, 60, 180),   // Zimmerpflanze
        Cell("ph-dining_chair_02", 12, 46, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 16, 46, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 20, 46, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 24, 46, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 36, 46, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 40, 46, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 44, 46, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 48, 46, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 12, 38, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 16, 38, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 20, 38, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 24, 38, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 36, 38, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 40, 38, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 44, 38, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 48, 38, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 12, 30, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 16, 30, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 20, 30, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 24, 30, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 36, 30, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 40, 30, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 44, 30, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 48, 30, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 12, 22, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 16, 22, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 20, 22, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 24, 22, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 36, 22, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 40, 22, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 44, 22, 0),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 48, 22, 0),   // Esszimmerstuhl
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
        Cell("custom-backbar", 54, 60, 0),   // Rückbuffet
        Cell("custom-skybar", 54, 52, 0),   // Bar
        Cell("custom-pendant", 56, 52, 0),   // Hängeleuchten
        Cell("custom-pendant", 68, 52, 0),   // Hängeleuchten
        Cell("custom-openkitchen", 82, 28, 90),   // Offene Küche
        Cell("ph-side_table_tall_01", 70, 32, 180),   // Hoher Beistelltisch
        Cell("ph-side_table_tall_01", 70, 40, 180),   // Hoher Beistelltisch
        Cell("ph-bar_chair_round_01", 68, 32, 90),   // Barstuhl
        Cell("ph-bar_chair_round_01", 72, 32, 270),   // Barstuhl
        Cell("ph-bar_chair_round_01", 68, 40, 90),   // Barstuhl
        Cell("ph-bar_chair_round_01", 72, 40, 270),   // Barstuhl
        Cell("custom-rug", 8, 48, 0),   // Teppich
        Cell("ph-sofa_02", 10, 58, 180),   // Chesterfield-Sofa
        Cell("ph-mid_century_lounge_chair", 4, 50, 90),   // Lounge Chair
        Cell("ph-mid_century_lounge_chair", 22, 50, 270),   // Lounge Chair
        Cell("ph-modern_coffee_table_01", 14, 50, 180),   // Moderner Couchtisch
        At("ph-tea_set_01", 3.875f, 13.25f, 270),   // Teeservice
        Cell("ph-Ottoman_01", 12, 44, 180),   // Hocker
        Cell("custom-pendant", 14, 52, 0),   // Hängeleuchten
        Cell("ph-side_table_tall_01", 2, 42, 180),   // Hoher Beistelltisch
        At("ph-marble_bust_01", 0.75f, 10.75f, 180),   // Marmorbüste
        Cell("custom-rug", 4, 20, 90),   // Teppich
        Cell("ph-sofa_03", 2, 22, 90),   // Ledersofa
        Cell("ph-coffee_table_round_01", 6, 24, 180),   // Runder Couchtisch
        Cell("ph-modern_arm_chair_01", 11, 22, 270),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 11, 28, 270),   // Moderner Sessel
        Cell("ph-potted_plant_02", 2, 36, 180),   // Zimmerpflanze
        Cell("custom-djbooth", 34, 58, 0),   // DJ-Pult
        Cell("custom-ledstrip", 32, 54, 0),   // LED-Band
        Cell("custom-elevator", 34, 32, 0),   // Lift
        Cell("game-tictactoe", 22, 34, 0),   // Tic-Tac-Toe-Tisch
        Cell("ph-dining_chair_02", 20, 36, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 29, 36, 270),   // Esszimmerstuhl
        Cell("custom-divider", 60, 6, 90),   // Raumteiler
        Cell("custom-rug", 68, 4, 90),   // Teppich
        Cell("game-quiz", 82, 8, 90),   // Quiz-TV
        Cell("ph-modern_arm_chair_01", 70, 6, 90),   // Moderner Sessel
        Cell("ph-modern_arm_chair_01", 70, 14, 90),   // Moderner Sessel
        Cell("ph-side_table_01", 70, 12, 180),   // Beistelltisch rund
        At("ph-ceramic_vase_01", 17.75f, 3.25f, 180),   // Vase
        Cell("custom-telescope", 28, 60, 0),   // Fernrohr
        Cell("custom-telescope", 2, 16, 270),   // Fernrohr
        Cell("custom-telescope", 84, 22, 90),   // Fernrohr
        Cell("ph-pachira_aquatica_01", 2, 58, 180),   // Glückskastanie
        Cell("ph-pachira_aquatica_01", 82, 58, 180),   // Glückskastanie
        Cell("ph-potted_plant_02", 2, 2, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 82, 2, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 48, 60, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 54, 2, 180),   // Zimmerpflanze
        Cell("ph-potted_plant_02", 76, 24, 180),   // Zimmerpflanze
        Cell("ph-bar_chair_round_01", 56, 50, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 60, 50, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 64, 50, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 68, 50, 0),   // Barstuhl
        Cell("ph-bar_chair_round_01", 72, 50, 0),   // Barstuhl
        Cell("ph-round_wooden_table_01", 6, 6, 180),   // Runder Holztisch
        Cell("ph-dining_chair_02", 4, 8, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 12, 8, 270),   // Esszimmerstuhl
        Cell("custom-pendant", 8, 8, 0),   // Hängeleuchten
        Cell("ph-round_wooden_table_01", 18, 6, 180),   // Runder Holztisch
        Cell("ph-dining_chair_02", 16, 8, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 24, 8, 270),   // Esszimmerstuhl
        Cell("custom-pendant", 20, 8, 0),   // Hängeleuchten
        Cell("ph-round_wooden_table_01", 30, 6, 180),   // Runder Holztisch
        Cell("ph-dining_chair_02", 28, 8, 90),   // Esszimmerstuhl
        Cell("ph-dining_chair_02", 36, 8, 270),   // Esszimmerstuhl
        Cell("custom-pendant", 32, 8, 0),   // Hängeleuchten
    ];

}
