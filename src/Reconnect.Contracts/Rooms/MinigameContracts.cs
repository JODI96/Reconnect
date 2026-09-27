using System;
using System.Collections.Generic;

namespace Reconnect.Contracts.Rooms
{
    public static class Emotes
    {
        public const string Wave = "wave";
        public const string Yes = "yes";
        public const string No = "no";
        public const string Jump = "jump";
        public const string Sit = "sit";

        public static readonly string[] All = { Wave, Yes, No, Jump, Sit };
    }

    public sealed record EmoteDto(Guid UserId, string Emote);

    public static class GameStatus
    {
        public const string Waiting = "waiting";
        public const string Playing = "playing";
        public const string Won = "won";
        public const string Draw = "draw";
        public const string Question = "question";
        public const string Reveal = "reveal";
        public const string Finished = "finished";
    }

    /// <param name="Board">9 characters, row by row: 'X', 'O' or '.'.</param>
    public sealed record TicTacToeStateDto(
        string Status,
        string Board,
        Guid? PlayerX,
        string? PlayerXName,
        Guid? PlayerO,
        string? PlayerOName,
        Guid? Turn,
        Guid? Winner);

    /// <summary>Games for two at a table; the id is also the station item's game id.</summary>
    public static class BoardGames
    {
        public const string ConnectFour = "connectfour";
        public const string Memory = "memory";
        public const string Chess = "chess";

        public static readonly IReadOnlyList<string> All = new[] { ConnectFour, Memory, Chess };
    }

    /// <summary>
    /// State of a game for two (<see cref="BoardGames"/>). Board encoding per game: Connect Four 42 cells bottom row
    /// first ('A'/'B'/'.'), move = column "0"–"6"; Memory 16 cards ('?' covered, face letter open, lower case found),
    /// move = card "0"–"15", Info = "pairsA:pairsB"; chess 64 squares a1 first (FEN letters), move = "e2e4"/"e7e8q",
    /// Info = "check …" or the last move. Player A starts (white in chess).
    /// </summary>
    public sealed record BoardGameStateDto(
        string Game,
        string Status,
        string Board,
        Guid? PlayerA,
        string? PlayerAName,
        Guid? PlayerB,
        string? PlayerBName,
        Guid? Turn,
        Guid? Winner,
        string? Info);

    public sealed record QuizScoreDto(Guid UserId, string DisplayName, int Points);

    /// <param name="CorrectIndex">Only set while the answer is revealed.</param>
    /// <param name="Answered">Players who already answered the current question.</param>
    public sealed record QuizStateDto(
        string Status,
        int Round,
        int TotalRounds,
        string? Question,
        IReadOnlyList<string> Answers,
        int? CorrectIndex,
        IReadOnlyList<QuizScoreDto> Scores,
        IReadOnlyList<Guid> Answered);
}
