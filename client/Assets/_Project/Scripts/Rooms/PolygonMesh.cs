using System.Collections.Generic;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>Floor slabs of any outline (tower storeys): a flat top with texture in metres and closed sides.</summary>
    public static class PolygonMesh
    {
        /// <summary>
        /// A slab from <paramref name="top"/> down by <paramref name="thickness"/>; UVs are metres / <paramref name="metresPerUv"/>
        /// so a tiled floor texture keeps its scale.
        /// </summary>
        public static Mesh Slab(IReadOnlyList<Vector2> outline, float top, float thickness, float metresPerUv)
        {
            var points = CounterClockwise(outline);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Top face.
            foreach (var p in points)
            {
                vertices.Add(new Vector3(p.x, top, p.y));
                uvs.Add(p / metresPerUv);
            }
            foreach (var (a, b, c) in Triangulate(points))
            {
                // Seen from above (+Y), counter-clockwise in X/Z is clockwise for Unity: flip.
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }

            // Sides: one quad per edge, facing outwards.
            var bottom = top - thickness;
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var q = points[(i + 1) % points.Count];
                var start = vertices.Count;
                vertices.Add(new Vector3(p.x, top, p.y));
                vertices.Add(new Vector3(q.x, top, q.y));
                vertices.Add(new Vector3(q.x, bottom, q.y));
                vertices.Add(new Vector3(p.x, bottom, p.y));
                var length = Vector2.Distance(p, q) / metresPerUv;
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(length, 0f));
                uvs.Add(new Vector2(length, thickness / metresPerUv));
                uvs.Add(new Vector2(0f, thickness / metresPerUv));
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            var mesh = new Mesh { name = "Floor Slab" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Ear clipping (outlines are small – a few dozen corners); works for concave outlines.</summary>
        public static List<(int A, int B, int C)> Triangulate(IReadOnlyList<Vector2> points)
        {
            var result = new List<(int, int, int)>();
            var remaining = new List<int>();
            for (var i = 0; i < points.Count; i++)
            {
                remaining.Add(i);
            }
            var guard = 0;
            while (remaining.Count > 3 && guard++ < 10000)
            {
                var clipped = false;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    var b = remaining[i];
                    var c = remaining[(i + 1) % remaining.Count];
                    if (Cross(points[a], points[b], points[c]) <= 1e-6f || ContainsAny(points, remaining, a, b, c))
                    {
                        continue;   // reflex corner or another point inside: not an ear
                    }
                    result.Add((a, b, c));
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    break;   // degenerate outline: stop rather than loop forever
                }
            }
            if (remaining.Count == 3)
            {
                result.Add((remaining[0], remaining[1], remaining[2]));
            }
            return result;
        }

        /// <summary>The outline counter-clockwise (positive area) in X/Z.</summary>
        public static List<Vector2> CounterClockwise(IReadOnlyList<Vector2> outline)
        {
            var points = new List<Vector2>(outline);
            var area = 0f;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                area += points[j].x * points[i].y - points[i].x * points[j].y;
            }
            if (area < 0f)
            {
                points.Reverse();
            }
            return points;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        private static bool ContainsAny(IReadOnlyList<Vector2> points, List<int> remaining, int a, int b, int c)
        {
            foreach (var index in remaining)
            {
                if (index == a || index == b || index == c)
                {
                    continue;
                }
                var p = points[index];
                if (Cross(points[a], points[b], p) >= 0f && Cross(points[b], points[c], p) >= 0f && Cross(points[c], points[a], p) >= 0f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
