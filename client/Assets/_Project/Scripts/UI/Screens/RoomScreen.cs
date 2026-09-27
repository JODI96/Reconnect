using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Reconnect.Client.Networking.Realtime;
using Reconnect.Client.City;
using Reconnect.Client.Economy;
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
    /// Tower floors also have a lift: panel with every floor and its occupancy, a queue banner while
    /// waiting for a full floor, and a lift-ride transition (doors close, floors count, new floor).
    /// </summary>
    public sealed class RoomScreen : ScreenBase
    {
        private static readonly TimeSpan BubbleDuration = TimeSpan.FromSeconds(7);

        private readonly VisualTreeAsset _template;
        private readonly RoomView _room;
        private readonly CityView _city;
        private readonly IRoomSession _session;
        private readonly TowerService _tower;
        private readonly RoomService _rooms;
        private readonly bool _isAdmin;
        private BuildPanel _build;
        private Guid _roomId;
        private RoomDto _current;
        private readonly Guid _localUserId;
        private readonly Action _leave;

        private readonly Dictionary<Guid, (Label Name, Label Bubble, DateTime Until)> _overlays = new();
        private VisualElement _labelLayer;
        private Label _status;
        private TextField _chatInput;
        private VisualElement _gamePanel;
        private TicTacToePanel _ticTacToe;
        private QuizPanel _quiz;
        private BoardGamePanel _boardGame;
        private VisualElement _liftPanel;
        private VisualElement _queueBanner;
        private IVisualElementScheduledItem _liftRefresh;
        private bool _riding;
        private bool _towerCredit;

        public RoomScreen(VisualTreeAsset template, RoomView room, CityView city, IRoomSession session, TowerService tower,
            RoomService rooms, Guid roomId, Guid localUserId, bool isAdmin, Action leave)
        {
            _rooms = rooms;
            _isAdmin = isAdmin;
            _template = template;
            _room = room;
            _city = city;
            _session = session;
            _tower = tower;
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
            _boardGame = new BoardGamePanel(Q<VisualElement>("board-panel"), _session, _localUserId, RunGameAction,
                id => _room.BuildIcons != null ? _room.BuildIcons.Find(id) : null);
            Q<Button>("game-close").clicked += () => OpenGame(null);
            _liftPanel = Q<VisualElement>("lift-panel");
            _queueBanner = Q<VisualElement>("queue-banner");
            Q<Button>("lift").clicked += OpenLift;
            _build = new BuildPanel(Root, _room, _room.TransparentMaterial, _isAdmin, SaveLayoutAsync, OnBuildClosed);
            Q<Button>("build").clicked += OpenBuild;
            var music = Q<Button>("music");
            ShowMusicState(music);
            music.clicked += () =>
            {
                if (_room.Music != null)
                {
                    _room.Music.Muted = !_room.Music.Muted;
                }
                ShowMusicState(music);
            };
            Q<Button>("lift-close").clicked += CloseLift;
            Q<Button>("queue-leave").clicked += () => RunAsync(async () =>
            {
                await _session.LeaveQueueAsync();
                ShowQueue(null);
            });
            foreach (var emote in Emotes.All)
            {
                // Sitting is not an emote button: tap a chair, sofa or lounger and you walk there and sit down.
                if (Root.Q<Button>("emote-" + emote) is { } button)
                {
                    button.clicked += () => PlayEmote(emote);
                }
            }
            Q<Button>("view-left").clicked += () => _room.RotateView(-1);
            Q<Button>("view-right").clicked += () => _room.RotateView(1);
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
            _session.PlayerSeated += OnPlayerSeated;
            _room.SeatTapped += OnSeatTapped;
            _session.TicTacToeUpdated += _ticTacToe.Render;
            _session.QuizUpdated += _quiz.Render;
            _session.BoardGameUpdated += _boardGame.Render;
            _session.QueueUpdated += OnQueueUpdated;
            _session.ElevatorArrived += OnElevatorArrived;
            _session.LayoutChanged += OnLayoutChanged;
            _room.TileTapped += OnTileTapped;
            _room.StationTapped += OnStationTapped;
            _room.IsPointerOverUi = IsPointerOverUi;
            _room.IsTyping = () => Root.panel?.focusController?.focusedElement is VisualElement focused
                && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);

            Root.schedule.Execute(UpdateOverlays).Every(0);
            RunAsync(JoinAsync);
        }

        protected override void OnHide()
        {
            if (_build.IsOpen)
            {
                _build.Close(_current?.Layout);
            }
            _session.LayoutChanged -= OnLayoutChanged;
            _session.PlayerJoined -= OnPlayerJoined;
            _session.PlayerLeft -= OnPlayerLeft;
            _session.PlayerMoved -= OnPlayerMoved;
            _session.ChatReceived -= OnChat;
            _session.Disconnected -= OnDisconnected;
            _session.EmoteReceived -= OnEmote;
            _session.PlayerSeated -= OnPlayerSeated;
            _room.SeatTapped -= OnSeatTapped;
            if (_room.Avatar(_localUserId) is { } me)
            {
                me.Arrived -= OnArrived;
            }
            _session.TicTacToeUpdated -= _ticTacToe.Render;
            _session.QuizUpdated -= _quiz.Render;
            _session.BoardGameUpdated -= _boardGame.Render;
            _session.QueueUpdated -= OnQueueUpdated;
            _session.ElevatorArrived -= OnElevatorArrived;
            _liftRefresh?.Pause();
            _room.TileTapped -= OnTileTapped;
            _room.StationTapped -= OnStationTapped;
            _room.IsPointerOverUi = _ => false;
            _room.IsTyping = () => false;
            _room.Hide();
            _city.HideTowerCutaway();
            _city.SetVisible(false);
            _ = _session.LeaveQueueAsync();
            _ = _session.LeaveAsync();
        }

        private async System.Threading.Tasks.Task JoinAsync()
        {
            SetStatus(_status, "Betrete Raum …", isError: false);
            try
            {
                var snapshot = await _session.JoinAsync(_roomId, Lifetime);
                await ShowSnapshotAsync(snapshot);
                SetStatus(_status, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                SetStatus(_status, "Raum konnte nicht betreten werden: " + ex.Message);
            }
        }

        /// <summary>Builds the room (again, after a lift ride) and the name labels of everyone in it.</summary>
        private async Task ShowSnapshotAsync(RoomSnapshotDto snapshot)
        {
            if (_build.IsOpen)
            {
                _build.Close(snapshot.Room.Layout);
            }
            _current = snapshot.Room;
            _roomId = snapshot.Room.Id;
            Q<Button>("build").style.display = CanBuild ? DisplayStyle.Flex : DisplayStyle.None;
            Q<Label>("room-name").text = snapshot.Room.Name;
            Q<Label>("room-owner").text = (snapshot.Room.Floor is { } floor ? FloorLabel(floor) + " · " : "") + "von " + snapshot.Room.OwnerDisplayName;
            Q<Button>("lift").style.display = snapshot.Room.Floor != null ? DisplayStyle.Flex : DisplayStyle.None;
            _towerCredit = snapshot.Room.Floor != null;
            Q<Label>("tower-credit").style.display = _towerCredit ? DisplayStyle.Flex : DisplayStyle.None;
            OpenGame(null);

            foreach (var overlay in _overlays.Values)
            {
                overlay.Name.RemoveFromHierarchy();
                overlay.Bubble.RemoveFromHierarchy();
            }
            _overlays.Clear();

            // Tower storeys stand at their real height in the cut-away tower, roof terraces on the real roof –
            // both with the 3D city around them.
            Vector3? anchor = null;
            var yaw = 0f;
            _city.HideTowerCutaway();
            if (RoomTheme.For(snapshot.Room.Theme).Outdoor)
            {
                _city.ShowAsBackdrop();
                var tower = snapshot.Room.Floor != null ? await _city.GetTowerAsync(snapshot.Room.BuildingId) : null;
                if (tower != null)
                {
                    var (centre, turn) = _city.StoreyPlacement(tower, snapshot.Room, snapshot.Width, snapshot.Depth);
                    anchor = centre;
                    yaw = turn;
                    _city.ShowTowerCutaway(tower, snapshot.Room.Floor.Value);
                    if (RoomView.HidesCity(snapshot.Room.Floor))
                    {
                        _city.SetVisible(false);   // low storey: black all around (see RoomView.CityHiddenBelowStorey)
                    }
                }
                else
                {
                    anchor = await _city.RoofAnchorAsync(snapshot.Room.BuildingId, snapshot.Width + 1f, snapshot.Depth + 1f);
                }
                if (anchor == null)
                {
                    _city.SetVisible(false);   // building unknown: room in the sky
                }
            }
            else
            {
                _city.SetVisible(false);
            }
            _room.Show(snapshot, _localUserId, anchor, yaw);
            _ticTacToe.Render(snapshot.TicTacToe);
            _quiz.Render(snapshot.Quiz);
            foreach (var state in snapshot.BoardGames ?? Array.Empty<BoardGameStateDto>())
            {
                _boardGame.Render(state);
            }
            foreach (var player in snapshot.Players)
            {
                AddOverlay(player.UserId, player.DisplayName);
            }
        }

        /// <summary>"Ton" when music plays, crossed out look when muted.</summary>
        private void ShowMusicState(Button button)
        {
            var muted = _room.Music == null || _room.Music.Muted;
            button.text = muted ? "Stumm" : "Ton";
            button.EnableInClassList("button--off", muted);
        }

        // ---------- Build editor ----------

        /// <summary>Owners build in their own rooms, admins everywhere.</summary>
        private bool CanBuild => _current != null && (_current.OwnerId == _localUserId || _isAdmin);

        internal BuildPanel Build => _build;

        internal void OpenBuild()
        {
            if (!CanBuild || _build.IsOpen)
            {
                return;
            }
            OpenGame(null);
            CloseLift();
            _pendingSeat = null;
            Q<Button>("build").style.display = DisplayStyle.None;
            Q<VisualElement>("emote-bar").style.display = DisplayStyle.None;
            Q<VisualElement>("chat-bar").style.display = DisplayStyle.None;
            _build.Open(_current.Theme);
        }

        private void OnBuildClosed()
        {
            Q<Button>("build").style.display = CanBuild ? DisplayStyle.Flex : DisplayStyle.None;
            Q<VisualElement>("chat-bar").style.display = DisplayStyle.Flex;
            UpdateEmoteBar();
        }

        private async Task<(bool Ok, string Error)> SaveLayoutAsync(IReadOnlyList<RoomItemDto> layout)
        {
            var result = await _rooms.UpdateLayoutAsync(_roomId, layout, Lifetime);
            if (result.IsSuccess)
            {
                _current = result.Value;
                return (true, null);
            }
            var problems = result.Error.FieldErrors.Values.SelectMany(v => v).Distinct().ToList();
            return (false, problems.Count > 0 ? string.Join(" ", problems.Take(2)) : result.Error.Message);
        }

        /// <summary>Someone (the owner, an admin) saved a new layout: rebuild, unless I'm building right now.</summary>
        private void OnLayoutChanged(RoomLayoutChangedDto change)
        {
            if (change.RoomId != _roomId || _current == null)
            {
                return;
            }
            _current = _current with { Layout = change.Layout };
            if (!_build.IsOpen)
            {
                _room.ApplyLayout(change.Layout);
            }
        }

        // ---------- Lift ----------

        internal void OpenLift()
        {
            if (_current?.Floor == null)
            {
                return;
            }
            OpenGame(null);
            _liftPanel.style.display = DisplayStyle.Flex;
            UpdateEmoteBar();
            Q<Label>("lift-subtitle").text = "Du bist: " + FloorLabel(_current.Floor.Value);
            RefreshLift();
            _liftRefresh ??= Root.schedule.Execute(RefreshLift).Every(2000);
            _liftRefresh.Resume();
        }

        private void CloseLift()
        {
            _liftPanel.style.display = DisplayStyle.None;
            _liftRefresh?.Pause();
            UpdateEmoteBar();
        }

        /// <summary>On a phone there is no room for emotes next to an open lift or game panel.</summary>
        private void UpdateEmoteBar()
        {
            var panelOpen = _liftPanel.style.display == DisplayStyle.Flex || _gamePanel.style.display == DisplayStyle.Flex
                || (_build != null && _build.IsOpen);
            Q<VisualElement>("emote-bar").style.display = panelOpen ? DisplayStyle.None : DisplayStyle.Flex;
            // The panels cover the city behind: the building credit comes back as soon as they close.
            Q<Label>("tower-credit").style.display = _towerCredit && !panelOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Floors top to bottom, like a real lift panel, with live occupancy.</summary>
        private void RefreshLift() => RunAsync(async () =>
        {
            if (_current == null || _liftPanel.style.display == DisplayStyle.None)
            {
                return;
            }
            var result = await _tower.GetAsync(_current.BuildingId, Lifetime);
            var status = Q<Label>("lift-status");
            if (!result.IsSuccess)
            {
                SetStatus(status, result.Error.ToDisplayString());
                return;
            }
            SetStatus(status, null);

            var list = Q<ScrollView>("lift-floors");
            list.Clear();
            foreach (var floor in result.Value.Floors.OrderByDescending(f => f.Floor))
            {
                var target = floor;
                var current = floor.RoomId == _current.Id;
                var full = floor.Occupancy >= floor.Capacity || floor.QueueLength > 0;
                var button = new Button(() => RideTo(target));
                button.SetEnabled(!current && !_riding);
                button.AddToClassList("button");
                button.AddToClassList("lift-floor");
                button.EnableInClassList("lift-floor--current", current);
                button.EnableInClassList("lift-floor--full", full && !current);
                button.EnableInClassList("lift-floor--mine", floor.IsMine);
                button.Add(Text(FloorLabel(floor.Floor), "lift-floor__number"));
                button.Add(Text(floor.IsMine ? "★ " + floor.Name : floor.Name, "lift-floor__name"));
                button.Add(Text(current ? "Du bist hier"
                    : full ? $"Voll · {floor.QueueLength} warten · anstellen"
                    : $"{floor.Occupancy}/{floor.Capacity}", "lift-floor__info"));
                list.Add(button);
            }
        });

        private static Label Text(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private void RideTo(TowerFloorDto floor)
        {
            CloseLift();
            RunAsync(async () =>
            {
                try
                {
                    var result = await _session.RideElevatorAsync(floor.RoomId);
                    if (result.Status == ElevatorStatus.Arrived && result.Snapshot != null)
                    {
                        ShowQueue(null);
                        await RideAsync(_current?.Floor ?? 0, floor.Floor, () => ShowSnapshotAsync(result.Snapshot));
                    }
                    else
                    {
                        ShowQueue(result.Queue);
                    }
                }
                catch (HubException ex)
                {
                    SetStatus(_status, ex.Message);
                }
            });
        }

        private void OnQueueUpdated(QueueStatusDto queue) => ShowQueue(queue.Position > 0 ? queue : null);

        /// <summary>It was my turn in the queue: the server already moved me – ride up and show the floor.</summary>
        private void OnElevatorArrived(RoomSnapshotDto snapshot)
        {
            ShowQueue(null);
            RunAsync(() => RideAsync(_current?.Floor ?? 0, snapshot.Room.Floor ?? 0, () => ShowSnapshotAsync(snapshot)));
        }

        private void ShowQueue(QueueStatusDto queue)
        {
            _queueBanner.style.display = queue == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (queue != null)
            {
                Q<Label>("queue-text").text = queue.Position == 1
                    ? $"Lift zu {FloorLabel(queue.Floor)} {queue.FloorName}: du bist als Nächstes dran"
                    : $"Lift zu {FloorLabel(queue.Floor)} {queue.FloorName}: Platz {queue.Position} in der Warteschlange";
            }
        }

        /// <summary>Doors close, the indicator counts floors, the new floor is built behind the doors, doors open.</summary>
        private async Task RideAsync(int from, int to, Func<Task> buildNewFloor)
        {
            _riding = true;
            var ride = Q<VisualElement>("lift-ride");
            var left = Q<VisualElement>("lift-door-left");
            var right = Q<VisualElement>("lift-door-right");
            var display = Q<VisualElement>("lift-display");
            var floorLabel = Q<Label>("lift-floor");
            Q<Label>("lift-arrow").text = to >= from ? "▲" : "▼";
            floorLabel.text = FloorLabel(from);
            ride.style.display = DisplayStyle.Flex;
            try
            {
                await Task.Yield();
                left.AddToClassList("lift-door--closed");
                right.AddToClassList("lift-door--closed");
                await Task.Delay(480);
                display.AddToClassList("lift-display--visible");

                var build = buildNewFloor();   // behind the closed doors
                var steps = Math.Abs(to - from);
                var stepMs = steps == 0 ? 0 : Mathf.Clamp(2400 / steps, 45, 220);
                for (var i = 1; i <= steps; i++)
                {
                    await Task.Delay(stepMs);
                    floorLabel.text = FloorLabel(from + Math.Sign(to - from) * i);
                }
                await build;
                await Task.Delay(350);   // "ding"
            }
            finally
            {
                display.RemoveFromClassList("lift-display--visible");
                left.RemoveFromClassList("lift-door--closed");
                right.RemoveFromClassList("lift-door--closed");
                await Task.Delay(480);
                ride.style.display = DisplayStyle.None;
                _riding = false;
            }
        }

        private static string FloorLabel(int floor) => floor == 0 ? "EG" : floor + ". OG";

        private void OnTileTapped(Vector2Int tile)
        {
            _pendingSeat = null;
            WalkTo(tile);
        }

        private void WalkTo(Vector2Int tile)
        {
            var me = _room.Avatar(_localUserId);
            if (me == null)
            {
                return;
            }
            if (me.IsSeated && me.Tile == tile)
            {
                _room.UnseatPlayer(_localUserId);   // tapped the floor right in front: just stand up
                RunAsync(() => _session.StandUpAsync());
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

        /// <summary>Walks next to the table/TV/lift and opens its game or the lift panel.</summary>
        private void OnStationTapped(GameStation station)
        {
            OnTileTapped(station.Tile);
            if (station.GameId == RoomView.ElevatorStation)
            {
                OpenLift();
                return;
            }
            OpenGame(station.GameId);
        }

        internal void OpenGame(string gameId)
        {
            if (gameId != null)
            {
                CloseLift();
            }
            _gamePanel.style.display = gameId == null ? DisplayStyle.None : DisplayStyle.Flex;
            var boardGame = gameId != null && BoardGames.All.Contains(gameId);
            Q<VisualElement>("ttt-panel").style.display = gameId == "tictactoe" ? DisplayStyle.Flex : DisplayStyle.None;
            Q<VisualElement>("quiz-panel").style.display = gameId == "quiz" ? DisplayStyle.Flex : DisplayStyle.None;
            Q<VisualElement>("board-panel").style.display = boardGame ? DisplayStyle.Flex : DisplayStyle.None;
            if (boardGame)
            {
                _boardGame.Open(gameId);
            }
            Q<Label>("game-title").text = gameId switch
            {
                "tictactoe" => "Tic-Tac-Toe",
                BoardGames.ConnectFour => "Vier gewinnt",
                BoardGames.Memory => "Memory",
                BoardGames.Chess => "Schach",
                _ => "Zürich-Quiz",
            };
            UpdateEmoteBar();
        }

        // ---------- Sitting ----------

        private SeatDto _pendingSeat;

        /// <summary>Walks to the tile in front of the place, then sits down (if it is still free).</summary>
        private void OnSeatTapped(Seat seat, int place)
        {
            var target = new SeatDto(seat.Item, place);
            if (!_room.IsFree(target, _localUserId))
            {
                SetStatus(_status, "Hier sitzt schon jemand.", isError: false);
                Root.schedule.Execute(() => SetStatus(_status, null)).StartingIn(2000);
                return;
            }
            GoSit(target);
        }

        private void GoSit(SeatDto target)
        {
            var me = _room.Avatar(_localUserId);
            var seat = _room.SeatFor(target.Item);
            if (me == null || seat == null)
            {
                return;
            }
            _pendingSeat = target;
            me.Arrived -= OnArrived;
            me.Arrived += OnArrived;
            var approach = seat.Approaches[target.Place];
            if (me.Tile == approach && !me.IsSeated)
            {
                OnArrived(me);
                return;
            }
            WalkTo(approach);
        }

        private void OnArrived(AvatarView me)
        {
            var target = _pendingSeat;
            _pendingSeat = null;
            if (target == null || me.IsSeated)
            {
                return;
            }
            RunAsync(async () =>
            {
                if (await _session.SitAsync(target))
                {
                    _room.SeatPlayer(_localUserId, target);
                }
                else
                {
                    SetStatus(_status, "Jemand war schneller – der Platz ist besetzt.", isError: false);
                    Root.schedule.Execute(() => SetStatus(_status, null)).StartingIn(2500);
                }
            });
        }

        /// <summary>The "Sitzen" button: nearest free seat within a few metres.</summary>
        private void SitNearby()
        {
            var me = _room.Avatar(_localUserId);
            if (me == null)
            {
                return;
            }
            if (me.IsSeated)
            {
                _room.UnseatPlayer(_localUserId);
                RunAsync(() => _session.StandUpAsync());
                return;
            }
            var seat = _room.NearestFreeSeat(me.Tile, maxDistance: 8f);
            if (seat == null)
            {
                SetStatus(_status, "Kein freier Sitzplatz in der Nähe – tippe auf einen Stuhl oder ein Sofa.", isError: false);
                Root.schedule.Execute(() => SetStatus(_status, null)).StartingIn(3000);
                return;
            }
            GoSit(seat);
        }

        private void OnPlayerSeated(PlayerSeatDto seated)
        {
            if (seated.Seat == null)
            {
                _room.UnseatPlayer(seated.UserId);
            }
            else
            {
                _room.SeatPlayer(seated.UserId, seated.Seat);
            }
        }

        private void PlayEmote(string emote)
        {
            if (emote == Emotes.Sit)
            {
                SitNearby();
                return;
            }
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
            _room.Avatar(message.UserId)?.Talk();
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
