using System;
using System.Collections.Generic;
using System.Linq;
using Reconnect.Contracts.Rooms;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// The build editor's state (plain C#, no Unity): the working layout, the item in hand (new from the catalog or
    /// picked up from the room, with the small things standing on it), undo. Every step follows the shared build rules
    /// (<see cref="RoomLayout"/>): positions snap to the 25 cm grid (turned furniture: its centre to 12.5 cm), paintings
    /// stick to walls, an item can only be put down where it is allowed. Furniture turns to any whole degree – aimed with
    /// the finger (<see cref="AimSelectionAt"/>), snapping onto the angle of a nearby wall. RoomScreen shows it
    /// (<see cref="BuildPreview"/>) and saves <see cref="Items"/>.
    /// </summary>
    public sealed class BuildEditor
    {
        private readonly string _theme;
        private readonly int _width;
        private readonly int _depth;
        private readonly IReadOnlyList<RoomPointDto> _outline;
        private readonly Stack<List<RoomItemDto>> _undo = new();
        private List<RoomItemDto> _items;
        private List<RoomItemDto> _beforeSelection;
        private readonly List<(float Dx, float Dz, RoomItemDto Item)> _carried = new();
        private bool _selectionIsNew;

        /// <param name="outline">Floor outline of tower storeys (null = rectangle).</param>
        public BuildEditor(string theme, int width, int depth, IReadOnlyList<RoomItemDto> layout, IReadOnlyList<RoomPointDto> outline = null)
        {
            _theme = theme;
            _width = width;
            _depth = depth;
            _outline = outline;
            _items = (layout ?? Array.Empty<RoomItemDto>()).ToList();
            if (RoomLayout.Validate(Context(_items), _items).Count > 0)
            {
                // Saved before the build grid existed: move everything to the nearest legal place.
                _items = RoomLayoutFixer.Legalize(theme, width, depth, _items, outline: outline);
                Notice = "Der Raum wurde aufs Bauraster gesetzt – „Speichern“ übernimmt es.";
                IsDirty = true;
            }
        }

        /// <summary>The layout without the item in hand (what the room shows).</summary>
        public IReadOnlyList<RoomItemDto> Items => _items;

        /// <summary>The item in hand (ghost), or null.</summary>
        public RoomItemDto Selection { get; private set; }

        public ItemDefinition SelectionDefinition => Selection == null ? null : ItemDefinitions.Find(Selection.ItemId);

        /// <summary>Small things riding on the item in hand (they move and turn with their table).</summary>
        public IReadOnlyList<RoomItemDto> Carried => _carried.Select(c => c.Item).ToList();

        public bool HasSelection => Selection != null;
        public bool SelectionIsNew => HasSelection && _selectionIsNew;
        public bool IsDirty { get; private set; }
        public bool CanUndo => _undo.Count > 0 || (HasSelection && !_selectionIsNew);

        /// <summary>One-off hint for the user (e.g. the room was moved onto the grid).</summary>
        public string Notice { get; private set; }

        /// <summary>Why the item in hand can't be put down here (empty = it can).</summary>
        public IReadOnlyList<string> SelectionProblems
        {
            get
            {
                if (Selection == null)
                {
                    return Array.Empty<string>();
                }
                var all = WithSelection();
                return RoomLayout.Validate(Context(all), all).Select(p => p.Message).Distinct().ToList();
            }
        }

        public bool SelectionIsValid => HasSelection && SelectionProblems.Count == 0;

        /// <summary>The layout changed (the room has to be rebuilt).</summary>
        public event Action LayoutChanged;

        /// <summary>The item in hand moved, turned, appeared or went away.</summary>
        public event Action SelectionChanged;

        public RoomLayoutContext Context(IReadOnlyList<RoomItemDto> items) => RoomZones.ContextFor(_theme, _width, _depth, items, _outline);

        /// <summary>Takes a new item from the catalog, standing at (x, z) metres, facing the camera.</summary>
        public void Pick(string itemId, float x, float z)
        {
            if (ItemDefinitions.Find(itemId) == null)
            {
                throw new ArgumentException("Unknown item " + itemId, nameof(itemId));
            }
            CancelSelection();
            _beforeSelection = _items.ToList();
            _selectionIsNew = true;
            Selection = new RoomItemDto(itemId, new Vector3Dto(x, 0f, z), RoomLayout.FacingIntoRoom(itemId, WallSides.North));
            MoveSelectionTo(x, z);
        }

        /// <summary>
        /// Picks up the item at (x, z) metres: small things before paintings before furniture before lamps before rugs.
        /// A table takes what stands on it along. False if nothing is there.
        /// </summary>
        public bool SelectAt(float x, float z)
        {
            CancelSelection();
            var cellX = (int)Math.Floor(x / BuildGrid.CellSize);
            var cellZ = (int)Math.Floor(z / BuildGrid.CellSize);
            var hit = _items
                .Select((item, index) => (Item: item, Index: index, Definition: ItemDefinitions.Find(item.ItemId)))
                .Where(i => i.Definition != null && !RoomZones.IsFixed(i.Item.ItemId) && (i.Definition.Kind == ItemKind.Decor
                    ? RoomLayout.DecorShape(i.Item, i.Definition).Contains(x, z, 0.15f)
                    : RoomLayout.IsQuarterTurn(i.Item.Rotation)
                        ? RoomLayout.Footprint(i.Item, i.Definition).Contains(cellX, cellZ)
                        : RoomLayout.Shape(i.Item, i.Definition).Contains(x, z)))
                .OrderBy(i => Priority(i.Definition.Kind))
                .Select(i => (int?)i.Index)
                .FirstOrDefault();
            if (hit is not int index)
            {
                return false;
            }

            _beforeSelection = _items.ToList();
            _selectionIsNew = false;
            Selection = _items[index];
            var definition = SelectionDefinition;
            var taken = new HashSet<int> { index };
            if (definition.Kind == ItemKind.Floor && definition.HasSurface)
            {
                var top = RoomLayout.SurfaceShape(Selection, definition);
                for (var i = 0; i < _items.Count; i++)
                {
                    var other = ItemDefinitions.Find(_items[i].ItemId);
                    if (other?.Kind == ItemKind.Decor && top.Contains(RoomLayout.DecorShape(_items[i], other), 0.02f))
                    {
                        taken.Add(i);
                        _carried.Add((_items[i].Position.X - Selection.Position.X, _items[i].Position.Z - Selection.Position.Z, _items[i]));
                    }
                }
            }
            _items = _items.Where((_, i) => !taken.Contains(i)).ToList();
            LayoutChanged?.Invoke();
            SelectionChanged?.Invoke();
            return true;
        }

        /// <summary>Moves the item in hand so its centre is as close as possible to (x, z); paintings stick to the nearest wall.</summary>
        public void MoveSelectionTo(float x, float z)
        {
            if (Selection == null)
            {
                return;
            }
            var definition = SelectionDefinition;
            var context = Context(_items);
            var rotation = Selection.Rotation;
            if (definition.Kind == ItemKind.Decor)
            {
                SetSelection(new RoomItemDto(Selection.ItemId, new Vector3Dto(x, 0f, z), rotation));
                return;
            }
            if (definition.Kind == ItemKind.Wall)
            {
                var near = RoomLayoutFixer.OntoNearestWall(context, RoomLayout.Footprint(definition, x, z, RoomLayout.Quarter(rotation)));
                var wall = RoomLayout.WallOf(context, near);
                if (wall != WallSides.None)
                {
                    rotation = RoomLayout.FacingIntoRoom(Selection.ItemId, wall);
                }
            }
            if (!RoomLayout.IsQuarterTurn(rotation))
            {
                // Turned furniture: its centre on the 12.5 cm grid, kept inside the room's bounds.
                var shape = RoomLayout.Shape(Selection with { Position = new Vector3Dto(x, 0f, z) }, definition).Bounds;
                var halfX = (shape.MaxX - shape.MinX) / 2f;
                var halfZ = (shape.MaxZ - shape.MinZ) / 2f;
                x = Math.Max(halfX, Math.Min(_width - halfX, x));
                z = Math.Max(halfZ, Math.Min(_depth - halfZ, z));
                SetSelection(new RoomItemDto(Selection.ItemId, new Vector3Dto(RoomLayout.SnapDecor(x), 0f, RoomLayout.SnapDecor(z)), rotation));
                return;
            }
            var cells = RoomLayout.Footprint(definition, x, z, RoomLayout.Quarter(rotation));
            if (definition.Kind == ItemKind.Wall)
            {
                cells = RoomLayoutFixer.OntoNearestWall(context, cells);
            }
            cells = Inside(cells, context.Cells);
            var (cx, cz) = RoomLayout.Centre(cells);
            SetSelection(new RoomItemDto(Selection.ItemId, new Vector3Dto(cx, 0f, cz), rotation));
        }

        /// <summary>Turns the item in hand a quarter turn (paintings turn with their wall instead).</summary>
        public void Rotate()
        {
            if (Selection == null)
            {
                return;
            }
            TurnSelectionTo((float)(Math.Floor(Selection.Rotation / 90f + 1e-3) + 1) * 90f);   // next quarter turn
        }

        /// <summary>How close (degrees) an aimed item snaps onto a wall's angle or a quarter turn.</summary>
        public const float SnapDegrees = 4f;

        /// <summary>
        /// Turns the item in hand so it faces the point (x, z) – drag the finger to the right and it looks right. Snaps
        /// onto quarter turns and onto the angle of the room's walls (slanted facades) within <see cref="SnapDegrees"/>,
        /// otherwise any whole degree. Items that only turn in quarter turns take the nearest one.
        /// </summary>
        public void AimSelectionAt(float x, float z)
        {
            if (Selection == null || SelectionDefinition.Kind == ItemKind.Wall)
            {
                return;
            }
            var centre = RoomLayout.Shape(Selection, SelectionDefinition);
            var dx = x - centre.CentreX;
            var dz = z - centre.CentreZ;
            if (dx * dx + dz * dz < 0.04f)
            {
                return;   // finger right on the item: no clear direction yet
            }
            var wanted = RoomLayout.RotationFacing(Selection.ItemId, dx, dz);
            var best = wanted;
            var bestDelta = SnapDegrees;
            foreach (var angle in WallAngles())
            {
                var delta = Math.Abs(Delta(wanted, angle));
                if (delta <= bestDelta)
                {
                    best = angle;
                    bestDelta = delta;
                }
            }
            TurnSelectionTo(best);
        }

        /// <summary>Turns the item in hand to <paramref name="rotation"/> degrees (whole degrees; what stands on it turns along).</summary>
        public void TurnSelectionTo(float rotation)
        {
            if (Selection == null || SelectionDefinition.Kind == ItemKind.Wall)
            {
                return;
            }
            var definition = SelectionDefinition;
            var target = RoomLayout.SnapRotation(definition, rotation);
            var delta = Delta(target, Selection.Rotation);
            if (Math.Abs(delta) < 0.01f)
            {
                return;
            }
            // Things on it turn around its centre (Unity: +r maps (x, z) to (x cos r + z sin r, −x sin r + z cos r)).
            var radians = delta * Math.PI / 180.0;
            var cos = (float)Math.Cos(radians);
            var sin = (float)Math.Sin(radians);
            for (var i = 0; i < _carried.Count; i++)
            {
                var (dx, dz, item) = _carried[i];
                _carried[i] = (dx * cos + dz * sin, -dx * sin + dz * cos,
                    item with { Rotation = RoomLayout.SnapRotation(ItemDefinitions.Find(item.ItemId), item.Rotation + delta) });
            }
            var centre = RoomLayout.Shape(Selection, definition);
            Selection = Selection with { Rotation = target };
            MoveSelectionTo(centre.CentreX, centre.CentreZ);
        }

        /// <summary>Angles at which furniture stands parallel to a wall: quarter turns and every facade edge (+ quarter turns).</summary>
        public IReadOnlyList<float> WallAngles()
        {
            var angles = new List<float> { 0f, 90f, 180f, 270f };
            if (_outline != null)
            {
                for (var i = 0; i < _outline.Count; i++)
                {
                    var a = _outline[i];
                    var b = _outline[(i + 1) % _outline.Count];
                    if (Math.Abs(b.X - a.X) + Math.Abs(b.Z - a.Z) < 1f)
                    {
                        continue;   // tiny edges don't count as walls
                    }
                    // Item X axis (cos r, −sin r) parallel to the edge.
                    var edge = RoomLayout.Normalize((float)Math.Round(-Math.Atan2(b.Z - a.Z, b.X - a.X) * 180.0 / Math.PI));
                    for (var k = 0; k < 4; k++)
                    {
                        angles.Add(RoomLayout.Normalize(edge + k * 90f));
                    }
                }
            }
            return angles.Distinct().ToList();
        }

        /// <summary>Signed smallest difference a − b in degrees (−180 … 180).</summary>
        private static float Delta(float a, float b)
        {
            var d = RoomLayout.Normalize(a - b);
            return d > 180f ? d - 360f : d;
        }

        /// <summary>Puts the item in hand down (with what stands on it). False if a rule is broken there.</summary>
        public bool Place()
        {
            if (Selection == null || SelectionProblems.Count > 0)
            {
                return false;
            }
            _undo.Push(_beforeSelection);
            _items = WithSelection();
            ClearSelection();
            IsDirty = true;
            LayoutChanged?.Invoke();
            SelectionChanged?.Invoke();
            return true;
        }

        /// <summary>Throws the item in hand away (a picked-up table together with what stood on it).</summary>
        public void Delete()
        {
            if (Selection == null)
            {
                return;
            }
            if (!_selectionIsNew)
            {
                _undo.Push(_beforeSelection);
                IsDirty = true;
            }
            ClearSelection();
            LayoutChanged?.Invoke();
            SelectionChanged?.Invoke();
        }

        /// <summary>Puts a picked-up item back where it was; a new one from the catalog simply goes away.</summary>
        public void CancelSelection()
        {
            if (Selection == null)
            {
                return;
            }
            var wasPickedUp = !_selectionIsNew;
            _items = _beforeSelection;
            ClearSelection();
            if (wasPickedUp)
            {
                LayoutChanged?.Invoke();
            }
            SelectionChanged?.Invoke();
        }

        public void Undo()
        {
            if (Selection != null && !_selectionIsNew)
            {
                CancelSelection();
                return;
            }
            CancelSelection();
            if (_undo.Count == 0)
            {
                return;
            }
            _items = _undo.Pop();
            IsDirty = true;
            LayoutChanged?.Invoke();
        }

        /// <summary>After saving: the saved layout is the new starting point.</summary>
        public void MarkSaved()
        {
            IsDirty = false;
            Notice = null;
        }

        private List<RoomItemDto> WithSelection()
        {
            var all = _items.ToList();
            all.Add(Selection);
            all.AddRange(Carried);
            return all;
        }

        private void SetSelection(RoomItemDto selection)
        {
            var definition = ItemDefinitions.Find(selection.ItemId);
            if (definition.Kind == ItemKind.Decor)
            {
                selection = OntoTable(selection, definition);
            }
            Selection = selection;
            for (var i = 0; i < _carried.Count; i++)
            {
                var (dx, dz, item) = _carried[i];
                _carried[i] = (dx, dz, item with
                {
                    Position = new Vector3Dto(RoomLayout.SnapDecor(selection.Position.X + dx), 0f, RoomLayout.SnapDecor(selection.Position.Z + dz)),
                });
            }
            SelectionChanged?.Invoke();
        }

        /// <summary>A small thing on the fine grid, pushed fully onto the table top under it (if there is one).</summary>
        private RoomItemDto OntoTable(RoomItemDto item, ItemDefinition definition)
        {
            var x = RoomLayout.SnapDecor(item.Position.X);
            var z = RoomLayout.SnapDecor(item.Position.Z);
            var tops = _items
                .Select(i => (Item: i, Definition: ItemDefinitions.Find(i.ItemId)))
                .Where(i => i.Definition is { Kind: ItemKind.Floor, HasSurface: true })
                .Select(i => RoomLayout.SurfaceShape(i.Item, i.Definition))
                .Where(top => top.Contains(item.Position.X, item.Position.Z, 0.25f))
                .OrderBy(t => Math.Abs(t.CentreX - item.Position.X) + Math.Abs(t.CentreZ - item.Position.Z))
                .ToList();
            if (tops.Count > 0 && !RoomLayout.IsQuarterTurn(tops[0].Rotation))
            {
                // Turned table: turn along with it and stay on its top (measured in the top's own axes).
                var turned = tops[0];
                var rotation = RoomLayout.SnapRotation(definition, turned.Rotation + RoomLayout.Quarter(item.Rotation - turned.Rotation) * 90f);
                var local = turned.ToLocal(item.Position.X, item.Position.Z);
                var shape = RoomLayout.DecorShape(item with { Rotation = rotation }, definition);
                var across = RoomLayout.Quarter(rotation - turned.Rotation) % 2 == 1;
                var halfX = across ? shape.HalfZ : shape.HalfX;
                var halfZ = across ? shape.HalfX : shape.HalfZ;
                var lx = halfX > turned.HalfX ? 0f : Math.Max(-turned.HalfX + halfX, Math.Min(turned.HalfX - halfX, local.X));
                var lz = halfZ > turned.HalfZ ? 0f : Math.Max(-turned.HalfZ + halfZ, Math.Min(turned.HalfZ - halfZ, local.Z));
                var (px, pz) = turned.ToRoom(lx, lz);
                return item with { Position = new Vector3Dto(RoomLayout.SnapDecor(px), 0f, RoomLayout.SnapDecor(pz)), Rotation = rotation };
            }
            if (tops.Count > 0)
            {
                var top = tops[0].Bounds;
                var area = RoomLayout.DecorArea(item with { Position = new Vector3Dto(x, 0f, z) }, definition);
                var halfX = (area.MaxX - area.MinX) / 2f;
                var halfZ = (area.MaxZ - area.MinZ) / 2f;
                // Nearest fine-grid point that keeps it on the top (a thing wider than the top stays centred).
                x = Clamp(x, top.MinX + halfX, top.MaxX - halfX, top.CentreX);
                z = Clamp(z, top.MinZ + halfZ, top.MaxZ - halfZ, top.CentreZ);
            }
            return item with { Position = new Vector3Dto(x, 0f, z) };
        }

        private static float Clamp(float value, float min, float max, float centre)
        {
            var step = BuildGrid.DecorStep;
            var low = (float)Math.Ceiling((min - 0.0001f) / step) * step;
            var high = (float)Math.Floor((max + 0.0001f) / step) * step;
            return low > high ? RoomLayout.SnapDecor(centre) : Math.Max(low, Math.Min(high, value));
        }

        private void ClearSelection()
        {
            Selection = null;
            _carried.Clear();
            _beforeSelection = null;
        }

        private static CellRect Inside(CellRect cells, CellRect room)
        {
            var x = Math.Max(room.X, Math.Min(cells.X, room.XMax - cells.Width));
            var z = Math.Max(room.Z, Math.Min(cells.Z, room.ZMax - cells.Depth));
            return new CellRect(x, z, cells.Width, cells.Depth);
        }

        private static int Priority(ItemKind kind) => kind switch
        {
            ItemKind.Decor => 0,
            ItemKind.Wall => 1,
            ItemKind.Floor => 2,
            ItemKind.Ceiling => 3,
            _ => 4,
        };
    }
}
