using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Hermit.Games;
using Hermit.Runtime.GameFramework;
using Hermit.Tests.PlayMode.Fakes;

namespace Hermit.Tests.PlayMode
{
    /// <summary>
    /// Proves the real GameSelectorHud enumerates and launches games
    /// generically — built here with two entirely fake games, zero Clasico
    /// involvement, zero reference to GameCatalog.asset. This is the
    /// selector-level equivalent of GameFrameworkExtensibilityTests: if
    /// making this pass had required editing GameSelectorHud with a
    /// Clasico-specific branch, it would not compile as written.
    /// </summary>
    public class GameSelectorPlayModeTests
    {
        private GameObject _root;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }

            yield return null;
        }

        private GameSelectorHud BuildSelector(params GameDefinition[] games)
        {
            _root = new GameObject("SelectorTest");
            var hud = _root.AddComponent<GameSelectorHud>();
            hud.Build(_root.transform, games);
            hud.Show();
            return hud;
        }

        private Button FindButton(string name) => _root.GetComponentsInChildren<Button>(true).First(b => b.name == name);

        [UnityTest]
        public IEnumerator Selector_RendersOneButtonPerRegisteredGame()
        {
            var gameA = SecondGameDefinition.CreateInMemory("game_a", "Game A");
            var gameB = SecondGameDefinition.CreateInMemory("game_b", "Game B");
            BuildSelector(gameA, gameB);
            yield return null;

            Assert.IsNotNull(FindButton("Game_game_a"));
            Assert.IsNotNull(FindButton("Game_game_b"));
        }

        [UnityTest]
        public IEnumerator ClickingAGameButton_FiresGameLaunchRequested_WithTheCorrectDefinition()
        {
            var gameA = SecondGameDefinition.CreateInMemory("game_a", "Game A");
            var gameB = SecondGameDefinition.CreateInMemory("game_b", "Game B");
            var hud = BuildSelector(gameA, gameB);
            yield return null;

            GameDefinition launched = null;
            hud.GameLaunchRequested += d => launched = d;

            FindButton("Game_game_b").onClick.Invoke();

            Assert.AreSame(gameB, launched);
        }

        [UnityTest]
        public IEnumerator DisabledGame_DoesNotFireLaunch_WhenClicked()
        {
            var disabled = SecondGameDefinition.CreateInMemory("disabled_game", "Disabled", isEnabled: false);
            var hud = BuildSelector(disabled);
            yield return null;

            var fired = false;
            hud.GameLaunchRequested += _ => fired = true;

            var button = FindButton("Game_disabled_game");
            Assert.IsFalse(button.interactable, "A disabled game's button must not be interactable.");

            button.onClick.Invoke();
            Assert.IsFalse(fired, "A disabled game must never fire GameLaunchRequested, even if onClick is invoked directly.");
        }

        [UnityTest]
        public IEnumerator EmptyRegistry_BuildsWithoutCrashing_AndShowsNoButtons()
        {
            BuildSelector();
            yield return null;

            Assert.AreEqual(0, _root.GetComponentsInChildren<Button>(true).Length);
        }
    }
}
