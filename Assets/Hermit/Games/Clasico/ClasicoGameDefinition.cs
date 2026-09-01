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

        [Header("C7 — round rhythm / combo")]
        [SerializeField] private float _countdownDurationSeconds;
        [SerializeField] private int _streakBonusThreshold = 3;
        [SerializeField] private int _streakBonusPoints = 30;

        public QuestionSet QuestionSet => _questionSet;
        public int QuestionCount => _questionCount;

        /// <summary>0 (or less) means untimed — no auto-submit, no speed bonus.</summary>
        public float TimePerQuestionSeconds => _timePerQuestionSeconds;
        public int PointsPerCorrectAnswer => _pointsPerCorrectAnswer;
        public int MaxSpeedBonusPoints => _maxSpeedBonusPoints;
        public float FeedbackDisplaySeconds => _feedbackDisplaySeconds;

        /// <summary>0 (or less) means no countdown — the session starts on the
        /// first question immediately, same as before C7. Defaults to 0 so
        /// every existing CreateInMemory call site (tests) keeps behaving
        /// exactly as before unless it opts in.</summary>
        public float CountdownDurationSeconds => _countdownDurationSeconds;

        /// <summary>0 (either field) disables the streak bonus entirely.</summary>
        public int StreakBonusThreshold => _streakBonusThreshold;
        public int StreakBonusPoints => _streakBonusPoints;

        public override IGameEngine CreateEngine() => new ClasicoGameEngine();

        /// <summary>Builds a definition from code instead of an Inspector asset —
        /// used by tests and by anything that needs a throwaway configuration.
        /// countdownDurationSeconds/streakBonus* default to "off" so pre-C7
        /// call sites are unaffected unless they opt in.</summary>
        public static ClasicoGameDefinition CreateInMemory(
            string gameId,
            string displayName,
            QuestionSet questionSet,
            int questionCount,
            float timePerQuestionSeconds,
            int pointsPerCorrectAnswer,
            int maxSpeedBonusPoints,
            float feedbackDisplaySeconds,
            float countdownDurationSeconds = 0f,
            int streakBonusThreshold = 0,
            int streakBonusPoints = 0)
        {
            var instance = CreateInstance<ClasicoGameDefinition>();
            instance.SetIdentity(gameId, displayName);
            instance._questionSet = questionSet;
            instance._questionCount = questionCount;
            instance._timePerQuestionSeconds = timePerQuestionSeconds;
            instance._pointsPerCorrectAnswer = pointsPerCorrectAnswer;
            instance._maxSpeedBonusPoints = maxSpeedBonusPoints;
            instance._feedbackDisplaySeconds = feedbackDisplaySeconds;
            instance._countdownDurationSeconds = countdownDurationSeconds;
            instance._streakBonusThreshold = streakBonusThreshold;
            instance._streakBonusPoints = streakBonusPoints;
            return instance;
        }
    }
}
