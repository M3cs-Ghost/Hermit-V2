using System;
using System.Linq;
using NUnit.Framework;
using Hermit.Games.Clasico.Microgames;

namespace Hermit.Tests.EditMode
{
    public class ClasicoMicrogameLibraryTests
    {
        [Test]
        public void BuildSequence_NeverHasAdjacentDuplicates_AcrossManySeedsAndCounts()
        {
            foreach (var count in new[] { 4, 8, 9, 12, 16, 18 })
            {
                for (var seed = 0; seed < 200; seed++)
                {
                    var sequence = ClasicoMicrogameLibrary.BuildSequence(count, new Random(seed));
                    for (var i = 1; i < sequence.Length; i++)
                    {
                        Assert.AreNotEqual(sequence[i - 1], sequence[i],
                            $"count={count}, seed={seed}: adjacent duplicate at index {i} ({string.Join(",", sequence)}).");
                    }
                }
            }
        }

        [Test]
        public void BuildSequence_ContainsEveryArchetypeAtLeastTwice_ForACountOfEightOrMore()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var sequence = ClasicoMicrogameLibrary.BuildSequence(9, new Random(seed));
                foreach (MicrogameArchetype archetype in Enum.GetValues(typeof(MicrogameArchetype)))
                {
                    Assert.GreaterOrEqual(sequence.Count(a => a == archetype), 2, $"seed={seed}: {archetype} appeared fewer than twice.");
                }
            }
        }

        [Test]
        public void BuildSequence_ReturnsExactlyTheRequestedCount()
        {
            Assert.AreEqual(0, ClasicoMicrogameLibrary.BuildSequence(0, new Random(0)).Length);
            Assert.AreEqual(1, ClasicoMicrogameLibrary.BuildSequence(1, new Random(0)).Length);
            Assert.AreEqual(9, ClasicoMicrogameLibrary.BuildSequence(9, new Random(0)).Length);
        }
    }
}
