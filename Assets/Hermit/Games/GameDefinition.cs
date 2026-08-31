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
        [SerializeField] private string _shortDescription = string.Empty;
        [SerializeField] private bool _isEnabled = true;
        [SerializeField] private int _displaySortOrder;

        public string GameId => _gameId;
        public string DisplayName => _displayName;

        /// <summary>Short selector flavor text. Optional — empty is fine.</summary>
        public string ShortDescription => _shortDescription;

        /// <summary>False means the selector still lists this game (so its
        /// existence is visible) but must not let the player launch it — e.g. a
        /// registered-but-unfinished game. Not the same as "not registered".</summary>
        public bool IsEnabled => _isEnabled;

        /// <summary>Lower sorts first in the selector. Ties break by
        /// registration order (see GameRegistry).</summary>
        public int DisplaySortOrder => _displaySortOrder;

        /// <summary>
        /// Creates a fresh engine instance for one playthrough. Each concrete
        /// definition knows how to build its own engine — this is what lets
        /// GameRegistry, GameFlowController, and the selector stay completely
        /// game-agnostic: nothing outside a GameDefinition subclass ever writes
        /// "new XGameEngine()".
        /// </summary>
        public abstract IGameEngine CreateEngine();

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

        /// <summary>Same code-created-instance rationale as SetIdentity, for the
        /// selector-facing metadata added in C6.</summary>
        protected void SetSelectorMetadata(bool isEnabled, int displaySortOrder, string shortDescription = "")
        {
            _isEnabled = isEnabled;
            _displaySortOrder = displaySortOrder;
            _shortDescription = shortDescription;
        }
    }
}
