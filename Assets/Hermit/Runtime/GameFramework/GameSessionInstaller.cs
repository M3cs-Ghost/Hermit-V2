using UnityEngine;
using Hermit.Core;
using Hermit.Games;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Dev/test composition root — lives on one GameObject in
    /// 02_GameplaySandbox, kept per the C7 brief specifically for
    /// development, PlayMode tests, and debugging, now that
    /// 01_Shell/ShellInstaller is the real product entry point (see
    /// Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md, "Scene strategy").
    ///
    /// C7 extracted the actual registry/selector/host wiring into
    /// <see cref="GameHub"/> so this class and ShellInstaller share one
    /// implementation instead of two copies of the same composition logic —
    /// this installer now differs from ShellInstaller only in *not* wrapping
    /// the hub behind a product Home screen, and in never wiring the
    /// selector's optional "Volver" button (there is no Shell to return to
    /// from this scene).
    /// </summary>
    public sealed class GameSessionInstaller : MonoBehaviour
    {
        private GameHub _hub;

        /// <summary>Public so PlayMode tests can drive the composed framework
        /// directly instead of simulating pointer clicks through the Input
        /// System — see Docs/C5_GAME_FRAMEWORK.md, "Manual validation" for why
        /// real clicks are a human-only gate.</summary>
        public GameFlowController FlowController => _hub?.FlowController;

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

            var registry = new GameRegistry();
            foreach (var definition in catalog.Games)
            {
                if (definition == null)
                {
                    HermitLog.Warning("GameCatalog contains a null entry — skipped.");
                    continue;
                }

                registry.Register(definition);
            }

            var selectorHud = gameObject.AddComponent<GameSelectorHud>();
            selectorHud.Build(canvas.transform, registry.All);

            var clasicoHud = gameObject.AddComponent<ClasicoHud>();
            clasicoHud.Build(canvas.transform);

            _hub = new GameHub(registry, selectorHud);
            clasicoHud.ExitToIdleRequested += () => _hub.FlowController.AcknowledgeResults();
            _hub.RegisterPresenter("clasico", new ClasicoGameHost(clasicoHud));

            _hub.ShowSelector();
        }

        private void Update()
        {
            _hub?.Tick(Time.deltaTime);
        }
    }
}
