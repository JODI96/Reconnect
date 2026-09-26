using System;
using System.Collections.Generic;
using System.Linq;
using Reconnect.Contracts.Rooms;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// The build editor's state (plain C#, no Unity): the working layout, the item in hand (new from the catalog or
    /// picked up from the room, with the small things standing on it), undo. Every step follows the shared build rules
    /// (<see cref="RoomLayout"/>): positions snap to the 50 cm grid, paintings stick to walls, an item can only be put
    /// down where it is allowed. RoomScreen shows it (<see cref="BuildPreview"/>) and saves <see cref="Items"/>.
    /// </summary>
    public sealed class BuildEditor
    {
        private readonly string _theme;
        private readonly int _width;
        private readonly int _depth;
        private readonly Stack<List<RoomItemDto>> _undo = new();
        private List<RoomItemDto> _items;
        private List<RoomItemDto> _beforeSelection;
        private readonly List<(float Dx, float Dz, RoomItemDto Item)> _carried = new();
        private bool _selectionIsNew;

        public BuildEditor(string theme, int width, int depth, IReadOnlyList<RoomItemDto> layout)
        {
            _theme = theme;
            _width = width;
            _depth = depth;
            _items = (layout ?? Array.Empty<RoomItemDto>()).ToList();
            if (RoomLayout.Validate(Context(_items), _items).Count > 0)
            {
                // Saved before the build grid existed: move everything to the nearest legal place.
                _items = RoomLayoutFixer.Legalize(theme, width, depth, _items);
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

        public RoomLayoutContext Context(IReadOnlyList<RoomItemDto> items) => RoomZones.ContextFor(_theme, _width, _depth, items);

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
                .Where(i => i.Definition != null && RoomLayout.Footprint(i.Item, i.Definition).Contains(cellX, cellZ))
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
            var cells = RoomLayout.Footprint(Selection, definition);
            var taken = new HashSet<int> { index };
            if (definition.Kind == ItemKind.Floor && definition.HasSurface)
            {
                for (var i = 0; i < _items.Count; i++)
                {
                    var other = ItemDefinitions.Find(_items[i].ItemId);
                    if (other?.Kind == ItemKind.Decor && cells.Contains(RoomLayout.Footprint(_items[i], other)))
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
            if (definition.Kind == ItemKind.Wall)
            {
                var near = RoomLayoutFixer.OntoNearestWall(context, RoomLayout.Footprint(definition, x, z, RoomLayout.Quarter(rotation)));
                var wall = RoomLayout.WallOf(context, near);
                if (wall != WallSides.None)
                {
                    rotation = RoomLayout.FacingIntoRoom(Selection.ItemId, wall);
                }
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
            if (Selection == null || SelectionDefinition.Kind == ItemKind.Wall)
            {
                return;
            }
            // Things on it turn around its centre: +90° around Y maps (x, z) to (z, -x).
            for (var i = 0; i < _carried.Count; i++)
            {
                var (dx, dz, item) = _carried[i];
                _carried[i] = (dz, -dx, item with { Rotation = (item.Rotation + 90f) % 360f });
            }
            Selection = Selection with { Rotation = (RoomLayout.Quarter(Selection.Rotation) + 1) % 4 * 90f };
            MoveSelectionTo(Selection.Position.X, Selection.Position.Z);
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
            Selection = selection;
            for (var i = 0; i < _carried.Count; i++)
            {
                var (dx, dz, item) = _carried[i];
                var definition = ItemDefinitions.Find(item.ItemId);
                var cells = RoomLayout.Footprint(definition, selection.Position.X + dx, selection.Position.Z + dz, RoomLayout.Quarter(item.Rotation));
                var (cx, cz) = RoomLayout.Centre(cells);
                _carried[i] = (dx, dz, item with { Position = new Vector3Dto(cx, 0f, cz) });
            }
            SelectionChanged?.Invoke();
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
