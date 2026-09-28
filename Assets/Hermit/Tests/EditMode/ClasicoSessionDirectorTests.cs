using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Hermit.Games;
using Hermit.Games.Analytics;
using Hermit.Games.Clasico;
using Hermit.Games.Clasico.Microgames;

namespace Hermit.Tests.EditMode
{
    /// <summary>
    /// C8.1's replacement for the deleted ClasicoGameEngineTests — covers
    /// the same lifecycle guarantees (begin/tick/submit/finish/result) but
    /// against a director that sequences four heterogeneous microgame
    /// archetypes instead of one fixed question shape. Content itself is the
    /// real shipped <see cref="Microgames.ClasicoMicrogameLibrary"/> pools —
    /// tests read back whichever challenge the RNG actually drew rather than
    /// assuming a specific one, same "assert shape, not identity" discipline
    /// the original C5-C7 tests used for shuffled options.
    /// </summary>
    public class ClasicoSessionDirectorTests
    {
        private static (ClasicoSessionDirector director, GameSession session) BeginDirector(
            int microgameCount = 4,
            float decisionWindowSeconds = 0f,
            float balanceDecisionWindowSeconds = 0f,
            float commandBeatSeconds = 0f,
            float lockSeconds = 0f,
            float feedbackSeconds = 0f,
            float countdownSeconds = 0f,
            int streakBonusThreshold = 0,
            int streakBonusPoints = 0,
            int westernEncounterRoundCount = 1,
            float encounterIntroSeconds = 0f,
            float roundTransitionSeconds = 0f,
            float encounterOutroSeconds = 0f,
            int seed = 0)
        {
            var definition = ClasicoGameDefinition.CreateInMemory(
                "clasico_test", "Clasico Test", microgameCount, decisionWindowSeconds, balanceDecisionWindowSeconds,
                pointsPerCorrectAnswer: 100, maxSpeedBonusPoints: 0,
                commandBeatSeconds: commandBeatSeconds, lockSeconds: lockSeconds, feedbackDisplaySeconds: feedbackSeconds,
                countdownDurationSeconds: countdownSeconds, streakBonusThreshold: streakBonusThreshold, streakBonusPoints: streakBonusPoints,
                westernEncounterRoundCount: westernEncounterRoundCount, encounterIntroSeconds: encounterIntroSeconds,
                roundTransitionSeconds: roundTransitionSeconds, encounterOutroSeconds: encounterOutroSeconds);

            var context = new GameContext(NullGameAnalyticsSink.Instance, new Random(seed));
            var session = new GameSession(definition.GameId);
            var director = new ClasicoSessionDirector();
            director.Begin(context, definition, session);

            return (director, session);
        }

        /// <summary>Submits a correct/incorrect AimSelect answer without also
        /// driving Tick — for tests that need explicit control over Encounter
        /// Intro/RoundTransition timing (<see cref="ResolveCurrent"/>'s own
        /// baked-in 0.001s ticks assume a near-zero Intro beat, which the
        /// Western Encounter's first round deliberately is not).</summary>
        private static void SubmitCurrentAimSelectAnswer(ClasicoSessionDirector director, bool answerCorrectly)
        {
            var options = director.CurrentClassification.CategoryOptions;
            var correctIndex = Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
            var chosen = answerCorrectly ? correctIndex : (correctIndex + 1) % options.Length;
            director.SubmitSelection(chosen);
        }

        /// <summary>Answers the currently-showing microgame correctly or
        /// incorrectly (whichever archetype it happens to be) and advances
        /// through Lock/Feedback to the next microgame's Intro. Assumes
        /// commandBeatSeconds/lockSeconds/feedbackSeconds are all small so a
        /// tiny Tick crosses each beat.</summary>
        private static bool ResolveCurrent(ClasicoSessionDirector director, bool answerCorrectly)
        {
            director.Tick(0.001f); // Intro -> Decision

            bool wasCorrect;
            switch (director.CurrentArchetype)
            {
                case MicrogameArchetype.AimSelect:
                {
                    var options = director.CurrentClassification.CategoryOptions;
                    var correctIndex = Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                    var chosen = answerCorrectly ? correctIndex : (correctIndex + 1) % options.Length;
                    director.SubmitSelection(chosen);
                    wasCorrect = chosen == correctIndex;
                    break;
                }

                case MicrogameArchetype.ChooseSide:
                {
                    var correctIndex = director.CurrentTrueFalse.IsTrue ? 0 : 1;
                    var chosen = answerCorrectly ? correctIndex : 1 - correctIndex;
                    director.SubmitSelection(chosen);
                    wasCorrect = chosen == correctIndex;
                    break;
                }

                case MicrogameArchetype.Balance:
                {
                    wasCorrect = SubmitDebitCreditAnswer(director, answerCorrectly);
                    break;
                }

                default: // DetectError
                {
                    // C8.1g.2: the engine now resolves against the
                    // DISPLAY anomaly slot (post-shuffle), not the
                    // challenge's own authored AnomalyIndex — see
                    // ClasicoSessionDirector.CurrentErrorDetectionDisplayAnomalyIndex.
                    var challenge = director.CurrentErrorDetection;
                    var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;
                    var chosen = answerCorrectly ? displayAnomalyIndex : (displayAnomalyIndex + 1) % challenge.Items.Length;
                    director.SubmitSelection(chosen);
                    wasCorrect = chosen == displayAnomalyIndex;
                    break;
                }
            }

            director.Tick(0.001f); // Lock -> Feedback
            director.Tick(0.001f); // Feedback -> next Intro (or finished)
            return wasCorrect;
        }

