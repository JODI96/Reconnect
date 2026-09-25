using UnityEngine;

namespace Reconnect.Client.City
{
    /// <summary>City view parameters. Asset: Assets/_Project/Settings/CitySettings.asset.</summary>
    [CreateAssetMenu(menuName = "Reconnect/City Settings", fileName = "CitySettings")]
    public sealed class CitySettings : ScriptableObject
    {
        [Header("World origin (= Unity 0/0/0), default: Zürich HB")]
        public double originLatitude = 47.37785;
        public double originLongitude = 8.54018;

        [Header("Buildings")]
        [Tooltip("Buildings within this radius around the origin are loaded.")]
        [Min(100)] public float loadRadiusMeters = 3000f;
        [Tooltip("Placeholder height until real 3D data arrives in phase 6.")]
        [Min(1)] public float buildingHeight = 35f;
        [Min(1)] public float buildingSize = 45f;

        [Header("Map tiles (swisstopo WMTS, level of detail)")]
        [Tooltip("Coarse tiles that always cover the whole area (no holes while sharper tiles load).")]
        [Range(10, 16)] public int baseZoom = 14;
        [Tooltip("Sharpest zoom used. Aerial goes up to 20 (10 cm/px), the map up to 19.")]
        [Range(14, 20)] public int maxZoom = 20;
        [Tooltip("1 = one texture pixel per screen pixel. Lower = fewer downloads, blurrier.")]
        [Range(0.25f, 2f)] public float tileDetail = 1f;
        [Tooltip("Tiles kept in memory; least recently used ones are evicted beyond this.")]
        [Min(50)] public int maxLoadedTiles = 350;
        [Tooltip("The covered area extends this far beyond the outermost buildings.")]
        [Min(0)] public float tileMarginMeters = 900f;
        public MapLayer defaultLayer = MapLayer.Aerial;

        [Header("Camera")]
        [Min(5)] public float minCameraHeight = 25f;
        [Min(10)] public float maxCameraHeight = 3500f;
        [Min(10)] public float startCameraHeight = 2400f;
        [Range(20, 90)] public float cameraPitch = 60f;
    }
}
