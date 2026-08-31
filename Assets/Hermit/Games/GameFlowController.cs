using System;

namespace Hermit.Games
{
    /// <summary>
    /// Drives any <see cref="IGameEngine"/> through the one lifecycle every
    /// game shares: Idle -> Preparing -> Playing -> Ending -> Results -> Idle.
    ///
    /// This is the merge of what C2 called "GameManager" and
    /// "GameFlowController" into a single class. In C2 those were two separate
    /// concepts; in practice a "manager" that owns the active game and a
    /// "flow controller" that owns its lifecycle state turned out to be the
    /// same responsibility wearing two names — splitting them only invited two
    /// objects to disagree about what state the game is in. See
    /// Docs/C5_GAME_FRAMEWORK.md, "Deviations from C2".
    ///
    /// Never branches on GameId or on a concrete engine type — every game,
    /// including the extensibility test's FakeGame, drives through the exact
    /// same code path here. Takes a <see cref="GameDefinition"/> directly
    /// (C6) rather than C5's separate "GameRegistration" wrapper — the
    /// engine factory now lives on the definition itself
    /// (<see cref="GameDefinition.CreateEngine"/>), so wrapping it a second
    /// time added a type without adding a real guarantee.
    /// </summary>
    public sealed class GameFlowController
    {
        public GameLifecycleState State { get; private set; } = GameLifecycleState.Idle;
        public GameSession CurrentSession { get; private set; }
        public GameResult LastResult { get; private set; }

        /// <summary>The active game's own engine. Typed as the generic
        /// interface here on purpose — a presenter that needs game-specific
        /// view state casts to the concrete engine type it already knows it is
        /// hosting (see Hermit.Runtime's per-game host classes). Null outside
        /// Playing/Ending.</summary>
        public IGameEngine CurrentEngine { get; private set; }

        public event Action<GameLifecycleState> StateChanged;
        public event Action<GameResult> ResultReady;

        private GameDefinition _definition;
        private GameContext _context;

        public void Start(GameDefinition definition, GameContext context)
        {
            if (State != GameLifecycleState.Idle && State != GameLifecycleState.Results)
            {
                throw new InvalidOperationException($"Cannot start a game while in state {State}.");
            }

            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _context = context ?? throw new ArgumentNullException(nameof(context));

            SetState(GameLifecycleState.Preparing);

            CurrentEngine = _definition.CreateEngine();
            CurrentSession = new GameSession(_definition.GameId);
            CurrentEngine.Begin(_context, _definition, CurrentSession);
            _context.Analytics.GameStarted(_definition.GameId, CurrentSession.SessionId);

            SetState(GameLifecycleState.Playing);
        }

        public void Tick(float deltaSeconds)
        {
            if (State != GameLifecycleState.Playing)
            {
                return;
            }

            CurrentSession.ElapsedSeconds += deltaSeconds;
            CurrentEngine.Tick(deltaSeconds);

            if (CurrentEngine.IsFinished)
            {
                Finish(completed: true);
            }
        }

        public void Abort()
        {
            if (State != GameLifecycleState.Playing)
            {
                return;
            }

            Finish(completed: false);
        }

        public void Restart()
        {
            if (State != GameLifecycleState.Results)
            {
                throw new InvalidOperationException($"Cannot restart from state {State}; restart is only valid from Results.");
            }

            // Start() already accepts Results as a starting state (see its guard
            // above) — restarting is just starting again with the same
            // definition/context, going straight to Preparing without a
            // visible Idle flicker in between.
            Start(_definition, _context);
        }

        public void AcknowledgeResults()
        {
            if (State != GameLifecycleState.Results)
            {
                return;
            }

            CurrentEngine = null;
            _definition = null;
            _context = null;
            CurrentSession = null;
            SetState(GameLifecycleState.Idle);
        }

        private void Finish(bool completed)
        {
            SetState(GameLifecycleState.Ending);

            LastResult = CurrentEngine.BuildResult(completed);
            CurrentEngine.Cleanup();

            if (completed)
            {
                _context.Analytics.GameCompleted(LastResult);
            }
            else
            {
                _context.Analytics.GameAborted(LastResult);
            }

            SetState(GameLifecycleState.Results);
            ResultReady?.Invoke(LastResult);
        }

        private void SetState(GameLifecycleState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
