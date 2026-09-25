using System;
using System.Collections.Generic;
using System.Linq;
using Reconnect.Contracts.Buildings;
using UnityEngine;

namespace Reconnect.Client.City
{
    /// <summary>
    /// The 3D city in Main.unity: streamed map tiles on the ground (<see cref="TiledGround"/>) plus one
    /// placeholder block per building. Pure presentation – data comes from the CityScreen, which also
    /// owns the UI (labels, panel).
    /// </summary>
    public sealed class CityView : MonoBehaviour
    {
        [SerializeField] private CityCameraController cameraController;
        [SerializeField] private Material tileMaterial;
        [SerializeField] private Material buildingMaterial;
        [SerializeField] private Material selectedBuildingMaterial;

        private readonly List<BuildingMarker> _markers = new();
        private CitySettings _settings;
        private GeoProjection _projection;
        private TiledGround _ground;
        private Transform _tileRoot;
        private Transform _buildingRoot;
        private BuildingMarker _selected;
        private bool _visible;
        private bool _cameraPlaced;

        public event Action<BuildingDto> BuildingTapped;

        public Camera Camera => cameraController.GetComponent<Camera>();
        public CityCameraController CameraController => cameraController;
        public GeoProjection Projection => _projection;
        public IReadOnlyList<BuildingMarker> Markers => _markers;
        public TiledGround Ground => _ground;
        public MapLayer Layer => _ground.Layer;

        /// <summary>Number of wanted tiles still loading – used by tests and the loading indicator.</summary>
        public int PendingTiles => _ground.PendingTiles;

        public void Initialize(CitySettings settings, MapTileLoader tileLoader)
        {
            _settings = settings;
            _projection = new GeoProjection(settings.originLatitude, settings.originLongitude);

            _tileRoot = new GameObject("Tiles").transform;
            _tileRoot.SetParent(transform, false);
            _buildingRoot = new GameObject("Buildings").transform;
            _buildingRoot.SetParent(transform, false);
            _ground = new TiledGround(_tileRoot, tileMaterial, tileLoader, _projection, settings);

            cameraController.Tapped += OnTapped;
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            _tileRoot.gameObject.SetActive(visible);
            _buildingRoot.gameObject.SetActive(visible);
            cameraController.enabled = visible;
        }

        /// <summary>Rebuilds the buildings and the covered map area. Keeps the camera if already placed.</summary>
        public void ShowBuildings(IReadOnlyList<BuildingDto> buildings)
        {
            ClearBuildings();
            foreach (var building in buildings)
            {
                _markers.Add(CreateMarker(building));
            }

            var area = ComputeMapBounds(buildings);
            _ground.SetArea(area, _projection);
            cameraController.Configure(_settings, area);
            if (!_cameraPlaced)
            {
                // Start above the world origin (Zürich HB by default).
                cameraController.LookAt(Vector3.zero, _settings.startCameraHeight);
                _cameraPlaced = true;
            }
        }

        public void SetLayer(MapLayer layer) => _ground.SetLayer(layer);

        public void Select(BuildingDto building)
        {
            if (_selected != null)
            {
                _selected.Renderer.sharedMaterial = buildingMaterial;
            }
            _selected = building == null ? null : _markers.FirstOrDefault(m => m.Building.Id == building.Id);
            if (_selected != null)
            {
                _selected.Renderer.sharedMaterial = selectedBuildingMaterial;
            }
        }

        // LateUpdate: the camera has already moved this frame.
        private void LateUpdate()
        {
            if (_visible)
            {
                _ground?.Update(Camera);
            }
        }

        private void OnTapped(Vector2 screenPosition)
        {
            var ray = Camera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out var hit, Camera.farClipPlane) && hit.collider.TryGetComponent<BuildingMarker>(out var marker))
            {
                BuildingTapped?.Invoke(marker.Building);
            }
        }

        private BuildingMarker CreateMarker(BuildingDto building)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = building.Name;
            block.transform.SetParent(_buildingRoot, false);

            var ground = _projection.ToWorld(building.Latitude, building.Longitude);
            block.transform.localPosition = ground + Vector3.up * (_settings.buildingHeight / 2f);
            block.transform.localScale = new Vector3(_settings.buildingSize, _settings.buildingHeight, _settings.buildingSize);

            var marker = block.AddComponent<BuildingMarker>();
            marker.Initialize(building, block.GetComponent<MeshRenderer>(), _settings.buildingHeight);
            marker.Renderer.sharedMaterial = buildingMaterial;
            return marker;
        }

        private void ClearBuildings()
        {
            foreach (var marker in _markers)
            {
                Destroy(marker.gameObject);
            }
            _markers.Clear();
            _selected = null;
        }

        private Bounds ComputeMapBounds(IReadOnlyList<BuildingDto> buildings)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var building in buildings)
            {
                bounds.Encapsulate(_projection.ToWorld(building.Latitude, building.Longitude));
            }
            var margin = _settings.tileMarginMeters * 2f;
            bounds.Expand(new Vector3(margin, 0f, margin));

            // The camera looks north, so the far (northern) edge is visible much earlier – extend it.
            bounds.max += new Vector3(0f, 0f, _settings.tileMarginMeters * 2f);
            return bounds;
        }

        private void OnDestroy()
        {
            _ground?.Dispose();
            if (cameraController != null)
            {
                cameraController.Tapped -= OnTapped;
            }
        }
    }
}
