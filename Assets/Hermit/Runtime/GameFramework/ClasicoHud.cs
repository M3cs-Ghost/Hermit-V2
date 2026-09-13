using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Hermit.Games;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// C8.1: Clasico's outer "Hermit chrome" — score, streak, decision timer,
    /// the big command word, correct/incorrect feedback text, the abort/
    /// results panel, and the punch/shake/transition motion — everything the
    /// C8.0 Design Lock calls "what never changes, no matter the world"
    /// (section S). It owns exactly one thing a presenter builds into:
    /// <see cref="StageRoot"/>. It has never heard of Western Shootout,
    /// TV Game Show, Balance Machine, or Detective Lineup — those live in
    /// Hermit.Runtime.GameFramework.Microgames, wired together only by
    /// <see cref="ClasicoGameHost"/>.
    ///
    /// Pre-C8.1 this class also built the single Q&amp;A question card and its
    /// four option buttons directly — that entire per-question rendering
    /// path is gone, replaced by the presenter swap <see cref="ClasicoGameHost"/>
    /// drives. See Docs/C8_1_GOLD_MICROGAME_SLICE.md, "Migration strategy".
    /// </summary>
    internal sealed class ClasicoHud : MonoBehaviour
    {
        public event Action AbortRequested;
        public event Action RestartRequested;
        public event Action ExitToIdleRequested;

        private RectTransform _playingPanel;
        private RectTransform _resultsPanel;
        private RectTransform _stageRoot;
        private Text _countdownText;
        private Text _commandText;
        private Button _abortButton;

        private Text _progressText;
        private Text _scoreText;
        private Text _streakText;
        private Text _feedbackText;
        private Image _timerFill;

        private Text _resultsTitleText;
        private Text _resultsSummaryText;
        private Button _restartButton;
        private Button _exitButton;

        private RectTransform _transitionOverlay;
        private Image _transitionImage;
        private CanvasGroup _transitionGroup;

        /// <summary>Reserved vertical band, measured down from the top of
        /// the whole playing area, that only the Top HUD row (Progress/
        /// Streak/Score) and the Timer bar are allowed to occupy — no
        /// presenter's own "concept" content may start above this line.
        /// Enforced once, generally, by insetting <see cref="StageRoot"/>
        /// from the top by exactly this amount (see <see cref="BuildPlayingPanel"/>)
        /// rather than by trusting every current and future presenter to
        /// individually leave enough clearance. Found overlapping in C8.1
        /// manual validation: Western's "Concept", Detective's "GroupLabel",
        /// and Balance's "Equation" text all previously sat inside the
        /// Timer bar's own vertical span, all top-anchored (0.5, 1) to their
        /// own root panel — which used to stretch to the full stage,
        /// starting right at the true top of the screen.</summary>
        private const float TopHudReservedHeight = 112f;

        private Coroutine _punchRoutine;
        private Coroutine _shakeRoutine;
        private Coroutine _transitionRoutine;

        // C8.1b: Clásico's shared audio identity — every clip synthesized
        // once via ProceduralAudio and cached, one AudioSource reused for
        // every play (never Instantiate'd per trigger). No music yet, no
        // per-world accent SFX yet (deferred — see Docs/C8_1B_GOLD_VISUAL_POLISH.md,
        // "Audio"); this is the four shared cues the C8.0 Design Lock's own
        // audio direction calls "world-independent, part of the glue".
        private AudioSource _audioSource;
        private AudioClip _commandClip;
        private AudioClip _correctClip;
        private AudioClip _incorrectClip;
        private AudioClip _transitionClip;

        public Transform StageRoot => _stageRoot;
        private HermitTheme Theme => RuntimeUIFactory.Theme;

        public void Build(Transform canvasRoot)
        {
            BuildPlayingPanel(canvasRoot);
            BuildResultsPanel(canvasRoot);
            _playingPanel.gameObject.SetActive(false);
            _resultsPanel.gameObject.SetActive(false);

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _commandClip = ProceduralAudio.Tone("HermitCommand", 880f, 0.08f, 0.22f);
            _correctClip = ProceduralAudio.Sweep("HermitCorrect", 660f, 990f, 0.16f, 0.26f);
            _incorrectClip = ProceduralAudio.Tone("HermitIncorrect", 180f, 0.18f, 0.24f);
            _transitionClip = ProceduralAudio.Noise("HermitTransition", 0.12f, 0.16f);
        }

        public void ShowPlaying()
        {
            _playingPanel.gameObject.SetActive(true);
            _resultsPanel.gameObject.SetActive(false);
            _feedbackText.text = string.Empty;
            _commandText.gameObject.SetActive(false);
            _countdownText.gameObject.SetActive(false);
        }

        public void Hide()
        {
            _playingPanel.gameObject.SetActive(false);
            _resultsPanel.gameObject.SetActive(false);
        }

        public void RenderCountdown(int secondsRemaining)
        {
            if (!_countdownText.gameObject.activeSelf)
            {
                RuntimeUIFactory.Select(_abortButton);
            }

            _countdownText.gameObject.SetActive(true);
            _countdownText.text = secondsRemaining > 0 ? secondsRemaining.ToString() : "¡YA!";
        }

        public void RenderProgress(int microgameNumber, int totalMicrogames, int score, int streak)
        {
            _countdownText.gameObject.SetActive(false);
            _progressText.text = $"Microjuego {microgameNumber}/{totalMicrogames}";
            _scoreText.text = $"Puntaje: {score}";
            _streakText.text = streak > 0 ? $"Racha: {streak}" : string.Empty;
        }

        public void ShowCommand(string command)
        {
            _feedbackText.text = string.Empty;
            _timerFill.fillAmount = 1f;
            _timerFill.color = Theme.Accent;
            _commandText.text = command;
            _commandText.gameObject.SetActive(true);
            _audioSource.PlayOneShot(_commandClip);
        }

        public void HideCommand()
        {
            _commandText.gameObject.SetActive(false);
        }

        /// <summary>Called every frame during the Decision phase — a cheap
        /// fill-amount/color write, never a rebuild or a selection change.</summary>
        public void RenderTimer(float fraction01)
        {
            _timerFill.fillAmount = fraction01;
            _timerFill.color = fraction01 > 0.5f ? Theme.Accent : fraction01 > 0.25f ? Theme.Warning : Theme.Incorrect;
        }

        /// <summary>C8.1d.9: <paramref name="playAudio"/> (default true, so
        /// every existing caller/archetype is unaffected) lets a caller opt
        /// out of the correct/incorrect ding while keeping every visual
        /// effect (score/streak/feedback text, the global punch/shake) — an
        /// explicit per-call choice, never a global disable, per
        /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.9": Western's own
        /// outlaw red/green tint and hit reaction already communicate
        /// correctness, so its own generic ding was found redundant, while
        /// every other archetype still wants it exactly as before.</summary>
        public void RenderFeedback(bool correct, int score, int streak, int streakBonus, bool playAudio = true)
        {
            _scoreText.text = $"Puntaje: {score}";
            _streakText.text = streak > 0 ? $"Racha: {streak}" : string.Empty;

            _feedbackText.text = correct
                ? streakBonus > 0 ? $"¡Correcto! +{streakBonus} combo" : "¡Correcto!"
                : "Incorrecto";
            _feedbackText.color = correct ? Theme.Correct : Theme.Incorrect;

            if (correct)
            {
                StartPunch();
                if (playAudio)
                {
                    _audioSource.PlayOneShot(_correctClip);
                }
            }
            else
            {
                StartShake();
                if (playAudio)
                {
                    _audioSource.PlayOneShot(_incorrectClip);
                }
            }
        }

        /// <summary>A fast full-screen flash (fade to Theme.Background and
        /// back via CanvasGroup.alpha) over the whole playing panel — the one
        /// signature Hermit transition connecting every world cut (see
        /// Docs/C8_CLASICO_DESIGN_LOCK.md, section W — an approximation of
        /// the radial-wipe direction, chosen after a real Image.Type.Filled/
        /// Radial360 implementation proved to render inconsistently without
        /// an assigned sprite; see Docs/C8_1_GOLD_MICROGAME_SLICE.md for the
        /// investigation). ~0.2s, never restarted mid-flight.</summary>
        public void PlayTransitionCut()
        {
            if (_transitionRoutine != null)
            {
                return;
            }

            _audioSource.PlayOneShot(_transitionClip);
            _transitionRoutine = StartCoroutine(TransitionRoutine(Theme.TransitionDuration));
        }

        public void ShowResults(GameResult result)
        {
            _playingPanel.gameObject.SetActive(false);
            _resultsPanel.gameObject.SetActive(true);

            var cleared = result.Correct + result.Incorrect;
            _resultsTitleText.text = result.Completed ? "Resultados" : "Partida abandonada";
            _resultsSummaryText.text =
                $"Puntaje: {result.Score}\n" +
                $"Precisión: {result.AccuracyPercent:0.0}%\n" +
                $"Microjuegos superados: {cleared}\n" +
                $"Duración: {result.DurationSeconds:0.0}s";

            RuntimeUIFactory.Select(_restartButton);
        }

        private void BuildPlayingPanel(Transform canvasRoot)
        {
            _playingPanel = RuntimeUIFactory.CreatePanel(canvasRoot, "PlayingPanel", Theme.Background);

            // Created first so every presenter's world panel (a sibling
            // added under it, not under _playingPanel) renders behind the
            // HUD chrome created afterward. Stretched to fill the playing
            // area *except* a reserved band at the top (TopHudReservedHeight) —
            // every presenter's own root panel then fully stretches to fill
            // StageRoot (see e.g. WesternShootoutPresenter.Build), so this one
            // inset is what keeps every presenter's top-anchored "concept"
            // content below the Top HUD row and Timer bar, without needing
            // to adjust each presenter's own offsets individually.
            var stageGo = new GameObject("StageRoot", typeof(RectTransform));
            _stageRoot = (RectTransform)stageGo.transform;
            _stageRoot.SetParent(_playingPanel, false);
            _stageRoot.anchorMin = Vector2.zero;
            _stageRoot.anchorMax = Vector2.one;
            _stageRoot.pivot = new Vector2(0.5f, 0.5f);
            _stageRoot.offsetMin = Vector2.zero;
            _stageRoot.offsetMax = new Vector2(0f, -TopHudReservedHeight);

            _progressText = RuntimeUIFactory.CreateText(
                _playingPanel, "Progress", "Microjuego 1/9", Theme.CaptionSize, TextAnchor.MiddleLeft, Theme.TextSecondary,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150, -36), new Vector2(260, 32));

            _streakText = RuntimeUIFactory.CreateText(
                _playingPanel, "Streak", string.Empty, Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.Warning,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(240, 32));

            _scoreText = RuntimeUIFactory.CreateText(
                _playingPanel, "Score", "Puntaje: 0", Theme.CaptionSize, TextAnchor.MiddleRight, Theme.TextPrimary,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150, -36), new Vector2(240, 32));

            // Thinner than the C8.1 prototype (was 14px) — its own reserved
            // band (TopHudReservedHeight) already keeps presenter content
            // clear of it regardless of its exact thickness, but a visibly
            // slimmer bar reads less like a second content row and more like
            // a HUD instrument, per the C8.1 polish request.
            _timerFill = RuntimeUIFactory.CreateFillBar(
                _playingPanel, "TimerBar", Theme.Accent,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -66), new Vector2(900, 8));

            _abortButton = RuntimeUIFactory.CreateButton(
                _playingPanel, "AbortButton", "Salir",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70, -100), new Vector2(120, 44));
            _abortButton.onClick.AddListener(() => AbortRequested?.Invoke());

            _commandText = RuntimeUIFactory.CreateText(
                _playingPanel, "CommandText", string.Empty, Theme.TitleSize, TextAnchor.MiddleCenter, Theme.Accent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(700, 100));
            _commandText.gameObject.SetActive(false);

            _feedbackText = RuntimeUIFactory.CreateText(
                _playingPanel, "Feedback", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(700, 40));

            _countdownText = RuntimeUIFactory.CreateText(
                _playingPanel, "CountdownText", string.Empty, Theme.TitleSize + 20, TextAnchor.MiddleCenter, Theme.Accent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(400, 200));
            _countdownText.gameObject.SetActive(false);

            // A plain opaque panel faded via CanvasGroup.alpha — not
            // Image.Type.Filled/Radial360, which needs a real assigned
            // sprite to compute its fill geometry correctly. This factory
            // never assigns one (RuntimeUIFactory.CreatePanel doesn't set
            // .sprite), and a null sprite on a Filled/Radial graphic proved
            // to render inconsistently between the logical fillAmount value
            // and actual GPU output: CanvasRenderer/Graphic state reported
            // fully correct (not culled, alpha 1) while the real Game View
            // stayed a solid, un-clearing block — this is the actual root
            // cause the C8.1 black-screen investigation found, confirmed by
            // disabling the call and watching the world reappear. A plain
            // alpha fade needs no sprite/fill-geometry at all.
            _transitionOverlay = RuntimeUIFactory.CreatePanel(_playingPanel, "TransitionOverlay", Theme.Background);
            _transitionImage = _transitionOverlay.GetComponent<Image>();
            _transitionImage.raycastTarget = false;
            _transitionGroup = _transitionOverlay.gameObject.AddComponent<CanvasGroup>();
            _transitionGroup.alpha = 0f;
            _transitionGroup.blocksRaycasts = false;
            _transitionGroup.interactable = false;
            _transitionOverlay.SetAsLastSibling();
        }

        private void BuildResultsPanel(Transform canvasRoot)
        {
            _resultsPanel = RuntimeUIFactory.CreatePanel(canvasRoot, "ResultsPanel", Theme.Background);

            var card = RuntimeUIFactory.CreateRoundedPanel(_resultsPanel, "ResultsCard", Theme.Panel);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(620, 400);

            _resultsTitleText = RuntimeUIFactory.CreateText(
                card, "ResultsTitle", "Resultados", Theme.HeadingSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(560, 60));

            _resultsSummaryText = RuntimeUIFactory.CreateText(
                card, "ResultsSummary", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.TextSecondary,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(500, 220));

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

        // --- Global feedback motion — coroutines + Mathf, no tweening
        // package. Deliberately "no-op if already playing", not "stop and
        // restart" — the exact C7 bug class (see Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md,
        // "Bug found during manual validation") was a restart-every-frame
        // accumulating an unbounded position offset. Applied to StageRoot
        // itself so it reads as one shared Hermit reaction regardless of
        // which world is currently active underneath it.

        private void StartPunch()
        {
            if (_punchRoutine != null)
            {
                return;
            }

            _punchRoutine = StartCoroutine(PunchRoutine(Theme.PunchDuration));
        }

        private IEnumerator PunchRoutine(float duration)
        {
            const float peakScale = 1.05f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var scale = 1f + Mathf.Sin(t * Mathf.PI) * (peakScale - 1f);
                _stageRoot.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            _stageRoot.localScale = Vector3.one;
            _punchRoutine = null;
        }

        private void StartShake()
        {
            if (_shakeRoutine != null)
            {
                return;
            }

            _shakeRoutine = StartCoroutine(ShakeRoutine(Theme.ShakeDuration));
        }

        private IEnumerator ShakeRoutine(float duration)
        {
            const float amplitude = 12f;
            var basePosition = _stageRoot.anchoredPosition;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                var damping = 1f - t;
                var offset = Mathf.Sin(t * Mathf.PI * 10f) * amplitude * damping;
                _stageRoot.anchoredPosition = basePosition + new Vector2(offset, 0f);
                yield return null;
            }

            _stageRoot.anchoredPosition = basePosition;
            _shakeRoutine = null;
        }

        private IEnumerator TransitionRoutine(float duration)
        {
            var half = duration * 0.5f;
            var elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                _transitionGroup.alpha = Mathf.Clamp01(elapsed / half);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                _transitionGroup.alpha = 1f - Mathf.Clamp01(elapsed / half);
                yield return null;
            }

            _transitionGroup.alpha = 0f;
            _transitionRoutine = null;
        }
    }
}
