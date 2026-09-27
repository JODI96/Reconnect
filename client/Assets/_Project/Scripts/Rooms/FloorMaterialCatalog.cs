using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Real (PBR) floors per room theme – Poly Haven textures (CC0, tools/fetch_textures.py) with colour, normal and
    /// smoothness maps. Themes without an entry keep their procedural floor (FloorTextures).
    /// Asset: Assets/_Project/Settings/FloorMaterials.asset, built by ProjectSetup.
    /// </summary>
    [CreateAssetMenu(menuName = "Reconnect/Floor Materials", fileName = "FloorMaterials")]
    public sealed class FloorMaterialCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string theme;
            public Material material;

            [Tooltip("How many metres one repeat of the texture covers.")]
            public float metresPerTile = 2f;
        }

        public List<Entry> entries = new();

        public Entry For(string theme) => entries.Find(e => string.Equals(e.theme, theme, StringComparison.OrdinalIgnoreCase));
    }
}
