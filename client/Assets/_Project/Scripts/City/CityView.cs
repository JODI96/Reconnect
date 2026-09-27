using System;
using System.Collections.Generic;
using System.Linq;
using CesiumForUnity;
using Reconnect.Contracts.Buildings;
using Reconnect.Contracts.Rooms;
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
        private MapSessionDto _appliedMap;
        private readonly Dictionary<Guid, TowerInfo> _towers = new();
        private TowerCutaway _cutaway;
        private bool _swisstopoBuildingsHidden;

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

        /// <summary>
        /// "Grafik: Hoch": finer tiles (lower screen-space error) for Google and swisstopo – sharper houses from close up,
        /// more downloads and memory. "Normal" keeps the lighter defaults for older phones.
        /// </summary>
        public void SetDetail(bool high)
        {
            if (_settings == null)
            {
                return;
            }
            terrain.maximumScreenSpaceError = high ? _settings.highScreenSpaceError : _settings.terrainScreenSpaceError;
            buildings.maximumScreenSpaceError = high ? _settings.highScreenSpaceError : _settings.buildingsScreenSpaceError;
            if (googleTiles != null)
            {
                googleTiles.maximumScreenSpaceError = high ? _settings.googleHighScreenSpaceError : _settings.googleScreenSpaceError;
            }
        }

        /// <summary>0–100: how much of what the camera needs is loaded (visible city + roof-height data).</summary>
        public float LoadProgress => IsGoogle
            ? Mathf.Min(googleTiles.ComputeLoadProgress(), buildings.ComputeLoadProgress())
            : Mathf.Min(terrain.ComputeLoadProgress(), buildings.ComputeLoadProgress());

        private static int DataOnlyLayer => LayerMask.NameToLayer(DataOnlyLayerName);

        public void Initialize(CitySettings settings)
        {
            _settings = settings;
            georeference.SetOriginLongitudeLatitudeHeight(settings.originLongitude, settings.originLatitude, settings.originHeight);
            SetDetail(high: false);
            aerialOverlay.maximumScreenSpaceError = settings.imageryScreenSpaceError;
            mapOverlay.maximumScreenSpaceError = settings.imageryScreenSpaceError;
            if (googleTiles != null)
            {
                // Google heights are ellipsoidal, swisstopo's above sea level: lower Google so roofs line up.
                googleTiles.transform.localPosition = new Vector3(0f, settings.googleHeightOffset, 0f);
                googleTiles.gameObject.SetActive(false);
            }

            // Google's tiles and the terrain use Cesium's material, which supports clipping the real tower out.
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            _cutaway = new TowerCutaway(georeference.transform, new[] { googleTiles, terrain }, new Material(lit), new Material(lit));
            if (DataOnlyLayer >= 0)
            {
                Camera.cullingMask &= ~(1 << DataOnlyLayer);   // that layer is loaded but never drawn
            }

            _markerRoot = new GameObject("Markers").transform;
            _markerRoot.SetParent(transform, false);

            cameraController.Configure(settings, this);
            cameraController.Tapped += OnTapped;
            SetLayer(settings.defaultLayer);
            SetVisible(false);
        }

        /// <summary>Footprint and height of a tower building with a marker (measured once from swisstopo, then cached).</summary>
        public async System.Threading.Tasks.Task<TowerInfo> GetTowerAsync(Guid buildingId)
        {
            if (_towers.TryGetValue(buildingId, out var known))
            {
                return known;
            }
            if (RoofPosition(buildingId) is not { } roof)
            {
                return null;
            }
            (double, double) Geo(Vector3 position)
            {
                var geo = ToGeo(position);
                return (geo.Latitude, geo.Longitude);
            }
            // The official/OSM ground plan is best (swisstopo's models of towers can be incomplete); else measure.
            var footprint = _markers.FirstOrDefault(m => m.Building.Id == buildingId)?.Building.Footprint;
            var tower = footprint is { Count: >= 3 }
                ? await TowerInfo.FromOutlineAsync(
                    footprint.Select(p => { var u = ToUnity(p.Latitude, p.Longitude, _settings.originHeight); return new Vector2(u.x, u.z); }).ToList(),
                    buildings, terrain, Geo, ToUnity, _settings.originHeight)
                : await TowerInfo.MeasureAsync(buildings, terrain, Geo, ToUnity, roof, _settings.originHeight);
            if (tower != null)
            {
                _towers[buildingId] = tower;
                Debug.Log($"[Reconnect] Tower measured: {tower.SizeX:0.0} × {tower.SizeZ:0.0} m, yaw {tower.Yaw:0}°, " +
                          $"ground {tower.GroundY:0.0}, roof {tower.RoofY:0.0} ({tower.RoofY - tower.GroundY:0} m high)");
            }
            return tower;
        }

        /// <summary>
        /// Doll's-house view while on a storey of the tower (see <see cref="TowerCutaway"/>). Google's tiles get the
        /// real tower clipped out; swisstopo's buildings can't be clipped (own material), so in the swisstopo look they
        /// are hidden meanwhile (terrain and aerial image stay, heights keep coming from the hidden data).
        /// </summary>
        /// <summary>
        /// Where a tower storey stands: rooms with the real outline have their corner (0, 0) on the globe (the backend's
        /// geo anchor), so their glass lies exactly on the tower's facade; older rectangular rooms stand in the tower's
        /// enclosing rectangle. Returns the world point for the room's centre and its yaw.
        /// </summary>
        public (Vector3 Centre, float Yaw) StoreyPlacement(TowerInfo tower, RoomDto room, int width, int depth)
        {
            var floor = room.Floor ?? 0;
            if (room.Anchor is { } geo)
            {
                var corner = ToUnityAtOrigin(geo.Latitude, geo.Longitude);
                corner.y = tower.FloorAnchor(floor).y;
                return (corner + Quaternion.Euler(0f, geo.Yaw, 0f) * new Vector3(width / 2f, 0f, depth / 2f), geo.Yaw);
            }
            return (tower.RoomAnchor(floor, width, depth), tower.Yaw);
        }

        public void ShowTowerCutaway(TowerInfo tower, int floor)
        {
            _cutaway.Show(tower, floor);
            if (!IsGoogle)
            {
                SetSwisstopoBuildingsHidden(true);
            }
        }

        public void HideTowerCutaway()
        {
            _cutaway?.Hide();
            if (!IsGoogle)
            {
                SetSwisstopoBuildingsHidden(false);
            }
        }

        private void SetSwisstopoBuildingsHidden(bool hidden)
        {
            if (hidden == _swisstopoBuildingsHidden || DataOnlyLayer < 0)
            {
                return;
            }
            _swisstopoBuildingsHidden = hidden;
            SetLayerRecursively(buildings.gameObject, hidden ? DataOnlyLayer : 0);
        }

        /// <summary>Switches between Google Photorealistic 3D Tiles and swisstopo (see class summary).</summary>
        /// <remarks>
        /// A Google session (root tileset request) is billed once and valid for ~3 h. Passing the same
        /// <paramref name="map"/> instance again is free; a new instance from <see cref="MapService"/> means a new
        /// session – the tileset is reloaded then, because the URL (key) is the same but the session inside expired.
        /// </remarks>
        public void ApplyMap(MapSessionDto map)
        {
            if (ReferenceEquals(map, _appliedMap))
            {
                return;
            }
            var wasGoogle = IsGoogle;
            _appliedMap = map;

            var google = googleTiles != null && map.Provider == MapProviders.Google && !string.IsNullOrEmpty(map.GoogleTilesetUrl);
            if (google && googleTiles.url != map.GoogleTilesetUrl)
            {
                googleTiles.url = map.GoogleTilesetUrl;   // setting the URL reloads the tileset
            }
            else if (google)
            {
                googleTiles.RecreateTileset();   // same URL, new session (also when switched back on from swisstopo)
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
            SetLayerRecursively(buildings.gameObject, google || _swisstopoBuildingsHidden ? hidden : 0);
            Camera.cullingMask &= ~(1 << hidden);
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
            // Google shows roof superstructures swisstopo doesn't know – lift the room above them.
            var clearance = IsGoogle ? _settings.googleRoofClearance : 0f;
            return new Vector3(center.x, highest + 0.15f + clearance, center.z);
        }

        public void SetLayer(MapLayer layer)
        {
            Layer = layer;
            aerialOverlay.enabled = layer == MapLayer.Aerial;
            mapOverlay.enabled = layer == MapLayer.Map;
        }

        /// <summary>WGS84 → Unity world position (metres, X east, Y up, Z north at the origin).</summary>
        /// <summary>A place on the globe at the height of the city origin (e.g. a room's corner; its height comes from elsewhere).</summary>
        public Vector3 ToUnityAtOrigin(double latitude, double longitude) => ToUnity(latitude, longitude, _settings.originHeight);

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
            // Markers stand on the roofs: look through them to the ground/roof below.
            var from = new Vector3(position.x, position.y + 3000f, position.z);
            var hits = Physics.RaycastAll(from, Vector3.down, 6000f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<CityMarker>() == null)
                {
                    return hit.point.y;
                }
            }
            return null;
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
