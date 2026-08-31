using System;
using System.Collections.Generic;

namespace Hermit.Games.Content
{
    /// <summary>Supplies questions to a game engine without the engine ever
    /// touching a QuestionSet asset (or any storage detail) directly.</summary>
    public interface IContentProvider
    {
        /// <summary>Draws up to <paramref name="count"/> distinct questions
        /// (fewer if the pool is smaller) using <paramref name="rng"/> for
        /// selection order. Sampling without replacement — a question never
        /// repeats within one draw.</summary>
        IReadOnlyList<QuestionDefinition> DrawQuestions(int count, Random rng);
    }
}
