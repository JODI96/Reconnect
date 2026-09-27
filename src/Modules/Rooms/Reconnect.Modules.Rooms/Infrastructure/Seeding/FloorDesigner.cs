using Reconnect.Contracts.Rooms;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Modules.Rooms.Features;

namespace Reconnect.Modules.Rooms.Infrastructure.Seeding;

/// <summary>Which way an item faces (the side one looks at when sitting on it, the front of a counter).</summary>
internal enum Facing
{
    North,
    East,
    South,
    West,
}

/// <summary>
/// Designs a tower storey the way the build editor places things, but in metres: the floor's core first, then groups
/// (lounges, desk islands, meeting rooms, dining tables …) built from catalog items. Floor items snap to the 25 cm grid
/// by their footprint's corner or centre, small things to the 12.5 cm decor grid. The result must pass the build rules
/// (SeedRoomLayoutTests).
/// </summary>
internal sealed class FloorDesigner
{
    private readonly List<RoomItem> _items = [];
    private readonly TowerFloorPlan _plan;

    public FloorDesigner(TowerFloorPlan plan)
    {
        _plan = plan;
        _items.Add(TowerFurnishing.Core(plan));
    }

    public List<RoomItem> Items => _items;

    /// <summary>
    /// Rotation that makes an item face <paramref name="facing"/>: Kenney and our own items face -Z (south) at 0°,
    /// Poly Haven models +Z.
    /// </summary>
    public static float Rotation(string itemId, Facing facing)
    {
        var kenney = facing switch
        {
            Facing.South => 0f,
            Facing.West => 90f,
            Facing.North => 180f,
            _ => 270f,
        };
        return itemId.StartsWith("ph-", StringComparison.Ordinal) ? (kenney + 180f) % 360f : kenney;
    }

    /// <summary>A floor item centred at (x, z) metres.</summary>
    public FloorDesigner At(string itemId, float x, float z, Facing facing = Facing.South)
    {
        var rotation = Rotation(itemId, facing);
        var definition = ItemDefinitions.Find(itemId) ?? throw new ArgumentException("Unknown item " + itemId);
        var cells = RoomLayout.Footprint(definition, x, z, RoomLayout.Quarter(rotation));
        _items.Add(RoomItem.AtCell(itemId, cells.X, cells.Z, rotation));
        return this;
    }

    /// <summary>A small thing on a table top, centred at (x, z) metres (12.5 cm grid).</summary>
    public FloorDesigner Decor(string itemId, float x, float z, Facing facing = Facing.South)
    {
        _items.Add(RoomItem.At(itemId, RoomLayout.SnapDecor(x), RoomLayout.SnapDecor(z), Rotation(itemId, facing)));
        return this;
    }

    /// <summary>
    /// Places a floor item only where it fits: inside the outline, off the lift landings, not in other furniture (with a
    /// little air around it). Returns whether it was placed.
    /// </summary>
    public bool TryAt(string itemId, float x, float z, Facing facing = Facing.South, float air = 0.25f) =>
        TryAt(itemId, x, z, facing, air, null) is not null;

    /// <param name="problems">How many rule problems the layout has now, if known (saves a validation).</param>
    /// <returns>The problem count after placing, or null if the item was not placed.</returns>
    private int? TryAt(string itemId, float x, float z, Facing facing, float air, int? problems)
    {
        var rotation = Rotation(itemId, facing);
        var definition = ItemDefinitions.Find(itemId) ?? throw new ArgumentException("Unknown item " + itemId);
        var cells = RoomLayout.Footprint(definition, x, z, RoomLayout.Quarter(rotation));
        var dtos = _items.Select(i => i.ToDto()).ToList();
        var room = RoomZones.ContextFor("office", _plan.Width, _plan.Depth, dtos, _plan.Outline);
        var margin = (int)Math.Round(air / BuildGrid.CellSize);
        var padded = new CellRect(cells.X - margin, cells.Z - margin, cells.Width + 2 * margin, cells.Depth + 2 * margin);
        if (!room.IsInside(cells) || room.Reserved.Any(r => r.Overlaps(padded)))
        {
            return null;
        }
        foreach (var item in dtos)
        {
            var other = ItemDefinitions.Find(item.ItemId);
            if (other is { Kind: ItemKind.Floor } && RoomLayout.Footprint(item, other).Overlaps(padded))
            {
                return null;
            }
        }
        // Keep it only if it breaks no rule (e.g. cuts a seat off from the rest of the floor).
        var before = problems ?? RoomLayout.Validate(room, dtos).Count;
        var placed = RoomItem.AtCell(itemId, cells.X, cells.Z, rotation);
        dtos.Add(placed.ToDto());
        if (RoomLayout.Validate(RoomZones.ContextFor("office", _plan.Width, _plan.Depth, dtos, _plan.Outline), dtos).Count > before)
        {
            return null;
        }
        _items.Add(placed);
        return before;
    }

