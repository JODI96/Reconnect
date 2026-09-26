using System.Globalization;
using System.Text;
using Reconnect.Contracts.Rooms;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Modules.Rooms.Features;
using Reconnect.Modules.Rooms.Infrastructure.Seeding;

namespace Reconnect.UnitTests.Rooms;

/// <summary>
/// Tool, not a test: with RECONNECT_EXPORT_ROOMS=&lt;folder&gt; it moves every shipped room onto the build grid
/// (RoomLayoutFixer) and writes the layouts as C# (Cell(...) calls) plus a log of what moved.
/// </summary>
public sealed class SeedRoomExport
{
    [Fact]
    public void Export_seed_rooms_onto_the_grid()
    {
        var folder = Environment.GetEnvironmentVariable("RECONNECT_EXPORT_ROOMS");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }
        Directory.CreateDirectory(folder);
        var rooms = new List<(string Name, string Theme, int Width, int Depth, List<RoomItem> Layout)>();
        rooms.AddRange(ShowcaseRooms.Definitions().Select(r => (r.Name, r.Theme, r.Width, r.Depth, r.Layout)));
        rooms.AddRange(PrimeTowerFloors.Floors().Select(f => (f.Name, f.Theme, f.Width, f.Depth, f.Layout)));
        rooms.Add(("Starter", RoomThemes.Coworking, RoomProvisioning.OfficeWidth, RoomProvisioning.OfficeDepth, StarterOffice.Layout()));

        foreach (var (name, theme, width, depth, layout) in rooms)
        {
            var items = layout.Select(i => i.ToDto()).ToList();
            var log = new List<string>();
            var fixedItems = RoomLayoutFixer.Legalize(theme, width, depth, items, log);
            var problems = RoomLayout.Validate(RoomZones.ContextFor(theme, width, depth, fixedItems), fixedItems);

            var code = new StringBuilder();
            foreach (var item in fixedItems)
            {
                var definition = ItemDefinitions.Find(item.ItemId)!;
                var cells = RoomLayout.Footprint(item, definition);
                code.AppendLine(CultureInfo.InvariantCulture,
                    $"        Cell(\"{item.ItemId}\", {cells.X}, {cells.Z}, {item.Rotation:0}),   // {definition.Name}");
            }
            var file = string.Concat(name.Where(char.IsLetterOrDigit));
            File.WriteAllText(Path.Combine(folder, file + ".map.txt"), Map(RoomZones.ContextFor(theme, width, depth, fixedItems), fixedItems));
            File.WriteAllText(Path.Combine(folder, file + ".cs.txt"), code.ToString());
            File.WriteAllLines(Path.Combine(folder, file + ".log"),
                log.Concat(problems.Select(p => $"PROBLEM #{p.Index}: {p.Message}"))
                   .Append($"{items.Count} → {fixedItems.Count} items"));
        }
    }

    /// <summary>Top view, one character per build cell (north at the top): furniture letters, ~ rug, # kept free.</summary>
    private static string Map(RoomLayoutContext room, List<RoomItemDto> items)
    {
        const string symbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var grid = new char[room.Cells.Depth, room.Cells.Width];
        for (var z = 0; z < room.Cells.Depth; z++)
        for (var x = 0; x < room.Cells.Width; x++)
        {
            grid[z, x] = room.Reserved.Any(r => r.Contains(x, z)) ? '#' : '.';
        }
        var legend = new StringBuilder();
        for (var i = 0; i < items.Count; i++)
        {
            var definition = ItemDefinitions.Find(items[i].ItemId)!;
            if (definition.Kind is not (ItemKind.Floor or ItemKind.Rug))
            {
                continue;
            }
            var symbol = definition.Kind == ItemKind.Rug ? '~' : symbols[i % symbols.Length];
            var cells = RoomLayout.Footprint(items[i], definition);
            for (var z = cells.Z; z < cells.ZMax; z++)
            for (var x = cells.X; x < cells.XMax; x++)
            {
                if (definition.Kind == ItemKind.Floor || grid[z, x] is '.' or '#')
                {
                    grid[z, x] = symbol;
                }
            }
            if (definition.Kind == ItemKind.Floor)
            {
                legend.AppendLine(CultureInfo.InvariantCulture, $"{symbol} = #{i} {definition.Name}");
            }
        }
        var map = new StringBuilder();
        for (var z = room.Cells.Depth - 1; z >= 0; z--)
        {
            for (var x = 0; x < room.Cells.Width; x++)
            {
                map.Append(grid[z, x]);
            }
            map.AppendLine();
        }
        return map + Environment.NewLine + legend;
    }
}
