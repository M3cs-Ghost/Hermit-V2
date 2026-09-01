using System.Collections.Generic;

namespace Hermit.Games
{
    /// <summary>
    /// Immutable outcome of one playthrough. Deliberately small — this is not
    /// the shape a future backend submission will use, it is only what this
    /// vertical slice needs to prove a game can report a result without
    /// coupling to UI or to Networking. No coins, no rewards, no leaderboard
    /// fields (explicitly out of scope through C6).
    ///
    /// ContentSetId/ContentSchemaVersion were added in C6 so a result can
    /// answer "which content set, at which schema shape, produced this score"
    /// — the minimum needed later for "what did this student actually see".
    /// Both default to empty/0 for a game that has no content-set concept.
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

        public string ContentSetId { get; }
        public int ContentSchemaVersion { get; }

        /// <summary>Best consecutive-correct streak reached — 0 for a game with
        /// no combo concept. Added in C7 alongside Clásico's combo mechanic.</summary>
        public int BestStreak { get; }

        public IReadOnlyDictionary<string, string> Metadata { get; }

        public GameResult(
            string gameId,
            string sessionId,
            int score,
            int correct,
            int incorrect,
            float durationSeconds,
            bool completed,
            string contentSetId = "",
            int contentSchemaVersion = 0,
            int bestStreak = 0,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            GameId = gameId;
            SessionId = sessionId;
            Score = score;
            Correct = correct;
            Incorrect = incorrect;
            DurationSeconds = durationSeconds;
            Completed = completed;
            ContentSetId = contentSetId ?? string.Empty;
            ContentSchemaVersion = contentSchemaVersion;
            BestStreak = bestStreak;
            Metadata = metadata ?? EmptyMetadata;

            var total = correct + incorrect;
            AccuracyPercent = total > 0 ? correct / (float)total * 100f : 0f;
        }
    }
}
