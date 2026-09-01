using Hermit.Games;
using Hermit.Games.Clasico;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Clasico-specific adapter between the generic GameFlowController and
    /// ClasicoHud. This is the "adapter específico de Clásico" the C6 brief
    /// explicitly allows — the selector and the composition root's generic
    /// plumbing never see this type or ClasicoGameEngine; only the one
    /// dictionary entry in GameSessionInstaller/ShellInstaller does.
    ///
    /// C7 note: the engine may start in a Countdown phase (see
    /// ClasicoGameEngine), during which CurrentView is null — this class is
    /// the reason that never leaks into ClasicoHud as a null-reference: it
    /// always checks IsCountingDown before ever touching CurrentView.
    /// </summary>
    internal sealed class ClasicoGameHost : IGamePresenterHost
    {
        private readonly ClasicoHud _hud;
        private GameFlowController _controller;
        private string _lastRenderedQuestionId;

        /// <summary>Bug fix (found in manual validation): RenderFrame used to
        /// call _hud.RenderReveal unconditionally on every frame of the reveal
        /// window (unlike RenderQuestion, which was already guarded by
        /// _lastRenderedQuestionId). RenderReveal restarts a feedback
        /// animation coroutine every time it is called — restarting it every
        /// frame for ~0.9s meant the incorrect-answer shake coroutine was
        /// cancelled before it ever reached its own cleanup line, so its
        /// positional offset accumulated frame after frame and flung the
        /// question card off-screen (see ClasicoHud.RenderReveal /
        /// StartShake). This flag makes reveal rendering fire exactly once
        /// per reveal, the same guarantee RenderQuestion already had.</summary>
        private bool _revealRendered;

        public ClasicoGameHost(ClasicoHud hud)
        {
            _hud = hud;
            _hud.RestartRequested += OnRestartRequested;
            _hud.AbortRequested += OnAbortRequested;
            _hud.AnswerSelected += OnAnswerSelected;
        }

        public void Show(GameFlowController controller, GameDefinition definition, GameContext context)
        {
            _controller = controller;
            _controller.ResultReady += _hud.ShowResults;

            _lastRenderedQuestionId = null;
            _controller.Start(definition, context);
            _hud.ShowPlaying();
            RenderCurrentState();
        }

        public void Hide()
        {
            if (_controller != null)
            {
                _controller.ResultReady -= _hud.ShowResults;
            }

            _hud.Hide();
            _controller = null;
        }

        public void RenderFrame()
        {
            var engine = (ClasicoGameEngine)_controller.CurrentEngine;

            if (engine.IsCountingDown)
            {
                _hud.RenderCountdown(engine.CountdownSecondsRemaining);
                return;
            }

            if (engine.IsRevealing)
            {
                if (!_revealRendered)
                {
                    _revealRendered = true;
                    _hud.RenderReveal(
                        engine.SelectedOptionIndex,
                        engine.CorrectOptionIndex,
                        _controller.CurrentSession.Score,
                        engine.LastStreakBonus,
                        _controller.CurrentSession.Streak);
                }

                return;
            }

            if (engine.CurrentView.QuestionId != _lastRenderedQuestionId)
            {
                RenderCurrentQuestion();
            }

            _hud.RenderTimer(engine.QuestionTimeFraction01);
        }

        /// <summary>Called right after Start/Restart — shows the countdown
        /// overlay if the engine begins counting down, otherwise renders the
        /// first question directly (countdownDurationSeconds == 0, C5/C6
        /// behavior). Never assumes CurrentView is non-null.</summary>
        private void RenderCurrentState()
        {
            var engine = (ClasicoGameEngine)_controller.CurrentEngine;
            if (engine.IsCountingDown)
            {
                _hud.RenderCountdown(engine.CountdownSecondsRemaining);
            }
            else
            {
                RenderCurrentQuestion();
            }
        }

        private void RenderCurrentQuestion()
        {
            var engine = (ClasicoGameEngine)_controller.CurrentEngine;
            _hud.RenderQuestion(engine.CurrentView, _controller.CurrentSession.Score, _controller.CurrentSession.Streak);
            _lastRenderedQuestionId = engine.CurrentView.QuestionId;
            _revealRendered = false;
        }

        private void OnRestartRequested()
        {
            _controller.Restart();
            _lastRenderedQuestionId = null;
            _hud.ShowPlaying();
            RenderCurrentState();
        }

        private void OnAbortRequested() => _controller?.Abort();

        private void OnAnswerSelected(int optionIndex)
        {
            if (_controller == null || _controller.State != GameLifecycleState.Playing)
            {
                return;
            }

            var engine = (ClasicoGameEngine)_controller.CurrentEngine;
            if (!engine.IsCountingDown && !engine.IsRevealing)
            {
                engine.SubmitAnswer(optionIndex);
            }
        }
    }
}