        /// <summary>C8.1f: submits both the debit and credit picks for the
        /// currently-showing Balance round in one call — both correct, or
        /// both wrong, never a Partial (this helper is only used by tests
        /// that want a clean fully-correct/fully-incorrect result; Partial
        /// gets its own dedicated tests below).</summary>
        private static bool SubmitDebitCreditAnswer(ClasicoSessionDirector director, bool answerCorrectly)
        {
            var challenge = director.CurrentDebitCredit;
            var correctDebitIndex = Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
            var correctCreditIndex = Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);

            var debitChoice = answerCorrectly ? correctDebitIndex : (correctDebitIndex + 1) % challenge.AccountOptions.Length;
            director.SubmitBalanceAccount(debitChoice);

            var creditChoice = answerCorrectly ? correctCreditIndex : (correctCreditIndex + 1) % challenge.AccountOptions.Length;
            director.SubmitBalanceAccount(creditChoice);

            return answerCorrectly;
        }

        [Test]
        public void Begin_PresentsTheFirstMicrogame_WithoutCountdownByDefault()
        {
            var (director, _) = BeginDirector();

            Assert.IsFalse(director.IsCountingDown);
            Assert.IsFalse(director.IsFinished);
            Assert.AreEqual(0, director.CurrentMicrogameIndex);
            Assert.AreEqual(4, director.TotalMicrogames);
        }

        [Test]
        public void Sequence_ContainsAllFourArchetypes_ForACountOfFour()
        {
            var (director, _) = BeginDirector(microgameCount: 4, seed: 7);
            var seen = new HashSet<MicrogameArchetype>();

            for (var i = 0; i < 4 && !director.IsFinished; i++)
            {
                seen.Add(director.CurrentArchetype);
                ResolveCurrent(director, answerCorrectly: true);
            }

            Assert.AreEqual(4, seen.Count, "A 4-microgame run must show every archetype exactly once.");
        }

        [Test]
        public void Sequence_NeverRepeatsTheSameArchetypeAdjacently()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var (director, _) = BeginDirector(microgameCount: 9, seed: seed);
                var previous = director.CurrentArchetype;

