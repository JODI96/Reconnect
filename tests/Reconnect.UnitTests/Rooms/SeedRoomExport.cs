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
    /// <summary>With RECONNECT_DUMP_ROOMS=&lt;file&gt;: the shipped layouts as JSON (item centres in metres).</summary>
    [Fact]
    public void Dump_seed_rooms()
    {
        var file = Environment.GetEnvironmentVariable("RECONNECT_DUMP_ROOMS");
        if (string.IsNullOrEmpty(file))
        {
            return;
        }
        var dump = SeedRooms().ToDictionary(r => r.Name, r => r.Layout.Select(i => i.ToDto()).ToList());
        File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(dump));
    }

    /// <summary>With RECONNECT_DUMP_PLAN=&lt;file&gt;: the tower floor plan (outline, size, core footprint) for designing.</summary>
    [Fact]
    public void Dump_tower_plan()
    {
        var file = Environment.GetEnvironmentVariable("RECONNECT_DUMP_PLAN");
        if (string.IsNullOrEmpty(file))
        {
            return;
        }
        var plan = TowerFurnishing.PrimeTower;
        var core = TowerFurnishing.Core(plan).ToDto();
        var cells = RoomLayout.Footprint(core, ItemDefinitions.Find(core.ItemId)!);
        var lines = new List<string> { $"size {plan.Width} {plan.Depth}", $"core {cells.X} {cells.Z} {cells.Width} {cells.Depth}" };
        lines.AddRange(plan.Outline.Select(p => FormattableString.Invariant($"{p.X:0.00} {p.Z:0.00}")));
        File.WriteAllLines(file, lines);
    }

    /// <summary>
    /// With RECONNECT_TILEMAP=&lt;folder&gt;: per room a walking-tile map ('#' furniture, '.' reachable, 'o' free but cut off,
    /// ' ' outside the outline) – to see why a seat is "zugestellt".
    /// </summary>
    [Fact]
    public void Dump_tile_maps()
    {
        var folder = Environment.GetEnvironmentVariable("RECONNECT_TILEMAP");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }
        Directory.CreateDirectory(folder);
        foreach (var (name, theme, width, depth, layout, outline) in SeedRooms())
        {
            var items = layout.Select(i => i.ToDto()).ToList();
            var room = RoomZones.ContextFor(theme, width, depth, items, outline);
            var blocked = RoomLayout.BlockedTiles(items);
            var outside = RoomLayout.OutsideTiles(room);
            var all = new HashSet<(int X, int Z)>(blocked);
            all.UnionWith(outside);
            var reachable = RoomLayout.Reachable(room, all);
            var map = new StringBuilder();
            map.AppendLine("    " + string.Concat(Enumerable.Range(0, width).Select(x => x % 10 == 0 ? (char)('0' + x / 10) : ' ')));
            for (var z = depth - 1; z >= 0; z--)
            {
                map.Append($"{z,3} ");
                for (var x = 0; x < width; x++)
                {
                    map.Append(outside.Contains((x, z)) ? ' ' : blocked.Contains((x, z)) ? '#' : reachable.Contains((x, z)) ? '.' : 'o');
                }
                map.AppendLine();
            }
            map.AppendLine("    " + string.Concat(Enumerable.Range(0, width).Select(x => (char)('0' + x % 10))));
            File.WriteAllText(Path.Combine(folder, string.Concat(name.Where(char.IsLetterOrDigit)) + ".tiles.txt"), map.ToString());
            // Every item with index, position and rotation (to find what a build-rule message points at).
            var list = new StringBuilder();
            for (var i = 0; i < items.Count; i++)
            {
                list.AppendLine($"#{i} {items[i].ItemId} ({items[i].Position.X}, {items[i].Position.Z}) r{items[i].Rotation} {items[i].Colours}");
            }
            File.WriteAllText(Path.Combine(folder, string.Concat(name.Where(char.IsLetterOrDigit)) + ".items.txt"), list.ToString());
        }
    }

    internal static List<(string Name, string Theme, int Width, int Depth, List<RoomItem> Layout, IReadOnlyList<RoomPointDto>? Outline)> SeedRooms()
    {
        var rooms = new List<(string Name, string Theme, int Width, int Depth, List<RoomItem> Layout, IReadOnlyList<RoomPointDto>? Outline)>();
        rooms.AddRange(ShowcaseRooms.Definitions().Select(r => (r.Name, r.Theme, r.Width, r.Depth, r.Layout, (IReadOnlyList<RoomPointDto>?)null)));
        rooms.AddRange(PrimeTowerFloors.Floors().Select(f => (f.Name, f.Theme, f.Width, f.Depth, f.Layout, (IReadOnlyList<RoomPointDto>?)f.Plan.Outline)));
        var plan = TowerFurnishing.PrimeTower;
        rooms.Add(("Starter", RoomThemes.Office, plan.Width, plan.Depth, StarterOffice.Layout(plan), plan.Outline));
        return rooms;
    }

    [Fact]
    public void Export_seed_rooms_onto_the_grid()
    {
        var folder = Environment.GetEnvironmentVariable("RECONNECT_EXPORT_ROOMS");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }
        Directory.CreateDirectory(folder);
        // RECONNECT_EXPORT_FROM=<file from Dump_seed_rooms>: take the layouts from there (e.g. across a grid change).
        var from = Environment.GetEnvironmentVariable("RECONNECT_EXPORT_FROM");
        var dumped = string.IsNullOrEmpty(from)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<RoomItemDto>>>(File.ReadAllText(from));

        foreach (var (name, theme, width, depth, layout, outline) in SeedRooms())
        {
            var items = dumped?[name] ?? layout.Select(i => i.ToDto()).ToList();
            var log = new List<string>();
            var fixedItems = RoomLayoutFixer.Legalize(theme, width, depth, items, log, outline);
            var problems = RoomLayout.Validate(RoomZones.ContextFor(theme, width, depth, fixedItems, outline), fixedItems);

            var code = new StringBuilder();
            foreach (var item in fixedItems)
            {
                var definition = ItemDefinitions.Find(item.ItemId)!;
                if (definition.Kind == ItemKind.Decor)
                {
                    // Small things stand on the finer decor grid: their centre in metres.
                    code.AppendLine(CultureInfo.InvariantCulture,
                        $"        At(\"{item.ItemId}\", {item.Position.X:0.###}f, {item.Position.Z:0.###}f, {item.Rotation:0}),   // {definition.Name}");
                    continue;
                }
                var cells = RoomLayout.Footprint(item, definition);
                code.AppendLine(CultureInfo.InvariantCulture,
                    $"        Cell(\"{item.ItemId}\", {cells.X}, {cells.Z}, {item.Rotation:0}),   // {definition.Name}");
            }
            var file = string.Concat(name.Where(char.IsLetterOrDigit));
            File.WriteAllText(Path.Combine(folder, file + ".map.txt"), Map(RoomZones.ContextFor(theme, width, depth, fixedItems, outline), fixedItems));
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
            grid[z, x] = !room.CellInside(x, z) ? ' ' : room.Reserved.Any(r => r.Contains(x, z)) ? '#' : '.';
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
