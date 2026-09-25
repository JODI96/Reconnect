using System;
using System.Linq;
using System.Threading.Tasks;
using Reconnect.Client.Rooms;
using Reconnect.Contracts.Rooms;
using UnityEngine.UIElements;

namespace Reconnect.Client.UI.Games
{
    /// <summary>Zürich quiz on the TV: everyone in the room answers the same question.</summary>
    public sealed class QuizPanel
    {
        private readonly VisualElement _root;
        private readonly IRoomSession _session;
        private readonly Guid _me;
        private readonly Func<Func<Task>, VisualElement, bool> _run;
        private readonly Button[] _answers = new Button[4];
        private readonly Label _question;
        private readonly Label _status;
        private readonly Button _start;
        private readonly Button _next;
        private int? _myAnswer;

        public QuizPanel(VisualElement root, IRoomSession session, Guid me, Func<Func<Task>, VisualElement, bool> run)
        {
            _root = root;
            _session = session;
            _me = me;
            _run = run;
            _question = root.Q<Label>("quiz-question");
            _status = root.Q<Label>("quiz-status");
            _start = root.Q<Button>("quiz-start");
            _next = root.Q<Button>("quiz-next");

            for (var i = 0; i < 4; i++)
            {
                var index = i;
                _answers[i] = root.Q<Button>("answer-" + i);
                _answers[i].clicked += () =>
                {
                    _myAnswer = index;
                    Act(() => _session.QuizAnswerAsync(index));
                };
            }
            _start.clicked += () => Act(_session.QuizStartAsync);
            _next.clicked += () => Act(_session.QuizNextAsync);
        }

        public void Render(QuizStateDto state)
        {
            if (state == null)
            {
                return;
            }

            var answered = state.Answered.Contains(_me);
            if (state.Status == GameStatus.Question && !answered)
            {
                _myAnswer = null;   // new question
            }

            var showQuestion = state.Status is GameStatus.Question or GameStatus.Reveal;
            _question.text = showQuestion
                ? $"Frage {state.Round}/{state.TotalRounds}\n{state.Question}"
                : state.Status == GameStatus.Finished ? "Quiz vorbei!" : "Zürich-Quiz: 5 Fragen, wer weiss am meisten?";

            for (var i = 0; i < 4; i++)
            {
                var visible = showQuestion && i < state.Answers.Count;
                _answers[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible)
                {
                    continue;
                }
                _answers[i].text = state.Answers[i];
                _answers[i].SetEnabled(state.Status == GameStatus.Question && !answered);
                _answers[i].EnableInClassList("quiz-answer--mine", _myAnswer == i);
                _answers[i].EnableInClassList("quiz-answer--correct", state.CorrectIndex == i);
                _answers[i].EnableInClassList("quiz-answer--wrong", state.CorrectIndex != null && _myAnswer == i && state.CorrectIndex != i);
            }

            var scores = string.Join("   ", state.Scores.Take(5).Select((s, i) => $"{i + 1}. {s.DisplayName} {s.Points}"));
            _status.text = state.Status switch
            {
                GameStatus.Question => (answered ? "Antwort gespeichert. " : "") + $"{state.Answered.Count} haben geantwortet.",
                GameStatus.Reveal => scores,
                GameStatus.Finished => "Endstand: " + (scores.Length > 0 ? scores : "keine Punkte"),
                _ => "",
            };

            _start.style.display = state.Status is GameStatus.Waiting or GameStatus.Finished ? DisplayStyle.Flex : DisplayStyle.None;
            _next.style.display = showQuestion ? DisplayStyle.Flex : DisplayStyle.None;
            _next.text = state.Status == GameStatus.Question ? "Auflösen" : "Nächste Frage";
        }

        private void Act(Func<Task<QuizStateDto>> call) => _run(async () => Render(await call()), _root);
    }
}
