using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Rooms;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Games
{
    /// <summary>
    /// Games for two at a table (Connect Four, Memory, chess): take a seat, play, see the other's moves live.
    /// One panel for all three; the board is drawn for the game that is open.
    /// </summary>
    public sealed class BoardGamePanel
    {
        /// <summary>Memory card faces 'A'–'H': pictures from the build catalog.</summary>
        private static readonly string[] MemoryPictures =
        {
            "ph-sofa_02", "ph-Chandelier_01", "ph-anthurium_botany_01", "ph-marble_bust_01",
            "ph-wine_bottles_01", "ph-bronze_shark_statue", "ph-chess_set", "ph-brass_vase_02",
        };

        /// <summary>German piece letters as a fallback when there is no picture of the piece.</summary>
        private static readonly Dictionary<char, string> PieceLetters = new()
        {
            ['k'] = "K", ['q'] = "D", ['r'] = "T", ['b'] = "L", ['n'] = "S", ['p'] = "B",
        };

        private readonly IRoomSession _session;
        private readonly Guid _me;
        private readonly Func<Func<Task>, VisualElement, bool> _run;
        private readonly Func<string, Texture2D> _picture;
        private readonly Label _status;
        private readonly VisualElement _grid;
        private readonly Button _join;
        private readonly Button _reset;
        private readonly Dictionary<string, BoardGameStateDto> _states = new();
        private string _game;
        private int? _selected;

        /// <param name="picture">Picture of a catalog item or chess piece ("chess-white-k"), or null.</param>
        public BoardGamePanel(VisualElement root, IRoomSession session, Guid me, Func<Func<Task>, VisualElement, bool> run,
            Func<string, Texture2D> picture)
        {
            _session = session;
            _me = me;
            _run = run;
            _picture = picture;
            _status = root.Q<Label>("board-status");
            _grid = root.Q<VisualElement>("board-grid");
            _join = root.Q<Button>("board-join");
            _reset = root.Q<Button>("board-reset");
            _join.clicked += () => Act(() => _session.BoardGameJoinAsync(_game));
            _reset.clicked += () =>
            {
                _selected = null;
                Act(() => _session.BoardGameResetAsync(_game));
            };
        }

        /// <summary>The game currently shown (connectfour / memory / chess).</summary>
        public string Game => _game;

        public BoardGameStateDto State(string game) => _states.TryGetValue(game, out var state) ? state : null;

        public void Open(string game)
        {
            _game = game;
            _selected = null;
            Draw();
        }

        public void Render(BoardGameStateDto state)
        {
            if (state == null)
            {
                return;
            }
            _states[state.Game] = state;
            if (state.Game == _game)
            {
                Draw();
            }
        }

        private BoardGameStateDto Current => State(_game) ?? new BoardGameStateDto(_game, GameStatus.Waiting, EmptyBoard(_game),
            null, null, null, null, null, null, null);

        private void Draw()
        {
            if (_game == null)
            {
                return;
            }
            var state = Current;
            var seated = state.PlayerA == _me || state.PlayerB == _me;
            var myTurn = state.Status == GameStatus.Playing && state.Turn == _me;
            _status.text = StatusText(state, seated, myTurn);
            _join.style.display = !seated && (state.PlayerA == null || state.PlayerB == null) ? DisplayStyle.Flex : DisplayStyle.None;
            _reset.style.display = seated || state.Status is GameStatus.Won or GameStatus.Draw ? DisplayStyle.Flex : DisplayStyle.None;

            _grid.Clear();
            switch (_game)
            {
                case BoardGames.ConnectFour: DrawConnectFour(state, myTurn); break;
                case BoardGames.Memory: DrawMemory(state, myTurn); break;
                case BoardGames.Chess: DrawChess(state, myTurn); break;
            }
        }

        private string StatusText(BoardGameStateDto state, bool seated, bool myTurn)
        {
            string Name(Guid? player) => player == state.PlayerA ? state.PlayerAName : state.PlayerBName;
            var extra = _game switch
            {
                BoardGames.Memory when state.Info is { } pairs => $" · Paare {pairs.Replace(":", " : ")}",
                BoardGames.Chess when state.Info?.StartsWith("check") == true => " – Schach!",
                _ => "",
            };
            return state.Status switch
            {
                GameStatus.Waiting => seated ? "Warte auf eine zweite Person …" : "Setz dich dazu: „Mitspielen“.",
                GameStatus.Playing => (myTurn ? "Du bist am Zug" : $"{Name(state.Turn)} ist am Zug") + extra,
                GameStatus.Won => (state.Winner == _me ? "Du hast gewonnen!" : $"{Name(state.Winner)} gewinnt.") + extra,
                GameStatus.Draw => "Unentschieden." + extra,
                _ => "",
            };
        }

        // ---------- Connect Four: 7 columns, top row first on screen ----------

        private void DrawConnectFour(BoardGameStateDto state, bool myTurn)
        {
            for (var row = 5; row >= 0; row--)
            {
                var line = Row();
                for (var column = 0; column < 7; column++)
                {
                    var disc = state.Board[row * 7 + column];
                    var col = column;
                    var cell = Cell("c4-cell", () => Act(() => _session.BoardGameMoveAsync(_game, col.ToString())));
                    cell.EnableInClassList("c4-cell--a", disc == 'A');
                    cell.EnableInClassList("c4-cell--b", disc == 'B');
                    cell.SetEnabled(myTurn);
                    line.Add(cell);
                }
                _grid.Add(line);
            }
        }

        // ---------- Memory: 4 × 4 cards ----------

        private void DrawMemory(BoardGameStateDto state, bool myTurn)
        {
            for (var row = 0; row < 4; row++)
            {
                var line = Row();
                for (var column = 0; column < 4; column++)
                {
                    var index = row * 4 + column;
                    var face = state.Board[index];
                    var card = Cell("memory-card", () => Act(() => _session.BoardGameMoveAsync(_game, index.ToString())));
                    card.EnableInClassList("memory-card--back", face == '?');
                    card.EnableInClassList("memory-card--found", char.IsLower(face));
                    if (face != '?')
                    {
                        var number = char.ToUpperInvariant(face) - 'A';
                        var picture = number is >= 0 and < 8 ? _picture?.Invoke(MemoryPictures[number]) : null;
                        if (picture != null)
                        {
                            card.style.backgroundImage = new StyleBackground(picture);
                        }
                        else
                        {
                            card.text = char.ToUpperInvariant(face).ToString();
                        }
                    }
                    card.SetEnabled(myTurn && face == '?');
                    line.Add(card);
                }
                _grid.Add(line);
            }
        }

        // ---------- Chess: tap a piece, then where it goes ----------

        private void DrawChess(BoardGameStateDto state, bool myTurn)
        {
            var white = state.PlayerB != _me;   // player A has white; spectators see white's view
            var mine = state.PlayerA == _me ? 'W' : state.PlayerB == _me ? 'B' : '-';
            for (var screenRow = 0; screenRow < 8; screenRow++)
            {
                var line = Row();
                for (var screenColumn = 0; screenColumn < 8; screenColumn++)
                {
                    var rank = white ? 7 - screenRow : screenRow;
                    var file = white ? screenColumn : 7 - screenColumn;
                    var square = rank * 8 + file;
                    var piece = state.Board[square];
                    var cell = Cell("chess-square", () => ChessTap(square, mine, myTurn));
                    cell.AddToClassList((rank + file) % 2 == 0 ? "chess-square--dark" : "chess-square--light");
                    cell.EnableInClassList("chess-square--selected", _selected == square);
                    cell.EnableInClassList("chess-square--last", state.Info != null && state.Info.Length >= 4 && IsLastMove(state.Info, square));
                    if (piece != '.')
                    {
                        var picture = _picture?.Invoke((char.IsUpper(piece) ? "chess-white-" : "chess-black-") + char.ToLowerInvariant(piece));
                        if (picture != null)
                        {
                            cell.style.backgroundImage = new StyleBackground(picture);
                        }
                        else
                        {
                            cell.text = PieceLetters[char.ToLowerInvariant(piece)];
                            cell.EnableInClassList("chess-piece--black", char.IsLower(piece));
                        }
                    }
                    cell.SetEnabled(myTurn);
                    line.Add(cell);
                }
                _grid.Add(line);
            }
        }

        private void ChessTap(int square, char mine, bool myTurn)
        {
            if (!myTurn)
            {
                return;
            }
            var piece = Current.Board[square];
            var own = piece != '.' && (char.IsUpper(piece) ? 'W' : 'B') == mine;
            if (_selected is { } from && !own)
            {
                _selected = null;
                var move = Name(from) + Name(square);
                Act(() => _session.BoardGameMoveAsync(_game, move));
                return;
            }
            _selected = own ? square : null;
            Draw();
        }

        private static bool IsLastMove(string info, int square)
        {
            var move = info.StartsWith("check") ? info.Substring(5).Trim() : info;
            return move.Length >= 4 && (move.Substring(0, 2) == Name(square) || move.Substring(2, 2) == Name(square));
        }

        private static string Name(int square) => $"{(char)('a' + square % 8)}{square / 8 + 1}";

        // ---------- Helpers ----------

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.AddToClassList("board-row");
            return row;
        }

        private static Button Cell(string kind, Action clicked)
        {
            var cell = new Button(clicked);
            cell.AddToClassList("board-cell");
            cell.AddToClassList(kind);
            return cell;
        }

        private static string EmptyBoard(string game) => game switch
        {
            BoardGames.ConnectFour => new string('.', 42),
            BoardGames.Memory => new string('?', 16),
            _ => "RNBQKBNRPPPPPPPP................................pppppppprnbqkbnr",
        };

        private void Act(Func<Task<BoardGameStateDto>> action) =>
            _run(async () => Render(await action()), _grid);
    }
}
