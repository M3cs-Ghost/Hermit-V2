using Hermit.Games;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// The one deliberate seam where a specific game's presentation gets wired
    /// into the composition root — see
    /// Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md, "Runtime flow". Games stay
    /// UI-free (Hermit.Games boundary), so *something* in Hermit.Runtime has to
    /// know how to show a given game's screen; this interface plus a
    /// GameId-keyed dictionary in <see cref="GameSessionInstaller"/> is that
    /// something. Adding a second real game means adding one new
    /// implementation of this interface and one dictionary entry — never an
    /// if/switch on GameId, and never a change to GameSelectorHud or any
    /// Hermit.Games type.
    /// </summary>
    internal interface IGamePresenterHost
    {
        /// <summary>Starts the given definition through the flow controller and
        /// activates this host's own screen.</summary>
        void Show(GameFlowController controller, GameDefinition definition, GameContext context);

        /// <summary>Deactivates this host's screen and unsubscribes from the
        /// controller. Safe to call even if Show was never called.</summary>
        void Hide();

        /// <summary>Called once per frame after the controller Ticks, while
        /// Playing. Reads the controller's CurrentEngine (cast to this host's
        /// concrete engine type) and pushes view state to its own Hud.</summary>
        void RenderFrame();
    }
}