                for (var i = 1; i < 9 && !director.IsFinished; i++)
                {
                    ResolveCurrent(director, answerCorrectly: true);
                    if (director.IsFinished)
                    {
                        break;
                    }

                    Assert.AreNotEqual(previous, director.CurrentArchetype, $"Seed {seed}: adjacent microgames must not share an archetype.");
                    previous = director.CurrentArchetype;
                }
            }
        }

        [Test]
        public void Begin_WithCountdownConfigured_StartsCountingDown_WithNoMicrogameYet()
        {
            var (director, _) = BeginDirector(countdownSeconds: 3f);

            Assert.IsTrue(director.IsCountingDown);
            Assert.AreEqual(3, director.CountdownSecondsRemaining);
        }

        [Test]
        public void Tick_PastCountdownDuration_AdvancesToTheFirstMicrogame()
        {
            var (director, _) = BeginDirector(countdownSeconds: 3f);

            director.Tick(3.1f);

            Assert.IsFalse(director.IsCountingDown);
            Assert.AreEqual(0, director.CurrentMicrogameIndex);
        }

        [Test]
        public void SubmitSelection_Correct_IncrementsScoreAndCorrectCount()
        {
            var (director, session) = BeginDirector(microgameCount: 1);

            var wasCorrect = ResolveCurrent(director, answerCorrectly: true);

            Assert.IsTrue(wasCorrect);
            Assert.AreEqual(100, session.Score);
            Assert.AreEqual(1, session.Correct);
            Assert.AreEqual(0, session.Incorrect);
            Assert.IsTrue(director.LastAnswerCorrect);
        }

        [Test]
        public void SubmitSelection_Incorrect_IncrementsIncorrectCount_NoScore()
        {
            var (director, session) = BeginDirector(microgameCount: 1);

            ResolveCurrent(director, answerCorrectly: false);

            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(0, session.Correct);
            Assert.AreEqual(1, session.Incorrect);
            Assert.IsFalse(director.LastAnswerCorrect);
        }

        [Test]
        public void SubmitSelection_Twice_SecondCallIsIgnored()
        {
            var (director, session) = BeginDirector(microgameCount: 1);
            director.Tick(0.001f); // Intro -> Decision

            // Whichever archetype this is, submit/confirm twice with
            // whatever inputs are valid for it — the second must be a no-op.
            switch (director.CurrentArchetype)
            {
                case MicrogameArchetype.Balance:
                    director.SubmitBalanceAccount(0); // locks debit
                    director.SubmitBalanceAccount(0); // locks credit, resolves
                    director.SubmitBalanceAccount(0); // must be ignored — already resolved
                    break;
                default:
                    director.SubmitSelection(0);
                    director.SubmitSelection(1);
                    break;
            }

            Assert.AreEqual(1, session.Correct + session.Incorrect, "A resolved microgame must not be resolved twice.");
        }

        // --- C8.1f: Debit/Credit replaces the old continuous-nudge Balance
        // mechanic (see Docs/C8_1F_BALANCE_MECHANIC_REDESIGN.md). The old
        // Equation-specific invariant test above no longer applies —
        // CurrentEquation is never populated for a Balance round any more
        // — and is replaced by tests of the new mechanic's own guarantees.

        [Test]
        public void DebitCredit_NoInputBeforeBothSelectionsLock_MachineStaysNeutral()
        {
            for (var seed = 0; seed < 10; seed++)
            {
                var (director, _) = BeginDirector(microgameCount: 4, seed: seed);
                for (var i = 0; i < 4 && !director.IsFinished; i++)
                {
                    if (director.CurrentArchetype == MicrogameArchetype.Balance)
                    {
                        director.Tick(0.001f); // Intro -> Decision
                        director.SubmitBalanceAccount(0); // debit only — must not resolve
                        Assert.IsTrue(director.IsDecisionPhase, $"Seed {seed}: a single (debit-only) selection must not resolve the microgame.");
                        return;
                    }

                    ResolveCurrent(director, answerCorrectly: true);
                }
            }

            Assert.Fail("No Balance microgame was drawn across 10 seeds — sequencing regressed.");
        }

        [Test]
        public void DebitCredit_OnlyOneAccountCorrect_OverallResultIsIncorrect_NeverPartialScore()
        {
            for (var seed = 0; seed < 10; seed++)
            {
                var (director, session) = BeginDirector(microgameCount: 4, seed: seed);
                for (var i = 0; i < 4 && !director.IsFinished; i++)
                {
                    if (director.CurrentArchetype == MicrogameArchetype.Balance)
                    {
                        director.Tick(0.001f); // Intro -> Decision
                        var challenge = director.CurrentDebitCredit;
                        var correctDebitIndex = Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
                        var correctCreditIndex = Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);
                        var wrongCreditChoice = (correctCreditIndex + 1) % challenge.AccountOptions.Length;
                        var scoreBeforeThisRound = session.Score;

                        director.SubmitBalanceAccount(correctDebitIndex); // right
                        director.SubmitBalanceAccount(wrongCreditChoice); // wrong

                        Assert.IsFalse(director.LastAnswerCorrect, $"Seed {seed}: exactly one correct account must never score as a full correct answer.");
                        Assert.AreEqual(scoreBeforeThisRound, session.Score, $"Seed {seed}: a Partial-classified round must not award any score of its own — only full Correct does.");
                        return;
                    }

                    ResolveCurrent(director, answerCorrectly: true);
                }
            }

            Assert.Fail("No Balance microgame was drawn across 10 seeds — sequencing regressed.");
        }

        [Test]
        public void DebitCredit_TimeoutAfterOnlyDebitSelected_NeverSynthesizesACreditAnswer()
        {
            var (director, session) = BeginDirector(microgameCount: 4, balanceDecisionWindowSeconds: 2f, seed: 0);

            for (var i = 0; i < 4 && !director.IsFinished; i++)
            {
                if (director.CurrentArchetype == MicrogameArchetype.Balance)
                {
                    director.Tick(0.001f); // Intro -> Decision
                    var challenge = director.CurrentDebitCredit;
                    var correctDebitIndex = Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
                    director.SubmitBalanceAccount(correctDebitIndex); // debit only, correctly — credit never comes

                    director.Tick(2.1f); // exceed the decision window
                    Assert.IsFalse(director.LastAnswerCorrect, "A timeout with only one of two selections made must never read as correct.");
                    Assert.AreEqual(1, session.Incorrect, "A Balance timeout must still count as one incorrect round.");
                    return;
                }

                ResolveCurrent(director, answerCorrectly: true);
            }

            Assert.Fail("No Balance microgame was drawn in this seed's sequence.");
        }

        [Test]
        public void Timeout_OnASelectionMicrogame_AutoResolvesAsIncorrect()
        {
            var (director, session) = BeginDirector(microgameCount: 4, decisionWindowSeconds: 2f, seed: 3);

            for (var i = 0; i < 4 && !director.IsFinished; i++)
            {
                if (director.CurrentArchetype != MicrogameArchetype.Balance)
                {
                    director.Tick(0.001f); // Intro -> Decision
                    director.Tick(2.1f); // exceed the decision window with no input
                    Assert.IsFalse(director.LastAnswerCorrect);
                    Assert.AreEqual(1, session.Incorrect);
                    return;
                }

                ResolveCurrent(director, answerCorrectly: true);
            }

            Assert.Fail("No selection-based microgame was drawn across this seed's sequence.");
        }

        [Test]
        public void AllMicrogamesResolved_MarksTheDirectorFinished()
        {
            var (director, _) = BeginDirector(microgameCount: 4);

            for (var i = 0; i < 4 && !director.IsFinished; i++)
            {
                ResolveCurrent(director, answerCorrectly: true);
            }

            Assert.IsTrue(director.IsFinished);
        }

        [Test]
        public void BuildResult_ReflectsSessionTotals()
        {
            var (director, session) = BeginDirector(microgameCount: 1);
            ResolveCurrent(director, answerCorrectly: true);
            session.ElapsedSeconds = 12.5f;

            var result = director.BuildResult(completed: true);

            Assert.AreEqual(100, result.Score);
            Assert.AreEqual(1, result.Correct);
            Assert.AreEqual(0, result.Incorrect);
            Assert.AreEqual(12.5f, result.DurationSeconds, 0.001f);
            Assert.IsTrue(result.Completed);
        }

        [Test]
        public void StreakBonus_StillAppliesAcrossDifferentArchetypes()
        {
            var (director, session) = BeginDirector(microgameCount: 3, streakBonusThreshold: 3, streakBonusPoints: 30);

            for (var i = 0; i < 3 && !director.IsFinished; i++)
            {
                ResolveCurrent(director, answerCorrectly: true);
            }

            Assert.AreEqual(3, session.BestStreak);
            Assert.AreEqual(330, session.Score, "Three consecutive correct answers across three different archetypes must still land the streak bonus.");
        }

        [Test]
        public void EmptyMicrogameCount_FinishesImmediately()
        {
            var (director, _) = BeginDirector(microgameCount: 0);
            Assert.IsTrue(director.IsFinished);
        }

        // --- C8.1d.1 Western Encounter — see
        // Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Western Encounter
        // Presentation". westernEncounterRoundCount defaults to 1 in
        // BeginDirector (Encounter grouping off), so every test above this
        // point is completely unaffected by any of this — only tests that
        // explicitly opt in below observe the new behavior.

        [Test]
        public void WesternEncounter_GroupsExactlyThreeConsecutiveRounds_NoOtherArchetypeInterleaved()
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var (director, _) = BeginDirector(microgameCount: 9, westernEncounterRoundCount: 3, seed: seed);
                var archetypes = new List<MicrogameArchetype>();

                for (var i = 0; i < 9 && !director.IsFinished; i++)
                {
                    archetypes.Add(director.CurrentArchetype);
                    ResolveCurrent(director, answerCorrectly: true);
                }

                var westernIndices = archetypes
                    .Select((a, i) => (archetype: a, index: i))
                    .Where(p => p.archetype == MicrogameArchetype.AimSelect)
                    .Select(p => p.index)
                    .ToList();

                Assert.AreEqual(3, westernIndices.Count, $"seed={seed}: expected exactly 3 Western rounds in a 9-round session, found at {string.Join(",", westernIndices)}.");
                Assert.AreEqual(westernIndices[0] + 1, westernIndices[1], $"seed={seed}: Western rounds must be consecutive.");
                Assert.AreEqual(westernIndices[1] + 1, westernIndices[2], $"seed={seed}: Western rounds must be consecutive.");
            }
        }

        [Test]
        public void WesternEncounter_NeverRepeatsTheSameChallenge_AcrossItsThreeRounds()
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var (director, _) = BeginDirector(microgameCount: 9, westernEncounterRoundCount: 3, seed: seed);
                var westernChallengeIds = new List<string>();

                for (var i = 0; i < 9 && !director.IsFinished; i++)
                {
                    if (director.CurrentArchetype == MicrogameArchetype.AimSelect)
                    {
                        westernChallengeIds.Add(director.CurrentClassification.Id);
                    }

                    ResolveCurrent(director, answerCorrectly: true);
                }

                Assert.AreEqual(3, westernChallengeIds.Count, $"seed={seed}");
                Assert.AreEqual(3, westernChallengeIds.Distinct().Count(), $"seed={seed}: repeated a challenge id within one Western Encounter: {string.Join(",", westernChallengeIds)}.");
            }
        }

        [Test]
        public void WesternEncounter_FirstRoundIntro_IsLonger_ThanContinuationRoundTransitions()
        {
            var (director, _) = BeginDirector(
                microgameCount: 9, westernEncounterRoundCount: 3,
                encounterIntroSeconds: 2.0f, roundTransitionSeconds: 0.3f, commandBeatSeconds: 0f,
                decisionWindowSeconds: 5f, balanceDecisionWindowSeconds: 5f, seed: 1);

            while (!director.IsFinished && director.CurrentArchetype != MicrogameArchetype.AimSelect)
            {
                ResolveCurrent(director, answerCorrectly: true);
            }

            Assume.That(director.IsFinished, Is.False);
            Assert.AreEqual(0, director.CurrentRoundWithinEncounter, "The first Western round found must open the Encounter (round 0).");

            director.Tick(1.0f);
            Assert.IsTrue(director.IsIntroPhase, "Round 1 of a Western Encounter should still be mid-way through its ~2s face-off intro after 1.0s.");
            director.Tick(1.2f);
            Assert.IsTrue(director.IsDecisionPhase, "Round 1's intro should have finished by 2.2s (EncounterIntroSeconds=2.0s).");

            SubmitCurrentAimSelectAnswer(director, answerCorrectly: true);
            director.Tick(0.01f); // Lock -> Feedback
            director.Tick(0.01f); // Feedback -> next round's Intro

            Assert.AreEqual(MicrogameArchetype.AimSelect, director.CurrentArchetype, "Round 2 must still be Western (same Encounter).");
            Assert.AreEqual(1, director.CurrentRoundWithinEncounter, "Round 2 is a continuation round.");

            director.Tick(1.0f);
            Assert.IsTrue(director.IsDecisionPhase, "A continuation round's quick 0.3s RoundTransition should be long over by 1.0s — the intro must not replay.");
        }

        [Test]
        public void WesternEncounter_IntroDoesNotConsumeTheDecisionTimer()
        {
            var (director, _) = BeginDirector(
                microgameCount: 9, westernEncounterRoundCount: 3,
                encounterIntroSeconds: 2.0f, commandBeatSeconds: 0f, seed: 1);

            while (!director.IsFinished && director.CurrentArchetype != MicrogameArchetype.AimSelect)
            {
                ResolveCurrent(director, answerCorrectly: true);
            }

            director.Tick(1.5f);
            Assert.IsTrue(director.IsIntroPhase);
            Assert.AreEqual(1f, director.DecisionFraction01, 0.001f, "The shared decision timer must not move while the Encounter intro is playing.");
        }

        [Test]
        public void WesternEncounter_EndingWithinASingleThreeRoundSession_ScoresAndFinishesNormally()
        {
            var (director, session) = BeginDirector(microgameCount: 3, westernEncounterRoundCount: 3, seed: 2);

            for (var i = 0; i < 3 && !director.IsFinished; i++)
            {
                Assert.AreEqual(MicrogameArchetype.AimSelect, director.CurrentArchetype, "microgameCount == westernEncounterRoundCount: the whole session is the one Western Encounter.");
                ResolveCurrent(director, answerCorrectly: true);
            }

            Assert.AreEqual(3, session.Correct);
            Assert.AreEqual(300, session.Score, "No separate Encounter score — three ordinary 100-point correct answers.");
            Assert.IsTrue(director.IsFinished);
        }

        // --- Production-config regression tests (C8.1d.1 manual-validation-
        // failure investigation, brief section 6). Every test above this
        // point exercises the Encounter logic only through synthetic
        // CreateInMemory definitions — deliberately fast, zero-duration
        // beats. That is real coverage of the *algorithm*, but it can never
        // catch a problem that only exists in the *actual shipped asset*
        // (missing field, wrong serialized value, or a duration so long a
        // hand-rolled test driver undershoots it — which is exactly the bug
        // this investigation found, in the test driver below, not the
        // product). These two load
        // Assets/Hermit/Data/Resources/ClasicoGameDefinition.asset directly
        // via AssetDatabase — the same asset GameCatalog.asset references by
        // GUID, which is what GameSessionInstaller/ShellInstaller both
        // resolve via Resources.Load<GameCatalog>("GameCatalog") — i.e. the
        // literal configuration 01_Shell loads, not a stand-in for it.

        private const string ProductionDefinitionAssetPath = "Assets/Hermit/Data/Resources/ClasicoGameDefinition.asset";

        [Test]
        public void ProductionClasicoGameDefinition_RequestsAContiguousThreeRoundWesternEncounter()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ClasicoGameDefinition>(ProductionDefinitionAssetPath);
            Assert.IsNotNull(definition, $"Could not load the production asset at {ProductionDefinitionAssetPath}.");
            Assert.AreEqual(3, definition.WesternEncounterRoundCount, "The shipped production asset must request a 3-round Western Encounter.");
            Assert.GreaterOrEqual(definition.MicrogameCount, definition.WesternEncounterRoundCount, "The production session length must fit a 3-round Western Encounter.");
        }

        [Test]
        public void ProductionClasicoGameDefinition_GeneratedRunPlan_HasAContiguousThreeRoundWesternEncounter_WithCorrectStartMiddleEndMarkers()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ClasicoGameDefinition>(ProductionDefinitionAssetPath);
            Assume.That(definition, Is.Not.Null);

            for (var seed = 0; seed < 20; seed++)
            {
                var context = new GameContext(NullGameAnalyticsSink.Instance, new Random(seed));
                var session = new GameSession(definition.GameId);
                var director = new ClasicoSessionDirector();
                director.Begin(context, definition, session);

                // The production asset has a real 3s pre-run countdown
                // (unlike every CreateInMemory-based test above, which
                // defaults it to 0) — cross it explicitly before the round
                // loop, exactly as ClasicoGameHost's own
                // `director.IsCountingDown` branch waits for it to clear.
                if (director.IsCountingDown)
                {
                    director.Tick(definition.CountdownDurationSeconds + 0.1f);
                }

                var archetypes = new List<MicrogameArchetype>();
                var roundsWithin = new List<int>();

                for (var i = 0; i < definition.MicrogameCount && !director.IsFinished; i++)
                {
                    archetypes.Add(director.CurrentArchetype);
                    roundsWithin.Add(director.CurrentRoundWithinEncounter);

                    // Real Intro/Lock/Feedback durations (0.6-2.2s / 0.2s /
                    // 0.9(+0.7)s) — not the near-zero beats CreateInMemory
                    // defaults to — so each tick is sized off the
                    // definition's own fields with a margin, safely crossing
                    // whichever phase duration actually applies to this round.
                    director.Tick(Math.Max(definition.EncounterIntroSeconds, Math.Max(definition.CommandBeatSeconds, definition.GameShowIntroSeconds)) + 0.5f);
                    Assert.IsTrue(director.IsDecisionPhase, $"seed={seed} round={i} ({archetypes[i]}, roundWithinEncounter={roundsWithin[i]}) did not reach Decision after a generous Intro-covering tick.");

                    ProductionSubmitAnswer(director, answerCorrectly: true);

                    director.Tick(definition.LockSeconds + 0.1f);
                    // C8.1f.2: Balance's Feedback phase is now its own,
                    // longer duration (BalanceFeedbackDisplaySeconds) than
                    // every other non-Encounter archetype's shared
                    // FeedbackDisplaySeconds — this tick must cover
                    // whichever one actually applies to the round just
                    // answered, or the next round's Intro genuinely hasn't
                    // started yet by the following iteration's Decision check.
                    // C8.1g.2: Detective now has the same kind of its-own-
                    // longer-Feedback override for its teaching recap.
                    // C8.1k: Game Show likewise has its own longer
                    // Feedback (suspense beat + reveal + explanation hold).
                    var feedbackSeconds = archetypes[i] == MicrogameArchetype.Balance
                        ? definition.BalanceFeedbackDisplaySeconds
                        : archetypes[i] == MicrogameArchetype.DetectError
                            ? definition.DetectiveFeedbackDisplaySeconds
                            : archetypes[i] == MicrogameArchetype.ChooseSide
                                ? definition.GameShowFeedbackDisplaySeconds
                                : definition.FeedbackDisplaySeconds;
                    director.Tick(feedbackSeconds + definition.EncounterOutroSeconds + 0.2f);
                }

                var westernIndices = archetypes
                    .Select((a, i) => (archetype: a, index: i))
                    .Where(p => p.archetype == MicrogameArchetype.AimSelect)
                    .Select(p => p.index)
                    .ToList();

                Assert.AreEqual(3, westernIndices.Count, $"seed={seed}: expected exactly 3 Western rounds, found at {string.Join(",", westernIndices)}.");
                Assert.AreEqual(westernIndices[0] + 1, westernIndices[1], $"seed={seed}: Western rounds must be contiguous.");
                Assert.AreEqual(westernIndices[1] + 1, westernIndices[2], $"seed={seed}: Western rounds must be contiguous.");
                Assert.AreEqual(0, roundsWithin[westernIndices[0]], $"seed={seed}: first Western round must be the Encounter start (round-within-encounter 0).");
                Assert.AreEqual(1, roundsWithin[westernIndices[1]], $"seed={seed}: second Western round must be a continuation (round-within-encounter 1).");
                Assert.AreEqual(2, roundsWithin[westernIndices[2]], $"seed={seed}: third Western round must be the Encounter end (round-within-encounter 2 of 3).");
            }
        }

        /// <summary>C8.1k: Game Show owns a longer Intro (its broadcast
        /// preamble) and a longer Feedback (suspense beat + reveal +
        /// explanation hold); every other non-Encounter archetype must keep
        /// the shared command beat / feedback length, and the decision
        /// window itself is unchanged.</summary>
        [Test]
        public void GameShow_UsesItsOwnIntroAndFeedbackDurations_OtherArchetypesKeepTheSharedBeats()
        {
            var definition = ClasicoGameDefinition.CreateInMemory(
                "clasico_test", "Clasico Test", 8, decisionWindowSeconds: 5f, balanceDecisionWindowSeconds: 5f,
                commandBeatSeconds: 0.6f, lockSeconds: 0.2f, feedbackDisplaySeconds: 0.8f,
                balanceFeedbackDisplaySeconds: 0.8f, detectiveFeedbackDisplaySeconds: 0.8f,
                gameShowIntroSeconds: 1.5f, gameShowFeedbackDisplaySeconds: 3.4f);

            var director = new ClasicoSessionDirector();
            director.Begin(new GameContext(NullGameAnalyticsSink.Instance, new Random(0)), definition, new GameSession(definition.GameId));

            var gameShowRounds = 0;
            for (var i = 0; i < definition.MicrogameCount && !director.IsFinished; i++)
            {
                var isGameShow = director.CurrentArchetype == MicrogameArchetype.ChooseSide;

                director.Tick(0.7f);
                if (isGameShow)
                {
                    gameShowRounds++;
                    Assert.IsTrue(director.IsIntroPhase, $"round {i}: Game Show's Intro must outlast the shared 0.6s command beat.");
                    director.Tick(0.9f);
                }

                Assert.IsTrue(director.IsDecisionPhase, $"round {i} ({director.CurrentArchetype}) must be in Decision after its own Intro.");

                ProductionSubmitAnswer(director, answerCorrectly: true);
                director.Tick(0.25f);
                Assert.IsTrue(director.IsFeedbackPhase, $"round {i}: Lock must hand over to Feedback.");

                director.Tick(0.9f);
                if (isGameShow)
                {
                    Assert.IsTrue(director.IsFeedbackPhase, $"round {i}: Game Show's Feedback must outlast the shared 0.8s.");
                    director.Tick(2.6f);
                }

                Assert.IsFalse(director.IsFeedbackPhase, $"round {i}: Feedback must have ended after its own duration.");
            }

            Assert.Greater(gameShowRounds, 0, "No Game Show round appeared in an 8-microgame session.");
        }

        /// <summary>C8.1l user-test pacing: the shipped definition gives
        /// Detective and Game Show a 4.2s decision window (+1.0s over the
        /// shared 3.2s Western keeps) and +1.0s of explanation hold each
        /// (Detective 4.2s, Game Show 4.4s Feedback). Game Show's 1.5s
        /// opening and every Western/Balance value are unchanged.</summary>
        [Test]
        public void ProductionDefinition_DetectiveAndGameShowPacing_OtherArchetypesUnchanged()
        {
            var definition = UnityEngine.Resources.Load<ClasicoGameDefinition>("ClasicoGameDefinition");
            Assert.IsNotNull(definition, "Production ClasicoGameDefinition must load from Resources.");

            Assert.AreEqual(4.2f, definition.DetectiveDecisionWindowSeconds, 0.001f);
            Assert.AreEqual(4.2f, definition.GameShowDecisionWindowSeconds, 0.001f);
            Assert.AreEqual(4.2f, definition.DetectiveFeedbackDisplaySeconds, 0.001f);
            Assert.AreEqual(4.4f, definition.GameShowFeedbackDisplaySeconds, 0.001f);
            Assert.AreEqual(1.5f, definition.GameShowIntroSeconds, 0.001f, "Game Show's opening must not be stretched.");

            Assert.AreEqual(3.2f, definition.DecisionWindowSeconds, 0.001f, "Western's (shared) decision window must be unchanged.");
            Assert.AreEqual(7f, definition.BalanceDecisionWindowSeconds, 0.001f, "Balance's decision window must be unchanged.");
            Assert.AreEqual(4.5f, definition.BalanceFeedbackDisplaySeconds, 0.001f, "Balance's feedback must be unchanged.");
            Assert.AreEqual(6.9f, definition.EncounterIntroSeconds, 0.001f, "Western's encounter intro must be unchanged.");
        }

        /// <summary>C8.1l: the director actually uses the per-archetype
        /// windows — an unanswered Detective/Game Show round stays in
        /// Decision past the shared 3.2s and times out after 4.2s, while
        /// Western times out at 3.2s and Balance keeps its own window.</summary>
        [Test]
        public void DetectiveAndGameShow_DecisionWindowsAreTheirOwn_WesternAndBalanceUnchanged()
        {
            var definition = ClasicoGameDefinition.CreateInMemory(
                "clasico_test", "Clasico Test", 8, decisionWindowSeconds: 3.2f, balanceDecisionWindowSeconds: 7f,
                feedbackDisplaySeconds: 0.1f, detectiveDecisionWindowSeconds: 4.2f, gameShowDecisionWindowSeconds: 4.2f);

            var director = new ClasicoSessionDirector();
            director.Begin(new GameContext(NullGameAnalyticsSink.Instance, new Random(0)), definition, new GameSession(definition.GameId));

            var seen = new HashSet<MicrogameArchetype>();
            for (var i = 0; i < definition.MicrogameCount && !director.IsFinished; i++)
            {
                var archetype = director.CurrentArchetype;
                seen.Add(archetype);

                director.Tick(0.001f); // Intro -> Decision
                Assert.IsTrue(director.IsDecisionPhase, $"round {i} ({archetype}) must reach Decision.");

                director.Tick(3.3f);
                switch (archetype)
                {
                    case MicrogameArchetype.AimSelect:
                        Assert.IsFalse(director.IsDecisionPhase, "Western must still time out at the shared 3.2s.");
                        break;
                    case MicrogameArchetype.Balance:
                        Assert.IsTrue(director.IsDecisionPhase, "Balance keeps its own longer window.");
                        director.Tick(3.8f);
                        break;
                    default:
                        Assert.IsTrue(director.IsDecisionPhase, $"{archetype} must still be open past the old 3.2s window.");
                        director.Tick(1.0f);
                        Assert.IsFalse(director.IsDecisionPhase, $"{archetype} must time out after its 4.2s window.");
                        break;
                }

                director.Tick(0.001f); // Lock -> Feedback
                director.Tick(0.2f);   // Feedback -> next Intro
            }

            Assert.IsTrue(seen.Contains(MicrogameArchetype.ChooseSide) && seen.Contains(MicrogameArchetype.DetectError),
                "The session must include both Game Show and Detective rounds.");
        }

        /// <summary>C9.1: the director's read-only session result for the
        /// Hermit economy — one record per resolved round, real interaction
        /// only when an answer was committed (a timeout is not interaction),
        /// and natural completion vs abort.</summary>
        [Test]
        public void SessionResult_RecordsEveryRound_InteractionAndCompletion()
        {
            var (answered, answeredSession) = BeginDirector(microgameCount: 4, decisionWindowSeconds: 1f, balanceDecisionWindowSeconds: 1f, seed: 3);
            while (!answered.IsFinished)
            {
                ResolveCurrent(answered, answerCorrectly: true);
            }

            answered.BuildResult(completed: true);
            var result = answered.LastSessionResult;
            Assert.IsTrue(result.CompletedNaturally);
            Assert.AreEqual(4, result.PlannedRounds);
            Assert.AreEqual(4, result.Rounds.Count);
            Assert.AreEqual(answeredSession.SessionId, result.SessionId);
            Assert.IsTrue(result.Rounds.All(r => r.Interacted), "Every answered round must count as interacted.");
            Assert.AreEqual(answeredSession.Correct, result.CorrectAnswers);
            Assert.IsTrue(result.Rounds.All(r => r.AnswerWindowSeconds > 0f));

            var (passive, _) = BeginDirector(microgameCount: 4, decisionWindowSeconds: 1f, balanceDecisionWindowSeconds: 1f, seed: 4);
            while (!passive.IsFinished)
            {
                passive.Tick(0.001f); // Intro -> Decision
                passive.Tick(1.5f);   // decision window runs out
                passive.Tick(0.001f); // Lock -> Feedback
                passive.Tick(0.001f); // Feedback -> next
            }

            passive.BuildResult(completed: true);
            Assert.AreEqual(4, passive.LastSessionResult.Rounds.Count);
            Assert.IsTrue(passive.LastSessionResult.Rounds.All(r => !r.Interacted && !r.Correct), "A timeout is never interaction.");

            var (aborted, _) = BeginDirector(microgameCount: 4, seed: 5);
            ResolveCurrent(aborted, answerCorrectly: true);
            aborted.BuildResult(completed: false);
            Assert.IsFalse(aborted.LastSessionResult.CompletedNaturally);
            Assert.AreEqual(1, aborted.LastSessionResult.Rounds.Count);
        }

        private static void ProductionSubmitAnswer(ClasicoSessionDirector director, bool answerCorrectly)
        {
            switch (director.CurrentArchetype)
            {
                case MicrogameArchetype.AimSelect:
                {
                    var options = director.CurrentClassification.CategoryOptions;
                    var correctIndex = Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                    director.SubmitSelection(answerCorrectly ? correctIndex : (correctIndex + 1) % options.Length);
                    break;
                }

                case MicrogameArchetype.ChooseSide:
                {
                    var correctIndex = director.CurrentTrueFalse.IsTrue ? 0 : 1;
                    director.SubmitSelection(answerCorrectly ? correctIndex : 1 - correctIndex);
                    break;
                }

                case MicrogameArchetype.Balance:
                {
                    SubmitDebitCreditAnswer(director, answerCorrectly);
                    break;
                }

                default:
                {
                    var challenge = director.CurrentErrorDetection;
                    var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;
                    var chosen = answerCorrectly ? displayAnomalyIndex : (displayAnomalyIndex + 1) % challenge.Items.Length;
                    director.SubmitSelection(chosen);
                    break;
                }
            }
        }

        // --- C8.1g.2: Detective's runtime display-shuffle contract (brief
        // sections 4/5/19) — eliminates the C8.1g.1 audit's "anomaly always
        // at the same slot" position-cheat. BuildSequence guarantees every
        // archetype appears at least twice for a count >= 8, so
        // microgameCount: 8 always surfaces at least 2 DetectError rounds
        // per session below.

        [Test]
        public void ErrorDetection_DisplayOrderIsAPermutation_AndNeverMutatesTheSourcePool()
        {
            var poolSnapshot = ClasicoMicrogameLibrary.ErrorDetectionPool
                .ToDictionary(c => c.Id, c => (string[])c.Items.Clone());

            for (var seed = 0; seed < 15; seed++)
            {
                var (director, _) = BeginDirector(microgameCount: 8, seed: seed);

                for (var i = 0; i < 8 && !director.IsFinished; i++)
                {
                    if (director.CurrentArchetype == MicrogameArchetype.DetectError)
                    {
                        var challenge = director.CurrentErrorDetection;
                        var displayOrder = director.CurrentErrorDetectionDisplayOrder;

                        Assert.AreEqual(challenge.Items.Length, displayOrder.Count,
                            $"seed={seed}: display order length must match Items length.");
                        CollectionAssert.AreEquivalent(
                            Enumerable.Range(0, challenge.Items.Length), displayOrder,
                            $"seed={seed}: display order must be a permutation of [0..Items.Length) — no duplicates, no gaps.");

                        // The source pool's own array must never be mutated
                        // in place by the shuffle — every round must still
                        // find the exact original authored content.
                        CollectionAssert.AreEqual(poolSnapshot[challenge.Id], challenge.Items,
                            $"seed={seed}: '{challenge.Id}' Items array was mutated — the display shuffle must never touch the source challenge.");
                    }

                    ResolveCurrent(director, answerCorrectly: true);
                }
            }
        }

        [Test]
        public void ErrorDetection_DisplayAnomalyIndex_AlwaysMapsBackToTheAuthoredAnomaly()
        {
            for (var seed = 0; seed < 15; seed++)
            {
                var (director, _) = BeginDirector(microgameCount: 8, seed: seed);

                for (var i = 0; i < 8 && !director.IsFinished; i++)
                {
                    if (director.CurrentArchetype == MicrogameArchetype.DetectError)
                    {
                        var challenge = director.CurrentErrorDetection;
                        var displayOrder = director.CurrentErrorDetectionDisplayOrder;
                        var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;

                        Assert.AreEqual(challenge.AnomalyIndex, displayOrder[displayAnomalyIndex],
                            $"seed={seed}: the item actually rendered at the reported display-anomaly slot must be the authored anomaly.");
                    }

                    ResolveCurrent(director, answerCorrectly: true);
                }
            }
        }

        [Test]
        public void ErrorDetection_SeededRun_ProducesIdenticalDisplayOrderEveryTime()
        {
            const int seed = 4242;
            var (directorA, _) = BeginDirector(microgameCount: 8, seed: seed);
            var (directorB, _) = BeginDirector(microgameCount: 8, seed: seed);

            for (var i = 0; i < 8 && !directorA.IsFinished; i++)
            {
                Assert.AreEqual(directorA.CurrentArchetype, directorB.CurrentArchetype, $"round {i}: archetype must match under the same seed.");

                if (directorA.CurrentArchetype == MicrogameArchetype.DetectError)
                {
                    Assert.AreEqual(directorA.CurrentErrorDetection.Id, directorB.CurrentErrorDetection.Id, $"round {i}: same seed must draw the same challenge.");
                    CollectionAssert.AreEqual(directorA.CurrentErrorDetectionDisplayOrder, directorB.CurrentErrorDetectionDisplayOrder,
                        $"round {i}: same seed must produce the exact same display order.");
                    Assert.AreEqual(directorA.CurrentErrorDetectionDisplayAnomalyIndex, directorB.CurrentErrorDetectionDisplayAnomalyIndex, $"round {i}: same seed must produce the same display anomaly slot.");
                }

                ResolveCurrent(directorA, answerCorrectly: true);
                ResolveCurrent(directorB, answerCorrectly: true);
            }
        }

        [Test]
        public void ErrorDetection_DisplayAnomalyIndex_CanLandInEveryDisplaySlotAcrossSeeds()
        {
            var seenSlots = new HashSet<int>();

            for (var seed = 0; seed < 40; seed++)
            {
                var (director, _) = BeginDirector(microgameCount: 8, seed: seed);

                for (var i = 0; i < 8 && !director.IsFinished; i++)
                {
                    if (director.CurrentArchetype == MicrogameArchetype.DetectError)
                    {
                        seenSlots.Add(director.CurrentErrorDetectionDisplayAnomalyIndex);
                    }

                    ResolveCurrent(director, answerCorrectly: true);
                }
            }

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, seenSlots,
                $"Across 40 seeds the anomaly only ever landed in slots [{string.Join(",", seenSlots)}] — the C8.1g.1 audit's 'always slot 3' position-cheat may still be present.");
        }
    }
}
