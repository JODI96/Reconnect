using Reconnect.Contracts.Rooms;
using Reconnect.Modules.Rooms.Domain;
using Reconnect.Modules.Rooms.Features;

namespace Reconnect.Modules.Rooms.Infrastructure.Seeding;

/// <summary>A lounge island: what it is made of and in which colours (see <see cref="ItemColours"/>).</summary>
internal sealed record LoungeStyle(
    string Sofa, string SofaColours,
    string Chair, string ChairColours,
    string Rug, string RugColours,
    string Table, string? TableColours = null,
    string Lamp = "custom-floorlamp-arc", string? LampColours = null,
    string Plant = "custom-plant-tall-tapered", string? PlantColours = null);

/// <summary>
/// Designs a tower storey like an interior designer: groups (lounge islands, dining tables, bistro sets, reading nooks,
/// libraries …) drawn in their own frame and placed anywhere at any angle – e.g. parallel to a slanted facade, facing the
/// view. Items at quarter turns snap to the 25 cm grid, turned ones to the 12.5 cm grid in whole degrees. The result must
/// pass the build rules (SeedRoomLayoutTests).
/// </summary>
/// <remarks>
/// Frame convention (Unity yaw): a frame at (x, z) turned by a degrees maps its local (lx, lz) to
/// (x + lx·cos a + lz·sin a, z − lx·sin a + lz·cos a); an item turned r inside it ends up turned a + r. Our own furniture
/// faces local −Z at rotation 0.
/// </remarks>
internal sealed class StoreyDesigner
{
    private readonly List<RoomItem> _items = [];
    private readonly TowerFloorPlan _plan;
    private readonly Stack<(float X, float Z, float Angle)> _frames = new();

    public StoreyDesigner(TowerFloorPlan plan)
    {
        _plan = plan;
        _items.Add(TowerFurnishing.Core(plan));
        _frames.Push((0f, 0f, 0f));
    }

    public List<RoomItem> Items => _items;

    public TowerFloorPlan Plan => _plan;

    // ---------- Frames ----------

    /// <summary>Draws <paramref name="draw"/> in a frame at local (x, z) turned by <paramref name="angle"/> degrees.</summary>
    public StoreyDesigner Group(float x, float z, float angle, Action<StoreyDesigner> draw)
    {
        var (wx, wz) = ToWorld(x, z);
        _frames.Push((wx, wz, _frames.Peek().Angle + angle));
        try
        {
            draw(this);
        }
        finally
        {
            _frames.Pop();
        }
        return this;
    }

    /// <summary>
    /// A frame on the facade: the point <paramref name="t"/> (0 … 1) along outline edge <paramref name="edge"/>,
    /// <paramref name="inset"/> metres into the room, turned so that local −Z looks out through the glass (+Z is the room).
    /// </summary>
    public (float X, float Z, float Angle) OnFacade(int edge, float t, float inset)
    {
        var outline = _plan.Outline;
        var a = outline[edge % outline.Count];
        var b = outline[(edge + 1) % outline.Count];
        var dx = b.X - a.X;
        var dz = b.Z - a.Z;
        var length = (float)Math.Sqrt(dx * dx + dz * dz);
        var (nx, nz) = (-dz / length, dx / length);   // into the room (counter-clockwise outline: left of the edge)
        if (Area(outline) < 0f)
        {
            (nx, nz) = (-nx, -nz);
        }
        // Local −Z = outward = (−nx, −nz): Unity's −Z axis turned by a is (−sin a, −cos a).
        var angle = (float)(Math.Atan2(-nx, -nz) * 180.0 / Math.PI) + 180f;
        var angleNormalised = RoomLayout.Normalize((float)Math.Round(angle));
        return (a.X + dx * t + nx * inset, a.Z + dz * t + nz * inset, angleNormalised);
    }

