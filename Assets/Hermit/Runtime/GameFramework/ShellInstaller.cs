using System.Collections;
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
    /// C9.1 flow: cold boot -> StartScreenHud (Title -> Press Start ->
    /// transition) -> Hermit Hub -> "Arcade" hotspot -> selector (GameHub) ->
    /// a game -> Results -> selector -> "Volver" -> Hub. Reuses exactly the
    /// same GameHub/GameSelectorHud/ClasicoHud/ClasicoGameHost stack
    /// GameSessionInstaller uses in 02_GameplaySandbox — Shell only adds the
    /// Start Screen/Hub wrapping it and the selector's "Volver" wiring back
    /// to the Hub. No gameplay-specific code lives here; this class only
    /// ever toggles which screen is visible. See
    /// Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md for the full brief.
    ///
    /// C7's original flat <see cref="ShellHomeHud"/> is retired from this
    /// composition (its file is untouched, just no longer built/shown here)
    /// rather than deleted — <see cref="StartScreenHud"/> is the new root
    /// presentation, including the session-status line and the "Juegos"
    /// button's replacement (the Arcade hotspot).
    /// </summary>
    public sealed class ShellInstaller : MonoBehaviour
    {
        private GameHub _hub;
        private StartScreenHud _startScreenHud;
        private ArcadeGalleryHud _arcadeGalleryHud;

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

            _startScreenHud = gameObject.AddComponent<StartScreenHud>();
            _startScreenHud.Build(canvas.transform);
            _startScreenHud.ArcadeRequested += OnArcadeRequested;

            _arcadeGalleryHud = gameObject.AddComponent<ArcadeGalleryHud>();
            _arcadeGalleryHud.Build(canvas.transform, registry.All);
            _arcadeGalleryHud.SetBackAction(OnSelectorBackRequested);

            var clasicoHud = gameObject.AddComponent<ClasicoHud>();
            clasicoHud.Build(canvas.transform);

            _hub = new GameHub(registry, _arcadeGalleryHud);
            clasicoHud.ExitToIdleRequested += () => _hub.FlowController.AcknowledgeResults();
            _hub.RegisterPresenter("clasico", new ClasicoGameHost(clasicoHud));

            RefreshSessionStatus();
            // Cold boot only — StartScreenHud.ShowTitle is never called
            // again for the life of the process; returning to the Hub from
            // anywhere else always goes through ShowHub below instead.
            _startScreenHud.ShowTitle();
        }

        private void Update()
        {
            _hub?.Tick(Time.deltaTime);
        }

        private void OnArcadeRequested()
        {
            StartCoroutine(TransitionToArcadeRoutine());
        }

        /// <summary>C9.2: the short (~0.35s, well within the brief's
        /// approved 0.35-0.60s range), deliberately non-cinematic Hub-&gt;
        /// Arcade hand-off — Hub navigation fades out
        /// (<see cref="StartScreenHud.HideNavigation"/>, ~0.20s) then the
        /// gallery fades/sweeps in (<see cref="ArcadeGalleryHud.Show"/>,
        /// ~0.18s). Never another full cinematic — the Start Screen already
        /// owns that role for this product.</summary>
        private IEnumerator TransitionToArcadeRoutine()
        {
            _startScreenHud.HideNavigation();
            yield return new WaitForSeconds(0.20f);
            _hub.ShowSelector();
        }

        private void OnSelectorBackRequested()
        {
            _arcadeGalleryHud.Hide();
            ShowHub();
        }

        private void ShowHub()
        {
            RefreshSessionStatus();
            _startScreenHud.ShowNavigation();
        }

        /// <summary>Reads Networking's own already-public session state — Shell
        /// never constructs a client or calls Supabase itself. Session restore
        /// is asynchronous (HermitRuntimeInstaller fires it and does not wait),
        /// so this can under-report "Invitado" for a brief window right after a
        /// cold start; refreshed every time the Hub is shown, so it is correct
        /// by the time a player has been through even one game. See "Known
        /// limitations" in Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md.</summary>
        private void RefreshSessionStatus()
        {
            var context = HermitRuntimeInstaller.Current;
            _startScreenHud.SetSessionStatus(context != null && context.Session.HasValue ? "Sesión activa" : "Invitado");
        }
    }
}
