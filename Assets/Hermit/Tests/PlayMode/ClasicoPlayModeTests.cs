using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Hermit.Games;
using Hermit.Games.Clasico;
using Hermit.Games.Clasico.Microgames;
using Hermit.Runtime.GameFramework;
using Hermit.Runtime.GameFramework.Microgames;

namespace Hermit.Tests.PlayMode
{
    /// <summary>
    /// C8.1 rewrite — Clasico is no longer one fixed Q&amp;A shape, so this
    /// file no longer looks for a single "Question"/"Option0" screen. It
    /// drives the real, composed runtime (GameSessionInstaller +
    /// ClasicoGameHost + the four Gold microgame presenters) through actual
    /// UI Button.onClick events, reading <see cref="ClasicoSessionDirector"/>
    /// (public, same as the deleted ClasicoGameEngine was) to know which
    /// archetype is currently showing and therefore which named buttons to
    /// click — never assuming a fixed screen shape.
    /// </summary>
    public class ClasicoPlayModeTests
    {
        private GameObject _root;
        private GameSessionInstaller _installer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("GameSessionTest");
            _installer = _root.AddComponent<GameSessionInstaller>();
            // C8.1d.8: WesternAudioEvents is a process-lifetime static log —
            // clear it per test so an earlier test's recorded events can
            // never leak into this one's assertions.
            WesternAudioEvents.Clear();
            GameShowAudioEvents.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            yield return null;
        }

        private Button FindButton(string name) => _root.GetComponentsInChildren<Button>(true).First(b => b.name == name);
        private Text FindText(string name) => _root.GetComponentsInChildren<Text>(true).First(t => t.name == name);
        private RectTransform FindRect(string name) => _root.GetComponentsInChildren<RectTransform>(true).First(r => r.name == name);

        /// <summary>All four Gold presenters are built once and coexist as
        /// siblings under StageRoot (only shown/hidden, never rebuilt) — so a
        /// plain name lookup for "Background"/"Portrait" is ambiguous across
        /// worlds. This disambiguates by requiring a named ancestor (the
        /// presenter's own root, or a portrait frame's own name).</summary>
        private Image FindImageUnder(string ancestorName, string imageName) =>
            _root.GetComponentsInChildren<Image>(true)
                .First(img => img.name == imageName && HasAncestorNamed(img.transform, ancestorName));

        /// <summary>C8.1f.3: same disambiguation need as
        /// <see cref="FindImageUnder"/> — Balance's debit and credit account
        /// tokens are each built as an identically-named "Label" Text
        /// (mirrors production's own <c>BalanceMachinePresenter.BuildToken</c>,
        /// which never needed to distinguish them by name since production
        /// code holds its own direct field references), so a plain
        /// <see cref="FindText"/> lookup is ambiguous between the two —
        /// this requires the caller to specify which pan ("LeftPan" for the
        /// debit token, "RightPan" for the credit token) it means.</summary>
        private Text FindTextUnder(string ancestorName, string textName) =>
            _root.GetComponentsInChildren<Text>(true)
                .First(t => t.name == textName && HasAncestorNamed(t.transform, ancestorName));

