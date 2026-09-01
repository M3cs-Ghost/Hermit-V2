using System;
using System.Collections.Generic;
using Hermit.Core;
using Hermit.Games.Content;

namespace Hermit.Games.Clasico
{
    /// <summary>
    /// Clasico's pure game logic: a brief countdown, then draw N questions,
    /// show one at a time, accept one answer, reveal briefly, advance;
    /// finished once every question has been answered. Knows nothing about
    /// uGUI, the Input System, or any concrete UI — a presenter in
    /// Hermit.Runtime reads <see cref="CurrentView"/> / <see cref="IsRevealing"/>
    /// / <see cref="IsCountingDown"/> / <see cref="SelectedOptionIndex"/> /
    /// <see cref="CorrectOptionIndex"/> and calls <see cref="SubmitAnswer"/>.
    ///
    /// C7 added the Countdown phase and streak tracking here — both are
    /// exactly the kind of "must be deterministically testable" logic the C7
    /// brief calls out as belonging in pure engine code, not the presenter.
    /// The *visual* countdown/timer/streak presentation is Hermit.Runtime's
    /// job (ClasicoGameHost/ClasicoHud); this class only ever exposes numbers
    /// and booleans, never colors or animation state.
    /// </summary>
    public sealed class ClasicoGameEngine : IGameEngine
    {
        private enum Phase
        {
            Countdown,
            AwaitingAnswer,
            Revealing
        }

        private ClasicoGameDefinition _definition;
        private GameContext _context;
        private GameSession _session;
        private IReadOnlyList<QuestionDefinition> _questions;
        private int _questionIndex = -1;
        private AnswerOption[] _shuffledOptions;
        private float _countdownTimer;
        private float _questionTimer;
        private float _revealTimer;
        private Phase _phase;
        private bool _finished;
        private int _selectedOptionIndex = -1;
        private int _lastStreakBonus;

        public bool IsFinished => _finished;
        public ClasicoQuestionView CurrentView { get; private set; }
        public bool IsRevealing => !_finished && _phase == Phase.Revealing;
        public bool IsCountingDown => !_finished && _phase == Phase.Countdown;
        public int SelectedOptionIndex => _selectedOptionIndex;

        /// <summary>Whole seconds left in the countdown, for a "3, 2, 1" style
        /// display — 0 once the countdown has elapsed (that frame still
        /// renders as the countdown until Tick advances past it).</summary>
        public int CountdownSecondsRemaining
        {
            get
            {
                if (!IsCountingDown)
                {
                    return 0;
                }

                var remaining = _definition.CountdownDurationSeconds - _countdownTimer;
                return (int)Math.Ceiling(Math.Max(0.0, (double)remaining));
            }
        }

        /// <summary>1 at the start of the decision window, decaying linearly to
        /// 0 at the time limit. Always 1 outside AwaitingAnswer or when
        /// untimed — a presenter can drive a timer bar directly off this
        /// without special-casing "no timer configured".</summary>
        public float QuestionTimeFraction01
        {
            get
            {
                if (_finished || _phase != Phase.AwaitingAnswer || _definition.TimePerQuestionSeconds <= 0f)
                {
                    return 1f;
                }

                var remaining = _definition.TimePerQuestionSeconds - _questionTimer;
                var fraction = remaining / _definition.TimePerQuestionSeconds;
                return (float)Math.Max(0.0, Math.Min(1.0, fraction));
            }
        }

        /// <summary>The streak bonus (if any) awarded on the most recent
        /// SubmitAnswer — for the presenter to call out "+30 combo!" style
        /// feedback separately from the base/speed score.</summary>
        public int LastStreakBonus => _lastStreakBonus;

