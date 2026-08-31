using NUnit.Framework;
using Hermit.Games.Content;

namespace Hermit.Tests.EditMode
{
    public class ContentValidatorTests
    {
        private static QuestionDefinition Valid(string id) => new QuestionDefinition
        {
            Id = id,
            PromptText = $"Prompt {id}",
            ContentVersion = 1,
            Options = new[]
            {
                new AnswerOption { Text = "Correct", IsCorrect = true },
                new AnswerOption { Text = "Wrong", IsCorrect = false }
            }
        };

        [Test]
        public void ValidSet_HasNoIssues()
        {
            var set = QuestionSet.CreateInMemory("set", "desc", new[] { Valid("q0"), Valid("q1") });
            Assert.AreEqual(0, ContentValidator.Validate(set).Count);
        }

        [Test]
        public void NullSet_ReportsAnIssue_WithoutThrowing()
        {
            var issues = ContentValidator.Validate(null);
            Assert.AreEqual(1, issues.Count);
        }

        [Test]
        public void EmptySet_ReportsAnIssue()
        {
            var set = QuestionSet.CreateInMemory("empty_set", "desc", new QuestionDefinition[0]);
            var issues = ContentValidator.Validate(set);

            Assert.AreEqual(1, issues.Count);
            StringAssert.Contains("no questions", issues[0]);
        }

        [Test]
        public void EmptyQuestionId_IsReported()
        {
            var bad = Valid("q0");
            bad.Id = string.Empty;
            var set = QuestionSet.CreateInMemory("set", "desc", new[] { bad });

            var issues = ContentValidator.Validate(set);
            Assert.That(issues, Has.Some.Contains("id is empty"));
        }

        [Test]
        public void DuplicateQuestionIds_AreReported()
        {
            var set = QuestionSet.CreateInMemory("set", "desc", new[] { Valid("dup"), Valid("dup") });

            var issues = ContentValidator.Validate(set);
            Assert.That(issues, Has.Some.Contains("Duplicate question id"));
        }

        [Test]
        public void QuestionWithNoOptions_IsReported()
        {
            var bad = Valid("q0");
            bad.Options = new AnswerOption[0];
            var set = QuestionSet.CreateInMemory("set", "desc", new[] { bad });

            var issues = ContentValidator.Validate(set);
            Assert.That(issues, Has.Some.Contains("no answer options"));
        }

        [Test]
        public void QuestionWithNoCorrectOption_IsReported()
        {
            var bad = Valid("q0");
            bad.Options = new[]
            {
                new AnswerOption { Text = "A", IsCorrect = false },
                new AnswerOption { Text = "B", IsCorrect = false }
            };
            var set = QuestionSet.CreateInMemory("set", "desc", new[] { bad });

            var issues = ContentValidator.Validate(set);
            Assert.That(issues, Has.Some.Contains("no correct option"));
        }

        [Test]
        public void QuestionWithMoreThanOneCorrectOption_IsReported()
        {
            var bad = Valid("q0");
            bad.Options = new[]
            {
                new AnswerOption { Text = "A", IsCorrect = true },
                new AnswerOption { Text = "B", IsCorrect = true }
            };
            var set = QuestionSet.CreateInMemory("set", "desc", new[] { bad });

            var issues = ContentValidator.Validate(set);
            Assert.That(issues, Has.Some.Contains("more than one correct option"));
        }

        [Test]
        public void QuestionWithInvalidContentVersion_IsReported()
        {
            var bad = Valid("q0");
            bad.ContentVersion = 0;
            var set = QuestionSet.CreateInMemory("set", "desc", new[] { bad });

            var issues = ContentValidator.Validate(set);
            Assert.That(issues, Has.Some.Contains("ContentVersion"));
        }

        [Test]
        public void SetWithInvalidSchemaVersion_IsReported()
        {
            var set = QuestionSet.CreateInMemory("set", "desc", new[] { Valid("q0") }, schemaVersion: 0);

            var issues = ContentValidator.Validate(set);
            Assert.That(issues, Has.Some.Contains("SchemaVersion"));
        }

        [Test]
        public void MultipleIssues_AreAllReported_NotJustTheFirst()
        {
            var bad1 = Valid("q0");
            bad1.Id = string.Empty;
            var bad2 = Valid("q1");
            bad2.Options = new AnswerOption[0];

            var set = QuestionSet.CreateInMemory("set", "desc", new[] { bad1, bad2 });
            var issues = ContentValidator.Validate(set);

            Assert.GreaterOrEqual(issues.Count, 2);
        }
    }
}
