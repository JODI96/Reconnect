using Reconnect.Contracts.Rooms;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Modules.Rooms.Features;
using Reconnect.Modules.Rooms.Infrastructure.Seeding;

namespace Reconnect.UnitTests.Rooms;

/// <summary>Every room the game ships (showcase rooms, tower floors, starter office) follows the build rules.</summary>
public sealed class SeedRoomLayoutTests
{
    public static TheoryData<string> Rooms()
    {
        var data = new TheoryData<string>();
        foreach (var name in All().Keys)
        {
            data.Add(name);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Rooms))]
    public void Seed_room_follows_the_build_rules(string name)
    {
        var (theme, width, depth, layout) = All()[name];
        var items = layout.Select(i => i.ToDto()).ToList();

        var problems = RoomLayout.Validate(RoomZones.ContextFor(theme, width, depth, items), items);

        Assert.True(problems.Count == 0, string.Join("\n", problems.Select(p =>
            p.Index < 0 ? p.Message : $"#{p.Index} {items[p.Index].ItemId} ({items[p.Index].Position.X}, {items[p.Index].Position.Z}) r{items[p.Index].Rotation}: {p.Message}")));
    }

    private static Dictionary<string, (string Theme, int Width, int Depth, List<RoomItem> Layout)> All()
    {
        var rooms = new Dictionary<string, (string, int, int, List<RoomItem>)>();
        foreach (var room in ShowcaseRooms.Definitions())
        {
            rooms[room.Name] = (room.Theme, room.Width, room.Depth, room.Layout);
        }
        foreach (var floor in PrimeTowerFloors.Floors())
        {
            rooms[floor.Name] = (floor.Theme, floor.Width, floor.Depth, floor.Layout);
        }
        rooms["Büro (Start)"] = (RoomThemes.Coworking, RoomProvisioning.OfficeWidth, RoomProvisioning.OfficeDepth, StarterOffice.Layout());
        return rooms;
    }
}
