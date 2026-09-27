using System;
using System.Collections.Generic;
using System.Linq;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>
    /// Where a room has walls and which cells must stay free (door, lift landing) – the rest is the build zone.
    /// Mirrors the client's enclosures (RoomTheme): Habbo rooms have walls in the north and east with the door in the
    /// middle of the north wall; the tower lobby has its stone wall in the north; glass floors and terraces have none.
    /// </summary>
    public static class RoomZones
    {
        /// <summary>Door of Habbo-style rooms: one 2 m wall piece in the middle of the north wall.</summary>
        public const int DoorWidthTiles = 2;
        public const int DoorClearanceTiles = 2;

        /// <summary>People arriving by lift step out here; keep it free.</summary>
        public const string ElevatorItem = "custom-elevator";

        /// <summary>
        /// The concrete core of a tower floor with lift doors on both long sides. Part of the building: it can't be moved
        /// or removed in the build editor, and people arrive in front of its doors.
        /// </summary>
        public const string CoreItem = "custom-core";

        public static bool IsLift(string itemId) => itemId == ElevatorItem || itemId == CoreItem;

        /// <summary>Items that belong to the building (the editor can't pick them up; the server keeps them in place).</summary>
        public static bool IsFixed(string itemId) => itemId == CoreItem;

        public static WallSides WallsFor(string theme) => theme switch
        {
            "lobby" => WallSides.North,
            "coworking" or "conference" or "skylounge" or "rooftop" or "pool" => WallSides.None,
            _ => WallSides.North | WallSides.East,
        };

        public static bool HasDoor(string theme) => WallsFor(theme) == (WallSides.North | WallSides.East);

        /// <summary>The full rule context of a room (walls and reserved cells for its current lift position).</summary>
        /// <param name="outline">Floor outline of rooms that are not rectangles (tower floors): glass all round, no door.</param>
        public static RoomLayoutContext ContextFor(string theme, int width, int depth, IReadOnlyList<RoomItemDto> items,
            IReadOnlyList<RoomPointDto> outline = null) =>
            new RoomLayoutContext(width, depth, outline != null ? WallSides.None : WallsFor(theme),
                Reserved(theme, width, depth, items, outline), outline);

        /// <summary>Cells that stay free: in front of the door and in front of the lift doors.</summary>
        public static IReadOnlyList<CellRect> Reserved(string theme, int width, int depth, IReadOnlyList<RoomItemDto> items,
            IReadOnlyList<RoomPointDto> outline = null)
        {
            var per = BuildGrid.CellsPerTile;
            var reserved = new List<CellRect>();
            if (outline == null && HasDoor(theme))
            {
                // Same arithmetic as the client's wall builder: piece (ceil(width / 2) / 2) of 2 m is the doorway.
                var pieces = (width + 1) / 2;
                var doorTile = (pieces / 2) * 2;
                reserved.Add(new CellRect(doorTile * per, (depth - DoorClearanceTiles) * per, DoorWidthTiles * per, DoorClearanceTiles * per));
            }
            foreach (var lift in items.Where(i => IsLift(i.ItemId)))
            {
                foreach (var (x, z, fx, fz) in Landings(lift))
                {
                    // Two tiles deep in front of the doors; three wide at a lift bank, the whole side at a core.
                    var (sx, sz) = (fz, fx);
                    var half = lift.ItemId == CoreItem ? CoreSideReach(lift) : 1;
                    var tiles = new[] { 0, 1 }.SelectMany(k => Enumerable.Range(-half, 2 * half + 1).Select(s => (X: x + k * fx + s * sx, Z: z + k * fz + s * sz))).ToList();
                    var minX = tiles.Min(t => t.X);
                    var minZ = tiles.Min(t => t.Z);
                    reserved.Add(new CellRect(minX * per, minZ * per, (tiles.Max(t => t.X) - minX + 1) * per, (tiles.Max(t => t.Z) - minZ + 1) * per));
                }
            }
            return reserved;
        }

        /// <summary>
        /// First tile in front of the lift doors (the doors face the item's -Z side) that the lift doesn't cover:
        /// people arriving by lift appear here.
        /// </summary>
        public static (int X, int Z) ElevatorLanding(RoomItemDto elevator)
        {
            var definition = ItemDefinitions.Find(elevator.ItemId);
            var covered = definition == null
                ? new HashSet<(int X, int Z)>()
                : new HashSet<(int X, int Z)>(RoomLayout.Tiles(RoomLayout.Footprint(elevator, definition)));
            var (fx, fz) = Forward(elevator.Rotation);
            var tile = ((int)Math.Floor(elevator.Position.X), (int)Math.Floor(elevator.Position.Z));
            for (var step = 0; step < 20 && covered.Contains(tile); step++)
            {
                tile = (tile.Item1 + fx, tile.Item2 + fz);
            }
            return tile;
        }

        /// <summary>Landing tiles of a lift: in front of a lift bank; on both long sides of a core.</summary>
        public static IEnumerable<(int X, int Z, int Fx, int Fz)> Landings(RoomItemDto lift)
        {
            var (fx, fz) = Forward(lift.Rotation);
            var (x, z) = ElevatorLanding(lift);
            yield return (x, z, fx, fz);
            if (lift.ItemId == CoreItem)
            {
                var (bx, bz) = ElevatorLanding(lift with { Rotation = lift.Rotation + 180f });
                yield return (bx, bz, -fx, -fz);
            }
        }

        /// <summary>Tiles to each side of the landing along a core's long side.</summary>
        private static int CoreSideReach(RoomItemDto core)
        {
            var definition = ItemDefinitions.Find(core.ItemId);
            if (definition == null)
            {
                return 2;
            }
            var (width, depth) = RoomLayout.Size(definition, RoomLayout.Quarter(core.Rotation));
            var along = RoomLayout.Quarter(core.Rotation) % 2 == 0 ? width : depth;
            return Math.Max(1, along / BuildGrid.CellsPerTile / 2 - 1);
        }

        /// <summary>The item's front (-Z at rotation 0) in whole tiles, turned in quarter steps.</summary>
        private static (int X, int Z) Forward(float rotation) => RoomLayout.Quarter(rotation) switch
        {
            1 => (-1, 0),
            2 => (0, 1),
            3 => (1, 0),
            _ => (0, -1),
        };
    }
}
