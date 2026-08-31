using System;

namespace Hermit.Games.Content
{
    /// <summary>
    /// One question, fully decoupled from any game's mechanic. Domain/Topic/
    /// Difficulty/Tags exist ahead of a real need in C5/C6 only because they
    /// cost nothing to add and are exactly the metadata the future Knowledge
    /// Engine (single-source-of-truth academic content) is expected to need.
    ///
    /// C6 additions — see Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md,
    /// "Content IDs" / "Versioning" / "Source":
    /// - <see cref="Id"/> is the stable identity (author-assigned, survives
    ///   reorder/text edits) that ContentVersion, analytics, and any future
    ///   mastery tracking key off of — never an array index, never the prompt
    ///   text.
    /// - <see cref="ContentVersion"/> answers "which revision of this specific
    ///   question's wording/answers did a student see" — bump it by hand when
    ///   PromptText or Options change meaningfully.
    /// - <see cref="SourceId"/>/<see cref="SourceLabel"/> are optional
    ///   academic-traceability fields — no real textbook citations yet, just
    ///   the fields to hold them later without a schema change.
    /// - <see cref="ConfusableWith"/> is a free-text hint (e.g. "often
    ///   confused with X"), not a cross-reference id — linking to another
    ///   question's stable Id would be the natural upgrade once there is a
    ///   real need to query it, not before.
    /// </summary>
    [Serializable]
    public sealed class QuestionDefinition
    {
        public string Id;
        public string Domain;
        public string Topic;
        public string Difficulty;
        public string[] Tags = Array.Empty<string>();
        public string PromptText;
        public AnswerOption[] Options = Array.Empty<AnswerOption>();

        public int ContentVersion = 1;
        public string SourceId = string.Empty;
        public string SourceLabel = string.Empty;
        public string ConfusableWith = string.Empty;
    }
}
