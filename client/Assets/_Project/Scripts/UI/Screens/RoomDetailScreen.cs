using System;
using Reconnect.Client.Rooms;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Screens
{
    /// <summary>
    /// Room info. In phase 3 the "Betreten" button will load the 3D room and join the Photon session.
    /// </summary>
    public sealed class RoomDetailScreen : ScreenBase
    {
        private readonly VisualTreeAsset _template;
        private readonly RoomService _rooms;
        private readonly Guid _roomId;
        private readonly Action _back;

        public RoomDetailScreen(VisualTreeAsset template, RoomService rooms, Guid roomId, Action back)
        {
            _template = template;
            _rooms = rooms;
            _roomId = roomId;
            _back = back;
        }

        protected override VisualTreeAsset Template => _template;

        protected override void OnShow()
        {
            var status = Q<Label>("status");
            var enter = Q<Button>("enter");
            enter.SetEnabled(false);
            Q<Button>("back").clicked += _back;

            RunAsync(async () =>
            {
                SetStatus(status, "Lade Raum …", isError: false);
                var result = await _rooms.GetRoomAsync(_roomId, Lifetime);
                if (!result.IsSuccess)
                {
                    SetStatus(status, result.Error.ToDisplayString());
                    return;
                }

                var room = result.Value;
                Q<Label>("name").text = room.Name;
                Q<Label>("owner").text = "Besitzer: " + room.OwnerDisplayName;
                Q<Label>("details").text =
                    $"{(room.IsPublic ? "Öffentlich" : "Privat")} · {room.Layout.Count} Gegenstände · " +
                    $"zuletzt geändert {room.UpdatedAt.ToLocalTime():dd.MM.yyyy HH:mm}";
                SetStatus(status, null);
            });
        }
    }
}
