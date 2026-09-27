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
        public const int MaxShift = 12;

        public static List<RoomItemDto> Legalize(string theme, int width, int depth, IReadOnlyList<RoomItemDto> items,
            ICollection<string> log = null, IReadOnlyList<RoomPointDto> outline = null)
        {
            var result = new RoomItemDto[items.Count];
            var placed = new List<(ItemDefinition Definition, CellRect Cells)>();
            var placedItems = new List<(ItemDefinition Definition, RoomItemDto Item)>();

            // Lifts first (the space in front of their doors stays free), then big things (in layout order), then
            // what stands on them.
            var order = Enumerable.Range(0, items.Count)
                .OrderBy(i => RoomZones.IsLift(items[i].ItemId) ? 0 : ItemDefinitions.Find(items[i].ItemId)?.Kind == ItemKind.Decor ? 2 : 1)
                .ToList();
            var room = RoomZones.ContextFor(theme, width, depth, new RoomItemDto[0], outline);
            var liftsPlaced = false;
            foreach (var index in order)
            {
                if (!liftsPlaced && !RoomZones.IsLift(items[index].ItemId))
                {
                    room = RoomZones.ContextFor(theme, width, depth, result.Where(r => r != null).ToList(), outline);
                    liftsPlaced = true;
                }
                var item = items[index];
                var definition = ItemDefinitions.Find(item.ItemId);
                if (definition == null)
                {
                    log?.Add($"#{index} {item.ItemId}: unknown item, dropped");
                    continue;
                }
                if (definition.Kind == ItemKind.Decor)
                {
                    var onTable = PlaceDecor(definition, item, placedItems);
                    if (onTable == null)
                    {
                        log?.Add($"#{index} {item.ItemId} ({item.Position.X}, {item.Position.Z}): no table nearby, dropped");
                        continue;
                    }
                    placedItems.Add((definition, onTable));
                    result[index] = onTable;
                    continue;
                }
                var quarter = RoomLayout.Quarter(item.Rotation);
                if (Place(room, definition, item, quarter, placed) is not CellRect found)
                {
                    log?.Add($"#{index} {item.ItemId} ({item.Position.X}, {item.Position.Z}): no free place nearby, dropped");
                    continue;
                }
                placed.Add((definition, found));
                var (x, z) = RoomLayout.Centre(found);
                result[index] = new RoomItemDto(item.ItemId, new Vector3Dto(x, 0f, z), quarter * 90f);
                placedItems.Add((definition, result[index]));
            }
            TuckSeats(room, result);
            return result.Where(i => i != null).ToList();
        }

        /// <summary>
        /// Chairs and stools slide forward (the way one sits) up to the table in front of them, so nobody sits half a
        /// metre away; at small tables (up to 1 m wide) they also line up with the middle of the table side. Only small
        /// seats (sofas keep their legroom) and only where the way is free.
        /// </summary>
        private static void TuckSeats(RoomLayoutContext room, RoomItemDto[] items)
        {
            const int reach = BuildGrid.CellsPerTile;          // a table up to 1 m ahead
            const int sideways = BuildGrid.CellsPerTile / 2;    // … and up to 50 cm to the side
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var definition = item == null ? null : ItemDefinitions.Find(item.ItemId);
                if (definition == null || definition.Seats == 0 || definition.Kind != ItemKind.Floor
                    || definition.Width > BuildGrid.CellsPerTile || definition.Depth > BuildGrid.CellsPerTile)
                {
                    continue;
                }
                var (fx, fz) = RoomLayout.Front(item.ItemId, item.Rotation);
                var chair = RoomLayout.Footprint(item, definition);
                var others = items
                    .Select((other, j) => (Index: j, Item: other, Definition: other == null ? null : ItemDefinitions.Find(other.ItemId)))
                    .Where(o => o.Index != i && o.Definition?.Kind == ItemKind.Floor)
                    .Select(o => (o.Definition, Cells: RoomLayout.Footprint(o.Item, o.Definition)))
                    .ToList();

                // Lateral axis: x when facing along z, z when facing along x.
                bool alongZ = fz != 0;
                int Lo(CellRect r) => alongZ ? r.X : r.Z;
                int Size(CellRect r) => alongZ ? r.Width : r.Depth;
                int Gap(CellRect t) => fx == 1 ? t.X - chair.XMax : fx == -1 ? chair.X - t.XMax : fz == 1 ? t.Z - chair.ZMax : chair.Z - t.ZMax;

                var table = others
                    .Where(o => o.Definition.HasSurface)
                    .Select(o => (o.Cells, Gap: Gap(o.Cells), Side: Math.Max(Lo(o.Cells) - (Lo(chair) + Size(chair)), Lo(chair) - (Lo(o.Cells) + Size(o.Cells)))))
                    .Where(t => t.Gap >= -BuildGrid.CellsPerTile / 2 && t.Gap <= reach && t.Side <= sideways)
                    .OrderBy(t => t.Gap)
                    .ThenBy(t => t.Side)
                    .Select(t => (CellRect?)t.Cells)
                    .FirstOrDefault();
                if (table is not CellRect t)
                {
                    continue;
                }

                var gap = Gap(t);
                var lateral = Lo(chair);
                if (Size(t) <= BuildGrid.CellsPerTile)
                {
                    lateral = Lo(t) + (Size(t) - Size(chair)) / 2;          // centred on the side of a small table
                }
                else
                {
                    lateral = Math.Max(Lo(t) - Size(chair) + 1, Math.Min(Lo(t) + Size(t) - 1, lateral));   // at least touching it
                }
                CellRect Moved(int side) => alongZ
                    ? new CellRect(side, chair.Z + fz * gap, chair.Width, chair.Depth)
                    : new CellRect(chair.X + fx * gap, side, chair.Width, chair.Depth);
                foreach (var candidate in new[] { Moved(lateral), Moved(Lo(chair)) })
                {
                    if (room.IsInside(candidate) && !room.Reserved.Any(r => r.Overlaps(candidate)) && !others.Any(o => o.Cells.Overlaps(candidate)))
                    {
                        var (x, z) = RoomLayout.Centre(candidate);
                        items[i] = new RoomItemDto(item.ItemId, new Vector3Dto(x, 0f, z), item.Rotation);
                        break;
                    }
                }
            }
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
                if (!room.IsInside(cells))
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

        /// <summary>Onto the table top under (or nearest to) the item, on the fine decor grid, turned if it only fits that way.</summary>
        private static RoomItemDto PlaceDecor(ItemDefinition definition, RoomItemDto item, List<(ItemDefinition Definition, RoomItemDto Item)> placed)
        {
            var surfaces = placed
                .Where(p => p.Definition.Kind == ItemKind.Floor && p.Definition.HasSurface)
                .Select(p => RoomLayout.SurfaceArea(p.Item, p.Definition))
                .Select(area => (Area: area, Distance: DistanceTo(area, item.Position.X, item.Position.Z)))
                .Where(s => s.Distance <= 1f)
                .OrderBy(s => s.Distance)
                .Select(s => s.Area)
                .ToList();
            var others = placed.Where(p => p.Definition.Kind == ItemKind.Decor).Select(p => RoomLayout.DecorArea(p.Item, p.Definition)).ToList();
            foreach (var rotation in new[] { item.Rotation, (item.Rotation + 90f) % 360f })
            {
                foreach (var surface in surfaces)
                {
                    foreach (var (x, z) in DecorSpots(surface, item.Position.X, item.Position.Z))
                    {
                        var candidate = new RoomItemDto(item.ItemId, new Vector3Dto(x, 0f, z), RoomLayout.Quarter(rotation) * 90f);
                        var area = RoomLayout.DecorArea(candidate, definition);
                        if (surface.Contains(area, 0.02f) && !others.Any(o => o.Overlaps(area, 0.01f)))
                        {
                            return candidate;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>Decor grid points on a table top, nearest to (x, z) first.</summary>
        private static IEnumerable<(float X, float Z)> DecorSpots(Area surface, float x, float z)
        {
            var step = BuildGrid.DecorStep;
            var spots = new List<(float X, float Z)>();
            for (var sx = RoomLayout.SnapDecor(surface.MinX); sx <= surface.MaxX + 0.001f; sx += step)
            {
                for (var sz = RoomLayout.SnapDecor(surface.MinZ); sz <= surface.MaxZ + 0.001f; sz += step)
                {
                    spots.Add((RoomLayout.SnapDecor(sx), RoomLayout.SnapDecor(sz)));
                }
            }
            return spots.OrderBy(p => (p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z));
        }

        private static float DistanceTo(Area area, float x, float z)
        {
            var dx = Math.Max(0f, Math.Max(area.MinX - x, x - area.MaxX));
            var dz = Math.Max(0f, Math.Max(area.MinZ - z, z - area.MaxZ));
            return (float)Math.Sqrt(dx * dx + dz * dz);
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

        /// <summary>The footprint moved against the nearest wall of the room (unchanged in rooms without walls).</summary>
        public static CellRect OntoNearestWall(RoomLayoutContext room, CellRect cells)
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

    }
}
