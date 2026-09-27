using Reconnect.Contracts.Rooms;
using Reconnect.Modules.Rooms.Domain;

namespace Reconnect.Modules.Rooms.Infrastructure.Seeding;

/// <summary>
/// Whole tower storeys furnished as homes. <see cref="Penthouse"/>: a luxury apartment on a full floor of the Prime Tower –
/// pool in the south-east corner with the view along the Hardbrücke, spa and gym beside it, living room with fireplace and
/// TV lounge, marble kitchen and dining, a master suite (dressing room, bath, 18+ bedroom), office and gaming room.
/// Room coordinates of the Prime Tower plan (x 0 … 63 west → east, z 0 … 36 south → north), the core in the middle.
/// </summary>
internal static class ResidenceDesigns
{
    public static List<RoomItem> Penthouse(TowerFloorPlan plan)
    {
        var d = new StoreyDesigner(plan);
        var core = d.Items[0];   // the core in walnut slats, marble and art
        d.Items[0] = new RoomItem { ItemId = core.ItemId, Position = core.Position, Rotation = core.Rotation, Colours = "walnut" };

        // The whole floor in large cream marble tiles; rooms get their own floors on top.
        d.Put("custom-floor-marble", 24.5f, 13.5f, 0f, "marble-tiles");
        Pool(d);
        Spa(d);
        Gym(d);
        Living(d);
        KitchenAndDining(d);
        MasterSuite(d);
        OfficeAndGaming(d);
        return d.Items;
    }

    /// <summary>South-east corner: the pool along the south glass, teak deck, loungers facing the water and the Hardbrücke.</summary>
    private static void Pool(StoreyDesigner d)
    {
        d.Put("custom-pool-10x4", 47f, 8.5f)
         .Put("custom-zone-deck-16x1", 47f, 6f, 0f, "teak")
         .Put("custom-zone-deck-16x2.5", 47f, 11.75f, 0f, "teak")
         .Put("custom-zone-deck-3x4", 40.5f, 8.5f, 0f, "teak")
         .Put("custom-zone-deck-3x4", 53.5f, 8.5f, 0f, "teak");
        foreach (var (a, b) in new[] { (43f, 44.8f), (49.2f, 51f) })
        {
            d.Put("custom-lounger", a, 11.9f)
             .Put("custom-sidetable-drum", (a + b) / 2f, 12.4f, 0f, "travertine").DecorOnLast("custom-tray", 0f, "brass")
             .Put("custom-lounger", b, 11.9f);
        }
        d.Put("custom-plant-tall-bowl", 53.9f, 7.1f, 0f, "travertine")
         .Put("custom-plant-bush-bowl", 53.9f, 9.9f, 0f, "travertine")
         .Put("custom-plant-leafy-cylinder", 40.4f, 7.1f, 0f, "black-marble")
         .Put("custom-towelrack", 40.4f, 9.6f, 270f, "black/cream");
    }

    /// <summary>Spa north of the pool on dark marble: hot tub, sauna, day beds; glass towards the gym.</summary>
    private static void Spa(StoreyDesigner d)
    {
        d.Put("custom-zone-darkmarble-17x8", 48.5f, 17f, 0f, "dark-marble-tiles")
         .Put("custom-hottub", 48.5f, 16.5f, 0f, "travertine")
         .Put("custom-sauna", 54.5f, 18.5f, 90f, "light-oak")
         .Put("custom-chaise", 42.6f, 15f, 270f, "cream/black")
         .Put("custom-chaise", 42.6f, 17.4f, 270f, "cream/black")
         .Put("custom-sidetable-drum", 42.6f, 16.2f, 0f, "travertine").DecorOnLast("custom-candles")
         .Put("custom-towelrack", 51.8f, 20.4f, 180f, "black/cream")
         .Put("custom-plant-leafy-bowl", 56.2f, 14.2f, 0f, "travertine")
         .Put("custom-plant-tall-cylinder", 41f, 20.2f, 0f, "black-marble")
         .Put("custom-floorlamp-globe", 45.5f, 19.8f, 0f, "brass/black-marble");
        foreach (var x in new[] { 45f, 47f, 51f, 53f, 55f })
        {
            d.Put("custom-glasswall", x, 21.125f);
        }
    }

