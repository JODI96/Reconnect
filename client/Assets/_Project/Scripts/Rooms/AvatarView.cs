using System;
using System.Collections.Generic;
using Reconnect.Contracts.Rooms;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// A player in the room: a realistic Humanoid figure (MakeHuman) that walks tile by tile, Habbo style, sits,
    /// talks while chatting and plays gestures (wave, nod, head shake) on masked animator layers.
    /// </summary>
    public sealed class AvatarView : MonoBehaviour
    {
        private const float TilesPerSecond = 2.2f;   // brisk game pace (m/s); the walk clip plays faster to match
        private const float TurnSpeed = 720f;

        private const string WalkingParameter = "Walking";
        private const string SittingParameter = "Sitting";
        private const string TalkingParameter = "Talking";
        private const float GestureFade = 6f;   // layer weight per second

        /// <summary>Gesture → animator layer that plays it and how long it lasts (see AvatarSetup).</summary>
        private static readonly Dictionary<string, (string Layer, float Seconds)> Gestures = new()
        {
            ["wave"] = ("Arm Gestures", 1.8f),
            ["yes"] = ("Head Gestures", 1.3f),
            ["no"] = ("Head Gestures", 1.3f),
        };

        private const float SitSeconds = 0.6f;   // sliding onto / off the seat
        private const float HipsAboveSeat = 0.1f;

        private readonly Queue<Vector2Int> _path = new();
        private readonly List<Vector2Int> _afterStandingUp = new();
        private Transform _hips;
        private GameObject _ring;
        private bool _seated;
        private Vector3 _seatPoint;
        private float _seatYaw;
        private float _seatBlend;
        private Vector3 _standPosition;
        private Quaternion _standRotation;
        private Animator _animator;
        private UprightPosture _posture;
        private bool _sitting;
        private Vector3 _stepTarget;
        private bool _walking;
        private int _gestureLayer = -1;
        private float _gestureUntil;
        private float _talkingUntil;

        public Guid UserId { get; private set; }
        public string DisplayName { get; private set; }
        public Vector2Int Tile { get; private set; }

        public bool IsSeated => _seated;

        /// <summary>Reached the end of a walk (e.g. the tile in front of a chair).</summary>
        public event Action<AvatarView> Arrived;

        /// <summary>World position above the head – anchor for name label and speech bubble.</summary>
        public Vector3 LabelAnchor => transform.position + Vector3.up * 2.0f;

        public static AvatarView Create(Transform parent, RoomPlayerDto player, AvatarCatalog catalog, Material fallbackMaterial, bool isLocal)
        {
            var root = new GameObject("Avatar " + player.DisplayName);
            root.transform.SetParent(parent, false);
            var color = ColorFor(player.UserId);

            var avatar = root.AddComponent<AvatarView>();
            var model = catalog != null ? catalog.CharacterFor(player.UserId) : null;
            if (model != null)
            {
                var figure = Instantiate(model, root.transform, false);
                figure.transform.localScale = Vector3.one * catalog.scale;
                avatar._animator = figure.GetComponentInChildren<Animator>();
                if (avatar._animator == null)
                {
                    avatar._animator = figure.AddComponent<Animator>();
                }
                avatar._animator.runtimeAnimatorController = catalog.animator;
                avatar._posture = figure.GetComponent<UprightPosture>();
                avatar._hips = avatar._animator.isHuman ? avatar._animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                avatar._animator.applyRootMotion = false;
                foreach (var renderer in figure.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
            else
            {
                BuildPlaceholderFigure(root.transform, fallbackMaterial, color);
            }

            if (isLocal)
            {
                // A soft ring under my own avatar.
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "Me";
                ring.transform.SetParent(root.transform, false);
                ring.transform.localScale = new Vector3(0.8f, 0.005f, 0.8f);
                ring.transform.localPosition = Vector3.up * 0.01f;
                var material = new Material(fallbackMaterial);
                material.SetColor("_BaseColor", new Color(1f, 0.36f, 0.54f));
                ring.GetComponent<Renderer>().sharedMaterial = material;
                Destroy(ring.GetComponent<Collider>());
                avatar._ring = ring;
            }

            avatar.UserId = player.UserId;
            avatar.DisplayName = player.DisplayName;
            avatar.Teleport(new Vector2Int(player.Tile.X, player.Tile.Z));
            return avatar;
        }

        /// <summary>Plays a gesture ("wave", "yes", "no") or jumps ("jump"). Sitting happens on seats: <see cref="SitOn"/>.</summary>
        public void PlayEmote(string emote)
        {
            if (_animator == null || emote == "sit" || (_seated && emote == "jump"))
            {
                return;
            }
            if (Gestures.TryGetValue(emote, out var gesture))
            {
                FadeOutGesture();
                _gestureLayer = _animator.GetLayerIndex(gesture.Layer);
                _gestureUntil = Time.time + gesture.Seconds;
            }
            _animator.SetTrigger(emote);
        }

        /// <summary>
        /// Sits down on a seat: turns to face <paramref name="yaw"/> and slides so the hips come to rest on
        /// <paramref name="point"/> (room coordinates) – fits chairs, sofas and bar stools alike.
        /// </summary>
        /// <param name="instant">Already sitting when I enter the room.</param>
        public void SitOn(Vector3 point, float yaw, bool instant = false)
        {
            _path.Clear();
            _afterStandingUp.Clear();
            _walking = false;
            if (!_seated)
            {
                _standPosition = transform.localPosition;
                _standRotation = transform.localRotation;
            }
            _seated = true;
            _sitting = true;
            _seatPoint = point;
            _seatYaw = yaw;
            if (_animator != null)
            {
                _animator.SetBool(WalkingParameter, false);
                _animator.SetBool(TalkingParameter, false);
                _animator.SetBool(SittingParameter, true);
                if (instant)
                {
                    _animator.Play("Sitting", 0, 0f);
                }
            }
            if (_posture != null)
            {
                _posture.Walking = false;
                _posture.Sitting = true;
            }
            if (_ring != null)
            {
                _ring.SetActive(false);
            }
            if (instant)
            {
                _seatBlend = 1f;
            }
        }

        /// <summary>Gets up and steps back to the tile in front of the seat.</summary>
        public void StandUp()
        {
            if (!_seated)
            {
                return;
            }
            _seated = false;
            _sitting = false;
            if (_animator != null)
            {
                _animator.SetBool(SittingParameter, false);
            }
            if (_posture != null)
            {
                _posture.Sitting = false;
            }
        }

        /// <summary>Talking body language for a few seconds (someone wrote in the chat).</summary>
        public void Talk(float seconds = 3f)
        {
            _talkingUntil = Time.time + seconds;
            if (_animator != null && !_sitting)
            {
                _animator.SetBool(TalkingParameter, true);
            }
        }

        private void FadeOutGesture()
        {
            if (_gestureLayer >= 0)
            {
                _animator.SetLayerWeight(_gestureLayer, 0f);
            }
            _gestureLayer = -1;
        }

        /// <summary>Fades the gesture layer in while the gesture plays and out afterwards; ends talking.</summary>
        private void UpdateBodyLanguage()
        {
            if (_animator == null)
            {
                return;
            }
            if (_gestureLayer >= 0)
            {
                var target = Time.time < _gestureUntil - 0.2f ? 1f : 0f;
                var weight = Mathf.MoveTowards(_animator.GetLayerWeight(_gestureLayer), target, GestureFade * Time.deltaTime);
                _animator.SetLayerWeight(_gestureLayer, weight);
                if (target == 0f && weight == 0f)
                {
                    _gestureLayer = -1;
                }
            }
            if (_talkingUntil > 0f && Time.time > _talkingUntil)
            {
                _talkingUntil = 0f;
                _animator.SetBool(TalkingParameter, false);
            }
        }

        private static void BuildPlaceholderFigure(Transform root, Material baseMaterial, Color color)
        {
            var material = new Material(baseMaterial) { color = color };
            material.SetColor("_BaseColor", color);
            foreach (var (type, scale, y) in new[]
                     {
                         (PrimitiveType.Capsule, new Vector3(0.55f, 0.6f, 0.55f), 0.6f),
                         (PrimitiveType.Sphere, Vector3.one * 0.45f, 1.45f),
                     })
            {
                var part = GameObject.CreatePrimitive(type);
                part.transform.SetParent(root, false);
                part.transform.localScale = scale;
                part.transform.localPosition = Vector3.up * y;
                part.GetComponent<Renderer>().sharedMaterial = material;
                Destroy(part.GetComponent<Collider>());
            }
        }

        public void Teleport(Vector2Int tile)
        {
            _path.Clear();
            _seated = false;
            _seatBlend = 0f;
            _walking = false;
            Tile = tile;
            transform.localPosition = RoomView.TileCenter(tile);
        }

        /// <summary>The tile the avatar stands on or is currently stepping onto – start of the next path.</summary>
        public Vector2Int NextTile => _walking ? RoomView.WorldToTile(_stepTarget) : Tile;

        /// <summary>Walks through the given tiles (from the pathfinder), finishing the current step first.</summary>
        public void WalkAlong(IReadOnlyList<Vector2Int> path)
        {
            if (_seated || _seatBlend > 0f)
            {
                // Get up first; the walk starts once standing (LateUpdate).
                StandUp();
                _afterStandingUp.Clear();
                _afterStandingUp.AddRange(path);
                return;
            }
            _path.Clear();
            foreach (var tile in path)
            {
                _path.Enqueue(tile);
            }
            if (!_walking)
            {
                NextStep();
            }
        }

        private void Update()
        {
            UpdateBodyLanguage();
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

        private void LateUpdate()
        {
            if (_seatBlend <= 0f && !_seated)
            {
                return;
            }
            _seatBlend = Mathf.MoveTowards(_seatBlend, _seated ? 1f : 0f, Time.deltaTime / SitSeconds);
            var t = Mathf.SmoothStep(0f, 1f, _seatBlend);
            transform.localRotation = Quaternion.Slerp(_standRotation, Quaternion.Euler(0f, _seatYaw, 0f), t);
            if (_hips != null && transform.parent != null)
            {
                // Where the root must be so that the animated hips rest on the seat.
                var hipsFromRoot = transform.parent.InverseTransformVector(_hips.position - transform.position);
                var onSeat = _seatPoint + Vector3.up * HipsAboveSeat - hipsFromRoot;
                transform.localPosition = Vector3.Lerp(_standPosition, onSeat, t);
            }
            if (_seatBlend <= 0f)
            {
                transform.localPosition = _standPosition;
                if (_ring != null)
                {
                    _ring.SetActive(true);
                }
                if (_afterStandingUp.Count > 0)
                {
                    var path = new List<Vector2Int>(_afterStandingUp);
                    _afterStandingUp.Clear();
                    WalkAlong(path);
                }
            }
        }

        private void NextStep()
        {
            var wasWalking = _walking;
            _walking = _path.Count > 0;
            if (_animator != null)
            {
                _animator.SetBool(WalkingParameter, _walking);
            }
            if (_posture != null)
            {
                _posture.Walking = _walking;
            }
            if (_walking)
            {
                _stepTarget = RoomView.TileCenter(_path.Dequeue());
            }
            else if (wasWalking)
            {
                Arrived?.Invoke(this);
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
