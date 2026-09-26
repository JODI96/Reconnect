using System;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>Background music per room theme (CC0 tracks, Assets/ThirdParty/Music). Asset: Settings/MusicCatalog.asset.</summary>
    [CreateAssetMenu(menuName = "Reconnect/Music Catalog", fileName = "MusicCatalog")]
    public sealed class MusicCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string theme;
            public AudioClip clip;
        }

        public Entry[] entries = Array.Empty<Entry>();
        public AudioClip fallback;

        public AudioClip For(string theme) =>
            Array.Find(entries, e => string.Equals(e.theme, theme, StringComparison.OrdinalIgnoreCase))?.clip ?? fallback;
    }
}
