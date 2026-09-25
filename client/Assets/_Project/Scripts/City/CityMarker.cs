using Reconnect.Contracts.Buildings;
using UnityEngine;

namespace Reconnect.Client.City
{
    /// <summary>
    /// A glowing beacon above a building that has rooms (our entrance). The real building geometry
    /// comes from swisstopo; this marker only makes it findable and tappable.
    /// </summary>
    public sealed class CityMarker : MonoBehaviour
    {
        private Renderer[] _renderers;

        public BuildingDto Building { get; private set; }

        /// <summary>World position just above the beacon – anchor for the name label.</summary>
        public Vector3 LabelAnchor => transform.position + Vector3.up * (_height + 12f);

        private float _height;

        public static CityMarker Create(Transform parent, BuildingDto building, Material material, float height)
        {
            var root = new GameObject(building.Name);
            root.transform.SetParent(parent, false);

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localScale = new Vector3(1.5f, height / 2f, 1.5f);   // Unity cylinder is 2 units tall
            pole.transform.localPosition = Vector3.up * (height / 2f);
            Object.Destroy(pole.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localScale = Vector3.one * 9f;
            head.transform.localPosition = Vector3.up * height;
            // Generous tap target: the whole beacon.
            var collider = head.GetComponent<SphereCollider>();
            collider.radius = 1.2f;

            var marker = root.AddComponent<CityMarker>();
            marker.Building = building;
            marker._height = height;
            marker._renderers = new Renderer[] { pole.GetComponent<Renderer>(), head.GetComponent<Renderer>() };
            marker.SetMaterial(material);
            return marker;
        }

        public void SetMaterial(Material material)
        {
            foreach (var renderer in _renderers)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
