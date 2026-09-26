using Reconnect.Contracts.Rooms;

namespace Reconnect.Modules.Rooms.Domain.Minigames;

/// <summary>
/// Zürich quiz for everyone in the room: a question with four answers, everyone answers once,
/// then the answer is revealed. Correct = 1 point, first correct answer = 1 bonus point.
/// </summary>
internal sealed class QuizGame
{
    public const int RoundsPerGame = 5;

    public string Status { get; set; } = GameStatus.Waiting;
    public List<int> QuestionOrder { get; set; } = [];
    public int Round { get; set; }
    public Dictionary<Guid, int> Scores { get; set; } = [];
    public Dictionary<Guid, string> Names { get; set; } = [];
    public Dictionary<Guid, int> CurrentAnswers { get; set; } = [];
    public Guid? FirstCorrect { get; set; }

    public void Start(Random random)
    {
        QuestionOrder = Enumerable.Range(0, QuizQuestions.All.Count).OrderBy(_ => random.Next()).Take(RoundsPerGame).ToList();
        Round = 1;
        Scores.Clear();
        Names.Clear();
        CurrentAnswers.Clear();
        FirstCorrect = null;
        Status = GameStatus.Question;
    }

    public void Answer(Guid userId, string displayName, int answerIndex)
    {
        if (Status != GameStatus.Question)
        {
            throw new MinigameException("No question is open.");
        }
        if (answerIndex is < 0 or > 3)
        {
            throw new MinigameException("Unknown answer.");
        }
        if (CurrentAnswers.ContainsKey(userId))
        {
            throw new MinigameException("You already answered.");
        }

        Names[userId] = displayName;
        Scores.TryAdd(userId, 0);
        CurrentAnswers[userId] = answerIndex;
        if (answerIndex == Current.Correct)
        {
            Scores[userId] += FirstCorrect is null ? 2 : 1;
            FirstCorrect ??= userId;
        }
    }

    /// <summary>Question → reveal; reveal → next question or finished.</summary>
    public void Next()
    {
        switch (Status)
        {
            case GameStatus.Question:
                Status = GameStatus.Reveal;
                break;
            case GameStatus.Reveal when Round < QuestionOrder.Count:
                Round++;
                CurrentAnswers.Clear();
                FirstCorrect = null;
                Status = GameStatus.Question;
                break;
            case GameStatus.Reveal:
                Status = GameStatus.Finished;
                break;
            default:
                throw new MinigameException("Start a new quiz first.");
        }
    }

    private QuizQuestion Current => QuizQuestions.All[QuestionOrder[Round - 1]];

    public QuizStateDto ToDto()
    {
        var showQuestion = Status is GameStatus.Question or GameStatus.Reveal;
        var scores = Scores
            .OrderByDescending(s => s.Value)
            .Select(s => new QuizScoreDto(s.Key, Names.GetValueOrDefault(s.Key, "?"), s.Value))
            .ToList();
        return new QuizStateDto(
            Status,
            Round,
            QuestionOrder.Count == 0 ? RoundsPerGame : QuestionOrder.Count,
            showQuestion ? Current.Text : null,
            showQuestion ? Current.Answers : [],
            Status == GameStatus.Reveal ? Current.Correct : null,
            scores,
            CurrentAnswers.Keys.ToList());
    }
}

internal sealed record QuizQuestion(string Text, string[] Answers, int Correct);

/// <summary>Zürich trivia – good icebreakers. Correct answer index is zero-based.</summary>
internal static class QuizQuestions
{
    public static readonly IReadOnlyList<QuizQuestion> All =
    [
        new("Wie heisst der Hausberg von Zürich?", ["Uetliberg", "Pilatus", "Rigi", "Säntis"], 0),
        new("Welcher Fluss fliesst vom Zürichsee durch die Altstadt?", ["Sihl", "Limmat", "Aare", "Rhein"], 1),
        new("Was wird am Sechseläuten verbrannt?", ["Ein Tannenbaum", "Ein Holzfass", "Der Böögg", "Ein Strohhut"], 2),
        new("Wie viele Türme hat das Grossmünster?", ["1", "2", "3", "4"], 1),
        new("Welche Kirche ist für ihre Chagall-Fenster berühmt?", ["Grossmünster", "St. Peter", "Predigerkirche", "Fraumünster"], 3),
        new("Was hat die Kirche St. Peter, das grösser ist als bei jeder anderen Kirche Europas?", ["Das Zifferblatt der Turmuhr", "Die Orgel", "Das Glockenspiel", "Das Kirchenschiff"], 0),
        new("In welchem Jahr wurde die ETH Zürich gegründet?", ["1798", "1833", "1855", "1901"], 2),
        new("Wie hoch ist der Prime Tower ungefähr?", ["88 m", "126 m", "156 m", "210 m"], 1),
        new("Welche Farben haben die Zürcher Trams?", ["Rot-weiss", "Grün-gelb", "Blau-weiss", "Ganz gelb"], 2),
        new("Welcher ist der grösste Bahnhof der Schweiz?", ["Bern", "Basel SBB", "Genf Cornavin", "Zürich HB"], 3),
        new("Welche Beilage gehört klassisch zum Zürcher Geschnetzelten?", ["Rösti", "Pommes frites", "Reis", "Spätzli"], 0),
        new("Welcher berühmte Physiker hat an der ETH Zürich studiert?", ["Isaac Newton", "Albert Einstein", "Niels Bohr", "Stephen Hawking"], 1),
        new("Welcher Techno-Umzug zieht jeden Sommer ums Seebecken?", ["Züri Fäscht", "Sechseläuten", "Street Parade", "Knabenschiessen"], 2),
        new("Auf welchem Platz wird der Böögg verbrannt?", ["Paradeplatz", "Helvetiaplatz", "Bellevue", "Sechseläutenplatz"], 3),
        new("Wie heisst die berühmte Einkaufsstrasse vom HB zum See?", ["Bahnhofstrasse", "Langstrasse", "Limmatquai", "Niederdorfstrasse"], 0),
    ];
}
