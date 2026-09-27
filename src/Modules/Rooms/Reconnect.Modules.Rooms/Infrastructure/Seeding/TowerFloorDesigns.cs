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
    /// Ground floor: access gates and a queue in front of the lifts, reception, a green wall on the core, a lobby café
    /// with counter and bistro tables, two lounges, the Zürich quiz and Connect Four for those waiting for the lift.
    /// </summary>
    public static List<RoomItem> Lobby(TowerFloorPlan plan)
    {
        var d = new FloorDesigner(plan);
        // Arrival: gates in front of the south lifts, the queue before them.
        d.At("custom-turnstiles", 30.25f, 11.25f)
         .At("custom-queuelane", 30.25f, 8.75f)
         .At("custom-reception", 17f, 11f)
         .At("ph-modern_wooden_cabinet", 17f, 13f)
         .Decor("ph-ceramic_vase_03", 16f, 13f)
         .At("custom-greenwall", 23.5f, 18f, Facing.West)
         .At("custom-planter", 17f, 16f)
         .At("custom-planter", 17f, 20f);
        // West lounges by the glass.
        d.Lounge(8f, 21f, Facing.South)
         .Lounge(8f, 13.5f, Facing.North, sofa: "ph-sofa_03")
         .At("game-quiz", 13f, 26.5f, Facing.South)
         .Row("ph-Ottoman_01", 11.5f, 24.25f, 3, 1.25f, Facing.North);
        // Art and big plants along the south-west facade.
        d.At("ph-side_table_tall_01", 20f, 5.5f).Decor("ph-marble_bust_01", 20f, 5.5f)
         .At("ph-pachira_aquatica_01", 25f, 4.25f)
         .At("ph-pachira_aquatica_01", 5f, 9.75f)
         .At("ph-pachira_aquatica_01", 5f, 27f);
        // Lobby café east of the core.
        d.At("custom-backbar", 46f, 25.75f)
         .At("custom-skybar", 46f, 23.75f)
         .Decor("kitchenCoffeeMachine", 44.5f, 23.75f)
         .Decor("ph-wine_bottles_01", 47.5f, 23.75f)
         .Row("ph-bar_chair_round_01", 44f, 22.25f, 5, 1f, Facing.North)
         .At("ph-caged_hanging_light", 44f, 23.75f)
         .At("ph-caged_hanging_light", 48f, 23.75f);
        for (var i = 0; i < 3; i++)
        {
            d.BistroTable(42.5f + i * 4f, 17.5f).BistroTable(42.5f + i * 4f, 12.5f);
        }
        d.At("game-connectfour", 55f, 17f)
         .At("ph-metal_stool_03", 55f, 15.75f, Facing.North)
         .At("ph-potted_plant_01", 53f, 8f)
         .At("ph-Chandelier_02", 8f, 21f)
         .At("ph-Chandelier_02", 8f, 13.5f);
        for (var i = 0; i < 3; i++)
        {
            d.At("ph-hanging_industrial_lamp", 42.5f + i * 4f, 17.5f).At("ph-hanging_industrial_lamp", 42.5f + i * 4f, 12.5f);
        }
        d.BistroTable(42.5f, 8.5f).BistroTable(46.5f, 8.5f);
        // Waiting lounges north of the lifts, a reading corner in the south-west, the view lounge at the east tip.
        d.Lounge(30f, 26.5f, Facing.South, sofa: "ph-Sofa_01", armchair: "ph-ArmChair_01")
         .Lounge(21f, 25.5f, Facing.South, sofa: "ph-sofa_03", armchair: "ph-mid_century_lounge_chair")
         .Lounge(38f, 27f, Facing.South)
         .At("ph-Chandelier_01", 30f, 26.5f)
         .Lounge(12f, 8f, Facing.North, armchair: "ph-ArmChair_01")
         .Lounge(56f, 21.5f, Facing.West, sofa: "ph-sofa_03")
         .Row("ph-painted_wooden_bench", 36.5f, 9.5f, 2, 2.5f, Facing.North);
        d.FacadeGreenery(5f);
        return d.Items;
    }

    /// <summary>
    /// Coworking: desk islands in the west wing, two glass meeting rooms and phone booths east, a kitchen with a long
    /// table, a lounge at the east tip; Connect Four and tic-tac-toe for the breaks.
    /// </summary>
    public static List<RoomItem> Coworking(TowerFloorPlan plan)
    {
        var d = new FloorDesigner(plan);
        foreach (var z in new[] { 10.5f, 15f, 19.5f, 24f })
        {
            d.DeskIsland(4.5f, z, 3).DeskIsland(12.5f, z, 3);
        }
        d.At("ph-steel_frame_shelves_02", 21.5f, 12f)
         .At("ph-steel_frame_shelves_02", 21.5f, 22.5f)
         .At("ph-pachira_aquatica_01", 21.5f, 26.5f)
         .At("ph-pachira_aquatica_01", 21f, 7.5f);
        // Meeting rooms east of the core (8 × 6 m, door to the corridor in the east, screen at the west end).
        foreach (var z0 in new[] { 7f, 22f })
        {
            d.GlassRoom(40f, z0, 48f, z0 + 6f, Facing.East)
             .TableWithChairs(44.5f, z0 + 3f, perSide: 2, ends: false)
             .At("ph-projector_screen", 40.75f, z0 + 3f, Facing.East);
        }
        d.At("custom-phonebooth", 50.25f, 9f, Facing.North)
         .At("custom-phonebooth", 51.75f, 9f, Facing.North);
        // Kitchen and long table next to the core.
        d.At("custom-openkitchen", 40f, 18f, Facing.West)
         .TableWithChairs(44f, 18f, perSide: 3)
         .Decor("ph-tea_set_01", 44f, 18f);
        // Lounge and games at the east tip.
        d.Lounge(54f, 21f, Facing.South)
         .At("game-connectfour", 52f, 14f)
         .At("ph-metal_stool_03", 52f, 12.75f, Facing.North)
         .At("game-tictactoe", 55.5f, 14.5f)
         .At("chairCushion", 54.25f, 14.5f, Facing.East)
         .At("chairCushion", 56.75f, 14.5f, Facing.West)
         .At("ph-potted_plant_01", 58.5f, 25f);
        // Quiet library lounge north of the core, a coffee corner south of it.
        d.Lounge(29f, 26.5f, Facing.South, sofa: "ph-Sofa_01", armchair: "ph-ArmChair_01")
         .At("ph-wooden_bookshelf_worn", 24f, 28.25f)
         .At("ph-wooden_display_shelves_01", 34.5f, 28.25f)
         .At("ph-Chandelier_01", 29f, 26.5f)
         .BistroTable(26f, 9f).BistroTable(30.5f, 9f).BistroTable(35f, 9f)
         .At("ph-CoffeeCart_01", 30.5f, 6.5f);
        d.FacadeGreenery(5f);
        return d.Items;
    }

    /// <summary>
    /// Sky Office: reception at the lifts, four executive offices behind glass in the west wing, a boardroom with a long
    /// table and screen, Memory in its foyer and a lounge with the view at the east tip.
    /// </summary>
    public static List<RoomItem> SkyOffice(TowerFloorPlan plan)
    {
        var d = new FloorDesigner(plan);
        d.At("custom-reception", 30.25f, 9.25f)
         .At("custom-officechair", 30.25f, 10.5f, Facing.South)
         .At("custom-greenwall", 23.5f, 18f, Facing.West)
         .At("ph-Chandelier_02", 30.25f, 9.25f);
        // Executive offices (6 × 6 m) along the west wing, doors to a 4 m corridor between them.
        foreach (var (x0, z0, door) in new[] { (3f, 8f, Facing.East), (13f, 8f, Facing.West), (3f, 21f, Facing.East), (13f, 21f, Facing.West) })
        {
            var cx = x0 + 3f;
            var cz = z0 + 3f;
            var wall = door == Facing.East ? x0 + 0.75f : x0 + 5.25f;   // shelf on the side away from the door
            d.GlassRoom(x0, z0, x0 + 6f, z0 + 6f, door)
             .At("ph-metal_office_desk", cx, cz + 0.5f)
             .Decor("ph-classic_laptop", cx - 0.25f, cz + 0.5f, Facing.South)
             .Decor("ph-desk_lamp_arm_01", cx + 0.6f, cz + 0.6f, Facing.South)
             .At("custom-officechair", cx, cz + 1.5f, Facing.South)
             .At("ph-GreenChair_01", cx - 0.75f, cz - 0.75f, Facing.North)
             .At("ph-GreenChair_01", cx + 0.75f, cz - 0.75f, Facing.North)
             .At("ph-steel_frame_shelves_02", wall, cz + 2f)
             .At("ph-potted_plant_02", wall, z0 + 0.75f);
        }
        // Boardroom east of the core.
        d.GlassRoom(40f, 20f, 50f, 28f, Facing.South)
         .TableWithChairs(43.4f, 24f, perSide: 3, ends: false)
         .TableWithChairs(46.6f, 24f, perSide: 3, ends: false)
         .At("ph-dining_chair_02", 41.4f, 24f, Facing.East)
         .At("ph-dining_chair_02", 48.6f, 24f, Facing.West)
         .At("ph-projector_screen", 48.75f, 27f, Facing.South)
         .At("game-memory", 44f, 16.5f)
         .At("ph-dining_chair_02", 42.75f, 16.5f, Facing.East)
         .At("ph-dining_chair_02", 45.25f, 16.5f, Facing.West);
        // Lounge with the view.
        d.Lounge(53f, 10.5f, Facing.North, sofa: "ph-sofa_03", armchair: "ph-mid_century_lounge_chair")
         .Lounge(56f, 19f, Facing.West)
         .At("ph-pachira_aquatica_01", 40f, 7.5f);
        // Waiting lounges beside the reception, a lounge north of the core, open desks east.
        d.Lounge(23.5f, 8.5f, Facing.North, sofa: "ph-sofa_03", armchair: "ph-GreenChair_01")
         .Lounge(36.5f, 9f, Facing.North, sofa: "ph-sofa_03", armchair: "ph-GreenChair_01")
         .Lounge(29f, 26.5f, Facing.South, sofa: "ph-Sofa_01", armchair: "ph-mid_century_lounge_chair")
         .At("ph-Chandelier_01", 29f, 26.5f)
         .DeskIsland(41f, 11f, 3);
        d.FacadeGreenery(5f);
        return d.Items;
    }

    /// <summary>
    /// Conference centre: a hall with stage and rows of chairs (aisle in the middle) in the west wing, the foyer with
    /// buffets and high tables at the core, a breakout room, and the quiz show stage at the east tip.
    /// </summary>
    public static List<RoomItem> Conference(TowerFloorPlan plan)
    {
        var d = new FloorDesigner(plan);
        d.At("custom-stage", 5.5f, 18f, Facing.East)
         .At("ph-projector_screen", 2.75f, 18f, Facing.East)
         .At("custom-greenwall", 23.5f, 18f, Facing.West);
        // Rows of chairs facing the stage, an aisle in the middle (z 17–19).
        foreach (var x in new[] { 10.5f, 12.5f, 14.5f, 16.5f, 18.5f })
        {
            d.Column("ph-dining_chair_02", x, 10.5f, 6, 1f, Facing.West)
             .Column("ph-dining_chair_02", x, 19.5f, 7, 1f, Facing.West);
        }
        // Foyer: buffets along the core's east side, high tables.
        d.At("custom-buffet", 38.5f, 17.75f, Facing.West)
         .Decor("ph-tea_set_01", 38.5f, 17.5f, Facing.West)
         .Decor("ph-brass_goblets", 38.5f, 18.5f)
         .HighTable(42f, 17f).HighTable(45f, 19f).HighTable(42f, 20.5f).HighTable(45f, 15.5f)
         .At("ph-Chandelier_02", 43.5f, 18f);
        // Breakout room.
        d.GlassRoom(40f, 23f, 48f, 29f, Facing.South)
         .TableWithChairs(44f, 26f);
        // Quiz show at the east tip.
        d.At("game-quizshow", 54f, 17f, Facing.West)
         .Column("ph-dining_chair_02", 49.5f, 14.5f, 5, 1.25f, Facing.East)
         .At("ph-potted_plant_01", 50f, 8f)
         .At("ph-potted_plant_01", 57f, 24f)
         .At("ph-pachira_aquatica_01", 26f, 5f)
         .Column("ph-dining_chair_02", 48f, 14.5f, 5, 1.25f, Facing.East);
        // Foyer lounges north and south of the core, coffee and high tables.
        d.Lounge(29f, 26.5f, Facing.South, sofa: "ph-Sofa_01", armchair: "ph-ArmChair_01")
         .Lounge(30.5f, 8.5f, Facing.North, sofa: "ph-sofa_03")
         .At("ph-CoffeeCart_01", 36.5f, 9f)
         .HighTable(22f, 9f).HighTable(22f, 25.5f).HighTable(36f, 26.5f)
         .At("ph-Chandelier_01", 29f, 26.5f);
        d.FacadeGreenery(5f);
        return d.Items;
    }

    /// <summary>
    /// Sky Lounge (top floor): bar with back bar and stools north-east of the core, restaurant tables by the glass in
    /// the west wing, open kitchen, DJ, a lounge with the chess table and telescopes at the tips.
    /// </summary>
    public static List<RoomItem> SkyLounge(TowerFloorPlan plan)
    {
        var d = new FloorDesigner(plan);
        // Bar.
        d.At("custom-backbar", 44f, 26.5f)
         .At("custom-skybar", 44f, 24.25f)
         .Decor("ph-wine_bottles_01", 42.5f, 24.25f)
         .Decor("ph-brass_goblets", 45f, 24.25f)
         .Row("ph-bar_chair_round_01", 42f, 22.75f, 5, 1f, Facing.North)
         .At("ph-caged_hanging_light", 42f, 24.25f)
         .At("ph-caged_hanging_light", 46f, 24.25f)
         .At("custom-djbooth", 51f, 26f, Facing.South);
        // Restaurant in the west wing.
        foreach (var (x, z) in new[] { (6f, 11f), (12f, 11f), (18f, 9f), (6f, 24.5f), (12f, 24.5f), (18f, 25f) })
        {
            d.TableWithChairs(x, z, table: "ph-round_wooden_table_02", perSide: 1, ends: true)
             .Decor("ph-brass_candleholders", x, z);
        }
        d.At("ph-Chandelier_02", 12f, 17.75f)
         .At("custom-openkitchen", 18f, 17.75f, Facing.West)
         .At("custom-greenwall", 23.5f, 18f, Facing.West);
        // Lounge, chess and the view.
        d.Lounge(46f, 12f, Facing.North, sofa: "ph-sofa_03")
         .At("game-chess", 53f, 17.5f)
         .At("ph-ArmChair_01", 53f, 16.5f, Facing.North)
         .At("ph-ArmChair_01", 53f, 18.75f, Facing.South)
         .At("custom-telescope", 60.5f, 24.5f, Facing.East)
         .At("custom-telescope", 3f, 20f, Facing.West)
         .At("custom-hologram", 38.5f, 18f)
         .At("ph-pachira_aquatica_01", 53.5f, 8f)
         .At("ph-potted_plant_01", 30f, 4f);
        // More restaurant tables in the west wing and along the south glass, a chef's table north of the core.
        foreach (var (x, z) in new[] { (7f, 15.5f), (11.5f, 15.5f), (7f, 20.5f), (11.5f, 20.5f), (9f, 7.5f), (24f, 9f), (29f, 9f), (34f, 9f) })
        {
            d.TableWithChairs(x, z, table: "ph-round_wooden_table_02", perSide: 1, ends: true)
             .Decor("ph-brass_candleholders", x, z);
        }
        d.TableWithChairs(30f, 26f, perSide: 3)
         .Decor("ph-brass_candleholders", 30f, 26f)
         .At("ph-Chandelier_01", 30f, 26f);
        // Lounges along the north glass, high tables at the bar.
        d.Lounge(38.5f, 29.5f, Facing.South, sofa: "ph-sofa_03", armchair: "ph-mid_century_lounge_chair")
         .HighTable(41f, 19.5f).HighTable(41f, 15.5f)
         .Lounge(55f, 11.5f, Facing.North, sofa: "ph-Sofa_01", armchair: "ph-ArmChair_01");
        d.FacadeGreenery(5f);
        return d.Items;
    }

    /// <summary>A freshly bought office storey: reception, one desk island, a lounge and some plants – the rest is the owner's.</summary>
    public static List<RoomItem> StarterOffice(TowerFloorPlan plan)
    {
        var d = new FloorDesigner(plan);
        d.At("custom-reception", 30.25f, 9.25f)
         .DeskIsland(8f, 15f, 3)
         .Lounge(47f, 18f, Facing.West)
         .At("ph-pachira_aquatica_01", 21f, 7.5f)
         .At("ph-potted_plant_01", 55f, 12f);
        d.FacadeGreenery(8f);
        return d.Items;
    }
}
