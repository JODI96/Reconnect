using Reconnect.Modules.Rooms.Domain;

namespace Reconnect.Modules.Rooms.Infrastructure.Seeding;

/// <summary>
/// The storeys of the Prime Tower, designed on the real floor plan (~63 × 35 m, core in the middle at x 24–36,
/// z 14.75–20.75 with lift doors north and south). Room coordinates: x along the building, z across; the camera looks
/// from the south-west. The space in front of the lift doors stays free (build rules).
/// </summary>
internal static class TowerFloorDesigns
{
    /// <summary>
    /// Ground floor "Ankunft": warm and generous – sand bouclé, cognac leather, walnut, travertine and brass, lots of green.
    /// Lounge islands look out through the south-west glass, a library wall with reading nooks closes the west end, a living
    /// room and a long communal table fill the middle, banquettes run along the north glass, the lobby café with its bar is
    /// east of the lifts, and the east tip is the games corner. Gates and the queue stay in front of the south lifts.
    /// </summary>
    public static List<RoomItem> Lobby(TowerFloorPlan plan)
    {
        var d = new StoreyDesigner(plan);
        var lounge = new LoungeStyle("custom-sofa-low-3", "sand/black", "custom-armchair-round", "cognac/walnut",
            "custom-zone-carpet-4x3", "oat", "custom-coffeetable-round", "travertine/brass",
            "custom-floorlamp-arc", "brass/white-marble", "custom-plant-tall-tapered", "travertine");

        // Arrival: gates and queue in front of the south lifts, the reception desk west of them, the green wall on the core.
        d.Put("custom-turnstiles", 30.25f, 11.25f)
         .Put("custom-queuelane", 30.25f, 8.75f)
         .Put("custom-reception", 18.5f, 11.5f)
         .Put("custom-plant-leafy-cylinder", 15.6f, 11.5f, 0f, "travertine")
         .Put("custom-plant-leafy-cylinder", 21.4f, 11.5f, 0f, "travertine")
         .Put("custom-greenwall", 23.5f, 18f, 90f);

        // South-west glass: three lounge islands with the view.
        foreach (var t in new[] { 0.2f, 0.5f, 0.8f })
        {
            d.AlongFacade(1, t, 2.6f, g => g.Lounge(0f, 0f, lounge));
        }

        // West end: library wall and reading nooks facing the room.
        foreach (var t in new[] { 0.34f, 0.47f, 0.6f })
        {
            d.AlongFacade(13, t, 0.5f, g => g.Put("custom-shelf-grid", 0f, 0f, 180f, "walnut"));
        }
        foreach (var t in new[] { 0.3f, 0.52f, 0.74f })
        {
            d.AlongFacade(13, t, 3.1f, g => g.Reading(0f, 0f, "cognac/walnut/black"), 180f);
        }

        // Living room: corner sofa around a round rug, tufted armchairs, and the long communal table behind it.
        d.Group(12.5f, 18.5f, 0f, g =>
        {
            g.Put("custom-rugmodern-round-300", 0f, -0.2f, 0f, "stone/charcoal")
             .Put("custom-coffeetable-oval", 0f, -0.3f, 0f, "walnut/black")
             .Put("custom-sofa-low-corner", 0.2f, 1.2f, 0f, "oat/black")
             .Put("custom-armchair-tufted", 2.3f, -0.5f, 90f, "emerald/walnut")
             .Put("custom-ottoman-round", -0.2f, -1.7f, 0f, "cognac/walnut")
             .Put("custom-floorlamp-globe", 2.4f, 1.3f, 0f, "brass/white-marble")
             .Decor("custom-vase-bowl", 0f, -0.3f, 0f, "black");
        });
        d.Dining(14f, 23f, "custom-table-legs-300", "walnut", "custom-chair-classic", "cognac/walnut", 4, pendantColours: "brass");
        d.Decor("custom-candles", 13f, 23f).Decor("custom-vase-tall", 15f, 23f, 0f, "sage");

        // North glass: banquettes with bistro tables, bistro sets in front of the north lifts.
        foreach (var t in new[] { 0.28f, 0.72f })
        {
            d.AlongFacade(10, t, 0.9f, g => g.BanquetteRow(0f, 0f, 240, "sand/walnut", "custom-chair-velvet", "sage/brass", "white-marble/brass"), 180f);
        }
        foreach (var x in new[] { 25.5f, 29f, 32.5f, 36f })
        {
            d.Bistro(x, 25.2f, "custom-chair-classic", "cognac/walnut", "custom-bistro", "white-marble/brass")
             .Put("custom-pendantlamp-globe", x, 25.2f, 0f, "brass");
        }

        // Lobby café east of the lifts: bar with stools, bistro tables, standing tables.
        d.Put("custom-backbar", 46f, 25.75f)
         .Put("custom-skybar", 46f, 23.75f)
         .Decor("kitchenCoffeeMachine", 44.5f, 23.75f)
         .Decor("custom-tray", 47.5f, 23.75f, 0f, "brass")
         .Row("custom-barstool-round", 44f, 22.3f, 5, 1f, 180f, "cognac/brass");
        foreach (var x in new[] { 43.5f, 46f, 48.5f })
        {
            d.Put("custom-pendantlamp-dome", x, 23.75f, 0f, "brass");
        }
        d.Put("custom-zone-wood-13x9", 46.5f, 16.25f, 0f, "walnut");   // the café's own floor
        foreach (var x in new[] { 42f, 45.5f, 49f })
        {
            foreach (var z in new[] { 18f, 14.5f })
            {
                d.Bistro(x, z, "custom-chair-cantilever", "cognac/chrome", "custom-bistro", "white-marble/black");
            }
        }
        d.HighTable(40.5f, 21.5f, "black-marble/brass", "custom-barstool-back", "cognac/black")
         .HighTable(51.5f, 21.5f, "black-marble/brass", "custom-barstool-back", "cognac/black");

        // South glass east of the gates: a lounge with the view and waiting benches.
        d.AlongFacade(4, 0.45f, 2.4f, g => g.Lounge(0f, 0f, lounge with { Sofa = "custom-sofa-round-3", SofaColours = "sage/walnut" }));
        d.AlongFacade(3, 0.82f, 0.9f, g => g.Put("custom-banquette-240", 0f, 0f, 0f, "cognac/walnut"));

        // East tip: games corner with a view lounge.
        d.Put("game-quiz", 56.5f, 21.5f, 90f)
         .Put("custom-pouf-round", 54.8f, 21f, 0f, "terracotta").Put("custom-pouf-round", 54.8f, 22.2f, 0f, "mustard")
         .Put("game-connectfour", 55.5f, 16f)
         .Put("custom-barstool-round", 55.5f, 14.8f, 0f, "cognac/brass");
        d.AlongFacade(6, 0.62f, 1.6f, g =>
        {
            g.Put("custom-armchair-slim", -0.9f, 0.3f, 0f, "cognac/black").Put("custom-armchair-slim", 0.9f, 0.3f, 0f, "cognac/black")
             .Put("custom-sidetable-drum", 0f, 0.3f);
        });

        // Arrival lounge between reception and gates: round rug, poufs and a pair of lounge chairs around a drum table.
        d.Group(23f, 7.5f, 0f, g =>
        {
            g.Put("custom-rugmodern-round-300", 0f, 0f, 0f, "sand/cognac")
             .Put("custom-coffeetable-cube", 0f, 0f, 0f, "travertine")
             .Put("custom-lounge-shell", -1.1f, 0.6f, 225f, "cognac/walnut/black")
             .Put("custom-lounge-shell", 1.1f, 0.6f, 135f, "cognac/walnut/black")
             .Put("custom-pouf-round", -0.9f, -1.0f, 0f, "sage")
             .Put("custom-pouf-cube", 0.9f, -1.0f, 0f, "oat");
        });

        // Sculpture island in front of the green wall: planters, a bust on a drum, two reading chairs.
        d.Group(19.5f, 17.5f, 0f, g =>
        {
            g.Put("custom-sidetable-drum", 0f, 0f, 0f, "white-marble").DecorOnLast("ph-marble_bust_01")
             .Put("custom-plant-tall-cylinder", 0f, 2.2f, 0f, "travertine")
             .Put("custom-plant-tall-cylinder", 0f, -2.2f, 0f, "travertine")
             .Put("custom-armchair-slim", 1.4f, 1.0f, 90f, "tan/brass")
             .Put("custom-armchair-slim", 1.4f, -1.0f, 90f, "tan/brass");
        });

        // North-west glass: a banquette row and a lounge facing the room.
        d.AlongFacade(11, 0.3f, 1.0f, g => g.BanquetteRow(0f, 0f, 320, "sage/walnut", "custom-chair-classic", "cognac/walnut", "white-marble/brass"), 180f);
        d.AlongFacade(11, 0.72f, 3.7f, g => g.Lounge(0f, 0f, lounge with { SofaColours = "cognac/black", ChairColours = "sand/walnut" }), 180f);

        // North-east glass: two lounges looking into the room (towards the bar), a reading nook at the corner.
        foreach (var t in new[] { 0.3f, 0.7f })
        {
            d.AlongFacade(8, t, 3.7f, g => g.Lounge(0f, 0f, lounge with { Sofa = "custom-sofa-tufted-3", SofaColours = "emerald/walnut", ChairColours = "cognac/walnut" }, extras: false), 180f);
        }
        d.AlongFacade(8, 0.08f, 3.0f, g => g.Reading(0f, 0f, "tan/walnut/black"), 180f);

        // North of the communal table: a lounge corner facing the room.
        d.Lounge(20.5f, 26f, lounge with { Sofa = "custom-sofa-round-3", SofaColours = "sand/walnut", ChairColours = "sage/walnut" }, extras: false);

        // Between café and the tip: bistro tables by the south-east glass.
        d.Bistro(52.5f, 11f, "custom-chair-cantilever", "cognac/chrome", "custom-bistro", "white-marble/black")
         .Bistro(52.5f, 8.2f, "custom-chair-cantilever", "cognac/chrome", "custom-bistro", "white-marble/black");

        FacadeGreenery(d, 4.5f);
        return d.Items;
    }

