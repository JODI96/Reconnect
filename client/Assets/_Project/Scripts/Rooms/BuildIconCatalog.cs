using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Picture of every buildable item for the build editor's catalog, rendered by the project setup from the real
    /// models (BuildCatalogGenerator). Asset: Assets/_Project/Settings/BuildIcons.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Reconnect/Build Icons", fileName = "BuildIcons")]
    public sealed class BuildIconCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string itemId;
            public Texture2D icon;
        }

        public List<Entry> items = new();

        private Dictionary<string, Texture2D> _lookup;

        public Texture2D Find(string itemId)
        {
            if (_lookup == null || _lookup.Count != items.Count)
            {
                _lookup = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in items)
                {
                    _lookup[entry.itemId] = entry.icon;
                }
            }
            return itemId != null && _lookup.TryGetValue(itemId, out var icon) ? icon : null;
        }
    }
}
