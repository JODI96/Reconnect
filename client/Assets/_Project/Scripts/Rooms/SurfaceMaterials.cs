using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Real materials for our own furniture (Poly Haven textures, CC0, tools/fetch_textures.py): wood veneers, leathers,
    /// and grey-scale fabrics that are tinted per piece (velvet, bouclé, linen). Meshes from <see cref="SoftShapes"/> have
    /// UVs in metres; <see cref="Entry.metresPerTile"/> sets the material's tiling.
    /// Asset: Assets/_Project/Settings/SurfaceMaterials.asset, built by ProjectSetup.
    /// </summary>
    [CreateAssetMenu(menuName = "Reconnect/Surface Materials", fileName = "SurfaceMaterials")]
    public sealed class SurfaceMaterials : ScriptableObject
    {
        public const string Walnut = "walnut";
        public const string Oak = "oak";
        public const string WhiteOak = "whiteoak";
        public const string Cognac = "cognac";
        public const string CreamLeather = "cream";
        public const string Velvet = "velvet";
        public const string Boucle = "boucle";
        public const string Linen = "linen";
        public const string Marble = "marble";
        public const string BlackMarble = "blackmarble";
        public const string Travertine = "travertine";

        [Serializable]
        public sealed class Entry
        {
            public string name;
            public Material material;

            [Tooltip("Fabrics: grey texture, the piece of furniture gives the colour.")]
            public bool tintable;
        }

        public List<Entry> entries = new();

        public Entry Find(string name) => entries.Find(e => string.Equals(e.name, name, StringComparison.OrdinalIgnoreCase));
    }
}
