using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Reconnect.Contracts.Avatars
{
    /// <summary>One worn part (garment, hairstyle, beard, hat): its colour variant or a tint (<see cref="Wardrobe.Tints"/>).</summary>
    public sealed record AvatarPartDto(string Id, string Variant = "default", string Tint = null);

    /// <summary>
    /// Face paint: each colour (hex RRGGBB) with how much of it (0 … 1). Nothing set = none.
    /// </summary>
    public sealed record AvatarMakeupDto(
        string Lips = null, float LipsAmount = 0f, string Eyeshadow = null, float EyeshadowAmount = 0f,
        string Blush = null, float BlushAmount = 0f, float Liner = 0f);

    /// <summary>
    /// What someone looks like (character creator): body, skin, eyes, brows, the worn parts, the way they walk, their
    /// height (0.94 … 1.06 of the body's), body and face shape (<see cref="Wardrobe.Morphs"/> id → −1 … 1, one-sided
    /// shapes 0 … 1; missing = 0), an eye colour of one's own (hex RRGGBB, tints the iris; null = the colour of
    /// <see cref="Eyes"/>) and make-up. Stored in the profile, sent with every player in a room.
    /// </summary>
    public sealed record AvatarLookDto(
        string Body, string Skin, string Eyes, string Brows, IReadOnlyList<AvatarPartDto> Parts,
        string WalkStyle = "normal", float Height = 1f, IReadOnlyDictionary<string, float> Shape = null,
        string EyeColour = null, AvatarMakeupDto Makeup = null);

    /// <summary>
    /// The wardrobe of the character creator (ids generated from MakeHuman by tools/avatars/build_wardrobe.py, see
    /// WardrobeData.cs) and the rules a look must follow – checked by the server and the creator alike.
    /// </summary>
    public static partial class Wardrobe
    {
        public const string Female = "female";
        public const string Male = "male";

        public const string Top = "top";
        public const string Bottom = "bottom";
        public const string Dress = "dress";
        public const string Outfit = "outfit";
        public const string Shoes = "shoes";
        public const string Hat = "hat";
        public const string Hair = "hair";
        public const string Beard = "beard";
        public const string Brows = "brows";

        public const float MinHeight = 0.94f;
        public const float MaxHeight = 1.06f;

        /// <summary>Ways to walk (animation per style, see the client's avatar setup).</summary>
        public static readonly IReadOnlyDictionary<string, string> WalkStyles = new Dictionary<string, string>
        {
            ["normal"] = "Normal", ["elegant"] = "Elegant", ["confident"] = "Selbstbewusst", ["relaxed"] = "Lässig", ["model"] = "Model",
        };

        /// <summary>Tints for hair and clothes (hex colour); a tint recolours the part keeping its light and shade.</summary>
        public static readonly IReadOnlyDictionary<string, string> Tints = new Dictionary<string, string>
        {
            // Hair.
            ["black"] = "1C1A1A", ["espresso"] = "3B2A22", ["chestnut"] = "5E3B26", ["caramel"] = "8C5A32", ["honey"] = "B68A52",
            ["blonde"] = "D8B97E", ["platinum"] = "E6DCC8", ["auburn"] = "7A2E1C", ["copper"] = "B0522A", ["silver"] = "B5B5B8",
            // Fashion.
            ["white"] = "EEEDEA", ["cream"] = "E8DFCC", ["sand"] = "C9B79A", ["camel"] = "A77B4F", ["grey"] = "7C7D80",
            ["charcoal"] = "3A3B3F", ["navy"] = "1F2A48", ["denim"] = "3E5A80", ["sky"] = "8FB5D8", ["teal"] = "1E6F6E",
            ["sage"] = "8E9F83", ["olive"] = "5E6437", ["emerald"] = "125B40", ["burgundy"] = "6A1624", ["red"] = "A8212A",
            ["coral"] = "E07A5F", ["blush"] = "E3B3AA", ["pink"] = "D96A9A", ["lilac"] = "A58BC4", ["mustard"] = "C99A2E",
        };

        /// <summary>Kinds a person can wear once each; a dress or outfit takes the place of top and bottom.</summary>
        private static readonly string[] Single = { Top, Bottom, Dress, Outfit, Shoes, Hat, Hair, Beard };

        /// <summary>Everything wrong with a look (empty = valid).</summary>
        public static IReadOnlyList<string> Problems(AvatarLookDto look)
        {
            var problems = new List<string>();
            if (look == null)
            {
                problems.Add("Kein Aussehen.");
                return problems;
            }
            if (!Parts.TryGetValue(look.Body ?? "", out var parts))
            {
                problems.Add("Unbekannter Körper.");
                return problems;
            }
            if (!Skins.TryGetValue(look.Body, out var skins) || !skins.Contains(look.Skin))
            {
                problems.Add("Unbekannter Hautton.");
            }
            if (!Eyes.Contains(look.Eyes))
            {
                problems.Add("Unbekannte Augenfarbe.");
            }
            if (!parts.TryGetValue(look.Brows ?? "", out var brows) || brows.Kind != Brows)
            {
                problems.Add("Unbekannte Augenbrauen.");
            }
            if (!WalkStyles.ContainsKey(look.WalkStyle ?? ""))
            {
                problems.Add("Unbekannter Laufstil.");
            }
            if (float.IsNaN(look.Height) || look.Height < MinHeight || look.Height > MaxHeight)
            {
                problems.Add("Grösse ausserhalb des Bereichs.");
            }
            var worn = look.Parts ?? Array.Empty<AvatarPartDto>();
            var kinds = new List<string>();
            foreach (var part in worn)
            {
                if (part == null || !parts.TryGetValue(part.Id ?? "", out var definition) || definition.Kind == Brows)
                {
                    problems.Add($"Unbekanntes Teil „{part?.Id}“.");
                    continue;
                }
                if (!definition.Variants.Contains(part.Variant ?? "default"))
                {
                    problems.Add($"Unbekannte Farbvariante für „{part.Id}“.");
                }
                if (part.Tint != null && !Tints.ContainsKey(part.Tint))
                {
                    problems.Add($"Unbekannte Farbe für „{part.Id}“.");
                }
                kinds.Add(definition.Kind);
            }
            foreach (var kind in Single.Where(k => kinds.Count(x => x == k) > 1))
            {
                problems.Add($"Zu viele Teile der Art „{kind}“.");
            }
            var fullBody = kinds.Contains(Dress) || kinds.Contains(Outfit);
            if (fullBody && (kinds.Contains(Top) || kinds.Contains(Bottom) || (kinds.Contains(Dress) && kinds.Contains(Outfit))))
            {
                problems.Add("Ein Kleid oder Outfit ersetzt Oberteil und Hose.");
            }
            if (worn.Count > 8)
            {
                problems.Add("Zu viele Teile.");
            }
            if (look.EyeColour != null && !IsHex(look.EyeColour))
            {
                problems.Add("Ungültige Augenfarbe.");
            }
            if (look.Makeup is { } makeup)
            {
                foreach (var (colour, amount, name) in new[]
                         {
                             (makeup.Lips, makeup.LipsAmount, "Lippen"), (makeup.Eyeshadow, makeup.EyeshadowAmount, "Lidschatten"),
                             (makeup.Blush, makeup.BlushAmount, "Rouge"), ("000000", makeup.Liner, "Eyeliner"),
                         })
                {
                    if ((colour != null && !IsHex(colour)) || float.IsNaN(amount) || amount < 0f || amount > 1f)
                    {
                        problems.Add($"Ungültiges Make-up ({name}).");
                    }
                }
            }
            if (look.Shape != null)
            {
                if (look.Shape.Count > Morphs.Length)
                {
                    problems.Add("Zu viele Formen.");
                }
                foreach (var shape in look.Shape)
                {
                    var morph = Array.Find(Morphs, m => m.Id == shape.Key);
                    if (morph.Id == null || Array.IndexOf(morph.Bodies, look.Body) < 0)
                    {
                        problems.Add($"Unbekannte Form „{shape.Key}“.");
                    }
                    else if (float.IsNaN(shape.Value) || shape.Value > 1f || shape.Value < (morph.TwoSided ? -1f : 0f))
                    {
                        problems.Add($"Form „{shape.Key}“ ausserhalb des Bereichs.");
                    }
                }
            }
            return problems;
        }

        public static bool IsValid(AvatarLookDto look) => Problems(look).Count == 0;

        /// <summary>A colour as six hex digits (RRGGBB).</summary>
        public static bool IsHex(string colour) =>
            colour is { Length: 6 } && colour.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'));

        /// <summary>Hex colour (RRGGBB) as channels 0 … 1.</summary>
        public static (float R, float G, float B) HexColour(string hex)
        {
            if (!IsHex(hex))
            {
                return (1f, 1f, 1f);
            }
            float Channel(int start) => int.Parse(hex.Substring(start, 2), NumberStyles.HexNumber) / 255f;
            return (Channel(0), Channel(2), Channel(4));
        }

        /// <summary>A tint as colour channels 0 … 1.</summary>
        public static (float R, float G, float B) TintColour(string tint)
        {
            var hex = Tints.TryGetValue(tint ?? "", out var value) ? value : "FFFFFF";
            float Channel(int start) => int.Parse(hex.Substring(start, 2), NumberStyles.HexNumber) / 255f;
            return (Channel(0), Channel(2), Channel(4));
        }

        /// <summary>A plain look for the body (first skin, brown eyes, simple clothes) – the start of the creator.</summary>
        public static AvatarLookDto Default(string body)
        {
            if (body == Male)
            {
                return new AvatarLookDto(Male, Skins[Male][1], "brown", "eyebrow001", new[]
                {
                    new AvatarPartDto("short02"), new AvatarPartDto("toigo_basic_tucked_t-shirt", Tint: "white"),
                    new AvatarPartDto("toigo_wool_pants", Tint: "charcoal"), new AvatarPartDto("shoes03"),
                });
            }
            return new AvatarLookDto(Female, Skins[Female][1], "brown", "eyebrow010", new[]
            {
                new AvatarPartDto("long01"), new AvatarPartDto("toigo_shift_dress"), new AvatarPartDto("toigo_ballet_flats"),
            });
        }
    }
}
