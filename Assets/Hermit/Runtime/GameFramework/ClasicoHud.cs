using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Hermit.Games;
using Hermit.Games.Clasico;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Presentation only. Builds Clasico's two in-game screens (Playing /
    /// Results) and exposes them as plain render calls plus input events — it
    /// never touches GameFlowController, GameRegistry, or any Networking type
    /// directly. <see cref="ClasicoGameHost"/> is the only thing that wires
    /// this to the framework.
    ///
    /// C6 removed the Idle screen/"Jugar" button this used to own in C5 — the
    /// game selector (<see cref="GameSelectorHud"/>) is now the single place a
    /// player chooses to play Clasico, so a second, game-local "start" screen
    /// would have been a redundant step. Both panels below start inactive;
    /// ClasicoGameHost.Show() activates Playing immediately.
    ///
    /// This is where "UI concreta" lives, deliberately outside Hermit.Games —
    /// see Docs/C5_GAME_FRAMEWORK.md, "Where the UI lives".
    /// </summary>
    internal sealed class ClasicoHud : MonoBehaviour
    {
        private const int MaxOptionButtons = 4;

        public event Action<int> AnswerSelected;
        public event Action AbortRequested;
        public event Action RestartRequested;
        public event Action ExitToIdleRequested;

        private RectTransform _playingPanel;
        private RectTransform _resultsPanel;

        private Text _progressText;
        private Text _scoreText;
        private Text _questionText;
        private Text _feedbackText;
        private readonly List<Button> _optionButtons = new List<Button>();
        private readonly List<Text> _optionLabels = new List<Text>();
        private readonly List<Image> _optionImages = new List<Image>();

        private Text _resultsTitleText;
        private Text _resultsSummaryText;
        private Button _restartButton;
        private Button _exitButton;

        private static readonly Color NeutralOptionColor = new Color(0.16f, 0.19f, 0.24f, 1f);
        private static readonly Color CorrectOptionColor = new Color(0.13f, 0.45f, 0.20f, 1f);
        private static readonly Color IncorrectOptionColor = new Color(0.55f, 0.15f, 0.15f, 1f);

        public void Build(Transform canvasRoot)
        {
            BuildPlayingPanel(canvasRoot);
            BuildResultsPanel(canvasRoot);
            _playingPanel.gameObject.SetActive(false);
            _resultsPanel.gameObject.SetActive(false);
        }

        public void ShowPlaying()
        {
            _playingPanel.gameObject.SetActive(true);
            _resultsPanel.gameObject.SetActive(false);
            _feedbackText.text = string.Empty;
        }

        public void Hide()
        {
            _playingPanel.gameObject.SetActive(false);
            _resultsPanel.gameObject.SetActive(false);
        }

        public void ShowResults(GameResult result)
        {
            _playingPanel.gameObject.SetActive(false);
            _resultsPanel.gameObject.SetActive(true);

            _resultsTitleText.text = result.Completed ? "Resultados" : "Partida abandonada";
            _resultsSummaryText.text =
                $"Puntaje: {result.Score}\n" +
                $"Correctas: {result.Correct}\n" +
                $"Incorrectas: {result.Incorrect}\n" +
                $"Precisión: {result.AccuracyPercent:0.0}%\n" +
                $"Duración: {result.DurationSeconds:0.0}s";

            RuntimeUIFactory.Select(_restartButton);
        }

        public void RenderQuestion(ClasicoQuestionView view, int score)
        {
            _progressText.text = $"Pregunta {view.QuestionNumber}/{view.TotalQuestions}";
            _scoreText.text = $"Puntaje: {score}";
            _questionText.text = view.PromptText;
            _feedbackText.text = string.Empty;

            Button firstActiveOption = null;
            for (var i = 0; i < _optionButtons.Count; i++)
            {
                var hasOption = i < view.OptionTexts.Count;
                _optionButtons[i].gameObject.SetActive(hasOption);
                if (!hasOption)
                {
                    continue;
                }

                _optionLabels[i].text = view.OptionTexts[i];
                _optionImages[i].color = NeutralOptionColor;
                _optionButtons[i].interactable = true;
                firstActiveOption ??= _optionButtons[i];
            }

            // A fresh question means a fresh EventSystem selection — without this,
            // keyboard Navigate/Submit have nothing to operate from the moment a
            // new question appears (see Docs/C5_GAME_FRAMEWORK.md, "Keyboard
            // navigation fix"). Only called once per question by the host,
            // never every frame, so it never fights the player's own navigation
            // within the same question.
            RuntimeUIFactory.Select(firstActiveOption);
        }

        public void RenderReveal(int selectedIndex, int correctIndex, int score)
        {
            _scoreText.text = $"Puntaje: {score}";

            for (var i = 0; i < _optionButtons.Count; i++)
            {
                if (!_optionButtons[i].gameObject.activeSelf)
                {
                    continue;
                }

                _optionButtons[i].interactable = false;
                if (i == correctIndex)
                {
                    _optionImages[i].color = CorrectOptionColor;
                }
                else if (i == selectedIndex)
                {
                    _optionImages[i].color = IncorrectOptionColor;
                }
            }

            _feedbackText.text = selectedIndex == correctIndex ? "¡Correcto!" : "Incorrecto";
            _feedbackText.color = selectedIndex == correctIndex ? Color.green : Color.red;
        }

        private void BuildPlayingPanel(Transform canvasRoot)
        {
            _playingPanel = RuntimeUIFactory.CreatePanel(canvasRoot, "PlayingPanel", new Color(0.08f, 0.09f, 0.12f, 1f));

            _progressText = RuntimeUIFactory.CreateText(
                _playingPanel, "Progress", "Pregunta 1/10", 22, TextAnchor.MiddleLeft, Color.white,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140, -40), new Vector2(240, 40));

            _scoreText = RuntimeUIFactory.CreateText(
                _playingPanel, "Score", "Puntaje: 0", 22, TextAnchor.MiddleRight, Color.white,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-140, -40), new Vector2(240, 40));

            _questionText = RuntimeUIFactory.CreateText(
                _playingPanel, "Question", string.Empty, 30, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(900, 140));

            var abortButton = RuntimeUIFactory.CreateButton(
                _playingPanel, "AbortButton", "Salir",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60, -100), new Vector2(120, 44));
            abortButton.onClick.AddListener(() => AbortRequested?.Invoke());

            const float buttonWidth = 420f;
            const float buttonHeight = 56f;
            const float spacing = 16f;
            var startY = -20f;

            for (var i = 0; i < MaxOptionButtons; i++)
            {
                var index = i;
                var y = startY - i * (buttonHeight + spacing);
                var button = RuntimeUIFactory.CreateButton(
                    _playingPanel, $"Option{i}", string.Empty,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(buttonWidth, buttonHeight));
                button.onClick.AddListener(() => AnswerSelected?.Invoke(index));

                _optionButtons.Add(button);
                _optionLabels.Add(button.GetComponentInChildren<Text>());
                _optionImages.Add(button.GetComponent<Image>());
            }

            // Explicit Up/Down chain between the option buttons rather than
            // relying on Selectable's spatial "Automatic" navigation — simple,
            // predictable, and immune to the Abort button (positioned well away
            // from the options) being picked as a confusing neighbor.
            RuntimeUIFactory.ChainVertical(_optionButtons.ToArray());

            _feedbackText = RuntimeUIFactory.CreateText(
                _playingPanel, "Feedback", string.Empty, 24, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -280), new Vector2(600, 40));
        }

        private void BuildResultsPanel(Transform canvasRoot)
        {
            _resultsPanel = RuntimeUIFactory.CreatePanel(canvasRoot, "ResultsPanel", new Color(0.08f, 0.09f, 0.12f, 1f));

            _resultsTitleText = RuntimeUIFactory.CreateText(
                _resultsPanel, "ResultsTitle", "Resultados", 44, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(600, 60));

            _resultsSummaryText = RuntimeUIFactory.CreateText(
                _resultsPanel, "ResultsSummary", string.Empty, 24, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(500, 220));

            _restartButton = RuntimeUIFactory.CreateButton(
                _resultsPanel, "RestartButton", "Reintentar",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-130, -140), new Vector2(220, 56));
            _restartButton.onClick.AddListener(() => RestartRequested?.Invoke());

            _exitButton = RuntimeUIFactory.CreateButton(
                _resultsPanel, "ExitButton", "Salir",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(130, -140), new Vector2(220, 56));
            _exitButton.onClick.AddListener(() => ExitToIdleRequested?.Invoke());

            RuntimeUIFactory.ChainHorizontal(_restartButton, _exitButton);
        }
    }
}
