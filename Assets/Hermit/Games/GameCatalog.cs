using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hermit.Games
{
    /// <summary>
    /// The single root asset the composition root loads via
    /// <c>Resources.Load</c> — replacing C5's per-game fixed path
    /// (<c>Resources.Load&lt;ClasicoGameDefinition&gt;("ClasicoGameDefinition")</c>).
    ///
    /// Adding a game to the game selector is now: create its
    /// GameDefinition subclass and a concrete asset (unavoidable — that is
    /// the game's own data), then drag that asset into this catalog's list in
    /// the Inspector. Zero changes to Hermit.Runtime, the selector, or
    /// GameRegistry. See Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md, "Resources
    /// strategy" for the alternatives considered (ScriptableObject registry
    /// won over Addressables/asset-discovery — simplest option that actually
    /// removes the per-game fixed path).
    /// </summary>
    [CreateAssetMenu(fileName = "GameCatalog", menuName = "Hermit/Game Catalog")]
    public sealed class GameCatalog : ScriptableObject
    {
        [SerializeField] private GameDefinition[] _games = Array.Empty<GameDefinition>();

        public IReadOnlyList<GameDefinition> Games => _games;

        /// <summary>Builds a catalog from code — used by tests; kept generally
        /// useful (e.g. a future procedurally-assembled catalog) rather than
        /// named as a test-only helper.</summary>
        public static GameCatalog CreateInMemory(GameDefinition[] games)
        {
            var instance = CreateInstance<GameCatalog>();
            instance._games = games ?? Array.Empty<GameDefinition>();
            return instance;
        }
    }
}
