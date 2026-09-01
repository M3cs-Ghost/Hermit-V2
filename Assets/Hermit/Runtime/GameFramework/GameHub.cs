using System;
using System.Collections.Generic;
using UnityEngine;
using Hermit.Core;
using Hermit.Games;
using Hermit.Games.Analytics;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Everything C6's GameSessionInstaller used to do inline — own a
    /// GameRegistry-backed GameFlowController, a GameSelectorHud, and the
    /// GameId-keyed presenter hosts — extracted into a plain, reusable class
    /// so C7's ShellInstaller (the product entry point) and the C5/C6-era
    /// GameSessionInstaller (kept for 02_GameplaySandbox dev/test) share one
    /// implementation instead of two copies of the same composition logic.
    /// Not a MonoBehaviour — whichever installer owns one drives its Tick()
    /// from its own Update().
    /// </summary>
    internal sealed class GameHub
    {
        private readonly GameRegistry _registry;
        private readonly GameSelectorHud _selectorHud;
        private readonly Dictionary<string, IGamePresenterHost> _hostsByGameId = new Dictionary<string, IGamePresenterHost>();
        private IGamePresenterHost _activeHost;

        public GameFlowController FlowController { get; }

        public GameHub(GameRegistry registry, GameSelectorHud selectorHud)
        {
            _registry = registry;
            _selectorHud = selectorHud;
            _selectorHud.GameLaunchRequested += OnGameLaunchRequested;

            FlowController = new GameFlowController();
            FlowController.StateChanged += OnFlowStateChanged;
        }

        /// <summary>Registers which presenter shows a given GameId's screen —
        /// the one explicit, non-branching wiring point described in
        /// Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md, "Where a specific game's
        /// presentation gets wired in".</summary>
        public void RegisterPresenter(string gameId, IGamePresenterHost host)
        {
            _hostsByGameId[gameId] = host;
        }

        public void ShowSelector()
        {
            _selectorHud.Show();
        }

        public void Tick(float deltaSeconds)
        {
            if (FlowController.State != GameLifecycleState.Playing)
            {
                return;
            }

            FlowController.Tick(deltaSeconds);

            if (FlowController.State == GameLifecycleState.Playing)
            {
                _activeHost?.RenderFrame();
            }
        }

        private void OnGameLaunchRequested(GameDefinition definition)
        {
            if (!_hostsByGameId.TryGetValue(definition.GameId, out var host))
            {
                HermitLog.Error($"No presenter host registered for game '{definition.GameId}' — cannot launch it.");
                return;
            }

            _selectorHud.Hide();
            _activeHost = host;

            var context = new GameContext(new HermitLogAnalyticsSink(), new System.Random());
            _activeHost.Show(FlowController, definition, context);
        }

        private void OnFlowStateChanged(GameLifecycleState state)
        {
            if (state != GameLifecycleState.Idle)
            {
                return;
            }

            _activeHost?.Hide();
            _activeHost = null;
            _selectorHud.Show();
        }
    }
}
