using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hermit.Games;
using Hermit.Games.Analytics;
using Hermit.Games.Clasico;
using Hermit.Games.Content;

namespace Hermit.Tests.EditMode
{
    public class ClasicoGameEngineTests
    {
        // Every question has exactly one option, so shuffling is a no-op and
        // index 0 is always the option under test — this isolates the engine's
        // state machine and scoring from option-shuffle order, which is covered
        // separately by QuestionSetContentProviderTests.
        private static QuestionDefinition SingleOption(string id, bool correct)
        {
            return new QuestionDefinition
            {
                Id = id,
                PromptText = $"Prompt for {id}",
                Options = new[] { new AnswerOption { Text = "Only option", IsCorrect = correct } }
            };
        }

        private static (ClasicoGameEngine engine, GameSession session) BeginEngine(
            QuestionDefinition[] questions,
            int questionCount = -1,
            float timePerQuestion = 0f,
            float feedbackSeconds = 1f,
            float countdownSeconds = 0f,
            int streakBonusThreshold = 0,
            int streakBonusPoints = 0)
        {
            var set = QuestionSet.CreateInMemory("set", "desc", questions);
            var definition = ClasicoGameDefinition.CreateInMemory(
                "clasico_test", "Clasico Test", set,
                questionCount < 0 ? questions.Length : questionCount,
                timePerQuestion, pointsPerCorrectAnswer: 100, maxSpeedBonusPoints: 0, feedbackDisplaySeconds: feedbackSeconds,
                countdownDurationSeconds: countdownSeconds, streakBonusThreshold: streakBonusThreshold, streakBonusPoints: streakBonusPoints);

            var context = new GameContext(NullGameAnalyticsSink.Instance, new System.Random(0));
            var session = new GameSession(definition.GameId);
            var engine = new ClasicoGameEngine();
            engine.Begin(context, definition, session);

            return (engine, session);
        }

        [Test]
        public void Begin_PresentsTheFirstQuestion()
        {
            // DrawQuestions samples without replacement, so which of the two
            // questions comes first is RNG-dependent — assert shape, not identity.
            // Draw order itself is covered by QuestionSetContentProviderTests.
            var (engine, _) = BeginEngine(new[] { SingleOption("q0", true), SingleOption("q1", true) });

            Assert.IsNotNull(engine.CurrentView);
            Assert.That(engine.CurrentView.QuestionId, Is.EqualTo("q0").Or.EqualTo("q1"));
            Assert.AreEqual(1, engine.CurrentView.QuestionNumber);
            Assert.AreEqual(2, engine.CurrentView.TotalQuestions);
        }

        [Test]
        public void SubmitAnswer_Correct_IncrementsScoreAndCorrectCount()
        {
            var (engine, session) = BeginEngine(new[] { SingleOption("q0", true) });

            engine.SubmitAnswer(0);

            Assert.AreEqual(100, session.Score);
            Assert.AreEqual(1, session.Correct);
            Assert.AreEqual(0, session.Incorrect);
            Assert.IsTrue(engine.IsRevealing);
            Assert.AreEqual(0, engine.CorrectOptionIndex);
        }

        [Test]
        public void SubmitAnswer_Incorrect_IncrementsIncorrectCount_NoScore()
        {
            var (engine, session) = BeginEngine(new[] { SingleOption("q0", false) });

            engine.SubmitAnswer(0);

            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(0, session.Correct);
            Assert.AreEqual(1, session.Incorrect);
        }

        [Test]
        public void SubmitAnswer_Twice_SecondCallIsIgnored()
        {
            var (engine, session) = BeginEngine(new[] { SingleOption("q0", true) });

            engine.SubmitAnswer(0);
            engine.SubmitAnswer(0);

            Assert.AreEqual(100, session.Score);
            Assert.AreEqual(1, session.Correct);
        }

        [Test]
        public void Tick_AfterRevealDuration_AdvancesToNextQuestion()
        {
            var (engine, _) = BeginEngine(
                new[] { SingleOption("q0", true), SingleOption("q1", true) },
                feedbackSeconds: 0.5f);
            var firstId = engine.CurrentView.QuestionId;
            var expectedSecondId = firstId == "q0" ? "q1" : "q0";

            engine.SubmitAnswer(0);
            engine.Tick(0.6f);

            Assert.IsFalse(engine.IsFinished);
            Assert.AreEqual(expectedSecondId, engine.CurrentView.QuestionId);
        }

