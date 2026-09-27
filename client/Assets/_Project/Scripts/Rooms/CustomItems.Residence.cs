using System.Collections.Generic;
using Reconnect.Contracts.Rooms;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// "Reconnect Residence" (<see cref="FurnitureFamilies"/>): what a luxury apartment needs – interior walls with art,
    /// a marble kitchen, fireplace and TV wall, beds and a walk-in wardrobe, bath and spa (sauna, hot tub), a home gym,
    /// an executive office, a gaming room and an 18+ bedroom. Same conventions as the modern family: metres, front −Z,
    /// colour zones via <see cref="CustomItems.Z"/>, places to sit marked with <see cref="SeatPoint"/>.
    /// </summary>
    public sealed partial class CustomItems
    {
        /// <summary>Height of interior walls (the storey's glass is 3.4 m).</summary>
        public const float WallHeight = 2.8f;

        /// <summary>Height a wall is cut down to while it would hide the room behind it (see RoomView).</summary>
        public const float WallCutHeight = 0.3f;

        /// <summary>Names of the two versions of a wall: full height and cut down (the room view shows one of them).</summary>
        public const string WallFullName = "Wall Full";
        public const string WallCutName = "Wall Cut";

        private readonly Dictionary<Color, Material> _glassTints = new();
        private readonly Dictionary<int, Material> _artworks = new();

        private bool TryBuildResidence(Transform model, string itemId, string[] parts)
        {
            string Part(int i) => i < parts.Length ? parts[i] : "";
            int Number(int i) => int.TryParse(Part(i), out var n) ? n : 0;
            switch (parts[0])
            {
                case "wall": Wall(model, Part(1), Part(1) == "art" ? 2 : Number(2), Part(1) == "art" ? Number(2) : 0); return true;
                case "kitchenisland": KitchenIsland(model, Number(1) / 100f); return true;
                case "kitchenwall": KitchenWall(model, Number(1) / 100f); return true;
                case "kitchenhood": KitchenHood(model); return true;
                case "winefridge": WineFridge(model); return true;
                case "espresso": Espresso(model); return true;
                case "fruitbowl": FruitBowl(model); return true;
                case "fireplace": Fireplace(model); return true;
                case "tvwall": TvWall(model); return true;
                case "piano": Piano(model); return true;
                case "sculpture": Sculpture(model, Number(1)); return true;
                case "bed": Bed(model, Part(1)); return true;
                case "nightstand": Nightstand(model); return true;
                case "wardrobe": Wardrobe(model, Number(1) / 100f); return true;
                case "vanity": Vanity(model); return true;
                case "chaise": Chaise(model); return true;
                case "mirror": StandingMirror(model); return true;
                case "bathtub": Bathtub(model); return true;
                case "shower": Shower(model); return true;
                case "washstand": Washstand(model); return true;
                case "toilet": Toilet(model); return true;
                case "towelrack": TowelRack(model); return true;
                case "sauna": Sauna(model); return true;
                case "hottub": HotTub(model); return true;
                case "towels": Towels(model); return true;
                case "treadmill": Treadmill(model); return true;
                case "spinbike": SpinBike(model); return true;
                case "powerrack": PowerRack(model); return true;
                case "weightbench": WeightBench(model); return true;
                case "dumbbells": Dumbbells(model); return true;
                case "boxingbag": BoxingBag(model); return true;
                case "gymmirror": GymMirror(model); return true;
                case "yogamat": Soft(model, Z(0), new Vector3(0f, 0.003f, 0f), new Vector3(0.62f, 0.006f, 1.8f), 0.0025f); return true;
                case "executivedesk": ExecutiveDesk(model); return true;
                case "executivechair": ExecutiveChair(model); return true;
                case "gamingdesk": GamingDesk(model); return true;
                case "gamingchair": GamingChair(model); return true;
                case "drinkfridge": DrinkFridge(model); return true;
                case "ledlamp": LedColumn(model); return true;
                case "arcade": Arcade(model); return true;
                case "adult":
                    switch (Part(1))
                    {
                        case "cross": AdultCross(model); return true;
                        case "bench": AdultBench(model); return true;
                        case "rack": AdultRack(model); return true;
                        case "cage": AdultCage(model); return true;
                        default: return false;
                    }
                default: return false;
            }
        }

        // ---------- Helpers ----------

        /// <summary>Colour of paint zone <paramref name="zone"/> as light (LED strips, RGB).</summary>
        private Material Led(int zone, float intensity = 2.5f) => Glow(Z(zone).GetColor("_BaseColor"), intensity);

        private Material Glassy(Color tint)
        {
            if (!_glassTints.TryGetValue(tint, out var material))
            {
                material = Tinted(_glass, tint);
                _glassTints[tint] = material;
            }
            return material;
        }

        private Material MirrorSurface => Metal(new Color(0.86f, 0.88f, 0.9f), 0.97f);
        private Material WhiteCeramic => Lit(new Color(0.96f, 0.96f, 0.95f), 0.85f);
        private Material Chrome => Metal(new Color(0.9f, 0.9f, 0.92f), 0.9f);
        private Material RubberBlack => Lit(new Color(0.08f, 0.08f, 0.085f), 0.15f);
        private Material ScreenOff => Lit(new Color(0.02f, 0.02f, 0.03f), 0.92f);

        /// <summary>A ring (torus) lying flat: rope coils, D-rings, fan rings.</summary>
        private static Mesh Ring(float radius, float thickness, int segments = 20)
        {
            var profile = new List<Vector2>();
            for (var i = 0; i <= 8; i++)
            {
                var a = Mathf.PI * 2f * i / 8f;
                profile.Add(new Vector2(radius + thickness * Mathf.Cos(a), thickness + thickness * Mathf.Sin(a)));
            }
            return SoftShapes.Lathe(profile, segments);
        }

        // ---------- Walls & art ----------

        /// <summary>
        /// Interior wall: fills its 25 cm cell over the whole length, so walls at right angles meet in a shared corner cell
        /// without a gap. Built twice – full height and cut down – the room view shows the cut version while the wall would
        /// hide what is behind it (like cutaway walls). Art walls carry a painting and a picture light on the front (−Z).
        /// </summary>
        private void Wall(Transform pivot, string style, int length, int artwork)
        {
            float l = Mathf.Max(1, length);
            var full = new GameObject(WallFullName).transform;
            full.SetParent(pivot, false);
            var cut = new GameObject(WallCutName).transform;
            cut.SetParent(pivot, false);
            cut.gameObject.SetActive(false);
            WallBody(full, style, l, WallHeight);
            WallBody(cut, style, l, WallCutHeight);
            if (style == "art")
            {
                // The painting and its light stay inside the cell: the wall behind is thinner (see WallBody).
                Painting(full, artwork, new Vector3(0f, 1.55f, ArtWallFront - 0.02f), 1.3f, 0.95f, Z(1));
            }
        }

        /// <summary>Front face of an art wall (its body is thinner so painting and light fit in the 25 cm cell).</summary>
        private const float ArtWallFront = -0.02f;

        private void WallBody(Transform parent, string style, float length, float height)
        {
            const float thickness = BuildGrid.CellSize;
            if (style == "art")
            {
                var back = thickness / 2f;
                Soft(parent, Z(0), new Vector3(0f, height / 2f, (ArtWallFront + back) / 2f), new Vector3(length, height, back - ArtWallFront), 0.01f);
                return;
            }
            var skirting = Lit(new Color(0.12f, 0.12f, 0.13f), 0.4f);
            switch (style)
            {
                case "slats":
                    Soft(parent, Z(1), new Vector3(0f, height / 2f, 0f), new Vector3(length, height, thickness - 0.06f), 0.005f);
                    var count = Mathf.RoundToInt(length / 0.065f);
                    for (var i = 0; i < count; i++)
                    {
                        var x = -length / 2f + (i + 0.5f) * length / count;
                        foreach (var side in new[] { -1f, 1f })
                        {
                            Box(parent, Z(0), new Vector3(x, height / 2f, side * (thickness / 2f - 0.015f)), new Vector3(0.04f, height, 0.03f));
                        }
                    }
                    break;
                case "marble":
                    Soft(parent, Z(0), new Vector3(0f, height / 2f, 0f), new Vector3(length, height, thickness), 0.004f);
                    break;
                default:   // plaster (also behind art)
                    Soft(parent, Z(0), new Vector3(0f, height / 2f, 0f), new Vector3(length, height, thickness), 0.01f);
                    if (height > 1f)
                    {
                        Soft(parent, skirting, new Vector3(0f, 0.04f, 0f), new Vector3(length - 0.002f, 0.08f, thickness + 0.012f), 0.003f);
                    }
                    break;
            }
        }

        /// <summary>A framed modern painting facing −Z at <paramref name="centre"/> with a brass picture light above.</summary>
        private void Painting(Transform parent, int artwork, Vector3 centre, float width, float height, Material frame)
        {
            Soft(parent, frame, centre, new Vector3(width + 0.08f, height + 0.08f, 0.04f), 0.006f);
            Box(parent, Lit(new Color(0.96f, 0.95f, 0.92f), 0.2f), centre + new Vector3(0f, 0f, -0.021f), new Vector3(width + 0.02f, height + 0.02f, 0.002f));
            Box(parent, Artwork(artwork), centre + new Vector3(0f, 0f, -0.023f), new Vector3(width - 0.08f, height - 0.08f, 0.002f));
            var light = centre + new Vector3(0f, height / 2f + 0.14f, -0.06f);
            Rod(parent, frame, light + new Vector3(0f, 0.02f, 0.06f), light, 0.008f);
            Soft(parent, frame, light, new Vector3(width * 0.55f, 0.035f, 0.05f), 0.012f);
            Box(parent, Glow(new Color(1f, 0.9f, 0.75f), 1.6f), light + new Vector3(0f, -0.019f, 0f), new Vector3(width * 0.5f, 0.004f, 0.03f));
        }

        /// <summary>Modern abstract paintings, drawn once per room (see <see cref="ArtCanvas"/>).</summary>
        private Material Artwork(int index)
        {
            if (!_artworks.TryGetValue(index, out var material))
            {
                material = new Material(_litBase);
                material.SetTexture("_BaseMap", ArtCanvas.Paint(index));
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0.15f);
                _artworks[index] = material;
            }
            return material;
        }

        private void Sculpture(Transform pivot, int style)
        {
            Soft(pivot, Z(1), new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.9f, 0.45f), 0.01f);   // plinth
            switch (style)
            {
                case 1:   // arch
                    var arch = Turned(pivot, Z(0), new Vector3(0f, 0.9f, 0f), Ring(0.28f, 0.045f, 32));
                    arch.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    arch.transform.localPosition = new Vector3(0f, 1.22f, 0f);
                    break;
                case 2:   // stacked spheres
                    for (var i = 0; i < 3; i++)
                    {
                        var r = 0.16f - i * 0.035f;
                        Sphere(pivot, Z(0), new Vector3(0.02f * (i - 1), 0.9f + r + i * 0.21f, 0f), Vector3.one * r * 2f);
                    }
                    break;
                default:  // twisted wave
                    for (var i = 0; i < 12; i++)
                    {
                        Soft(pivot, Z(0), new Vector3(0f, 0.93f + i * 0.05f, 0f), new Vector3(0.34f - i * 0.012f, 0.045f, 0.1f), 0.01f, turnY: i * 15f);
                    }
                    break;
            }
        }

        // ---------- Lift core ----------

        /// <summary>
        /// The building core dressed for a residence (when the core has a colour): wood slats on the long sides between the
        /// lift portals with warm light lines, white marble on the ends with a large painting each, and a travertine rim
        /// around a softly glowing light field on top – what one sees from above instead of a dark block.
        /// </summary>
        private void CoreResidence(Transform pivot, Material wood)
        {
            const float width = CoreWidth, depth = CoreDepth, height = 3.4f;
            var doorCentres = new[] { -2.6f, -1.1f, 1.1f, 2.6f };
            Box(pivot, Lit(new Color(0.1f, 0.09f, 0.08f), 0.3f), new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth));
            var warm = Glow(new Color(1f, 0.86f, 0.66f), 1.8f);
            foreach (var side in new[] { 0f, 180f })
            {
                var face = new GameObject("Side").transform;
                face.SetParent(pivot, false);
                face.localRotation = Quaternion.Euler(0f, side, 0f);
                var front = -depth / 2f;
                for (var x = -width / 2f + 0.06f; x < width / 2f - 0.04f; x += 0.09f)
                {
                    var nearDoor = false;
                    foreach (var door in doorCentres)
                    {
                        nearDoor |= Mathf.Abs(x - door) < 0.74f;
                    }
                    if (!nearDoor)
                    {
                        Box(face, wood, new Vector3(x, 0.12f + (height - 0.24f) / 2f, front - 0.02f), new Vector3(0.05f, height - 0.24f, 0.04f));
                    }
                }
                Box(face, warm, new Vector3(0f, 0.1f, front - 0.012f), new Vector3(width, 0.012f, 0.02f));
                Box(face, warm, new Vector3(0f, height - 0.1f, front - 0.012f), new Vector3(width, 0.012f, 0.02f));
                LiftDoors(face, width, depth, doorCentres);
            }
            var marble = Surface(SurfaceMaterials.Marble, Color.white);
            var brass = Metal(new Color(0.8f, 0.62f, 0.34f), 0.72f);
            foreach (var (side, artwork) in new[] { (90f, 3), (270f, 7) })
            {
                var end = new GameObject("End").transform;
                end.SetParent(pivot, false);
                end.localRotation = Quaternion.Euler(0f, side, 0f);
                // In the end's frame the face lies at z = −width/2 (the core's length runs along its local X).
                Soft(end, marble, new Vector3(0f, height / 2f, -width / 2f - 0.02f), new Vector3(depth + 0.08f, height, 0.04f), 0.004f);
                Painting(end, artwork, new Vector3(0f, 1.65f, -width / 2f - 0.06f), 1.9f, 1.3f, brass);
                foreach (var x in new[] { -1.9f, 1.9f })
                {
                    Soft(end, brass, new Vector3(x, 1.9f, -width / 2f - 0.07f), new Vector3(0.1f, 0.5f, 0.06f), 0.02f);
                    Box(end, warm, new Vector3(x, 1.9f, -width / 2f - 0.101f), new Vector3(0.05f, 0.44f, 0.004f));
                }
            }
            // Top: travertine rim, glowing field with bronze lines.
            var travertine = Surface(SurfaceMaterials.Travertine, Color.white);
            foreach (var z in new[] { -1f, 1f })
            {
                Box(pivot, travertine, new Vector3(0f, height + 0.03f, z * (depth / 2f - 0.2f)), new Vector3(width + 0.06f, 0.06f, 0.46f));
            }
            foreach (var x in new[] { -1f, 1f })
            {
                Box(pivot, travertine, new Vector3(x * (width / 2f - 0.2f), height + 0.03f, 0f), new Vector3(0.46f, 0.06f, depth + 0.06f));
            }
            Box(pivot, Glow(new Color(0.9f, 0.8f, 0.66f), 0.22f), new Vector3(0f, height + 0.01f, 0f), new Vector3(width - 0.8f, 0.02f, depth - 0.8f));
            var bronze = Metal(new Color(0.46f, 0.31f, 0.2f), 0.6f);
            for (var x = -width / 2f + 1.4f; x < width / 2f - 1f; x += 1.2f)
            {
                Box(pivot, bronze, new Vector3(x, height + 0.025f, 0f), new Vector3(0.03f, 0.01f, depth - 0.8f));
            }
            Box(pivot, bronze, new Vector3(0f, height + 0.025f, 0f), new Vector3(width - 0.8f, 0.01f, 0.03f));
        }

        // ---------- Kitchen ----------

        /// <summary>Marble island with waterfall ends, walnut fronts, a sink and an induction hob; overhang for stools at −Z.</summary>
        private void KitchenIsland(Transform pivot, float length)
        {
            const float height = 0.92f, depth = 1.15f, top = 0.06f, overhang = 0.32f;
            var baseDepth = depth - overhang;
            var baseZ = overhang / 2f;
            Soft(pivot, Z(0), new Vector3(0f, height - top / 2f, 0f), new Vector3(length, top, depth), 0.006f);
            foreach (var side in new[] { -1f, 1f })
            {
                Soft(pivot, Z(0), new Vector3(side * (length / 2f - top / 2f), (height - top) / 2f, 0f), new Vector3(top, height - top, depth), 0.006f);
            }
            var inner = length - 2f * top;
            Soft(pivot, Ink, new Vector3(0f, 0.05f, baseZ + 0.04f), new Vector3(inner - 0.02f, 0.1f, baseDepth - 0.1f), 0.004f);   // toe kick
            var doors = Mathf.Max(2, Mathf.RoundToInt(inner / 0.6f));
            for (var i = 0; i < doors; i++)
            {
                var x = -inner / 2f + (i + 0.5f) * inner / doors;
                Soft(pivot, Z(1), new Vector3(x, 0.1f + (height - top - 0.1f) / 2f, baseZ + 0.005f), new Vector3(inner / doors - 0.006f, height - top - 0.1f, baseDepth - 0.01f), 0.004f);
            }
            // Stool side: a recessed back panel under the overhang.
            Soft(pivot, Z(1), new Vector3(0f, (height - top) / 2f, -depth / 2f + overhang + 0.01f), new Vector3(inner, height - top, 0.02f), 0.004f);
            // Sink with tap, hob.
            var sinkX = -length / 4f;
            Soft(pivot, Ink, new Vector3(sinkX, height + 0.001f, 0.12f), new Vector3(0.62f, 0.004f, 0.42f), 0.002f);
            var tapBase = new Vector3(sinkX, height, 0.4f);
            Rod(pivot, Z(2), tapBase, tapBase + Vector3.up * 0.34f, 0.014f);
            Rod(pivot, Z(2), tapBase + Vector3.up * 0.34f, tapBase + new Vector3(0f, 0.34f, -0.2f), 0.012f);
            Rod(pivot, Z(2), tapBase + new Vector3(0f, 0.34f, -0.2f), tapBase + new Vector3(0f, 0.26f, -0.22f), 0.011f);
            var hobX = length / 4f;
            Box(pivot, Lit(new Color(0.02f, 0.02f, 0.025f), 0.95f), new Vector3(hobX, height + 0.003f, 0.12f), new Vector3(0.78f, 0.006f, 0.5f));
            foreach (var (dx, dz) in new[] { (-0.2f, -0.1f), (0.2f, -0.1f), (-0.2f, 0.12f), (0.2f, 0.12f) })
            {
                Turned(pivot, Lit(new Color(0.18f, 0.18f, 0.2f), 0.6f), new Vector3(hobX + dx, height + 0.006f, 0.12f + dz), Ring(0.08f, 0.003f, 24));
            }
        }

        /// <summary>Floor-to-ceiling kitchen wall: fronts, a column with two ovens, a fridge column and a lit marble niche.</summary>
        private void KitchenWall(Transform pivot, float length)
        {
            const float height = 2.6f, depth = 0.65f;
            var columns = Mathf.RoundToInt(length / 0.6f);
            var width = length / columns;
            var ovenColumn = columns / 2 - 1;
            var niche = (columns / 2) + 1;
            var blackGlass = Lit(new Color(0.03f, 0.03f, 0.035f), 0.95f);
            Soft(pivot, Ink, new Vector3(0f, 0.05f, 0.03f), new Vector3(length - 0.02f, 0.1f, depth - 0.06f), 0.004f);
            for (var c = 0; c < columns; c++)
            {
                var x = -length / 2f + (c + 0.5f) * width;
                if (c == niche && columns >= 4)
                {
                    // Open niche at counter height: marble back, LED strip, coffee machine.
                    Soft(pivot, Z(0), new Vector3(x, 0.1f + 0.4f, 0f), new Vector3(width - 0.006f, 0.8f, depth), 0.004f);
                    Soft(pivot, Z(1), new Vector3(x, 0.92f, 0.02f), new Vector3(width - 0.006f, 0.04f, depth - 0.04f), 0.004f);
                    Soft(pivot, Z(1), new Vector3(x, 1.2f, depth / 2f - 0.02f), new Vector3(width - 0.006f, 0.6f, 0.04f), 0.004f);
                    Box(pivot, Glow(new Color(1f, 0.88f, 0.7f), 2f), new Vector3(x, 1.49f, 0f), new Vector3(width - 0.06f, 0.006f, 0.02f));
                    Soft(pivot, Z(0), new Vector3(x, 1.5f + (height - 1.5f) / 2f, 0f), new Vector3(width - 0.006f, height - 1.5f, depth), 0.004f);
                    Espresso(pivot, new Vector3(x, 0.94f, 0.08f));
                    continue;
                }
                Soft(pivot, Z(0), new Vector3(x, 0.1f + (height - 0.1f) / 2f, 0f), new Vector3(width - 0.006f, height - 0.1f, depth), 0.004f);
                if (c == ovenColumn)
                {
                    foreach (var y in new[] { 1.0f, 1.62f })
                    {
                        Box(pivot, blackGlass, new Vector3(x, y, -depth / 2f - 0.003f), new Vector3(width - 0.06f, 0.56f, 0.006f));
                        Rod(pivot, Z(2), new Vector3(x - width / 2f + 0.08f, y + 0.22f, -depth / 2f - 0.03f), new Vector3(x + width / 2f - 0.08f, y + 0.22f, -depth / 2f - 0.03f), 0.008f);
                    }
                    continue;
                }
                // Tall handle bar on every other front.
                Rod(pivot, Z(2), new Vector3(x + width / 2f - 0.06f, 0.9f, -depth / 2f - 0.025f), new Vector3(x + width / 2f - 0.06f, 1.4f, -depth / 2f - 0.025f), 0.007f);
            }
        }

        private void KitchenHood(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.15f, 0f), new Vector3(1.1f, 0.3f, 0.55f), 0.02f);
            Box(pivot, Glow(new Color(1f, 0.92f, 0.8f), 1.2f), new Vector3(0f, -0.001f, 0f), new Vector3(0.9f, 0.004f, 0.08f));
            foreach (var x in new[] { -0.45f, 0.45f })
            {
                Rod(pivot, Z(0), new Vector3(x, 0.3f, 0f), new Vector3(x, 1.2f, 0f), 0.006f);
            }
        }

        private void WineFridge(Transform pivot)
        {
            const float width = 0.62f, height = 2.1f, depth = 0.62f;
            Soft(pivot, Z(0), new Vector3(0f, height / 2f, 0.02f), new Vector3(width, height, depth - 0.04f), 0.01f);
            Box(pivot, Glow(new Color(1f, 0.85f, 0.65f), 0.6f), new Vector3(0f, height / 2f, depth / 2f - 0.07f), new Vector3(width - 0.08f, height - 0.2f, 0.01f));
            var bottles = new[] { Lit(new Color(0.12f, 0.2f, 0.12f), 0.85f), Lit(new Color(0.3f, 0.05f, 0.08f), 0.85f), Lit(new Color(0.75f, 0.68f, 0.45f), 0.85f) };
            for (var row = 0; row < 9; row++)
            {
                for (var i = 0; i < 4; i++)
                {
                    var bottle = Rod(pivot, bottles[(row + i) % 3], new Vector3(-0.21f + i * 0.14f, 0.2f + row * 0.2f, 0.18f),
                        new Vector3(-0.21f + i * 0.14f, 0.2f + row * 0.2f, -0.18f), 0.035f);
                    bottle.name = "Bottle";
                }
            }
            Box(pivot, Glassy(new Color(0.2f, 0.22f, 0.25f, 0.35f)), new Vector3(0f, height / 2f, -depth / 2f + 0.01f), new Vector3(width - 0.05f, height - 0.08f, 0.01f));
            Rod(pivot, Chrome, new Vector3(width / 2f - 0.06f, 0.8f, -depth / 2f - 0.03f), new Vector3(width / 2f - 0.06f, 1.4f, -depth / 2f - 0.03f), 0.008f);
        }

        private void Espresso(Transform pivot) => Espresso(pivot, Vector3.zero);

        private void Espresso(Transform pivot, Vector3 at)
        {
            Soft(pivot, Z(0), at + new Vector3(0f, 0.18f, 0f), new Vector3(0.3f, 0.36f, 0.34f), 0.02f);
            Soft(pivot, Ink, at + new Vector3(0f, 0.24f, -0.172f), new Vector3(0.22f, 0.1f, 0.01f), 0.004f);
            Rod(pivot, Ink, at + new Vector3(0f, 0.2f, -0.17f), at + new Vector3(0f, 0.2f, -0.28f), 0.012f);
            Turned(pivot, WhiteCeramic, at + new Vector3(0f, 0.03f, -0.1f), SoftShapes.RoundedCylinder(0.035f, 0.06f, 0.005f, 16));
        }

        private void FruitBowl(Transform pivot)
        {
            Turned(pivot, Z(0), Vector3.zero, SoftShapes.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.08f, 0f), new Vector2(0.18f, 0.08f), new Vector2(0.17f, 0.09f), new Vector2(0f, 0.03f) }, 28));
            var fruit = new[] { Lit(new Color(0.95f, 0.55f, 0.1f), 0.5f), Lit(new Color(0.55f, 0.75f, 0.2f), 0.5f), Lit(new Color(0.75f, 0.1f, 0.12f), 0.6f) };
            var spots = new[] { (-0.06f, -0.02f), (0.05f, 0.04f), (0.02f, -0.07f), (-0.03f, 0.07f), (0.08f, -0.03f) };
            for (var i = 0; i < spots.Length; i++)
            {
                Sphere(pivot, fruit[i % 3], new Vector3(spots[i].Item1, 0.08f, spots[i].Item2), Vector3.one * 0.075f);
            }
        }

        // ---------- Living ----------

        /// <summary>Stone-clad fireplace wall with a long firebox (glowing embers and flames) facing −Z.</summary>
        private void Fireplace(Transform pivot)
        {
            const float width = 3.2f, height = 2.8f, depth = 0.5f;
            Soft(pivot, Z(0), new Vector3(0f, 1.0f + (height - 1.0f) / 2f, 0f), new Vector3(width, height - 1.0f, depth), 0.006f);
            Soft(pivot, Z(0), new Vector3(0f, 0.2f, 0f), new Vector3(width, 0.4f, depth), 0.006f);
            foreach (var side in new[] { -1f, 1f })
            {
                Soft(pivot, Z(0), new Vector3(side * (width / 2f - 0.35f), 0.7f, 0f), new Vector3(0.7f, 0.6f, depth), 0.006f);
            }
            Soft(pivot, Ink, new Vector3(0f, 0.7f, 0.06f), new Vector3(width - 1.4f, 0.6f, depth - 0.12f), 0.004f);
            Soft(pivot, Z(1), new Vector3(0f, 0.7f, -depth / 2f + 0.01f), new Vector3(width - 1.36f, 0.64f, 0.02f), 0.004f);
            Box(pivot, Glassy(new Color(0.9f, 0.95f, 1f, 0.12f)), new Vector3(0f, 0.7f, -depth / 2f + 0.03f), new Vector3(width - 1.44f, 0.56f, 0.006f));
            // Embers and flames.
            Box(pivot, Glow(new Color(1f, 0.35f, 0.05f), 3f), new Vector3(0f, 0.43f, 0.05f), new Vector3(width - 1.6f, 0.03f, 0.18f));
            var flame = Glow(new Color(1f, 0.55f, 0.12f), 3.2f);
            var core = Glow(new Color(1f, 0.85f, 0.4f), 3.5f);
            for (var i = 0; i < 14; i++)
            {
                var x = -0.85f + i * 0.13f;
                var h = 0.12f + 0.14f * Mathf.Abs(Mathf.Sin(i * 1.7f));
                Soft(pivot, flame, new Vector3(x, 0.45f + h / 2f, 0.05f), new Vector3(0.06f, h, 0.05f), 0.02f);
                Soft(pivot, core, new Vector3(x + 0.02f, 0.45f + h * 0.3f, 0.05f), new Vector3(0.03f, h * 0.55f, 0.03f), 0.012f);
            }
        }

        /// <summary>Stone TV wall with an 85-inch screen and a floating walnut lowboard with an LED strip below.</summary>
        private void TvWall(Transform pivot)
        {
            const float width = 3.6f, height = 2.6f;
            Soft(pivot, Z(1), new Vector3(0f, height / 2f, 0.2f), new Vector3(width, height, 0.1f), 0.005f);
            Soft(pivot, Z(0), new Vector3(0f, 0.38f, 0f), new Vector3(2.8f, 0.36f, 0.42f), 0.01f);
            Box(pivot, Glow(new Color(1f, 0.88f, 0.7f), 1.5f), new Vector3(0f, 0.19f, 0.05f), new Vector3(2.7f, 0.006f, 0.3f));
            for (var i = 0; i < 4; i++)
            {
                Box(pivot, Ink, new Vector3(-1.05f + i * 0.7f, 0.38f, -0.211f), new Vector3(0.004f, 0.3f, 0.002f));
            }
            Soft(pivot, Ink, new Vector3(0f, 1.45f, 0.13f), new Vector3(1.9f, 1.08f, 0.04f), 0.006f);
            Box(pivot, Glow(new Color(0.12f, 0.2f, 0.35f), 0.6f), new Vector3(0f, 1.45f, 0.108f), new Vector3(1.86f, 1.04f, 0.002f));
            Box(pivot, Glow(new Color(0.9f, 0.55f, 0.3f), 0.8f), new Vector3(-0.3f, 1.3f, 0.106f), new Vector3(0.9f, 0.35f, 0.002f));
        }

        /// <summary>Black lacquered grand piano (outline extruded), lid open, with a bench.</summary>
        private void Piano(Transform pivot)
        {
            var outline = new List<Vector2>
            {
                new(-0.74f, -0.9f), new(0.74f, -0.9f), new(0.74f, 0.1f), new(0.6f, 0.55f), new(0.3f, 0.8f), new(-0.05f, 0.95f),
                new(-0.45f, 0.95f), new(-0.7f, 0.75f), new(-0.74f, 0.4f),
            };
            var lacquer = Z(0);
            var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(pivot, false);
            outline = PolygonMesh.CounterClockwise(outline);
            body.GetComponent<MeshFilter>().sharedMesh = PolygonMesh.Slab(outline, 1.0f, 0.32f, 1f);
            body.GetComponent<MeshRenderer>().sharedMaterial = lacquer;
            var lid = new GameObject("Lid", typeof(MeshFilter), typeof(MeshRenderer));
            lid.transform.SetParent(pivot, false);
            lid.transform.localPosition = new Vector3(0.74f, 1.0f, 0f);
            lid.transform.localRotation = Quaternion.Euler(0f, 0f, 38f);
            lid.GetComponent<MeshFilter>().sharedMesh = PolygonMesh.Slab(outline.ConvertAll(p => new Vector2(p.x - 0.74f, p.y)), 0.02f, 0.02f, 1f);
            lid.GetComponent<MeshRenderer>().sharedMaterial = lacquer;
            Rod(pivot, lacquer, new Vector3(0.72f, 1.0f, 0.1f), new Vector3(0.1f, 1.48f, 0.1f), 0.01f);
            Soft(pivot, WhiteCeramic, new Vector3(0f, 0.76f, -0.97f), new Vector3(1.3f, 0.03f, 0.16f), 0.003f);
            for (var i = 0; i < 36; i++)
            {
                if (i % 7 is 2 or 6)
                {
                    continue;
                }
                Box(pivot, Ink, new Vector3(-0.62f + i * 0.036f, 0.785f, -0.93f), new Vector3(0.016f, 0.02f, 0.09f));
            }
            foreach (var (x, z) in new[] { (-0.62f, -0.75f), (0.62f, -0.75f), (-0.2f, 0.75f) })
            {
                Turned(pivot, lacquer, new Vector3(x, 0f, z), TaperedLeg(0.68f, 0.05f, 0.035f));
            }
            Soft(pivot, lacquer, new Vector3(0f, 0.47f, -1.45f), new Vector3(0.8f, 0.06f, 0.36f), 0.01f);
            Soft(pivot, Lit(new Color(0.1f, 0.1f, 0.11f), 0.4f), new Vector3(0f, 0.51f, -1.45f), new Vector3(0.76f, 0.04f, 0.32f), 0.015f);
            foreach (var (x, z) in Corners(0.34f, 0.13f))
            {
                Rod(pivot, lacquer, new Vector3(x, 0f, -1.45f + z), new Vector3(x, 0.45f, -1.45f + z), 0.02f);
            }
        }

        // ---------- Sleeping ----------

        /// <summary>
        /// Double bed (1.8 × 2.1 m mattress), headboard at +Z, foot at −Z. "panel": tall upholstered channel headboard on a
        /// walnut plinth; "canopy": four slim posts and a frame; "dungeon" (18+): black leather, crimson satin, cuffs on the
        /// posts and chains from the frame. People sit on the foot end.
        /// </summary>
        private void Bed(Transform pivot, string style)
        {
            const float width = 1.9f, length = 2.15f, mattressTop = 0.58f;
            var dungeon = style == "dungeon";
            var headboard = Z(0);
            var sheets = Z(1);
            var frame = Z(2);
            // Plinth / frame and mattress.
            Soft(pivot, frame, new Vector3(0f, 0.14f, 0f), new Vector3(width + 0.08f, 0.28f, length), 0.02f);
            Soft(pivot, dungeon ? headboard : Lit(new Color(0.93f, 0.92f, 0.9f), 0.2f), new Vector3(0f, 0.43f, -0.02f), new Vector3(width, 0.3f, length - 0.1f), 0.06f);
            // Duvet over the lower two thirds, folded edge, throw.
            Soft(pivot, sheets, new Vector3(0f, mattressTop + 0.02f, -0.3f), new Vector3(width + 0.04f, 0.1f, 1.45f), 0.05f);
            Soft(pivot, sheets, new Vector3(0f, mattressTop + 0.07f, 0.38f), new Vector3(width, 0.06f, 0.18f), 0.03f);
            Soft(pivot, dungeon ? Lit(new Color(0.08f, 0.08f, 0.09f), 0.55f) : Lit(new Color(0.3f, 0.3f, 0.32f), 0.1f),
                new Vector3(0f, mattressTop + 0.075f, -0.75f), new Vector3(width + 0.06f, 0.03f, 0.5f), 0.012f);
            // Pillows.
            foreach (var x in new[] { -0.45f, 0.45f })
            {
                Soft(pivot, sheets, new Vector3(x, mattressTop + 0.1f, 0.78f), new Vector3(0.8f, 0.18f, 0.42f), 0.08f, tiltX: -12f);
                Soft(pivot, dungeon ? headboard : Z(0), new Vector3(x * 0.8f, mattressTop + 0.12f, 0.6f), new Vector3(0.5f, 0.14f, 0.3f), 0.06f, tiltX: -18f);
            }
            // Headboard.
            var headHeight = dungeon ? 1.5f : style == "canopy" ? 1.2f : 1.45f;
            var channels = 9;
            for (var i = 0; i < channels; i++)
            {
                var x = -(width + 0.2f) / 2f + (i + 0.5f) * (width + 0.2f) / channels;
                Soft(pivot, headboard, new Vector3(x, headHeight / 2f, length / 2f + 0.06f), new Vector3((width + 0.2f) / channels - 0.01f, headHeight, 0.14f), 0.05f);
            }
            if (style is "canopy" or "dungeon")
            {
                const float posts = 2.3f;
                var thick = dungeon ? 0.07f : 0.04f;
                foreach (var (x, z) in Corners(width / 2f + 0.08f, length / 2f + 0.08f))
                {
                    Soft(pivot, frame, new Vector3(x, posts / 2f, z), new Vector3(thick, posts, thick), 0.005f);
                }
                foreach (var z in new[] { -1f, 1f })
                {
                    Soft(pivot, frame, new Vector3(0f, posts, z * (length / 2f + 0.08f)), new Vector3(width + 0.16f + thick, thick, thick), 0.005f);
                }
                foreach (var x in new[] { -1f, 1f })
                {
                    Soft(pivot, frame, new Vector3(x * (width / 2f + 0.08f), posts, 0f), new Vector3(thick, thick, length + 0.16f + thick), 0.005f);
                }
            }
            if (dungeon)
            {
                // Leather cuffs with chrome rings on every post, short chains from the frame.
                foreach (var (x, z) in Corners(width / 2f + 0.08f, length / 2f + 0.08f))
                {
                    Soft(pivot, headboard, new Vector3(x, 0.72f, z), new Vector3(0.11f, 0.07f, 0.11f), 0.02f);
                    var ring = Turned(pivot, Chrome, new Vector3(x - Mathf.Sign(x) * 0.07f, 0.72f, z), Ring(0.028f, 0.006f, 12));
                    ring.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    for (var link = 0; link < 5; link++)
                    {
                        var chainLink = Turned(pivot, Chrome, new Vector3(x - Mathf.Sign(x) * 0.12f, 2.25f - link * 0.07f, z - Mathf.Sign(z) * 0.12f), Ring(0.022f, 0.005f, 10));
                        chainLink.transform.localRotation = Quaternion.Euler(link % 2 == 0 ? 90f : 0f, link % 2 == 0 ? 0f : 90f, 0f);
                    }
                }
                // Padded bench at the foot.
                Soft(pivot, headboard, new Vector3(0f, 0.42f, -length / 2f - 0.35f), new Vector3(1.5f, 0.14f, 0.42f), 0.05f);
                foreach (var (x, z) in Corners(0.68f, 0.16f))
                {
                    Rod(pivot, frame, new Vector3(x, 0f, -length / 2f - 0.35f + z), new Vector3(x, 0.36f, -length / 2f - 0.35f + z), 0.015f);
                }
            }
            foreach (var x in new[] { -0.45f, 0.45f })
            {
                SeatPoint(pivot, new Vector3(x, mattressTop, -length / 2f + 0.3f));
            }
        }

        private void Nightstand(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.3f, 0f), new Vector3(0.55f, 0.36f, 0.45f), 0.012f);
            Box(pivot, Ink, new Vector3(0f, 0.34f, -0.226f), new Vector3(0.5f, 0.004f, 0.002f));
            Rod(pivot, Z(1), new Vector3(-0.08f, 0.26f, -0.235f), new Vector3(0.08f, 0.26f, -0.235f), 0.006f);
            foreach (var (x, z) in Corners(0.22f, 0.17f))
            {
                Rod(pivot, Z(1), new Vector3(x, 0f, z), new Vector3(x, 0.12f, z), 0.012f);
            }
        }

        /// <summary>Walk-in style wardrobe: bronze frame, smoked glass doors, lit inside, clothes on a rail, shoes, boxes.</summary>
        private void Wardrobe(Transform pivot, float length)
        {
            const float height = 2.4f, depth = 0.62f;
            Soft(pivot, Z(1), new Vector3(0f, height / 2f, depth / 2f - 0.02f), new Vector3(length, height, 0.04f), 0.004f);
            Soft(pivot, Z(1), new Vector3(0f, height - 0.02f, 0f), new Vector3(length, 0.04f, depth), 0.004f);
            Soft(pivot, Z(1), new Vector3(0f, 0.02f, 0f), new Vector3(length, 0.04f, depth), 0.004f);
            var bays = Mathf.RoundToInt(length / 1f);
            var bay = length / bays;
            var clothes = new[]
            {
                Lit(new Color(0.12f, 0.13f, 0.16f), 0.2f), Lit(new Color(0.85f, 0.83f, 0.78f), 0.2f), Lit(new Color(0.45f, 0.35f, 0.28f), 0.2f),
                Lit(new Color(0.3f, 0.34f, 0.42f), 0.2f), Lit(new Color(0.55f, 0.1f, 0.14f), 0.2f),
            };
            for (var b = 0; b <= bays; b++)
            {
                var x = -length / 2f + b * bay;
                Soft(pivot, Z(1), new Vector3(Mathf.Clamp(x, -length / 2f + 0.015f, length / 2f - 0.015f), height / 2f, 0f), new Vector3(0.03f, height, depth), 0.004f);
                Soft(pivot, Z(0), new Vector3(Mathf.Clamp(x, -length / 2f + 0.015f, length / 2f - 0.015f), height / 2f, -depth / 2f), new Vector3(0.035f, height, 0.035f), 0.004f);
            }
            for (var b = 0; b < bays; b++)
            {
                var cx = -length / 2f + (b + 0.5f) * bay;
                Box(pivot, Glow(new Color(1f, 0.9f, 0.75f), 1.8f), new Vector3(cx, height - 0.05f, 0f), new Vector3(bay - 0.08f, 0.006f, 0.04f));
                if (b % 2 == 0)
                {
                    Rod(pivot, Z(0), new Vector3(cx - bay / 2f + 0.03f, 1.95f, 0.02f), new Vector3(cx + bay / 2f - 0.03f, 1.95f, 0.02f), 0.01f);
                    for (var i = 0; i < 8; i++)
                    {
                        var x = cx - bay / 2f + 0.1f + i * (bay - 0.2f) / 7f;
                        var long_ = i % 3 == 0;
                        Soft(pivot, clothes[(i + b) % clothes.Length], new Vector3(x, 1.95f - (long_ ? 0.55f : 0.4f), 0.02f), new Vector3(0.05f, long_ ? 1.1f : 0.8f, 0.46f), 0.02f);
                    }
                    for (var i = 0; i < 4; i++)
                    {
                        Soft(pivot, clothes[i % 2 == 0 ? 0 : 2], new Vector3(cx - 0.3f + i * 0.2f, 0.1f, -0.05f), new Vector3(0.1f, 0.1f, 0.26f), 0.03f);
                    }
                }
                else
                {
                    for (var shelf = 0; shelf < 5; shelf++)
                    {
                        var y = 0.4f + shelf * 0.4f;
                        Soft(pivot, Z(1), new Vector3(cx, y, 0.02f), new Vector3(bay - 0.04f, 0.025f, depth - 0.06f), 0.004f);
                        for (var i = 0; i < 3; i++)
                        {
                            Soft(pivot, clothes[(shelf + i) % clothes.Length], new Vector3(cx - bay / 3f + i * bay / 3f, y + 0.07f, 0.02f), new Vector3(bay / 3f - 0.06f, 0.12f, 0.35f), 0.02f);
                        }
                    }
                }
                Box(pivot, Glassy(new Color(0.25f, 0.22f, 0.2f, 0.3f)), new Vector3(cx, height / 2f, -depth / 2f), new Vector3(bay - 0.04f, height - 0.08f, 0.008f));
            }
        }

        private void Vanity(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.73f, 0.05f), new Vector3(1.2f, 0.05f, 0.45f), 0.01f);
            Soft(pivot, Z(0), new Vector3(0f, 0.62f, 0.07f), new Vector3(1.1f, 0.16f, 0.4f), 0.01f);
            foreach (var (x, z) in Corners(0.55f, 0.18f))
            {
                Rod(pivot, Z(2), new Vector3(x, 0f, 0.05f + z), new Vector3(x, 0.54f, 0.05f + z), 0.012f);
            }
            var mirror = Turned(pivot, Z(2), new Vector3(0f, 1.35f, 0.25f), SoftShapes.RoundedCylinder(0.42f, 0.03f, 0.01f, 40));
            mirror.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var glass = Turned(pivot, MirrorSurface, new Vector3(0f, 1.35f, 0.225f), SoftShapes.RoundedCylinder(0.39f, 0.006f, 0.002f, 40));
            glass.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var halo = Turned(pivot, Glow(new Color(1f, 0.9f, 0.78f), 1.5f), new Vector3(0f, 1.35f, 0.27f), Ring(0.44f, 0.008f, 40));
            halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Turned(pivot, Z(1), new Vector3(0f, 0f, -0.45f), SoftShapes.RoundedCylinder(0.22f, 0.46f, 0.06f));
        }

        private void Chaise(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.3f, -0.15f), new Vector3(0.7f, 0.2f, 1.4f), 0.07f);
            Soft(pivot, Z(0), new Vector3(0f, 0.52f, 0.62f), new Vector3(0.7f, 0.2f, 0.62f), 0.08f, tiltX: -38f);
            Soft(pivot, Z(0), new Vector3(0f, 0.38f, -0.8f), new Vector3(0.7f, 0.14f, 0.3f), 0.06f, tiltX: 12f);
            foreach (var (x, z) in Corners(0.3f, 0.75f))
            {
                Rod(pivot, Z(1), new Vector3(x, 0f, z), new Vector3(x * 0.95f, 0.2f, z * 0.95f), 0.012f);
            }
            SeatPoint(pivot, new Vector3(0f, 0.4f, 0.15f));
        }

        private void StandingMirror(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.95f, 0.1f), new Vector3(0.75f, 1.9f, 0.04f), 0.01f, tiltX: -6f);
            Soft(pivot, MirrorSurface, new Vector3(0f, 0.95f, 0.078f), new Vector3(0.68f, 1.82f, 0.006f), 0.002f, tiltX: -6f);
        }

        // ---------- Bath & spa ----------

        private void Bathtub(Transform pivot)
        {
            var shell = SoftShapes.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.3f, 0f), new Vector2(0.42f, 0.2f), new Vector2(0.45f, 0.56f), new Vector2(0.43f, 0.6f),
                new Vector2(0.39f, 0.58f), new Vector2(0.36f, 0.2f), new Vector2(0.28f, 0.08f), new Vector2(0f, 0.08f),
            }, 40);
            Turned(pivot, Z(0), Vector3.zero, shell, new Vector3(1.9f, 1f, 1f));
            Turned(pivot, Glassy(new Color(0.55f, 0.8f, 0.85f, 0.55f)), new Vector3(0f, 0.46f, 0f), SoftShapes.RoundedCylinder(0.39f, 0.01f, 0.004f, 40), new Vector3(1.9f, 1f, 1f));
            var tap = new Vector3(-0.95f, 0f, 0f);
            Rod(pivot, Z(1), tap, tap + Vector3.up * 0.95f, 0.02f);
            Rod(pivot, Z(1), tap + Vector3.up * 0.95f, tap + new Vector3(0.2f, 0.95f, 0f), 0.016f);
            Rod(pivot, Z(1), tap + new Vector3(0.2f, 0.95f, 0f), tap + new Vector3(0.22f, 0.85f, 0f), 0.014f);
        }

        /// <summary>Walk-in rain shower: stone back wall and tray, a glass screen on the front half, rain head and a niche.</summary>
        private void Shower(Transform pivot)
        {
            const float width = 1.6f, depth = 1.0f, height = 2.3f;
            Soft(pivot, Z(1), new Vector3(0f, 0.015f, 0f), new Vector3(width, 0.03f, depth), 0.005f);
            Soft(pivot, Z(1), new Vector3(0f, height / 2f, depth / 2f - 0.05f), new Vector3(width, height, 0.1f), 0.005f);
            Box(pivot, Ink, new Vector3(0f, 0.032f, depth / 2f - 0.16f), new Vector3(width - 0.2f, 0.002f, 0.04f));
            Soft(pivot, Ink, new Vector3(0.4f, 1.2f, depth / 2f - 0.09f), new Vector3(0.5f, 0.3f, 0.02f), 0.004f);
            Box(pivot, Glow(new Color(1f, 0.9f, 0.78f), 1.5f), new Vector3(0.4f, 1.34f, depth / 2f - 0.1f), new Vector3(0.46f, 0.004f, 0.02f));
            Box(pivot, Glassy(new Color(0.85f, 0.93f, 0.95f, 0.2f)), new Vector3(-width / 4f, height / 2f, -depth / 2f + 0.02f), new Vector3(width / 2f, height - 0.04f, 0.01f));
            Soft(pivot, Z(0), new Vector3(-width / 4f, height / 2f, -depth / 2f + 0.02f), new Vector3(0.02f, height, 0.03f), 0.004f);
            Soft(pivot, Z(0), new Vector3(-width / 4f, 2.0f, 0f), new Vector3(0.02f, 0.02f, depth - 0.1f), 0.004f);
            Rod(pivot, Z(0), new Vector3(0f, 2.2f, depth / 2f - 0.1f), new Vector3(0f, 2.2f, 0.05f), 0.012f);
            Soft(pivot, Z(0), new Vector3(0f, 2.18f, 0.05f), new Vector3(0.3f, 0.02f, 0.3f), 0.004f);
            Rod(pivot, Z(0), new Vector3(0.6f, 1.0f, depth / 2f - 0.1f), new Vector3(0.6f, 1.0f, depth / 2f - 0.16f), 0.025f);
        }

        /// <summary>Floating double washstand: marble top, two vessel basins, black taps, backlit mirror on a marble wall.</summary>
        private void Washstand(Transform pivot)
        {
            const float width = 1.8f;
            Soft(pivot, Z(0), new Vector3(0f, 1.2f, 0.22f), new Vector3(width + 0.2f, 2.4f, 0.06f), 0.004f);
            Soft(pivot, Z(1), new Vector3(0f, 0.6f, 0f), new Vector3(width, 0.36f, 0.45f), 0.01f);
            Box(pivot, Ink, new Vector3(0f, 0.6f, -0.226f), new Vector3(0.004f, 0.3f, 0.002f));
            Soft(pivot, Z(0), new Vector3(0f, 0.8f, -0.02f), new Vector3(width + 0.04f, 0.04f, 0.5f), 0.004f);
            foreach (var x in new[] { -0.45f, 0.45f })
            {
                Turned(pivot, WhiteCeramic, new Vector3(x, 0.82f, -0.04f), SoftShapes.Lathe(new[]
                {
                    new Vector2(0f, 0f), new Vector2(0.1f, 0f), new Vector2(0.2f, 0.06f), new Vector2(0.22f, 0.13f), new Vector2(0.2f, 0.14f), new Vector2(0f, 0.03f),
                }, 32));
                var tap = new Vector3(x, 0.82f, 0.14f);
                Rod(pivot, Z(2), tap, tap + Vector3.up * 0.3f, 0.012f);
                Rod(pivot, Z(2), tap + Vector3.up * 0.3f, tap + new Vector3(0f, 0.3f, -0.12f), 0.01f);
                Soft(pivot, Z(2), new Vector3(x, 1.6f, 0.18f), new Vector3(0.7f, 0.9f, 0.02f), 0.01f);
                Soft(pivot, MirrorSurface, new Vector3(x, 1.6f, 0.168f), new Vector3(0.66f, 0.86f, 0.004f), 0.002f);
                Box(pivot, Glow(new Color(1f, 0.92f, 0.82f), 1.5f), new Vector3(x, 1.6f, 0.19f), new Vector3(0.76f, 0.96f, 0.002f));
            }
        }

        private void Toilet(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.6f, 0.22f), new Vector3(0.6f, 1.2f, 0.12f), 0.01f);
            Soft(pivot, Ink, new Vector3(0f, 1.0f, 0.157f), new Vector3(0.24f, 0.16f, 0.006f), 0.004f);
            Soft(pivot, WhiteCeramic, new Vector3(0f, 0.3f, -0.08f), new Vector3(0.37f, 0.3f, 0.55f), 0.14f);
            Soft(pivot, WhiteCeramic, new Vector3(0f, 0.46f, -0.08f), new Vector3(0.38f, 0.02f, 0.54f), 0.01f);
        }

        private void TowelRack(Transform pivot)
        {
            foreach (var x in new[] { -0.22f, 0.22f })
            {
                Rod(pivot, Z(0), new Vector3(x, 0f, -0.2f), new Vector3(x, 1.7f, 0.1f), 0.012f);
            }
            for (var i = 0; i < 5; i++)
            {
                var t = (i + 0.5f) / 5f;
                var y = t * 1.7f;
                var z = -0.2f + t * 0.3f;
                Rod(pivot, Z(0), new Vector3(-0.22f, y, z), new Vector3(0.22f, y, z), 0.008f);
                if (i % 2 == 0)
                {
                    Soft(pivot, Z(1), new Vector3(0f, y - 0.12f, z - 0.01f), new Vector3(0.4f, 0.26f, 0.05f), 0.02f, tiltX: 10f);
                }
            }
        }

        /// <summary>
        /// Sauna for three: wooden walls and benches, glass front with door, stove with stones, warm light under the benches.
        /// Open at the top (seen from above like a cutaway), so the people inside show.
        /// </summary>
        private void Sauna(Transform pivot)
        {
            const float width = 2.6f, depth = 2.0f, height = 2.2f;
            var wood = Z(0);
            Soft(pivot, wood, new Vector3(0f, 0.03f, 0f), new Vector3(width, 0.06f, depth), 0.005f);
            Soft(pivot, wood, new Vector3(0f, height / 2f, depth / 2f - 0.04f), new Vector3(width, height, 0.08f), 0.005f);
            foreach (var side in new[] { -1f, 1f })
            {
                Soft(pivot, wood, new Vector3(side * (width / 2f - 0.04f), height / 2f, 0f), new Vector3(0.08f, height, depth), 0.005f);
            }
            for (var i = 0; i < 22; i++)
            {
                Box(pivot, Lit(new Color(0.35f, 0.25f, 0.16f), 0.3f), new Vector3(-width / 2f + 0.1f + i * 0.115f, height / 2f, depth / 2f - 0.081f), new Vector3(0.004f, height - 0.1f, 0.002f));
            }
            // Front: glass with a door frame.
            Box(pivot, Glassy(new Color(0.75f, 0.6f, 0.45f, 0.25f)), new Vector3(0f, height / 2f, -depth / 2f + 0.02f), new Vector3(width - 0.1f, height - 0.06f, 0.012f));
            foreach (var x in new[] { -0.9f, -0.2f })
            {
                Soft(pivot, wood, new Vector3(x, height / 2f, -depth / 2f + 0.02f), new Vector3(0.06f, height, 0.05f), 0.005f);
            }
            Soft(pivot, wood, new Vector3(0f, height - 0.03f, -depth / 2f + 0.02f), new Vector3(width, 0.06f, 0.05f), 0.005f);
            Rod(pivot, wood, new Vector3(-0.28f, 0.9f, -depth / 2f - 0.02f), new Vector3(-0.28f, 1.3f, -depth / 2f - 0.02f), 0.018f);
            // Two tiers of benches along the back.
            Soft(pivot, wood, new Vector3(0f, 0.45f, depth / 2f - 0.55f), new Vector3(width - 0.2f, 0.05f, 0.5f), 0.008f);
            Soft(pivot, wood, new Vector3(0f, 0.9f, depth / 2f - 0.3f), new Vector3(width - 0.2f, 0.05f, 0.45f), 0.008f);
            Soft(pivot, wood, new Vector3(0f, 0.68f, depth / 2f - 0.54f), new Vector3(width - 0.2f, 0.44f, 0.04f), 0.006f);
            Box(pivot, Glow(new Color(1f, 0.65f, 0.3f), 2.2f), new Vector3(0f, 0.41f, depth / 2f - 0.62f), new Vector3(width - 0.3f, 0.01f, 0.02f));
            Box(pivot, Glow(new Color(1f, 0.65f, 0.3f), 2.2f), new Vector3(0f, 0.86f, depth / 2f - 0.34f), new Vector3(width - 0.3f, 0.01f, 0.02f));
            // Stove with stones in the front corner.
            var stove = new Vector3(width / 2f - 0.4f, 0f, -depth / 2f + 0.4f);
            Soft(pivot, Ink, stove + new Vector3(0f, 0.35f, 0f), new Vector3(0.45f, 0.7f, 0.4f), 0.02f);
            var stone = Lit(new Color(0.35f, 0.34f, 0.33f), 0.2f);
            for (var i = 0; i < 9; i++)
            {
                Sphere(pivot, stone, stove + new Vector3(-0.14f + (i % 3) * 0.14f, 0.74f + (i / 3) * 0.03f, -0.12f + (i / 3) * 0.12f), new Vector3(0.12f, 0.08f, 0.11f));
            }
            Box(pivot, Glow(new Color(1f, 0.45f, 0.1f), 2.5f), stove + new Vector3(0f, 0.3f, -0.201f), new Vector3(0.3f, 0.12f, 0.002f));
            for (var i = 0; i < 3; i++)
            {
                SeatPoint(pivot, new Vector3(-0.8f + i * 0.8f, 0.475f, depth / 2f - 0.55f));
            }
        }

        /// <summary>Square hot tub: stone surround, lit turquoise water with bubbles, four places around the inside.</summary>
        private void HotTub(Transform pivot)
        {
            const float size = 2.3f, height = 0.85f;
            Soft(pivot, Z(0), new Vector3(0f, height / 2f, 0f), new Vector3(size, height, size), 0.06f);
            Soft(pivot, Ink, new Vector3(0f, height - 0.01f, 0f), new Vector3(size - 0.3f, 0.01f, size - 0.3f), 0.05f);
            Soft(pivot, HologramMaterial(new Color(0.2f, 0.75f, 0.8f), 0.75f), new Vector3(0f, height + 0.004f, 0f), new Vector3(size - 0.3f, 0.01f, size - 0.3f), 0.004f);
            var foam = Lit(new Color(0.95f, 0.98f, 1f), 0.6f);
            for (var i = 0; i < 18; i++)
            {
                var a = i * 2.4f;
                var r = 0.25f + (i % 5) * 0.14f;
                Sphere(pivot, foam, new Vector3(Mathf.Cos(a) * r, height + 0.01f, Mathf.Sin(a) * r), new Vector3(0.05f, 0.015f, 0.05f));
            }
            foreach (var (x, z) in Corners(0.6f, 0.6f))
            {
                var towardsCentre = Quaternion.LookRotation(new Vector3(-x, 0f, -z)).eulerAngles.y;
                SeatPoint(pivot, new Vector3(x, 0.45f, z), towardsCentre);
            }
        }

        private void Towels(Transform pivot)
        {
            for (var i = 0; i < 3; i++)
            {
                Soft(pivot, Z(0), new Vector3(0f, 0.035f + i * 0.065f, 0f), new Vector3(0.36f - i * 0.02f, 0.06f, 0.26f), 0.025f);
            }
        }

        // ---------- Fitness ----------

        private void Treadmill(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.12f, 0.1f), new Vector3(0.85f, 0.18f, 1.9f), 0.03f);
            Soft(pivot, RubberBlack, new Vector3(0f, 0.215f, 0.15f), new Vector3(0.55f, 0.01f, 1.6f), 0.004f);
            foreach (var x in new[] { -0.36f, 0.36f })
            {
                Soft(pivot, Z(0), new Vector3(x, 0.7f, -0.72f), new Vector3(0.07f, 1.1f, 0.1f), 0.02f, tiltX: 12f);
                Rod(pivot, Z(0), new Vector3(x, 1.05f, -0.8f), new Vector3(x, 1.05f, -0.4f), 0.016f);
            }
            Soft(pivot, Z(0), new Vector3(0f, 1.3f, -0.84f), new Vector3(0.8f, 0.1f, 0.3f), 0.03f, tiltX: -25f);
            Box(pivot, Glow(new Color(0.2f, 0.55f, 1f), 1.6f), new Vector3(0f, 1.34f, -0.8f), new Vector3(0.36f, 0.002f, 0.16f)).transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        }

        private void SpinBike(Transform pivot)
        {
            var frame = Z(0);
            Soft(pivot, frame, new Vector3(0f, 0.04f, 0f), new Vector3(0.5f, 0.06f, 0.08f), 0.02f);
            Soft(pivot, frame, new Vector3(0f, 0.04f, -0.9f), new Vector3(0.5f, 0.06f, 0.08f), 0.02f);
            Rod(pivot, frame, new Vector3(0f, 0.07f, 0f), new Vector3(0f, 0.95f, -0.1f), 0.03f);
            Rod(pivot, frame, new Vector3(0f, 0.07f, -0.9f), new Vector3(0f, 1.05f, -0.7f), 0.03f);
            Rod(pivot, frame, new Vector3(0f, 0.45f, -0.05f), new Vector3(0f, 0.55f, -0.75f), 0.025f);
            var wheel = Turned(pivot, Chrome, new Vector3(0.03f, 0.45f, -0.72f), SoftShapes.RoundedCylinder(0.25f, 0.05f, 0.01f, 32));
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Soft(pivot, Z(1), new Vector3(0f, 0.98f, -0.08f), new Vector3(0.16f, 0.06f, 0.28f), 0.025f);
            Rod(pivot, frame, new Vector3(-0.22f, 1.08f, -0.7f), new Vector3(0.22f, 1.08f, -0.7f), 0.016f);
            SeatPoint(pivot, new Vector3(0f, 1.0f, -0.08f));
        }

        /// <summary>Power rack: four uprights, barbell with plates on the hooks, plates stored on pegs.</summary>
        private void PowerRack(Transform pivot)
        {
            var frame = Z(0);
            const float width = 1.25f, depth = 1.3f, height = 2.3f;
            foreach (var (x, z) in Corners(width / 2f, depth / 2f))
            {
                Soft(pivot, frame, new Vector3(x, height / 2f, z), new Vector3(0.07f, height, 0.07f), 0.005f);
            }
            foreach (var z in new[] { -1f, 1f })
            {
                Soft(pivot, frame, new Vector3(0f, height, z * depth / 2f), new Vector3(width + 0.07f, 0.07f, 0.07f), 0.005f);
                Soft(pivot, frame, new Vector3(0f, 0.035f, z * depth / 2f), new Vector3(width + 0.07f, 0.07f, 0.07f), 0.005f);
            }
            foreach (var x in new[] { -1f, 1f })
            {
                Soft(pivot, frame, new Vector3(x * width / 2f, height, 0f), new Vector3(0.07f, 0.07f, depth), 0.005f);
            }
            Rod(pivot, Chrome, new Vector3(-1.1f, 1.4f, -depth / 2f + 0.05f), new Vector3(1.1f, 1.4f, -depth / 2f + 0.05f), 0.014f);
            var plate = RubberBlack;
            foreach (var side in new[] { -1f, 1f })
            {
                for (var i = 0; i < 2; i++)
                {
                    var disc = Turned(pivot, plate, new Vector3(side * (0.78f + i * 0.06f), 1.4f, -depth / 2f + 0.05f), SoftShapes.RoundedCylinder(0.225f - i * 0.04f, 0.05f, 0.008f, 32));
                    disc.transform.localRotation = Quaternion.Euler(0f, 0f, side * 90f);
                }
                for (var i = 0; i < 3; i++)
                {
                    var stored = Turned(pivot, plate, new Vector3(side * (width / 2f + 0.06f + i * 0.05f), 0.35f, depth / 2f), SoftShapes.RoundedCylinder(0.225f - i * 0.03f, 0.045f, 0.008f, 32));
                    stored.transform.localRotation = Quaternion.Euler(0f, 0f, side * 90f);
                }
            }
        }

        private void WeightBench(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.44f, 0f), new Vector3(0.3f, 0.08f, 1.2f), 0.03f);
            foreach (var z in new[] { -0.45f, 0.45f })
            {
                Soft(pivot, Z(1), new Vector3(0f, 0.2f, z), new Vector3(0.08f, 0.4f, 0.08f), 0.01f);
                Soft(pivot, Z(1), new Vector3(0f, 0.03f, z), new Vector3(0.45f, 0.06f, 0.08f), 0.01f);
            }
            SeatPoint(pivot, new Vector3(0f, 0.48f, 0f), 270f);
        }

        private void Dumbbells(Transform pivot)
        {
            const float width = 1.5f;
            foreach (var x in new[] { -0.7f, 0.7f })
            {
                Soft(pivot, Z(0), new Vector3(x, 0.4f, 0f), new Vector3(0.06f, 0.8f, 0.5f), 0.01f, tiltX: 0f);
            }
            var heads = RubberBlack;
            foreach (var (y, z) in new[] { (0.42f, 0.1f), (0.78f, -0.08f) })
            {
                Soft(pivot, Z(0), new Vector3(0f, y - 0.05f, z), new Vector3(width - 0.1f, 0.03f, 0.3f), 0.006f);
                for (var i = 0; i < 5; i++)
                {
                    var x = -0.55f + i * 0.275f;
                    var size = 0.07f + i * 0.012f;
                    Rod(pivot, Chrome, new Vector3(x - 0.1f, y + size / 2f, z), new Vector3(x + 0.1f, y + size / 2f, z), 0.014f);
                    foreach (var end in new[] { -1f, 1f })
                    {
                        Soft(pivot, heads, new Vector3(x + end * 0.11f, y + size / 2f, z), new Vector3(0.06f, size, size), 0.01f);
                    }
                }
            }
        }

        private void BoxingBag(Transform pivot)
        {
            Soft(pivot, Ink, new Vector3(0.45f, 0.03f, 0f), new Vector3(0.6f, 0.06f, 0.6f), 0.02f);
            Rod(pivot, Ink, new Vector3(0.45f, 0.06f, 0f), new Vector3(0.45f, 2.4f, 0f), 0.035f);
            Rod(pivot, Ink, new Vector3(0.45f, 2.4f, 0f), new Vector3(-0.05f, 2.4f, 0f), 0.03f);
            Rod(pivot, Z(1), new Vector3(0f, 2.38f, 0f), new Vector3(0f, 1.82f, 0f), 0.008f);
            Turned(pivot, Z(0), new Vector3(0f, 0.7f, 0f), SoftShapes.RoundedCylinder(0.19f, 1.12f, 0.06f, 28));
        }

        private void GymMirror(Transform pivot)
        {
            for (var i = 0; i < 3; i++)
            {
                var x = -1f + i;
                Soft(pivot, Z(0), new Vector3(x, 1.2f, 0.02f), new Vector3(1f, 2.2f, 0.04f), 0.006f);
                Soft(pivot, MirrorSurface, new Vector3(x, 1.2f, -0.002f), new Vector3(0.96f, 2.16f, 0.006f), 0.002f);
            }
        }

        // ---------- Office & gaming ----------

        private void ExecutiveDesk(Transform pivot)
        {
            const float width = 2.2f, depth = 0.95f, height = 0.76f;
            Soft(pivot, Z(0), new Vector3(0f, height - 0.025f, 0f), new Vector3(width, 0.05f, depth), 0.008f);
            Soft(pivot, Z(1), new Vector3(0f, height + 0.001f, 0.08f), new Vector3(0.9f, 0.002f, 0.5f), 0.001f);
            Soft(pivot, Z(0), new Vector3(0.62f, (height - 0.05f) / 2f, 0.05f), new Vector3(0.7f, height - 0.05f, depth - 0.2f), 0.008f);
            for (var i = 0; i < 3; i++)
            {
                Rod(pivot, Z(2), new Vector3(0.52f, 0.15f + i * 0.2f, 0.4f), new Vector3(0.72f, 0.15f + i * 0.2f, 0.4f), 0.006f);
            }
            foreach (var z in new[] { -0.35f, 0.35f })
            {
                Rod(pivot, Z(2), new Vector3(-0.95f, 0f, z), new Vector3(-0.95f, height - 0.05f, z), 0.02f);
            }
            Rod(pivot, Z(2), new Vector3(-0.95f, 0.02f, -0.35f), new Vector3(-0.95f, 0.02f, 0.35f), 0.02f);
        }

        private void ExecutiveChair(Transform pivot)
        {
            var leather = Z(0);
            var metal = Z(1);
            for (var i = 0; i < 5; i++)
            {
                var a = i * 72f * Mathf.Deg2Rad;
                Rod(pivot, metal, new Vector3(0f, 0.1f, 0f), new Vector3(Mathf.Sin(a) * 0.33f, 0.05f, Mathf.Cos(a) * 0.33f), 0.02f);
            }
            Rod(pivot, metal, new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.42f, 0f), 0.03f);
            Soft(pivot, leather, new Vector3(0f, 0.48f, -0.02f), new Vector3(0.58f, 0.12f, 0.56f), 0.05f);
            for (var i = 0; i < 4; i++)
            {
                Soft(pivot, leather, new Vector3(0f, 0.7f + i * 0.19f, 0.28f + i * 0.02f), new Vector3(0.56f - i * 0.03f, 0.18f, 0.1f), 0.045f, tiltX: -8f);
            }
            foreach (var x in new[] { -0.31f, 0.31f })
            {
                Soft(pivot, leather, new Vector3(x, 0.66f, 0f), new Vector3(0.07f, 0.06f, 0.42f), 0.025f);
                Rod(pivot, metal, new Vector3(x, 0.48f, 0.05f), new Vector3(x, 0.63f, 0.05f), 0.012f);
            }
            SeatPoint(pivot, new Vector3(0f, 0.54f, -0.04f));
        }

        /// <summary>
        /// Big gaming desk (2.4 m) with an LED strip, three monitors in a curve plus one above, a gaming PC with glass side
        /// and RGB fans, keyboard, mouse, desk mat, headset. One sits on the +Z side looking −Z at the screens.
        /// </summary>
        private void GamingDesk(Transform pivot)
        {
            const float width = 2.4f, depth = 0.9f, height = 0.75f;
            var led = Led(1, 3f);
            Soft(pivot, Z(0), new Vector3(0f, height - 0.02f, 0f), new Vector3(width, 0.04f, depth), 0.008f);
            Box(pivot, led, new Vector3(0f, height - 0.043f, depth / 2f - 0.02f), new Vector3(width - 0.04f, 0.004f, 0.01f));
            Box(pivot, led, new Vector3(0f, height - 0.043f, -depth / 2f + 0.02f), new Vector3(width - 0.04f, 0.004f, 0.01f));
            foreach (var x in new[] { -1.1f, 1.1f })
            {
                Soft(pivot, Ink, new Vector3(x, (height - 0.04f) / 2f, 0f), new Vector3(0.08f, height - 0.04f, depth - 0.1f), 0.01f);
            }
            Soft(pivot, Lit(new Color(0.05f, 0.05f, 0.06f), 0.1f), new Vector3(-0.15f, height + 0.002f, 0.15f), new Vector3(1.4f, 0.004f, 0.45f), 0.002f);
            // Monitors: three 27" in a curve, a fourth above the middle.
            var screens = new[] { Glow(new Color(0.15f, 0.3f, 0.6f), 0.9f), Glow(new Color(0.5f, 0.2f, 0.55f), 0.9f), Glow(new Color(0.15f, 0.5f, 0.35f), 0.9f), Glow(new Color(0.6f, 0.3f, 0.15f), 0.9f) };
            var mounts = new[] { (-0.63f, 1.08f, -0.18f, 28f), (0f, 1.08f, -0.25f, 0f), (0.63f, 1.08f, -0.18f, -28f), (0f, 1.47f, -0.27f, 0f) };
            for (var i = 0; i < mounts.Length; i++)
            {
                var (x, y, z, turn) = mounts[i];
                var monitor = new GameObject("Monitor").transform;
                monitor.SetParent(pivot, false);
                monitor.localPosition = new Vector3(x, y, z);
                monitor.localRotation = Quaternion.Euler(i == 3 ? 8f : 0f, turn, 0f);
                Soft(monitor, Ink, Vector3.zero, new Vector3(0.62f, 0.37f, 0.03f), 0.006f);
                Box(monitor, screens[i], new Vector3(0f, 0.005f, 0.016f), new Vector3(0.6f, 0.34f, 0.002f));
            }
            Rod(pivot, Ink, new Vector3(0f, height, -0.34f), new Vector3(0f, 1.47f, -0.34f), 0.02f);
            Soft(pivot, Ink, new Vector3(0f, 1.08f, -0.3f), new Vector3(1.3f, 0.04f, 0.04f), 0.01f);
            // Gaming PC: black case, glass side, RGB fans and strip.
            var pc = new Vector3(1.0f, height, -0.12f);
            Soft(pivot, Ink, pc + new Vector3(0f, 0.26f, 0f), new Vector3(0.24f, 0.52f, 0.5f), 0.01f);
            Box(pivot, Glassy(new Color(0.2f, 0.2f, 0.25f, 0.35f)), pc + new Vector3(-0.122f, 0.26f, 0f), new Vector3(0.004f, 0.48f, 0.46f));
            for (var i = 0; i < 3; i++)
            {
                var fan = Turned(pivot, led, pc + new Vector3(-0.1f, 0.12f + i * 0.14f, 0.2f), Ring(0.055f, 0.006f, 20));
                fan.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Box(pivot, Glow(new Color(0.3f, 0.6f, 1f), 2f), pc + new Vector3(-0.1f, 0.26f, -0.1f), new Vector3(0.004f, 0.4f, 0.01f));
            Soft(pivot, Lit(new Color(0.2f, 0.2f, 0.22f), 0.5f), pc + new Vector3(-0.05f, 0.3f, -0.05f), new Vector3(0.05f, 0.14f, 0.22f), 0.01f);   // graphics card
            // Keyboard (glowing keys), mouse, headset.
            Soft(pivot, Ink, new Vector3(-0.2f, height + 0.015f, 0.18f), new Vector3(0.44f, 0.025f, 0.14f), 0.006f);
            Box(pivot, led, new Vector3(-0.2f, height + 0.028f, 0.18f), new Vector3(0.42f, 0.002f, 0.12f));
            Soft(pivot, Ink, new Vector3(0.28f, height + 0.015f, 0.2f), new Vector3(0.065f, 0.035f, 0.11f), 0.03f);
            Rod(pivot, Ink, new Vector3(-0.95f, height, 0f), new Vector3(-0.95f, height + 0.3f, 0f), 0.01f);
            var headset = Turned(pivot, Ink, new Vector3(-0.95f, height + 0.3f, 0f), Ring(0.09f, 0.012f, 18));
            headset.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (var x in new[] { -0.8f, 0.72f })
            {
                Soft(pivot, Ink, new Vector3(x, height + 0.12f, -0.3f), new Vector3(0.1f, 0.24f, 0.12f), 0.02f);
            }
        }

        private void GamingChair(Transform pivot)
        {
            var main = Z(0);
            var accent = Z(1);
            for (var i = 0; i < 5; i++)
            {
                var a = i * 72f * Mathf.Deg2Rad;
                Rod(pivot, Ink, new Vector3(0f, 0.1f, 0f), new Vector3(Mathf.Sin(a) * 0.34f, 0.05f, Mathf.Cos(a) * 0.34f), 0.022f);
            }
            Rod(pivot, Ink, new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.42f, 0f), 0.03f);
            Soft(pivot, main, new Vector3(0f, 0.48f, -0.02f), new Vector3(0.5f, 0.1f, 0.52f), 0.04f);
            foreach (var x in new[] { -0.26f, 0.26f })
            {
                Soft(pivot, accent, new Vector3(x, 0.55f, -0.02f), new Vector3(0.1f, 0.12f, 0.5f), 0.04f);
                Soft(pivot, accent, new Vector3(x * 1.02f, 1.0f, 0.3f), new Vector3(0.1f, 0.8f, 0.14f), 0.04f, tiltX: -10f);
                Soft(pivot, Ink, new Vector3(x * 1.2f, 0.68f, 0.02f), new Vector3(0.06f, 0.05f, 0.34f), 0.02f);
            }
            Soft(pivot, main, new Vector3(0f, 1.02f, 0.3f), new Vector3(0.44f, 0.95f, 0.1f), 0.04f, tiltX: -10f);
            Soft(pivot, accent, new Vector3(0f, 1.35f, 0.34f), new Vector3(0.24f, 0.12f, 0.06f), 0.03f, tiltX: -10f);
            SeatPoint(pivot, new Vector3(0f, 0.53f, -0.04f));
        }

        /// <summary>Glass-door drinks fridge (1.8 m) full of cans and bottles, LED-lit in the chosen colour.</summary>
        private void DrinkFridge(Transform pivot)
        {
            const float width = 0.62f, height = 1.8f, depth = 0.62f;
            Soft(pivot, Z(0), new Vector3(0f, height / 2f, 0.02f), new Vector3(width, height, depth - 0.04f), 0.01f);
            Box(pivot, Led(1, 1.2f), new Vector3(0f, height / 2f, depth / 2f - 0.07f), new Vector3(width - 0.08f, height - 0.2f, 0.01f));
            var cans = new[] { Lit(new Color(0.8f, 0.1f, 0.1f), 0.8f), Lit(new Color(0.1f, 0.3f, 0.8f), 0.8f), Lit(new Color(0.9f, 0.9f, 0.9f), 0.8f), Lit(new Color(0.1f, 0.6f, 0.2f), 0.8f) };
            for (var shelf = 0; shelf < 5; shelf++)
            {
                var y = 0.18f + shelf * 0.32f;
                Soft(pivot, Glassy(new Color(0.9f, 0.95f, 1f, 0.3f)), new Vector3(0f, y - 0.01f, 0.02f), new Vector3(width - 0.08f, 0.01f, depth - 0.1f), 0.003f);
                for (var i = 0; i < 4; i++)
                {
                    Turned(pivot, cans[(shelf + i) % cans.Length], new Vector3(-0.2f + i * 0.13f, y, -0.1f), SoftShapes.RoundedCylinder(0.033f, 0.12f, 0.006f, 14));
                    Turned(pivot, cans[(shelf + i + 1) % cans.Length], new Vector3(-0.2f + i * 0.13f, y, 0.05f), SoftShapes.RoundedCylinder(0.033f, 0.12f, 0.006f, 14));
                }
            }
            Box(pivot, Glassy(new Color(0.8f, 0.9f, 1f, 0.2f)), new Vector3(0f, height / 2f, -depth / 2f + 0.01f), new Vector3(width - 0.05f, height - 0.08f, 0.01f));
            Rod(pivot, Chrome, new Vector3(width / 2f - 0.06f, 0.8f, -depth / 2f - 0.03f), new Vector3(width / 2f - 0.06f, 1.3f, -depth / 2f - 0.03f), 0.008f);
        }

        private void LedColumn(Transform pivot)
        {
            Soft(pivot, Ink, new Vector3(0f, 0.02f, 0f), new Vector3(0.22f, 0.04f, 0.22f), 0.01f);
            Soft(pivot, Led(0, 3f), new Vector3(0f, 0.85f, 0f), new Vector3(0.05f, 1.6f, 0.05f), 0.02f);
        }

        private void Arcade(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.9f, 0.05f), new Vector3(0.72f, 1.8f, 0.75f), 0.01f);
            Box(pivot, Led(1, 2.5f), new Vector3(0f, 1.68f, -0.33f), new Vector3(0.66f, 0.18f, 0.02f));
            Soft(pivot, Ink, new Vector3(0f, 1.25f, -0.3f), new Vector3(0.62f, 0.5f, 0.05f), 0.006f, tiltX: -15f);
            Box(pivot, Glow(new Color(0.3f, 0.7f, 0.4f), 1.2f), new Vector3(0f, 1.25f, -0.33f), new Vector3(0.56f, 0.44f, 0.002f)).transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            Soft(pivot, Z(0), new Vector3(0f, 0.95f, -0.42f), new Vector3(0.72f, 0.06f, 0.3f), 0.01f, tiltX: 10f);
            Rod(pivot, Ink, new Vector3(-0.15f, 0.97f, -0.45f), new Vector3(-0.15f, 1.07f, -0.45f), 0.01f);
            Sphere(pivot, Glow(new Color(0.9f, 0.1f, 0.1f), 1.5f), new Vector3(-0.15f, 1.08f, -0.45f), Vector3.one * 0.04f);
            for (var i = 0; i < 4; i++)
            {
                Turned(pivot, Glow(i % 2 == 0 ? new Color(0.9f, 0.8f, 0.1f) : new Color(0.1f, 0.5f, 0.9f), 1.5f), new Vector3(0.02f + i * 0.07f, 0.98f, -0.45f), SoftShapes.RoundedCylinder(0.022f, 0.015f, 0.004f, 12));
            }
        }

        // ---------- Adults only ----------

        /// <summary>St Andrew's cross: two padded beams in an X on a base, leather cuffs with rings at the four ends.</summary>
        private void AdultCross(Transform pivot)
        {
            var leather = Z(0);
            var wood = Z(1);
            Soft(pivot, wood, new Vector3(0f, 0.04f, 0.25f), new Vector3(1.3f, 0.08f, 0.7f), 0.01f);
            foreach (var turn in new[] { 28f, -28f })
            {
                Soft(pivot, wood, new Vector3(0f, 1.15f, 0.2f), new Vector3(0.14f, 2.25f, 0.1f), 0.01f).transform.localRotation = Quaternion.Euler(0f, 0f, turn);
                Soft(pivot, leather, new Vector3(0f, 1.15f, 0.13f), new Vector3(0.12f, 2.0f, 0.05f), 0.02f).transform.localRotation = Quaternion.Euler(0f, 0f, turn);
            }
            Rod(pivot, wood, new Vector3(0f, 0.08f, 0.55f), new Vector3(0f, 1.2f, 0.26f), 0.03f);
            foreach (var (x, y) in new[] { (-0.5f, 2.08f), (0.5f, 2.08f), (-0.42f, 0.35f), (0.42f, 0.35f) })
            {
                Soft(pivot, leather, new Vector3(x, y, 0.1f), new Vector3(0.12f, 0.08f, 0.08f), 0.025f);
                Turned(pivot, Chrome, new Vector3(x, y - 0.06f, 0.07f), Ring(0.03f, 0.006f, 12)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        /// <summary>Spanking bench: padded top and kneelers in leather on a black frame, cuffs at the front legs.</summary>
        private void AdultBench(Transform pivot)
        {
            var leather = Z(0);
            var frame = Z(1);
            Soft(pivot, leather, new Vector3(0f, 0.78f, 0f), new Vector3(0.42f, 0.12f, 0.95f), 0.05f, tiltX: -8f);
            foreach (var x in new[] { -0.28f, 0.28f })
            {
                Soft(pivot, leather, new Vector3(x, 0.38f, 0.32f), new Vector3(0.2f, 0.08f, 0.4f), 0.03f);
                Soft(pivot, leather, new Vector3(x * 1.1f, 0.62f, -0.38f), new Vector3(0.14f, 0.07f, 0.28f), 0.025f);
            }
            foreach (var (x, z) in Corners(0.3f, 0.42f))
            {
                Rod(pivot, frame, new Vector3(x, 0f, z), new Vector3(x * 0.6f, 0.72f, z * 0.8f), 0.022f);
            }
            Rod(pivot, frame, new Vector3(-0.3f, 0.06f, -0.42f), new Vector3(0.3f, 0.06f, -0.42f), 0.02f);
            Rod(pivot, frame, new Vector3(-0.3f, 0.06f, 0.42f), new Vector3(0.3f, 0.06f, 0.42f), 0.02f);
            foreach (var x in new[] { -0.22f, 0.22f })
            {
                Soft(pivot, leather, new Vector3(x, 0.15f, -0.4f), new Vector3(0.09f, 0.06f, 0.09f), 0.02f);
                Turned(pivot, Chrome, new Vector3(x, 0.1f, -0.45f), Ring(0.025f, 0.005f, 12)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        /// <summary>Wall stand of dark wood: whips, floggers and paddles on pegs, rope coils, cuffs and a collar.</summary>
        private void AdultRack(Transform pivot)
        {
            var wood = Z(0);
            var leather = Z(1);
            Soft(pivot, wood, new Vector3(0f, 1.05f, 0.1f), new Vector3(1.5f, 2.0f, 0.06f), 0.008f);
            Soft(pivot, wood, new Vector3(0f, 0.03f, 0f), new Vector3(1.5f, 0.06f, 0.35f), 0.008f);
            for (var i = 0; i < 6; i++)
            {
                var x = -0.6f + i * 0.24f;
                Rod(pivot, Chrome, new Vector3(x, 1.75f, 0.07f), new Vector3(x, 1.75f, -0.02f), 0.01f);
                if (i % 3 == 0)
                {
                    // Flogger: handle and tails.
                    Rod(pivot, leather, new Vector3(x, 1.72f, 0f), new Vector3(x, 1.5f, 0f), 0.018f);
                    for (var t = 0; t < 7; t++)
                    {
                        var spread = (t - 3) * 0.012f;
                        Rod(pivot, leather, new Vector3(x, 1.5f, 0f), new Vector3(x + spread, 0.95f, -0.01f * t), 0.004f);
                    }
                }
                else if (i % 3 == 1)
                {
                    // Paddle.
                    Rod(pivot, wood, new Vector3(x, 1.72f, 0f), new Vector3(x, 1.52f, 0f), 0.015f);
                    Soft(pivot, leather, new Vector3(x, 1.3f, 0f), new Vector3(0.12f, 0.42f, 0.02f), 0.02f);
                }
                else
                {
                    // Riding crop.
                    Rod(pivot, leather, new Vector3(x, 1.72f, 0f), new Vector3(x, 1.0f, 0f), 0.007f);
                    Soft(pivot, leather, new Vector3(x, 0.97f, 0f), new Vector3(0.05f, 0.06f, 0.006f), 0.004f);
                }
            }
            var rope = Lit(new Color(0.55f, 0.06f, 0.08f), 0.3f);
            for (var i = 0; i < 2; i++)
            {
                var coil = Turned(pivot, rope, new Vector3(-0.35f + i * 0.7f, 0.6f, 0.03f), Ring(0.14f, 0.02f, 24));
                coil.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            foreach (var x in new[] { -0.12f, 0.12f })
            {
                var cuff = Turned(pivot, leather, new Vector3(x, 0.6f, 0.03f), SoftShapes.RoundedCylinder(0.055f, 0.06f, 0.01f, 16));
                cuff.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            var collar = Turned(pivot, leather, new Vector3(0f, 0.35f, 0.05f), Ring(0.09f, 0.015f, 24));
            collar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Turned(pivot, Chrome, new Vector3(0f, 0.25f, 0.03f), Ring(0.025f, 0.006f, 12)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void AdultCage(Transform pivot)
        {
            const float size = 1.0f, height = 1.2f;
            var bars = Z(0);
            Soft(pivot, bars, new Vector3(0f, 0.03f, 0f), new Vector3(size, 0.06f, size), 0.006f);
            Soft(pivot, Lit(new Color(0.1f, 0.1f, 0.11f), 0.5f), new Vector3(0f, 0.08f, 0f), new Vector3(size - 0.1f, 0.05f, size - 0.1f), 0.02f);
            Soft(pivot, bars, new Vector3(0f, height, 0f), new Vector3(size, 0.04f, size), 0.006f);
            var count = 9;
            for (var i = 0; i < count; i++)
            {
                var t = -size / 2f + i * size / (count - 1);
                foreach (var (x, z) in new[] { (t, -size / 2f), (t, size / 2f), (-size / 2f, t), (size / 2f, t) })
                {
                    Rod(pivot, bars, new Vector3(x, 0.06f, z), new Vector3(x, height, z), 0.009f);
                }
            }
            Turned(pivot, Chrome, new Vector3(0.3f, 0.6f, -size / 2f - 0.03f), Ring(0.04f, 0.008f, 14)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    /// <summary>
    /// Modern abstract paintings, painted in code so every room gets the same eight pictures without image files: colour
    /// fields, Bauhaus arcs, a sunset, a gold stroke on black, waves, a grid, a sun and fluid marble. Canvas grain added.
    /// </summary>
    public static class ArtCanvas
    {
        private const int Width = 256;
        private const int Height = 192;
        private static readonly Dictionary<int, Texture2D> Cache = new();

        public static Texture2D Paint(int index)
        {
            if (Cache.TryGetValue(index, out var cached) && cached != null)
            {
                return cached;
            }
            var pixels = new Color[Width * Height];
            var random = new System.Random(1000 + index);
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var u = x / (float)(Width - 1);
                    var v = y / (float)(Height - 1);
                    var colour = Sample(index, u, v);
                    var grain = (float)(random.NextDouble() - 0.5) * 0.05f;
                    pixels[y * Width + x] = new Color(colour.r + grain, colour.g + grain, colour.b + grain);
                }
            }
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, true) { name = "Artwork " + index, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            Cache[index] = texture;
            return texture;
        }

        private static Color Sample(int index, float u, float v)
        {
            var cream = new Color(0.93f, 0.89f, 0.81f);
            switch ((index - 1) % 8)
            {
                case 0:   // colour fields
                {
                    var background = new Color(0.55f, 0.12f, 0.1f);
                    if (Soft(u, 0.12f, 0.88f) * Soft(v, 0.55f, 0.9f) > 0.5f) return new Color(0.85f, 0.45f, 0.18f);
                    if (Soft(u, 0.12f, 0.88f) * Soft(v, 0.1f, 0.47f) > 0.5f) return new Color(0.32f, 0.07f, 0.08f);
                    return background;
                }
                case 1:   // Bauhaus arcs
                {
                    var c = Vector2.Distance(new Vector2(u * 1.33f, v), new Vector2(0.35f, 0f));
                    if (c < 0.55f && c > 0.35f) return new Color(0.84f, 0.62f, 0.2f);
                    var d = Vector2.Distance(new Vector2(u * 1.33f, v), new Vector2(1.0f, 1f));
                    if (d < 0.42f) return new Color(0.16f, 0.22f, 0.36f);
                    if (u > 0.62f && v < 0.35f) return new Color(0.76f, 0.4f, 0.3f);
                    return cream;
                }
                case 2:   // sunset
                {
                    var sky = Color.Lerp(new Color(0.95f, 0.62f, 0.38f), new Color(0.35f, 0.3f, 0.55f), v);
                    if (Mathf.Abs(v - 0.32f) < 0.004f) return new Color(0.2f, 0.15f, 0.2f);
                    if (v < 0.32f) return Color.Lerp(new Color(0.25f, 0.18f, 0.28f), new Color(0.6f, 0.4f, 0.4f), v / 0.32f);
                    var sun = Vector2.Distance(new Vector2(u * 1.33f, v), new Vector2(0.9f, 0.4f));
                    return sun < 0.12f ? new Color(1f, 0.85f, 0.6f) : sky;
                }
                case 3:   // gold stroke on black
                {
                    var curve = 0.5f + 0.22f * Mathf.Sin(u * 5.5f + 0.6f) * (1f - u * 0.4f);
                    var width = 0.05f + 0.04f * Mathf.Sin(u * 13f);
                    return Mathf.Abs(v - curve) < width ? new Color(0.82f, 0.65f, 0.32f) * (0.85f + 0.15f * Mathf.Sin(u * 90f)) : new Color(0.06f, 0.06f, 0.07f);
                }
                case 4:   // waves
                {
                    var wave = Mathf.Sin(u * 9f + Mathf.Sin(v * 6f) * 2f + v * 14f);
                    return Color.Lerp(new Color(0.12f, 0.27f, 0.45f), new Color(0.88f, 0.92f, 0.95f), wave * 0.5f + 0.5f);
                }
                case 5:   // muted grid
                {
                    if (Mathf.Abs(u - 0.38f) < 0.012f || Mathf.Abs(v - 0.6f) < 0.016f || (u > 0.38f && Mathf.Abs(u - 0.72f) < 0.012f)) return new Color(0.15f, 0.15f, 0.16f);
                    if (u < 0.38f && v > 0.6f) return new Color(0.72f, 0.45f, 0.32f);
                    if (u > 0.72f && v < 0.6f) return new Color(0.45f, 0.52f, 0.42f);
                    return cream;
                }
                case 6:   // sun
                {
                    var d = Vector2.Distance(new Vector2(u * 1.33f, v), new Vector2(0.67f, 0.55f));
                    if (d < 0.26f) return new Color(0.88f, 0.4f, 0.16f);
                    if (v < 0.2f) return new Color(0.55f, 0.42f, 0.32f);
                    return new Color(0.9f, 0.82f, 0.68f);
                }
                default:  // fluid marble green/gold
                {
                    var flow = Mathf.Sin(u * 7f + Mathf.Sin(v * 9f + u * 3f) * 2.5f);
                    var vein = Mathf.Abs(flow) < 0.06f ? 1f : 0f;
                    var baseColour = Color.Lerp(new Color(0.08f, 0.3f, 0.26f), new Color(0.9f, 0.92f, 0.88f), flow * 0.5f + 0.5f);
                    return Color.Lerp(baseColour, new Color(0.85f, 0.68f, 0.3f), vein);
                }
            }
        }

        private static float Soft(float t, float from, float to) => t > from && t < to ? 1f : 0f;
    }
}
