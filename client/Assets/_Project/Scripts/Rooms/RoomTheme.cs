using UnityEngine;

namespace Reconnect.Client.Rooms
{
    public enum FloorPattern
    {
        Planks,
        Parquet,
        Checker,
        Marble,
        Concrete,
        Terrazzo,
    }

    /// <summary>What surrounds the floor.</summary>
    public enum Enclosure
    {
        /// <summary>Habbo-style back walls (north + east) with windows and a door.</summary>
        Walls,

        /// <summary>Open roof terrace: glass railing on all sides, sky and city around it.</summary>
        Railing,

        /// <summary>Glass box on top of a tower: floor-to-ceiling glass facade, luminous ceiling, city around it.</summary>
        GlassFacade,

        /// <summary>Entrance hall of a tower: 10 m polished stone wall (lift core side), full-height glass to the street.</summary>
        StoneHall,
    }

    /// <summary>Look of a room theme (ids must match the backend's RoomThemes).</summary>
    public readonly struct RoomTheme
    {
        public RoomTheme(FloorPattern floor, Color floorA, Color floorB, Color wall, Color wallTrim, Color light,
            float lightIntensity, Enclosure enclosure = Enclosure.Walls)
        {
            Floor = floor;
            FloorA = floorA;
            FloorB = floorB;
            Wall = wall;
            WallTrim = wallTrim;
            Light = light;
            LightIntensity = lightIntensity;
            Enclosure = enclosure;
        }

        public FloorPattern Floor { get; }
        public Color FloorA { get; }
        public Color FloorB { get; }
        public Color Wall { get; }
        public Color WallTrim { get; }
        public Color Light { get; }
        public float LightIntensity { get; }
        public Enclosure Enclosure { get; }

        /// <summary>The room stands on its real building in the 3D city (terrace, glass floor or tower lobby).</summary>
        public bool Outdoor => Enclosure != Enclosure.Walls;

        /// <summary>Wall/stone texture: the lobby's green serpentine.</summary>
        public bool StoneWalls => Enclosure == Enclosure.StoneHall;

        public static RoomTheme For(string id) => id switch
        {
            "lobby" => new RoomTheme(FloorPattern.Marble, Hex(0xD6D0C5), Hex(0xA69D8F), Hex(0x1E3B30), Hex(0xC9A45C), Hex(0xFFE6C4), 1.1f, Enclosure.StoneHall),
            "coworking" => new RoomTheme(FloorPattern.Planks, Hex(0xCDAA7D), Hex(0xB38D62), Hex(0x55675F), Hex(0xFFB547), Hex(0xFFF1DC), 1.5f, Enclosure.GlassFacade),
            "conference" => new RoomTheme(FloorPattern.Concrete, Hex(0x4B505B), Hex(0x3C414B), Hex(0x6B7A74), Hex(0xFFC46B), Hex(0xFFEAD0), 1.6f, Enclosure.GlassFacade),
            "skylounge" => new RoomTheme(FloorPattern.Terrazzo, Hex(0xCFC9BE), Hex(0xBDB6AA), Hex(0x9FD8CF), Hex(0x5CE1FF), Hex(0xFFE2C0), 1.4f, Enclosure.GlassFacade),
            "pool" => new RoomTheme(FloorPattern.Planks, Hex(0xB58B62), Hex(0x9A7250), Hex(0xEDEAE3), Hex(0x2EB6D9), Hex(0xFFF1D8), 1.9f, Enclosure.Railing),
            "rooftop" => new RoomTheme(FloorPattern.Planks, Hex(0xA87450), Hex(0x8C5E3E), Hex(0xE8E4DC), Hex(0xFF5C8A), Hex(0xFFC98A), 1.6f, Enclosure.Railing),
            "cafe" => new RoomTheme(FloorPattern.Checker, Hex(0xF1EBDD), Hex(0x2B2B30), Hex(0xE9D3B0), Hex(0x7A4A2E), Hex(0xFFD9A0), 2.0f),
            "atelier" => new RoomTheme(FloorPattern.Concrete, Hex(0xBFC3C8), Hex(0xA9AEB5), Hex(0xF5F5F2), Hex(0x2F80ED), Hex(0xF0F5FF), 1.8f),
            "opera" => new RoomTheme(FloorPattern.Marble, Hex(0xF0E8DA), Hex(0xC9B99E), Hex(0x7A1627), Hex(0xD4AF37), Hex(0xFFD27A), 2.4f),
            "library" => new RoomTheme(FloorPattern.Parquet, Hex(0x8A6038), Hex(0x6E4A2A), Hex(0x2F5D50), Hex(0xD9C49A), Hex(0xFFD8A8), 2.2f),
            _ => new RoomTheme(FloorPattern.Planks, Hex(0xD6AA78), Hex(0xC49668), Hex(0xECE2D6), Hex(0xFF5C8A), Hex(0xFFE0B8), 1.8f),
        };

        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
