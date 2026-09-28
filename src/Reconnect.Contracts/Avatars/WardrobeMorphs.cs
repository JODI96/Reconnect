using System;
using System.Collections.Generic;
using System.Linq;

namespace Reconnect.Contracts.Avatars
{
    /// <summary>Body and face shapes of the character creator: German names, groups, weight in kilograms.</summary>
    public static partial class Wardrobe
    {
        /// <summary>Groups of shapes in the creator's order, with their German titles.</summary>
        public static readonly IReadOnlyList<(string Id, string Title)> MorphGroups = new[]
        {
            ("body", "Körperbau"), ("shape", "Gesichtsform"), ("face", "Gesicht"), ("eyes", "Augen"), ("nose", "Nase"),
            ("mouth", "Mund"), ("ears", "Ohren"), ("brows", "Brauen"),
        };

        private static readonly Dictionary<string, string> MorphNames = new Dictionary<string, string>
        {
            ["weight"] = "Gewicht", ["muscle"] = "Muskeln", ["bust"] = "Brust", ["chest"] = "Brustmuskeln", ["shoulders"] = "Schultern",
            ["waist"] = "Taille", ["hips"] = "Hüften", ["buttocks"] = "Po", ["neck"] = "Hals",
            ["head-oval"] = "Oval", ["head-round"] = "Rund", ["head-square"] = "Eckig", ["head-rectangular"] = "Länglich",
            ["head-triangular"] = "Dreieckig", ["head-heart"] = "Herzförmig", ["head-diamond"] = "Raute",
            ["face-width"] = "Gesichtsbreite", ["face-length"] = "Gesichtslänge", ["face-fullness"] = "Fülle", ["jaw"] = "Kiefer",
            ["chin"] = "Kinn vorne", ["chin-height"] = "Kinnhöhe", ["cheekbones"] = "Wangenknochen", ["cheeks"] = "Wangen",
            ["forehead"] = "Stirn",
            ["eye-size"] = "Grösse", ["eye-distance"] = "Abstand", ["eye-height"] = "Höhe", ["eye-tilt"] = "Neigung",
            ["eye-open"] = "Öffnung", ["eye-lid"] = "Lidfalte", ["eye-depth"] = "Tiefe", ["eye-bags"] = "Tränensäcke",
            ["nose-width"] = "Breite", ["nose-length"] = "Länge", ["nose-size"] = "Grösse", ["nose-bridge"] = "Höcker",
            ["nose-tip"] = "Spitze", ["nostrils"] = "Nasenflügel", ["nose-depth"] = "Tiefe",
            ["mouth-width"] = "Breite", ["upper-lip"] = "Oberlippe", ["lower-lip"] = "Unterlippe", ["cupids-bow"] = "Amorbogen",
            ["mouth-corners"] = "Mundwinkel", ["mouth-height"] = "Höhe",
            ["ear-size"] = "Grösse", ["ear-angle"] = "Abstehend", ["ear-lobe"] = "Ohrläppchen",
            ["brow-height"] = "Höhe", ["brow-angle"] = "Winkel",
        };

        /// <summary>
        /// How strongly a slider at 1 moves (times MakeHuman's target): faces change in millimetres at 1, games show more.
        /// </summary>
        public static float MorphGain(string id)
        {
            var group = Array.Find(Morphs, m => m.Id == id).Group;
            return group switch
            {
                "eyes" or "nose" or "mouth" => 2.2f,
                "face" => 1.6f,
                "ears" or "brows" => 1.5f,
                "shape" => 1.3f,
                _ => 1f,
            };
        }

        public static string MorphName(string id) => id != null && MorphNames.TryGetValue(id, out var name) ? name : id;

        /// <summary>The shapes of a group this body has, in order.</summary>
        public static IReadOnlyList<(string Id, string Group, bool TwoSided, string[] Bodies)> MorphsOf(string body, string group) =>
            Morphs.Where(m => m.Group == group && Array.IndexOf(m.Bodies, body) >= 0).ToList();

        /// <summary>A shape's value in the look (0 when not set).</summary>
        public static float ShapeOf(AvatarLookDto look, string id) =>
            look.Shape != null && look.Shape.TryGetValue(id, out var value) ? value : 0f;

        /// <summary>The look with one shape changed (0 removes it: the look stays short).</summary>
        public static AvatarLookDto WithShape(AvatarLookDto look, string id, float value)
        {
            var morph = Array.Find(Morphs, m => m.Id == id);
            value = (float)Math.Round(Math.Max(morph.TwoSided ? -1f : 0f, Math.Min(1f, value)), 2);
            var shape = look.Shape != null ? new Dictionary<string, float>(look.Shape.ToDictionary(p => p.Key, p => p.Value)) : new Dictionary<string, float>();
            if (Math.Abs(value) < 0.005f)
            {
                shape.Remove(id);
            }
            else
            {
                shape[id] = value;
            }
            return look with { Shape = shape.Count == 0 ? null : shape };
        }

        /// <summary>
        /// Body mass index of the weight shape: 22 for the middle, 17 at the lightest, 35 at the heaviest (MakeHuman's range),
        /// muscles add a little.
        /// </summary>
        public static double BodyMassIndex(AvatarLookDto look)
        {
            var weight = ShapeOf(look, "weight");
            var muscle = ShapeOf(look, "muscle");
            return 22.0 + (weight < 0 ? weight * 5.0 : weight * 13.0) + muscle * 1.5;
        }

        /// <summary>Weight in kilograms (body mass index × height²).</summary>
        public static int WeightKg(AvatarLookDto look)
        {
            var metres = HeightCm(look) / 100.0;
            return (int)Math.Round(BodyMassIndex(look) * metres * metres);
        }
    }
}
