using Reconnect.Contracts.Rooms;

namespace Reconnect.Modules.Rooms.Domain.Minigames;

/// <summary>
/// A game for two at a table (Connect Four, Memory, chess): two seats, whose turn it is, winner, reset. Plain state
/// stored as JSON in Redis; the concrete game keeps its board as a string and checks the moves.
/// </summary>
internal abstract class TwoPlayerGame
{
    public string Status { get; set; } = GameStatus.Waiting;
    public Guid? PlayerA { get; set; }
    public string? PlayerAName { get; set; }
    public Guid? PlayerB { get; set; }
    public string? PlayerBName { get; set; }
    public Guid? Turn { get; set; }
    public Guid? Winner { get; set; }

    /// <summary>The id the hub and the client use ("connectfour", "memory", "chess").</summary>
    protected abstract string GameId { get; }

    /// <summary>What everyone sees of the board (Memory hides the faces of covered cards).</summary>
    protected abstract string VisibleBoard { get; }

    /// <summary>Extra line for the players (check, scores …).</summary>
    protected virtual string? Info => null;

    public void Join(Guid userId, string displayName)
    {
        if (Status is GameStatus.Won or GameStatus.Draw)
        {
            Reset();
        }
        if (PlayerA == userId || PlayerB == userId)
        {
            return;
        }
        if (PlayerA is null)
        {
            (PlayerA, PlayerAName) = (userId, displayName);
        }
        else if (PlayerB is null)
        {
            (PlayerB, PlayerBName) = (userId, displayName);
        }
        else
        {
            throw new MinigameException("Beide Plätze sind besetzt.");
        }
        if (PlayerA is not null && PlayerB is not null)
        {
            Status = GameStatus.Playing;
            Turn = PlayerA;
            Begin();
        }
    }

    public void Move(Guid userId, string move)
    {
        if (Status != GameStatus.Playing)
        {
            throw new MinigameException("Das Spiel läuft gerade nicht.");
        }
        if (Turn != userId)
        {
            throw new MinigameException("Du bist nicht am Zug.");
        }
        Play(userId, move ?? "");
    }

    /// <summary>A player leaves the room: the game ends and the table is free again.</summary>
    public bool Leave(Guid userId)
    {
        if (PlayerA != userId && PlayerB != userId)
        {
            return false;
        }
        Reset();
        return true;
    }

    public void Reset()
    {
        Status = GameStatus.Waiting;
        PlayerA = PlayerB = Turn = Winner = null;
        PlayerAName = PlayerBName = null;
        Clear();
    }

    public BoardGameStateDto ToDto() => new(GameId, Status, VisibleBoard, PlayerA, PlayerAName, PlayerB, PlayerBName, Turn, Winner, Info);

    protected Guid Other(Guid player) => player == PlayerA ? PlayerB!.Value : PlayerA!.Value;

    protected void Win(Guid player)
    {
        Status = GameStatus.Won;
        Winner = player;
        Turn = null;
    }

    protected void Draw()
    {
        Status = GameStatus.Draw;
        Turn = null;
    }

    /// <summary>Both seats are taken: set up a fresh board.</summary>
    protected abstract void Begin();

    /// <summary>A move of the player whose turn it is (already checked).</summary>
    protected abstract void Play(Guid player, string move);

    protected abstract void Clear();
}

/// <summary>
/// Connect Four: 7 columns × 6 rows; drop a disc into a column, four in a row (any direction) wins.
/// Board = 42 characters, bottom row first: 'A' / 'B' for the players' discs, '.' empty. Move = column "0"–"6".
/// </summary>
internal sealed class ConnectFourGame : TwoPlayerGame
{
    public const int Columns = 7;
    public const int Rows = 6;

    public string Board { get; set; } = new('.', Columns * Rows);

    protected override string GameId => BoardGames.ConnectFour;
    protected override string VisibleBoard => Board;

    protected override void Begin() => Board = new string('.', Columns * Rows);

    protected override void Clear() => Board = new string('.', Columns * Rows);

    protected override void Play(Guid player, string move)
    {
        if (!int.TryParse(move, out var column) || column is < 0 or >= Columns)
        {
            throw new MinigameException("Diese Spalte gibt es nicht.");
        }
        var row = Enumerable.Range(0, Rows).FirstOrDefault(r => Board[r * Columns + column] == '.', -1);
        if (row < 0)
        {
            throw new MinigameException("Die Spalte ist voll.");
        }
        var disc = player == PlayerA ? 'A' : 'B';
        var cells = Board.ToCharArray();
        cells[row * Columns + column] = disc;
        Board = new string(cells);

        if (FourInARow(row, column, disc))
        {
            Win(player);
        }
        else if (!Board.Contains('.'))
        {
            Draw();
        }
        else
        {
            Turn = Other(player);
        }
    }

    private bool FourInARow(int row, int column, char disc)
    {
        foreach (var (dr, dc) in new[] { (0, 1), (1, 0), (1, 1), (1, -1) })
        {
            var count = 1 + Count(row, column, dr, dc, disc) + Count(row, column, -dr, -dc, disc);
            if (count >= 4)
            {
                return true;
            }
        }
        return false;
    }

    private int Count(int row, int column, int dr, int dc, char disc)
    {
        var count = 0;
        for (var (r, c) = (row + dr, column + dc); r is >= 0 and < Rows && c is >= 0 and < Columns && Board[r * Columns + c] == disc; r += dr, c += dc)
        {
            count++;
        }
        return count;
    }
}

