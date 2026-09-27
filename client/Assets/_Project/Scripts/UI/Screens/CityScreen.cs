using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Reconnect.Client.Auth;
using Reconnect.Client.City;
using Reconnect.Client.Economy;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Buildings;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Screens
{
    /// <summary>
    /// Zürich from above: shows the 3D <see cref="CityView"/> behind a transparent UI with
    /// building labels, a map/aerial toggle and a panel listing the rooms of the selected building.
    /// </summary>
    public sealed class CityScreen : ScreenBase
    {
        private readonly VisualTreeAsset _template;
        private readonly CityView _city;
        private readonly CitySettings _settings;
        private readonly BuildingService _buildings;
        private readonly MapService _maps;
        private readonly TowerService _tower;
        private readonly WalletService _wallet;
        private readonly Action<Guid> _enterRoom;
        private readonly Action<Guid> _openOffices;
        private Guid _lobbyId;
        private readonly RoomService _rooms;
        private readonly AuthService _auth;
        private readonly Action _openRoomList;
        private readonly Action<Guid> _openRoom;

        private readonly Dictionary<Guid, Label> _labels = new();
        private VisualElement _labelLayer;
        private VisualElement _sheet;
        private Label _status;
        private Label _tileStatus;
        private BuildingDto _selected;
        private CancellationTokenSource _selectionLoad;

        public CityScreen(VisualTreeAsset template, CityView city, CitySettings settings, BuildingService buildings,
            MapService maps, TowerService tower, WalletService wallet, RoomService rooms, AuthService auth,
            Action openRoomList, Action<Guid> openRoom, Action<Guid> enterRoom, Action<Guid> openOffices,
            Reconnect.Client.Core.GraphicsQuality graphics = null)
        {
            _graphics = graphics;
            _tower = tower;
            _wallet = wallet;
            _enterRoom = enterRoom;
            _openOffices = openOffices;
            _maps = maps;
            _template = template;
            _city = city;
            _settings = settings;
            _buildings = buildings;
            _rooms = rooms;
            _auth = auth;
            _openRoomList = openRoomList;
            _openRoom = openRoom;
        }

        protected override VisualTreeAsset Template => _template;

        protected override void OnShow()
        {
            // Let pointer events reach the 3D city wherever no control is.
            Root.AddToClassList("screen--transparent");
            Root.pickingMode = PickingMode.Ignore;
            Root.Query(className: "pass-through").ForEach(e => e.pickingMode = PickingMode.Ignore);

            _labelLayer = Q<VisualElement>("labels");
            _sheet = Q<VisualElement>("sheet");
            _status = Q<Label>("status");
            _tileStatus = Q<Label>("tile-status");
            _sheet.style.display = DisplayStyle.None;

            var layerButton = Q<Button>("layer");
            UpdateLayerButton(layerButton);
            layerButton.clicked += () =>
            {
                _city.SetLayer(_city.Layer == MapLayer.Aerial ? MapLayer.Map : MapLayer.Aerial);
                UpdateLayerButton(layerButton);
                ShowMenu(false);
            };
            var providerButton = Q<Button>("map-provider");
            ShowProvider(providerButton);
            providerButton.clicked += () =>
            {
                _maps.SetGoogleWanted(!_maps.GoogleWanted);
                ShowProvider(providerButton);
                ShowMenu(false);
                RunAsync(async () =>
                {
                    var map = await _maps.GetAsync(Lifetime);
                    _city.ApplyMap(map);
                    ShowMapInfo(map);
                    UpdateLayerButton(layerButton);
                });
            };
            Q<Button>("menu").clicked += () => ShowMenu(Q<VisualElement>("menu-panel").style.display == DisplayStyle.None);
            var graphicsButton = Q<Button>("graphics");
            ShowGraphics(graphicsButton);
            graphicsButton.clicked += () =>
            {
                _graphics?.Set(!_graphics.High);
                ShowGraphics(graphicsButton);
            };
            Q<Button>("rooms").clicked += _openRoomList;
            Q<Button>("logout").clicked += _auth.Logout;
            Q<Button>("sheet-close").clicked += () => Select(null);
            Q<Button>("create-room").clicked += CreateRoom;
            Q<Button>("tower-enter").clicked += () => _enterRoom(_lobbyId);
            Q<Button>("tower-offices").clicked += () => _openOffices(_selected.Id);
            RunAsync(async () =>
            {
                var wallet = await _wallet.GetAsync(Lifetime);
                Q<Label>("balance").text = wallet.IsSuccess ? Money.Format(wallet.Value.Balance) : "";
            });

            _city.CameraController.IsPointerOverUi = IsPointerOverUi;
            _city.BuildingTapped += Select;
            Root.schedule.Execute(UpdateOverlay).Every(0);
            // Staying in the city for hours: renew the map session once it expires (free while it is valid).
            Root.schedule.Execute(() => RunAsync(async () => _city.ApplyMap(await _maps.GetAsync(Lifetime))))
                .StartingIn(60_000).Every(60_000);

            RunAsync(async () =>
            {
                // Which city look this session gets, before anything streams (no swisstopo → Google flash).
                var map = await _maps.GetAsync(Lifetime);
                _city.ApplyMap(map);
                ShowMapInfo(map);
                _city.SetVisible(true);
                await LoadBuildingsAsync();
            });
        }

        /// <summary>Attribution, map/aerial toggle (swisstopo only) and a hint about the photorealistic city.</summary>
        private void ShowMapInfo(MapSessionDto map)
        {
            var google = _city.IsGoogle;
            // Google's logo and data credits are drawn by Cesium (showCreditsOnScreen on the Google tileset).
            Q<Label>("attribution").style.display = google ? DisplayStyle.None : DisplayStyle.Flex;
            Q<Button>("layer").style.display = google ? DisplayStyle.None : DisplayStyle.Flex;

            var hint = map.FallbackReason switch
            {
                MapFallbackReasons.FreeQuotaUsed => "Fotorealistische Stadt: Gratis-Kontingent für diesen Monat aufgebraucht – mit Premium unbegrenzt.",
                MapFallbackReasons.BudgetExhausted => "Fotorealistische Stadt gerade ausgelastet – mit Premium immer verfügbar.",
                _ when google && !map.IsPremium && map.FreeGoogleSessionsLeft is { } left =>
                    $"Fotorealistische Stadt · noch {left} Gratis-Besuche diesen Monat",
                _ => null,
            };
            var label = Q<Label>("map-hint");
            label.text = hint ?? "";
            label.style.display = hint == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        protected override void OnHide()
        {
            _selectionLoad?.Cancel();
            _city.BuildingTapped -= Select;
            _city.CameraController.IsPointerOverUi = _ => false;
            _city.Select(null);
            _city.SetVisible(false);
        }

        private async System.Threading.Tasks.Task LoadBuildingsAsync()
        {
            SetStatus(_status, "Lade Gebäude …", isError: false);
            var result = await _buildings.GetNearbyAsync(_settings.originLatitude, _settings.originLongitude,
                _settings.loadRadiusMeters, Lifetime);
            if (!result.IsSuccess)
            {
                SetStatus(_status, result.Error.ToDisplayString());
                return;
            }

            _city.ShowBuildings(result.Value);
            CreateLabels();
            SetStatus(_status, result.Value.Count == 0 ? "Keine Gebäude in der Nähe." : null, isError: false);
        }

        private void CreateLabels()
        {
            _labelLayer.Clear();
            _labels.Clear();
            foreach (var marker in _city.Markers)
            {
                var building = marker.Building;
                var label = new Label(building.Name);
                label.AddToClassList("building-label");
                label.RegisterCallback<ClickEvent>(_ => Select(building));
                _labelLayer.Add(label);
                _labels[building.Id] = label;
            }
        }

        /// <summary>Runs every frame: keeps labels above their buildings and shows tile progress.</summary>
        private void UpdateOverlay()
        {
            var panel = Root.panel;
            if (panel == null)
            {
                return;
            }

            var camera = _city.Camera;
            foreach (var marker in _city.Markers)
            {
                if (!_labels.TryGetValue(marker.Building.Id, out var label))
                {
                    continue;
                }

                var screen = camera.WorldToScreenPoint(marker.LabelAnchor);
                if (screen.z <= 0f)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
                var local = _labelLayer.WorldToLocal(panelPosition);
                label.style.display = DisplayStyle.Flex;
                label.style.left = local.x;
                label.style.top = local.y;
                label.EnableInClassList("building-label--selected", _selected?.Id == marker.Building.Id);
            }

            var progress = _city.LoadProgress;
            _tileStatus.text = progress < 99.5f ? $"Lade 3D-Stadt … {progress:0}%" : "";
        }

        /// <summary>Menu with the map layer, the room list and logout; closes the building panel to make room.</summary>
        private readonly Reconnect.Client.Core.GraphicsQuality _graphics;

        /// <summary>"Stadt: Google 3D" (photorealistic, if the account gets it) or "Stadt: swisstopo" (default, free).</summary>
        private void ShowProvider(Button button) =>
            button.text = _maps.GoogleWanted ? "Stadt: Google 3D" : "Stadt: swisstopo";

        /// <summary>"Grafik: Hoch" (ambient occlusion, reflections) or "Grafik: Normal" (older phones).</summary>
        private void ShowGraphics(Button button)
        {
            button.style.display = _graphics == null ? DisplayStyle.None : DisplayStyle.Flex;
            button.text = _graphics is { High: true } ? "Grafik: Hoch" : "Grafik: Normal";
        }

        internal void ShowMenu(bool open)
        {
            Q<VisualElement>("menu-panel").style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
                Select(null);
            }
        }

        internal void Select(BuildingDto building)
        {
            if (building != null)
            {
                ShowMenu(false);
            }
            _selected = building;
            _city.Select(building);
            _selectionLoad?.Cancel();

            if (building == null)
            {
                _sheet.style.display = DisplayStyle.None;
                return;
            }

            _sheet.style.display = DisplayStyle.Flex;
            Q<Label>("sheet-title").text = building.Name;
            Q<Label>("sheet-address").text = building.Address;
            Q<TextField>("new-room-name").value = "";
            LoadRoomsOfSelected();
        }

        private void LoadRoomsOfSelected()
        {
            var building = _selected;
            _selectionLoad = CancellationTokenSource.CreateLinkedTokenSource(Lifetime);
            var ct = _selectionLoad.Token;
            var list = Q<VisualElement>("sheet-rooms");
            var sheetStatus = Q<Label>("sheet-status");

            RunAsync(async () =>
            {
                list.Clear();
                SetStatus(sheetStatus, "Lade Räume …", isError: false);

                // A tower (floors with a lobby) is entered through its lobby; offices are bought, not created.
                var tower = await _tower.GetAsync(building.Id, ct);
                var lobby = tower.IsSuccess ? tower.Value.Floors.FirstOrDefault(f => f.Floor == 0 && f.IsPublic) : null;
                ShowTowerActions(lobby, tower.IsSuccess ? tower.Value.Floors.Sum(f => f.Occupancy) : 0);
                if (lobby != null)
                {
                    SetStatus(sheetStatus, null);
                    return;
                }

                var result = await _rooms.GetRoomsAsync(1, building.Id, ct);
                if (!result.IsSuccess)
                {
                    SetStatus(sheetStatus, result.Error.ToDisplayString());
                    return;
                }

                SetStatus(sheetStatus, result.Value.Items.Count == 0 ? "Noch keine Räume – erstelle den ersten!" : null, isError: false);
                foreach (var room in result.Value.Items)
                {
                    var roomId = room.Id;
                    var button = new Button(() => _openRoom(roomId)) { text = $"{room.Name}  ·  {room.OwnerDisplayName}" };
                    button.AddToClassList("button");
                    button.AddToClassList("sheet__room");
                    list.Add(button);
                }
            });
        }

        private void CreateRoom()
        {
            var nameField = Q<TextField>("new-room-name");
            var sheetStatus = Q<Label>("sheet-status");
            var building = _selected;
            if (building == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(nameField.value))
            {
                SetStatus(sheetStatus, "Bitte einen Namen für den Raum eingeben.");
                return;
            }

            RunAsync(async () =>
            {
                var result = await _rooms.CreateRoomAsync(building.Id, nameField.value, isPublic: true, Lifetime);
                if (!result.IsSuccess)
                {
                    SetStatus(sheetStatus, result.Error.ToDisplayString());
                    return;
                }
                nameField.value = "";
                LoadRoomsOfSelected();
            }, Q<Button>("create-room"));
        }

        private bool IsPointerOverUi(Vector2 screenPosition)
        {
            var panel = Root.panel;
            if (panel == null)
            {
                return false;
            }
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return panel.Pick(panelPosition) != null;
        }

        private void ShowTowerActions(TowerFloorDto lobby, int peopleInside)
        {
            var isTower = lobby != null;
            _lobbyId = lobby?.RoomId ?? Guid.Empty;
            Q<VisualElement>("tower-actions").style.display = isTower ? DisplayStyle.Flex : DisplayStyle.None;
            Q<VisualElement>("sheet-scroll").style.display = isTower ? DisplayStyle.None : DisplayStyle.Flex;
            Q<VisualElement>("new-room-name").parent.style.display = isTower ? DisplayStyle.None : DisplayStyle.Flex;
            if (isTower)
            {
                Q<Button>("tower-enter").text = $"Betreten · Lobby ({peopleInside} im Tower)";
            }
        }

        /// <summary>The button offers the other layer.</summary>
        private void UpdateLayerButton(Button button) =>
            button.text = _city.Layer == MapLayer.Aerial ? "Karte" : "Luftbild";
    }
}
