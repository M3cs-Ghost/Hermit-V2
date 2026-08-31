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
            float feedbackSeconds = 1f)
        {
            var set = QuestionSet.CreateInMemory("set", "desc", questions);
            var definition = ClasicoGameDefinition.CreateInMemory(
                "clasico_test", "Clasico Test", set,
                questionCount < 0 ? questions.Length : questionCount,
                timePerQuestion, pointsPerCorrectAnswer: 100, maxSpeedBonusPoints: 0, feedbackDisplaySeconds: feedbackSeconds);

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
    }
}
