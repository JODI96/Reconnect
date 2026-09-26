using System;
using System.Collections.Generic;
using System.Linq;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>A rectangle in metres (room coordinates).</summary>
    public readonly struct Area
    {
        public Area(float minX, float minZ, float maxX, float maxZ)
        {
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        public float MinX { get; }
        public float MinZ { get; }
        public float MaxX { get; }
        public float MaxZ { get; }
        public float CentreX => (MinX + MaxX) / 2f;
        public float CentreZ => (MinZ + MaxZ) / 2f;

        public bool Contains(Area other, float tolerance = 0f) =>
            other.MinX >= MinX - tolerance && other.MaxX <= MaxX + tolerance && other.MinZ >= MinZ - tolerance && other.MaxZ <= MaxZ + tolerance;

        public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        public bool Overlaps(Area other, float margin = 0f) =>
            MinX < other.MaxX - margin && other.MinX < MaxX - margin && MinZ < other.MaxZ - margin && other.MinZ < MaxZ - margin;
    }

    /// <summary>A room as the build rules see it: size in walking tiles, walls, cells kept free (entrance, lift landing).</summary>
    public sealed class RoomLayoutContext
    {
        public RoomLayoutContext(int width, int depth, WallSides walls, IReadOnlyList<CellRect> reserved = null)
        {
            Width = width;
            Depth = depth;
            Walls = walls;
            Reserved = reserved ?? Array.Empty<CellRect>();
        }

        /// <summary>Walking tiles (metres).</summary>
        public int Width { get; }
        public int Depth { get; }
        public WallSides Walls { get; }

        /// <summary>No furniture here (entrance, in front of the lift): the build zones are the room minus these.</summary>
        public IReadOnlyList<CellRect> Reserved { get; }

        public CellRect Cells => new CellRect(0, 0, Width * BuildGrid.CellsPerTile, Depth * BuildGrid.CellsPerTile);
    }

    /// <summary>One broken rule, for the item at <see cref="Index"/> in the layout (-1 = the layout as a whole).</summary>
    public sealed record LayoutProblem(int Index, string Message);

    /// <summary>
    /// The build rules, shared by server (validates every layout it stores) and client (editor preview, where furniture
    /// stands, which tiles one can walk on). Item positions are the centre of the footprint in metres, rotations 0/90/180/270.
    /// </summary>
    public static class RoomLayout
    {
        private const float Tolerance = 0.02f;

        /// <summary>0–3 quarter turns of a rotation in degrees.</summary>
        public static int Quarter(float rotation) => (((int)Math.Round(rotation / 90f)) % 4 + 4) % 4;

        public static bool IsQuarterTurn(float rotation) => Math.Abs(rotation / 90f - Math.Round(rotation / 90f)) < 0.01f;

        /// <summary>Footprint of a definition turned by <paramref name="quarter"/> quarter turns, centred at (x, z) metres.</summary>
        public static CellRect Footprint(ItemDefinition definition, float x, float z, int quarter)
        {
            var (width, depth) = Size(definition, quarter);
            var cellX = (int)Math.Round(x / BuildGrid.CellSize - width / 2.0);
            var cellZ = (int)Math.Round(z / BuildGrid.CellSize - depth / 2.0);
            return new CellRect(cellX, cellZ, width, depth);
        }

        public static CellRect Footprint(RoomItemDto item, ItemDefinition definition) =>
            Footprint(definition, item.Position.X, item.Position.Z, Quarter(item.Rotation));

        public static (int Width, int Depth) Size(ItemDefinition definition, int quarter) =>
            quarter % 2 == 0 ? (definition.Width, definition.Depth) : (definition.Depth, definition.Width);

        /// <summary>Centre of a footprint in metres (where the item's pivot goes).</summary>
        public static (float X, float Z) Centre(CellRect cells) =>
            ((cells.X + cells.Width / 2f) * BuildGrid.CellSize, (cells.Z + cells.Depth / 2f) * BuildGrid.CellSize);

        /// <summary>The item moved onto the grid: centre on the nearest cell rectangle, rotation to the nearest quarter turn.</summary>
        public static RoomItemDto Snap(RoomItemDto item)
        {
            var definition = ItemDefinitions.Find(item.ItemId);
            if (definition == null)
            {
                return item;
            }
            var quarter = Quarter(item.Rotation);
            if (definition.Kind == ItemKind.Decor)
            {
                return new RoomItemDto(item.ItemId, new Vector3Dto(SnapDecor(item.Position.X), 0f, SnapDecor(item.Position.Z)), quarter * 90f);
            }
            var (x, z) = Centre(Footprint(definition, item.Position.X, item.Position.Z, quarter));
            return new RoomItemDto(item.ItemId, new Vector3Dto(x, 0f, z), quarter * 90f);
        }

        /// <summary>Small things stand on a finer grid (<see cref="BuildGrid.DecorStep"/>).</summary>
        public static float SnapDecor(float metres) => (float)Math.Round(metres / BuildGrid.DecorStep) * BuildGrid.DecorStep;

        /// <summary>Where a small thing stands (metres, its real size turned with it).</summary>
        public static Area DecorArea(RoomItemDto item, ItemDefinition definition)
        {
            var turned = Quarter(item.Rotation) % 2 == 1;
            var halfX = (turned ? definition.SizeZ : definition.SizeX) / 2f;
            var halfZ = (turned ? definition.SizeX : definition.SizeZ) / 2f;
            return new Area(item.Position.X - halfX, item.Position.Z - halfZ, item.Position.X + halfX, item.Position.Z + halfZ);
        }

        /// <summary>The table top of a surface item (metres), turned with it, centred on its footprint.</summary>
        public static Area SurfaceArea(RoomItemDto item, ItemDefinition definition)
        {
            var (x, z) = Centre(Footprint(item, definition));
            var turned = Quarter(item.Rotation) % 2 == 1;
            var halfX = (turned ? definition.SurfaceDepth : definition.SurfaceWidth) / 2f;
            var halfZ = (turned ? definition.SurfaceWidth : definition.SurfaceDepth) / 2f;
            return new Area(x - halfX, z - halfZ, x + halfX, z + halfZ);
        }

        /// <summary>Checks every rule; empty = the layout can be stored.</summary>
        public static IReadOnlyList<LayoutProblem> Validate(RoomLayoutContext room, IReadOnlyList<RoomItemDto> items)
        {
            var problems = new List<LayoutProblem>();
            if (items.Count > BuildGrid.MaxItems)
            {
                problems.Add(new LayoutProblem(-1, $"Höchstens {BuildGrid.MaxItems} Gegenstände pro Raum."));
                return problems;
            }

            var placed = new List<(int Index, ItemDefinition Definition, CellRect Cells)>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var definition = ItemDefinitions.Find(item.ItemId);
                if (definition == null)
                {
                    problems.Add(new LayoutProblem(i, $"Unbekannter Gegenstand „{item.ItemId}“."));
                    continue;
                }
                if (!IsQuarterTurn(item.Rotation))
                {
                    problems.Add(new LayoutProblem(i, $"„{definition.Name}“ lässt sich nur in 90°-Schritten drehen."));
                    continue;
                }
                var cells = Footprint(item, definition);
                var (centreX, centreZ) = definition.Kind == ItemKind.Decor
                    ? (SnapDecor(item.Position.X), SnapDecor(item.Position.Z))
                    : Centre(cells);
                if (Math.Abs(centreX - item.Position.X) > Tolerance || Math.Abs(centreZ - item.Position.Z) > Tolerance)
                {
                    problems.Add(new LayoutProblem(i, $"„{definition.Name}“ steht nicht auf dem Raster."));
                    continue;
                }
                if (!room.Cells.Contains(cells))
                {
                    problems.Add(new LayoutProblem(i, $"„{definition.Name}“ ragt aus dem Raum."));
                    continue;
                }
                placed.Add((i, definition, cells));
            }

            foreach (var (index, definition, cells) in placed)
            {
                switch (definition.Kind)
                {
                    case ItemKind.Floor:
                        if (room.Reserved.Any(r => r.Overlaps(cells)))
                        {
                            problems.Add(new LayoutProblem(index, $"„{definition.Name}“ steht im Eingang oder vor dem Lift – dort muss frei bleiben."));
                        }
                        AddOverlaps(problems, placed, index, definition, cells, ItemKind.Floor);
                        break;
                    case ItemKind.Rug:
                    case ItemKind.Ceiling:
                        AddOverlaps(problems, placed, index, definition, cells, definition.Kind);
                        break;
                    case ItemKind.Decor:
                        var area = DecorArea(items[index], definition);
                        if (!placed.Any(p => p.Definition.Kind == ItemKind.Floor && p.Definition.HasSurface
                                             && SurfaceArea(items[p.Index], p.Definition).Contains(area, Tolerance)))
                        {
                            problems.Add(new LayoutProblem(index, $"„{definition.Name}“ muss ganz auf einem Tisch, Regal oder einer Theke stehen."));
                        }
                        var other = placed.FirstOrDefault(p => p.Index < index && p.Definition.Kind == ItemKind.Decor
                                                               && DecorArea(items[p.Index], p.Definition).Overlaps(area, 0.01f));
                        if (other.Definition != null)
                        {
                            problems.Add(new LayoutProblem(index, $"„{definition.Name}“ überlappt mit „{other.Definition.Name}“."));
                        }
                        break;
                    case ItemKind.Wall:
                        if (WallOf(room, cells) == WallSides.None)
                        {
                            problems.Add(new LayoutProblem(index, $"„{definition.Name}“ gehört an eine Wand."));
                        }
                        AddOverlaps(problems, placed, index, definition, cells, ItemKind.Wall);
                        break;
                }
            }

            // Everyone must be able to reach every seat.
            if (problems.Count == 0)
            {
                var blocked = BlockedTiles(items);
                var reachable = Reachable(room, blocked);
                foreach (var (index, definition, cells) in placed.Where(p => p.Definition.Seats > 0))
                {
                    if (!Neighbours(cells).Any(reachable.Contains))
                    {
                        problems.Add(new LayoutProblem(index, $"„{definition.Name}“ ist zugestellt – niemand kommt hin."));
                    }
                }
            }
            return problems;
        }

        /// <summary>The wall an item touches (none if it stands free in the room).</summary>
        public static WallSides WallOf(RoomLayoutContext room, CellRect cells)
        {
            if (room.Walls.HasFlag(WallSides.North) && cells.ZMax == room.Cells.ZMax)
            {
                return WallSides.North;
            }
            if (room.Walls.HasFlag(WallSides.East) && cells.XMax == room.Cells.XMax)
            {
                return WallSides.East;
            }
            if (room.Walls.HasFlag(WallSides.South) && cells.Z == 0)
            {
                return WallSides.South;
            }
            if (room.Walls.HasFlag(WallSides.West) && cells.X == 0)
            {
                return WallSides.West;
            }
            return WallSides.None;
        }

        /// <summary>The way an item faces (the side one sits looking at) in whole cells: Kenney -Z, Poly Haven +Z at 0°.</summary>
        public static (int X, int Z) Front(string itemId, float rotation)
        {
            var quarter = Quarter(rotation);
            if (itemId != null && itemId.StartsWith("ph-", StringComparison.Ordinal))
            {
                quarter = (quarter + 2) % 4;
            }
            // -Z turned by +90° around Y points to -X.
            return quarter switch
            {
                1 => (-1, 0),
                2 => (0, 1),
                3 => (1, 0),
                _ => (0, -1),
            };
        }

        /// <summary>
        /// Rotation that turns an item's front into the room when it hangs on <paramref name="wall"/>. Kenney models face
        /// -Z at rotation 0, Poly Haven models ("ph-") +Z.
        /// </summary>
        public static float FacingIntoRoom(string itemId, WallSides wall)
        {
            var rotation = wall switch
            {
                WallSides.East => 90f,
                WallSides.South => 180f,
                WallSides.West => 270f,
                _ => 0f,
            };
            return itemId != null && itemId.StartsWith("ph-", StringComparison.Ordinal) ? (rotation + 180f) % 360f : rotation;
        }

        /// <summary>Walking tiles covered by floor furniture (pools are water: one swims there).</summary>
        public static HashSet<(int X, int Z)> BlockedTiles(IReadOnlyList<RoomItemDto> items)
        {
            var blocked = new HashSet<(int, int)>();
            foreach (var item in items)
            {
                var definition = ItemDefinitions.Find(item.ItemId);
                if (definition == null || definition.Kind != ItemKind.Floor || ItemDefinitions.IsPool(item.ItemId))
                {
                    continue;
                }
                foreach (var tile in Tiles(Footprint(item, definition)))
                {
                    blocked.Add(tile);
                }
            }
            return blocked;
        }

        /// <summary>Walking tiles in the water of pools.</summary>
        public static HashSet<(int X, int Z)> WaterTiles(IReadOnlyList<RoomItemDto> items)
        {
            var water = new HashSet<(int, int)>();
            foreach (var item in items.Where(i => ItemDefinitions.IsPool(i.ItemId)))
            {
                foreach (var tile in Tiles(Footprint(item, ItemDefinitions.Find(item.ItemId))))
                {
                    water.Add(tile);
                }
            }
            return water;
        }

        /// <summary>Walking tiles (1 m) that a cell rectangle touches.</summary>
        public static IEnumerable<(int X, int Z)> Tiles(CellRect cells)
        {
            var per = BuildGrid.CellsPerTile;
            for (var x = cells.X / per; x <= (cells.XMax - 1) / per; x++)
            {
                for (var z = cells.Z / per; z <= (cells.ZMax - 1) / per; z++)
                {
                    yield return (x, z);
                }
            }
        }

        /// <summary>Free tiles connected to the biggest free area of the room (where people can walk around).</summary>
        public static HashSet<(int X, int Z)> Reachable(RoomLayoutContext room, HashSet<(int X, int Z)> blocked)
        {
            var best = new HashSet<(int, int)>();
            var seen = new HashSet<(int, int)>();
            for (var x = 0; x < room.Width; x++)
            {
                for (var z = 0; z < room.Depth; z++)
                {
                    if (blocked.Contains((x, z)) || seen.Contains((x, z)))
                    {
                        continue;
                    }
                    var region = new HashSet<(int, int)>();
                    var queue = new Queue<(int X, int Z)>();
                    queue.Enqueue((x, z));
                    seen.Add((x, z));
                    while (queue.Count > 0)
                    {
                        var tile = queue.Dequeue();
                        region.Add(tile);
                        foreach (var next in new[] { (tile.X + 1, tile.Z), (tile.X - 1, tile.Z), (tile.X, tile.Z + 1), (tile.X, tile.Z - 1) })
                        {
                            if (next.Item1 >= 0 && next.Item1 < room.Width && next.Item2 >= 0 && next.Item2 < room.Depth
                                && !blocked.Contains(next) && seen.Add(next))
                            {
                                queue.Enqueue(next);
                            }
                        }
                    }
                    if (region.Count > best.Count)
                    {
                        best = region;
                    }
                }
            }
            return best;
        }

        private static IEnumerable<(int X, int Z)> Neighbours(CellRect cells)
        {
            var tiles = new HashSet<(int X, int Z)>(Tiles(cells));
            foreach (var (x, z) in tiles)
            {
                foreach (var next in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
                {
                    if (!tiles.Contains(next))
                    {
                        yield return next;
                    }
                }
            }
        }

        private static void AddOverlaps(List<LayoutProblem> problems, List<(int Index, ItemDefinition Definition, CellRect Cells)> placed,
            int index, ItemDefinition definition, CellRect cells, ItemKind layer)
        {
            foreach (var other in placed)
            {
                // Report each pair once (at the later item).
                if (other.Index < index && other.Definition.Kind == layer && other.Cells.Overlaps(cells))
                {
                    problems.Add(new LayoutProblem(index, $"„{definition.Name}“ überlappt mit „{other.Definition.Name}“."));
                    return;
                }
            }
        }
    }
}
