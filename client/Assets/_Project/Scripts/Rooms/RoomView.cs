using System;
using System.Collections.Generic;
using System.Linq;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// The 3D room in Main.unity: tiled floor, two walls, furniture placeholders from the layout,
    /// and one <see cref="AvatarView"/> per player – seen from a fixed isometric camera (Habbo style).
    /// Pure presentation: RoomScreen feeds it with session events and handles the UI.
    /// </summary>
    public sealed class RoomView : MonoBehaviour
    {
        public const float TileSize = 1f;
        private const float WallHeight = 2.6f;
        private const float TapMaxMovePixels = 12f;

        [SerializeField] private Camera roomCamera;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material floorAltMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material itemMaterial;
        [SerializeField] private Material avatarMaterial;

        private readonly Dictionary<Guid, AvatarView> _avatars = new();
        private Transform _content;
        private CameraState _savedCamera;
        private Vector2 _pressPosition;
        private bool _pressed;
        private int _width;
        private int _depth;

        /// <summary>Tapped floor tile (only when <see cref="IsPointerOverUi"/> is false).</summary>
        public event Action<Vector2Int> TileTapped;

        public Func<Vector2, bool> IsPointerOverUi { get; set; } = _ => false;
        public Camera Camera => roomCamera;
        public IReadOnlyCollection<AvatarView> Avatars => _avatars.Values;

        public static Vector3 TileCenter(Vector2Int tile) => new((tile.x + 0.5f) * TileSize, 0f, (tile.y + 0.5f) * TileSize);

        public static Vector2Int WorldToTile(Vector3 local) =>
            new(Mathf.FloorToInt(local.x / TileSize), Mathf.FloorToInt(local.z / TileSize));

        public void Show(RoomSnapshotDto snapshot, Guid localUserId)
        {
            Hide();
            _width = snapshot.Width;
            _depth = snapshot.Depth;
            _content = new GameObject("Room " + snapshot.Room.Name).transform;
            _content.SetParent(transform, false);

            BuildFloor();
            BuildWalls();
            BuildFurniture(snapshot.Room.Layout);
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
            _avatars[player.UserId] = AvatarView.Create(_content, player, avatarMaterial, isLocal);
        }

        public void RemovePlayer(Guid userId)
        {
            if (_avatars.Remove(userId, out var avatar))
            {
                Destroy(avatar.gameObject);
            }
        }

        public void MovePlayer(Guid userId, TilePosition tile)
        {
            if (_avatars.TryGetValue(userId, out var avatar))
            {
                avatar.WalkTo(new Vector2Int(tile.X, tile.Z));
            }
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
                if ((position - _pressPosition).magnitude <= TapMaxMovePixels && TryTileAt(position, out var tile))
                {
                    TileTapped?.Invoke(tile);
                }
            }
        }

        private bool TryTileAt(Vector2 screenPosition, out Vector2Int tile)
        {
            var ray = roomCamera.ScreenPointToRay(screenPosition);
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
            for (var x = 0; x < _width; x++)
            for (var z = 0; z < _depth; z++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Tile {x}/{z}";
                tile.transform.SetParent(_content, false);
                tile.transform.localPosition = TileCenter(new Vector2Int(x, z)) + Vector3.down * 0.05f;
                tile.transform.localScale = new Vector3(TileSize * 0.98f, 0.1f, TileSize * 0.98f);
                tile.GetComponent<Renderer>().sharedMaterial = (x + z) % 2 == 0 ? floorMaterial : floorAltMaterial;
                Destroy(tile.GetComponent<Collider>());
            }
        }

        private void BuildWalls()
        {
            // Habbo rooms show the two back walls; the camera looks from the south-west corner.
            CreateWall("Wall North", new Vector3(_width / 2f, WallHeight / 2f, _depth + 0.05f), new Vector3(_width + 0.2f, WallHeight, 0.1f));
            CreateWall("Wall East", new Vector3(_width + 0.05f, WallHeight / 2f, _depth / 2f), new Vector3(0.1f, WallHeight, _depth + 0.2f));
        }

        private void CreateWall(string name, Vector3 position, Vector3 size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(_content, false);
            wall.transform.localPosition = position;
            wall.transform.localScale = size;
            wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            Destroy(wall.GetComponent<Collider>());
        }

        /// <summary>Placeholder blocks until phase 4 brings the item catalogue.</summary>
        private void BuildFurniture(IEnumerable<RoomItemDto> layout)
        {
            foreach (var item in layout ?? Enumerable.Empty<RoomItemDto>())
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = item.ItemId;
                block.transform.SetParent(_content, false);
                block.transform.localPosition = new Vector3(item.Position.X, item.Position.Y + 0.35f, item.Position.Z);
                block.transform.localRotation = Quaternion.Euler(0f, item.Rotation, 0f);
                block.transform.localScale = new Vector3(0.9f, 0.7f, 0.6f);
                block.GetComponent<Renderer>().sharedMaterial = itemMaterial;
                Destroy(block.GetComponent<Collider>());
            }
        }

        /// <summary>Fixed isometric view from the south-west corner; the whole floor fits the screen width.</summary>
        private void PlaceCamera()
        {
            var center = transform.TransformPoint(new Vector3(_width / 2f, 0.8f, _depth / 2f));
            roomCamera.clearFlags = CameraClearFlags.SolidColor;
            roomCamera.backgroundColor = new Color32(20, 18, 32, 255);
            roomCamera.orthographic = true;

            // Seen diagonally, the floor is (width + depth)·cos45° wide; portrait screens are narrow.
            var halfWidth = (_width + _depth) * 0.7071f / 2f * 1.08f;
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
