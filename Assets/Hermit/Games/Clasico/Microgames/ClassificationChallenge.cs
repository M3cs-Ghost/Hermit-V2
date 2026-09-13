using System;

namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>
    /// Academic Challenge Type: "which category does this concept belong
    /// to" (see Docs/C8_CLASICO_DESIGN_LOCK.md, section E). Pure data — knows
    /// nothing about Western Shootout, reticles, or outlaws. Compatible
    /// archetype for C8.1: <see cref="MicrogameArchetype.AimSelect"/>.
    /// </summary>
    [Serializable]
    public sealed class ClassificationChallenge
    {
        public string Id;
        public int ContentVersion = 1;
        public string Difficulty = "easy";

        /// <summary>The concept being classified, e.g. "Cuentas por cobrar".</summary>
        public string ConceptLabel;

        /// <summary>Must equal exactly one entry of <see cref="CategoryOptions"/>.</summary>
        public string CorrectCategory;

        public string[] CategoryOptions = Array.Empty<string>();
    }
}