    /// <summary><see cref="Group"/> at a place on the facade (see <see cref="OnFacade"/>).</summary>
    public StoreyDesigner AlongFacade(int edge, float t, float inset, Action<StoreyDesigner> draw, float extraTurn = 0f)
    {
        var (x, z, angle) = OnFacade(edge, t, inset);
        _frames.Push((x, z, angle + extraTurn));
        try
        {
            draw(this);
        }
        finally
        {
            _frames.Pop();
        }
        return this;
    }

    /// <summary>
    /// Extra spacing inside groups when the frame is turned off the grid: turned pieces cover every cell they reach into,
    /// so neighbours need a little more room.
    /// </summary>
    public float Slack => RoomLayout.IsQuarterTurn(_frames.Peek().Angle) ? 0f : 0.15f;

    private (float X, float Z) ToWorld(float x, float z)
    {
        var (ox, oz, angle) = _frames.Peek();
        var radians = angle * Math.PI / 180.0;
        var cos = (float)Math.Cos(radians);
        var sin = (float)Math.Sin(radians);
        return (ox + x * cos + z * sin, oz - x * sin + z * cos);
    }

    private static float Area(IReadOnlyList<RoomPointDto> outline)
    {
        var sum = 0f;
        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        {
            sum += outline[j].X * outline[i].Z - outline[i].X * outline[j].Z;
        }
        return sum;
    }

    // ---------- Items ----------

    /// <summary>
    /// A floor item centred at local (x, z), turned <paramref name="rotation"/> degrees in the frame (our furniture faces
    /// local −Z at 0; Poly Haven models are turned for you so 0 means the same).
    /// </summary>
    public StoreyDesigner Put(string itemId, float x, float z, float rotation = 0f, string? colours = null)
    {
        _items.Add(Place(itemId, x, z, rotation, colours));
        _knownProblems = null;
        return this;
    }

    /// <summary>A small thing on a table top at local (x, z) (12.5 cm grid).</summary>
    public StoreyDesigner Decor(string itemId, float x, float z, float rotation = 0f, string? colours = null)
    {
        var (wx, wz) = ToWorld(x, z);
        var world = WorldRotation(itemId, rotation);
        _items.Add(new RoomItem
        {
            ItemId = itemId,
            Position = new Position3 { X = RoomLayout.SnapDecor(wx), Z = RoomLayout.SnapDecor(wz) },
            Rotation = world,
            Colours = colours,
        });
        _knownProblems = null;
        return this;
    }

    /// <summary>A small thing on the middle of the piece placed last (a lamp on a side table, books on a drum table).</summary>
    public StoreyDesigner DecorOnLast(string itemId, float rotation = 0f, string? colours = null)
    {
        var table = _items[^1];
        var definition = ItemDefinitions.Find(table.ItemId)!;
        var top = RoomLayout.SurfaceShape(table.ToDto(), definition);
        _items.Add(new RoomItem
        {
            ItemId = itemId,
            Position = new Position3 { X = RoomLayout.SnapDecor(top.CentreX), Z = RoomLayout.SnapDecor(top.CentreZ) },
            Rotation = WorldRotation(itemId, rotation),
            Colours = colours,
        });
        _knownProblems = null;
        return this;
    }

