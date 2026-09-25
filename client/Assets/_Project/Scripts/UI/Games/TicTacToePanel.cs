using System;
using System.Threading.Tasks;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Rooms;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Games
{
    /// <summary>Tic-tac-toe board over the room: take a seat, play, see the others' moves live.</summary>
    public sealed class TicTacToePanel
    {
        private readonly VisualElement _root;
        private readonly IRoomSession _session;
        private readonly Guid _me;
        private readonly Func<Func<Task>, VisualElement, bool> _run;
        private readonly Button[] _cells = new Button[9];
        private readonly Label _status;
        private readonly Button _join;
        private readonly Button _reset;

        /// <param name="run">Runs an async action with error handling (from the screen); returns false if busy.</param>
        public TicTacToePanel(VisualElement root, IRoomSession session, Guid me, Func<Func<Task>, VisualElement, bool> run)
        {
            _root = root;
            _session = session;
            _me = me;
            _run = run;
            _status = root.Q<Label>("ttt-status");
            _join = root.Q<Button>("ttt-join");
            _reset = root.Q<Button>("ttt-reset");

            for (var i = 0; i < 9; i++)
            {
                var cell = i;
                _cells[i] = root.Q<Button>("cell-" + i);
                _cells[i].clicked += () => Act(() => _session.TicTacToeMoveAsync(cell));
            }
            _join.clicked += () => Act(_session.TicTacToeJoinAsync);
            _reset.clicked += () => Act(_session.TicTacToeResetAsync);
        }

        public TicTacToeStateDto State { get; private set; }

        public void Render(TicTacToeStateDto state)
        {
            if (state == null)
            {
                return;
            }
            State = state;
            var seated = state.PlayerX == _me || state.PlayerO == _me;
            var myTurn = state.Status == GameStatus.Playing && state.Turn == _me;

            for (var i = 0; i < 9; i++)
            {
                var mark = state.Board[i];
                _cells[i].text = mark == '.' ? "" : mark.ToString();
                _cells[i].EnableInClassList("ttt-cell--x", mark == 'X');
                _cells[i].EnableInClassList("ttt-cell--o", mark == 'O');
                _cells[i].SetEnabled(myTurn && mark == '.');
            }

            var x = state.PlayerXName ?? "frei";
            var o = state.PlayerOName ?? "frei";
            _status.text = state.Status switch
            {
                GameStatus.Waiting => $"X: {x}   ·   O: {o}\nWarte auf Mitspieler …",
                GameStatus.Playing => myTurn ? "Du bist dran!" : $"{NameOf(state, state.Turn)} ist dran …",
                GameStatus.Won => state.Winner == _me ? "Du hast gewonnen!" : $"{NameOf(state, state.Winner)} hat gewonnen.",
                GameStatus.Draw => "Unentschieden!",
                _ => "",
            };
            _join.style.display = !seated && state.Status != GameStatus.Playing ? DisplayStyle.Flex : DisplayStyle.None;
            _reset.style.display = state.Status is GameStatus.Won or GameStatus.Draw || seated ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Act(Func<Task<TicTacToeStateDto>> call) =>
            _run(async () => Render(await call()), _root);

        private static string NameOf(TicTacToeStateDto state, Guid? userId) =>
            userId == state.PlayerX ? state.PlayerXName : userId == state.PlayerO ? state.PlayerOName : "?";
    }
}
