using System;

namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>
    /// Academic Challenge Type: spot the one item that doesn't belong to the
    /// stated family. <see cref="AnomalyIndex"/> is the single source of
    /// truth for which item is wrong — by construction there is always
    /// exactly one (see <see cref="MicrogameContentValidator"/>).
    /// Compatible archetype for C8.1: <see cref="MicrogameArchetype.DetectError"/>.
    /// </summary>
    [Serializable]
    public sealed class ErrorDetectionChallenge
    {
        public string Id;
        public int ContentVersion = 1;
        public string Difficulty = "easy";

        /// <summary>What the group is supposed to share, e.g. "Cuentas de Activo".</summary>
        public string GroupLabel;

        public string[] Items = Array.Empty<string>();
        public int AnomalyIndex;
    }
}
