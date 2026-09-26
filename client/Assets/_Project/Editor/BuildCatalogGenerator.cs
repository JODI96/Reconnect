using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Rooms;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reconnect.Client.Editor
{
    /// <summary>
    /// Measures every buildable model (Kenney, Poly Haven, custom items, game stations) and writes the build catalog
    /// – footprint in 50 cm cells, height, table surface, kind, category, German name – to
    /// src/Reconnect.Contracts/Rooms/ItemCatalogData.cs. Server and client share it (placement rules, walkable tiles).
    /// Run by ProjectSetup; after it, <c>dotnet build</c> brings the new catalog into the Contracts DLL.
    /// </summary>
    internal static class BuildCatalogGenerator
    {
        private static readonly string OutputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..",
            "src", "Reconnect.Contracts", "Rooms", "ItemCatalogData.cs"));

        /// <summary>Structural pieces of the Kenney kit (walls, floor tiles, stairs) and unusable models.</summary>
        private static readonly string[] Excluded =
        {
            "wall", "floor", "doorway", "stairs", "paneling", "ph-WoodenChair_01",
        };

        private static readonly HashSet<string> Decor = new(StringComparer.OrdinalIgnoreCase)
        {
            "laptop", "books", "computerScreen", "computerKeyboard", "computerMouse", "kitchenCoffeeMachine", "kitchenBlender",
            "kitchenMicrowave", "toaster", "lampRoundTable", "lampSquareTable", "plantSmall1", "plantSmall2", "plantSmall3", "radio",
            "televisionModern", "televisionVintage", "televisionAntenna", "speakerSmall", "pillow", "pillowBlue", "pillowBlueLong",
            "pillowLong", "ph-tea_set_01", "ph-ceramic_vase_01", "ph-ceramic_vase_03", "ph-antique_ceramic_vase_01", "ph-throw_pillows_01",
            "ph-marble_bust_01", "ph-desk_lamp_arm_01", "ph-chess_set", "ph-book_encyclopedia_set_01", "ph-mantel_clock_01",
            "ph-brass_candleholders", "ph-standing_picture_frame_01", "ph-bronze_ray_statue", "ph-potted_plant_04",
        };

        private static readonly Dictionary<string, float> Wall = new(StringComparer.OrdinalIgnoreCase)
        {
            ["lampWall"] = 1.8f, ["coatRack"] = 1.5f, ["bathroomMirror"] = 1.2f, ["hoodLarge"] = 1.6f, ["hoodModern"] = 1.6f,
            ["kitchenCabinetUpper"] = 1.55f, ["kitchenCabinetUpperCorner"] = 1.55f, ["kitchenCabinetUpperDouble"] = 1.55f,
            ["kitchenCabinetUpperLow"] = 1.55f,
            ["ph-hanging_picture_frame_01"] = 1.2f, ["ph-hanging_picture_frame_02"] = 1.3f, ["ph-hanging_picture_frame_03"] = 1.3f,
        };

        private static readonly HashSet<string> Ceiling = new(StringComparer.OrdinalIgnoreCase)
        {
            "lampSquareCeiling", "ceilingFan", "ph-Chandelier_01", "ph-modern_ceiling_lamp_01", "custom-pendant", "custom-lightstring",
            "custom-ledstrip",
        };

        private static readonly HashSet<string> Rugs = new(StringComparer.OrdinalIgnoreCase)
        {
            "custom-rug", "custom-ruground", "custom-queuelane", "custom-turnstiles",
        };

        /// <summary>Things can stand on these (their top is the surface).</summary>
        private static readonly string[] SurfaceWords =
        {
            "table", "desk", "kitchenBar", "kitchenCabinet", "kitchenSink", "sideTable", "cabinetTelevision", "bookcaseOpenLow",
            "bookcaseClosed", "cabinetBedDrawer", "bathroomCabinetDrawer", "washer", "Shelf_01", "display_shelves", "modern_wooden_cabinet",
            "CoffeeCart", "custom-skybar", "custom-backbar", "custom-reception", "custom-openkitchen", "outdoor_table_chair_set",
        };

        /// <summary>Items whose top is much wider than what stands on the floor (parasol: only the pole).</summary>
        private static readonly Dictionary<string, (int, int)> Footprints = new(StringComparer.OrdinalIgnoreCase)
        {
            ["custom-parasol"] = (1, 1),
        };

        /// <summary>Counters whose top is not the highest point (shelves, hoods above them).</summary>
        private static readonly Dictionary<string, float> SurfaceOverrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["custom-skybar"] = 1.11f, ["custom-backbar"] = 0.9f, ["custom-openkitchen"] = 0.93f, ["custom-reception"] = 1.15f,
        };

        private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
        {
            ["chair"] = "Holzstuhl", ["chairCushion"] = "Polsterstuhl", ["chairDesk"] = "Bürostuhl", ["chairRounded"] = "Café-Stuhl",
            ["chairModernCushion"] = "Designstuhl", ["chairModernFrameCushion"] = "Rahmenstuhl", ["stoolBar"] = "Barhocker",
            ["stoolBarSquare"] = "Barhocker eckig", ["loungeChair"] = "Loungesessel", ["loungeChairRelax"] = "Relaxsessel",
            ["loungeDesignChair"] = "Designsessel", ["loungeDesignSofa"] = "Designsofa", ["loungeSofa"] = "Sofa",
            ["loungeSofaLong"] = "Langes Sofa", ["loungeSofaCorner"] = "Ecksofa", ["loungeSofaOttoman"] = "Sofa mit Hocker",
            ["benchCushion"] = "Polsterbank", ["bench"] = "Holzbank", ["table"] = "Tisch", ["tableRound"] = "Runder Tisch",
            ["tableCoffee"] = "Couchtisch", ["tableCoffeeGlass"] = "Glas-Couchtisch", ["tableCross"] = "Arbeitstisch",
            ["desk"] = "Schreibtisch", ["deskCorner"] = "Eckschreibtisch", ["sideTable"] = "Beistelltisch",
            ["bookcaseOpen"] = "Offenes Regal", ["bookcaseOpenLow"] = "Niedriges Regal", ["bookcaseClosedWide"] = "Bücherschrank",
            ["kitchenBar"] = "Theke", ["kitchenCabinet"] = "Küchenschrank", ["kitchenCabinetUpper"] = "Hängeschrank",
            ["kitchenFridgeLarge"] = "Kühlschrank", ["kitchenCoffeeMachine"] = "Kaffeemaschine", ["pottedPlant"] = "Topfpflanze",
            ["lampRoundFloor"] = "Stehlampe", ["lampSquareFloor"] = "Stehlampe eckig", ["lampSquareTable"] = "Tischlampe",
            ["lampRoundTable"] = "Tischlampe rund", ["coatRackStanding"] = "Garderobe", ["speaker"] = "Lautsprecher",
            ["laptop"] = "Laptop", ["books"] = "Bücher", ["radio"] = "Radio", ["computerScreen"] = "Bildschirm",
            ["ph-sofa_02"] = "Chesterfield-Sofa", ["ph-sofa_03"] = "Ledersofa", ["ph-Sofa_01"] = "Klassisches Sofa",
            ["ph-ArmChair_01"] = "Ohrensessel", ["ph-modern_arm_chair_01"] = "Moderner Sessel", ["ph-GreenChair_01"] = "Samtstuhl",
            ["ph-mid_century_lounge_chair"] = "Lounge Chair", ["ph-Ottoman_01"] = "Hocker", ["ph-dining_chair_02"] = "Esszimmerstuhl",
            ["ph-bar_chair_round_01"] = "Barstuhl", ["ph-gallinera_chair"] = "Bistrostuhl", ["ph-gallinera_table"] = "Bistrotisch",
            ["ph-painted_wooden_bench"] = "Holzbank bemalt", ["ph-WoodenTable_01"] = "Langer Holztisch", ["ph-dining_table"] = "Esstisch",
            ["ph-CoffeeTable_01"] = "Couchtisch klassisch", ["ph-coffee_table_round_01"] = "Runder Couchtisch",
            ["ph-modern_coffee_table_01"] = "Moderner Couchtisch", ["ph-round_wooden_table_01"] = "Runder Holztisch",
            ["ph-side_table_01"] = "Beistelltisch rund", ["ph-side_table_tall_01"] = "Hoher Beistelltisch", ["ph-metal_office_desk"] = "Bürotisch",
            ["ph-wooden_bookshelf_worn"] = "Altes Bücherregal", ["ph-Shelf_01"] = "Metallregal", ["ph-wooden_display_shelves_01"] = "Vitrinenregal",
            ["ph-modern_wooden_cabinet"] = "Sideboard", ["ph-CoffeeCart_01"] = "Kaffeewagen", ["ph-standing_chalkboard_01"] = "Menütafel",
            ["ph-vintage_grandfather_clock_01"] = "Standuhr", ["ph-Chandelier_01"] = "Kronleuchter", ["ph-modern_ceiling_lamp_01"] = "Deckenleuchte",
            ["ph-street_lamp_02"] = "Laterne", ["ph-potted_plant_01"] = "Grosse Pflanze", ["ph-potted_plant_02"] = "Zimmerpflanze",
            ["ph-potted_plant_04"] = "Kleine Topfpflanze", ["ph-pachira_aquatica_01"] = "Glückskastanie", ["ph-planter_box_01"] = "Pflanzkasten",
            ["ph-planter_box_02"] = "Pflanzkasten gross", ["ph-outdoor_table_chair_set_01"] = "Gartentisch mit Stühlen",
            ["ph-chess_set"] = "Schachspiel", ["ph-book_encyclopedia_set_01"] = "Lexikon", ["ph-mantel_clock_01"] = "Kaminuhr",
            ["ph-brass_candleholders"] = "Kerzenständer", ["ph-ceramic_vase_01"] = "Vase", ["ph-ceramic_vase_03"] = "Vase hell",
            ["ph-antique_ceramic_vase_01"] = "Antike Vase", ["ph-tea_set_01"] = "Teeservice", ["ph-marble_bust_01"] = "Marmorbüste",
            ["ph-bronze_ray_statue"] = "Bronzeskulptur", ["ph-desk_lamp_arm_01"] = "Schreibtischlampe", ["ph-throw_pillows_01"] = "Kissen",
            ["ph-standing_picture_frame_01"] = "Bilderrahmen", ["ph-hanging_picture_frame_01"] = "Gemälde hoch",
            ["ph-hanging_picture_frame_02"] = "Gemälde quer", ["ph-hanging_picture_frame_03"] = "Kleines Bild",
            ["custom-skybar"] = "Bar", ["custom-backbar"] = "Rückbuffet", ["custom-reception"] = "Empfang", ["custom-elevator"] = "Lift",
            ["custom-turnstiles"] = "Drehkreuze", ["custom-queuelane"] = "Absperrung", ["custom-screenwall"] = "Videowand",
            ["custom-hologram"] = "Hologramm", ["custom-djbooth"] = "DJ-Pult", ["custom-telescope"] = "Fernrohr",
            ["custom-divider"] = "Raumteiler", ["custom-pendant"] = "Hängeleuchten", ["custom-ledstrip"] = "LED-Band",
            ["custom-lightstring"] = "Lichterkette", ["custom-parasol"] = "Sonnenschirm", ["custom-lounger"] = "Sonnenliege",
            ["custom-firepit"] = "Feuerstelle", ["custom-planter"] = "Pflanztrog", ["custom-column"] = "Säule",
            ["custom-easel"] = "Staffelei", ["custom-openkitchen"] = "Offene Küche", ["custom-rug"] = "Teppich",
            ["custom-ruground"] = "Runder Teppich", ["game-tictactoe"] = "Tic-Tac-Toe-Tisch", ["game-quiz"] = "Quiz-TV",
            ["custom-pool-6x3"] = "Pool klein", ["custom-pool-12x6"] = "Pool gross",
            ["bathroomCabinet"] = "Badschrank", ["bathroomCabinetDrawer"] = "Badkommode", ["bookcaseClosed"] = "Schrank",
            ["bookcaseClosedDoors"] = "Schrank mit Türen", ["cabinetBed"] = "Nachttisch", ["cabinetBedDrawer"] = "Nachttisch mit Schublade",
            ["cabinetBedDrawerTable"] = "Nachttisch klein", ["cabinetTelevision"] = "TV-Möbel", ["cabinetTelevisionDoors"] = "TV-Möbel mit Türen",
            ["bathtub"] = "Badewanne", ["bear"] = "Teddybär", ["bedBunk"] = "Kajütenbett", ["bedDouble"] = "Doppelbett",
            ["bedSingle"] = "Einzelbett", ["cardboardBoxClosed"] = "Karton", ["cardboardBoxOpen"] = "Karton offen",
            ["coatRack"] = "Wandgarderobe", ["computerKeyboard"] = "Tastatur", ["computerMouse"] = "Maus", ["dryer"] = "Trockner",
            ["pillow"] = "Kissen klein", ["pillowBlue"] = "Kissen blau", ["pillowBlueLong"] = "Nackenrolle blau", ["pillowLong"] = "Nackenrolle",
            ["shower"] = "Dusche", ["showerRound"] = "Runde Dusche", ["speakerSmall"] = "Kleiner Lautsprecher",
            ["televisionAntenna"] = "Zimmerantenne", ["televisionModern"] = "Fernseher", ["televisionVintage"] = "Röhrenfernseher",
            ["toilet"] = "WC", ["toiletSquare"] = "WC eckig", ["trashcan"] = "Abfalleimer", ["washer"] = "Waschmaschine",
            ["washerDryerStacked"] = "Waschturm", ["bathroomSink"] = "Lavabo", ["bathroomSinkSquare"] = "Lavabo eckig",
            ["kitchenBarEnd"] = "Theken-Ende", ["kitchenBlender"] = "Mixer", ["kitchenCabinetCornerInner"] = "Küchen-Eckschrank",
            ["kitchenCabinetCornerRound"] = "Küchen-Eckschrank rund", ["kitchenCabinetDrawer"] = "Küchenschubladen",
            ["kitchenFridge"] = "Kühlschrank mittel", ["kitchenFridgeBuiltIn"] = "Einbau-Kühlschrank", ["kitchenFridgeSmall"] = "Minibar",
            ["kitchenMicrowave"] = "Mikrowelle", ["kitchenStove"] = "Herd", ["kitchenStoveElectric"] = "Elektroherd",
            ["tableCoffeeGlassSquare"] = "Glastisch quadratisch", ["tableCoffeeSquare"] = "Couchtisch quadratisch",
            ["ceilingFan"] = "Deckenventilator", ["lampSquareCeiling"] = "Deckenlampe", ["lampWall"] = "Wandlampe",
            ["plantSmall1"] = "Kleine Pflanze", ["plantSmall2"] = "Kleiner Kaktus", ["plantSmall3"] = "Kleine Blume",
            ["benchCushionLow"] = "Niedrige Polsterbank", ["loungeDesignSofaCorner"] = "Design-Ecksofa", ["ph-metal_stool_02"] = "Metallhocker",
            ["sideTableDrawers"] = "Beistelltisch mit Schubladen", ["tableCloth"] = "Tisch mit Tischtuch", ["tableCrossCloth"] = "Arbeitstisch mit Tuch",
            ["tableGlass"] = "Glastisch", ["bathroomMirror"] = "Badspiegel", ["hoodLarge"] = "Dunstabzug", ["hoodModern"] = "Dunstabzug modern",
            ["kitchenCabinetUpperCorner"] = "Hängeschrank Ecke", ["kitchenCabinetUpperDouble"] = "Hängeschrank doppelt",
            ["kitchenCabinetUpperLow"] = "Hängeschrank flach",
        };

        public static void Generate(ItemCatalog catalog, CustomItems custom)
        {
            var root = new GameObject("Build Catalog Measure");
            var definitions = new List<ItemDefinition>();
            try
            {
                foreach (var entry in catalog.items)
                {
                    if (entry.model == null || Excluded.Any(e => entry.itemId.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                    var instance = (GameObject)Object.Instantiate(entry.model, root.transform);
                    instance.transform.localScale = Vector3.one * catalog.ScaleFor(entry.itemId);
                    definitions.Add(Define(entry.itemId, Bounds(instance)));
                    Object.DestroyImmediate(instance);
                }
                foreach (var id in CustomItems.Ids)
                {
                    var pivot = new GameObject(id).transform;
                    pivot.SetParent(root.transform, false);
                    if (custom.TryBuild(id, pivot, out _))
                    {
                        definitions.Add(Define(id, Bounds(pivot.gameObject)));
                    }
                    Object.DestroyImmediate(pivot.gameObject);
                }
                // Game stations stand on the Kenney models the room builds them from.
                definitions.Add(Define("game-tictactoe", Measure(catalog, root, "table")));
                definitions.Add(Define("game-quiz", Measure(catalog, root, "cabinetTelevision")));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            // Two ready-made pools for the catalog (any size works: custom-pool-<W>x<D>).
            definitions.Add(new ItemDefinition("custom-pool-6x3", Names["custom-pool-6x3"], "Spezial", ItemKind.Floor, 12, 6, 0.05f));
            definitions.Add(new ItemDefinition("custom-pool-12x6", Names["custom-pool-12x6"], "Spezial", ItemKind.Floor, 24, 12, 0.05f));
            Write(definitions.OrderBy(d => d.Category).ThenBy(d => d.Name).ToList());
            Debug.Log($"[Reconnect] Build catalog: {definitions.Count} items → {OutputPath}");
        }

        private static Bounds Measure(ItemCatalog catalog, GameObject root, string itemId)
        {
            var instance = (GameObject)Object.Instantiate(catalog.Find(itemId), root.transform);
            instance.transform.localScale = Vector3.one * catalog.ScaleFor(itemId);
            var bounds = Bounds(instance);
            Object.DestroyImmediate(instance);
            return bounds;
        }

        /// <summary>World bounds of all renderers of an instance (models are measured at the origin).</summary>
        public static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(go.transform.position, Vector3.one * 0.4f);
            }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private static ItemDefinition Define(string id, Bounds bounds)
        {
            var kind = KindOf(id);
            var height = bounds.size.y;
            // Up to 10 cm may overhang on each side (splayed legs, cushions), otherwise a 60 cm chair would take a whole metre.
            int Cells(float metres) => Math.Max(1, Mathf.CeilToInt((metres - 0.2f) / BuildGrid.CellSize));
            var width = Cells(bounds.size.x);
            var depth = kind == ItemKind.Wall ? 1 : Cells(bounds.size.z);
            if (Footprints.TryGetValue(id, out var footprint))
            {
                (width, depth) = footprint;
            }
            var surface = SurfaceOverrides.TryGetValue(id, out var counter) ? counter
                : kind == ItemKind.Floor && !RoomSeats.IsSeat(id) && !id.StartsWith("chair", StringComparison.OrdinalIgnoreCase)
                  && SurfaceWords.Any(w => id.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)
                    ? Mathf.Round(height * 100f) / 100f
                    : 0f;
            var mount = kind switch
            {
                ItemKind.Wall => Wall[id],
                ItemKind.Ceiling => Mathf.Max(1.9f, 2.5f - height),
                _ => 0f,
            };
            return new ItemDefinition(id, NameOf(id), CategoryOf(id, kind), kind, width, depth, Mathf.Round(height * 100f) / 100f, surface, mount);
        }

        private static ItemKind KindOf(string id)
        {
            if (Rugs.Contains(id) || id.StartsWith("rug", StringComparison.OrdinalIgnoreCase))
            {
                return ItemKind.Rug;
            }
            if (Decor.Contains(id))
            {
                return ItemKind.Decor;
            }
            if (Wall.ContainsKey(id))
            {
                return ItemKind.Wall;
            }
            return Ceiling.Contains(id) ? ItemKind.Ceiling : ItemKind.Floor;
        }

        private static string CategoryOf(string id, ItemKind kind)
        {
            bool Has(params string[] words) => words.Any(w => id.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
            if (kind == ItemKind.Rug)
            {
                return "Teppiche";
            }
            if (kind == ItemKind.Wall)
            {
                return "Wand";
            }
            if (id.StartsWith("custom-") || id.StartsWith("game-"))
            {
                return Has("lounger") ? "Sitzen" : Has("pendant", "lightstring", "ledstrip") ? "Licht" : Has("planter") ? "Pflanzen" : "Spezial";
            }
            if (RoomSeats.IsSeat(id) || Has("chair", "stool", "sofa", "bench", "ottoman", "lounge"))
            {
                return "Sitzen";
            }
            if (Has("lamp", "light", "chandelier", "lantern", "candle", "ceilingFan"))
            {
                return "Licht";
            }
            if (Has("plant", "pachira", "planter", "flower"))
            {
                return "Pflanzen";
            }
            if (Has("table", "desk") && !Has("CoffeeMachine"))
            {
                return "Tische";
            }
            if (Has("kitchen", "fridge", "toaster", "CoffeeMachine", "CoffeeCart", "hood", "stove", "sink", "microwave", "blender", "tea_set"))
            {
                return "Küche & Bar";
            }
            if (Has("bookcase", "shelf", "shelves", "cabinet", "drawer", "dresser", "nightstand", "wardrobe", "commode"))
            {
                return "Aufbewahrung";
            }
            return "Deko";
        }

        private static string NameOf(string id)
        {
            if (Names.TryGetValue(id, out var name))
            {
                return name;
            }
            var raw = id.StartsWith("ph-") ? id.Substring(3) : id;
            raw = Regex.Replace(raw, "_0?\\d+$", "");
            raw = Regex.Replace(raw.Replace('_', ' '), "(?<=[a-z])(?=[A-Z])", " ");
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.ToLowerInvariant());
        }

        private static void Write(List<ItemDefinition> definitions)
        {
            string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture) + "f";
            string S(string v) => "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            var code = new StringBuilder();
            code.AppendLine("// <auto-generated>");
            code.AppendLine("// Written by the Unity project setup (Reconnect → Setup Project, BuildCatalogGenerator) from the real 3D models.");
            code.AppendLine("// Do not edit by hand: change the rules in BuildCatalogGenerator and run the setup again.");
            code.AppendLine("// </auto-generated>");
            code.AppendLine("namespace Reconnect.Contracts.Rooms");
            code.AppendLine("{");
            code.AppendLine("    internal static class ItemCatalogData");
            code.AppendLine("    {");
            code.AppendLine("        public static readonly ItemDefinition[] Items =");
            code.AppendLine("        {");
            foreach (var d in definitions)
            {
                code.AppendLine($"            new ItemDefinition({S(d.Id)}, {S(d.Name)}, {S(d.Category)}, ItemKind.{d.Kind}, {d.Width}, {d.Depth}, " +
                                $"{F(d.Height)}, {F(d.SurfaceHeight)}, {F(d.MountHeight)}),");
            }
            code.AppendLine("        };");
            code.AppendLine("    }");
            code.AppendLine("}");
            File.WriteAllText(OutputPath, code.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
        }
    }
}
