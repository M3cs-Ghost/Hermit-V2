using Hermit.Games;

namespace Hermit.Tests.EditMode.Fakes
{
    /// <summary>
    /// A second, deliberately trivial game used only to prove the framework's
    /// extensibility claim: this type lives outside Hermit.Games entirely (a
    /// separate assembly) and implements GameDefinition/IGameEngine using only
    /// their public contract — no edit to GameFlowController, GameRegistry, or
    /// any Clasico type was needed to make it work. See
    /// GameFrameworkExtensibilityTests and Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md.
    ///
    /// C6 moved engine construction onto GameDefinition.CreateEngine() — config
    /// (here, TicksToFinish) now lives on the definition and is read lazily in
    /// Begin(), the same pattern ClasicoGameDefinition/ClasicoGameEngine already
    /// use, instead of C5's externally-supplied factory closure.
    /// </summary>
    internal sealed class FakeGameDefinition : GameDefinition
    {
        public int TicksToFinish { get; private set; } = 3;

        /// <summary>Set once per CreateEngine() call — lets a test assert how
        /// many engine instances this definition has produced (e.g. to prove
        /// Restart creates a fresh one) and inspect the most recent one.</summary>
        public int EnginesCreatedCount { get; private set; }
        public FakeGameEngine LastCreatedEngine { get; private set; }

        public static FakeGameDefinition CreateInMemory(
            string gameId,
            string displayName,
            int ticksToFinish = 3,
            bool isEnabled = true,
            int displaySortOrder = 0)
        {
            var instance = CreateInstance<FakeGameDefinition>();
            instance.SetIdentity(gameId, displayName);
            instance.SetSelectorMetadata(isEnabled, displaySortOrder);
            instance.TicksToFinish = ticksToFinish;
            return instance;
        }

        public override IGameEngine CreateEngine()
        {
            EnginesCreatedCount++;
            LastCreatedEngine = new FakeGameEngine();
            return LastCreatedEngine;
        }
    }

    /// <summary>Finishes after a configurable number of Tick calls, scoring one
    /// point per tick — just enough behavior to exercise every GameFlowController
    /// transition without pretending to be a real game.</summary>
    internal sealed class FakeGameEngine : IGameEngine
    {
        private int _ticksToFinish;
        private int _ticksSoFar;
        private GameSession _session;

        public int BeginCallCount { get; private set; }
        public int CleanupCallCount { get; private set; }

        public bool IsFinished => _ticksSoFar >= _ticksToFinish;

        public void Begin(GameContext context, GameDefinition definition, GameSession session)
        {
            BeginCallCount++;
            _session = session;
            _ticksToFinish = ((FakeGameDefinition)definition).TicksToFinish;
            _ticksSoFar = 0;
        }

        public void Tick(float deltaSeconds)
        {
            _ticksSoFar++;
            _session.Score += 1;
        }

        public GameResult BuildResult(bool completed)
        {
            return new GameResult(_session.GameId, _session.SessionId, _session.Score, _session.Correct, _session.Incorrect, _session.ElapsedSeconds, completed);
        }

        public void Cleanup()
        {
            CleanupCallCount++;
        }
    }
}
