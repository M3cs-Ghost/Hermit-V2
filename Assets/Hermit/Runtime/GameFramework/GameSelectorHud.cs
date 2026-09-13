using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Hermit.Games;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Generic game selector — enumerates whatever GameRegistry.All hands it
    /// and renders one card/button per game. Knows GameDefinition's public
    /// contract (GameId, DisplayName, ShortDescription, IsEnabled) and
    /// nothing else: no reference to ClasicoGameEngine, ClasicoHud, or any
    /// other concrete game type anywhere in this file. That absence is the
    /// actual proof of "the selector does not know Clasico" — see
    /// GameSelectorPlayModeTests, which registers a second, unrelated fake
    /// game into this exact class and confirms it renders correctly.
    ///
    /// C7: reads colors/sizes from the shared theme and gained an optional
    /// "Volver" button (see <see cref="SetBackAction"/>) so ShellInstaller can
    /// integrate this screen visually into Shell without GameSessionInstaller
    /// (02_GameplaySandbox, dev/test) needing to show a back button that would
    /// have nowhere meaningful to go.
    ///
    /// Public — like GameSessionInstaller — specifically so PlayMode tests in
    /// a separate assembly can build one directly with fake definitions and
    /// prove it enumerates/launches games generically, with zero Clasico
    /// involvement at all (a stronger isolation than testing it only through
    /// the real catalog).
    /// </summary>
    public sealed class GameSelectorHud : MonoBehaviour, IGameSelectorScreen
    {
        public event Action<GameDefinition> GameLaunchRequested;

        private RectTransform _panel;
        private Transform _listRoot;
        private Text _emptyStateText;
        private Button _backButton;
        private Action _onBack;
        private readonly List<Button> _gameButtons = new List<Button>();

        private HermitTheme Theme => RuntimeUIFactory.Theme;

        public void Build(Transform canvasRoot, IReadOnlyList<GameDefinition> games)
        {
            _panel = RuntimeUIFactory.CreatePanel(canvasRoot, "SelectorPanel", Theme.Background);

            RuntimeUIFactory.CreateText(
                _panel, "Title", "Juegos", Theme.TitleSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(800, 70));

            var listGo = new GameObject("GameList", typeof(RectTransform));
            var listRect = (RectTransform)listGo.transform;
            listRect.SetParent(_panel, false);
            listRect.anchorMin = new Vector2(0.5f, 0.5f);
            listRect.anchorMax = new Vector2(0.5f, 0.5f);
            listRect.anchoredPosition = new Vector2(0, 20);
            listRect.sizeDelta = new Vector2(560, 400);
            _listRoot = listRect;

            _emptyStateText = RuntimeUIFactory.CreateText(
                _panel, "EmptyState", "No hay juegos disponibles.", Theme.BodySize, TextAnchor.MiddleCenter, Theme.TextSecondary,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 60));
            _emptyStateText.gameObject.SetActive(false);

            _backButton = RuntimeUIFactory.CreateButton(
                _panel, "BackButton", "Volver",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(140, 70), new Vector2(180, 52));
            _backButton.onClick.AddListener(() => _onBack?.Invoke());
            _backButton.gameObject.SetActive(false);

            BuildGameButtons(games);
            _panel.gameObject.SetActive(false);
        }

        /// <summary>Shows and wires a "Volver" button. Never called by the
        /// 02_GameplaySandbox dev/test installer — there is nothing meaningful
        /// to go "back" to there, so the button stays hidden by default.</summary>
        public void SetBackAction(Action onBack)
        {
            _onBack = onBack;
            _backButton.gameObject.SetActive(onBack != null);

            if (onBack == null)
            {
                return;
            }

            // Extend the existing vertical chain to include Back as the last
            // stop, rather than leaving it keyboard-unreachable.
            var chain = new List<Selectable>(_gameButtons.Count + 1);
            chain.AddRange(_gameButtons);
            chain.Add(_backButton);
            RuntimeUIFactory.ChainVertical(chain);
        }

        public void Show()
        {
            _panel.gameObject.SetActive(true);

            Button firstEnabled = null;
            foreach (var button in _gameButtons)
            {
                if (button.interactable)
                {
                    firstEnabled = button;
                    break;
                }
            }

            var fallback = _backButton.gameObject.activeSelf ? _backButton : null;
            RuntimeUIFactory.Select(firstEnabled != null ? firstEnabled : fallback);
        }

        public void Hide()
        {
            _panel.gameObject.SetActive(false);
        }

        private void BuildGameButtons(IReadOnlyList<GameDefinition> games)
        {
            if (games.Count == 0)
            {
                _emptyStateText.gameObject.SetActive(true);
                return;
            }

            const float buttonHeight = 84f;
            const float spacing = 20f;
            var startY = ((games.Count - 1) * (buttonHeight + spacing)) / 2f;

            for (var i = 0; i < games.Count; i++)
            {
                var definition = games[i];
                var y = startY - i * (buttonHeight + spacing);

                var label = definition.DisplayName;
                if (!definition.IsEnabled)
                {
                    label += " (próximamente)";
                }
                else if (!string.IsNullOrEmpty(definition.ShortDescription))
                {
                    label += $"\n{definition.ShortDescription}";
                }

                var button = RuntimeUIFactory.CreateButton(
                    _listRoot, $"Game_{definition.GameId}", label,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(520, buttonHeight));
                button.interactable = definition.IsEnabled;

                var labelText = button.GetComponentInChildren<Text>();
                labelText.fontSize = Theme.BodySize;

                var capturedDefinition = definition;
                button.onClick.AddListener(() =>
                {
                    if (capturedDefinition.IsEnabled)
                    {
                        GameLaunchRequested?.Invoke(capturedDefinition);
                    }
                });

                _gameButtons.Add(button);
            }

            RuntimeUIFactory.ChainVertical(_gameButtons);
        }
    }
}
