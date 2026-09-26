using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Reconnect.Client.City
{
    /// <summary>
    /// Where a tower stands in the Unity world, measured from swisstopo data (never from Google's): footprint as an
    /// oriented rectangle, ground and roof height. Floors are <see cref="FloorHeight"/> apart.
    /// </summary>
    public sealed class TowerInfo
    {
        public const float FloorHeight = 3.5f;

        public TowerInfo(Vector3 center, float yaw, float sizeX, float sizeZ, float groundY, float roofY, IReadOnlyList<Vector2> outline)
        {
            Outline = outline;
            Center = center;
            Yaw = yaw;
            SizeX = sizeX;
            SizeZ = sizeZ;
            GroundY = groundY;
            RoofY = roofY;
        }

        /// <summary>Footprint centre (y = ground).</summary>
        public Vector3 Center { get; }

        /// <summary>Rotation of the footprint's X axis around Y, in degrees.</summary>
        public float Yaw { get; }
        public float SizeX { get; }
        public float SizeZ { get; }
        public float GroundY { get; }
        public float RoofY { get; }
        public Quaternion Rotation => Quaternion.Euler(0f, Yaw, 0f);

        /// <summary>Real footprint outline (convex, counter-clockwise), world X/Z.</summary>
        public IReadOnlyList<Vector2> Outline { get; }

        /// <summary>Distance of a room's back walls from the facade.</summary>
        private const float FacadeInset = 0.4f;

        /// <summary>Centre of a storey's floor (0 = ground floor).</summary>
        public Vector3 FloorAnchor(int floor) => new(Center.x, GroundY + 0.05f + floor * FloorHeight, Center.z);

        /// <summary>
        /// Where a room of the given size stands on a storey: in the corner the camera looks at, so its back glass
        /// walls are the tower's facade with the city right behind them (like the real Clouds).
        /// </summary>
        public Vector3 RoomAnchor(int floor, float width, float depth)
        {
            // Start in the corner of the enclosing rectangle, then slide towards the middle until the whole room
            // is inside the real (chamfered) outline.
            var corner = new Vector3(Mathf.Max(0f, (SizeX - width) / 2f - FacadeInset), 0f, Mathf.Max(0f, (SizeZ - depth) / 2f - FacadeInset));
            var offset = corner;
            for (var step = 0; step <= 40; step++)
            {
                offset = Vector3.Lerp(corner, Vector3.zero, step / 40f);
                if (RoomInsideOutline(Center + Rotation * offset, width, depth))
                {
                    break;
                }
            }
            return FloorAnchor(floor) + Rotation * offset;
        }

        private bool RoomInsideOutline(Vector3 centre, float width, float depth)
        {
            foreach (var (dx, dz) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                var corner = centre + Rotation * new Vector3(dx * width / 2f, 0f, dz * depth / 2f);
                if (!Contains(new Vector2(corner.x, corner.z)))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Point in the convex, counter-clockwise outline.</summary>
        public bool Contains(Vector2 point)
        {
            for (var i = 0; i < Outline.Count; i++)
            {
                var a = Outline[i];
                var b = Outline[(i + 1) % Outline.Count];
                if ((b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x) < 0f)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Measures a tower around <paramref name="near"/>: samples a grid of roof heights, keeps the cells that are
        /// much higher than the ground and connected to the cell at <paramref name="near"/> (so a neighbouring tower
        /// doesn't count), then fits an oriented rectangle (principal axes) and the outline (convex hull).
        /// </summary>
        public static async Task<TowerInfo> MeasureAsync(Cesium3DTileset buildings, Cesium3DTileset terrain, Func<Vector3, (double Lat, double Lon)> toGeo,
            Func<double, double, double, Vector3> toUnity, Vector3 near, double sampleHeight)
        {
            const float radius = 75f, step = 2.5f, minTowerHeight = 45f;
            var size = Mathf.RoundToInt(radius * 2f / step) + 1;
            var cells = new List<Vector3>(size * size);
            for (var ix = 0; ix < size; ix++)
            for (var iz = 0; iz < size; iz++)
            {
                cells.Add(new Vector3(near.x - radius + ix * step, near.y, near.z - radius + iz * step));
            }
            var positions = cells.Select(c => { var (lat, lon) = toGeo(c); return new double3(lon, lat, sampleHeight); }).ToArray();

            var roofs = await buildings.SampleHeightMostDetailed(positions);
            var ground = await terrain.SampleHeightMostDetailed(new[] { positions[positions.Length / 2] });
            if (!ground.sampleSuccess[0])
            {
                return null;
            }
            var g = ground.longitudeLatitudeHeightPositions[0];
            var groundY = toUnity(g.y, g.x, g.z).y;

            var high = new Vector3?[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                if (!roofs.sampleSuccess[i])
                {
                    continue;
                }
                var p = roofs.longitudeLatitudeHeightPositions[i];
                var world = toUnity(p.y, p.x, p.z);
                if (world.y > groundY + minTowerHeight)
                {
                    high[i] = world;
                }
            }

            // Flood fill from the high cell nearest to the marker: only this tower, not a neighbouring one.
            var startIndex = Enumerable.Range(0, high.Length).Where(i => high[i] != null)
                .OrderBy(i => (cells[i] - near).sqrMagnitude).DefaultIfEmpty(-1).First();
            if (startIndex < 0)
            {
                return null;
            }
            var tower = new List<Vector3>();
            var visited = new bool[high.Length];
            var pending = new Stack<int>();
            pending.Push(startIndex);
            visited[startIndex] = true;
            while (pending.Count > 0)
            {
                var index = pending.Pop();
                tower.Add(high[index]!.Value);
                var (ix, iz) = (index / size, index % size);
                foreach (var (nx, nz) in new[] { (ix + 1, iz), (ix - 1, iz), (ix, iz + 1), (ix, iz - 1) })
                {
                    var neighbour = nx * size + nz;
                    if (nx >= 0 && nx < size && nz >= 0 && nz < size && !visited[neighbour] && high[neighbour] != null)
                    {
                        visited[neighbour] = true;
                        pending.Push(neighbour);
                    }
                }
            }
            if (tower.Count < 8)
            {
                return null;
            }

            // Principal axes of the footprint cells.
            var mx = tower.Average(p => p.x);
            var mz = tower.Average(p => p.z);
            double sxx = 0, szz = 0, sxz = 0;
            foreach (var p in tower)
            {
                sxx += (p.x - mx) * (p.x - mx);
                szz += (p.z - mz) * (p.z - mz);
                sxz += (p.x - mx) * (p.z - mz);
            }
            var angle = 0.5 * Math.Atan2(2 * sxz, sxx - szz);   // direction of the long axis (radians, from +X towards +Z)
            var axisX = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            var axisZ = new Vector2(-axisX.y, axisX.x);
            float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
            foreach (var p in tower)
            {
                var d = new Vector2(p.x - (float)mx, p.z - (float)mz);
                var a = Vector2.Dot(d, axisX);
                var b = Vector2.Dot(d, axisZ);
                minA = Mathf.Min(minA, a); maxA = Mathf.Max(maxA, a);
                minB = Mathf.Min(minB, b); maxB = Mathf.Max(maxB, b);
            }
            // Cells are samples: the real edge is half a step further out.
            var sizeX = maxA - minA + step;
            var sizeZ = maxB - minB + step;
            var centre2 = new Vector2((float)mx, (float)mz) + axisX * ((minA + maxA) / 2f) + axisZ * ((minB + maxB) / 2f);
            // Unity yaw rotates +X towards -Z, the angle above goes from +X towards +Z.
            var yaw = -(float)(angle * Mathf.Rad2Deg);
            var outline = Expand(ConvexHull(tower.Select(p => new Vector2(p.x, p.z)).ToList()), step / 2f);
            return new TowerInfo(new Vector3(centre2.x, groundY, centre2.y), yaw, sizeX, sizeZ, groundY, tower.Max(p => p.y), outline);
        }

        /// <summary>
        /// A tower from its known ground plan (e.g. OpenStreetMap, world X/Z): the outline defines clipping and model,
        /// the smallest enclosing rectangle the room placement; ground and roof height come from swisstopo.
        /// </summary>
        public static async Task<TowerInfo> FromOutlineAsync(IReadOnlyList<Vector2> outline, Cesium3DTileset buildings, Cesium3DTileset terrain,
            Func<Vector3, (double Lat, double Lon)> toGeo, Func<double, double, double, Vector3> toUnity, double sampleHeight)
        {
            var hull = ConvexHull(outline.ToList());
            if (hull.Count < 3)
            {
                return null;
            }
            var centroid = new Vector2(hull.Average(p => p.x), hull.Average(p => p.y));
            // Roof: highest of a few points inside the outline. Ground: terrain at the centre.
            var inside = hull.Select(p => Vector2.Lerp(centroid, p, 0.5f)).Append(centroid).ToList();
            var positions = inside.Select(p => { var (lat, lon) = toGeo(new Vector3(p.x, 0f, p.y)); return new double3(lon, lat, sampleHeight); }).ToArray();
            var roofs = await buildings.SampleHeightMostDetailed(positions);
            var ground = await terrain.SampleHeightMostDetailed(new[] { positions[^1] });
            if (!ground.sampleSuccess[0])
            {
                return null;
            }
            var g = ground.longitudeLatitudeHeightPositions[0];
            var groundY = toUnity(g.y, g.x, g.z).y;
            var roofY = groundY + 30f;
            for (var i = 0; i < positions.Length; i++)
            {
                if (roofs.sampleSuccess[i])
                {
                    var r = roofs.longitudeLatitudeHeightPositions[i];
                    roofY = Mathf.Max(roofY, toUnity(r.y, r.x, r.z).y);
                }
            }

            // Smallest enclosing rectangle: one side lies on a hull edge (rotating calipers).
            float bestArea = float.MaxValue, bestAngle = 0f, bestX = 0f, bestZ = 0f;
            var bestCentre = centroid;
            for (var i = 0; i < hull.Count; i++)
            {
                var edge = hull[(i + 1) % hull.Count] - hull[i];
                var angle = Mathf.Atan2(edge.y, edge.x);
                var axisX = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var axisZ = new Vector2(-axisX.y, axisX.x);
                float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
                foreach (var p in hull)
                {
                    minA = Mathf.Min(minA, Vector2.Dot(p, axisX)); maxA = Mathf.Max(maxA, Vector2.Dot(p, axisX));
                    minB = Mathf.Min(minB, Vector2.Dot(p, axisZ)); maxB = Mathf.Max(maxB, Vector2.Dot(p, axisZ));
                }
                var area = (maxA - minA) * (maxB - minB);
                if (area < bestArea)
                {
                    bestArea = area;
                    bestAngle = angle;
                    bestX = maxA - minA;
                    bestZ = maxB - minB;
                    bestCentre = axisX * ((minA + maxA) / 2f) + axisZ * ((minB + maxB) / 2f);
                }
            }
            if (bestX < bestZ)
            {
                // Rooms are wider than deep: the long side becomes X.
                (bestX, bestZ) = (bestZ, bestX);
                bestAngle += Mathf.PI / 2f;
            }
            var yaw = -bestAngle * Mathf.Rad2Deg;
            return new TowerInfo(new Vector3(bestCentre.x, groundY, bestCentre.y), yaw, bestX, bestZ, groundY, roofY, hull);
        }

        /// <summary>Andrew's monotone chain; counter-clockwise, without the repeated first point.</summary>
        public static List<Vector2> ConvexHull(List<Vector2> points)
        {
            var sorted = points.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            if (sorted.Count < 3)
            {
                return sorted;
            }
            static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            var hull = new List<Vector2>();
            foreach (var pass in new[] { sorted, Enumerable.Reverse(sorted).ToList() })
            {
                var start = hull.Count;
                foreach (var p in pass)
                {
                    while (hull.Count >= start + 2 && Cross(hull[^2], hull[^1], p) <= 0f)
                    {
                        hull.RemoveAt(hull.Count - 1);
                    }
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull;
        }

        /// <summary>Pushes every corner of a convex outline outwards by <paramref name="distance"/>.</summary>
        public static List<Vector2> Expand(IReadOnlyList<Vector2> outline, float distance)
        {
            var centre = new Vector2(outline.Average(p => p.x), outline.Average(p => p.y));
            return outline.Select(p => p + (p - centre).normalized * distance).ToList();
        }
    }

    /// <summary>
    /// "Doll's house" view of a tower while you are on one of its floors: the real building is clipped out of the
    /// streamed 3D tiles (Cesium polygon clipping) and replaced by our own model up to your storey – green glass
    /// with floor bands and mullions – with your floor plate on top. Everything above is open, so the camera looks
    /// into the floor and over the real city at the real height.
    /// </summary>
    public sealed class TowerCutaway
    {
        private const float ClipMargin = 1.5f;

        private readonly Transform _georeference;
        private readonly CesiumPolygonRasterOverlay[] _clipOverlays;
        private readonly Material _facadeBase;
        private readonly Material _slabBase;
        private GameObject _polygon;
        private GameObject _model;

        public TowerCutaway(Transform georeference, IEnumerable<Cesium3DTileset> clippedTilesets, Material facadeBase, Material slabBase)
        {
            _georeference = georeference;
            _facadeBase = facadeBase;
            _slabBase = slabBase;
            _clipOverlays = clippedTilesets.Where(t => t != null).Select(t =>
            {
                var overlay = t.gameObject.GetComponent<CesiumPolygonRasterOverlay>() ?? t.gameObject.AddComponent<CesiumPolygonRasterOverlay>();
                // The editor sets this in Reset(); added at runtime it would default to "0" and replace the aerial image.
                overlay.materialKey = "Clipping";
                overlay.invertSelection = false;
                overlay.excludeSelectedTiles = false;
                overlay.enabled = false;
                return overlay;
            }).ToArray();
        }

        public bool IsShown => _model != null;

        public void Show(TowerInfo tower, int floor)
        {
            Hide();

            // 1) Clip the real tower out of the city tiles (slightly larger than the footprint).
            _polygon = new GameObject("Tower Clip");
            _polygon.SetActive(false);
            _polygon.transform.SetParent(_georeference, true);
            _polygon.transform.SetPositionAndRotation(tower.Center, Quaternion.identity);
            var container = _polygon.AddComponent<SplineContainer>();
            _polygon.AddComponent<CesiumGlobeAnchor>();
            var clip = TowerInfo.Expand(tower.Outline, ClipMargin);
            var spline = new Spline(clip.Select(p => new BezierKnot(new float3(p.x - tower.Center.x, 0f, p.y - tower.Center.z))).ToArray(), closed: true);
            spline.SetTangentMode(TangentMode.Linear);
            container.Spline = spline;
            var polygon = _polygon.AddComponent<CesiumCartographicPolygon>();
            _polygon.SetActive(true);
            foreach (var overlay in _clipOverlays)
            {
                overlay.polygons = new List<CesiumCartographicPolygon> { polygon };
                overlay.enabled = true;
                overlay.Refresh();
            }

            // 2) Our tower up to (not including) this storey, and the floor plate the room stands on.
            _model = BuildModel(tower, floor);
        }

        public void Hide()
        {
            foreach (var overlay in _clipOverlays)
            {
                if (overlay != null && overlay.enabled)
                {
                    overlay.enabled = false;
                }
            }
            if (_polygon != null)
            {
                UnityEngine.Object.Destroy(_polygon);
                _polygon = null;
            }
            if (_model != null)
            {
                UnityEngine.Object.Destroy(_model);
                _model = null;
            }
        }

        /// <summary>
        /// Our tower up to the current storey, extruded from the real footprint outline: one glass facade panel per
        /// outline edge (floor bands and mullions from the facade texture), a dark body inside and the floor plate of
        /// the current storey (the room's own glass walls stand on it).
        /// </summary>
        private GameObject BuildModel(TowerInfo tower, int floor)
        {
            var root = new GameObject("Tower Cutaway");
            root.transform.position = new Vector3(0f, tower.GroundY, 0f);
            var height = Mathf.Max(0f, floor * TowerInfo.FloorHeight);
            var outline = tower.Outline;

            if (height > 0.1f)
            {
                var facade = FacadeTextures.Glass();
                for (var i = 0; i < outline.Count; i++)
                {
                    var a = outline[i];
                    var b = outline[(i + 1) % outline.Count];
                    var length = Vector2.Distance(a, b);
                    if (length < 0.2f)
                    {
                        continue;
                    }
                    var material = new Material(_facadeBase) { mainTexture = facade };
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Smoothness", 0.92f);
                    material.SetFloat("_Metallic", 0.35f);
                    material.mainTextureScale = new Vector2(length / FacadeTextures.PaneWidth, height / TowerInfo.FloorHeight);
                    Edge(root.transform, "Facade", material, a, b, 0f, height, 0.15f);
                }
                var body = new Material(_slabBase);
                body.SetColor("_BaseColor", new Color(0.12f, 0.14f, 0.15f));
                Prism(root.transform, "Body", body, TowerInfo.Expand(outline, -0.3f), 0.05f, height - 0.1f);
            }

            var slab = new Material(_slabBase);
            slab.SetColor("_BaseColor", new Color(0.62f, 0.63f, 0.63f));
            slab.SetFloat("_Smoothness", 0.45f);
            Prism(root.transform, "Floor Plate", slab, TowerInfo.Expand(outline, 0.15f), height - 0.4f, height);

            return root;
        }

        /// <summary>A wall panel along the outline edge a→b between two heights (relative to the ground).</summary>
        private static void Edge(Transform parent, string name, Material material, Vector2 a, Vector2 b, float bottom, float top, float thickness)
        {
            var middle = (a + b) / 2f;
            var direction = b - a;
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = new Vector3(middle.x, (bottom + top) / 2f, middle.y);
            part.transform.localRotation = Quaternion.LookRotation(new Vector3(-direction.y, 0f, direction.x));
            part.transform.localScale = new Vector3(direction.magnitude + thickness, top - bottom, thickness);
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.Destroy(part.GetComponent<Collider>());
        }

        /// <summary>Closed prism over a convex outline (top, bottom and sides), heights relative to the ground.</summary>
        private static void Prism(Transform parent, string name, Material material, IReadOnlyList<Vector2> outline, float bottom, float top)
        {
            var n = outline.Count;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            // Top and bottom caps (triangle fans – the outline is convex).
            foreach (var (y, up) in new[] { (top, true), (bottom, false) })
            {
                var start = vertices.Count;
                vertices.AddRange(outline.Select(p => new Vector3(p.x, y, p.y)));
                for (var i = 1; i < n - 1; i++)
                {
                    if (up)
                    {
                        triangles.AddRange(new[] { start, start + i + 1, start + i });
                    }
                    else
                    {
                        triangles.AddRange(new[] { start, start + i, start + i + 1 });
                    }
                }
            }
            // Sides.
            for (var i = 0; i < n; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % n];
                var start = vertices.Count;
                vertices.Add(new Vector3(a.x, bottom, a.y));
                vertices.Add(new Vector3(b.x, bottom, b.y));
                vertices.Add(new Vector3(b.x, top, b.y));
                vertices.Add(new Vector3(a.x, top, a.y));
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }

    /// <summary>Procedural facade texture: one storey × two panes of green-tinted glass with mullions and a slab band.</summary>
    public static class FacadeTextures
    {
        /// <summary>Width of one pane in metres (the texture holds two).</summary>
        public const float PaneWidth = 2.7f;

        private static Texture2D _glass;

        public static Texture2D Glass()
        {
            if (_glass != null)
            {
                return _glass;
            }
            const int size = 256;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = x / (float)size;
                var v = y / (float)size;
                // Glass reflects a brighter sky at the top of each pane, darker towards the bottom.
                var color = Color.Lerp(new Color(0.16f, 0.3f, 0.3f), new Color(0.46f, 0.64f, 0.64f), Mathf.Pow(v, 1.6f));
                color *= 0.94f + 0.06f * Mathf.PerlinNoise(u * 6f, v * 3f);
                if (v < 0.12f)
                {
                    color = new Color(0.1f, 0.12f, 0.13f);   // slab band between storeys
                }
                var mullion = Mathf.Abs(Mathf.Repeat(u * 2f, 1f) - 0.5f) > 0.485f;
                if (mullion && v >= 0.12f)
                {
                    color = new Color(0.18f, 0.2f, 0.21f);
                }
                pixels[y * size + x] = color;
            }
            _glass = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                name = "Facade Glass",
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 8,
            };
            _glass.SetPixels32(pixels);
            _glass.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return _glass;
        }
    }
}
