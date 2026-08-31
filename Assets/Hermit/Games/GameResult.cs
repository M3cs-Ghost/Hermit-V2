using System.Collections.Generic;

namespace Hermit.Games
{
    /// <summary>
    /// Immutable outcome of one playthrough. Deliberately small — this is not
    /// the shape a future backend submission will use, it is only what this
    /// vertical slice needs to prove a game can report a result without
    /// coupling to UI or to Networking. No coins, no rewards, no leaderboard
    /// fields (explicitly out of scope for C5).
    /// </summary>
    public sealed class GameResult
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
            new Dictionary<string, string>();

        public string GameId { get; }
        public string SessionId { get; }
        public int Score { get; }
        public int Correct { get; }
        public int Incorrect { get; }
        public float AccuracyPercent { get; }
        public float DurationSeconds { get; }

        /// <summary>True if the game reached its own natural end; false if the
        /// player left early (Abort).</summary>
        public bool Completed { get; }

        public IReadOnlyDictionary<string, string> Metadata { get; }

        public GameResult(
            string gameId,
            string sessionId,
            int score,
            int correct,
            int incorrect,
            float durationSeconds,
            bool completed,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            GameId = gameId;
            SessionId = sessionId;
            Score = score;
            Correct = correct;
            Incorrect = incorrect;
            DurationSeconds = durationSeconds;
            Completed = completed;
            Metadata = metadata ?? EmptyMetadata;

            var total = correct + incorrect;
            AccuracyPercent = total > 0 ? correct / (float)total * 100f : 0f;
        }
    }
}
