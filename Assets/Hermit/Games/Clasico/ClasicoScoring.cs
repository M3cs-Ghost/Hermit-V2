using System;

namespace Hermit.Games.Clasico
{
    /// <summary>Pure, deterministic scoring — no UnityEngine, no randomness, so
    /// it is trivial to unit test exhaustively.</summary>
    public static class ClasicoScoring
    {
        /// <summary>Correct = base points plus a bonus that decays linearly from
        /// full at answerTimeSeconds=0 to zero at answerTimeSeconds&gt;=timeLimitSeconds.
        /// Incorrect is always 0, regardless of speed. Untimed (timeLimitSeconds
        /// &lt;= 0) or no bonus configured skips the bonus entirely.</summary>
        public static int ComputeQuestionScore(
            bool correct,
            float answerTimeSeconds,
            float timeLimitSeconds,
            int pointsPerCorrectAnswer,
            int maxSpeedBonusPoints)
        {
            if (!correct)
            {
                return 0;
            }

            if (timeLimitSeconds <= 0f || maxSpeedBonusPoints <= 0)
            {
                return pointsPerCorrectAnswer;
            }

            var remainingFraction = 1f - answerTimeSeconds / timeLimitSeconds;
            remainingFraction = Math.Max(0f, Math.Min(1f, remainingFraction));

            var bonus = (int)Math.Round(remainingFraction * maxSpeedBonusPoints);
            return pointsPerCorrectAnswer + bonus;
        }
    }
}
