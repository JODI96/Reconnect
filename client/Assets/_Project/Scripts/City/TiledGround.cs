using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Reconnect.Client.City
{
    /// <summary>
    /// Streams swisstopo map tiles as a level-of-detail ground: every frame the camera decides which
    /// tiles are needed (<see cref="TileLodSelector"/>); missing ones are downloaded nearest-first,
    /// a coarser loaded ancestor fills the gap meanwhile, and unused tiles are evicted.
    /// Layering: tiles don't write depth and are drawn in zoom order (render queue), so a sharp tile
    /// always covers its coarse fallback – no z-fighting.
    /// </summary>
    public sealed class TiledGround : IDisposable
    {
        private const int RenderQueueBase = 2010;   // after opaque geometry (buildings) at 2000
        private const int MaxConcurrentDownloads = 8;
        private const int FallbackChildDepth = 2;    // when zooming out, keep showing loaded children

        private readonly Transform _root;
        private readonly Material _tileMaterial;
        private readonly MapTileLoader _loader;
        private readonly TileLodSelector _selector;
        private readonly CitySettings _settings;

        private readonly Dictionary<TileId, TileNode> _nodes = new();
        private readonly List<TileId> _roots = new();
        private readonly List<TileId> _desired = new();
        private readonly HashSet<TileId> _shown = new();
        private Mesh _quad;
        private CancellationTokenSource _lifetime = new();
        private int _downloading;
        private int _generation;
        private bool _dirty = true;
        private Vector3 _lastCameraPosition;
        private Quaternion _lastCameraRotation;

        public TiledGround(Transform root, Material tileMaterial, MapTileLoader loader, GeoProjection projection, CitySettings settings)
        {
            _root = root;
            _tileMaterial = tileMaterial;
            _loader = loader;
            _settings = settings;
            _selector = new TileLodSelector(projection);
            Layer = settings.defaultLayer;
        }

        public MapLayer Layer { get; private set; }

        /// <summary>Wanted tiles that are not loaded yet (downloading or queued).</summary>
        public int PendingTiles { get; private set; }

        public int VisibleTiles => _shown.Count;
        public int LoadedTiles => _nodes.Values.Count(n => n.State == TileState.Loaded);
        public int MaxVisibleZoom => _shown.Count == 0 ? 0 : _shown.Max(t => t.Zoom);

        /// <summary>Area that must always be covered (by the coarse base tiles).</summary>
        public void SetArea(Bounds worldArea, GeoProjection projection)
        {
            var zoom = _settings.baseZoom;
            var (northLat, westLon) = projection.ToGeo(new Vector3(worldArea.min.x, 0f, worldArea.max.z));
            var (southLat, eastLon) = projection.ToGeo(new Vector3(worldArea.max.x, 0f, worldArea.min.z));
            var (minX, minY) = WebMercatorTiles.ToTile(northLat, westLon, zoom);
            var (maxX, maxY) = WebMercatorTiles.ToTile(southLat, eastLon, zoom);

            _roots.Clear();
            for (var x = (int)minX; x <= (int)maxX; x++)
            for (var y = (int)minY; y <= (int)maxY; y++)
            {
                _roots.Add(new TileId(zoom, x, y));
            }
            _dirty = true;
        }

        public void SetLayer(MapLayer layer)
        {
            if (layer == Layer)
            {
                return;
            }
            Layer = layer;
            Clear();
        }

        /// <summary>Call every frame while the city is visible.</summary>
        public void Update(Camera camera)
        {
            if (_roots.Count == 0)
            {
                return;
            }

            var cameraMoved = camera.transform.position != _lastCameraPosition || camera.transform.rotation != _lastCameraRotation;
            if (!cameraMoved && !_dirty)
            {
                return;
            }
            _lastCameraPosition = camera.transform.position;
            _lastCameraRotation = camera.transform.rotation;
            _dirty = false;

            var pixelsPerRadian = camera.pixelHeight / (2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            _selector.Select(_roots, camera.transform.position, pixelsPerRadian, _settings.tileDetail,
                Mathf.Min(Layer.MaxZoom(), _settings.maxZoom), GeometryUtility.CalculateFrustumPlanes(camera), _desired);

            RequestDownloads(camera.transform.position);
            UpdateVisibility();
            Evict();
        }

        private void RequestDownloads(Vector3 cameraPosition)
        {
            // The coarse base layer first (never leaves holes), then the wanted tiles nearest-first.
            var wanted = _roots
                .Concat(_desired.OrderBy(t => Vector3.SqrMagnitude(_selector.WorldBounds(t).ClosestPoint(cameraPosition) - cameraPosition)))
                .Where(t => !_nodes.TryGetValue(t, out var node) || node.State == TileState.Missing)
                .ToList();

            PendingTiles = _downloading + wanted.Count;
            foreach (var tile in wanted)
            {
                if (_downloading >= MaxConcurrentDownloads)
                {
                    break;
                }
                StartDownload(tile);
            }
        }

        private async void StartDownload(TileId tile)
        {
            var node = GetOrCreateNode(tile);
            node.State = TileState.Loading;
            _downloading++;
            var layer = Layer;
            var generation = _generation;
            var ct = _lifetime.Token;
            try
            {
                var texture = await _loader.LoadAsync(layer, tile, ct);
                if (ct.IsCancellationRequested || layer != Layer)
                {
                    if (texture != null)
                    {
                        UnityEngine.Object.Destroy(texture);
                    }
                    return;
                }

                if (texture == null)
                {
                    node.State = TileState.Failed;
                    return;
                }
                node.Attach(CreateTileObject(tile, texture), texture);
            }
            catch (OperationCanceledException)
            {
                // Layer switched or city destroyed.
            }
            finally
            {
                if (generation == _generation)   // downloads from before Clear() were already discounted
                {
                    _downloading--;
                }
                _dirty = true;   // re-evaluate visibility and start the next downloads
            }
        }

        private void UpdateVisibility()
        {
            _shown.Clear();
            foreach (var tile in _desired)
            {
                if (IsLoaded(tile))
                {
                    _shown.Add(tile);
                    continue;
                }

                // Fallback while loading: nearest loaded ancestor (zooming in) …
                for (var zoom = tile.Zoom - 1; zoom >= _settings.baseZoom; zoom--)
                {
                    var ancestor = tile.AncestorAt(zoom);
                    if (IsLoaded(ancestor))
                    {
                        _shown.Add(ancestor);
                        break;
                    }
                }
                // … and already loaded sharper children (zooming out).
                AddLoadedDescendants(tile, FallbackChildDepth);
            }

            var now = Time.unscaledTime;
            foreach (var node in _nodes.Values)
            {
                var visible = _shown.Contains(node.Id);
                node.SetVisible(visible);
                if (visible)
                {
                    node.LastUsed = now;
                }
            }
        }

        private void AddLoadedDescendants(TileId tile, int depth)
        {
            if (depth == 0)
            {
                return;
            }
            foreach (var child in tile.Children)
            {
                if (IsLoaded(child))
                {
                    _shown.Add(child);
                }
                else
                {
                    AddLoadedDescendants(child, depth - 1);
                }
            }
        }

        /// <summary>Destroys the least recently shown tiles when over budget. Base tiles are kept.</summary>
        private void Evict()
        {
            var loaded = _nodes.Values.Where(n => n.State == TileState.Loaded).ToList();
            var excess = loaded.Count - _settings.maxLoadedTiles;
            if (excess <= 0)
            {
                return;
            }

            foreach (var node in loaded
                         .Where(n => !_shown.Contains(n.Id) && n.Id.Zoom > _settings.baseZoom)
                         .OrderBy(n => n.LastUsed)
                         .Take(excess))
            {
                node.Destroy();
                _nodes.Remove(node.Id);
            }
        }

        private bool IsLoaded(TileId tile) => _nodes.TryGetValue(tile, out var node) && node.State == TileState.Loaded;

        private TileNode GetOrCreateNode(TileId tile)
        {
            if (!_nodes.TryGetValue(tile, out var node))
            {
                node = new TileNode(tile);
                _nodes[tile] = node;
            }
            return node;
        }

        private GameObject CreateTileObject(TileId tile, Texture2D texture)
        {
            var bounds = _selector.WorldBounds(tile);
            var go = new GameObject($"Tile {tile}", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(bounds.center.x, 0f, bounds.center.z);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // Unity quad faces -Z → lay flat, texture top = north
            go.transform.localScale = new Vector3(bounds.size.x, bounds.size.z, 1f);
            go.SetActive(false);

            go.GetComponent<MeshFilter>().sharedMesh = QuadMesh();
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(_tileMaterial)
            {
                mainTexture = texture,
                renderQueue = RenderQueueBase + tile.Zoom,   // sharper tiles are drawn over coarser ones
            };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private Mesh QuadMesh()
        {
            if (_quad == null)
            {
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _quad = primitive.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.Destroy(primitive);
            }
            return _quad;
        }

        private void Clear()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _lifetime = new CancellationTokenSource();
            foreach (var node in _nodes.Values)
            {
                node.Destroy();
            }
            _nodes.Clear();
            _shown.Clear();
            _downloading = 0;
            _generation++;
            _dirty = true;
        }

        public void Dispose()
        {
            Clear();
            _lifetime.Cancel();
        }

        private enum TileState
        {
            Missing,
            Loading,
            Loaded,
            Failed,
        }

        private sealed class TileNode
        {
            private GameObject _go;
            private Texture2D _texture;

            public TileNode(TileId id) => Id = id;

            public TileId Id { get; }
            public TileState State { get; set; } = TileState.Missing;
            public float LastUsed { get; set; }

            public void Attach(GameObject go, Texture2D texture)
            {
                _go = go;
                _texture = texture;
                State = TileState.Loaded;
            }

            public void SetVisible(bool visible)
            {
                if (_go != null && _go.activeSelf != visible)
                {
                    _go.SetActive(visible);
                }
            }

            public void Destroy()
            {
                if (_go != null)
                {
                    UnityEngine.Object.Destroy(_go.GetComponent<MeshRenderer>().sharedMaterial);
                    UnityEngine.Object.Destroy(_go);
                }
                if (_texture != null)
                {
                    UnityEngine.Object.Destroy(_texture);
                }
                _go = null;
                _texture = null;
                State = TileState.Missing;
            }
        }
    }
}
