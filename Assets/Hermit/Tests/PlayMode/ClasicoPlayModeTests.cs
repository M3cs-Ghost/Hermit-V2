using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Hermit.Games;
using Hermit.Runtime.GameFramework;

namespace Hermit.Tests.PlayMode
{
    /// <summary>
    /// Exercises the real, composed C6 runtime (GameSessionInstaller + the
    /// generic selector + ClasicoGameHost/ClasicoHud + the shipped
    /// Resources/GameCatalog + ClasicoGameDefinition/content assets) end to
    /// end, driven by invoking the actual UI Button.onClick events a player's
    /// click would fire — not by calling GameFlowController directly — so
    /// this test also proves the selector and Clasico's host are wired
    /// correctly together.
    ///
    /// C6 changed the entry point: there is no more in-game "Jugar" button —
    /// launching Clasico now means clicking its entry in the selector
    /// ("Game_clasico").
    ///
    /// What this does NOT cover: an actual mouse/touch event routed through the
    /// Input System's raycaster. That is the manual test checklist's job (see
    /// Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md) — simulating real pointer
    /// input reliably in an automated PlayMode run is its own source of
    /// flakiness this slice does not need to take on.
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
        private float FindTimerFillAmount() => _root.GetComponentsInChildren<Image>(true).First(i => i.name == "Fill").fillAmount;

        /// <summary>The question card is built at anchoredPosition (0, -10)
        /// (see ClasicoHud.BuildPlayingPanel) and must always be back there
        /// after any reveal-feedback animation finishes. This is exactly what
        /// a real bug broke: RenderReveal used to run every frame of the
        /// reveal window instead of once, so the incorrect-answer shake
        /// coroutine was restarted every frame and never reached its own
        /// cleanup line — each restart re-captured an already-shifted base
        /// position, so the offset accumulated without bound and flung the
        /// card off-screen within about a second (the correct-answer punch
        /// did not show this because it computes scale fresh from elapsed
        /// time each restart, which does not accumulate).</summary>
        private static void AssertQuestionCardIsOnScreen(RectTransform questionCard)
        {
            var position = questionCard.anchoredPosition;
            Assert.Less(
                Vector2.Distance(position, new Vector2(0, -10)),
                5f,
                $"QuestionCard drifted to {position} — a reveal-feedback animation did not restore its resting position.");
        }

        private void AssertActiveOptionsAreInteractable()
        {
            var activeOptions = new[] { "Option0", "Option1", "Option2", "Option3" }
                .Select(FindButton)
                .Where(b => b.gameObject.activeSelf)
                .ToList();

            Assert.Greater(activeOptions.Count, 0, "At least one option button must be active for the new question.");
            foreach (var button in activeOptions)
            {
                Assert.IsTrue(button.interactable, $"{button.name} must be interactable for the new question.");
            }
        }

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

        private IEnumerator LaunchClasico()
        {
            FindButton("Game_clasico").onClick.Invoke();
            // The shipped ClasicoGameDefinition now runs a 3s countdown (C7)
            // before the first question — wait past it so callers land on a
            // real question, not mid-countdown. See
            // LaunchingClasico_ShowsACountdownBeforeTheFirstQuestion below for
            // the dedicated test of the countdown itself.
            yield return new WaitForSeconds(3.2f);
        }

        private IEnumerator PlayThroughToResults()
        {
            yield return LaunchClasico();

            const int maxQuestions = 20;
            var iterations = 0;
            while (_installer.FlowController.State == GameLifecycleState.Playing && iterations < maxQuestions)
            {
                FindButton("Option0").onClick.Invoke();
                yield return new WaitForSeconds(1.0f);
                iterations++;
            }
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
            Assert.IsNotNull(eventSystem, "No EventSystem exists — no UI input (mouse or keyboard) can work at all.");
            Assert.IsNotNull(
                eventSystem.GetComponent<InputSystemUIInputModule>(),
                "EventSystem must carry an InputSystemUIInputModule (New Input System) — this project does not use the legacy StandaloneInputModule.");
            yield break;
        }

