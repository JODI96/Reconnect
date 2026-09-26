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
        private readonly Dictionary<int, Seat> _seats = new();
        private readonly List<Rect> _pools = new();              // water surfaces (room coordinates)
        private readonly HashSet<Vector2Int> _water = new();
        private readonly Dictionary<SeatDto, Guid> _occupied = new();
        private readonly HashSet<Vector2Int> _blocked = new();
        private readonly List<Bounds> _surfaces = new();
        private readonly List<Bounds> _obstacles = new();   // everything that blocks tiles (counters for stools)
        private readonly List<string> _missingItems = new();
        private Transform _content;
        private Transform _floorRoot;       // floor and slab (rebuilt with the layout: pools cut holes into it)
        private Transform _furnitureRoot;   // everything from the layout
        private IReadOnlyList<RoomItemDto> _layout = Array.Empty<RoomItemDto>();
        private string _themeId;
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
        private bool _buildDrag;
        private float _lastPinchDistance;

        /// <summary>Tapped floor tile (only when <see cref="IsPointerOverUi"/> is false).</summary>
        public event Action<Vector2Int> TileTapped;

        /// <summary>Tapped minigame station (tic-tac-toe table, quiz TV).</summary>
        public event Action<GameStation> StationTapped;

        /// <summary>Tapped a chair, stool or sofa (and which place on it).</summary>
        public event Action<Seat, int> SeatTapped;

        public IReadOnlyCollection<Seat> Seats => _seats.Values;

        /// <summary>The layout on display (index = the server's item index, seats refer to it).</summary>
        public IReadOnlyList<RoomItemDto> Layout => _layout;

        public int Width => _width;

        /// <summary>See-through material (glass) for build helpers.</summary>
        public Material TransparentMaterial => glassMaterial;

        /// <summary>Point on the floor in the middle of the view (local room coordinates).</summary>
        public Vector3 Focus => _focus;
        public int Depth => _depth;

        /// <summary>Build rules of this room: walls, entrance and lift zones (<see cref="RoomZones"/>).</summary>
        public RoomLayoutContext BuildContext(IReadOnlyList<RoomItemDto> layout) => RoomZones.ContextFor(_themeId, _width, _depth, layout);

        /// <summary>Local room point → point in world space (e.g. to put build helpers onto the floor).</summary>
        public Transform Content => _content;

        /// <summary>Build mode: taps and drags go to <see cref="BuildPointer"/> instead of walking/panning.</summary>
        public bool BuildMode { get; set; }

        /// <summary>Build mode pointer: (phase, local floor point). Return true to consume a drag (no camera pan).</summary>
        public Func<BuildPointerPhase, Vector3, bool> BuildPointer { get; set; }

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
            _themeId = snapshot.Room.Theme;
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

            switch (_theme.Enclosure)
            {
                case Enclosure.Railing: BuildRailing(); break;
                case Enclosure.GlassFacade: BuildGlassFacade(); break;
                case Enclosure.StoneHall: BuildStoneHall(); break;
                default: BuildWalls(); break;
            }
            BuildLighting();
            BuildLayout(snapshot.Room.Layout);
            if (groundAnchor.HasValue && Mathf.Abs(yaw) > 0.01f)
            {
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                transform.SetPositionAndRotation(groundAnchor.Value - rotation * new Vector3(_width / 2f, 0f, _depth / 2f), rotation);
            }

            foreach (var player in snapshot.Players)
            {
                AddPlayer(player, player.UserId == localUserId);
                if (player.Seat != null)
                {
                    SeatPlayer(player.UserId, player.Seat, instant: true);
                }
            }

            _savedCamera = CameraState.Capture(roomCamera);
            SetupCamera();
            if (Music != null)
            {
                Music.Play(snapshot.Room.Theme);
            }
            enabled = true;
        }

        /// <summary>
        /// Shows a new layout (build editor, or someone else saved one): floor and furniture are rebuilt, people stay
        /// where they are and stand up (seats are addressed by layout index).
        /// </summary>
        public void ApplyLayout(IReadOnlyList<RoomItemDto> layout)
        {
            if (_content == null)
            {
                return;
            }
            foreach (var userId in _occupied.Values.ToList())
            {
                UnseatPlayer(userId);
            }
            // Furniture is measured with world-space boxes: build axis-aligned, then turn back.
            var rotation = transform.rotation;
            var centre = transform.TransformPoint(new Vector3(_width / 2f, 0f, _depth / 2f));
            transform.SetPositionAndRotation(centre - new Vector3(_width / 2f, 0f, _depth / 2f), Quaternion.identity);
            BuildLayout(layout);
            transform.SetPositionAndRotation(centre - rotation * new Vector3(_width / 2f, 0f, _depth / 2f), rotation);
        }

        private void BuildLayout(IReadOnlyList<RoomItemDto> layout)
        {
            ClearLayout();
            _layout = layout ?? Array.Empty<RoomItemDto>();
            _floorRoot = new GameObject("Floor").transform;
            _floorRoot.SetParent(_content, false);
            _furnitureRoot = new GameObject("Furniture").transform;
            _furnitureRoot.SetParent(_content, false);

            FindPools(_layout);
            BuildFloor();
            BuildFurniture(_layout);
            foreach (var (x, z) in RoomLayout.BlockedTiles(_layout))
            {
                _blocked.Add(new Vector2Int(x, z));
            }
            Pathfinder = new RoomPathfinder(_width, _depth, _blocked);
            foreach (var seat in _seats.Values)
            {
                if ((!seat.HasBackrest || IsStool(seat.name)) && NearestCounter(seat) is { } counter)
                {
                    // Knees under the counter only if there is room; a closed cabinet front gets the back instead.
                    var point = seat.Points[0];
                    var gap = Vector2.Distance(new Vector2(counter.x, counter.z), new Vector2(point.x, point.z));
                    seat.FaceTowards(gap >= 0.8f ? counter : point + (point - counter));   // legs reach ~0.75 m forward
                }
                seat.ResolveApproaches(Pathfinder);
            }
        }

        private void ClearLayout()
        {
            if (_floorRoot != null)
            {
                Destroy(_floorRoot.gameObject);
            }
            if (_furnitureRoot != null)
            {
                Destroy(_furnitureRoot.gameObject);
            }
            _floorRoot = _furnitureRoot = null;
            _stations.Clear();
            _seats.Clear();
            _occupied.Clear();
            _pools.Clear();
            _water.Clear();
            _blocked.Clear();
            _surfaces.Clear();
            _obstacles.Clear();
            _missingItems.Clear();
            _lampLights = 0;
        }

        public void Hide()
        {
            ClearLayout();
            if (_content != null)
            {
                Destroy(_content.gameObject);
                _content = null;
            }
            _layout = Array.Empty<RoomItemDto>();
            _avatars.Clear();
            _savedCamera?.Restore(roomCamera);
            _savedCamera = null;
            if (Music != null)
            {
                Music.Stop();
            }
            enabled = false;
        }

        public void AddPlayer(RoomPlayerDto player, bool isLocal = false)
        {
            if (_content == null || _avatars.ContainsKey(player.UserId))
            {
                return;
            }
            var avatar = AvatarView.Create(_content, player, avatarCatalog, avatarMaterial, isLocal);
            avatar.WaterAt = IsWater;
            _avatars[player.UserId] = avatar;
        }

        public void RemovePlayer(Guid userId)
        {
            FreeSeatOf(userId);
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
            FreeSeatOf(userId);   // walking stands up
            var from = avatar.NextTile;
            var goal = Pathfinder.NearestWalkable(new Vector2Int(tile.X, tile.Z), from);
            avatar.WalkAlong(Pathfinder.FindPath(from, goal));
            if (userId == _localUserId)
            {
                _follow = true;   // walking re-centres the camera on me
            }
        }

        public AvatarView Avatar(Guid userId) => _avatars.TryGetValue(userId, out var avatar) ? avatar : null;

        /// <summary>Tile of a pool: walkable, avatars swim there.</summary>
        public bool IsWater(Vector2Int tile) => _water.Contains(tile);

        /// <summary>Pools are sunk into the floor, so the floor needs holes there: collect them before building it.</summary>
        private void FindPools(IReadOnlyList<RoomItemDto> layout)
        {
            foreach (var item in layout.Where(i => ItemDefinitions.IsPool(i.ItemId)))
            {
                var cells = RoomLayout.Footprint(item, ItemDefinitions.Find(item.ItemId));
                _pools.Add(new Rect(cells.X * BuildGrid.CellSize, cells.Z * BuildGrid.CellSize,
                    cells.Width * BuildGrid.CellSize, cells.Depth * BuildGrid.CellSize));
            }
            foreach (var (x, z) in RoomLayout.WaterTiles(layout))
            {
                _water.Add(new Vector2Int(x, z));
            }
        }

        /// <summary>The rectangle minus all pool holes, as a few rectangles.</summary>
        private List<Rect> WithoutPools(Rect area)
        {
            var pieces = new List<Rect> { area };
            foreach (var hole in _pools)
            {
                var next = new List<Rect>();
                foreach (var piece in pieces)
                {
                    if (!piece.Overlaps(hole))
                    {
                        next.Add(piece);
                        continue;
                    }
                    var cut = Rect.MinMaxRect(Mathf.Max(piece.xMin, hole.xMin), Mathf.Max(piece.yMin, hole.yMin),
                        Mathf.Min(piece.xMax, hole.xMax), Mathf.Min(piece.yMax, hole.yMax));
                    next.Add(Rect.MinMaxRect(piece.xMin, piece.yMin, cut.xMin, piece.yMax));   // left
                    next.Add(Rect.MinMaxRect(cut.xMax, piece.yMin, piece.xMax, piece.yMax));   // right
                    next.Add(Rect.MinMaxRect(cut.xMin, piece.yMin, cut.xMax, cut.yMin));       // front
                    next.Add(Rect.MinMaxRect(cut.xMin, cut.yMax, cut.xMax, piece.yMax));       // back
                }
                pieces = next.Where(r => r.width > 0.01f && r.height > 0.01f).ToList();
            }
            return pieces;
        }

        // ---------- Seats ----------

        public Seat SeatFor(int item) => _seats.TryGetValue(item, out var seat) ? seat : null;

        public bool IsFree(SeatDto seat, Guid exceptUser = default) =>
            !_occupied.TryGetValue(seat, out var user) || user == exceptUser;

        /// <summary>Puts the player on the seat (animated, or at once for people already sitting when I enter).</summary>
        public void SeatPlayer(Guid userId, SeatDto seat, bool instant = false)
        {
            if (!_avatars.TryGetValue(userId, out var avatar) || SeatFor(seat.Item) is not { } furniture || seat.Place >= furniture.Points.Count)
            {
                return;
            }
            FreeSeatOf(userId);
            _occupied[seat] = userId;
            avatar.SitOn(furniture.Points[seat.Place], furniture.Facings[seat.Place], instant);
        }

        public void UnseatPlayer(Guid userId)
        {
            FreeSeatOf(userId);
            Avatar(userId)?.StandUp();
        }

        /// <summary>Nearest free place (by walking distance from <paramref name="from"/>), or null.</summary>
        public SeatDto NearestFreeSeat(Vector2Int from, float maxDistance)
        {
            SeatDto best = null;
            var bestDistance = maxDistance * maxDistance;
            foreach (var seat in _seats.Values)
            {
                for (var place = 0; place < seat.Points.Count; place++)
                {
                    var candidate = new SeatDto(seat.Item, place);
                    var distance = (seat.Approaches[place] - from).sqrMagnitude;
                    if (distance <= bestDistance && IsFree(candidate))
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }
            }
            return best;
        }

        /// <summary>Bar stools and chairs: turned towards the bar (their footrests would pass for a backrest).</summary>
        private static bool IsStool(string itemId) => itemId.Contains("stool") || itemId.Contains("bar_chair");

        /// <summary>Closest bar counter / table next to a stool (within 1.2 m).</summary>
        private Vector3? NearestCounter(Seat seat)
        {
            var point = _content.TransformPoint(seat.Points[0]);
            Vector3? best = null;
            var bestDistance = 1.2f;
            foreach (var surface in _obstacles)
            {
                // Counters and tables: about as high as the seat or higher, and bigger than a stool.
                if (surface.max.y < point.y - 0.1f || surface.size.x * surface.size.z < 0.25f || surface.Contains(point))
                {
                    continue;
                }
                var closest = surface.ClosestPoint(new Vector3(point.x, surface.center.y, point.z));
                var distance = Vector2.Distance(new Vector2(closest.x, closest.z), new Vector2(point.x, point.z));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = _content.InverseTransformPoint(closest);
                }
            }
            return best;
        }

        private void FreeSeatOf(Guid userId)
        {
            foreach (var entry in _occupied.Where(e => e.Value == userId).ToList())
            {
                _occupied.Remove(entry.Key);
            }
        }

        /// <summary>Zooms out so the whole room is visible (used for previews/screenshots).</summary>
        public void FrameWholeRoom()
        {
            _follow = false;
            _focus = new Vector3(_width / 2f, 0f, _depth / 2f);
            _viewWidth = MaxViewWidth;
            ApplyCamera();
        }

        private float MaxViewWidth => (_width + _depth) * 0.7071f * 1.05f;

        /// <summary>Background music of the room (theme track, mute switch).</summary>
        public RoomMusic Music { get; private set; }

        private void Awake()
        {
            enabled = false;
            Music = GetComponent<RoomMusic>();
        }

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
                _buildDrag = false;
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
                    // Build mode: a drag that starts on the selected item moves it instead of the camera.
                    _buildDrag = BuildMode && BuildPointer != null && TryFloorPoint(_pressPosition, out var start)
                        && BuildPointer(BuildPointerPhase.DragStart, start);
                }
                if (_buildDrag)
                {
                    if (TryFloorPoint(position, out var at))
                    {
                        BuildPointer(BuildPointerPhase.Drag, at);
                    }
                    _lastPointer = position;
                    return;
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
            if (_buildDrag)
            {
                _buildDrag = false;
                if (TryFloorPoint(position, out var end))
                {
                    BuildPointer(BuildPointerPhase.DragEnd, end);
                }
                return;
            }
            if (_dragging)
            {
                return;
            }
            if (BuildMode)
            {
                if (BuildPointer != null && TryFloorPoint(position, out var tapped))
                {
                    BuildPointer(BuildPointerPhase.Tap, tapped);
                }
                return;
            }
            var ray = roomCamera.ScreenPointToRay(position);
            if (Physics.Raycast(ray, out var hit, 500f) && hit.collider.GetComponentInParent<GameStation>() is { } station)
            {
                StationTapped?.Invoke(station);
            }
            else if (hit.collider != null && hit.collider.GetComponentInParent<Seat>() is { } seat)
            {
                SeatTapped?.Invoke(seat, seat.NearestPlace(_content.InverseTransformPoint(hit.point)));
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

            // One piece normally; around pools several, each with its part of the texture so the pattern runs on.
            foreach (var piece in WithoutPools(new Rect(0f, 0f, _width, _depth)))
            {
                var floor = Primitive(PrimitiveType.Cube, "Floor", _pools.Count == 0 ? material : new Material(material)
                {
                    mainTextureScale = new Vector2(piece.width / FloorTextures.MetersPerTexture, piece.height / FloorTextures.MetersPerTexture),
                    mainTextureOffset = new Vector2(piece.xMin / FloorTextures.MetersPerTexture, piece.yMin / FloorTextures.MetersPerTexture),
                }, _floorRoot);
                floor.transform.localPosition = new Vector3(piece.center.x, -0.05f, piece.center.y);
                floor.transform.localScale = new Vector3(piece.width, 0.1f, piece.height);
            }

            // A thick base under the floor: a solid block indoors, the building's roof slab outdoors.
            var baseColor = _theme.Outdoor ? new Color(0.55f, 0.56f, 0.58f) : _theme.FloorB * 0.6f;
            var slabMaterial = Tinted(wallMaterial, baseColor);
            // Roof terraces: a structure that meets the roof. Upper tower storeys stand on the tower's floor plate.
            var thickness = _theme.Outdoor ? (_storey is > 0 ? 0.3f : 4f) : 0.5f;
            if (_pools.Count > 0)
            {
                thickness = Mathf.Max(thickness, CustomItems.PoolDepth + 0.3f);   // room for the basins
            }
            var margin = _theme.Outdoor ? 0.3f : 0f;
            foreach (var piece in WithoutPools(new Rect(-margin, -margin, _width + 2 * margin, _depth + 2 * margin)))
            {
                var slab = Primitive(PrimitiveType.Cube, "Floor Base", slabMaterial, _floorRoot);
                slab.transform.localPosition = new Vector3(piece.center.x, -0.1f - thickness / 2f, piece.center.y);
                slab.transform.localScale = new Vector3(piece.width, thickness, piece.height);
            }
            foreach (var pool in _pools)
            {
                // Under the basin the slab continues.
                var below = Primitive(PrimitiveType.Cube, "Floor Base", slabMaterial, _floorRoot);
                var rest = thickness + 0.1f - CustomItems.PoolDepth - 0.1f;
                below.transform.localPosition = new Vector3(pool.center.x, -CustomItems.PoolDepth - 0.1f - rest / 2f, pool.center.y);
                below.transform.localScale = new Vector3(pool.width, Mathf.Max(0.05f, rest), pool.height);
            }
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
            }
            for (var i = 0; i < eastPieces; i++)
            {
                var piece = i % 2 == 0 ? "wallWindow" : "wall";
                var position = new Vector3(_width + 0.06f, 0f, i * WallPieceWidth + WallPieceWidth / 2f);
                PlaceWallPiece(piece, position, 90f, wallTint);
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

        private void BuildFurniture(IReadOnlyList<RoomItemDto> layout)
        {
            // What stands on the floor first, so small things find the table under them.
            // The server knows seats by their index in the layout, so keep it through the sorting.
            var items = layout
                .Select((item, index) => (Item: item, Index: index, Definition: ItemDefinitions.Find(item.ItemId)))
                .OrderBy(i => i.Definition?.Kind == ItemKind.Decor ? 1 : 0)
                .ToList();
            var context = BuildContext(layout);
            foreach (var (item, index, definition) in items)
            {
                var pivot = PlaceItem(_furnitureRoot, item, definition, context);
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
                    BuildElevatorStation(pivot, item);
                    continue;
                }
                if (item.ItemId.StartsWith(CustomItems.Prefix) && _custom.TryBuild(item.ItemId, pivot, out var blocks))
                {
                    if (blocks)
                    {
                        _obstacles.Add(Bounds(pivot));
                    }
                    if (RoomSeats.PlacesFor(item.ItemId) is var loungerPlaces and > 0)
                    {
                        _seats[index] = Seat.Build(pivot, _content, Bounds(pivot), index, loungerPlaces);
                    }
                    if (definition is { HasSurface: true })
                    {
                        _surfaces.Add(Bounds(pivot));
                    }
                    continue;
                }

                var bounds = Spawn(pivot, item.ItemId);
                MountOnWall(pivot, definition, ref bounds, context);
                if (RoomSeats.PlacesFor(item.ItemId) is var places and > 0)
                {
                    _seats[index] = Seat.Build(pivot, _content, bounds, index, places);
                }
                if (definition?.Kind == ItemKind.Floor)
                {
                    _obstacles.Add(bounds);
                    if (definition.HasSurface)
                    {
                        _surfaces.Add(bounds);
                    }
                }
                if (IsLamp(item.ItemId) && _lampLights++ < MaxLampLights)
                {
                    AddLampLight(pivot, bounds);
                }
            }
        }

        /// <summary>
        /// Pivot of a layout item: centre of its build-grid footprint; small things on the table under them, paintings at
        /// their height on the wall, lamps under the ceiling. Also used by the build editor for its preview.
        /// </summary>
        public Transform PlaceItem(Transform parent, RoomItemDto item, ItemDefinition definition, RoomLayoutContext context)
        {
            var pivot = new GameObject(item.ItemId).transform;
            pivot.SetParent(parent, false);
            var position = new Vector3(item.Position.X, 0f, item.Position.Z);
            if (definition != null)
            {
                var (x, z) = RoomLayout.Centre(RoomLayout.Footprint(item, definition));
                position = new Vector3(x, definition.Kind switch
                {
                    ItemKind.Wall or ItemKind.Ceiling => definition.MountHeight,
                    _ => 0f,
                }, z);
            }
            pivot.localPosition = position;
            pivot.localRotation = Quaternion.Euler(0f, item.Rotation, 0f);
            if (definition?.Kind == ItemKind.Decor)
            {
                pivot.localPosition = new Vector3(position.x, SurfaceHeightAt(pivot.position), position.z);
            }
            return pivot;
        }

        /// <summary>
        /// The model of a layout item without anything interactive (seats, stations, lights) – for the build editor's
        /// ghost. <paramref name="surfaceHeight"/> overrides the table height for small things carried with their table.
        /// Returns the world bounds.
        /// </summary>
        public Bounds BuildGhost(Transform parent, RoomItemDto item, ItemDefinition definition, RoomLayoutContext context, float? surfaceHeight = null)
        {
            var pivot = PlaceItem(parent, item, definition, context);
            if (surfaceHeight is { } height)
            {
                pivot.localPosition = new Vector3(pivot.localPosition.x, height, pivot.localPosition.z);
            }
            if (item.ItemId == TicTacToeItem || item.ItemId == QuizItem)
            {
                return Spawn(pivot, item.ItemId == TicTacToeItem ? "table" : "cabinetTelevision");
            }
            if (item.ItemId.StartsWith(CustomItems.Prefix) && _custom.TryBuild(item.ItemId, pivot, out _))
            {
                return Bounds(pivot);
            }
            var bounds = Spawn(pivot, item.ItemId);
            MountOnWall(pivot, definition, ref bounds, context);
            return bounds;
        }

        /// <summary>Paintings and wall cabinets hang flat against the wall their footprint touches.</summary>
        private void MountOnWall(Transform pivot, ItemDefinition definition, ref Bounds bounds, RoomLayoutContext context)
        {
            if (definition?.Kind != ItemKind.Wall)
            {
                return;
            }
            var wall = RoomLayout.WallOf(context, RoomLayout.Footprint(RoomItemAt(pivot), definition));
            var inner = transform.TransformPoint(new Vector3(_width, 0f, _depth));
            var shift = wall switch
            {
                WallSides.North => new Vector3(0f, 0f, inner.z - 0.01f - bounds.max.z),
                WallSides.East => new Vector3(inner.x - 0.01f - bounds.max.x, 0f, 0f),
                WallSides.South => new Vector3(0f, 0f, transform.position.z + 0.01f - bounds.min.z),
                WallSides.West => new Vector3(transform.position.x + 0.01f - bounds.min.x, 0f, 0f),
                _ => Vector3.zero,
            };
            pivot.position += shift;
            bounds.center += shift;
        }

        private RoomItemDto RoomItemAt(Transform pivot) =>
            new(pivot.name, new Vector3Dto(pivot.localPosition.x, 0f, pivot.localPosition.z), pivot.localEulerAngles.y);

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
            _obstacles.Add(bounds);
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

        /// <summary>The lift bank: tapping it opens the lift panel; you walk to the doors (same landing as the server).</summary>
        private void BuildElevatorStation(Transform pivot, RoomItemDto item)
        {
            var bounds = Bounds(pivot);
            _obstacles.Add(bounds);
            var (x, z) = RoomZones.ElevatorLanding(item);
            var station = pivot.gameObject.AddComponent<GameStation>();
            station.Initialize(ElevatorStation, new Vector2Int(x, z));
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

        private GameObject Primitive(PrimitiveType type, string name, Material material, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : _content, false);
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

    /// <summary>What the pointer does in build mode (points are local floor coordinates of the room).</summary>
    public enum BuildPointerPhase
    {
        Tap,
        DragStart,
        Drag,
        DragEnd,
    }
}
