using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Generates floor textures at runtime (no image assets needed): wooden planks, parquet,
    /// checkered tiles, marble, concrete and terrazzo. One texture covers <see cref="MetersPerTexture"/> metres
    /// and is tiled across the room floor.
    /// </summary>
    public static class FloorTextures
    {
        public const float MetersPerTexture = 2f;
        private const int Size = 512;

        public static Texture2D Create(FloorPattern pattern, Color a, Color b)
        {
            var pixels = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var u = x / (float)Size;
                var v = y / (float)Size;
                pixels[y * Size + x] = pattern switch
                {
                    FloorPattern.Planks => Planks(u, v, a, b),
                    FloorPattern.Parquet => Parquet(u, v, a, b),
                    FloorPattern.Checker => Checker(u, v, a, b),
                    FloorPattern.Marble => Marble(u, v, a, b),
                    FloorPattern.Terrazzo => Terrazzo(u, v, a, b),
                    _ => Concrete(u, v, a, b),
                };
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: true)
            {
                name = "Floor " + pattern,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 8,
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return texture;
        }

        /// <summary>8 planks per texture (25 cm each), staggered joints, grain and colour variation.</summary>
        private static Color Planks(float u, float v, Color a, Color b)
        {
            const int planks = 8;
            var row = Mathf.FloorToInt(v * planks);
            var inRow = v * planks - row;
            var offset = Hash(row) * 0.7f;
            var along = Mathf.Repeat(u + offset, 1f) * 2f;   // two plank lengths per texture
            var plank = Mathf.FloorToInt(along);
            var seed = Hash(row * 13 + plank * 7);

            var color = Color.Lerp(a, b, seed);
            color *= 0.92f + 0.08f * Mathf.PerlinNoise(u * 40f + seed * 10f, v * 4f + row);   // grain
            if (inRow < 0.04f || inRow > 0.97f || Mathf.Abs(along - Mathf.Round(along)) < 0.006f)
            {
                color *= 0.55f;   // gaps between planks
            }
            return color;
        }

        /// <summary>Herringbone-like basket weave: 4×4 squares with strips alternating direction.</summary>
        private static Color Parquet(float u, float v, Color a, Color b)
        {
            const int squares = 4;
            var sx = Mathf.FloorToInt(u * squares);
            var sy = Mathf.FloorToInt(v * squares);
            var lu = u * squares - sx;
            var lv = v * squares - sy;
            var horizontal = (sx + sy) % 2 == 0;
            var stripPos = horizontal ? lv : lu;
            var strip = Mathf.FloorToInt(stripPos * 3f);
            var seed = Hash(sx * 31 + sy * 17 + strip);

            var color = Color.Lerp(a, b, seed * 0.8f);
            color *= 0.9f + 0.1f * Mathf.PerlinNoise((horizontal ? u * 60f : v * 60f) + seed * 5f, strip);
            var inStrip = stripPos * 3f - strip;
            if (inStrip < 0.05f || lu < 0.012f || lv < 0.012f)
            {
                color *= 0.6f;
            }
            return color;
        }

        /// <summary>Classic café tiles: 4×4 checker per texture (50 cm tiles) with thin grout.</summary>
        private static Color Checker(float u, float v, Color a, Color b)
        {
            const int tiles = 4;
            var tx = Mathf.FloorToInt(u * tiles);
            var ty = Mathf.FloorToInt(v * tiles);
            var color = (tx + ty) % 2 == 0 ? a : b;
            var lu = u * tiles - tx;
            var lv = v * tiles - ty;
            if (lu < 0.02f || lv < 0.02f)
            {
                color = Color.Lerp(color, new Color(0.55f, 0.55f, 0.55f), 0.6f);
            }
            return color * (0.97f + 0.03f * Mathf.PerlinNoise(u * 30f, v * 30f));
        }

        /// <summary>Polished marble slabs (1 m) with soft veins.</summary>
        private static Color Marble(float u, float v, Color a, Color b)
        {
            var turbulence = Mathf.PerlinNoise(u * 6f, v * 6f) * 2f + Mathf.PerlinNoise(u * 18f, v * 18f) * 0.6f;
            var vein = Mathf.Abs(Mathf.Sin((u * 3f + v * 2f + turbulence) * Mathf.PI * 2f));
            var color = Color.Lerp(b, a, Mathf.SmoothStep(0f, 1f, Mathf.Pow(vein, 0.25f)));
            var lu = Mathf.Repeat(u * 2f, 1f);
            var lv = Mathf.Repeat(v * 2f, 1f);
            if (lu < 0.006f || lv < 0.006f)
            {
                color *= 0.8f;   // slab joints
            }
            return color;
        }

        /// <summary>Polished dark terrazzo: stone chips in three sizes, 1 m slabs with fine joints.</summary>
        private static Color Terrazzo(float u, float v, Color a, Color b)
        {
            var color = Color.Lerp(a, b, Mathf.PerlinNoise(u * 5f, v * 5f) * 0.6f);
            foreach (var (cells, size, tint) in new[] { (90, 0.28f, 0.55f), (45, 0.22f, 0.35f), (160, 0.3f, 0.75f) })
            {
                var cx = Mathf.FloorToInt(u * cells);
                var cy = Mathf.FloorToInt(v * cells);
                var seed = Hash(cx * 73 + cy * 151 + cells);
                if (seed > 0.55f)
                {
                    continue;   // not every cell has a chip
                }
                var centre = new Vector2(Hash(cx * 11 + cy * 7 + cells), Hash(cx * 5 + cy * 13 + cells)) * 0.6f + new Vector2(0.2f, 0.2f);
                var local = new Vector2(u * cells - cx, v * cells - cy);
                if ((local - centre).sqrMagnitude < size * size * (0.5f + seed))
                {
                    var shade = Hash(cx * 29 + cy * 83 + cells * 3);
                    var chip = seed < 0.06f ? new Color(0.3f, 0.66f, 0.64f) : Color.Lerp(new Color(0.22f, 0.22f, 0.24f), new Color(0.97f, 0.96f, 0.94f), shade);
                    color = Color.Lerp(color, chip, tint);
                }
            }
            if (Mathf.Repeat(u * 2f, 1f) < 0.003f || Mathf.Repeat(v * 2f, 1f) < 0.003f)
            {
                color *= 0.7f;
            }
            return color;
        }

        /// <summary>Smooth concrete with subtle blotches and 1 m joints.</summary>
        private static Color Concrete(float u, float v, Color a, Color b)
        {
            var noise = Mathf.PerlinNoise(u * 8f, v * 8f) * 0.6f + Mathf.PerlinNoise(u * 40f, v * 40f) * 0.4f;
            var color = Color.Lerp(a, b, noise);
            if (Mathf.Repeat(u * 2f, 1f) < 0.004f || Mathf.Repeat(v * 2f, 1f) < 0.004f)
            {
                color *= 0.75f;
            }
            return color;
        }

        private static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / (float)0x7fffffff;
        }
    }
}