    /// <summary>Like <see cref="Put"/>, but only if it breaks no rule (facade greenery, fillers). Returns whether it was placed.</summary>
    public bool TryPut(string itemId, float x, float z, float rotation = 0f, string? colours = null)
    {
        var item = Place(itemId, x, z, rotation, colours);
        var dto = item.ToDto();
        var definition = ItemDefinitions.Find(itemId)!;

        // Quick checks first (most candidates fail here): inside the outline, not on another floor item.
        var dtos = _items.Select(i => i.ToDto()).ToList();
        var room = Context(dtos);
        var shape = RoomLayout.Shape(dto, definition).Grown(-0.02f);
        if (!(RoomLayout.IsQuarterTurn(dto.Rotation) ? room.IsInside(RoomLayout.Footprint(dto, definition)) : room.IsInside(shape)))
        {
            return false;
        }
        foreach (var other in dtos)
        {
            if (ItemDefinitions.Find(other.ItemId) is { } otherDefinition && otherDefinition.Kind == definition.Kind
                && RoomLayout.Shape(other, otherDefinition).Grown(-0.02f).Overlaps(shape))
            {
                return false;
            }
        }

        // Then the full rules. Accept it only if it adds no problem – comparing counts is not enough: some rules (seats
        // reachable) are only checked when nothing else is wrong, so a new overlap could hide older problems. What was
        // wrong before stays the same while only such items are added, so it is worked out once.
        _knownProblems ??= RoomLayout.Validate(room, dtos).Select(p => (p.Index, p.Message)).ToHashSet();
        dtos.Add(dto);
        if (RoomLayout.Validate(Context(dtos), dtos).Any(p => !_knownProblems.Contains((p.Index, p.Message))))
        {
            return false;
        }
        _items.Add(item);
        return true;
    }

    /// <summary>Problems of the layout before the current run of <see cref="TryPut"/> (null = unknown, after a plain Put).</summary>
    private HashSet<(int Index, string Message)>? _knownProblems;

    private RoomLayoutContext Context(IReadOnlyList<RoomItemDto> dtos) =>
        RoomZones.ContextFor("office", _plan.Width, _plan.Depth, dtos, _plan.Outline);

    private RoomItem Place(string itemId, float x, float z, float rotation, string? colours)
    {
        var definition = ItemDefinitions.Find(itemId) ?? throw new ArgumentException("Unknown item " + itemId);
        var (wx, wz) = ToWorld(x, z);
        var world = WorldRotation(itemId, rotation);
        if (!RoomLayout.TurnsFreely(definition))
        {
            world = RoomLayout.Quarter(world) * 90f;
        }
        RoomItemDto dto = new(itemId, new Vector3Dto(wx, 0f, wz), world, colours);
        dto = RoomLayout.Snap(dto);
        return new RoomItem
        {
            ItemId = itemId,
            Position = new Position3 { X = dto.Position.X, Z = dto.Position.Z },
            Rotation = dto.Rotation,
            Colours = colours,
        };
    }

    private float WorldRotation(string itemId, float rotation)
    {
        var turn = itemId.StartsWith("ph-", StringComparison.Ordinal) ? 180f : 0f;
        return RoomLayout.Normalize((float)Math.Round(_frames.Peek().Angle + rotation + turn));
    }

    // ---------- Groups (local frame: the group faces −Z) ----------

    /// <summary>
    /// Lounge island: rug, sofa behind the coffee table looking −Z, an armchair on each side, a side table with a lamp at
    /// the sofa's end, a floor lamp and a big plant.
    /// </summary>
    public StoreyDesigner Lounge(float x, float z, LoungeStyle style, bool armchairs = true, bool extras = true) => Group(x, z, 0f, d =>
    {
        var sofaHalf = SofaHalfWidth(style.Sofa) + d.Slack;
        if (!string.IsNullOrEmpty(style.Rug))
        {
            d.Put(style.Rug, 0f, 0.1f, 0f, style.RugColours);   // none when the lounge stands on a bigger floor zone
        }
        d.Put(style.Table, 0f, 0f, 0f, style.TableColours);
        d.Put(style.Sofa, 0f, 1.35f + d.Slack, 0f, style.SofaColours);
        if (armchairs)
        {
            d.Put(style.Chair, -1.9f - d.Slack, 0f, 270f, style.ChairColours);
            d.Put(style.Chair, 1.9f + d.Slack, 0f, 90f, style.ChairColours);
        }
        if (!extras)
        {
            return;
        }
        d.Put("custom-sidetable-round", sofaHalf + 0.55f, 1.45f, 0f, "oak/black")
         .DecorOnLast("custom-tablelamp-dome", 0f, "warm-white/brass");
        d.Put(style.Lamp, -sofaHalf - 0.45f, 1.5f, 0f, style.LampColours);
        d.Put(style.Plant, -sofaHalf - 0.35f, 2.75f + d.Slack, 0f, style.PlantColours);
    });

