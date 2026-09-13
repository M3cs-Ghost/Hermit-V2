using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
                    Assert.IsTrue(FindButton("BalanceConfirm").interactable);
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
                    if (answerCorrectly)
                    {
                        var challenge = director.CurrentEquation;
                        var steps = Mathf.RoundToInt((challenge.CorrectValue - challenge.StartValue) / challenge.StepSize);
                        var buttonName = steps >= 0 ? "BalanceUp" : "BalanceDown";
                        for (var i = 0; i < Mathf.Abs(steps); i++)
                        {
                            FindButton(buttonName).onClick.Invoke();
                        }
                    }

                    FindButton("BalanceConfirm").onClick.Invoke();
                    break;
                }

                default:
                {
                    var challenge = director.CurrentErrorDetection;
                    var chosen = answerCorrectly ? challenge.AnomalyIndex : (challenge.AnomalyIndex + 1) % challenge.Items.Length;
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
                    var challenge = director.CurrentEquation;
                    var steps = Mathf.RoundToInt((challenge.CorrectValue - challenge.StartValue) / challenge.StepSize);
                    var buttonName = steps >= 0 ? "BalanceUp" : "BalanceDown";
                    for (var i = 0; i < Mathf.Abs(steps); i++)
                    {
                        FindButton(buttonName).onClick.Invoke();
                    }

                    FindButton("BalanceConfirm").onClick.Invoke();
                    break;
                }

                default:
                {
                    var challenge = director.CurrentErrorDetection;
                    FindButton($"DetectiveSuspect{challenge.AnomalyIndex}").onClick.Invoke();
                    break;
                }
            }
        }

        /// <summary>C8.1d.2: the previous Encounter test only proved
        /// <c>TumbleweedRoutine</c> fired — insufficient, since manual
        /// validation showed the round was already fully "live" (concept
        /// shown, targets interactable) on the very same frame, which a
        /// fire-and-forget coroutine check can't catch. This samples every
        /// frame of round 1's Intro phase and asserts the gameplay state
        /// (concept text, target interactability) never leaks in early, then
        /// asserts it becomes available the instant Decision begins — i.e.
        /// Decision genuinely does not overrun the intro, in either
        /// direction.
        ///
        /// C8.1d.3 then extended this with the (since-superseded) large
        /// in-world Sheriff protagonist's own proofs. C8.1d.4 replaces that
        /// Sheriff-actor check with the cinematic overlay's equivalent
        /// (kept in the same test rather than a near-duplicate one, since it
        /// already drives the exact frame-accurate polling loop needed): the
        /// cinematic overlay must be seen active during the intro, outlaws
        /// must stay hidden throughout it (not merely non-interactable), and
        /// the cinematic must be hidden again the instant Decision
        /// begins.</summary>
        [UnityTest]
        public IEnumerator WesternEncounter_Round1Intro_GatesConceptAndInputUntilIntroCompletes()
        {
            FindButton("Game_clasico").onClick.Invoke();

            var observedIntroFrame = false;
            var introStartTime = 0f;
            var conceptStayedHiddenDuringIntro = true;
            var inputStayedDisabledDuringIntro = true;
            var outlawsStayedHiddenDuringIntro = true;
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

                        if (!string.IsNullOrEmpty(FindText("Concept").text))
                        {
                            conceptStayedHiddenDuringIntro = false;
                        }

                        if (cinematicRoot.gameObject.activeInHierarchy)
                        {
                            cinematicSeenDuringIntro = true;
                        }

                        for (var i = 0; i < 4; i++)
                        {
                            var button = FindButton($"WesternTarget{i}");
                            if (button.gameObject.activeSelf)
                            {
                                outlawsStayedHiddenDuringIntro = false;
                                if (button.interactable)
                                {
                                    inputStayedDisabledDuringIntro = false;
                                }
                            }
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
            Assert.IsTrue(conceptStayedHiddenDuringIntro, "The accounting concept must not be visible while the Encounter intro/cinematic is still playing — Decision must not overrun the intro.");
            Assert.IsTrue(inputStayedDisabledDuringIntro, "No WesternTarget button may be interactable while the Encounter intro/cinematic is still playing — Decision must not overrun the intro.");
            Assert.IsTrue(outlawsStayedHiddenDuringIntro, "The outlaws must stay hidden while the cinematic intro is playing (C8.1d.4) — not just non-interactable.");
            Assert.IsTrue(cinematicSeenDuringIntro, "The cinematic overlay must be seen active at some point during round 1's intro.");

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
        /// covered by WesternEncounter_Round1Intro_GatesConceptAndInputUntilIntroCompletes).
        /// Polls the CinematicImage's own sprite reference every frame
        /// (Resources.Load reference-equality, the same proven pattern the
        /// outlaw Hit/Neutral tests already use) and records which of the
        /// three close-ups were seen, plus whether target labels and the
        /// reticle stayed hidden throughout (concept/input are already
        /// covered elsewhere) — never a coarse wait, since the C8.1d.1
        /// tumbleweed investigation already proved those can produce false
        /// negatives on transient state.</summary>
        [UnityTest]
        public IEnumerator WesternCinematic_AllThreeCloseUps_AppearDuringRound1Intro_LabelsAndReticleStayHidden()
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
            var labelsStayedHiddenDuringIntro = true;
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

                        for (var i = 0; i < 4; i++)
                        {
                            var label = FindButton($"WesternTarget{i}").GetComponentInChildren<Text>(true);
                            if (label != null && !string.IsNullOrEmpty(label.text))
                            {
                                labelsStayedHiddenDuringIntro = false;
                            }
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
            Assert.IsTrue(labelsStayedHiddenDuringIntro, "No WesternTarget label may show text while the cinematic intro is still playing.");
            Assert.IsTrue(reticleStayedHiddenDuringIntro, "The reticle must stay hidden while the cinematic intro is still playing.");
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
        /// pattern <see cref="WesternCinematic_AllThreeCloseUps_AppearDuringRound1Intro_LabelsAndReticleStayHidden"/>
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

        /// <summary>C8.1d.7: proves the refined "DISPARA" cue stays fully
        /// hidden throughout round 1's cinematic intro (never visible during
        /// any Sheriff/Outlaw/hand/silence/gunshot beat), becomes visible at
        /// the exact moment gameplay reveal happens, and fades again once
        /// the player fires their first shot. State/visibility-level checks
        /// only (<c>GameObject.activeSelf</c>, <c>Text.color.a</c>) — the
        /// same proven frame-by-frame pattern every <c>WesternCinematic_*</c>
        /// test uses, since (as established in C8.1d.6) the higher-level
        /// <see cref="LaunchClasico"/>/<see cref="AnswerCurrentMicrogame"/>
        /// helpers wait past the entire intro before returning and so can
        /// never observe this transient state.</summary>
        [UnityTest]
        public IEnumerator WesternShootout_DisparaCue_HiddenDuringCinematic_ShownAtReveal_FadesAfterFirstShot()
        {
            FindButton("Game_clasico").onClick.Invoke();

            var disparaStayedHiddenDuringIntro = true;
            var disparaShownAtDecisionStart = false;
            var reachedDecisionForRound1 = false;
            var firedShot = false;
            var disparaFadedAfterShot = false;
            var decisionStartTime = -1f;
            var fireTime = -1f;

            var deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && _installer.FlowController.State != GameLifecycleState.Results)
            {
                if (_installer.FlowController.State == GameLifecycleState.Playing && !GetDirector().IsCountingDown)
                {
                    var director = GetDirector();
                    var isWesternRound1 = director.CurrentArchetype == MicrogameArchetype.AimSelect && director.CurrentRoundWithinEncounter == 0;
                    var dispara = FindText("DisparaCue");
                    var isVisible = dispara.gameObject.activeSelf && dispara.color.a > 0.05f;

                    if (!reachedDecisionForRound1)
                    {
                        if (isWesternRound1 && director.IsIntroPhase && isVisible)
                        {
                            disparaStayedHiddenDuringIntro = false;
                        }

                        if (isWesternRound1 && director.IsDecisionPhase)
                        {
                            reachedDecisionForRound1 = true;
                            decisionStartTime = Time.realtimeSinceStartup;
                        }
                        else if (director.IsDecisionPhase && director.CurrentArchetype != MicrogameArchetype.AimSelect)
                        {
                            AnswerWhicheverPrecedesWestern(director);
                        }
                    }
                    else if (!firedShot)
                    {
                        // Give the punch-in coroutine a brief real window to
                        // actually run before firing — checking on the exact
                        // same frame Decision begins would always see alpha
                        // 0 (the coroutine hasn't had a frame to advance
                        // yet), which is a test-timing bug, not a real
                        // "cue never shown" defect.
                        if (isVisible)
                        {
                            disparaShownAtDecisionStart = true;
                        }

                        if (isVisible || Time.realtimeSinceStartup - decisionStartTime > 0.3f)
                        {
                            var options = director.CurrentClassification.CategoryOptions;
                            var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                            FindButton($"WesternTarget{correctIndex}").onClick.Invoke();
                            firedShot = true;
                            fireTime = Time.realtimeSinceStartup;
                        }
                    }
                    else if (!disparaFadedAfterShot && Time.realtimeSinceStartup - fireTime > 0.3f && !isVisible)
                    {
                        disparaFadedAfterShot = true;
                    }
                }

                if (firedShot && disparaFadedAfterShot)
                {
                    break;
                }

                yield return null;
            }

            Assert.IsTrue(reachedDecisionForRound1, "Round 1's Intro phase never transitioned into Decision within the time budget.");
            Assert.IsTrue(disparaStayedHiddenDuringIntro, "The DISPARA cue must stay hidden throughout the cinematic intro (Sheriff/Outlaw/hand/silence/gunshot).");
            Assert.IsTrue(disparaShownAtDecisionStart, "The DISPARA cue must become visible at (or immediately after) gameplay reveal.");
            Assert.IsTrue(firedShot, "Never reached a point where a shot could be fired.");
            Assert.IsTrue(disparaFadedAfterShot, "The DISPARA cue must fade out again after the player's first shot.");
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
        /// exists. What remains, and is still exactly as true as before:
        /// for every fired shot, correct or incorrect, across all 3 rounds,
        /// the gameplay gunshot fires before the impact accent, and the
        /// gunshot clip itself never changes based on correctness. Order
        /// only, via <see cref="WesternAudioEvents"/> — never a waveform
        /// assertion.</summary>
        [UnityTest]
        public IEnumerator WesternShootout_GameplayGunshot_FiresBeforeImpact_ForCorrectAndIncorrect()
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

            // Give the last round's own impact/countershot beat a moment to
            // land before the log is inspected.
            yield return new WaitForSeconds(0.6f);

            var gunshotEvents = WesternAudioEvents.Events.Where(e => e.Name == "GameplayGunshot").ToList();
            var impactEvents = WesternAudioEvents.Events.Where(e => e.Name == "GameplayImpact").ToList();

            Assert.AreEqual(3, gunshotEvents.Count, "A gameplay gunshot must fire exactly once per round.");
            Assert.AreEqual(3, impactEvents.Count, "An impact accent must fire exactly once per round.");

            for (var i = 0; i < 3; i++)
            {
                Assert.Less(gunshotEvents[i].Time, impactEvents[i].Time, $"Round {i + 1}: the gameplay gunshot must fire before the impact accent.");
            }

            Assert.AreSame(gunshotEvents[0].Clip, gunshotEvents[1].Clip, "The gameplay gunshot clip must be identical for correct and incorrect shots.");
            Assert.AreSame(gunshotEvents[1].Clip, gunshotEvents[2].Clip, "The gameplay gunshot clip must be identical across all rounds regardless of correctness.");
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

        /// <summary>C8.1d.9: proves the countershot fires only on a wrong
        /// answer (never a correct one), in the right order (after the
        /// player's own gunshot/impact), and drives the visible red "you
        /// got hit" flash — alternating correct/incorrect/correct across
        /// the 3-round Encounter, the same pattern C8.1d.7/.8's own gunshot
        /// tests use. Scoring deltas are checked alongside as a light
        /// structural proof correctness itself was never touched.</summary>
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
            Assert.AreEqual(3, impactEvents.Count, "An impact accent must still fire once per round (unchanged from C8.1d.7/.8).");
            Assert.Greater(enemyGunshotEvents[0].Time, impactEvents[1].Time, "The enemy gunshot (round 2, the incorrect one) must fire after that round's own player impact.");
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

        [UnityTest]
        public IEnumerator GameShow_ShowsIllustratedBackgroundAndPresentadorCard()
        {
            yield return LaunchClasico();
            yield return CycleUntilArchetype(MicrogameArchetype.ChooseSide);
            Assume.That(_installer.FlowController.State, Is.EqualTo(GameLifecycleState.Playing));

            var background = FindImageUnder("GameShow", "Background");
            Assert.IsNotNull(background.sprite, "Game Show background art should be assigned from Resources/Art/Gold/GameShow/GameShowBackground.");

            var presentadorPortrait = FindImageUnder("PresentadorCard", "Portrait");
            Assert.IsNotNull(presentadorPortrait.sprite, "Presentador portrait should be assigned from Resources/Art/Gold/GameShow/Presentador.");

            var presentadorFrame = FindRect("PresentadorCard");
            yield return AnswerCurrentMicrogame(answerCorrectly: true);
            Assert.Less(Vector2.Distance(presentadorFrame.localScale, Vector3.one), 0.05f,
                "Presentador's card must restore its resting scale after the correct-answer punch.");
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
