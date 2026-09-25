using System;
using System.Collections.Generic;
using Reconnect.Client.Auth;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Rooms;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Screens
{
    /// <summary>Paged list of visible rooms (public + own). Tapping a room opens its details.</summary>
    public sealed class RoomListScreen : ScreenBase
    {
        private readonly VisualTreeAsset _template;
        private readonly VisualTreeAsset _itemTemplate;
        private readonly RoomService _rooms;
        private readonly AuthService _auth;
        private readonly Action<Guid> _openRoom;
        private readonly Action _openMap;

        private readonly List<RoomSummaryDto> _items = new();
        private int _loadedPage;
        private int _totalCount;

        private ListView _list;
        private Label _status;
        private Button _loadMore;

        public RoomListScreen(VisualTreeAsset template, VisualTreeAsset itemTemplate, RoomService rooms, AuthService auth,
            Action<Guid> openRoom, Action openMap)
        {
            _template = template;
            _itemTemplate = itemTemplate;
            _rooms = rooms;
            _auth = auth;
            _openRoom = openRoom;
            _openMap = openMap;
        }

        protected override VisualTreeAsset Template => _template;

        protected override void OnShow()
        {
            _status = Q<Label>("status");
            _loadMore = Q<Button>("load-more");
            _list = Q<ListView>("rooms");

            _list.itemsSource = _items;
            _list.makeItem = () => _itemTemplate.Instantiate();
            _list.bindItem = (element, index) => Bind(element, _items[index]);
            _list.selectionType = SelectionType.Single;
            _list.selectionChanged += selection =>
            {
                foreach (RoomSummaryDto room in selection)
                {
                    _openRoom(room.Id);
                    return;
                }
            };

            Q<Button>("map").clicked += _openMap;
            Q<Button>("refresh").clicked += Reload;
            Q<Button>("logout").clicked += _auth.Logout;
            _loadMore.clicked += () => RunAsync(() => LoadPageAsync(_loadedPage + 1), _loadMore);

            Reload();
        }

        private void Reload()
        {
            _items.Clear();
            _loadedPage = 0;
            _totalCount = 0;
            _list.RefreshItems();
            RunAsync(() => LoadPageAsync(1));
        }

        private async System.Threading.Tasks.Task LoadPageAsync(int page)
        {
            SetStatus(_status, "Lade Räume …", isError: false);
            var result = await _rooms.GetRoomsAsync(page, ct: Lifetime);
            if (!result.IsSuccess)
            {
                SetStatus(_status, result.Error.ToDisplayString());
                return;
            }

            _items.AddRange(result.Value.Items);
            _loadedPage = page;
            _totalCount = result.Value.TotalCount;
            _list.RefreshItems();

            SetStatus(_status, _items.Count == 0 ? "Noch keine Räume vorhanden." : null, isError: false);
            _loadMore.style.display = _items.Count < _totalCount ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void Bind(VisualElement element, RoomSummaryDto room)
        {
            element.Q<Label>("name").text = room.Name;
            element.Q<Label>("owner").text = "von " + room.OwnerDisplayName;
            element.Q<Label>("visibility").text = room.IsPublic ? "Öffentlich" : "Privat";
        }
    }
}
