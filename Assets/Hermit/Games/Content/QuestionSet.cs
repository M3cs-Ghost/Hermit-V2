using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hermit.Games.Content
{
    /// <summary>
    /// A named, Inspector-editable collection of questions. One asset per
    /// content pack — <see cref="SchemaVersion"/> (added in C6) is the
    /// *structural* shape version of this collection (bump it if
    /// QuestionDefinition's fields change shape in a way old data can't be
    /// read against); it is deliberately separate from each question's own
    /// <see cref="QuestionDefinition.ContentVersion"/>, which tracks that
    /// question's own wording revisions. Swapping which QuestionSet a game
    /// reads from never requires touching game logic.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestionSet", menuName = "Hermit/Games/Question Set")]
    public sealed class QuestionSet : ScriptableObject
    {
        [SerializeField] private string _setId = string.Empty;
        [SerializeField] private string _description = string.Empty;
        [SerializeField] private int _schemaVersion = 1;
        [SerializeField] private QuestionDefinition[] _questions = Array.Empty<QuestionDefinition>();

        public string SetId => _setId;
        public string Description => _description;
        public int SchemaVersion => _schemaVersion;
        public IReadOnlyList<QuestionDefinition> Questions => _questions;

        /// <summary>Builds a QuestionSet from code instead of an Inspector asset —
        /// used by tests, and available later for a procedurally-imported set
        /// (e.g. a future Knowledge Engine export) without needing a second type.</summary>
        public static QuestionSet CreateInMemory(string setId, string description, QuestionDefinition[] questions, int schemaVersion = 1)
        {
            var instance = CreateInstance<QuestionSet>();
            instance._setId = setId;
            instance._description = description;
            instance._questions = questions ?? Array.Empty<QuestionDefinition>();
            instance._schemaVersion = schemaVersion;
            return instance;
        }
    }
}
