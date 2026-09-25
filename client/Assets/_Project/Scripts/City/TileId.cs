using System;

namespace Reconnect.Client.City
{
    /// <summary>A Web-Mercator map tile (zoom, x, y). Each tile has 4 children at zoom + 1.</summary>
    public readonly struct TileId : IEquatable<TileId>
    {
        public TileId(int zoom, int x, int y)
        {
            Zoom = zoom;
            X = x;
            Y = y;
        }

        public int Zoom { get; }
        public int X { get; }
        public int Y { get; }

        public TileId Parent => new(Zoom - 1, X >> 1, Y >> 1);

        public TileId[] Children => new[]
        {
            new TileId(Zoom + 1, X * 2, Y * 2),
            new TileId(Zoom + 1, X * 2 + 1, Y * 2),
            new TileId(Zoom + 1, X * 2, Y * 2 + 1),
            new TileId(Zoom + 1, X * 2 + 1, Y * 2 + 1),
        };

        /// <summary>The ancestor at the given (lower) zoom level.</summary>
        public TileId AncestorAt(int zoom)
        {
            var shift = Zoom - zoom;
            return shift <= 0 ? this : new TileId(zoom, X >> shift, Y >> shift);
        }

        public bool Equals(TileId other) => Zoom == other.Zoom && X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is TileId other && Equals(other);
        public override int GetHashCode() => (Zoom * 397 ^ X) * 397 ^ Y;
        public override string ToString() => $"{Zoom}/{X}/{Y}";
    }
}
