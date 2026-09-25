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

        [Header("Map tiles (swisstopo WMTS)")]
        [Range(12, 18)] public int tileZoom = 16;
        [Tooltip("Ground is covered around all buildings plus this margin.")]
        [Min(0)] public float tileMarginMeters = 900f;
        [Min(1)] public int maxTiles = 220;
        public MapLayer defaultLayer = MapLayer.Aerial;

        [Header("Camera")]
        [Min(10)] public float minCameraHeight = 120f;
        [Min(10)] public float maxCameraHeight = 3500f;
        [Min(10)] public float startCameraHeight = 2400f;
        [Range(20, 90)] public float cameraPitch = 60f;
    }
}
