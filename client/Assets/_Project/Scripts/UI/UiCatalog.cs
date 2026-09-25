using UnityEngine;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI
{
    /// <summary>All UI templates in one place. Asset: Assets/_Project/Settings/UiCatalog.asset.</summary>
    [CreateAssetMenu(menuName = "Reconnect/UI Catalog", fileName = "UiCatalog")]
    public sealed class UiCatalog : ScriptableObject
    {
        public StyleSheet theme;
        public VisualTreeAsset login;
        public VisualTreeAsset register;
        public VisualTreeAsset roomList;
        public VisualTreeAsset roomListItem;
        public VisualTreeAsset roomDetail;
        public VisualTreeAsset city;
    }
}
