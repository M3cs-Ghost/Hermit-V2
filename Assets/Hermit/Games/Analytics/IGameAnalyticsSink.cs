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
        void QuestionPresented(string gameId, string sessionId, string questionId, int questionIndex);
        void AnswerSubmitted(string gameId, string sessionId, string questionId, bool correct, float answerTimeSeconds);
        void GameCompleted(GameResult result);
        void GameAborted(GameResult result);
    }
}
