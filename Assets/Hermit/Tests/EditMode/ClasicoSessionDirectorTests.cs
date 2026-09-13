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
                    var challenge = director.CurrentEquation;
                    if (answerCorrectly)
                    {
                        var steps = (int)Math.Round((challenge.CorrectValue - challenge.StartValue) / challenge.StepSize);
                        var direction = Math.Sign(steps);
                        for (var i = 0; i < Math.Abs(steps); i++)
                        {
                            director.NudgeBalance(direction);
                        }
                    }

                    director.ConfirmBalance();
                    wasCorrect = answerCorrectly;
                    break;
                }

                default: // DetectError
                {
                    var challenge = director.CurrentErrorDetection;
                    var chosen = answerCorrectly ? challenge.AnomalyIndex : (challenge.AnomalyIndex + 1) % challenge.Items.Length;
                    director.SubmitSelection(chosen);
                    wasCorrect = chosen == challenge.AnomalyIndex;
                    break;
                }
            }

            director.Tick(0.001f); // Lock -> Feedback
            director.Tick(0.001f); // Feedback -> next Intro (or finished)
            return wasCorrect;
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
                    director.ConfirmBalance();
                    director.NudgeBalance(1);
                    director.ConfirmBalance();
                    break;
                default:
                    director.SubmitSelection(0);
                    director.SubmitSelection(1);
                    break;
            }

            Assert.AreEqual(1, session.Correct + session.Incorrect, "A resolved microgame must not be resolved twice.");
        }

        [Test]
        public void Equation_ConfirmingWithoutNudging_IsAlwaysIncorrect()
        {
            for (var seed = 0; seed < 10; seed++)
            {
                var (director, _) = BeginDirector(microgameCount: 4, seed: seed);
                for (var i = 0; i < 4 && !director.IsFinished; i++)
                {
                    if (director.CurrentArchetype == MicrogameArchetype.Balance)
                    {
                        director.Tick(0.001f);
                        director.ConfirmBalance();
                        Assert.IsFalse(director.LastAnswerCorrect, $"Seed {seed}: StartValue must never already equal CorrectValue by construction.");
                        return;
                    }

                    ResolveCurrent(director, answerCorrectly: true);
                }
            }

            Assert.Fail("No Balance microgame was drawn across 10 seeds — sequencing regressed.");
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
                    director.Tick(Math.Max(definition.EncounterIntroSeconds, definition.CommandBeatSeconds) + 0.5f);
                    Assert.IsTrue(director.IsDecisionPhase, $"seed={seed} round={i} ({archetypes[i]}, roundWithinEncounter={roundsWithin[i]}) did not reach Decision after a generous Intro-covering tick.");

                    ProductionSubmitAnswer(director, answerCorrectly: true);

                    director.Tick(definition.LockSeconds + 0.1f);
                    director.Tick(definition.FeedbackDisplaySeconds + definition.EncounterOutroSeconds + 0.2f);
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
                    var challenge = director.CurrentEquation;
                    if (answerCorrectly)
                    {
                        var steps = (int)Math.Round((challenge.CorrectValue - challenge.StartValue) / challenge.StepSize);
                        var direction = Math.Sign(steps);
                        for (var i = 0; i < Math.Abs(steps); i++)
                        {
                            director.NudgeBalance(direction);
                        }
                    }

                    director.ConfirmBalance();
                    break;
                }

                default:
                {
                    var challenge = director.CurrentErrorDetection;
                    var chosen = answerCorrectly ? challenge.AnomalyIndex : (challenge.AnomalyIndex + 1) % challenge.Items.Length;
                    director.SubmitSelection(chosen);
                    break;
                }
            }
        }
    }
}
