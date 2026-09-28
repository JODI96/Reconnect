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
    /// on a turntable (drag to turn, body or face, walking), categories with a picture card per hairstyle, garment, skin …,
    /// colours per part, random look, walk style with preview. Dressing rules come from <see cref="Wardrobe"/> (Contracts),
    /// the same the server checks.
    /// </summary>
    public sealed class CharacterCreatorPanel
    {
        private const string None = "";

        private sealed class Category
        {
            public Category(string title, string kind, bool female = true, bool male = true)
            {
                Title = title;
                Kind = kind;
                Female = female;
                Male = male;
            }

            public string Title { get; }
            public string Kind { get; }
            public bool Female { get; }
            public bool Male { get; }
        }

        private static readonly Category[] Categories =
        {
            new("Körper", "body"),
            new("Haut", "skin"),
            new("Augen", "eyes"),
            new("Haare", Wardrobe.Hair),
            new("Bart", Wardrobe.Beard, female: false),
            new("Oberteil", Wardrobe.Top),
            new("Hose & Rock", Wardrobe.Bottom),
            new("Kleid", Wardrobe.Dress, male: false),
            new("Outfit", Wardrobe.Outfit),
            new("Schuhe", Wardrobe.Shoes),
            new("Hut", Wardrobe.Hat),
            new("Laufstil", "walk"),
        };

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
        private ScrollView _cards;
        private VisualElement _extras;
        private Label _status;
        private Button _faceButton;
        private Button _walkButton;
        private Button _saveButton;
        private CharacterPreview _preview;
        private AvatarLookDto _look;
        private AvatarLookDto _saved;
        private Category _category = Categories[3];
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
            _category = firstTime ? Categories[0] : Categories[3];
            Build(firstTime);
            _preview = new CharacterPreview(_catalog, 720, 960);
            _stage.style.backgroundImage = Background.FromRenderTexture(_preview.Texture);
            _preview.Show(_look);
            ShowCategory(_category);
            _panel.schedule.Execute(RebuildPreviewIfNeeded).Every(0);
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
            header.Add(SmallButton("Zufall", "creator-random", Randomise));
            header.Add(SmallButton("Schliessen", "creator-close", Close));
            _panel.Add(header);

            _stage = new VisualElement { name = "creator-stage" };
            _stage.AddToClassList("creator__stage");
            _stage.RegisterCallback<PointerDownEvent>(e =>
            {
                _lastDragX = e.position.x;
                _stage.CapturePointer(e.pointerId);
            });
            _stage.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (float.IsNaN(_lastDragX) || !_stage.HasPointerCapture(e.pointerId))
                {
                    return;
                }
                _preview?.Turn(-(e.position.x - _lastDragX) * 0.6f);
                _lastDragX = e.position.x;
            });
            _stage.RegisterCallback<PointerUpEvent>(e =>
            {
                _lastDragX = float.NaN;
                _stage.ReleasePointer(e.pointerId);
            });
            var tools = new VisualElement { pickingMode = PickingMode.Ignore };
            tools.AddToClassList("creator__tools");
            _faceButton = SmallButton("Gesicht", "creator-face", () =>
            {
                _preview.FaceView = !_preview.FaceView;
                RefreshTools();
            });
            _walkButton = SmallButton("Gehen", "creator-walk", () =>
            {
                _preview.Walking = !_preview.Walking;
                RefreshTools();
            });
            tools.Add(_faceButton);
            tools.Add(_walkButton);
            _stage.Add(tools);
            var hint = new Label("Ziehen zum Drehen") { pickingMode = PickingMode.Ignore };
            hint.AddToClassList("creator__hint");
            _stage.Add(hint);
            _panel.Add(_stage);

            _categories = new ScrollView(ScrollViewMode.Horizontal) { name = "creator-categories" };
            _categories.AddToClassList("creator__categories");
            _panel.Add(_categories);

            _cards = new ScrollView(ScrollViewMode.Horizontal) { name = "creator-cards" };
            _cards.AddToClassList("creator__cards");
            _panel.Add(_cards);

            _extras = new VisualElement { name = "creator-extras" };
            _extras.AddToClassList("creator__extras");
            _panel.Add(_extras);

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

            foreach (var scroll in new[] { _categories, _cards })
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
            _faceButton.text = _preview.FaceView ? "Ganzer Körper" : "Gesicht";
            _walkButton.text = _preview.Walking ? "Stehen" : "Gehen";
            _walkButton.EnableInClassList("button--active", _preview.Walking);
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
                category = Categories[3];
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
                    break;
                case "skin":
                    foreach (var skin in Wardrobe.Skins[_look.Body])
                    {
                        Card(Wardrobe.SkinName(_look.Body, skin), Icon(_look.Body, "skin-" + skin), _look.Skin == skin,
                            () => Apply(_look with { Skin = skin }));
                    }
                    break;
                case "eyes":
                    foreach (var brows in Wardrobe.PartsOf(_look.Body, Wardrobe.Brows))
                    {
                        Card(Wardrobe.PartName(brows), Icon(_look.Body, brows), _look.Brows == brows, () => Apply(_look with { Brows = brows }));
                    }
                    Swatches("Augenfarbe", Wardrobe.Eyes.Select(e => (e, (Color)EyeColours[e], Wardrobe.EyeName(e))), _look.Eyes,
                        eyes => Apply(_look with { Eyes = eyes }), withOriginal: false);
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
                _cards.scrollOffset = Vector2.zero;
                // Close-ups for the face, the whole figure for everything else.
                _preview.FaceView = category.Kind is "eyes" or Wardrobe.Hair or Wardrobe.Beard or "skin";
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
                _look = Wardrobe.WithHeightCm(_look, e.newValue);
                label.text = $"Grösse {Wardrobe.HeightCm(_look)} cm";
                _lookPerBody[_look.Body] = _look;
                _dirtyPreview = true;
                RefreshState();
            });
            row.Add(label);
            row.Add(slider);
            _extras.Add(row);
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

        private void Apply(AvatarLookDto look)
        {
            _look = look;
            _lookPerBody[look.Body] = look;
            _preview.Show(look);
            var scroll = _cards.scrollOffset;
            ShowCategory(_category);
            _cards.scrollOffset = scroll;
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

        /// <summary>The height slider changes the look many times a second: the figure is rebuilt once per frame at most.</summary>
        private void RebuildPreviewIfNeeded()
        {
            if (_dirtyPreview && _preview != null)
            {
                _dirtyPreview = false;
                _preview.Show(_look);
            }
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
            && Mathf.Approximately(a.Height, b.Height) && a.Parts.SequenceEqual(b.Parts);

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
