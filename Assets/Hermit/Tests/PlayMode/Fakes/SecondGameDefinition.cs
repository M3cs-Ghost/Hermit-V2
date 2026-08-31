using Hermit.Games;

namespace Hermit.Tests.PlayMode.Fakes
{
    /// <summary>
    /// A second, deliberately trivial game used only by
    /// GameSelectorPlayModeTests to prove the real GameSelectorHud can
    /// enumerate and launch more than one game with zero Clasico involvement.
    /// Never referenced by GameCatalog.asset — this must never become visible
    /// in a real build (see C6 brief, "Fake second game").
    ///
    /// Duplicated from Hermit.Tests.EditMode.Fakes.FakeGame rather than shared
    /// across assemblies — ~30 lines of trivial fake code twice is simpler
    /// than introducing a shared test-only assembly for one type.
    /// </summary>
    internal sealed class SecondGameDefinition : GameDefinition
    {
        public static SecondGameDefinition CreateInMemory(string gameId, string displayName, bool isEnabled = true)
        {
            var instance = CreateInstance<SecondGameDefinition>();
            instance.SetIdentity(gameId, displayName);
            instance.SetSelectorMetadata(isEnabled, displaySortOrder: 0);
            return instance;
        }

        public override IGameEngine CreateEngine() => new SecondGameEngine();
    }

    internal sealed class SecondGameEngine : IGameEngine
    {
        private GameSession _session;
        private bool _finished;

        public bool IsFinished => _finished;

        public void Begin(GameContext context, GameDefinition definition, GameSession session)
        {
            _session = session;
        }

        public void Tick(float deltaSeconds)
        {
            _finished = true;
        }

        public GameResult BuildResult(bool completed)
        {
            return new GameResult(_session.GameId, _session.SessionId, _session.Score, _session.Correct, _session.Incorrect, _session.ElapsedSeconds, completed);
        }

        public void Cleanup()
        {
        }
    }
}
