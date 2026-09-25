using UnityEngine;

namespace Reconnect.Client.Core
{
    /// <summary>
    /// Backend URLs per environment. Asset: Assets/_Project/Settings/ApiSettings.asset.
    /// The Editor and desktop talk to localhost; the Android emulator reaches the host PC via 10.0.2.2;
    /// a real phone needs the PC's LAN address (see CLAUDE.md).
    /// </summary>
    [CreateAssetMenu(menuName = "Reconnect/Api Settings", fileName = "ApiSettings")]
    public sealed class ApiSettings : ScriptableObject
    {
        [SerializeField] private string editorBaseUrl = "http://localhost:5191";
        [SerializeField] private string androidEmulatorBaseUrl = "http://10.0.2.2:5191";
        [SerializeField] private string deviceBaseUrl = "http://192.168.1.100:5191";
        [SerializeField] private bool useEmulatorUrlOnAndroid = true;
        [SerializeField, Min(1)] private int requestTimeoutSeconds = 15;

        public int RequestTimeoutSeconds => requestTimeoutSeconds;

        public string BaseUrl => ResolveBaseUrl(Application.platform);

        public string ResolveBaseUrl(RuntimePlatform platform) => platform switch
        {
            RuntimePlatform.WindowsEditor or RuntimePlatform.OSXEditor or RuntimePlatform.LinuxEditor
                or RuntimePlatform.WindowsPlayer or RuntimePlatform.OSXPlayer or RuntimePlatform.LinuxPlayer => editorBaseUrl,
            RuntimePlatform.Android when useEmulatorUrlOnAndroid => androidEmulatorBaseUrl,
            _ => deviceBaseUrl,
        };
    }
}