    private static float SofaHalfWidth(string sofa) =>
        ItemDefinitions.Find(sofa) is { } definition ? definition.Width * BuildGrid.CellSize / 2f : 1.2f;

    /// <summary>Dining / meeting table along local X with chairs on both long sides (and at the ends if asked).</summary>
    public StoreyDesigner Dining(float x, float z, string table, string? tableColours, string chair, string? chairColours,
        int perSide, bool ends = false, string? pendant = "custom-pendantlamp-linear", string? pendantColours = null) => Group(x, z, 0f, d =>
    {
        var definition = ItemDefinitions.Find(table)!;
        var length = definition.Width * BuildGrid.CellSize;
        var depth = definition.Depth * BuildGrid.CellSize;
        d.Put(table, 0f, 0f, 0f, tableColours);
        for (var i = 0; i < perSide; i++)
        {
            var cx = -length / 2f + (i + 0.5f) * length / perSide;
            d.Put(chair, cx, depth / 2f + 0.3f + d.Slack, 0f, chairColours);
            d.Put(chair, cx, -depth / 2f - 0.3f - d.Slack, 180f, chairColours);
        }
        if (ends)
        {
            d.Put(chair, -length / 2f - 0.3f - d.Slack, 0f, 270f, chairColours);
            d.Put(chair, length / 2f + 0.3f + d.Slack, 0f, 90f, chairColours);
        }
        if (pendant != null)
        {
            d.Put(pendant, 0f, 0f, 0f, pendantColours);
        }
    });

    /// <summary>Round table for two with chairs left and right.</summary>
    public StoreyDesigner Bistro(float x, float z, string chair = "custom-chair-cantilever", string? chairColours = null,
        string table = "custom-bistro", string? tableColours = null) => Group(x, z, 0f, d =>
    {
        d.Put(table, 0f, 0f, 0f, tableColours);
        d.Put(chair, -0.7f - d.Slack, 0f, 270f, chairColours);
        d.Put(chair, 0.7f + d.Slack, 0f, 90f, chairColours);
    });

    /// <summary>Round table with <paramref name="chairs"/> chairs around it (2 or 4), optional pendant above.</summary>
    public StoreyDesigner RoundDining(float x, float z, string table, string? tableColours, string chair, string? chairColours, int chairs = 4,
        string? pendant = null, string? pendantColours = null) => Group(x, z, 0f, d =>
    {
        var definition = ItemDefinitions.Find(table)!;
        var reach = definition.Width * BuildGrid.CellSize / 2f + 0.3f + d.Slack;
        d.Put(table, 0f, 0f, 0f, tableColours);
        var sides = chairs == 2 ? new[] { (-1f, 0f, 270f), (1f, 0f, 90f) } : new[] { (-1f, 0f, 270f), (1f, 0f, 90f), (0f, 1f, 0f), (0f, -1f, 180f) };
        foreach (var (sx, sz, rotation) in sides)
        {
            d.Put(chair, sx * reach, sz * reach, rotation, chairColours);
        }
        if (pendant != null)
        {
            d.Put(pendant, 0f, 0f, 0f, pendantColours);
        }
    });

    /// <summary>Standing table with two bar stools.</summary>
    public StoreyDesigner HighTable(float x, float z, string? colours = null, string stool = "custom-barstool-back", string? stoolColours = null) =>
        Group(x, z, 0f, d =>
        {
            d.Put("custom-hightable", 0f, 0f, 0f, colours);
            d.Put(stool, 0f, 0.65f + d.Slack, 0f, stoolColours);
            d.Put(stool, 0f, -0.65f - d.Slack, 180f, stoolColours);
        });

