using Reconnect.Contracts.Rooms;
using Reconnect.Modules.Rooms.Domain.Minigames;

namespace Reconnect.UnitTests.Rooms;

public sealed class BoardGameRuleTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Ben = Guid.NewGuid();

    private static T Playing<T>(T game) where T : TwoPlayerGame
    {
        game.Join(Anna, "Anna");
        game.Join(Ben, "Ben");
        return game;
    }

    [Fact]
    public void Connect_four_needs_two_players_and_takes_turns()
    {
        var game = new ConnectFourGame();
        game.Join(Anna, "Anna");
        Assert.Equal(GameStatus.Waiting, game.Status);
        game.Join(Ben, "Ben");

        Assert.Equal(GameStatus.Playing, game.Status);
        Assert.Throws<MinigameException>(() => game.Move(Ben, "0"));   // Anna starts
        game.Move(Anna, "3");
        Assert.Equal(Ben, game.Turn);
        Assert.Equal('A', game.Board[3]);   // bottom row
    }

    [Fact]
    public void Connect_four_discs_stack_and_four_in_a_row_wins()
    {
        var game = Playing(new ConnectFourGame());
        foreach (var (player, column) in new[] { (Anna, 0), (Ben, 0), (Anna, 1), (Ben, 1), (Anna, 2), (Ben, 2) })
        {
            game.Move(player, column.ToString());
        }
        Assert.Equal('B', game.Board[ConnectFourGame.Columns]);   // second row, column 0

        game.Move(Anna, "3");

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(Anna, game.Winner);
    }

    [Fact]
    public void Connect_four_rejects_a_full_column()
    {
        var game = Playing(new ConnectFourGame());
        for (var i = 0; i < ConnectFourGame.Rows; i++)
        {
            game.Move(i % 2 == 0 ? Anna : Ben, "5");
        }

        Assert.Throws<MinigameException>(() => game.Move(Anna, "5"));
    }

    [Fact]
    public void Memory_hides_the_faces_and_a_pair_lets_the_player_go_again()
    {
        var game = new MemoryGame { Seed = 1 };
        Playing(game);
        Assert.Equal(new string('?', MemoryGame.Cards), game.ToDto().Board);

        var first = 0;
        var partner = Enumerable.Range(1, MemoryGame.Cards - 1).First(i => game.Faces[i] == game.Faces[first]);
        game.Move(Anna, first.ToString());
        game.Move(Anna, partner.ToString());

        Assert.Equal(Anna, game.Turn);
        Assert.Equal(1, game.PairsA);
        Assert.Equal(char.ToLowerInvariant(game.Faces[first]), game.ToDto().Board[first]);
    }

    [Fact]
    public void Memory_a_miss_passes_the_turn_and_covers_the_cards_on_the_next_move()
    {
        var game = new MemoryGame { Seed = 2 };
        Playing(game);
        var first = 0;
        var other = Enumerable.Range(1, MemoryGame.Cards - 1).First(i => game.Faces[i] != game.Faces[first]);
        game.Move(Anna, first.ToString());
        game.Move(Anna, other.ToString());
        Assert.Equal(Ben, game.Turn);
        Assert.Equal(game.Faces[other], game.ToDto().Board[other]);   // both visible for a moment

        var next = Enumerable.Range(0, MemoryGame.Cards).First(i => i != first && i != other);
        game.Move(Ben, next.ToString());

        Assert.Equal('?', game.ToDto().Board[first]);
        Assert.Equal('?', game.ToDto().Board[other]);
    }

    [Fact]
    public void Chess_game_checks_moves_and_ends_with_mate()
    {
        var game = Playing(new ChessGame());

        Assert.Throws<MinigameException>(() => game.Move(Anna, "e2e5"));
        foreach (var (player, move) in new[] { (Anna, "f2f3"), (Ben, "e7e5"), (Anna, "g2g4"), (Ben, "d8h4") })
        {
            game.Move(player, move);
        }

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(Ben, game.Winner);   // fool's mate
    }

    [Fact]
    public void Leaving_frees_the_table()
    {
        var game = Playing(new ChessGame());

        Assert.True(game.Leave(Ben));

        Assert.Equal(GameStatus.Waiting, game.Status);
        Assert.Null(game.PlayerA);
    }
}
