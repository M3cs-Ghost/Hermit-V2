namespace Hermit.Games.Analytics
{
    /// <summary>
    /// Conceptual analytics events only — nothing here sends anything to
    /// Supabase or any backend. C5 wires this to a console-logging sink; a
    /// later phase can swap in a real sink without touching a single game.
    /// </summary>
    public interface IGameAnalyticsSink
    {
        void GameStarted(string gameId, string sessionId);

        /// <summary>contentVersion is the presented question's own
        /// QuestionDefinition.ContentVersion (added in C6) — 0 for a game with
        /// no content-set concept.</summary>
        void QuestionPresented(string gameId, string sessionId, string questionId, int questionIndex, int contentVersion);

        void AnswerSubmitted(string gameId, string sessionId, string questionId, bool correct, float answerTimeSeconds, int contentVersion);
        void GameCompleted(GameResult result);
        void GameAborted(GameResult result);
    }
}
