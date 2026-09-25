using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>Look of a room theme (ids must match the backend's RoomThemes).</summary>
    public readonly struct RoomTheme
    {
        public RoomTheme(Color floorA, Color floorB, Color wall, Color wallTrim, Color light, float lightIntensity)
        {
            FloorA = floorA;
            FloorB = floorB;
            Wall = wall;
            WallTrim = wallTrim;
            Light = light;
            LightIntensity = lightIntensity;
        }

        public Color FloorA { get; }
        public Color FloorB { get; }
        public Color Wall { get; }
        public Color WallTrim { get; }
        public Color Light { get; }
        public float LightIntensity { get; }

        public static RoomTheme For(string id) => id switch
        {
            "rooftop" => new RoomTheme(Hex(0x3B3F4E), Hex(0x33364A), Hex(0x2A2D52), Hex(0xFF5C8A), Hex(0xFFC98A), 2.4f),
            "cafe" => new RoomTheme(Hex(0xF2E6D0), Hex(0xB8573E), Hex(0xE8D4B4), Hex(0x7A4A2E), Hex(0xFFD9A0), 2.0f),
            "atelier" => new RoomTheme(Hex(0xC9CCD1), Hex(0xB9BCC2), Hex(0xF4F4F2), Hex(0x2F80ED), Hex(0xE8F1FF), 1.6f),
            "opera" => new RoomTheme(Hex(0x8E1B2E), Hex(0x7A1627), Hex(0xF3E3C3), Hex(0xC9A227), Hex(0xFFD27A), 2.6f),
            "library" => new RoomTheme(Hex(0x7B5A3C), Hex(0x6E5034), Hex(0x2F5D50), Hex(0xD9C49A), Hex(0xFFD8A8), 2.2f),
            _ => new RoomTheme(Hex(0xD6AA78), Hex(0xC49668), Hex(0xECE2D6), Hex(0xFF5C8A), Hex(0xFFE0B8), 1.8f),
        };

        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