        [UnityTest]
        public IEnumerator SelectorScreen_HasAVisibleInitialSelection()
        {
            AssertValidSelection("Game_clasico");
            yield break;
        }

        [UnityTest]
        public IEnumerator LaunchingClasico_StartsAGame_AndShowsAQuestion()
        {
            yield return LaunchClasico();

            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
            Assert.IsFalse(string.IsNullOrEmpty(FindText("Question").text));
            AssertValidSelection("Option0");
        }

        [UnityTest]
        public IEnumerator LaunchingClasico_ShowsACountdown_BeforeTheFirstQuestion()
        {
            FindButton("Game_clasico").onClick.Invoke();
            yield return null;

            // Still within the countdown window — no question yet, but the
            // game is already "Playing" (Countdown is a Clasico-internal
            // sub-phase, not a GameFlowController state of its own). No
            // option buttons exist yet, so the one real Selectable available
            // is AbortButton — never nothing, never a stale selection.
            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
            Assert.IsTrue(string.IsNullOrEmpty(FindText("Question").text));
            AssertValidSelection("AbortButton");

            yield return new WaitForSeconds(3.2f);

            Assert.IsFalse(string.IsNullOrEmpty(FindText("Question").text));
            AssertValidSelection("Option0");
        }

        [UnityTest]
        public IEnumerator AdvancingToTheNextQuestion_KeepsASelectionOnAnActiveOption()
        {
            yield return LaunchClasico();
            var questionCard = FindRect("QuestionCard");

            FindButton("Option0").onClick.Invoke();
            yield return new WaitForSeconds(1.0f); // past FeedbackDisplaySeconds -> next question rendered

            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
            AssertValidSelection("Option0");
            // Whichever this first answer turned out to be (correct or
            // incorrect — both are possible depending on shuffle), the
            // question card must still be on-screen for the next question.
            AssertQuestionCardIsOnScreen(questionCard);
        }

        [UnityTest]
        public IEnumerator Timeout_StillShowsANewInteractableQuestion()
        {
            // Deterministic incorrect path: no click at all, let the real 8s
            // per-question timeout auto-submit as incorrect
            // (ClasicoGameEngine.Tick -> SubmitAnswer(-1)), same reveal path
            // a wrong click takes.
            yield return LaunchClasico();
            var questionCard = FindRect("QuestionCard");
            var firstQuestion = FindText("Question").text;

            yield return new WaitForSeconds(9.2f); // 8s timeout + ~0.9s reveal + margin

            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
            Assert.AreNotEqual(firstQuestion, FindText("Question").text, "A new question must replace the timed-out one.");
            Assert.IsFalse(string.IsNullOrEmpty(FindText("Question").text));
            AssertQuestionCardIsOnScreen(questionCard);
            AssertActiveOptionsAreInteractable();
            Assert.Greater(FindTimerFillAmount(), 0.5f, "The new question's timer must be freshly reset, not stuck from before.");
        }

