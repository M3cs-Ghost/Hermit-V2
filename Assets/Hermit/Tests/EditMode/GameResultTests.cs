using NUnit.Framework;
using Hermit.Games;

namespace Hermit.Tests.EditMode
{
    public class GameResultTests
    {
        [Test]
        public void AccuracyPercent_ComputedFromCorrectAndIncorrect()
        {
            var result = new GameResult("g", "s", score: 300, correct: 3, incorrect: 1, durationSeconds: 10f, completed: true);
            Assert.AreEqual(75f, result.AccuracyPercent, 0.001f);
        }

        [Test]
        public void AccuracyPercent_IsZero_WhenNoQuestionsWereAnswered()
        {
            var result = new GameResult("g", "s", score: 0, correct: 0, incorrect: 0, durationSeconds: 0f, completed: false);
            Assert.AreEqual(0f, result.AccuracyPercent, 0.001f);
        }

        [Test]
        public void AccuracyPercent_IsHundred_WhenAllCorrect()
        {
            var result = new GameResult("g", "s", score: 100, correct: 1, incorrect: 0, durationSeconds: 1f, completed: true);
            Assert.AreEqual(100f, result.AccuracyPercent, 0.001f);
        }

        [Test]
        public void Metadata_DefaultsToEmpty_NotNull()
        {
            var result = new GameResult("g", "s", 0, 0, 0, 0f, true);
            Assert.IsNotNull(result.Metadata);
            Assert.AreEqual(0, result.Metadata.Count);
        }

        [Test]
        public void ContentSetIdAndSchemaVersion_DefaultToEmptyAndZero_ForAGameWithNoContentSetConcept()
        {
            var result = new GameResult("g", "s", 0, 0, 0, 0f, true);
            Assert.AreEqual(string.Empty, result.ContentSetId);
            Assert.AreEqual(0, result.ContentSchemaVersion);
        }

        [Test]
        public void ContentSetIdAndSchemaVersion_AreCarriedThrough_WhenProvided()
        {
            var result = new GameResult("g", "s", 0, 0, 0, 0f, true, contentSetId: "c5_sample", contentSchemaVersion: 2);
            Assert.AreEqual("c5_sample", result.ContentSetId);
            Assert.AreEqual(2, result.ContentSchemaVersion);
        }

        [Test]
        public void BestStreak_DefaultsToZero_ForAGameWithNoComboConcept()
        {
            var result = new GameResult("g", "s", 0, 0, 0, 0f, true);
            Assert.AreEqual(0, result.BestStreak);
        }

        [Test]
        public void BestStreak_IsCarriedThrough_WhenProvided()
        {
            var result = new GameResult("g", "s", 0, 0, 0, 0f, true, bestStreak: 7);
            Assert.AreEqual(7, result.BestStreak);
        }
    }
}
