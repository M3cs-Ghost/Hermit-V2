using System;
using System.Collections.Generic;
using System.Linq;

namespace Hermit.Games
{
    /// <summary>
    /// Explicit runtime registry: one instance, owned by the composition root
    /// (Hermit.Runtime), that games are added to by calling Register.
    ///
    /// C5 wrapped each entry in a separate "GameRegistration" (definition +
    /// externally-supplied engine factory). C6 drops that wrapper —
    /// <see cref="GameDefinition.CreateEngine"/> now carries the factory, so
    /// the registry only ever needs to hold the definitions themselves. One
    /// less type, same guarantees.
    ///
    /// Chosen over a ScriptableObject-based auto-discovery registry or a
    /// reflection scan for the same reason as C5: exactly one real caller
    /// (the composition root, reading a <see cref="GameCatalog"/>) needs the
    /// full game list, and it already knows what to register. No
    /// static/global instance anywhere — whoever builds the registry owns it
    /// and hands it to whatever needs to read it.
    /// </summary>
    public sealed class GameRegistry
    {
        private readonly Dictionary<string, GameDefinition> _byId = new Dictionary<string, GameDefinition>();
        private readonly List<GameDefinition> _registrationOrder = new List<GameDefinition>();

        public int Count => _registrationOrder.Count;

        public void Register(GameDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrEmpty(definition.GameId))
            {
                throw new ArgumentException("GameDefinition.GameId must be set before registration.", nameof(definition));
            }

            if (_byId.ContainsKey(definition.GameId))
            {
                throw new InvalidOperationException($"A game with id '{definition.GameId}' is already registered.");
            }

            _byId[definition.GameId] = definition;
            _registrationOrder.Add(definition);
        }

        public bool TryGet(string gameId, out GameDefinition definition) => _byId.TryGetValue(gameId, out definition);

        /// <summary>Every registered game — enabled and disabled alike; callers
        /// decide how to present a disabled one — ordered by
        /// <see cref="GameDefinition.DisplaySortOrder"/> then by registration
        /// order for ties. Recomputed on each call (registration is a one-time
        /// startup step, not a hot path) so it is never stale.</summary>
        public IReadOnlyList<GameDefinition> All =>
            _registrationOrder
                .Select((definition, index) => (definition, index))
                .OrderBy(entry => entry.definition.DisplaySortOrder)
                .ThenBy(entry => entry.index)
                .Select(entry => entry.definition)
                .ToList();
    }
}
