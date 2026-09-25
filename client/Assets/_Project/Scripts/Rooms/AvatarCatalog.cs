using System;
using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>
    /// Avatar figures (Kenney Mini Characters, CC0) and their animator.
    /// Until avatars can be customised, each user gets a stable figure derived from the user id.
    /// Asset: Assets/_Project/Settings/AvatarCatalog.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Reconnect/Avatar Catalog", fileName = "AvatarCatalog")]
    public sealed class AvatarCatalog : ScriptableObject
    {
        public GameObject[] characters = Array.Empty<GameObject>();
        public RuntimeAnimatorController animator;
        public float scale = 1.25f;

        public GameObject CharacterFor(Guid userId) =>
            characters.Length == 0 ? null : characters[(userId.GetHashCode() & int.MaxValue) % characters.Length];
    }
}
