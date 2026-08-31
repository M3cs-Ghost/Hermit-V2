namespace Hermit.Games
{
    /// <summary>
    /// The single lifecycle every game drives through. Owned exclusively by
    /// <see cref="GameFlowController"/> — no other type tracks lifecycle state,
    /// so there is never a second source of truth to desync from.
    /// </summary>
    public enum GameLifecycleState
    {
        Idle,
        Preparing,
        Playing,
        Ending,
        Results
    }
}
