using Reconnect.SharedKernel.Domain;
using Reconnect.Contracts.Rooms;

namespace Reconnect.Modules.Rooms.Domain.Minigames;

/// <summary>A rule was broken (not your turn, cell taken …). The hub reports it to the caller.</summary>
internal sealed class MinigameException(string message) : DomainException(message);

/// <summary>Tic-tac-toe at a table in a room. Plain state + rules, stored as JSON in Redis.</summary>
internal sealed class TicTacToeGame
{
    private static readonly int[][] Lines =
    [
        [0, 1, 2], [3, 4, 5], [6, 7, 8],   // rows
        [0, 3, 6], [1, 4, 7], [2, 5, 8],   // columns
        [0, 4, 8], [2, 4, 6],              // diagonals
    ];

    public string Status { get; set; } = GameStatus.Waiting;
    public string Board { get; set; } = ".........";
    public Guid? PlayerX { get; set; }
    public string? PlayerXName { get; set; }
    public Guid? PlayerO { get; set; }
    public string? PlayerOName { get; set; }
    public Guid? Turn { get; set; }
    public Guid? Winner { get; set; }

    public void Join(Guid userId, string displayName)
    {
        if (Status is GameStatus.Won or GameStatus.Draw)
        {
            Reset();
        }
        if (PlayerX == userId || PlayerO == userId)
        {
            return;   // already seated
        }

        if (PlayerX is null)
        {
            (PlayerX, PlayerXName) = (userId, displayName);
        }
        else if (PlayerO is null)
        {
            (PlayerO, PlayerOName) = (userId, displayName);
        }
        else
        {
            throw new MinigameException("Both seats are taken.");
        }

        if (PlayerX is not null && PlayerO is not null)
        {
            Status = GameStatus.Playing;
            Turn = PlayerX;
        }
    }

    public void Move(Guid userId, int cell)
    {
        if (Status != GameStatus.Playing)
        {
            throw new MinigameException("The game is not running.");
        }
        if (Turn != userId)
        {
            throw new MinigameException("It's not your turn.");
        }
        if (cell is < 0 or > 8 || Board[cell] != '.')
        {
            throw new MinigameException("That cell is not free.");
        }

        var mark = userId == PlayerX ? 'X' : 'O';
        var board = Board.ToCharArray();
        board[cell] = mark;
        Board = new string(board);

        if (Lines.Any(line => line.All(i => Board[i] == mark)))
        {
            Status = GameStatus.Won;
            Winner = userId;
            Turn = null;
        }
        else if (!Board.Contains('.'))
        {
            Status = GameStatus.Draw;
            Turn = null;
        }
        else
        {
            Turn = userId == PlayerX ? PlayerO : PlayerX;
        }
    }

    /// <summary>A player leaves the room: the game ends and the table is free again.</summary>
    public bool Leave(Guid userId)
    {
        if (PlayerX != userId && PlayerO != userId)
        {
            return false;
        }
        Reset();
        return true;
    }

    public void Reset()
    {
        Status = GameStatus.Waiting;
        Board = ".........";
        PlayerX = PlayerO = Turn = Winner = null;
        PlayerXName = PlayerOName = null;
    }

    public TicTacToeStateDto ToDto() => new(Status, Board, PlayerX, PlayerXName, PlayerO, PlayerOName, Turn, Winner);
}
