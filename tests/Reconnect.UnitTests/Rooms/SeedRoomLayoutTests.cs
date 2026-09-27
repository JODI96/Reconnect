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
        var (theme, width, depth, layout, outline) = All()[name];
        var items = layout.Select(i => i.ToDto()).ToList();

        var problems = RoomLayout.Validate(RoomZones.ContextFor(theme, width, depth, items, outline), items);

        Assert.True(problems.Count == 0, string.Join("\n", problems.Select(p =>
            p.Index < 0 ? p.Message : $"#{p.Index} {items[p.Index].ItemId} ({items[p.Index].Position.X}, {items[p.Index].Position.Z}) r{items[p.Index].Rotation}: {p.Message}")));
    }

    [Fact]
    public void Tower_storeys_have_the_real_floor_plan_with_the_core_in_the_middle()
    {
        var plan = TowerFurnishing.PrimeTower;

        Assert.InRange(plan.Width, 60, 66);
        Assert.InRange(plan.Depth, 33, 38);
        Assert.InRange(RoomOutline.Area(plan.Outline), 1450f, 1700f);
        Assert.All(plan.Outline, p => Assert.True(p.X >= -0.01f && p.X <= plan.Width && p.Z >= -0.01f && p.Z <= plan.Depth));
        foreach (var floor in PrimeTowerFloors.Floors())
        {
            var core = Assert.Single(floor.Layout, i => i.ItemId == RoomZones.CoreItem);
            Assert.InRange(core.Position.X, plan.Width / 2f - 3, plan.Width / 2f + 3);
            Assert.Contains(floor.Layout, i => i.ItemId != RoomZones.CoreItem);
        }
    }

    private static Dictionary<string, (string Theme, int Width, int Depth, List<RoomItem> Layout, IReadOnlyList<RoomPointDto>? Outline)> All()
    {
        var rooms = new Dictionary<string, (string, int, int, List<RoomItem>, IReadOnlyList<RoomPointDto>?)>();
        foreach (var room in ShowcaseRooms.Definitions())
        {
            rooms[room.Name] = (room.Theme, room.Width, room.Depth, room.Layout, null);
        }
        foreach (var floor in PrimeTowerFloors.Floors())
        {
            rooms[floor.Name] = (floor.Theme, floor.Width, floor.Depth, floor.Layout, floor.Plan.Outline);
        }
        var plan = TowerFurnishing.PrimeTower;
        rooms["Büro (Start)"] = (RoomThemes.Office, plan.Width, plan.Depth, StarterOffice.Layout(plan), plan.Outline);
        return rooms;
    }
}
