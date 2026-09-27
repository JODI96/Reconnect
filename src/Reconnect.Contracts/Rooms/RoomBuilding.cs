using System;
using System.Collections.Generic;
using System.Globalization;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>
    /// The build grid: a walking tile (1 m) has 4 × 4 build cells of 25 cm. Every item stands exactly on build cells and
    /// is turned in 90° steps, so its footprint is a rectangle of cells (see <see cref="RoomLayout"/>).
    /// </summary>
    public static class BuildGrid
    {
        public const float CellSize = 0.25f;
        public const int CellsPerTile = 4;
        public const int MaxItems = 400;

        /// <summary>Small things on tables snap finer: 12.5 cm steps inside the table top.</summary>
        public const float DecorStep = 0.125f;
    }

    /// <summary>How an item is placed.</summary>
    public enum ItemKind
    {
        /// <summary>Stands on the floor, blocks its cells (and walking there).</summary>
        Floor = 0,

        /// <summary>Lies on the floor under other things (rugs); walkable, only one rug per cell.</summary>
        Rug = 1,

        /// <summary>Small thing on a table, shelf or counter: must lie fully on the surface of one item.</summary>
        Decor = 2,

        /// <summary>Hangs on a wall (paintings): along a wall of the room.</summary>
        Wall = 3,

        /// <summary>Hangs from the ceiling (chandeliers, string lights): above everything, not on the floor.</summary>
        Ceiling = 4,
    }

    /// <summary>Walls of a room (Habbo style rooms have walls in the north and east).</summary>
    [Flags]
    public enum WallSides
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8,
    }

    /// <summary>A catalog entry: footprint and placement rules of one item id (generated from the 3D models).</summary>
    public sealed class ItemDefinition
    {
        public ItemDefinition(string id, string name, string category, ItemKind kind, int width, int depth, float height,
            float surfaceHeight = 0f, float mountHeight = 0f, float sizeX = 0f, float sizeZ = 0f,
            float surfaceWidth = 0f, float surfaceDepth = 0f)
        {
            SizeX = sizeX > 0f ? sizeX : Math.Max(1, width) * BuildGrid.CellSize;
            SizeZ = sizeZ > 0f ? sizeZ : Math.Max(1, depth) * BuildGrid.CellSize;
            SurfaceWidth = surfaceHeight > 0f ? (surfaceWidth > 0f ? surfaceWidth : Math.Max(1, width) * BuildGrid.CellSize) : 0f;
            SurfaceDepth = surfaceHeight > 0f ? (surfaceDepth > 0f ? surfaceDepth : Math.Max(1, depth) * BuildGrid.CellSize) : 0f;
            Id = id;
            Name = name;
            Category = category;
            Kind = kind;
            Width = Math.Max(1, width);
            Depth = Math.Max(1, depth);
            Height = height;
            SurfaceHeight = surfaceHeight;
            MountHeight = mountHeight;
        }

        public string Id { get; }

        /// <summary>German display name for the build catalog.</summary>
        public string Name { get; }

        /// <summary>Catalog tab (Sitzen, Tische, Aufbewahrung, Küche & Bar, Licht, Pflanzen, Deko, Wand, Teppiche, Spezial).</summary>
        public string Category { get; }

        public ItemKind Kind { get; }

        /// <summary>Footprint in build cells at rotation 0 (X).</summary>
        public int Width { get; }

        /// <summary>Footprint in build cells at rotation 0 (Z).</summary>
        public int Depth { get; }

        /// <summary>Height of the model in metres.</summary>
        public float Height { get; }

        /// <summary>Things can stand on it at this height (tables, shelves, counters); 0 = no surface.</summary>
        public float SurfaceHeight { get; }

        /// <summary>Wall and ceiling items: height of their lower edge above the floor.</summary>
        public float MountHeight { get; }

        public bool HasSurface => SurfaceHeight > 0f;

        /// <summary>Real size of the model in metres at rotation 0 (small things on tables are placed by it).</summary>
        public float SizeX { get; }
        public float SizeZ { get; }

        /// <summary>The flat top things can stand on (metres, centred on the footprint, at rotation 0) – measured on the model.</summary>
        public float SurfaceWidth { get; }
        public float SurfaceDepth { get; }

        /// <summary>Places to sit on it (<see cref="RoomSeats"/>).</summary>
        public int Seats => RoomSeats.PlacesFor(Id);
    }

    /// <summary>A rectangle of build cells (X/Z = lowest cell).</summary>
    public readonly struct CellRect : IEquatable<CellRect>
    {
        public CellRect(int x, int z, int width, int depth)
        {
            X = x;
            Z = z;
            Width = width;
            Depth = depth;
        }

        public int X { get; }
        public int Z { get; }
        public int Width { get; }
        public int Depth { get; }
        public int XMax => X + Width;
        public int ZMax => Z + Depth;

        public bool Overlaps(CellRect other) => X < other.XMax && other.X < XMax && Z < other.ZMax && other.Z < ZMax;

        public bool Contains(CellRect other) => other.X >= X && other.XMax <= XMax && other.Z >= Z && other.ZMax <= ZMax;

        public bool Contains(int x, int z) => x >= X && x < XMax && z >= Z && z < ZMax;

        public bool Equals(CellRect other) => X == other.X && Z == other.Z && Width == other.Width && Depth == other.Depth;

        public override bool Equals(object obj) => obj is CellRect other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ (Z * 7919) ^ (Width * 31) ^ Depth;

        public override string ToString() => $"({X},{Z}) {Width}×{Depth}";
    }

    /// <summary>
    /// All build items (generated catalog + pools of any size). <see cref="ItemCatalogData"/> is written by the Unity
    /// project setup from the real 3D models; server and client use the same definitions.
    /// </summary>
    public static class ItemDefinitions
    {
        private static Dictionary<string, ItemDefinition> _byId;

        public static IReadOnlyCollection<ItemDefinition> All => ById.Values;

        private static Dictionary<string, ItemDefinition> ById
        {
            get
            {
                if (_byId == null)
                {
                    var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
                    foreach (var definition in ItemCatalogData.Items)
                    {
                        map[definition.Id] = definition;
                    }
                    _byId = map;
                }
                return _byId;
            }
        }

        public static ItemDefinition Find(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }
            if (ById.TryGetValue(itemId, out var definition))
            {
                return definition;
            }
            if (TryPoolSize(itemId, out var width, out var depth))
            {
                return new ItemDefinition(itemId, $"Pool {width}×{depth} m", "Spezial", ItemKind.Floor, width * BuildGrid.CellsPerTile, depth * BuildGrid.CellsPerTile, 0.05f);
            }
            // Floor zones come in any size (custom-zone-wood-15x16): a thin inlay covering exactly that area.
            if (FurnitureFamilies.TryZone(itemId, out _, out var zoneWidth, out var zoneDepth))
            {
                var cellsX = (int)Math.Round(zoneWidth / BuildGrid.CellSize);
                var cellsZ = (int)Math.Round(zoneDepth / BuildGrid.CellSize);
                return new ItemDefinition(itemId, FurnitureFamilies.Find(itemId).Name, FurnitureFamilies.Floors, ItemKind.Rug, cellsX, cellsZ, 0.012f,
                    sizeX: zoneWidth, sizeZ: zoneDepth);
            }
            return null;
        }

        /// <summary>"custom-pool" = 6 × 3 m, "custom-pool-12x6" = 12 × 6 m (whole metres, water surface).</summary>
        public static bool TryPoolSize(string itemId, out int width, out int depth)
        {
            width = 6;
            depth = 3;
            if (itemId == "custom-pool")
            {
                return true;
            }
            const string prefix = "custom-pool-";
            if (itemId == null || !itemId.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }
            var parts = itemId.Substring(prefix.Length).Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d))
            {
                return false;
            }
            width = Math.Max(2, Math.Min(30, w));
            depth = Math.Max(2, Math.Min(20, d));
            return true;
        }

        public static bool IsPool(string itemId) => TryPoolSize(itemId, out _, out _);
    }
}
