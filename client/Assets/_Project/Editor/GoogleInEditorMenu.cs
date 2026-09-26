using Reconnect.Client.City;
using UnityEditor;

namespace Reconnect.Client.Editor
{
    /// <summary>
    /// Menu "Reconnect > Google 3D im Editor": off by default, so pressing Play doesn't start a billed Google
    /// Photorealistic 3D Tiles session. Switch on to see the photorealistic city (takes effect when the city is
    /// shown next, at the latest after a minute). Stored per machine in EditorPrefs, not in the repository.
    /// </summary>
    public static class GoogleInEditorMenu
    {
        private const string MenuPath = "Reconnect/Google 3D im Editor";

        [MenuItem(MenuPath)]
        private static void Toggle() =>
            EditorPrefs.SetBool(MapService.GoogleInEditorPref, !EditorPrefs.GetBool(MapService.GoogleInEditorPref, false));

        [MenuItem(MenuPath, true)]
        private static bool ShowState()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(MapService.GoogleInEditorPref, false));
            return true;
        }
    }
}
