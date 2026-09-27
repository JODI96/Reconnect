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
        [Tooltip("Ground height at the origin. swisstopo's 3D tiles use heights above sea level (HB ≈ 408 m), so the ground there is at y ≈ 0.")]
        public double originHeight = 408;

        [Header("Buildings (our room entrances)")]
        [Tooltip("Buildings within this radius around the origin are loaded from the backend.")]
        [Min(100)] public float loadRadiusMeters = 3000f;
        [Tooltip("Height of the marker beacon above the roof.")]
        [Min(1)] public float markerHeight = 40f;

        [Header("3D detail (Cesium, lower screen-space error = sharper, more downloads)")]
        [Range(1, 64)] public float buildingsScreenSpaceError = 8f;
        [Range(1, 64)] public float terrainScreenSpaceError = 8f;
        [Tooltip("swisstopo buildings and terrain with \"Grafik: Hoch\".")]
        [Range(1, 64)] public float highScreenSpaceError = 6f;
        [Tooltip("Imagery texture detail on the terrain.")]
        [Range(1, 16)] public float imageryScreenSpaceError = 1f;
        public MapLayer defaultLayer = MapLayer.Aerial;

        [Header("Google Photorealistic 3D Tiles (when the backend grants a Google session)")]
        [Tooltip("Google uses heights above the WGS84 ellipsoid, swisstopo heights above sea level. Google tiles are lowered by the geoid height (Zurich: 47.75 m, EGM2008) so both line up.")]
        public float googleHeightOffset = -47.75f;
        [Range(1, 64)] public float googleScreenSpaceError = 12f;
        [Tooltip("Google tiles with \"Grafik: Hoch\" (sharper houses, more downloads).")]
        [Range(1, 64)] public float googleHighScreenSpaceError = 8f;
        [Tooltip("Google's model has roof superstructures (ventilation, lift machinery) that swisstopo lacks. Rooms on roofs are lifted by this much; their 4 m base closes the gap.")]
        [Range(0, 4)] public float googleRoofClearance = 4f;

        [Header("Camera (orbit around a point on the ground)")]
        [Min(5)] public float minDistance = 20f;
        [Min(100)] public float maxDistance = 8000f;
        [Min(10)] public float startDistance = 2600f;
        [Range(10, 89)] public float startPitch = 60f;
        [Range(5, 89)] public float minPitch = 12f;
        [Range(10, 90)] public float maxPitch = 89f;
        [Tooltip("The camera target can't leave this radius around the origin.")]
        [Min(500)] public float panRadius = 6000f;
    }

    public enum MapLayer
    {
        Aerial,
        Map,
    }
}
