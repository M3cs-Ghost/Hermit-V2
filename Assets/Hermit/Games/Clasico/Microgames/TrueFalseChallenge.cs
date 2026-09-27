using System;

namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>
    /// Academic Challenge Type: is this accounting statement true or false.
    /// Compatible archetype for C8.1: <see cref="MicrogameArchetype.ChooseSide"/>.
    /// </summary>
    [Serializable]
    public sealed class TrueFalseChallenge
    {
        public string Id;
        public int ContentVersion = 1;
        public string Difficulty = "easy";
        public string Statement;
        public bool IsTrue;

        /// <summary>C8.1i: one-to-two sentence explanation of WHY the
        /// statement is true or false. Not currently displayed by
        /// <see cref="Hermit.Runtime.GameFramework.Microgames.GameShowPresenter"/>
        /// (Game Show's Gold presentation is frozen and untouched this
        /// phase) — carried as content data now so the teaching text exists
        /// and is validated, ready for a future presentation hookup,
        /// mirroring <see cref="DebitCreditChallenge.FeedbackExplanation"/>.</summary>
        public string FeedbackExplanation;
    }
}
