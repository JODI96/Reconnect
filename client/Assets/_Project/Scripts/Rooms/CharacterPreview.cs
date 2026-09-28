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
        private bool _face;
        private bool _walking;
        private float _height = 1f;

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
            material.SetFloat("_Smoothness", 0.6f);
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
            if (_figure != null)
            {
                _figure.SetActive(false);
                UnityEngine.Object.Destroy(_figure);
            }
            _figure = AvatarAssembler.Build(look, _turntable, _catalog.wardrobe);
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
            _height = look.Height;
            Frame();
        }

        /// <summary>Turns the figure (drag in the preview).</summary>
        public void Turn(float degrees)
        {
            _yaw = Mathf.Repeat(_yaw + degrees, 360f);
            _turntable.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        public bool FaceView
        {
            get => _face;
            set
            {
                _face = value;
                Frame();
            }
        }

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
            var focus = _face ? new Vector3(0f, 1.6f * _height, 0f) : new Vector3(0f, 0.92f * _height, 0f);
            var distance = _face ? 1.6f : 4.2f;
            _camera.transform.localPosition = focus + new Vector3(0f, _face ? 0.04f : 0.35f, -distance);
            _camera.transform.LookAt(_stage.transform.TransformPoint(focus));
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
