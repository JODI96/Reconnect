using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Reconnect.Client.City
{
    /// <summary>
    /// Map-style orbit camera around a point on the ground (like Google Maps 3D):
    /// <list type="bullet">
    /// <item>Pan: left mouse / one finger – the grabbed ground point stays under the pointer.</item>
    /// <item>Zoom: mouse wheel / pinch – towards the pointer.</item>
    /// <item>Rotate + tilt: right mouse drag / two-finger twist and vertical two-finger drag.</item>
    /// <item>Tap (short press without moving) raises <see cref="Tapped"/>.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CityCameraController : MonoBehaviour
    {
        private const float TapMaxMovePixels = 12f;
        private const float TapMaxSeconds = 0.35f;
        private const float ScrollZoomFactor = 0.0015f;
        private const float RotateDegreesPerPixel = 0.25f;
        private const float TiltDegreesPerPixel = 0.2f;
        private const float MinClearance = 3f;   // metres between camera and roofs/ground

        private Camera _camera;
        private CitySettings _settings;
        private CityView _city;

        private Vector3 _target;
        private float _distance;
        private float _pitch;
        private float _yaw;

        // Single pointer (mouse left / one finger)
        private bool _pressed;
        private bool _dragging;
        private Vector2 _pressPosition;
        private float _pressTime;
        private Vector3 _grabbedPoint;
        private bool _hasGrab;

        // Two fingers
        private float _lastPinchDistance;
        private float _lastTwistAngle;
        private float _lastTwoFingerY;

        /// <summary>Screen position of a tap (not a drag).</summary>
        public event Action<Vector2> Tapped;

        /// <summary>Set by the UI so that touches on buttons/panels don't move the map.</summary>
        public Func<Vector2, bool> IsPointerOverUi { get; set; } = _ => false;

        public Vector3 Target => _target;
        public float Distance => _distance;
        public float Pitch => _pitch;

        public void Configure(CitySettings settings, CityView city)
        {
            _settings = settings;
            _city = city;
            _camera = GetComponent<Camera>();
            _camera.nearClipPlane = 0.5f;
            _camera.farClipPlane = 80000f;
        }

        /// <summary>Looks at <paramref name="target"/> from the given distance, pitch (down from horizontal) and yaw (0 = north).</summary>
        public void Orbit(Vector3 target, float distance, float pitch, float yaw)
        {
            _target = target;
            _distance = distance;
            _pitch = pitch;
            _yaw = yaw;
            Apply();
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            _pressed = _dragging = false;
        }

        private void Update()
        {
            if (_settings == null)
            {
                return;
            }

            var touches = Touch.activeTouches;
            if (touches.Count >= 2)
            {
                HandleTwoFingers(touches[0], touches[1]);
                _pressed = false;   // a two-finger gesture is never a tap
                return;
            }
            _lastPinchDistance = 0f;

            if (touches.Count == 1)
            {
                var touch = touches[0];
                HandlePointer(touch.screenPosition, touch.phase == TouchPhase.Began,
                    touch.phase is TouchPhase.Ended or TouchPhase.Canceled, held: true);
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var position = mouse.position.ReadValue();
            HandlePointer(position, mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame,
                mouse.leftButton.isPressed);

            if (mouse.rightButton.isPressed && !mouse.rightButton.wasPressedThisFrame && !IsPointerOverUi(position))
            {
                var delta = mouse.delta.ReadValue();
                Rotate(delta.x * RotateDegreesPerPixel, -delta.y * TiltDegreesPerPixel);
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUi(position))
            {
                Zoom(1f - scroll * ScrollZoomFactor, position);
            }
        }

        private void HandlePointer(Vector2 position, bool began, bool ended, bool held)
        {
            if (began)
            {
                if (IsPointerOverUi(position))
                {
                    return;
                }
                _pressed = true;
                _dragging = false;
                _pressPosition = position;
                _pressTime = Time.unscaledTime;
                _hasGrab = TryGroundPoint(position, out _grabbedPoint);
                return;
            }

            if (!_pressed)
            {
                return;
            }

            if (ended || !held)
            {
                if (!_dragging && Time.unscaledTime - _pressTime <= TapMaxSeconds)
                {
                    Tapped?.Invoke(position);
                }
                _pressed = _dragging = false;
                return;
            }

            if (!_dragging && (position - _pressPosition).magnitude > TapMaxMovePixels)
            {
                _dragging = true;
            }

            if (_dragging && _hasGrab && TryGroundPointOnPlane(position, _grabbedPoint.y, out var current))
            {
                // Shift the camera so that the grabbed point is under the pointer again.
                var delta = _grabbedPoint - current;
                _target += new Vector3(delta.x, 0f, delta.z);
                Apply(updateTargetHeight: false);
            }
        }

        private void HandleTwoFingers(Touch a, Touch b)
        {
            var pa = a.screenPosition;
            var pb = b.screenPosition;
            var distance = Vector2.Distance(pa, pb);
            var angle = Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg;
            var averageY = (pa.y + pb.y) / 2f;

            if (_lastPinchDistance > 0f && distance > 0f)
            {
                Zoom(_lastPinchDistance / distance, (pa + pb) / 2f);
                Rotate(Mathf.DeltaAngle(_lastTwistAngle, angle), -(averageY - _lastTwoFingerY) * TiltDegreesPerPixel);
            }

            _lastPinchDistance = distance;
            _lastTwistAngle = angle;
            _lastTwoFingerY = averageY;
        }

        private void Rotate(float yawDelta, float pitchDelta)
        {
            _yaw = Mathf.Repeat(_yaw + yawDelta, 360f);
            _pitch = Mathf.Clamp(_pitch + pitchDelta, _settings.minPitch, _settings.maxPitch);
            Apply();
        }

        /// <summary>factor &lt; 1 zooms in, towards the ground point under <paramref name="screenPoint"/>.</summary>
        private void Zoom(float factor, Vector2 screenPoint)
        {
            var newDistance = Mathf.Clamp(_distance * factor, _settings.minDistance, _settings.maxDistance);
            if (Mathf.Approximately(newDistance, _distance))
            {
                return;
            }

            if (TryGroundPoint(screenPoint, out var focus))
            {
                var t = 1f - newDistance / _distance;
                _target += new Vector3(focus.x - _target.x, 0f, focus.z - _target.z) * t;
            }
            _distance = newDistance;
            Apply();
        }

        private void Apply(bool updateTargetHeight = true)
        {
            // Stay within the city area.
            var flat = new Vector2(_target.x, _target.z);
            if (flat.magnitude > _settings.panRadius)
            {
                flat = flat.normalized * _settings.panRadius;
                _target = new Vector3(flat.x, _target.y, flat.y);
            }

            // Keep the orbit point on the terrain/roofs (Zürich is not flat: HB 408 m, Zürichberg 680 m).
            if (updateTargetHeight && _city != null && _city.SurfaceHeightAt(_target) is { } surface)
            {
                _target.y = surface;
            }

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var position = _target - rotation * Vector3.forward * _distance;
            transform.SetPositionAndRotation(position, rotation);

            // Never dive into a roof or hill: lift the camera and keep looking at the target.
            if (_city != null && _city.SurfaceHeightAt(position) is { } below && position.y < below + MinClearance)
            {
                transform.position = new Vector3(position.x, below + MinClearance, position.z);
                transform.LookAt(_target);
            }
        }

        private bool TryGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            var ray = _camera.ScreenPointToRay(screenPosition);
            var hits = Physics.RaycastAll(ray, _camera.farClipPlane, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));   // RaycastAll is unordered
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<CityMarker>() == null)
                {
                    point = hit.point;
                    return true;
                }
            }
            // Tiles not loaded yet: fall back to a plane at the target's height.
            return TryGroundPointOnPlane(screenPosition, _target.y, out point);
        }

        private bool TryGroundPointOnPlane(Vector2 screenPosition, float height, out Vector3 point)
        {
            var ray = _camera.ScreenPointToRay(screenPosition);
            if (new Plane(Vector3.up, new Vector3(0f, height, 0f)).Raycast(ray, out var enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }
    }
}
