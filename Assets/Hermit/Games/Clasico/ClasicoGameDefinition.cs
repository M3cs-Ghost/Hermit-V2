using UnityEngine;
using Hermit.Games.Content;

namespace Hermit.Games.Clasico
{
    /// <summary>Static config for one Clasico session. An instance ships as
    /// Assets/Hermit/Data/Resources/ClasicoGameDefinition.asset.</summary>
    [CreateAssetMenu(fileName = "ClasicoGameDefinition", menuName = "Hermit/Games/Clasico Game Definition")]
    public sealed class ClasicoGameDefinition : GameDefinition
    {
        [SerializeField] private QuestionSet _questionSet;
        [SerializeField] private int _questionCount = 8;
        [SerializeField] private float _timePerQuestionSeconds = 8f;
        [SerializeField] private int _pointsPerCorrectAnswer = 100;
        [SerializeField] private int _maxSpeedBonusPoints = 50;
        [SerializeField] private float _feedbackDisplaySeconds = 0.9f;

        public QuestionSet QuestionSet => _questionSet;
        public int QuestionCount => _questionCount;

        /// <summary>0 (or less) means untimed — no auto-submit, no speed bonus.</summary>
        public float TimePerQuestionSeconds => _timePerQuestionSeconds;
        public int PointsPerCorrectAnswer => _pointsPerCorrectAnswer;
        public int MaxSpeedBonusPoints => _maxSpeedBonusPoints;
        public float FeedbackDisplaySeconds => _feedbackDisplaySeconds;

        /// <summary>Builds a definition from code instead of an Inspector asset —
        /// used by tests and by anything that needs a throwaway configuration.</summary>
        public static ClasicoGameDefinition CreateInMemory(
            string gameId,
            string displayName,
            QuestionSet questionSet,
            int questionCount,
            float timePerQuestionSeconds,
            int pointsPerCorrectAnswer,
            int maxSpeedBonusPoints,
            float feedbackDisplaySeconds)
        {
            var instance = CreateInstance<ClasicoGameDefinition>();
            instance.SetIdentity(gameId, displayName);
            instance._questionSet = questionSet;
            instance._questionCount = questionCount;
            instance._timePerQuestionSeconds = timePerQuestionSeconds;
            instance._pointsPerCorrectAnswer = pointsPerCorrectAnswer;
            instance._maxSpeedBonusPoints = maxSpeedBonusPoints;
            instance._feedbackDisplaySeconds = feedbackDisplaySeconds;
            return instance;
        }
    }
}
