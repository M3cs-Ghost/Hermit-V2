using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hermit.Games.Clasico.Microgames;

namespace Hermit.Tests.PlayMode
{
    /// <summary>
    /// C8.1j premium typography pass: independent text-fit verification for
    /// every shipped challenge string against the exact box/font parameters
    /// each presenter now uses, per the brief's own instruction ("Use
    /// layout measurement / TextGenerator to guarantee fit... Test all 35
    /// Balance FeedbackExplanation strings" etc.).
    ///
    /// Deliberately does NOT drive full gameplay (launch -&gt; cycle -&gt;
    /// answer, 125 times) to reach each presenter's live recap/statement
    /// state — that would be slow and, since the session director draws
    /// randomly, could not guarantee visiting every single pool item in one
    /// run. Instead this measures each content string directly against a
    /// throwaway Text/TMP_Text component configured with the SAME box size
    /// and font as the real presenter (values duplicated here as named
    /// constants — see each test's own comment for exactly which
    /// presenter/method they mirror). If a presenter's layout constants
    /// ever change, these constants must be updated to match, or this test
    /// stops proving anything.
    ///
    /// "Fits" is defined the same way for both text systems: the preferred
    /// (natural, unclipped) height of the WRAPPED text at the smallest font
    /// size the presenter's own best-fit/auto-size range allows must not
    /// exceed the box's real height. If the floor size fits, best-fit/
    /// auto-size is guaranteed to resolve to something that also fits
    /// (larger sizes only need more room, never less) — so this is a
    /// sufficient, not just necessary, fit proof.
    /// </summary>
    public class ClasicoTypographyFitTests
    {
        private static Font LegacyFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static TMP_FontAsset DisplayFont => Resources.Load<TMP_FontAsset>("Fonts/Marcellus-Regular SDF");

