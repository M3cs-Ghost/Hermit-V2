using Hermit.Core;

namespace Hermit.Games.Analytics
{
    /// <summary>Logs every event through HermitLog so the vertical slice's
    /// analytics hooks are visible in the Console during manual testing,
    /// without sending anything anywhere.</summary>
    public sealed class HermitLogAnalyticsSink : IGameAnalyticsSink
    {
        public void GameStarted(string gameId, string sessionId)
        {
            HermitLog.Info($"[Analytics] game_started game={gameId} session={sessionId}");
        }

        public void QuestionPresented(string gameId, string sessionId, string questionId, int questionIndex)
        {
            HermitLog.Info($"[Analytics] question_presented game={gameId} session={sessionId} question={questionId} index={questionIndex}");
        }

        public void AnswerSubmitted(string gameId, string sessionId, string questionId, bool correct, float answerTimeSeconds)
        {
            HermitLog.Info($"[Analytics] answer_submitted game={gameId} session={sessionId} question={questionId} correct={correct} timeSec={answerTimeSeconds:0.00}");
        }

        public void GameCompleted(GameResult result)
        {
            HermitLog.Info($"[Analytics] game_completed game={result.GameId} session={result.SessionId} score={result.Score} accuracy={result.AccuracyPercent:0.0}%");
        }

        public void GameAborted(GameResult result)
        {
            HermitLog.Info($"[Analytics] game_aborted game={result.GameId} session={result.SessionId} score={result.Score}");
        }
    }
}
