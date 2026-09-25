using System.Collections.Generic;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Shortest walk on the tile grid (8 directions, no corner cutting past furniture).
    /// Every client computes the same path from the same layout, so remote avatars walk
    /// identically without the server sending paths. Pure logic – unit tested.
    /// </summary>
    public sealed class RoomPathfinder
    {
        private static readonly Vector2Int[] Directions =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
        };

        private readonly int _width;
        private readonly int _depth;
        private readonly HashSet<Vector2Int> _blocked;

        public RoomPathfinder(int width, int depth, IEnumerable<Vector2Int> blocked)
        {
            _width = width;
            _depth = depth;
            _blocked = new HashSet<Vector2Int>(blocked);
        }

        public bool IsWalkable(Vector2Int tile) =>
            tile.x >= 0 && tile.x < _width && tile.y >= 0 && tile.y < _depth && !_blocked.Contains(tile);

        /// <summary>The walkable tile closest to <paramref name="target"/> (the target itself if free).</summary>
        public Vector2Int NearestWalkable(Vector2Int target, Vector2Int from)
        {
            if (IsWalkable(target))
            {
                return target;
            }

            var best = from;
            var bestScore = float.MaxValue;
            for (var x = 0; x < _width; x++)
            for (var z = 0; z < _depth; z++)
            {
                var tile = new Vector2Int(x, z);
                if (!IsWalkable(tile))
                {
                    continue;
                }
                // Closest to the target first, then closest to where we are.
                var score = (tile - target).sqrMagnitude * 100f + (tile - from).sqrMagnitude;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = tile;
                }
            }
            return best;
        }

        /// <summary>Tiles to step through (excluding <paramref name="start"/>); empty if unreachable or already there.</summary>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            var path = new List<Vector2Int>();
            if (start == goal || !IsWalkable(goal))
            {
                return path;
            }

            // Breadth-first over 8 directions; grid is tiny (10×10), so this is plenty fast.
            var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == goal)
                {
                    break;
                }
                foreach (var direction in Directions)
                {
                    var next = current + direction;
                    if (cameFrom.ContainsKey(next) || !IsWalkable(next))
                    {
                        continue;
                    }
                    // Diagonal steps must not squeeze between two blocked tiles.
                    if (direction.x != 0 && direction.y != 0 &&
                        (!IsWalkable(current + new Vector2Int(direction.x, 0)) || !IsWalkable(current + new Vector2Int(0, direction.y))))
                    {
                        continue;
                    }
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!cameFrom.ContainsKey(goal))
            {
                return path;
            }
            for (var tile = goal; tile != start; tile = cameFrom[tile])
            {
                path.Add(tile);
            }
            path.Reverse();
            return path;
        }
    }
}
