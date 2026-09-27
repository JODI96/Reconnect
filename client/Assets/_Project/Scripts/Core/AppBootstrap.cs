using System;
using Reconnect.Client.Auth;
using Reconnect.Client.City;
using Reconnect.Client.Economy;
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
        [SerializeField] private CitySettings citySettings;
        [SerializeField] private UiCatalog ui;
        [SerializeField] private CityView city;
        [SerializeField] private RoomView room;

        [Tooltip("URP renderers whose ambient occlusion follows the graphics setting.")]
        [SerializeField] private UnityEngine.Rendering.Universal.ScriptableRendererData[] renderers;

        internal UiCatalog Ui => ui;
        internal CitySettings CitySettings => citySettings;

        private ScreenNavigator _navigator;
        private AuthService _auth;
        private RoomService _rooms;
        private BuildingService _buildings;
        private MapService _maps;
        private TowerService _tower;
        private WalletService _wallet;
        private RealEstateService _realEstate;
        private IRoomSession _roomSession;
        private GraphicsQuality _graphics;

        private async void Start()
        {
            var api = new ApiClient(new UnityWebRequestTransport(apiSettings.RequestTimeoutSeconds), apiSettings.BaseUrl);
            _auth = new AuthService(api, new PlayerPrefsTokenStore());
            api.Tokens = _auth;
            _rooms = new RoomService(api);
            _buildings = new BuildingService(api);
            _maps = new MapService(api);
            _tower = new TowerService(api);
            _wallet = new WalletService(api);
            _realEstate = new RealEstateService(api);
            _roomSession = new SignalRRoomSession(apiSettings.BaseUrl, _auth);
            city.Initialize(citySettings);
            _graphics = new GraphicsQuality(renderers);
            room.HighQuality = _graphics.High;
            _graphics.Changed += () => room.HighQuality = _graphics.High;

            // Runtime copy: the navigator scales the panel per device, the asset stays untouched.
            var document = GetComponent<UIDocument>();
            document.panelSettings = Instantiate(document.panelSettings);
            _navigator = new ScreenNavigator(document.rootVisualElement, ui.theme, document.panelSettings);
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

        private void Update() => _navigator?.UpdateLayout();

        private void OnDestroy()
        {
            if (_auth != null)
            {
                _auth.SessionChanged -= ShowStartScreen;
            }
            _roomSession?.Dispose();
        }

        private void ShowStartScreen()
        {
            _maps.Reset();   // another user may get another map (Premium)
            if (_auth.IsLoggedIn)
            {
                ShowCity();
            }
            else
            {
                ShowLogin();
            }
        }

        private void ShowLogin() => _navigator.Show(new LoginScreen(ui.login, _auth, ShowRegister));

        private void ShowRegister() => _navigator.Show(new RegisterScreen(ui.register, _auth, ShowLogin));

        private void ShowCity() => _navigator.Show(new CityScreen(ui.city, city, citySettings, _buildings, _maps, _tower, _wallet, _rooms, _auth,
            openRoomList: ShowRooms, openRoom: id => ShowRoom(id, back: ShowCity), enterRoom: EnterRoom, openOffices: ShowOffices, graphics: _graphics));

        private void ShowOffices(Guid buildingId) =>
            _navigator.Show(new OfficesScreen(ui.offices, _realEstate, _wallet, buildingId, back: ShowCity));

        private void ShowRooms() => _navigator.Show(new RoomListScreen(ui.roomList, ui.roomListItem, _rooms, _auth,
            openRoom: id => ShowRoom(id, back: ShowRooms), openMap: ShowCity));

        private void ShowRoom(Guid roomId, Action back) =>
            _navigator.Show(new RoomDetailScreen(ui.roomDetail, _rooms, roomId, back, enter: EnterRoom));

        private void EnterRoom(Guid roomId) =>
            _navigator.Show(new RoomScreen(ui.room, room, city, _roomSession, _tower, _rooms, roomId, _auth.UserId!.Value, _auth.IsAdmin, leave: ShowCity));
    }
}
