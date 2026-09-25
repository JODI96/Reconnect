using System;
using System.Collections.Generic;
using Reconnect.Contracts.Rooms;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// A player in the room: placeholder figure (body + head) that walks tile by tile, Habbo style.
    /// Real avatar models and animations come later; the public API stays the same.
    /// </summary>
    public sealed class AvatarView : MonoBehaviour
    {
        private const float TilesPerSecond = 2.5f;
        private const float TurnSpeed = 720f;

        private readonly Queue<Vector2Int> _path = new();
        private Vector3 _stepTarget;
        private bool _walking;

        public Guid UserId { get; private set; }
        public string DisplayName { get; private set; }
        public Vector2Int Tile { get; private set; }

        /// <summary>World position above the head – anchor for name label and speech bubble.</summary>
        public Vector3 LabelAnchor => transform.position + Vector3.up * 2.1f;

        public static AvatarView Create(Transform parent, RoomPlayerDto player, Material baseMaterial, bool isLocal)
        {
            var root = new GameObject("Avatar " + player.DisplayName);
            root.transform.SetParent(parent, false);

            var color = ColorFor(player.UserId);
            var material = new Material(baseMaterial) { color = color };
            material.SetColor("_BaseColor", color);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.55f, 0.6f, 0.55f);
            body.transform.localPosition = Vector3.up * 0.6f;
            body.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(body.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(root.transform, false);
            head.transform.localScale = Vector3.one * 0.45f;
            head.transform.localPosition = Vector3.up * 1.45f;
            head.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(head.GetComponent<Collider>());

            // Nose, so you can see where someone is looking.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.transform.SetParent(head.transform, false);
            nose.transform.localScale = new Vector3(0.25f, 0.2f, 0.35f);
            nose.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            nose.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(nose.GetComponent<Collider>());

            if (isLocal)
            {
                // A ring under my own avatar.
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.transform.SetParent(root.transform, false);
                ring.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
                ring.transform.localPosition = Vector3.up * 0.02f;
                ring.GetComponent<Renderer>().sharedMaterial = material;
                Destroy(ring.GetComponent<Collider>());
            }

            var avatar = root.AddComponent<AvatarView>();
            avatar.UserId = player.UserId;
            avatar.DisplayName = player.DisplayName;
            avatar.Teleport(new Vector2Int(player.Tile.X, player.Tile.Z));
            return avatar;
        }

        public void Teleport(Vector2Int tile)
        {
            _path.Clear();
            _walking = false;
            Tile = tile;
            transform.localPosition = RoomView.TileCenter(tile);
        }

        /// <summary>Walks to <paramref name="destination"/>: diagonal steps first, then straight (like Habbo).</summary>
        public void WalkTo(Vector2Int destination)
        {
            _path.Clear();
            var current = _walking ? RoomView.WorldToTile(_stepTarget) : Tile;
            while (current != destination)
            {
                current += new Vector2Int(Math.Sign(destination.x - current.x), Math.Sign(destination.y - current.y));
                _path.Enqueue(current);
            }
            if (!_walking)
            {
                NextStep();
            }
        }

        private void Update()
        {
            if (!_walking)
            {
                return;
            }

            var position = transform.localPosition;
            var direction = _stepTarget - position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z));
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, look, TurnSpeed * Time.deltaTime);
            }

            transform.localPosition = Vector3.MoveTowards(position, _stepTarget, TilesPerSecond * Time.deltaTime);
            if ((transform.localPosition - _stepTarget).sqrMagnitude < 0.0001f)
            {
                Tile = RoomView.WorldToTile(_stepTarget);
                NextStep();
            }
        }

        private void NextStep()
        {
            _walking = _path.Count > 0;
            if (_walking)
            {
                _stepTarget = RoomView.TileCenter(_path.Dequeue());
            }
        }

        /// <summary>Stable colour per user, so people are recognisable.</summary>
        private static Color ColorFor(Guid userId)
        {
            var hue = (userId.GetHashCode() & 0xFFFF) / 65535f;
            return Color.HSVToRGB(hue, 0.55f, 0.95f);
        }
    }
}
