using System;
using System.Collections.Generic;
using Hermit.Games.Content;

namespace Hermit.Games.Clasico
{
    /// <summary>
    /// Clasico's pure game logic: draw N questions, show one at a time, accept
    /// one answer, reveal briefly, advance; finished once every question has
    /// been answered. Knows nothing about uGUI, the Input System, or any
    /// concrete UI — a presenter in Hermit.Runtime reads <see cref="CurrentView"/>
    /// / <see cref="IsRevealing"/> / <see cref="SelectedOptionIndex"/> /
    /// <see cref="CorrectOptionIndex"/> and calls <see cref="SubmitAnswer"/>.
    /// </summary>
    public sealed class ClasicoGameEngine : IGameEngine
    {
        private enum Phase
        {
            AwaitingAnswer,
            Revealing
        }

        private ClasicoGameDefinition _definition;
        private GameContext _context;
        private GameSession _session;
        private IReadOnlyList<QuestionDefinition> _questions;
        private int _questionIndex = -1;
        private AnswerOption[] _shuffledOptions;
        private float _questionTimer;
        private float _revealTimer;
        private Phase _phase;
        private bool _finished;
        private int _selectedOptionIndex = -1;

        public bool IsFinished => _finished;
        public ClasicoQuestionView CurrentView { get; private set; }
        public bool IsRevealing => !_finished && _phase == Phase.Revealing;
        public int SelectedOptionIndex => _selectedOptionIndex;

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

            var provider = new QuestionSetContentProvider(_definition.QuestionSet);
            _questions = provider.DrawQuestions(_definition.QuestionCount, context.Rng);
            _questionIndex = -1;
            _finished = _questions.Count == 0;

            if (!_finished)
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

            _session.Score += gained;
            if (correct)
            {
                _session.Correct++;
            }
            else
            {
                _session.Incorrect++;
            }

            _context.Analytics.AnswerSubmitted(_definition.GameId, _session.SessionId, _questions[_questionIndex].Id, correct, _questionTimer);

            _revealTimer = 0f;
            _phase = Phase.Revealing;
        }

        public void Tick(float deltaSeconds)
        {
            if (_finished)
            {
                return;
            }

            if (_phase == Phase.AwaitingAnswer)
            {
                _questionTimer += deltaSeconds;
                if (_definition.TimePerQuestionSeconds > 0f && _questionTimer >= _definition.TimePerQuestionSeconds)
                {
                    SubmitAnswer(-1);
                }
            }
            else
            {
                _revealTimer += deltaSeconds;
                if (_revealTimer >= _definition.FeedbackDisplaySeconds)
                {
                    AdvanceToNextQuestion();
                }
            }
        }

        public GameResult BuildResult(bool completed)
        {
            return new GameResult(
                _definition.GameId,
                _session.SessionId,
                _session.Score,
                _session.Correct,
                _session.Incorrect,
                _session.ElapsedSeconds,
                completed);
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
            _phase = Phase.AwaitingAnswer;

            CurrentView = new ClasicoQuestionView(question, _shuffledOptions, _questionIndex, _questions.Count);
            _context.Analytics.QuestionPresented(_definition.GameId, _session.SessionId, question.Id, _questionIndex);
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
