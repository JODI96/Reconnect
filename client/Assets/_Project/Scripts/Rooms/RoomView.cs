using System;
using System.Collections.Generic;
using System.Linq;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// The 3D room in Main.unity: themed floor and back walls, furniture from the <see cref="ItemCatalog"/>,
    /// interactive game stations, lamps with real light, and one <see cref="AvatarView"/> per player –
    /// seen from a fixed isometric camera (Habbo style). Furniture blocks tiles; avatars walk around it.
    /// Pure presentation: RoomScreen feeds it with session events and handles the UI.
    /// </summary>
    public sealed class RoomView : MonoBehaviour
    {
        public const float TileSize = 1f;
        public const string TicTacToeItem = "game-tictactoe";
        public const string QuizItem = "game-quiz";
        private const float WallHeight = 2.6f;
        private const float TapMaxMovePixels = 12f;

        [SerializeField] private Camera roomCamera;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material floorAltMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material itemMaterial;
        [SerializeField] private Material avatarMaterial;
        [SerializeField] private ItemCatalog itemCatalog;
        [SerializeField] private AvatarCatalog avatarCatalog;

        private readonly Dictionary<Guid, AvatarView> _avatars = new();
        private readonly List<GameStation> _stations = new();
        private readonly HashSet<Vector2Int> _blocked = new();
        private Transform _content;
        private CameraState _savedCamera;
        private RoomTheme _theme;
        private Vector2 _pressPosition;
        private bool _pressed;
        private int _width;
        private int _depth;

        /// <summary>Tapped floor tile (only when <see cref="IsPointerOverUi"/> is false).</summary>
        public event Action<Vector2Int> TileTapped;

        /// <summary>Tapped minigame station (tic-tac-toe table, quiz TV).</summary>
        public event Action<GameStation> StationTapped;

        public Func<Vector2, bool> IsPointerOverUi { get; set; } = _ => false;
        public Camera Camera => roomCamera;
        public IReadOnlyCollection<AvatarView> Avatars => _avatars.Values;
        public IReadOnlyList<GameStation> Stations => _stations;
        public RoomPathfinder Pathfinder { get; private set; }

        public static Vector3 TileCenter(Vector2Int tile) => new((tile.x + 0.5f) * TileSize, 0f, (tile.y + 0.5f) * TileSize);

        public static Vector2Int WorldToTile(Vector3 local) =>
            new(Mathf.FloorToInt(local.x / TileSize), Mathf.FloorToInt(local.z / TileSize));

        public void Show(RoomSnapshotDto snapshot, Guid localUserId)
        {
            Hide();
            _width = snapshot.Width;
            _depth = snapshot.Depth;
            _theme = RoomTheme.For(snapshot.Room.Theme);
            _content = new GameObject("Room " + snapshot.Room.Name).transform;
            _content.SetParent(transform, false);

            BuildFloor();
            BuildWalls();
            BuildFurniture(snapshot.Room.Layout);
            BuildLighting();
            Pathfinder = new RoomPathfinder(_width, _depth, _blocked);

            foreach (var player in snapshot.Players)
            {
                AddPlayer(player, player.UserId == localUserId);
            }

            _savedCamera = CameraState.Capture(roomCamera);
            PlaceCamera();
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
        }

        public AvatarView Avatar(Guid userId) => _avatars.TryGetValue(userId, out var avatar) ? avatar : null;

        private void Awake() => enabled = false;

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            var position = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                _pressed = !IsPointerOverUi(position);
                _pressPosition = position;
            }
            else if (pointer.press.wasReleasedThisFrame && _pressed)
            {
                _pressed = false;
                if ((position - _pressPosition).magnitude > TapMaxMovePixels)
                {
                    return;
                }

                var ray = roomCamera.ScreenPointToRay(position);
                if (Physics.Raycast(ray, out var hit, 200f) && hit.collider.GetComponentInParent<GameStation>() is { } station)
                {
                    StationTapped?.Invoke(station);
                }
                else if (TryTileAt(ray, out var tile))
                {
                    TileTapped?.Invoke(tile);
                }
            }
        }

        private bool TryTileAt(Ray ray, out Vector2Int tile)
        {
            var floor = new Plane(transform.up, transform.position);
            if (floor.Raycast(ray, out var enter))
            {
                tile = WorldToTile(transform.InverseTransformPoint(ray.GetPoint(enter)));
                return tile.x >= 0 && tile.x < _width && tile.y >= 0 && tile.y < _depth;
            }
            tile = default;
            return false;
        }

        private void BuildFloor()
        {
            var floorA = Tinted(floorMaterial, _theme.FloorA);
            var floorB = Tinted(floorAltMaterial, _theme.FloorB);
            for (var x = 0; x < _width; x++)
            for (var z = 0; z < _depth; z++)
            {
                var tile = Primitive(PrimitiveType.Cube, $"Tile {x}/{z}", (x + z) % 2 == 0 ? floorA : floorB);
                tile.transform.localPosition = TileCenter(new Vector2Int(x, z)) + Vector3.down * 0.05f;
                tile.transform.localScale = new Vector3(TileSize * 0.985f, 0.1f, TileSize * 0.985f);
            }

            // A thick base under the floor makes the room read as a solid block (Habbo look).
            var slab = Primitive(PrimitiveType.Cube, "Floor Base", Tinted(floorAltMaterial, _theme.FloorB * 0.7f));
            slab.transform.localPosition = new Vector3(_width / 2f, -0.35f, _depth / 2f);
            slab.transform.localScale = new Vector3(_width, 0.5f, _depth);
        }

        private void BuildWalls()
        {
            var wall = Tinted(wallMaterial, _theme.Wall);
            var trim = Tinted(wallMaterial, _theme.WallTrim);

            // Habbo rooms show the two back walls; the camera looks from the south-west corner.
            Box("Wall North", wall, new Vector3(_width / 2f, WallHeight / 2f, _depth + 0.1f), new Vector3(_width + 0.4f, WallHeight, 0.2f));
            Box("Wall East", wall, new Vector3(_width + 0.1f, WallHeight / 2f, _depth / 2f), new Vector3(0.2f, WallHeight, _depth));
            Box("Skirting North", trim, new Vector3(_width / 2f, 0.08f, _depth - 0.02f), new Vector3(_width, 0.16f, 0.04f));
            Box("Skirting East", trim, new Vector3(_width - 0.02f, 0.08f, _depth / 2f), new Vector3(0.04f, 0.16f, _depth));
            Box("Wall Top North", trim, new Vector3(_width / 2f, WallHeight + 0.04f, _depth + 0.1f), new Vector3(_width + 0.44f, 0.08f, 0.24f));
            Box("Wall Top East", trim, new Vector3(_width + 0.1f, WallHeight + 0.04f, _depth / 2f), new Vector3(0.24f, 0.08f, _depth + 0.2f));

            // A few framed pictures in the theme's accent colour.
            foreach (var (x, width, height) in new[] { (2.2f, 1.1f, 0.8f), (6.3f, 0.7f, 0.9f) })
            {
                Box("Frame", trim, new Vector3(x, 1.65f, _depth - 0.01f), new Vector3(width + 0.1f, height + 0.1f, 0.03f));
                Box("Picture", Tinted(wallMaterial, Color.Lerp(_theme.WallTrim, Color.white, 0.55f)),
                    new Vector3(x, 1.65f, _depth - 0.03f), new Vector3(width, height, 0.03f));
            }
            Box("Frame", trim, new Vector3(_width - 0.01f, 1.65f, 2.8f), new Vector3(0.03f, 0.9f, 1.3f));
            Box("Picture", Tinted(wallMaterial, Color.Lerp(_theme.WallTrim, Color.white, 0.4f)),
                new Vector3(_width - 0.03f, 1.65f, 2.8f), new Vector3(0.03f, 0.8f, 1.2f));
        }

        private void BuildFurniture(IEnumerable<RoomItemDto> layout)
        {
            foreach (var item in layout ?? Enumerable.Empty<RoomItemDto>())
            {
                var pivot = new GameObject(item.ItemId).transform;
                pivot.SetParent(_content, false);
                pivot.localPosition = new Vector3(item.Position.X, item.Position.Y, item.Position.Z);
                pivot.localRotation = Quaternion.Euler(0f, item.Rotation, 0f);

                switch (item.ItemId)
                {
                    case TicTacToeItem:
                        BuildStation(pivot, "tictactoe", "table", BuildTicTacToeBoard);
                        break;
                    case QuizItem:
                        BuildStation(pivot, "quiz", "cabinetTelevision", top => Stack(top, "televisionModern"));
                        break;
                    default:
                        var bounds = Spawn(pivot, item.ItemId);
                        if (!IsFlat(item.ItemId, bounds))
                        {
                            BlockTiles(bounds);
                        }
                        if (item.ItemId.StartsWith("lamp"))
                        {
                            AddLampLight(pivot, bounds);
                        }
                        break;
                }
            }
        }

        /// <summary>Instantiates a catalog model centred on the pivot; returns its world bounds.</summary>
        private Bounds Spawn(Transform pivot, string itemId)
        {
            var model = itemCatalog != null ? itemCatalog.Find(itemId) : null;
            GameObject instance;
            if (model != null)
            {
                instance = Instantiate(model, pivot, false);
                instance.transform.localScale = Vector3.one * itemCatalog.modelScale;
            }
            else
            {
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
            collider.size = pivot.InverseTransformVector(bounds.size + Vector3.one * 0.2f);
            collider.size = new Vector3(Mathf.Abs(collider.size.x), Mathf.Abs(collider.size.y), Mathf.Abs(collider.size.z));
            _stations.Add(station);

            // A soft glowing ring on the floor marks the station as interactive.
            var ring = Primitive(PrimitiveType.Cylinder, "Station Glow", Glowing(_theme.WallTrim));
            ring.transform.SetParent(pivot, true);
            ring.transform.position = new Vector3(bounds.center.x, transform.position.y + 0.005f, bounds.center.z);
            ring.transform.localScale = new Vector3(1.9f, 0.003f, 1.9f);
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

        private void BuildLighting()
        {
            // Warm fill light in the room; lamps add their own pools of light.
            var fill = new GameObject("Room Light").AddComponent<Light>();
            fill.transform.SetParent(_content, false);
            fill.transform.localPosition = new Vector3(_width * 0.45f, 3.2f, _depth * 0.45f);
            fill.type = LightType.Point;
            fill.color = _theme.Light;
            fill.intensity = _theme.LightIntensity;
            fill.range = Mathf.Max(_width, _depth) * 1.3f;
            fill.shadows = LightShadows.None;
        }

        private void AddLampLight(Transform pivot, Bounds bounds)
        {
            var light = new GameObject("Lamp Light").AddComponent<Light>();
            light.transform.SetParent(pivot, true);
            light.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.1f, bounds.center.z);
            light.type = LightType.Point;
            light.color = _theme.Light;
            light.intensity = 2.2f;
            light.range = 3.2f;
            light.shadows = LightShadows.None;
        }

        private static bool IsFlat(string itemId, Bounds bounds) => itemId.StartsWith("rug") || bounds.size.y < 0.06f;

        private void BlockTiles(Bounds worldBounds)
        {
            var min = transform.InverseTransformPoint(worldBounds.min);
            var max = transform.InverseTransformPoint(worldBounds.max);
            // Shrink a little so a sofa that barely touches a neighbouring tile doesn't block it.
            var from = WorldToTile(new Vector3(Mathf.Min(min.x, max.x) + 0.2f, 0f, Mathf.Min(min.z, max.z) + 0.2f));
            var to = WorldToTile(new Vector3(Mathf.Max(min.x, max.x) - 0.2f, 0f, Mathf.Max(min.z, max.z) - 0.2f));
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

        /// <summary>Fixed isometric view from the south-west corner; the whole floor fits the screen width.</summary>
        private void PlaceCamera()
        {
            var center = transform.TransformPoint(new Vector3(_width / 2f, 0.9f, _depth / 2f));
            roomCamera.clearFlags = CameraClearFlags.SolidColor;
            roomCamera.backgroundColor = new Color32(20, 18, 32, 255);
            roomCamera.orthographic = true;

            // Seen diagonally, the floor is (width + depth)·cos45° wide; portrait screens are narrow.
            var halfWidth = (_width + _depth) * 0.7071f / 2f * 0.98f;   // edges may touch the screen border
            var aspect = Mathf.Max(0.1f, roomCamera.aspect);
            roomCamera.orthographicSize = Mathf.Max(halfWidth / aspect, (_width + _depth) * 0.3f);
            roomCamera.nearClipPlane = 0.1f;
            roomCamera.farClipPlane = 200f;
            roomCamera.transform.rotation = transform.rotation * Quaternion.Euler(33f, 45f, 0f);
            roomCamera.transform.position = center - roomCamera.transform.forward * 50f;
        }

        /// <summary>The main camera is shared with the city; remember and restore its setup.</summary>
        private sealed class CameraState
        {
            private CameraClearFlags _clearFlags;
            private Color _background;
            private bool _orthographic;
            private float _size;
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
                camera.nearClipPlane = _near;
                camera.farClipPlane = _far;
                camera.transform.SetPositionAndRotation(_position, _rotation);
            }
        }
    }
}