    /// <summary>North-east: the home gym on rubber, treadmills looking out east, rack, bench, bag, mirror wall.</summary>
    private static void Gym(StoreyDesigner d)
    {
        d.Put("custom-zone-rubber-15x7", 50.5f, 24.75f, 0f, "rubber")
         .Put("custom-treadmill", 55.5f, 23f, 270f, "gunmetal")
         .Put("custom-treadmill", 55.5f, 24.6f, 270f, "gunmetal")
         .Put("custom-spinbike", 56f, 26.6f, 270f, "black/black-leather")
         .Put("custom-powerrack", 46.2f, 24.6f, 0f, "black")
         .Put("custom-weightbench", 49.2f, 24.6f, 0f, "black-leather/black")
         .Put("custom-dumbbells", 50.6f, 27.6f, 0f, "black")
         .Put("custom-boxingbag", 53.2f, 27.4f, 0f, "red-leather/chrome")
         .Put("custom-gymmirror", 43.4f, 24.75f, 270f, "black")
         .Put("custom-yogamat", 51.6f, 22.8f, 0f, "sage")
         .Put("custom-yogamat", 52.4f, 22.8f, 0f, "charcoal")
         .Put("custom-plant-tall-cylinder", 44.2f, 27.6f, 0f, "concrete");
    }

    /// <summary>South of the core on marble: fireplace lounge towards the pool, TV lounge on the west side.</summary>
    private static void Living(StoreyDesigner d)
    {
        d.Put("custom-fireplace", 38.25f, 8.875f, 90f, "black-marble/black")
         // Fireplace as the divider to the pool, a lounge in front of it.
         .Put("custom-rugmodern-round-300", 35.3f, 8.9f, 0f, "oat/charcoal")
         .Put("custom-sofa-low-3", 33.4f, 8.9f, 270f, "sand/black")
         .Put("custom-coffeetable-oval", 35.4f, 8.9f, 90f, "walnut/brass").DecorOnLast("custom-vase-bowl", 0f, "black")
         .Put("custom-armchair-tufted", 35.5f, 6.6f, 180f, "emerald/walnut")
         .Put("custom-armchair-tufted", 35.5f, 11.2f, 0f, "emerald/walnut")
         // TV lounge.
         .Put("custom-tvwall", 24.75f, 8.9f, 270f, "walnut/black-marble")
         .Put("custom-rugmodern-300x200", 27.4f, 8.9f, 90f, "stone/charcoal")
         .Put("custom-sofa-low-corner", 29.2f, 8.9f, 90f, "oat/black")
         .Put("custom-coffeetable-rect", 26.9f, 8.9f, 90f, "walnut/black").DecorOnLast("custom-books", 0f, "sage")
         .Put("custom-floorlamp-arc", 30.4f, 11.4f, 0f, "brass/white-marble")
         .Put("custom-plant-tall-tapered", 25.6f, 6.1f, 0f, "travertine")
         .Put("custom-plant-tall-tapered", 37.4f, 11.6f, 0f, "travertine")
         .Put("custom-sculpture-1", 31.5f, 5.9f, 0f, "brass/travertine");
    }

