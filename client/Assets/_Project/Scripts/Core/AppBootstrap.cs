using System;
using Reconnect.Client.Auth;
using Reconnect.Client.Networking;
using Reconnect.Client.Rooms;
using Reconnect.Client.UI;
using Reconnect.Client.UI.Screens;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reconnect.Client.Core
{
    /// <summary>
    /// Composition root: creates all services once and decides which screen to show.
    /// Lives on the "App" GameObject in Main.unity (created by Reconnect → Setup Project).
    /// New services are constructed here and passed to the screens that need them.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class AppBootstrap : MonoBehaviour
    {
        [SerializeField] private ApiSettings apiSettings;
        [SerializeField] private UiCatalog ui;

        private ScreenNavigator _navigator;
        private AuthService _auth;
        private RoomService _rooms;

        private async void Start()
        {
            var api = new ApiClient(new UnityWebRequestTransport(apiSettings.RequestTimeoutSeconds), apiSettings.BaseUrl);
            _auth = new AuthService(api, new PlayerPrefsTokenStore());
            api.Tokens = _auth;
            _rooms = new RoomService(api);

            _navigator = new ScreenNavigator(GetComponent<UIDocument>().rootVisualElement, ui.theme);
            _auth.SessionChanged += ShowStartScreen;

            Debug.Log($"[Reconnect] API: {apiSettings.BaseUrl}");
            try
            {
                await _auth.TryRestoreSessionAsync(destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            ShowStartScreen();
        }

        private void Update() => _navigator?.UpdateSafeArea();

        private void OnDestroy()
        {
            if (_auth != null)
            {
                _auth.SessionChanged -= ShowStartScreen;
            }
        }

        private void ShowStartScreen()
        {
            if (_auth.IsLoggedIn)
            {
                ShowRooms();
            }
            else
            {
                ShowLogin();
            }
        }

        private void ShowLogin() => _navigator.Show(new LoginScreen(ui.login, _auth, ShowRegister));

        private void ShowRegister() => _navigator.Show(new RegisterScreen(ui.register, _auth, ShowLogin));

        private void ShowRooms() => _navigator.Show(new RoomListScreen(ui.roomList, ui.roomListItem, _rooms, _auth, ShowRoom));

        private void ShowRoom(Guid roomId) => _navigator.Show(new RoomDetailScreen(ui.roomDetail, _rooms, roomId, ShowRooms));
    }
}
