using System;
using System.Collections.Generic;
using UnityEngine;
using Hermit.Core;
using Hermit.Games;
using Hermit.Games.Analytics;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Composition root for the game framework's runtime surface. Lives on one
    /// GameObject in 02_GameplaySandbox (no serialized references — everything
    /// is built or loaded in code, the same zero-scene-wiring approach
    /// HermitRuntimeInstaller uses for Networking).
    ///
    /// C6 replaces C5's ClasicoVerticalSliceInstaller (which loaded exactly
    /// one game by a fixed Resources path and wired itself directly to
    /// Clasico) with a generic root: build a registry from one catalog asset,
    /// show a selector, launch whichever game was picked, route lifecycle
    /// generically, return to the selector on Idle. The only Clasico-specific
    /// line is the one dictionary registration below — see
    /// Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md, "Runtime flow".
    ///
    /// Deliberately separate from HermitRuntimeInstaller: that installer is
    /// global (RuntimeInitializeOnLoadMethod, every scene) and owns Networking;
    /// this one is scene-scoped and owns nothing beyond the game framework.
    /// Neither knows the other exists.
    /// </summary>
    public sealed class GameSessionInstaller : MonoBehaviour
    {
        private GameRegistry _registry;
        private GameFlowController _flowController;
        private GameSelectorHud _selectorHud;
        private readonly Dictionary<string, IGamePresenterHost> _hostsByGameId = new Dictionary<string, IGamePresenterHost>();
        private IGamePresenterHost _activeHost;

        /// <summary>Public so PlayMode tests can drive the composed framework
        /// directly instead of simulating pointer clicks through the Input
        /// System — see Docs/C5_GAME_FRAMEWORK.md, "Manual validation" for why
        /// real clicks are a human-only gate.</summary>
        public GameFlowController FlowController => _flowController;

        private void Awake()
        {
            RuntimeUIFactory.EnsureEventSystem();
            var canvas = RuntimeUIFactory.CreateCanvas(transform, "GameSessionCanvas");

            var catalog = Resources.Load<GameCatalog>("GameCatalog");
            if (catalog == null)
            {
                HermitLog.Error("GameCatalog not found under a Resources/ folder — no games will be available. See Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md.");
                enabled = false;
                return;
            }

            _registry = new GameRegistry();
            foreach (var definition in catalog.Games)
            {
                if (definition == null)
                {
                    HermitLog.Warning("GameCatalog contains a null entry — skipped.");
                    continue;
                }

                _registry.Register(definition);
            }

            _flowController = new GameFlowController();
            _flowController.StateChanged += OnFlowStateChanged;

            var clasicoHud = gameObject.AddComponent<ClasicoHud>();
            clasicoHud.Build(canvas.transform);
            clasicoHud.ExitToIdleRequested += () => _flowController.AcknowledgeResults();
            _hostsByGameId["clasico"] = new ClasicoGameHost(clasicoHud);

            _selectorHud = gameObject.AddComponent<GameSelectorHud>();
            _selectorHud.Build(canvas.transform, _registry.All);
            _selectorHud.GameLaunchRequested += OnGameLaunchRequested;

            _selectorHud.Show();
        }

        private void Update()
        {
            if (_flowController == null || _flowController.State != GameLifecycleState.Playing)
            {
                return;
            }

            _flowController.Tick(Time.deltaTime);

            if (_flowController.State == GameLifecycleState.Playing)
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
            _activeHost.Show(_flowController, definition, context);
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
