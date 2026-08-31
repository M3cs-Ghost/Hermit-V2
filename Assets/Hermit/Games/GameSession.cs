using System;

namespace Hermit.Games
{
    /// <summary>
    /// Mutable state for one playthrough. Created fresh by
    /// <see cref="GameFlowController"/> for every Start/Restart, then handed to
    /// the active <see cref="IGameEngine"/>, which is the only thing expected to
    /// write to it as play proceeds.
    ///
    /// Score/Round/Correct/Incorrect are here (not left to each engine to invent
    /// its own names for) because C5's own brief asks the framework to prove
    /// these exact concepts generically. A future game shaped nothing like a
    /// quiz (no right/wrong, no discrete rounds) can simply leave them at 0 —
    /// acceptable for now; revisit only once a second real game actually needs
    /// something these four fields cannot express.
    /// </summary>
    public sealed class GameSession
    {
        public string SessionId { get; }
        public string GameId { get; }
        public DateTime StartedAtUtc { get; }

        public float ElapsedSeconds { get; set; }
        public int Score { get; set; }
        public int Round { get; set; }
        public int Correct { get; set; }
        public int Incorrect { get; set; }

        public GameSession(string gameId)
        {
            GameId = gameId;
            SessionId = Guid.NewGuid().ToString("N");
            StartedAtUtc = DateTime.UtcNow;
        }
    }
}
