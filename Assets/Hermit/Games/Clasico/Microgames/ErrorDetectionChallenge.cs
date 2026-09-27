using System;

namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>
    /// Academic Challenge Type: spot the one item that doesn't belong to the
    /// stated family. <see cref="AnomalyIndex"/> is the single source of
    /// truth for which item is wrong, by its AUTHORED/logical position in
    /// <see cref="Items"/> — by construction there is always exactly one
    /// (see <see cref="MicrogameContentValidator"/>). Compatible archetype
    /// for C8.1: <see cref="MicrogameArchetype.DetectError"/>.
    ///
    /// C8.1g.2: this authored index is deliberately NEVER the index the
    /// player actually sees on screen — <see cref="Hermit.Games.Clasico.ClasicoSessionDirector"/>
    /// draws a fresh, seeded random permutation of <see cref="Items"/> every
    /// round (never mutating this array) and hands
    /// <see cref="Hermit.Runtime.GameFramework.Microgames.DetectiveLineupPresenter"/>
    /// the DISPLAY order and the DISPLAY anomaly slot instead. This is what
    /// closes the pre-C8.1g.2 "anomaly is always the 4th suspect" position
    /// cheat found in the C8.1g.1 audit — content only ever expresses
    /// identity/logic, never a fixed screen position.
    ///
    /// Also added this phase: <see cref="CasePrompt"/>, <see cref="RuleLabel"/>
    /// (the old <c>GroupLabel</c>, renamed to match the Detective brief's own
    /// vocabulary) and <see cref="Explanation"/> — the teaching-recap text
    /// every round must now carry, per Docs/C8_1G_DETECTIVE... section 10.
    /// </summary>
    [Serializable]
    public sealed class ErrorDetectionChallenge
    {
        public string Id;
        public int ContentVersion = 1;

        /// <summary>"easy" or "medium" for this phase's Gold slice (see
        /// <see cref="MicrogameContentValidator"/>) — "easy" pairs broad,
        /// obvious families (assets vs. liabilities); "medium" asks for a
        /// closer classification (current/non-current, debit/credit nature,
        /// expense/revenue family). Deliberately no "hard" tier yet.</summary>
        public string Difficulty = "easy";

        /// <summary>The framing line shown above the lineup while it's still
        /// a live decision — kept short and reusable across rounds (brief
        /// section 6: "avoid paragraphs during Decision"). e.g. "Uno de
        /// estos sospechosos no pertenece al grupo."</summary>
        public string CasePrompt;

        /// <summary>What the group is supposed to share, e.g. "Cuentas de
        /// Activo" — was <c>GroupLabel</c> before C8.1g.2.</summary>
        public string RuleLabel;

        public string[] Items = Array.Empty<string>();
        public int AnomalyIndex;

        /// <summary>The one-sentence teaching explanation shown in the
        /// post-reveal recap — why the anomaly item doesn't belong, e.g.
        /// "Cuentas por pagar es un pasivo; las otras tres son cuentas de
        /// activo." Required (see <see cref="MicrogameContentValidator"/>);
        /// concise by design, never a lecture.</summary>
        public string Explanation;
    }
}