        [UnityTest]
        public IEnumerator IncorrectAnswers_StillShowANewInteractableQuestion_AcrossMultipleOccurrences()
        {
            // Real content + real per-question option shuffling means we
            // cannot pick a specific option and *guarantee* it is wrong on a
            // given question without reaching into engine internals this
            // test deliberately does not touch (it only drives the public
            // UI, like every other test in this file). Instead: play an
            // entire session clicking Option0 every time, and check the
            // regression's invariant every time CurrentSession.Incorrect
            // actually ticked up. Across 10 independently-shuffled questions
            // (3-4 options each) it is a near-certainty (see the final
            // assertion, which fails loudly if it somehow was not) that
            // Option0 is wrong at least twice — satisfying "repeat at least
            // two consecutive-or-not incorrect answers" without a flaky
            // artificial setup.
            yield return LaunchClasico();
            var questionCard = FindRect("QuestionCard");

            var incorrectObserved = 0;
            const int maxQuestions = 20;
            var iterations = 0;

            while (_installer.FlowController.State == GameLifecycleState.Playing && iterations < maxQuestions)
            {
                var incorrectBefore = _installer.FlowController.CurrentSession.Incorrect;
                var questionBefore = FindText("Question").text;

                FindButton("Option0").onClick.Invoke();
                yield return new WaitForSeconds(1.0f); // past FeedbackDisplaySeconds -> next question rendered

                if (_installer.FlowController.State != GameLifecycleState.Playing)
                {
                    break; // reached Results
                }

                if (_installer.FlowController.CurrentSession.Incorrect > incorrectBefore)
                {
                    incorrectObserved++;

                    // This is the exact regression: after an incorrect
                    // answer's feedback/shake finishes, a new, on-screen,
                    // interactable question must be showing — not a blank
                    // screen with a runaway card and a still-ticking timer.
                    Assert.AreNotEqual(questionBefore, FindText("Question").text);
                    Assert.IsFalse(string.IsNullOrEmpty(FindText("Question").text));
                    AssertQuestionCardIsOnScreen(questionCard);
                    AssertActiveOptionsAreInteractable();
                    Assert.Greater(FindTimerFillAmount(), 0.5f, "The new question's timer must be freshly reset, not stuck from before.");
                }

                iterations++;
            }

            Assert.GreaterOrEqual(incorrectObserved, 2, "Expected at least two incorrect answers across a full 10-question session when always picking Option0 — if this fails, the sample content or shuffle changed enough that this needs revisiting, not that the regression itself is unverified (Timeout_StillShowsANewInteractableQuestion above covers the deterministic case).");
        }

        [UnityTest]
        public IEnumerator AnsweringAQuestion_UpdatesScoreOrIncorrectCount()
        {
            yield return LaunchClasico();

            FindButton("Option0").onClick.Invoke();
            yield return null;

            var session = _installer.FlowController.CurrentSession;
            Assert.AreEqual(1, session.Correct + session.Incorrect);
        }

        [UnityTest]
        public IEnumerator AnsweringEveryQuestion_ReachesResults()
        {
            yield return PlayThroughToResults();

            Assert.AreEqual(GameLifecycleState.Results, _installer.FlowController.State);
            Assert.IsTrue(_installer.FlowController.LastResult.Completed);
            Assert.IsFalse(string.IsNullOrEmpty(FindText("ResultsSummary").text));
            AssertValidSelection("RestartButton");
        }

        [UnityTest]
        public IEnumerator Restart_FromResults_StartsAFreshSession_WithAValidSelection()
        {
            yield return PlayThroughToResults();

            FindButton("RestartButton").onClick.Invoke();
            yield return null;

            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
            Assert.AreEqual(0, _installer.FlowController.CurrentSession.Score);

            // The button just clicked (RestartButton) lives on the now-hidden
            // Results panel — selection must have moved on, not be left
            // dangling on a deactivated object. Restart re-enters the 3s
            // countdown, so the immediate landing spot is AbortButton (the
            // only real Selectable available before the first question
            // renders) — see ClasicoHud.RenderCountdown.
            AssertValidSelection("AbortButton");

            yield return new WaitForSeconds(3.2f);
            AssertValidSelection("Option0");
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

        [UnityTest]
        public IEnumerator EnsureEventSystem_NeverCreatesADuplicate()
        {
            // A second installer (e.g. a scene reload in a real game) must reuse
            // the existing DontDestroyOnLoad EventSystem, not spawn another one.
            var secondRoot = new GameObject("GameSessionTest2");
            secondRoot.AddComponent<GameSessionInstaller>();
            yield return null;

            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);

            Object.Destroy(secondRoot);
            yield return null;
        }
    }
}