        [Test]
        public void Tick_BeforeRevealDurationElapses_DoesNotAdvance()
        {
            var (engine, _) = BeginEngine(
                new[] { SingleOption("q0", true), SingleOption("q1", true) },
                feedbackSeconds: 1f);
            var firstId = engine.CurrentView.QuestionId;

            engine.SubmitAnswer(0);
            engine.Tick(0.2f);

            Assert.AreEqual(firstId, engine.CurrentView.QuestionId);
            Assert.IsTrue(engine.IsRevealing);
        }

        [Test]
        public void AllQuestionsAnswered_MarksTheEngineFinished()
        {
            var (engine, _) = BeginEngine(new[] { SingleOption("q0", true) }, feedbackSeconds: 0.1f);

            engine.SubmitAnswer(0);
            engine.Tick(0.2f);

            Assert.IsTrue(engine.IsFinished);
            Assert.IsNull(engine.CurrentView);
        }

        [Test]
        public void TimedQuestion_AutoSubmitsAsIncorrect_OnTimeout()
        {
            var (engine, session) = BeginEngine(new[] { SingleOption("q0", true) }, timePerQuestion: 5f);

            engine.Tick(5f);

            Assert.IsTrue(engine.IsRevealing);
            Assert.AreEqual(0, session.Correct);
            Assert.AreEqual(1, session.Incorrect);
        }

        [Test]
        public void BuildResult_ReflectsSessionTotals()
        {
            var (engine, session) = BeginEngine(new[] { SingleOption("q0", true) });
            engine.SubmitAnswer(0);
            session.ElapsedSeconds = 12.5f;

            var result = engine.BuildResult(completed: true);

            Assert.AreEqual(100, result.Score);
            Assert.AreEqual(1, result.Correct);
            Assert.AreEqual(0, result.Incorrect);
            Assert.AreEqual(12.5f, result.DurationSeconds, 0.001f);
            Assert.IsTrue(result.Completed);
            Assert.AreEqual("set", result.ContentSetId);
            Assert.AreEqual(1, result.ContentSchemaVersion);
        }

        [Test]
        public void EmptyQuestionPool_FinishesImmediately()
        {
            var (engine, _) = BeginEngine(Array.Empty<QuestionDefinition>());
            Assert.IsTrue(engine.IsFinished);
        }

        [Test]
        public void Begin_WithInvalidContent_LogsAWarning_ButDoesNotThrow()
        {
            var badQuestion = SingleOption("bad_q", true);
            badQuestion.Options = Array.Empty<AnswerOption>(); // triggers "no answer options"

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*ContentValidation.*bad_q.*"));

            Assert.DoesNotThrow(() => BeginEngine(new[] { badQuestion }));
        }

        // --- Countdown (C7) ---

        [Test]
        public void Begin_WithNoCountdownConfigured_SkipsStraightToFirstQuestion()
        {
            var (engine, _) = BeginEngine(new[] { SingleOption("q0", true) }, countdownSeconds: 0f);

            Assert.IsFalse(engine.IsCountingDown);
            Assert.IsNotNull(engine.CurrentView);
        }

        [Test]
        public void Begin_WithCountdownConfigured_StartsCountingDown_WithNoQuestionYet()
        {
            var (engine, _) = BeginEngine(new[] { SingleOption("q0", true) }, countdownSeconds: 3f);

            Assert.IsTrue(engine.IsCountingDown);
            Assert.IsNull(engine.CurrentView);
            Assert.AreEqual(3, engine.CountdownSecondsRemaining);
        }

        [Test]
        public void Tick_DuringCountdown_CountsDownWholeSeconds()
        {
            var (engine, _) = BeginEngine(new[] { SingleOption("q0", true) }, countdownSeconds: 3f);

            engine.Tick(1.1f);
            Assert.IsTrue(engine.IsCountingDown);
            Assert.AreEqual(2, engine.CountdownSecondsRemaining);
        }

        [Test]
        public void Tick_PastCountdownDuration_AdvancesToFirstQuestion()
        {
            var (engine, _) = BeginEngine(new[] { SingleOption("q0", true) }, countdownSeconds: 3f);

            engine.Tick(3.1f);

            Assert.IsFalse(engine.IsCountingDown);
            Assert.IsNotNull(engine.CurrentView);
            Assert.AreEqual("q0", engine.CurrentView.QuestionId);
        }

