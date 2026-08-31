using System;
using System.Collections.Generic;

namespace Hermit.Games
{
    /// <summary>Bundles a game's static definition with a factory that produces
    /// a fresh <see cref="IGameEngine"/> instance per playthrough.</summary>
    public sealed class GameRegistration
    {
        public GameDefinition Definition { get; }
        public Func<IGameEngine> CreateEngine { get; }

        public GameRegistration(GameDefinition definition, Func<IGameEngine> createEngine)
        {
            Definition = definition;
            CreateEngine = createEngine;
        }
    }

    /// <summary>
    /// Explicit runtime registry: one instance, owned by the composition root
    /// (Hermit.Runtime), that games are added to by calling Register.
    ///
    /// Chosen over a ScriptableObject-based auto-discovery registry or
    /// reflection scan because C5 only ever needs one real caller (the vertical
    /// slice installer) to know the full game list, and that caller already
    /// knows exactly which definitions exist — scanning for them would be
    /// solving a problem C5 doesn't have. Revisit if/when a menu needs to list
    /// "every installed game" without Runtime enumerating them by hand.
    ///
    /// Deliberately has no static/global instance: whoever composes the game
    /// list (Runtime) owns the registry and hands it to whatever needs to read
    /// it, so nothing anywhere else can silently reach into "the games" as a
    /// hidden global.
    /// </summary>
    public sealed class GameRegistry
    {
        private readonly Dictionary<string, GameRegistration> _byId = new Dictionary<string, GameRegistration>();

        public void Register(GameDefinition definition, Func<IGameEngine> createEngine)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (createEngine == null) throw new ArgumentNullException(nameof(createEngine));
            if (string.IsNullOrEmpty(definition.GameId))
            {
                throw new ArgumentException("GameDefinition.GameId must be set before registration.", nameof(definition));
            }

            if (_byId.ContainsKey(definition.GameId))
            {
                throw new InvalidOperationException($"A game with id '{definition.GameId}' is already registered.");
            }

            _byId[definition.GameId] = new GameRegistration(definition, createEngine);
        }

        public bool TryGet(string gameId, out GameRegistration registration) => _byId.TryGetValue(gameId, out registration);

        public IReadOnlyCollection<GameRegistration> All => _byId.Values;
    }
}
