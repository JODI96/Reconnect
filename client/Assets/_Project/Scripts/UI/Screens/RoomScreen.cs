using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reconnect.Client.Networking.Realtime;
using Reconnect.Client.Rooms;
using Reconnect.Client.UI.Games;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Screens
{
    /// <summary>
    /// Inside a room: the 3D <see cref="RoomView"/> behind a transparent UI with name labels,
    /// speech bubbles and a chat bar. Talks to the room only through <see cref="IRoomSession"/>.
    /// </summary>
    public sealed class RoomScreen : ScreenBase
    {
        private static readonly TimeSpan BubbleDuration = TimeSpan.FromSeconds(7);

        private readonly VisualTreeAsset _template;
        private readonly RoomView _room;
        private readonly IRoomSession _session;
        private readonly Guid _roomId;
        private readonly Guid _localUserId;
        private readonly Action _leave;

        private readonly Dictionary<Guid, (Label Name, Label Bubble, DateTime Until)> _overlays = new();
        private VisualElement _labelLayer;
        private Label _status;
        private TextField _chatInput;
        private VisualElement _gamePanel;
        private TicTacToePanel _ticTacToe;
        private QuizPanel _quiz;

        public RoomScreen(VisualTreeAsset template, RoomView room, IRoomSession session, Guid roomId, Guid localUserId, Action leave)
        {
            _template = template;
            _room = room;
            _session = session;
            _roomId = roomId;
            _localUserId = localUserId;
            _leave = leave;
        }

        protected override VisualTreeAsset Template => _template;

        protected override void OnShow()
        {
            Root.AddToClassList("screen--transparent");
            Root.pickingMode = PickingMode.Ignore;
            Root.Query(className: "pass-through").ForEach(e => e.pickingMode = PickingMode.Ignore);

            _labelLayer = Q<VisualElement>("labels");
            _status = Q<Label>("status");
            _chatInput = Q<TextField>("chat-input");
            _chatInput.maxLength = RoomGrid.MaxChatLength;
            Q<Button>("leave").clicked += _leave;
            Q<Button>("send").clicked += SendChat;
            _gamePanel = Q<VisualElement>("game-panel");
            _ticTacToe = new TicTacToePanel(Q<VisualElement>("ttt-panel"), _session, _localUserId, RunGameAction);
            _quiz = new QuizPanel(Q<VisualElement>("quiz-panel"), _session, _localUserId, RunGameAction);
            Q<Button>("game-close").clicked += () => OpenGame(null);
            foreach (var emote in Emotes.All)
            {
                Q<Button>("emote-" + emote).clicked += () => PlayEmote(emote);
            }
            _chatInput.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    SendChat();
                }
            }, TrickleDown.TrickleDown);

            _session.PlayerJoined += OnPlayerJoined;
            _session.PlayerLeft += OnPlayerLeft;
            _session.PlayerMoved += OnPlayerMoved;
            _session.ChatReceived += OnChat;
            _session.Disconnected += OnDisconnected;
            _session.EmoteReceived += OnEmote;
            _session.TicTacToeUpdated += _ticTacToe.Render;
            _session.QuizUpdated += _quiz.Render;
            _room.TileTapped += OnTileTapped;
            _room.StationTapped += OnStationTapped;
            _room.IsPointerOverUi = IsPointerOverUi;

            Root.schedule.Execute(UpdateOverlays).Every(0);
            RunAsync(JoinAsync);
        }

        protected override void OnHide()
        {
            _session.PlayerJoined -= OnPlayerJoined;
            _session.PlayerLeft -= OnPlayerLeft;
            _session.PlayerMoved -= OnPlayerMoved;
            _session.ChatReceived -= OnChat;
            _session.Disconnected -= OnDisconnected;
            _session.EmoteReceived -= OnEmote;
            _session.TicTacToeUpdated -= _ticTacToe.Render;
            _session.QuizUpdated -= _quiz.Render;
            _room.TileTapped -= OnTileTapped;
            _room.StationTapped -= OnStationTapped;
            _room.IsPointerOverUi = _ => false;
            _room.Hide();
            _ = _session.LeaveAsync();
        }

        private async System.Threading.Tasks.Task JoinAsync()
        {
            SetStatus(_status, "Betrete Raum …", isError: false);
            try
            {
                var snapshot = await _session.JoinAsync(_roomId, Lifetime);
                Q<Label>("room-name").text = snapshot.Room.Name;
                Q<Label>("room-owner").text = "von " + snapshot.Room.OwnerDisplayName;
                _room.Show(snapshot, _localUserId);
                _ticTacToe.Render(snapshot.TicTacToe);
                _quiz.Render(snapshot.Quiz);
                foreach (var player in snapshot.Players)
                {
                    AddOverlay(player.UserId, player.DisplayName);
                }
                SetStatus(_status, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                SetStatus(_status, "Raum konnte nicht betreten werden: " + ex.Message);
            }
        }

        private void OnTileTapped(Vector2Int tile)
        {
            var me = _room.Avatar(_localUserId);
            if (me == null)
            {
                return;
            }

            // Tapped a sofa/table? Walk to the free tile next to it. Walk immediately (feels instant),
            // then follow what the server accepted.
            var target = _room.Pathfinder.NearestWalkable(tile, me.NextTile);
            _room.MovePlayer(_localUserId, new TilePosition(target.x, target.y));
            RunAsync(async () =>
            {
                var accepted = await _session.MoveToAsync(new TilePosition(target.x, target.y));
                if (accepted.X != target.x || accepted.Z != target.y)
                {
                    _room.MovePlayer(_localUserId, accepted);
                }
            });
        }

        /// <summary>Walks next to the table/TV and opens its game.</summary>
        private void OnStationTapped(GameStation station)
        {
            OnTileTapped(station.Tile);
            OpenGame(station.GameId);
        }

        private void OpenGame(string gameId)
        {
            _gamePanel.style.display = gameId == null ? DisplayStyle.None : DisplayStyle.Flex;
            Q<VisualElement>("ttt-panel").style.display = gameId == "tictactoe" ? DisplayStyle.Flex : DisplayStyle.None;
            Q<VisualElement>("quiz-panel").style.display = gameId == "quiz" ? DisplayStyle.Flex : DisplayStyle.None;
            Q<Label>("game-title").text = gameId == "tictactoe" ? "Tic-Tac-Toe" : "Zürich-Quiz";
        }

        private void PlayEmote(string emote)
        {
            _room.Avatar(_localUserId)?.PlayEmote(emote);
            RunGameAction(() => _session.EmoteAsync(emote), null);
        }

        private void OnEmote(EmoteDto emote) => _room.Avatar(emote.UserId)?.PlayEmote(emote.Emote);

        /// <summary>Game calls: rule violations from the server ("not your turn") show up as status.</summary>
        private bool RunGameAction(Func<Task> action, VisualElement busy)
        {
            RunAsync(async () =>
            {
                try
                {
                    await action();
                    SetStatus(_status, null);
                }
                catch (HubException ex)
                {
                    SetStatus(_status, ex.Message);
                }
            }, busy);
            return true;
        }

        private void SendChat()
        {
            var text = _chatInput.value?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            _chatInput.value = "";
            RunAsync(async () =>
            {
                try
                {
                    await _session.SayAsync(text);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    SetStatus(_status, ex.Message);
                }
            });
        }

        private void OnPlayerJoined(RoomPlayerDto player)
        {
            _room.AddPlayer(player);
            AddOverlay(player.UserId, player.DisplayName);
        }

        private void OnPlayerLeft(Guid userId)
        {
            _room.RemovePlayer(userId);
            if (_overlays.Remove(userId, out var overlay))
            {
                overlay.Name.RemoveFromHierarchy();
                overlay.Bubble.RemoveFromHierarchy();
            }
        }

        private void OnPlayerMoved(PlayerMovedDto move) => _room.MovePlayer(move.UserId, move.Tile);

        private void OnChat(RoomChatMessageDto message)
        {
            if (!_overlays.TryGetValue(message.UserId, out var overlay))
            {
                return;
            }
            overlay.Bubble.text = message.Text;
            overlay.Bubble.style.display = DisplayStyle.Flex;
            _overlays[message.UserId] = (overlay.Name, overlay.Bubble, DateTime.UtcNow + BubbleDuration);
        }

        private void OnDisconnected(string reason) =>
            SetStatus(_status, "Verbindung zum Raum verloren" + (reason != null ? ": " + reason : "."));

        private void AddOverlay(Guid userId, string displayName)
        {
            if (_overlays.ContainsKey(userId))
            {
                return;
            }

            var name = new Label(displayName) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("avatar-name");
            name.EnableInClassList("avatar-name--me", userId == _localUserId);
            var bubble = new Label { pickingMode = PickingMode.Ignore };
            bubble.AddToClassList("speech-bubble");
            bubble.style.display = DisplayStyle.None;

            _labelLayer.Add(name);
            _labelLayer.Add(bubble);
            _overlays[userId] = (name, bubble, DateTime.MinValue);
        }

        /// <summary>Every frame: keep names and bubbles above the heads; hide old bubbles.</summary>
        private void UpdateOverlays()
        {
            var panel = Root.panel;
            if (panel == null)
            {
                return;
            }

            foreach (var avatar in _room.Avatars)
            {
                if (!_overlays.TryGetValue(avatar.UserId, out var overlay))
                {
                    continue;
                }

                var screen = _room.Camera.WorldToScreenPoint(avatar.LabelAnchor);
                var local = _labelLayer.WorldToLocal(RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y)));
                overlay.Name.style.left = local.x;
                overlay.Name.style.top = local.y;
                overlay.Bubble.style.left = local.x;
                overlay.Bubble.style.top = local.y - 34f;

                if (overlay.Bubble.style.display == DisplayStyle.Flex && DateTime.UtcNow > overlay.Until)
                {
                    overlay.Bubble.style.display = DisplayStyle.None;
                }
            }
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
    }
}
