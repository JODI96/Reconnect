using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Reconnect.Client.City
{
    /// <summary>
    /// Map-style camera: drag to pan (mouse or one finger), scroll / pinch to zoom, tap to select.
    /// Panning projects the pointer onto the ground plane, so the ground "sticks" to the finger.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CityCameraController : MonoBehaviour
    {
        private const float TapMaxMovePixels = 12f;
        private const float TapMaxSeconds = 0.35f;
        private const float ScrollZoomFactor = 0.0015f;

        private static readonly Plane Ground = new(Vector3.up, Vector3.zero);

        private Camera _camera;
        private CitySettings _settings;
        private Bounds _panBounds;

        private Vector2 _pressPosition;
        private float _pressTime;
        private bool _pressed;
        private bool _dragging;
        private Vector3 _lastGroundPoint;
        private float _lastPinchDistance;

        /// <summary>Screen position of a tap (not a drag). Raised only when <see cref="IsPointerOverUi"/> is false.</summary>
        public event Action<Vector2> Tapped;

        /// <summary>Set by the UI so that touches on buttons/panels don't move the map.</summary>
        public Func<Vector2, bool> IsPointerOverUi { get; set; } = _ => false;

        public void Configure(CitySettings settings, Bounds panBounds)
        {
            _settings = settings;
            _panBounds = panBounds;
            _camera = GetComponent<Camera>();
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = settings.maxCameraHeight * 6f;
        }

        /// <summary>Places the camera so that it looks at <paramref name="groundTarget"/> from the given height.</summary>
        public void LookAt(Vector3 groundTarget, float height)
        {
            var pitch = _settings.cameraPitch;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            var distanceBack = height / Mathf.Tan(pitch * Mathf.Deg2Rad);
            transform.position = new Vector3(groundTarget.x, height, groundTarget.z - distanceBack);
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

            if (Touch.activeTouches.Count >= 2)
            {
                HandlePinch(Touch.activeTouches[0].screenPosition, Touch.activeTouches[1].screenPosition);
                _pressed = false;   // a pinch is never a tap
                return;
            }
            _lastPinchDistance = 0f;

            if (Touch.activeTouches.Count == 1)
            {
                var touch = Touch.activeTouches[0];
                HandlePointer(touch.screenPosition, touch.phase is UnityEngine.InputSystem.TouchPhase.Began,
                    touch.phase is UnityEngine.InputSystem.TouchPhase.Ended or UnityEngine.InputSystem.TouchPhase.Canceled, true);
            }
            else if (Mouse.current != null)
            {
                var mouse = Mouse.current;
                HandlePointer(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame,
                    mouse.leftButton.wasReleasedThisFrame, mouse.leftButton.isPressed);

                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUi(mouse.position.ReadValue()))
                {
                    Zoom(1f - scroll * ScrollZoomFactor, mouse.position.ReadValue());
                }
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
                TryGroundPoint(position, out _lastGroundPoint);
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
            if (_dragging && TryGroundPoint(position, out var groundPoint))
            {
                // Move the camera so that the ground point under the finger stays under the finger.
                Translate(_lastGroundPoint - groundPoint);
                TryGroundPoint(position, out _lastGroundPoint);
            }
        }

        private void HandlePinch(Vector2 a, Vector2 b)
        {
            var distance = Vector2.Distance(a, b);
            if (_lastPinchDistance > 0f && distance > 0f)
            {
                Zoom(_lastPinchDistance / distance, (a + b) * 0.5f);
            }
            _lastPinchDistance = distance;
        }

        /// <summary>factor &lt; 1 zooms in. Zooms towards the pointer position like map apps do.</summary>
        private void Zoom(float factor, Vector2 towardsScreenPoint)
        {
            var height = transform.position.y;
            var newHeight = Mathf.Clamp(height * factor, _settings.minCameraHeight, _settings.maxCameraHeight);
            if (Mathf.Approximately(newHeight, height) || !TryGroundPoint(towardsScreenPoint, out var focus))
            {
                return;
            }

            // Move along the ray from the camera to the focus point, keeping the focus fixed on screen.
            var t = 1f - newHeight / height;
            Translate((focus - transform.position) * t, clampHeightOnly: true);
        }

        private void Translate(Vector3 delta, bool clampHeightOnly = false)
        {
            var position = transform.position + delta;
            if (!clampHeightOnly)
            {
                position.y = transform.position.y;
            }

            // Keep the ground point in the centre of the screen inside the map area.
            var distanceBack = position.y / Mathf.Tan(_settings.cameraPitch * Mathf.Deg2Rad);
            var center = new Vector3(position.x, 0f, position.z + distanceBack);
            var clamped = _panBounds.ClosestPoint(center);
            transform.position = new Vector3(clamped.x, position.y, clamped.z - distanceBack);
        }

        private bool TryGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            var ray = _camera.ScreenPointToRay(screenPosition);
            if (Ground.Raycast(ray, out var enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }
    }
}
