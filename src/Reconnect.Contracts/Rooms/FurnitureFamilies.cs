using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Reconnect.Contracts.Rooms
{
    /// <summary>What the catalog needs to know about one of our own furniture items.</summary>
    public sealed class FurnitureInfo
    {
        public FurnitureInfo(string id, string name, string category, ItemKind kind, int seats = 0, bool surface = false)
        {
            Id = id;
            Name = name;
            Category = category;
            Kind = kind;
            Seats = seats;
            Surface = surface;
        }

        public string Id { get; }

        /// <summary>German name in the catalog.</summary>
        public string Name { get; }
        public string Category { get; }
        public ItemKind Kind { get; }

        /// <summary>Places to sit (0 = not a seat).</summary>
        public int Seats { get; }

        /// <summary>Small things can stand on it (table, sideboard, shelf).</summary>
        public bool Surface { get; }
    }

    /// <summary>
    /// "Reconnect Modern": our own furniture, built by the client (CustomItems) in families – every style in several
    /// sizes, every piece in the colours of its zones (<see cref="ItemColours"/>). Ids start with "custom-". This list is
    /// the single source for catalog, seats and colour zones on server and client.
    /// </summary>
    public static class FurnitureFamilies
    {
        public const string Seating = "Sofas & Sessel";
        public const string Chairs = "Stühle & Hocker";
        public const string Tables = "Tische";
        public const string Storage = "Aufbewahrung";
        public const string Lights = "Licht";
        public const string Plants = "Pflanzen";
        public const string Floors = "Teppiche & Böden";
        public const string Work = "Arbeiten";
        public const string Decor = "Deko";

        public static readonly string[] SofaStyles = { "box", "round", "tufted", "low", "slim" };
        public static readonly string[] ChairStyles = { "classic", "shell", "wood", "cantilever", "velvet", "ladder" };
        public static readonly string[] BarStoolStyles = { "round", "back", "saddle" };
        public static readonly int[] TableLengths = { 120, 160, 200, 240, 300, 360 };
        public static readonly int[] RoundTableSizes = { 80, 100, 120, 140 };
        public static readonly string[] CoffeeTableStyles = { "rect", "round", "oval", "nesting", "cube" };
        public static readonly string[] SideTableStyles = { "round", "cube", "drum" };
        public static readonly string[] PendantStyles = { "dome", "globe", "linear", "cluster" };
        public static readonly string[] FloorLampStyles = { "arc", "tripod", "globe", "column" };
        public static readonly string[] TableLampStyles = { "dome", "mushroom", "globe" };
        public static readonly string[] PlantTypes = { "tall", "bush", "leafy" };
        public static readonly string[] PotStyles = { "cylinder", "bowl", "tapered" };
        public static readonly string[] VaseStyles = { "bottle", "round", "tall", "bowl" };

        /// <summary>Floor zones the catalog offers (any size works: custom-zone-&lt;material&gt;-&lt;W&gt;x&lt;D&gt;).</summary>
        public static readonly string[] ZoneSizes = { "3x2", "4x3", "6x4", "8x6" };

        private static readonly Dictionary<string, string> StyleNames = new Dictionary<string, string>
        {
            ["box"] = "kantig", ["round"] = "rund", ["tufted"] = "gesteppt", ["low"] = "Lounge", ["slim"] = "schlank",
            ["classic"] = "gepolstert", ["shell"] = "Schale", ["wood"] = "Holz", ["cantilever"] = "Freischwinger",
            ["velvet"] = "Samt", ["ladder"] = "Sprossen", ["back"] = "mit Lehne", ["saddle"] = "Sattel",
            ["rect"] = "eckig", ["oval"] = "oval", ["nesting"] = "Satz", ["cube"] = "Würfel", ["drum"] = "Trommel",
            ["dome"] = "Glocke", ["globe"] = "Kugel", ["linear"] = "Balken", ["cluster"] = "Traube", ["arc"] = "Bogen",
            ["tripod"] = "Dreibein", ["column"] = "Säule", ["mushroom"] = "Pilz",
            ["tall"] = "Baum", ["bush"] = "Busch", ["leafy"] = "Grossblatt", ["cylinder"] = "Zylinder", ["bowl"] = "Schale",
            ["tapered"] = "konisch", ["bottle"] = "Flasche",
        };

        private static List<FurnitureInfo> _all;
        private static Dictionary<string, FurnitureInfo> _byId;

        public static IReadOnlyList<FurnitureInfo> All => _all ?? (_all = Build());

        public static IEnumerable<string> Ids => All.Select(f => f.Id);

        public static FurnitureInfo Find(string itemId)
        {
            if (itemId == null)
            {
                return null;
            }
            _byId = _byId ?? All.ToDictionary(f => f.Id, StringComparer.Ordinal);
            if (_byId.TryGetValue(itemId, out var info))
            {
                return info;
            }
            return TryZone(itemId, out var material, out _, out _) ? new FurnitureInfo(itemId, ZoneName(material), Floors, ItemKind.Rug) : null;
        }

        public static int Seats(string itemId) => Find(itemId)?.Seats ?? 0;

        /// <summary>custom-zone-&lt;material&gt;-&lt;W&gt;x&lt;D&gt; → material ("carpet", "wood", "stone") and size in metres.</summary>
        public static bool TryZone(string itemId, out string material, out float width, out float depth)
        {
            material = null;
            width = depth = 0f;
            if (itemId == null || !itemId.StartsWith("custom-zone-", StringComparison.Ordinal))
            {
                return false;
            }
            var rest = itemId.Substring("custom-zone-".Length);
            var dash = rest.LastIndexOf('-');
            if (dash <= 0)
            {
                return false;
            }
            var parts = rest.Substring(dash + 1).Split('x');
            if (parts.Length != 2 || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out width)
                                  || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out depth))
            {
                return false;
            }
            material = rest.Substring(0, dash);
            width = Math.Max(1f, Math.Min(30f, width));
            depth = Math.Max(1f, Math.Min(30f, depth));
            return material == "carpet" || material == "wood" || material == "stone";
        }

        private static string ZoneName(string material) => material switch
        {
            "wood" => "Holzboden-Insel",
            "stone" => "Steinboden-Insel",
            _ => "Teppichfläche",
        };

        private static string Style(string style) => StyleNames.TryGetValue(style, out var name) ? name : style;

        private static List<FurnitureInfo> Build()
        {
            var list = new List<FurnitureInfo>();
            void Add(string id, string name, string category, ItemKind kind = ItemKind.Floor, int seats = 0, bool surface = false) =>
                list.Add(new FurnitureInfo(id, name, category, kind, seats, surface));

            // Sofas & armchairs.
            foreach (var style in SofaStyles)
            {
                Add($"custom-sofa-{style}-2", $"Sofa {Style(style)} 2er", Seating, seats: 2);
                Add($"custom-sofa-{style}-3", $"Sofa {Style(style)} 3er", Seating, seats: 3);
                Add($"custom-sofa-{style}-corner", $"Ecksofa {Style(style)}", Seating, seats: 4);
                Add($"custom-armchair-{style}", $"Sessel {Style(style)}", Seating, seats: 1);
                Add($"custom-ottoman-{style}", $"Fusshocker {Style(style)}", Seating, seats: 1);
            }
            Add("custom-lounge-shell", "Lounge Chair Schale", Seating, seats: 1);
            Add("custom-lounge-sling", "Lounge Chair Leder", Seating, seats: 1);
            foreach (var length in new[] { 160, 240, 320 })
            {
                Add($"custom-banquette-{length}", $"Sitzbank gepolstert {length} cm", Seating, seats: length / 80);
            }
            Add("custom-banquette-corner", "Eckbank gepolstert", Seating, seats: 4);

            // Chairs, stools, benches.
            foreach (var style in ChairStyles)
            {
                Add($"custom-chair-{style}", $"Stuhl {Style(style)}", Chairs, seats: 1);
            }
            foreach (var style in BarStoolStyles)
            {
                Add($"custom-barstool-{style}", $"Barhocker {Style(style)}", Chairs, seats: 1);
            }
            Add("custom-pouf-round", "Pouf rund", Chairs, seats: 1);
            Add("custom-pouf-cube", "Pouf eckig", Chairs, seats: 1);
            foreach (var length in new[] { 120, 180 })
            {
                Add($"custom-bench-wood-{length}", $"Holzbank {length} cm", Chairs, seats: length / 60);
                Add($"custom-bench-soft-{length}", $"Polsterbank {length} cm", Chairs, seats: length / 60);
            }

            // Tables.
            foreach (var length in TableLengths)
            {
                Add($"custom-table-sled-{length}", $"Tisch Kufengestell {length} cm", Tables, surface: true);
                Add($"custom-table-legs-{length}", $"Tisch Holzbeine {length} cm", Tables, surface: true);
            }
            foreach (var length in new[] { 160, 200, 240, 300 })
            {
                Add($"custom-table-stone-{length}", $"Steintisch {length} cm", Tables, surface: true);
            }
            foreach (var size in RoundTableSizes)
            {
                Add($"custom-roundtable-wood-{size}", $"Runder Holztisch Ø {size} cm", Tables, surface: true);
                Add($"custom-roundtable-stone-{size}", $"Runder Steintisch Ø {size} cm", Tables, surface: true);
            }
            Add("custom-bistro", "Bistrotisch", Tables, surface: true);
            Add("custom-bistro-square", "Bistrotisch eckig", Tables, surface: true);
            Add("custom-hightable", "Stehtisch", Tables, surface: true);
            Add("custom-hightable-long", "Stehtisch lang", Tables, surface: true);
            Add("custom-dinner-2", "Gedeckter Tisch für 2", Tables, surface: true);
            Add("custom-dinner-4", "Gedeckter Tisch für 4", Tables, surface: true);
            Add("custom-dinner-6", "Gedeckter Tisch für 6", Tables, surface: true);
            Add("custom-dinner-round-120", "Gedeckter runder Tisch Ø 120", Tables, surface: true);
            Add("custom-dinner-round-160", "Gedeckter runder Tisch Ø 160", Tables, surface: true);
            foreach (var style in CoffeeTableStyles)
            {
                Add($"custom-coffeetable-{style}", $"Couchtisch {Style(style)}", Tables, surface: true);
            }
            foreach (var style in SideTableStyles)
            {
                Add($"custom-sidetable-{style}", $"Beistelltisch {Style(style)}", Tables, surface: true);
            }
            Add("custom-console", "Konsolentisch", Tables, surface: true);
            foreach (var length in new[] { 120, 140, 160 })
            {
                Add($"custom-desk-{length}", $"Schreibtisch {length} cm", Work, surface: true);
            }

            // Storage.
            foreach (var length in new[] { 120, 180, 240 })
            {
                Add($"custom-sideboard-{length}", $"Sideboard {length} cm", Storage, surface: true);
            }
            Add("custom-shelf-wide", "Bücherregal breit", Storage);
            Add("custom-shelf-tall", "Bücherregal hoch", Storage);
            Add("custom-shelf-grid", "Regalwand", Storage);
            Add("custom-cabinet-tall", "Hochschrank", Storage);
            Add("custom-vitrine", "Vitrine", Storage);
            Add("custom-barcabinet", "Barschrank", Storage, surface: true);

            // Lights.
            foreach (var style in FloorLampStyles)
            {
                Add($"custom-floorlamp-{style}", $"Stehleuchte {Style(style)}", Lights);
            }
            foreach (var style in TableLampStyles)
            {
                Add($"custom-tablelamp-{style}", $"Tischleuchte {Style(style)}", Lights, ItemKind.Decor);
            }
            foreach (var style in PendantStyles)
            {
                Add($"custom-pendantlamp-{style}", $"Pendelleuchte {Style(style)}", Lights, ItemKind.Ceiling);
            }

            // Plants.
            foreach (var plant in PlantTypes)
            {
                foreach (var pot in PotStyles)
                {
                    Add($"custom-plant-{plant}-{pot}", $"Pflanze {Style(plant)} · Topf {Style(pot)}", Plants);
                }
            }
            Add("custom-planterbench", "Pflanzbank", Plants);

            // Work.
            Add("custom-acoustic", "Akustikwand", Work);
            Add("custom-acoustic-curved", "Akustikwand geschwungen", Work);
            Add("custom-whiteboard", "Whiteboard", Work);
            Add("custom-screen", "Präsentations-Bildschirm", Work);

            // Decor on tables.
            foreach (var style in VaseStyles)
            {
                Add($"custom-vase-{style}", $"Vase {Style(style)}", Decor, ItemKind.Decor);
            }
            Add("custom-candles", "Kerzen", Decor, ItemKind.Decor);
            Add("custom-books", "Bücherstapel", Decor, ItemKind.Decor);
            Add("custom-tray", "Tablett mit Gläsern", Decor, ItemKind.Decor);

            // Floors.
            foreach (var size in ZoneSizes)
            {
                Add($"custom-zone-carpet-{size}", $"Teppichfläche {size.Replace("x", " × ")} m", Floors, ItemKind.Rug);
                Add($"custom-zone-wood-{size}", $"Holzboden-Insel {size.Replace("x", " × ")} m", Floors, ItemKind.Rug);
                Add($"custom-zone-stone-{size}", $"Steinboden-Insel {size.Replace("x", " × ")} m", Floors, ItemKind.Rug);
            }
            Add("custom-rugmodern-round-200", "Teppich rund Ø 200", Floors, ItemKind.Rug);
            Add("custom-rugmodern-round-300", "Teppich rund Ø 300", Floors, ItemKind.Rug);
            Add("custom-rugmodern-250x170", "Teppich 250 × 170", Floors, ItemKind.Rug);
            Add("custom-rugmodern-300x200", "Teppich 300 × 200", Floors, ItemKind.Rug);
            return list;
        }

        /// <summary>Colour zones of every family (called by <see cref="ItemColours"/>).</summary>
        internal static void RegisterColours()
        {
            var Fabric = SwatchKind.Fabric;
            var Wood = SwatchKind.Wood;
            var Metal = SwatchKind.Metal;
            var Stone = SwatchKind.Stone;
            var Paint = SwatchKind.Paint;
            ColourZone Z(string label, SwatchKind kind, string swatch) => ItemColours.Zone(label, kind, swatch);

            var sofaDefaults = new Dictionary<string, string> { ["box"] = "sand", ["round"] = "sage", ["tufted"] = "emerald", ["low"] = "oat", ["slim"] = "cognac" };
            foreach (var style in SofaStyles)
            {
                var legs = style == "round" || style == "tufted" ? Z("Füsse", Wood, "walnut") : Z("Füsse", Metal, "black");
                foreach (var prefix in new[] { $"custom-sofa-{style}-", $"custom-armchair-{style}", $"custom-ottoman-{style}" })
                {
                    ItemColours.Register(prefix, Z("Bezug", Fabric, sofaDefaults[style]), legs);
                }
            }
            ItemColours.Register("custom-lounge-shell", Z("Polster", Fabric, "cognac"), Z("Schale", Wood, "walnut"), Z("Fuss", Metal, "black"));
            ItemColours.Register("custom-lounge-sling", Z("Leder", Fabric, "tan"), Z("Gestell", Metal, "chrome"));
            ItemColours.Register("custom-banquette-", Z("Bezug", Fabric, "emerald"), Z("Rückwand", Wood, "walnut"));

            ItemColours.Register("custom-chair-classic", Z("Bezug", Fabric, "cognac"), Z("Gestell", Wood, "oak"));
            ItemColours.Register("custom-chair-shell", Z("Schale", Paint, "warm-white"), Z("Beine", Wood, "oak"));
            ItemColours.Register("custom-chair-wood", Z("Holz", Wood, "oak"), Z("Sitz", Fabric, "oat"));
            ItemColours.Register("custom-chair-cantilever", Z("Leder", Fabric, "espresso"), Z("Gestell", Metal, "chrome"));
            ItemColours.Register("custom-chair-velvet", Z("Bezug", Fabric, "blush"), Z("Beine", Metal, "brass"));
            ItemColours.Register("custom-chair-ladder", Z("Holz", Wood, "walnut"), Z("Sitz", Fabric, "charcoal"));
            ItemColours.Register("custom-barstool-", Z("Sitz", Fabric, "cognac"), Z("Gestell", Metal, "black"));
            ItemColours.Register("custom-pouf-", Z("Bezug", Fabric, "sand"));
            ItemColours.Register("custom-bench-wood-", Z("Holz", Wood, "oak"));
            ItemColours.Register("custom-bench-soft-", Z("Bezug", Fabric, "charcoal"), Z("Gestell", Metal, "black"));

            ItemColours.Register("custom-table-sled-", Z("Platte", Wood, "oak"), Z("Gestell", Metal, "black"));
            ItemColours.Register("custom-table-legs-", Z("Holz", Wood, "walnut"));
            ItemColours.Register("custom-table-stone-", Z("Platte", Stone, "white-marble"), Z("Gestell", Metal, "black"));
            ItemColours.Register("custom-roundtable-wood-", Z("Platte", Wood, "oak"), Z("Fuss", Metal, "black"));
            ItemColours.Register("custom-roundtable-stone-", Z("Platte", Stone, "white-marble"), Z("Fuss", Metal, "black"));
            ItemColours.Register("custom-bistro", Z("Platte", Stone, "white-marble"), Z("Fuss", Metal, "black"));
            ItemColours.Register("custom-hightable", Z("Platte", Stone, "white-marble"), Z("Fuss", Metal, "black"));
            ItemColours.Register("custom-hightable-long", Z("Platte", Wood, "oak"), Z("Gestell", Metal, "black"));
            ItemColours.Register("custom-dinner-", Z("Tischtuch", Fabric, "cream"));
            ItemColours.Register("custom-coffeetable-", Z("Platte", Wood, "walnut"), Z("Gestell", Metal, "black"));
            ItemColours.Register("custom-coffeetable-round", Z("Platte", Stone, "white-marble"), Z("Gestell", Metal, "black"));
            ItemColours.Register("custom-coffeetable-cube", Z("Stein", Stone, "travertine"));
            ItemColours.Register("custom-sidetable-", Z("Platte", Wood, "oak"), Z("Gestell", Metal, "black"));
            ItemColours.Register("custom-sidetable-drum", Z("Stein", Stone, "travertine"));
            ItemColours.Register("custom-console", Z("Holz", Wood, "walnut"), Z("Gestell", Metal, "brass"));
            ItemColours.Register("custom-desk-", Z("Platte", Wood, "light-oak"), Z("Gestell", Metal, "white"));

            ItemColours.Register("custom-sideboard-", Z("Korpus", Wood, "walnut"), Z("Füsse", Metal, "black"));
            ItemColours.Register("custom-shelf-", Z("Holz", Wood, "walnut"));
            ItemColours.Register("custom-cabinet-tall", Z("Front", Paint, "greige"), Z("Griffe", Metal, "brass"));
            ItemColours.Register("custom-vitrine", Z("Rahmen", Metal, "black"));
            ItemColours.Register("custom-barcabinet", Z("Korpus", Wood, "walnut"), Z("Beschläge", Metal, "brass"));

            ItemColours.Register("custom-floorlamp-", Z("Metall", Metal, "brass"), Z("Fuss", Stone, "white-marble"));
            ItemColours.Register("custom-tablelamp-", Z("Schirm", Paint, "warm-white"), Z("Fuss", Metal, "brass"));
            ItemColours.Register("custom-pendantlamp-", Z("Schirm", Metal, "black"));
            ItemColours.Register("custom-plant-", Z("Topf", Stone, "concrete"));
            ItemColours.Register("custom-planterbench", Z("Holz", Wood, "oak"));

            ItemColours.Register("custom-acoustic", Z("Filz", Fabric, "sage"));
            ItemColours.Register("custom-whiteboard", Z("Rahmen", Metal, "chrome"));
            ItemColours.Register("custom-screen", Z("Ständer", Metal, "black"));
            ItemColours.Register("custom-vase-", Z("Keramik", Paint, "terracotta"));
            ItemColours.Register("custom-candles", Z("Halter", Metal, "brass"));
            ItemColours.Register("custom-tray", Z("Tablett", Metal, "brass"));
            ItemColours.Register("custom-books", Z("Einbände", Paint, "sage"));

            ItemColours.Register("custom-zone-carpet-", Z("Teppich", Fabric, "oat"));
            ItemColours.Register("custom-zone-wood-", Z("Holz", Wood, "oak"));
            ItemColours.Register("custom-zone-stone-", Z("Stein", Stone, "white-marble"));
            ItemColours.Register("custom-rugmodern-", Z("Teppich", Fabric, "stone"), Z("Rand", Fabric, "charcoal"));
        }
    }
}
