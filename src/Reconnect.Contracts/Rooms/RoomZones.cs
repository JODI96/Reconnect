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

        public static WallSides WallsFor(string theme) => theme switch
        {
            "lobby" => WallSides.North,
            "coworking" or "conference" or "skylounge" or "rooftop" or "pool" => WallSides.None,
            _ => WallSides.North | WallSides.East,
        };

        public static bool HasDoor(string theme) => WallsFor(theme) == (WallSides.North | WallSides.East);

        /// <summary>The full rule context of a room (walls and reserved cells for its current lift position).</summary>
        public static RoomLayoutContext ContextFor(string theme, int width, int depth, IReadOnlyList<RoomItemDto> items) =>
            new RoomLayoutContext(width, depth, WallsFor(theme), Reserved(theme, width, depth, items));

        /// <summary>Cells that stay free: in front of the door and in front of the lift doors.</summary>
        public static IReadOnlyList<CellRect> Reserved(string theme, int width, int depth, IReadOnlyList<RoomItemDto> items)
        {
            var per = BuildGrid.CellsPerTile;
            var reserved = new List<CellRect>();
            if (HasDoor(theme))
            {
                // Same arithmetic as the client's wall builder: piece (ceil(width / 2) / 2) of 2 m is the doorway.
                var pieces = (width + 1) / 2;
                var doorTile = (pieces / 2) * 2;
                reserved.Add(new CellRect(doorTile * per, (depth - DoorClearanceTiles) * per, DoorWidthTiles * per, DoorClearanceTiles * per));
            }
            foreach (var elevator in items.Where(i => i.ItemId == ElevatorItem))
            {
                // Two tiles deep in front of the doors, three wide.
                var (x, z) = ElevatorLanding(elevator);
                var (fx, fz) = Forward(elevator.Rotation);
                var (sx, sz) = (fz, fx);
                var tiles = new[] { 0, 1 }.SelectMany(k => new[] { -1, 0, 1 }.Select(s => (X: x + k * fx + s * sx, Z: z + k * fz + s * sz))).ToList();
                var minX = tiles.Min(t => t.X);
                var minZ = tiles.Min(t => t.Z);
                reserved.Add(new CellRect(minX * per, minZ * per, (tiles.Max(t => t.X) - minX + 1) * per, (tiles.Max(t => t.Z) - minZ + 1) * per));
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
