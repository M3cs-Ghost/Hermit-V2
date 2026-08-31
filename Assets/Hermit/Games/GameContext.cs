using System;
using Hermit.Games.Analytics;

namespace Hermit.Games
{
    /// <summary>
    /// The only external dependencies a game engine is allowed to see.
    /// Deliberately minimal: analytics and a shared RNG. There is no Supabase
    /// client, no HTTP client, no raw session token here — a game that wants
    /// network access does not get it through this type, by design (see
    /// Docs/C5_GAME_FRAMEWORK.md, "Context").
    /// </summary>
    public sealed class GameContext
    {
        public IGameAnalyticsSink Analytics { get; }
        public Random Rng { get; }

        public GameContext(IGameAnalyticsSink analytics, Random rng)
        {
            Analytics = analytics ?? NullGameAnalyticsSink.Instance;
            Rng = rng ?? new Random();
        }
    }
}
