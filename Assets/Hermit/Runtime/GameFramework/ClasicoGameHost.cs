using Hermit.Games;
using Hermit.Games.Clasico;
using Hermit.Games.Clasico.Microgames;
using Hermit.Runtime.GameFramework.Microgames;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Clasico-specific adapter between the generic GameFlowController and
    /// the outer <see cref="ClasicoHud"/> chrome plus the four Gold
    /// microgame presenters — the "adapter específico de Clásico" the C6
    /// brief allows. GameSelectorHud and the composition root's generic
    /// plumbing never see this type, <see cref="ClasicoSessionDirector"/>, or
    /// any presenter; only the one dictionary entry in
    /// GameSessionInstaller/ShellInstaller does.
    ///
    /// The only branching here is presentation routing — "which presenter
    /// shows this archetype" — never game logic (that lives entirely in
    /// <see cref="ClasicoSessionDirector"/> and the per-archetype engines).
    /// All four presenters are built exactly once, at construction time, and
    /// only ever shown/hidden afterward (see Docs/C8_1_GOLD_MICROGAME_SLICE.md,
    /// "Performance").
    /// </summary>
    internal sealed class ClasicoGameHost : IGamePresenterHost
    {
        private readonly ClasicoHud _hud;
        private readonly WesternShootoutPresenter _western;
        private readonly GameShowPresenter _gameShow;
        private readonly BalanceMachinePresenter _balance;
        private readonly DetectiveLineupPresenter _detective;

        private GameFlowController _controller;
        private int _lastRenderedMicrogameIndex = -1;
        private bool _introVisualsActive;
        private bool _feedbackRendered;
        private MicrogameArchetype _activeArchetype;

        public ClasicoGameHost(ClasicoHud hud)
        {
            _hud = hud;
            _hud.RestartRequested += OnRestartRequested;
            _hud.AbortRequested += OnAbortRequested;

            // C8.1b: each presenter's own local reaction motion (punch/shake/
            // flash on its own characters, via LocalMotionFx) needs a
            // MonoBehaviour to run coroutines on — presenters are plain C#
            // classes, so they borrow ClasicoHud's (the same pattern
            // ClasicoHud already uses for its own global punch/shake).
            _western = new WesternShootoutPresenter(_hud);
            _gameShow = new GameShowPresenter(_hud);
            _balance = new BalanceMachinePresenter(_hud);
            _detective = new DetectiveLineupPresenter(_hud);

            _western.Build(_hud.StageRoot);
            _gameShow.Build(_hud.StageRoot);
            _balance.Build(_hud.StageRoot);
            _detective.Build(_hud.StageRoot);

            _western.TargetSelected += OnSelectionInput;
            _detective.SuspectAccused += OnSelectionInput;
            _gameShow.AnswerChosen += chosenTrue => OnSelectionInput(chosenTrue ? 0 : 1);
            _balance.NudgeRequested += OnBalanceNudge;
            _balance.ConfirmRequested += OnBalanceConfirm;
        }

        public void Show(GameFlowController controller, GameDefinition definition, GameContext context)
        {
            _controller = controller;
            _controller.ResultReady += _hud.ShowResults;

            _lastRenderedMicrogameIndex = -1;
            _introVisualsActive = false;
            _feedbackRendered = false;
            _controller.Start(definition, context);
            _hud.ShowPlaying();
            HideAllPresenters();
        }

        public void Hide()
        {
            if (_controller != null)
            {
                _controller.ResultReady -= _hud.ShowResults;
            }

            HideAllPresenters();
            _hud.Hide();
            _controller = null;
        }

        public void RenderFrame()
        {
            var director = (ClasicoSessionDirector)_controller.CurrentEngine;

            if (director.IsCountingDown)
            {
                _hud.RenderCountdown(director.CountdownSecondsRemaining);
                return;
            }

            if (director.CurrentMicrogameIndex != _lastRenderedMicrogameIndex)
            {
                // C8.1d.1: a continuation round of the same Encounter (e.g.
                // Western round 2/3) must NOT hide/rebuild the world or play
                // the fade — "the scene persists" (see
                // Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Western Encounter
                // Presentation"). Only a genuine archetype change (a new
                // Encounter) gets the full hide-all/show/transition-cut
                // treatment this always did before.
                var isFirstMicrogame = _lastRenderedMicrogameIndex == -1;
                _lastRenderedMicrogameIndex = director.CurrentMicrogameIndex;
                _feedbackRendered = false;

                var isNewEncounter = isFirstMicrogame || director.CurrentArchetype != _activeArchetype;
                if (isNewEncounter)
                {
                    HideAllPresenters();
                    _activeArchetype = director.CurrentArchetype;
                    ShowActiveChallenge(director, isEncounterStart: true);
                    _hud.PlayTransitionCut();
                }
                else
                {
                    ShowActiveChallenge(director, isEncounterStart: false);
                }
            }

            if (director.IsIntroPhase)
            {
                if (!_introVisualsActive)
                {
                    _introVisualsActive = true;
                    _hud.RenderProgress(director.CurrentMicrogameIndex + 1, director.TotalMicrogames, _controller.CurrentSession.Score, _controller.CurrentSession.Streak);

                    // C8.1d.7: Western's own command word ("DISPARA") is no
                    // longer shown via this generic HUD banner — that banner
                    // stays onscreen for the archetype's entire Intro phase,
                    // which for a Western Encounter's round 1 is the whole
                    // ~7.5s cinematic (it would float over every close-up).
                    // WesternShootoutPresenter now shows its own, smaller
                    // cue at the actual moment of gameplay reveal instead
                    // (see ShowDisparaCue). ShowCommand is still called for
                    // its other side effects (clearing stale feedback text,
                    // resetting the timer fill) that every archetype's Intro
                    // still needs — only the visible banner text is
                    // suppressed for AimSelect.
                    var isWestern = director.CurrentArchetype == MicrogameArchetype.AimSelect;
                    _hud.ShowCommand(isWestern ? string.Empty : MicrogameVocabulary.CommandFor(director.CurrentArchetype));
                }

                return;
            }

            if (director.IsDecisionPhase)
            {
                if (_introVisualsActive)
                {
                    _introVisualsActive = false;
                    _hud.HideCommand();

                    // C8.1d.2: this is the exact "intro is over" transition
                    // — the only moment Western's face-off should turn into
                    // a live, playable round (concept/labels/reticle/input).
                    // For a continuation round (2/3) ShowChallenge already
                    // revealed everything synchronously, so this is a
                    // harmless idempotent re-application, not a second
                    // reveal — see WesternShootoutPresenter.RevealAfterIntro.
                    if (_activeArchetype == MicrogameArchetype.AimSelect)
                    {
                        _western.RevealAfterIntro();
                    }
                }

                _hud.RenderTimer(director.DecisionFraction01);
                RenderActivePresenterDecision(director);
                return;
            }

            if (director.IsFeedbackPhase && !_feedbackRendered)
            {
                _feedbackRendered = true;

                // C8.1d.9: removes C8.1d.8's deferred-timing workaround
                // entirely — that phase delayed this whole call by 0.30s so
                // Western's own generic correct/incorrect ding wouldn't
                // precede the gameplay gunshot. Manual validation then
                // concluded the ding itself was redundant (the outlaw's own
                // red/green tint and hit reaction already communicate
                // correctness) and asked for it removed outright, not
                // re-timed — so this call is back to being immediate and
                // unconditional for every archetype (text/score/streak
                // always update right away, exactly as before C8.1d.8 ever
                // existed), and only the *audio* is now conditionally
                // suppressed via ClasicoHud.RenderFeedback's own
                // `playAudio` parameter — see
                // Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.9". Every other
                // archetype still gets its ding exactly as before —
                // ClasicoHud's own correct/incorrect audio is never
                // globally disabled, only opted out of for AimSelect.
                _hud.RenderFeedback(
                    director.LastAnswerCorrect,
                    _controller.CurrentSession.Score,
                    _controller.CurrentSession.Streak,
                    director.LastStreakBonus,
                    playAudio: _activeArchetype != MicrogameArchetype.AimSelect);

                RevealActivePresenter(director);
            }
        }

        /// <summary>Shows whichever challenge is currently active on its
        /// presenter. <paramref name="isEncounterStart"/> only matters to
        /// Western: true plays the full face-off intro (background/sting/
        /// tumbleweed), false is a quick in-place reset for a continuation
        /// round — see <see cref="WesternShootoutPresenter.ShowChallenge"/>.
        /// The other three presenters don't have an Encounter concept yet
        /// (brief section 6: "prove Western first"), so they ignore it.</summary>
        private void ShowActiveChallenge(ClasicoSessionDirector director, bool isEncounterStart)
        {
            switch (_activeArchetype)
            {
                case MicrogameArchetype.AimSelect:
                    _western.ShowChallenge(director.CurrentClassification, isEncounterStart);
                    break;
                case MicrogameArchetype.ChooseSide:
                    _gameShow.ShowChallenge(director.CurrentTrueFalse);
                    break;
                case MicrogameArchetype.Balance:
                    _balance.ShowChallenge(director.CurrentEquation);
                    break;
                default:
                    _detective.ShowChallenge(director.CurrentErrorDetection);
                    break;
            }
        }

        private void RenderActivePresenterDecision(ClasicoSessionDirector director)
        {
            switch (_activeArchetype)
            {
                case MicrogameArchetype.AimSelect:
                    _western.RenderDecision();
                    break;
                case MicrogameArchetype.Balance:
                    _balance.RenderLiveValue(director.CurrentBalanceValue);
                    break;
                case MicrogameArchetype.DetectError:
                    _detective.RenderDecision();
                    break;
            }
        }

        private void RevealActivePresenter(ClasicoSessionDirector director)
        {
            switch (_activeArchetype)
            {
                case MicrogameArchetype.AimSelect:
                    _western.RevealOutcome(director.LastSelectedIndex);
                    break;
                case MicrogameArchetype.ChooseSide:
                    _gameShow.RevealOutcome(director.LastSelectedIndex);
                    break;
                case MicrogameArchetype.Balance:
                    _balance.RevealOutcome(director.LastAnswerCorrect, director.LastBalanceValue);
                    break;
                default:
                    _detective.RevealOutcome(director.LastSelectedIndex);
                    break;
            }
        }

        private void HideAllPresenters()
        {
            _western.Hide();
            _gameShow.Hide();
            _balance.Hide();
            _detective.Hide();
        }

        private void OnRestartRequested()
        {
            _controller.Restart();
            _lastRenderedMicrogameIndex = -1;
            _introVisualsActive = false;
            _feedbackRendered = false;
            _hud.ShowPlaying();
            HideAllPresenters();
        }

        private void OnAbortRequested() => _controller?.Abort();

        private void OnSelectionInput(int index)
        {
            if (!TryGetActiveDirector(out var director) || !director.IsDecisionPhase)
            {
                return;
            }

            director.SubmitSelection(index);
        }

        private void OnBalanceNudge(int direction)
        {
            if (!TryGetActiveDirector(out var director) || !director.IsDecisionPhase)
            {
                return;
            }

            director.NudgeBalance(direction);
        }

        private void OnBalanceConfirm()
        {
            if (!TryGetActiveDirector(out var director) || !director.IsDecisionPhase)
            {
                return;
            }

            director.ConfirmBalance();
        }

        private bool TryGetActiveDirector(out ClasicoSessionDirector director)
        {
            director = null;
            if (_controller == null || _controller.State != GameLifecycleState.Playing)
            {
                return false;
            }

            director = (ClasicoSessionDirector)_controller.CurrentEngine;
            return true;
        }
    }
}
