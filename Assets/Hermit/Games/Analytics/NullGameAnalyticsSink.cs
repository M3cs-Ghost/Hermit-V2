namespace Hermit.Games.Analytics
{
    /// <summary>No-op sink — the default for tests and for any GameContext that
    /// does not care about analytics.</summary>
    public sealed class NullGameAnalyticsSink : IGameAnalyticsSink
    {
        public static readonly NullGameAnalyticsSink Instance = new NullGameAnalyticsSink();

        private NullGameAnalyticsSink()
        {
        }

        public void GameStarted(string gameId, string sessionId)
        {
        }

        public void QuestionPresented(string gameId, string sessionId, string questionId, int questionIndex, int contentVersion)
        {
        }

        public void AnswerSubmitted(string gameId, string sessionId, string questionId, bool correct, float answerTimeSeconds, int contentVersion)
        {
        }

        public void GameCompleted(GameResult result)
        {
        }

        public void GameAborted(GameResult result)
        {
        }
    }
}
