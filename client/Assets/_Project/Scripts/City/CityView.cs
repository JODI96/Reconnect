using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reconnect.Contracts.Buildings;
using UnityEngine;

namespace Reconnect.Client.City
{
    /// <summary>
    /// The 3D city in Main.unity: map tiles on the ground plus one placeholder block per building.
    /// Pure presentation – data comes from the CityScreen, which also owns the UI (labels, panel).
    /// </summary>
    public sealed class CityView : MonoBehaviour
    {
        private const int MaxConcurrentTileDownloads = 6;

        [SerializeField] private CityCameraController cameraController;
        [SerializeField] private Material tileMaterial;
        [SerializeField] private Material buildingMaterial;
        [SerializeField] private Material selectedBuildingMaterial;

        private readonly List<BuildingMarker> _markers = new();
        private readonly List<GameObject> _tiles = new();
        private CitySettings _settings;
        private GeoProjection _projection;
        private MapTileLoader _tileLoader;
        private Transform _tileRoot;
        private Transform _buildingRoot;
        private CancellationTokenSource _tileLoading;
        private Mesh _quad;
        private BuildingMarker _selected;
        private Bounds _mapBounds;
        private bool _cameraPlaced;

        public event Action<BuildingDto> BuildingTapped;

        public Camera Camera => cameraController.GetComponent<Camera>();
        public CityCameraController CameraController => cameraController;
        public GeoProjection Projection => _projection;
        public IReadOnlyList<BuildingMarker> Markers => _markers;
        public MapLayer Layer { get; private set; }

        /// <summary>Number of tiles still loading – used by tests and a loading indicator.</summary>
        public int PendingTiles { get; private set; }

        public void Initialize(CitySettings settings, MapTileLoader tileLoader)
        {
            _settings = settings;
            _tileLoader = tileLoader;
            _projection = new GeoProjection(settings.originLatitude, settings.originLongitude);
            Layer = settings.defaultLayer;

            _tileRoot = new GameObject("Tiles").transform;
            _tileRoot.SetParent(transform, false);
            _buildingRoot = new GameObject("Buildings").transform;
            _buildingRoot.SetParent(transform, false);

            cameraController.Tapped += OnTapped;
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            _tileRoot.gameObject.SetActive(visible);
            _buildingRoot.gameObject.SetActive(visible);
            cameraController.enabled = visible;
        }

        /// <summary>Rebuilds buildings and ground for the given data. Keeps the camera if already placed.</summary>
        public void ShowBuildings(IReadOnlyList<BuildingDto> buildings)
        {
            ClearBuildings();
            foreach (var building in buildings)
            {
                _markers.Add(CreateMarker(building));
            }

            _mapBounds = ComputeMapBounds(buildings);
            cameraController.Configure(_settings, _mapBounds);
            if (!_cameraPlaced)
            {
                // Start above the world origin (Zürich HB by default).
                cameraController.LookAt(Vector3.zero, _settings.startCameraHeight);
                _cameraPlaced = true;
            }

            ReloadTiles();
        }

        public void SetLayer(MapLayer layer)
        {
            if (layer == Layer)
            {
                return;
            }
            Layer = layer;
            ReloadTiles();
        }

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

        private void ReloadTiles()
        {
            _tileLoading?.Cancel();
            _tileLoading = new CancellationTokenSource();
            foreach (var tile in _tiles)
            {
                Destroy(tile);
            }
            _tiles.Clear();
            _ = LoadTilesAsync(_tileLoading.Token);
        }

        private async Task LoadTilesAsync(CancellationToken ct)
        {
            var zoom = _settings.tileZoom;
            var (northLat, westLon) = _projection.ToGeo(new Vector3(_mapBounds.min.x, 0f, _mapBounds.max.z));
            var (southLat, eastLon) = _projection.ToGeo(new Vector3(_mapBounds.max.x, 0f, _mapBounds.min.z));
            var (minX, minY) = WebMercatorTiles.ToTile(northLat, westLon, zoom);
            var (maxX, maxY) = WebMercatorTiles.ToTile(southLat, eastLon, zoom);

            // Load from the centre outwards so the interesting part appears first.
            var center = new Vector2((float)(minX + maxX) / 2f, (float)(minY + maxY) / 2f);
            var tiles = new List<Vector2Int>();
            for (var x = (int)minX; x <= (int)maxX; x++)
            for (var y = (int)minY; y <= (int)maxY; y++)
            {
                tiles.Add(new Vector2Int(x, y));
            }
            tiles = tiles.OrderBy(t => Vector2.Distance(t, center)).Take(_settings.maxTiles).ToList();

            PendingTiles = tiles.Count;
            var layer = Layer;
            using var throttle = new SemaphoreSlim(MaxConcurrentTileDownloads);
            try
            {
                await Task.WhenAll(tiles.Select(async tile =>
                {
                    await throttle.WaitAsync(ct);
                    try
                    {
                        var texture = await _tileLoader.LoadAsync(layer, zoom, tile.x, tile.y, ct);
                        if (texture != null && !ct.IsCancellationRequested && this != null)
                        {
                            _tiles.Add(CreateTile(tile.x, tile.y, zoom, texture));
                        }
                    }
                    finally
                    {
                        throttle.Release();
                        PendingTiles--;
                    }
                }));
            }
            catch (OperationCanceledException)
            {
                // Layer switched or view destroyed.
            }
        }

        private GameObject CreateTile(int x, int y, int zoom, Texture2D texture)
        {
            var (northLat, westLon) = WebMercatorTiles.ToGeo(x, y, zoom);
            var (southLat, eastLon) = WebMercatorTiles.ToGeo(x + 1, y + 1, zoom);
            var northWest = _projection.ToWorld(northLat, westLon);
            var southEast = _projection.ToWorld(southLat, eastLon);

            var tile = new GameObject($"Tile {zoom}/{x}/{y}", typeof(MeshFilter), typeof(MeshRenderer));
            tile.transform.SetParent(_tileRoot, false);
            tile.transform.localPosition = (northWest + southEast) / 2f;
            tile.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // Unity quad faces -Z → lay it flat, texture top = north
            tile.transform.localScale = new Vector3(southEast.x - northWest.x, northWest.z - southEast.z, 1f);

            tile.GetComponent<MeshFilter>().sharedMesh = QuadMesh();
            var renderer = tile.GetComponent<MeshRenderer>();
            renderer.material = new Material(tileMaterial) { mainTexture = texture };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tile;
        }

        private Mesh QuadMesh()
        {
            if (_quad == null)
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _quad = primitive.GetComponent<MeshFilter>().sharedMesh;
                Destroy(primitive);
            }
            return _quad;
        }

        private void OnDestroy()
        {
            _tileLoading?.Cancel();
            if (cameraController != null)
            {
                cameraController.Tapped -= OnTapped;
            }
        }
    }
}
