using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hermit.Games.Content
{
    /// <summary>
    /// A named, Inspector-editable collection of questions. One asset per
    /// content pack — C5 ships exactly one, explicitly marked as sample data
    /// (see Assets/Hermit/Content/Resources/C5SampleQuestions.asset). Swapping
    /// which QuestionSet a game reads from never requires touching game logic.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestionSet", menuName = "Hermit/Games/Question Set")]
    public sealed class QuestionSet : ScriptableObject
    {
        [SerializeField] private string _setId = string.Empty;
        [SerializeField] private string _description = string.Empty;
        [SerializeField] private QuestionDefinition[] _questions = Array.Empty<QuestionDefinition>();

        public string SetId => _setId;
        public string Description => _description;
        public IReadOnlyList<QuestionDefinition> Questions => _questions;

        /// <summary>Builds a QuestionSet from code instead of an Inspector asset —
        /// used by tests, and available later for a procedurally-imported set
        /// (e.g. a future Knowledge Engine export) without needing a second type.</summary>
        public static QuestionSet CreateInMemory(string setId, string description, QuestionDefinition[] questions)
        {
            var instance = CreateInstance<QuestionSet>();
            instance._setId = setId;
            instance._description = description;
            instance._questions = questions ?? Array.Empty<QuestionDefinition>();
            return instance;
        }
    }
}
