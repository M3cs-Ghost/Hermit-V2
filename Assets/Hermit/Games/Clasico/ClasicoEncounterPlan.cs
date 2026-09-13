using Hermit.Games.Clasico.Microgames;

namespace Hermit.Games.Clasico
{
    /// <summary>
    /// C8.1d.1 — the minimum local concept needed to represent "one intro,
    /// N consecutive challenges, one outro" (see
    /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Western Encounter Presentation").
    /// Deliberately not a generic engine-wide framework — this is consumed
    /// only by <see cref="ClasicoSessionDirector"/>, which flattens a list of
    /// these into its existing per-round <c>MicrogameArchetype[]</c> sequence
    /// so every other part of the director (challenge drawing, scoring,
    /// <c>_index</c>/<c>TotalMicrogames</c>) needs no change at all — an
    /// Encounter is just metadata about how many *consecutive* rounds one
    /// archetype occupies and whether it owns a themed intro/outro, not a new
    /// execution model.
    /// </summary>
    internal readonly struct ClasicoEncounterPlan
    {
        public readonly MicrogameArchetype Archetype;

        /// <summary>How many consecutive rounds this encounter spans. 1 for
        /// every archetype except Western in this phase — see
        /// <see cref="ClasicoGameDefinition.WesternEncounterRoundCount"/>.</summary>
        public readonly int RoundCount;

        public ClasicoEncounterPlan(MicrogameArchetype archetype, int roundCount)
        {
            Archetype = archetype;
            RoundCount = roundCount;
        }
    }
}
