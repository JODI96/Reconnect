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

    /// <summary>
    /// A room as the build rules see it: size in walking tiles, walls, cells kept free (entrance, lift landing) and – for
    /// rooms that are not rectangles (tower floors) – the outline of the floor.
    /// </summary>
    public sealed class RoomLayoutContext
    {
        /// <summary>People keep this distance from the facade (they would stand in the glass otherwise).</summary>
        public const float FacadeClearance = 0.3f;

        private bool[,] _cellMask;

        /// <summary>
        /// Outline geometry (which cells and tiles are on the floor) only depends on the outline, so it is worked out once
        /// per outline instance, not for every layout checked against it.
        /// </summary>
        private sealed class Shape
        {
            public int Width;
            public int Depth;
            public bool[,] Cells;
            public bool[,] Tiles;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IReadOnlyList<RoomPointDto>, Shape> Shapes =
            new System.Runtime.CompilerServices.ConditionalWeakTable<IReadOnlyList<RoomPointDto>, Shape>();

        private Shape OutlineShape
        {
            get
            {
                var shape = Shapes.GetValue(Outline, _ => new Shape { Width = Width, Depth = Depth });
                return shape.Width == Width && shape.Depth == Depth ? shape : new Shape { Width = Width, Depth = Depth };
            }
        }

        public RoomLayoutContext(int width, int depth, WallSides walls, IReadOnlyList<CellRect> reserved = null,
            IReadOnlyList<RoomPointDto> outline = null)
        {
            Width = width;
            Depth = depth;
            Walls = walls;
            Reserved = reserved ?? Array.Empty<CellRect>();
            Outline = outline != null && outline.Count >= 3 ? outline : null;
        }

        /// <summary>Floor outline in metres; null = the whole Width × Depth rectangle.</summary>
        public IReadOnlyList<RoomPointDto> Outline { get; }

        /// <summary>Whether a rectangle of build cells lies fully on the floor (inside the room and its outline).</summary>
        public bool IsInside(CellRect cells)
        {
            if (!Cells.Contains(cells))
            {
                return false;
            }
            if (Outline == null)
            {
                return true;
            }
            var mask = CellMask;
            for (var x = cells.X; x < cells.XMax; x++)
            {
                for (var z = cells.Z; z < cells.ZMax; z++)
                {
                    if (!mask[x, z])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// A turned item's real shape lies on the floor (inside the room, or the outline): measured on the shape itself,
        /// not its cells, so furniture can stand right up to a slanted facade.
        /// </summary>
        public bool IsInside(OrientedArea shape)
        {
            if (Outline == null)
            {
                var bounds = shape.Bounds;
                return bounds.MinX >= -0.001f && bounds.MinZ >= -0.001f && bounds.MaxX <= Width + 0.001f && bounds.MaxZ <= Depth + 0.001f;
            }
            foreach (var (x, z) in shape.Corners())
            {
                if (!RoomOutline.Contains(Outline, x, z))
                {
                    return false;
                }
            }
            // A concave corner of the outline must not reach into the shape.
            var inner = shape.Grown(-0.001f);
            foreach (var point in Outline)
            {
                if (inner.Contains(point.X, point.Z))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A build cell lies fully inside the outline.</summary>
        public bool CellInside(int x, int z) => Cells.Contains(x, z) && (Outline == null || CellMask[x, z]);

        /// <summary>People can stand on this walking tile (inside the outline, not in the facade).</summary>
        public bool TileWalkable(int x, int z)
        {
            if (x < 0 || z < 0 || x >= Width || z >= Depth)
            {
                return false;
            }
            if (Outline == null)
            {
                return true;
            }
            var shape = OutlineShape;
            var tiles = shape.Tiles;
            if (tiles == null)
            {
                tiles = new bool[Width, Depth];
                for (var tx = 0; tx < Width; tx++)
                {
                    for (var tz = 0; tz < Depth; tz++)
                    {
                        var cx = tx + 0.5f;
                        var cz = tz + 0.5f;
                        tiles[tx, tz] = RoomOutline.Contains(Outline, cx, cz) && RoomOutline.DistanceToEdge(Outline, cx, cz) >= FacadeClearance;
                    }
                }
                shape.Tiles = tiles;
            }
            return tiles[x, z];
        }

        private bool[,] CellMask
        {
            get
            {
                if (_cellMask != null)
                {
                    return _cellMask;
                }
                var shape = OutlineShape;
                if (shape.Cells != null)
                {
                    return _cellMask = shape.Cells;
                }
                var cells = Cells;
                var mask = new bool[cells.Width, cells.Depth];
                const float inset = 0.01f;
                var size = BuildGrid.CellSize;
                for (var x = 0; x < cells.Width; x++)
                {
                    for (var z = 0; z < cells.Depth; z++)
                    {
                        float x0 = x * size + inset, x1 = (x + 1) * size - inset, z0 = z * size + inset, z1 = (z + 1) * size - inset;
                        mask[x, z] = RoomOutline.Contains(Outline, x0, z0) && RoomOutline.Contains(Outline, x1, z0)
                                     && RoomOutline.Contains(Outline, x0, z1) && RoomOutline.Contains(Outline, x1, z1);
                    }
                }
                return _cellMask = shape.Cells = mask;
            }
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
    /// stands, which tiles one can walk on). Item positions are the centre of the footprint in metres. Furniture turns in
    /// whole degrees (so it can stand parallel to slanted walls); paintings, pools and lifts in quarter turns. Items at a
    /// quarter turn stand on the 25 cm grid by their footprint, turned ones by their centre on the 12.5 cm grid and cover
    /// the cells under their turned footprint.
    /// </summary>
    public static class RoomLayout
    {
        private const float Tolerance = 0.02f;

        /// <summary>A turned footprint covers a cell only if it reaches more than this far into it (metres).</summary>
        private const float CellGrace = 0.03f;

        /// <summary>0–3 quarter turns of a rotation in degrees.</summary>
        public static int Quarter(float rotation) => (((int)Math.Round(rotation / 90f)) % 4 + 4) % 4;

        public static bool IsQuarterTurn(float rotation) => Math.Abs(rotation / 90f - Math.Round(rotation / 90f)) < 0.01f;

        public static bool IsWholeDegree(float rotation) => Math.Abs(rotation - Math.Round(rotation)) < 0.01f;

        /// <summary>Degrees in [0, 360).</summary>
        public static float Normalize(float rotation)
        {
            var value = rotation % 360f;
            return value < 0f ? value + 360f : value;
        }

        /// <summary>
        /// Whether an item turns in whole degrees: everything standing or lying in the room. Paintings hang flat on a wall,
        /// pools are cut into the floor and lifts belong to the building – they turn in quarter turns.
        /// </summary>
        public static bool TurnsFreely(ItemDefinition definition) =>
            definition.Kind != ItemKind.Wall && !ItemDefinitions.IsPool(definition.Id) && !RoomZones.IsLift(definition.Id);

        /// <summary>The nearest allowed rotation: whole degrees for free-turning items, else the nearest quarter turn.</summary>
        public static float SnapRotation(ItemDefinition definition, float rotation) =>
            TurnsFreely(definition) ? Normalize((float)Math.Round(Normalize(rotation))) : Quarter(rotation) * 90f;

        /// <summary>Footprint of a definition turned by <paramref name="quarter"/> quarter turns, centred at (x, z) metres.</summary>
        public static CellRect Footprint(ItemDefinition definition, float x, float z, int quarter)
        {
            var (width, depth) = Size(definition, quarter);
            var cellX = (int)Math.Round(x / BuildGrid.CellSize - width / 2.0);
            var cellZ = (int)Math.Round(z / BuildGrid.CellSize - depth / 2.0);
            return new CellRect(cellX, cellZ, width, depth);
        }

        /// <summary>
        /// The cells an item stands on as a rectangle: exact at quarter turns; for turned items the rectangle around the
        /// cells they cover (<see cref="Cells"/> has the exact set).
        /// </summary>
        public static CellRect Footprint(RoomItemDto item, ItemDefinition definition)
        {
            if (IsQuarterTurn(item.Rotation))
            {
                return Footprint(definition, item.Position.X, item.Position.Z, Quarter(item.Rotation));
            }
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var (x, z) in Cells(item, definition))
            {
                minX = Math.Min(minX, x);
                minZ = Math.Min(minZ, z);
                maxX = Math.Max(maxX, x);
                maxZ = Math.Max(maxZ, z);
            }
            return minX > maxX
                ? new CellRect((int)Math.Floor(item.Position.X / BuildGrid.CellSize), (int)Math.Floor(item.Position.Z / BuildGrid.CellSize), 1, 1)
                : new CellRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
        }

        /// <summary>Where an item stands: its footprint (cells × 25 cm) turned with it, centred on the item.</summary>
        /// <summary>Partition walls that may share the corner cells where they meet at a right angle (glass rooms).</summary>
        public static readonly IReadOnlyCollection<string> Partitions = new HashSet<string> { "custom-glasswall" };

        /// <summary>A rug lying on a floor zone (a zone is a floor finish, rugs go on top of it; two zones don't stack).</summary>
        public static bool IsRugOnZone(RoomItemDto a, RoomItemDto b) =>
            FurnitureFamilies.TryZone(a.ItemId, out _, out _, out _) != FurnitureFamilies.TryZone(b.ItemId, out _, out _, out _);

        private static bool IsPartition(string itemId) => Partitions.Contains(itemId) || FurnitureFamilies.IsWall(itemId);

        /// <summary>Two partition walls meeting at a right angle: they share their corner cells instead of overlapping.</summary>
        public static bool IsCornerJoint(RoomItemDto a, RoomItemDto b) =>
            IsPartition(a.ItemId) && IsPartition(b.ItemId)
            && IsQuarterTurn(a.Rotation) && IsQuarterTurn(b.Rotation) && (Quarter(a.Rotation) + Quarter(b.Rotation)) % 2 == 1;

        public static OrientedArea Shape(RoomItemDto item, ItemDefinition definition)
        {
            if (IsQuarterTurn(item.Rotation))
            {
                var cells = Footprint(definition, item.Position.X, item.Position.Z, Quarter(item.Rotation));
                var (x, z) = Centre(cells);
                return new OrientedArea(x, z, cells.Width * BuildGrid.CellSize / 2f, cells.Depth * BuildGrid.CellSize / 2f, 0f);
            }
            return new OrientedArea(item.Position.X, item.Position.Z, definition.Width * BuildGrid.CellSize / 2f,
                definition.Depth * BuildGrid.CellSize / 2f, item.Rotation);
        }

        /// <summary>The build cells an item covers (turned items: every cell its turned footprint reaches into).</summary>
        public static IEnumerable<(int X, int Z)> Cells(RoomItemDto item, ItemDefinition definition)
        {
            if (IsQuarterTurn(item.Rotation))
            {
                var rect = Footprint(definition, item.Position.X, item.Position.Z, Quarter(item.Rotation));
                for (var x = rect.X; x < rect.XMax; x++)
                {
                    for (var z = rect.Z; z < rect.ZMax; z++)
                    {
                        yield return (x, z);
                    }
                }
                yield break;
            }
            var shape = Shape(item, definition).Grown(-CellGrace);
            var bounds = shape.Bounds;
            var size = BuildGrid.CellSize;
            for (var x = (int)Math.Floor(bounds.MinX / size); x <= (int)Math.Floor(bounds.MaxX / size); x++)
            {
                for (var z = (int)Math.Floor(bounds.MinZ / size); z <= (int)Math.Floor(bounds.MaxZ / size); z++)
                {
                    var cell = new OrientedArea((x + 0.5f) * size, (z + 0.5f) * size, size / 2f, size / 2f, 0f);
                    if (shape.Overlaps(cell))
                    {
                        yield return (x, z);
                    }
                }
            }
        }

        /// <summary>
        /// The way an item faces (the side one sits looking at), any rotation: Kenney and our items face −Z at 0°,
        /// Poly Haven +Z. Unity turns −Z by r degrees to (−sin r, −cos r).
        /// </summary>
        public static (float X, float Z) FrontVector(string itemId, float rotation)
        {
            var radians = rotation * Math.PI / 180.0;
            var sign = itemId != null && itemId.StartsWith("ph-", StringComparison.Ordinal) ? 1f : -1f;
            return ((float)Math.Sin(radians) * sign, (float)Math.Cos(radians) * sign);
        }

        /// <summary>The rotation that makes an item face (dx, dz) (inverse of <see cref="FrontVector"/>), in [0, 360).</summary>
        public static float RotationFacing(string itemId, float dx, float dz)
        {
            var sign = itemId != null && itemId.StartsWith("ph-", StringComparison.Ordinal) ? 1f : -1f;
            return Normalize((float)(Math.Atan2(dx * sign, dz * sign) * 180.0 / Math.PI));
        }

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
            var rotation = SnapRotation(definition, item.Rotation);
            if (definition.Kind == ItemKind.Decor || !IsQuarterTurn(rotation))
            {
                return item with { Position = new Vector3Dto(SnapDecor(item.Position.X), 0f, SnapDecor(item.Position.Z)), Rotation = rotation };
            }
            var (x, z) = Centre(Footprint(definition, item.Position.X, item.Position.Z, Quarter(rotation)));
            return item with { Position = new Vector3Dto(x, 0f, z), Rotation = rotation };
        }

        /// <summary>Small things stand on a finer grid (<see cref="BuildGrid.DecorStep"/>).</summary>
        public static float SnapDecor(float metres) => (float)Math.Round(metres / BuildGrid.DecorStep) * BuildGrid.DecorStep;

        /// <summary>Where a small thing stands (metres, its real size turned with it).</summary>
        public static OrientedArea DecorShape(RoomItemDto item, ItemDefinition definition) =>
            new OrientedArea(item.Position.X, item.Position.Z, definition.SizeX / 2f, definition.SizeZ / 2f, Normalize(item.Rotation));

        /// <summary>The bounds of <see cref="DecorShape"/> (exact at quarter turns).</summary>
        public static Area DecorArea(RoomItemDto item, ItemDefinition definition) => Snapped(DecorShape(item, definition).Bounds);

        /// <summary>The table top of a surface item (metres), turned with it, centred on the item.</summary>
        public static OrientedArea SurfaceShape(RoomItemDto item, ItemDefinition definition)
        {
            var (x, z) = IsQuarterTurn(item.Rotation) ? Centre(Footprint(item, definition)) : (item.Position.X, item.Position.Z);
            return new OrientedArea(x, z, definition.SurfaceWidth / 2f, definition.SurfaceDepth / 2f, Normalize(item.Rotation));
        }

        /// <summary>The bounds of <see cref="SurfaceShape"/> (exact at quarter turns).</summary>
        public static Area SurfaceArea(RoomItemDto item, ItemDefinition definition) => Snapped(SurfaceShape(item, definition).Bounds);

        /// <summary>Removes float noise from cos/sin of quarter turns (bounds that should be exact).</summary>
        private static Area Snapped(Area area)
        {
            float Clean(float v) => (float)Math.Round(v * 10000f) / 10000f;
            return new Area(Clean(area.MinX), Clean(area.MinZ), Clean(area.MaxX), Clean(area.MaxZ));
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

            var placed = new List<(int Index, ItemDefinition Definition, CellRect Cells, List<(int X, int Z)> Covered)>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var definition = ItemDefinitions.Find(item.ItemId);
                if (definition == null)
                {
                    problems.Add(new LayoutProblem(i, $"Unbekannter Gegenstand „{item.ItemId}“."));
                    continue;
                }
                if (TurnsFreely(definition) ? !IsWholeDegree(item.Rotation) : !IsQuarterTurn(item.Rotation))
                {
                    problems.Add(new LayoutProblem(i, TurnsFreely(definition)
                        ? $"„{definition.Name}“ lässt sich nur in ganzen Grad drehen."
                        : $"„{definition.Name}“ lässt sich nur in 90°-Schritten drehen."));
                    continue;
                }
                var cells = Footprint(item, definition);
                var (centreX, centreZ) = definition.Kind == ItemKind.Decor || !IsQuarterTurn(item.Rotation)
                    ? (SnapDecor(item.Position.X), SnapDecor(item.Position.Z))
                    : Centre(cells);
                if (Math.Abs(centreX - item.Position.X) > Tolerance || Math.Abs(centreZ - item.Position.Z) > Tolerance)
                {
                    problems.Add(new LayoutProblem(i, $"„{definition.Name}“ steht nicht auf dem Raster."));
                    continue;
                }
                if (ItemColours.Problem(item.ItemId, item.Colours) is { } colourProblem)
                {
                    problems.Add(new LayoutProblem(i, $"„{definition.Name}“: {colourProblem}."));
                    continue;
                }
                var covered = Cells(item, definition).ToList();
                if (!(IsQuarterTurn(item.Rotation) ? room.IsInside(cells) : room.IsInside(Shape(item, definition).Grown(-Tolerance))))
                {
                    problems.Add(new LayoutProblem(i, $"„{definition.Name}“ ragt aus dem Raum."));
                    continue;
                }
                placed.Add((i, definition, cells, covered));
            }

            // Overlaps per layer (reported once, at the later item). Shared cells find the candidates; two items at quarter
            // turns overlap when they share a cell, a turned one only if the real turned rectangles overlap.
            var owners = new Dictionary<(ItemKind Layer, int X, int Z), List<int>>();
            var reported = new HashSet<int>();
            var checkedPairs = new HashSet<(int, int)>();
            foreach (var (index, definition, _, covered) in placed)
            {
                if (definition.Kind == ItemKind.Decor)
                {
                    continue;
                }
                foreach (var (x, z) in covered)
                {
                    if (!owners.TryGetValue((definition.Kind, x, z), out var others))
                    {
                        owners[(definition.Kind, x, z)] = others = new List<int>();
                    }
                    foreach (var other in others)
                    {
                        if (reported.Contains(index) || !checkedPairs.Add((other, index)))
                        {
                            continue;
                        }
                        var otherDefinition = ItemDefinitions.Find(items[other].ItemId);
                        if (IsCornerJoint(items[index], items[other]) || IsRugOnZone(items[index], items[other]))
                        {
                            continue;
                        }
                        var overlaps = IsQuarterTurn(items[index].Rotation) && IsQuarterTurn(items[other].Rotation)
                            || Shape(items[index], definition).Grown(-Tolerance).Overlaps(Shape(items[other], otherDefinition).Grown(-Tolerance));
                        if (overlaps)
                        {
                            reported.Add(index);
                            problems.Add(new LayoutProblem(index, $"„{definition.Name}“ überlappt mit „{otherDefinition.Name}“."));
                        }
                    }
                    others.Add(index);
                }
            }

            foreach (var (index, definition, cells, covered) in placed)
            {
                switch (definition.Kind)
                {
                    case ItemKind.Floor:
                        if (covered.Any(c => room.Reserved.Any(r => r.Contains(c.X, c.Z))))
                        {
                            problems.Add(new LayoutProblem(index, $"„{definition.Name}“ steht im Eingang oder vor dem Lift – dort muss frei bleiben."));
                        }
                        break;
                    case ItemKind.Decor:
                        var area = DecorShape(items[index], definition);
                        if (!placed.Any(p => p.Definition.Kind == ItemKind.Floor && p.Definition.HasSurface
                                             && SurfaceShape(items[p.Index], p.Definition).Contains(area, Tolerance)))
                        {
                            problems.Add(new LayoutProblem(index, $"„{definition.Name}“ muss ganz auf einem Tisch, Regal oder einer Theke stehen."));
                        }
                        var other = placed.FirstOrDefault(p => p.Index < index && p.Definition.Kind == ItemKind.Decor
                                                               && DecorShape(items[p.Index], p.Definition).Overlaps(area, 0.01f));
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
                        break;
                }
            }

            // Everyone must be able to reach every seat.
            if (problems.Count == 0)
            {
                var blocked = BlockedTiles(items);
                blocked.UnionWith(OutsideTiles(room));
                var reachable = Reachable(room, blocked);
                foreach (var (index, definition, _, covered) in placed.Where(p => p.Definition.Seats > 0))
                {
                    if (!Neighbours(Tiles(covered)).Any(reachable.Contains))
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
                foreach (var tile in Tiles(Cells(item, definition)))
                {
                    blocked.Add(tile);
                }
            }
            return blocked;
        }

        /// <summary>Walking tiles outside the floor outline (and right at the facade): nobody stands there.</summary>
        public static HashSet<(int X, int Z)> OutsideTiles(RoomLayoutContext room)
        {
            var outside = new HashSet<(int, int)>();
            if (room.Outline == null)
            {
                return outside;
            }
            for (var x = 0; x < room.Width; x++)
            {
                for (var z = 0; z < room.Depth; z++)
                {
                    if (!room.TileWalkable(x, z))
                    {
                        outside.Add((x, z));
                    }
                }
            }
            return outside;
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

        /// <summary>Walking tiles (1 m) that some build cells touch.</summary>
        public static HashSet<(int X, int Z)> Tiles(IEnumerable<(int X, int Z)> cells)
        {
            var per = BuildGrid.CellsPerTile;
            var tiles = new HashSet<(int X, int Z)>();
            foreach (var (x, z) in cells)
            {
                tiles.Add(((int)Math.Floor(x / (double)per), (int)Math.Floor(z / (double)per)));
            }
            return tiles;
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

        private static IEnumerable<(int X, int Z)> Neighbours(HashSet<(int X, int Z)> tiles)
        {
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
    }
}
