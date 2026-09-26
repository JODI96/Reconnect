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
    /// The 3D city in Main.unity, streamed by Cesium for Unity. Two looks, chosen per app session by the
    /// backend (<see cref="ApplyMap"/>):
    /// <list type="bullet">
    /// <item>swisstopo (OGD, © swisstopo): terrain with SWISSIMAGE aerial / national map, untextured buildings.</item>
    /// <item>Google Photorealistic 3D Tiles: photorealistic city. swisstopo keeps streaming invisibly on the
    /// <see cref="DataOnlyLayer"/> – roof heights for markers and rooms and camera collisions always come from
    /// swisstopo, never from Google data (Google's terms forbid extracting geodata).</item>
    /// </list>
    /// Our buildings with rooms get a tappable <see cref="CityMarker"/>.
    /// Pure presentation – data comes from the CityScreen, which also owns the UI (labels, panel).
    /// </summary>
    public sealed class CityView : MonoBehaviour
    {
        [SerializeField] private CesiumGeoreference georeference;
        [SerializeField] private Cesium3DTileset terrain;
        [SerializeField] private Cesium3DTileset buildings;
        [SerializeField] private Cesium3DTileset googleTiles;
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

        /// <summary>Layer for data that is loaded but not drawn (swisstopo while Google is shown).</summary>
        public const string DataOnlyLayerName = "CityData";

        public event Action<BuildingDto> BuildingTapped;

        public Camera Camera => cameraController.GetComponent<Camera>();
        public CityCameraController CameraController => cameraController;
        public IReadOnlyList<CityMarker> Markers => _markers;
        public MapLayer Layer { get; private set; }

        /// <summary><see cref="MapProviders"/> value of what is shown.</summary>
        public string Provider { get; private set; } = MapProviders.Swisstopo;
        public bool IsGoogle => Provider == MapProviders.Google;

        /// <summary>0–100: how much of what the camera needs is loaded (visible city + roof-height data).</summary>
        public float LoadProgress => IsGoogle
            ? Mathf.Min(googleTiles.ComputeLoadProgress(), buildings.ComputeLoadProgress())
            : Mathf.Min(terrain.ComputeLoadProgress(), buildings.ComputeLoadProgress());

        private static int DataOnlyLayer => LayerMask.NameToLayer(DataOnlyLayerName);

        public void Initialize(CitySettings settings)
        {
            _settings = settings;
            georeference.SetOriginLongitudeLatitudeHeight(settings.originLongitude, settings.originLatitude, settings.originHeight);
            terrain.maximumScreenSpaceError = settings.terrainScreenSpaceError;
            buildings.maximumScreenSpaceError = settings.buildingsScreenSpaceError;
            aerialOverlay.maximumScreenSpaceError = settings.imageryScreenSpaceError;
            mapOverlay.maximumScreenSpaceError = settings.imageryScreenSpaceError;
            if (googleTiles != null)
            {
                googleTiles.maximumScreenSpaceError = settings.googleScreenSpaceError;
                // Google heights are ellipsoidal, swisstopo's above sea level: lower Google so roofs line up.
                googleTiles.transform.localPosition = new Vector3(0f, settings.googleHeightOffset, 0f);
                googleTiles.gameObject.SetActive(false);
            }

            _markerRoot = new GameObject("Markers").transform;
            _markerRoot.SetParent(transform, false);

            cameraController.Configure(settings, this);
            cameraController.Tapped += OnTapped;
            SetLayer(settings.defaultLayer);
            SetVisible(false);
        }

        /// <summary>Switches between Google Photorealistic 3D Tiles and swisstopo (see class summary).</summary>
        public void ApplyMap(MapSessionDto map)
        {
            var google = googleTiles != null && map.Provider == MapProviders.Google && !string.IsNullOrEmpty(map.GoogleTilesetUrl);
            if (google && googleTiles.url != map.GoogleTilesetUrl)
            {
                googleTiles.url = map.GoogleTilesetUrl;   // a new session (the URL carries the key)
            }
            Provider = google ? MapProviders.Google : MapProviders.Swisstopo;
            if (googleTiles != null)
            {
                googleTiles.gameObject.SetActive(google);
            }

            var hidden = DataOnlyLayer;
            if (hidden < 0)
            {
                Debug.LogWarning($"[Reconnect] Layer '{DataOnlyLayerName}' is missing – run Reconnect > Setup Project.");
                return;
            }
            SetLayerRecursively(terrain.gameObject, google ? hidden : 0);
            SetLayerRecursively(buildings.gameObject, google ? hidden : 0);
            var camera = Camera;
            camera.cullingMask = google ? camera.cullingMask & ~(1 << hidden) : camera.cullingMask | (1 << hidden);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                child.gameObject.layer = layer;   // new tiles inherit the tileset's layer
            }
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

        /// <summary>
        /// City as scenery behind a roof-terrace room: tiles keep streaming around the (room) camera,
        /// but markers and the city camera controls are off.
        /// </summary>
        public void ShowAsBackdrop()
        {
            georeference.gameObject.SetActive(true);
            _markerRoot.gameObject.SetActive(false);
            cameraController.enabled = false;
        }

        /// <summary>Roof position of a building with a marker (height sampled from swissBUILDINGS3D), or null.</summary>
        public Vector3? RoofPosition(Guid buildingId)
        {
            var marker = _markers.FirstOrDefault(m => m.Building.Id == buildingId);
            return marker != null ? marker.transform.position : null;
        }

        /// <summary>
        /// Where a roof terrace of the given size should stand on a building: centred on the building's
        /// marker, just above the highest roof point under the whole terrace (roofs aren't flat everywhere).
        /// </summary>
        public async System.Threading.Tasks.Task<Vector3?> RoofAnchorAsync(Guid buildingId, float width, float depth)
        {
            if (RoofPosition(buildingId) is not { } center)
            {
                return null;
            }

            const int samples = 5;
            var points = new List<double3>();
            for (var ix = 0; ix < samples; ix++)
            for (var iz = 0; iz < samples; iz++)
            {
                var world = center + new Vector3((ix / (samples - 1f) - 0.5f) * width, 0f, (iz / (samples - 1f) - 0.5f) * depth);
                var (lat, lon, _) = ToGeo(world);
                points.Add(new double3(lon, lat, _settings.originHeight));
            }

            var result = await buildings.SampleHeightMostDetailed(points.ToArray());
            var highest = center.y;
            for (var i = 0; i < points.Count; i++)
            {
                if (result.sampleSuccess[i])
                {
                    var p = result.longitudeLatitudeHeightPositions[i];
                    highest = Mathf.Max(highest, ToUnity(p.y, p.x, p.z).y);
                }
            }
            return new Vector3(center.x, highest + 0.15f, center.z);
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
