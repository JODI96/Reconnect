using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Avatars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Screens
{
    /// <summary>
    /// The character creator (mirror or washstand in your own room, and the first time you enter a room): a big 3D preview
    /// on a turntable (drag to turn, pinch or wheel to zoom, walking), the camera glides to what is being changed (face,
    /// upper body, legs, feet) and "Ganzer Look" shows everything; categories with a grid of picture cards per hairstyle,
    /// garment, skin …, colours per part, sliders for body and face shape (live), random look, undo, walk style with
    /// preview. Dressing rules come from <see cref="Wardrobe"/> (Contracts), the same the server checks.
    /// </summary>
    public sealed class CharacterCreatorPanel
    {
        private const string None = "";

        private sealed class Category
        {
            public Category(string title, string kind, CharacterPreview.Focus focus, bool female = true, bool male = true)
            {
                Title = title;
                Kind = kind;
                Focus = focus;
                Female = female;
                Male = male;
            }

            public string Title { get; }
            public string Kind { get; }
            public CharacterPreview.Focus Focus { get; }
            public bool Female { get; }
            public bool Male { get; }
        }

        private static readonly Category[] Categories =
        {
            new("Körper", "body", CharacterPreview.Focus.Full),
            new("Haut", "skin", CharacterPreview.Focus.Upper),
            new("Gesicht", "face", CharacterPreview.Focus.Face),
            new("Augen", "eyes", CharacterPreview.Focus.Face),
            new("Nase", "nose", CharacterPreview.Focus.Face),
            new("Mund", "mouth", CharacterPreview.Focus.Face),
            new("Ohren", "ears", CharacterPreview.Focus.Face),
            new("Haare", Wardrobe.Hair, CharacterPreview.Focus.Face),
            new("Bart", Wardrobe.Beard, CharacterPreview.Focus.Face, female: false),
            new("Oberteil", Wardrobe.Top, CharacterPreview.Focus.Upper),
            new("Hose & Rock", Wardrobe.Bottom, CharacterPreview.Focus.Legs),
            new("Kleid", Wardrobe.Dress, CharacterPreview.Focus.Full, male: false),
            new("Outfit", Wardrobe.Outfit, CharacterPreview.Focus.Full),
            new("Schuhe", Wardrobe.Shoes, CharacterPreview.Focus.Feet),
            new("Hut", Wardrobe.Hat, CharacterPreview.Focus.Face),
            new("Laufstil", "walk", CharacterPreview.Focus.Full),
        };

        private static Category CategoryOf(string kind) => Array.Find(Categories, c => c.Kind == kind);

        private const int UndoSteps = 50;

        /// <summary>Eye colours as swatches (the textures themselves are too small to show).</summary>
        private static readonly Dictionary<string, Color32> EyeColours = new()
        {
            ["brown"] = new Color32(0x5A, 0x3A, 0x22, 255), ["brownlight"] = new Color32(0x8A, 0x60, 0x38, 255),
            ["green"] = new Color32(0x4E, 0x7A, 0x3A, 255), ["bluegreen"] = new Color32(0x3F, 0x7F, 0x7A, 255),
            ["blue"] = new Color32(0x3F, 0x6F, 0xB0, 255), ["lightblue"] = new Color32(0x7F, 0xA8, 0xD8, 255),
            ["deepblue"] = new Color32(0x23, 0x40, 0x7A, 255), ["grey"] = new Color32(0x7D, 0x85, 0x90, 255),
            ["ice"] = new Color32(0xA9, 0xCD, 0xE0, 255),
        };

        private static readonly Dictionary<string, string> WalkDescriptions = new()
        {
            ["normal"] = "Locker und natürlich", ["elegant"] = "Aufrecht, kleine Schritte", ["confident"] = "Breite Schultern, fester Schritt",
            ["relaxed"] = "Gemütlich, Hände locker", ["model"] = "Laufsteg: Schritt vor Schritt",
        };

        private readonly VisualElement _root;
        private readonly AvatarCatalog _catalog;
        private readonly Func<AvatarLookDto, Task<string>> _save;
        private readonly Action _closed;
        private readonly Dictionary<string, AvatarLookDto> _lookPerBody = new();
        private readonly System.Random _random = new();
        private VisualElement _panel;
        private VisualElement _stage;
        private ScrollView _categories;
        private ScrollView _content;
        private VisualElement _cards;
        private VisualElement _extras;
        private Label _status;
        private Button _fullButton;
        private Button _walkButton;
        private Button _undoButton;
        private readonly List<AvatarLookDto> _undo = new();
        private float _lastSliderChange = -10f;
        private readonly Dictionary<int, Vector2> _pointers = new();
        private Button _saveButton;
        private CharacterPreview _preview;
        private AvatarLookDto _look;
        private AvatarLookDto _saved;
        private Category _category = CategoryOf(Wardrobe.Hair);
        private float _lastDragX = float.NaN;
        private bool _saving;
        private bool _dirtyPreview;

        public CharacterCreatorPanel(VisualElement root, AvatarCatalog catalog, Func<AvatarLookDto, Task<string>> save, Action closed)
        {
            _root = root;
            _catalog = catalog;
            _save = save;
            _closed = closed;
        }

        public bool IsOpen => _panel != null;

        /// <summary>The look being made (tests).</summary>
        public AvatarLookDto Look => _look;

        /// <summary>The 3D preview while open (tests).</summary>
        public CharacterPreview Preview => _preview;

        /// <summary>Opens with <paramref name="look"/> (null = no look yet: starts with the default of the chosen body).</summary>
        public void Open(AvatarLookDto look, bool firstTime)
        {
            if (_panel != null || _catalog == null)
            {
                return;
            }
            _saved = look;
            _look = look ?? Wardrobe.Default(Wardrobe.Female);
            _lookPerBody.Clear();
            _lookPerBody[_look.Body] = _look;
            _category = firstTime ? CategoryOf("body") : CategoryOf(Wardrobe.Hair);
            _undo.Clear();
            Build(firstTime);
            _preview = new CharacterPreview(_catalog, 720, 960);
            _stage.style.backgroundImage = Background.FromRenderTexture(_preview.Texture);
            _preview.Show(_look);
            _preview.View = _category.Focus;
            ShowCategory(_category);
            _panel.schedule.Execute(Tick).Every(0);
        }

        public void Close()
        {
            if (_panel == null)
            {
                return;
            }
            _preview.Dispose();
            _preview = null;
            _panel.RemoveFromHierarchy();
            _panel = null;
            _closed();
        }

        // ---------- Layout ----------

        private void Build(bool firstTime)
        {
            _panel = new VisualElement { name = "creator" };
            _panel.AddToClassList("creator");

            var header = new VisualElement();
            header.AddToClassList("creator__header");
            var title = new Label(firstTime ? "Neuer Look" : "Dein Look");
            title.AddToClassList("creator__title");
            header.Add(title);
            _undoButton = SmallButton("↶", "creator-undo", Undo);
            _undoButton.tooltip = "Rückgängig";
            header.Add(_undoButton);
            header.Add(SmallButton("Zufall", "creator-random", Randomise));
            header.Add(SmallButton("Schliessen", "creator-close", Close));
            _panel.Add(header);

            _stage = new VisualElement { name = "creator-stage" };
            _stage.AddToClassList("creator__stage");
            // One finger turns the figure, two fingers pinch to zoom; the mouse wheel zooms too.
            _stage.RegisterCallback<PointerDownEvent>(e =>
            {
                _pointers[e.pointerId] = e.position;
                _lastDragX = e.position.x;
                _stage.CapturePointer(e.pointerId);
            });
            _stage.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_pointers.ContainsKey(e.pointerId))
                {
                    return;
                }
                if (_pointers.Count >= 2)
                {
                    var others = _pointers.Where(p => p.Key != e.pointerId).Select(p => p.Value).First();
                    var before = Vector2.Distance(_pointers[e.pointerId], others);
                    var after = Vector2.Distance(e.position, others);
                    if (before > 1f && after > 1f)
                    {
                        _preview?.Zoom(before / after);
                    }
                }
                else if (!float.IsNaN(_lastDragX))
                {
                    _preview?.Turn(-(e.position.x - _lastDragX) * 0.6f);
                }
                _pointers[e.pointerId] = e.position;
                _lastDragX = e.position.x;
            });
            _stage.RegisterCallback<PointerUpEvent>(e =>
            {
                _pointers.Remove(e.pointerId);
                _lastDragX = _pointers.Count == 1 ? _pointers.Values.First().x : float.NaN;
                _stage.ReleasePointer(e.pointerId);
            });
            _stage.RegisterCallback<WheelEvent>(e =>
            {
                _preview?.Zoom(e.delta.y > 0 ? 1.1f : 1f / 1.1f);
                e.StopPropagation();
            });
            var tools = new VisualElement { pickingMode = PickingMode.Ignore };
            tools.AddToClassList("creator__tools");
            _fullButton = SmallButton("Ganzer Look", "creator-full", () =>
            {
                // The whole look – tapped again, back to what is being changed.
                _preview.View = _preview.View == CharacterPreview.Focus.Full ? _category.Focus : CharacterPreview.Focus.Full;
                RefreshTools();
            });
            _walkButton = SmallButton("Gehen", "creator-walk", () =>
            {
                _preview.Walking = !_preview.Walking;
                RefreshTools();
            });
            tools.Add(_fullButton);
            tools.Add(_walkButton);
            _stage.Add(tools);
            var hint = new Label("Ziehen zum Drehen · zwei Finger zum Zoomen") { pickingMode = PickingMode.Ignore };
            hint.AddToClassList("creator__hint");
            _stage.Add(hint);
            _panel.Add(_stage);

            _categories = new ScrollView(ScrollViewMode.Horizontal) { name = "creator-categories" };
            _categories.AddToClassList("creator__categories");
            _panel.Add(_categories);

            // Colours and sliders on top, then the grid of cards – one scrolling list.
            _content = new ScrollView(ScrollViewMode.Vertical) { name = "creator-content" };
            _content.AddToClassList("creator__content");
            _extras = new VisualElement { name = "creator-extras" };
            _extras.AddToClassList("creator__extras");
            _cards = new VisualElement { name = "creator-cards" };
            _cards.AddToClassList("creator__grid");
            _content.Add(_extras);
            _content.Add(_cards);
            _panel.Add(_content);

            var footer = new VisualElement();
            footer.AddToClassList("creator__footer");
            _status = new Label { name = "creator-status" };
            _status.AddToClassList("creator__status");
            footer.Add(_status);
            _saveButton = new Button(() => _ = SaveAsync()) { text = "Speichern", name = "creator-save" };
            _saveButton.AddToClassList("button");
            _saveButton.AddToClassList("button--primary");
            _saveButton.AddToClassList("creator__save");
            footer.Add(_saveButton);
            _panel.Add(footer);

            foreach (var scroll in new[] { _categories, _content })
            {
                scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            }
            _root.Add(_panel);
        }

        private static Button SmallButton(string text, string name, Action clicked)
        {
            var button = new Button(clicked) { text = text, name = name };
            button.AddToClassList("button");
            button.AddToClassList("button--small");
            return button;
        }

        private void RefreshTools()
        {
            _fullButton.EnableInClassList("button--active", _preview.View == CharacterPreview.Focus.Full && _category.Focus != CharacterPreview.Focus.Full);
            _walkButton.text = _preview.Walking ? "Stehen" : "Gehen";
            _walkButton.EnableInClassList("button--active", _preview.Walking);
            _undoButton.SetEnabled(_undo.Count > 0);
        }

        private void BuildCategoryChips()
        {
            _categories.Clear();
            foreach (var category in Categories.Where(c => _look.Body == Wardrobe.Female ? c.Female : c.Male))
            {
                var chosen = category;
                var chip = new Button(() => ShowCategory(chosen)) { text = category.Title, name = "creator-category-" + category.Kind };
                chip.AddToClassList("build-chip");
                chip.EnableInClassList("build-chip--selected", category == _category);
                _categories.Add(chip);
            }
        }

        // ---------- Categories ----------

        private void ShowCategory(Category category)
        {
            if (_look.Body == Wardrobe.Female ? !category.Female : !category.Male)
            {
                category = CategoryOf(Wardrobe.Hair);
            }
            var changed = category != _category;
            _category = category;
            BuildCategoryChips();
            _cards.Clear();
            _extras.Clear();
            switch (category.Kind)
            {
                case "body":
                    Card("Frau", Icon(Wardrobe.Female, "body"), _look.Body == Wardrobe.Female, () => SwitchBody(Wardrobe.Female));
                    Card("Mann", Icon(Wardrobe.Male, "body"), _look.Body == Wardrobe.Male, () => SwitchBody(Wardrobe.Male));
                    HeightSlider();
                    MorphSliders("body");
                    break;
                case "face":
                    FaceShapes();
                    MorphSliders("face");
                    break;
                case "nose":
                case "mouth":
                    MorphSliders(category.Kind);
                    break;
                case "ears":
                    MorphSliders("ears");
                    break;
                case "skin":
                    foreach (var skin in Wardrobe.Skins[_look.Body])
                    {
                        Card(Wardrobe.SkinName(_look.Body, skin), Icon(_look.Body, "skin-" + skin), _look.Skin == skin,
                            () => Apply(_look with { Skin = skin }));
                    }
                    break;
                case "eyes":
                    Swatches("Augenfarbe", Wardrobe.Eyes.Select(e => (e, (Color)EyeColours[e], Wardrobe.EyeName(e))), _look.Eyes,
                        eyes => Apply(_look with { Eyes = eyes }), withOriginal: false);
                    MorphSliders("eyes");
                    MorphSliders("brows", "Brauen");
                    foreach (var brows in Wardrobe.PartsOf(_look.Body, Wardrobe.Brows))
                    {
                        Card(Wardrobe.PartName(brows), Icon(_look.Body, brows), _look.Brows == brows, () => Apply(_look with { Brows = brows }));
                    }
                    break;
                case "walk":
                    foreach (var (style, name) in Wardrobe.WalkStyles)
                    {
                        var card = Card(name, null, _look.WalkStyle == style, () =>
                        {
                            Apply(_look with { WalkStyle = style });
                            _preview.Walking = true;   // show it at once
                            RefreshTools();
                        });
                        card.AddToClassList("creator-card--text");
                        var description = new Label(WalkDescriptions.TryGetValue(style, out var text) ? text : "") { pickingMode = PickingMode.Ignore };
                        description.AddToClassList("creator-card__description");
                        card.Add(description);
                    }
                    break;
                default:
                    PartCards(category.Kind);
                    break;
            }
            if (changed)
            {
                _content.scrollOffset = Vector2.zero;
                _preview.View = category.Focus;   // the camera goes to what is being changed
                if (category.Kind == "walk")
                {
                    _preview.Walking = true;
                }
                RefreshTools();
            }
            RefreshState();
        }

        private void PartCards(string kind)
        {
            var worn = Wardrobe.Worn(_look, kind);
            if (Wardrobe.IsOptional(kind))
            {
                var none = Card(kind switch { Wardrobe.Hair => "Glatze", Wardrobe.Shoes => "Barfuss", _ => "Ohne" }, null, worn == null,
                    () => Apply(Wardrobe.TakeOff(_look, kind)));
                none.name = "creator-card-none";
            }
            foreach (var id in Wardrobe.PartsOf(_look.Body, kind))
            {
                var card = Card(Wardrobe.PartName(id), Icon(_look.Body, id), worn?.Id == id, () =>
                {
                    var current = Wardrobe.Worn(_look, kind);
                    Apply(Wardrobe.Wear(_look, id, tint: current?.Tint));
                });
                card.name = "creator-card-" + id;
            }
            if (worn == null)
            {
                return;
            }
            var variants = Wardrobe.Parts[_look.Body][worn.Id].Variants;
            if (variants.Length > 1)
            {
                var row = new ScrollView(ScrollViewMode.Horizontal);
                row.AddToClassList("creator__variants");
                foreach (var variant in variants)
                {
                    var chosen = variant;
                    var chip = new Button(() => Apply(Wardrobe.Wear(_look, worn.Id, chosen, worn.Tint))) { text = VariantName(variant) };
                    chip.AddToClassList("build-chip");
                    chip.EnableInClassList("build-chip--selected", worn.Variant == variant);
                    row.Add(chip);
                }
                _extras.Add(row);
            }
            var tints = kind is Wardrobe.Hair or Wardrobe.Beard ? Wardrobe.HairTints : Wardrobe.FashionTints;
            Swatches("Farbe", tints.Select(t => (t, TintColour(t), t)), worn.Tint, tint => Apply(Wardrobe.Tint(_look, kind, tint)), withOriginal: true);
        }

        private static string VariantName(string variant) => variant == "default" ? "Original" : char.ToUpper(variant[0]) + variant.Substring(1);

        private static Color TintColour(string tint)
        {
            var (r, g, b) = Wardrobe.TintColour(tint);
            return new Color(r, g, b);
        }

        private void HeightSlider()
        {
            var row = new VisualElement();
            row.AddToClassList("creator__height");
            var label = new Label($"Grösse {Wardrobe.HeightCm(_look)} cm");
            label.AddToClassList("creator__label");
            var (min, max) = Wardrobe.HeightRangeCm(_look.Body);
            var slider = new SliderInt(min, max) { value = Wardrobe.HeightCm(_look), name = "creator-height" };
            slider.AddToClassList("creator__slider");
            slider.RegisterValueChangedCallback(e =>
            {
                SliderChange(Wardrobe.WithHeightCm(_look, e.newValue));
                label.text = $"Grösse {Wardrobe.HeightCm(_look)} cm";
                if (_weightLabel != null)
                {
                    _weightLabel.text = MorphLabel("weight");
                }
            });
            row.Add(label);
            row.Add(slider);
            _extras.Add(row);
        }

        private Label _weightLabel;

        /// <summary>The face shape: one card per shape (oval, round, square …), the chosen one with its strength.</summary>
        private void FaceShapes()
        {
            var shapes = Wardrobe.MorphsOf(_look.Body, "shape");
            var chosen = shapes.Select(m => m.Id).FirstOrDefault(id => Wardrobe.ShapeOf(_look, id) > 0f);
            var neutral = Card("Natürlich", null, chosen == null, () => Apply(ClearShapes(_look, shapes.Select(m => m.Id))));
            neutral.name = "creator-shape-none";
            foreach (var (id, _, _, _) in shapes)
            {
                var card = Card(Wardrobe.MorphName(id), null, chosen == id, () =>
                {
                    var strength = chosen != null ? Wardrobe.ShapeOf(_look, chosen) : 0.6f;
                    Apply(Wardrobe.WithShape(ClearShapes(_look, shapes.Select(m => m.Id)), id, strength));
                });
                card.name = "creator-shape-" + id;
            }
            if (chosen != null)
            {
                Slider("Gesichtsform: " + Wardrobe.MorphName(chosen), chosen, 0f, 1f);
            }
        }

        private static AvatarLookDto ClearShapes(AvatarLookDto look, IEnumerable<string> ids) =>
            ids.Aggregate(look, (current, id) => Wardrobe.WithShape(current, id, 0f));

        /// <summary>A slider per shape of the group (live: the figure changes while dragging).</summary>
        private void MorphSliders(string group, string title = null)
        {
            var morphs = Wardrobe.MorphsOf(_look.Body, group);
            if (morphs.Count == 0)
            {
                return;
            }
            var header = new VisualElement();
            header.AddToClassList("creator__group");
            var heading = new Label(title ?? Wardrobe.MorphGroups.First(g => g.Id == group).Title);
            heading.AddToClassList("creator__label");
            header.Add(heading);
            var reset = new Button(() => Apply(ClearShapes(_look, morphs.Select(m => m.Id)))) { text = "Zurücksetzen", name = "creator-reset-" + group };
            reset.AddToClassList("button--link");
            reset.AddToClassList("creator__reset");
            header.Add(reset);
            _extras.Add(header);
            foreach (var (id, _, twoSided, _) in morphs)
            {
                var label = Slider(MorphLabel(id), id, twoSided ? -1f : 0f, 1f);
                if (id == "weight")
                {
                    _weightLabel = label;
                }
            }
        }

        private string MorphLabel(string id) =>
            id == "weight" ? $"Gewicht {Wardrobe.WeightKg(_look)} kg" : Wardrobe.MorphName(id);

        private Label Slider(string title, string id, float min, float max)
        {
            var row = new VisualElement();
            row.AddToClassList("creator__morph");
            var label = new Label(title);
            label.AddToClassList("creator__morph-label");
            var slider = new Slider(min, max) { value = Wardrobe.ShapeOf(_look, id), name = "creator-morph-" + id };
            slider.AddToClassList("creator__morph-slider");
            slider.RegisterValueChangedCallback(e =>
            {
                SliderChange(Wardrobe.WithShape(_look, id, e.newValue));
                if (id == "weight")
                {
                    label.text = MorphLabel(id);
                }
            });
            row.Add(label);
            row.Add(slider);
            _extras.Add(row);
            return label;
        }

        /// <summary>A slider moved: the figure is rebuilt once per frame; one undo step per drag (a pause starts a new one).</summary>
        private void SliderChange(AvatarLookDto look)
        {
            if (Time.realtimeSinceStartup - _lastSliderChange > 0.6f)
            {
                PushUndo();
            }
            _lastSliderChange = Time.realtimeSinceStartup;
            _look = look;
            _lookPerBody[look.Body] = look;
            _dirtyPreview = true;
            RefreshState();
        }

        private VisualElement Card(string title, Texture2D icon, bool selected, Action clicked)
        {
            var card = new Button(clicked) { tooltip = title };
            card.AddToClassList("build-card");
            card.AddToClassList("creator-card");
            card.EnableInClassList("build-card--selected", selected);
            if (icon != null)
            {
                var picture = new VisualElement { pickingMode = PickingMode.Ignore };
                picture.AddToClassList("build-card__picture");
                picture.AddToClassList("creator-card__picture");
                picture.style.backgroundImage = new StyleBackground(icon);
                card.Add(picture);
            }
            else
            {
                card.AddToClassList("creator-card--text");
            }
            var name = new Label(title) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("build-card__name");
            card.Add(name);
            _cards.Add(card);
            return card;
        }

        private void Swatches(string title, IEnumerable<(string Id, Color Colour, string Name)> swatches, string current, Action<string> chosen, bool withOriginal)
        {
            var label = new Label(title);
            label.AddToClassList("creator__label");
            _extras.Add(label);
            var row = new ScrollView(ScrollViewMode.Horizontal) { name = "creator-swatches" };
            row.AddToClassList("build-swatches");
            row.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            if (withOriginal)
            {
                var original = new Button(() => chosen(null)) { tooltip = "Originalfarben", name = "swatch-original", text = "∅" };
                original.AddToClassList("swatch");
                original.AddToClassList("creator-swatch--original");
                original.EnableInClassList("swatch--selected", current == null);
                row.Add(original);
            }
            foreach (var (id, colour, name) in swatches)
            {
                var chip = new Button(() => chosen(id)) { tooltip = name, name = "swatch-" + id };
                chip.AddToClassList("swatch");
                chip.style.backgroundColor = colour;
                chip.EnableInClassList("swatch--selected", current == id);
                row.Add(chip);
            }
            _extras.Add(row);
        }

        private static Texture2D Icon(string body, string id) => Resources.Load<Texture2D>($"WardrobeIcons/{body}/{id}");

        // ---------- Changes ----------

        private void Apply(AvatarLookDto look, bool undoable = true)
        {
            if (undoable)
            {
                PushUndo();
            }
            _lastSliderChange = -10f;   // the next slider drag is its own step
            _look = look;
            _lookPerBody[look.Body] = look;
            _preview.Show(look);
            var scroll = _content.scrollOffset;
            ShowCategory(_category);
            _content.scrollOffset = scroll;
        }

        private void PushUndo()
        {
            _undo.Add(_look);
            if (_undo.Count > UndoSteps)
            {
                _undo.RemoveAt(0);
            }
        }

        private void Undo()
        {
            if (_undo.Count == 0)
            {
                return;
            }
            var previous = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            Apply(previous, undoable: false);
        }

        /// <summary>Woman or man: each body remembers its own look while the creator is open.</summary>
        private void SwitchBody(string body)
        {
            if (body == _look.Body)
            {
                return;
            }
            Apply(_lookPerBody.TryGetValue(body, out var look) ? look : Wardrobe.Default(body) with
            {
                WalkStyle = _look.WalkStyle, Eyes = _look.Eyes,
            });
        }

        private void Randomise() => Apply(Wardrobe.Random(_look.Body, _random));

        /// <summary>
        /// Every frame: sliders change the look many times a second – the figure is rebuilt once per frame at most; the
        /// camera glides.
        /// </summary>
        private void Tick()
        {
            if (_preview == null)
            {
                return;
            }
            if (_dirtyPreview)
            {
                _dirtyPreview = false;
                _preview.Show(_look);
            }
            _preview.Tick(Time.unscaledDeltaTime);
            _undoButton.SetEnabled(_undo.Count > 0);
        }

        private void RefreshState()
        {
            var problems = Wardrobe.Problems(_look);
            _saveButton.SetEnabled(!_saving && problems.Count == 0);
            if (!_saving)
            {
                _status.text = problems.FirstOrDefault() ?? (_saved != null && Same(_saved, _look) ? "Gespeichert" : "");
                _status.EnableInClassList("creator__status--error", problems.Count > 0);
            }
        }

        private static bool Same(AvatarLookDto a, AvatarLookDto b) =>
            a.Body == b.Body && a.Skin == b.Skin && a.Eyes == b.Eyes && a.Brows == b.Brows && a.WalkStyle == b.WalkStyle
            && Mathf.Approximately(a.Height, b.Height) && a.Parts.SequenceEqual(b.Parts)
            && (a.Shape ?? new Dictionary<string, float>()).OrderBy(p => p.Key).SequenceEqual((b.Shape ?? new Dictionary<string, float>()).OrderBy(p => p.Key));

        private async Task SaveAsync()
        {
            if (_saving || _panel == null)
            {
                return;
            }
            _saving = true;
            _status.text = "Speichere …";
            _status.RemoveFromClassList("creator__status--error");
            _saveButton.SetEnabled(false);
            try
            {
                string error;
                try
                {
                    error = await _save(_look);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    error = "Speichern fehlgeschlagen: " + ex.Message;
                }
                if (_panel == null)
                {
                    return;
                }
                if (error == null)
                {
                    _saved = _look;
                    Close();
                    return;
                }
                _status.text = error;
                _status.AddToClassList("creator__status--error");
            }
            finally
            {
                _saving = false;
                _saveButton?.SetEnabled(Wardrobe.IsValid(_look));
            }
        }
    }
}
