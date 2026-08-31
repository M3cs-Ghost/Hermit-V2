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
            yield return null;
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
        public IEnumerator AdvancingToTheNextQuestion_KeepsASelectionOnAnActiveOption()
        {
            yield return LaunchClasico();

            FindButton("Option0").onClick.Invoke();
            yield return new WaitForSeconds(1.0f); // past FeedbackDisplaySeconds -> next question rendered

            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
            AssertValidSelection("Option0");
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
            // Results panel — selection must have moved to the new Playing
            // panel's first option, not be left dangling on a deactivated object.
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
