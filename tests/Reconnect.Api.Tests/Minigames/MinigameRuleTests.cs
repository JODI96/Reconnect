using Reconnect.Api.Features.Minigames;
using Reconnect.Contracts.Rooms;

namespace Reconnect.Api.Tests.Minigames;

public sealed class MinigameRuleTests
{
    private static readonly Guid Anna = Guid.NewGuid();
    private static readonly Guid Ben = Guid.NewGuid();

    [Fact]
    public void Tic_tac_toe_starts_when_both_seats_are_taken_and_X_begins()
    {
        var game = new TicTacToeGame();

        game.Join(Anna, "Anna");
        Assert.Equal(GameStatus.Waiting, game.Status);
        game.Join(Ben, "Ben");

        Assert.Equal(GameStatus.Playing, game.Status);
        Assert.Equal(Anna, game.Turn);
    }

    [Fact]
    public void Tic_tac_toe_detects_a_win_on_the_diagonal()
    {
        var game = Playing();

        foreach (var (player, cell) in new[] { (Anna, 0), (Ben, 1), (Anna, 4), (Ben, 2), (Anna, 8) })
        {
            game.Move(player, cell);
        }

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(Anna, game.Winner);
        Assert.Equal("XOO.X...X", game.Board);
    }

    [Fact]
    public void Tic_tac_toe_detects_a_draw()
    {
        var game = Playing();

        // X O X / X O O / O X X
        foreach (var (player, cell) in new[] { (Anna, 0), (Ben, 1), (Anna, 2), (Ben, 4), (Anna, 3), (Ben, 5), (Anna, 7), (Ben, 6), (Anna, 8) })
        {
            game.Move(player, cell);
        }

        Assert.Equal(GameStatus.Draw, game.Status);
        Assert.Null(game.Winner);
    }

    [Fact]
    public void Tic_tac_toe_rejects_moves_out_of_turn_and_on_taken_cells()
    {
        var game = Playing();

        Assert.Throws<MinigameException>(() => game.Move(Ben, 0));
        game.Move(Anna, 0);
        Assert.Throws<MinigameException>(() => game.Move(Ben, 0));
        Assert.Throws<MinigameException>(() => game.Join(Guid.NewGuid(), "Carl"));
    }

    [Fact]
    public void Leaving_player_frees_the_table()
    {
        var game = Playing();

        Assert.True(game.Leave(Ben));

        Assert.Equal(GameStatus.Waiting, game.Status);
        Assert.Null(game.PlayerX);
        Assert.False(game.Leave(Guid.NewGuid()));
    }

    [Fact]
    public void Quiz_scores_correct_answers_with_a_bonus_for_the_fastest()
    {
        var quiz = new QuizGame();
        quiz.Start(new Random(42));
        var correct = QuizQuestions.All[quiz.QuestionOrder[0]].Correct;

        quiz.Answer(Anna, "Anna", correct);
        quiz.Answer(Ben, "Ben", correct);
        Assert.Throws<MinigameException>(() => quiz.Answer(Ben, "Ben", correct));

        quiz.Next();   // reveal
        var state = quiz.ToDto();
        Assert.Equal(GameStatus.Reveal, state.Status);
        Assert.Equal(correct, state.CorrectIndex);
        Assert.Equal([2, 1], state.Scores.Select(s => s.Points));
        Assert.Equal("Anna", state.Scores[0].DisplayName);
    }

    [Fact]
    public void Quiz_hides_the_answer_until_revealed_and_finishes_after_five_rounds()
    {
        var quiz = new QuizGame();
        quiz.Start(new Random(1));
        Assert.Null(quiz.ToDto().CorrectIndex);
        Assert.Equal(4, quiz.ToDto().Answers.Count);

        for (var round = 0; round < QuizGame.RoundsPerGame; round++)
        {
            quiz.Next();   // reveal
            quiz.Next();   // next question / finished
        }

        Assert.Equal(GameStatus.Finished, quiz.Status);
        Assert.Throws<MinigameException>(() => quiz.Answer(Anna, "Anna", 0));
    }

    private static TicTacToeGame Playing()
    {
        var game = new TicTacToeGame();
        game.Join(Anna, "Anna");
        game.Join(Ben, "Ben");
        return game;
    }
}
