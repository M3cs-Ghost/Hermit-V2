using Hermit.Games;
using Hermit.Games.Clasico;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Clasico-specific adapter between the generic GameFlowController and
    /// ClasicoHud. This is the "adapter específico de Clásico" the C6 brief
    /// explicitly allows — the selector and the composition root's generic
    /// plumbing never see this type or ClasicoGameEngine; only the one
    /// dictionary entry in GameSessionInstaller does.
    /// </summary>
    internal sealed class ClasicoGameHost : IGamePresenterHost
    {
        private readonly ClasicoHud _hud;
        private GameFlowController _controller;
        private string _lastRenderedQuestionId;

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
            RenderCurrentQuestion();
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

            if (engine.IsRevealing)
            {
                _hud.RenderReveal(engine.SelectedOptionIndex, engine.CorrectOptionIndex, _controller.CurrentSession.Score);
            }
            else if (engine.CurrentView.QuestionId != _lastRenderedQuestionId)
            {
                RenderCurrentQuestion();
            }
        }

        private void RenderCurrentQuestion()
        {
            var engine = (ClasicoGameEngine)_controller.CurrentEngine;
            _hud.RenderQuestion(engine.CurrentView, _controller.CurrentSession.Score);
            _lastRenderedQuestionId = engine.CurrentView.QuestionId;
        }

        private void OnRestartRequested()
        {
            _controller.Restart();
            _lastRenderedQuestionId = null;
            _hud.ShowPlaying();
            RenderCurrentQuestion();
        }

        private void OnAbortRequested() => _controller?.Abort();

        private void OnAnswerSelected(int optionIndex)
        {
            if (_controller == null || _controller.State != GameLifecycleState.Playing)
            {
                return;
            }

            var engine = (ClasicoGameEngine)_controller.CurrentEngine;
            if (!engine.IsRevealing)
            {
                engine.SubmitAnswer(optionIndex);
            }
        }
    }
}