/// <summary>
/// Memory: 16 covered cards, 8 pairs (faces 'A'–'H'). Turn two cards; a pair stays open and the player goes again, else
/// the cards are covered at the next move and the other player's turn begins. Most pairs wins.
/// Visible board: '?' covered, the face letter when open, lower case = found pair. Move = card "0"–"15".
/// </summary>
internal sealed class MemoryGame : TwoPlayerGame
{
    public const int Cards = 16;

    /// <summary>Faces of all cards (secret).</summary>
    public string Faces { get; set; } = new('?', Cards);

    /// <summary>Per card: '0' covered, '1' turned this move, '2' found.</summary>
    public string Open { get; set; } = new('0', Cards);

    public int PairsA { get; set; }
    public int PairsB { get; set; }

    /// <summary>Seed for shuffling (tests set it).</summary>
    public int? Seed { get; set; }

    protected override string GameId => BoardGames.Memory;

    protected override string VisibleBoard => new(Enumerable.Range(0, Cards).Select(i => Open[i] switch
    {
        '1' => Faces[i],
        '2' => char.ToLowerInvariant(Faces[i]),
        _ => '?',
    }).ToArray());

    protected override string? Info => $"{PairsA}:{PairsB}";

    protected override void Begin()
    {
        var random = Seed is { } seed ? new Random(seed) : new Random();
        var faces = "AABBCCDDEEFFGGHH".ToCharArray();
        random.Shuffle(faces);
        Faces = new string(faces);
        Open = new string('0', Cards);
        PairsA = PairsB = 0;
    }

    protected override void Clear()
    {
        Faces = new string('?', Cards);
        Open = new string('0', Cards);
        PairsA = PairsB = 0;
    }

    protected override void Play(Guid player, string move)
    {
        if (!int.TryParse(move, out var card) || card is < 0 or >= Cards)
        {
            throw new MinigameException("Diese Karte gibt es nicht.");
        }
        var open = Open.ToCharArray();
        var turned = Enumerable.Range(0, Cards).Where(i => open[i] == '1').ToList();
        if (turned.Count == 2)
        {
            // The last two did not match: cover them, now it's this player's (already switched) turn.
            foreach (var i in turned)
            {
                open[i] = '0';
            }
            turned.Clear();
        }
        if (open[card] != '0')
        {
            throw new MinigameException("Diese Karte liegt schon offen.");
        }
        open[card] = '1';
        turned.Add(card);

        if (turned.Count == 2)
        {
            if (Faces[turned[0]] == Faces[turned[1]])
            {
                open[turned[0]] = open[turned[1]] = '2';
                if (player == PlayerA)
                {
                    PairsA++;
                }
                else
                {
                    PairsB++;
                }
                Open = new string(open);
                if (PairsA + PairsB == Cards / 2)
                {
                    if (PairsA == PairsB)
                    {
                        Draw();
                    }
                    else
                    {
                        Win(PairsA > PairsB ? PlayerA!.Value : PlayerB!.Value);
                    }
                }
                return;   // a pair: same player again
            }
            Turn = Other(player);   // no pair: both stay visible until the next move
        }
        Open = new string(open);
    }
}

/// <summary>
/// Chess (rules in <see cref="ChessBoard"/>): player A plays white. Board = 64 squares a1 first (FEN letters).
/// Move = "e2e4", promotion "e7e8q". Info: "check" while the side to move is in check.
/// </summary>
internal sealed class ChessGame : TwoPlayerGame
{
    public string Squares { get; set; } = new ChessBoard().Squares;
    public bool WhiteToMove { get; set; } = true;
    public string Castling { get; set; } = "KQkq";
    public int EnPassant { get; set; } = -1;
    public string? LastMove { get; set; }

    protected override string GameId => BoardGames.Chess;
    protected override string VisibleBoard => Squares;
    protected override string? Info => Status == GameStatus.Playing && Board().InCheck ? "check" + Last : LastMove;

    private string Last => LastMove is null ? "" : " " + LastMove;

    protected override void Begin() => Clear();

    protected override void Clear()
    {
        var board = new ChessBoard();
        Squares = board.Squares;
        WhiteToMove = true;
        Castling = board.Castling;
        EnPassant = -1;
        LastMove = null;
    }

    protected override void Play(Guid player, string move)
    {
        var from = move.Length >= 4 ? ChessBoard.Parse(move[..2]) : -1;
        var to = move.Length >= 4 ? ChessBoard.Parse(move[2..4]) : -1;
        var board = Board();
        if (from < 0 || to < 0 || !board.TryMove(from, to, move.Length > 4 ? move[4] : 'q'))
        {
            throw new MinigameException("Dieser Zug ist nicht erlaubt.");
        }
        Squares = board.Squares;
        WhiteToMove = board.WhiteToMove;
        Castling = board.Castling;
        EnPassant = board.EnPassant;
        LastMove = move[..4];

        if (board.IsCheckmate)
        {
            Win(player);
        }
        else if (board.IsDraw)
        {
            Draw();
        }
        else
        {
            Turn = Other(player);
        }
    }

    private ChessBoard Board() => ChessBoard.FromSquares(Squares, WhiteToMove, Castling, EnPassant);
}
