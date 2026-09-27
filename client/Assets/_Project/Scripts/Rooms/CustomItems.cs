using System;
using System.Collections.Generic;
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

        /// <summary>Lift bank (tower floors). The room turns it into a tappable station that opens the lift panel.</summary>
        public const string ElevatorItem = "custom-elevator";

        /// <summary>Depth of the lift core; the doors are on its front (-Z) side.</summary>
        public const float ElevatorDepth = 2.4f;

        /// <summary>The concrete core of a tower floor (lift doors on both long sides).</summary>
        public const string CoreItem = "custom-core";
        public const float CoreWidth = 11.8f;
        public const float CoreDepth = 5.8f;

        /// <summary>Pools are sunk into the floor: basin depth and the water surface (room coordinates, floor = 0).</summary>
        public const float PoolDepth = 2f;
        public const float WaterLevel = -0.18f;

        /// <summary>Water surface of a pool item: "custom-pool" = 6 × 3 m, "custom-pool-12x6" = 12 × 6 m (before rotation).</summary>
        public static bool TryPoolSize(string itemId, out Vector2 size)
        {
            size = new Vector2(6f, 3f);
            if (itemId == "custom-pool")
            {
                return true;
            }
            if (!itemId.StartsWith("custom-pool-"))
            {
                return false;
            }
            var parts = itemId.Substring("custom-pool-".Length).Split('x');
            if (parts.Length == 2 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)
                && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
            {
                size = new Vector2(Mathf.Clamp(w, 2f, 30f), Mathf.Clamp(d, 2f, 20f));
            }
            return true;
        }

        /// <summary>Every custom item id (pools come in any size: see <see cref="TryPoolSize"/>).</summary>
        public static readonly string[] Ids =
        {
            "custom-lounger", "custom-parasol", "custom-firepit", "custom-planter", "custom-column", "custom-easel",
            "custom-lightstring", "custom-skybar", "custom-backbar", "custom-openkitchen", "custom-hologram", "custom-djbooth",
            "custom-telescope", "custom-divider", "custom-pendant", "custom-ledstrip", ElevatorItem, CoreItem, "custom-turnstiles",
            "custom-reception", "custom-screenwall", "custom-queuelane", "custom-rug", "custom-ruground",
            "custom-glasswall", "custom-phonebooth", "custom-officechair", "custom-benchdesk", "custom-stage", "custom-buffet",
            "custom-greenwall",
        };

        /// <summary>Game stations built here (item id → game id the room hub knows).</summary>
        public static readonly IReadOnlyDictionary<string, string> GameStations = new Dictionary<string, string>
        {
            ["game-connectfour"] = "connectfour",
            ["game-memory"] = "memory",
            ["game-chess"] = "chess",
            ["game-quizshow"] = "quiz",
        };

        private readonly Material _litBase;
        private readonly Func<string, Transform, GameObject> _spawnModel;
        private readonly Material _water;
        private readonly Material _glass;
        private readonly RoomTheme _theme;

        /// <param name="spawnModel">Places a catalog model (e.g. the Poly Haven chess set) under a transform; optional.</param>
        public CustomItems(Material litBase, Material water, Material glass, RoomTheme theme, Func<string, Transform, GameObject> spawnModel = null)
        {
            _spawnModel = spawnModel;
            _litBase = litBase;
            _water = water;
            _glass = glass;
            _theme = theme;
        }

        /// <summary>Builds the item under <paramref name="pivot"/>. Returns false if the id is unknown.</summary>
        /// <param name="blocksTiles">Whether avatars must walk around it.</param>
        public bool TryBuild(string itemId, Transform pivot, out bool blocksTiles)
        {
            blocksTiles = true;
            if (TryPoolSize(itemId, out var poolSize))
            {
                Pool(pivot, poolSize);   // one can swim in it: the room makes its tiles water, not obstacles
                blocksTiles = false;
                return true;
            }
            switch (itemId)
            {
                case "custom-lounger": Lounger(pivot); return true;
                case "custom-parasol": Parasol(pivot); return true;
                case "custom-firepit": FirePit(pivot); return true;
                case "custom-planter": Planter(pivot); return true;
                case "custom-column": Column(pivot); return true;
                case "custom-easel": Easel(pivot); return true;
                case "custom-lightstring": LightString(pivot); blocksTiles = false; return true;
                case "custom-skybar": SkyBar(pivot); return true;
                case "custom-backbar": BackBar(pivot); return true;
                case "custom-openkitchen": OpenKitchen(pivot); return true;
                case "custom-hologram": Hologram(pivot); return true;
                case "custom-djbooth": DjBooth(pivot); return true;
                case "custom-telescope": Telescope(pivot); return true;
                case "custom-divider": Divider(pivot); return true;
                case "custom-pendant": Pendant(pivot); blocksTiles = false; return true;
                case "custom-ledstrip": LedStrip(pivot); blocksTiles = false; return true;
                case ElevatorItem: Elevator(pivot); return true;
                case CoreItem: Core(pivot); return true;
                case "custom-glasswall": GlassWall(pivot); return true;
                case "custom-phonebooth": PhoneBooth(pivot); return true;
                case "custom-officechair": OfficeChair(pivot); return true;
                case "custom-benchdesk": BenchDesk(pivot); return true;
                case "custom-stage": Stage(pivot); return true;
                case "custom-buffet": Buffet(pivot); return true;
                case "custom-greenwall": GreenWall(pivot); return true;
                case "game-connectfour": ConnectFourStation(pivot); return true;
                case "game-memory": MemoryStation(pivot); return true;
                case "game-chess": ChessStation(pivot); return true;
                case "game-quizshow": QuizShowStation(pivot); return true;
                case "custom-turnstiles": Turnstiles(pivot); blocksTiles = false; return true;
                case "custom-reception": Reception(pivot); return true;
                case "custom-screenwall": ScreenWall(pivot); return true;
                case "custom-queuelane": QueueLane(pivot); blocksTiles = false; return true;
                case "custom-rug": Rug(pivot, round: false); blocksTiles = false; return true;
                case "custom-ruground": Rug(pivot, round: true); blocksTiles = false; return true;
                default: return false;
            }
        }

        /// <summary>
        /// Pool sunk into the floor (the room leaves a hole there): tiled walls and floor with lane lines, translucent
        /// water a little below the edge, a stone coping around it, a steel ladder and underwater light.
        /// </summary>
        private void Pool(Transform pivot, Vector2 size)
        {
            float w = size.x, d = size.y;
            const float rim = 0.35f;
            var tiles = Lit(new Color(0.55f, 0.82f, 0.9f), smoothness: 0.6f);
            var deep = Lit(new Color(0.18f, 0.55f, 0.72f), smoothness: 0.6f);
            var stone = Lit(new Color(0.92f, 0.9f, 0.86f), smoothness: 0.25f);

            // Basin: floor and four walls below the room floor.
            Box(pivot, deep, new Vector3(0f, -PoolDepth - 0.05f, 0f), new Vector3(w, 0.1f, d));
            Box(pivot, tiles, new Vector3(0f, -PoolDepth / 2f, d / 2f + 0.05f), new Vector3(w + 0.2f, PoolDepth, 0.1f));
            Box(pivot, tiles, new Vector3(0f, -PoolDepth / 2f, -d / 2f - 0.05f), new Vector3(w + 0.2f, PoolDepth, 0.1f));
            Box(pivot, tiles, new Vector3(w / 2f + 0.05f, -PoolDepth / 2f, 0f), new Vector3(0.1f, PoolDepth, d));
            Box(pivot, tiles, new Vector3(-w / 2f - 0.05f, -PoolDepth / 2f, 0f), new Vector3(0.1f, PoolDepth, d));
            var lane = Lit(new Color(0.08f, 0.2f, 0.35f));
            for (var i = 1; i < Mathf.RoundToInt(d / 2f); i++)
            {
                Box(pivot, lane, new Vector3(0f, -PoolDepth + 0.01f, -d / 2f + i * 2f), new Vector3(w - 1.4f, 0.02f, 0.2f));
            }

            // Coping on the floor around the edge.
            Box(pivot, stone, new Vector3(0f, 0.02f, d / 2f + rim / 2f), new Vector3(w + 2 * rim, 0.04f, rim));
            Box(pivot, stone, new Vector3(0f, 0.02f, -d / 2f - rim / 2f), new Vector3(w + 2 * rim, 0.04f, rim));
            Box(pivot, stone, new Vector3(w / 2f + rim / 2f, 0.02f, 0f), new Vector3(rim, 0.04f, d));
            Box(pivot, stone, new Vector3(-w / 2f - rim / 2f, 0.02f, 0f), new Vector3(rim, 0.04f, d));

            Box(pivot, _water, new Vector3(0f, WaterLevel, 0f), new Vector3(w, 0.02f, d));

            var steel = Lit(new Color(0.78f, 0.8f, 0.84f), smoothness: 0.85f);
            foreach (var side in new[] { -0.3f, 0.3f })
            {
                var rail = Cylinder(pivot, steel, new Vector3(w / 2f - 0.15f, -0.15f, side), new Vector3(0.05f, 0.75f, 0.05f));
                rail.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            }
            for (var i = 0; i < 4; i++)
            {
                Box(pivot, steel, new Vector3(w / 2f - 0.12f - i * 0.05f, -0.35f - i * 0.3f, 0f), new Vector3(0.1f, 0.03f, 0.55f));
            }
            AddLight(pivot, new Vector3(0f, -PoolDepth + 0.4f, 0f), new Color(0.45f, 0.85f, 1f), 2.5f, Mathf.Max(w, d));
        }

        /// <summary>Wooden sun lounger with a cushion and a raised back rest (one can sit / lie on it).</summary>
        private void Lounger(Transform pivot)
        {
            var wood = Lit(new Color(0.55f, 0.38f, 0.24f), smoothness: 0.3f);
            var cushion = Lit(new Color(0.94f, 0.93f, 0.9f), smoothness: 0.1f);
            // Seat part towards -Z, back rest rising at +Z.
            Box(pivot, wood, new Vector3(0f, 0.28f, -0.25f), new Vector3(0.7f, 0.06f, 1.4f));
            Box(pivot, cushion, new Vector3(0f, 0.34f, -0.25f), new Vector3(0.64f, 0.07f, 1.35f));
            foreach (var (x, z) in new[] { (-0.3f, -0.9f), (0.3f, -0.9f), (-0.3f, 0.35f), (0.3f, 0.35f) })
            {
                Box(pivot, wood, new Vector3(x, 0.13f, z), new Vector3(0.06f, 0.26f, 0.06f));
            }
            var back = Box(pivot, wood, new Vector3(0f, 0.55f, 0.72f), new Vector3(0.7f, 0.06f, 0.75f));
            back.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            var backCushion = Box(pivot, cushion, new Vector3(0f, 0.58f, 0.69f), new Vector3(0.64f, 0.07f, 0.7f));
            backCushion.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            // A rolled towel at the foot end.
            var towel = Cylinder(pivot, Lit(_theme.WallTrim), new Vector3(0f, 0.43f, -0.8f), new Vector3(0.12f, 0.25f, 0.12f));
            towel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
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

        // ---------- Sky lounge (Prime Tower top floor, futuristic) ----------

        /// <summary>5 m bar counter: backlit onyx front, black stone top, steel foot rail, LED line on the floor.</summary>
        private void SkyBar(Transform pivot)
        {
            const float length = 5f, depth = 0.75f, height = 1.08f;
            var body = Lit(new Color(0.08f, 0.08f, 0.1f), 0.6f);
            Box(pivot, body, new Vector3(0f, height / 2f, 0.05f), new Vector3(length, height, depth - 0.1f));
            // Backlit front: warm onyx panels with thin seams.
            var onyx = Glow(new Color(1f, 0.86f, 0.62f), 1.4f);
            for (var i = 0; i < 5; i++)
            {
                Box(pivot, onyx, new Vector3(-length / 2f + 0.5f + i * 1f, 0.55f, -depth / 2f + 0.04f), new Vector3(0.96f, 0.82f, 0.02f));
            }
            Box(pivot, Lit(new Color(0.03f, 0.03f, 0.035f), 0.9f), new Vector3(0f, height + 0.03f, -0.02f), new Vector3(length + 0.1f, 0.06f, depth + 0.1f));
            var steel = Lit(new Color(0.78f, 0.8f, 0.84f), 0.85f);
            Box(pivot, steel, new Vector3(0f, 0.2f, -depth / 2f - 0.14f), new Vector3(length - 0.2f, 0.04f, 0.04f));
            Box(pivot, Glow(_theme.WallTrim, 2.5f), new Vector3(0f, 0.012f, -depth / 2f - 0.02f), new Vector3(length, 0.02f, 0.04f));
            // Glasses on the counter.
            var glass = Tinted(_glass, new Color(0.85f, 0.95f, 1f, 0.45f));
            for (var i = 0; i < 6; i++)
            {
                Cylinder(pivot, glass, new Vector3(-2f + i * 0.8f, height + 0.13f, 0.05f), new Vector3(0.07f, 0.07f, 0.07f));
            }
            AddLight(pivot, new Vector3(0f, 1.8f, -0.6f), new Color(1f, 0.85f, 0.65f), 1.6f, 4.5f);
        }

        /// <summary>Back bar: mirrored wall with three glowing glass shelves full of bottles.</summary>
        private void BackBar(Transform pivot)
        {
            const float length = 5f, height = 2.6f;
            var frame = Lit(new Color(0.07f, 0.07f, 0.08f), 0.5f);
            Box(pivot, frame, new Vector3(0f, 0.45f, 0f), new Vector3(length, 0.9f, 0.5f));   // cabinets
            Box(pivot, Lit(new Color(0.55f, 0.6f, 0.65f), 0.95f), new Vector3(0f, 1.75f, 0.2f), new Vector3(length - 0.1f, 1.7f, 0.03f));   // mirror
            Box(pivot, frame, new Vector3(-length / 2f, height / 2f, 0.05f), new Vector3(0.08f, height, 0.4f));
            Box(pivot, frame, new Vector3(length / 2f, height / 2f, 0.05f), new Vector3(0.08f, height, 0.4f));
            Box(pivot, frame, new Vector3(0f, height, 0.05f), new Vector3(length + 0.08f, 0.08f, 0.4f));

            var shelf = Glow(new Color(0.75f, 0.95f, 1f), 1.2f);
            var colors = new[]
            {
                new Color(0.2f, 0.55f, 0.25f), new Color(0.75f, 0.45f, 0.12f), new Color(0.9f, 0.9f, 0.95f),
                new Color(0.55f, 0.1f, 0.18f), new Color(0.2f, 0.35f, 0.7f), new Color(0.85f, 0.7f, 0.2f),
            };
            var bottles = new Material[colors.Length];
            for (var c = 0; c < colors.Length; c++)
            {
                bottles[c] = Tinted(_glass, new Color(colors[c].r, colors[c].g, colors[c].b, 0.8f));
            }
            var random = new System.Random(7);
            for (var level = 0; level < 3; level++)
            {
                var y = 1.1f + level * 0.5f;
                Box(pivot, shelf, new Vector3(0f, y, 0.08f), new Vector3(length - 0.2f, 0.03f, 0.3f));
                for (var x = -length / 2f + 0.25f; x < length / 2f - 0.2f; x += 0.19f + (float)random.NextDouble() * 0.1f)
                {
                    var bottle = bottles[random.Next(bottles.Length)];
                    var h = 0.24f + (float)random.NextDouble() * 0.1f;
                    Cylinder(pivot, bottle, new Vector3(x, y + 0.015f + h / 2f, 0.08f), new Vector3(0.08f, h / 2f, 0.08f));
                    Cylinder(pivot, bottle, new Vector3(x, y + 0.015f + h + 0.05f, 0.08f), new Vector3(0.03f, 0.05f, 0.03f));
                }
            }
        }

        /// <summary>Open kitchen: steel counter, black induction top, pass with heat lamps and a floating hood.</summary>
        private void OpenKitchen(Transform pivot)
        {
            const float length = 4f, depth = 1f;
            var steel = Lit(new Color(0.72f, 0.74f, 0.78f), 0.8f);
            Box(pivot, steel, new Vector3(0f, 0.45f, 0f), new Vector3(length, 0.9f, depth));
            Box(pivot, Lit(new Color(0.05f, 0.05f, 0.06f), 0.9f), new Vector3(-0.8f, 0.915f, 0.05f), new Vector3(1.6f, 0.03f, 0.7f));
            var ring = Glow(new Color(1f, 0.35f, 0.1f), 1.5f);
            foreach (var x in new[] { -1.3f, -0.3f })
            {
                Cylinder(pivot, ring, new Vector3(x, 0.93f, 0.05f), new Vector3(0.28f, 0.005f, 0.28f));
            }
            // Chopping board and a bowl.
            Box(pivot, Lit(new Color(0.6f, 0.42f, 0.25f)), new Vector3(0.9f, 0.92f, 0.1f), new Vector3(0.5f, 0.03f, 0.32f));
            Sphere(pivot, Lit(new Color(0.95f, 0.95f, 0.93f), 0.8f), new Vector3(1.5f, 0.95f, 0.1f), new Vector3(0.22f, 0.1f, 0.22f));
            // Pass shelf with heat lamps on the guest side.
            Box(pivot, steel, new Vector3(0f, 1.35f, -0.35f), new Vector3(length, 0.04f, 0.35f));
            foreach (var x in new[] { -1.4f, 0f, 1.4f })
            {
                Cylinder(pivot, steel, new Vector3(x, 1.12f, -0.35f), new Vector3(0.03f, 0.22f, 0.03f));
                Cylinder(pivot, Glow(new Color(1f, 0.5f, 0.2f), 2f), new Vector3(x, 1.33f, -0.35f), new Vector3(0.12f, 0.01f, 0.12f));
            }
            // Floating hood.
            Box(pivot, steel, new Vector3(-0.8f, 2.6f, 0.05f), new Vector3(1.8f, 0.35f, 0.8f));
            Box(pivot, Glow(new Color(1f, 0.95f, 0.85f), 1.2f), new Vector3(-0.8f, 2.42f, 0.05f), new Vector3(1.6f, 0.01f, 0.6f));
            AddLight(pivot, new Vector3(0f, 1.9f, -0.6f), new Color(1f, 0.8f, 0.6f), 1.2f, 4f);
        }

        /// <summary>Pedestal with a slowly turning hologram of the Prime Tower and scan rings.</summary>
        private void Hologram(Transform pivot)
        {
            var dark = Lit(new Color(0.06f, 0.06f, 0.08f), 0.8f);
            Cylinder(pivot, dark, new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.35f, 0.9f));
            Cylinder(pivot, Glow(_theme.WallTrim, 3f), new Vector3(0f, 0.71f, 0f), new Vector3(0.8f, 0.01f, 0.8f));

            var holo = new GameObject("Hologram").transform;
            holo.SetParent(pivot, false);
            holo.localPosition = new Vector3(0f, 0.75f, 0f);
            holo.localScale = Vector3.one * 1.5f;
            holo.gameObject.AddComponent<Spinner>().degreesPerSecond = 18f;
            var beam = HologramMaterial(_theme.WallTrim, 0.3f);
            var solid = HologramMaterial(_theme.WallTrim, 0.6f);
            // Prime Tower: stacked, slightly twisted floors that widen towards the top.
            for (var i = 0; i < 12; i++)
            {
                var widen = 1f + i * 0.012f;
                var floor = Box(holo, i % 3 == 0 ? solid : beam, new Vector3(0f, 0.1f + i * 0.09f, 0f), new Vector3(0.34f * widen, 0.07f, 0.24f * widen));
                floor.transform.localRotation = Quaternion.Euler(0f, i * 1.5f, 0f);
            }
            Box(holo, solid, new Vector3(0.03f, 1.2f, 0f), new Vector3(0.3f, 0.04f, 0.2f));
            foreach (var height in new[] { 0.3f, 0.75f })
            {
                var ring = Cylinder(holo, beam, new Vector3(0f, height, 0f), new Vector3(0.62f, 0.004f, 0.62f));
                ring.AddComponent<Bobbing>().amplitude = 0.35f;
            }
            AddLight(pivot, new Vector3(0f, 1.4f, 0f), _theme.WallTrim, 1.4f, 3.5f);
        }

        /// <summary>DJ booth with a glowing front and an animated equalizer.</summary>
        private void DjBooth(Transform pivot)
        {
            const float length = 2.2f;
            Box(pivot, Lit(new Color(0.06f, 0.06f, 0.07f), 0.7f), new Vector3(0f, 0.5f, 0f), new Vector3(length, 1f, 0.8f));
            Box(pivot, Lit(new Color(0.02f, 0.02f, 0.025f), 0.9f), new Vector3(0f, 1.02f, 0f), new Vector3(length + 0.05f, 0.04f, 0.85f));
            var decks = Lit(new Color(0.12f, 0.12f, 0.14f), 0.6f);
            foreach (var x in new[] { -0.6f, 0.6f })
            {
                Cylinder(pivot, decks, new Vector3(x, 1.06f, 0.05f), new Vector3(0.36f, 0.02f, 0.36f));
                Cylinder(pivot, Glow(_theme.WallTrim, 2f), new Vector3(x, 1.075f, 0.05f), new Vector3(0.08f, 0.01f, 0.08f));
            }
            Box(pivot, decks, new Vector3(0f, 1.07f, 0.05f), new Vector3(0.4f, 0.04f, 0.3f));
            // Equalizer bars on the front (facing the guests).
            var colors = new[] { Glow(_theme.WallTrim, 2.5f), Glow(new Color(0.7f, 0.35f, 1f), 2.5f), Glow(new Color(1f, 0.3f, 0.6f), 2.5f) };
            for (var i = 0; i < 14; i++)
            {
                var bar = Box(pivot, colors[i % 3], new Vector3(-length / 2f + 0.2f + i * 0.13f, 0.5f, -0.41f), new Vector3(0.08f, 0.6f, 0.01f));
                bar.AddComponent<EqualizerBar>().phase = i * 0.7f;
            }
        }

        /// <summary>Panorama viewer at the window, like on observation decks, with a glowing lens.</summary>
        private void Telescope(Transform pivot)
        {
            var metal = Lit(new Color(0.18f, 0.19f, 0.22f), 0.75f);
            Cylinder(pivot, metal, new Vector3(0f, 0.04f, 0f), new Vector3(0.45f, 0.04f, 0.45f));
            Cylinder(pivot, metal, new Vector3(0f, 0.6f, 0f), new Vector3(0.1f, 0.55f, 0.1f));
            var head = Box(pivot, Lit(new Color(0.85f, 0.87f, 0.9f), 0.85f), new Vector3(0f, 1.25f, 0.05f), new Vector3(0.34f, 0.24f, 0.5f));
            head.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            foreach (var x in new[] { -0.08f, 0.08f })
            {
                Cylinder(pivot, metal, new Vector3(x, 1.27f, -0.23f), new Vector3(0.07f, 0.03f, 0.07f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Cylinder(pivot, Glow(_theme.WallTrim, 2.5f), new Vector3(0f, 1.3f, 0.31f), new Vector3(0.18f, 0.01f, 0.18f)).transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
        }

        /// <summary>3 m frosted glass partition with glowing edges (Prive, smokers lounge).</summary>
        private void Divider(Transform pivot)
        {
            const float length = 3f, height = 2.3f;
            Box(pivot, Tinted(_glass, new Color(0.85f, 0.95f, 0.95f, 0.35f)), new Vector3(0f, height / 2f, 0f), new Vector3(length, height, 0.04f));
            var edge = Glow(_theme.WallTrim, 2.2f);
            Box(pivot, edge, new Vector3(0f, 0.02f, 0f), new Vector3(length, 0.04f, 0.08f));
            Box(pivot, edge, new Vector3(0f, height, 0f), new Vector3(length, 0.03f, 0.06f));
            var metal = Lit(new Color(0.12f, 0.12f, 0.14f), 0.7f);
            Box(pivot, metal, new Vector3(-length / 2f, height / 2f, 0f), new Vector3(0.05f, height, 0.08f));
            Box(pivot, metal, new Vector3(length / 2f, height / 2f, 0f), new Vector3(0.05f, height, 0.08f));
        }

        /// <summary>
        /// Cluster of hand-blown glass orbs (Murano style) that float and breathe above tables – the room has
        /// no visible ceiling (camera from above), so they levitate instead of hanging on cables.
        /// </summary>
        private void Pendant(Transform pivot)
        {
            var shades = new[] { Glow(new Color(1f, 0.78f, 0.45f), 1.4f), Glow(new Color(1f, 0.64f, 0.42f), 1.4f), Glow(new Color(0.98f, 0.86f, 0.62f), 1.4f) };
            var offsets = new[] { new Vector3(0f, 2.55f, 0f), new Vector3(0.2f, 2.35f, 0.12f), new Vector3(-0.18f, 2.45f, 0.14f), new Vector3(0.06f, 2.25f, -0.2f), new Vector3(-0.14f, 2.65f, -0.12f) };
            for (var i = 0; i < offsets.Length; i++)
            {
                var orb = Sphere(pivot, shades[i % 3], offsets[i], Vector3.one * (0.16f + 0.04f * (i % 2)));
                var bob = orb.AddComponent<Bobbing>();
                bob.amplitude = 0.06f;
                bob.speed = 0.6f + 0.15f * i;
            }
            AddLight(pivot, new Vector3(0f, 2.2f, 0f), new Color(1f, 0.8f, 0.55f), 1.4f, 3.5f);
        }

        /// <summary>
        /// Lift bank in the building core: a stone-clad shaft block (4.8 × 2.4 m, full storey height) with two
        /// cars. Brushed steel doors with a centre seam in steel portals, a glowing floor indicator above each
        /// door and a call-button plate between them. Doors face -Z.
        /// </summary>
        private void Elevator(Transform pivot)
        {
            const float width = 4.8f, depth = ElevatorDepth, height = 3.4f;
            var stone = Lit(Color.Lerp(_theme.Wall, new Color(0.08f, 0.1f, 0.09f), 0.55f), 0.7f);
            var darkSteel = Lit(new Color(0.22f, 0.23f, 0.25f), 0.75f);
            Box(pivot, stone, new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth));
            Box(pivot, darkSteel, new Vector3(0f, height + 0.03f, 0f), new Vector3(width + 0.06f, 0.06f, depth + 0.06f));   // top edge
            LiftDoors(pivot, width, depth, new[] { -1.2f, 1.2f });
        }

        /// <summary>
        /// Concrete core of a tower floor (lifts, stairs, shafts): board-marked concrete, lift doors on both long sides,
        /// a brass band and the floor number. Part of the building – the build editor can't move it.
        /// </summary>
        private void Core(Transform pivot)
        {
            const float width = CoreWidth, depth = CoreDepth, height = 3.4f;
            var concrete = Lit(new Color(0.62f, 0.61f, 0.58f), 0.18f);
            var darkSteel = Lit(new Color(0.22f, 0.23f, 0.25f), 0.75f);
            var brass = Lit(new Color(0.78f, 0.62f, 0.34f), 0.8f);
            Box(pivot, concrete, new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth));
            Box(pivot, darkSteel, new Vector3(0f, height + 0.03f, 0f), new Vector3(width + 0.06f, 0.06f, depth + 0.06f));
            // Board marks of the formwork: slightly darker bands every 60 cm.
            var band = Lit(new Color(0.55f, 0.54f, 0.51f), 0.12f);
            for (var y = 0.6f; y < height - 0.1f; y += 0.6f)
            {
                Box(pivot, band, new Vector3(0f, y, 0f), new Vector3(width + 0.01f, 0.02f, depth + 0.01f));
            }
            Box(pivot, brass, new Vector3(0f, 2.95f, 0f), new Vector3(width + 0.02f, 0.05f, depth + 0.02f));
            foreach (var side in new[] { 0f, 180f })
            {
                var face = new GameObject("Side").transform;
                face.SetParent(pivot, false);
                face.localRotation = Quaternion.Euler(0f, side, 0f);
                LiftDoors(face, width, depth, new[] { -2.6f, -1.1f, 1.1f, 2.6f });
            }
        }

        /// <summary>
        /// Glass partition for meeting rooms in open-plan offices: 2 m of clear glass in a slim black frame with a frosted
        /// band at eye level (so nobody walks into it). Rows of them make rooms; leave a gap as the door.
        /// </summary>
        private void GlassWall(Transform pivot)
        {
            const float length = 1.98f, height = 2.6f;
            var frame = Lit(new Color(0.08f, 0.08f, 0.09f), 0.6f);
            Box(pivot, Tinted(_glass, new Color(0.86f, 0.94f, 0.95f, 0.22f)), new Vector3(0f, height / 2f, 0f), new Vector3(length, height, 0.02f));
            Box(pivot, Tinted(_glass, new Color(1f, 1f, 1f, 0.55f)), new Vector3(0f, 1.15f, 0f), new Vector3(length, 0.3f, 0.025f));
            Box(pivot, frame, new Vector3(0f, 0.03f, 0f), new Vector3(length, 0.06f, 0.06f));
            Box(pivot, frame, new Vector3(0f, height, 0f), new Vector3(length, 0.05f, 0.06f));
            Box(pivot, frame, new Vector3(-length / 2f, height / 2f, 0f), new Vector3(0.03f, height, 0.05f));
            Box(pivot, frame, new Vector3(length / 2f, height / 2f, 0f), new Vector3(0.03f, height, 0.05f));
        }

        /// <summary>Acoustic phone booth: walnut shell, glass door, green felt inside, a small shelf and a pendant light.</summary>
        private void PhoneBooth(Transform pivot)
        {
            const float width = 1.05f, depth = 1.05f, height = 2.2f;
            var walnut = Lit(new Color(0.35f, 0.22f, 0.14f), 0.45f);
            var felt = Lit(new Color(0.18f, 0.32f, 0.27f), 0.05f);
            Box(pivot, walnut, new Vector3(0f, height - 0.05f, 0f), new Vector3(width, 0.1f, depth));               // roof
            Box(pivot, walnut, new Vector3(0f, 0.04f, 0f), new Vector3(width, 0.08f, depth));                       // floor
            Box(pivot, walnut, new Vector3(0f, height / 2f, depth / 2f - 0.04f), new Vector3(width, height, 0.08f)); // back
            Box(pivot, walnut, new Vector3(-width / 2f + 0.04f, height / 2f, 0f), new Vector3(0.08f, height, depth));
            Box(pivot, walnut, new Vector3(width / 2f - 0.04f, height / 2f, 0f), new Vector3(0.08f, height, depth));
            Box(pivot, felt, new Vector3(0f, height / 2f, depth / 2f - 0.09f), new Vector3(width - 0.18f, height - 0.2f, 0.02f));
            Box(pivot, Tinted(_glass, new Color(0.85f, 0.93f, 0.93f, 0.25f)), new Vector3(0f, height / 2f, -depth / 2f + 0.02f),
                new Vector3(width - 0.1f, height - 0.2f, 0.02f));                                                   // glass door
            Box(pivot, Lit(new Color(0.75f, 0.75f, 0.72f), 0.8f), new Vector3(0.36f, 1.1f, -depth / 2f + 0.01f), new Vector3(0.02f, 0.4f, 0.03f));
            Box(pivot, walnut, new Vector3(0f, 1.05f, depth / 2f - 0.25f), new Vector3(0.7f, 0.04f, 0.3f));        // shelf
            Box(pivot, Glow(new Color(1f, 0.92f, 0.78f), 1.4f), new Vector3(0f, height - 0.12f, 0f), new Vector3(0.5f, 0.02f, 0.5f));
        }

        /// <summary>Ergonomic office chair: mesh back, padded seat, five-star base on castors.</summary>
        private void OfficeChair(Transform pivot)
        {
            var black = Lit(new Color(0.07f, 0.07f, 0.08f), 0.35f);
            var mesh = Lit(new Color(0.15f, 0.15f, 0.17f), 0.2f);
            var chrome = Lit(new Color(0.7f, 0.72f, 0.75f), 0.9f);
            for (var i = 0; i < 5; i++)
            {
                var angle = i * 72f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var leg = Box(pivot, chrome, dir * 0.15f + Vector3.up * 0.08f, new Vector3(0.04f, 0.03f, 0.3f));
                leg.transform.localRotation = Quaternion.LookRotation(dir);
                Sphere(pivot, black, dir * 0.29f + Vector3.up * 0.03f, new Vector3(0.06f, 0.06f, 0.06f));      // castor
            }
            Cylinder(pivot, chrome, new Vector3(0f, 0.27f, 0f), new Vector3(0.05f, 0.18f, 0.05f));             // gas lift
            Box(pivot, black, new Vector3(0f, 0.47f, 0f), new Vector3(0.5f, 0.08f, 0.48f));                    // seat
            Box(pivot, black, new Vector3(0f, 0.53f, 0.02f), new Vector3(0.46f, 0.04f, 0.42f));                // cushion
            Box(pivot, chrome, new Vector3(0f, 0.72f, 0.26f), new Vector3(0.05f, 0.4f, 0.04f));                // spine
            Box(pivot, mesh, new Vector3(0f, 0.92f, 0.27f), new Vector3(0.46f, 0.55f, 0.04f));                 // mesh back
            Box(pivot, black, new Vector3(0f, 1.2f, 0.27f), new Vector3(0.46f, 0.04f, 0.05f));
            foreach (var x in new[] { -0.27f, 0.27f })
            {
                Box(pivot, black, new Vector3(x, 0.68f, 0.02f), new Vector3(0.05f, 0.03f, 0.3f));              // armrests
                Box(pivot, black, new Vector3(x, 0.6f, 0.08f), new Vector3(0.03f, 0.14f, 0.03f));
            }
        }

        /// <summary>Modern desk: white top, black steel frame, cable tray – rows of them make open-plan workplaces.</summary>
        private void BenchDesk(Transform pivot)
        {
            const float width = 1.6f, depth = 0.8f, height = 0.74f;
            var white = Lit(new Color(0.93f, 0.93f, 0.91f), 0.35f);
            var steel = Lit(new Color(0.09f, 0.09f, 0.1f), 0.55f);
            Box(pivot, white, new Vector3(0f, height - 0.015f, 0f), new Vector3(width, 0.03f, depth));
            foreach (var x in new[] { -width / 2f + 0.06f, width / 2f - 0.06f })
            {
                Box(pivot, steel, new Vector3(x, height / 2f, 0f), new Vector3(0.05f, height - 0.03f, 0.05f));   // leg
                Box(pivot, steel, new Vector3(x, 0.02f, 0f), new Vector3(0.06f, 0.04f, depth - 0.1f));            // foot
                Box(pivot, steel, new Vector3(x, height - 0.06f, 0f), new Vector3(0.05f, 0.05f, depth - 0.1f));   // top rail
            }
            Box(pivot, steel, new Vector3(0f, height - 0.18f, 0.2f), new Vector3(width - 0.3f, 0.08f, 0.12f));     // cable tray
        }

        /// <summary>Conference stage: dark oak platform with a glowing edge and two steps at the front.</summary>
        private void Stage(Transform pivot)
        {
            const float width = 6f, depth = 3f, height = 0.4f;
            var oak = Lit(new Color(0.24f, 0.17f, 0.12f), 0.5f);
            var black = Lit(new Color(0.06f, 0.06f, 0.07f), 0.3f);
            Box(pivot, black, new Vector3(0f, height / 2f - 0.02f, 0f), new Vector3(width, height - 0.04f, depth));
            Box(pivot, oak, new Vector3(0f, height - 0.02f, 0f), new Vector3(width + 0.04f, 0.04f, depth + 0.04f));
            Box(pivot, Glow(_theme.WallTrim, 2f), new Vector3(0f, 0.06f, -depth / 2f - 0.01f), new Vector3(width, 0.03f, 0.02f));
            Box(pivot, oak, new Vector3(0f, height / 2f - 0.07f, -depth / 2f - 0.2f), new Vector3(1.4f, height / 2f, 0.4f));
        }

        /// <summary>Catering buffet: table with a white cloth down to the floor and a dark top runner.</summary>
        private void Buffet(Transform pivot)
        {
            const float width = 2.4f, depth = 0.8f, height = 0.9f;
            Box(pivot, Lit(new Color(0.96f, 0.96f, 0.95f), 0.15f), new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth));
            Box(pivot, Lit(new Color(0.16f, 0.22f, 0.2f), 0.2f), new Vector3(0f, height + 0.005f, 0f), new Vector3(width - 0.2f, 0.01f, 0.3f));
        }

        /// <summary>Vertical garden: a panel of moss and ferns in a slim oak frame, lit from above.</summary>
        private void GreenWall(Transform pivot)
        {
            const float width = 3f, height = 2.5f;
            var oak = Lit(new Color(0.55f, 0.4f, 0.26f), 0.4f);
            Box(pivot, oak, new Vector3(0f, height / 2f, 0.05f), new Vector3(width + 0.1f, height + 0.1f, 0.12f));
            Box(pivot, Lit(new Color(0.22f, 0.36f, 0.18f), 0.05f), new Vector3(0f, height / 2f, -0.02f), new Vector3(width, height, 0.04f));
            var greens = new[] { new Color(0.27f, 0.5f, 0.24f), new Color(0.36f, 0.6f, 0.3f), new Color(0.2f, 0.42f, 0.22f), new Color(0.45f, 0.62f, 0.32f) };
            var random = new System.Random(7);
            for (var i = 0; i < 36; i++)
            {
                var x = -width / 2f + 0.15f + (float)random.NextDouble() * (width - 0.3f);
                var y = 0.2f + (float)random.NextDouble() * (height - 0.35f);
                var size = 0.18f + (float)random.NextDouble() * 0.22f;
                Sphere(pivot, Lit(greens[i % greens.Length], 0.1f), new Vector3(x, y, -0.06f), new Vector3(size, size * 0.85f, size * 0.5f));
            }
            Box(pivot, Glow(new Color(1f, 0.93f, 0.8f), 1.2f), new Vector3(0f, height + 0.06f, -0.08f), new Vector3(width, 0.02f, 0.04f));
        }

        /// <summary>A small café table (round top on a pedestal) that games stand on; returns the height of its top.</summary>
        private float GameTable(Transform pivot, float radius, Color top)
        {
            const float height = 0.74f;
            var dark = Lit(new Color(0.1f, 0.1f, 0.11f), 0.5f);
            Cylinder(pivot, dark, new Vector3(0f, 0.02f, 0f), new Vector3(radius * 1.1f, 0.02f, radius * 1.1f));
            Cylinder(pivot, dark, new Vector3(0f, height / 2f, 0f), new Vector3(0.08f, height / 2f, 0.08f));
            Cylinder(pivot, Lit(top, 0.55f), new Vector3(0f, height - 0.02f, 0f), new Vector3(radius * 2f, 0.02f, radius * 2f));
            return height;
        }

        /// <summary>Connect Four: an upright blue frame with a few discs on a table.</summary>
        private void ConnectFourStation(Transform pivot)
        {
            var top = GameTable(pivot, 0.45f, new Color(0.93f, 0.93f, 0.91f));
            var blue = Lit(new Color(0.1f, 0.3f, 0.75f), 0.5f);
            Box(pivot, blue, new Vector3(0f, top + 0.26f, 0f), new Vector3(0.62f, 0.5f, 0.05f));
            Box(pivot, blue, new Vector3(0f, top + 0.01f, 0f), new Vector3(0.7f, 0.02f, 0.2f));
            var red = Lit(new Color(0.95f, 0.25f, 0.4f), 0.6f);
            var yellow = Lit(new Color(1f, 0.82f, 0.2f), 0.6f);
            var hole = Lit(new Color(0.05f, 0.1f, 0.2f), 0.3f);
            for (var column = 0; column < 7; column++)
            {
                for (var row = 0; row < 6; row++)
                {
                    var filled = row < (column * 5 + 3) % 4;
                    var disc = Cylinder(pivot, filled ? ((column + row) % 2 == 0 ? red : yellow) : hole,
                        new Vector3(-0.255f + column * 0.085f, top + 0.06f + row * 0.075f, -0.03f), new Vector3(0.065f, 0.005f, 0.065f));
                    disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }
        }

        /// <summary>Memory: 16 cards face down on a square table, two turned.</summary>
        private void MemoryStation(Transform pivot)
        {
            const float height = 0.74f;
            var oak = Lit(new Color(0.62f, 0.45f, 0.3f), 0.45f);
            Box(pivot, oak, new Vector3(0f, height - 0.02f, 0f), new Vector3(0.9f, 0.04f, 0.9f));
            var dark = Lit(new Color(0.1f, 0.1f, 0.11f), 0.5f);
            foreach (var (x, z) in new[] { (-0.38f, -0.38f), (0.38f, -0.38f), (-0.38f, 0.38f), (0.38f, 0.38f) })
            {
                Box(pivot, dark, new Vector3(x, height / 2f, z), new Vector3(0.04f, height, 0.04f));
            }
            var back = Lit(_theme.WallTrim, 0.4f);
            var face = Lit(new Color(0.97f, 0.96f, 0.93f), 0.3f);
            for (var i = 0; i < 16; i++)
            {
                var (x, z) = (-0.27f + i % 4 * 0.18f, -0.27f + i / 4 * 0.18f);
                Box(pivot, i is 5 or 10 ? face : back, new Vector3(x, height + 0.004f, z), new Vector3(0.13f, 0.006f, 0.15f));
            }
        }

        /// <summary>Chess: the real chess set (Poly Haven) on a walnut table; a board of squares if the model is missing.</summary>
        private void ChessStation(Transform pivot)
        {
            const float height = 0.74f;
            var walnut = Lit(new Color(0.3f, 0.19f, 0.12f), 0.55f);
            Box(pivot, walnut, new Vector3(0f, height - 0.025f, 0f), new Vector3(0.8f, 0.05f, 0.8f));
            foreach (var (x, z) in new[] { (-0.33f, -0.33f), (0.33f, -0.33f), (-0.33f, 0.33f), (0.33f, 0.33f) })
            {
                Box(pivot, walnut, new Vector3(x, height / 2f, z), new Vector3(0.05f, height, 0.05f));
            }
            var set = _spawnModel?.Invoke("ph-chess_set", pivot);
            if (set != null)
            {
                set.transform.localPosition = new Vector3(0f, height, 0f);
                return;
            }
            var light = Lit(new Color(0.9f, 0.86f, 0.76f), 0.5f);
            var dark = Lit(new Color(0.25f, 0.16f, 0.1f), 0.5f);
            for (var i = 0; i < 64; i++)
            {
                Box(pivot, (i % 8 + i / 8) % 2 == 0 ? dark : light, new Vector3(-0.21f + i % 8 * 0.06f, height + 0.005f, -0.21f + i / 8 * 0.06f),
                    new Vector3(0.06f, 0.01f, 0.06f));
            }
        }

        /// <summary>Quiz show: a small stage with two lecterns (glowing buzzers) and a big screen showing the ranking.</summary>
        private void QuizShowStation(Transform pivot)
        {
            const float width = 4f, depth = 2.4f, height = 0.3f;
            var black = Lit(new Color(0.06f, 0.06f, 0.07f), 0.3f);
            var oak = Lit(new Color(0.24f, 0.17f, 0.12f), 0.5f);
            Box(pivot, black, new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth));
            Box(pivot, oak, new Vector3(0f, height, 0f), new Vector3(width + 0.04f, 0.03f, depth + 0.04f));
            Box(pivot, Glow(_theme.WallTrim, 2.2f), new Vector3(0f, 0.05f, -depth / 2f - 0.01f), new Vector3(width, 0.03f, 0.02f));
            // Screen at the back.
            Box(pivot, black, new Vector3(0f, height + 1.25f, depth / 2f - 0.1f), new Vector3(2.8f, 1.7f, 0.08f));
            Box(pivot, Glow(new Color(0.25f, 0.45f, 0.95f), 1.3f), new Vector3(0f, height + 1.25f, depth / 2f - 0.15f), new Vector3(2.6f, 1.5f, 0.01f));
            foreach (var y in new[] { 0.45f, 0.1f, -0.25f })
            {
                Box(pivot, Glow(new Color(1f, 0.95f, 0.85f), 1.5f), new Vector3(-0.2f, height + 1.25f + y, depth / 2f - 0.16f), new Vector3(1.8f, 0.12f, 0.005f));
            }
            // Two lecterns with buzzers.
            foreach (var x in new[] { -1.1f, 1.1f })
            {
                Box(pivot, Lit(new Color(0.93f, 0.93f, 0.91f), 0.4f), new Vector3(x, height + 0.5f, -0.3f), new Vector3(0.6f, 1f, 0.45f));
                Box(pivot, Glow(_theme.WallTrim, 1.8f), new Vector3(x, height + 0.5f, -0.53f), new Vector3(0.6f, 0.08f, 0.01f));
                Cylinder(pivot, Glow(new Color(1f, 0.25f, 0.3f), 2.2f), new Vector3(x, height + 1.02f, -0.3f), new Vector3(0.14f, 0.02f, 0.14f));
            }
        }

        /// <summary>Steel lift portals with door leaves, floor indicator and call buttons on the item's front (-Z) face.</summary>
        private void LiftDoors(Transform face, float width, float depth, float[] doorCentres)
        {
            const float doorWidth = 1.1f, doorHeight = 2.3f;
            var front = -depth / 2f;
            var steel = Lit(new Color(0.74f, 0.76f, 0.8f), 0.85f);
            var darkSteel = Lit(new Color(0.22f, 0.23f, 0.25f), 0.75f);
            var seam = Lit(new Color(0.08f, 0.08f, 0.09f), 0.4f);
            var indicator = Lit(new Color(0.02f, 0.02f, 0.03f), 0.95f);
            var arrow = Glow(_theme.WallTrim, 2.2f);
            var digits = Glow(new Color(1f, 0.95f, 0.85f), 1.6f);

            Box(face, darkSteel, new Vector3(0f, 0.05f, front - 0.01f), new Vector3(width, 0.1f, 0.02f));   // skirting
            foreach (var x in doorCentres)
            {
                // Portal: steel frame around the doors, set slightly in front of the wall.
                Box(face, steel, new Vector3(x - doorWidth / 2f - 0.06f, doorHeight / 2f, front - 0.03f), new Vector3(0.1f, doorHeight + 0.1f, 0.06f));
                Box(face, steel, new Vector3(x + doorWidth / 2f + 0.06f, doorHeight / 2f, front - 0.03f), new Vector3(0.1f, doorHeight + 0.1f, 0.06f));
                Box(face, steel, new Vector3(x, doorHeight + 0.06f, front - 0.03f), new Vector3(doorWidth + 0.22f, 0.12f, 0.06f));
                Box(face, darkSteel, new Vector3(x, 0.01f, front - 0.12f), new Vector3(doorWidth + 0.2f, 0.02f, 0.2f));    // sill

                // Two door leaves with the seam in the middle.
                Box(face, steel, new Vector3(x - doorWidth / 4f, doorHeight / 2f, front - 0.015f), new Vector3(doorWidth / 2f - 0.01f, doorHeight, 0.03f));
                Box(face, steel, new Vector3(x + doorWidth / 4f, doorHeight / 2f, front - 0.015f), new Vector3(doorWidth / 2f - 0.01f, doorHeight, 0.03f));
                Box(face, seam, new Vector3(x, doorHeight / 2f, front - 0.032f), new Vector3(0.012f, doorHeight, 0.005f));

                // Floor indicator above the door: black glass with a glowing arrow and digits.
                Box(face, indicator, new Vector3(x, doorHeight + 0.38f, front - 0.02f), new Vector3(0.62f, 0.22f, 0.02f));
                Box(face, arrow, new Vector3(x - 0.16f, doorHeight + 0.38f, front - 0.032f), new Vector3(0.08f, 0.1f, 0.005f));
                Box(face, digits, new Vector3(x + 0.08f, doorHeight + 0.38f, front - 0.032f), new Vector3(0.26f, 0.09f, 0.005f));
            }

            // Call buttons between the middle doors.
            Box(face, steel, new Vector3(0f, 1.15f, front - 0.02f), new Vector3(0.16f, 0.36f, 0.02f));
            Sphere(face, Glow(_theme.WallTrim, 2.5f), new Vector3(0f, 1.23f, front - 0.035f), new Vector3(0.05f, 0.05f, 0.02f));
            Sphere(face, Glow(new Color(1f, 0.95f, 0.85f), 1.5f), new Vector3(0f, 1.07f, front - 0.035f), new Vector3(0.05f, 0.05f, 0.02f));
            AddLight(face, new Vector3(0f, 2.9f, front - 0.8f), new Color(1f, 0.93f, 0.82f), 1.1f, 3.5f);
        }

        /// <summary>
        /// Row of five access gates (6 m): steel pedestals with glass wings and green status lights.
        /// Visual only – avatars can walk through (the lifts behind must stay reachable).
        /// </summary>
        private void Turnstiles(Transform pivot)
        {
            var steel = Lit(new Color(0.78f, 0.8f, 0.84f), 0.85f);
            var glass = Tinted(_glass, new Color(0.85f, 0.95f, 0.95f, 0.4f));
            var ok = Glow(new Color(0.3f, 1f, 0.55f), 2.5f);
            for (var i = 0; i <= 5; i++)
            {
                var x = -3f + i * 1.2f;
                Box(pivot, steel, new Vector3(x, 0.5f, 0f), new Vector3(0.22f, 1f, 1.1f));
                Box(pivot, Lit(new Color(0.05f, 0.05f, 0.06f), 0.9f), new Vector3(x, 1.005f, 0f), new Vector3(0.2f, 0.01f, 1.06f));
                Box(pivot, ok, new Vector3(x, 1.012f, -0.4f), new Vector3(0.1f, 0.005f, 0.06f));
                if (i < 5)
                {
                    Box(pivot, glass, new Vector3(x + 0.33f, 0.75f, 0f), new Vector3(0.42f, 0.6f, 0.02f));
                    Box(pivot, glass, new Vector3(x + 0.87f, 0.75f, 0f), new Vector3(0.42f, 0.6f, 0.02f));
                }
            }
        }

        /// <summary>
        /// Queue lane (6 × 2.4 m) like at an airport: chrome posts with navy retractable belts forming three
        /// lanes that zigzag towards the gates. Visual only – avatars walk through.
        /// </summary>
        private void QueueLane(Transform pivot)
        {
            var chrome = Lit(new Color(0.82f, 0.84f, 0.88f), 0.9f);
            var belt = Lit(new Color(0.1f, 0.14f, 0.32f), 0.3f);
            var rows = new[] { -1.2f, 0f, 1.2f };
            var columns = new[] { -3f, -1.5f, 0f, 1.5f, 3f };
            foreach (var z in rows)
            {
                foreach (var x in columns)
                {
                    Cylinder(pivot, chrome, new Vector3(x, 0.015f, z), new Vector3(0.32f, 0.015f, 0.32f));
                    Cylinder(pivot, chrome, new Vector3(x, 0.5f, z), new Vector3(0.05f, 0.5f, 0.05f));
                    Cylinder(pivot, chrome, new Vector3(x, 0.98f, z), new Vector3(0.08f, 0.03f, 0.08f));
                }
            }
            // Belts: each row closed except one opening, alternating ends (zigzag).
            for (var r = 0; r < rows.Length; r++)
            {
                var open = r % 2 == 0 ? columns.Length - 2 : 0;
                for (var c = 0; c < columns.Length - 1; c++)
                {
                    if (c == open)
                    {
                        continue;
                    }
                    Box(pivot, belt, new Vector3((columns[c] + columns[c + 1]) / 2f, 0.92f, rows[r]), new Vector3(1.4f, 0.05f, 0.01f));
                }
            }
        }

        /// <summary>Reception desk (4.2 m): stone front with a brass edge, light top, two slim monitors, backlit base.</summary>
        private void Reception(Transform pivot)
        {
            const float length = 4.2f, depth = 0.9f, height = 1.1f;
            var stone = Lit(Color.Lerp(_theme.Wall, Color.black, 0.2f), 0.8f);
            var brass = Lit(_theme.WallTrim, 0.8f);
            Box(pivot, stone, new Vector3(0f, height / 2f, 0f), new Vector3(length, height, depth));
            Box(pivot, Lit(new Color(0.93f, 0.91f, 0.87f), 0.7f), new Vector3(0f, height + 0.025f, -0.05f), new Vector3(length + 0.1f, 0.05f, depth + 0.15f));
            Box(pivot, brass, new Vector3(0f, height - 0.08f, -depth / 2f - 0.005f), new Vector3(length, 0.04f, 0.01f));
            Box(pivot, Glow(new Color(1f, 0.85f, 0.6f), 1.6f), new Vector3(0f, 0.04f, -depth / 2f - 0.02f), new Vector3(length - 0.2f, 0.02f, 0.03f));
            // Staff side: a lower work top with two monitors.
            Box(pivot, Lit(new Color(0.2f, 0.2f, 0.22f)), new Vector3(0f, 0.75f, depth / 2f - 0.15f), new Vector3(length - 0.4f, 0.04f, 0.5f));
            foreach (var x in new[] { -0.9f, 0.9f })
            {
                Box(pivot, Lit(new Color(0.08f, 0.08f, 0.09f)), new Vector3(x, 1.05f, depth / 2f - 0.2f), new Vector3(0.56f, 0.34f, 0.03f));
                Box(pivot, Glow(new Color(0.55f, 0.8f, 1f), 0.8f), new Vector3(x, 1.05f, depth / 2f - 0.22f), new Vector3(0.52f, 0.3f, 0.005f));
                Box(pivot, Lit(new Color(0.6f, 0.62f, 0.66f), 0.8f), new Vector3(x, 0.85f, depth / 2f - 0.18f), new Vector3(0.05f, 0.2f, 0.05f));
            }
        }

        /// <summary>Presentation wall (5.5 m) on a low stage: black frame, glowing screen with a keynote slide.</summary>
        private void ScreenWall(Transform pivot)
        {
            const float width = 5.5f, height = 2.8f;
            var frame = Lit(new Color(0.05f, 0.05f, 0.06f), 0.8f);
            Box(pivot, Lit(new Color(0.18f, 0.18f, 0.2f), 0.5f), new Vector3(0f, 0.12f, 0.6f), new Vector3(width + 1.2f, 0.24f, 2f));   // stage
            Box(pivot, frame, new Vector3(0f, 0.24f + 0.35f + height / 2f, 1.45f), new Vector3(width + 0.2f, height + 0.2f, 0.12f));
            Box(pivot, Glow(new Color(0.1f, 0.16f, 0.3f), 1f), new Vector3(0f, 0.24f + 0.35f + height / 2f, 1.38f), new Vector3(width, height, 0.02f));
            // Slide: title bar, three content blocks, accent line.
            var screenZ = 1.365f;
            var centreY = 0.24f + 0.35f + height / 2f;
            Box(pivot, Glow(new Color(1f, 0.97f, 0.92f), 1.4f), new Vector3(-0.8f, centreY + 0.9f, screenZ), new Vector3(3.2f, 0.28f, 0.01f));
            for (var i = 0; i < 3; i++)
            {
                Box(pivot, Glow(Color.Lerp(_theme.WallTrim, new Color(0.4f, 0.8f, 1f), i / 2f), 1.2f),
                    new Vector3(-1.7f + i * 1.7f, centreY - 0.2f, screenZ), new Vector3(1.4f, 1.1f, 0.01f));
            }
            Box(pivot, Glow(_theme.WallTrim, 2f), new Vector3(0f, centreY - 1.05f, screenZ), new Vector3(width - 0.6f, 0.04f, 0.01f));
            // Lectern.
            Box(pivot, Lit(new Color(0.12f, 0.12f, 0.14f), 0.7f), new Vector3(2.2f, 0.24f + 0.55f, 0f), new Vector3(0.6f, 1.1f, 0.5f));
            Box(pivot, Glow(_theme.WallTrim, 1.5f), new Vector3(2.2f, 0.24f + 0.2f, -0.26f), new Vector3(0.5f, 0.03f, 0.01f));
            AddLight(pivot, new Vector3(0f, 2.6f, -0.8f), new Color(0.7f, 0.8f, 1f), 1.2f, 5f);
        }

        /// <summary>4 m LED line set into the floor.</summary>
        private void LedStrip(Transform pivot) =>
            Box(pivot, Glow(_theme.WallTrim, 1.3f), new Vector3(0f, 0.004f, 0f), new Vector3(4f, 0.008f, 0.05f));

        /// <summary>Wool rug in deep teal with a light border: 3.6 × 2.6 m, or 3 m round.</summary>
        private void Rug(Transform pivot, bool round)
        {
            var border = Lit(new Color(0.86f, 0.83f, 0.77f), 0.05f);
            var wool = Lit(new Color(0.16f, 0.32f, 0.34f), 0.05f);
            if (round)
            {
                Cylinder(pivot, border, new Vector3(0f, 0.005f, 0f), new Vector3(3f, 0.005f, 3f));
                Cylinder(pivot, wool, new Vector3(0f, 0.008f, 0f), new Vector3(2.7f, 0.005f, 2.7f));
                return;
            }
            Box(pivot, border, new Vector3(0f, 0.005f, 0f), new Vector3(3.6f, 0.01f, 2.6f));
            Box(pivot, wool, new Vector3(0f, 0.008f, 0f), new Vector3(3.3f, 0.01f, 2.3f));
        }

        private static Material Tinted(Material source, Color color)
        {
            var material = new Material(source);
            material.SetColor("_BaseColor", color);
            return material;
        }

        private Material HologramMaterial(Color color, float alpha)
        {
            var material = Tinted(_water, new Color(color.r, color.g, color.b, alpha));
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.7f);   // stays cyan instead of blooming to white
            return material;
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
            DestroyNow(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Destroy that also works in the editor (the build catalog measures the items outside play mode).</summary>
        private static void DestroyNow(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
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

    /// <summary>Turns an object around its Y axis (holograms).</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 20f;

        private void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }

    /// <summary>Moves an object up and down above its start height (hologram scan rings).</summary>
    public sealed class Bobbing : MonoBehaviour
    {
        public float amplitude = 0.3f;
        public float speed = 0.8f;
        private Vector3 _start;
        private float _seed;

        private void Awake()
        {
            _start = transform.localPosition;
            _seed = UnityEngine.Random.value * 10f;
        }

        private void Update() =>
            transform.localPosition = _start + Vector3.up * (amplitude * (0.5f + 0.5f * Mathf.Sin((Time.time + _seed) * speed)));
    }

    /// <summary>Equalizer bar that pumps to an imaginary beat (DJ booth); grows from its bottom edge.</summary>
    public sealed class EqualizerBar : MonoBehaviour
    {
        public float phase;
        private Vector3 _scale;
        private Vector3 _position;

        private void Awake()
        {
            _scale = transform.localScale;
            _position = transform.localPosition;
        }

        private void Update()
        {
            var level = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(Time.time * 2.1f + phase)) * Mathf.PerlinNoise(phase, Time.time * 1.5f);
            transform.localScale = new Vector3(_scale.x, _scale.y * level, _scale.z);
            transform.localPosition = _position + Vector3.down * (_scale.y * (1f - level) / 2f);
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
