using Hermit.Games;

namespace Hermit.Tests.EditMode.Fakes
{
    /// <summary>
    /// A second, deliberately trivial game used only to prove the framework's
    /// extensibility claim: this type lives outside Hermit.Games entirely (a
    /// separate assembly) and implements GameDefinition/IGameEngine using only
    /// their public contract — no edit to GameFlowController, GameRegistry, or
    /// any Clasico type was needed to make it work. See
    /// GameFrameworkExtensibilityTests and Docs/C5_GAME_FRAMEWORK.md.
    /// </summary>
    internal sealed class FakeGameDefinition : GameDefinition
    {
        public static FakeGameDefinition CreateInMemory(string gameId, string displayName)
        {
            var instance = CreateInstance<FakeGameDefinition>();
            instance.SetIdentity(gameId, displayName);
            return instance;
        }
    }

    /// <summary>Finishes after a configurable number of Tick calls, scoring one
    /// point per tick — just enough behavior to exercise every GameFlowController
    /// transition without pretending to be a real game.</summary>
    internal sealed class FakeGameEngine : IGameEngine
    {
        private readonly int _ticksToFinish;
        private int _ticksSoFar;
        private GameSession _session;

        public int BeginCallCount { get; private set; }
        public int CleanupCallCount { get; private set; }

        public FakeGameEngine(int ticksToFinish = 3)
        {
            _ticksToFinish = ticksToFinish;
        }

        public bool IsFinished => _ticksSoFar >= _ticksToFinish;

        public void Begin(GameContext context, GameDefinition definition, GameSession session)
        {
            BeginCallCount++;
            _session = session;
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
