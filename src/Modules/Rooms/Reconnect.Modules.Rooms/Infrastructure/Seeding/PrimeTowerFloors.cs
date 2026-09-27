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
    internal sealed record Floor(int Storey, string Name, string Theme, int Width, int Depth, int Capacity, List<RoomItem> Layout,
        TowerFloorPlan Plan);

    /// <summary>Every public storey has the real outline of the tower (about 63 × 35 m) with the core in the middle.</summary>
    internal static IEnumerable<Floor> Floors()
    {
        var plan = TowerFurnishing.PrimeTower;
        Floor Storey(int storey, string name, string theme, int capacity, List<RoomItem> layout) =>
            new(storey, name, theme, plan.Width, plan.Depth, capacity, layout, plan);
        return
        [
            Storey(0, "Lobby", RoomThemes.Lobby, 150, TowerFloorDesigns.Lobby(plan)),
            Storey(12, "Coworking", RoomThemes.Coworking, 100, TowerFloorDesigns.Coworking(plan)),
            Storey(24, "Sky Office", RoomThemes.Office, 80, TowerFloorDesigns.SkyOffice(plan)),
            Storey(34, "Konferenzzentrum", RoomThemes.Conference, 140, TowerFloorDesigns.Conference(plan)),
            Storey(35, "Sky Lounge", RoomThemes.SkyLounge, 120, TowerFloorDesigns.SkyLounge(plan)),
        ];
    }

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
            room.ShapeAs(floor.Plan);
            room.PlaceOnFloor(floor.Storey, floor.Capacity);
            room.ReplaceLayout(floor.Layout);
        }

        // Offices bought before storeys had the real floor plan get it now (with the starter furniture).
        var oldOffices = await db.Rooms
            .Where(r => r.BuildingId == building && r.Floor != null && r.OwnerId != owner)
            .ToListAsync(ct);
        foreach (var office in oldOffices.Where(r => !r.HasOutline))
        {
            office.ShapeAs(TowerFurnishing.PrimeTower);
            office.PlaceOnFloor(office.Floor, Features.RoomProvisioning.OfficeCapacity);
            office.ReplaceLayout(StarterOffice.Layout(TowerFurnishing.PrimeTower));
        }
        await db.SaveChangesAsync(ct);
    }
}
