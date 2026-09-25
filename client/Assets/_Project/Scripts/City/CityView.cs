using System;
using System.Collections.Generic;
using System.Linq;
using CesiumForUnity;
using Reconnect.Contracts.Buildings;
using Unity.Mathematics;
using UnityEngine;

namespace Reconnect.Client.City
{
    /// <summary>
    /// The 3D city in Main.unity, streamed by Cesium for Unity from swisstopo (OGD, © swisstopo):
    /// terrain (swissALTI3D) with SWISSIMAGE aerial / national map draped on it, and every building
    /// from swissBUILDINGS3D. Our buildings with rooms get a tappable <see cref="CityMarker"/>.
    /// Pure presentation – data comes from the CityScreen, which also owns the UI (labels, panel).
    /// </summary>
    public sealed class CityView : MonoBehaviour
    {
        [SerializeField] private CesiumGeoreference georeference;
        [SerializeField] private Cesium3DTileset terrain;
        [SerializeField] private Cesium3DTileset buildings;
        [SerializeField] private CesiumRasterOverlay aerialOverlay;
        [SerializeField] private CesiumRasterOverlay mapOverlay;
        [SerializeField] private CityCameraController cameraController;
        [SerializeField] private Material markerMaterial;
        [SerializeField] private Material selectedMarkerMaterial;

        private readonly List<CityMarker> _markers = new();
        private CitySettings _settings;
        private Transform _markerRoot;
        private CityMarker _selected;
        private bool _cameraPlaced;
        private int _markerGeneration;

        public event Action<BuildingDto> BuildingTapped;

        public Camera Camera => cameraController.GetComponent<Camera>();
        public CityCameraController CameraController => cameraController;
        public IReadOnlyList<CityMarker> Markers => _markers;
        public MapLayer Layer { get; private set; }

        /// <summary>0–100: how much of what the camera needs is loaded (terrain + buildings).</summary>
        public float LoadProgress => Mathf.Min(terrain.ComputeLoadProgress(), buildings.ComputeLoadProgress());

        public void Initialize(CitySettings settings)
        {
            _settings = settings;
            georeference.SetOriginLongitudeLatitudeHeight(settings.originLongitude, settings.originLatitude, settings.originHeight);
            terrain.maximumScreenSpaceError = settings.terrainScreenSpaceError;
            buildings.maximumScreenSpaceError = settings.buildingsScreenSpaceError;
            aerialOverlay.maximumScreenSpaceError = settings.imageryScreenSpaceError;
            mapOverlay.maximumScreenSpaceError = settings.imageryScreenSpaceError;

            _markerRoot = new GameObject("Markers").transform;
            _markerRoot.SetParent(transform, false);

            cameraController.Configure(settings, this);
            cameraController.Tapped += OnTapped;
            SetLayer(settings.defaultLayer);
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            georeference.gameObject.SetActive(visible);   // also pauses tile streaming
            _markerRoot.gameObject.SetActive(visible);
            cameraController.enabled = visible;
            if (visible && !_cameraPlaced)
            {
                // Start above the world origin (Zürich HB by default).
                cameraController.Orbit(Vector3.zero, _settings.startDistance, _settings.startPitch, yaw: 0f);
                _cameraPlaced = true;
            }
        }

        public void SetLayer(MapLayer layer)
        {
            Layer = layer;
            aerialOverlay.enabled = layer == MapLayer.Aerial;
            mapOverlay.enabled = layer == MapLayer.Map;
        }

        /// <summary>WGS84 → Unity world position (metres, X east, Y up, Z north at the origin).</summary>
        public Vector3 ToUnity(double latitude, double longitude, double ellipsoidHeight)
        {
            var ecef = georeference.ellipsoid.LongitudeLatitudeHeightToCenteredFixed(new double3(longitude, latitude, ellipsoidHeight));
            var unity = georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
            return new Vector3((float)unity.x, (float)unity.y, (float)unity.z);
        }

        /// <summary>Unity world position → (latitude, longitude, ellipsoid height).</summary>
        public (double Latitude, double Longitude, double Height) ToGeo(Vector3 position)
        {
            var ecef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(new double3(position.x, position.y, position.z));
            var llh = georeference.ellipsoid.CenteredFixedToLongitudeLatitudeHeight(ecef);
            return (llh.y, llh.x, llh.z);
        }

        /// <summary>Places a marker per building, first at origin height, then on the real roof once sampled.</summary>
        public async void ShowBuildings(IReadOnlyList<BuildingDto> list)
        {
            ClearMarkers();
            var generation = ++_markerGeneration;
            foreach (var building in list)
            {
                var marker = CityMarker.Create(_markerRoot, building, markerMaterial, _settings.markerHeight);
                marker.transform.position = ToUnity(building.Latitude, building.Longitude, _settings.originHeight);
                _markers.Add(marker);
            }

            try
            {
                await PlaceOnRoofsAsync(generation);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Reconnect] Could not sample building heights: {ex.Message}");
            }
        }

        public void Select(BuildingDto building)
        {
            _selected?.SetMaterial(markerMaterial);
            _selected = building == null ? null : _markers.FirstOrDefault(m => m.Building.Id == building.Id);
            _selected?.SetMaterial(selectedMarkerMaterial);
        }

        /// <summary>Ground/roof height under a Unity position, from the loaded physics meshes (null if not loaded).</summary>
        public float? SurfaceHeightAt(Vector3 position)
        {
            var from = new Vector3(position.x, position.y + 3000f, position.z);
            return Physics.Raycast(from, Vector3.down, out var hit, 6000f, ~0, QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponentInParent<CityMarker>() == null
                ? hit.point.y
                : null;
        }

        private async System.Threading.Tasks.Task PlaceOnRoofsAsync(int generation)
        {
            if (_markers.Count == 0)
            {
                return;
            }

            var positions = _markers
                .Select(m => new double3(m.Building.Longitude, m.Building.Latitude, _settings.originHeight))
                .ToArray();

            // Roof height from swissBUILDINGS3D; where there is no building, fall back to the terrain.
            var roofs = await buildings.SampleHeightMostDetailed(positions);
            var ground = await terrain.SampleHeightMostDetailed(positions);
            if (generation != _markerGeneration || this == null)
            {
                return;   // markers were replaced meanwhile
            }

            for (var i = 0; i < _markers.Count; i++)
            {
                var height = roofs.sampleSuccess[i] ? roofs.longitudeLatitudeHeightPositions[i].z
                    : ground.sampleSuccess[i] ? ground.longitudeLatitudeHeightPositions[i].z
                    : _settings.originHeight;
                var building = _markers[i].Building;
                _markers[i].transform.position = ToUnity(building.Latitude, building.Longitude, height);
            }
        }

        private void OnTapped(Vector2 screenPosition)
        {
            var ray = Camera.ScreenPointToRay(screenPosition);
            // Markers stand out above the roofs, so check them first, ignoring the city geometry.
            var hit = Physics.RaycastAll(ray, Camera.farClipPlane)
                .OrderBy(h => h.distance)
                .Select(h => h.collider.GetComponentInParent<CityMarker>())
                .FirstOrDefault(m => m != null);
            if (hit != null)
            {
                BuildingTapped?.Invoke(hit.Building);
            }
        }

        private void ClearMarkers()
        {
            foreach (var marker in _markers)
            {
                Destroy(marker.gameObject);
            }
            _markers.Clear();
            _selected = null;
        }

        private void OnDestroy()
        {
            if (cameraController != null)
            {
                cameraController.Tapped -= OnTapped;
            }
        }
    }
}