    /// <summary>
    /// Plants along the glass all round the storey where there is room (every <paramref name="spacing"/> metres), tall and
    /// bushy in turn, in travertine and concrete pots.
    /// </summary>
    private static void FacadeGreenery(StoreyDesigner d, float spacing)
    {
        var plants = new[] { ("custom-plant-tall-tapered", "travertine"), ("custom-plant-bush-bowl", "concrete"), ("custom-plant-leafy-cylinder", "travertine") };
        var outline = d.Plan.Outline;
        var next = 0;
        for (var edge = 0; edge < outline.Count; edge++)
        {
            var a = outline[edge];
            var b = outline[(edge + 1) % outline.Count];
            var length = (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
            var count = (int)(length / spacing);
            for (var k = 0; k < count; k++)
            {
                var t = (k + 0.5f) / count;
                var (id, colours) = plants[next % plants.Length];
                var (x, z, angle) = d.OnFacade(edge, t, 0.75f);
                if (d.TryPut(id, x, z, angle, colours))
                {
                    next++;
                }
            }
        }
    }

    /// <summary>
    /// Coworking "Werkstatt": light oak, sage, olive, mustard and terracotta felt, black metal. Six desk islands with felt
    /// panels fill the west wing, focus nooks look out through the south-west glass, a library wall closes the west end,
    /// the community kitchen with two long tables is north of the lifts, two glass meeting rooms and a break-out lounge
    /// are east of the core, standing tables and games further east, lounges along the north-east glass.
    /// </summary>
    public static List<RoomItem> Coworking(TowerFloorPlan plan)
    {
        var d = new StoreyDesigner(plan);
        var lounge = new LoungeStyle("custom-sofa-slim-3", "olive/black", "custom-armchair-box", "mustard/black",
            "custom-zone-carpet-4x3", "stone", "custom-coffeetable-rect", "light-oak/black",
            "custom-floorlamp-column", "black/concrete", "custom-plant-bush-cylinder", "concrete");

        // Open workspace: two columns of desk islands, felt panels between them.
        foreach (var z in new[] { 13f, 18.5f, 24f })
        {
            d.WorkIsland(9.5f, z, 4, "light-oak/black").WorkIsland(17.5f, z, 4, "light-oak/black")
             .Put("custom-acoustic", 13.5f, z, 90f, z > 20f ? "terracotta" : "sage");
        }
        d.Put("custom-greenwall", 23.5f, 18f, 90f);

        // Focus nooks by the south-west glass, the library wall at the west end.
        foreach (var t in new[] { 0.18f, 0.5f, 0.82f })
        {
            d.AlongFacade(1, t, 1.9f, g => g.Reading(0f, 0f, "mustard/oak/black"));
        }
        foreach (var t in new[] { 0.34f, 0.47f, 0.6f })
        {
            d.AlongFacade(13, t, 0.5f, g => g.Put("custom-shelf-grid", 0f, 0f, 180f, "light-oak"));
        }

        // Community kitchen north of the lifts: open kitchen, two long tables under linear pendants, on an oak floor.
        d.Put("custom-zone-wood-14x5", 30.25f, 27.5f, 0f, "light-oak")
         .Put("custom-openkitchen", 30.25f, 24.2f)
         .Dining(26.5f, 27.6f, "custom-table-sled-300", "light-oak/black", "custom-chair-wood", "light-oak/sage", 4, pendantColours: "black")
         .Dining(34f, 27.6f, "custom-table-sled-300", "light-oak/black", "custom-chair-wood", "light-oak/terracotta", 4, pendantColours: "black")
         .Decor("custom-vase-bowl", 26.5f, 27.6f, 0f, "sage").Decor("custom-tray", 34f, 27.6f, 0f, "black");
        foreach (var t in new[] { 0.1f, 0.9f })
        {
            d.AlongFacade(10, t, 2.8f, g => g.Reading(0f, 0f, "olive/oak/black"), 180f);
        }

        // Two glass meeting rooms east of the core, a break-out lounge between them.
        d.GlassRoom(39f, 7f, 47f, 13f, "north")
         .Dining(43f, 10f, "custom-table-sled-240", "light-oak/black", "custom-chair-shell", "sage/oak", 3, pendantColours: "black")
         .Put("custom-screen", 46.3f, 10f, 90f)
         .GlassRoom(39f, 23f, 47f, 29f, "south")
         .Dining(43f, 26f, "custom-table-sled-240", "light-oak/black", "custom-chair-shell", "terracotta/oak", 3, pendantColours: "black")
         .Put("custom-whiteboard", 46.3f, 26f, 90f);
        d.Lounge(43f, 17.2f, lounge);

        // East: phone booths, standing tables with stools, games.
        d.Put("custom-phonebooth", 50.25f, 8.5f).Put("custom-phonebooth", 51.75f, 8.5f);
        foreach (var z in new[] { 13.5f, 19f })
        {
            d.Group(51.5f, z, 0f, g =>
            {
                g.Put("custom-hightable-long", 0f, 0f, 0f, "light-oak/black");
                foreach (var x in new[] { -0.5f, 0.5f })
                {
                    g.Put("custom-barstool-saddle", x, 0.7f, 0f, "mustard/black").Put("custom-barstool-saddle", x, -0.7f, 180f, "olive/black");
                }
            });
        }
        d.Put("game-connectfour", 55.5f, 16.2f)
         .Put("custom-pouf-cube", 55.5f, 15f, 0f, "terracotta")
         .Put("game-tictactoe", 56.5f, 21.5f)
         .Put("custom-pouf-round", 55.2f, 21.5f, 0f, "sage").Put("custom-pouf-round", 57.8f, 21.5f, 0f, "mustard");

        // North-east glass: lounges looking into the room.
        foreach (var t in new[] { 0.12f, 0.42f })
        {
            d.AlongFacade(8, t, 3.7f, g => g.Lounge(0f, 0f, lounge with { SofaColours = "sage/black", ChairColours = "terracotta/black" }, extras: false), 180f);
        }

        FacadeGreenery(d, 4.5f);
        return d.Items;
    }

    /// <summary>
    /// Sky Office "Executive": walnut, ebony, cognac leather, charcoal and brass. Reception facing the south lifts with
    /// waiting chairs, four executive offices behind glass in the west wing, the boardroom with a black marble table east of
    /// the core, client lounges along the south-west glass, a library wall by the south glass, a bar corner and lounges in
    /// the east, Memory in the foyer.
    /// </summary>
    public static List<RoomItem> SkyOffice(TowerFloorPlan plan)
    {
        var d = new StoreyDesigner(plan);
        var lounge = new LoungeStyle("custom-sofa-slim-3", "cognac/black", "custom-armchair-slim", "charcoal/brass",
            "custom-zone-carpet-4x3", "charcoal", "custom-coffeetable-oval", "walnut/brass",
            "custom-floorlamp-arc", "brass/black-marble", "custom-plant-tall-tapered", "black-marble");

        // Reception at the south lifts, waiting chairs either side.
        d.Put("custom-reception", 30.25f, 9.75f, 180f)
         .Put("custom-officechair", 30.25f, 8.4f, 180f)
         .Put("custom-greenwall", 23.5f, 18f, 90f);
        foreach (var x in new[] { 23f, 37.5f })
        {
            d.Group(x, 9f, 0f, g =>
            {
                g.Put("custom-armchair-slim", -0.8f, 0f, 0f, "cognac/black").Put("custom-armchair-slim", 0.8f, 0f, 0f, "cognac/black")
                 .Put("custom-sidetable-drum", 0f, 0.1f, 0f, "black-marble").DecorOnLast("custom-vase-bottle", 0f, "black");
            });
        }

        // Executive offices behind glass: desk, chair, two guest chairs, sideboard, plant and a reading lamp.
        foreach (var (x0, z0, door) in new[] { (4f, 12f, "east"), (14f, 12f, "west"), (4f, 22.5f, "east"), (14f, 22.5f, "west") })
        {
            var cx = x0 + 3f;
            d.GlassRoom(x0, z0, x0 + 6f, z0 + 6f, door)
             .Put("custom-rugmodern-300x200", cx, z0 + 3f, 0f, "charcoal/cognac")
             .Put("custom-desk-160", cx, z0 + 3.4f, 0f, "walnut/brass")
             .Put("custom-officechair", cx, z0 + 4.3f)
             .Put("custom-chair-cantilever", cx - 0.5f, z0 + 2.3f, 180f, "cognac/chrome")
             .Put("custom-chair-cantilever", cx + 0.5f, z0 + 2.3f, 180f, "cognac/chrome")
             .Put("custom-sideboard-120", cx, z0 + 5.5f, 0f, "walnut/brass").DecorOnLast("custom-tablelamp-dome", 0f, "warm-white/brass")
             .Decor("ph-classic_laptop", cx - 0.1f, z0 + 3.4f)
             .Put("custom-plant-tall-tapered", door == "east" ? x0 + 0.7f : x0 + 5.3f, z0 + 0.8f, 0f, "black-marble")
             .Put("custom-floorlamp-tripod", door == "east" ? x0 + 0.7f : x0 + 5.3f, z0 + 5.2f);
        }

        // Boardroom east of the core: black marble table for 10 under two linear pendants, screen at the end.
        d.GlassRoom(39f, 20f, 51f, 28f, "south")
         .Put("custom-zone-wood-11x7", 45f, 24f, 0f, "walnut")
         .Dining(45f, 24f, "custom-table-stone-300", "black-marble/brass", "custom-chair-classic", "cognac/walnut", 4, ends: true, pendant: null)
         .Put("custom-pendantlamp-linear", 44f, 24f, 0f, "brass").Put("custom-pendantlamp-linear", 46f, 24f, 0f, "brass")
         .Put("custom-screen", 50.3f, 24f, 90f)
         .Put("custom-plant-leafy-cylinder", 40f, 27f, 0f, "black-marble");
        d.Put("game-memory", 44f, 16.5f)
         .Put("custom-chair-classic", 42.8f, 16.5f, 270f, "cognac/walnut").Put("custom-chair-classic", 45.2f, 16.5f, 90f, "cognac/walnut");

        // Executive lounge north of the lifts: a wide carpet, corner sofa, club chairs, library along the north glass.
        d.Put("custom-zone-carpet-12x6", 30.25f, 26.5f, 0f, "charcoal")
         .Group(27f, 26.5f, 0f, g =>
         {
             g.Put("custom-sofa-low-corner", 0f, 1.1f, 0f, "cognac/black")
              .Put("custom-coffeetable-oval", 0f, -0.4f, 0f, "walnut/brass").DecorOnLast("custom-vase-bowl", 0f, "black")
              .Put("custom-armchair-slim", 2.2f, -0.4f, 90f, "charcoal/brass")
              .Put("custom-floorlamp-arc", 2.4f, 1.4f, 0f, "brass/black-marble");
         })
         .RoundDining(33.5f, 26.5f, "custom-roundtable-wood-100", "walnut/brass", "custom-chair-cantilever", "cognac/chrome", 4, "custom-pendantlamp-dome", "brass");
        foreach (var t in new[] { 0.3f, 0.45f, 0.6f })
        {
            d.AlongFacade(10, t, 0.5f, g => g.Put("custom-shelf-wide", 0f, 0f, 180f, "walnut"));
        }

        // Client lounges with the view (south-west glass), library by the south glass, lounges in the east.
        foreach (var t in new[] { 0.52f, 0.82f })
        {
            d.AlongFacade(1, t, 2.6f, g => g.Lounge(0f, 0f, lounge));
        }
        d.AlongFacade(1, 0.2f, 1.6f, g => g.Reading(0f, 0f, "cognac/walnut/black"));
        foreach (var t in new[] { 0.25f, 0.45f, 0.65f })
        {
            d.AlongFacade(4, t, 0.5f, g => g.Put("custom-shelf-wide", 0f, 0f, 180f, "walnut"));
        }
        d.AlongFacade(4, 0.88f, 2.4f, g => g.Reading(0f, 0f, "cognac/walnut/black"), 180f);
        d.Lounge(51f, 12.5f, lounge with { Sofa = "custom-sofa-box-3", SofaColours = "charcoal/black", ChairColours = "cognac/black" });
        d.AlongFacade(6, 0.55f, 3.7f, g => g.Lounge(0f, 0f, lounge with { Sofa = "custom-sofa-low-corner", SofaColours = "charcoal/black" }, armchairs: false, extras: false), 180f);

        // Bar corner by the north-east glass.
        d.AlongFacade(8, 0.2f, 1.0f, g =>
        {
            g.Put("custom-barcabinet", 0f, 0f, 180f, "walnut/brass");
            g.Put("custom-hightable", 0f, -1.6f, 0f, "black-marble/brass")
             .Put("custom-barstool-back", -0.8f, -1.6f, 270f, "cognac/black").Put("custom-barstool-back", 0.8f, -1.6f, 90f, "cognac/black");
        }, 180f);
        d.AlongFacade(8, 0.75f, 3.7f, g => g.Lounge(0f, 0f, lounge, extras: false), 180f);

        FacadeGreenery(d, 4.5f);
        return d.Items;
    }

    /// <summary>
    /// Conference centre "Forum": wool carpet, petrol, navy, charcoal and oak. The auditorium with stage and screen in the
    /// west wing, lounges by the south-west glass, the foyer with coffee counters and standing tables east of the core, two
    /// breakout rooms, bistro tables north of the lifts, and the quiz show stage with its audience at the east tip.
    /// </summary>
    public static List<RoomItem> Conference(TowerFloorPlan plan)
    {
        var d = new StoreyDesigner(plan);
        var lounge = new LoungeStyle("custom-sofa-box-3", "petrol/black", "custom-armchair-box", "stone/black",
            "custom-zone-carpet-4x3", "charcoal", "custom-coffeetable-rect", "oak/black",
            "custom-floorlamp-globe", "black/concrete", "custom-plant-leafy-cylinder", "concrete");

        // Auditorium: stage with screen, rows of chairs facing it on a navy carpet, an aisle in the middle.
        d.Put("custom-zone-carpet-10x17", 14.5f, 18f, 0f, "navy")
         .Put("custom-stage", 5.5f, 18f, 270f)
         .Put("custom-screen", 3.4f, 18f, 270f)
         .Put("custom-greenwall", 23.5f, 18f, 90f);
        foreach (var x in new[] { 10.5f, 12.5f, 14.5f, 16.5f, 18.5f })
        {
            for (var i = 0; i < 8; i++)
            {
                d.Put("custom-chair-shell", x, 10.5f + i * 0.8f, 90f, "navy/oak");
                d.Put("custom-chair-shell", x, 19.5f + i * 0.8f, 90f, "navy/oak");
            }
        }
        foreach (var t in new[] { 0.62f, 0.88f })
        {
            d.AlongFacade(1, t, 2.6f, g => g.Lounge(0f, 0f, lounge));
        }

        // Foyer: coffee counters on the core's east side, standing tables under globe pendants on a petrol carpet.
        d.Put("custom-zone-carpet-11x9", 44.5f, 18f, 0f, "petrol");
        d.Put("custom-buffet", 38.5f, 16.5f, 90f).DecorOnLast("ph-tea_set_01", 90f)
         .Put("custom-buffet", 38.5f, 19.5f, 90f).DecorOnLast("custom-tray", 0f, "brass");
        foreach (var x in new[] { 42f, 45.5f, 49f })
        {
            foreach (var z in new[] { 15.5f, 20.5f })
            {
                d.HighTable(x, z, "white-marble/black", "custom-barstool-round", "petrol/black")
                 .Put("custom-pendantlamp-globe", x, z, 0f, "black");
            }
        }

        // Breakout rooms.
        d.GlassRoom(39f, 6.5f, 47f, 12.5f, "north")
         .RoundDining(43f, 9.5f, "custom-roundtable-stone-140", "white-marble/black", "custom-chair-shell", "navy/oak", 4, "custom-pendantlamp-dome", "black")
         .Put("custom-whiteboard", 46.3f, 9.5f, 90f)
         .GlassRoom(39f, 24f, 47f, 30f, "south")
         .Dining(43f, 27f, "custom-table-sled-240", "oak/black", "custom-chair-shell", "navy/oak", 3, pendantColours: "black")
         .Put("custom-screen", 46.3f, 27f, 90f);

        // North of the lifts: bistro tables on oak; north-west glass: a lounge; south glass: coffee lounges.
        d.Put("custom-zone-wood-13x4", 30.75f, 25.25f, 0f, "oak");
        foreach (var t in new[] { 0.3f, 0.75f })
        {
            d.AlongFacade(3, t, 2.4f, g => g.Lounge(0f, 0f, lounge with { SofaColours = "stone/black", ChairColours = "petrol/black" }));
        }
        foreach (var x in new[] { 25.5f, 29f, 32.5f, 36f })
        {
            d.Bistro(x, 25.2f, "custom-chair-shell", "greige/oak", "custom-bistro", "white-marble/black");
        }
        d.AlongFacade(11, 0.5f, 3.7f, g => g.Lounge(0f, 0f, lounge with { SofaColours = "navy/black", Rug = "" }), 180f);

        // Quiz show at the east tip with its audience.
        d.Put("game-quizshow", 55f, 17.5f, 90f);
        foreach (var x in new[] { 49.8f, 51.4f })
        {
            for (var i = 0; i < 5; i++)
            {
                d.Put("custom-chair-shell", x, 14.5f + i * 1.25f, 270f, "navy/oak");
            }
        }
        d.AlongFacade(8, 0.3f, 3.7f, g => g.Lounge(0f, 0f, lounge, extras: false), 180f);

        FacadeGreenery(d, 4.5f);
        return d.Items;
    }

    /// <summary>
    /// Sky Lounge "Restaurant & Bar" (top floor): black marble, walnut, emerald velvet, blush and brass. Laid tables for two
    /// line the south-west glass, round laid tables fill the restaurant in the west wing with the open kitchen by the core
    /// and a laid long table, the bar with stools and standing tables is north of the lifts, velvet lounges, the dance
    /// floor with the DJ and the chess table are in the east, telescopes at both tips.
    /// </summary>
    public static List<RoomItem> SkyLounge(TowerFloorPlan plan)
    {
        var d = new StoreyDesigner(plan);
        var lounge = new LoungeStyle("custom-sofa-tufted-3", "emerald/walnut", "custom-armchair-round", "blush/walnut",
            "custom-zone-carpet-4x3", "charcoal", "custom-coffeetable-round", "black-marble/brass",
            "custom-floorlamp-arc", "brass/black-marble", "custom-plant-tall-tapered", "black-marble");

        // Window tables for two along the south-west glass.
        foreach (var t in new[] { 0.08f, 0.22f, 0.36f, 0.5f, 0.64f, 0.78f, 0.92f })
        {
            d.AlongFacade(1, t, 1.3f, g =>
            {
                g.Put("custom-dinner-2", 0f, 0f, 0f, "cream").DecorOnLast("custom-candles")
                 .Put("custom-chair-velvet", -0.9f - g.Slack, 0f, 270f, "emerald/brass").Put("custom-chair-velvet", 0.9f + g.Slack, 0f, 90f, "emerald/brass");
            });
        }

        // Restaurant on a walnut floor: laid round tables with candles, a laid long table, the open kitchen by the core.
        d.Put("custom-zone-wood-15x16", 12.5f, 20f, 0f, "walnut");
        foreach (var (x, z) in new[] { (7f, 14f), (11.5f, 14f), (16f, 14f), (7f, 18.5f), (11.5f, 18.5f), (16f, 18.5f), (7f, 22.5f), (11.5f, 22.5f), (16f, 22.5f) })
        {
            d.RoundDining(x, z, "custom-dinner-round-120", "cream", "custom-chair-velvet", "emerald/brass", 4, "custom-pendantlamp-globe", "brass")
             .Decor("custom-candles", x, z);
        }
        d.Put("custom-openkitchen", 21.5f, 18f, 90f)
         .Dining(11.5f, 26.9f, "custom-dinner-6", "cream", "custom-chair-classic", "blush/walnut", 3, pendant: "custom-pendantlamp-linear", pendantColours: "brass")
         .Decor("ph-wine_bottles_01", 11f, 26.9f).Decor("custom-candles", 12f, 26.9f);
        foreach (var x in new[] { 6.5f, 16.5f })
        {
            d.Put("custom-planterbench", x, 11.1f, 0f, "walnut");
        }

        // Bar north of the lifts on walnut, standing tables beside it.
        d.Put("custom-zone-wood-15x6", 30.25f, 26f, 0f, "walnut")
         .Put("custom-backbar", 30.25f, 26.6f)
         .Put("custom-skybar", 30.25f, 24.6f)
         .Decor("ph-wine_bottles_01", 29f, 24.6f).Decor("ph-brass_goblets", 31.5f, 24.6f)
         .Row("custom-barstool-back", 28.25f, 23.4f, 5, 1f, 180f, "emerald/brass");
        foreach (var x in new[] { 28.25f, 30.25f, 32.25f })
        {
            d.Put("custom-pendantlamp-dome", x, 24.6f, 0f, "brass");
        }
        d.HighTable(24.5f, 25f, "black-marble/brass", "custom-barstool-back", "emerald/brass")
         .HighTable(36f, 25f, "black-marble/brass", "custom-barstool-back", "emerald/brass");

        // East: velvet lounges on one big carpet, dance floor with DJ, chess, hologram.
        d.Put("custom-zone-carpet-9x19", 43f, 17.5f, 0f, "charcoal")
         .Lounge(43.5f, 11.5f, lounge with { Rug = "" })
         .Lounge(43.5f, 18f, lounge with { Rug = "", SofaColours = "blush/walnut", ChairColours = "emerald/walnut" })
         .Lounge(43.5f, 24.5f, lounge with { Rug = "", Sofa = "custom-sofa-low-3", SofaColours = "emerald/black" }, extras: false)
         .Put("custom-hologram", 38.5f, 18f)
         .Put("custom-zone-stone-6x4", 51f, 20.5f, 0f, "black-marble")
         .Put("custom-djbooth", 51f, 24f)
         .Put("custom-pendantlamp-cluster", 51f, 20.5f, 0f, "brass")
         .Put("game-chess", 53.5f, 13f)
         .Put("custom-armchair-slim", 53.5f, 11.8f, 180f, "emerald/brass").Put("custom-armchair-slim", 53.5f, 14.2f, 0f, "emerald/brass");

        // South glass: more tables for two with the view.
        foreach (var t in new[] { 0.15f, 0.38f, 0.62f, 0.85f })
        {
            d.AlongFacade(3, t, 1.3f, g =>
            {
                g.Put("custom-dinner-2", 0f, 0f, 0f, "cream").DecorOnLast("custom-candles")
                 .Put("custom-chair-velvet", -0.9f - g.Slack, 0f, 270f, "blush/brass").Put("custom-chair-velvet", 0.9f + g.Slack, 0f, 90f, "blush/brass");
            });
        }
        foreach (var t in new[] { 0.2f, 0.5f, 0.8f })
        {
            d.AlongFacade(4, t, 1.3f, g =>
            {
                g.Put("custom-dinner-2", 0f, 0f, 0f, "cream").DecorOnLast("custom-candles")
                 .Put("custom-chair-velvet", -0.9f, 0f, 270f, "blush/brass").Put("custom-chair-velvet", 0.9f, 0f, 90f, "blush/brass");
            });
        }

        // North-east glass: banquettes with bistro tables; telescopes at both tips.
        foreach (var t in new[] { 0.35f, 0.7f })
        {
            d.AlongFacade(8, t, 0.9f, g => g.BanquetteRow(0f, 0f, 240, "emerald/walnut", "custom-chair-velvet", "blush/brass", "black-marble/brass"), 180f);
        }
        d.Put("custom-telescope", 60.5f, 24.5f, 270f).Put("custom-telescope", 3f, 20f, 90f);

        FacadeGreenery(d, 4.5f);
        return d.Items;
    }

    /// <summary>A freshly bought office storey: reception, one desk island, a lounge and plants (the rest is up to the owner).</summary>
    public static List<RoomItem> StarterOffice(TowerFloorPlan plan)
    {
        var d = new StoreyDesigner(plan);
        d.Put("custom-reception", 30.25f, 9.75f, 180f)
         .Put("custom-officechair", 30.25f, 8.4f, 180f)
         .WorkIsland(10f, 18f, 3, "light-oak/black")
         .Lounge(47f, 18f, new LoungeStyle("custom-sofa-box-3", "stone/black", "custom-armchair-box", "sage/black",
             "custom-zone-carpet-4x3", "oat", "custom-coffeetable-rect", "oak/black"));
        FacadeGreenery(d, 7f);
        return d.Items;
    }
}
