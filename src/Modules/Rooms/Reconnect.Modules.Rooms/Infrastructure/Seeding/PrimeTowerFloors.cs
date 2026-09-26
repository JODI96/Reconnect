using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reconnect.Modules.City.Public;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Modules.Rooms.Hubs;
using static Reconnect.Modules.Rooms.Infrastructure.Seeding.ShowcaseRooms;

namespace Reconnect.Modules.Rooms.Infrastructure.Seeding;

/// <summary>
/// The public floors of the Prime Tower (all environments – they are part of the game, not dev data).
/// Real building: 36 storeys, 126 m; lobby with 10 m high walls of green Aosta serpentine, conference centre on
/// the 34th, "Clouds" on the 35th floor, offices in between (some of them can be bought – RealEstate module).
/// Idempotent: floors are matched by storey and updated on every start.
/// </summary>
internal static class PrimeTowerFloors
{
    private const string RoomHubElevatorItem = RoomHub.ElevatorItem;

    private sealed record Floor(int Storey, string Name, string Theme, int Width, int Depth, int Capacity, List<RoomItem> Layout);

    private static IEnumerable<Floor> Floors() =>
    [
        new(0, "Lobby", RoomThemes.Lobby, 30, 20, 80, Lobby()),
        new(12, "Coworking", RoomThemes.Coworking, 22, 16, 20, Coworking()),
        new(24, "Sky Office", RoomThemes.Coworking, 22, 16, 20, SkyOffice()),
        new(34, "Konferenzzentrum", RoomThemes.Conference, 24, 16, 25, Conference()),
        new(35, "Clouds", RoomThemes.SkyLounge, 22, 16, 30, Clouds()),
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
    private static List<RoomItem> Lobby()
    {
        var items = new List<RoomItem>
        {
            Item(RoomHubElevatorItem, 10.5f, 18.8f), Item(RoomHubElevatorItem, 19.5f, 18.8f),
            Ph("Ottoman_01", 14.2f, 19.2f), Ph("Ottoman_01", 15.8f, 19.2f),
            Item("custom-turnstiles", 15f, 14.2f),
            Item("custom-queuelane", 15f, 11.4f),

            // Reception with two staff seats behind the desk.
            Item("custom-reception", 6f, 12.6f),
            Ph("GreenChair_01", 5.1f, 13.7f, South), Ph("GreenChair_01", 6.9f, 13.7f, South),
            Ph("Shelf_01", 3.6f, 15.9f), Ph("Shelf_01", 4.7f, 15.9f),

            // Tower info hologram next to the café, busts flanking the entrance.
            Item("custom-ruground", 23.6f, 16.2f),
            Item("custom-hologram", 23.6f, 16.2f),
            Ph("side_table_tall_01", 12f, 3.2f), Ph("marble_bust_01", 12f, 3.2f),
            Ph("side_table_tall_01", 18f, 3.2f), Ph("marble_bust_01", 18f, 3.2f),

            // Waiting lounge west: two sofas facing each other.
            Item("custom-rug", 5f, 5f),
            Ph("sofa_02", 5f, 6.6f, South), Ph("sofa_02", 5f, 3.4f, North),
            Ph("modern_coffee_table_01", 5f, 5f), Ph("tea_set_01", 5f, 5f),
            Ph("mid_century_lounge_chair", 2.4f, 5f, East), Ph("mid_century_lounge_chair", 7.6f, 5f, West),

            // Waiting lounge east at the glass: four armchairs around a round table.
            Item("custom-ruground", 24.5f, 5f),
            Ph("coffee_table_round_01", 24.5f, 5f), Ph("ceramic_vase_01", 24.5f, 5f),
            Ph("modern_arm_chair_01", 23.2f, 5f, East), Ph("modern_arm_chair_01", 25.8f, 5f, West),
            Ph("modern_arm_chair_01", 24.5f, 3.7f, North), Ph("modern_arm_chair_01", 24.5f, 6.3f, South),

            // Lobby café along the street glass.
            Item("custom-skybar", 27.2f, 12.2f, West),

            // Plants.
            Ph("pachira_aquatica_01", 1f, 19f), Ph("pachira_aquatica_01", 29f, 19f),
            Ph("pachira_aquatica_01", 29f, 1f), Ph("potted_plant_02", 1f, 1f),
            Ph("potted_plant_04", 8.2f, 19.2f), Ph("potted_plant_04", 21.8f, 19.2f),
            Item("custom-planter", 29.2f, 8f, West), Item("custom-planter", 29.2f, 17.2f, West),
            Item("custom-planter", 9f, 0.6f), Item("custom-planter", 21f, 0.6f),
        };
        items.AddRange(Enumerable.Range(0, 4).Select(i => Ph("bar_chair_round_01", 26f, 10.4f + i * 1.2f, East)));
        return items;
    }

    /// <summary>Coworking (22 × 16 m): two desk islands, glass meeting room, café counter, lounge, lift core in the middle.</summary>
    private static List<RoomItem> Coworking()
    {
        var items = new List<RoomItem>
        {
            Item(RoomHubElevatorItem, 11f, 9.4f),

            // Meeting room behind glass (north-west).
            Ph("dining_table", 4f, 12.6f),
            Ph("dining_chair_02", 3.3f, 11.6f, North), Ph("dining_chair_02", 4.7f, 11.6f, North),
            Ph("dining_chair_02", 3.3f, 13.6f, South), Ph("dining_chair_02", 4.7f, 13.6f, South),
            Ph("dining_chair_02", 2.4f, 12.6f, East), Ph("dining_chair_02", 5.6f, 12.6f, West),
            Item("custom-divider", 7f, 12.6f, West), Item("custom-divider", 4f, 10.9f),

            // Café counter (north-east) with standing tables.
            Item("custom-openkitchen", 18f, 15.1f),
            Ph("side_table_tall_01", 16.8f, 12.8f), Ph("side_table_tall_01", 19.2f, 12.8f),
            Ph("bar_chair_round_01", 16.8f, 12f, North), Ph("bar_chair_round_01", 19.2f, 12f, North),

            // Lounge at the east glass.
            Item("custom-rug", 19.6f, 7.6f, West),
            Ph("sofa_03", 21f, 7.6f, West), Ph("coffee_table_round_01", 19.6f, 7.6f),
            Ph("modern_arm_chair_01", 18.2f, 6.8f, East), Ph("modern_arm_chair_01", 18.2f, 8.4f, East),

            // Tic-tac-toe next to the lift core.
            Item("game-tictactoe", 15.8f, 10.4f),
            Ph("dining_chair_02", 14.8f, 10.4f, East), Ph("dining_chair_02", 16.8f, 10.4f, West),

            // Shelves and plants.
            Ph("Shelf_01", 8.6f, 15.6f), Ph("Shelf_01", 9.7f, 15.6f),
            Ph("pachira_aquatica_01", 0.9f, 15.1f), Ph("pachira_aquatica_01", 21.1f, 0.9f),
            Ph("potted_plant_02", 0.8f, 0.8f), Ph("potted_plant_04", 21.2f, 15.2f),
        };
        items.AddRange(DeskIsland(2.3f));
        items.AddRange(DeskIsland(15.3f));
        return items;
    }

    /// <summary>Two rows of three desks back to back, each with chair, laptop and lamp (x = first desk).</summary>
    private static IEnumerable<RoomItem> DeskIsland(float x)
    {
        for (var i = 0; i < 3; i++)
        {
            var dx = x + i * 2.2f;
            yield return Ph("metal_office_desk", dx, 2.6f, South);
            yield return Ph("dining_chair_02", dx, 1.7f, North);
            yield return Ph("desk_lamp_arm_01", dx + 0.7f, 2.75f);
            yield return Ph("metal_office_desk", dx, 3.65f, North);
            yield return Ph("dining_chair_02", dx, 4.55f, South);
        }
    }

    /// <summary>Sky Office (22 × 16 m): executive desks, boardroom table, lounge, quiz screen, sideboard, viewer.</summary>
    private static List<RoomItem> SkyOffice() =>
    [
        Item(RoomHubElevatorItem, 11f, 9.4f),

        // Two executive desks with bookshelves behind (west).
        Ph("metal_office_desk", 3.2f, 5.6f, East), Ph("ArmChair_01", 2.2f, 5.6f, East),
        Ph("desk_lamp_arm_01", 3.3f, 6.3f),
        Ph("metal_office_desk", 3.2f, 9.4f, East), Ph("ArmChair_01", 2.2f, 9.4f, East),
        Ph("desk_lamp_arm_01", 3.3f, 10.1f),
        Ph("Shelf_01", 0.4f, 4.6f, East), Ph("Shelf_01", 0.4f, 7.5f, East), Ph("Shelf_01", 0.4f, 10.4f, East),

        // Boardroom table (north).
        Ph("dining_table", 6.8f, 13.4f),
        Ph("GreenChair_01", 6.1f, 12.4f, North), Ph("GreenChair_01", 7.5f, 12.4f, North),
        Ph("GreenChair_01", 6.1f, 14.4f, South), Ph("GreenChair_01", 7.5f, 14.4f, South),
        Ph("GreenChair_01", 5.2f, 13.4f, East), Ph("GreenChair_01", 8.4f, 13.4f, West),

        // Lounge (south-west of the core).
        Item("custom-rug", 8f, 3f),
        Ph("sofa_02", 8f, 1.6f, North), Ph("modern_coffee_table_01", 8f, 3f), Ph("tea_set_01", 8f, 3f),
        Ph("mid_century_lounge_chair", 6.2f, 3.6f, East), Ph("mid_century_lounge_chair", 9.8f, 3.6f, West),

        // Quiz screen with armchairs (east) and tic-tac-toe.
        Item("game-quiz", 21f, 5f, West),
        Ph("ArmChair_01", 18.3f, 4.1f, East), Ph("ArmChair_01", 18.3f, 5.9f, East), Ph("side_table_01", 18.3f, 5f),
        Item("game-tictactoe", 15.6f, 12.6f),
        Ph("dining_chair_02", 14.6f, 12.6f, East), Ph("dining_chair_02", 16.6f, 12.6f, West),

        // Sideboard with tea at the north glass, viewer at the east glass.
        Ph("modern_wooden_cabinet", 17.5f, 15.4f), Ph("ceramic_vase_01", 16.8f, 15.4f), Ph("tea_set_01", 18.2f, 15.4f),
        Item("custom-telescope", 21.2f, 10.6f, West),
        Ph("pachira_aquatica_01", 0.9f, 15.1f), Ph("pachira_aquatica_01", 21.1f, 15.1f),
        Ph("potted_plant_04", 21.2f, 0.8f), Ph("potted_plant_02", 12.8f, 0.8f),
        Item("custom-ruground", 16.5f, 3f),
        Ph("sofa_03", 16.5f, 1.4f, North), Ph("coffee_table_round_01", 16.5f, 3f), Ph("ceramic_vase_01", 16.5f, 3f),
        Ph("GreenChair_01", 14.9f, 3.6f, East), Ph("GreenChair_01", 18.1f, 3.6f, West),
    ];

    /// <summary>Konferenzzentrum (24 × 16 m): hall with stage and presentation wall, boardroom behind glass, foyer.</summary>
    private static List<RoomItem> Conference()
    {
        var items = new List<RoomItem>
        {
            Item(RoomHubElevatorItem, 20.6f, 8f, West),

            // Hall: stage with presentation wall (north-west).
            Item("custom-screenwall", 8f, 13.8f),

            // Boardroom behind glass (north-east).
            Ph("dining_table", 18f, 13.8f),
            Ph("GreenChair_01", 17.3f, 12.8f, North), Ph("GreenChair_01", 18.7f, 12.8f, North),
            Ph("GreenChair_01", 17.3f, 14.8f, South), Ph("GreenChair_01", 18.7f, 14.8f, South),
            Ph("GreenChair_01", 16.4f, 13.8f, East), Ph("GreenChair_01", 19.6f, 13.8f, West),
            Item("custom-divider", 15f, 13.8f, West), Item("custom-divider", 18f, 11.9f),

            // Foyer with standing tables and a coffee sideboard (south-east).
            Ph("side_table_tall_01", 15f, 3f), Ph("side_table_tall_01", 18f, 3f),
            Ph("bar_chair_round_01", 15f, 2.2f, North), Ph("bar_chair_round_01", 18f, 2.2f, North),
            Ph("modern_wooden_cabinet", 23.2f, 3.5f, West), Ph("tea_set_01", 23.2f, 3f),

            Ph("pachira_aquatica_01", 0.9f, 15.1f), Ph("pachira_aquatica_01", 23.1f, 0.9f),
            Ph("potted_plant_02", 0.8f, 0.8f), Ph("potted_plant_04", 23.2f, 15.2f),
        };

        // Audience: four rows facing the stage, with an aisle in the middle.
        foreach (var z in new[] { 10.8f, 9.6f, 8.4f, 7.2f })
        {
            foreach (var x in new[] { 3.5f, 4.5f, 5.5f, 6.5f, 9.5f, 10.5f, 11.5f, 12.5f })
            {
                items.Add(Ph("dining_chair_02", x, z, North));
            }
        }
        return items;
    }

    /// <summary>
    /// Clouds, 35th floor: modelled on the real "Clouds": cocktail bar with a
    /// back bar along the north facade, open kitchen with a bistro in front, restaurant tables, a sunset
    /// lounge facing the Uetliberg (west), a Prive with the quiz screen behind a glass partition, a DJ booth,
    /// panorama viewers at the glass and the lift core in the middle.
    /// Glass facade, luminous "sky" ceiling and LED lines come from the skylounge theme (client).
    /// Furniture: realistic Poly Haven models ("ph-*") plus custom pieces built by the client.
    /// </summary>
    private static List<RoomItem> Clouds()
    {
        var items = new List<RoomItem>
        {
            // Cocktail bar (north-east): back bar against the glass, backlit counter, pendants above.
            Item("custom-backbar", 16f, 15.5f),
            Item("custom-skybar", 16f, 13.6f),
            Item("custom-pendant", 14.5f, 13.5f), Item("custom-pendant", 17.5f, 13.5f),

            // Open kitchen along the east facade, bistro high tables in front of the pass.
            Item("custom-openkitchen", 20.9f, 9.2f, West),
            Ph("side_table_tall_01", 17.8f, 8.2f), Ph("side_table_tall_01", 17.8f, 10.4f),
            Ph("bar_chair_round_01", 16.9f, 8.2f, East), Ph("bar_chair_round_01", 18.7f, 8.2f, West),
            Ph("bar_chair_round_01", 16.9f, 10.4f, East), Ph("bar_chair_round_01", 18.7f, 10.4f, West),

            // Sunset lounge (north-west) looking towards the Uetliberg.
            Item("custom-rug", 3.6f, 13.2f),
            Ph("sofa_02", 3.6f, 15.1f, South),
            Ph("mid_century_lounge_chair", 1.4f, 12.9f, East), Ph("mid_century_lounge_chair", 5.8f, 12.9f, West),
            Ph("modern_coffee_table_01", 3.6f, 13.3f), Ph("tea_set_01", 3.6f, 13.3f),
            Ph("Ottoman_01", 3.6f, 11.5f),
            Item("custom-pendant", 3.6f, 13.3f),
            Ph("side_table_tall_01", 0.8f, 10.6f), Ph("marble_bust_01", 0.8f, 10.6f),

            // Second lounge at the west facade.
            Item("custom-rug", 2.4f, 7f, West),
            Ph("sofa_03", 0.9f, 7f, East),
            Ph("coffee_table_round_01", 2.4f, 7f),
            Ph("modern_arm_chair_01", 3.9f, 6.2f, West), Ph("modern_arm_chair_01", 3.9f, 7.8f, West),
            Ph("potted_plant_02", 0.8f, 9.4f),

            // DJ booth on the north side with an LED line in front.
            Item("custom-djbooth", 9.8f, 15f),
            Item("custom-ledstrip", 9.8f, 13.8f),

            // Lift core in the middle of the floor, doors facing the restaurant.
            Item(RoomHubElevatorItem, 11f, 9.4f),

            // Tic-tac-toe table.
            Item("game-tictactoe", 7.2f, 9f),
            Ph("dining_chair_02", 6.2f, 9f, East), Ph("dining_chair_02", 8.2f, 9f, West),

            // Prive (south-east) behind a frosted glass partition: quiz screen with armchairs.
            Item("custom-divider", 15.2f, 3f, West),
            Item("custom-rug", 18.4f, 3f, West),
            Item("game-quiz", 20.8f, 3f, West),
            Ph("modern_arm_chair_01", 17.9f, 2f, East), Ph("modern_arm_chair_01", 17.9f, 4f, East),
            Ph("side_table_01", 17.9f, 3f), Ph("ceramic_vase_01", 17.9f, 3f),

            // Panorama viewers at the glass.
            Item("custom-telescope", 7.2f, 15.3f),
            Item("custom-telescope", 0.9f, 4.4f, East),
            Item("custom-telescope", 21.2f, 5.6f, West),

            // Plants.
            Ph("pachira_aquatica_01", 0.9f, 15.2f), Ph("pachira_aquatica_01", 21.1f, 15.1f),
            Ph("potted_plant_04", 0.8f, 0.8f), Ph("potted_plant_02", 21.2f, 0.8f),
            Ph("potted_plant_04", 12.4f, 15.3f), Ph("potted_plant_02", 14.2f, 0.8f),
            Ph("potted_plant_04", 19.6f, 6.3f),
        };

        // Bar stools in front of the counter.
        items.AddRange(Enumerable.Range(0, 5).Select(i => Ph("bar_chair_round_01", 14f + i, 12.6f, North)));

        // Restaurant along the south glass: three tables for two, a pendant above each.
        foreach (var x in new[] { 2.4f, 5.4f, 8.4f })
        {
            items.Add(Ph("round_wooden_table_01", x, 2.4f));
            items.Add(Ph("dining_chair_02", x - 0.85f, 2.4f, East));
            items.Add(Ph("dining_chair_02", x + 0.85f, 2.4f, West));
            items.Add(Item("custom-pendant", x, 2.4f));
        }
        return items;
    }

}
