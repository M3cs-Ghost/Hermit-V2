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

            // C8.1i content bank expansion: Western now authors a real
            // Medium tier (see CorrienteCategories in
            // ClasicoMicrogameLibrary), so Difficulty is no longer just an
            // unchecked default — enforced the same way every other
            // Challenge Type already is.
            if (challenge.Difficulty != "easy" && challenge.Difficulty != "medium")
            {
                issues.Add($"[{label}] Difficulty must be 'easy' or 'medium' for this Gold slice, was '{challenge.Difficulty}'.");
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

            // C8.1i content bank expansion: mirrors the DebitCredit/
            // ErrorDetection rules below — every statement now carries a
            // real teaching explanation and a meaningful Difficulty tier.
            if (string.IsNullOrEmpty(challenge.FeedbackExplanation))
            {
                issues.Add($"[{label}] FeedbackExplanation is empty.");
            }

            if (challenge.Difficulty != "easy" && challenge.Difficulty != "medium")
            {
                issues.Add($"[{label}] Difficulty must be 'easy' or 'medium' for this Gold slice, was '{challenge.Difficulty}'.");
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

        public static IReadOnlyList<string> Validate(DebitCreditChallenge challenge)
        {
            var issues = new List<string>();
            var label = string.IsNullOrEmpty(challenge?.Id) ? "(empty id)" : challenge.Id;

            if (challenge == null)
            {
                issues.Add("DebitCreditChallenge is null.");
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

            if (string.IsNullOrEmpty(challenge.TransactionText))
            {
                issues.Add($"[{label}] TransactionText is empty.");
            }

            if (string.IsNullOrEmpty(challenge.CorrectDebitAccount))
            {
                issues.Add($"[{label}] CorrectDebitAccount is empty.");
            }

            if (string.IsNullOrEmpty(challenge.CorrectCreditAccount))
            {
                issues.Add($"[{label}] CorrectCreditAccount is empty.");
            }

            if (!string.IsNullOrEmpty(challenge.CorrectDebitAccount)
                && challenge.CorrectDebitAccount == challenge.CorrectCreditAccount)
            {
                issues.Add($"[{label}] CorrectDebitAccount and CorrectCreditAccount must differ.");
            }

            if (challenge.AccountOptions == null || challenge.AccountOptions.Length < 3)
            {
                issues.Add($"[{label}] Needs at least 3 AccountOptions.");
                return issues;
            }

            if (challenge.AccountOptions.Distinct().Count() != challenge.AccountOptions.Length)
            {
                issues.Add($"[{label}] AccountOptions has duplicates.");
            }

            if (challenge.AccountOptions.Count(o => o == challenge.CorrectDebitAccount) != 1)
            {
                issues.Add($"[{label}] CorrectDebitAccount must appear exactly once in AccountOptions.");
            }

            if (challenge.AccountOptions.Count(o => o == challenge.CorrectCreditAccount) != 1)
            {
                issues.Add($"[{label}] CorrectCreditAccount must appear exactly once in AccountOptions.");
            }

            // C8.1h content-quality audit: every Balance round's recap
            // panel has a dedicated slot for FeedbackExplanation (see
            // BalanceMachinePresenter.ShowRecap), but every one of the 15
            // shipped challenges left it empty — the "why" was silently
            // never shown. Now enforced the same way Detective's
            // Explanation already is.
            if (string.IsNullOrEmpty(challenge.FeedbackExplanation))
            {
                issues.Add($"[{label}] FeedbackExplanation is empty.");
            }

            if (challenge.Difficulty != "easy" && challenge.Difficulty != "medium")
            {
                issues.Add($"[{label}] Difficulty must be 'easy' or 'medium' for this Gold slice, was '{challenge.Difficulty}'.");
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

            // C8.1g.2: the redesign's new content contract — every round
            // must carry real framing/teaching text, not just a bare item
            // list, and Difficulty must actually mean something (brief
            // section 12: only "easy"/"medium" for this Gold slice, no
            // ambiguous expert tier yet).
            if (string.IsNullOrEmpty(challenge.CasePrompt))
            {
                issues.Add($"[{label}] CasePrompt is empty.");
            }

            if (string.IsNullOrEmpty(challenge.RuleLabel))
            {
                issues.Add($"[{label}] RuleLabel is empty.");
            }

            if (string.IsNullOrEmpty(challenge.Explanation))
            {
                issues.Add($"[{label}] Explanation is empty.");
            }

            if (challenge.Difficulty != "easy" && challenge.Difficulty != "medium")
            {
                issues.Add($"[{label}] Difficulty must be 'easy' or 'medium' for this Gold slice, was '{challenge.Difficulty}'.");
            }

            return issues;
        }
    }
}
