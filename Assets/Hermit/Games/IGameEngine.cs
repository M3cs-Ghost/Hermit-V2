namespace Hermit.Games
{
    /// <summary>
    /// The contract every game implements. <see cref="GameFlowController"/>
    /// only ever talks to a game through this interface — it never inspects a
    /// concrete engine type, which is what lets a second game (or the test-only
    /// FakeGame in Hermit.Tests.EditMode) plug in without a single edit to the
    /// framework itself. See Docs/C5_GAME_FRAMEWORK.md, "Extensibility test".
    /// </summary>
    public interface IGameEngine
    {
        /// <summary>Called once, transitioning into Playing. Implementations
        /// should treat this as their entire "new game" setup.</summary>
        void Begin(GameContext context, GameDefinition definition, GameSession session);

        /// <summary>Called once per frame while Playing.</summary>
        void Tick(float deltaSeconds);

        /// <summary>True once the game has reached its own natural end (e.g. all
        /// rounds played, time limit hit). Checked by the flow controller after
        /// every Tick; never set from outside the engine.</summary>
        bool IsFinished { get; }

        /// <summary>Called exactly once, whether the game finished naturally or
        /// was aborted, to produce the immutable result.</summary>
        GameResult BuildResult(bool completed);

        /// <summary>Called exactly once, right after BuildResult, so the engine
        /// can drop any per-playthrough state before the next Begin.</summary>
        void Cleanup();
    }
}
