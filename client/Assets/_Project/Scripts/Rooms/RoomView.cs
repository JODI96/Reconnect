using System;
using System.Collections.Generic;
using System.Linq;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// The 3D room in Main.unity. Builds the floor (procedural texture per theme), walls from Kenney
    /// pieces with windows and a door – or a roof terrace with glass railing / a glass-walled top floor
    /// that sits on the real building in the 3D city – furniture from the <see cref="ItemCatalog"/>,
    /// <see cref="CustomItems"/>, stacked small items, game stations, lights and one
    /// <see cref="AvatarView"/> per player. Camera: Habbo-like angle with perspective, drag to pan,
    /// pinch/scroll to zoom, follows your own avatar. Presentation only; RoomScreen drives it.
    /// </summary>
    public sealed class RoomView : MonoBehaviour
    {
        public const float TileSize = 1f;
        public const string TicTacToeItem = "game-tictactoe";
        public const string QuizItem = "game-quiz";

        /// <summary>GameStation id of the lift bank.</summary>
        public const string ElevatorStation = "elevator";

        private const float WallHeight = 2.57f;          // Kenney wall piece at scale 0.2
        private const float WallPieceWidth = 2f;
        private const float TapMaxMovePixels = 12f;
        private const float CameraPitch = 38f;
        private const float CameraFieldOfView = 24f;
        private const float MinViewWidth = 3.5f;
        private const float StartViewWidth = 8f;     // metres of floor across the screen when entering: close to the people
        private const int MaxLampLights = 10;
        private const float FacadeHeight = 3.4f;         // glass top floor: floor-to-ceiling glass
        private const float HallHeight = 10f;            // tower lobby (Prime Tower: 10 m serpentine walls)

        private static readonly string[] StackableItems =
        {
            "laptop", "books", "computerScreen", "computerKeyboard", "computerMouse", "kitchenCoffeeMachine",
            "kitchenBlender", "kitchenMicrowave", "toaster", "lampSquareTable", "lampRoundTable", "plantSmall",
            "radio", "televisionModern", "televisionVintage", "speakerSmall", "pillow", "cardboardBox",
            "ph-tea_set_01", "ph-ceramic_vase_01", "ph-throw_pillows_01", "ph-marble_bust_01", "ph-desk_lamp_arm_01",
        };

        [SerializeField] private Camera roomCamera;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material itemMaterial;
        [SerializeField] private Material avatarMaterial;
        [SerializeField] private Material glassMaterial;
        [SerializeField] private Material waterMaterial;
        [SerializeField] private ItemCatalog itemCatalog;
        [SerializeField] private AvatarCatalog avatarCatalog;

        private readonly Dictionary<Guid, AvatarView> _avatars = new();
        private readonly List<GameStation> _stations = new();
        private readonly HashSet<Vector2Int> _blocked = new();
        private readonly List<Bounds> _surfaces = new();
        private readonly List<string> _missingItems = new();
        private Transform _content;
        private CameraState _savedCamera;
        private RoomTheme _theme;
        private CustomItems _custom;
        private Guid _localUserId;
        private int _width;
        private int _depth;
        private int _lampLights;
        private int? _storey;

        // Camera rig (local room coordinates).
        private Vector3 _focus;
        private float _viewWidth;
        private bool _follow = true;

        // Pointer state.
        private Vector2 _pressPosition;
        private Vector2 _lastPointer;
        private bool _pressed;
        private bool _dragging;
        private float _lastPinchDistance;

        /// <summary>Tapped floor tile (only when <see cref="IsPointerOverUi"/> is false).</summary>
        public event Action<Vector2Int> TileTapped;

        /// <summary>Tapped minigame station (tic-tac-toe table, quiz TV).</summary>
        public event Action<GameStation> StationTapped;

        public Func<Vector2, bool> IsPointerOverUi { get; set; } = _ => false;
        public Camera Camera => roomCamera;
        public IReadOnlyCollection<AvatarView> Avatars => _avatars.Values;
        public IReadOnlyList<GameStation> Stations => _stations;
        public RoomPathfinder Pathfinder { get; private set; }
        public bool IsOutdoor => _theme.Outdoor;

        /// <summary>Item ids of the current layout that have no model (shown as placeholder boxes) – should stay empty.</summary>
        public IReadOnlyList<string> MissingItems => _missingItems;

        public static Vector3 TileCenter(Vector2Int tile) => new((tile.x + 0.5f) * TileSize, 0f, (tile.y + 0.5f) * TileSize);

        public static Vector2Int WorldToTile(Vector3 local) =>
            new(Mathf.FloorToInt(local.x / TileSize), Mathf.FloorToInt(local.z / TileSize));

        /// <param name="groundAnchor">World position the room's centre should stand on (e.g. a real roof); null = default place.</param>
        /// <param name="yaw">Rotation around the anchor, degrees (tower floors follow the tower's orientation).</param>
        public void Show(RoomSnapshotDto snapshot, Guid localUserId, Vector3? groundAnchor = null, float yaw = 0f)
        {
            Hide();
            _localUserId = localUserId;
            _width = snapshot.Width;
            _depth = snapshot.Depth;
            _theme = RoomTheme.For(snapshot.Room.Theme);
            _custom = new CustomItems(wallMaterial, waterMaterial, glassMaterial, _theme);
            _lampLights = 0;

            _storey = snapshot.Room.Floor;
            // Build axis-aligned (bounds, blocked tiles and stacking use world-space boxes), rotate at the end.
            transform.rotation = Quaternion.identity;
            transform.position = groundAnchor.HasValue
                ? groundAnchor.Value - new Vector3(_width / 2f, 0f, _depth / 2f)
                : new Vector3(0f, -2000f, 0f);   // far away from the city origin when shown on its own
            _content = new GameObject("Room " + snapshot.Room.Name).transform;
            _content.SetParent(transform, false);

            BuildFloor();
            switch (_theme.Enclosure)
            {
                case Enclosure.Railing: BuildRailing(); break;
                case Enclosure.GlassFacade: BuildGlassFacade(); break;
                case Enclosure.StoneHall: BuildStoneHall(); break;
                default: BuildWalls(); break;
            }
            BuildFurniture(snapshot.Room.Layout);
            BuildLighting();
            Pathfinder = new RoomPathfinder(_width, _depth, _blocked);
            if (groundAnchor.HasValue && Mathf.Abs(yaw) > 0.01f)
            {
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                transform.SetPositionAndRotation(groundAnchor.Value - rotation * new Vector3(_width / 2f, 0f, _depth / 2f), rotation);
            }

            foreach (var player in snapshot.Players)
            {
                AddPlayer(player, player.UserId == localUserId);
            }

            _savedCamera = CameraState.Capture(roomCamera);
            SetupCamera();
            enabled = true;
        }

        public void Hide()
        {
            if (_content != null)
            {
                Destroy(_content.gameObject);
                _content = null;
            }
            _avatars.Clear();
            _stations.Clear();
            _blocked.Clear();
            _surfaces.Clear();
            _missingItems.Clear();
            _savedCamera?.Restore(roomCamera);
            _savedCamera = null;
            enabled = false;
        }

        public void AddPlayer(RoomPlayerDto player, bool isLocal = false)
        {
            if (_content == null || _avatars.ContainsKey(player.UserId))
            {
                return;
            }
            _avatars[player.UserId] = AvatarView.Create(_content, player, avatarCatalog, avatarMaterial, isLocal);
        }

        public void RemovePlayer(Guid userId)
        {
            if (_avatars.Remove(userId, out var avatar))
            {
                Destroy(avatar.gameObject);
            }
        }

        /// <summary>Walks the avatar to the tile around any furniture in the way.</summary>
        public void MovePlayer(Guid userId, TilePosition tile)
        {
            if (!_avatars.TryGetValue(userId, out var avatar))
            {
                return;
            }
            var from = avatar.NextTile;
            var goal = Pathfinder.NearestWalkable(new Vector2Int(tile.X, tile.Z), from);
            avatar.WalkAlong(Pathfinder.FindPath(from, goal));
            if (userId == _localUserId)
            {
                _follow = true;   // walking re-centres the camera on me
            }
        }

        public AvatarView Avatar(Guid userId) => _avatars.TryGetValue(userId, out var avatar) ? avatar : null;

        /// <summary>Zooms out so the whole room is visible (used for previews/screenshots).</summary>
        public void FrameWholeRoom()
        {
            _follow = false;
            _focus = new Vector3(_width / 2f, 0f, _depth / 2f);
            _viewWidth = MaxViewWidth;
            ApplyCamera();
        }

        private float MaxViewWidth => (_width + _depth) * 0.7071f * 1.05f;

        private void Awake() => enabled = false;

        private void Update()
        {
            HandlePinch();
            HandlePointer();

            var scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
            if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUi(Mouse.current.position.ReadValue()))
            {
                Zoom(1f - scroll * 0.0012f);
            }
        }

        private void LateUpdate()
        {
            if (_follow && Avatar(_localUserId) is { } me)
            {
                var target = me.transform.localPosition;
                _focus = Vector3.Lerp(_focus, new Vector3(target.x, 0f, target.z), 1f - Mathf.Exp(-6f * Time.deltaTime));
            }
            ApplyCamera();
        }

        private void HandlePointer()
        {
            var pointer = Pointer.current;
            if (pointer == null || _lastPinchDistance > 0f)
            {
                return;
            }

            var position = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                _pressed = !IsPointerOverUi(position);
                _dragging = false;
                _pressPosition = _lastPointer = position;
                return;
            }
            if (!_pressed)
            {
                return;
            }

            if (pointer.press.isPressed)
            {
                if (!_dragging && (position - _pressPosition).magnitude > TapMaxMovePixels)
                {
                    _dragging = true;
                    _follow = false;
                }
                if (_dragging && TryFloorPoint(_lastPointer, out var from) && TryFloorPoint(position, out var to))
                {
                    _focus += from - to;
                    ClampFocus();
                }
                _lastPointer = position;
                return;
            }

            // Released.
            _pressed = false;
            if (_dragging)
            {
                return;
            }
            var ray = roomCamera.ScreenPointToRay(position);
            if (Physics.Raycast(ray, out var hit, 500f) && hit.collider.GetComponentInParent<GameStation>() is { } station)
            {
                StationTapped?.Invoke(station);
            }
            else if (TryTileAt(position, out var tile))
            {
                TileTapped?.Invoke(tile);
            }
        }

        private void HandlePinch()
        {
            var touches = Touchscreen.current?.touches;
            if (touches == null || !touches.Value[0].isInProgress || !touches.Value[1].isInProgress)
            {
                _lastPinchDistance = 0f;
                return;
            }
            var distance = Vector2.Distance(touches.Value[0].position.ReadValue(), touches.Value[1].position.ReadValue());
            if (_lastPinchDistance > 0f && distance > 0f)
            {
                Zoom(_lastPinchDistance / distance);
            }
            _lastPinchDistance = distance;
            _pressed = false;
        }

        private void Zoom(float factor)
        {
            _viewWidth = Mathf.Clamp(_viewWidth * factor, MinViewWidth, MaxViewWidth);
        }

        private bool TryTileAt(Vector2 screenPosition, out Vector2Int tile)
        {
            if (TryFloorPoint(screenPosition, out var local))
            {
                tile = WorldToTile(local);
                return tile.x >= 0 && tile.x < _width && tile.y >= 0 && tile.y < _depth;
            }
            tile = default;
            return false;
        }

        /// <summary>Screen point → point on the floor in local room coordinates.</summary>
        private bool TryFloorPoint(Vector2 screenPosition, out Vector3 local)
        {
            var ray = roomCamera.ScreenPointToRay(screenPosition);
            if (new Plane(transform.up, transform.position).Raycast(ray, out var enter))
            {
                local = transform.InverseTransformPoint(ray.GetPoint(enter));
                return true;
            }
            local = default;
            return false;
        }

        // ---------- Floor, walls, railing ----------

        private void BuildFloor()
        {
            var material = new Material(floorMaterial) { mainTexture = FloorTextures.Create(_theme.Floor, _theme.FloorA, _theme.FloorB) };
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", _theme.Floor switch
            {
                FloorPattern.Marble => 0.75f,
                FloorPattern.Terrazzo => 0.55f,   // polished, without mirror-like light spots
                _ => 0.3f,
            });
            material.mainTextureScale = new Vector2(_width / FloorTextures.MetersPerTexture, _depth / FloorTextures.MetersPerTexture);

            var floor = Primitive(PrimitiveType.Cube, "Floor", material);
            floor.transform.localPosition = new Vector3(_width / 2f, -0.05f, _depth / 2f);
            floor.transform.localScale = new Vector3(_width, 0.1f, _depth);

            // A thick base under the floor: a solid block indoors, the building's roof slab outdoors.
            var baseColor = _theme.Outdoor ? new Color(0.55f, 0.56f, 0.58f) : _theme.FloorB * 0.6f;
            var slab = Primitive(PrimitiveType.Cube, "Floor Base", Tinted(wallMaterial, baseColor));
            // Roof terraces: a structure that meets the roof. Upper tower storeys stand on the tower's floor plate.
            var thickness = _theme.Outdoor ? (_storey is > 0 ? 0.3f : 4f) : 0.5f;
            slab.transform.localPosition = new Vector3(_width / 2f, -0.1f - thickness / 2f, _depth / 2f);
            slab.transform.localScale = new Vector3(_width + (_theme.Outdoor ? 0.6f : 0f), thickness, _depth + (_theme.Outdoor ? 0.6f : 0f));
        }

        /// <summary>North and east walls from Kenney pieces: windows, a door in the middle of the north wall.</summary>
        private void BuildWalls()
        {
            var wallTint = Tinted(wallMaterial, _theme.Wall);
            var trim = Tinted(wallMaterial, _theme.WallTrim);
            var northPieces = Mathf.CeilToInt(_width / WallPieceWidth);
            var eastPieces = Mathf.CeilToInt(_depth / WallPieceWidth);

            for (var i = 0; i < northPieces; i++)
            {
                var piece = i == northPieces / 2 ? "wallDoorway" : i % 2 == 1 ? "wallWindow" : "wall";
                var position = new Vector3(i * WallPieceWidth + WallPieceWidth / 2f, 0f, _depth + 0.06f);
                PlaceWallPiece(piece, position, 0f, wallTint);
                if (piece == "wall" && i % 4 == 0)
                {
                    Picture(new Vector3(position.x, 1.5f, _depth - 0.01f), new Vector3(1.0f, 0.75f, 0.03f), trim);
                }
            }
            for (var i = 0; i < eastPieces; i++)
            {
                var piece = i % 2 == 0 ? "wallWindow" : "wall";
                var position = new Vector3(_width + 0.06f, 0f, i * WallPieceWidth + WallPieceWidth / 2f);
                PlaceWallPiece(piece, position, 90f, wallTint);
                if (piece == "wall" && i % 4 == 1)
                {
                    Picture(new Vector3(_width - 0.01f, 1.5f, position.z), new Vector3(0.03f, 0.75f, 1.0f), trim);
                }
            }

            Box("Skirting North", trim, new Vector3(_width / 2f, 0.07f, _depth - 0.02f), new Vector3(_width, 0.14f, 0.04f));
            Box("Skirting East", trim, new Vector3(_width - 0.02f, 0.07f, _depth / 2f), new Vector3(0.04f, 0.14f, _depth));
            Box("Wall Top North", trim, new Vector3(_width / 2f, WallHeight + 0.05f, _depth + 0.06f), new Vector3(_width + 0.3f, 0.1f, 0.3f));
            Box("Wall Top East", trim, new Vector3(_width + 0.06f, WallHeight + 0.05f, _depth / 2f), new Vector3(0.3f, 0.1f, _depth + 0.3f));
        }

        private void PlaceWallPiece(string itemId, Vector3 localPosition, float rotation, Material tint)
        {
            var pivot = new GameObject(itemId).transform;
            pivot.SetParent(_content, false);
            pivot.localPosition = localPosition;
            pivot.localRotation = Quaternion.Euler(0f, rotation, 0f);

            var model = itemCatalog != null ? itemCatalog.Find(itemId) : null;
            if (model == null)
            {
                var wall = Primitive(PrimitiveType.Cube, itemId, tint);
                wall.transform.SetParent(pivot, false);
                wall.transform.localPosition = Vector3.up * WallHeight / 2f;
                wall.transform.localScale = new Vector3(WallPieceWidth, WallHeight, 0.12f);
                return;
            }

            var instance = Instantiate(model, pivot, false);
            instance.transform.localScale = Vector3.one * itemCatalog.ScaleFor(itemId);
            PlaceCentered(instance);
            // Kenney walls are white: paint the bright surfaces in the theme colour, keep frames/glass.
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (var m = 0; m < materials.Length; m++)
                {
                    var color = materials[m].HasProperty("_BaseColor") ? materials[m].GetColor("_BaseColor") : Color.white;
                    if (color.grayscale > 0.8f)
                    {
                        materials[m] = tint;
                    }
                }
                renderer.sharedMaterials = materials;
            }
        }

        private void Picture(Vector3 position, Vector3 size, Material frame)
        {
            Box("Picture Frame", frame, position, size + new Vector3(size.x > 0.05f ? 0.1f : 0f, 0.1f, size.z > 0.05f ? 0.1f : 0f));
            var canvas = Tinted(wallMaterial, Color.Lerp(_theme.WallTrim, Color.white, 0.55f));
            Box("Picture", canvas, position + new Vector3(size.x > 0.05f ? 0f : -0.01f, 0f, size.z > 0.05f ? 0f : -0.01f), size);
        }

        /// <summary>Roof terrace edge: stone coping, metal posts, glass panels and a handrail on all four sides.</summary>
        private void BuildRailing()
        {
            const float height = 1.1f;
            var metal = Tinted(wallMaterial, new Color(0.22f, 0.22f, 0.25f));
            var stone = Tinted(wallMaterial, new Color(0.82f, 0.8f, 0.76f));
            var edges = new[]
            {
                (from: new Vector3(0f, 0f, 0f), to: new Vector3(_width, 0f, 0f)),
                (from: new Vector3(_width, 0f, 0f), to: new Vector3(_width, 0f, _depth)),
                (from: new Vector3(_width, 0f, _depth), to: new Vector3(0f, 0f, _depth)),
                (from: new Vector3(0f, 0f, _depth), to: new Vector3(0f, 0f, 0f)),
            };
            foreach (var (from, to) in edges)
            {
                var direction = (to - from).normalized;
                var length = Vector3.Distance(from, to);
                var rotation = Quaternion.LookRotation(Vector3.Cross(direction, Vector3.up));
                var middle = (from + to) / 2f;

                var coping = Primitive(PrimitiveType.Cube, "Coping", stone);
                coping.transform.localPosition = middle + Vector3.up * 0.06f;
                coping.transform.localRotation = rotation;
                coping.transform.localScale = new Vector3(length + 0.3f, 0.12f, 0.3f);

                var glass = Primitive(PrimitiveType.Cube, "Glass", glassMaterial);
                glass.transform.localPosition = middle + Vector3.up * (0.12f + height / 2f);
                glass.transform.localRotation = rotation;
                glass.transform.localScale = new Vector3(length, height - 0.1f, 0.03f);

                var rail = Primitive(PrimitiveType.Cube, "Handrail", metal);
                rail.transform.localPosition = middle + Vector3.up * (0.12f + height);
                rail.transform.localRotation = rotation;
                rail.transform.localScale = new Vector3(length + 0.06f, 0.06f, 0.08f);

                for (var t = 0f; t <= length + 0.01f; t += 2f)
                {
                    var post = Primitive(PrimitiveType.Cube, "Post", metal);
                    post.transform.localPosition = from + direction * t + Vector3.up * (0.12f + height / 2f);
                    post.transform.localScale = new Vector3(0.06f, height, 0.06f);
                }
            }
        }

        /// <summary>
        /// Glass top floor (modelled on the top floor of Zurich's Prime Tower): floor-to-ceiling,
        /// frameless green-tinted glass on the back sides, a low glass parapet on the camera sides so you
        /// can look in, slim mullions and LED lines – the top edge breathes softly like a luminous ceiling.
        /// </summary>
        private void BuildGlassFacade()
        {
            var glass = Tinted(glassMaterial, new Color(0.62f, 0.86f, 0.82f, 0.14f));
            var frame = Tinted(wallMaterial, new Color(0.1f, 0.11f, 0.13f));
            var led = Glowing(_theme.WallTrim);
            var sides = new[]
            {
                (from: new Vector3(0f, 0f, _depth), to: new Vector3(_width, 0f, _depth), back: true),     // north
                (from: new Vector3(_width, 0f, _depth), to: new Vector3(_width, 0f, 0f), back: true),     // east
                (from: new Vector3(_width, 0f, 0f), to: new Vector3(0f, 0f, 0f), back: false),            // south
                (from: new Vector3(0f, 0f, 0f), to: new Vector3(0f, 0f, _depth), back: false),            // west
            };
            foreach (var (from, to, back) in sides)
            {
                var direction = (to - from).normalized;
                var length = Vector3.Distance(from, to);
                var rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, direction));
                var middle = (from + to) / 2f;
                var height = back ? FacadeHeight : 1.05f;

                Oriented("Facade Glass", glass, middle + Vector3.up * height / 2f, rotation, new Vector3(length, height, 0.03f));
                Oriented("Facade Sill", frame, middle + Vector3.up * 0.05f, rotation, new Vector3(length + 0.1f, 0.1f, 0.14f));
                Oriented("Facade Top", frame, middle + Vector3.up * height, rotation, new Vector3(length + 0.1f, 0.08f, 0.14f));
                Oriented("Facade LED", led, middle + new Vector3(0f, 0.012f, 0f) + Vector3.Cross(Vector3.up, direction) * 0.12f, rotation,
                    new Vector3(length, 0.02f, 0.03f));

                // Slim mullions every 1.5 m (the real facade is frameless outside – from inside you see thin joints).
                for (var t = 0f; t <= length + 0.01f; t += 1.5f)
                {
                    Box("Mullion", frame, from + direction * t + Vector3.up * height / 2f, new Vector3(0.05f, height, 0.05f));
                }

                if (back)
                {
                    var inward = Vector3.Cross(Vector3.up, direction);
                    var cornice = Oriented("Facade Cornice LED", Glowing(_theme.WallTrim), middle + Vector3.up * (height - 0.02f) + inward * 0.1f,
                        rotation, new Vector3(length, 0.04f, 0.04f));
                    cornice.AddComponent<ShimmerPanel>().Initialize(_theme.WallTrim * 1.6f, from.x + from.z);
                }
            }
        }

        /// <summary>
        /// Tower lobby (Prime Tower): the back wall along the lift core is 10 m of polished green serpentine with a
        /// brass edge and uplights, the street side is full-height glass with transoms, and the camera sides get a
        /// low glass parapet so you can look in.
        /// </summary>
        private void BuildStoneHall()
        {
            var stone = new Material(wallMaterial)
            {
                mainTexture = FloorTextures.Create(FloorPattern.Marble, _theme.Wall, Color.Lerp(_theme.Wall, new Color(0.8f, 0.88f, 0.82f), 0.75f)),
            };
            stone.SetColor("_BaseColor", Color.white);
            stone.SetFloat("_Smoothness", 0.82f);
            stone.mainTextureScale = new Vector2(_width / 3f, HallHeight / 3f);
            var brass = Tinted(wallMaterial, _theme.WallTrim);
            brass.SetFloat("_Smoothness", 0.8f);
            var glass = Tinted(glassMaterial, new Color(0.62f, 0.86f, 0.82f, 0.14f));
            var frame = Tinted(wallMaterial, new Color(0.1f, 0.11f, 0.13f));
            var warmLight = Glowing(new Color(1f, 0.85f, 0.6f));

            // North: serpentine wall with brass cap and a line of warm uplights at its foot.
            Box("Serpentine Wall", stone, new Vector3(_width / 2f, HallHeight / 2f, _depth + 0.2f), new Vector3(_width + 0.4f, HallHeight, 0.4f));
            Box("Brass Cap", brass, new Vector3(_width / 2f, HallHeight + 0.04f, _depth + 0.2f), new Vector3(_width + 0.5f, 0.08f, 0.5f));
            Box("Brass Skirting", brass, new Vector3(_width / 2f, 0.05f, _depth - 0.01f), new Vector3(_width, 0.1f, 0.02f));
            for (var x = 1.5f; x < _width; x += 3f)
            {
                Box("Uplight", warmLight, new Vector3(x, 0.03f, _depth - 0.25f), new Vector3(0.5f, 0.02f, 0.12f));
            }

            // East: street side, full-height glass with a transom every storey.
            Box("Hall Glass", glass, new Vector3(_width + 0.05f, HallHeight / 2f, _depth / 2f), new Vector3(0.03f, HallHeight, _depth));
            for (var z = 0f; z <= _depth + 0.01f; z += 1.5f)
            {
                Box("Mullion", frame, new Vector3(_width + 0.05f, HallHeight / 2f, z), new Vector3(0.06f, HallHeight, 0.06f));
            }
            for (var y = 0.05f; y <= HallHeight + 0.01f; y += HallHeight / 3f)
            {
                Box("Transom", frame, new Vector3(_width + 0.05f, y, _depth / 2f), new Vector3(0.08f, 0.08f, _depth));
            }

            // Camera sides: low glass parapet with an LED line (entrance along the south side).
            var led = Glowing(_theme.WallTrim);
            foreach (var (from, to) in new[] { (new Vector3(_width, 0f, 0f), new Vector3(0f, 0f, 0f)), (new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, _depth)) })
            {
                var direction = (to - from).normalized;
                var length = Vector3.Distance(from, to);
                var rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, direction));
                var middle = (from + to) / 2f;
                Oriented("Parapet Glass", glass, middle + Vector3.up * 0.525f, rotation, new Vector3(length, 1.05f, 0.03f));
                Oriented("Parapet Top", frame, middle + Vector3.up * 1.05f, rotation, new Vector3(length + 0.1f, 0.06f, 0.1f));
                Oriented("Parapet LED", led, middle + Vector3.up * 0.012f + Vector3.Cross(Vector3.up, direction) * 0.12f, rotation, new Vector3(length, 0.02f, 0.03f));
            }
        }

        private GameObject Oriented(string name, Material material, Vector3 position, Quaternion rotation, Vector3 size)
        {
            var box = Primitive(PrimitiveType.Cube, name, material);
            box.transform.localPosition = position;
            box.transform.localRotation = rotation;
            box.transform.localScale = size;
            return box;
        }

        // ---------- Furniture ----------

        private void BuildFurniture(IEnumerable<RoomItemDto> layout)
        {
            // Big pieces first, so small items can be stacked onto them.
            var items = (layout ?? Enumerable.Empty<RoomItemDto>()).OrderBy(i => IsStackable(i.ItemId) ? 1 : 0).ToList();
            foreach (var item in items)
            {
                var pivot = new GameObject(item.ItemId).transform;
                pivot.SetParent(_content, false);
                pivot.localPosition = new Vector3(item.Position.X, item.Position.Y, item.Position.Z);
                pivot.localRotation = Quaternion.Euler(0f, item.Rotation, 0f);

                if (item.ItemId == TicTacToeItem)
                {
                    BuildStation(pivot, "tictactoe", "table", BuildTicTacToeBoard);
                    continue;
                }
                if (item.ItemId == QuizItem)
                {
                    BuildStation(pivot, "quiz", "cabinetTelevision", top => Stack(top, "televisionModern"));
                    continue;
                }
                if (item.ItemId == CustomItems.ElevatorItem && _custom.TryBuild(item.ItemId, pivot, out _))
                {
                    BuildElevatorStation(pivot);
                    continue;
                }
                if (item.ItemId.StartsWith(CustomItems.Prefix) && _custom.TryBuild(item.ItemId, pivot, out var blocks))
                {
                    if (blocks)
                    {
                        BlockTiles(Bounds(pivot));
                    }
                    continue;
                }

                var stackable = IsStackable(item.ItemId);
                if (stackable && item.Position.Y <= 0f)
                {
                    pivot.localPosition = new Vector3(item.Position.X, SurfaceHeightAt(pivot.position), item.Position.Z);
                }

                var bounds = Spawn(pivot, item.ItemId);
                // Only furniture standing on the floor carries small items; hanging lamps don't.
                if (!stackable && item.Position.Y <= 0f)
                {
                    _surfaces.Add(bounds);
                    if (!IsFlat(item.ItemId, bounds))
                    {
                        BlockTiles(bounds);
                    }
                }
                if (IsLamp(item.ItemId) && _lampLights++ < MaxLampLights)
                {
                    AddLampLight(pivot, bounds);
                }
            }
        }

        private static bool IsStackable(string itemId) => StackableItems.Any(itemId.StartsWith);

        private static bool IsLamp(string itemId) => itemId.StartsWith("lamp") || itemId.Contains("_lamp");

        /// <summary>Local height of the highest furniture top under a world position (0 = floor).</summary>
        private float SurfaceHeightAt(Vector3 world)
        {
            var top = transform.position.y;
            foreach (var surface in _surfaces)
            {
                if (world.x >= surface.min.x && world.x <= surface.max.x && world.z >= surface.min.z && world.z <= surface.max.z)
                {
                    top = Mathf.Max(top, surface.max.y);
                }
            }
            return top - transform.position.y;
        }

        /// <summary>Instantiates a catalog model centred on the pivot; returns its world bounds.</summary>
        private Bounds Spawn(Transform pivot, string itemId)
        {
            var model = itemCatalog != null ? itemCatalog.Find(itemId) : null;
            GameObject instance;
            if (model != null)
            {
                instance = Instantiate(model, pivot, false);
                instance.transform.localScale = Vector3.one * itemCatalog.ScaleFor(itemId);
            }
            else
            {
                _missingItems.Add(itemId);
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.SetParent(pivot, false);
                instance.transform.localScale = new Vector3(0.9f, 0.7f, 0.6f);
                instance.GetComponent<Renderer>().sharedMaterial = itemMaterial;
                Destroy(instance.GetComponent<Collider>());
            }
            return PlaceCentered(instance);
        }

        /// <summary>Kenney models have their origin at a corner: centre them on the pivot, standing on it.</summary>
        private static Bounds PlaceCentered(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            var pivot = instance.transform.parent;
            var shift = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - pivot.position;
            instance.transform.position -= shift;
            bounds.center -= shift;
            return bounds;
        }

        /// <summary>Puts a catalog model on top of <paramref name="below"/> (e.g. a TV on a cabinet).</summary>
        private void Stack(Transform below, string itemId)
        {
            var bounds = Bounds(below);
            var top = new GameObject(itemId).transform;
            top.SetParent(below, false);
            top.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            Spawn(top, itemId);
        }

        private void BuildStation(Transform pivot, string gameId, string baseModel, Action<Transform> decorate)
        {
            var bounds = Spawn(pivot, baseModel);
            decorate(pivot);
            BlockTiles(bounds);
            bounds = Bounds(pivot);

            var station = pivot.gameObject.AddComponent<GameStation>();
            station.Initialize(gameId, WorldToTile(transform.InverseTransformPoint(bounds.center)));
            var collider = pivot.gameObject.AddComponent<BoxCollider>();
            collider.center = pivot.InverseTransformPoint(bounds.center);
            var size = pivot.InverseTransformVector(bounds.size + Vector3.one * 0.2f);
            collider.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            _stations.Add(station);

            // A soft glowing ring on the floor marks the station as interactive.
            var ring = Primitive(PrimitiveType.Cylinder, "Station Glow", Glowing(_theme.WallTrim));
            ring.transform.SetParent(pivot, true);
            ring.transform.position = new Vector3(bounds.center.x, transform.position.y + 0.005f, bounds.center.z);
            ring.transform.localScale = new Vector3(2.4f, 0.003f, 2.4f);
        }

        /// <summary>The lift bank: blocks its tiles, tapping it opens the lift panel; you walk to the doors.</summary>
        private void BuildElevatorStation(Transform pivot)
        {
            var bounds = Bounds(pivot);
            BlockTiles(bounds);
            var inFront = pivot.localPosition - pivot.localRotation * Vector3.forward * (CustomItems.ElevatorDepth / 2f + 0.8f);
            var station = pivot.gameObject.AddComponent<GameStation>();
            station.Initialize(ElevatorStation, WorldToTile(inFront));
            var collider = pivot.gameObject.AddComponent<BoxCollider>();
            collider.center = pivot.InverseTransformPoint(bounds.center);
            var size = pivot.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            _stations.Add(station);
        }

        private void BuildTicTacToeBoard(Transform table)
        {
            var bounds = Bounds(table);
            var board = Primitive(PrimitiveType.Cube, "Board", Tinted(wallMaterial, new Color(0.12f, 0.11f, 0.18f)));
            board.transform.SetParent(table, true);
            board.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.015f, bounds.center.z);
            board.transform.localScale = new Vector3(0.62f, 0.03f, 0.62f);

            var line = Glowing(_theme.WallTrim);
            foreach (var offset in new[] { -0.1f, 0.1f })
            {
                var a = Primitive(PrimitiveType.Cube, "Line", line);
                a.transform.SetParent(board.transform, true);
                a.transform.position = board.transform.position + new Vector3(offset, 0.02f, 0f);
                a.transform.localScale = new Vector3(0.012f, 0.01f, 0.55f);
                var b = Primitive(PrimitiveType.Cube, "Line", line);
                b.transform.SetParent(board.transform, true);
                b.transform.position = board.transform.position + new Vector3(0f, 0.02f, offset);
                b.transform.localScale = new Vector3(0.55f, 0.01f, 0.012f);
            }
        }

        // ---------- Light ----------

        private void BuildLighting()
        {
            if (_theme.Enclosure == Enclosure.Railing)
            {
                return;   // sun + fairy lights + fire pit
            }

            // Warm ceiling-like fill lights spread over the room (one per ~8 m). Glass rooms get daylight
            // through the facade, so their fill lights stay soft.
            var countX = Mathf.Max(1, Mathf.RoundToInt(_width / 8f));
            var countZ = Mathf.Max(1, Mathf.RoundToInt(_depth / 8f));
            for (var ix = 0; ix < countX; ix++)
            for (var iz = 0; iz < countZ; iz++)
            {
                var light = new GameObject("Room Light").AddComponent<Light>();
                light.transform.SetParent(_content, false);
                light.transform.localPosition = new Vector3((ix + 0.5f) * _width / countX,
                    _theme.Enclosure switch { Enclosure.GlassFacade => FacadeHeight, Enclosure.StoneHall => 6f, _ => 3.2f },
                    (iz + 0.5f) * _depth / countZ);
                light.type = LightType.Point;
                light.color = _theme.Light;
                light.intensity = _theme.LightIntensity;
                light.range = _theme.Enclosure switch { Enclosure.GlassFacade => 8f, Enclosure.StoneHall => 13f, _ => 10f };
                light.shadows = LightShadows.None;
            }
        }

        private void AddLampLight(Transform pivot, Bounds bounds)
        {
            var light = new GameObject("Lamp Light").AddComponent<Light>();
            light.transform.SetParent(pivot, true);
            light.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.1f, bounds.center.z);
            light.type = LightType.Point;
            light.color = _theme.Light;
            light.intensity = 2f;
            light.range = 3.5f;
            light.shadows = LightShadows.None;
        }

        // ---------- Tiles & helpers ----------

        private static bool IsFlat(string itemId, Bounds bounds) => itemId.StartsWith("rug") || bounds.size.y < 0.06f;

        private void BlockTiles(Bounds worldBounds)
        {
            var min = transform.InverseTransformPoint(worldBounds.min);
            var max = transform.InverseTransformPoint(worldBounds.max);
            // Shrink a little so furniture that barely touches a neighbouring tile doesn't block it.
            var from = WorldToTile(new Vector3(Mathf.Min(min.x, max.x) + 0.25f, 0f, Mathf.Min(min.z, max.z) + 0.25f));
            var to = WorldToTile(new Vector3(Mathf.Max(min.x, max.x) - 0.25f, 0f, Mathf.Max(min.z, max.z) - 0.25f));
            for (var x = from.x; x <= to.x; x++)
            for (var z = from.y; z <= to.y; z++)
            {
                _blocked.Add(new Vector2Int(x, z));
            }
        }

        private static Bounds Bounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private GameObject Primitive(PrimitiveType type, string name, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(_content, false);
            go.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private void Box(string name, Material material, Vector3 position, Vector3 size)
        {
            var box = Primitive(PrimitiveType.Cube, name, material);
            box.transform.localPosition = position;
            box.transform.localScale = size;
        }

        private static Material Tinted(Material source, Color color)
        {
            var material = new Material(source);
            material.SetColor("_BaseColor", color);
            return material;
        }

        private Material Glowing(Color color)
        {
            var material = Tinted(itemMaterial, color);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.6f);
            return material;
        }

        // ---------- Camera ----------

        private void SetupCamera()
        {
            roomCamera.orthographic = false;
            roomCamera.fieldOfView = CameraFieldOfView;
            roomCamera.nearClipPlane = 0.3f;
            roomCamera.farClipPlane = _theme.Outdoor ? 40000f : 400f;
            if (_theme.Outdoor)
            {
                roomCamera.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                roomCamera.clearFlags = CameraClearFlags.SolidColor;
                roomCamera.backgroundColor = new Color32(20, 18, 32, 255);
            }

            // Start close, centred on me; pinch out to see the whole room.
            _viewWidth = Mathf.Min(MaxViewWidth, StartViewWidth);
            _follow = _viewWidth < MaxViewWidth;
            var me = Avatar(_localUserId);
            _focus = _follow && me != null
                ? new Vector3(me.transform.localPosition.x, 0f, me.transform.localPosition.z)
                : new Vector3(_width / 2f, 0f, _depth / 2f);
            ApplyCamera();
        }

        private void ClampFocus() =>
            _focus = new Vector3(Mathf.Clamp(_focus.x, 0f, _width), 0f, Mathf.Clamp(_focus.z, 0f, _depth));

        /// <summary>Places the camera so that <see cref="_viewWidth"/> metres of floor are visible around the focus.</summary>
        private void ApplyCamera()
        {
            if (_content == null)
            {
                return;
            }
            var halfHorizontalFov = Mathf.Atan(Mathf.Tan(CameraFieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Max(0.1f, roomCamera.aspect));
            var distance = _viewWidth / 2f / Mathf.Tan(halfHorizontalFov);
            var rotation = transform.rotation * Quaternion.Euler(CameraPitch, 45f, 0f);
            var focus = transform.TransformPoint(_focus + Vector3.up * 0.8f);
            roomCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }

        /// <summary>The main camera is shared with the city; remember and restore its setup.</summary>
        private sealed class CameraState
        {
            private CameraClearFlags _clearFlags;
            private Color _background;
            private bool _orthographic;
            private float _size;
            private float _fieldOfView;
            private float _near;
            private float _far;
            private Vector3 _position;
            private Quaternion _rotation;

            public static CameraState Capture(Camera camera) => new()
            {
                _clearFlags = camera.clearFlags,
                _background = camera.backgroundColor,
                _orthographic = camera.orthographic,
                _size = camera.orthographicSize,
                _fieldOfView = camera.fieldOfView,
                _near = camera.nearClipPlane,
                _far = camera.farClipPlane,
                _position = camera.transform.position,
                _rotation = camera.transform.rotation,
            };

            public void Restore(Camera camera)
            {
                camera.clearFlags = _clearFlags;
                camera.backgroundColor = _background;
                camera.orthographic = _orthographic;
                camera.orthographicSize = _size;
                camera.fieldOfView = _fieldOfView;
                camera.nearClipPlane = _near;
                camera.farClipPlane = _far;
                camera.transform.SetPositionAndRotation(_position, _rotation);
            }
        }
    }

    /// <summary>Slowly breathing emission (LED cornice of the glass top floor).</summary>
    public sealed class ShimmerPanel : MonoBehaviour
    {
        private Material _material;
        private Color _color;
        private float _phase;

        public void Initialize(Color color, float phase)
        {
            _material = GetComponent<Renderer>().material;
            _color = color;
            _phase = phase;
        }

        private void Update()
        {
            if (_material == null)
            {
                return;
            }
            var level = 1.1f + 0.45f * Mathf.PerlinNoise(_phase, Time.time * 0.25f);
            _material.SetColor("_EmissionColor", _color * level);
        }
    }
}
