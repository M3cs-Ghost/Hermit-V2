namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>
    /// What <see cref="ClasicoSessionDirector"/> needs from whichever
    /// archetype engine is currently active, regardless of which one it is —
    /// the director orchestrates through this alone, never through a
    /// per-archetype switch on game logic (only presentation routing does
    /// that, in Hermit.Runtime). Deliberately just two concrete
    /// implementations exist for C8.1
    /// (<see cref="SelectionMicrogameEngine"/> covers AimSelect/ChooseSide/
    /// DetectError — all three reduce to "pick 1 of N, compare to a correct
    /// index" at the data level; only Balance needed a distinct shape) — see
    /// Docs/C8_1_GOLD_MICROGAME_SLICE.md, "Migration strategy" for why that
    /// collapse is a legitimate reading of the grammar, not a shortcut around it.
    /// </summary>
    internal interface IMicrogameEngine
    {
        /// <summary>Called once per frame during the Decision phase.</summary>
        void Tick(float deltaSeconds);

        /// <summary>True once an answer has been locked in — either the
        /// player acted, or the decision window ran out.</summary>
        bool IsResolved { get; }

        bool IsCorrect { get; }

        /// <summary>1 at the start of the decision window, decaying linearly
        /// to 0 at the time limit — same contract C7's timer bar already used.</summary>
        float DecisionFraction01 { get; }
    }
}
