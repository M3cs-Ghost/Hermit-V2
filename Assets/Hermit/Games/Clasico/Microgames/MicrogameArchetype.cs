namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>
    /// The C8.0 Design Lock's mechanical grammar, trimmed to exactly the four
    /// archetypes the C8.1 Gold Slice needs (see
    /// Docs/C8_CLASICO_DESIGN_LOCK.md, section D — ten archetypes total, only
    /// four implemented this phase). Every microgame concept declares exactly
    /// one of these; presentation (which world it wears) never leaks in here.
    /// </summary>
    public enum MicrogameArchetype
    {
        /// <summary>Western Shootout — aim a reticle at one of N labeled
        /// targets and confirm.</summary>
        AimSelect,

        /// <summary>TV Game Show — a binary left/right commit.</summary>
        ChooseSide,

        /// <summary>Balance Machine — nudge a value up/down until it matches
        /// a target, then confirm.</summary>
        Balance,

        /// <summary>Detective Lineup — point at the one anomalous item among
        /// several similar ones.</summary>
        DetectError
    }
}