    /// <summary>North of the core: kitchen wall, marble island with stools and hood, dining for ten, piano, bar corner.</summary>
    private static void KitchenAndDining(StoreyDesigner d)
    {
        d.Put("custom-kitchenwall-400", 23.125f, 25.5f, 270f, "walnut/white-marble/brass")
         .Put("custom-winefridge", 23.125f, 28.05f, 270f, "black")
         .Put("custom-kitchenisland-400", 27.25f, 25.5f, 270f, "white-marble/walnut/black")
         .Decor("custom-fruitbowl", 27.1f, 26.6f, 0f, "travertine")
         .Put("custom-kitchenhood", 27.25f, 25.5f, 90f, "black");
        foreach (var z in new[] { 24.3f, 25.5f, 26.7f })
        {
            d.Put("custom-barstool-back", 28.4f, z, 90f, "cognac/black");
        }
        d.Dining(33.6f, 27.1f, "custom-table-stone-300", "black-marble/brass", "custom-chair-velvet", "emerald/brass", 3, ends: true,
                pendant: "custom-pendantlamp-linear", pendantColours: "brass")
         .Decor("custom-candles", 33.1f, 27.1f)
         .Decor("custom-vase-tall", 34.2f, 27.1f, 0f, "black")
         .Put("custom-piano", 30.4f, 31.3f, 90f, "black")
         // Bar corner by the north-east glass.
         .Put("custom-barcabinet", 38.6f, 33.4f, 0f, "walnut/brass").DecorOnLast("custom-tray", 0f, "brass")
         .Put("custom-lounge-shell", 37.7f, 31.3f, 180f, "cognac/walnut/black")
         .Put("custom-lounge-shell", 39.5f, 31.3f, 180f, "cognac/walnut/black")
         .Put("custom-plant-tall-bowl", 36.2f, 32.6f, 0f, "travertine");
    }

    /// <summary>
    /// West wing, south to north: bath at the window, dressing room, bedroom (18+). One enters the suite through the dressing
    /// room from the hall.
    /// </summary>
    private static void MasterSuite(StoreyDesigner d)
    {
        // Walls: dressing room/bath | bedroom, bath | dressing room, suite | hall and office (walnut slats, art to the hall).
        d.WallLine(1f, 18f, 14f, 18f, "plaster", "warm-white", [(12f, 14f)])
         .WallLine(9f, 6f, 9f, 18f, "marble", "black-marble", [(15f, 17f)])
         .WallLine(14f, 4f, 14f, 29f, "slats", "walnut/black", [(15f, 17f)], new Dictionary<float, int> { [13f] = 5 }, artFacesPlus: true);

        // Bath.
        d.Put("custom-zone-darkmarble-6.75x9.25", 5.375f, 13.125f, 0f, "dark-marble-tiles")
         .Put("custom-bathtub", 5f, 9.8f, 0f, "white/black")
         .Put("custom-shower", 7.9f, 17.25f, 0f, "black/black-marble")
         .Put("custom-washstand", 4f, 17.5f, 0f, "white-marble/walnut/black")
         .Put("custom-toilet", 8.5f, 12.5f, 90f, "white")
         .Put("custom-towelrack", 2.2f, 15f, 270f, "black/cream")
         .Put("custom-plant-leafy-bowl", 2.6f, 11.2f, 0f, "black-marble");

        // Dressing room.
        d.Put("custom-zone-herringbone-4.75x12.25", 11.625f, 11.625f, 0f, "herringbone")
         .Put("custom-wardrobe-300", 13.625f, 11f, 90f, "bronze/walnut")
         .Put("custom-wardrobe-300", 9.625f, 11f, 270f, "bronze/walnut")
         .Put("custom-vanity", 10.8f, 17.25f, 0f, "walnut/blush/brass")
         .Put("custom-mirror", 11.6f, 6.4f, 180f, "brass")
         .Put("custom-pouf-round", 11.6f, 14.8f, 0f, "blush");

        // Bedroom (18+): dark herringbone, leather four-poster with cuffs, cross, bench, rack, cage, red light.
        d.Put("custom-zone-herringbone-12x11", 7.75f, 23.75f, 0f, "dark-herringbone")
         .Put("custom-bed-dungeon", 7.5f, 26.3f, 0f, "black-leather/crimson/black")
         .Put("custom-nightstand", 5.6f, 27.6f, 0f, "ebony/brass").DecorOnLast("custom-tablelamp-globe", 0f, "black/brass")
         .Put("custom-nightstand", 9.4f, 27.6f, 0f, "ebony/brass").DecorOnLast("custom-candles", 0f, "black")
         .Put("custom-rugmodern-round-300", 6.4f, 21.6f, 0f, "crimson/black")
         .Put("custom-adult-bench", 6.4f, 21.6f, 0f, "black-leather/black")
         .Put("custom-adult-cross", 2.3f, 22.2f, 270f, "black-leather/ebony")
         .Put("custom-adult-rack", 13.6f, 26f, 90f, "ebony/black-leather")
         .Put("custom-adult-cage", 11.9f, 20.1f, 0f, "black")
         .Put("custom-chaise", 12.6f, 23.2f, 90f, "crimson/black")
         .Put("custom-mirror", 4f, 19f, 180f, "black")
         .Put("custom-ledlamp-rgb", 2.4f, 28.3f, 0f, "red")
         .Put("custom-ledlamp-rgb", 12.9f, 28.4f, 0f, "red")
         .Put("custom-ledlamp-rgb", 2f, 19f, 0f, "red");
    }

