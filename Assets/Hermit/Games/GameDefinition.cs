using UnityEngine;

namespace Hermit.Games
{
    /// <summary>
    /// Static/config metadata for one game, authored as a ScriptableObject asset
    /// so designers can tune values in the Inspector without touching code.
    ///
    /// Concrete games (e.g. Clasico) subclass this to add their own config
    /// fields. Identity (GameId/DisplayName) lives on the base type because
    /// <see cref="GameRegistry"/> and <see cref="GameFlowController"/> only
    /// ever need to know a game by these two values — everything else is
    /// opaque to the framework.
    /// </summary>
    public abstract class GameDefinition : ScriptableObject
    {
        [SerializeField] private string _gameId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;

        public string GameId => _gameId;
        public string DisplayName => _displayName;

        /// <summary>
        /// Lets a subclass set its own identity from a code-created instance
        /// (e.g. <c>ClasicoGameDefinition.CreateInMemory</c>) instead of an
        /// Inspector-authored asset. Inspector-authored assets never call this —
        /// the serialized fields above already hold the value.
        /// </summary>
        protected void SetIdentity(string gameId, string displayName)
        {
            _gameId = gameId;
            _displayName = displayName;
        }
    }
}
