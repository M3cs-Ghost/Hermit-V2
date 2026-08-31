using System;

namespace Hermit.Games.Content
{
    /// <summary>One selectable answer for a QuestionDefinition. Plain data —
    /// deliberately public fields, not properties, so a QuestionSet asset can
    /// hold an Inspector-editable array of these without extra boilerplate.</summary>
    [Serializable]
    public sealed class AnswerOption
    {
        public string Text;
        public bool IsCorrect;
    }
}
