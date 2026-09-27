using System.Collections.Generic;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// A piece of furniture people can sit on (chair, stool, sofa). The places are measured from the model itself:
    /// the backrest is the side where the model is highest, the seat surface is found by a ray from above, so no
    /// per-model numbers are needed. Positions are in room coordinates (children of the room content).
    /// </summary>
    public sealed class Seat : MonoBehaviour
    {
        /// <summary>Index of the item in the room layout (what the server knows the seat by).</summary>
        public int Item { get; private set; }

        /// <summary>Point on the seat surface per place (room coordinates).</summary>
        public IReadOnlyList<Vector3> Points => _points;

        /// <summary>Direction a sitting person faces per place (yaw in degrees, room coordinates).</summary>
        public IReadOnlyList<float> Facings => _facings;

        /// <summary>Free floor tile in front of each place, where one walks to before sitting down.</summary>
        public IReadOnlyList<Vector2Int> Approaches => _approaches;

        private readonly List<Vector3> _points = new();
        private readonly List<float> _facings = new();
        private readonly List<Vector2Int> _approaches = new();
        private readonly List<Vector3> _standing = new();

        /// <summary>Stools have none: they turn towards the counter or table next to them (<see cref="FaceTowards"/>).</summary>
        public bool HasBackrest { get; private set; } = true;

        /// <summary>
        /// The backrest measured on the model is not where the model's front says (a wing chair's wings, high armrests):
        /// the known front wins; tests list these models.
        /// </summary>
        public bool MeasuredBackDisagrees { get; private set; }

        /// <summary>
        /// Measures the places of the model under <paramref name="pivot"/> (world bounds <paramref name="bounds"/>,
        /// room built axis-aligned) and adds colliders so the seat can be tapped.
        /// </summary>
        /// <param name="front">
        /// The way the item faces in world space (known from the catalog: Kenney models face -Z, Poly Haven +Z). People sit
        /// looking this way; null = guess from the model (backrest = highest side).
        /// </param>
        public static Seat Build(Transform pivot, Transform room, Bounds bounds, int item, int places, Vector3? front = null)
        {
            var colliders = new List<Collider>();
            foreach (var filter in pivot.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh != null && filter.GetComponent<Collider>() == null)
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    colliders.Add(collider);
                }
            }
            Physics.SyncTransforms();   // the new colliders must be known before measuring with rays
            var seat = pivot.gameObject.AddComponent<Seat>();
            seat.Item = item;

            var alongX = bounds.size.x >= bounds.size.z;
            var along = alongX ? Vector3.right : Vector3.forward;
            var across = alongX ? Vector3.forward : Vector3.right;
            var length = alongX ? bounds.size.x : bounds.size.z;
            var depth = alongX ? bounds.size.z : bounds.size.x;
            for (var place = 0; place < places; place++)
            {
                var centre = bounds.center + along * ((place + 0.5f) / places - 0.5f) * length;
                centre.y = bounds.max.y;

                // Backrest = highest side. Sofas: only front/back; single seats: all four sides.
                var sides = places > 1 ? new[] { across, -across } : new[] { across, -across, along, -along };
                var back = sides[0];
                var highest = float.MinValue;
                var lowest = float.MaxValue;
                foreach (var side in sides)
                {
                    var extent = Mathf.Abs(Vector3.Dot(bounds.extents, side));
                    var height = HeightAt(colliders, centre + side * extent * 0.7f, bounds);
                    if (height > highest)
                    {
                        highest = height;
                        back = side;
                    }
                    lowest = Mathf.Min(lowest, height);
                }
                if (highest - lowest < 0.12f)
                {
                    seat.HasBackrest = false;
                    back = -(front ?? pivot.forward);   // stool: no backrest, face the way the item is turned (turns to a counter later)
                    back.y = 0f;
                    back.Normalize();
                }
                else if (front is { } known)
                {
                    // Chairs, armchairs, sofas: sit looking the way the item faces.
                    var knownBack = -new Vector3(known.x, 0f, known.z).normalized;
                    seat.MeasuredBackDisagrees |= Vector3.Dot(back, knownBack) < 0.5f;
                    back = knownBack;
                }

                var facing = -back;
                var reach = Mathf.Abs(Vector3.Dot(bounds.extents, facing));
                var point = centre + facing * reach * 0.15f;
                point.y = HeightAt(colliders, point, bounds);
                seat._points.Add(room.InverseTransformPoint(point));
                seat._facings.Add(Quaternion.LookRotation(room.InverseTransformDirection(facing)).eulerAngles.y);
                seat._standing.Add(room.InverseTransformPoint(centre + facing * (reach + 0.55f)));
            }
            return seat;
        }

        /// <summary>Turns every place (and where one stands before sitting) towards a room point, e.g. the bar.</summary>
        public void FaceTowards(Vector3 roomPoint)
        {
            for (var i = 0; i < _points.Count; i++)
            {
                var towards = roomPoint - _points[i];
                towards.y = 0f;
                if (towards.sqrMagnitude < 0.0001f)
                {
                    continue;
                }
                towards.Normalize();
                _facings[i] = Quaternion.LookRotation(towards).eulerAngles.y;
                _standing[i] = _points[i] - towards * 0.6f;
                _standing[i] = new Vector3(_standing[i].x, 0f, _standing[i].z);
            }
        }

        /// <summary>Finds the free tile in front of each place once the room's walkable tiles are known.</summary>
        public void ResolveApproaches(RoomPathfinder walkable)
        {
            _approaches.Clear();
            foreach (var standing in _standing)
            {
                var tile = RoomView.WorldToTile(standing);
                _approaches.Add(walkable.NearestWalkable(tile, tile));
            }
        }

        /// <summary>Place nearest to a world point (the tapped spot on a sofa).</summary>
        public int NearestPlace(Vector3 roomPoint)
        {
            var best = 0;
            for (var i = 1; i < _points.Count; i++)
            {
                if ((_points[i] - roomPoint).sqrMagnitude < (_points[best] - roomPoint).sqrMagnitude)
                {
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Top of the model at a point (ray from above), or the bounds' middle height if missed.</summary>
        private static float HeightAt(List<Collider> colliders, Vector3 point, Bounds bounds)
        {
            var ray = new Ray(new Vector3(point.x, bounds.max.y + 1f, point.z), Vector3.down);
            var best = float.MinValue;
            foreach (var collider in colliders)
            {
                if (collider.Raycast(ray, out var hit, bounds.size.y + 2f))
                {
                    best = Mathf.Max(best, hit.point.y);
                }
            }
            return best > float.MinValue ? best : bounds.center.y;
        }
    }
}
