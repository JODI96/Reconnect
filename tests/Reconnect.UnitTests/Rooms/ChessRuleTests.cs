using Reconnect.Modules.Rooms.Domain.Minigames;

namespace Reconnect.UnitTests.Rooms;

public sealed class ChessRuleTests
{
    private static void Play(ChessBoard board, params string[] moves)
    {
        foreach (var move in moves)
        {
            Assert.True(board.TryMove(ChessBoard.Parse(move[..2]), ChessBoard.Parse(move[2..4]), move.Length > 4 ? move[4] : 'q'), move);
        }
    }

    /// <summary>64 squares from rank 8 down to rank 1 (as a diagram reads), turned into a1-first order.</summary>
    private static string Diagram(params string[] ranksFrom8) => string.Concat(ranksFrom8.Reverse());

    [Fact]
    public void The_start_position_has_twenty_moves()
    {
        Assert.Equal(20, new ChessBoard().LegalMoves().Count);
    }

    [Fact]
    public void Scholars_mate_is_checkmate()
    {
        var board = new ChessBoard();
        Play(board, "e2e4", "e7e5", "d1h5", "b8c6", "f1c4", "g8f6", "h5f7");

        Assert.True(board.IsCheckmate);
        Assert.False(board.IsDraw);
    }

    [Fact]
    public void Pieces_move_only_by_their_rules()
    {
        var board = new ChessBoard();

        Assert.False(board.TryMove(ChessBoard.Parse("e2"), ChessBoard.Parse("e5")));   // pawn three squares
        Assert.False(board.TryMove(ChessBoard.Parse("f1"), ChessBoard.Parse("c4")));   // bishop through its pawn
        Assert.False(board.TryMove(ChessBoard.Parse("e7"), ChessBoard.Parse("e5")));   // black when it's white's turn
        Assert.True(board.TryMove(ChessBoard.Parse("g1"), ChessBoard.Parse("f3")));    // knights jump
    }

    [Fact]
    public void Castling_moves_the_rook_and_is_gone_once_the_king_moved()
    {
        var board = new ChessBoard();
        Play(board, "e2e4", "e7e5", "g1f3", "b8c6", "f1c4", "g8f6", "e1g1");

        Assert.Equal('K', board[ChessBoard.Parse("g1")]);
        Assert.Equal('R', board[ChessBoard.Parse("f1")]);
        Assert.DoesNotContain('K', board.Castling);
        Assert.DoesNotContain('Q', board.Castling);
    }

    [Fact]
    public void No_castling_out_of_or_through_check()
    {
        // White king e1, rook h1; a black rook on f8 attacks f1.
        var board = ChessBoard.FromSquares(Diagram(
            "....kr..",
            "........",
            "........",
            "........",
            "........",
            "........",
            "........",
            "....K..R"), whiteToMove: true, castling: "K");

        Assert.False(board.TryMove(ChessBoard.Parse("e1"), ChessBoard.Parse("g1")));
    }

    [Fact]
    public void En_passant_takes_the_pawn_beside()
    {
        var board = new ChessBoard();
        Play(board, "e2e4", "a7a6", "e4e5", "d7d5", "e5d6");

        Assert.Equal('P', board[ChessBoard.Parse("d6")]);
        Assert.Equal('.', board[ChessBoard.Parse("d5")]);
    }

    [Fact]
    public void A_pawn_on_the_last_rank_becomes_the_chosen_piece()
    {
        var board = ChessBoard.FromSquares(Diagram(
            "........",
            "P......k",
            "........",
            "........",
            "........",
            "........",
            "........",
            "K......."), whiteToMove: true);

        Assert.True(board.TryMove(ChessBoard.Parse("a7"), ChessBoard.Parse("a8"), 'n'));
        Assert.Equal('N', board[ChessBoard.Parse("a8")]);
    }

    [Fact]
    public void A_pinned_piece_may_not_leave_the_king_in_check()
    {
        // White king e1, white bishop e2, black rook e8: the bishop is pinned.
        var board = ChessBoard.FromSquares(Diagram(
            "....r..k",
            "........",
            "........",
            "........",
            "........",
            "........",
            "....B...",
            "....K..."), whiteToMove: true);

        Assert.False(board.TryMove(ChessBoard.Parse("e2"), ChessBoard.Parse("d3")));
    }

    [Fact]
    public void Stalemate_is_a_draw()
    {
        // Black king a8 has no move and is not in check.
        var board = ChessBoard.FromSquares(Diagram(
            "k.......",
            "..Q.....",
            ".K......",
            "........",
            "........",
            "........",
            "........",
            "........"), whiteToMove: false);

        Assert.False(board.InCheck);
        Assert.True(board.IsDraw);
        Assert.False(board.IsCheckmate);
    }
}
