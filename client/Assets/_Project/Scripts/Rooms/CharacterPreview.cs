using System;
using Reconnect.Contracts.Avatars;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// The character creator's stage: the figure on a turntable under studio light (key, fill, rim) far below the world,
    /// filmed by its own camera into a render texture the UI shows. Full body or face, standing or walking in place.
    /// </summary>
    public sealed class CharacterPreview : IDisposable
    {
        /// <summary>What the camera looks at: the whole look, or the part being changed.</summary>
        public enum Focus
        {
            Full,
            Face,
            Upper,
            Legs,
            Feet,
        }

        private static readonly Vector3 StagePosition = new(0f, -2000f, 0f);

        /// <summary>The stage's own layer: its camera sees only it, its lights light only it.</summary>
        private const int PreviewLayer = 30;

        private readonly AvatarCatalog _catalog;
        private readonly GameObject _stage;
        private readonly Transform _turntable;
        private readonly Camera _camera;
        private GameObject _figure;
        private Animator _animator;
        private float _yaw = 180f;
        private Focus _focus = Focus.Full;
        private bool _walking;
        private float _zoom = 1f;
        private Vector3 _cameraPosition;
        private Vector3 _cameraTarget;
        private bool _placed;

        public CharacterPreview(AvatarCatalog catalog, int width, int height)
        {
            _catalog = catalog;
            _stage = new GameObject("Character Preview");
            _stage.transform.position = StagePosition;
            _turntable = new GameObject("Turntable").transform;
            _turntable.SetParent(_stage.transform, false);

            Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Character Preview", antiAliasing = 4 };
            _camera = new GameObject("Preview Camera").AddComponent<Camera>();
            _camera.transform.SetParent(_stage.transform, false);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.11f, 0.11f, 0.15f);
            _camera.fieldOfView = 28f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 30f;
            _camera.targetTexture = Texture;
            _camera.cullingMask = 1 << PreviewLayer;

            // Studio light: warm key from the front left, cool fill from the right, rim light from behind.
            // Directional, so the face close-up is lit like the whole figure (point lights blew it out up close).
            Light(Quaternion.Euler(28f, 32f, 0f), new Color(1f, 0.93f, 0.85f), 1.25f);
            Light(Quaternion.Euler(12f, -45f, 0f), new Color(0.75f, 0.83f, 1f), 0.45f);
            Light(Quaternion.Euler(22f, 170f, 0f), new Color(1f, 1f, 1f), 0.9f);

            // A soft round floor to stand on.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            floor.transform.SetParent(_stage.transform, false);
            floor.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);
            floor.transform.localPosition = new Vector3(0f, -0.01f, 0f);
            UnityEngine.Object.Destroy(floor.GetComponent<Collider>());
            var material = new Material(catalog.wardrobe.opaque) { name = "Preview Floor" };
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", new Color(0.07f, 0.07f, 0.09f));
            material.SetFloat("_Smoothness", 0.15f);
            floor.GetComponent<Renderer>().sharedMaterial = material;
            floor.layer = PreviewLayer;
            Frame();
        }

        public RenderTexture Texture { get; }

        public AvatarLookDto Look { get; private set; }

        /// <summary>Builds the figure for <paramref name="look"/> (keeps the view: turn, face or body, walking).</summary>
        public void Show(AvatarLookDto look)
        {
            Look = look;
            // A rebuilt figure (a slider moved) carries on where the old one was in its walk or idle.
            var state = _animator != null && _animator.isActiveAndEnabled ? _animator.GetCurrentAnimatorStateInfo(0) : default;
            var carryOn = _animator != null && state.fullPathHash != 0;
            if (_figure != null)
            {
                _figure.SetActive(false);
                UnityEngine.Object.Destroy(_figure);
            }
            _figure = AvatarAssembler.Build(look, _turntable, _catalog.wardrobe, levels: 1);
            if (_figure == null)
            {
                return;
            }
            foreach (var child in _figure.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = PreviewLayer;
            }
            _figure.GetComponent<LODGroup>().ForceLOD(0);   // always the full detail here (the assembler adds the group)
            _animator = _figure.GetComponent<Animator>();
            _animator.runtimeAnimatorController = _catalog.ControllerFor(look.WalkStyle);
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // only its own camera sees it
            _animator.SetBool("Walking", _walking);
            if (carryOn)
            {
                _animator.Play(state.fullPathHash, 0, state.normalizedTime);
            }
            Frame();
        }

        /// <summary>Turns the figure (drag in the preview).</summary>
        public void Turn(float degrees)
        {
            _yaw = Mathf.Repeat(_yaw + degrees, 360f);
            _turntable.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        /// <summary>Where the camera goes (it glides there, see <see cref="Tick"/>); a new focus resets the zoom.</summary>
        public Focus View
        {
            get => _focus;
            set
            {
                if (_focus != value)
                {
                    _zoom = 1f;
                }
                _focus = value;
                Frame();
            }
        }

        /// <summary>Closer (&lt; 1) or further away (&gt; 1), within limits: pinch or mouse wheel.</summary>
        public void Zoom(float factor)
        {
            _zoom = Mathf.Clamp(_zoom * factor, 0.45f, 2.2f);
            Frame();
        }

        /// <summary>Glides the camera towards the focus (call every frame).</summary>
        public void Tick(float deltaTime)
        {
            var t = 1f - Mathf.Exp(-deltaTime * 7f);
            var position = Vector3.Lerp(_camera.transform.localPosition, _cameraPosition, t);
            var target = Vector3.Lerp(_lookAt, _cameraTarget, t);
            _lookAt = target;
            _camera.transform.localPosition = position;
            _camera.transform.LookAt(_stage.transform.TransformPoint(target));
        }

        private Vector3 _lookAt;

        /// <summary>Walks in place with the look's walk style (the clips don't move the figure).</summary>
        public bool Walking
        {
            get => _walking;
            set
            {
                _walking = value;
                if (_animator != null)
                {
                    _animator.SetBool("Walking", value);
                }
            }
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(_stage);
            Texture.Release();
            UnityEngine.Object.Destroy(Texture);
        }

        private void Frame()
        {
            _turntable.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            var tall = Wardrobe.HeightCm(Look ?? Wardrobe.Default(Wardrobe.Female)) / 100f;   // soles to crown
            var (height, distance, above) = _focus switch
            {
                Focus.Face => (0.89f * tall, 1.45f, 0.03f),
                Focus.Upper => (0.72f * tall, 2.3f, 0.1f),
                Focus.Legs => (0.3f * tall, 2.6f, 0.25f),
                Focus.Feet => (0.12f * tall, 2.5f, 0.7f),
                _ => (0.52f * tall, 4.5f, 0.35f),
            };
            _cameraTarget = new Vector3(0f, height, 0f);
            _cameraPosition = _cameraTarget + new Vector3(0f, above * _zoom, -distance * _zoom);
            if (!_placed)
            {
                _placed = true;
                _lookAt = _cameraTarget;
                _camera.transform.localPosition = _cameraPosition;
                _camera.transform.LookAt(_stage.transform.TransformPoint(_cameraTarget));
            }
        }

        private void Light(Quaternion direction, Color colour, float intensity)
        {
            var light = new GameObject("Studio Light").AddComponent<Light>();
            light.transform.SetParent(_stage.transform, false);
            light.transform.localRotation = direction;
            light.type = LightType.Directional;
            light.cullingMask = 1 << PreviewLayer;
            light.color = colour;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }
    }
}
