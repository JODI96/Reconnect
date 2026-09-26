using System;
using System.Collections.Generic;
using System.Linq;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>
    /// Moves a free-form layout (made before the build grid existed) onto the grid with as little change as possible:
    /// every item goes to the nearest free place that follows the rules (<see cref="RoomLayout"/>) – small things onto
    /// the table under them, paintings onto the nearest wall. Items without any legal place nearby are dropped.
    /// Used for the shipped rooms and by the build editor for rooms saved before the grid.
    /// </summary>
    public static class RoomLayoutFixer
    {
        /// <summary>How far (cells) an item may move to find a free place.</summary>
        public const int MaxShift = 6;

        public static List<RoomItemDto> Legalize(string theme, int width, int depth, IReadOnlyList<RoomItemDto> items,
            ICollection<string> log = null)
        {
            var result = new RoomItemDto[items.Count];
            var placed = new List<(ItemDefinition Definition, CellRect Cells)>();

            // Lifts first (the space in front of their doors stays free), then big things (in layout order), then
            // what stands on them.
            var order = Enumerable.Range(0, items.Count)
                .OrderBy(i => items[i].ItemId == RoomZones.ElevatorItem ? 0 : ItemDefinitions.Find(items[i].ItemId)?.Kind == ItemKind.Decor ? 2 : 1)
                .ToList();
            var room = RoomZones.ContextFor(theme, width, depth, new RoomItemDto[0]);
            var liftsPlaced = false;
            foreach (var index in order)
            {
                if (!liftsPlaced && items[index].ItemId != RoomZones.ElevatorItem)
                {
                    room = RoomZones.ContextFor(theme, width, depth, result.Where(r => r != null).ToList());
                    liftsPlaced = true;
                }
                var item = items[index];
                var definition = ItemDefinitions.Find(item.ItemId);
                if (definition == null)
                {
                    log?.Add($"#{index} {item.ItemId}: unknown item, dropped");
                    continue;
                }
                var quarter = RoomLayout.Quarter(item.Rotation);
                var cells = definition.Kind == ItemKind.Decor
                    ? PlaceDecor(definition, item, ref quarter, placed)
                    : Place(room, definition, item, quarter, placed);
                if (cells is not CellRect found)
                {
                    log?.Add($"#{index} {item.ItemId} ({item.Position.X}, {item.Position.Z}): no free place nearby, dropped");
                    continue;
                }
                placed.Add((definition, found));
                var (x, z) = RoomLayout.Centre(found);
                result[index] = new RoomItemDto(item.ItemId, new Vector3Dto(x, 0f, z), quarter * 90f);
            }
            return result.Where(i => i != null).ToList();
        }

        private static CellRect? Place(RoomLayoutContext room, ItemDefinition definition, RoomItemDto item, int quarter,
            List<(ItemDefinition Definition, CellRect Cells)> placed)
        {
            var wanted = RoomLayout.Footprint(definition, item.Position.X, item.Position.Z, quarter);
            if (definition.Kind == ItemKind.Wall)
            {
                wanted = OntoNearestWall(room, wanted);
            }
            foreach (var cells in Around(wanted, MaxShift))
            {
                if (!room.Cells.Contains(cells))
                {
                    continue;
                }
                var free = definition.Kind switch
                {
                    ItemKind.Floor => !room.Reserved.Any(r => r.Overlaps(cells)) && !Collides(placed, cells, ItemKind.Floor),
                    ItemKind.Wall => RoomLayout.WallOf(room, cells) != WallSides.None && !Collides(placed, cells, ItemKind.Wall),
                    _ => !Collides(placed, cells, definition.Kind),
                };
                if (free)
                {
                    return cells;
                }
            }
            return null;
        }

        /// <summary>Onto the surface under (or nearest to) the item, turned if it only fits that way.</summary>
        private static CellRect? PlaceDecor(ItemDefinition definition, RoomItemDto item, ref int quarter,
            List<(ItemDefinition Definition, CellRect Cells)> placed)
        {
            var cellX = item.Position.X / BuildGrid.CellSize;
            var cellZ = item.Position.Z / BuildGrid.CellSize;
            var surfaces = placed
                .Where(p => p.Definition.Kind == ItemKind.Floor && p.Definition.HasSurface)
                .Select(p => (p.Cells, Distance: DistanceTo(p.Cells, cellX, cellZ)))
                .Where(s => s.Distance <= 2f)
                .OrderBy(s => s.Distance)
                .ToList();
            foreach (var turn in new[] { quarter, (quarter + 1) % 4 })
            {
                var wanted = RoomLayout.Footprint(definition, item.Position.X, item.Position.Z, turn);
                foreach (var (surface, _) in surfaces)
                {
                    foreach (var cells in Around(ClampInto(wanted, surface), MaxShift))
                    {
                        if (surface.Contains(cells) && !Collides(placed, cells, ItemKind.Decor))
                        {
                            quarter = turn;
                            return cells;
                        }
                    }
                }
            }
            return null;
        }

        private static bool Collides(List<(ItemDefinition Definition, CellRect Cells)> placed, CellRect cells, ItemKind layer) =>
            placed.Any(p => p.Definition.Kind == layer && p.Cells.Overlaps(cells));

        /// <summary>The rectangle and its shifted copies, nearest first.</summary>
        private static IEnumerable<CellRect> Around(CellRect start, int radius)
        {
            var shifts = new List<(int Dx, int Dz)>();
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    shifts.Add((dx, dz));
                }
            }
            return shifts
                .OrderBy(s => s.Dx * s.Dx + s.Dz * s.Dz)
                .ThenBy(s => Math.Abs(s.Dx) + Math.Abs(s.Dz))
                .Select(s => new CellRect(start.X + s.Dx, start.Z + s.Dz, start.Width, start.Depth));
        }

        private static CellRect OntoNearestWall(RoomLayoutContext room, CellRect cells)
        {
            var options = new List<(int Distance, CellRect Cells)>();
            var bounds = room.Cells;
            if (room.Walls.HasFlag(WallSides.North))
            {
                options.Add((bounds.ZMax - cells.ZMax, new CellRect(cells.X, bounds.ZMax - cells.Depth, cells.Width, cells.Depth)));
            }
            if (room.Walls.HasFlag(WallSides.East))
            {
                options.Add((bounds.XMax - cells.XMax, new CellRect(bounds.XMax - cells.Width, cells.Z, cells.Width, cells.Depth)));
            }
            if (room.Walls.HasFlag(WallSides.South))
            {
                options.Add((cells.Z, new CellRect(cells.X, 0, cells.Width, cells.Depth)));
            }
            if (room.Walls.HasFlag(WallSides.West))
            {
                options.Add((cells.X, new CellRect(0, cells.Z, cells.Width, cells.Depth)));
            }
            return options.Count == 0 ? cells : options.OrderBy(o => Math.Abs(o.Distance)).First().Cells;
        }

        private static CellRect ClampInto(CellRect cells, CellRect area)
        {
            var x = Math.Max(area.X, Math.Min(cells.X, area.XMax - cells.Width));
            var z = Math.Max(area.Z, Math.Min(cells.Z, area.ZMax - cells.Depth));
            return new CellRect(x, z, cells.Width, cells.Depth);
        }

        private static float DistanceTo(CellRect cells, float x, float z)
        {
            var dx = Math.Max(0f, Math.Max(cells.X - x, x - cells.XMax));
            var dz = Math.Max(0f, Math.Max(cells.Z - z, z - cells.ZMax));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
