using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Screens
{
    /// <summary>
    /// The build editor's panel in the room (owner or admin): catalog by category, the item in hand (turn, remove,
    /// put down), undo, save. Drag the item in hand or tap where it should go; tap an item in the room to pick it up.
    /// Rules come from <see cref="BuildEditor"/>; the room shows every step at once.
    /// </summary>
    public sealed class BuildPanel
    {
        private static readonly string[] Categories =
        {
            "Sofas & Sessel", "Stühle & Hocker", "Tische", "Aufbewahrung", "Licht", "Pflanzen", "Teppiche & Böden", "Deko", "Arbeiten",
            "Küche & Bar", "Wohnen", "Schlafen", "Bad & Spa", "Fitness", "Gaming", "Wände", "Kunst", "Ab 18",
            "Wand", "Spiele", "Spezial", "Klassisch", "Retro",
        };

        private readonly VisualElement _panel;
        private readonly RoomView _room;
        private readonly BuildPreview _preview;
        private readonly Func<IReadOnlyList<RoomItemDto>, Task<(bool Ok, string Error)>> _save;
        private readonly Action _closed;
        private readonly Label _hint;
        private readonly VisualElement _actions;
        private readonly ScrollView _categories;
        private readonly ScrollView _items;
        private readonly VisualElement _colours;
        private readonly VisualElement _zones;
        private readonly ScrollView _swatches;
        private int _zone;
        private string _colouredItem;
        private readonly bool _isAdmin;
        private BuildEditor _editor;
        private IReadOnlyList<RoomItemDto> _original;
        private string _category = Categories[0];
        private string _message;
        private bool _saving;

        /// <summary>"Drehen" is on: dragging turns the item in hand towards the finger instead of moving it.</summary>
        private bool _turning;

        public BuildPanel(VisualElement root, RoomView room, Material transparent, bool isAdmin,
            Func<IReadOnlyList<RoomItemDto>, Task<(bool Ok, string Error)>> save, Action closed)
        {
            _panel = root.Q<VisualElement>("build-panel");
            _room = room;
            _preview = new BuildPreview(room, transparent);
            _isAdmin = isAdmin;
            _save = save;
            _closed = closed;
            _hint = _panel.Q<Label>("build-hint");
            _actions = _panel.Q<VisualElement>("build-actions");
            _categories = _panel.Q<ScrollView>("build-categories");
            _items = _panel.Q<ScrollView>("build-items");
            _colours = _panel.Q<VisualElement>("build-colours");
            _zones = _panel.Q<VisualElement>("build-zones");
            _swatches = _panel.Q<ScrollView>("build-swatches");
            foreach (var scroll in new[] { _categories, _items, _swatches })
            {
                scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            }

            _panel.Q<Button>("build-rotate").clicked += () =>
            {
                _turning = !_turning && _editor is { HasSelection: true };
                Refresh();
            };
            _panel.Q<Button>("build-delete").clicked += () => _editor?.Delete();
            _panel.Q<Button>("build-cancel").clicked += () => _editor?.CancelSelection();
            _panel.Q<Button>("build-place").clicked += Place;
            _panel.Q<Button>("build-undo").clicked += () => _editor?.Undo();
            _panel.Q<Button>("build-discard").clicked += Discard;
            _panel.Q<Button>("build-save").clicked += () => _ = SaveAsync();
            BuildCategories();
        }

        public bool IsOpen => _editor != null;

        /// <summary>The editor while open (tests drive it directly).</summary>
        public BuildEditor Editor => _editor;

        public void Open(string theme)
        {
            _original = _room.Layout;
            _editor = new BuildEditor(theme, _room.Width, _room.Depth, _room.Layout, _room.Outline);
            _editor.LayoutChanged += () =>
            {
                _room.ApplyLayout(_editor.Items);
                Refresh();
            };
            _editor.SelectionChanged += Refresh;
            _message = _editor.Notice;
            if (_editor.IsDirty)
            {
                _room.ApplyLayout(_editor.Items);
            }
            _room.BuildMode = true;
            _room.BuildPointer = OnPointer;
            _preview.Show(_editor.Context(_editor.Items));
            _panel.style.display = DisplayStyle.Flex;
            ShowCategory(_category);
            Refresh();
        }

        /// <summary>Leaves build mode; the room shows <paramref name="layout"/> (saved or the one from before).</summary>
        public void Close(IReadOnlyList<RoomItemDto> layout)
        {
            if (_editor == null)
            {
                return;
            }
            _editor = null;
            _preview.Hide();
            _room.BuildMode = false;
            _room.BuildPointer = null;
            _room.ApplyLayout(layout);
            _panel.style.display = DisplayStyle.None;
            _closed();
        }

        // ---------- Pointer in the room ----------

        private bool OnPointer(BuildPointerPhase phase, Vector3 point)
        {
            if (_editor == null)
            {
                return false;
            }
            if (_turning && _editor.HasSelection)
            {
                // Turn mode: the item looks where the finger is (1° steps, snapping onto walls).
                _editor.AimSelectionAt(point.x, point.z);
                return true;
            }
            switch (phase)
            {
                case BuildPointerPhase.Tap:
                    if (_editor.HasSelection)
                    {
                        _editor.MoveSelectionTo(point.x, point.z);
                    }
                    else
                    {
                        _message = _editor.SelectAt(point.x, point.z) ? null : "Wähle unten etwas aus oder tippe auf einen Gegenstand im Raum.";
                        Refresh();
                    }
                    return true;
                case BuildPointerPhase.DragStart:
                    // Drag the item in hand – or pick up the one under the finger. Anything else pans the camera.
                    if (_editor.HasSelection)
                    {
                        var cells = RoomLayout.Footprint(_editor.Selection, _editor.SelectionDefinition);
                        var (cx, cz) = RoomLayout.Centre(cells);
                        var reach = Mathf.Max(cells.Width, cells.Depth) * BuildGrid.CellSize / 2f + 0.5f;
                        if (Mathf.Abs(point.x - cx) > reach || Mathf.Abs(point.z - cz) > reach)
                        {
                            return false;
                        }
                        _editor.MoveSelectionTo(point.x, point.z);
                        return true;
                    }
                    return _editor.SelectAt(point.x, point.z);
                default:
                    _editor.MoveSelectionTo(point.x, point.z);
                    return true;
            }
        }

        // ---------- Buttons ----------

        private void Place()
        {
            if (_editor == null)
            {
                return;
            }
            if (!_editor.Place())
            {
                _message = _editor.SelectionProblems.FirstOrDefault();
            }
            else
            {
                _turning = false;
            }
            Refresh();
        }

        /// <summary>Leaves without saving: the room looks as before.</summary>
        private void Discard() => Close(_original);

        private async Task SaveAsync()
        {
            if (_editor == null || _saving)
            {
                return;
            }
            _editor.CancelSelection();
            if (!_editor.IsDirty)
            {
                Close(_editor.Items);
                return;
            }
            _saving = true;
            _message = "Speichere …";
            Refresh();
            try
            {
                var layout = _editor.Items.ToList();
                var (ok, error) = await _save(layout);
                if (_editor == null)
                {
                    return;
                }
                if (ok)
                {
                    _editor.MarkSaved();
                    Close(layout);
                    return;
                }
                _message = error;
                Refresh();
            }
            finally
            {
                _saving = false;
            }
        }

        // ---------- Colours ----------

        /// <summary>Colour zones of the item in hand (tabs) and the swatches of the chosen zone (round chips).</summary>
        private void RefreshColours()
        {
            var zones = _editor.SelectionZones;
            _colours.style.display = zones.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (zones.Count == 0)
            {
                _colouredItem = null;
                return;
            }
            if (_colouredItem != _editor.Selection.ItemId)
            {
                _colouredItem = _editor.Selection.ItemId;
                _zone = 0;
            }
            _zone = Mathf.Clamp(_zone, 0, zones.Count - 1);
            var current = _editor.SelectionSwatches;
            _zones.Clear();
            _zones.style.display = zones.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            for (var i = 0; i < zones.Count; i++)
            {
                var index = i;
                var tab = new Button(() =>
                {
                    _zone = index;
                    Refresh();
                }) { text = zones[i].Label, name = "build-zone-" + i };
                tab.AddToClassList("build-zone");
                tab.EnableInClassList("build-zone--selected", i == _zone);
                _zones.Add(tab);
            }
            _swatches.Clear();
            var zone = zones[_zone];
            foreach (var swatch in ItemColours.Swatches[zone.Kind])
            {
                var chosen = swatch;
                var chip = new Button(() => _editor.SetSelectionColour(_zone, chosen)) { name = "swatch-" + swatch, tooltip = swatch };
                chip.AddToClassList("swatch");
                chip.style.backgroundColor = SwatchLook.Chip(zone.Kind, swatch);
                chip.EnableInClassList("swatch--selected", current[_zone] == swatch);
                _swatches.Add(chip);
            }
        }

        // ---------- Catalog ----------

        private void BuildCategories()
        {
            _categories.Clear();
            foreach (var category in Categories)
            {
                var chip = new Button(() => ShowCategory(category)) { text = category, name = "build-category-" + category };
                chip.AddToClassList("build-chip");
                _categories.Add(chip);
            }
        }

        private void ShowCategory(string category)
        {
            _category = category;
            foreach (var chip in _categories.Children())
            {
                chip.EnableInClassList("build-chip--selected", ((Button)chip).text == category);
            }
            _items.Clear();
            var definitions = ItemDefinitions.All
                .Where(d => d.Category == category && !RoomZones.IsFixed(d.Id) && (_isAdmin || d.Id != RoomZones.ElevatorItem))
                .OrderBy(d => d.Name, StringComparer.CurrentCulture);
            foreach (var definition in definitions)
            {
                var card = new Button(() => Pick(definition)) { name = "build-item-" + definition.Id, tooltip = definition.Name };
                card.AddToClassList("build-card");
                var picture = new VisualElement { pickingMode = PickingMode.Ignore };
                picture.AddToClassList("build-card__picture");
                if (_room.BuildIcons != null && _room.BuildIcons.Find(definition.Id) is { } icon)
                {
                    picture.style.backgroundImage = new StyleBackground(icon);
                }
                var title = new Label(definition.Name) { pickingMode = PickingMode.Ignore };
                title.AddToClassList("build-card__name");
                card.Add(picture);
                card.Add(title);
                _items.Add(card);
            }
            _items.scrollOffset = Vector2.zero;
        }

        private void Pick(ItemDefinition definition)
        {
            if (_editor == null)
            {
                return;
            }
            var focus = _room.Focus;
            _editor.Pick(definition.Id, focus.x, focus.z);
            _message = null;
            Refresh();
        }

        // ---------- State ----------

        private void Refresh()
        {
            if (_editor == null)
            {
                return;
            }
            _preview.Update(_editor);
            var hasSelection = _editor.HasSelection;
            _turning &= hasSelection;
            var rotate = _panel.Q<Button>("build-rotate");
            rotate.text = _turning ? $"Drehen {Mathf.RoundToInt(_editor.Selection.Rotation) % 360}°" : "Drehen";
            rotate.EnableInClassList("button--active", _turning);
            _actions.style.display = hasSelection ? DisplayStyle.Flex : DisplayStyle.None;
            _panel.Q<Button>("build-delete").style.display = hasSelection && !_editor.SelectionIsNew ? DisplayStyle.Flex : DisplayStyle.None;
            _panel.Q<Button>("build-place").SetEnabled(_editor.SelectionIsValid);
            _panel.Q<Button>("build-undo").SetEnabled(_editor.CanUndo);
            foreach (var card in _items.Children())
            {
                card.EnableInClassList("build-card--selected", hasSelection && _editor.SelectionIsNew && card.name == "build-item-" + _editor.Selection.ItemId);
            }

            RefreshColours();
            var problem = hasSelection ? _editor.SelectionProblems.FirstOrDefault() : null;
            var text = _turning
                ? "Ziehe in die Richtung, in die es schauen soll – an Wänden rastet es parallel ein. „Drehen“ beendet."
                : problem
                  ?? _message
                  ?? (hasSelection
                      ? "Ziehen oder dorthin tippen, wo es hin soll – „Drehen“ zum Ausrichten, dann „Setzen“."
                      : "Wähle unten etwas aus oder tippe auf einen Gegenstand, um ihn zu verschieben.");
            _hint.text = text;
            _hint.EnableInClassList("build-hint--error", problem != null);
        }
    }
}