        private static float MeasureLegacyPreferredHeight(string content, int fontSize, float boxWidth)
        {
            var go = new GameObject("MeasureText", typeof(RectTransform), typeof(Text));
            try
            {
                var text = go.GetComponent<Text>();
                text.font = LegacyFont;
                text.fontSize = fontSize;
                text.text = content;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.alignment = TextAnchor.MiddleCenter;

                var settings = text.GetGenerationSettings(new Vector2(boxWidth, 4000f));
                return new TextGenerator().GetPreferredHeight(content, settings);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static float MeasureTmpPreferredHeight(string content, float fontSize, float boxWidth)
        {
            var go = new GameObject("MeasureTmp", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                var tmp = go.GetComponent<TextMeshProUGUI>();
                if (DisplayFont != null)
                {
                    tmp.font = DisplayFont;
                }

                tmp.fontSize = fontSize;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.alignment = TextAlignmentOptions.Center;
                return tmp.GetPreferredValues(content, boxWidth, 0f).y;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- Balance: BalanceMachinePresenter.BuildRecap's Explanation box ---
        // 840x84, TMP best-fit range [13, 18] (Theme.CaptionSize).
        private const float BalanceExplanationWidth = 840f;
        private const float BalanceExplanationHeight = 84f;
        private const float BalanceExplanationMinSize = 13f;

        [Test]
        public void Balance_AllFeedbackExplanations_FitWithinTheRecapPanel()
        {
            foreach (var challenge in ClasicoMicrogameLibrary.DebitCreditPool)
            {
                Assert.IsFalse(string.IsNullOrEmpty(challenge.FeedbackExplanation), $"'{challenge.Id}' has no FeedbackExplanation to measure.");

                var height = MeasureTmpPreferredHeight(challenge.FeedbackExplanation, BalanceExplanationMinSize, BalanceExplanationWidth);
                Assert.LessOrEqual(height, BalanceExplanationHeight,
                    $"'{challenge.Id}' FeedbackExplanation ({challenge.FeedbackExplanation.Length} chars) needs {height:F1}px at the {BalanceExplanationMinSize}pt floor, but the recap panel's Explanation box is only {BalanceExplanationHeight}px tall — it would overflow/clip.");
            }
        }

        // --- Detective: DetectiveLineupPresenter.BuildTeachingRecap's
        // RecapWhy box — 740x112, legacy best-fit range [13, 18]. ---
        private const float DetectiveExplanationWidth = 740f;
        private const float DetectiveExplanationHeight = 112f;
        private const float DetectiveExplanationMinSize = 13f;

        [Test]
        public void Detective_AllExplanations_FitWithinTheRecapPanel()
        {
            foreach (var challenge in ClasicoMicrogameLibrary.ErrorDetectionPool)
            {
                Assert.IsFalse(string.IsNullOrEmpty(challenge.Explanation), $"'{challenge.Id}' has no Explanation to measure.");

                var height = MeasureLegacyPreferredHeight(challenge.Explanation, (int)DetectiveExplanationMinSize, DetectiveExplanationWidth);
                Assert.LessOrEqual(height, DetectiveExplanationHeight,
                    $"'{challenge.Id}' Explanation ({challenge.Explanation.Length} chars) needs {height:F1}px at the {DetectiveExplanationMinSize}pt floor, but the recap panel's RecapWhy box is only {DetectiveExplanationHeight}px tall — it would overflow/clip.");
            }
        }

        /// <summary>Brief section 11: RuleLabel renders in the recap's small
        /// brass tab line (740 wide, single line at its own [13,18] best-fit
        /// range, same box width as the explanation but effectively
        /// single-line since it's a short phrase).
        ///
        /// C8.1k.3: the dossier account-name (Items) half of this test was
        /// removed — it measured every name against the label's
        /// RectTransform (139.5x42 at a 6pt floor), which was wider than the
        /// dossier art's real paper area, so "Préstamo bancario por pagar"
        /// passed here while visibly running onto the frame. Items are now
        /// audited against the REAL runtime label, the measured paper area,
        /// and the runtime sizing (TwoLineTextFit) at 1280x720 and
        /// 1920x1080 by ClasicoPlayModeTests.Detective_AllAccountNames_FitTheDossierPaper_InTwoLines_AtBothResolutions.</summary>
        [Test]
        public void Detective_AllRuleLabels_FitTheRecapTab()
        {
            const float ruleLabelHeight = 26f;
            foreach (var challenge in ClasicoMicrogameLibrary.ErrorDetectionPool)
            {
                var ruleHeight = MeasureLegacyPreferredHeight($"Regla: {challenge.RuleLabel}", (int)DetectiveExplanationMinSize, DetectiveExplanationWidth);
                Assert.LessOrEqual(ruleHeight, ruleLabelHeight + 20f,
                    $"'{challenge.Id}' RuleLabel ('{challenge.RuleLabel}') is too long for the recap's brass-tab line even at the floor size.");
            }
        }

        // --- Western: C8.1j.1 replaced the procedural signage with the
        // approved Gold sprites. Dimensions below are the TRUE usable
        // interior of each sprite (excluding rope border/brass corner
        // hardware/beams — see Docs/C8_1J1_WESTERN_SIGNAGE_ART.md), not the
        // full sprite RectTransform: NamePlate sprite is 250x84 with
        // interior fraction x[0.14,0.86] y[0.32,0.70] -> 180x31.9. ConceptSign
        // sprite is 434x146 with interior fraction x[0.13,0.87] y[0.27,0.71]
        // -> 321.2x64.2. Best-fit ranges [10,18] and [16,32] respectively,
        // matching WesternShootoutPresenter's own resizeTextMinSize/MaxSize.
        // Only the 8 distinct category strings actually ever appear (both
        // category-option sets combined), so measuring the distinct set is
        // equivalent to and cheaper than measuring per-challenge. ---
        private const float NamePlateWidth = 180f;
        private const float NamePlateHeight = 31.9f;
        private const float NamePlateMinSize = 10f;

        private const float ConceptSignWidth = 321.2f;
        private const float ConceptSignHeight = 64.2f;
        private const float ConceptSignMinSize = 16f;

        [Test]
        public void Western_AllCategoryOptions_FitTheSignage()
        {
            var distinctOptions = ClasicoMicrogameLibrary.ClassificationPool
                .SelectMany(c => c.CategoryOptions)
                .Distinct()
                .ToList();

            Assert.IsNotEmpty(distinctOptions);

            foreach (var option in distinctOptions)
            {
                var height = MeasureLegacyPreferredHeight(option, (int)NamePlateMinSize, NamePlateWidth);
                Assert.LessOrEqual(height, NamePlateHeight,
                    $"Category option '{option}' needs {height:F1}px at the {NamePlateMinSize}pt floor, but the outlaw nameplate is only {NamePlateHeight}px tall.");
            }
        }

        [Test]
        public void Western_AllConceptLabels_FitTheWoodSign()
        {
            foreach (var challenge in ClasicoMicrogameLibrary.ClassificationPool)
            {
                var height = MeasureLegacyPreferredHeight(challenge.ConceptLabel, (int)ConceptSignMinSize, ConceptSignWidth);
                Assert.LessOrEqual(height, ConceptSignHeight,
                    $"'{challenge.Id}' ConceptLabel ('{challenge.ConceptLabel}') needs {height:F1}px at the {ConceptSignMinSize}pt floor, but the wood sign's Concept box is only {ConceptSignHeight}px tall.");
            }
        }

        // --- Game Show: GameShowPresenter.BuildStatementArea's Statement
        // box — 760x96, legacy best-fit range [16, 22] (Theme.BodySize). ---
        private const float StatementWidth = 760f;
        private const float StatementHeight = 96f;
        private const float StatementMinSize = 16f;

        [Test]
        public void GameShow_AllStatements_FitTheStage()
        {
            foreach (var challenge in ClasicoMicrogameLibrary.TrueFalsePool)
            {
                var height = MeasureLegacyPreferredHeight(challenge.Statement, (int)StatementMinSize, StatementWidth);
                Assert.LessOrEqual(height, StatementHeight,
                    $"'{challenge.Id}' Statement ({challenge.Statement.Length} chars) needs {height:F1}px at the {StatementMinSize}pt floor, but the stage's Statement box is only {StatementHeight}px tall — it would overflow into ClasicoHud's Intro banner above it.");
            }
        }

        // --- Game Show C8.1k: GameShowPresenter.BuildExplanationCard's
        // "GameShowExplanation" box — 368x106, TMP best-fit range [14, 18],
        // lineSpacing 2 (not modelled by the measurement helper, so an 8px
        // allowance is held back for it). Verdict line: 370 wide, 20pt, one
        // line (~30px). ---
        private const float GameShowExplanationWidth = 368f;
        private const float GameShowExplanationHeight = 106f;
        private const float GameShowExplanationMinSize = 14f;
        private const float GameShowLineSpacingAllowance = 8f;
        private const float GameShowVerdictWidth = 370f;
        private const float GameShowVerdictSize = 20f;
        private const float GameShowVerdictSingleLineHeight = 30f;

        [Test]
        public void GameShow_AllFeedbackExplanations_FitTheExplanationCard()
        {
            var pool = ClasicoMicrogameLibrary.TrueFalsePool;
            Assert.AreEqual(30, pool.Count, "C8.1k expects all 30 Game Show challenges to be measured.");

            foreach (var challenge in pool)
            {
                Assert.IsFalse(string.IsNullOrEmpty(challenge.FeedbackExplanation), $"'{challenge.Id}' has no FeedbackExplanation to show on the explanation card.");

                var height = MeasureTmpPreferredHeight(challenge.FeedbackExplanation, GameShowExplanationMinSize, GameShowExplanationWidth);
                Assert.LessOrEqual(height, GameShowExplanationHeight - GameShowLineSpacingAllowance,
                    $"'{challenge.Id}' FeedbackExplanation ({challenge.FeedbackExplanation.Length} chars) needs {height:F1}px at the {GameShowExplanationMinSize}pt floor, but the explanation card's box is only {GameShowExplanationHeight}px tall — it would overflow the card.");
            }
        }

        [Test]
        public void GameShow_VerdictLines_FitOnOneLine()
        {
            foreach (var verdict in new[] { "¡RESPUESTA CORRECTA!", "RESPUESTA INCORRECTA", "SE ACABÓ EL TIEMPO" })
            {
                var height = MeasureTmpPreferredHeight(verdict, GameShowVerdictSize, GameShowVerdictWidth);
                Assert.LessOrEqual(height, GameShowVerdictSingleLineHeight, $"Verdict '{verdict}' wraps onto a second line at {GameShowVerdictSize}pt in a {GameShowVerdictWidth}px box.");
            }
        }
    }
}