        [Test]
        public void SubmitAnswer_DuringCountdown_IsIgnored()
        {
            var (engine, session) = BeginEngine(new[] { SingleOption("q0", true) }, countdownSeconds: 3f);

            engine.SubmitAnswer(0);

            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(0, session.Correct + session.Incorrect);
            Assert.IsTrue(engine.IsCountingDown);
        }

        // --- Timer fraction (C7) ---

        [Test]
        public void QuestionTimeFraction01_IsOne_AtTheStartOfTheDecisionWindow()
        {
            var (engine, _) = BeginEngine(new[] { SingleOption("q0", true) }, timePerQuestion: 8f);
            Assert.AreEqual(1f, engine.QuestionTimeFraction01, 0.001f);
        }

        [Test]
        public void QuestionTimeFraction01_DecaysLinearlyToZero()
        {
            var (engine, _) = BeginEngine(new[] { SingleOption("q0", true) }, timePerQuestion: 8f);

            engine.Tick(4f);
            Assert.AreEqual(0.5f, engine.QuestionTimeFraction01, 0.01f);
        }

        [Test]
        public void QuestionTimeFraction01_IsAlwaysOne_WhenUntimed()
        {
            var (engine, _) = BeginEngine(new[] { SingleOption("q0", true) }, timePerQuestion: 0f);

            engine.Tick(100f);
            Assert.AreEqual(1f, engine.QuestionTimeFraction01, 0.001f);
        }

        // --- Streak (C7) ---

        [Test]
        public void ConsecutiveCorrectAnswers_IncreaseStreak_AndAwardBonusAtThreshold()
        {
            var questions = new[] { SingleOption("q0", true), SingleOption("q1", true), SingleOption("q2", true) };
            var (engine, session) = BeginEngine(questions, feedbackSeconds: 0.1f, streakBonusThreshold: 3, streakBonusPoints: 30);

            engine.SubmitAnswer(0);
            Assert.AreEqual(1, session.Streak);
            Assert.AreEqual(100, session.Score);

            engine.Tick(0.2f); // past reveal -> next question
            engine.SubmitAnswer(0);
            Assert.AreEqual(2, session.Streak);
            Assert.AreEqual(200, session.Score);

            engine.Tick(0.2f);
            engine.SubmitAnswer(0);
            Assert.AreEqual(3, session.Streak);
            Assert.AreEqual(330, session.Score, "Third consecutive correct answer should add the 30-point streak bonus.");
            Assert.AreEqual(3, session.BestStreak);
        }

        [Test]
        public void IncorrectAnswer_ResetsStreakToZero()
        {
            // Draw order is RNG-dependent (covered separately by
            // QuestionSetContentProviderTests) — read which question is
            // actually showing at each step rather than assuming "correct_q"
            // is drawn first; both option-1 and only-option questions keep
            // shuffling a no-op (see SingleOption's comment above), so this
            // is purely about not assuming *question* order.
            var questions = new[] { SingleOption("correct_q", true), SingleOption("incorrect_q", false) };
            var (engine, session) = BeginEngine(questions, feedbackSeconds: 0.1f, streakBonusThreshold: 3, streakBonusPoints: 30);

            var firstIsCorrect = engine.CurrentView.QuestionId == "correct_q";
            engine.SubmitAnswer(0);
            var streakAfterFirst = firstIsCorrect ? 1 : 0;
            Assert.AreEqual(streakAfterFirst, session.Streak);

            engine.Tick(0.2f);
            var secondIsCorrect = engine.CurrentView.QuestionId == "correct_q";
            engine.SubmitAnswer(0);
            var streakAfterSecond = secondIsCorrect ? streakAfterFirst + 1 : 0;
            Assert.AreEqual(streakAfterSecond, session.Streak, "An incorrect answer must reset the streak to 0 at the moment it happens.");

            Assert.AreEqual(1, session.BestStreak, "Exactly one answer in this sequence is correct, so the streak must have reached 1 at some point.");
        }

        [Test]
        public void BuildResult_IncludesBestStreak()
        {
            var (engine, session) = BeginEngine(new[] { SingleOption("q0", true) }, streakBonusThreshold: 3, streakBonusPoints: 30);
            engine.SubmitAnswer(0);

            var result = engine.BuildResult(completed: true);

            Assert.AreEqual(session.BestStreak, result.BestStreak);
            Assert.AreEqual(1, result.BestStreak);
        }
    }
}
