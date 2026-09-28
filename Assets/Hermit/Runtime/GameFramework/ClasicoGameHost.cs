using System;
using Hermit.Core;
using Hermit.Economy;
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
            _balance.AccountSelected += OnBalanceAccountSelected;
        }

        public void Show(GameFlowController controller, GameDefinition definition, GameContext context)
        {
            _controller = controller;
            _controller.ResultReady += _hud.ShowResults;
            // C9.1: after the normal results, hand the finished session to
            // the Hermit economy (subscribed after ShowResults, so the
            // reward card appears on an already-shown Results screen).
            _controller.ResultReady += OnResultReadyForEconomy;

            _lastRenderedMicrogameIndex = -1;
            _introVisualsActive = false;
            _feedbackRendered = false;
            _gameShow.ResetSession();
            _controller.Start(definition, context);
            _hud.ShowPlaying();
            HideAllPresenters();
        }

        public void Hide()
        {
            if (_controller != null)
            {
                _controller.ResultReady -= _hud.ShowResults;
                _controller.ResultReady -= OnResultReadyForEconomy;
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
                    // WesternShootoutPresenter showed its own, smaller cue
                    // at gameplay reveal instead, until C8.1p removed that
                    // visual cue entirely. ShowCommand is still called for
                    // its other side effects (clearing stale feedback text,
                    // resetting the timer fill) that every archetype's Intro
                    // still needs — only the visible banner text is
                    // suppressed for AimSelect.
                    // C8.1k: Game Show opts out the same way — its Intro is
                    // now a ~1.5s broadcast preamble (host/prize/statement
                    // entrance), and the banner would float over it.
                    var suppressBanner = director.CurrentArchetype == MicrogameArchetype.AimSelect
                        || director.CurrentArchetype == MicrogameArchetype.ChooseSide;
                    _hud.ShowCommand(suppressBanner ? string.Empty : MicrogameVocabulary.CommandFor(director.CurrentArchetype));
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
                    // C8.1e: Game Show's own Gold intro (a fast lights/actor/
                    // choice-zone reveal, see GameShowPresenter.IntroRoutine)
                    // needs this exact same "intro is over" signal to gate
                    // input correctly — mirrors Western's pattern above
                    // rather than inventing a second mechanism.
                    else if (_activeArchetype == MicrogameArchetype.ChooseSide)
                    {
                        _gameShow.RevealAfterIntro();
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
                // C8.1g.2: Detective now has its own correct/incorrect
                // reveal cues (DetectiveLineupPresenter's own
                // Detective_CorrectReveal/Detective_IncorrectReveal) — the
                // shared generic ding is retired from being Detective's
                // hero feedback too, same reasoning and same mechanism as
                // AimSelect's own opt-out above (C8.1d.9).
                // C8.1j: Balance also opts out of the shared banner TEXT
                // (not just audio) — its own pivot verdict + teaching recap
                // are strictly more specific, and the generic banner was
                // the literal cause of the reported verdict/recap overlap
                // (both are bottom-center anchored). See ClasicoHud.RenderFeedback.
                // C8.1k: Game Show holds a short answer-lock suspense beat
                // before showing correctness, so the shared HUD update
                // (score/streak + its global punch/shake) is handed to the
                // presenter and fires at that exact reveal moment instead
                // of giving the answer away first. It also opts out of the
                // shared ding (its own GameShow_* reveal cues replace it)
                // and the shared banner (its explanation card carries the
                // verdict).
                if (_activeArchetype == MicrogameArchetype.ChooseSide)
                {
                    var correct = director.LastAnswerCorrect;
                    var score = _controller.CurrentSession.Score;
                    var streak = _controller.CurrentSession.Streak;
                    var streakBonus = director.LastStreakBonus;
                    _gameShow.RevealOutcome(
                        director.LastSelectedIndex,
                        () => _hud.RenderFeedback(correct, score, streak, streakBonus, playAudio: false, showBanner: false));
                    return;
                }

                _hud.RenderFeedback(
                    director.LastAnswerCorrect,
                    _controller.CurrentSession.Score,
                    _controller.CurrentSession.Streak,
                    director.LastStreakBonus,
                    playAudio: _activeArchetype != MicrogameArchetype.AimSelect && _activeArchetype != MicrogameArchetype.DetectError,
                    // C8.1k.1: Detective opts out of the banner text too —
                    // it sat on top of the lineup's own header, and the
                    // IMPOSTOR reveal + teaching recap already carry the
                    // verdict. Score/streak still update.
                    showBanner: _activeArchetype != MicrogameArchetype.Balance && _activeArchetype != MicrogameArchetype.DetectError);

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
                    _balance.ShowChallenge(director.CurrentDebitCredit);
                    break;
                default:
                    _detective.ShowChallenge(
                        director.CurrentErrorDetection,
                        director.CurrentErrorDetectionDisplayOrder,
                        director.CurrentErrorDetectionDisplayAnomalyIndex);
                    break;
            }
        }

        /// <summary>C8.1f: Balance no longer needs a case here — like Game
        /// Show, its new debit/credit selection is a static "pick 1 of N"
        /// screen with nothing to render per-frame during Decision (the old
        /// continuous-nudge mechanic was the one archetype that did).</summary>
        private void RenderActivePresenterDecision(ClasicoSessionDirector director)
        {
            switch (_activeArchetype)
            {
                case MicrogameArchetype.AimSelect:
                    _western.RenderDecision();
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
                case MicrogameArchetype.Balance:
                    _balance.RevealOutcome(director.LastDebitCreditOutcome);
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
            _gameShow.ResetSession();
            _controller.Restart();
            _lastRenderedMicrogameIndex = -1;
            _introVisualsActive = false;
            _feedbackRendered = false;
            _hud.ShowPlaying();
            HideAllPresenters();
        }

        /// <summary>C8.1f.2 manual-acceptance fix: previously only called
        /// <see cref="GameFlowController.Abort"/>, which moves state to
        /// Ending/Results — not Idle — so <see cref="Hide"/> (and with it,
        /// Western's music-stopping <see cref="WesternShootoutPresenter.Hide"/>)
        /// was never reached until the player *also* clicked "Volver" on
        /// the Results screen. A player who pressed "Salir" mid-Western-
        /// round would hear the music keep playing through the entire
        /// Results screen — "exits Clásico" (this action) is its own
        /// explicit stop trigger, not just the later Idle transition.
        /// Hiding presenters here is the same safe, already-established
        /// pattern <see cref="OnRestartRequested"/> already uses — every
        /// presenter is rebuilt fresh from its own next ShowChallenge, so
        /// hiding early is never destructive.</summary>
        private void OnAbortRequested()
        {
            HideAllPresenters();
            _controller?.Abort();
        }

        /// <summary>C9.1: the ONLY coupling between Clásico and the Hermit
        /// economy — the director's read-only <see cref="ClasicoSessionResult"/>
        /// goes to <see cref="HermitEconomy.Service"/>, whose outcome the HUD
        /// merely displays. Clásico never touches currency, and an economy
        /// failure can never break the normal results flow.</summary>
        private void OnResultReadyForEconomy(GameResult result)
        {
            if (!(_controller?.CurrentEngine is ClasicoSessionDirector director) || director.LastSessionResult == null)
            {
                return;
            }

            try
            {
                var outcome = HermitEconomy.Service.ProcessClasicoSession(director.LastSessionResult);
                _hud.ShowRewardSummary(outcome);
            }
            catch (Exception e)
            {
                HermitLog.Error($"Hermit economy could not process the Clásico session: {e}");
            }
        }

        private void OnSelectionInput(int index)
        {
            if (!TryGetActiveDirector(out var director) || !director.IsDecisionPhase)
            {
                return;
            }

            director.SubmitSelection(index);

            // C8.1k: lock Game Show's chosen zone only once the director has
            // actually accepted the answer (it leaves Decision on accept).
            if (_activeArchetype == MicrogameArchetype.ChooseSide && !director.IsDecisionPhase)
            {
                _gameShow.LockAnswer(index);
            }
        }

        /// <summary>C8.1f: fired twice per Balance round — once for the
        /// debit pick, once for the credit pick. Which step each call
        /// belongs to is <see cref="ClasicoSessionDirector.SubmitBalanceAccount"/>'s
        /// own concern (it reads the active engine's current step), not
        /// something tracked here — mirrors <see cref="OnSelectionInput"/>
        /// needing no step concept either.</summary>
        private void OnBalanceAccountSelected(int index)
        {
            if (!TryGetActiveDirector(out var director) || !director.IsDecisionPhase)
            {
                return;
            }

            director.SubmitBalanceAccount(index);
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
