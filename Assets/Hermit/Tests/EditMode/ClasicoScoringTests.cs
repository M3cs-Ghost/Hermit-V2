using NUnit.Framework;
using Hermit.Games.Clasico;

namespace Hermit.Tests.EditMode
{
    public class ClasicoScoringTests
    {
        [Test]
        public void IncorrectAnswer_AlwaysScoresZero_RegardlessOfSpeed()
        {
            Assert.AreEqual(0, ClasicoScoring.ComputeQuestionScore(false, 0f, 8f, 100, 50));
            Assert.AreEqual(0, ClasicoScoring.ComputeQuestionScore(false, 100f, 8f, 100, 50));
        }

        [Test]
        public void CorrectAnswer_Instant_GetsFullSpeedBonus()
        {
            Assert.AreEqual(150, ClasicoScoring.ComputeQuestionScore(true, 0f, 8f, 100, 50));
        }

        [Test]
        public void CorrectAnswer_AtTimeLimit_GetsNoBonus()
        {
            Assert.AreEqual(100, ClasicoScoring.ComputeQuestionScore(true, 8f, 8f, 100, 50));
        }

        [Test]
        public void CorrectAnswer_PastTimeLimit_NeverGoesBelowBasePoints()
        {
            Assert.AreEqual(100, ClasicoScoring.ComputeQuestionScore(true, 20f, 8f, 100, 50));
        }

        [Test]
        public void CorrectAnswer_Halfway_GetsHalfBonus()
        {
            Assert.AreEqual(125, ClasicoScoring.ComputeQuestionScore(true, 4f, 8f, 100, 50));
        }

        [Test]
        public void Untimed_NeverAppliesABonus()
        {
            Assert.AreEqual(100, ClasicoScoring.ComputeQuestionScore(true, 0f, 0f, 100, 50));
        }

        [Test]
        public void ZeroMaxBonus_NeverAppliesABonus_EvenWhenTimed()
        {
            Assert.AreEqual(100, ClasicoScoring.ComputeQuestionScore(true, 0f, 8f, 100, 0));
        }

        // --- Streak bonus (C7) ---

        [Test]
        public void StreakBonus_LandsExactlyOnThresholdMultiples()
        {
            Assert.AreEqual(0, ClasicoScoring.ComputeStreakBonus(1, 3, 30));
            Assert.AreEqual(0, ClasicoScoring.ComputeStreakBonus(2, 3, 30));
            Assert.AreEqual(30, ClasicoScoring.ComputeStreakBonus(3, 3, 30));
            Assert.AreEqual(0, ClasicoScoring.ComputeStreakBonus(4, 3, 30));
            Assert.AreEqual(0, ClasicoScoring.ComputeStreakBonus(5, 3, 30));
            Assert.AreEqual(30, ClasicoScoring.ComputeStreakBonus(6, 3, 30));
        }

        [Test]
        public void StreakBonus_ZeroOrNegativeStreak_IsZero()
        {
            Assert.AreEqual(0, ClasicoScoring.ComputeStreakBonus(0, 3, 30));
            Assert.AreEqual(0, ClasicoScoring.ComputeStreakBonus(-1, 3, 30));
        }

        [Test]
        public void StreakBonus_DisabledByZeroThresholdOrZeroPoints()
        {
            Assert.AreEqual(0, ClasicoScoring.ComputeStreakBonus(3, 0, 30));
            Assert.AreEqual(0, ClasicoScoring.ComputeStreakBonus(3, 3, 0));
        }

        [Test]
        public void StreakBonus_NeverCompounds_StaysFlatAcrossMultipleThresholdHits()
        {
            // A flat bonus every Nth streak, not a growing multiplier — see
            // ClasicoScoring.ComputeStreakBonus's doc-comment for why a
            // multiplier was rejected (unbounded growth reads as an exploit).
            Assert.AreEqual(30, ClasicoScoring.ComputeStreakBonus(3, 3, 30));
            Assert.AreEqual(30, ClasicoScoring.ComputeStreakBonus(30, 3, 30));
            Assert.AreEqual(30, ClasicoScoring.ComputeStreakBonus(300, 3, 30));
        }
    }
}
