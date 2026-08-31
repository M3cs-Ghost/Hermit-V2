using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Hermit.Games;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Generic game selector — enumerates whatever GameRegistry.All hands it
    /// and renders one button per game. Knows GameDefinition's public
    /// contract (GameId, DisplayName, ShortDescription, IsEnabled) and
    /// nothing else: no reference to ClasicoGameEngine, ClasicoHud, or any
    /// other concrete game type anywhere in this file. That absence is the
    /// actual proof of "the selector does not know Clasico" — see
    /// GameSelectorPlayModeTests, which registers a second, unrelated fake
    /// game into this exact class and confirms it renders correctly.
    ///
    /// Public — like GameSessionInstaller — specifically so PlayMode tests in
    /// a separate assembly can build one directly with fake definitions and
    /// prove it enumerates/launches games generically, with zero Clasico
    /// involvement at all (a stronger isolation than testing it only through
    /// the real catalog).
    /// </summary>
    public sealed class GameSelectorHud : MonoBehaviour
    {
        public event Action<GameDefinition> GameLaunchRequested;

        private RectTransform _panel;
        private Transform _listRoot;
        private Text _emptyStateText;
        private readonly List<Button> _gameButtons = new List<Button>();

        public void Build(Transform canvasRoot, IReadOnlyList<GameDefinition> games)
        {
            _panel = RuntimeUIFactory.CreatePanel(canvasRoot, "SelectorPanel", new Color(0.08f, 0.09f, 0.12f, 1f));

            RuntimeUIFactory.CreateText(
                _panel, "Title", "Hermit — Selecciona un juego", 40, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(800, 60));

            var listGo = new GameObject("GameList", typeof(RectTransform));
            var listRect = (RectTransform)listGo.transform;
            listRect.SetParent(_panel, false);
            listRect.anchorMin = new Vector2(0.5f, 0.5f);
            listRect.anchorMax = new Vector2(0.5f, 0.5f);
            listRect.anchoredPosition = Vector2.zero;
            listRect.sizeDelta = new Vector2(560, 400);
            _listRoot = listRect;

            _emptyStateText = RuntimeUIFactory.CreateText(
                _panel, "EmptyState", "No hay juegos disponibles.", 24, TextAnchor.MiddleCenter,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 60));
            _emptyStateText.gameObject.SetActive(false);

            BuildGameButtons(games);
            _panel.gameObject.SetActive(false);
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

            RuntimeUIFactory.Select(firstEnabled);
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

            const float buttonHeight = 72f;
            const float spacing = 18f;
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

                var button = RuntimeUIFactory.CreateButton(
                    _listRoot, $"Game_{definition.GameId}", label,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(520, buttonHeight));
                button.interactable = definition.IsEnabled;

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
