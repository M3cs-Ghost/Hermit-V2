using System;
using System.Collections.Generic;

namespace Hermit.Games.Content
{
    /// <summary>Wraps a single QuestionSet asset as an IContentProvider.</summary>
    public sealed class QuestionSetContentProvider : IContentProvider
    {
        private readonly QuestionSet _set;

        public QuestionSetContentProvider(QuestionSet set)
        {
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public IReadOnlyList<QuestionDefinition> DrawQuestions(int count, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var pool = new List<QuestionDefinition>(_set.Questions);
            var drawCount = Math.Min(count, pool.Count);
            var result = new List<QuestionDefinition>(drawCount);

            for (var i = 0; i < drawCount; i++)
            {
                var index = rng.Next(pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }
    }
}
