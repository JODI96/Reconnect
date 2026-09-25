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
    }

    /// <summary>Look of a room theme (ids must match the backend's RoomThemes).</summary>
    public readonly struct RoomTheme
    {
        public RoomTheme(FloorPattern floor, Color floorA, Color floorB, Color wall, Color wallTrim, Color light,
            float lightIntensity, bool outdoor = false)
        {
            Floor = floor;
            FloorA = floorA;
            FloorB = floorB;
            Wall = wall;
            WallTrim = wallTrim;
            Light = light;
            LightIntensity = lightIntensity;
            Outdoor = outdoor;
        }

        public FloorPattern Floor { get; }
        public Color FloorA { get; }
        public Color FloorB { get; }
        public Color Wall { get; }
        public Color WallTrim { get; }
        public Color Light { get; }
        public float LightIntensity { get; }

        /// <summary>Open-air room (roof terrace): no walls, glass railing, sky and city around it.</summary>
        public bool Outdoor { get; }

        public static RoomTheme For(string id) => id switch
        {
            "rooftop" => new RoomTheme(FloorPattern.Planks, Hex(0xA87450), Hex(0x8C5E3E), Hex(0xE8E4DC), Hex(0xFF5C8A), Hex(0xFFC98A), 1.6f, outdoor: true),
            "cafe" => new RoomTheme(FloorPattern.Checker, Hex(0xF1EBDD), Hex(0x2B2B30), Hex(0xE9D3B0), Hex(0x7A4A2E), Hex(0xFFD9A0), 2.0f),
            "atelier" => new RoomTheme(FloorPattern.Concrete, Hex(0xBFC3C8), Hex(0xA9AEB5), Hex(0xF5F5F2), Hex(0x2F80ED), Hex(0xF0F5FF), 1.8f),
            "opera" => new RoomTheme(FloorPattern.Marble, Hex(0xF0E8DA), Hex(0xC9B99E), Hex(0x7A1627), Hex(0xD4AF37), Hex(0xFFD27A), 2.4f),
            "library" => new RoomTheme(FloorPattern.Parquet, Hex(0x8A6038), Hex(0x6E4A2A), Hex(0x2F5D50), Hex(0xD9C49A), Hex(0xFFD8A8), 2.2f),
            _ => new RoomTheme(FloorPattern.Planks, Hex(0xD6AA78), Hex(0xC49668), Hex(0xECE2D6), Hex(0xFF5C8A), Hex(0xFFE0B8), 1.8f),
        };

        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
