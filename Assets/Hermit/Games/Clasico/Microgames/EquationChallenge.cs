using System;

namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>
    /// Academic Challenge Type: complete the accounting identity (Activo =
    /// Pasivo + Patrimonio) by nudging the unknown value until it balances.
    /// <see cref="StartValue"/> is pre-computed to differ from
    /// <see cref="CorrectValue"/> by a nonzero multiple of
    /// <see cref="StepSize"/> — a player who confirms without nudging at all
    /// is always wrong, by construction, never wrong by accident of tuning.
    /// Compatible archetype for C8.1: <see cref="MicrogameArchetype.Balance"/>.
    /// </summary>
    [Serializable]
    public sealed class EquationChallenge
    {
        public string Id;
        public int ContentVersion = 1;
        public string Difficulty = "easy";

        public string KnownLabelA = "Activo";
        public float KnownValueA;
        public string KnownLabelB = "Pasivo";
        public float KnownValueB;
        public string UnknownLabel = "Patrimonio";

        public float CorrectValue;
        public float StepSize;
        public float StartValue;
    }
}