        public int CorrectOptionIndex
        {
            get
            {
                if (!IsRevealing || _shuffledOptions == null)
                {
                    return -1;
                }

                for (var i = 0; i < _shuffledOptions.Length; i++)
                {
                    if (_shuffledOptions[i].IsCorrect)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        public void Begin(GameContext context, GameDefinition definition, GameSession session)
        {
            _context = context;
            _definition = (ClasicoGameDefinition)definition;
            _session = session;

            foreach (var issue in ContentValidator.Validate(_definition.QuestionSet))
            {
                HermitLog.Warning($"[ContentValidation] {issue}");
            }

            var provider = new QuestionSetContentProvider(_definition.QuestionSet);
            _questions = provider.DrawQuestions(_definition.QuestionCount, context.Rng);
            _questionIndex = -1;
            _finished = _questions.Count == 0;

            if (_finished)
            {
                return;
            }

            if (_definition.CountdownDurationSeconds > 0f)
            {
                _phase = Phase.Countdown;
                _countdownTimer = 0f;
            }
            else
            {
                AdvanceToNextQuestion();
            }
        }

        public void SubmitAnswer(int optionIndex)
        {
            if (_finished || _phase != Phase.AwaitingAnswer)
            {
                return;
            }

            _selectedOptionIndex = optionIndex;
            var correct = optionIndex >= 0 && optionIndex < _shuffledOptions.Length && _shuffledOptions[optionIndex].IsCorrect;

            var gained = ClasicoScoring.ComputeQuestionScore(
                correct,
                _questionTimer,
                _definition.TimePerQuestionSeconds,
                _definition.PointsPerCorrectAnswer,
                _definition.MaxSpeedBonusPoints);

            if (correct)
            {
                _session.Correct++;
                _session.Streak++;
                if (_session.Streak > _session.BestStreak)
                {
                    _session.BestStreak = _session.Streak;
                }
            }
            else
            {
                _session.Incorrect++;
                _session.Streak = 0;
            }

            _lastStreakBonus = ClasicoScoring.ComputeStreakBonus(_session.Streak, _definition.StreakBonusThreshold, _definition.StreakBonusPoints);
            _session.Score += gained + _lastStreakBonus;

            _context.Analytics.AnswerSubmitted(_definition.GameId, _session.SessionId, _questions[_questionIndex].Id, correct, _questionTimer, _questions[_questionIndex].ContentVersion);

            _revealTimer = 0f;
            _phase = Phase.Revealing;
        }

        public void Tick(float deltaSeconds)
        {
            if (_finished)
            {
                return;
            }

            switch (_phase)
            {
                case Phase.Countdown:
                    _countdownTimer += deltaSeconds;
                    if (_countdownTimer >= _definition.CountdownDurationSeconds)
                    {
                        AdvanceToNextQuestion();
                    }

                    break;

                case Phase.AwaitingAnswer:
                    _questionTimer += deltaSeconds;
                    if (_definition.TimePerQuestionSeconds > 0f && _questionTimer >= _definition.TimePerQuestionSeconds)
                    {
                        SubmitAnswer(-1);
                    }

                    break;

                default:
                    _revealTimer += deltaSeconds;
                    if (_revealTimer >= _definition.FeedbackDisplaySeconds)
                    {
                        AdvanceToNextQuestion();
                    }

                    break;
            }
        }

        public GameResult BuildResult(bool completed)
        {
            var questionSet = _definition.QuestionSet;
            return new GameResult(
                _definition.GameId,
                _session.SessionId,
                _session.Score,
                _session.Correct,
                _session.Incorrect,
                _session.ElapsedSeconds,
                completed,
                questionSet != null ? questionSet.SetId : string.Empty,
                questionSet != null ? questionSet.SchemaVersion : 0,
                _session.BestStreak);
        }

        public void Cleanup()
        {
            CurrentView = null;
            _questions = null;
            _shuffledOptions = null;
        }

        private void AdvanceToNextQuestion()
        {
            _questionIndex++;
            if (_questionIndex >= _questions.Count)
            {
                _finished = true;
                CurrentView = null;
                return;
            }

            _session.Round = _questionIndex + 1;
            var question = _questions[_questionIndex];
            _shuffledOptions = Shuffle(question.Options, _context.Rng);
            _questionTimer = 0f;
            _selectedOptionIndex = -1;
            _lastStreakBonus = 0;
            _phase = Phase.AwaitingAnswer;

            CurrentView = new ClasicoQuestionView(question, _shuffledOptions, _questionIndex, _questions.Count);
            _context.Analytics.QuestionPresented(_definition.GameId, _session.SessionId, question.Id, _questionIndex, question.ContentVersion);
        }

        private static AnswerOption[] Shuffle(AnswerOption[] source, Random rng)
        {
            var copy = (AnswerOption[])source.Clone();
            for (var i = copy.Length - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }

            return copy;
        }
    }
}
