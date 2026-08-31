using System;
using UnityEngine;
using Hermit.Core;
using Hermit.Games;
using Hermit.Games.Analytics;
using Hermit.Games.Clasico;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Composition root for the C5 vertical slice. Lives on one GameObject in
    /// 02_GameplaySandbox (no serialized references — everything is built or
    /// loaded in code, the same zero-scene-wiring approach HermitRuntimeInstaller
    /// uses for Networking).
    ///
    /// Deliberately separate from HermitRuntimeInstaller: that installer is
    /// global (RuntimeInitializeOnLoadMethod, every scene) and owns Networking;
    /// this one is scene-scoped and owns nothing beyond the game framework +
    /// this one game's UI. Neither knows the other exists — see
    /// Docs/C5_GAME_FRAMEWORK.md, "Runtime integration".
    /// </summary>
    public sealed class ClasicoVerticalSliceInstaller : MonoBehaviour
    {
        private GameFlowController _flowController;
        private GameRegistry _registry;
        private ClasicoHud _hud;
        private ClasicoGameEngine _activeEngine;
        private string _lastRenderedQuestionId;

        /// <summary>Public so PlayMode tests (a separate assembly, like
        /// HermitBootstrapPlayModeTests already is for Hermit.Runtime) can drive
        /// the composed framework directly instead of simulating pointer clicks
        /// through the Input System — see Docs/C5_GAME_FRAMEWORK.md, "Manual
        /// validation" for why real clicks are a human-only gate.</summary>
        public GameFlowController FlowController => _flowController;

        private void Awake()
        {
            RuntimeUIFactory.EnsureEventSystem();
            var canvas = RuntimeUIFactory.CreateCanvas(transform, "ClasicoCanvas");

            _hud = gameObject.AddComponent<ClasicoHud>();
            _hud.Build(canvas.transform);

            var definition = Resources.Load<ClasicoGameDefinition>("ClasicoGameDefinition");
            if (definition == null)
            {
                HermitLog.Error("ClasicoGameDefinition not found under a Resources/ folder — the Clasico vertical slice will not start. See Docs/C5_GAME_FRAMEWORK.md.");
                enabled = false;
                return;
            }

            _registry = new GameRegistry();
            _registry.Register(definition, () => _activeEngine = new ClasicoGameEngine());

            _flowController = new GameFlowController();
            _flowController.StateChanged += OnStateChanged;
            _flowController.ResultReady += _hud.ShowResults;

            _hud.PlayRequested += StartClasico;
            _hud.AnswerSelected += OnAnswerSelected;
            _hud.AbortRequested += () => _flowController.Abort();
            _hud.RestartRequested += RestartClasico;
            _hud.ExitToIdleRequested += () => _flowController.AcknowledgeResults();
        }

        private void Update()
        {
            if (_flowController == null || _flowController.State != GameLifecycleState.Playing)
            {
                return;
            }

            _flowController.Tick(Time.deltaTime);

            if (_flowController.State != GameLifecycleState.Playing)
            {
                return;
            }

            if (_activeEngine.IsRevealing)
            {
                _hud.RenderReveal(_activeEngine.SelectedOptionIndex, _activeEngine.CorrectOptionIndex, _flowController.CurrentSession.Score);
            }
            else if (_activeEngine.CurrentView.QuestionId != _lastRenderedQuestionId)
            {
                // Only re-render (and re-select the first option) when the
                // question actually changes — calling this every frame while
                // awaiting an answer would keep resetting the EventSystem's
                // selection back to option 0, making keyboard navigation
                // impossible to move away from it.
                RenderCurrentQuestion();
            }
        }

        private void RenderCurrentQuestion()
        {
            _hud.RenderQuestion(_activeEngine.CurrentView, _flowController.CurrentSession.Score);
            _lastRenderedQuestionId = _activeEngine.CurrentView.QuestionId;
        }

        private void StartClasico()
        {
            if (!_registry.TryGet("clasico", out var registration))
            {
                HermitLog.Error("Clasico is not registered — cannot start.");
                return;
            }

            var context = new GameContext(new HermitLogAnalyticsSink(), new System.Random());
            _flowController.Start(registration, context);
            _lastRenderedQuestionId = null;
            RenderCurrentQuestion();
        }

        private void RestartClasico()
        {
            _flowController.Restart();
            _lastRenderedQuestionId = null;
            RenderCurrentQuestion();
        }

        private void OnAnswerSelected(int optionIndex)
        {
            if (_flowController.State != GameLifecycleState.Playing || _activeEngine == null || _activeEngine.IsRevealing)
            {
                return;
            }

            _activeEngine.SubmitAnswer(optionIndex);
        }

        private void OnStateChanged(GameLifecycleState state)
        {
            if (state == GameLifecycleState.Idle)
            {
                _hud.ShowIdle();
            }
            else if (state == GameLifecycleState.Playing)
            {
                _hud.ShowPlaying();
            }
        }
    }
}
