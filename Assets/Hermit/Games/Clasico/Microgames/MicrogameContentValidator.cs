using System.Collections.Generic;
using System.Linq;

namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>
    /// Minimal, explicit validation for the four C8.1 Challenge Types —
    /// same philosophy as <see cref="Hermit.Games.Content.ContentValidator"/>:
    /// a plain static function returning human-readable issue strings, not a
    /// validation framework. Only the rules this phase actually needs (see
    /// Docs/C8_1_GOLD_MICROGAME_SLICE.md, "Content validation") — not the
    /// full ten Challenge Types' worth of rules.
    /// </summary>
    public static class MicrogameContentValidator
    {
        public static IReadOnlyList<string> Validate(ClassificationChallenge challenge)
        {
            var issues = new List<string>();
            var label = string.IsNullOrEmpty(challenge?.Id) ? "(empty id)" : challenge.Id;

            if (challenge == null)
            {
                issues.Add("ClassificationChallenge is null.");
                return issues;
            }

            if (string.IsNullOrEmpty(challenge.Id))
            {
                issues.Add($"[{label}] Id is empty.");
            }

            if (challenge.ContentVersion <= 0)
            {
                issues.Add($"[{label}] ContentVersion must be >= 1.");
            }

            if (string.IsNullOrEmpty(challenge.CorrectCategory))
            {
                issues.Add($"[{label}] CorrectCategory is empty.");
            }

            if (challenge.CategoryOptions == null || challenge.CategoryOptions.Length < 3)
            {
                issues.Add($"[{label}] Needs at least 3 CategoryOptions.");
                return issues;
            }

            if (challenge.CategoryOptions.Distinct().Count() != challenge.CategoryOptions.Length)
            {
                issues.Add($"[{label}] CategoryOptions has duplicates.");
            }

            if (challenge.CategoryOptions.Count(o => o == challenge.CorrectCategory) != 1)
            {
                issues.Add($"[{label}] CorrectCategory must appear exactly once in CategoryOptions.");
            }

            return issues;
        }

        public static IReadOnlyList<string> Validate(TrueFalseChallenge challenge)
        {
            var issues = new List<string>();
            var label = string.IsNullOrEmpty(challenge?.Id) ? "(empty id)" : challenge.Id;

            if (challenge == null)
            {
                issues.Add("TrueFalseChallenge is null.");
                return issues;
            }

            if (string.IsNullOrEmpty(challenge.Id))
            {
                issues.Add($"[{label}] Id is empty.");
            }

            if (challenge.ContentVersion <= 0)
            {
                issues.Add($"[{label}] ContentVersion must be >= 1.");
            }

            if (string.IsNullOrEmpty(challenge.Statement))
            {
                issues.Add($"[{label}] Statement is empty.");
            }

            return issues;
        }

        public static IReadOnlyList<string> Validate(EquationChallenge challenge)
        {
            var issues = new List<string>();
            var label = string.IsNullOrEmpty(challenge?.Id) ? "(empty id)" : challenge.Id;

            if (challenge == null)
            {
                issues.Add("EquationChallenge is null.");
                return issues;
            }

            if (string.IsNullOrEmpty(challenge.Id))
            {
                issues.Add($"[{label}] Id is empty.");
            }

            if (challenge.ContentVersion <= 0)
            {
                issues.Add($"[{label}] ContentVersion must be >= 1.");
            }

            if (challenge.StepSize <= 0f)
            {
                issues.Add($"[{label}] StepSize must be > 0.");
                return issues;
            }

            var deltaSteps = (challenge.CorrectValue - challenge.StartValue) / challenge.StepSize;
            var isWholeMultiple = System.Math.Abs(deltaSteps - System.Math.Round(deltaSteps)) < 0.001;

            if (!isWholeMultiple)
            {
                issues.Add($"[{label}] StartValue must be reachable from CorrectValue in whole StepSize increments.");
            }

            if (System.Math.Abs(challenge.CorrectValue - challenge.StartValue) < 0.001f)
            {
                issues.Add($"[{label}] StartValue must not already equal CorrectValue.");
            }

            return issues;
        }

        public static IReadOnlyList<string> Validate(ErrorDetectionChallenge challenge)
        {
            var issues = new List<string>();
            var label = string.IsNullOrEmpty(challenge?.Id) ? "(empty id)" : challenge.Id;

            if (challenge == null)
            {
                issues.Add("ErrorDetectionChallenge is null.");
                return issues;
            }

            if (string.IsNullOrEmpty(challenge.Id))
            {
                issues.Add($"[{label}] Id is empty.");
            }

            if (challenge.ContentVersion <= 0)
            {
                issues.Add($"[{label}] ContentVersion must be >= 1.");
            }

            if (challenge.Items == null || challenge.Items.Length < 3)
            {
                issues.Add($"[{label}] Needs at least 3 Items.");
                return issues;
            }

            if (challenge.Items.Distinct().Count() != challenge.Items.Length)
            {
                issues.Add($"[{label}] Items has duplicates.");
            }

            if (challenge.AnomalyIndex < 0 || challenge.AnomalyIndex >= challenge.Items.Length)
            {
                issues.Add($"[{label}] AnomalyIndex is out of range.");
            }

            return issues;
        }
    }
}
