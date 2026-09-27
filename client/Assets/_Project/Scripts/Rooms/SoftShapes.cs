using System.Collections.Generic;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Meshes for our own furniture: boxes with rounded edges (cushions, table tops, cabinets – soft instead of sharp
    /// primitives) and turned shapes (table bases, stools, pots) from a profile. UVs are in metres, so the surface
    /// materials tile at their real scale. Meshes are made once per size and shared.
    /// </summary>
    public static class SoftShapes
    {
        private static readonly Dictionary<string, Mesh> Cache = new();

        /// <summary>A box of <paramref name="size"/> metres with edges rounded by <paramref name="radius"/>.</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int segments = 2)
        {
            radius = Mathf.Clamp(radius, 0.001f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) / 2f - 0.0005f);
            var key = $"box {size.x:0.###} {size.y:0.###} {size.z:0.###} {radius:0.###} {segments}";
            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var half = size / 2f;
            var inner = half - Vector3.one * radius;
            float[] Axis(float h, float i)
            {
                // Dense where the edge bends (cosine spacing), nothing in the flat middle.
                var values = new List<float>();
                for (var k = 0; k <= segments; k++)
                {
                    values.Add(-i - radius * Mathf.Cos(Mathf.PI / 2f * k / segments));
                }
                for (var k = segments; k >= 0; k--)
                {
                    values.Add(i + radius * Mathf.Cos(Mathf.PI / 2f * k / segments));
                }
                return values.ToArray();
            }
            var xs = Axis(half.x, inner.x);
            var ys = Axis(half.y, inner.y);
            var zs = Axis(half.z, inner.z);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // One grid per face: (axis fixed at ±half, two axes varying).
            void Face(Vector3 normal, float[] us, float[] vs, System.Func<float, float, Vector3> point, System.Func<Vector3, Vector2> uv)
            {
                var start = vertices.Count;
                foreach (var v in vs)
                {
                    foreach (var u in us)
                    {
                        var p = point(u, v);
                        var clamped = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        var direction = p - clamped;
                        var n = direction.sqrMagnitude > 1e-10f ? direction.normalized : normal;
                        var position = clamped + n * radius;
                        vertices.Add(position);
                        normals.Add(n);
                        uvs.Add(uv(position));
                    }
                }
                var columns = us.Length;
                for (var row = 0; row < vs.Length - 1; row++)
                {
                    for (var column = 0; column < columns - 1; column++)
                    {
                        var a = start + row * columns + column;
                        var b = a + 1;
                        var c = a + columns;
                        var d = c + 1;
                        AddQuad(triangles, vertices, normal, a, b, d, c);
                    }
                }
            }

            Face(Vector3.right, zs, ys, (u, v) => new Vector3(half.x, v, u), p => new Vector2(p.z, p.y));
            Face(Vector3.left, zs, ys, (u, v) => new Vector3(-half.x, v, u), p => new Vector2(p.z, p.y));
            Face(Vector3.up, xs, zs, (u, v) => new Vector3(u, half.y, v), p => new Vector2(p.x, p.z));
            Face(Vector3.down, xs, zs, (u, v) => new Vector3(u, -half.y, v), p => new Vector2(p.x, p.z));
            Face(Vector3.forward, xs, ys, (u, v) => new Vector3(u, v, half.z), p => new Vector2(p.x, p.y));
            Face(Vector3.back, xs, ys, (u, v) => new Vector3(u, v, -half.z), p => new Vector2(p.x, p.y));

            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// A shape turned around the Y axis: <paramref name="profile"/> = (radius, height) points from the bottom centre
        /// out, up and back in to the top centre (radius 0 at both ends closes it).
        /// </summary>
        public static Mesh Lathe(IReadOnlyList<Vector2> profile, int segments = 24)
        {
            var key = "lathe " + segments + string.Concat(System.Linq.Enumerable.Select(profile, p => $" {p.x:0.####},{p.y:0.####}"));
            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var along = new float[profile.Count];
            for (var i = 1; i < profile.Count; i++)
            {
                along[i] = along[i - 1] + Vector2.Distance(profile[i - 1], profile[i]);
            }
            var maxRadius = 0f;
            foreach (var p in profile)
            {
                maxRadius = Mathf.Max(maxRadius, p.x);
            }
            // Each profile segment gets its own ring pair, so edges between segments stay crisp (table top vs rim).
            for (var i = 0; i < profile.Count - 1; i++)
            {
                var a = profile[i];
                var b = profile[i + 1];
                var outward = new Vector2(b.y - a.y, -(b.x - a.x));   // right of the profile direction
                var start = vertices.Count;
                for (var s = 0; s <= segments; s++)
                {
                    var angle = 2f * Mathf.PI * s / segments;
                    var cos = Mathf.Cos(angle);
                    var sin = Mathf.Sin(angle);
                    vertices.Add(new Vector3(a.x * cos, a.y, a.x * sin));
                    vertices.Add(new Vector3(b.x * cos, b.y, b.x * sin));
                    var u = angle * maxRadius;
                    uvs.Add(new Vector2(u, along[i]));
                    uvs.Add(new Vector2(u, along[i + 1]));
                }
                for (var s = 0; s < segments; s++)
                {
                    var p0 = start + s * 2;
                    var p1 = p0 + 1;
                    var p2 = p0 + 2;
                    var p3 = p0 + 3;
                    var angle = 2f * Mathf.PI * (s + 0.5f) / segments;
                    var normal = new Vector3(outward.x * Mathf.Cos(angle), outward.y, outward.x * Mathf.Sin(angle));
                    AddQuad(triangles, vertices, normal, p0, p2, p3, p1);
                }
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>A disc/cylinder with softly rounded rims (table tops, cushions, pots): radius, height, rim radius.</summary>
        public static Mesh RoundedCylinder(float radius, float height, float rim, int segments = 28)
        {
            rim = Mathf.Clamp(rim, 0.001f, Mathf.Min(radius, height / 2f) - 0.0005f);
            var profile = new List<Vector2> { new(0f, 0f) };
            for (var k = 0; k <= 3; k++)
            {
                var a = Mathf.PI / 2f * k / 3f;
                profile.Add(new Vector2(radius - rim + rim * Mathf.Sin(a), rim - rim * Mathf.Cos(a)));
            }
            for (var k = 0; k <= 3; k++)
            {
                var a = Mathf.PI / 2f * k / 3f;
                profile.Add(new Vector2(radius - rim + rim * Mathf.Cos(a), height - rim + rim * Mathf.Sin(a)));
            }
            profile.Add(new Vector2(0f, height));
            return Lathe(profile, segments);
        }

        /// <summary>Adds a quad (a b c d around it) facing <paramref name="outward"/>.</summary>
        private static void AddQuad(List<int> triangles, List<Vector3> vertices, Vector3 outward, int a, int b, int c, int d)
        {
            var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (normal.sqrMagnitude < 1e-14f)
            {
                normal = Vector3.Cross(vertices[c] - vertices[a], vertices[d] - vertices[a]);
            }
            // Unity draws the side Cross(b − a, c − a) points to (e.g. the built-in quad: (0, 3, 1) faces −Z).
            if (Vector3.Dot(normal, outward) > 0f)
            {
                triangles.AddRange(new[] { a, b, c, a, c, d });
            }
            else
            {
                triangles.AddRange(new[] { a, c, b, a, d, c });
            }
        }
    }
}
