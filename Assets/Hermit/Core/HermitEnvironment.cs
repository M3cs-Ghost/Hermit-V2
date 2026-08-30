namespace Hermit.Core
{
    /// <summary>
    /// The three environments Hermit V2 can target. See EnvironmentConfig for the
    /// data that varies per environment. No environment is selected by code yet —
    /// that wiring is C4's job.
    /// </summary>
    public enum HermitEnvironment
    {
        Development,
        Staging,
        Production
    }
}
