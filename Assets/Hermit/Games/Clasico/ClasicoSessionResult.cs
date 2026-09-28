using System.Collections.Generic;
using System.Linq;
using Hermit.Games.Clasico.Microgames;

namespace Hermit.Games.Clasico
{
    /// <summary>
    /// C9.1: one resolved Clásico round, recorded by
    /// <see cref="ClasicoSessionDirector"/> at the exact moment the round
    /// resolves (answer or timeout). Read-only session data for systems
    /// OUTSIDE the game mode (the Hermit economy) — Clásico itself never
    /// reads it back, and it carries no currency concept of its own.
    /// </summary>
    public sealed class ClasicoRoundRecord
    {
        public MicrogameArchetype Archetype { get; }

        public bool Correct { get; }

        /// <summary>True only when the player actually committed an answer
        /// through the real input path — a selection was submitted
        /// (Western/Game Show/Detective), or at least the debit pick was
        /// committed (Balance). A pure decision timeout is false. Never
        /// inferred from elapsed time.</summary>
        public bool Interacted { get; }

        /// <summary>Seconds from the start of the round's Decision phase to
        /// its resolution (the answer, or the timeout).</summary>
        public float ResponseSeconds { get; }

        /// <summary>That archetype's own allowed answer window for this
        /// round (Western 3.2s, Game Show/Detective 4.2s, Balance 7s with the
        /// shipped definition).</summary>
        public float AnswerWindowSeconds { get; }

        public ClasicoRoundRecord(MicrogameArchetype archetype, bool correct, bool interacted, float responseSeconds, float answerWindowSeconds)
        {
            Archetype = archetype;
            Correct = correct;
            Interacted = interacted;
            ResponseSeconds = responseSeconds;
            AnswerWindowSeconds = answerWindowSeconds;
        }
    }

    /// <summary>
    /// C9.1: the gameplay facts of one finished Clásico session, produced by
    /// <see cref="ClasicoSessionDirector.BuildResult"/> alongside the
    /// generic <see cref="GameResult"/>. This is the ONLY thing the Hermit
    /// economy reads from Clásico — it never scrapes UI or presenter state,
    /// and Clásico never touches currency.
    /// </summary>
    public sealed class ClasicoSessionResult
    {
        /// <summary>The session's own unique id (<see cref="GameSession.SessionId"/>)
        /// — doubles as the reward idempotency key.</summary>
        public string SessionId { get; }

        /// <summary>True when the session reached its natural end (all
        /// planned rounds resolved); false for an aborted session.</summary>
        public bool CompletedNaturally { get; }

        /// <summary>How many rounds the session was built with (9 in the
        /// shipped definition).</summary>
        public int PlannedRounds { get; }

        public IReadOnlyList<ClasicoRoundRecord> Rounds { get; }

        public int CorrectAnswers => Rounds.Count(r => r.Correct);

        public int InteractedRounds => Rounds.Count(r => r.Interacted);

        public IReadOnlyCollection<MicrogameArchetype> ArchetypesPlayed =>
            Rounds.Select(r => r.Archetype).Distinct().ToList();

        public ClasicoSessionResult(string sessionId, bool completedNaturally, int plannedRounds, IEnumerable<ClasicoRoundRecord> rounds)
        {
            SessionId = sessionId;
            CompletedNaturally = completedNaturally;
            PlannedRounds = plannedRounds;
            Rounds = (rounds ?? Enumerable.Empty<ClasicoRoundRecord>()).ToList();
        }
    }
}