    /// <summary>Hall with art, office behind glass, gaming room with four screens and a drinks fridge.</summary>
    private static void OfficeAndGaming(StoreyDesigner d)
    {
        // Gaming room walls: north towards the hall (four paintings on the hall side), east towards the living room.
        d.WallLine(14f, 13f, 24f, 13f, "plaster", "anthracite", [(22f, 24f)],
                new Dictionary<float, int> { [14f] = 1, [16f] = 2, [18f] = 6, [20f] = 8 }, artFacesPlus: true)
         .WallLine(24f, 1f, 24f, 13f, "slats", "walnut/black");

        // Hall.
        d.Put("custom-sculpture-2", 23.2f, 17.3f, 0f, "brass/travertine")
         .Put("custom-console", 19f, 17.4f, 0f, "walnut/brass").DecorOnLast("custom-candles", 0f, "brass");

        // Office behind glass.
        foreach (var x in new[] { 15f, 19f, 21f })
        {
            d.Put("custom-glasswall", x, 18.125f);
        }
        d.Put("custom-zone-herringbone-8.25x10.75", 18.375f, 23.625f, 0f, "herringbone")
         .Put("custom-executivedesk", 18.4f, 26f, 0f, "walnut/black-leather/brass")
         .Decor("custom-books", 17.6f, 26f, 0f, "navy")
         .Decor("custom-tablelamp-dome", 19.2f, 26f, 0f, "black/brass")
         .Put("custom-executivechair", 18.4f, 27.2f, 0f, "black-leather/chrome")
         .Put("custom-shelf-wide", 16f, 28.8f, 0f, "walnut")
         .Put("custom-shelf-wide", 20.8f, 28.8f, 0f, "walnut")
         .Put("custom-armchair-tufted", 15.9f, 20.6f, 270f, "cognac/walnut")
         .Put("custom-coffeetable-round", 17.2f, 20.6f, 0f, "black-marble/brass")
         .Put("custom-armchair-tufted", 18.5f, 20.6f, 90f, "cognac/walnut")
         .Put("custom-plant-tall-tapered", 21.6f, 20f, 0f, "concrete");

        // Gaming.
        d.Put("custom-zone-carpet-9.5x9", 19f, 8.5f, 0f, "charcoal")
         .Put("custom-gamingdesk", 19f, 5.2f, 0f, "black/red")
         .Put("custom-gamingchair", 19f, 6.4f, 0f, "black-leather/red-leather")
         .Put("custom-tvwall", 18.5f, 12.6f, 0f, "ebony/black-marble")
         .Put("custom-sofa-low-3", 18.5f, 9.8f, 180f, "charcoal/black")
         .Put("custom-pouf-round", 16.4f, 11f, 0f, "crimson")
         .Put("custom-pouf-round", 20.6f, 11f, 0f, "crimson")
         .Put("custom-drinkfridge", 15f, 12.4f, 0f, "black/red")
         .Put("custom-arcade", 23.4f, 7f, 90f, "black/red")
         .Put("custom-arcade", 23.4f, 8.1f, 90f, "black/cyan")
         .Put("custom-ledlamp-rgb", 14.7f, 5f, 0f, "red")
         .Put("custom-ledlamp-rgb", 23.5f, 11.8f, 0f, "cyan");
    }
}
