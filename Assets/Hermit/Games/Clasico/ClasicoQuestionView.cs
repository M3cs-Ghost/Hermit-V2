using System;
using System.Collections.Generic;
using Hermit.Games.Content;

namespace Hermit.Games.Clasico
{
    /// <summary>Read-only projection of "the question on screen right now",
    /// built once per question with its options already shuffled. A presenter
    /// (Hermit.Runtime) reads this; it never sees the raw QuestionDefinition or
    /// which option is correct ahead of a reveal.</summary>
    public sealed class ClasicoQuestionView
    {
        public string QuestionId { get; }
        public string PromptText { get; }
        public IReadOnlyList<string> OptionTexts { get; }
        public int QuestionNumber { get; }
        public int TotalQuestions { get; }

        public ClasicoQuestionView(QuestionDefinition question, AnswerOption[] shuffledOptions, int zeroBasedIndex, int totalQuestions)
        {
            QuestionId = question.Id;
            PromptText = question.PromptText;
            TotalQuestions = totalQuestions;
            QuestionNumber = zeroBasedIndex + 1;

            var texts = new string[shuffledOptions.Length];
            for (var i = 0; i < shuffledOptions.Length; i++)
            {
                texts[i] = shuffledOptions[i].Text;
            }

            OptionTexts = Array.AsReadOnly(texts);
        }
    }
}
