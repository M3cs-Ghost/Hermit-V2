using UnityEngine;
using Hermit.Core;
using Hermit.Games;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Composition root for 01_Shell — Hermit's real product entry point as
    /// of C7 (see Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md, "Shell"). Lives on
    /// one GameObject in 01_Shell (no serialized references — everything is
    /// built or loaded in code, same zero-scene-wiring pattern every other
    /// installer in this project uses).
    ///
    /// Flow: Shell Home -> "Juegos" -> selector (GameHub) -> a game -> Results
    /// -> selector -> "Volver" -> Shell Home. Reuses exactly the same
    /// GameHub/GameSelectorHud/ClasicoHud/ClasicoGameHost stack
    /// GameSessionInstaller uses in 02_GameplaySandbox — Shell only adds the
    /// Home screen wrapping it and the selector's "Volver" wiring back to
    /// Home. No gameplay-specific code lives here; this class only ever
    /// toggles which screen is visible.
    /// </summary>
    public sealed class ShellInstaller : MonoBehaviour
    {
        private GameHub _hub;
        private ShellHomeHud _homeHud;
        private GameSelectorHud _selectorHud;

        /// <summary>Public so PlayMode tests can drive the composed framework
        /// directly instead of simulating pointer clicks — same rationale as
        /// GameSessionInstaller.FlowController.</summary>
        public GameFlowController FlowController => _hub?.FlowController;

        private void Awake()
        {
            RuntimeUIFactory.EnsureEventSystem();
            var canvas = RuntimeUIFactory.CreateCanvas(transform, "ShellCanvas");

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

            _homeHud = gameObject.AddComponent<ShellHomeHud>();
            _homeHud.Build(canvas.transform);
            _homeHud.PlayRequested += OnPlayRequested;

            _selectorHud = gameObject.AddComponent<GameSelectorHud>();
            _selectorHud.Build(canvas.transform, registry.All);
            _selectorHud.SetBackAction(OnSelectorBackRequested);

            var clasicoHud = gameObject.AddComponent<ClasicoHud>();
            clasicoHud.Build(canvas.transform);

            _hub = new GameHub(registry, _selectorHud);
            clasicoHud.ExitToIdleRequested += () => _hub.FlowController.AcknowledgeResults();
            _hub.RegisterPresenter("clasico", new ClasicoGameHost(clasicoHud));

            ShowHome();
        }

        private void Update()
        {
            _hub?.Tick(Time.deltaTime);
        }

        private void OnPlayRequested()
        {
            _homeHud.Hide();
            _hub.ShowSelector();
        }

        private void OnSelectorBackRequested()
        {
            _selectorHud.Hide();
            ShowHome();
        }

        private void ShowHome()
        {
            RefreshSessionStatus();
            _homeHud.Show();
        }

        /// <summary>Reads Networking's own already-public session state — Shell
        /// never constructs a client or calls Supabase itself. Session restore
        /// is asynchronous (HermitRuntimeInstaller fires it and does not wait),
        /// so this can under-report "Invitado" for a brief window right after a
        /// cold start; refreshed every time Home is shown, so it is correct by
        /// the time a player has been through even one game. See "Known
        /// limitations" in Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md.</summary>
        private void RefreshSessionStatus()
        {
            var context = HermitRuntimeInstaller.Current;
            _homeHud.SetSessionStatus(context != null && context.Session.HasValue ? "Sesión activa" : "Invitado");
        }
    }
}
