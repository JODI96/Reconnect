using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Room items that the Kenney kit doesn't have (pool, fire pit, fairy lights …), built from
    /// primitives in the same low-poly style. Ids start with "custom-". Sizes in metres.
    /// </summary>
    public sealed class CustomItems
    {
        public const string Prefix = "custom-";

        private readonly Material _litBase;
        private readonly Material _water;
        private readonly RoomTheme _theme;

        public CustomItems(Material litBase, Material water, RoomTheme theme)
        {
            _litBase = litBase;
            _water = water;
            _theme = theme;
        }

        /// <summary>Builds the item under <paramref name="pivot"/>. Returns false if the id is unknown.</summary>
        /// <param name="blocksTiles">Whether avatars must walk around it.</param>
        public bool TryBuild(string itemId, Transform pivot, out bool blocksTiles)
        {
            blocksTiles = true;
            switch (itemId)
            {
                case "custom-pool": Pool(pivot); return true;
                case "custom-parasol": Parasol(pivot); return true;
                case "custom-firepit": FirePit(pivot); return true;
                case "custom-planter": Planter(pivot); return true;
                case "custom-column": Column(pivot); return true;
                case "custom-easel": Easel(pivot); return true;
                case "custom-lightstring": LightString(pivot); blocksTiles = false; return true;
                default: return false;
            }
        }

        /// <summary>6 × 3 m pool with a stone coping, translucent water over a deep-blue basin and a ladder.</summary>
        private void Pool(Transform pivot)
        {
            const float w = 6f, d = 3f, rim = 0.3f;
            var stone = Lit(new Color(0.9f, 0.88f, 0.84f));
            Box(pivot, stone, new Vector3(0f, 0.08f, d / 2f - rim / 2f), new Vector3(w, 0.16f, rim));
            Box(pivot, stone, new Vector3(0f, 0.08f, -d / 2f + rim / 2f), new Vector3(w, 0.16f, rim));
            Box(pivot, stone, new Vector3(w / 2f - rim / 2f, 0.08f, 0f), new Vector3(rim, 0.16f, d));
            Box(pivot, stone, new Vector3(-w / 2f + rim / 2f, 0.08f, 0f), new Vector3(rim, 0.16f, d));
            Box(pivot, Lit(new Color(0.05f, 0.35f, 0.55f)), new Vector3(0f, 0.01f, 0f), new Vector3(w - 2 * rim, 0.02f, d - 2 * rim));
            Box(pivot, _water, new Vector3(0f, 0.1f, 0f), new Vector3(w - 2 * rim, 0.02f, d - 2 * rim));

            var steel = Lit(new Color(0.75f, 0.78f, 0.82f), smoothness: 0.8f);
            foreach (var side in new[] { -0.25f, 0.25f })
            {
                Cylinder(pivot, steel, new Vector3(w / 2f - rim - 0.2f, 0.55f, side), new Vector3(0.04f, 0.45f, 0.04f));
            }
            for (var i = 0; i < 3; i++)
            {
                Box(pivot, steel, new Vector3(w / 2f - rim - 0.2f, 0.2f + i * 0.25f, 0f), new Vector3(0.04f, 0.03f, 0.5f));
            }
        }

        private void Parasol(Transform pivot)
        {
            Cylinder(pivot, Lit(new Color(0.3f, 0.3f, 0.32f)), new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.05f, 0.5f));
            Cylinder(pivot, Lit(new Color(0.85f, 0.85f, 0.85f)), new Vector3(0f, 1.2f, 0f), new Vector3(0.06f, 1.15f, 0.06f));
            Cylinder(pivot, Lit(Color.white), new Vector3(0f, 2.3f, 0f), new Vector3(2.8f, 0.06f, 2.8f));
            Cylinder(pivot, Lit(_theme.WallTrim), new Vector3(0f, 2.38f, 0f), new Vector3(2.2f, 0.06f, 2.2f));
            Sphere(pivot, Lit(Color.white), new Vector3(0f, 2.48f, 0f), Vector3.one * 0.18f);
        }

        /// <summary>Stone ring with logs and a glowing, flickering fire.</summary>
        private void FirePit(Transform pivot)
        {
            Cylinder(pivot, Lit(new Color(0.45f, 0.43f, 0.42f)), new Vector3(0f, 0.15f, 0f), new Vector3(1.3f, 0.15f, 1.3f));
            Cylinder(pivot, Lit(new Color(0.12f, 0.1f, 0.1f)), new Vector3(0f, 0.16f, 0f), new Vector3(1.0f, 0.15f, 1.0f));
            var wood = Lit(new Color(0.4f, 0.24f, 0.13f));
            for (var i = 0; i < 3; i++)
            {
                var log = Box(pivot, wood, new Vector3(0f, 0.36f, 0f), new Vector3(0.8f, 0.1f, 0.1f));
                log.transform.localRotation = Quaternion.Euler(0f, i * 60f, 10f);
            }
            var fire = Glow(new Color(1f, 0.45f, 0.1f), 3f);
            Sphere(pivot, fire, new Vector3(0f, 0.55f, 0f), new Vector3(0.45f, 0.6f, 0.45f));
            Sphere(pivot, Glow(new Color(1f, 0.8f, 0.3f), 3f), new Vector3(0.08f, 0.7f, 0.05f), new Vector3(0.22f, 0.4f, 0.22f));

            var light = AddLight(pivot, new Vector3(0f, 0.9f, 0f), new Color(1f, 0.55f, 0.25f), 3.5f, 6f);
            light.gameObject.AddComponent<FlickeringLight>();
        }

        private void Planter(Transform pivot)
        {
            Box(pivot, Lit(new Color(0.55f, 0.38f, 0.24f)), new Vector3(0f, 0.25f, 0f), new Vector3(1.5f, 0.5f, 0.5f));
            Box(pivot, Lit(new Color(0.25f, 0.18f, 0.12f)), new Vector3(0f, 0.49f, 0f), new Vector3(1.4f, 0.04f, 0.4f));
            var leaves = new[] { new Color(0.3f, 0.6f, 0.3f), new Color(0.24f, 0.52f, 0.26f), new Color(0.38f, 0.66f, 0.34f) };
            for (var i = 0; i < 4; i++)
            {
                var size = 0.45f + 0.12f * (i % 3);
                Sphere(pivot, Lit(leaves[i % 3]), new Vector3(-0.55f + i * 0.37f, 0.62f + size * 0.3f, 0f), new Vector3(size, size * 0.9f, size));
            }
        }

        private void Column(Transform pivot)
        {
            var stone = Lit(Color.Lerp(_theme.FloorA, Color.white, 0.5f));
            var trim = Lit(_theme.WallTrim, smoothness: 0.7f);
            Box(pivot, stone, new Vector3(0f, 0.12f, 0f), new Vector3(0.75f, 0.24f, 0.75f));
            Cylinder(pivot, stone, new Vector3(0f, 1.6f, 0f), new Vector3(0.5f, 1.4f, 0.5f));
            Box(pivot, trim, new Vector3(0f, 3.05f, 0f), new Vector3(0.8f, 0.1f, 0.8f));
            Box(pivot, stone, new Vector3(0f, 3.2f, 0f), new Vector3(0.7f, 0.2f, 0.7f));
        }

        /// <summary>Wooden easel with a canvas and a colourful abstract painting.</summary>
        private void Easel(Transform pivot)
        {
            var wood = Lit(new Color(0.62f, 0.44f, 0.28f));
            foreach (var (x, tilt) in new[] { (-0.3f, 8f), (0.3f, -8f) })
            {
                var leg = Box(pivot, wood, new Vector3(x, 0.8f, 0f), new Vector3(0.05f, 1.65f, 0.05f));
                leg.transform.localRotation = Quaternion.Euler(-8f, 0f, tilt);
            }
            var back = Box(pivot, wood, new Vector3(0f, 0.75f, 0.35f), new Vector3(0.05f, 1.55f, 0.05f));
            back.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            Box(pivot, wood, new Vector3(0f, 0.72f, -0.08f), new Vector3(0.8f, 0.05f, 0.08f));

            Box(pivot, Lit(new Color(0.97f, 0.96f, 0.93f)), new Vector3(0f, 1.18f, -0.1f), new Vector3(0.8f, 0.9f, 0.03f));
            var random = new System.Random(pivot.GetInstanceID());
            for (var i = 0; i < 4; i++)
            {
                var color = Color.HSVToRGB((float)random.NextDouble(), 0.6f, 0.9f);
                Box(pivot, Lit(color), new Vector3(-0.25f + (float)random.NextDouble() * 0.5f, 0.9f + (float)random.NextDouble() * 0.55f, -0.12f),
                    new Vector3(0.12f + (float)random.NextDouble() * 0.25f, 0.1f + (float)random.NextDouble() * 0.25f, 0.01f));
            }
        }

        /// <summary>4 m of fairy lights between two poles, slightly sagging, with glowing bulbs.</summary>
        private void LightString(Transform pivot)
        {
            var pole = Lit(new Color(0.2f, 0.2f, 0.22f));
            foreach (var x in new[] { -2f, 2f })
            {
                Cylinder(pivot, pole, new Vector3(x, 1.2f, 0f), new Vector3(0.05f, 1.2f, 0.05f));
            }

            const int bulbs = 14;
            var warm = Glow(new Color(1f, 0.85f, 0.55f), 4f);
            var accent = Glow(_theme.WallTrim, 3f);
            var wire = Lit(new Color(0.1f, 0.1f, 0.1f));
            Vector3 Point(float t) => new(-2f + 4f * t, 2.35f - 0.35f * 4f * t * (1f - t), 0f);
            for (var i = 0; i <= bulbs; i++)
            {
                var t = i / (float)bulbs;
                var point = Point(t);
                Sphere(pivot, i % 3 == 1 ? accent : warm, point + Vector3.down * 0.06f, Vector3.one * 0.09f);
                if (i < bulbs)
                {
                    var next = Point((i + 1) / (float)bulbs);
                    var segment = Box(pivot, wire, (point + next) / 2f, new Vector3(Vector3.Distance(point, next), 0.012f, 0.012f));
                    segment.transform.localRotation = Quaternion.FromToRotation(Vector3.right, next - point);
                }
            }
            AddLight(pivot, new Vector3(0f, 2f, 0f), new Color(1f, 0.82f, 0.55f), 1.2f, 4.5f);
        }

        private Material Lit(Color color, float smoothness = 0.35f)
        {
            var material = new Material(_litBase);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private Material Glow(Color color, float intensity)
        {
            var material = Lit(color);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
            return material;
        }

        private static GameObject Box(Transform parent, Material material, Vector3 position, Vector3 size) =>
            Shape(PrimitiveType.Cube, parent, material, position, size);

        private static GameObject Cylinder(Transform parent, Material material, Vector3 position, Vector3 size) =>
            Shape(PrimitiveType.Cylinder, parent, material, position, size);

        private static GameObject Sphere(Transform parent, Material material, Vector3 position, Vector3 size) =>
            Shape(PrimitiveType.Sphere, parent, material, position, size);

        private static GameObject Shape(PrimitiveType type, Transform parent, Material material, Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static Light AddLight(Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }
    }

    /// <summary>Gentle random flicker for fire lights.</summary>
    public sealed class FlickeringLight : MonoBehaviour
    {
        private Light _light;
        private float _base;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _base = _light.intensity;
            _seed = UnityEngine.Random.value * 100f;
        }

        private void Update() =>
            _light.intensity = _base * (0.8f + 0.35f * Mathf.PerlinNoise(_seed, Time.time * 3f));
    }
}
