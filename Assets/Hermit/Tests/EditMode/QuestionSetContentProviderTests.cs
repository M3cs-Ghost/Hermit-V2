using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Hermit.Games.Content;

namespace Hermit.Tests.EditMode
{
    public class QuestionSetContentProviderTests
    {
        private static QuestionSet BuildSet(int questionCount)
        {
            var questions = new QuestionDefinition[questionCount];
            for (var i = 0; i < questionCount; i++)
            {
                questions[i] = new QuestionDefinition
                {
                    Id = $"q{i}",
                    PromptText = $"Prompt {i}",
                    Options = new[] { new AnswerOption { Text = "A", IsCorrect = true } }
                };
            }

            return QuestionSet.CreateInMemory("test_set", "Test set", questions);
        }

        [Test]
        public void DrawQuestions_ReturnsExactlyTheRequestedCount_WhenPoolIsLargeEnough()
        {
            var provider = new QuestionSetContentProvider(BuildSet(10));
            var drawn = provider.DrawQuestions(4, new Random(1));

            Assert.AreEqual(4, drawn.Count);
        }

        [Test]
        public void DrawQuestions_NeverRepeatsAQuestion_WithinOneDraw()
        {
            var provider = new QuestionSetContentProvider(BuildSet(10));

            for (var seed = 0; seed < 20; seed++)
            {
                var drawn = provider.DrawQuestions(10, new Random(seed));
                var distinctIds = drawn.Select(q => q.Id).Distinct().Count();
                Assert.AreEqual(drawn.Count, distinctIds, $"Seed {seed} produced a repeated question.");
            }
        }

        [Test]
        public void DrawQuestions_RequestingMoreThanAvailable_CapsAtPoolSize_WithoutDuplicates()
        {
            var provider = new QuestionSetContentProvider(BuildSet(5));
            var drawn = provider.DrawQuestions(50, new Random(2));

            Assert.AreEqual(5, drawn.Count);
            Assert.AreEqual(5, drawn.Select(q => q.Id).Distinct().Count());
        }

        [Test]
        public void DrawQuestions_ZeroCount_ReturnsEmpty()
        {
            var provider = new QuestionSetContentProvider(BuildSet(5));
            var drawn = provider.DrawQuestions(0, new Random(3));

            Assert.AreEqual(0, drawn.Count);
        }

        [Test]
        public void DrawQuestions_OnlyEverReturnsQuestionsFromTheSet()
        {
            var set = BuildSet(6);
            var provider = new QuestionSetContentProvider(set);
            var validIds = new HashSet<string>(set.Questions.Select(q => q.Id));

            var drawn = provider.DrawQuestions(6, new Random(4));

            Assert.IsTrue(drawn.All(q => validIds.Contains(q.Id)));
        }
    }
}
