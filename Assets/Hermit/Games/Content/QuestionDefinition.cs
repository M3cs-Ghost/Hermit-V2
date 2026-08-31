using System;

namespace Hermit.Games.Content
{
    /// <summary>
    /// One question, fully decoupled from any game's mechanic. Domain/Topic/
    /// Difficulty/Tags exist now — ahead of a real need in C5 — only because
    /// they cost nothing to add and are exactly the metadata the future
    /// Knowledge Engine (single-source-of-truth academic content) is expected
    /// to need; C5 itself only reads PromptText/Options. No misconception/
    /// confusable/source fields yet — those would be speculative for a
    /// 10-question sample set.
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
    }
}
