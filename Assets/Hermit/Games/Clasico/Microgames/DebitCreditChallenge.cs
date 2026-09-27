using System;

namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>
    /// Academic Challenge Type: identify which account is debited (se carga)
    /// and which is credited (se acredita) for a short transaction — the
    /// Design Lock's DebitCredit type (Docs/C8_CLASICO_DESIGN_LOCK.md,
    /// section E), exercising its "Balance"-compatible pairing rather than
    /// the originally-sketched Choose Side/Boxing one (see
    /// Docs/C8_1F_BALANCE_MECHANIC_REDESIGN.md, section 1). Replaces
    /// <see cref="EquationChallenge"/> as Balance's shipped content — that
    /// class is retired from the active Balance runtime, not deleted.
    /// Compatible archetype for C8.1: <see cref="MicrogameArchetype.Balance"/>.
    /// </summary>
    [Serializable]
    public sealed class DebitCreditChallenge
    {
        public string Id;
        public int ContentVersion = 1;
        public string Difficulty = "easy";

        /// <summary>The short transaction, e.g. "Se compra mobiliario al
        /// crédito.". Kept short by design — see the redesign doc, section 12.</summary>
        public string TransactionText;

        /// <summary>Must equal exactly one entry of <see cref="AccountOptions"/>,
        /// and must differ from <see cref="CorrectCreditAccount"/>.</summary>
        public string CorrectDebitAccount;

        /// <summary>Must equal exactly one entry of <see cref="AccountOptions"/>,
        /// and must differ from <see cref="CorrectDebitAccount"/>.</summary>
        public string CorrectCreditAccount;

        /// <summary>Shared pool shown for BOTH the "se carga" and "se
        /// acredita" steps — same list both times, never reshuffled or
        /// re-curated between steps (redesign doc, section 7).</summary>
        public string[] AccountOptions = Array.Empty<string>();

        /// <summary>Optional, e.g. "L 25,000". Never used for correctness —
        /// only the two account choices determine the outcome.</summary>
        public string AmountDisplay;

        /// <summary>Optional one-line explanation shown under the teaching
        /// recap. Falls back to just the transaction + correct pair when empty.</summary>
        public string FeedbackExplanation;
    }
}
