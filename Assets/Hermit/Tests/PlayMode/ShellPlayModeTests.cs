using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Hermit.Games;
using Hermit.Runtime;
using Hermit.Runtime.GameFramework;

namespace Hermit.Tests.PlayMode
{
    /// <summary>
    /// Exercises the real, composed C7 Shell (ShellInstaller + ShellHomeHud +
    /// the same GameHub/GameSelectorHud/ClasicoGameHost stack
    /// GameSessionInstaller uses) end to end, driven by invoking the actual
    /// UI Button.onClick events a player's click would fire. Covers the full
    /// flow from the C7 brief: Shell Home -> Juegos -> Selector -> Clásico ->
    /// Results -> Selector -> Shell.
    /// </summary>
    public class ShellPlayModeTests
    {
        private GameObject _root;
        private ShellInstaller _installer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("ShellTest");
            _installer = _root.AddComponent<ShellInstaller>();
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

        [UnityTest]
        public IEnumerator ShellInstaller_BuildsTheUI_StartingOnTheHomeScreen()
        {
            Assert.AreEqual(GameLifecycleState.Idle, _installer.FlowController.State);
            Assert.IsNotNull(FindButton("JuegosButton"));
            AssertValidSelection("JuegosButton");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ClickingJuegos_ShowsTheSelector_WithClasicoListed()
        {
            FindButton("JuegosButton").onClick.Invoke();
            yield return null;

            Assert.IsFalse(FindButton("JuegosButton").gameObject.activeInHierarchy, "Home must hide once the selector is shown.");
            Assert.IsNotNull(FindButton("Game_clasico"));
            AssertValidSelection("Game_clasico");
        }

        [UnityTest]
        public IEnumerator SelectorBack_ReturnsToHome()
        {
            FindButton("JuegosButton").onClick.Invoke();
            yield return null;

            FindButton("BackButton").onClick.Invoke();
            yield return null;

            Assert.IsTrue(FindButton("JuegosButton").gameObject.activeInHierarchy);
            AssertValidSelection("JuegosButton");
        }

        [UnityTest]
        public IEnumerator LaunchingClasicoFromShell_StartsAGame()
        {
            FindButton("JuegosButton").onClick.Invoke();
            yield return null;

            FindButton("Game_clasico").onClick.Invoke();
            yield return new WaitForSeconds(3.2f); // past the countdown

            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
            Assert.IsFalse(string.IsNullOrEmpty(FindText("Question").text));
            AssertValidSelection("Option0");
        }

        [UnityTest]
        public IEnumerator FullLoop_ShellToClasicoToResultsToSelectorToShell()
        {
            // Shell -> Juegos -> Selector -> Clásico
            FindButton("JuegosButton").onClick.Invoke();
            yield return null;
            FindButton("Game_clasico").onClick.Invoke();
            yield return new WaitForSeconds(3.2f);

            // Play through to Results.
            const int maxQuestions = 20;
            var iterations = 0;
            while (_installer.FlowController.State == GameLifecycleState.Playing && iterations < maxQuestions)
            {
                FindButton("Option0").onClick.Invoke();
                yield return new WaitForSeconds(1.0f);
                iterations++;
            }

            Assert.AreEqual(GameLifecycleState.Results, _installer.FlowController.State);

            // Results -> Selector.
            FindButton("ExitButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual(GameLifecycleState.Idle, _installer.FlowController.State);
            Assert.IsNotNull(FindButton("Game_clasico"));
            AssertValidSelection("Game_clasico");

            // Selector -> Shell.
            FindButton("BackButton").onClick.Invoke();
            yield return null;
            Assert.IsTrue(FindButton("JuegosButton").gameObject.activeInHierarchy);
            AssertValidSelection("JuegosButton");
        }
        [UnityTest]
        public IEnumerator F1DebugPanelToggle_DoesNotInterfereWithShell()
        {
            var debugPanel = Object.FindFirstObjectByType<C4DebugPanel>();

            Assert.IsNotNull(
                debugPanel,
                "C4DebugPanel should exist because HermitRuntimeInstaller runs globally during PlayMode."
            );

            Assert.IsTrue(
                FindButton("JuegosButton").gameObject.activeInHierarchy,
                "Shell home must remain usable while C4DebugPanel exists."
            );

            AssertValidSelection("JuegosButton");

            yield return null;
        }
    }
}
