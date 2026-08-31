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
    }
}
