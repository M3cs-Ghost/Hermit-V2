using System.Collections.Generic;
using System.Linq;

namespace Hermit.Games.Content
{
    /// <summary>
    /// Minimal, explicit validation for a QuestionSet — deliberately a plain
    /// static function returning human-readable issue strings, not a
    /// validation framework or a custom Editor tool. Runs equally well from
    /// an EditMode test or from game code at runtime (see
    /// ClasicoGameEngine.Begin, which logs any issues via HermitLog rather
    /// than crashing — a malformed sample question should not take down the
    /// vertical slice).
    /// </summary>
    public static class ContentValidator
    {
        public static IReadOnlyList<string> Validate(QuestionSet set)
        {
            var issues = new List<string>();

            if (set == null)
            {
                issues.Add("QuestionSet is null.");
                return issues;
            }

            if (set.SchemaVersion <= 0)
            {
                issues.Add($"[{set.SetId}] SchemaVersion must be >= 1.");
            }

            if (set.Questions.Count == 0)
            {
                issues.Add($"[{set.SetId}] QuestionSet has no questions.");
                return issues;
            }

            var seenIds = new HashSet<string>();
            foreach (var question in set.Questions)
            {
                var label = string.IsNullOrEmpty(question.Id) ? "(empty id)" : question.Id;

                if (string.IsNullOrEmpty(question.Id))
                {
                    issues.Add($"[{label}] Question id is empty.");
                }
                else if (!seenIds.Add(question.Id))
                {
                    issues.Add($"[{label}] Duplicate question id.");
                }

                if (question.ContentVersion <= 0)
                {
                    issues.Add($"[{label}] ContentVersion must be >= 1.");
                }

                if (question.Options == null || question.Options.Length == 0)
                {
                    issues.Add($"[{label}] Question has no answer options.");
                    continue;
                }

                var correctCount = question.Options.Count(option => option.IsCorrect);
                if (correctCount == 0)
                {
                    issues.Add($"[{label}] Question has no correct option.");
                }
                else if (correctCount > 1)
                {
                    issues.Add($"[{label}] Question has more than one correct option.");
                }
            }

            return issues;
        }
    }
}
