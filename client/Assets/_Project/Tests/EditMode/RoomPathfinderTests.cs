using NUnit.Framework;
using Reconnect.Client.Rooms;
using UnityEngine;

namespace Reconnect.Client.Tests
{
    public sealed class RoomPathfinderTests
    {
        [Test]
        public void Free_room_walks_diagonally()
        {
            var finder = new RoomPathfinder(10, 10, new Vector2Int[0]);

            var path = finder.FindPath(new Vector2Int(0, 0), new Vector2Int(3, 3));

            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 1), new Vector2Int(2, 2), new Vector2Int(3, 3) }, path);
        }

        [Test]
        public void Walks_around_furniture()
        {
            // A wall of furniture at x = 2 from z = 0 to z = 3.
            var finder = new RoomPathfinder(10, 10, new[] { new Vector2Int(2, 0), new Vector2Int(2, 1), new Vector2Int(2, 2), new Vector2Int(2, 3) });

            var path = finder.FindPath(new Vector2Int(0, 0), new Vector2Int(4, 0));

            Assert.IsNotEmpty(path);
            Assert.AreEqual(new Vector2Int(4, 0), path[path.Count - 1]);
            Assert.IsFalse(path.Exists(t => t.x == 2 && t.y <= 3), "never steps onto furniture");
        }

        [Test]
        public void Tap_on_furniture_walks_next_to_it()
        {
            var sofa = new Vector2Int(5, 9);
            var finder = new RoomPathfinder(10, 10, new[] { sofa });

            var target = finder.NearestWalkable(sofa, from: new Vector2Int(5, 2));

            Assert.AreEqual(new Vector2Int(5, 8), target, "the free tile in front of the sofa");
        }

        [Test]
        public void Unreachable_goal_gives_empty_path()
        {
            var finder = new RoomPathfinder(3, 3, new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2) });

            Assert.IsEmpty(finder.FindPath(new Vector2Int(0, 0), new Vector2Int(2, 2)));
        }
    }
}
