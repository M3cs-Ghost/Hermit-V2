using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Hermit.Games;
using Hermit.Games.Clasico;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Presentation only. Builds Clasico's in-game screens (Playing / Results)
    /// and exposes them as plain render calls plus input events — it never
    /// touches GameFlowController, GameRegistry, or any Networking type
    /// directly. <see cref="ClasicoGameHost"/> is the only thing that wires
    /// this to the framework.
    ///
    /// C7 redesign: reads every color/size from <see cref="RuntimeUIFactory.Theme"/>
    /// instead of hardcoded literals, adds a countdown overlay, a timer fill
    /// bar, a streak readout, and lightweight punch/shake/fade feedback —
    /// all driven by coroutines + Mathf (no tweening package). The functional
    /// round-rhythm phases themselves (Countdown/AwaitingAnswer/Revealing)
    /// live in ClasicoGameEngine; this class only ever translates engine state
    /// into pixels and animation, never decides game logic.
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
        private RectTransform _questionCard;
        private CanvasGroup _questionCardGroup;
        private Text _countdownText;
        private Button _abortButton;

        private Text _progressText;
        private Text _scoreText;
        private Text _streakText;
        private Text _questionText;
        private Text _feedbackText;
        private Image _timerFill;
        private readonly List<Button> _optionButtons = new List<Button>();
        private readonly List<Text> _optionLabels = new List<Text>();
        private readonly List<Image> _optionImages = new List<Image>();

        private Text _resultsTitleText;
        private Text _resultsSummaryText;
        private Button _restartButton;
        private Button _exitButton;

        private Coroutine _punchRoutine;
        private Coroutine _shakeRoutine;
        private Coroutine _fadeRoutine;

        private HermitTheme Theme => RuntimeUIFactory.Theme;

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
            _countdownText.gameObject.SetActive(false);
            _questionCardGroup.alpha = 1f;
        }

        public void Hide()
        {
            _playingPanel.gameObject.SetActive(false);
            _resultsPanel.gameObject.SetActive(false);
        }

        public void RenderCountdown(int secondsRemaining)
        {
            // No option buttons exist yet during the countdown — leaving
            // whatever was selected before (possibly a now-hidden Results
            // button, e.g. right after Restart) would repeat the C5 "selection
            // on a deactivated object" bug. Abort is the one real, meaningful
            // Selectable available at this point (a player can bail out even
            // mid-countdown) — select it exactly once, on the frame the
            // countdown overlay first appears, not every frame (so it never
            // fights a player who deliberately navigates away from it).
            if (!_countdownText.gameObject.activeSelf)
            {
                RuntimeUIFactory.Select(_abortButton);
            }

            _questionCardGroup.alpha = 0f;
            _countdownText.gameObject.SetActive(true);
            _countdownText.text = secondsRemaining > 0 ? secondsRemaining.ToString() : "¡YA!";
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
                $"Mejor racha: {result.BestStreak}\n" +
                $"Duración: {result.DurationSeconds:0.0}s";

            RuntimeUIFactory.Select(_restartButton);
        }

        public void RenderQuestion(ClasicoQuestionView view, int score, int streak)
        {
            _countdownText.gameObject.SetActive(false);
            _progressText.text = $"Pregunta {view.QuestionNumber}/{view.TotalQuestions}";
            _scoreText.text = $"Puntaje: {score}";
            _streakText.text = streak > 0 ? $"Racha: {streak}" : string.Empty;
            _questionText.text = view.PromptText;
            _feedbackText.text = string.Empty;
            _timerFill.fillAmount = 1f;
            _timerFill.color = Theme.Accent;

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
                _optionImages[i].color = Theme.PanelRaised;
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

            StartFade(_questionCardGroup, 0f, 1f, Theme.TransitionDuration);
        }

        /// <summary>Called every frame while awaiting an answer — cheap fill-bar
        /// update only, no rebuild, no selection change, so it never fights
        /// keyboard navigation within the question.</summary>
        public void RenderTimer(float fraction01)
        {
            _timerFill.fillAmount = fraction01;
            _timerFill.color = fraction01 > 0.5f ? Theme.Accent : fraction01 > 0.25f ? Theme.Warning : Theme.Incorrect;
        }

        public void RenderReveal(int selectedIndex, int correctIndex, int score, int streakBonus, int streak)
        {
            _scoreText.text = $"Puntaje: {score}";
            _streakText.text = streak > 0 ? $"Racha: {streak}" : string.Empty;

            for (var i = 0; i < _optionButtons.Count; i++)
            {
                if (!_optionButtons[i].gameObject.activeSelf)
                {
                    continue;
                }

                _optionButtons[i].interactable = false;
                if (i == correctIndex)
                {
                    _optionImages[i].color = Theme.Correct;
                }
                else if (i == selectedIndex)
                {
                    _optionImages[i].color = Theme.Incorrect;
                }
            }

            var wasCorrect = selectedIndex == correctIndex;
            _feedbackText.text = wasCorrect
                ? streakBonus > 0 ? $"¡Correcto! +{streakBonus} combo" : "¡Correcto!"
                : "Incorrecto";
            _feedbackText.color = wasCorrect ? Theme.Correct : Theme.Incorrect;

            if (wasCorrect)
            {
                StartPunch(_questionCard);
            }
            else
            {
                StartShake(_questionCard);
            }
        }

        private void BuildPlayingPanel(Transform canvasRoot)
        {
            _playingPanel = RuntimeUIFactory.CreatePanel(canvasRoot, "PlayingPanel", Theme.Background);

            _progressText = RuntimeUIFactory.CreateText(
                _playingPanel, "Progress", "Pregunta 1/10", Theme.CaptionSize, TextAnchor.MiddleLeft, Theme.TextSecondary,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150, -36), new Vector2(240, 32));

            _streakText = RuntimeUIFactory.CreateText(
                _playingPanel, "Streak", string.Empty, Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.Warning,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(240, 32));

            _scoreText = RuntimeUIFactory.CreateText(
                _playingPanel, "Score", "Puntaje: 0", Theme.CaptionSize, TextAnchor.MiddleRight, Theme.TextPrimary,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150, -36), new Vector2(240, 32));

            _timerFill = RuntimeUIFactory.CreateFillBar(
                _playingPanel, "TimerBar", Theme.Accent,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -66), new Vector2(900, 14));

            _abortButton = RuntimeUIFactory.CreateButton(
                _playingPanel, "AbortButton", "Salir",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70, -100), new Vector2(120, 44));
            _abortButton.onClick.AddListener(() => AbortRequested?.Invoke());

            _questionCard = RuntimeUIFactory.CreateRoundedPanel(_playingPanel, "QuestionCard", Theme.Panel);
            _questionCard.anchorMin = new Vector2(0.5f, 0.5f);
            _questionCard.anchorMax = new Vector2(0.5f, 0.5f);
            _questionCard.anchoredPosition = new Vector2(0, -10);
            _questionCard.sizeDelta = new Vector2(980, 560);
            _questionCardGroup = _questionCard.gameObject.AddComponent<CanvasGroup>();

            _questionText = RuntimeUIFactory.CreateText(
                _questionCard, "Question", string.Empty, Theme.HeadingSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(880, 160));

            const float buttonWidth = 860f;
            const float buttonHeight = 56f;
            const float spacing = 16f;
            var startY = 40f;

            for (var i = 0; i < MaxOptionButtons; i++)
            {
                var index = i;
                var y = startY - i * (buttonHeight + spacing);
                var button = RuntimeUIFactory.CreateButton(
                    _questionCard, $"Option{i}", string.Empty,
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
                _questionCard, "Feedback", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(700, 40));

            _countdownText = RuntimeUIFactory.CreateText(
                _playingPanel, "CountdownText", string.Empty, Theme.TitleSize + 20, TextAnchor.MiddleCenter, Theme.Accent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(400, 200));
            _countdownText.gameObject.SetActive(false);
        }

        private void BuildResultsPanel(Transform canvasRoot)
        {
            _resultsPanel = RuntimeUIFactory.CreatePanel(canvasRoot, "ResultsPanel", Theme.Background);

            var card = RuntimeUIFactory.CreateRoundedPanel(_resultsPanel, "ResultsCard", Theme.Panel);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(620, 460);

            _resultsTitleText = RuntimeUIFactory.CreateText(
                card, "ResultsTitle", "Resultados", Theme.HeadingSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(560, 60));

            _resultsSummaryText = RuntimeUIFactory.CreateText(
                card, "ResultsSummary", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.TextSecondary,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(500, 260));

            _restartButton = RuntimeUIFactory.CreateButton(
                card, "RestartButton", "Reintentar",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-130, 60), new Vector2(220, 56));
            _restartButton.onClick.AddListener(() => RestartRequested?.Invoke());

            _exitButton = RuntimeUIFactory.CreateButton(
                card, "ExitButton", "Volver",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(130, 60), new Vector2(220, 56));
            _exitButton.onClick.AddListener(() => ExitToIdleRequested?.Invoke());

            RuntimeUIFactory.ChainHorizontal(_restartButton, _exitButton);
        }

        // --- Lightweight feedback animation — coroutines + Mathf, no tweening
        // package (see Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md, "Animation
        // policy"). Each one is short, self-contained, and always leaves the
        // target in a clean resting state even if interrupted by another call.

        private void StartPunch(RectTransform target)
        {
            // Deliberately a no-op if one is already playing, not a
            // stop-and-restart — a restart would re-capture a fresh base
            // state from the target's *current* (already mid-animation)
            // transform and abandon the old routine before it reached its own
            // cleanup line. That exact pattern in StartShake was the root
            // cause of a real bug: RenderReveal used to be called every frame
            // during the reveal window, so the shake coroutine was restarted
            // every frame, each time capturing a new base position from an
            // already-shifted one, accumulating an unbounded offset that flung
            // the question card off-screen. RenderReveal is now called at most
            // once per reveal (see ClasicoGameHost), so this should never
            // actually re-enter mid-animation — this guard is defense in
            // depth so a routine always reaches its own cleanup and restores
            // a clean resting state.
            if (_punchRoutine != null)
            {
                return;
            }

            _punchRoutine = StartCoroutine(PunchRoutine(target, Theme.PunchDuration));
        }

        private IEnumerator PunchRoutine(RectTransform target, float duration)
        {
            const float peakScale = 1.08f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var scale = 1f + Mathf.Sin(t * Mathf.PI) * (peakScale - 1f);
                target.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            target.localScale = Vector3.one;
            _punchRoutine = null;
        }

        private void StartShake(RectTransform target)
        {
            // See StartPunch's comment — same reasoning, and this is the
            // routine that actually caused the bug (an *additive* position
            // offset accumulates when restarted; a scale computed fresh from
            // elapsed time, like PunchRoutine's, does not).
            if (_shakeRoutine != null)
            {
                return;
            }

            _shakeRoutine = StartCoroutine(ShakeRoutine(target, Theme.ShakeDuration));
        }

        private IEnumerator ShakeRoutine(RectTransform target, float duration)
        {
            const float amplitude = 14f;
            var basePosition = target.anchoredPosition;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                var damping = 1f - t;
                var offset = Mathf.Sin(t * Mathf.PI * 10f) * amplitude * damping;
                target.anchoredPosition = basePosition + new Vector2(offset, 0f);
                yield return null;
            }

            target.anchoredPosition = basePosition;
            _shakeRoutine = null;
        }

        private void StartFade(CanvasGroup group, float from, float to, float duration)
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
            }

            _fadeRoutine = StartCoroutine(FadeRoutine(group, from, to, duration));
        }

        private IEnumerator FadeRoutine(CanvasGroup group, float from, float to, float duration)
        {
            group.alpha = from;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = to;
            _fadeRoutine = null;
        }
    }
}