    /// <summary>Banquette along local X (back at +Z) with square bistro tables and chairs opposite.</summary>
    public StoreyDesigner BanquetteRow(float x, float z, int length, string? colours, string chair, string? chairColours, string? tableColours = null) =>
        Group(x, z, 0f, d =>
        {
            d.Put($"custom-banquette-{length}", 0f, 0.35f, 0f, colours);
            var tables = Math.Max(1, length / 110);   // one table per 1.1 m of bench
            for (var i = 0; i < tables; i++)
            {
                var tx = -length / 200f + (i + 0.5f) * length / 100f / tables;
                d.Put("custom-bistro-square", tx, -0.65f - d.Slack, 0f, tableColours);
                d.Put(chair, tx, -1.4f - 2f * d.Slack, 180f, chairColours);
            }
        });

    /// <summary>Reading nook: lounge chair, side table with a lamp, floor lamp, plant.</summary>
    public StoreyDesigner Reading(float x, float z, string? chairColours = null) => Group(x, z, 0f, d =>
    {
        d.Put("custom-lounge-shell", 0f, 0f, 0f, chairColours);
        d.Put("custom-sidetable-drum", 1.0f + d.Slack, 0.1f)
         .DecorOnLast("custom-books", 15f, "sage");
        d.Put("custom-floorlamp-tripod", -1.05f - d.Slack, 0.3f);
        d.Put("custom-plant-bush-bowl", -1.1f - d.Slack, 1.45f + d.Slack);
    });

    /// <summary>A row of <paramref name="count"/> items along local X, <paramref name="pitch"/> metres apart.</summary>
    public StoreyDesigner Row(string itemId, float x, float z, int count, float pitch, float rotation = 0f, string? colours = null)
    {
        for (var i = 0; i < count; i++)
        {
            Put(itemId, x + i * pitch, z, rotation, colours);
        }
        return this;
    }

    /// <summary>
    /// Glass-walled room from (x0, z0) to (x1, z1) in world metres (whole metres work best), with a door gap about 2 m wide
    /// in the middle of the <paramref name="door"/> side ("north", "east", "south", "west").
    /// </summary>
    public StoreyDesigner GlassRoom(float x0, float z0, float x1, float z1, string door)
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
                    Put("custom-glasswall", ax + t, az);
                }
                else
                {
                    // Side walls stand just outside the corners so they don't cut into the front and back walls.
                    Put("custom-glasswall", ax + (ax <= Math.Min(x0, x1) ? -0.125f : 0.125f), az + t, 90f);
                }
            }
        }
        Wall(x0, z0, x1, z0, door == "south");
        Wall(x0, z1, x1, z1, door == "north");
        Wall(x0, z0, x0, z1, door == "west");
        Wall(x1, z0, x1, z1, door == "east");
        return this;
    }

    /// <summary>Desk island: desks in two rows facing each other, office chairs, screens and plants.</summary>
    public StoreyDesigner WorkIsland(float x, float z, int desks, string? deskColours = null) => Group(x, z, 0f, d =>
    {
        const float width = 1.55f;   // 1.4 m desks, a little air between them
        for (var i = 0; i < desks; i++)
        {
            var dx = -desks * width / 2f + (i + 0.5f) * width;
            d.Put("custom-desk-140", dx, 0.5f, 0f, deskColours);
            d.Put("custom-desk-140", dx, -0.5f, 180f, deskColours);
            d.Put("custom-officechair", dx, 1.3f, 0f);
            d.Put("custom-officechair", dx, -1.3f, 180f);
            d.Decor(i % 2 == 0 ? "laptop" : "ph-classic_laptop", dx - 0.15f, 0.5f, 0f);
            d.Decor("computerScreen", dx - 0.2f, -0.6f, 180f);
            if (i % 2 == 1)
            {
                d.Decor("custom-tablelamp-mushroom", dx + 0.45f, 0.55f, 0f, "sage/black");
            }
        }
    });
}
