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

        /// <summary>Material templates for figures made in the character creator (see <see cref="AvatarAssembler"/>).</summary>
        public AvatarMaterials wardrobe = new();

        /// <summary>Walk animation per walk style (<see cref="Reconnect.Contracts.Avatars.Wardrobe.WalkStyles"/>), one override each.</summary>
        public AnimatorOverrideController[] walkStyles = Array.Empty<AnimatorOverrideController>();

        public RuntimeAnimatorController ControllerFor(string walkStyle) =>
            (RuntimeAnimatorController)Array.Find(walkStyles, o => o != null && o.name == "Walk " + walkStyle) ?? animator;

        public GameObject CharacterFor(Guid userId) =>
            characters.Length == 0 ? null : characters[(userId.GetHashCode() & int.MaxValue) % characters.Length];
    }
}
