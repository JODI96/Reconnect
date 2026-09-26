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
                var (x, z) = ElevatorLanding(elevator);
                reserved.Add(new CellRect((x - 1) * per, (z - 1) * per, 3 * per, 2 * per));
            }
            return reserved;
        }

        /// <summary>Tile in front of the lift doors (the doors face the item's -Z side) where arriving people appear.</summary>
        public static (int X, int Z) ElevatorLanding(RoomItemDto elevator)
        {
            var definition = ItemDefinitions.Find(elevator.ItemId);
            var depth = definition != null ? RoomLayout.Size(definition, RoomLayout.Quarter(elevator.Rotation)).Depth * BuildGrid.CellSize : 2.4f;
            var radians = elevator.Rotation * System.Math.PI / 180.0;
            // Forward (-Z at rotation 0) turned around Y.
            var dx = -System.Math.Sin(radians);
            var dz = -System.Math.Cos(radians);
            var reach = depth / 2 + 0.8;
            return ((int)System.Math.Floor(elevator.Position.X + dx * reach), (int)System.Math.Floor(elevator.Position.Z + dz * reach));
        }
    }
}