        private static bool HasAncestorNamed(Transform t, string name)
        {
            for (var current = t; current != null; current = current.parent)
            {
                if (current.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Answers correctly, archetype-agnostic, until the given
        /// archetype is the one currently showing (or the session ends) —
        /// used by the C8.1d art-integration tests below, which only care
        /// about one specific world's presenter.</summary>
        private IEnumerator CycleUntilArchetype(MicrogameArchetype target)
        {
            const int maxIterations = 9;
            for (var i = 0; i < maxIterations && _installer.FlowController.State == GameLifecycleState.Playing; i++)
            {
                if (GetDirector().CurrentArchetype == target)
                {
                    yield break;
                }

                yield return AnswerCurrentMicrogame(answerCorrectly: true);
            }
        }

        private ClasicoSessionDirector GetDirector() => (ClasicoSessionDirector)_installer.FlowController.CurrentEngine;

        private static GameObject AssertValidSelection(string expectedName = null)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(selected, "Nothing is selected — keyboard Navigate/Submit have nothing to act on.");
            Assert.IsTrue(selected.activeInHierarchy, $"Selected object '{selected.name}' is not active in the hierarchy.");
            if (expectedName != null)
            {
                Assert.AreEqual(expectedName, selected.name);
            }

            return selected;
        }

        /// <summary>Same regression this class has guarded since C7 (see
        /// ClasicoGameHost's own doc-comment): a reveal-feedback animation
        /// must always restore the thing it animates to its resting
        /// transform. The thing being punched/shaken is now the shared
        /// StageRoot (whichever presenter is inside it), not a single
        /// question card.
        ///
        /// restingPosition is captured by the caller right after launch
        /// (before any punch/shake could have run) rather than assumed to be
        /// Vector2.zero — StageRoot's resting anchoredPosition depends on
        /// ClasicoHud.TopHudReservedHeight (the C8.1 timer/concept layout
        /// fix insets it from the top), so hardcoding zero here would make
        /// this assertion stale the next time that inset is tuned.</summary>
        private static void AssertStageIsOnScreen(RectTransform stageRoot, Vector2 restingPosition)
        {
            Assert.Less(Vector2.Distance(stageRoot.anchoredPosition, restingPosition), 5f,
                $"StageRoot drifted to {stageRoot.anchoredPosition} (resting position is {restingPosition}) — a reveal animation did not restore its resting position.");
            Assert.Less(Vector2.Distance(stageRoot.localScale, Vector2.one), 0.05f,
                $"StageRoot scale is stuck at {stageRoot.localScale} — a reveal animation did not restore its resting scale.");
        }

        private void AssertControlsForArchetypeAreInteractable(MicrogameArchetype archetype)
        {
            switch (archetype)
            {
                case MicrogameArchetype.AimSelect:
                    Assert.IsTrue(FindButton("WesternTarget0").interactable);
                    break;
                case MicrogameArchetype.ChooseSide:
                    Assert.IsTrue(FindButton("GameShowTrue").interactable);
                    Assert.IsTrue(FindButton("GameShowFalse").interactable);
                    break;
                case MicrogameArchetype.Balance:
                    Assert.IsTrue(FindButton("BalanceAccountOption0").interactable);
                    break;
                default:
                    Assert.IsTrue(FindButton("DetectiveSuspect0").interactable);
                    break;
            }
        }

        private bool IsPlaying() => _installer.FlowController.State == GameLifecycleState.Playing;

        /// <summary>C8.1d.5: polls once per frame instead of sleeping a fixed
        /// duration — the C8.1d.5 brief's own lesson (a Balance round's real
        /// decision-timeout window, Feedback(0.8)+CommandBeat(0.6)+
        /// BalanceDecisionWindow(6)=7.4s, is now *shorter* than a flat wait
        /// sized for the worst-case Western cinematic gate once that gate
        /// grew past ~5.9s) — a fixed "wait past the worst case" constant
        /// can no longer simultaneously be long enough for Western round 1
        /// and short enough to never let an untouched Balance round silently
        /// time out (and cascade an extra, uncounted round) during the same
        /// wait. State/event synchronization has no such ceiling: it returns
        /// the instant the real condition is true, regardless of which
        /// archetype or Encounter timing produced it. <paramref name="timeoutSeconds"/>
        /// is a generous safety net (loud test failure), never the normal
        /// exit path.</summary>
        private IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds, string timeoutMessage)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), timeoutMessage);
        }

        private IEnumerator LaunchClasico()
        {
            FindButton("Game_clasico").onClick.Invoke();
            yield return WaitUntil(
                () => !IsPlaying() || GetDirector().IsDecisionPhase,
                20f,
                "The first microgame never reached its Decision phase within 20s.");
        }

        /// <summary>Answers whichever microgame is currently showing, correctly
        /// or incorrectly, via the real named UI control for its archetype —
        /// then waits, state-driven (see <see cref="WaitUntil"/>), for
        /// Decision to be left (Lock/Feedback begins) and then re-entered on
        /// the next round (or for the session to end).</summary>
        private IEnumerator AnswerCurrentMicrogame(bool answerCorrectly)
        {
            var director = GetDirector();

            switch (director.CurrentArchetype)
            {
                case MicrogameArchetype.AimSelect:
                {
                    var options = director.CurrentClassification.CategoryOptions;
                    var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                    var chosen = answerCorrectly ? correctIndex : (correctIndex + 1) % options.Length;
                    FindButton($"WesternTarget{chosen}").onClick.Invoke();
                    break;
                }

                case MicrogameArchetype.ChooseSide:
                {
                    var correctIsTrue = director.CurrentTrueFalse.IsTrue;
                    var chooseTrue = answerCorrectly ? correctIsTrue : !correctIsTrue;
                    FindButton(chooseTrue ? "GameShowTrue" : "GameShowFalse").onClick.Invoke();
                    break;
                }

                case MicrogameArchetype.Balance:
                {
                    var challenge = director.CurrentDebitCredit;
                    var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
                    var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);
                    var debitChoice = answerCorrectly ? correctDebitIndex : (correctDebitIndex + 1) % challenge.AccountOptions.Length;
                    var creditChoice = answerCorrectly ? correctCreditIndex : (correctCreditIndex + 1) % challenge.AccountOptions.Length;
                    FindButton($"BalanceAccountOption{debitChoice}").onClick.Invoke();
                    FindButton($"BalanceAccountOption{creditChoice}").onClick.Invoke();
                    break;
                }

                default:
                {
                    // C8.1g.2: the engine (and thus which button is
                    // correct) now resolves against the DISPLAY anomaly
                    // slot (post-shuffle), never the challenge's own
                    // authored AnomalyIndex.
                    var challenge = director.CurrentErrorDetection;
                    var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;
                    var chosen = answerCorrectly ? displayAnomalyIndex : (displayAnomalyIndex + 1) % challenge.Items.Length;
                    FindButton($"DetectiveSuspect{chosen}").onClick.Invoke();
                    break;
                }
            }

            yield return WaitUntil(
                () => !IsPlaying() || !GetDirector().IsDecisionPhase,
                5f,
                "Answering did not leave Decision phase (Lock/Feedback never began) within 5s.");

            yield return WaitUntil(
                () => !IsPlaying() || GetDirector().IsDecisionPhase,
                20f,
                "The next round never reached its Decision phase within 20s.");
        }

        [UnityTest]
        public IEnumerator Installer_BuildsTheUI_StartingOnTheSelectorScreen()
        {
            Assert.AreEqual(GameLifecycleState.Idle, _installer.FlowController.State);
            Assert.IsNotNull(FindButton("Game_clasico"));
            yield break;
        }

        [UnityTest]
        public IEnumerator EventSystem_ExistsAndUsesInputSystemUIInputModule()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            Assert.IsNotNull(eventSystem);
            Assert.IsNotNull(eventSystem.GetComponent<InputSystemUIInputModule>());
            yield break;
        }

        [UnityTest]
        public IEnumerator SelectorScreen_HasAVisibleInitialSelection()
        {
            AssertValidSelection("Game_clasico");
            yield break;
        }

        [UnityTest]
        public IEnumerator LaunchingClasico_ShowsACountdown_BeforeTheFirstMicrogame()
        {
            FindButton("Game_clasico").onClick.Invoke();
            yield return null;

            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
            Assert.IsTrue(GetDirector().IsCountingDown);
            AssertValidSelection("AbortButton");

            yield return WaitUntil(() => !GetDirector().IsCountingDown, 6f, "The pre-run countdown never finished within 6s.");
            yield return WaitUntil(() => GetDirector().IsDecisionPhase, 20f, "The first microgame never reached its Decision phase within 20s.");

            Assert.IsFalse(GetDirector().IsCountingDown);
            AssertControlsForArchetypeAreInteractable(GetDirector().CurrentArchetype);
        }

        [UnityTest]
        public IEnumerator LaunchingClasico_StartsASession_AndShowsAMicrogame()
        {
            yield return LaunchClasico();

            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
            var director = GetDirector();
            Assert.IsFalse(director.IsCountingDown);
            Assert.IsTrue(director.IsDecisionPhase);
            AssertControlsForArchetypeAreInteractable(director.CurrentArchetype);
        }

        [UnityTest]
        public IEnumerator EveryArchetype_RendersDistinctInteractableControls_WhenItAppears()
        {
            yield return LaunchClasico();
            var stageRoot = FindRect("StageRoot");
            var stageRestingPosition = stageRoot.anchoredPosition;

            var seen = new HashSet<MicrogameArchetype>();
            const int maxIterations = 9;
            for (var i = 0; i < maxIterations && _installer.FlowController.State == GameLifecycleState.Playing; i++)
            {
                var archetype = GetDirector().CurrentArchetype;
                seen.Add(archetype);
                AssertControlsForArchetypeAreInteractable(archetype);
                AssertStageIsOnScreen(stageRoot, stageRestingPosition);

                yield return AnswerCurrentMicrogame(answerCorrectly: true);
            }

            Assert.AreEqual(4, seen.Count, "A full 9-microgame session must show all four Gold archetypes at least once.");
        }

        [UnityTest]
        public IEnumerator AtLeastThreeArchetypeSwitches_OccurWithinOneSession()
        {
            yield return LaunchClasico();

            var previous = GetDirector().CurrentArchetype;
            var switches = 0;
            const int maxIterations = 9;

            for (var i = 0; i < maxIterations && _installer.FlowController.State == GameLifecycleState.Playing; i++)
            {
                yield return AnswerCurrentMicrogame(answerCorrectly: true);
                if (_installer.FlowController.State != GameLifecycleState.Playing)
                {
                    break;
                }

                var current = GetDirector().CurrentArchetype;
                if (current != previous)
                {
                    switches++;
                }

                previous = current;
            }

            Assert.GreaterOrEqual(switches, 3, "Expected at least 3 archetype switches across a 9-microgame session.");
        }

        [UnityTest]
        public IEnumerator IncorrectAnswer_TransitionsCleanly_IntoADifferentMicrogameType()
        {
            // The exact regression this phase's brief calls out by name: an
            // incorrect answer must transition cleanly even when the next
            // microgame is a completely different archetype (mouse/world/
            // input all change at once) — the C7 bug class this guards
            // against (an animation that never reaches its own cleanup line)
            // would be *more* likely to surface across a bigger visual jump,
            // not less.
            yield return LaunchClasico();
            var stageRoot = FindRect("StageRoot");
            var stageRestingPosition = stageRoot.anchoredPosition;

            var incorrectTransitionsObserved = 0;
            const int maxIterations = 9;

            for (var i = 0; i < maxIterations && _installer.FlowController.State == GameLifecycleState.Playing; i++)
            {
                var before = GetDirector().CurrentArchetype;
                var incorrectBefore = _installer.FlowController.CurrentSession.Incorrect;

                yield return AnswerCurrentMicrogame(answerCorrectly: false);

                if (_installer.FlowController.State != GameLifecycleState.Playing)
                {
                    break;
                }

                var after = GetDirector().CurrentArchetype;
                Assert.AreEqual(incorrectBefore + 1, _installer.FlowController.CurrentSession.Incorrect, "AnswerCurrentMicrogame(false) must always register as incorrect.");

                if (after != before)
                {
                    incorrectTransitionsObserved++;
                    AssertControlsForArchetypeAreInteractable(after);
                    AssertStageIsOnScreen(stageRoot, stageRestingPosition);
                }
            }

            Assert.GreaterOrEqual(incorrectTransitionsObserved, 2, "Expected at least two incorrect-answer transitions into a different microgame type across a 9-microgame session.");
        }

        [UnityTest]
        public IEnumerator AnsweringEveryMicrogame_ReachesResults()
        {
            yield return LaunchClasico();

            const int maxIterations = 12;
            for (var i = 0; i < maxIterations && _installer.FlowController.State == GameLifecycleState.Playing; i++)
            {
                yield return AnswerCurrentMicrogame(answerCorrectly: true);
            }

            Assert.AreEqual(GameLifecycleState.Results, _installer.FlowController.State);
            Assert.IsTrue(_installer.FlowController.LastResult.Completed);
            Assert.IsFalse(string.IsNullOrEmpty(FindText("ResultsSummary").text));
            AssertValidSelection("RestartButton");
        }

        [UnityTest]
        public IEnumerator Restart_FromResults_StartsAFreshSession_WithAValidSelection()
        {
            yield return LaunchClasico();
            const int maxIterations = 12;
            for (var i = 0; i < maxIterations && _installer.FlowController.State == GameLifecycleState.Playing; i++)
            {
                yield return AnswerCurrentMicrogame(answerCorrectly: true);
            }

            FindButton("RestartButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
            Assert.AreEqual(0, _installer.FlowController.CurrentSession.Score);
            AssertValidSelection("AbortButton");

            yield return WaitUntil(() => GetDirector().IsDecisionPhase, 20f, "The restarted session's first microgame never reached its Decision phase within 20s.");
            AssertControlsForArchetypeAreInteractable(GetDirector().CurrentArchetype);
        }

        [UnityTest]
        public IEnumerator Abort_DuringPlay_ReachesResults_AsIncomplete()
        {
            yield return LaunchClasico();

            FindButton("AbortButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(GameLifecycleState.Results, _installer.FlowController.State);
            Assert.IsFalse(_installer.FlowController.LastResult.Completed);
            AssertValidSelection("RestartButton");
        }

        [UnityTest]
        public IEnumerator ExitFromResults_ReturnsToTheSelector_AndClasicoCanLaunchAgain()
        {
            yield return LaunchClasico();
            FindButton("AbortButton").onClick.Invoke();
            yield return null;

            FindButton("ExitButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(GameLifecycleState.Idle, _installer.FlowController.State);
            AssertValidSelection("Game_clasico");

            yield return LaunchClasico();
            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
        }

        // --- C8.1d Gold art integration — see Docs/C8_1D_GOLD_ART_INTEGRATION.md.
        // These assert the illustrated art actually made it into the built
        // presenters (non-null sprite assignments) and that every new
        // reaction (sprite-swap or punch on a portrait card) restores its
        // resting transform exactly like every other LocalMotionFx reaction
        // already has to.

        [UnityTest]
        public IEnumerator WesternShootout_ShowsIllustratedBackground_AndCoreStructure()
        {
            // C8.1d correction: manual validation rejected the earlier
            // portrait-card Sheriff as "pasted on" rather than "standing in
            // the world" — see Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Manual
            // Validation Correction". C8.1d.4 replaced the in-world Sheriff
            // entirely with a cinematic close-up intro (see the dedicated
            // WesternCinematic_* tests below) — this test now only asserts
            // the structural pieces that never changed: the background, no
            // rejected portrait card, the off-screen shot rig, and the four
            // outlaw slots.
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.AimSelect);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            var background = FindImageUnder("WesternShootout", "Background");
            Assert.IsNotNull(background.sprite, "Western background art should be assigned from Resources/Art/Gold/Western/WesternBackground.");

            var noPortraitCard = _root.GetComponentsInChildren<Transform>(true)
                .Where(t => HasAncestorNamed(t, "WesternShootout"))
                .All(t => t.name != "SheriffCameo" && t.name != "PortraitFrame" && t.name != "SheriffActor");
            Assert.IsTrue(noPortraitCard, "The rejected Sheriff portrait card, and the since-superseded C8.1d.3 SheriffActor, must not exist in Western's active composition.");

            // C8.1d.3/.4: the muzzle flash lives on a dedicated
            // OffscreenShotOrigin (the gameplay shot's visible origin),
            // independent of any Sheriff actor.
            Assert.IsNotNull(FindRect("OffscreenShotOrigin"), "An OffscreenShotOrigin must exist for the gameplay shot's muzzle flash/tracer.");
            Assert.IsNotNull(FindImageUnder("OffscreenShotOrigin", "MuzzleFlash"), "OffscreenShotOrigin must own the muzzle flash used for the gameplay shot.");

            foreach (var i in Enumerable.Range(0, 4))
            {
                Assert.IsNotNull(FindButton($"WesternTarget{i}"), $"Outlaw slot WesternTarget{i} must exist.");
            }
        }

        [UnityTest]
        public IEnumerator WesternShootout_FireSequence_RestoresScreenKickTransform()
        {
            // C8.1d.3: Sheriff no longer recoils (he isn't on-screen during
            // gameplay at all) — the fire sequence's own transform-motion
            // reaction is now a small screen kick on the whole WesternShootout
            // root, which must restore to rest exactly like every other
            // LocalMotionFx reaction in this codebase.
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.AimSelect);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            var westernRoot = FindRect("WesternShootout");
            yield return AnswerCurrentMicrogame(answerCorrectly: true);

            Assert.Less(Vector2.Distance(westernRoot.localScale, Vector3.one), 0.05f,
                "The WesternShootout root must restore its resting scale after the fire sequence's screen-kick punch.");
        }

        // --- C8.1d Western production sprite integration — see
        // Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Western Production Sprite
        // Integration". Real transparent Sheriff (Idle/Aim/Fire) and outlaw
        // (Neutral/Hit ×4) sprites now exist, replacing the procedural
        // placeholders these tests previously exercised.

        [UnityTest]
        public IEnumerator WesternShootout_ProductionSpritesLoad_AndReplacePlaceholderVisuals()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.AimSelect);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            for (var i = 0; i < 4; i++)
            {
                var letter = (char)('A' + i);
                var outlawSprite = FindImageUnder($"WesternTarget{i}", "Sprite");
                Assert.IsNotNull(outlawSprite.sprite, $"Outlaw_{letter}'s Sprite Image must have a non-null sprite (Outlaw_{letter}_Neutral).");

                var targetRoot = FindButton($"WesternTarget{i}").transform;
                var placeholderGone = targetRoot.GetComponentsInChildren<Transform>(true)
                    .All(t => t.name != "Head" && t.name != "Hat" && t.name != "Bandana");
                Assert.IsTrue(placeholderGone, $"Outlaw_{letter}'s procedural placeholder parts (Head/Hat/Bandana) must not exist once real outlaw art is loaded.");
            }
        }

        /// <summary>Western Outlaw Candidate 02 production family: the new
        /// Neutral sprites (350x820 canvas) and Hit sprites (420x820 —
        /// deliberately wider, to fit the raised-fist/kicked-leg reaction
        /// poses without clipping them) have different pixel aspect ratios.
        /// The outlaw Sprite Image uses <c>preserveAspect</c> inside a fixed
        /// 240x320 target box; since both new aspect ratios are narrower
        /// than the box's own (0.75), the box's HEIGHT is the constraining
        /// dimension for both, so both must render at the exact same
        /// effective on-screen height — proving Neutral-&gt;Hit sprite swap
        /// causes no visible size jump without needing to touch the
        /// presenter or the target box (brief: "fix the presentation sizing
        /// method, not the source art" -- verified here that no fix was
        /// actually needed). Computed directly from each Sprite asset's own
        /// pixel rect against the real target box size, not by rendering
        /// and measuring a mesh.</summary>
        [Test]
        public void WesternOutlaws_NeutralAndHitSprites_RenderAtTheSameApparentHeight()
        {
            const float boxWidth = 240f;
            const float boxHeight = 320f;

            for (var i = 0; i < 4; i++)
            {
                var letter = (char)('A' + i);
                var neutral = Resources.Load<Sprite>($"Art/Gold/Western/Actors/Outlaw_{letter}_Neutral");
                var hit = Resources.Load<Sprite>($"Art/Gold/Western/Actors/Outlaw_{letter}_Hit");
                Assume.That(neutral, Is.Not.Null, $"Outlaw_{letter}_Neutral must load.");
                Assume.That(hit, Is.Not.Null, $"Outlaw_{letter}_Hit must load.");

                var neutralScale = Mathf.Min(boxWidth / neutral.rect.width, boxHeight / neutral.rect.height);
                var hitScale = Mathf.Min(boxWidth / hit.rect.width, boxHeight / hit.rect.height);

                var neutralHeight = neutral.rect.height * neutralScale;
                var hitHeight = hit.rect.height * hitScale;

                Assert.AreEqual(boxHeight, neutralHeight, 0.5f, $"Outlaw_{letter}_Neutral must be height-constrained (fill the target box's full 320-unit height), not width-constrained.");
                Assert.AreEqual(boxHeight, hitHeight, 0.5f, $"Outlaw_{letter}_Hit must be height-constrained (fill the target box's full 320-unit height), not width-constrained.");
                Assert.AreEqual(neutralHeight, hitHeight, 0.5f, $"Outlaw_{letter}'s Neutral and Hit sprites must render at the same apparent height despite their different canvas widths — no visible size jump on the Hit-state swap.");
            }
        }

        /// <summary>C8.1d.4: the three cinematic close-up stills must all
        /// load from their production path before anything else about the
        /// cinematic intro can be trusted.</summary>
        [Test]
        public void WesternCinematic_AllThreeCloseUpAssets_Load()
        {
            Assert.IsNotNull(Resources.Load<Sprite>("Art/Gold/Western/Cinematic/Western_Sheriff_Closeup"), "Western_Sheriff_Closeup must load.");
            Assert.IsNotNull(Resources.Load<Sprite>("Art/Gold/Western/Cinematic/Western_OutlawA_Closeup"), "Western_OutlawA_Closeup must load.");
            Assert.IsNotNull(Resources.Load<Sprite>("Art/Gold/Western/Cinematic/Western_Sheriff_HandGun_Closeup"), "Western_Sheriff_HandGun_Closeup must load.");
        }

        /// <summary>C8.1d.3 replaced the old
        /// WesternShootout_SheriffPose_IdleDuringIntro_AimDuringDecision_RestoresAfterFire
        /// (which asserted Sheriff was visible/posed throughout Decision —
        /// exactly the presentation manual validation rejected) with a proof
        /// that Sheriff (an in-world actor at the time) stayed hidden
        /// throughout gameplay. C8.1d.4 removed that in-world Sheriff actor
        /// entirely, replacing his intro appearance with cinematic close-ups
        /// (see the dedicated WesternCinematic_* tests) — this test now
        /// proves the equivalent, updated contract: the cinematic overlay
        /// (CinematicRoot) never lingers into gameplay, and the outlaws are
        /// the ones visible, across both Decision and Feedback — using the
        /// same coarse CycleUntilArchetype pattern the rest of this file's
        /// non-timing-critical Western tests use, since by this point the
        /// round-1 intro has already completed (see AnswerCurrentMicrogame's
        /// budget comment).</summary>
        [UnityTest]
        public IEnumerator WesternShootout_CinematicRoot_NotVisibleDuringActiveGameplay_OutlawsAre()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.AimSelect);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            var cinematicRoot = FindRect("CinematicRoot");
            Assert.IsFalse(cinematicRoot.gameObject.activeInHierarchy, "The cinematic overlay must not be visible once round 1's intro has completed and Decision is under way.");
            Assert.IsTrue(FindButton("WesternTarget0").gameObject.activeInHierarchy, "The outlaws must be the visible gameplay composition once Decision is under way.");

            yield return AnswerCurrentMicrogame(answerCorrectly: true);

            Assert.IsFalse(cinematicRoot.gameObject.activeInHierarchy, "The cinematic overlay must remain hidden through Feedback and into the next round — it never reappears mid-gameplay.");
            Assert.IsTrue(FindButton("WesternTarget0").gameObject.activeInHierarchy, "The outlaws must remain the visible gameplay composition on round 2.");
        }

        [UnityTest]
        public IEnumerator WesternShootout_SelectedOutlawSwitchesToHit_OthersStayNeutral()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.AimSelect);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            var director = GetDirector();
            var options = director.CurrentClassification.CategoryOptions;
            var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);

            FindButton($"WesternTarget{correctIndex}").onClick.Invoke();

            // Short, precise wait (see the identical reasoning in
            // WesternShootout_SheriffPose_...RestoresAfterFire just above) —
            // long enough for the Hit sprite to land (~0.28s into Feedback),
            // short enough to stay inside this round's own dwell, before
            // round 2 of the same Encounter resets everyone back to Neutral.
            yield return new WaitForSeconds(0.5f);

            for (var i = 0; i < options.Length; i++)
            {
                var letter = (char)('A' + i);
                var neutralArt = Resources.Load<Sprite>($"Art/Gold/Western/Actors/Outlaw_{letter}_Neutral");
                var hitArt = Resources.Load<Sprite>($"Art/Gold/Western/Actors/Outlaw_{letter}_Hit");
                Assume.That(neutralArt, Is.Not.Null);
                Assume.That(hitArt, Is.Not.Null);

                var outlawSprite = FindImageUnder($"WesternTarget{i}", "Sprite");
                if (i == correctIndex)
                {
                    Assert.AreEqual(hitArt, outlawSprite.sprite, $"The selected (correct) outlaw {letter} must switch to its Hit sprite.");
                }
                else
                {
                    Assert.AreEqual(neutralArt, outlawSprite.sprite, $"Non-selected outlaw {letter} must remain on its Neutral sprite.");
                }
            }
        }

        [UnityTest]
        public IEnumerator WesternShootout_NextRoundWithinEncounter_RestoresEveryOutlawToNeutral()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.AimSelect);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            yield return AnswerCurrentMicrogame(answerCorrectly: true);

            // C8.1d.1: Western is one 3-round Encounter now — "the next
            // challenge" is the very next round of the SAME encounter, not
            // a later re-appearance (Western never reappears later in the
            // same session — see Docs/C8_1D_GOLD_ART_INTEGRATION.md,
            // "Western Encounter Presentation"). Hit art must not persist
            // into round 2.
            Assume.That(GetDirector().CurrentArchetype, Is.EqualTo(MicrogameArchetype.AimSelect));

            for (var i = 0; i < 4; i++)
            {
                var letter = (char)('A' + i);
                var neutralArt = Resources.Load<Sprite>($"Art/Gold/Western/Actors/Outlaw_{letter}_Neutral");
                Assume.That(neutralArt, Is.Not.Null);
                var outlawSprite = FindImageUnder($"WesternTarget{i}", "Sprite");
                Assert.AreEqual(neutralArt, outlawSprite.sprite, $"Outlaw {letter} must be restored to Neutral for round 2.");
            }
        }

        /// <summary>C8.1d.3: proves the off-screen shot rig (the brief's
        /// "OffscreenShotOrigin") actually fires — polls frame-by-frame
        /// (never a coarse wait; the tracer's own travel is only ~0.12s, and
        /// the C8.1d.1 tumbleweed investigation already proved a coarse wait
        /// can miss a window that short) from the moment the correct target
        /// is clicked, watching the Tracer object for at least one active
        /// frame.</summary>
        [UnityTest]
        public IEnumerator WesternShootout_OffscreenShotRig_TracerActivatesOnFire()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.AimSelect);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            var tracer = _root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Tracer" && HasAncestorNamed(t, "WesternShootout"));
            Assume.That(tracer, Is.Not.Null);

            var director = GetDirector();
            var options = director.CurrentClassification.CategoryOptions;
            var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
            FindButton($"WesternTarget{correctIndex}").onClick.Invoke();

            var tracerSeenActive = false;
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && !tracerSeenActive)
            {
                if (tracer.gameObject.activeSelf)
                {
                    tracerSeenActive = true;
                }

                yield return null;
            }

            Assert.IsTrue(tracerSeenActive, "The off-screen shot's tracer must become active at some point after firing.");
        }

        // --- C8.1d.1 Encounter/visual-correction tests. NOTE (manual
        // validation failure investigation): these four tests were designed
        // and described in the C8.1d.1 turn's own narration but the Edit
        // calls to actually insert them into this file were never made —
        // a real process gap this investigation caught by literally
        // re-reading the file rather than trusting the earlier report. They
        // are added for real here, driven through GameSessionInstaller
        // (which loads the REAL Resources/GameCatalog -> the REAL
        // ClasicoGameDefinition.asset, byte-for-byte the same resolution
        // 01_Shell/ShellInstaller uses — see GameSessionInstaller.Awake).

        [UnityTest]
        public IEnumerator WesternTarget_ButtonChrome_IsVisuallyTransparent()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.AimSelect);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            for (var i = 0; i < 4; i++)
            {
                var buttonImage = FindButton($"WesternTarget{i}").GetComponent<Image>();
                Assert.AreEqual(0f, buttonImage.color.a, 0.001f,
                    $"WesternTarget{i}'s own button background must be invisible — the outlaw sprite is the target, not a card behind it.");
            }
        }

        /// <summary>C8.1j.1 section 13: the Gold ConceptSign/OutlawNameplate
        /// art must actually be in active use (not silently falling back to
        /// the C8.1j procedural panel), every outlaw slot must share the
        /// exact same nameplate sprite, and neither decoration may block
        /// raycasts — the existing buttons/hit areas remain authoritative.</summary>
        [UnityTest]
        public IEnumerator Western_GoldSignage_UsesProductionSprites_SharedAcrossOutlaws_AndNeverBlocksRaycasts()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.AimSelect);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            var conceptSignImage = FindImageUnder("WesternShootout", "ConceptSign");
            Assert.IsNotNull(conceptSignImage.sprite, "ConceptSign must have a sprite assigned.");
            Assert.AreEqual("Western_ConceptSign", conceptSignImage.sprite.texture.name,
                "ConceptSign must use the production Gold sprite, not a fallback/placeholder.");
            Assert.IsFalse(conceptSignImage.raycastTarget, "ConceptSign art is decoration only and must never block raycasts.");

            Sprite firstNamePlateSprite = null;
            for (var i = 0; i < 4; i++)
            {
                var plateImage = FindImageUnder($"WesternTarget{i}", "NamePlate");
                Assert.IsNotNull(plateImage.sprite, $"WesternTarget{i}'s NamePlate must have a sprite assigned.");
                Assert.IsFalse(plateImage.raycastTarget, $"WesternTarget{i}'s NamePlate art is decoration only and must never block raycasts.");

                if (firstNamePlateSprite == null)
                {
                    firstNamePlateSprite = plateImage.sprite;
                    Assert.AreEqual("Western_OutlawNameplate", firstNamePlateSprite.texture.name,
                        "NamePlate must use the production Gold sprite, not a fallback/placeholder.");
                }
                else
                {
                    Assert.AreSame(firstNamePlateSprite, plateImage.sprite,
                        $"WesternTarget{i}'s NamePlate must use the exact same production sprite as every other outlaw slot.");
                }
            }
        }

        /// <summary>C8.1j.2: the Gold ConceptSign (backing sprite + its
        /// Concept text) must stay hidden before Western starts, from the
        /// first frame of round 1's cinematic intro through its close-ups,
        /// appear only together with the outlaws' own gameplay-visual reveal,
        /// carry the current challenge's text by Decision, and reset to
        /// hidden on abort — with no stale sign flashing on re-entry. The
        /// four outlaw nameplates are deliberately not asserted here.</summary>
        [UnityTest]
        public IEnumerator Western_ConceptSign_HiddenUntilGameplayReveal_ResetOnAbortAndReEntry()
        {
            var conceptSign = FindImageUnder("WesternShootout", "ConceptSign").gameObject;

            FindButton("Game_clasico").onClick.Invoke();
            yield return DriveToWesternRound1DecisionAssertingConceptSignGating(conceptSign, "first session");

            Assert.IsTrue(conceptSign.activeSelf, "ConceptSign must be visible once round 1's Decision begins.");
            Assert.AreEqual(GetDirector().CurrentClassification.ConceptLabel, FindText("Concept").text,
                "ConceptSign text must match the current challenge.");

            FindButton("AbortButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual(GameLifecycleState.Results, _installer.FlowController.State, "Aborting must still reach Results normally.");
            Assert.IsFalse(conceptSign.activeSelf, "ConceptSign must be reset to hidden by Hide()/abort.");

            FindButton("ExitButton").onClick.Invoke();
            yield return null;
            FindButton("Game_clasico").onClick.Invoke();
            yield return DriveToWesternRound1DecisionAssertingConceptSignGating(conceptSign, "re-entry session");

            Assert.IsTrue(conceptSign.activeSelf, "ConceptSign must be visible again once the re-entered session's round 1 Decision begins.");
            Assert.AreEqual(GetDirector().CurrentClassification.ConceptLabel, FindText("Concept").text,
                "Re-entered ConceptSign text must match the new session's current challenge, never a stale one.");
        }

        private IEnumerator DriveToWesternRound1DecisionAssertingConceptSignGating(GameObject conceptSign, string label)
        {
            var cinematicImage = FindImageUnder("CinematicRoot", "CinematicImage").gameObject;
            var observedIntro = false;
            var hiddenWhileCloseUpShown = false;
            var reachedDecision = false;

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && !reachedDecision)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWesternRound1 = director.CurrentArchetype == MicrogameArchetype.AimSelect && director.CurrentRoundWithinEncounter == 0;

                    if (!isWesternRound1)
                    {
                        Assert.IsFalse(conceptSign.activeSelf, $"{label}: ConceptSign must stay hidden before Western round 1 begins.");
                        if (director.IsDecisionPhase)
                        {
                            AnswerWhicheverPrecedesWestern(director);
                        }
                    }
                    else if (director.IsIntroPhase)
                    {
                        if (!observedIntro)
                        {
                            observedIntro = true;
                            Assert.IsFalse(conceptSign.activeSelf, $"{label}: ConceptSign must be hidden on the first frame after ShowChallenge (pre-cinematic).");
                        }

                        var anyOutlawActive = false;
                        for (var i = 0; i < 4; i++)
                        {
                            anyOutlawActive |= FindButton($"WesternTarget{i}").gameObject.activeSelf;
                        }

                        if (conceptSign.activeSelf)
                        {
                            Assert.IsTrue(anyOutlawActive, $"{label}: ConceptSign may only appear together with the outlaws' gameplay-visual reveal, never before it.");
                        }
                        else if (cinematicImage.activeInHierarchy)
                        {
                            hiddenWhileCloseUpShown = true;
                        }
                    }
                    else if (director.IsDecisionPhase && observedIntro)
                    {
                        reachedDecision = true;
                    }
                }

                yield return null;
            }

            Assert.IsTrue(observedIntro, $"{label}: never observed Western round 1's Intro phase.");
            Assert.IsTrue(hiddenWhileCloseUpShown, $"{label}: ConceptSign must stay hidden while the cinematic close-ups are on screen.");
            Assert.IsTrue(reachedDecision, $"{label}: Western round 1 never reached Decision within the time budget.");
        }

        [UnityTest]
        public IEnumerator WesternOutlaw_Sprite_IsSubstantiallyLargerThanThePreviousCardPresentation()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.AimSelect);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            var sprite = FindImageUnder("WesternTarget0", "Sprite");
            Assert.Greater(sprite.rectTransform.sizeDelta.y, 300f,
                "Outlaw sprites must be substantially larger than the earlier 220-unit-tall card-bound presentation.");
        }

        [UnityTest]
        public IEnumerator WesternEncounter_WorldStaysActive_AndIntroFlourishFiresOnlyOnRound1()
        {
            // Deliberately does NOT use CycleUntilArchetype/AnswerCurrentMicrogame
            // — their fixed multi-second waits can let a transient ~1.6s
            // animation (the tumbleweed) run start-to-finish *before* this
            // test starts watching, if Western isn't the very first
            // microgame. This drives frame-by-frame from the literal first
            // frame after launch so no window can be skipped over — this
            // gap is exactly what the first version of this test had, and
            // exactly why it reported a false "tumbleweed never fires"
            // (see Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Manual Validation
            // Failure" investigation).
            FindButton("Game_clasico").onClick.Invoke();

            var tumbleweed = _root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Tumbleweed" && HasAncestorNamed(t, "WesternShootout"));
            var westernRoot = FindRect("WesternShootout");
            var cinematicRoot = FindRect("CinematicRoot");

            var reachedWesternRound1 = false;
            var tumbleweedSeenDuringRound1 = false;
            var cinematicSeenDuringRound1 = false;
            var roundsPlayed = 0;
            var lastRoundKey = -1;
            var seenConcepts = new HashSet<string>();

            // Bounded by real wall-clock time, not a frame count: batch-mode
            // frames track real elapsed time roughly 1:1 (confirmed
            // empirically — an earlier 20000-frame budget covered only
            // ~2.6s of simulated time, nowhere near enough to clear even the
            // 3s countdown), so a frame-count budget would need to guess the
            // per-frame duration. 60 real seconds comfortably covers the
            // worst case (Western landing as late as microgame 9 of 9,
            // behind up to 8 other rounds, plus the 3s countdown).
            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && roundsPlayed < 3)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWestern = director.CurrentArchetype == MicrogameArchetype.AimSelect;

                    if (isWestern && director.CurrentRoundWithinEncounter == 0)
                    {
                        reachedWesternRound1 = true;
                    }

                    if (tumbleweed.gameObject.activeSelf)
                    {
                        if (isWestern && director.CurrentRoundWithinEncounter == 0)
                        {
                            tumbleweedSeenDuringRound1 = true;
                        }
                        else if (isWestern)
                        {
                            Assert.Fail($"The tumbleweed must not replay on a continuation round (roundWithinEncounter={director.CurrentRoundWithinEncounter}).");
                        }
                    }

                    // C8.1d.4: the cinematic overlay is intro-only — it must
                    // only ever be seen active during round 1
                    // (roundWithinEncounter 0), never on a continuation
                    // round (2/3).
                    if (cinematicRoot.gameObject.activeInHierarchy)
                    {
                        if (isWestern && director.CurrentRoundWithinEncounter == 0)
                        {
                            cinematicSeenDuringRound1 = true;
                        }
                        else if (isWestern)
                        {
                            Assert.Fail($"The cinematic overlay must not reappear on a continuation round (roundWithinEncounter={director.CurrentRoundWithinEncounter}) — it is intro-only.");
                        }
                    }

                    if (director.IsDecisionPhase)
                    {
                        if (isWestern)
                        {
                            Assert.IsTrue(westernRoot.gameObject.activeInHierarchy, "The Western world must be active while a Western round is in Decision.");
                            Assert.IsFalse(cinematicRoot.gameObject.activeInHierarchy, "The cinematic overlay must already be hidden by the time any Western round reaches Decision.");

                            var roundKey = director.CurrentRoundWithinEncounter;
                            if (roundKey != lastRoundKey)
                            {
                                lastRoundKey = roundKey;
                                seenConcepts.Add(FindText("Concept").text);
                                roundsPlayed++;

                                var options = director.CurrentClassification.CategoryOptions;
                                var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                                FindButton($"WesternTarget{correctIndex}").onClick.Invoke();
                            }
                        }
                        else if (reachedWesternRound1)
                        {
                            // A non-Western round appearing after Western's
                            // own block started would itself be a bug (only
                            // one contiguous block should exist) — but this
                            // test only needs to keep the session moving
                            // *before* Western appears, so just bail here
                            // rather than re-implementing every archetype's
                            // input for a state that must not occur anyway.
                            Assert.Fail("A non-Western round appeared while already inside the Western Encounter's own round count.");
                        }
                        else
                        {
                            AnswerWhicheverPrecedesWestern(director);
                        }
                    }
                }

                yield return null;
            }

            Assert.IsTrue(reachedWesternRound1, "Never reached round 1 of the Western Encounter within this session.");
            Assert.IsTrue(tumbleweedSeenDuringRound1, "The tumbleweed must become active at some point during round 1's Encounter intro.");
            Assert.IsTrue(cinematicSeenDuringRound1, "The cinematic overlay must become active at some point during round 1's Encounter intro.");
            Assert.AreEqual(3, roundsPlayed, "A Western Encounter must play exactly 3 consecutive rounds.");
            Assert.AreEqual(3, seenConcepts.Count, "Each of the 3 rounds must show a different accounting concept.");
        }

        /// <summary>Answers whatever non-Western archetype is currently
        /// showing (always correctly) — used only to keep a session moving
        /// while frame-polling toward Western, so a genuine bug can't be
        /// masked by re-deriving per-archetype input logic incorrectly.
        /// Idempotent per call site: the caller only invokes this once per
        /// newly-observed Decision phase (see the round-key gating above).</summary>
        private void AnswerWhicheverPrecedesWestern(ClasicoSessionDirector director)
        {
            switch (director.CurrentArchetype)
            {
                case MicrogameArchetype.ChooseSide:
                {
                    var correctIsTrue = director.CurrentTrueFalse.IsTrue;
                    FindButton(correctIsTrue ? "GameShowTrue" : "GameShowFalse").onClick.Invoke();
                    break;
                }

                case MicrogameArchetype.Balance:
                {
                    var challenge = director.CurrentDebitCredit;
                    var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
                    var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);
                    FindButton($"BalanceAccountOption{correctDebitIndex}").onClick.Invoke();
                    FindButton($"BalanceAccountOption{correctCreditIndex}").onClick.Invoke();
                    break;
                }

                default:
                {
                    FindButton($"DetectiveSuspect{director.CurrentErrorDetectionDisplayAnomalyIndex}").onClick.Invoke();
                    break;
                }
            }
        }

        /// <summary>C8.1d.2: the previous Encounter test only proved
        /// <c>TumbleweedRoutine</c> fired — insufficient, since manual
        /// validation showed the round was already fully "live" (concept
        /// shown, targets interactable) on the very same frame, which a
        /// fire-and-forget coroutine check can't catch. This samples every
        /// frame of round 1's Intro phase and asserts INPUT never leaks in
        /// early, then asserts it becomes available the instant Decision
        /// begins — i.e. Decision genuinely does not overrun the intro, in
        /// either direction.
        ///
        /// C8.1d.3 then extended this with the (since-superseded) large
        /// in-world Sheriff protagonist's own proofs. C8.1d.4 replaced that
        /// Sheriff-actor check with the cinematic overlay's equivalent, and
        /// at the time also required the outlaws/concept to stay hidden for
        /// the *entire* intro, not just non-interactable — C8.1d.12 now
        /// deliberately reverses that specific requirement: manual
        /// validation found the cinematic finishing well before the
        /// director's own Intro-&gt;Decision gate created a "~1 second empty
        /// Western background" dead gap (see
        /// <c>ClasicoGameDefinition.EncounterIntroSeconds</c>'s own
        /// doc-comment for the exact arithmetic), fixed by overlapping the
        /// outlaws' visual entrance with the cinematic's own flash/cut
        /// teardown near the very end of the intro. So this test now
        /// explicitly proves the NEW contract instead: outlaws/concept stay
        /// hidden through the bulk of the intro (the establishing shot and
        /// every push-in close-up), appear before the intro phase actually
        /// ends (proving the overlap really happens, not just eventually),
        /// there is never a frame where the cinematic is already gone AND
        /// the outlaws are not yet visible (the "no empty background gap"
        /// requirement itself), and — the one requirement that never
        /// changed — no WesternTarget is ever interactable until Decision
        /// begins, keeping visual reveal and input strictly separate.</summary>
        [UnityTest]
        public IEnumerator WesternEncounter_Round1Intro_OutlawsAppearBeforeIntroEnds_ButInputStaysGatedUntilDecision()
        {
            FindButton("Game_clasico").onClick.Invoke();

            var observedIntroFrame = false;
            var introStartTime = 0f;
            var conceptHiddenAtSomePoint = false;
            var outlawsHiddenAtSomePoint = false;
            var outlawsVisibleBeforeIntroEnded = false;
            var neverAnEmptyBackgroundFrame = true;
            var inputStayedDisabledDuringIntro = true;
            var cinematicSeenDuringIntro = false;
            var reachedDecisionForRound1 = false;
            var introDuration = 0f;

            var cinematicRoot = FindRect("CinematicRoot");

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && !reachedDecisionForRound1)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWesternRound1 = director.CurrentArchetype == MicrogameArchetype.AimSelect && director.CurrentRoundWithinEncounter == 0;

                    if (isWesternRound1 && director.IsIntroPhase)
                    {
                        if (!observedIntroFrame)
                        {
                            observedIntroFrame = true;
                            introStartTime = Time.realtimeSinceStartup;
                        }

                        if (string.IsNullOrEmpty(FindText("Concept").text))
                        {
                            conceptHiddenAtSomePoint = true;
                        }

                        var cinematicActive = cinematicRoot.gameObject.activeInHierarchy;
                        if (cinematicActive)
                        {
                            cinematicSeenDuringIntro = true;
                        }

                        var anyOutlawActive = false;
                        for (var i = 0; i < 4; i++)
                        {
                            var button = FindButton($"WesternTarget{i}");
                            if (button.gameObject.activeSelf)
                            {
                                anyOutlawActive = true;
                                outlawsVisibleBeforeIntroEnded = true;
                                if (button.interactable)
                                {
                                    inputStayedDisabledDuringIntro = false;
                                }
                            }
                        }

                        if (!anyOutlawActive)
                        {
                            outlawsHiddenAtSomePoint = true;
                        }

                        // The core "no dead gap" proof: at every sampled
                        // frame of the intro, either the cinematic is still
                        // covering the stage, or the outlaws are already
                        // visible — never neither.
                        if (!cinematicActive && !anyOutlawActive)
                        {
                            neverAnEmptyBackgroundFrame = false;
                        }
                    }
                    else if (isWesternRound1 && director.IsDecisionPhase && observedIntroFrame)
                    {
                        introDuration = Time.realtimeSinceStartup - introStartTime;
                        reachedDecisionForRound1 = true;
                    }
                    else if (director.IsDecisionPhase && director.CurrentArchetype != MicrogameArchetype.AimSelect)
                    {
                        AnswerWhicheverPrecedesWestern(director);
                    }
                }

                yield return null;
            }

            Assert.IsTrue(observedIntroFrame, "Never observed round 1 of the Western Encounter in its Intro phase.");
            Assert.IsTrue(reachedDecisionForRound1, "Round 1's Intro phase never transitioned into Decision within the time budget.");
            Assert.Greater(introDuration, 0f, "Round 1's Encounter intro must have a real, non-zero duration before Decision begins.");
            Assert.IsTrue(conceptHiddenAtSomePoint, "The accounting concept must still be hidden for at least part of the intro (the establishing shot/push-ins), not shown from frame one.");
            Assert.IsTrue(outlawsHiddenAtSomePoint, "The outlaws must still be hidden for at least part of the intro (the establishing shot/push-ins), not shown from frame one.");
            Assert.IsTrue(cinematicSeenDuringIntro, "The cinematic overlay must be seen active at some point during round 1's intro.");
            Assert.IsTrue(outlawsVisibleBeforeIntroEnded, "C8.1d.12: the outlaws must become visible before the Intro phase itself ends — overlapping the cinematic's own flash/cut teardown, not waiting for Decision to begin.");
            Assert.IsTrue(neverAnEmptyBackgroundFrame, "C8.1d.12: there must never be a sampled intro frame where the cinematic has already cleared AND the outlaws are not yet visible — that empty-background gap is exactly the manually-reported regression this test guards against.");
            Assert.IsTrue(inputStayedDisabledDuringIntro, "No WesternTarget button may be interactable while still in the Intro phase, even after the outlaws become visually appear — visual reveal and input-enable must stay strictly separate.");

            Assert.IsFalse(cinematicRoot.gameObject.activeInHierarchy, "The cinematic overlay must be hidden again the instant Decision begins — it is intro-only.");
            Assert.IsFalse(string.IsNullOrEmpty(FindText("Concept").text), "The concept must be visible the instant Decision begins for round 1.");
            var anyInteractable = false;
            for (var i = 0; i < 4; i++)
            {
                var button = FindButton($"WesternTarget{i}");
                if (button.gameObject.activeSelf && button.interactable)
                {
                    anyInteractable = true;
                }
            }

            Assert.IsTrue(anyInteractable, "At least one WesternTarget must be interactable the instant Decision begins for round 1.");
        }

        /// <summary>C8.1d.4: proves the actual cinematic montage plays, shot
        /// by shot, rather than just that "some overlay" is active (already
        /// covered by WesternEncounter_Round1Intro_OutlawsAppearBeforeIntroEnds_ButInputStaysGatedUntilDecision).
        /// Polls the CinematicImage's own sprite reference every frame
        /// (Resources.Load reference-equality, the same proven pattern the
        /// outlaw Hit/Neutral tests already use) and records which of the
        /// three close-ups were seen — never a coarse wait, since the
        /// C8.1d.1 tumbleweed investigation already proved those can produce
        /// false negatives on transient state. C8.1d.12 changed what this
        /// test expects of the labels: they used to stay hidden for the
        /// *entire* intro; now (per the "no empty background gap" fix —
        /// outlaws appear overlapping the cinematic's own teardown) they
        /// must stay hidden through the close-up portion but are allowed —
        /// expected — to appear before the intro phase itself ends. The
        /// reticle's own contract is unchanged: it stays hidden for the
        /// entire intro, since it is the input-enable half
        /// (<c>EnableOutlawInput</c>), never the visual-reveal half.</summary>
        [UnityTest]
        public IEnumerator WesternCinematic_AllThreeCloseUps_AppearDuringRound1Intro_ReticleStaysHiddenUntilDecision()
        {
            FindButton("Game_clasico").onClick.Invoke();

            var sheriffCloseup = Resources.Load<Sprite>("Art/Gold/Western/Cinematic/Western_Sheriff_Closeup");
            var outlawCloseup = Resources.Load<Sprite>("Art/Gold/Western/Cinematic/Western_OutlawA_Closeup");
            var handCloseup = Resources.Load<Sprite>("Art/Gold/Western/Cinematic/Western_Sheriff_HandGun_Closeup");
            Assume.That(sheriffCloseup, Is.Not.Null);
            Assume.That(outlawCloseup, Is.Not.Null);
            Assume.That(handCloseup, Is.Not.Null);

            var seenSheriff = false;
            var seenOutlaw = false;
            var seenHand = false;
            var labelsHiddenAtSomePoint = false;
            var reticleStayedHiddenDuringIntro = true;
            var reachedDecisionForRound1 = false;

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && !reachedDecisionForRound1)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWesternRound1 = director.CurrentArchetype == MicrogameArchetype.AimSelect && director.CurrentRoundWithinEncounter == 0;

                    if (isWesternRound1 && director.IsIntroPhase)
                    {
                        var image = FindImageUnder("CinematicRoot", "CinematicImage");
                        if (image.gameObject.activeSelf)
                        {
                            if (image.sprite == sheriffCloseup) seenSheriff = true;
                            else if (image.sprite == outlawCloseup) seenOutlaw = true;
                            else if (image.sprite == handCloseup) seenHand = true;
                        }

                        var anyLabelShown = false;
                        for (var i = 0; i < 4; i++)
                        {
                            var label = FindButton($"WesternTarget{i}").GetComponentInChildren<Text>(true);
                            if (label != null && !string.IsNullOrEmpty(label.text))
                            {
                                anyLabelShown = true;
                            }
                        }

                        if (!anyLabelShown)
                        {
                            labelsHiddenAtSomePoint = true;
                        }

                        if (FindRect("Reticle").gameObject.activeSelf)
                        {
                            reticleStayedHiddenDuringIntro = false;
                        }
                    }
                    else if (isWesternRound1 && director.IsDecisionPhase)
                    {
                        reachedDecisionForRound1 = true;
                    }
                    else if (director.IsDecisionPhase && director.CurrentArchetype != MicrogameArchetype.AimSelect)
                    {
                        AnswerWhicheverPrecedesWestern(director);
                    }
                }

                yield return null;
            }

            Assert.IsTrue(reachedDecisionForRound1, "Round 1's Intro phase never transitioned into Decision within the time budget.");
            Assert.IsTrue(seenSheriff, "The Sheriff close-up must appear at some point during round 1's cinematic intro.");
            Assert.IsTrue(seenOutlaw, "The Outlaw close-up must appear at some point during round 1's cinematic intro.");
            Assert.IsTrue(seenHand, "The Sheriff hand/holster close-up must appear at some point during round 1's cinematic intro.");
            Assert.IsTrue(labelsHiddenAtSomePoint, "No WesternTarget label may show text during the close-up portion of the cinematic intro (before the C8.1d.12 overlap begins).");
            Assert.IsTrue(reticleStayedHiddenDuringIntro, "The reticle must stay hidden for the entire intro — it is part of input-enable, never the visual reveal.");
        }

        /// <summary>C8.1d.4: proves the gunshot punctuation actually fires
        /// once near the end of the intro, and that gameplay reveals right
        /// after it — using the CinematicFlash's own alpha as the
        /// observable proxy for "the gunshot beat happened" (it is only
        /// ever touched by GunshotFlashRoutine, which CinematicIntroRoutine
        /// calls exactly once), rather than reaching into the audio system.</summary>
        [UnityTest]
        public IEnumerator WesternCinematic_GunshotFlash_FiresOnce_ThenGameplayReveals()
        {
            FindButton("Game_clasico").onClick.Invoke();

            var flashSeenCount = 0;
            var wasFlashVisible = false;
            var reachedDecisionForRound1 = false;

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && !reachedDecisionForRound1)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWesternRound1 = director.CurrentArchetype == MicrogameArchetype.AimSelect && director.CurrentRoundWithinEncounter == 0;

                    if (isWesternRound1 && director.IsIntroPhase)
                    {
                        var flash = FindImageUnder("CinematicRoot", "CinematicFlash");
                        var isVisible = flash.color.a > 0.05f;
                        if (isVisible && !wasFlashVisible)
                        {
                            flashSeenCount++;
                        }

                        wasFlashVisible = isVisible;
                    }
                    else if (isWesternRound1 && director.IsDecisionPhase)
                    {
                        reachedDecisionForRound1 = true;
                    }
                    else if (director.IsDecisionPhase && director.CurrentArchetype != MicrogameArchetype.AimSelect)
                    {
                        AnswerWhicheverPrecedesWestern(director);
                    }
                }

                yield return null;
            }

            Assert.IsTrue(reachedDecisionForRound1, "Round 1's Intro phase never transitioned into Decision within the time budget.");
            Assert.AreEqual(1, flashSeenCount, "The gunshot flash must become visible exactly once during round 1's cinematic intro.");

            // Gameplay reveal, immediately following.
            Assert.IsFalse(string.IsNullOrEmpty(FindText("Concept").text), "The concept must be visible immediately once Decision begins, right after the gunshot.");
            var anyOutlawVisible = false;
            for (var i = 0; i < 4; i++)
            {
                if (FindButton($"WesternTarget{i}").gameObject.activeInHierarchy)
                {
                    anyOutlawVisible = true;
                }
            }

            Assert.IsTrue(anyOutlawVisible, "At least one outlaw must be visible immediately after the cinematic gunshot.");
        }

        [UnityTest]
        public IEnumerator WesternCinematic_DuelMusicAsset_Loads()
        {
            var clip = Resources.Load<AudioClip>("Audio/Gold/Western/Western_DuelMusic_01");
            Assert.IsNotNull(clip, "The primary Western duel music candidate ('Dust & Silence') must be present under Resources/Audio/Gold/Western/Western_DuelMusic_01 (C8.1d.6).");
            yield break;
        }

        /// <summary>C8.1d.6: proves the real duel music track starts once on
        /// round 1 of a Western Encounter, plays on its own AudioSource
        /// independent of the sound-design/gunshot source, ducks toward
        /// near-silence through the tension-silence window, is stopped
        /// before Decision begins (gameplay input active), and never plays
        /// again on rounds 2/3 or leaks into any other round. State/trigger
        /// level checks only (<c>AudioSource.isPlaying</c>/<c>.volume</c>/
        /// <c>.clip</c> reference equality) — never waveform content, per
        /// the brief's own "avoid brittle sample-level audio assertions".
        /// Must drive frame-by-frame from session start, the same proven
        /// pattern <see cref="WesternCinematic_AllThreeCloseUps_AppearDuringRound1Intro_ReticleStaysHiddenUntilDecision"/>
        /// uses — the higher-level <see cref="LaunchClasico"/>/
        /// <see cref="AnswerCurrentMicrogame"/> helpers are state-driven to
        /// *wait past* the entire intro before returning, so they can never
        /// observe transient mid-intro audio state.</summary>
        [UnityTest]
        public IEnumerator WesternCinematic_DuelMusic_PlaysOnlyDuringRound1Intro_SeparateFromSfx_DucksThenStops()
        {
            var musicClip = Resources.Load<AudioClip>("Audio/Gold/Western/Western_DuelMusic_01");
            Assume.That(musicClip, Is.Not.Null);

            FindButton("Game_clasico").onClick.Invoke();

            AudioSource musicSource = null;
            AudioSource otherSource = null;
            var musicPlayingDuringRound1Intro = false;
            var musicPlayingOutsideRound1Intro = false;
            var otherSourcePlayingDuringRound1Intro = false;
            var sawNearBaseVolume = false;
            var sawDuckedVolume = false;
            var musicStoppedBeforeRound1Decision = true;
            var roundsPlayed = 0;
            var lastRoundKey = -1;

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && roundsPlayed < 3)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWestern = director.CurrentArchetype == MicrogameArchetype.AimSelect;
                    var isRound1Intro = isWestern && director.CurrentRoundWithinEncounter == 0 && director.IsIntroPhase;

                    if (musicSource == null || otherSource == null)
                    {
                        foreach (var source in _root.GetComponentsInChildren<AudioSource>(true))
                        {
                            if (source.clip == musicClip)
                            {
                                musicSource = source;
                            }
                            else if (source.isPlaying)
                            {
                                otherSource = source;
                            }
                        }
                    }

                    if (musicSource != null && musicSource.isPlaying)
                    {
                        if (isRound1Intro)
                        {
                            musicPlayingDuringRound1Intro = true;
                            if (musicSource.volume >= 0.40f)
                            {
                                sawNearBaseVolume = true;
                            }

                            if (musicSource.volume <= 0.30f)
                            {
                                sawDuckedVolume = true;
                            }
                        }
                        else
                        {
                            musicPlayingOutsideRound1Intro = true;
                        }
                    }

                    if (otherSource != null && otherSource.isPlaying && isRound1Intro)
                    {
                        otherSourcePlayingDuringRound1Intro = true;
                    }

                    if (isWestern && director.CurrentRoundWithinEncounter == 0 && director.IsDecisionPhase
                        && musicSource != null && musicSource.isPlaying)
                    {
                        musicStoppedBeforeRound1Decision = false;
                    }

                    if (director.IsDecisionPhase)
                    {
                        if (isWestern)
                        {
                            var roundKey = director.CurrentRoundWithinEncounter;
                            if (roundKey != lastRoundKey)
                            {
                                lastRoundKey = roundKey;
                                roundsPlayed++;
                                var options = director.CurrentClassification.CategoryOptions;
                                var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                                FindButton($"WesternTarget{correctIndex}").onClick.Invoke();
                            }
                        }
                        else
                        {
                            AnswerWhicheverPrecedesWestern(director);
                        }
                    }
                }

                yield return null;
            }

            Assert.AreEqual(3, roundsPlayed, "Must play all 3 rounds of the Western Encounter within the time budget.");
            Assert.IsNotNull(musicSource, "No AudioSource ever carried the duel music clip — music never started.");
            Assert.IsTrue(musicPlayingDuringRound1Intro, "Duel music must play at some point during round 1's cinematic intro.");
            Assert.IsFalse(musicPlayingOutsideRound1Intro, "Duel music must never play outside round 1's cinematic intro — no restart on round 2/3, no leak into gameplay.");
            Assert.IsTrue(musicStoppedBeforeRound1Decision, "Duel music must be stopped before round 1's Decision phase begins (gameplay input active).");
            Assert.IsNotNull(otherSource, "No separate sound-design AudioSource was ever observed playing — the SFX cues (wind/creak/pre-draw/gunshot) must fire on their own source.");
            Assert.AreNotSame(musicSource, otherSource, "Duel music and sound-design SFX must play on two independent AudioSources.");
            Assert.IsTrue(otherSourcePlayingDuringRound1Intro, "Sound-design cues (wind, at minimum) must still fire during round 1's cinematic intro.");
            Assert.IsTrue(sawNearBaseVolume, "Duel music must be observed near its base/moderate-low volume earlier in the intro.");
            Assert.IsTrue(sawDuckedVolume, "Duel music must be observed ducked toward near-silence later in the intro (the tension-silence window).");
        }

        /// <summary>C8.1p manual RC review: the visible Western "DISPARA"
        /// prompt was removed (it did not match the Gold presentation) and
        /// must not come back in any form — no presenter-owned cue object,
        /// and no visible text saying DISPARA anywhere (presenter or shared
        /// HUD) from round 1's cinematic through Decision, a shot, and the
        /// reveal. Everything the round relies on instead must still be
        /// there at the moment input goes live: the ConceptSign with the
        /// round's own label, and all four outlaw targets interactable with
        /// their nameplates. Western's answer window is unchanged (the
        /// shared 3.2s).</summary>
        [UnityTest]
        public IEnumerator Western_NoVisibleDisparaPrompt_ConceptSignAndTargetsLive_TimingUnchanged()
        {
            Assert.IsFalse(_root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "DisparaCue"),
                "The removed DisparaCue object must not exist at all.");

            FindButton("Game_clasico").onClick.Invoke();

            string VisibleDisparaText() => _root.GetComponentsInChildren<Text>(true)
                .Where(t => t.gameObject.activeInHierarchy && t.color.a > 0.05f && t.text != null
                            && t.text.ToUpperInvariant().Contains("DISPARA"))
                .Select(t => $"{t.name}='{t.text}'")
                .FirstOrDefault();

            string seenDispara = null;
            var reachedDecision = false;
            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && IsPlaying() && !reachedDecision)
            {
                if (!GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    if (director.CurrentArchetype == MicrogameArchetype.AimSelect)
                    {
                        seenDispara ??= VisibleDisparaText();
                        reachedDecision = director.CurrentRoundWithinEncounter == 0 && director.IsDecisionPhase;
                    }
                    else if (director.IsDecisionPhase)
                    {
                        AnswerWhicheverPrecedesWestern(director);
                    }
                }

                if (!reachedDecision)
                {
                    yield return null;
                }
            }

            Assert.IsTrue(reachedDecision, "Never reached Western round 1's Decision phase within 60s.");
            var decisionStart = Time.realtimeSinceStartup;
            Assert.IsNull(seenDispara, $"No DISPARA text may be visible during Western's intro/reveal (saw {seenDispara}).");

            var concept = FindTextUnder("WesternShootout", "Concept");
            Assert.IsTrue(FindImageUnder("WesternShootout", "ConceptSign").gameObject.activeInHierarchy, "The ConceptSign must be visible once input is live.");
            Assert.AreEqual(GetDirector().CurrentClassification.ConceptLabel, concept.text, "The ConceptSign must show this round's own label.");

            for (var i = 0; i < 4; i++)
            {
                var target = FindButton($"WesternTarget{i}");
                Assert.IsTrue(target.gameObject.activeInHierarchy && target.interactable, $"WesternTarget{i} must be live and interactable once input is live.");
                Assert.IsTrue(FindImageUnder($"WesternTarget{i}", "NamePlate").gameObject.activeInHierarchy, $"WesternTarget{i}'s nameplate must be visible.");
            }

            // Western's answer window is unchanged: let round 1 time out.
            var sawDisparaDuringDecision = false;
            while (IsPlaying() && GetDirector().IsDecisionPhase && Time.realtimeSinceStartup - decisionStart < 6f)
            {
                sawDisparaDuringDecision |= VisibleDisparaText() != null;
                yield return null;
            }

            var decisionLength = Time.realtimeSinceStartup - decisionStart;
            Assert.IsFalse(sawDisparaDuringDecision, "No DISPARA text may appear during Western's Decision phase.");
            Assert.GreaterOrEqual(decisionLength, 2.9f, "Western's decision window must still be ~3.2s.");
            Assert.LessOrEqual(decisionLength, 3.7f, "Western's decision window must still be ~3.2s.");

            // Through the countershot/reveal too.
            var revealEnd = Time.realtimeSinceStartup + 1.0f;
            while (Time.realtimeSinceStartup < revealEnd)
            {
                Assert.IsNull(VisibleDisparaText(), "No DISPARA text may appear during Western's reveal.");
                yield return null;
            }
        }

        /// <summary>C8.1d.7: proves the real gameplay firearm cue fires on
        /// every shot — for a correct answer, an incorrect answer, and
        /// again for a correct answer, across all 3 rounds of the Encounter
        /// — using the same "any non-music AudioSource becomes isPlaying"
        /// trigger-level proxy the C8.1d.6 music test established, never a
        /// waveform-level assertion. Also structurally proves the cue never
        /// depends on correctness ("do not use silence to indicate a wrong
        /// answer").</summary>
        [UnityTest]
        public IEnumerator WesternShootout_GameplayGunshot_FiresOnSfxSource_ForBothCorrectAndIncorrectShots()
        {
            var musicClip = Resources.Load<AudioClip>("Audio/Gold/Western/Western_DuelMusic_01");
            Assume.That(musicClip, Is.Not.Null);

            FindButton("Game_clasico").onClick.Invoke();

            var roundsPlayed = 0;
            var lastRoundKey = -1;
            var gunshotFiredThisRound = false;
            var gunshotResultsByRound = new List<bool>();
            var fireTime = -1f;
            var waitingForGunshot = false;

            var deadline = Time.realtimeSinceStartup + 60f;
            // Bounded by gunshotResultsByRound.Count, not roundsPlayed — the
            // 3rd round's own 0.5s post-shot confirmation window still needs
            // to run *after* roundsPlayed reaches 3, so gating the loop on
            // roundsPlayed alone would exit one iteration too early and
            // silently drop that round's result.
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && gunshotResultsByRound.Count < 3)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWestern = director.CurrentArchetype == MicrogameArchetype.AimSelect;

                    if (waitingForGunshot)
                    {
                        var anySfxPlaying = _root.GetComponentsInChildren<AudioSource>(true)
                            .Any(a => a.clip != musicClip && a.isPlaying);
                        if (anySfxPlaying)
                        {
                            gunshotFiredThisRound = true;
                        }

                        if (Time.realtimeSinceStartup - fireTime > 0.5f)
                        {
                            gunshotResultsByRound.Add(gunshotFiredThisRound);
                            waitingForGunshot = false;
                        }
                    }
                    else if (isWestern && director.IsDecisionPhase)
                    {
                        var roundKey = director.CurrentRoundWithinEncounter;
                        if (roundKey != lastRoundKey)
                        {
                            lastRoundKey = roundKey;
                            roundsPlayed++;

                            var options = director.CurrentClassification.CategoryOptions;
                            var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                            // Alternate correct/incorrect by round so both paths are proven to fire the shot.
                            var answerCorrectly = roundKey != 1;
                            var chosen = answerCorrectly ? correctIndex : (correctIndex + 1) % options.Length;

                            FindButton($"WesternTarget{chosen}").onClick.Invoke();
                            gunshotFiredThisRound = false;
                            fireTime = Time.realtimeSinceStartup;
                            waitingForGunshot = true;
                        }
                    }
                    else if (director.IsDecisionPhase)
                    {
                        AnswerWhicheverPrecedesWestern(director);
                    }
                }

                yield return null;
            }

            Assert.AreEqual(3, roundsPlayed, "Must play all 3 rounds of the Western Encounter within the time budget.");
            Assert.AreEqual(3, gunshotResultsByRound.Count, "Must observe a post-shot window for all 3 rounds.");
            Assert.IsTrue(gunshotResultsByRound[0], "A gunshot must fire on a correct answer (round 1).");
            Assert.IsTrue(gunshotResultsByRound[1], "A gunshot must fire on an incorrect answer (round 2) — silence must never indicate a wrong answer.");
            Assert.IsTrue(gunshotResultsByRound[2], "A gunshot must fire on a correct answer (round 3).");
        }

        /// <summary>C8.1d.8: coarse, non-brittle peak-amplitude reader for a
        /// procedurally-generated <see cref="AudioClip"/> — used only to
        /// prove one recorded cue is objectively quieter than another
        /// (e.g. "the pre-draw cue must stay under the gunshot"), never to
        /// assert exact waveform/pitch/timbre content.</summary>
        private static float PeakAmplitude(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            var peak = 0f;
            foreach (var sample in samples)
            {
                var abs = Mathf.Abs(sample);
                if (abs > peak)
                {
                    peak = abs;
                }
            }

            return peak;
        }

        /// <summary>C8.1d.8: proves the cinematic pre-draw cue fires before
        /// the cinematic gunshot, fires exactly once (no second impact-like
        /// event competing with the gunshot), and stays objectively quieter
        /// than the gunshot (peak amplitude) — the exact "extra sound
        /// immediately before the gunshot" manual bug report this phase
        /// fixes. Order/loudness only, via <see cref="WesternAudioEvents"/>
        /// — never a waveform/pitch assertion.</summary>
        [UnityTest]
        public IEnumerator WesternCinematic_PreDrawCue_FiresBeforeGunshot_QuieterThanGunshot_ExactlyOnce()
        {
            FindButton("Game_clasico").onClick.Invoke();

            var reachedDecisionForRound1 = false;
            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && !reachedDecisionForRound1)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWesternRound1 = director.CurrentArchetype == MicrogameArchetype.AimSelect && director.CurrentRoundWithinEncounter == 0;

                    if (isWesternRound1 && director.IsDecisionPhase)
                    {
                        reachedDecisionForRound1 = true;
                    }
                    else if (director.IsDecisionPhase && director.CurrentArchetype != MicrogameArchetype.AimSelect)
                    {
                        AnswerWhicheverPrecedesWestern(director);
                    }
                }

                yield return null;
            }

            Assert.IsTrue(reachedDecisionForRound1, "Round 1's Intro phase never transitioned into Decision within the time budget.");

            var preDrawEvents = WesternAudioEvents.Events.Where(e => e.Name == "CinematicPreDraw").ToList();
            var gunshotEvents = WesternAudioEvents.Events.Where(e => e.Name == "CinematicGunshot").ToList();

            Assert.AreEqual(1, preDrawEvents.Count, "Exactly one pre-draw cue must fire during the cinematic — a second one would read as a second impact-like event.");
            Assert.AreEqual(1, gunshotEvents.Count, "The cinematic gunshot must fire exactly once.");
            Assert.Less(preDrawEvents[0].Time, gunshotEvents[0].Time, "The pre-draw cue must fire before the cinematic gunshot.");

            var preDrawPeak = PeakAmplitude(preDrawEvents[0].Clip);
            var gunshotPeak = PeakAmplitude(gunshotEvents[0].Clip);
            Assert.Less(preDrawPeak, gunshotPeak, $"The pre-draw cue (peak={preDrawPeak}) must stay quieter than the cinematic gunshot (peak={gunshotPeak}) — it should sit under the near-silence window, not announce itself.");
        }

        /// <summary>C8.1d.8 originally also proved outcome feedback (a
        /// generic correct/incorrect HUD ding, deferred so it landed after
        /// impact) fired last in this sequence. C8.1d.9 removed that ding
        /// from Western entirely (see
        /// <see cref="WesternShootout_NoGenericFeedbackAudio_ButOtherArchetypesKeepTheirs"/>
        /// and <see cref="WesternShootout_Countershot_FiresOnlyOnWrongAnswer_WithEnemyGunshotAndRedFlash"/>
        /// for its replacement, the countershot) — those assertions were
        /// removed here rather than left checking a feature that no longer
        /// exists. C8.1d.10 then found the remaining impact accent itself
        /// was the manually-reported "unwanted extra TUM" on a correct hit
        /// and gated it to misses only. C8.1d.11's own second human manual
        /// pass then reported the *incorrect*-answer sequence itself as
        /// overcrowded ("TUM -> PSS -> TUM") — the impact accent's own 0.3s
        /// tail was still ringing (measurably, ~16% of peak amplitude) when
        /// the countershot's enemy gunshot fired ~0.18s after it, and the
        /// countershot alone already fully punctuates a miss. The accent is
        /// now removed from the gameplay fire sequence entirely (its clip
        /// is untouched and still used by the cinematic's own gunshot beat).
        /// What remains true for every fired shot: the gunshot clip itself
        /// never changes based on correctness, and it is never followed by
        /// any impact accent at all any more. Order only, via
        /// <see cref="WesternAudioEvents"/> — never a waveform assertion.</summary>
        [UnityTest]
        public IEnumerator WesternShootout_GameplayGunshot_NeverFollowedByAnImpactAccent_ForCorrectOrIncorrect()
        {
            FindButton("Game_clasico").onClick.Invoke();

            var roundsPlayed = 0;
            var lastRoundKey = -1;

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && roundsPlayed < 3)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWestern = director.CurrentArchetype == MicrogameArchetype.AimSelect;

                    if (isWestern && director.IsDecisionPhase)
                    {
                        var roundKey = director.CurrentRoundWithinEncounter;
                        if (roundKey != lastRoundKey)
                        {
                            lastRoundKey = roundKey;
                            roundsPlayed++;

                            var options = director.CurrentClassification.CategoryOptions;
                            var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                            // Alternate correct/incorrect by round so both paths are proven.
                            // roundKey is 0-indexed, so this makes the SECOND round
                            // (index 1) the only miss.
                            var answerCorrectly = roundKey != 1;
                            var chosen = answerCorrectly ? correctIndex : (correctIndex + 1) % options.Length;

                            FindButton($"WesternTarget{chosen}").onClick.Invoke();
                        }
                    }
                    else if (director.IsDecisionPhase)
                    {
                        AnswerWhicheverPrecedesWestern(director);
                    }
                }

                yield return null;
            }

            Assert.AreEqual(3, roundsPlayed, "Must play all 3 rounds of the Western Encounter within the time budget.");

            // Give the last round's own countershot beat a moment to land
            // before the log is inspected.
            yield return new WaitForSeconds(0.6f);

            var gunshotEvents = WesternAudioEvents.Events.Where(e => e.Name == "GameplayGunshot").ToList();
            var impactEvents = WesternAudioEvents.Events.Where(e => e.Name == "GameplayImpact").ToList();

            Assert.AreEqual(3, gunshotEvents.Count, "A gameplay gunshot must fire exactly once per round, hit or miss.");
            Assert.AreEqual(0, impactEvents.Count, "C8.1d.11: the impact accent must never fire from the gameplay fire sequence any more — for a hit OR a miss — it was redundant with the countershot and overlapped it, producing the manually-reported 'TUM -> PSS -> TUM' mud.");

            Assert.AreSame(gunshotEvents[0].Clip, gunshotEvents[1].Clip, "The gameplay gunshot clip must be identical for correct and incorrect shots.");
            Assert.AreSame(gunshotEvents[1].Clip, gunshotEvents[2].Clip, "The gameplay gunshot clip must be identical across all rounds regardless of correctness.");
        }

        /// <summary>C8.1d.10: proves SALIR (AbortButton) actually stops the
        /// Western cinematic in flight rather than merely hiding the visual
        /// root. Before this fix, <c>WesternShootoutPresenter.Hide()</c> only
        /// deactivated <c>_root</c> and stopped the music source — the whole
        /// <c>CinematicIntroRoutine</c> chain (~11 fire-and-forget coroutines
        /// living on the persistent <c>ClasicoHud</c> host, which outlives
        /// the presenter's own root being deactivated) kept running
        /// regardless, still firing wind/creak/pre-draw/gunshot/dust-accent
        /// on schedule on an SFX AudioSource <c>Hide()</c> never touched —
        /// manually reported as "dust-ball / intro FX can still play,
        /// cinematic gunshot can still fire after leaving." Aborts partway
        /// through the cinematic (past the wide establishing shot, well
        /// before the ~6.2s pre-draw/gunshot beat) so there is a real
        /// in-flight sequence to cancel, then waits well past where that
        /// beat would have landed and proves neither
        /// <see cref="WesternAudioEvents"/> event ever gets recorded.</summary>
        [UnityTest]
        public IEnumerator WesternShootout_Abort_DuringCinematic_StopsDelayedAudioAndNeverFiresTheCinematicGunshot()
        {
            FindButton("Game_clasico").onClick.Invoke();

            yield return DriveSessionTowardWesternUntil(
                () => _installer.FlowController.State == GameLifecycleState.Playing
                      && !GetDirector().IsCountingDown
                      && GetDirector().CurrentArchetype == MicrogameArchetype.AimSelect
                      && GetDirector().CurrentRoundWithinEncounter == 0
                      && GetDirector().IsIntroPhase,
                60f,
                "Never reached Western round 1's Intro phase (the cinematic) within 60s.");

            // Let the cinematic run partway — past the establishing shot,
            // well before the ~6.2s pre-draw/gunshot beat — so there is a
            // genuine in-flight sequence for Hide() to cancel.
            yield return new WaitForSeconds(1.5f);

            WesternAudioEvents.Clear();
            FindButton("AbortButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(GameLifecycleState.Results, _installer.FlowController.State, "Aborting must still reach Results normally.");

            // Wait well past the ~4.7s remaining in the cinematic's own
            // timeline (predraw at ~6.2s from cinematic start, so ~4.7s
            // from the abort point above) plus margin.
            yield return new WaitForSeconds(6.0f);

            var preDrawEvents = WesternAudioEvents.Events.Where(e => e.Name == "CinematicPreDraw").ToList();
            var gunshotEvents = WesternAudioEvents.Events.Where(e => e.Name == "CinematicGunshot").ToList();

            Assert.AreEqual(0, preDrawEvents.Count, "No cinematic pre-draw cue may fire after SALIR cancelled the cinematic mid-flight.");
            Assert.AreEqual(0, gunshotEvents.Count, "No cinematic gunshot may fire after SALIR cancelled the cinematic mid-flight — this is the exact 'cinematic gunshot can still fire after leaving' regression.");
        }

        /// <summary>C8.1d.10: proves the same cancellation holds when the
        /// player re-enters a fresh Clásico session immediately afterward —
        /// the generation bump must not leak into (or block) the next
        /// session's own new Western cinematic, and that new cinematic must
        /// run and reach its own pre-draw/gunshot normally.</summary>
        [UnityTest]
        public IEnumerator WesternShootout_AfterAbortDuringCinematic_ReEntryStartsACleanNewCinematic()
        {
            FindButton("Game_clasico").onClick.Invoke();

            yield return DriveSessionTowardWesternUntil(
                () => _installer.FlowController.State == GameLifecycleState.Playing
                      && !GetDirector().IsCountingDown
                      && GetDirector().CurrentArchetype == MicrogameArchetype.AimSelect
                      && GetDirector().CurrentRoundWithinEncounter == 0
                      && GetDirector().IsIntroPhase,
                60f,
                "Never reached Western round 1's Intro phase (the cinematic) within 60s, first session.");

            yield return new WaitForSeconds(1.0f);

            FindButton("AbortButton").onClick.Invoke();
            yield return null;
            FindButton("ExitButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(GameLifecycleState.Idle, _installer.FlowController.State, "Must return to Idle after exiting Results.");

            WesternAudioEvents.Clear();
            FindButton("Game_clasico").onClick.Invoke();

            yield return DriveSessionTowardWesternUntil(
                () => _installer.FlowController.State == GameLifecycleState.Playing
                      && !GetDirector().IsCountingDown
                      && GetDirector().CurrentArchetype == MicrogameArchetype.AimSelect
                      && GetDirector().CurrentRoundWithinEncounter == 0
                      && GetDirector().IsDecisionPhase,
                60f,
                "The re-launched session's own Western round 1 never reached Decision within 60s — a leaked cancellation would stall it.");

            var gunshotEvents = WesternAudioEvents.Events.Where(e => e.Name == "CinematicGunshot").ToList();
            Assert.AreEqual(1, gunshotEvents.Count, "The re-launched session's own cinematic must still reach and fire its cinematic gunshot exactly once — a clean, un-leaked new sequence.");
        }

        /// <summary>C8.1d.12: the sibling tests above abort early in the
        /// cinematic (~1s in), well before the new outlaw-settle overlap
        /// this phase introduces (which begins at the ~6.2s gunshot beat,
        /// via <c>RevealOutlawVisuals</c> called directly from
        /// <c>CinematicIntroRoutine</c>, and runs alongside the flash/cut
        /// teardown). This test specifically aborts AFTER that overlap has
        /// begun — once the cinematic gunshot has fired but before Decision
        /// (so outlaws are visible and mid-settle, exactly the new window —
        /// see the wait budget below) — to prove the same lifecycle
        /// cancellation contract still holds for this new code path: no
        /// audio leaks, the presenter hides completely, and a fresh
        /// re-launch's own round 1 reaches Decision normally with its
        /// outlaws visible and interactable and its own cinematic gunshot
        /// firing exactly once — never inheriting a half-settled outlaw or a
        /// stuck <c>_outlawVisualsRevealedForCurrentRound</c> flag from the
        /// aborted round.</summary>
        [UnityTest]
        public IEnumerator WesternShootout_Abort_DuringOutlawSettleOverlap_StopsCleanly_AndReEntryIsClean()
        {
            FindButton("Game_clasico").onClick.Invoke();

            // The cinematic gunshot (and this phase's new outlaw-settle
            // overlap) fires at ~6.2s into round 1's intro — wait for the
            // event itself rather than a fixed wall-clock guess, then give
            // the settle-in a brief real window to actually be mid-flight.
            // A generous budget, matching the other whole-session-scanning
            // Western tests' own 60s deadlines — Western's block can land
            // anywhere in a 9-round session, so this must cover up to 8
            // non-Western rounds' own full Intro/Decision/Lock/Feedback
            // cycles PLUS this round's own ~6.2s cinematic before the
            // gunshot fires, not just "reach Western's Intro phase" (the
            // sibling abort test's own, cheaper condition).
            yield return DriveSessionTowardWesternUntil(
                () => WesternAudioEvents.Events.Any(e => e.Name == "CinematicGunshot"),
                60f,
                "The cinematic gunshot never fired within 60s — never reached the outlaw-settle overlap window.");
            yield return new WaitForSeconds(0.1f);

            Assume.That(GetDirector().CurrentArchetype, Is.EqualTo(MicrogameArchetype.AimSelect));
            Assume.That(GetDirector().CurrentRoundWithinEncounter, Is.EqualTo(0));
            Assume.That(GetDirector().IsIntroPhase, Is.True, "Must still be in round 1's Intro phase — aborting mid-overlap, not after Decision already began.");

            WesternAudioEvents.Clear();
            FindButton("AbortButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(GameLifecycleState.Results, _installer.FlowController.State, "Aborting mid-overlap must still reach Results normally.");

            // Wait well past where the director's own Intro->Decision gate
            // would have fired for the aborted round, to prove nothing from
            // it leaks through.
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(0, WesternAudioEvents.Events.Count, "No further Western audio may fire after aborting mid-overlap — the settle-in itself is silent, but this also guards against any other leaked cue.");

            FindButton("ExitButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual(GameLifecycleState.Idle, _installer.FlowController.State, "Must return to Idle after exiting Results.");

            WesternAudioEvents.Clear();
            FindButton("Game_clasico").onClick.Invoke();

            yield return DriveSessionTowardWesternUntil(
                () => _installer.FlowController.State == GameLifecycleState.Playing
                      && !GetDirector().IsCountingDown
                      && GetDirector().CurrentArchetype == MicrogameArchetype.AimSelect
                      && GetDirector().CurrentRoundWithinEncounter == 0
                      && GetDirector().IsDecisionPhase,
                60f,
                "The re-launched session's own Western round 1 never reached Decision within 60s — a leaked overlap-window flag/coroutine would stall or corrupt it.");

            var gunshotEvents = WesternAudioEvents.Events.Where(e => e.Name == "CinematicGunshot").ToList();
            Assert.AreEqual(1, gunshotEvents.Count, "The re-launched session's own cinematic must reach and fire its cinematic gunshot exactly once.");

            var anyOutlawVisible = false;
            var anyInteractable = false;
            for (var i = 0; i < 4; i++)
            {
                var button = FindButton($"WesternTarget{i}");
                if (button.gameObject.activeSelf)
                {
                    anyOutlawVisible = true;
                    if (button.interactable)
                    {
                        anyInteractable = true;
                    }
                }
            }

            Assert.IsTrue(anyOutlawVisible, "The re-launched session's own round 1 must have visible outlaws once Decision begins.");
            Assert.IsTrue(anyInteractable, "The re-launched session's own round 1 must have at least one interactable target once Decision begins.");
        }

        /// <summary>C8.1n/C8.1p: every one of the 30 shipped Western
        /// ConceptLabels must render entirely inside the Gold ConceptSign
        /// once input is live, at 1280x720 and 1920x1080 (real
        /// CanvasScaler math). C8.1n's DISPARA-overlap half was retired with
        /// the cue itself (C8.1p); the label-containment half stays.</summary>
        [UnityTest]
        public IEnumerator Western_AllConceptLabels_RenderInsideTheConceptSign_AtBothResolutions()
        {
            FindButton("Game_clasico").onClick.Invoke();
            yield return DriveSessionTowardWesternUntil(
                () => IsPlaying() && !GetDirector().IsCountingDown
                      && GetDirector().CurrentArchetype == MicrogameArchetype.AimSelect && GetDirector().IsDecisionPhase,
                60f,
                "Never reached a Western Decision phase within 60s.");

            var concept = FindTextUnder("WesternShootout", "Concept");
            var sign = FindImageUnder("WesternShootout", "ConceptSign").rectTransform;
            Assert.IsTrue(sign.gameObject.activeInHierarchy, "The ConceptSign must be visible once Western input is live.");

            var canvas = concept.canvas.rootCanvas;
            var scaler = canvas.GetComponent<CanvasScaler>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            var originalConcept = concept.text;

            foreach (var resolution in new[] { new Vector2(1280f, 720f), new Vector2(1920f, 1080f) })
            {
                var scaleFactor = Mathf.Pow(2f, Mathf.Lerp(
                    Mathf.Log(resolution.x / scaler.referenceResolution.x, 2f),
                    Mathf.Log(resolution.y / scaler.referenceResolution.y, 2f),
                    scaler.matchWidthOrHeight));
                canvasRect.sizeDelta = resolution / scaleFactor;
                canvasRect.localScale = Vector3.one * scaleFactor;
                Canvas.ForceUpdateCanvases();
                yield return null;

                var label = $"{resolution.x}x{resolution.y}";
                var signBounds = WorldBounds(sign, 1f);
                foreach (var challenge in ClasicoMicrogameLibrary.ClassificationPool)
                {
                    concept.text = challenge.ConceptLabel;
                    Canvas.ForceUpdateCanvases();
                    var glyphs = ConceptGlyphWorldBounds(concept);
                    Assert.IsTrue(signBounds.Contains(glyphs.min) && signBounds.Contains(glyphs.max),
                        $"[{label}] '{challenge.ConceptLabel}' renders outside the ConceptSign ({glyphs} vs {signBounds}).");
                }
            }

            concept.text = originalConcept;
        }

        private static Rect WorldBounds(RectTransform rect, float scale)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bounds = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            var center = bounds.center;
            bounds.size *= scale;
            bounds.center = center;
            return bounds;
        }

        private static Rect ConceptGlyphWorldBounds(Text text)
        {
            var generator = text.cachedTextGenerator;
            var unitsPerPixel = 1f / text.pixelsPerUnit;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var vertex in generator.verts)
            {
                var world = text.rectTransform.TransformPoint(vertex.position * unitsPerPixel);
                min = Vector2.Min(min, world);
                max = Vector2.Max(max, world);
            }

            Assert.Less(min.x, max.x, $"'{text.text}' produced no glyphs to measure.");
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>C8.1d.9: proves Western's generic correct/incorrect HUD
        /// ding is gone (removed via <c>ClasicoHud.RenderFeedback</c>'s new
        /// <c>playAudio</c> opt-out) while every other archetype keeps its
        /// own ding exactly as before — never a global audio disable.
        /// <c>ClasicoHud</c>'s own <c>AudioSource</c> is identified
        /// structurally: it is the source that becomes <c>isPlaying</c>
        /// during a *non*-Western round's Feedback phase (Western's own two
        /// sources never fire while a different archetype is active), then
        /// that same source's activity is checked through every Western
        /// round's own Feedback phase and must stay silent throughout.
        /// Drives up to a full 9-round session (bounded by the usual 60s
        /// wall-clock deadline) since a session's non-Western rounds can
        /// land before or after Western's own 3-round block depending on
        /// the random draw.</summary>
        [UnityTest]
        public IEnumerator WesternShootout_NoGenericFeedbackAudio_ButOtherArchetypesKeepTheirs()
        {
            FindButton("Game_clasico").onClick.Invoke();

            AudioSource hudAudioSource = null;
            var hudPlayedForNonWestern = false;
            var hudPlayedDuringWestern = false;
            var westernRoundsSeen = 0;
            var lastWesternRoundKey = -1;
            var westernRoundsCompleted = 0;

            var deadline = Time.realtimeSinceStartup + 60f;
            // Bounded on *both* "all 3 Western rounds completed" and "a
            // non-Western round's feedback was observed" — Western's own
            // 3-round block can land anywhere in the 9-round session
            // (including first), so stopping the instant Western finishes
            // would sometimes never see a non-Western round at all,
            // depending purely on the random draw.
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results
                   && !(westernRoundsCompleted >= 3 && hudPlayedForNonWestern))
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWestern = director.CurrentArchetype == MicrogameArchetype.AimSelect;

                    // C8.1d.9: only sampled during IsFeedbackPhase — the
                    // exact (and only) moment ClasicoHud.RenderFeedback's
                    // own correct/incorrect PlayOneShot call happens.
                    // Sampling more broadly (e.g. anywhere "isWestern") was
                    // found to produce a false positive: ClasicoHud's own
                    // shared AudioSource *also* plays the unrelated
                    // encounter/world-transition-cut sting
                    // (ClasicoHud.PlayTransitionCut) right as Western's own
                    // block begins, which has nothing to do with the
                    // correct/incorrect ding this test is about.
                    if (director.IsFeedbackPhase)
                    {
                        if (hudAudioSource == null && !isWestern)
                        {
                            foreach (var source in _root.GetComponentsInChildren<AudioSource>(true))
                            {
                                if (source.isPlaying)
                                {
                                    hudAudioSource = source;
                                }
                            }
                        }

                        if (hudAudioSource != null && hudAudioSource.isPlaying)
                        {
                            if (isWestern)
                            {
                                hudPlayedDuringWestern = true;
                            }
                            else
                            {
                                hudPlayedForNonWestern = true;
                            }
                        }
                    }

                    if (director.IsDecisionPhase)
                    {
                        if (isWestern)
                        {
                            westernRoundsSeen++;
                            var roundKey = director.CurrentRoundWithinEncounter;
                            if (roundKey != lastWesternRoundKey)
                            {
                                lastWesternRoundKey = roundKey;
                                var options = director.CurrentClassification.CategoryOptions;
                                var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                                FindButton($"WesternTarget{correctIndex}").onClick.Invoke();
                            }
                        }
                        else
                        {
                            AnswerWhicheverPrecedesWestern(director);
                        }
                    }
                    else if (director.IsFeedbackPhase && isWestern && lastWesternRoundKey >= 0)
                    {
                        westernRoundsCompleted = Mathf.Max(westernRoundsCompleted, lastWesternRoundKey + 1);
                    }
                }

                yield return null;
            }

            Assert.IsNotNull(hudAudioSource, "Never observed ClasicoHud's own feedback AudioSource playing during a non-Western round — the test could not establish its fingerprint.");
            Assert.IsTrue(hudPlayedForNonWestern, "ClasicoHud's own correct/incorrect ding must still play for non-Western archetypes.");
            Assert.IsFalse(hudPlayedDuringWestern, "ClasicoHud's own correct/incorrect ding must never play during a Western round.");
        }

        /// <summary>C8.1d.11: <see cref="WesternShootout_NoGenericFeedbackAudio_ButOtherArchetypesKeepTheirs"/>
        /// above only ever answers Western CORRECTLY, so it never actually
        /// exercised the countershot/reveal window a second human manual
        /// pass specifically flagged as overcrowded ("TUM -> PSS -> TUM",
        /// with the final TUM landing "exactly when the UI reveals which
        /// answer was correct"). This test drives an INCORRECT Western round
        /// instead and reuses the same fingerprint-then-watch technique
        /// (identify ClasicoHud's own shared AudioSource during a
        /// *non*-Western round's Feedback phase first — Western's own
        /// sources are guaranteed silent there — then watch that exact
        /// source through the whole incorrect round's Feedback phase, which
        /// at the shared 0.8s non-final-round default comfortably contains
        /// the full gunshot -&gt; reveal -&gt; countershot -&gt; hit-flash
        /// sequence) to prove, empirically rather than by inspection, that
        /// no shared/duplicated audio fires at the reveal or anywhere else
        /// in that window. Also proves the Western-owned event list directly:
        /// exactly one player gunshot, zero impact accents (removed this
        /// same phase — see
        /// <see cref="WesternShootout_GameplayGunshot_NeverFollowedByAnImpactAccent_ForCorrectOrIncorrect"/>),
        /// and exactly one enemy-gunshot countershot after it — confirming
        /// the "final TUM" is <c>EnemyGunshot</c> itself: Western-owned, not
        /// a shared cue, and (per <see cref="CountershotRoutine"/>'s own
        /// doc-comment — it is the sole failure-punctuation cue since
        /// C8.1d.9 removed the old generic ding) not redundant, so it is
        /// kept rather than removed.</summary>
        [UnityTest]
        public IEnumerator WesternShootout_IncorrectAnswer_NoSharedRevealAudio_AndOnlyGunshotThenCountershot()
        {
            FindButton("Game_clasico").onClick.Invoke();

            // A session's non-Western rounds can land before OR after
            // Western's own 3-round block depending on the random draw, so
            // fingerprinting (which needs a non-Western round) and observing
            // the incorrect Western round can happen in either order —
            // recorded independently below and only cross-checked once both
            // are known, rather than assuming fingerprint-before-observe.
            AudioSource hudAudioSource = null;
            var hudPlayedForNonWestern = false;
            var playingSourcesDuringIncorrectWesternFeedback = new HashSet<AudioSource>();
            var reachedIncorrectWesternFeedback = false;
            var leftIncorrectWesternFeedback = false;
            var lastRoundKey = -1;
            var answeredWestern = false;
            // Snapshotted the instant we leave the incorrect round's own
            // Feedback phase — the outer loop may keep running afterward
            // purely to still find the HUD fingerprint (which can come from
            // a non-Western round either before or after Western's block),
            // and during that extra time the SAME Western Encounter's
            // remaining rounds (2/3) keep playing unanswered (this test only
            // ever clicks the one round it's testing) and time out, each
            // producing their own legitimate countershot/enemy-gunshot —
            // reading WesternAudioEvents.Events live at the very end would
            // wrongly count those too.
            List<WesternAudioEvents.Entry> eventsDuringIncorrectRound = null;

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results
                   && !(leftIncorrectWesternFeedback && hudAudioSource != null))
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWestern = director.CurrentArchetype == MicrogameArchetype.AimSelect;

                    if (!isWestern && director.IsFeedbackPhase)
                    {
                        hudPlayedForNonWestern = true;
                        if (hudAudioSource == null)
                        {
                            foreach (var source in _root.GetComponentsInChildren<AudioSource>(true))
                            {
                                if (source.isPlaying)
                                {
                                    hudAudioSource = source;
                                }
                            }
                        }
                    }

                    if (isWestern && director.IsDecisionPhase && !answeredWestern)
                    {
                        var roundKey = director.CurrentRoundWithinEncounter;
                        if (roundKey != lastRoundKey)
                        {
                            lastRoundKey = roundKey;
                            answeredWestern = true;

                            var options = director.CurrentClassification.CategoryOptions;
                            var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                            var wrong = (correctIndex + 1) % options.Length;
                            FindButton($"WesternTarget{wrong}").onClick.Invoke();
                        }
                    }
                    else if (!isWestern && director.IsDecisionPhase)
                    {
                        AnswerWhicheverPrecedesWestern(director);
                    }

                    if (answeredWestern && !leftIncorrectWesternFeedback && isWestern && director.IsFeedbackPhase)
                    {
                        reachedIncorrectWesternFeedback = true;
                        foreach (var source in _root.GetComponentsInChildren<AudioSource>(true))
                        {
                            if (source.isPlaying)
                            {
                                playingSourcesDuringIncorrectWesternFeedback.Add(source);
                            }
                        }
                    }
                    else if (reachedIncorrectWesternFeedback && !leftIncorrectWesternFeedback && !(isWestern && director.IsFeedbackPhase))
                    {
                        leftIncorrectWesternFeedback = true;
                        eventsDuringIncorrectRound = WesternAudioEvents.Events.ToList();
                    }
                }

                yield return null;
            }

            Assert.IsTrue(hudPlayedForNonWestern, "Never observed a non-Western Feedback phase — could not establish ClasicoHud's own AudioSource fingerprint.");
            Assert.IsNotNull(hudAudioSource, "Could not fingerprint ClasicoHud's own shared AudioSource.");
            Assert.IsTrue(reachedIncorrectWesternFeedback, "Never reached the incorrect Western round's Feedback phase within the time budget.");
            Assert.IsFalse(playingSourcesDuringIncorrectWesternFeedback.Contains(hudAudioSource), "No shared ClasicoHud audio may play at any point during an incorrect Western round's Feedback phase (gunshot -> reveal -> countershot -> hit-flash) — this is the manually-reported 'final TUM at the reveal' this test guards against.");
            Assert.IsNotNull(eventsDuringIncorrectRound, "Never captured an audio-event snapshot for the incorrect round's own Feedback phase.");

            var gunshotEvents = eventsDuringIncorrectRound.Where(e => e.Name == "GameplayGunshot").ToList();
            var impactEvents = eventsDuringIncorrectRound.Where(e => e.Name == "GameplayImpact").ToList();
            var enemyGunshotEvents = eventsDuringIncorrectRound.Where(e => e.Name == "EnemyGunshot").ToList();

            Assert.AreEqual(1, gunshotEvents.Count, "The player's own gunshot must fire exactly once.");
            Assert.AreEqual(0, impactEvents.Count, "No impact accent may fire on the gameplay fire sequence any more (C8.1d.11).");
            Assert.AreEqual(1, enemyGunshotEvents.Count, "The countershot's enemy gunshot must fire exactly once.");
            Assert.Less(gunshotEvents[0].Time, enemyGunshotEvents[0].Time, "The player's own gunshot must fire before the countershot's enemy gunshot.");
        }

        /// <summary>C8.1d.9: proves the countershot fires only on a wrong
        /// answer (never a correct one), in the right order (after the
        /// player's own gunshot), and drives the visible red "you got hit"
        /// flash — alternating correct/incorrect/correct across the 3-round
        /// Encounter, the same pattern C8.1d.7/.8's own gunshot tests use.
        /// Scoring deltas are checked alongside as a light structural proof
        /// correctness itself was never touched. C8.1d.10 first gated the
        /// impact accent to misses only, then C8.1d.11 removed it from the
        /// gameplay fire sequence entirely (see
        /// <see cref="WesternShootout_GameplayGunshot_NeverFollowedByAnImpactAccent_ForCorrectOrIncorrect"/>
        /// — its own 0.3s tail was still ringing when this very countershot
        /// fired, which is exactly the manually-reported "TUM -> PSS -> TUM"
        /// mud), so this test's own impact assertion now expects zero
        /// impacts across the whole Encounter rather than one.</summary>
        [UnityTest]
        public IEnumerator WesternShootout_Countershot_FiresOnlyOnWrongAnswer_WithEnemyGunshotAndRedFlash()
        {
            FindButton("Game_clasico").onClick.Invoke();

            var roundsPlayed = 0;
            var lastRoundKey = -1;
            var roundAnsweredCorrectly = new List<bool>();
            var roundSawRedFlash = new List<bool>();
            var watchingRoundIndex = -1;
            var watchDeadline = -1f;

            var flash = FindImageUnder("WesternShootout", "PlayerHitFlash");

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && roundsPlayed < 3)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWestern = director.CurrentArchetype == MicrogameArchetype.AimSelect;

                    if (watchingRoundIndex >= 0 && flash.color.a > 0.05f)
                    {
                        roundSawRedFlash[watchingRoundIndex] = true;
                    }

                    if (watchingRoundIndex >= 0 && Time.realtimeSinceStartup > watchDeadline)
                    {
                        watchingRoundIndex = -1;
                    }

                    if (isWestern && director.IsDecisionPhase)
                    {
                        var roundKey = director.CurrentRoundWithinEncounter;
                        if (roundKey != lastRoundKey)
                        {
                            lastRoundKey = roundKey;
                            roundsPlayed++;

                            var options = director.CurrentClassification.CategoryOptions;
                            var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                            var answerCorrectly = roundKey != 1;
                            var chosen = answerCorrectly ? correctIndex : (correctIndex + 1) % options.Length;
                            roundAnsweredCorrectly.Add(answerCorrectly);
                            roundSawRedFlash.Add(false);
                            watchingRoundIndex = roundsPlayed - 1;
                            watchDeadline = Time.realtimeSinceStartup + 0.9f;

                            FindButton($"WesternTarget{chosen}").onClick.Invoke();
                        }
                    }
                    else if (director.IsDecisionPhase)
                    {
                        AnswerWhicheverPrecedesWestern(director);
                    }
                }

                yield return null;
            }

            // Let the last round's watch window fully elapse before reading the log.
            yield return new WaitForSeconds(1.0f);

            Assert.AreEqual(3, roundsPlayed, "Must play all 3 rounds of the Western Encounter within the time budget.");

            var enemyGunshotEvents = WesternAudioEvents.Events.Where(e => e.Name == "EnemyGunshot").ToList();
            var impactEvents = WesternAudioEvents.Events.Where(e => e.Name == "GameplayImpact").ToList();

            for (var i = 0; i < 3; i++)
            {
                if (roundAnsweredCorrectly[i])
                {
                    Assert.IsFalse(roundSawRedFlash[i], $"Round {i + 1} (correct): the red player-hit flash must not appear.");
                }
                else
                {
                    Assert.IsTrue(roundSawRedFlash[i], $"Round {i + 1} (incorrect): the red player-hit flash must appear.");
                }
            }

            Assert.AreEqual(1, enemyGunshotEvents.Count, "Exactly one countershot (enemy gunshot) must fire across the Encounter — for the single incorrect round only.");
            Assert.AreEqual(0, impactEvents.Count, "C8.1d.11: the impact accent must never fire from the gameplay fire sequence any more — it was redundant with (and audibly overlapped) this very countershot.");
        }

        /// <summary>C8.1d.9 brief section 10: proves the preferred timeout
        /// behavior — no player gunshot is invented when the player never
        /// fires, but the correct outlaw still counters as failure
        /// punctuation. Lets round 1's own Decision window run out
        /// unanswered (bounded by <c>ClasicoGameDefinition.DecisionWindowSeconds</c>,
        /// 3.2s default, plus margin) rather than clicking a target.</summary>
        [UnityTest]
        public IEnumerator WesternShootout_Timeout_NoFakePlayerGunshot_ButCountershotStillFires()
        {
            FindButton("Game_clasico").onClick.Invoke();

            var reachedRound1Decision = false;
            var leftDecisionPhase = false;

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && !leftDecisionPhase)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWesternRound1 = director.CurrentArchetype == MicrogameArchetype.AimSelect && director.CurrentRoundWithinEncounter == 0;

                    if (isWesternRound1 && director.IsDecisionPhase)
                    {
                        reachedRound1Decision = true;
                        // Deliberately never click a target — let it time out.
                    }
                    else if (reachedRound1Decision && !director.IsDecisionPhase)
                    {
                        leftDecisionPhase = true;
                    }
                    else if (director.IsDecisionPhase && director.CurrentArchetype != MicrogameArchetype.AimSelect)
                    {
                        AnswerWhicheverPrecedesWestern(director);
                    }
                }

                yield return null;
            }

            Assert.IsTrue(reachedRound1Decision, "Never reached round 1 of the Western Encounter within the time budget.");
            Assert.IsTrue(leftDecisionPhase, "Round 1's Decision phase never ended (timeout never resolved) within the time budget.");

            // Give the countershot's own ~0.35-0.45s sequence time to land.
            yield return new WaitForSeconds(0.8f);

            var gunshotEvents = WesternAudioEvents.Events.Where(e => e.Name == "GameplayGunshot").ToList();
            var enemyGunshotEvents = WesternAudioEvents.Events.Where(e => e.Name == "EnemyGunshot").ToList();

            Assert.AreEqual(0, gunshotEvents.Count, "No player gunshot must be invented on a timeout — the player never fired.");
            Assert.AreEqual(1, enemyGunshotEvents.Count, "The correct outlaw must still counter-fire as failure punctuation on a timeout.");
        }

        [UnityTest]
        public IEnumerator WesternEncounter_EndsAfterThreeRounds_NextMicrogameIsADifferentArchetype()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.AimSelect);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            for (var i = 0; i < 3; i++)
            {
                Assume.That(GetDirector().CurrentArchetype, Is.EqualTo(MicrogameArchetype.AimSelect));
                yield return AnswerCurrentMicrogame(answerCorrectly: true);
            }

            if (_installer.FlowController.State == GameLifecycleState.Playing)
            {
                Assert.AreNotEqual(MicrogameArchetype.AimSelect, GetDirector().CurrentArchetype,
                    "After a Western Encounter's 3rd round, the next microgame must be a different archetype — only one Encounter block exists per session.");
            }
        }

        /// <summary>C8.1e Gold replacement — the presenter is now a real
        /// in-world actor standing on the illustrated stage (see
        /// Docs/C8_1E_GAME_SHOW_GOLD_REPLACEMENT.md), not the C8.1d
        /// portrait-card pattern this test used to check for. Renamed from
        /// <c>GameShow_ShowsIllustratedBackgroundAndPresentadorCard</c>.</summary>
        [UnityTest]
        public IEnumerator GameShow_ShowsInWorldPresenterOnTheGoldStage_NotTheOldPortraitCard()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.ChooseSide);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            var background = FindImageUnder("GameShow", "Background");
            Assert.IsNotNull(background.sprite, "Game Show background art should be assigned from Resources/Art/Gold/GameShow/GameShowBackground.");

            var presenterActor = FindImageUnder("GameShow", "PresenterActor");
            var expectedSprite = Resources.Load<Sprite>("Art/Gold/GameShow/Actors/Presentador_Gameplay_01");
            Assert.IsNotNull(expectedSprite, "Presentador_Gameplay_01 must resolve as a Resources-loadable sprite.");
            Assert.AreSame(expectedSprite, presenterActor.sprite, "PresenterActor must show the new Gold gameplay sprite.");

            // The retired C8.1d visual language must not exist anywhere
            // under the Game Show root anymore — not merely hidden.
            var retiredNames = new[] { "PresentadorCard", "ContestantHead", "PrizeBoard" };
            foreach (var name in retiredNames)
            {
                var stillPresent = _root.GetComponentsInChildren<Transform>(true)
                    .Any(t => t.name == name && HasAncestorNamed(t, "GameShow"));
                Assert.IsFalse(stillPresent, $"'{name}' must be fully retired from the Gold Game Show presentation, not just hidden.");
            }

            Assert.IsNotNull(FindRect("ChoiceLeft"), "A left stage-choice zone must exist.");
            Assert.IsNotNull(FindRect("ChoiceRight"), "A right stage-choice zone must exist.");
            Assert.IsTrue(FindButton("GameShowTrue").interactable, "GameShowTrue must be interactable once in Decision phase.");
            Assert.IsTrue(FindButton("GameShowFalse").interactable, "GameShowFalse must be interactable once in Decision phase.");

            var presenterRect = FindRect("PresenterActor");
            yield return AnswerCurrentMicrogame(answerCorrectly: true);
            Assert.Less(Vector2.Distance(presenterRect.localScale, Vector3.one), 0.05f,
                "The presenter actor must restore its resting scale after the correct-answer reaction punch.");
        }

        /// <summary>C8.1m test-setup hardening: the Western abort/re-entry
        /// tests used to wait PASSIVELY for Western's block, letting every
        /// earlier non-Western round run out its own decision timeout — with
        /// a random archetype order that made them order-dependent (a session
        /// opening with Detective/Balance/Game Show rounds could exhaust the
        /// budget before Western ever started; reproduced in a fresh isolated
        /// run). This keeps the session moving by answering any non-Western
        /// Decision (the same <see cref="AnswerWhicheverPrecedesWestern"/>
        /// the other Western tests use) until <paramref name="condition"/>
        /// holds. Western rounds are never touched, so the lifecycle under
        /// test is unchanged.</summary>
        private IEnumerator DriveSessionTowardWesternUntil(System.Func<bool> condition, float timeoutSeconds, string timeoutMessage)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    if (director.IsDecisionPhase && director.CurrentArchetype != MicrogameArchetype.AimSelect)
                    {
                        AnswerWhicheverPrecedesWestern(director);
                    }
                }

                yield return null;
            }

            Assert.IsTrue(condition(), timeoutMessage);
        }

        /// <summary>Answers whatever non-Game-Show archetype is currently
        /// showing (always correctly) — used only to keep a session moving
        /// while frame-polling toward a fresh Game Show round, mirroring
        /// <see cref="AnswerWhicheverPrecedesWestern"/>'s own established
        /// pattern for the identical problem.</summary>
        private void AnswerWhicheverPrecedesGameShow(ClasicoSessionDirector director)
        {
            switch (director.CurrentArchetype)
            {
                case MicrogameArchetype.AimSelect:
                {
                    var options = director.CurrentClassification.CategoryOptions;
                    var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                    FindButton($"WesternTarget{correctIndex}").onClick.Invoke();
                    break;
                }

                case MicrogameArchetype.Balance:
                {
                    var challenge = director.CurrentDebitCredit;
                    var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
                    var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);
                    FindButton($"BalanceAccountOption{correctDebitIndex}").onClick.Invoke();
                    FindButton($"BalanceAccountOption{correctCreditIndex}").onClick.Invoke();
                    break;
                }

                default:
                    FindButton($"DetectiveSuspect{director.CurrentErrorDetectionDisplayAnomalyIndex}").onClick.Invoke();
                    break;
            }
        }

        /// <summary>C8.1e: proves Game Show's new Gold intro (see
        /// GameShowPresenter.IntroRoutine/RevealAfterIntro) actually gates
        /// input — GameShowTrue/False must stay non-interactable for the
        /// entire real Intro phase, then become interactable exactly once
        /// Decision begins. Uses the same frame-polling-across-the-whole-
        /// session strategy (never a narrow race window) already
        /// established and proven non-flaky by
        /// <see cref="WesternEncounter_Round1Intro_OutlawsAppearBeforeIntroEnds_ButInputStaysGatedUntilDecision"/>,
        /// rather than trying to catch the short (~0.5-0.6s) window with a
        /// fixed wait.</summary>
        [UnityTest]
        public IEnumerator GameShow_IntroGatesInput_ThenDecisionPhaseRestoresInteraction()
        {
            FindButton("Game_clasico").onClick.Invoke();

            var observedIntroFrame = false;
            var inputStayedDisabledDuringIntro = true;
            var reachedDecisionForThisRound = false;

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results && !reachedDecisionForThisRound)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isGameShow = director.CurrentArchetype == MicrogameArchetype.ChooseSide;

                    if (isGameShow && director.IsIntroPhase)
                    {
                        observedIntroFrame = true;

                        if (FindButton("GameShowTrue").interactable || FindButton("GameShowFalse").interactable)
                        {
                            inputStayedDisabledDuringIntro = false;
                        }
                    }
                    else if (isGameShow && director.IsDecisionPhase && observedIntroFrame)
                    {
                        reachedDecisionForThisRound = true;
                    }
                    else if (director.IsDecisionPhase && director.CurrentArchetype != MicrogameArchetype.ChooseSide)
                    {
                        AnswerWhicheverPrecedesGameShow(director);
                    }
                }

                yield return null;
            }

            Assert.IsTrue(observedIntroFrame, "Never observed a Game Show round in its Intro phase within the time budget.");
            Assert.IsTrue(inputStayedDisabledDuringIntro, "GameShowTrue/False must stay non-interactable for the entire Intro phase.");
            Assert.IsTrue(reachedDecisionForThisRound, "Game Show's Intro phase never transitioned into Decision within the time budget.");
            Assert.IsTrue(FindButton("GameShowTrue").interactable, "GameShowTrue must be interactable once Decision phase begins.");
            Assert.IsTrue(FindButton("GameShowFalse").interactable, "GameShowFalse must be interactable once Decision phase begins.");
        }

        /// <summary>C8.1e: correct/incorrect visual states must actually
        /// differ (a real reveal, not a no-op) — checked via the winning/
        /// losing choice zone's own Glow overlay ending at a visibly
        /// different alpha, without over-specifying exact colors (that
        /// belongs to manual visual validation, not an automated test).
        /// Deliberately does NOT use <see cref="AnswerCurrentMicrogame"/> —
        /// that helper waits all the way into the *next* round, by which
        /// point GameShowPresenter.ShowChallenge has already reset both
        /// glows back to their rest alpha for the new round. This checks
        /// the state during *this* round's own Feedback phase instead,
        /// state-driven via the file's own <see cref="WaitUntil"/>.</summary>
        [UnityTest]
        public IEnumerator GameShow_CorrectAndIncorrectRevealsProduceDifferentZoneStates()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.ChooseSide);

            var correctIsTrue = GetDirector().CurrentTrueFalse.IsTrue;
            var winningGlow = FindImageUnder(correctIsTrue ? "ChoiceLeft" : "ChoiceRight", "Glow");
            var losingGlow = FindImageUnder(correctIsTrue ? "ChoiceRight" : "ChoiceLeft", "Glow");
            var restAlpha = winningGlow.color.a;

            FindButton(correctIsTrue ? "GameShowTrue" : "GameShowFalse").onClick.Invoke();
            // C8.1k: correctness is now revealed after a short suspense beat
            // (not on the first Feedback frame) — wait for the reveal itself.
            var correctTag = FindGameShowTmp(correctIsTrue ? "ChoiceLeft" : "ChoiceRight", "CorrectTag");
            yield return WaitUntil(() => correctTag.color.a > 0.5f, 5f, "Game Show never revealed the correct zone after answering.");

            Assert.Greater(winningGlow.color.a, restAlpha + 0.05f, "The correct zone's glow must visibly brighten on a correct reveal.");
            // Compared as full colors, not just alpha: Theme.Correct and
            // Theme.Incorrect are both fully-opaque (alpha 1) but different
            // hues, so an alpha-only comparison would false-positive-fail
            // here even though the two zones are genuinely showing
            // different colors.
            Assert.AreNotEqual(winningGlow.color, losingGlow.color, "Correct and incorrect zones must end up in visibly different states after a reveal.");
        }

        // --- C8.1k: Game Show showmanship pass ---

        private TMP_Text FindGameShowTmp(string ancestorName, string textName) =>
            _root.GetComponentsInChildren<TMP_Text>(true)
                .First(t => t.name == textName && HasAncestorNamed(t.transform, ancestorName) && HasAncestorNamed(t.transform, "GameShow"));

        private CanvasGroup FindGameShowGroup(string name) =>
            _root.GetComponentsInChildren<CanvasGroup>(true)
                .First(g => g.name == name && HasAncestorNamed(g.transform, "GameShow"));

        private AudioSource[] GameShowAudioSources() =>
            FindRect("GameShow").GetComponentsInChildren<AudioSource>(true);

        private bool AnyGameShowButtonInteractable() =>
            FindButton("GameShowTrue").interactable || FindButton("GameShowFalse").interactable;

        /// <summary>Frame-polls the session (answering every other
        /// archetype correctly) until a Game Show round is in its Intro
        /// phase — the preamble is only ~1.5s, so this never relies on a
        /// fixed wait to catch it.</summary>
        private IEnumerator DriveToGameShowIntro()
        {
            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    if (director.CurrentArchetype == MicrogameArchetype.ChooseSide && director.IsIntroPhase)
                    {
                        yield break;
                    }

                    if (director.IsDecisionPhase && director.CurrentArchetype != MicrogameArchetype.ChooseSide)
                    {
                        AnswerWhicheverPrecedesGameShow(director);
                    }
                }

                yield return null;
            }

            Assert.Fail("Never observed a Game Show round in its Intro phase within 60s.");
        }

        /// <summary>C8.1k.1: a GameShow_* hook plays only a real shipped
        /// clip under Resources/Audio/Gold/GameShow — never a synthesized
        /// fallback — so "should this hook have fired?" is exactly "does the
        /// asset exist?".</summary>
        private static bool GameShowClipShips(string hook) =>
            Resources.Load<AudioClip>($"Audio/Gold/GameShow/{hook}") != null;

        private static void AssertGameShowHookFiredOnlyIfClipShips(string hook)
        {
            var fired = GameShowAudioEvents.Events.Any(e => e.Name == hook);
            if (GameShowClipShips(hook))
            {
                Assert.IsTrue(fired, $"{hook} ships a real clip but never fired.");
            }
            else
            {
                Assert.IsFalse(fired, $"{hook} has no real clip — it must stay silent, not play a procedural fallback.");
            }
        }

        /// <summary>C8.1k.1 manual review: the temporary procedural Game Show
        /// audio was removed. Across a whole round (preamble, lock, reveal,
        /// explanation), no Game Show AudioSource may ever play a clip that
        /// is not a real shipped GameShow_* asset, and with no ambience asset
        /// the ambience source must never be playing.</summary>
        [UnityTest]
        public IEnumerator GameShow_MissingExternalClips_StaySilent_NoProceduralFallbackOrAmbience()
        {
            FindButton("Game_clasico").onClick.Invoke();
            yield return DriveToGameShowIntro();

            var shippedClips = new[]
                {
                    "GameShow_Opening", "GameShow_PrizeReveal", "GameShow_QuestionReveal", "GameShow_AnswerLock",
                    "GameShow_CorrectReveal", "GameShow_IncorrectReveal", "GameShow_AmbienceLoop",
                }
                .Select(h => Resources.Load<AudioClip>($"Audio/Gold/GameShow/{h}"))
                .Where(c => c != null)
                .ToList();
            var ambienceShips = GameShowClipShips("GameShow_AmbienceLoop");

            var answered = false;
            var sawReveal = false;
            var deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline && IsPlaying() && GetDirector().CurrentArchetype == MicrogameArchetype.ChooseSide)
            {
                var director = GetDirector();
                if (director.IsDecisionPhase && !answered)
                {
                    answered = true;
                    FindButton(director.CurrentTrueFalse.IsTrue ? "GameShowTrue" : "GameShowFalse").onClick.Invoke();
                }

                if (director.IsFeedbackPhase)
                {
                    sawReveal = true;
                }

                foreach (var source in GameShowAudioSources())
                {
                    if (source.clip != null)
                    {
                        Assert.IsTrue(shippedClips.Contains(source.clip), $"Game Show source is holding a non-shipped clip '{source.clip.name}'.");
                    }

                    if (source.loop && !ambienceShips)
                    {
                        Assert.IsFalse(source.isPlaying, "No ambience may play when GameShow_AmbienceLoop does not ship.");
                    }
                }

                yield return null;
            }

            Assert.IsTrue(answered && sawReveal, "The round never reached its reveal while being sampled.");
            Assert.IsTrue(GameShowAudioEvents.Events.All(e => shippedClips.Contains(e.Clip)),
                "Every fired GameShow_* hook must carry a real shipped clip.");
            if (shippedClips.Count == 0)
            {
                Assert.AreEqual(0, GameShowAudioEvents.Events.Count, "With no GameShow_* assets shipped, Game Show must be completely silent.");
            }
        }

        /// <summary>C8.1l user-test pacing: an unanswered Game Show round
        /// keeps its choices open for ~4.2s (was the shared 3.2s), and the
        /// explanation card, once fully visible, stays up ~1s longer than
        /// before (4.4s Feedback minus the unchanged 0.15s timeout breath,
        /// 0.35s explanation delay and 0.25s fade = ~3.65s; was ~2.65s).
        /// The 1.5s opening is covered by the preamble test and the ~0.4s
        /// lock suspense by the answer-lock test, both unchanged.</summary>
        [UnityTest]
        public IEnumerator GameShow_Pacing_DecisionOpenFor4_2s_ExplanationHeldAboutOneSecondLonger()
        {
            FindButton("Game_clasico").onClick.Invoke();
            yield return DriveToGameShowIntro();
            yield return WaitUntil(() => !IsPlaying() || GetDirector().IsDecisionPhase, 5f, "Game Show never reached Decision.");

            var decisionStart = Time.realtimeSinceStartup;
            yield return WaitUntil(() => !IsPlaying() || !GetDirector().IsDecisionPhase, 8f, "Game Show's decision window never timed out.");
            var decisionLength = Time.realtimeSinceStartup - decisionStart;
            Assert.GreaterOrEqual(decisionLength, 3.9f, "Game Show's decision window must stay open ~4.2s.");
            Assert.LessOrEqual(decisionLength, 4.7f, "Game Show's decision window must not exceed ~4.2s.");

            var explanation = FindGameShowGroup("ExplanationCard");
            yield return WaitUntil(() => !IsPlaying() || explanation.alpha > 0.99f, 3f, "The explanation card never became fully visible.");
            var visibleAt = Time.realtimeSinceStartup;
            yield return WaitUntil(() => !IsPlaying() || !GetDirector().IsFeedbackPhase, 8f, "Game Show's Feedback never ended.");
            var hold = Time.realtimeSinceStartup - visibleAt;
            Assert.GreaterOrEqual(hold, 3.3f, $"The explanation card must stay fully visible ~3.65s (was ~2.65s); held {hold:F2}s.");
            Assert.LessOrEqual(hold, 4.1f, $"The explanation hold must only grow by ~1s; held {hold:F2}s.");
        }

        /// <summary>Brief section 17: the preamble blocks input the whole
        /// time, the PREMIO plaque appears during it (before the statement),
        /// the statement is fully in place before Decision, the three
        /// preamble hooks fire in order, and the choices become interactive
        /// exactly at Decision.</summary>
        [UnityTest]
        public IEnumerator GameShow_Preamble_BlocksInput_RevealsPrizeThenStatement_BeforeDecision()
        {
            FindButton("Game_clasico").onClick.Invoke();
            yield return DriveToGameShowIntro();

            var prizeGroup = FindGameShowGroup("PrizePlaque");
            var statementGroup = FindGameShowGroup("StatementArea");
            var introStart = Time.realtimeSinceStartup;

            Assert.Less(statementGroup.alpha, 0.5f, "The statement must not already be fully shown on the preamble's first frame.");

            var inputStayedDisabled = true;
            var prizeShownAt = -1f;
            var statementShownAt = -1f;
            while (GetDirector().IsIntroPhase && Time.realtimeSinceStartup - introStart < 5f)
            {
                if (AnyGameShowButtonInteractable())
                {
                    inputStayedDisabled = false;
                }

                if (prizeShownAt < 0f && prizeGroup.alpha > 0.5f)
                {
                    prizeShownAt = Time.realtimeSinceStartup;
                }

                if (statementShownAt < 0f && statementGroup.alpha > 0.9f)
                {
                    statementShownAt = Time.realtimeSinceStartup;
                }

                yield return null;
            }

            var introDuration = Time.realtimeSinceStartup - introStart;

            Assert.IsTrue(GetDirector().IsDecisionPhase, "The preamble never handed over to Decision.");
            Assert.IsTrue(inputStayedDisabled, "VERDADERO/FALSO must stay non-interactable for the whole preamble.");
            Assert.GreaterOrEqual(introDuration, 1.1f, "The preamble should be a real ~1.2-1.8s opening, not the old 0.6s beat.");
            Assert.LessOrEqual(introDuration, 2.2f, "The preamble must stay short enough for Clásico.");
            Assert.Greater(prizeShownAt, 0f, "The PREMIO plaque must appear during the preamble.");
            Assert.Greater(statementShownAt, 0f, "The statement must be fully shown before Decision begins.");
            Assert.LessOrEqual(prizeShownAt, statementShownAt, "The prize reveal must come before the statement entrance.");

            Assert.AreEqual(1f, statementGroup.alpha, 0.001f, "The statement must be fully visible at Decision.");
            Assert.AreEqual(GetDirector().CurrentTrueFalse.Statement, FindTextUnder("GameShow", "Statement").text);
            StringAssert.StartsWith("PREGUNTA ", FindGameShowTmp("ProgressBadge", "ProgressTag").text);
            Assert.AreEqual("$1,000,000", FindGameShowTmp("PrizePlaque", "PrizeAmount").text);

            Assert.IsTrue(FindButton("GameShowTrue").interactable, "GameShowTrue must be interactable once Decision begins.");
            Assert.IsTrue(FindButton("GameShowFalse").interactable, "GameShowFalse must be interactable once Decision begins.");

            foreach (var hook in new[] { "GameShow_Opening", "GameShow_PrizeReveal", "GameShow_QuestionReveal", "GameShow_AmbienceLoop" })
            {
                AssertGameShowHookFiredOnlyIfClipShips(hook);
            }

            Assert.AreEqual(GameShowClipShips("GameShow_AmbienceLoop"), GameShowAudioSources().Any(s => s.loop && s.isPlaying),
                "The ambience loop may only play when a real GameShow_AmbienceLoop clip ships.");
        }

        /// <summary>Brief sections 7/8/17: the lock does not reveal
        /// correctness in the click frame; after a ~0.25-0.45s beat the
        /// correct zone is confirmed, the host celebrates, the correct cue
        /// (not the shared ding) fires, the explanation card shows the
        /// challenge's own FeedbackExplanation — and the host/confetti are
        /// back at rest once the round is over.</summary>
        [UnityTest]
        public IEnumerator GameShow_AnswerLock_HoldsSuspense_ThenCorrectRevealMarksCorrectZone_AndResets()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.ChooseSide);
            Assume.That(GetDirector().CurrentArchetype, Is.EqualTo(MicrogameArchetype.ChooseSide));

            var challenge = GetDirector().CurrentTrueFalse;
            var correctZoneName = challenge.IsTrue ? "ChoiceLeft" : "ChoiceRight";
            var correctTag = FindGameShowTmp(correctZoneName, "CorrectTag");
            var correctGlow = FindImageUnder(correctZoneName, "Glow");
            var explanationGroup = FindGameShowGroup("ExplanationCard");

            GameShowAudioEvents.Clear();
            var clickTime = Time.realtimeSinceStartup;
            FindButton(challenge.IsTrue ? "GameShowTrue" : "GameShowFalse").onClick.Invoke();

            Assert.IsFalse(AnyGameShowButtonInteractable(), "Both choices must lock the instant an answer is accepted.");
            Assert.AreEqual(0f, correctTag.color.a, 0.001f, "Correctness must not be revealed in the click frame.");
            AssertGameShowHookFiredOnlyIfClipShips("GameShow_AnswerLock");
            Assert.IsFalse(GameShowAudioEvents.Events.Any(e => e.Name == "GameShow_CorrectReveal"), "The reveal cue must not fire in the click frame.");

            yield return WaitUntil(() => correctTag.color.a > 0.5f, 5f, "The correct zone was never revealed.");
            var suspense = Time.realtimeSinceStartup - clickTime;
            Assert.GreaterOrEqual(suspense, 0.25f, "The answer-lock suspense beat must last at least ~0.25s.");
            Assert.LessOrEqual(suspense, 0.8f, "The answer-lock suspense beat must stay short.");

            Assert.Greater(correctGlow.color.g, correctGlow.color.r, "The correct zone must turn to the green/gold confirmation.");
            AssertGameShowHookFiredOnlyIfClipShips("GameShow_CorrectReveal");
            Assert.IsFalse(GameShowAudioEvents.Events.Any(e => e.Name == "GameShow_IncorrectReveal"));
            var confetti = _root.GetComponentsInChildren<Image>(true).Where(i => i.name == "Confetti" && HasAncestorNamed(i.transform, "GameShow")).ToList();

            yield return WaitUntil(() => explanationGroup.alpha > 0.95f, 3f, "The explanation card never appeared after the reveal.");
            Assert.IsTrue(GetDirector().IsFeedbackPhase, "The explanation must be shown within the round's own Feedback phase.");
            Assert.AreEqual("¡RESPUESTA CORRECTA!", FindGameShowTmp("ExplanationCard", "ExplanationVerdict").text);
            Assert.AreEqual(challenge.FeedbackExplanation, FindGameShowTmp("ExplanationCard", "GameShowExplanation").text);

            var presenterRect = FindRect("PresenterActor");
            yield return WaitUntil(() => !IsPlaying() || !GetDirector().IsFeedbackPhase, 6f, "Game Show's Feedback never ended.");
            Assert.Less(Vector3.Distance(presenterRect.localScale, Vector3.one), 0.01f, "The host's reaction scale must be back at rest after the round.");
            Assert.Less(Vector2.Distance(presenterRect.anchoredPosition, new Vector2(0f, 40f)), 0.01f, "The host must be back at rest position after the round.");
            Assert.IsTrue(confetti.All(c => c.color.a < 0.001f), "Confetti must be fully cleared after the round.");
        }

        /// <summary>Brief sections 9/17: a wrong answer mutes the chosen
        /// zone and unmistakably confirms the actual correct one.</summary>
        [UnityTest]
        public IEnumerator GameShow_IncorrectReveal_ExposesTheActualCorrectZone()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.ChooseSide);
            Assume.That(GetDirector().CurrentArchetype, Is.EqualTo(MicrogameArchetype.ChooseSide));

            var challenge = GetDirector().CurrentTrueFalse;
            var correctZoneName = challenge.IsTrue ? "ChoiceLeft" : "ChoiceRight";
            var wrongZoneName = challenge.IsTrue ? "ChoiceRight" : "ChoiceLeft";
            var correctTag = FindGameShowTmp(correctZoneName, "CorrectTag");

            GameShowAudioEvents.Clear();
            FindButton(challenge.IsTrue ? "GameShowFalse" : "GameShowTrue").onClick.Invoke();
            Assert.AreEqual(0f, correctTag.color.a, 0.001f, "Correctness must not be revealed in the click frame.");

            yield return WaitUntil(() => correctTag.color.a > 0.5f, 5f, "The actual correct zone was never revealed after a wrong answer.");

            Assert.AreEqual(0f, FindGameShowTmp(wrongZoneName, "CorrectTag").color.a, 0.001f, "The chosen wrong zone must not be tagged correct.");
            var correctGlow = FindImageUnder(correctZoneName, "Glow");
            var wrongGlow = FindImageUnder(wrongZoneName, "Glow");
            Assert.Greater(correctGlow.color.g, correctGlow.color.r, "The actual correct zone must get the green/gold confirmation.");
            Assert.Greater(wrongGlow.color.r, wrongGlow.color.g, "The chosen wrong zone must be muted red.");
            Assert.Greater(correctGlow.color.a, wrongGlow.color.a, "The correct zone must read stronger than the muted wrong one.");
            AssertGameShowHookFiredOnlyIfClipShips("GameShow_IncorrectReveal");
            Assert.IsFalse(GameShowAudioEvents.Events.Any(e => e.Name == "GameShow_CorrectReveal"));

            yield return WaitUntil(() => FindGameShowGroup("ExplanationCard").alpha > 0.95f, 3f, "The explanation card never appeared after a wrong answer.");
            Assert.AreEqual("RESPUESTA INCORRECTA", FindGameShowTmp("ExplanationCard", "ExplanationVerdict").text);

            var presenterRect = FindRect("PresenterActor");
            yield return WaitUntil(() => !IsPlaying() || !GetDirector().IsFeedbackPhase, 6f, "Game Show's Feedback never ended.");
            Assert.Less(Vector3.Distance(presenterRect.localScale, Vector3.one), 0.01f, "The host's deflate must recover to rest scale.");
            Assert.Less(Vector2.Distance(presenterRect.anchoredPosition, new Vector2(0f, 40f)), 0.01f, "The host's deflate must recover to rest position.");
        }

        /// <summary>Brief sections 15/17: aborting mid-reveal (explanation
        /// up, confetti/sweep possibly in flight) must stop every Game Show
        /// sound and clear every showmanship visual, nothing may fire
        /// afterward, and re-entry must start a clean preamble.</summary>
        [UnityTest]
        public IEnumerator GameShow_AbortMidReveal_StopsAudioAndClearsVisuals_ReEntryStartsClean()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.ChooseSide);
            Assume.That(GetDirector().CurrentArchetype, Is.EqualTo(MicrogameArchetype.ChooseSide));

            var challenge = GetDirector().CurrentTrueFalse;
            FindButton(challenge.IsTrue ? "GameShowTrue" : "GameShowFalse").onClick.Invoke();
            var correctTag = FindGameShowTmp(challenge.IsTrue ? "ChoiceLeft" : "ChoiceRight", "CorrectTag");
            yield return WaitUntil(() => correctTag.color.a > 0.5f, 5f, "The reveal never happened.");

            FindButton("AbortButton").onClick.Invoke();
            yield return null;
            GameShowAudioEvents.Clear();

            Assert.AreEqual(GameLifecycleState.Results, _installer.FlowController.State, "Aborting must still reach Results normally.");
            Assert.IsFalse(FindRect("GameShow").gameObject.activeSelf, "The Game Show stage must be hidden after abort.");
            Assert.IsTrue(GameShowAudioSources().All(s => !s.isPlaying), "Every Game Show audio source (cues and ambience) must stop on abort.");
            AssertGameShowShowmanshipCleared();

            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(0, GameShowAudioEvents.Events.Count, "No Game Show cue may fire after abort.");
            AssertGameShowShowmanshipCleared();

            FindButton("ExitButton").onClick.Invoke();
            yield return null;
            FindButton("Game_clasico").onClick.Invoke();
            yield return DriveToGameShowIntro();

            Assert.IsFalse(AnyGameShowButtonInteractable(), "Re-entry must start with input gated.");
            Assert.Less(FindGameShowGroup("StatementArea").alpha, 0.5f, "Re-entry must replay the preamble, not land on a static statement.");
            Assert.AreEqual(0f, FindGameShowGroup("ExplanationCard").alpha, 0.001f, "No stale explanation card on re-entry.");
            Assert.AreEqual(0f, FindGameShowTmp("ChoiceLeft", "CorrectTag").color.a, 0.001f, "No stale correct tag on re-entry.");
            Assert.AreEqual(0f, FindGameShowTmp("ChoiceRight", "CorrectTag").color.a, 0.001f, "No stale correct tag on re-entry.");
            Assert.AreEqual(Vector3.one, FindRect("ChoiceLeft").localScale, "No stale locked-zone scale on re-entry.");
            Assert.AreEqual(Vector3.one, FindRect("ChoiceRight").localScale, "No stale locked-zone scale on re-entry.");
            Assert.AreEqual("PREGUNTA 1", FindGameShowTmp("ProgressBadge", "ProgressTag").text, "The on-air question count must restart with a new session.");
        }

        /// <summary>Brief section 14 / C8.1k.1: the broadcast elements
        /// (prize plaque above the host, progress badge, explanation card,
        /// correct-answer tags) must not overlap the statement, the
        /// True/False zones, the host, or the shared HUD. The plaque sits
        /// above the host by design, so it is checked against the host's
        /// real HEAD (the sprite's opaque top) rather than the actor rect.
        /// Validated at 1280x720 and 1920x1080: the real CanvasScaler
        /// (ScaleWithScreenSize, match 0.5) math is applied to size the
        /// canvas in world space for each resolution.</summary>
        [UnityTest]
        public IEnumerator GameShow_ShowmanshipElements_DoNotOverlapStatementZonesPresenterOrHud()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.ChooseSide);
            Assume.That(GetDirector().CurrentArchetype, Is.EqualTo(MicrogameArchetype.ChooseSide));

            var canvas = FindRect("GameShow").GetComponentInParent<Canvas>().rootCanvas;
            var scaler = canvas.GetComponent<CanvasScaler>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;

            foreach (var resolution in new[] { new Vector2(1280f, 720f), new Vector2(1920f, 1080f) })
            {
                var logWidth = Mathf.Log(resolution.x / scaler.referenceResolution.x, 2f);
                var logHeight = Mathf.Log(resolution.y / scaler.referenceResolution.y, 2f);
                var scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
                canvasRect.sizeDelta = resolution / scaleFactor;
                canvasRect.localScale = Vector3.one * scaleFactor;
                Canvas.ForceUpdateCanvases();
                yield return null;

                AssertGameShowLayoutClear($"{resolution.x}x{resolution.y}");
            }
        }

        private void AssertGameShowLayoutClear(string label)
        {
            var gameShowRects = FindRect("GameShow").GetComponentsInChildren<RectTransform>(true);
            RectTransform GameShowRect(string name) => gameShowRects.First(r => r.name == name);

            var plaque = GameShowRect("PrizePlaque");
            var presenter = GameShowRect("PresenterActor");
            var statement = GameShowRect("StatementArea");

            var hud = new[] { FindRect("TimerBar"), FindRect("AbortButton"), FindRect("Progress"), FindRect("Score") };
            var zones = new[] { GameShowRect("ChoiceLeft"), GameShowRect("ChoiceRight") };

            var lateral = new[]
            {
                GameShowRect("ProgressBadge"),
                GameShowRect("ExplanationCard"),
                FindGameShowTmp("ChoiceLeft", "CorrectTag").rectTransform,
                FindGameShowTmp("ChoiceRight", "CorrectTag").rectTransform,
            };

            foreach (var element in lateral)
            {
                foreach (var other in hud.Concat(zones).Append(statement).Append(presenter).Append(plaque))
                {
                    Assert.IsFalse(WorldRectsOverlap(element, other), $"[{label}] Game Show '{element.name}' (under {element.parent.name}) overlaps '{other.name}'.");
                }
            }

            foreach (var other in hud.Concat(zones).Append(statement))
            {
                Assert.IsFalse(WorldRectsOverlap(plaque, other), $"[{label}] The prize plaque overlaps '{other.name}'.");
            }

            // The host's real head: the actor sprite is drawn preserveAspect
            // inside its rect, and the art has a 46/1232 transparent top
            // margin (measured from Presentador_Gameplay_01).
            var presenterImage = presenter.GetComponent<Image>();
            var presenterCorners = new Vector3[4];
            presenter.GetWorldCorners(presenterCorners);
            var rectHeight = presenterCorners[1].y - presenterCorners[0].y;
            var rectWidth = presenterCorners[2].x - presenterCorners[0].x;
            var spriteAspect = presenterImage.sprite.rect.width / presenterImage.sprite.rect.height;
            var drawnHeight = Mathf.Min(rectHeight, rectWidth / spriteAspect);
            var drawnBottom = presenterCorners[0].y + (rectHeight - drawnHeight) * 0.5f;
            var headTop = drawnBottom + drawnHeight * (1f - 46f / 1232f);

            var plaqueCorners = new Vector3[4];
            plaque.GetWorldCorners(plaqueCorners);
            var plaqueCenterX = (plaqueCorners[0].x + plaqueCorners[2].x) * 0.5f;
            var presenterCenterX = (presenterCorners[0].x + presenterCorners[2].x) * 0.5f;

            Assert.Greater(plaqueCorners[0].y, headTop, $"[{label}] The prize plaque must sit above the host's head, never covering it.");
            Assert.Less(Mathf.Abs(plaqueCenterX - presenterCenterX), 1f, $"[{label}] The prize plaque must be centred over the host.");
            Assert.Less(plaque.GetSiblingIndex(), presenter.GetSiblingIndex(), "The plaque must draw behind the host, so a reaction lift can never be covered by it.");
        }

        private static bool WorldRectsOverlap(RectTransform a, RectTransform b)
        {
            var cornersA = new Vector3[4];
            a.GetWorldCorners(cornersA);
            var cornersB = new Vector3[4];
            b.GetWorldCorners(cornersB);

            var overlapsHorizontally = cornersA[0].x < cornersB[2].x && cornersB[0].x < cornersA[2].x;
            var overlapsVertically = cornersA[0].y < cornersB[1].y && cornersB[0].y < cornersA[1].y;
            return overlapsHorizontally && overlapsVertically;
        }

        private void AssertGameShowShowmanshipCleared()
        {
            Assert.AreEqual(0f, FindGameShowGroup("ExplanationCard").alpha, 0.001f, "The explanation card must be cleared.");
            Assert.AreEqual(0f, FindGameShowGroup("PrizePlaque").alpha, 0.001f, "The prize plaque must be cleared.");
            Assert.AreEqual(0f, FindGameShowTmp("ChoiceLeft", "CorrectTag").color.a, 0.001f, "Result tags must be cleared.");
            Assert.AreEqual(0f, FindGameShowTmp("ChoiceRight", "CorrectTag").color.a, 0.001f, "Result tags must be cleared.");
            Assert.AreEqual(Vector3.one, FindRect("ChoiceLeft").localScale, "Locked-zone scale must be cleared.");
            Assert.AreEqual(Vector3.one, FindRect("ChoiceRight").localScale, "Locked-zone scale must be cleared.");
            Assert.AreEqual(0f, FindImageUnder("GameShow", "ResultFlash").color.a, 0.001f, "The result flash must be cleared.");
            Assert.IsTrue(_root.GetComponentsInChildren<Image>(true).Where(i => i.name == "Confetti" && HasAncestorNamed(i.transform, "GameShow")).All(c => c.color.a < 0.001f), "Confetti must be cleared.");
            var presenterRect = FindRect("PresenterActor");
            Assert.AreEqual(Vector3.one, presenterRect.localScale, "The host's reaction scale must be reset.");
        }

        [UnityTest]
        public IEnumerator DetectiveLineup_ShowsAuditorCard()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            var auditorPortrait = FindImageUnder("AuditorCard", "Portrait");
            Assert.IsNotNull(auditorPortrait.sprite, "Auditor portrait should be assigned from Resources/Art/Gold/Detective/Auditor.");

            var auditorFrame = FindRect("AuditorCard");
            yield return AnswerCurrentMicrogame(answerCorrectly: true);
            Assert.Less(Vector2.Distance(auditorFrame.localScale, Vector3.one), 0.05f,
                "Auditor's card must restore its resting scale after the correct-accusation punch.");
        }

        // --- C8.1g.2: Detective Lineup mechanic + experience redesign
        // (brief section 19). The runtime display-shuffle contract itself
        // (deterministic under a seeded GameContext, correct answer
        // survives shuffle, source Items[] never mutated, anomaly can land
        // in every slot) is proven at the ClasicoSessionDirector level in
        // ClasicoSessionDirectorTests.cs, where a seeded GameContext is
        // actually available — these PlayMode tests exercise the real UI
        // this session already established the pattern of for every other
        // archetype, reading whichever challenge/slot the RNG actually
        // drew rather than assuming one.

        /// <summary>Brief section 15: every decorative Detective element
        /// must be raycastTarget=false — correctness must not depend on
        /// sibling draw order. Checked by name against Detective's own
        /// root specifically (not the whole test root) so this can't
        /// accidentally match a same-named element on another
        /// presenter.</summary>
        [UnityTest]
        public IEnumerator Detective_DecorativeElements_NeverBlockRaycasts()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var detectiveRoot = FindRect("DetectiveLineup");
            // C8.1g.3: "Wall"/"HeightLine"/"AmbientGlow"/"Badge"/"Star" were
            // the C8.1g.2 procedural placeholders — retired now that the
            // approved Gold art is integrated (LineupRoom as the actual
            // backdrop). C8.1g.5 additionally retired "ImpostorRevealArt"
            // itself from this list — the nested reveal frame no longer
            // exists at all (see Detective_ImpostorRevealArt_IsFullyRetired_...
            // below). If the Gold art is ever missing, RuntimeUIFactory.LoadArt's
            // own warn-and-degrade contract brings the old procedural pieces
            // back — this test intentionally checks the current (art-present)
            // production path, same as every other Gold presenter's own tests.
            var decorativeNames = new[] { "LineupRoom", "Spotlight", "LightsOutOverlay" };

            foreach (var name in decorativeNames)
            {
                var images = detectiveRoot.GetComponentsInChildren<Image>(true).Where(img => img.gameObject.name == name).ToList();
                var texts = detectiveRoot.GetComponentsInChildren<Text>(true).Where(t => t.gameObject.name == name).ToList();
                Assert.IsTrue(images.Count > 0 || texts.Count > 0, $"Expected at least one '{name}' element under DetectiveLineup — the element name list is stale.");

                foreach (var img in images)
                {
                    Assert.IsFalse(img.raycastTarget, $"Decorative Detective element '{name}' must not block raycasts.");
                }

                foreach (var t in texts)
                {
                    Assert.IsFalse(t.raycastTarget, $"Decorative Detective element '{name}' (Text) must not block raycasts.");
                }
            }

            // The Auditor no longer has his own background frame Image
            // (brief section 6 retires the floating portrait card) — only
            // his "Portrait" art image exists, and it must not block clicks.
            var auditorPortraitImage = FindImageUnder("AuditorCard", "Portrait");
            Assert.IsFalse(auditorPortraitImage.raycastTarget, "Auditor's own portrait must not block raycasts.");

            // Contrast: the suspect buttons themselves must remain genuinely clickable.
            for (var i = 0; i < 4; i++)
            {
                var suspectImage = FindButton($"DetectiveSuspect{i}").GetComponent<Image>();
                Assert.IsTrue(suspectImage.raycastTarget, $"DetectiveSuspect{i} itself must remain clickable.");
            }
        }

        /// <summary>Brief section 14: the confirmed, previously-documented
        /// Spotlight lifecycle bug — after a decision timeout, the
        /// Spotlight's transform must be restored to its known resting
        /// values (captured once at Build time, never re-derived), not
        /// left wherever RenderDecision's per-frame follow last placed
        /// it.</summary>
        [UnityTest]
        public IEnumerator Detective_Timeout_RestoresSpotlightTransform()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            // Deliberately give no input — the decision window runs out on
            // its own (production DetectiveDecisionWindowSeconds, ~4.2s
            // since C8.1l).
            yield return WaitUntil(
                () => !IsPlaying() || GetDirector().IsFeedbackPhase,
                10f,
                "Detective's decision window never timed out into Feedback.");

            var spotlight = FindRect("Spotlight");
            Assert.AreEqual(Vector2.zero, spotlight.anchoredPosition,
                "Spotlight anchoredPosition must be restored to its resting (0,0) after a decision timeout — see the C8.1g.1 audit's documented Spotlight lifecycle bug.");
            Assert.AreEqual(Vector3.one, spotlight.localScale,
                "Spotlight scale must be restored to its resting (1,1,1) after a decision timeout.");
        }

        /// <summary>Brief section 14: Hide()/abort must restore the
        /// Spotlight immediately too, not only on a natural timeout — the
        /// default first-suspect selection (kept for controller
        /// accessibility) already moves it well off (0,0) by the time
        /// Decision is reached, so this is a real, non-vacuous check.</summary>
        [UnityTest]
        public IEnumerator Detective_Abort_DuringDecision_RestoresSpotlightTransformImmediately()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var spotlight = FindRect("Spotlight");
            yield return null;
            Assert.AreNotEqual(Vector2.zero, spotlight.anchoredPosition,
                "Test setup assumption failed — the Spotlight never actually moved off its rest position before Abort, so this test cannot prove anything about restoring it.");

            FindButton("AbortButton").onClick.Invoke();

            Assert.AreEqual(Vector2.zero, spotlight.anchoredPosition, "Hide() must immediately restore the Spotlight's resting anchoredPosition on abort.");
            Assert.AreEqual(Vector3.one, spotlight.localScale, "Hide() must immediately restore the Spotlight's resting scale on abort.");
        }

        // C8.1k.3: the Detective_SuspectDossier art's real writing area — the
        // light "paper" interior, measured from the 260x705 source PNG
        // (paper color ~(100,143,148)): x 39..236 px, rows 361..603 from the
        // top. As fractions of the dossier rect (y from the bottom):
        private const float DossierPaperXMin = 39f / 260f;
        private const float DossierPaperXMax = 237f / 260f;
        private const float DossierPaperYMin = 1f - 604f / 705f;
        private const float DossierPaperYMax = 1f - 361f / 705f;
        private const int DossierLabelMinFontSize = 9;
        private const int DossierLabelMaxFontSize = 18;

        /// <summary>C8.1k.3 manual review: "Préstamo bancario por pagar"
        /// ran onto the dossier's dark frame although the old fit test
        /// passed (it measured the label RectTransform, which was wider than
        /// the art's real paper). This audits the REAL runtime label: its
        /// rect must sit inside the measured paper interior, and every
        /// distinct shipped account name — sized exactly as the runtime
        /// sizes it (TwoLineTextFit) — must wrap into at most 2 lines with
        /// every glyph inside that rect, at a readable size, at both
        /// 1280x720 (canvas scale 1.0) and 1920x1080 (1.5). Logs a per-item
        /// size/lines/bounds table.</summary>
        [UnityTest]
        public IEnumerator Detective_AllAccountNames_FitTheDossierPaper_InTwoLines_AtBothResolutions()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);
            Assume.That(GetDirector().CurrentArchetype, Is.EqualTo(MicrogameArchetype.DetectError));
            Canvas.ForceUpdateCanvases();

            var dossier = FindRect("DetectiveSuspect0");
            var label = FindTextUnder("DetectiveSuspect0", "Label");
            var labelRect = label.rectTransform;

            // 1) The label box must lie inside the art's real paper area.
            var dossierSize = dossier.rect.size;
            var labelCorners = new Vector3[4];
            labelRect.GetWorldCorners(labelCorners);
            var min = (Vector2)dossier.InverseTransformPoint(labelCorners[0]) - dossier.rect.min;
            var max = (Vector2)dossier.InverseTransformPoint(labelCorners[2]) - dossier.rect.min;
            var labelNormMin = new Vector2(min.x / dossierSize.x, min.y / dossierSize.y);
            var labelNormMax = new Vector2(max.x / dossierSize.x, max.y / dossierSize.y);
            Assert.GreaterOrEqual(labelNormMin.x, DossierPaperXMin, $"Label box left edge ({labelNormMin.x:F3}) is on the dossier frame, not the paper ({DossierPaperXMin:F3}).");
            Assert.LessOrEqual(labelNormMax.x, DossierPaperXMax, $"Label box right edge ({labelNormMax.x:F3}) is on the dossier frame, not the paper ({DossierPaperXMax:F3}).");
            Assert.GreaterOrEqual(labelNormMin.y, DossierPaperYMin, "Label box bottom is below the paper area.");
            Assert.LessOrEqual(labelNormMax.y, DossierPaperYMax, "Label box top is above the paper area.");

            // 2) The live runtime labels for this round are already sized.
            for (var i = 0; i < 4; i++)
            {
                var live = FindTextUnder($"DetectiveSuspect{i}", "Label");
                if (!live.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Assert.LessOrEqual(live.cachedTextGenerator.lineCount, 2, $"Live dossier {i} ('{live.text}') renders on {live.cachedTextGenerator.lineCount} lines.");
                Assert.IsFalse(live.resizeTextForBestFit, "Dossier labels are sized by TwoLineTextFit, not Best Fit.");
            }

            // 3) Every distinct shipped account name, at both resolutions.
            var items = ClasicoMicrogameLibrary.ErrorDetectionPool.SelectMany(c => c.Items).Distinct().ToList();
            // C8.1n: the manually-reported name now ships as "Préstamo
            // bancario"; the old, longer phrase is still measured below as a
            // stress case so this audit never gets weaker.
            Assert.Contains("Préstamo bancario", items, "The manually-reported account (now 'Préstamo bancario') must be part of the audit.");

            var box = labelRect.rect;
            var report = new System.Text.StringBuilder($"C8.1k.3 dossier label audit — box {box.width:F1}x{box.height:F1} (canvas units), {items.Count} distinct names\n");
            var smallest = int.MaxValue;
            foreach (var scale in new[] { 1f, 1.5f })
            {
                foreach (var item in items.OrderByDescending(x => x.Length))
                {
                    var size = TwoLineTextFit.ResolveFontSize(label, item, DossierLabelMaxFontSize, DossierLabelMinFontSize, 2, scale);
                    var m = TwoLineTextFit.Measure(label, item, size, scale);
                    report.AppendLine($"  [{(scale > 1f ? "1920x1080" : "1280x720")}] {size,2}pt  {m.LineCount} line(s)  glyphs x {m.GlyphBounds.xMin:F1}..{m.GlyphBounds.xMax:F1} y {m.GlyphBounds.yMin:F1}..{m.GlyphBounds.yMax:F1}  h {m.PreferredHeight:F1}  '{item}'");

                    Assert.IsTrue(m.FitsIn(box, 2),
                        $"[scale {scale}] '{item}' does not fit the dossier paper in <=2 lines at {size}pt: lines={m.LineCount}, glyphs={m.GlyphBounds}, height={m.PreferredHeight:F1}, box={box}.");
                    Assert.Greater(size, DossierLabelMinFontSize - 1, $"'{item}' fell below the {DossierLabelMinFontSize}pt floor.");
                    smallest = Mathf.Min(smallest, size);
                }
            }

            foreach (var stress in new[] { "Préstamo bancario", "Préstamo bancario por pagar", "Papelería comprada por adelantado para uso futuro" })
            {
                var stressSize = TwoLineTextFit.ResolveFontSize(label, stress, DossierLabelMaxFontSize, DossierLabelMinFontSize, 2, 1f);
                Assert.IsTrue(TwoLineTextFit.Measure(label, stress, stressSize, 1f).FitsIn(box, 2),
                    $"'{stress}' must fit the dossier paper completely.");
            }
            Assert.AreEqual(DossierLabelMaxFontSize, TwoLineTextFit.ResolveFontSize(label, "Caja", DossierLabelMaxFontSize, DossierLabelMinFontSize, 2, 1f),
                "Short names like 'Caja' must stay at the full size.");

            report.AppendLine($"  smallest resolved size: {smallest}pt");
            Debug.Log(report.ToString());
        }

        /// <summary>C8.1l user-test pacing: an unanswered Detective round
        /// keeps the dossiers open for ~4.2s (was the shared 3.2s), and the
        /// teaching recap, once shown, stays fully visible ~1s longer (4.2s
        /// Feedback minus the unchanged ~0.6s timeout lights-out reveal =
        /// ~3.6s; was ~2.6s).</summary>
        [UnityTest]
        public IEnumerator Detective_Pacing_DecisionOpenFor4_2s_RecapHeldAboutOneSecondLonger()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);
            Assume.That(GetDirector().CurrentArchetype, Is.EqualTo(MicrogameArchetype.DetectError));
            Assume.That(GetDirector().IsDecisionPhase, Is.True);

            var decisionStart = Time.realtimeSinceStartup;
            yield return WaitUntil(() => !IsPlaying() || !GetDirector().IsDecisionPhase, 8f, "Detective's decision window never timed out.");
            var decisionLength = Time.realtimeSinceStartup - decisionStart;
            Assert.GreaterOrEqual(decisionLength, 3.9f, "Detective's decision window must stay open ~4.2s.");
            Assert.LessOrEqual(decisionLength, 4.7f, "Detective's decision window must not exceed ~4.2s.");

            var recapWhy = FindText("RecapWhy");
            yield return WaitUntil(() => !IsPlaying() || (!string.IsNullOrEmpty(recapWhy.text) && recapWhy.gameObject.activeInHierarchy), 3f, "The teaching recap never appeared.");
            var visibleAt = Time.realtimeSinceStartup;
            yield return WaitUntil(() => !IsPlaying() || !GetDirector().IsFeedbackPhase, 8f, "Detective's Feedback never ended.");
            var hold = Time.realtimeSinceStartup - visibleAt;
            Assert.GreaterOrEqual(hold, 3.3f, $"The recap must stay fully visible ~3.6s (was ~2.6s); held {hold:F2}s.");
            Assert.LessOrEqual(hold, 4.1f, $"The recap hold must only grow by ~1s; held {hold:F2}s.");
        }

        /// <summary>C8.1k.1 manual review: the shared ClasicoHud
        /// "¡Correcto!/Incorrecto" banner collided with the Detective scene's
        /// header. Detective's own IMPOSTOR tag + teaching recap carry the
        /// verdict, so the shared banner must stay empty for Detective (for
        /// both a correct and an incorrect accusation) while score/streak
        /// still update and the proprietary reveal still appears.</summary>
        [UnityTest]
        public IEnumerator Detective_SharedFeedbackBanner_IsSuppressed_ProprietaryRevealStillShows()
        {
            yield return LaunchClasico();

            var banner = _root.GetComponentsInChildren<Text>(true)
                .First(t => t.name == "Feedback" && HasAncestorNamed(t.transform, "PlayingPanel") && !HasAncestorNamed(t.transform, "StageRoot"));
            var scoreText = FindText("Score");

            foreach (var answerCorrectly in new[] { true, false })
            {
                yield return CycleUntilArchetype(MicrogameArchetype.DetectError);
                if (!IsPlaying() || GetDirector().CurrentArchetype != MicrogameArchetype.DetectError)
                {
                    Assert.Inconclusive("The session ended before a second Detective round could be sampled.");
                }

                var director = GetDirector();
                var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;
                var chosen = answerCorrectly ? displayAnomalyIndex : (displayAnomalyIndex + 1) % director.CurrentErrorDetection.Items.Length;
                var scoreBefore = scoreText.text;

                FindButton($"DetectiveSuspect{chosen}").onClick.Invoke();

                var impostorTag = FindTextUnder($"DetectiveSuspect{displayAnomalyIndex}", "ImpostorTag");
                var recapWhy = FindText("RecapWhy");
                var sawImpostor = false;
                var sawRecap = false;
                while (IsPlaying() && GetDirector().CurrentArchetype == MicrogameArchetype.DetectError && !GetDirector().IsDecisionPhase)
                {
                    Assert.IsTrue(string.IsNullOrEmpty(banner.text), $"The shared HUD banner must stay empty for Detective (showed '{banner.text}').");
                    sawImpostor |= impostorTag.color.a > 0.9f;
                    sawRecap |= !string.IsNullOrEmpty(recapWhy.text);
                    if (sawImpostor && sawRecap && GetDirector().IsFeedbackPhase)
                    {
                        break;
                    }

                    yield return null;
                }

                Assert.IsTrue(sawImpostor, $"The IMPOSTOR tag must still appear (answerCorrectly={answerCorrectly}).");
                Assert.IsTrue(sawRecap, $"The teaching recap must still appear (answerCorrectly={answerCorrectly}).");
                if (answerCorrectly)
                {
                    Assert.AreNotEqual(scoreBefore, scoreText.text, "The HUD score must still update for Detective.");
                }

                yield return WaitUntil(() => !IsPlaying() || GetDirector().IsDecisionPhase, 20f, "The next round never reached Decision.");
            }
        }

        /// <summary>Brief section 9/10: a correct accusation must expose
        /// the actual impostor suspect (not just flash a generic color)
        /// and the recap must name that exact impostor and its authored
        /// explanation — reads whichever challenge/slot the RNG actually
        /// drew rather than assuming one.</summary>
        [UnityTest]
        public IEnumerator Detective_CorrectReveal_ExposesTheImpostor_AndRecapMatchesTheChallenge()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var director = GetDirector();
            var challenge = director.CurrentErrorDetection;
            var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;
            var impostorName = challenge.Items[challenge.AnomalyIndex];

            FindButton($"DetectiveSuspect{displayAnomalyIndex}").onClick.Invoke();

            var impostorTag = FindTextUnder($"DetectiveSuspect{displayAnomalyIndex}", "ImpostorTag");
            yield return WaitUntil(
                () => !IsPlaying() || impostorTag.color.a > 0.9f,
                5f,
                "The impostor tag never appeared on the correctly-accused suspect after a correct reveal.");

            var recapRule = FindText("RecapRule");
            var recapImpostor = FindText("RecapImpostor");
            var recapWhy = FindText("RecapWhy");
            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(recapWhy.text),
                5f,
                "The teaching recap never appeared after a correct reveal.");

            Assert.IsTrue(recapRule.text.Contains(challenge.RuleLabel), $"Recap rule text ('{recapRule.text}') must name the round's own RuleLabel ('{challenge.RuleLabel}').");
            Assert.IsTrue(recapImpostor.text.Contains(impostorName), $"Recap impostor text ('{recapImpostor.text}') must name the actual impostor ('{impostorName}').");
            Assert.AreEqual(challenge.Explanation, recapWhy.text, "Recap explanation must match the challenge's own authored Explanation for this exact anomaly.");

            // C8.1j section 10: the recap must be a near-opaque evidence
            // panel — the reported bug was that the environment showed
            // through it strongly.
            var recapPanelImage = FindImageUnder("DetectiveLineup", "TeachingRecap");
            Assert.GreaterOrEqual(recapPanelImage.color.a, 0.9f,
                "Detective's recap panel must be near-opaque once shown — the environment must not compete with the explanation text.");
        }

        /// <summary>Brief section 9: an incorrect accusation must still end
        /// with the ACTUAL impostor exposed (not just the wrongly-accused
        /// suspect reacting) and the same teaching recap.</summary>
        [UnityTest]
        public IEnumerator Detective_IncorrectReveal_StillExposesTheActualImpostor()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var director = GetDirector();
            var challenge = director.CurrentErrorDetection;
            var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;
            var wrongIndex = (displayAnomalyIndex + 1) % challenge.Items.Length;
            var impostorName = challenge.Items[challenge.AnomalyIndex];

            FindButton($"DetectiveSuspect{wrongIndex}").onClick.Invoke();

            var impostorTag = FindTextUnder($"DetectiveSuspect{displayAnomalyIndex}", "ImpostorTag");
            yield return WaitUntil(
                () => !IsPlaying() || impostorTag.color.a > 0.9f,
                5f,
                "The actual impostor was never exposed after an incorrect accusation.");

            var recapImpostor = FindText("RecapImpostor");
            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(recapImpostor.text),
                5f,
                "The teaching recap never appeared after an incorrect reveal.");

            Assert.IsTrue(recapImpostor.text.Contains(impostorName), $"Recap impostor text ('{recapImpostor.text}') must still name the actual impostor ('{impostorName}'), not the wrongly-accused suspect.");
        }

        // --- C8.1g.3: Detective Gold art integration (brief section 22).
        // The mechanic itself is unchanged from C8.1g.2 — these tests only
        // prove the new production art is actually wired in, dynamic
        // labels/text survived the swap, long labels still fit, and the
        // lifecycle guarantees still hold with the new visual elements.

        [UnityTest]
        public IEnumerator Detective_UsesGoldEnvironmentArt()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var lineupRoom = FindImageUnder("Environment", "LineupRoom");
            Assert.IsNotNull(lineupRoom.sprite, "LineupRoom should be assigned from Resources/Art/Gold/Detective/Environment/Detective_LineupRoom.");
        }

        [UnityTest]
        public IEnumerator Detective_AuditorUsesGoldArt_NotAFloatingCard()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var auditorPortrait = FindImageUnder("AuditorCard", "Portrait");
            Assert.IsNotNull(auditorPortrait.sprite, "Auditor portrait should be assigned from Resources/Art/Gold/Detective/Characters/Detective_Auditor.");

            // Brief section 6: no floating card background any more —
            // "AuditorCard" is a plain RectTransform container now, not an
            // Image-backed panel.
            var auditorCard = FindRect("AuditorCard");
            Assert.IsNull(auditorCard.GetComponent<Image>(), "AuditorCard must not have its own background Image — the floating portrait card treatment is retired.");
        }

        [UnityTest]
        public IEnumerator Detective_AllFourDossiers_UseGoldSuspectArt()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            for (var i = 0; i < 4; i++)
            {
                var suspectImage = FindButton($"DetectiveSuspect{i}").GetComponent<Image>();
                Assert.IsNotNull(suspectImage.sprite, $"DetectiveSuspect{i} should be assigned the shared Detective_SuspectDossier sprite.");
                Assert.AreEqual("Detective_SuspectDossier", suspectImage.sprite.name, $"DetectiveSuspect{i} must use the SAME dossier artwork as every other slot.");
            }
        }

        /// <summary>Brief section 8: dynamic account names — including the
        /// longest real names in the shipped pool — must always fit inside
        /// the dossier art. Mirrors the exact TextGenerator-measurement
        /// technique the C8.1f.6 Balance token-text hotfix established
        /// (direct Unity TextGenerator measurement, not guessed font
        /// metrics), tested against the specific names the brief calls
        /// out plus the two longest strings actually in
        /// ClasicoMicrogameLibrary.ErrorDetectionPool today.</summary>
        [UnityTest]
        public IEnumerator Detective_LongAccountNames_StayWithinDossierLabelBounds()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var label = FindButton("DetectiveSuspect0").GetComponentInChildren<Text>();
            var labelRect = (RectTransform)label.transform;

            var candidates = new[]
            {
                "Caja",
                "Bancos",
                "Inventario",
                "Cuentas por pagar",
                "Cuentas por cobrar",
                "Seguro pagado por anticipado",
                "Préstamo bancario por pagar",
            };

            foreach (var candidate in candidates)
            {
                label.text = candidate;
                // C8.1k.3: Best Fit is off — size exactly as ShowChallenge
                // does before measuring, or this would measure whatever size
                // the previous candidate left behind.
                TwoLineTextFit.Apply(label, DossierLabelMaxFontSize, DossierLabelMinFontSize, 2);
                var settings = label.GetGenerationSettings(labelRect.rect.size);
                var generator = new TextGenerator();
                generator.Populate(candidate, settings);
                var extents = generator.rectExtents;

                Assert.LessOrEqual(generator.lineCount, 2,
                    $"'{candidate}' wrapped to {generator.lineCount} lines — brief section 8 allows at most 2.");
                Assert.LessOrEqual(extents.width, labelRect.rect.width + 0.5f,
                    $"'{candidate}' rendered {extents.width:F1} units wide, wider than its own label box ({labelRect.rect.width:F1}) — it would overflow the dossier frame horizontally.");
                Assert.LessOrEqual(extents.height, labelRect.rect.height + 0.5f,
                    $"'{candidate}' rendered {extents.height:F1} units tall, taller than its own label box ({labelRect.rect.height:F1}) — it would overflow the dossier frame vertically.");
            }

            yield return null;
        }

        // --- C8.1g.5: Detective result feedback simplification (brief
        // section 11). Manual review found the nested ImpostorRevealArt
        // frame inside the dossier frame inside the environment bay read
        // as visual clutter — it's retired entirely from active
        // presentation (the production asset stays on disk, just unused).
        // Only ONE dossier ever carries result color now (the actual
        // anomaly, regardless of outcome), and an incorrect guess never
        // gets a persistent color — only a brief shake.

        [UnityTest]
        public IEnumerator Detective_ImpostorRevealArt_IsFullyRetired_AndImpostorTagAppearsOnlyOnTheActualAnomaly()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var detectiveRoot = FindRect("DetectiveLineup");
            var revealArtElements = detectiveRoot.GetComponentsInChildren<Image>(true)
                .Where(img => img.gameObject.name == "ImpostorRevealArt")
                .ToList();
            Assert.IsEmpty(revealArtElements, "Detective_ImpostorReveal must be fully retired from active presentation — no ImpostorRevealArt element may exist (brief section 1).");

            var director = GetDirector();
            var challenge = director.CurrentErrorDetection;
            var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;

            FindButton($"DetectiveSuspect{displayAnomalyIndex}").onClick.Invoke();

            var impostorTag = FindTextUnder($"DetectiveSuspect{displayAnomalyIndex}", "ImpostorTag");
            yield return WaitUntil(
                () => !IsPlaying() || impostorTag.color.a > 0.9f,
                5f,
                "IMPOSTOR text never appeared on the actual anomaly after a correct accusation.");

            for (var i = 0; i < challenge.Items.Length; i++)
            {
                if (i == displayAnomalyIndex)
                {
                    continue;
                }

                var otherTag = FindTextUnder($"DetectiveSuspect{i}", "ImpostorTag");
                Assert.Less(otherTag.color.a, 0.1f, $"DetectiveSuspect{i} is not the impostor and must not show IMPOSTOR text.");
            }
        }

        [UnityTest]
        public IEnumerator Detective_CorrectAnswer_OnlyHighlightsTheAnomaly_OthersStayNeutral()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var director = GetDirector();
            var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;

            FindButton($"DetectiveSuspect{displayAnomalyIndex}").onClick.Invoke();

            var anomalyImage = FindButton($"DetectiveSuspect{displayAnomalyIndex}").GetComponent<Image>();
            yield return WaitUntil(
                () => !IsPlaying() || anomalyImage.color != Color.white,
                5f,
                "The anomaly dossier never received its result tint after a correct accusation.");

            for (var i = 0; i < 4; i++)
            {
                if (i == displayAnomalyIndex)
                {
                    continue;
                }

                var otherImage = FindButton($"DetectiveSuspect{i}").GetComponent<Image>();
                Assert.AreEqual(Color.white, otherImage.color, $"DetectiveSuspect{i} must stay visually neutral after a correct accusation — no cleared/dimmed treatment (brief section 5).");
            }
        }

        /// <summary>Brief section 4 — "the key behavior change": an
        /// incorrect accusation must never leave persistent color on the
        /// wrongly-accused suspect (only a brief shake), while the actual
        /// anomaly always ends up with the result tint once the recap
        /// appears.</summary>
        [UnityTest]
        public IEnumerator Detective_IncorrectAnswer_NeverPersistentlyColorsTheWrongSuspect()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var director = GetDirector();
            var challenge = director.CurrentErrorDetection;
            var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;
            var wrongIndex = (displayAnomalyIndex + 1) % challenge.Items.Length;

            FindButton($"DetectiveSuspect{wrongIndex}").onClick.Invoke();

            var recapWhy = FindText("RecapWhy");
            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(recapWhy.text),
                5f,
                "The teaching recap never appeared after an incorrect reveal.");

            var wrongImage = FindButton($"DetectiveSuspect{wrongIndex}").GetComponent<Image>();
            Assert.AreEqual(Color.white, wrongImage.color, "The wrongly-accused suspect must never carry a persistent result color (brief section 4).");

            var anomalyImage = FindButton($"DetectiveSuspect{displayAnomalyIndex}").GetComponent<Image>();
            Assert.AreNotEqual(Color.white, anomalyImage.color, "The actual anomaly must carry the result tint once the recap appears.");
        }

        [UnityTest]
        public IEnumerator Detective_Timeout_HighlightsOnlyTheActualAnomaly()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var director = GetDirector();
            var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;

            yield return WaitUntil(
                () => !IsPlaying() || GetDirector().IsFeedbackPhase,
                10f,
                "Detective's decision window never timed out into Feedback.");

            var impostorTag = FindTextUnder($"DetectiveSuspect{displayAnomalyIndex}", "ImpostorTag");
            yield return WaitUntil(
                () => !IsPlaying() || impostorTag.color.a > 0.9f,
                5f,
                "IMPOSTOR text never appeared on the actual anomaly after a timeout.");

            for (var i = 0; i < 4; i++)
            {
                if (i == displayAnomalyIndex)
                {
                    continue;
                }

                var otherImage = FindButton($"DetectiveSuspect{i}").GetComponent<Image>();
                Assert.AreEqual(Color.white, otherImage.color, $"DetectiveSuspect{i} must stay visually neutral after a timeout reveal.");
            }
        }

        [UnityTest]
        public IEnumerator Detective_HideAndReEntry_ClearsAllGoldVisualState()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.DetectError);

            var director = GetDirector();
            var displayAnomalyIndex = director.CurrentErrorDetectionDisplayAnomalyIndex;
            FindButton($"DetectiveSuspect{displayAnomalyIndex}").onClick.Invoke();

            var impostorTag = FindTextUnder($"DetectiveSuspect{displayAnomalyIndex}", "ImpostorTag");
            yield return WaitUntil(
                () => !IsPlaying() || impostorTag.color.a > 0.9f,
                5f,
                "IMPOSTOR text never appeared — cannot prove Hide() clears it if it was never showing.");

            FindButton("AbortButton").onClick.Invoke();

            Assert.Less(impostorTag.color.a, 0.05f, "Hide() must clear the IMPOSTOR text immediately.");
            for (var i = 0; i < 4; i++)
            {
                var suspectImage = FindButton($"DetectiveSuspect{i}").GetComponent<Image>();
                Assert.AreEqual(Color.white, suspectImage.color, $"Hide() must reset DetectiveSuspect{i}'s dossier tint back to neutral white.");
            }
        }

        // --- C8.1f: Balance's new Debit/Credit account-selection mechanic
        // (Docs/C8_1F_BALANCE_MECHANIC_REDESIGN.md) replaces the old
        // continuous nudge/confirm flow. These tests exercise the real
        // named UI controls (BalanceAccountOption0..3) exactly the way the
        // player would, never the engine directly — Western/Game
        // Show/Detective's own existing tests elsewhere in this file
        // already prove those three are unaffected, since every one of
        // them still drives its own named controls through the same
        // session/host wiring this phase touched only for Balance.

        private (string debitLabel, string creditLabel) FindCorrectAccountLabels(ClasicoSessionDirector director)
        {
            var challenge = director.CurrentDebitCredit;
            return (challenge.CorrectDebitAccount, challenge.CorrectCreditAccount);
        }

        [UnityTest]
        public IEnumerator Balance_ShowsTransactionAndOpensOnTheChargeStep()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            Assume.That(GetDirector().CurrentArchetype, Is.EqualTo(MicrogameArchetype.Balance));

            var director = GetDirector();
            Assert.AreEqual(director.CurrentDebitCredit.TransactionText, FindText("Transaction").text);
            Assert.AreEqual("¿QUÉ CUENTA SE CARGA?", FindText("Prompt").text);
            Assert.IsTrue(FindButton("BalanceAccountOption0").interactable);
        }

        [UnityTest]
        public IEnumerator Balance_FirstSelection_LocksDebitAndAdvancesToCreditStep_WithoutAnyMachineReaction()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();

            var beamBefore = FindRect("Beam").localRotation;
            var leftPanColorBefore = FindImageUnder("BalanceMachine", "LeftPan").color;
            var rightPanColorBefore = FindImageUnder("BalanceMachine", "RightPan").color;

            FindButton("BalanceAccountOption0").onClick.Invoke();

            Assert.AreEqual("¿QUÉ CUENTA SE ACREDITA?", FindText("Prompt").text,
                "A single (debit) selection must advance the prompt to the credit step.");
            Assert.IsTrue(director.IsDecisionPhase, "A single selection must not resolve the microgame.");
            Assert.AreEqual(beamBefore, FindRect("Beam").localRotation,
                "The beam must not move before the second (credit) selection locks — no visual weight before both commit.");
            Assert.AreEqual(leftPanColorBefore, FindImageUnder("BalanceMachine", "LeftPan").color,
                "No pan may change color before both selections lock (the anti-cheat rule).");
            Assert.AreEqual(rightPanColorBefore, FindImageUnder("BalanceMachine", "RightPan").color);
            Assert.IsTrue(FindButton("BalanceAccountOption1").interactable,
                "Options must remain interactable (neutral, re-enabled) for the credit step.");

            // C8.1f brief, section 26 — explicit anti-cheat structural
            // check: no result glow, no smoke, no correctness feedback of
            // any kind may be visible before both selections lock.
            var recapPanelImage = FindImageUnder("BalanceMachine", "TeachingRecap");
            Assert.AreEqual(0f, recapPanelImage.color.a, 0.001f, "The teaching recap must not be visible before both selections lock.");
            foreach (var smokeImage in _root.GetComponentsInChildren<Image>(true).Where(img => img.name == "Smoke" && HasAncestorNamed(img.transform, "BalanceMachine")))
            {
                Assert.AreEqual(0f, smokeImage.color.a, 0.001f, "No smoke may be visible before both selections lock.");
            }

            // C8.1f.4 brief section 15: neither token may have begun
            // traveling/materializing, and the Processing beat/verdict must
            // not have started, after only the debit pick locks.
            var debitTokenPlate = FindImageUnder("LeftPan", "Plate");
            var creditTokenPlate = FindImageUnder("RightPan", "Plate");
            Assert.AreEqual(0f, debitTokenPlate.color.a, 0.001f, "The debit token must not be visible/traveling before both selections lock.");
            Assert.AreEqual(0f, creditTokenPlate.color.a, 0.001f, "The credit token must not be visible/traveling before both selections lock.");
            Assert.AreEqual(string.Empty, FindText("Verdict").text, "The central verdict must not appear before both selections lock.");
        }

        [UnityTest]
        public IEnumerator Balance_BothSelectionsCorrect_ProducesCorrectFeedback_AndAwardsScore()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var scoreBefore = _installer.FlowController.CurrentSession.Score;

            yield return AnswerCurrentMicrogame(answerCorrectly: true);

            Assert.IsTrue(director.LastAnswerCorrect);
            Assert.Greater(_installer.FlowController.CurrentSession.Score, scoreBefore,
                "A fully correct Debit/Credit answer must still award normal Clasico score.");
        }

        [UnityTest]
        public IEnumerator Balance_OneAccountWrong_IsIncorrectOverall_AndAwardsNoScore()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var challenge = director.CurrentDebitCredit;
            var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
            var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);
            var wrongCreditChoice = (correctCreditIndex + 1) % challenge.AccountOptions.Length;
            var scoreBefore = _installer.FlowController.CurrentSession.Score;

            FindButton($"BalanceAccountOption{correctDebitIndex}").onClick.Invoke();
            FindButton($"BalanceAccountOption{wrongCreditChoice}").onClick.Invoke();
            yield return WaitUntil(() => !IsPlaying() || !GetDirector().IsDecisionPhase, 5f, "Submitting both picks did not leave Decision phase.");

            Assert.IsFalse(director.LastAnswerCorrect, "Exactly one correct account must never score as a full correct answer (Partial is a visual/teaching state only).");
            Assert.AreEqual(scoreBefore, _installer.FlowController.CurrentSession.Score, "A Partial result must award no score of its own.");
        }

        [UnityTest]
        public IEnumerator Balance_BothAccountsWrong_IsIncorrect_AndAwardsNoScore()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var scoreBefore = _installer.FlowController.CurrentSession.Score;

            yield return AnswerCurrentMicrogame(answerCorrectly: false);

            Assert.IsFalse(director.LastAnswerCorrect);
            Assert.AreEqual(scoreBefore, _installer.FlowController.CurrentSession.Score);
        }

        [UnityTest]
        public IEnumerator Balance_TeachingRecap_ShowsTheCorrectChargeAndCreditAccounts_AfterAnAnswer()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var challenge = director.CurrentDebitCredit;
            var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
            var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);
            var (debitLabel, creditLabel) = FindCorrectAccountLabels(director);

            FindButton($"BalanceAccountOption{correctDebitIndex}").onClick.Invoke();
            FindButton($"BalanceAccountOption{correctCreditIndex}").onClick.Invoke();

            // The recap only appears once the presenter's own machine-
            // reaction coroutine finishes (token insert + oscillation +
            // settle) — a real animation timeline decoupled from the
            // director's own (much shorter) Lock/Feedback phase durations,
            // so this polls the actual UI state rather than trusting
            // director.IsFeedbackPhase to mean "the recap is already showing".
            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(FindText("Debit").text),
                5f,
                "The teaching recap's debit text never appeared after answering.");

            StringAssert.Contains(debitLabel, FindText("Debit").text);
            StringAssert.Contains(creditLabel, FindText("Credit").text);
        }

        /// <summary>C8.1j section 6/7: the recap must be a structurally
        /// separate, near-opaque zone that never overlaps the verdict —
        /// the actual reported "verdict/explanation collision" bug. Checked
        /// geometrically (world-space bounds) rather than by inference.</summary>
        [UnityTest]
        public IEnumerator Balance_VerdictAndRecapPanels_NeverOverlap_AndRecapIsNearOpaque()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var challenge = director.CurrentDebitCredit;
            var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
            var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);

            FindButton($"BalanceAccountOption{correctDebitIndex}").onClick.Invoke();
            FindButton($"BalanceAccountOption{correctCreditIndex}").onClick.Invoke();

            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(FindText("Debit").text),
                6f,
                "The teaching recap never appeared after a correct answer.");

            var verdictImage = FindImageUnder("BalanceMachine", "VerdictPanel");
            var recapImage = FindImageUnder("BalanceMachine", "TeachingRecap");

            var verdictCorners = new Vector3[4];
            ((RectTransform)verdictImage.transform).GetWorldCorners(verdictCorners);
            var recapCorners = new Vector3[4];
            ((RectTransform)recapImage.transform).GetWorldCorners(recapCorners);

            var verdictBottom = verdictCorners[0].y;
            var recapTop = recapCorners[1].y;
            Assert.Less(recapTop, verdictBottom,
                "The teaching recap panel must sit entirely below the verdict panel — they must never overlap (the reported verdict/explanation collision).");

            Assert.GreaterOrEqual(recapImage.color.a, 0.9f,
                "The recap panel must be near-opaque once shown (brief section 7: not transparent enough for machine art to interfere).");
        }

        [UnityTest]
        public IEnumerator Balance_Timeout_RevealsTheCorrectPair_AndCountsAsIncorrect_WithoutSynthesizingAnAnswer()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var session = _installer.FlowController.CurrentSession;
            var scoreBefore = session.Score;

            // Deliberately submit nothing and just wait out the real
            // configured decision window (production asset value) rather
            // than either selection — proves a timeout never synthesizes a
            // fake credit answer just to reach a resolvable state.
            var deadline = Time.realtimeSinceStartup + 15f;
            while (director.IsDecisionPhase && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsFalse(director.IsDecisionPhase, "The Balance round never timed out within 15s — decision window regressed.");
            Assert.IsFalse(director.LastAnswerCorrect);
            Assert.AreEqual(scoreBefore, session.Score, "A timeout must award no score.");
        }

        // --- C8.1f.3: Gold visual pass — the "Beam" GameObject's own
        // localRotation is the single, already-established observable proxy
        // (reused from the pre-existing anti-cheat test above) for whether
        // the physical machine actually solved itself. These tests never
        // touch the presenter internals, only that one named transform plus
        // named UI controls, exactly like every other test in this file.

        /// <summary>Brief section 11/12: the machine must visibly solve
        /// itself — the beam settles at an exact 0-degree equilibrium on a
        /// fully correct answer, not merely "close enough".</summary>
        [UnityTest]
        public IEnumerator Balance_CorrectAnswer_BeamSettlesAtExactEquilibrium()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var challenge = director.CurrentDebitCredit;
            var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
            var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);

            FindButton($"BalanceAccountOption{correctDebitIndex}").onClick.Invoke();
            FindButton($"BalanceAccountOption{correctCreditIndex}").onClick.Invoke();

            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(FindText("Debit").text),
                6f,
                "The teaching recap never appeared after a correct answer — the recap is only shown once the machine sequence itself finishes.");

            var angle = FindRect("Beam").localEulerAngles.z;
            if (angle > 180f) { angle -= 360f; }
            Assert.AreEqual(0f, angle, 0.5f, "A correct answer must leave the beam at exact 0-degree equilibrium — the machine must visibly solve itself, not just imply success.");

            // C8.1f.4 brief section 5/6: the central verdict must present
            // "EQUILIBRIO" for a correct answer.
            Assert.AreEqual("EQUILIBRIO", FindText("Verdict").text, "A correct answer must present the central EQUILIBRIO verdict.");
        }

        /// <summary>Brief section 14: Partial must NEVER let the beam
        /// SETTLE/STOP at 0-degree equilibrium ("do NOT make Partial look
        /// almost successful enough to imply partial scoring") — the beam
        /// must end the reaction visibly off-level. A brief, continuous
        /// pass near 0 while ramping away from the neutral starting pose is
        /// not itself a violation (any animation that starts at rest and
        /// moves away must cross small angles on the way out); what would
        /// be a violation is the beam coming to rest there, which the final
        /// angle below directly checks.</summary>
        [UnityTest]
        public IEnumerator Balance_PartialAnswer_BeamNeverSettlesAtEquilibrium()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var challenge = director.CurrentDebitCredit;
            var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
            var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);
            var wrongCreditChoice = (correctCreditIndex + 1) % challenge.AccountOptions.Length;
            var beam = FindRect("Beam");

            FindButton($"BalanceAccountOption{correctDebitIndex}").onClick.Invoke();
            FindButton($"BalanceAccountOption{wrongCreditChoice}").onClick.Invoke();

            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(FindText("Debit").text),
                6f,
                "The teaching recap never appeared after a Partial answer.");

            var finalAngle = beam.localEulerAngles.z;
            if (finalAngle > 180f) { finalAngle -= 360f; }
            Assert.Greater(Mathf.Abs(finalAngle), 1f, "A Partial answer must end with the beam visibly off-level, never settled at equilibrium (brief section 14).");

            // C8.1f.4 brief section 5/7: the central verdict must present
            // "DESEQUILIBRIO" — never EQUILIBRIO — for a Partial answer.
            Assert.AreEqual("DESEQUILIBRIO", FindText("Verdict").text, "A Partial answer must present the central DESEQUILIBRIO verdict, never EQUILIBRIO.");
        }

        /// <summary>Brief section 15: an Incorrect answer must leave the
        /// beam visibly off-level (jammed) — never destroyed, but never
        /// resolved either.</summary>
        [UnityTest]
        public IEnumerator Balance_IncorrectAnswer_BeamEndsVisiblyOffLevel()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);

            yield return AnswerCurrentMicrogame(answerCorrectly: false);

            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(FindText("Debit").text),
                6f,
                "The teaching recap never appeared after an incorrect answer.");

            var angle = FindRect("Beam").localEulerAngles.z;
            if (angle > 180f) { angle -= 360f; }
            Assert.Greater(Mathf.Abs(angle), 5f, "An incorrect answer must leave the beam visibly off-level (jammed) — never settled near equilibrium.");

            // C8.1f.4 brief section 5/8: the central verdict must present
            // "DESEQUILIBRIO" for an incorrect answer.
            Assert.AreEqual("DESEQUILIBRIO", FindText("Verdict").text, "An incorrect answer must present the central DESEQUILIBRIO verdict.");
        }

        /// <summary>Brief section 7: the physical token itself (not just the
        /// teaching recap afterward) must show the actual selected account
        /// names — proves the mapping (left/debit token gets the debit
        /// pick, right/credit token gets the credit pick) is preserved end
        /// to end, not just the final recap text.</summary>
        [UnityTest]
        public IEnumerator Balance_TokenLabels_ShowTheActualSelectedAccountNames()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var challenge = director.CurrentDebitCredit;
            var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
            var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);

            FindButton($"BalanceAccountOption{correctDebitIndex}").onClick.Invoke();
            FindButton($"BalanceAccountOption{correctCreditIndex}").onClick.Invoke();

            // Tokens materialize during the presenter's own token-insert
            // beat, well before the outcome sequence/recap — poll the
            // tokens' own label text directly rather than waiting for the
            // recap.
            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(FindTextUnder("LeftPan", "Label").text),
                6f,
                "The debit token's own label never showed any text after both picks locked.");

            StringAssert.Contains(challenge.CorrectDebitAccount, FindTextUnder("LeftPan", "Label").text, "The left (debit) token must show the actual selected debit account name.");
            StringAssert.Contains(challenge.CorrectCreditAccount, FindTextUnder("RightPan", "Label").text, "The right (credit) token must show the actual selected credit account name.");
        }

        /// <summary>Brief section 22: Hide (via SALIR/Abort) must leave no
        /// trace of a prior round's off-level/jammed machine state, and a
        /// fresh Balance round afterward must begin clean — beam at neutral
        /// rest, recap hidden — never inheriting the aborted round's visual
        /// state. Reuses the same Abort -&gt; Results -&gt; Exit -&gt;
        /// relaunch pattern already proven reliable by the Western
        /// lifecycle tests elsewhere in this file.</summary>
        [UnityTest]
        public IEnumerator Balance_HideAndReEntry_RestoresNeutralMachineState()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);

            yield return AnswerCurrentMicrogame(answerCorrectly: false);
            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(FindText("Debit").text),
                6f,
                "The teaching recap never appeared after an incorrect answer.");

            var angleAfterIncorrect = FindRect("Beam").localEulerAngles.z;
            if (angleAfterIncorrect > 180f) { angleAfterIncorrect -= 360f; }
            Assume.That(Mathf.Abs(angleAfterIncorrect), Is.GreaterThan(1f), "Test setup assumption failed: the beam should be off-level after an incorrect answer.");

            FindButton("AbortButton").onClick.Invoke();
            yield return null;
            FindButton("ExitButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual(GameLifecycleState.Idle, _installer.FlowController.State, "Must return to Idle after exiting Results.");

            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);

            var angleAfterReEntry = FindRect("Beam").localEulerAngles.z;
            if (angleAfterReEntry > 180f) { angleAfterReEntry -= 360f; }
            Assert.AreEqual(0f, angleAfterReEntry, 0.5f, "A fresh Balance round must start with the beam at neutral 0-degree rest, never inheriting a prior round's off-level state.");

            var recapImage = FindImageUnder("BalanceMachine", "TeachingRecap");
            Assert.AreEqual(0f, recapImage.color.a, 0.001f, "A fresh Balance round must start with the teaching recap hidden.");

            // C8.1f.4: the central verdict from the aborted round ("DESEQUILIBRIO")
            // must never leak into the fresh round.
            Assert.AreEqual(string.Empty, FindText("Verdict").text, "A fresh Balance round must start with the central verdict text cleared, never inheriting a prior round's DESEQUILIBRIO/EQUILIBRIO.");

            // C8.1f.5 brief section 11.F/18: re-entry must start clean —
            // no leftover audio from the aborted round's own jam cue.
            var anyAudioPlaying = false;
            foreach (var source in _root.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.isPlaying)
                {
                    anyAudioPlaying = true;
                }
            }

            Assert.IsFalse(anyAudioPlaying, "A fresh Balance round must start with no audio still playing from the aborted previous round.");
        }

        /// <summary>C8.1f.4 brief section 3/15: proves the new Processing
        /// beat is real — the verdict must stay empty for a meaningfully
        /// long window after both tokens have visibly finished traveling
        /// in, not appear instantly the moment they seat. Uses the token's
        /// own final scale (1.0, reached only once
        /// <c>InsertBothTokens</c>'s travel animation completes) as the
        /// concrete "tokens are fully seated" signal, then measures how
        /// long the verdict stays empty afterward.</summary>
        [UnityTest]
        public IEnumerator Balance_Processing_KeepsVerdictEmptyForAMeaningfulWindowAfterTokensSeat()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var challenge = director.CurrentDebitCredit;
            var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
            var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);

            FindButton($"BalanceAccountOption{correctDebitIndex}").onClick.Invoke();
            FindButton($"BalanceAccountOption{correctCreditIndex}").onClick.Invoke();

            // FindRect("Token") would be ambiguous — both the debit and
            // credit pans each build an identically-named "Token" child —
            // so this walks up from the already-disambiguated debit
            // token's own "Label" text (FindTextUnder requires the
            // "LeftPan" ancestor) to its parent Token rect instead.
            var debitToken = (RectTransform)FindTextUnder("LeftPan", "Label").transform.parent;
            yield return WaitUntil(
                () => !IsPlaying() || Mathf.Approximately(debitToken.localScale.x, 1f),
                6f,
                "The debit token never finished traveling in (never reached its final scale) after both picks locked.");

            var seatedTime = Time.realtimeSinceStartup;
            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(FindText("Verdict").text),
                6f,
                "The central verdict never appeared after the tokens seated.");

            var processingSpan = Time.realtimeSinceStartup - seatedTime;
            Assert.Greater(processingSpan, 0.6f, $"The verdict appeared only {processingSpan:F2}s after the tokens seated — the Processing 'thinking' beat (brief section 3, target ~0.8-1.2s) does not appear to be running.");
        }

        /// <summary>C8.1f.5 brief sections 10/16: traces and proves the
        /// exact audio-leak bug the human review reported — Balance's
        /// Processing audio kept playing well past the round. Fingerprints
        /// Balance's own shared <c>_sfxAudioSource</c> structurally (the
        /// same "find whichever AudioSource is isPlaying" technique the
        /// Western lifecycle tests already use elsewhere in this file) at
        /// the moment Balance's own Processing beat is definitely running,
        /// then proves SALIR/Abort silences it immediately AND that nothing
        /// resumes afterward (no delayed lock/jam/processing tail — brief
        /// section 16's explicit list).</summary>
        [UnityTest]
        public IEnumerator Balance_Abort_DuringProcessing_StopsAudioImmediately_WithNoDelayedTail()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var challenge = director.CurrentDebitCredit;
            var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
            var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);

            FindButton($"BalanceAccountOption{correctDebitIndex}").onClick.Invoke();
            FindButton($"BalanceAccountOption{correctCreditIndex}").onClick.Invoke();

            // All four presenters share the same host GameObject for their
            // own SFX AudioSource (ClasicoGameHost borrows ClasicoHud as
            // every presenter's coroutine host — see BuildAudio's own
            // doc-comment), so a plain "whichever source is isPlaying"
            // fingerprint can accidentally capture an unrelated stray
            // voice left over from CycleUntilArchetype's own navigation
            // through the other three archetypes on the way here. Polling
            // directly for the real condition this test cares about — a
            // clip actually named for Balance's Processing beat is
            // playing — sidesteps proxying through UI state entirely (a
            // token-travel-alpha proxy was tried and found to fire well
            // before InsertBothTokens actually finishes, since alpha
            // saturates to 1 partway through the fall by design).
            AudioSource playingSource = null;
            yield return WaitUntil(
                () =>
                {
                    if (!IsPlaying())
                    {
                        return true;
                    }

                    foreach (var source in _root.GetComponentsInChildren<AudioSource>(true))
                    {
                        if (source.isPlaying && source.clip != null && source.clip.name.Contains("Processing"))
                        {
                            playingSource = source;
                            return true;
                        }
                    }

                    return false;
                },
                6f,
                "Balance's Processing audio cue never started playing after both accounts were selected.");

            Assert.IsNotNull(playingSource, "Balance's Processing audio cue never started playing — could not fingerprint it before testing Abort.");

            FindButton("AbortButton").onClick.Invoke();
            // A handful of frames, not a real wait — Hide()'s Stop() call
            // itself runs synchronously inside the same click handler, but
            // Unity's audio backend can take a frame or two to actually
            // reflect a Stop() in isPlaying. This is not the "delayed
            // tail" brief section 16 forbids (that's about a full
            // lock/jam/processing cue resuming, not a few-millisecond
            // audio-engine propagation delay) — the 2s check below is
            // what actually proves no such tail exists.
            for (var i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.IsFalse(playingSource.isPlaying, "SALIR/Abort must immediately stop Balance's Processing audio (brief section 16).");

            // No delayed lock/jam/processing tail — wait well past where
            // the aborted round's own remaining beats (oscillate + lock,
            // or ramp + jam) would have landed, and confirm silence holds.
            yield return new WaitForSeconds(2f);
            Assert.IsFalse(playingSource.isPlaying, "No Balance audio may resume after SALIR/Abort — no delayed CorrectLock/IncorrectJam/Processing tail (brief section 16).");
        }

        /// <summary>C8.1f.5 brief section 13/15: the Results-screen hard
        /// guarantee's own actual mechanism — <c>MachineSequenceRoutine</c>
        /// stops all Balance audio unconditionally right before showing the
        /// recap, regardless of what follows (another round or Results),
        /// so this is exercised identically either way. Deliberately
        /// checked against a CORRECT answer specifically because
        /// <c>Balance_CorrectLock.mp3</c> is the longer of the two real
        /// supplied clips (~7s) — if the fix works here, against the worst
        /// real offender in the project today, it holds for every shorter
        /// clip too.</summary>
        [UnityTest]
        public IEnumerator Balance_CorrectAnswer_NoAudioStillPlayingOnceRecapAppears()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);
            var director = GetDirector();
            var challenge = director.CurrentDebitCredit;
            var correctDebitIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectDebitAccount);
            var correctCreditIndex = System.Array.IndexOf(challenge.AccountOptions, challenge.CorrectCreditAccount);

            FindButton($"BalanceAccountOption{correctDebitIndex}").onClick.Invoke();
            FindButton($"BalanceAccountOption{correctCreditIndex}").onClick.Invoke();

            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(FindText("Debit").text),
                6f,
                "The teaching recap never appeared after a correct answer.");

            // One extra frame so StopAllBalanceAudio's own Stop() call
            // (issued the same frame the recap becomes visible) has fully
            // taken effect.
            yield return null;

            var anyAudioPlaying = false;
            foreach (var source in _root.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.isPlaying)
                {
                    anyAudioPlaying = true;
                }
            }

            Assert.IsFalse(anyAudioPlaying, "No audio source may still be playing once the recap appears after a correct answer — Balance_CorrectLock.mp3's own real ~7s length must be cut off, never left to finish naturally underneath the recap/next round/Results (brief section 13/15).");
        }

        /// <summary>Same hard guarantee as
        /// <see cref="Balance_CorrectAnswer_NoAudioStillPlayingOnceRecapAppears"/>,
        /// exercised on the Incorrect path instead (a different code path
        /// through <see cref="MachineSequenceRoutine"/> that also plays
        /// smoke-pressure audio).</summary>
        [UnityTest]
        public IEnumerator Balance_IncorrectAnswer_NoAudioStillPlayingOnceRecapAppears()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);

            yield return AnswerCurrentMicrogame(answerCorrectly: false);
            yield return WaitUntil(
                () => !IsPlaying() || !string.IsNullOrEmpty(FindText("Debit").text),
                6f,
                "The teaching recap never appeared after an incorrect answer.");

            yield return null;

            var anyAudioPlaying = false;
            foreach (var source in _root.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.isPlaying)
                {
                    anyAudioPlaying = true;
                }
            }

            Assert.IsFalse(anyAudioPlaying, "No audio source may still be playing once the recap appears after an incorrect answer (brief section 13/15).");
        }

        /// <summary>C8.1f.6 brief section 5: proves the reported "text
        /// overflows outside the token frame" defect is actually fixed —
        /// not just "shrinks a lot" but genuinely stays within the token's
        /// own Label RectTransform, on both axes, in at most 2 lines.
        /// Exercises the two names the brief itself calls out
        /// ("Cuentas por cobrar"/"Cuentas por pagar") plus the two longest
        /// strings actually present in <c>ClasicoMicrogameLibrary.DebitCreditPool</c>
        /// today ("Préstamo bancario por pagar", "Seguro pagado por
        /// anticipado") — worse worst-cases than the brief's own examples,
        /// so this is a strictly stronger check. Reads Unity's own
        /// generated text bounds (<see cref="Text.cachedTextGenerator"/>)
        /// rather than guessing from font metrics, since that is the same
        /// authority Unity's own renderer uses to decide what actually
        /// draws.</summary>
        [UnityTest]
        public IEnumerator Balance_LongAccountNames_StayWithinTokenLabelBounds()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.Balance);

            var debitLabel = FindTextUnder("LeftPan", "Label");
            var labelRect = (RectTransform)debitLabel.transform;

            var candidates = new[]
            {
                "Cuentas por cobrar",
                "Cuentas por pagar",
                "Préstamo bancario por pagar",
                "Seguro pagado por anticipado",
            };

            foreach (var candidate in candidates)
            {
                // A fresh TextGenerator populated directly (rather than
                // reading Text.cachedTextGenerator after a frame) avoids
                // any lazy-rebuild timing ambiguity in Unity's own
                // Graphic/Canvas pipeline — this measures exactly what the
                // Label's own current Best Fit settings would render for
                // this string, deterministically, no yields required.
                debitLabel.text = candidate;
                var settings = debitLabel.GetGenerationSettings(labelRect.rect.size);
                var generator = new TextGenerator();
                generator.Populate(candidate, settings);
                var extents = generator.rectExtents;

                Assert.LessOrEqual(generator.lineCount, 2,
                    $"'{candidate}' wrapped to {generator.lineCount} lines — brief section 2 allows at most 2.");
                Assert.LessOrEqual(extents.width, labelRect.rect.width + 0.5f,
                    $"'{candidate}' rendered {extents.width:F1} units wide, wider than its own label box ({labelRect.rect.width:F1}) — it would spill past the token frame horizontally.");
                Assert.LessOrEqual(extents.height, labelRect.rect.height + 0.5f,
                    $"'{candidate}' rendered {extents.height:F1} units tall, taller than its own label box ({labelRect.rect.height:F1}) — it would spill past the token frame vertically.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator EnsureEventSystem_NeverCreatesADuplicate()
        {
            var secondRoot = new GameObject("GameSessionTest2");
            secondRoot.AddComponent<GameSessionInstaller>();
            yield return null;

            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);

            Object.Destroy(secondRoot);
            yield return null;
        }
    }
}
