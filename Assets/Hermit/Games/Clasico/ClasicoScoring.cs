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

        /// <summary>Flat bonus every Nth consecutive correct answer (e.g.
        /// threshold=3 -> a bonus lands on streaks 3, 6, 9...). Deterministic,
        /// no RNG, bounded to one bonus per question — evaluated and kept
        /// deliberately simple after considering a growing multiplier: a
        /// multiplier compounds without an obvious cap and reads as an
        /// "exploit evidente" for a long streak, which the brief explicitly
        /// asks to avoid. streakAfterThisAnswer is the streak count *after*
        /// this correct answer (1-based); 0 or a broken streak scores 0.</summary>
        public static int ComputeStreakBonus(int streakAfterThisAnswer, int streakBonusThreshold, int streakBonusPoints)
        {
            if (streakAfterThisAnswer <= 0 || streakBonusThreshold <= 0 || streakBonusPoints <= 0)
            {
                return 0;
            }

            return streakAfterThisAnswer % streakBonusThreshold == 0 ? streakBonusPoints : 0;
        }
    }
}
