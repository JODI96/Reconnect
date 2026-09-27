using System.Collections.Generic;
using Reconnect.Contracts.Rooms;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// What each swatch (<see cref="ItemColours"/>) looks like: surface material and tint. Shared by the furniture builder
    /// and the build editor's colour chips.
    /// </summary>
    public static class SwatchLook
    {
        public static readonly IReadOnlyDictionary<string, (string Surface, Color Tint)> Fabrics = new Dictionary<string, (string, Color)>
        {
            ["sand"] = (SurfaceMaterials.Boucle, new Color(0.9f, 0.85f, 0.77f)),
            ["cream"] = (SurfaceMaterials.Linen, new Color(0.96f, 0.94f, 0.9f)),
            ["oat"] = (SurfaceMaterials.Boucle, new Color(0.8f, 0.74f, 0.64f)),
            ["stone"] = (SurfaceMaterials.Boucle, new Color(0.63f, 0.62f, 0.59f)),
            ["charcoal"] = (SurfaceMaterials.Boucle, new Color(0.3f, 0.3f, 0.32f)),
            ["black"] = (SurfaceMaterials.Velvet, new Color(0.12f, 0.12f, 0.13f)),
            ["sage"] = (SurfaceMaterials.Velvet, new Color(0.56f, 0.64f, 0.53f)),
            ["olive"] = (SurfaceMaterials.Velvet, new Color(0.43f, 0.45f, 0.27f)),
            ["emerald"] = (SurfaceMaterials.Velvet, new Color(0.07f, 0.34f, 0.24f)),
            ["petrol"] = (SurfaceMaterials.Velvet, new Color(0.13f, 0.35f, 0.41f)),
            ["navy"] = (SurfaceMaterials.Velvet, new Color(0.13f, 0.17f, 0.32f)),
            ["sky"] = (SurfaceMaterials.Linen, new Color(0.63f, 0.75f, 0.86f)),
            ["blush"] = (SurfaceMaterials.Velvet, new Color(0.9f, 0.68f, 0.64f)),
            ["terracotta"] = (SurfaceMaterials.Boucle, new Color(0.77f, 0.44f, 0.31f)),
            ["rust"] = (SurfaceMaterials.Velvet, new Color(0.6f, 0.27f, 0.15f)),
            ["mustard"] = (SurfaceMaterials.Velvet, new Color(0.84f, 0.63f, 0.22f)),
            ["plum"] = (SurfaceMaterials.Velvet, new Color(0.37f, 0.19f, 0.31f)),
            ["burgundy"] = (SurfaceMaterials.Velvet, new Color(0.43f, 0.1f, 0.15f)),
            ["cognac"] = (SurfaceMaterials.Cognac, Color.white),
            ["tan"] = (SurfaceMaterials.CreamLeather, new Color(0.82f, 0.62f, 0.42f)),
            ["espresso"] = (SurfaceMaterials.Cognac, new Color(0.45f, 0.36f, 0.32f)),
            ["white-leather"] = (SurfaceMaterials.CreamLeather, new Color(0.97f, 0.96f, 0.93f)),
        };

        public static readonly IReadOnlyDictionary<string, (string Surface, Color Tint)> Woods = new Dictionary<string, (string, Color)>
        {
            ["oak"] = (SurfaceMaterials.Oak, Color.white),
            ["light-oak"] = (SurfaceMaterials.WhiteOak, Color.white),
            ["walnut"] = (SurfaceMaterials.Walnut, Color.white),
            ["smoked-oak"] = (SurfaceMaterials.Oak, new Color(0.55f, 0.47f, 0.4f)),
            ["ebony"] = (SurfaceMaterials.Walnut, new Color(0.3f, 0.27f, 0.25f)),
            ["cherry"] = (SurfaceMaterials.Oak, new Color(0.88f, 0.58f, 0.44f)),
        };

        public static readonly IReadOnlyDictionary<string, (Color Colour, float Smoothness, float Metallic)> Metals = new Dictionary<string, (Color, float, float)>
        {
            ["black"] = (new Color(0.07f, 0.07f, 0.075f), 0.55f, 1f),
            ["brass"] = (new Color(0.8f, 0.62f, 0.34f), 0.72f, 1f),
            ["chrome"] = (new Color(0.9f, 0.9f, 0.92f), 0.9f, 1f),
            ["bronze"] = (new Color(0.46f, 0.31f, 0.2f), 0.6f, 1f),
            ["white"] = (new Color(0.93f, 0.93f, 0.92f), 0.4f, 0f),
            ["gunmetal"] = (new Color(0.28f, 0.29f, 0.31f), 0.65f, 1f),
        };

        public static readonly IReadOnlyDictionary<string, (string Surface, Color Tint, float Smoothness)> Stones = new Dictionary<string, (string, Color, float)>
        {
            ["white-marble"] = (SurfaceMaterials.Marble, Color.white, -1f),
            ["black-marble"] = (SurfaceMaterials.BlackMarble, Color.white, -1f),
            ["green-marble"] = (SurfaceMaterials.Marble, new Color(0.45f, 0.62f, 0.52f), -1f),
            ["travertine"] = (SurfaceMaterials.Travertine, Color.white, -1f),
            ["terrazzo"] = (SurfaceMaterials.Boucle, new Color(0.9f, 0.88f, 0.84f), 0.4f),
            ["concrete"] = (null, new Color(0.64f, 0.63f, 0.6f), 0.15f),
        };

        public static readonly IReadOnlyDictionary<string, Color> Paints = new Dictionary<string, Color>
        {
            ["white"] = new Color(0.95f, 0.95f, 0.94f), ["warm-white"] = new Color(0.94f, 0.91f, 0.85f), ["greige"] = new Color(0.72f, 0.68f, 0.62f),
            ["sage"] = new Color(0.6f, 0.66f, 0.56f), ["navy"] = new Color(0.15f, 0.2f, 0.33f), ["black"] = new Color(0.09f, 0.09f, 0.1f),
            ["terracotta"] = new Color(0.75f, 0.43f, 0.31f), ["mustard"] = new Color(0.86f, 0.66f, 0.26f), ["blush"] = new Color(0.9f, 0.73f, 0.69f),
        };

        /// <summary>Colour of the chip in the build editor.</summary>
        public static Color Chip(SwatchKind kind, string swatch)
        {
            switch (kind)
            {
                case SwatchKind.Fabric when Fabrics.TryGetValue(swatch, out var fabric):
                    return fabric.Surface == SurfaceMaterials.Cognac ? new Color(0.6f, 0.34f, 0.18f) * fabric.Tint : fabric.Tint;
                case SwatchKind.Wood when Woods.TryGetValue(swatch, out var wood):
                    var baseWood = wood.Surface switch
                    {
                        SurfaceMaterials.Walnut => new Color(0.4f, 0.27f, 0.19f),
                        SurfaceMaterials.WhiteOak => new Color(0.78f, 0.68f, 0.55f),
                        _ => new Color(0.7f, 0.53f, 0.36f),
                    };
                    return baseWood * wood.Tint;
                case SwatchKind.Metal when Metals.TryGetValue(swatch, out var metal):
                    return metal.Colour;
                case SwatchKind.Stone when Stones.TryGetValue(swatch, out var stone):
                    return stone.Surface switch
                    {
                        null => stone.Tint,
                        SurfaceMaterials.BlackMarble => new Color(0.12f, 0.12f, 0.13f),
                        SurfaceMaterials.Travertine => new Color(0.86f, 0.79f, 0.66f),
                        _ => new Color(0.92f, 0.91f, 0.89f) * stone.Tint,
                    };
                case SwatchKind.Paint when Paints.TryGetValue(swatch, out var paint):
                    return paint;
                default:
                    return Color.grey;
            }
        }
    }

    /// <summary>
    /// "Reconnect Modern" (<see cref="FurnitureFamilies"/>): our own contemporary furniture – soft upholstery, oiled wood,
    /// marble and metal, built from <see cref="SoftShapes"/> with real materials, every colour zone in the chosen swatch.
    /// Real sizes in metres, the front (the side one sits looking at) is −Z like all custom items. Seats are 45 cm high,
    /// table tops 75 cm, so chairs slide under the tables.
    /// </summary>
    public sealed partial class CustomItems
    {
        private readonly Dictionary<(string Surface, Color Tint, float Smoothness), Material> _surfaceCache = new();
        private readonly Dictionary<(Color Colour, float Smoothness, float Metallic), Material> _metals = new();
        private Material[] _zones;

        private bool TryBuildFamily(string itemId, Transform pivot, string colours, out bool blocksTiles)
        {
            blocksTiles = true;
            if (!itemId.StartsWith(Prefix) || (FurnitureFamilies.Find(itemId) is not { } info))
            {
                return false;
            }
            var zones = ItemColours.ZonesFor(itemId);
            var swatches = ItemColours.Resolve(itemId, colours);
            _zones = new Material[zones.Count];
            for (var i = 0; i < zones.Count; i++)
            {
                _zones[i] = SwatchMaterial(zones[i].Kind, swatches[i]);
            }
            blocksTiles = info.Kind == ItemKind.Floor;
            var parts = itemId.Substring(Prefix.Length).Split('-');
            string Part(int i) => i < parts.Length ? parts[i] : "";
            int Number(int i) => int.TryParse(Part(i), out var n) ? n : 0;
            // Built in a child that is then centred on the pivot: the footprint (centred on the pivot) and the piece line up
            // even for asymmetric ones (corner sofa, corner banquette, arc lamp).
            var model = new GameObject("Model").transform;
            model.SetParent(pivot, false);
            bool built;
            switch (parts[0])
            {
                case "sofa": Sofa(model, Part(1), Part(2) == "corner" ? -1 : Number(2)); built = true; break;
                case "armchair": Sofa(model, Part(1), 1); built = true; break;
                case "ottoman": Ottoman(model, Part(1)); built = true; break;
                case "lounge": LoungeChair(model, Part(1)); built = true; break;
                case "banquette": Banquette(model, Part(1) == "corner" ? -1 : Number(1) / 100f); built = true; break;
                case "chair": Chair(model, Part(1)); built = true; break;
                case "barstool": BarStool(model, Part(1)); built = true; break;
                case "pouf": Pouf(model, Part(1)); built = true; break;
                case "bench": Bench(model, Part(1), Number(2) / 100f); built = true; break;
                case "table": Table(model, Part(1), Number(2) / 100f); built = true; break;
                case "roundtable": RoundTable(model, Number(2) / 200f); built = true; break;
                case "bistro": Bistro(model, Part(1) == "square"); built = true; break;
                case "hightable": HighTable(model, Part(1) == "long"); built = true; break;
                case "dinner": Dinner(model, Part(1), Number(2)); built = true; break;
                case "coffeetable": CoffeeTable(model, Part(1)); built = true; break;
                case "sidetable": SideTable(model, Part(1)); built = true; break;
                case "console": ConsoleTable(model); built = true; break;
                case "desk": Desk(model, Number(1) / 100f); built = true; break;
                case "sideboard": Sideboard(model, Number(1) / 100f); built = true; break;
                case "shelf": Shelf(model, Part(1)); built = true; break;
                case "cabinet": TallCabinet(model); built = true; break;
                case "vitrine": Vitrine(model); built = true; break;
                case "barcabinet": BarCabinet(model); built = true; break;
                case "floorlamp": FloorLamp(model, Part(1)); built = true; break;
                case "tablelamp": TableLamp(model, Part(1)); built = true; break;
                case "pendantlamp": PendantLamp(model, Part(1)); built = true; break;
                case "plant": Plant(model, Part(1), Part(2)); built = true; break;
                case "planterbench": PlanterBench(model); built = true; break;
                case "acoustic": Acoustic(model, Part(1) == "curved"); built = true; break;
                case "whiteboard": Whiteboard(model); built = true; break;
                case "screen": Screen(model); built = true; break;
                case "vase": Vase(model, Part(1)); built = true; break;
                case "candles": Candles(model); built = true; break;
                case "books": Books(model); built = true; break;
                case "tray": Tray(model); built = true; break;
                case "zone":
                    FurnitureFamilies.TryZone(itemId, out _, out var w, out var d);
                    Soft(model, Z(0), new Vector3(0f, 0.006f, 0f), new Vector3(w, 0.012f, d), 0.005f);
                    built = true; break;
                case "rugmodern": ModernRug(model, itemId); built = true; break;
                default: built = false; break;
            }
            if (!built)
            {
                DestroyNow(model.gameObject);
                return false;
            }
            CentreOnPivot(pivot, model);
            return true;
        }

        /// <summary>Moves <paramref name="model"/> so the middle of everything it shows lies on the pivot (x/z, in pivot space).</summary>
        private static void CentreOnPivot(Transform pivot, Transform model)
        {
            var min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            var max = new Vector3(float.MinValue, 0f, float.MinValue);
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || IsSpawnedModel(filter.transform, model))
                {
                    continue;   // plants and books placed into it don't count (their leaves are lopsided)
                }
                var toPivot = pivot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = filter.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = toPivot.MultiplyPoint3x4(corner);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            if (min.x > max.x)
            {
                return;
            }
            model.localPosition -= new Vector3((min.x + max.x) / 2f, 0f, (min.z + max.z) / 2f);
        }

        private static bool IsSpawnedModel(Transform part, Transform model)
        {
            for (var t = part; t != null && t != model; t = t.parent)
            {
                if (t.name.StartsWith("ph-") || t.name.StartsWith("books"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Material of colour zone <paramref name="i"/> of the item being built.</summary>
        private Material Z(int i) => _zones != null && i < _zones.Length ? _zones[i] : BlackSteel;

        // ---------- Materials ----------

        private Material SwatchMaterial(SwatchKind kind, string swatch)
        {
            switch (kind)
            {
                case SwatchKind.Fabric:
                    var (fabric, fabricTint) = SwatchLook.Fabrics.TryGetValue(swatch, out var f) ? f : SwatchLook.Fabrics["sand"];
                    return Surface(fabric, fabricTint);
                case SwatchKind.Wood:
                    var (wood, woodTint) = SwatchLook.Woods.TryGetValue(swatch, out var w) ? w : SwatchLook.Woods["oak"];
                    return Surface(wood, woodTint);
                case SwatchKind.Metal:
                    var (colour, smoothness, metallic) = SwatchLook.Metals.TryGetValue(swatch, out var m) ? m : SwatchLook.Metals["black"];
                    return Metal(colour, smoothness, metallic);
                case SwatchKind.Stone:
                    var (stone, stoneTint, stoneSmooth) = SwatchLook.Stones.TryGetValue(swatch, out var s) ? s : SwatchLook.Stones["white-marble"];
                    return stone == null ? Lit(stoneTint, stoneSmooth) : Surface(stone, stoneTint, stoneSmooth);
                default:
                    return Lit(SwatchLook.Paints.TryGetValue(swatch, out var paint) ? paint : Color.white, 0.4f);
            }
        }

        /// <summary>A surface from the library, multiplied by <paramref name="tint"/>; a plain colour if the library is missing.</summary>
        private Material Surface(string name, Color tint, float smoothness = -1f)
        {
            if (_surfaceCache.TryGetValue((name, tint, smoothness), out var cached))
            {
                return cached;
            }
            var entry = _surfaces != null ? _surfaces.Find(name) : null;
            Material material;
            if (entry?.material == null)
            {
                material = Lit(FallbackColour(name) * tint, smoothness >= 0f ? smoothness : 0.35f);
            }
            else if (tint == Color.white && smoothness < 0f)
            {
                material = entry.material;
            }
            else
            {
                material = new Material(entry.material);
                material.SetColor("_BaseColor", entry.material.GetColor("_BaseColor") * tint);
                if (smoothness >= 0f)
                {
                    material.SetFloat("_Smoothness", smoothness);
                }
            }
            _surfaceCache[(name, tint, smoothness)] = material;
            return material;
        }

        private static Color FallbackColour(string name) => name switch
        {
            SurfaceMaterials.Walnut => new Color(0.4f, 0.27f, 0.19f),
            SurfaceMaterials.Oak => new Color(0.72f, 0.56f, 0.38f),
            SurfaceMaterials.WhiteOak => new Color(0.82f, 0.72f, 0.58f),
            SurfaceMaterials.Cognac => new Color(0.6f, 0.34f, 0.18f),
            SurfaceMaterials.Marble => new Color(0.93f, 0.92f, 0.9f),
            _ => new Color(0.9f, 0.9f, 0.9f),
        };

        private Material Metal(Color colour, float smoothness, float metallic = 1f)
        {
            if (!_metals.TryGetValue((colour, smoothness, metallic), out var material))
            {
                material = new Material(_litBase);
                material.SetColor("_BaseColor", colour);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", metallic);
                _metals[(colour, smoothness, metallic)] = material;
            }
            return material;
        }

        private Material BlackSteel => Metal(new Color(0.07f, 0.07f, 0.075f), 0.55f);
        private Material Ink => Lit(new Color(0.06f, 0.06f, 0.07f), 0.3f);

        // ---------- Shapes ----------

        private static GameObject Soft(Transform parent, Material material, Vector3 centre, Vector3 size, float radius, float tiltX = 0f, float turnY = 0f)
        {
            var go = new GameObject("Soft", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(tiltX, turnY, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = SoftShapes.RoundedBox(size, radius);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static GameObject Turned(Transform parent, Material material, Vector3 position, Mesh mesh, Vector3? scale = null)
        {
            var go = new GameObject("Turned", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale ?? Vector3.one;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>A round rod from a to b.</summary>
        private static GameObject Rod(Transform parent, Material material, Vector3 a, Vector3 b, float radius)
        {
            var go = Turned(parent, material, a, SoftShapes.RoundedCylinder(radius, Vector3.Distance(a, b), radius * 0.5f, 10));
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            return go;
        }

        /// <summary>A leg that gets thinner towards the floor.</summary>
        private static Mesh TaperedLeg(float height, float top, float bottom) =>
            SoftShapes.Lathe(new[] { new Vector2(0f, 0f), new Vector2(bottom, 0f), new Vector2(top, height), new Vector2(0f, height) }, 12);

        private static IEnumerable<(float X, float Z)> Corners(float halfX, float halfZ)
        {
            yield return (-halfX, -halfZ);
            yield return (halfX, -halfZ);
            yield return (halfX, halfZ);
            yield return (-halfX, halfZ);
        }

        // ---------- Sofas & armchairs ----------

        /// <summary>
        /// A sofa in one of five styles; <paramref name="seats"/> 1 = armchair, 2 or 3 seats, −1 = corner sofa (three seats
        /// plus a chaise on the left). Zone 0 upholstery, zone 1 legs.
        /// </summary>
        private void Sofa(Transform pivot, string style, int seats)
        {
            var fabric = Z(0);
            var legs = Z(1);
            var corner = seats < 0;
            var count = corner ? 3 : seats;
            var (arm, depth, seat, back, radius, legHeight) = style switch
            {
                "round" => (0.26f, 0.95f, 0.44f, 0.76f, 0.12f, 0.1f),
                "tufted" => (0.2f, 0.9f, 0.45f, 0.72f, 0.05f, 0.12f),
                "low" => (0.22f, 1.02f, 0.4f, 0.66f, 0.09f, 0.02f),
                "slim" => (0.08f, 0.86f, 0.44f, 0.78f, 0.03f, 0.2f),
                _ => (0.18f, 0.92f, 0.44f, 0.74f, 0.06f, 0.1f),
            };
            var cushion = count == 1 ? 0.62f : count == 2 ? 0.7f : 0.66f;
            var width = count * cushion + 2f * arm;
            // Legs (hidden under the plinth of the low style).
            if (legHeight > 0.05f)
            {
                var leg = style is "round" or "tufted" ? TaperedLeg(legHeight, 0.025f, 0.016f) : SoftShapes.RoundedCylinder(0.018f, legHeight, 0.006f, 10);
                foreach (var (x, z) in Corners(width / 2f - 0.08f, depth / 2f - 0.08f))
                {
                    Turned(pivot, legs, new Vector3(x, 0f, z), leg);
                }
            }
            else
            {
                Soft(pivot, legs, new Vector3(0f, 0.02f, 0.02f), new Vector3(width - 0.1f, 0.04f, depth - 0.1f), 0.01f);
            }
            var baseTop = seat - 0.14f;
            Soft(pivot, fabric, new Vector3(0f, (legHeight + baseTop) / 2f, 0.01f), new Vector3(width, baseTop - legHeight, depth - 0.02f), Mathf.Min(radius, 0.06f));
            // Seat cushions.
            for (var i = 0; i < count; i++)
            {
                var x = -count * cushion / 2f + (i + 0.5f) * cushion;
                Soft(pivot, fabric, new Vector3(x, baseTop + 0.07f, -0.06f), new Vector3(cushion - 0.01f, 0.15f, depth - 0.3f), radius * 0.9f);
            }
            // Back.
            var backDepth = style == "slim" ? 0.1f : 0.16f;
            Soft(pivot, fabric, new Vector3(0f, (legHeight + back) / 2f, depth / 2f - backDepth / 2f), new Vector3(width, back - legHeight, backDepth), Mathf.Min(radius, 0.07f));
            if (style == "tufted")
            {
                var channels = count * 3;
                for (var i = 0; i < channels; i++)
                {
                    var x = -count * cushion / 2f + (i + 0.5f) * count * cushion / channels;
                    Soft(pivot, fabric, new Vector3(x, seat + 0.15f, depth / 2f - backDepth - 0.05f), new Vector3(count * cushion / channels - 0.008f, 0.3f, 0.1f), 0.045f, 8f);
                }
            }
            else if (style != "slim")
            {
                for (var i = 0; i < count; i++)
                {
                    var x = -count * cushion / 2f + (i + 0.5f) * cushion;
                    Soft(pivot, fabric, new Vector3(x, seat + 0.15f, depth / 2f - backDepth - 0.07f), new Vector3(cushion - 0.012f, 0.34f, 0.16f), radius * 1.2f, 10f);
                }
            }
            // Arms (tufted: as high as the back).
            var armHeight = style == "tufted" ? back : style == "slim" ? seat + 0.16f : seat + 0.2f;
            foreach (var side in new[] { -1f, 1f })
            {
                if (corner && side < 0f)
                {
                    continue;   // the chaise closes the left end
                }
                Soft(pivot, fabric, new Vector3(side * (width / 2f - arm / 2f), (legHeight + armHeight) / 2f, 0f), new Vector3(arm, armHeight - legHeight, depth), radius);
            }
            if (corner)
            {
                // Chaise: one more cushion towards the front on the left, 1.6 m deep in all.
                var x = -width / 2f + arm + cushion / 2f;
                var reach = 0.75f;
                Soft(pivot, fabric, new Vector3(x - arm / 2f, (legHeight + baseTop) / 2f, -depth / 2f - reach / 2f + 0.01f), new Vector3(cushion + arm, baseTop - legHeight, reach), Mathf.Min(radius, 0.06f));
                Soft(pivot, fabric, new Vector3(x, baseTop + 0.07f, -depth / 2f - reach / 2f - 0.02f), new Vector3(cushion - 0.01f, 0.15f, reach + 0.1f), radius * 0.9f);
                Soft(pivot, fabric, new Vector3(-width / 2f + arm / 2f, (legHeight + armHeight) / 2f, -reach / 2f), new Vector3(arm, armHeight - legHeight, depth + reach), radius);
                if (legHeight > 0.05f)
                {
                    Turned(pivot, legs, new Vector3(x - arm / 2f, 0f, -depth / 2f - reach + 0.08f), SoftShapes.RoundedCylinder(0.018f, legHeight, 0.006f, 10));
                }
            }
        }

        private void Ottoman(Transform pivot, string style)
        {
            var round = style is "round" or "tufted";
            if (round)
            {
                Turned(pivot, Z(0), Vector3.zero, SoftShapes.RoundedCylinder(0.34f, 0.42f, 0.1f));
                return;
            }
            Soft(pivot, Z(1), new Vector3(0f, 0.04f, 0f), new Vector3(0.66f, 0.08f, 0.5f), 0.01f);
            Soft(pivot, Z(0), new Vector3(0f, 0.25f, 0f), new Vector3(0.74f, 0.34f, 0.56f), 0.07f);
        }

        /// <summary>Lounge chairs: "shell" (moulded wood shell, leather cushions, swivel foot) and "sling" (chrome frame, leather).</summary>
        private void LoungeChair(Transform pivot, string style)
        {
            if (style == "sling")
            {
                var frame = Z(1);
                foreach (var side in new[] { -1f, 1f })
                {
                    var x = side * 0.34f;
                    Rod(pivot, frame, new Vector3(x, 0.02f, -0.4f), new Vector3(x, 0.02f, 0.42f), 0.012f);
                    Rod(pivot, frame, new Vector3(x, 0.02f, -0.4f), new Vector3(x, 0.4f, -0.3f), 0.012f);
                    Rod(pivot, frame, new Vector3(x, 0.4f, -0.3f), new Vector3(x, 0.32f, 0.2f), 0.012f);
                    Rod(pivot, frame, new Vector3(x, 0.32f, 0.2f), new Vector3(x, 0.82f, 0.42f), 0.012f);
                    Rod(pivot, frame, new Vector3(x, 0.02f, 0.42f), new Vector3(x, 0.32f, 0.2f), 0.012f);
                }
                Soft(pivot, Z(0), new Vector3(0f, 0.38f, -0.05f), new Vector3(0.64f, 0.06f, 0.5f), 0.025f, 10f);
                Soft(pivot, Z(0), new Vector3(0f, 0.6f, 0.3f), new Vector3(0.64f, 0.52f, 0.06f), 0.025f, 24f);
                return;
            }
            Turned(pivot, Z(2), Vector3.zero, SoftShapes.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.3f, 0f), new Vector2(0.3f, 0.02f), new Vector2(0.04f, 0.05f), new Vector2(0.03f, 0.3f), new Vector2(0f, 0.3f),
            }, 20));
            Soft(pivot, Z(1), new Vector3(0f, 0.34f, -0.02f), new Vector3(0.78f, 0.05f, 0.7f), 0.02f, 4f);
            Soft(pivot, Z(1), new Vector3(0f, 0.66f, 0.32f), new Vector3(0.78f, 0.62f, 0.05f), 0.02f, 16f);
            Soft(pivot, Z(0), new Vector3(0f, 0.41f, -0.05f), new Vector3(0.66f, 0.1f, 0.6f), 0.045f, 4f);
            Soft(pivot, Z(0), new Vector3(0f, 0.68f, 0.26f), new Vector3(0.66f, 0.5f, 0.1f), 0.045f, 16f);
            foreach (var side in new[] { -1f, 1f })
            {
                Soft(pivot, Z(1), new Vector3(side * 0.37f, 0.5f, 0.02f), new Vector3(0.05f, 0.08f, 0.56f), 0.02f);
            }
        }

        /// <summary>Restaurant banquette (length in metres, −1 = corner): plinth, deep seat, channel-tufted back on a wood panel.</summary>
        private void Banquette(Transform pivot, float length)
        {
            void Run(Vector3 centre, float runLength, float turn)
            {
                var run = new GameObject("Run").transform;
                run.SetParent(pivot, false);
                run.localPosition = centre;
                run.localRotation = Quaternion.Euler(0f, turn, 0f);
                const float depth = 0.64f;
                Soft(run, BlackSteel, new Vector3(0f, 0.06f, 0.02f), new Vector3(runLength - 0.06f, 0.12f, depth - 0.08f), 0.01f);
                Soft(run, Z(0), new Vector3(0f, 0.3f, -0.02f), new Vector3(runLength, 0.3f, depth - 0.12f), 0.06f);
                Soft(run, Z(1), new Vector3(0f, 0.55f, depth / 2f - 0.03f), new Vector3(runLength, 1.1f, 0.06f), 0.02f);
                var channels = Mathf.Max(2, Mathf.RoundToInt(runLength / 0.3f));
                for (var i = 0; i < channels; i++)
                {
                    var x = -runLength / 2f + (i + 0.5f) * runLength / channels;
                    Soft(run, Z(0), new Vector3(x, 0.76f, depth / 2f - 0.13f), new Vector3(runLength / channels - 0.01f, 0.56f, 0.15f), 0.07f, 6f);
                }
            }
            if (length < 0f)
            {
                Run(new Vector3(0.32f, 0f, 0f), 1.76f, 0f);
                Run(new Vector3(-0.88f + 0.32f, 0f, -0.56f), 1.12f, -90f);
                return;
            }
            Run(Vector3.zero, length, 0f);
        }

        // ---------- Chairs & stools ----------

        private void Chair(Transform pivot, string style)
        {
            switch (style)
            {
                case "shell":
                {
                    // Moulded shell (zone 0) on four splayed wooden legs (zone 1).
                    foreach (var (x, z) in Corners(0.19f, 0.19f))
                    {
                        Rod(pivot, Z(1), new Vector3(x * 1.15f, 0f, z * 1.15f), new Vector3(x * 0.7f, 0.42f, z * 0.7f), 0.016f);
                    }
                    Soft(pivot, BlackSteel, new Vector3(0f, 0.42f, 0f), new Vector3(0.3f, 0.02f, 0.3f), 0.008f);
                    Soft(pivot, Z(0), new Vector3(0f, 0.45f, -0.01f), new Vector3(0.47f, 0.04f, 0.44f), 0.02f);
                    Soft(pivot, Z(0), new Vector3(0f, 0.66f, 0.22f), new Vector3(0.47f, 0.38f, 0.04f), 0.02f, 14f);
                    foreach (var side in new[] { -1f, 1f })
                    {
                        Soft(pivot, Z(0), new Vector3(side * 0.23f, 0.56f, 0.07f), new Vector3(0.04f, 0.2f, 0.34f), 0.02f);
                    }
                    return;
                }
                case "cantilever":
                {
                    var frame = Z(1);
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var x = side * 0.21f;
                        Rod(pivot, frame, new Vector3(x, 0.012f, 0.22f), new Vector3(x, 0.012f, -0.24f), 0.011f);
                        Rod(pivot, frame, new Vector3(x, 0.012f, -0.24f), new Vector3(x, 0.44f, -0.2f), 0.011f);
                        Rod(pivot, frame, new Vector3(x, 0.44f, -0.2f), new Vector3(x, 0.44f, 0.2f), 0.011f);
                        Rod(pivot, frame, new Vector3(x, 0.44f, 0.2f), new Vector3(x, 0.86f, 0.26f), 0.011f);
                    }
                    Soft(pivot, Z(0), new Vector3(0f, 0.465f, 0f), new Vector3(0.44f, 0.04f, 0.42f), 0.018f);
                    Soft(pivot, Z(0), new Vector3(0f, 0.7f, 0.235f), new Vector3(0.44f, 0.28f, 0.035f), 0.015f, 8f);
                    return;
                }
                case "velvet":
                {
                    // Tub chair: round seat, curved back in three pieces, slim metal legs.
                    foreach (var (x, z) in Corners(0.18f, 0.18f))
                    {
                        Turned(pivot, Z(1), new Vector3(x, 0f, z), TaperedLeg(0.3f, 0.014f, 0.01f));
                    }
                    Turned(pivot, Z(0), new Vector3(0f, 0.3f, 0f), SoftShapes.RoundedCylinder(0.27f, 0.17f, 0.06f));
                    for (var i = -1; i <= 1; i++)
                    {
                        var angle = i * 50f;
                        var position = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.63f, 0.21f);
                        Soft(pivot, Z(0), position, new Vector3(0.3f, 0.34f, 0.09f), 0.045f, 6f, angle);
                    }
                    return;
                }
                default:
                {
                    // classic / wood / ladder: four legs, seat, back.
                    var wood = style == "classic" ? Z(1) : Z(0);
                    var seat = style == "classic" ? Z(0) : Z(1);
                    foreach (var (x, z) in Corners(0.2f, 0.2f))
                    {
                        Turned(pivot, wood, new Vector3(x, 0f, z), TaperedLeg(0.43f, 0.02f, 0.014f));
                    }
                    Soft(pivot, wood, new Vector3(0f, 0.43f, 0f), new Vector3(0.46f, 0.025f, 0.46f), 0.01f);
                    Soft(pivot, seat, new Vector3(0f, 0.46f, -0.005f), new Vector3(0.45f, style == "classic" ? 0.05f : 0.035f, 0.44f), 0.02f);
                    foreach (var side in new[] { -1f, 1f })
                    {
                        Rod(pivot, wood, new Vector3(side * 0.2f, 0.43f, 0.2f), new Vector3(side * 0.2f, 0.84f, 0.24f), 0.013f);
                    }
                    if (style == "classic")
                    {
                        Soft(pivot, seat, new Vector3(0f, 0.72f, 0.235f), new Vector3(0.46f, 0.24f, 0.05f), 0.022f, 8f);
                    }
                    else if (style == "ladder")
                    {
                        foreach (var y in new[] { 0.58f, 0.68f, 0.78f })
                        {
                            Soft(pivot, wood, new Vector3(0f, y, 0.2f + (y - 0.43f) * 0.1f), new Vector3(0.4f, 0.04f, 0.02f), 0.008f, 6f);
                        }
                    }
                    else
                    {
                        Soft(pivot, wood, new Vector3(0f, 0.76f, 0.232f), new Vector3(0.44f, 0.12f, 0.025f), 0.01f, 8f);
                    }
                    return;
                }
            }
        }

        private void BarStool(Transform pivot, string style)
        {
            var frame = Z(1);
            Turned(pivot, frame, Vector3.zero, SoftShapes.RoundedCylinder(0.21f, 0.025f, 0.01f));
            Turned(pivot, frame, Vector3.zero, SoftShapes.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.025f, 0f), new Vector2(0.025f, 0.72f), new Vector2(0f, 0.72f) }, 12));
            var ring = new List<Vector2>();
            for (var k = 0; k <= 12; k++)
            {
                var a = Mathf.PI * 2f * k / 12f;
                ring.Add(new Vector2(0.17f + 0.011f * Mathf.Cos(a), 0.011f * Mathf.Sin(a)));
            }
            Turned(pivot, frame, new Vector3(0f, 0.3f, 0f), SoftShapes.Lathe(ring, 24));
            if (style == "saddle")
            {
                Soft(pivot, Z(0), new Vector3(0f, 0.74f, 0f), new Vector3(0.42f, 0.07f, 0.34f), 0.03f);
                return;
            }
            Turned(pivot, Z(0), new Vector3(0f, 0.7f, 0f), SoftShapes.RoundedCylinder(0.2f, 0.075f, 0.03f));
            if (style == "back")
            {
                Rod(pivot, frame, new Vector3(0f, 0.76f, 0.17f), new Vector3(0f, 0.9f, 0.19f), 0.012f);
                Soft(pivot, Z(0), new Vector3(0f, 0.97f, 0.19f), new Vector3(0.36f, 0.14f, 0.04f), 0.02f, 8f);
            }
        }

        private void Pouf(Transform pivot, string shape)
        {
            if (shape == "cube")
            {
                Soft(pivot, Z(0), new Vector3(0f, 0.21f, 0f), new Vector3(0.48f, 0.42f, 0.48f), 0.08f);
                return;
            }
            Turned(pivot, Z(0), Vector3.zero, SoftShapes.RoundedCylinder(0.26f, 0.44f, 0.1f));
        }

        private void Bench(Transform pivot, string style, float length)
        {
            if (style == "wood")
            {
                foreach (var side in new[] { -1f, 1f })
                {
                    Soft(pivot, Z(0), new Vector3(side * (length / 2f - 0.12f), 0.2f, 0f), new Vector3(0.04f, 0.4f, 0.36f), 0.01f);
                }
                for (var i = 0; i < 4; i++)
                {
                    Soft(pivot, Z(0), new Vector3(0f, 0.43f, -0.15f + i * 0.1f), new Vector3(length, 0.035f, 0.08f), 0.01f);
                }
                return;
            }
            foreach (var (x, z) in Corners(length / 2f - 0.06f, 0.14f))
            {
                Turned(pivot, Z(1), new Vector3(x, 0f, z), SoftShapes.RoundedCylinder(0.014f, 0.3f, 0.005f, 10));
            }
            Soft(pivot, Z(0), new Vector3(0f, 0.38f, 0f), new Vector3(length, 0.14f, 0.4f), 0.05f);
        }

        // ---------- Tables ----------

        /// <summary>Rectangular table: "sled" (wood top on metal sled frames), "legs" (all wood, tapered legs), "stone" (stone top, A-frame).</summary>
        private void Table(Transform pivot, string style, float length)
        {
            var depth = length <= 1.2f ? 0.8f : length <= 2f ? 0.9f : length <= 2.4f ? 1.0f : 1.1f;
            var top = Z(0);
            Soft(pivot, top, new Vector3(0f, 0.73f, 0f), new Vector3(length, style == "stone" ? 0.03f : 0.04f, depth), 0.012f);
            if (style == "legs")
            {
                Soft(pivot, top, new Vector3(0f, 0.67f, 0f), new Vector3(length - 0.16f, 0.08f, depth - 0.16f), 0.01f);
                foreach (var (x, z) in Corners(length / 2f - 0.1f, depth / 2f - 0.1f))
                {
                    Turned(pivot, top, new Vector3(x, 0f, z), TaperedLeg(0.71f, 0.032f, 0.02f));
                }
                return;
            }
            var frame = Z(1);
            var frames = length > 2.6f ? new[] { -1f, 0f, 1f } : new[] { -1f, 1f };
            foreach (var f in frames)
            {
                var x = f * (length / 2f - 0.3f);
                if (style == "stone")
                {
                    foreach (var side in new[] { -1f, 1f })
                    {
                        Rod(pivot, frame, new Vector3(x, 0f, side * (depth / 2f - 0.12f)), new Vector3(x, 0.715f, side * 0.08f), 0.018f);
                    }
                    Soft(pivot, frame, new Vector3(x, 0.7f, 0f), new Vector3(0.04f, 0.03f, depth - 0.2f), 0.008f);
                    continue;
                }
                foreach (var side in new[] { -1f, 1f })
                {
                    Soft(pivot, frame, new Vector3(x, 0.355f, side * (depth / 2f - 0.1f)), new Vector3(0.04f, 0.71f, 0.04f), 0.008f);
                }
                Soft(pivot, frame, new Vector3(x, 0.69f, 0f), new Vector3(0.04f, 0.03f, depth - 0.16f), 0.008f);
                Soft(pivot, frame, new Vector3(x, 0.015f, 0f), new Vector3(0.05f, 0.03f, depth - 0.14f), 0.008f);
            }
        }

        /// <summary>Round table on a turned column; radius in metres.</summary>
        private void RoundTable(Transform pivot, float radius)
        {
            var foot = Z(1);
            Turned(pivot, foot, Vector3.zero, SoftShapes.RoundedCylinder(Mathf.Min(0.36f, radius * 0.6f), 0.03f, 0.012f));
            Turned(pivot, foot, Vector3.zero, SoftShapes.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.08f, 0f), new Vector2(0.04f, 0.14f), new Vector2(0.04f, 0.69f), new Vector2(0.1f, 0.71f), new Vector2(0f, 0.71f),
            }, 16));
            Turned(pivot, Z(0), new Vector3(0f, 0.71f, 0f), SoftShapes.RoundedCylinder(radius, 0.04f, 0.014f, 36));
        }

        private void Bistro(Transform pivot, bool square)
        {
            var foot = Z(1);
            Turned(pivot, foot, Vector3.zero, SoftShapes.RoundedCylinder(0.23f, 0.025f, 0.01f));
            Turned(pivot, foot, Vector3.zero, SoftShapes.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.05f, 0f), new Vector2(0.028f, 0.1f), new Vector2(0.028f, 0.71f), new Vector2(0.07f, 0.72f), new Vector2(0f, 0.72f),
            }, 14));
            if (square)
            {
                Soft(pivot, Z(0), new Vector3(0f, 0.735f, 0f), new Vector3(0.68f, 0.03f, 0.68f), 0.012f);
                return;
            }
            Turned(pivot, Z(0), new Vector3(0f, 0.72f, 0f), SoftShapes.RoundedCylinder(0.35f, 0.03f, 0.012f));
        }

        private void HighTable(Transform pivot, bool long_)
        {
            var foot = Z(1);
            if (long_)
            {
                Soft(pivot, Z(0), new Vector3(0f, 1.03f, 0f), new Vector3(1.6f, 0.04f, 0.6f), 0.012f);
                foreach (var side in new[] { -1f, 1f })
                {
                    Soft(pivot, foot, new Vector3(side * 0.6f, 0.5f, 0f), new Vector3(0.05f, 1.0f, 0.05f), 0.01f);
                    Soft(pivot, foot, new Vector3(side * 0.6f, 0.015f, 0f), new Vector3(0.06f, 0.03f, 0.5f), 0.01f);
                }
                Soft(pivot, foot, new Vector3(0f, 0.3f, 0f), new Vector3(1.2f, 0.03f, 0.03f), 0.008f);
                return;
            }
            Turned(pivot, foot, Vector3.zero, SoftShapes.RoundedCylinder(0.25f, 0.03f, 0.012f));
            Turned(pivot, foot, Vector3.zero, SoftShapes.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.06f, 0f), new Vector2(0.03f, 0.12f), new Vector2(0.03f, 1.0f), new Vector2(0.07f, 1.02f), new Vector2(0f, 1.02f),
            }, 14));
            Turned(pivot, Z(0), new Vector3(0f, 1.02f, 0f), SoftShapes.RoundedCylinder(0.32f, 0.03f, 0.012f));
        }

        /// <summary>Restaurant tables with a tailored cloth (zone 0) that falls straight to 30 cm below the top: 2, 4, 6 seats or round.</summary>
        private void Dinner(Transform pivot, string kind, int size)
        {
            var walnut = Surface(SurfaceMaterials.Walnut, Color.white);
            var cloth = Z(0);
            if (kind == "round")
            {
                var radius = size / 200f;
                Turned(pivot, walnut, Vector3.zero, SoftShapes.RoundedCylinder(0.3f, 0.03f, 0.01f));
                Turned(pivot, walnut, Vector3.zero, SoftShapes.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.05f, 0f), new Vector2(0.05f, 0.5f), new Vector2(0f, 0.5f) }, 14));
                Turned(pivot, cloth, Vector3.zero, SoftShapes.Lathe(new[]
                {
                    new Vector2(0f, 0.46f), new Vector2(radius + 0.03f, 0.46f), new Vector2(radius + 0.035f, 0.48f),
                    new Vector2(radius + 0.035f, 0.755f), new Vector2(radius + 0.02f, 0.77f), new Vector2(0f, 0.77f),
                }, 44));
                return;
            }
            var (length, depth) = kind == "2" ? (0.8f, 0.8f) : kind == "4" ? (1.4f, 0.9f) : (2.0f, 1.0f);
            foreach (var (x, z) in Corners(length / 2f - 0.06f, depth / 2f - 0.06f))
            {
                Soft(pivot, walnut, new Vector3(x, 0.24f, z), new Vector3(0.05f, 0.48f, 0.05f), 0.01f);
            }
            Soft(pivot, cloth, new Vector3(0f, 0.62f, 0f), new Vector3(length + 0.06f, 0.3f, depth + 0.06f), 0.02f);
        }

        private void CoffeeTable(Transform pivot, string style)
        {
            switch (style)
            {
                case "round":
                    Turned(pivot, Z(1), Vector3.zero, SoftShapes.RoundedCylinder(0.28f, 0.33f, 0.02f));
                    Turned(pivot, Z(0), new Vector3(0f, 0.33f, 0f), SoftShapes.RoundedCylinder(0.44f, 0.03f, 0.012f, 36));
                    return;
                case "oval":
                    Turned(pivot, Z(1), Vector3.zero, SoftShapes.RoundedCylinder(0.22f, 0.33f, 0.02f), new Vector3(1.8f, 1f, 1f));
                    Turned(pivot, Z(0), new Vector3(0f, 0.33f, 0f), SoftShapes.RoundedCylinder(0.34f, 0.04f, 0.015f, 36), new Vector3(1.8f, 1f, 1f));
                    return;
                case "nesting":
                    foreach (var (x, z, r, h) in new[] { (-0.18f, 0.05f, 0.34f, 0.4f), (0.26f, -0.12f, 0.26f, 0.32f) })
                    {
                        foreach (var k in new[] { 0f, 120f, 240f })
                        {
                            var foot = Quaternion.Euler(0f, k, 0f) * new Vector3(r * 0.8f, 0f, 0f);
                            Rod(pivot, Z(1), new Vector3(x, 0f, z) + foot, new Vector3(x, h, z) + foot * 0.85f, 0.011f);
                        }
                        Turned(pivot, Z(0), new Vector3(x, h, z), SoftShapes.RoundedCylinder(r, 0.025f, 0.01f, 32));
                    }
                    return;
                case "cube":
                    Soft(pivot, Z(0), new Vector3(0f, 0.18f, 0f), new Vector3(0.8f, 0.36f, 0.8f), 0.02f);
                    return;
                default:
                    Soft(pivot, Z(0), new Vector3(0f, 0.36f, 0f), new Vector3(1.2f, 0.04f, 0.62f), 0.015f);
                    Soft(pivot, Z(1), new Vector3(0f, 0.17f, 0f), new Vector3(0.95f, 0.34f, 0.4f), 0.02f);
                    return;
            }
        }

        private void SideTable(Transform pivot, string style)
        {
            switch (style)
            {
                case "cube":
                    foreach (var (x, z) in Corners(0.2f, 0.2f))
                    {
                        Soft(pivot, Z(1), new Vector3(x, 0.24f, z), new Vector3(0.02f, 0.48f, 0.02f), 0.005f);
                    }
                    Soft(pivot, Z(0), new Vector3(0f, 0.49f, 0f), new Vector3(0.44f, 0.03f, 0.44f), 0.008f);
                    Soft(pivot, Z(0), new Vector3(0f, 0.12f, 0f), new Vector3(0.4f, 0.02f, 0.4f), 0.006f);
                    return;
                case "drum":
                    Turned(pivot, Z(0), Vector3.zero, SoftShapes.RoundedCylinder(0.22f, 0.46f, 0.02f));
                    return;
                default:
                    Turned(pivot, Z(1), Vector3.zero, SoftShapes.RoundedCylinder(0.17f, 0.02f, 0.008f));
                    Turned(pivot, Z(1), Vector3.zero, SoftShapes.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.014f, 0f), new Vector2(0.014f, 0.5f), new Vector2(0f, 0.5f) }, 10));
                    Turned(pivot, Z(0), new Vector3(0f, 0.5f, 0f), SoftShapes.RoundedCylinder(0.24f, 0.03f, 0.01f));
                    return;
            }
        }

        private void ConsoleTable(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.78f, 0f), new Vector3(1.4f, 0.04f, 0.36f), 0.01f);
            Soft(pivot, Z(0), new Vector3(0f, 0.2f, 0f), new Vector3(1.3f, 0.025f, 0.3f), 0.008f);
            foreach (var (x, z) in Corners(0.66f, 0.15f))
            {
                Soft(pivot, Z(1), new Vector3(x, 0.38f, z), new Vector3(0.022f, 0.76f, 0.022f), 0.006f);
            }
        }

        private void Desk(Transform pivot, float length)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.735f, 0f), new Vector3(length, 0.03f, 0.75f), 0.01f);
            foreach (var side in new[] { -1f, 1f })
            {
                var x = side * (length / 2f - 0.08f);
                Soft(pivot, Z(1), new Vector3(x, 0.36f, 0f), new Vector3(0.05f, 0.72f, 0.05f), 0.01f);
                Soft(pivot, Z(1), new Vector3(x, 0.015f, 0f), new Vector3(0.06f, 0.03f, 0.68f), 0.01f);
                Soft(pivot, Z(1), new Vector3(x, 0.705f, 0f), new Vector3(0.05f, 0.03f, 0.62f), 0.008f);
            }
            Soft(pivot, Z(1), new Vector3(0f, 0.6f, 0.26f), new Vector3(length - 0.3f, 0.1f, 0.12f), 0.01f);   // cable tray
        }

        // ---------- Storage ----------

        private void Sideboard(Transform pivot, float length)
        {
            const float depth = 0.45f;
            foreach (var (x, z) in Corners(length / 2f - 0.08f, depth / 2f - 0.07f))
            {
                Turned(pivot, Z(1), new Vector3(x, 0f, z), SoftShapes.RoundedCylinder(0.016f, 0.14f, 0.005f, 10));
            }
            Soft(pivot, Z(0), new Vector3(0f, 0.44f, 0f), new Vector3(length, 0.6f, depth), 0.012f);
            var doors = Mathf.RoundToInt(length / 0.6f);
            for (var i = 1; i < doors; i++)
            {
                Soft(pivot, Ink, new Vector3(-length / 2f + i * length / doors, 0.44f, -depth / 2f - 0.001f), new Vector3(0.004f, 0.56f, 0.004f), 0.001f);
            }
            for (var i = 0; i < doors; i++)
            {
                Soft(pivot, Z(1), new Vector3(-length / 2f + (i + 0.5f) * length / doors, 0.66f, -depth / 2f - 0.008f), new Vector3(0.16f, 0.012f, 0.012f), 0.004f);
            }
        }

        private void Shelf(Transform pivot, string style)
        {
            var (width, height, columns, rows) = style switch
            {
                "tall" => (1.0f, 2.0f, 1, 5),
                "grid" => (2.4f, 2.2f, 4, 5),
                _ => (1.8f, 1.2f, 2, 3),
            };
            const float depth = 0.38f;
            var wood = Z(0);
            for (var c = 0; c <= columns; c++)
            {
                Soft(pivot, wood, new Vector3(-width / 2f + c * width / columns, height / 2f, 0f), new Vector3(0.03f, height, depth), 0.006f);
            }
            for (var r = 0; r <= rows; r++)
            {
                var y = 0.05f + r * (height - 0.08f) / rows;
                Soft(pivot, wood, new Vector3(0f, y, 0f), new Vector3(width, 0.03f, depth), 0.006f);
                if (r == rows)
                {
                    continue;
                }
                for (var c = 0; c < columns; c++)
                {
                    var x = -width / 2f + (c + 0.5f) * width / columns;
                    ShelfFill(pivot, x, y + 0.015f, width / columns - 0.05f, (r * 7 + c * 3) % 5);
                }
            }
        }

        /// <summary>
        /// What stands in a shelf compartment, built here in a few shared colours (so a whole shelf wall costs a handful of
        /// draw calls): rows of book spines, sometimes a lying stack or a vase.
        /// </summary>
        private void ShelfFill(Transform pivot, float x, float y, float room, int pattern)
        {
            var spines = new[] { Lit(new Color(0.55f, 0.62f, 0.52f), 0.3f), Lit(new Color(0.78f, 0.45f, 0.32f), 0.3f), Lit(new Color(0.92f, 0.89f, 0.82f), 0.3f), Lit(new Color(0.2f, 0.22f, 0.26f), 0.3f) };
            if (pattern == 0)
            {
                Turned(pivot, Lit(new Color(0.93f, 0.91f, 0.86f), 0.5f), new Vector3(x, y, 0f), SoftShapes.Lathe(new[]
                {
                    new Vector2(0f, 0f), new Vector2(0.05f, 0f), new Vector2(0.08f, 0.1f), new Vector2(0.05f, 0.2f), new Vector2(0.035f, 0.21f), new Vector2(0f, 0.2f),
                }, 16));
                return;
            }
            var left = x - room / 2f + 0.03f;
            var count = pattern == 3 ? 5 : 9;
            for (var i = 0; i < count; i++)
            {
                var height = 0.2f + ((i * 37 + pattern * 11) % 7) * 0.012f;
                var thickness = 0.03f + ((i * 13 + pattern) % 3) * 0.008f;
                Box(pivot, spines[(i + pattern) % spines.Length], new Vector3(left + thickness / 2f, y + height / 2f, 0f), new Vector3(thickness, height, 0.22f));
                left += thickness + 0.004f;
            }
            if (pattern == 3)
            {
                for (var i = 0; i < 3; i++)
                {
                    Box(pivot, spines[i], new Vector3(x + room / 4f, y + 0.02f + i * 0.035f, 0f), new Vector3(0.24f - i * 0.02f, 0.03f, 0.18f));
                }
            }
        }

        private void TallCabinet(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 1.05f, 0f), new Vector3(1.0f, 2.1f, 0.5f), 0.015f);
            Soft(pivot, Ink, new Vector3(0f, 1.05f, -0.251f), new Vector3(0.004f, 2.0f, 0.004f), 0.001f);
            foreach (var side in new[] { -1f, 1f })
            {
                Soft(pivot, Z(1), new Vector3(side * 0.05f, 1.05f, -0.26f), new Vector3(0.015f, 0.4f, 0.015f), 0.005f);
            }
        }

        private void Vitrine(Transform pivot)
        {
            var frame = Z(0);
            const float width = 1.0f, depth = 0.42f, height = 1.9f;
            foreach (var (x, z) in Corners(width / 2f - 0.015f, depth / 2f - 0.015f))
            {
                Soft(pivot, frame, new Vector3(x, height / 2f, z), new Vector3(0.03f, height, 0.03f), 0.006f);
            }
            foreach (var y in new[] { 0.06f, 0.5f, 0.95f, 1.4f, height - 0.015f })
            {
                Soft(pivot, frame, new Vector3(0f, y, 0f), new Vector3(width, 0.03f, depth), 0.006f);
                if (y < height - 0.1f && _spawnModel?.Invoke(y < 0.3f ? "ph-brass_vase_02" : "ph-ceramic_vase_03", pivot) is { } vase)
                {
                    vase.transform.localPosition = new Vector3(0.15f, y + 0.015f, 0f);
                }
            }
            if (_glass != null)
            {
                Box(pivot, _glass, new Vector3(0f, height / 2f, -depth / 2f + 0.01f), new Vector3(width - 0.03f, height - 0.03f, 0.008f));
                Box(pivot, _glass, new Vector3(0f, height / 2f, depth / 2f - 0.01f), new Vector3(width - 0.03f, height - 0.03f, 0.008f));
            }
        }

        private void BarCabinet(Transform pivot)
        {
            foreach (var (x, z) in Corners(0.42f, 0.18f))
            {
                Turned(pivot, Z(1), new Vector3(x, 0f, z), TaperedLeg(0.18f, 0.02f, 0.012f));
            }
            Soft(pivot, Z(0), new Vector3(0f, 0.62f, 0f), new Vector3(1.0f, 0.88f, 0.48f), 0.015f);
            Soft(pivot, Ink, new Vector3(0f, 0.62f, -0.241f), new Vector3(0.004f, 0.82f, 0.004f), 0.001f);
            Soft(pivot, Z(1), new Vector3(0f, 1.075f, 0f), new Vector3(1.0f, 0.02f, 0.48f), 0.004f);
            if (_spawnModel?.Invoke("ph-wine_bottles_01", pivot) is { } bottles)
            {
                bottles.transform.localPosition = new Vector3(-0.2f, 1.085f, 0f);
            }
        }

        // ---------- Lights ----------

        private void FloorLamp(Transform pivot, string style)
        {
            var metal = Z(0);
            var glow = Glow(new Color(1f, 0.86f, 0.62f), 2.4f);
            switch (style)
            {
                case "tripod":
                    foreach (var k in new[] { 0f, 120f, 240f })
                    {
                        var foot = Quaternion.Euler(0f, k, 0f) * new Vector3(0.28f, 0f, 0f);
                        Rod(pivot, Surface(SurfaceMaterials.Walnut, Color.white), foot, new Vector3(0f, 1.25f, 0f), 0.013f);
                    }
                    Turned(pivot, Lit(new Color(0.95f, 0.93f, 0.88f), 0.2f), new Vector3(0f, 1.2f, 0f), SoftShapes.Lathe(new[]
                    {
                        new Vector2(0.24f, 0f), new Vector2(0.18f, 0.32f), new Vector2(0.17f, 0.32f), new Vector2(0.23f, 0f),
                    }, 28));
                    Turned(pivot, glow, new Vector3(0f, 1.25f, 0f), SoftShapes.RoundedCylinder(0.05f, 0.1f, 0.04f));
                    return;
                case "globe":
                    Turned(pivot, Z(1), Vector3.zero, SoftShapes.RoundedCylinder(0.16f, 0.04f, 0.015f));
                    Rod(pivot, metal, new Vector3(0f, 0.04f, 0f), new Vector3(0f, 1.35f, 0f), 0.011f);
                    Turned(pivot, glow, new Vector3(0f, 1.35f, 0f), SoftShapes.RoundedCylinder(0.17f, 0.34f, 0.169f, 24));
                    return;
                case "column":
                    Turned(pivot, Z(1), Vector3.zero, SoftShapes.RoundedCylinder(0.14f, 0.04f, 0.015f));
                    Turned(pivot, Lit(new Color(0.97f, 0.95f, 0.9f), 0.2f), new Vector3(0f, 0.04f, 0f), SoftShapes.RoundedCylinder(0.09f, 1.3f, 0.04f));
                    Turned(pivot, glow, new Vector3(0f, 0.3f, 0f), SoftShapes.RoundedCylinder(0.085f, 1.0f, 0.03f));
                    return;
                default:
                {
                    // Arc lamp: marble foot behind, brass arc over the seat in front, dome shade.
                    Turned(pivot, Z(1), new Vector3(0f, 0f, 0.35f), SoftShapes.RoundedCylinder(0.17f, 0.07f, 0.02f));
                    var previous = new Vector3(0f, 0.07f, 0.35f);
                    var points = new List<Vector3> { new(0f, 1.2f, 0.35f) };
                    for (var k = 1; k <= 7; k++)
                    {
                        var a = Mathf.PI / 2f * k / 7f;
                        points.Add(new Vector3(0f, 1.2f + 0.7f * Mathf.Sin(a), 0.35f - 0.7f * (1f - Mathf.Cos(a))));
                    }
                    points.Add(new Vector3(0f, 1.75f, -0.45f));
                    foreach (var point in points)
                    {
                        Rod(pivot, metal, previous, point, 0.012f);
                        previous = point;
                    }
                    Turned(pivot, metal, new Vector3(0f, 1.58f, -0.45f), SoftShapes.Lathe(new[]
                    {
                        new Vector2(0.2f, 0f), new Vector2(0.19f, 0.08f), new Vector2(0.12f, 0.16f), new Vector2(0.03f, 0.19f), new Vector2(0f, 0.19f),
                    }, 28));
                    Turned(pivot, glow, new Vector3(0f, 1.585f, -0.45f), SoftShapes.RoundedCylinder(0.17f, 0.01f, 0.004f));
                    return;
                }
            }
        }

        private void TableLamp(Transform pivot, string style)
        {
            var glow = Glow(new Color(1f, 0.86f, 0.62f), 2f);
            switch (style)
            {
                case "mushroom":
                    Turned(pivot, Z(0), Vector3.zero, SoftShapes.Lathe(new[]
                    {
                        new Vector2(0f, 0f), new Vector2(0.09f, 0f), new Vector2(0.03f, 0.05f), new Vector2(0.025f, 0.26f), new Vector2(0.17f, 0.3f),
                        new Vector2(0.15f, 0.38f), new Vector2(0f, 0.4f),
                    }, 28));
                    Turned(pivot, glow, new Vector3(0f, 0.27f, 0f), SoftShapes.RoundedCylinder(0.12f, 0.02f, 0.008f));
                    return;
                case "globe":
                    Turned(pivot, Z(1), Vector3.zero, SoftShapes.RoundedCylinder(0.08f, 0.03f, 0.012f));
                    Turned(pivot, glow, new Vector3(0f, 0.03f, 0f), SoftShapes.RoundedCylinder(0.12f, 0.24f, 0.119f, 24));
                    return;
                default:
                    Turned(pivot, Z(1), Vector3.zero, SoftShapes.RoundedCylinder(0.08f, 0.02f, 0.008f));
                    Rod(pivot, Z(1), new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0.3f, 0f), 0.008f);
                    Turned(pivot, Z(0), new Vector3(0f, 0.24f, 0f), SoftShapes.Lathe(new[]
                    {
                        new Vector2(0.14f, 0f), new Vector2(0.1f, 0.18f), new Vector2(0.095f, 0.18f), new Vector2(0.135f, 0f),
                    }, 28));
                    Turned(pivot, glow, new Vector3(0f, 0.25f, 0f), SoftShapes.RoundedCylinder(0.04f, 0.06f, 0.02f));
                    return;
            }
        }

        /// <summary>Pendant lamps: the shade hangs at the item's height, a short cord above it (the rooms are open to the camera).</summary>
        private void PendantLamp(Transform pivot, string style)
        {
            var shade = Z(0);
            var glow = Glow(new Color(1f, 0.84f, 0.6f), 2.2f);
            var cord = Ink;
            switch (style)
            {
                case "globe":
                    Rod(pivot, cord, new Vector3(0f, 0.3f, 0f), new Vector3(0f, 0.9f, 0f), 0.004f);
                    Turned(pivot, shade, new Vector3(0f, 0.28f, 0f), SoftShapes.RoundedCylinder(0.03f, 0.04f, 0.01f));
                    Turned(pivot, glow, Vector3.zero, SoftShapes.RoundedCylinder(0.15f, 0.3f, 0.149f, 24));
                    return;
                case "linear":
                    foreach (var side in new[] { -1f, 1f })
                    {
                        Rod(pivot, cord, new Vector3(side * 0.5f, 0.06f, 0f), new Vector3(side * 0.5f, 0.8f, 0f), 0.003f);
                    }
                    Soft(pivot, shade, new Vector3(0f, 0.03f, 0f), new Vector3(1.4f, 0.06f, 0.08f), 0.015f);
                    Soft(pivot, glow, new Vector3(0f, -0.001f, 0f), new Vector3(1.34f, 0.01f, 0.05f), 0.004f);
                    return;
                case "cluster":
                    for (var i = 0; i < 5; i++)
                    {
                        var offset = Quaternion.Euler(0f, i * 72f, 0f) * new Vector3(i == 0 ? 0f : 0.22f, 0f, 0f);
                        var y = i % 2 == 0 ? 0f : 0.18f;
                        Rod(pivot, cord, offset + new Vector3(0f, y + 0.14f, 0f), new Vector3(0f, 0.9f, 0f), 0.003f);
                        Turned(pivot, glow, offset + new Vector3(0f, y, 0f), SoftShapes.RoundedCylinder(0.07f, 0.14f, 0.069f, 18));
                    }
                    return;
                default:
                    Rod(pivot, cord, new Vector3(0f, 0.22f, 0f), new Vector3(0f, 0.9f, 0f), 0.004f);
                    Turned(pivot, shade, Vector3.zero, SoftShapes.Lathe(new[]
                    {
                        new Vector2(0.24f, 0f), new Vector2(0.23f, 0.05f), new Vector2(0.15f, 0.17f), new Vector2(0.04f, 0.22f), new Vector2(0f, 0.22f),
                    }, 32));
                    Turned(pivot, glow, new Vector3(0f, 0.005f, 0f), SoftShapes.RoundedCylinder(0.21f, 0.01f, 0.004f, 32));
                    return;
            }
        }

        // ---------- Plants ----------

        private void Plant(Transform pivot, string type, string pot)
        {
            var (radius, height) = pot switch
            {
                "bowl" => (0.4f, 0.36f),
                "tapered" => (0.28f, 0.7f),
                _ => (0.3f, 0.58f),
            };
            var bottom = pot == "tapered" ? radius * 0.6f : pot == "bowl" ? radius * 0.55f : radius;
            Turned(pivot, Z(0), Vector3.zero, SoftShapes.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(bottom - 0.02f, 0f), new Vector2(bottom, 0.02f), new Vector2(radius, height - 0.02f),
                new Vector2(radius - 0.02f, height), new Vector2(radius - 0.035f, height - 0.03f), new Vector2(0f, height - 0.03f),
            }, 32));
            Turned(pivot, Lit(new Color(0.16f, 0.12f, 0.09f), 0.05f), new Vector3(0f, height - 0.04f, 0f), SoftShapes.RoundedCylinder(radius - 0.04f, 0.015f, 0.005f));
            var model = type switch
            {
                "tall" => "ph-pachira_aquatica_01",
                "leafy" => "ph-potted_plant_01",
                _ => "ph-potted_plant_02",
            };
            var plant = _spawnModel?.Invoke(model, pivot);
            if (plant == null)
            {
                Sphere(pivot, Lit(new Color(0.25f, 0.45f, 0.22f), 0.1f), new Vector3(0f, height + 0.4f, 0f), Vector3.one * 0.7f);
                return;
            }
            // The model's own pot sinks right into ours (only the plant shows above the rim).
            var scale = type == "bush" ? radius / 0.3f * 1.2f : 1f;
            plant.transform.localScale = Vector3.one * scale;
            var ownPot = type switch
            {
                "tall" => 0.3f,
                "leafy" => 0.4f,
                _ => 0.3f,
            };
            plant.transform.localPosition = new Vector3(0f, height - 0.03f - ownPot * scale, 0f);
        }

        private void PlanterBench(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.22f, 0f), new Vector3(2.0f, 0.44f, 0.5f), 0.012f);
            Turned(pivot, Lit(new Color(0.16f, 0.12f, 0.09f), 0.05f), new Vector3(0f, 0.44f, 0f), SoftShapes.RoundedCylinder(0.2f, 0.005f, 0.002f), new Vector3(4.6f, 1f, 1.1f));
            for (var i = 0; i < 4 && _spawnModel != null; i++)
            {
                if (_spawnModel(i % 2 == 0 ? "ph-potted_plant_04" : "ph-fern_02", pivot) is { } plant)
                {
                    plant.transform.localPosition = new Vector3(-0.72f + i * 0.48f, 0.3f, 0f);
                    plant.transform.localScale = Vector3.one * 1.6f;
                }
            }
        }

        // ---------- Work ----------

        private void Acoustic(Transform pivot, bool curved)
        {
            var felt = Z(0);
            var panels = curved ? new[] { (-0.75f, 20f), (0f, 0f), (0.75f, -20f) } : new[] { (0f, 0f) };
            foreach (var (x, turn) in panels)
            {
                var z = curved ? Mathf.Abs(x) * 0.3f : 0f;
                Soft(pivot, felt, new Vector3(x, 0.93f, z), new Vector3(curved ? 0.78f : 1.6f, 1.5f, 0.06f), 0.03f, 0f, turn);
            }
            foreach (var side in curved ? new[] { -1f, 0f, 1f } : new[] { -1f, 1f })
            {
                var x = side * (curved ? 0.75f : 0.62f);
                Soft(pivot, BlackSteel, new Vector3(x, 0.015f, curved ? Mathf.Abs(x) * 0.3f : 0f), new Vector3(0.05f, 0.03f, 0.42f), 0.008f);
                Soft(pivot, BlackSteel, new Vector3(x, 0.1f, curved ? Mathf.Abs(x) * 0.3f : 0f), new Vector3(0.03f, 0.18f, 0.03f), 0.006f);
            }
        }

        private void Whiteboard(Transform pivot)
        {
            var aluminium = Z(0);
            Soft(pivot, aluminium, new Vector3(0f, 1.3f, 0f), new Vector3(1.6f, 1.0f, 0.04f), 0.012f);
            Soft(pivot, Lit(new Color(0.97f, 0.97f, 0.98f), 0.85f), new Vector3(0f, 1.3f, -0.015f), new Vector3(1.52f, 0.92f, 0.02f), 0.005f);
            foreach (var side in new[] { -1f, 1f })
            {
                Soft(pivot, aluminium, new Vector3(side * 0.74f, 0.45f, 0f), new Vector3(0.035f, 0.9f, 0.035f), 0.008f);
                Soft(pivot, aluminium, new Vector3(side * 0.74f, 0.05f, 0f), new Vector3(0.05f, 0.03f, 0.5f), 0.008f);
            }
            Soft(pivot, Lit(new Color(0.2f, 0.2f, 0.22f), 0.3f), new Vector3(-0.3f, 0.81f, -0.05f), new Vector3(0.5f, 0.02f, 0.06f), 0.006f);
        }

        private void Screen(Transform pivot)
        {
            Soft(pivot, Z(0), new Vector3(0f, 0.015f, 0f), new Vector3(0.7f, 0.03f, 0.5f), 0.01f);
            Soft(pivot, Z(0), new Vector3(0f, 0.7f, 0.05f), new Vector3(0.06f, 1.4f, 0.06f), 0.01f);
            Soft(pivot, Ink, new Vector3(0f, 1.35f, 0f), new Vector3(1.65f, 0.95f, 0.05f), 0.01f);
            Soft(pivot, Glow(new Color(0.25f, 0.45f, 0.7f), 0.6f), new Vector3(0f, 1.35f, -0.026f), new Vector3(1.6f, 0.9f, 0.004f), 0.002f);
        }

        // ---------- Decor ----------

        private void Vase(Transform pivot, string style)
        {
            var profile = style switch
            {
                "bottle" => new[] { new Vector2(0f, 0f), new Vector2(0.06f, 0f), new Vector2(0.08f, 0.1f), new Vector2(0.07f, 0.2f), new Vector2(0.025f, 0.28f), new Vector2(0.025f, 0.34f), new Vector2(0f, 0.34f) },
                "tall" => new[] { new Vector2(0f, 0f), new Vector2(0.06f, 0f), new Vector2(0.075f, 0.3f), new Vector2(0.06f, 0.42f), new Vector2(0.05f, 0.42f), new Vector2(0f, 0.41f) },
                "bowl" => new[] { new Vector2(0f, 0f), new Vector2(0.08f, 0f), new Vector2(0.17f, 0.08f), new Vector2(0.16f, 0.09f), new Vector2(0f, 0.03f) },
                _ => new[] { new Vector2(0f, 0f), new Vector2(0.06f, 0f), new Vector2(0.12f, 0.1f), new Vector2(0.08f, 0.2f), new Vector2(0.06f, 0.21f), new Vector2(0f, 0.2f) },
            };
            Turned(pivot, Z(0), Vector3.zero, SoftShapes.Lathe(profile, 24));
            if (style == "tall" && _spawnModel?.Invoke("ph-fern_02", pivot) is { } fern)
            {
                fern.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            }
        }

        private void Candles(Transform pivot)
        {
            var wax = Lit(new Color(0.96f, 0.94f, 0.88f), 0.3f);
            var flame = Glow(new Color(1f, 0.75f, 0.4f), 3f);
            foreach (var (x, z, h) in new[] { (-0.07f, 0f, 0.22f), (0.02f, 0.05f, 0.3f), (0.08f, -0.04f, 0.18f) })
            {
                Turned(pivot, Z(0), new Vector3(x, 0f, z), SoftShapes.RoundedCylinder(0.03f, 0.03f, 0.01f, 16));
                Turned(pivot, wax, new Vector3(x, 0.03f, z), SoftShapes.RoundedCylinder(0.012f, h - 0.03f, 0.004f, 12));
                Turned(pivot, flame, new Vector3(x, h, z), SoftShapes.RoundedCylinder(0.006f, 0.02f, 0.005f, 8));
            }
        }

        private void Books(Transform pivot)
        {
            var pages = Lit(new Color(0.94f, 0.92f, 0.86f), 0.2f);
            var covers = new[] { Z(0), Lit(new Color(0.9f, 0.88f, 0.84f), 0.3f), Lit(new Color(0.15f, 0.15f, 0.17f), 0.3f), Z(0) };
            var y = 0f;
            for (var i = 0; i < 4; i++)
            {
                var (w, d, h) = (0.26f - i * 0.02f, 0.2f - i * 0.012f, 0.03f + (i % 2) * 0.008f);
                Soft(pivot, covers[i], new Vector3(0f, y + h / 2f, 0f), new Vector3(w, h, d), 0.004f, 0f, i * 7f - 8f);
                Soft(pivot, pages, new Vector3(0.004f, y + h / 2f, 0f), new Vector3(w - 0.012f, h - 0.008f, d + 0.001f), 0.002f, 0f, i * 7f - 8f);
                y += h;
            }
        }

        private void Tray(Transform pivot)
        {
            Turned(pivot, Z(0), Vector3.zero, SoftShapes.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.17f, 0f), new Vector2(0.18f, 0.025f), new Vector2(0.175f, 0.025f), new Vector2(0.165f, 0.006f), new Vector2(0f, 0.006f),
            }, 32));
            foreach (var (x, z) in new[] { (-0.06f, 0.03f), (0.07f, -0.02f) })
            {
                Turned(pivot, _glass ?? Lit(Color.white, 0.9f), new Vector3(x, 0.006f, z), SoftShapes.Lathe(new[]
                {
                    new Vector2(0f, 0f), new Vector2(0.03f, 0f), new Vector2(0.004f, 0.01f), new Vector2(0.004f, 0.08f), new Vector2(0.035f, 0.12f),
                    new Vector2(0.038f, 0.17f), new Vector2(0.034f, 0.17f), new Vector2(0f, 0.13f),
                }, 16));
            }
        }

        private void ModernRug(Transform pivot, string itemId)
        {
            if (itemId.Contains("round"))
            {
                var radius = itemId.EndsWith("300") ? 1.5f : 1.0f;
                Turned(pivot, Z(1), Vector3.zero, SoftShapes.RoundedCylinder(radius, 0.012f, 0.005f, 48));
                Turned(pivot, Z(0), new Vector3(0f, 0.001f, 0f), SoftShapes.RoundedCylinder(radius - 0.08f, 0.012f, 0.005f, 48));
                return;
            }
            var (w, d) = itemId.EndsWith("300x200") ? (3f, 2f) : (2.5f, 1.7f);
            Soft(pivot, Z(1), new Vector3(0f, 0.006f, 0f), new Vector3(w, 0.012f, d), 0.005f);
            Soft(pivot, Z(0), new Vector3(0f, 0.007f, 0f), new Vector3(w - 0.16f, 0.012f, d - 0.16f), 0.005f);
        }
    }
}
