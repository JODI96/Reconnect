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

    /// <summary>Ground floor: entrance hall, reception, turnstiles, lift bank – and the waiting area of the lift queue.</summary>
    private static List<RoomItem> Lobby() =>
    [
        Item(RoomHubElevatorItem, 15f, 18.8f),
        Item("custom-hologram", 15f, 9f),
        Ph("sofa_02", 5f, 6f, East), Ph("sofa_02", 25f, 6f, West),
        Ph("pachira_aquatica_01", 1f, 19f), Ph("pachira_aquatica_01", 29f, 19f),
    ];

    private static List<RoomItem> Coworking() =>
    [
        Item(RoomHubElevatorItem, 11f, 9.4f),
        Ph("sofa_03", 3f, 13f, South),
    ];

    private static List<RoomItem> SkyOffice() =>
    [
        Item(RoomHubElevatorItem, 11f, 9.4f),
        Ph("sofa_02", 3f, 13f, South),
    ];

    private static List<RoomItem> Conference() =>
    [
        Item(RoomHubElevatorItem, 12f, 9.4f),
        Ph("round_wooden_table_01", 5f, 5f),
    ];

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
