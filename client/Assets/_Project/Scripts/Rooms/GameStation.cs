using UnityEngine;

namespace Reconnect.Client.Rooms
{
    /// <summary>An interactive minigame spot in a room (tic-tac-toe table, quiz TV). Tapping it opens the game.</summary>
    public sealed class GameStation : MonoBehaviour
    {
        /// <summary>"tictactoe" or "quiz".</summary>
        public string GameId { get; private set; }

        /// <summary>Tile the station stands on (players walk next to it).</summary>
        public Vector2Int Tile { get; private set; }

        public void Initialize(string gameId, Vector2Int tile)
        {
            GameId = gameId;
            Tile = tile;
        }
    }
}