    /// <summary>
    /// Plants along the glass all round the storey (every <paramref name="spacing"/> metres, a little inside the facade),
    /// only where there is room: big pachiras, potted plants and planter boxes in turn.
    /// </summary>
    public FloorDesigner FacadeGreenery(float spacing = 5f, float inset = 0.9f)
    {
        var plants = new[] { "ph-pachira_aquatica_01", "ph-potted_plant_01", "ph-planter_box_02", "ph-potted_plant_02" };
        var outline = _plan.Outline;
        var area = 0f;
        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        {
            area += outline[j].X * outline[i].Z - outline[i].X * outline[j].Z;
        }
        var counterClockwise = area > 0f;
        var next = 0;
        int? problems = null;
        for (var i = 0; i < outline.Count; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Count];
            var dx = b.X - a.X;
            var dz = b.Z - a.Z;
            var length = (float)Math.Sqrt(dx * dx + dz * dz);
            if (length < spacing * 0.8f)
            {
                continue;
            }
            // Inward normal: left of the edge for a counter-clockwise outline.
            var (nx, nz) = counterClockwise ? (-dz / length, dx / length) : (dz / length, -dx / length);
            var count = (int)(length / spacing);
            for (var k = 0; k < count; k++)
            {
                var t = (k + 0.5f) / count;
                var x = a.X + dx * t + nx * inset;
                var z = a.Z + dz * t + nz * inset;
                if (TryAt(plants[next % plants.Length], x, z, Facing.South, 0.25f, problems) is { } now)
                {
                    problems = now;
                    next++;
                }
            }
        }
        return this;
    }

    // ---------- Groups ----------

    /// <summary>
    /// Lounge island around (x, z): rug, a sofa behind the coffee table facing <paramref name="facing"/>, two armchairs at
    /// the sides, a vase on the table, a floor lamp and a plant.
    /// </summary>
    public FloorDesigner Lounge(float x, float z, Facing facing, string sofa = "ph-sofa_02", string armchair = "ph-modern_arm_chair_01",
        string rug = "custom-rug", bool plant = true)
    {
        var (fx, fz) = Direction(facing);
        var (sx, sz) = (fz, -fx);   // to the right of the sofa's view
        At(rug, x, z, facing is Facing.North or Facing.South ? Facing.South : Facing.West);
        At("ph-coffee_table_round_01", x, z);
        Decor("ph-ceramic_vase_02", x, z);
        At(sofa, x - fx * 1.45f, z - fz * 1.45f, facing);
        At(armchair, x + sx * 1.55f, z + sz * 1.55f, Turn(facing, -1));
        At(armchair, x - sx * 1.55f, z - sz * 1.55f, Turn(facing, 1));
        // Lamp and plant just beside the sofa's ends (long sofas push them out).
        var half = ItemDefinitions.Find(sofa)!.Width * BuildGrid.CellSize / 2f;
        At("lampRoundFloor", x - fx * 1.55f + sx * (half + 0.3f), z - fz * 1.55f + sz * (half + 0.3f));
        if (plant)
        {
            At("ph-potted_plant_02", x - fx * 1.5f - sx * (half + 0.55f), z - fz * 1.5f - sz * (half + 0.55f));
        }
        return this;
    }

    /// <summary>Desk island: <paramref name="desks"/> desks in two rows back to back (along x), chairs on both sides, laptops.</summary>
    public FloorDesigner DeskIsland(float x, float z, int desks, bool lamps = true)
    {
        const float deskWidth = 1.5f;
        for (var i = 0; i < desks; i++)
        {
            var dx = x + i * deskWidth;
            At("custom-benchdesk", dx + deskWidth / 2f, z + 0.375f);          // north row
            At("custom-benchdesk", dx + deskWidth / 2f, z - 0.375f);          // south row
            At("custom-officechair", dx + deskWidth / 2f, z + 1.05f, Facing.South);
            At("custom-officechair", dx + deskWidth / 2f, z - 1.05f, Facing.North);
            Decor(i % 2 == 0 ? "laptop" : "ph-classic_laptop", dx + 0.75f, z + 0.5f, Facing.South);
            Decor("computerScreen", dx + 0.75f, z - 0.55f, Facing.North);
            if (lamps && i % 2 == 1)
            {
                Decor("ph-desk_lamp_arm_01", dx + 1.25f, z + 0.4f, Facing.South);
            }
        }
        return this;
    }

    /// <summary>
    /// Glass-walled room from (x0, z0) to (x1, z1) metres (whole metres work best), a door gap on <paramref name="door"/>
    /// side of about 1.5 m in the middle.
    /// </summary>
    public FloorDesigner GlassRoom(float x0, float z0, float x1, float z1, Facing door)
    {
        void Wall(float ax, float az, float bx, float bz, bool hasDoor)
        {
            var horizontal = Math.Abs(bz - az) < 0.01f;
            var length = horizontal ? bx - ax : bz - az;
            var pieces = (int)Math.Round(length / 2f);
            var gap = hasDoor ? pieces / 2 : -1;
            for (var i = 0; i < pieces; i++)
            {
                if (i == gap)
                {
                    continue;
                }
                var t = (i + 0.5f) * length / pieces;
                if (horizontal)
                {
                    At("custom-glasswall", ax + t, az);
                }
                else
                {
                    // Side walls stand just outside the corners so they don't cut into the front and back walls.
                    At("custom-glasswall", ax + (ax <= Math.Min(x0, x1) ? -0.125f : 0.125f), az + t, Facing.West);
                }
            }
        }
        Wall(x0, z0, x1, z0, door == Facing.South);
        Wall(x0, z1, x1, z1, door == Facing.North);
        Wall(x0, z0, x0, z1, door == Facing.West);
        Wall(x1, z0, x1, z1, door == Facing.East);
        return this;
    }

    /// <summary>Dining / meeting table (along x) with chairs on both long sides and one at each end.</summary>
    public FloorDesigner TableWithChairs(float x, float z, string table = "ph-dining_table", string chair = "ph-dining_chair_02", int perSide = 3,
        bool ends = true)
    {
        var definition = ItemDefinitions.Find(table)!;
        var length = definition.Width * BuildGrid.CellSize;
        var depth = definition.Depth * BuildGrid.CellSize;
        At(table, x, z);
        for (var i = 0; i < perSide; i++)
        {
            var cx = x - length / 2f + (i + 0.5f) * length / perSide;
            At(chair, cx, z + depth / 2f + 0.3f, Facing.South);
            At(chair, cx, z - depth / 2f - 0.3f, Facing.North);
        }
        if (ends)
        {
            At(chair, x - length / 2f - 0.3f, z, Facing.East);
            At(chair, x + length / 2f + 0.3f, z, Facing.West);
        }
        return this;
    }

    /// <summary>Bistro table for two (chairs west and east).</summary>
    public FloorDesigner BistroTable(float x, float z)
    {
        At("ph-gallinera_table", x, z);
        At("ph-gallinera_chair", x - 0.85f, z, Facing.East);
        At("ph-gallinera_chair", x + 0.85f, z, Facing.West);
        return this;
    }

    /// <summary>High table with stools around it (standing reception).</summary>
    public FloorDesigner HighTable(float x, float z)
    {
        At("ph-side_table_tall_01", x, z);
        At("ph-metal_stool_03", x, z + 0.55f, Facing.South);
        At("ph-metal_stool_03", x, z - 0.55f, Facing.North);
        return this;
    }

    /// <summary>A row of <paramref name="count"/> seats along x facing <paramref name="facing"/>, <paramref name="pitch"/> metres apart.</summary>
    public FloorDesigner Row(string itemId, float x, float z, int count, float pitch, Facing facing)
    {
        for (var i = 0; i < count; i++)
        {
            At(itemId, x + i * pitch, z, facing);
        }
        return this;
    }

    /// <summary>A column of items along z.</summary>
    public FloorDesigner Column(string itemId, float x, float z, int count, float pitch, Facing facing)
    {
        for (var i = 0; i < count; i++)
        {
            At(itemId, x, z + i * pitch, facing);
        }
        return this;
    }

    private static (float X, float Z) Direction(Facing facing) => facing switch
    {
        Facing.North => (0f, 1f),
        Facing.East => (1f, 0f),
        Facing.South => (0f, -1f),
        _ => (-1f, 0f),
    };

    /// <summary>A quarter turn clockwise (+1) or counter-clockwise (-1) seen from above.</summary>
    private static Facing Turn(Facing facing, int quarters) => (Facing)(((int)facing + quarters + 4) % 4);
}
