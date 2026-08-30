namespace Hermit.Networking
{
    /// <summary>
    /// Pure expiry check, factored out of HermitAppContext specifically so it
    /// is unit-testable without networking or the Unity runtime.
    /// </summary>
    public static class SessionExpiry
    {
        public const long DefaultSafetyMarginSeconds = 30;

        public static bool IsStillValid(HermitSession session, long nowUnixSeconds, long safetyMarginSeconds = DefaultSafetyMarginSeconds)
        {
            return session.ExpiresAtUnixSeconds > nowUnixSeconds + safetyMarginSeconds;
        }
    }
}
