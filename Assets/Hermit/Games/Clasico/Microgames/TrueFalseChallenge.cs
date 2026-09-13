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
    }
}
