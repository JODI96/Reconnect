using System;
using System.Collections.Generic;
using System.Linq;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>What a colour zone of a piece of furniture is made of (which swatches it offers).</summary>
    public enum SwatchKind
    {
        /// <summary>Upholstery: velvet, bouclé, linen, leather in many colours.</summary>
        Fabric,
        Wood,
        Metal,
        Stone,
        /// <summary>Lacquered / painted surfaces (cabinets, panels).</summary>
        Paint,
    }

    /// <summary>One part of an item that can be coloured (e.g. the sofa's upholstery) and its default swatch.</summary>
    public sealed class ColourZone
    {
        public ColourZone(string label, SwatchKind kind, string defaultSwatch)
        {
            Label = label;
            Kind = kind;
            Default = defaultSwatch;
        }

        /// <summary>German name shown in the build editor ("Bezug", "Gestell", "Platte" …).</summary>
        public string Label { get; }
        public SwatchKind Kind { get; }
        public string Default { get; }
    }

    /// <summary>
    /// Colours of furniture, like in The Sims: an item has up to three colour zones, each takes a swatch of its kind.
    /// The layout stores them as <see cref="RoomItemDto.Colours"/> ("sage/walnut/brass", empty = defaults); the server
    /// accepts only swatches of the right kind. Swatch ids are shared with the client, which maps them to materials.
    /// </summary>
    public static class ItemColours
    {
        public const char Separator = '/';

        public static readonly IReadOnlyDictionary<SwatchKind, string[]> Swatches = new Dictionary<SwatchKind, string[]>
        {
            [SwatchKind.Fabric] = new[]
            {
                "sand", "cream", "oat", "stone", "charcoal", "black", "sage", "olive", "emerald", "petrol", "navy", "sky",
                "blush", "terracotta", "rust", "mustard", "plum", "burgundy", "cognac", "tan", "espresso", "white-leather",
            },
            [SwatchKind.Wood] = new[] { "oak", "light-oak", "walnut", "smoked-oak", "ebony", "cherry" },
            [SwatchKind.Metal] = new[] { "black", "brass", "chrome", "bronze", "white", "gunmetal" },
            [SwatchKind.Stone] = new[] { "white-marble", "black-marble", "green-marble", "travertine", "terrazzo", "concrete" },
            [SwatchKind.Paint] = new[] { "white", "warm-white", "greige", "sage", "navy", "black", "terracotta", "mustard", "blush" },
        };

        private static readonly List<(string Prefix, ColourZone[] Zones)> Families = new List<(string, ColourZone[])>();

        private static readonly ColourZone[] None = new ColourZone[0];

        /// <summary>Registers the colour zones of every item whose id starts with <paramref name="prefix"/> (longest prefix wins).</summary>
        public static void Register(string prefix, params ColourZone[] zones)
        {
            Families.RemoveAll(f => f.Prefix == prefix);
            Families.Add((prefix, zones));
            Families.Sort((a, b) => b.Prefix.Length.CompareTo(a.Prefix.Length));
        }

        public static ColourZone Zone(string label, SwatchKind kind, string defaultSwatch) => new ColourZone(label, kind, defaultSwatch);

        static ItemColours() => FurnitureFamilies.RegisterColours();

        /// <summary>The colour zones of an item (none for items that can't be coloured).</summary>
        public static IReadOnlyList<ColourZone> ZonesFor(string itemId)
        {
            if (itemId == null)
            {
                return None;
            }
            foreach (var (prefix, zones) in Families)
            {
                if (itemId.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return zones;
                }
            }
            return None;
        }

        /// <summary>The swatch of every zone: the stored one or the zone's default.</summary>
        public static string[] Resolve(string itemId, string colours)
        {
            var zones = ZonesFor(itemId);
            var chosen = Split(colours);
            var result = new string[zones.Count];
            for (var i = 0; i < zones.Count; i++)
            {
                result[i] = i < chosen.Length && !string.IsNullOrEmpty(chosen[i]) ? chosen[i] : zones[i].Default;
            }
            return result;
        }

        /// <summary>Stored form of a choice ("sage/walnut"); null when every zone keeps its default.</summary>
        public static string Join(string itemId, IReadOnlyList<string> swatches)
        {
            var zones = ZonesFor(itemId);
            if (zones.Count == 0 || swatches.Count == 0 || zones.Select((z, i) => i < swatches.Count ? swatches[i] : z.Default).SequenceEqual(zones.Select(z => z.Default)))
            {
                return null;
            }
            return string.Join(Separator.ToString(), zones.Select((z, i) => i < swatches.Count && !string.IsNullOrEmpty(swatches[i]) ? swatches[i] : z.Default));
        }

        /// <summary>Why a colour choice isn't allowed (null = fine).</summary>
        public static string Problem(string itemId, string colours)
        {
            if (string.IsNullOrEmpty(colours))
            {
                return null;
            }
            var zones = ZonesFor(itemId);
            var chosen = Split(colours);
            if (chosen.Length > zones.Count)
            {
                return "hat nicht so viele Farbzonen";
            }
            for (var i = 0; i < chosen.Length; i++)
            {
                if (!string.IsNullOrEmpty(chosen[i]) && !Swatches[zones[i].Kind].Contains(chosen[i]))
                {
                    return $"„{chosen[i]}“ gibt es für {zones[i].Label} nicht";
                }
            }
            return null;
        }

        private static string[] Split(string colours) => string.IsNullOrEmpty(colours) ? new string[0] : colours.Split(Separator);
    }
}
