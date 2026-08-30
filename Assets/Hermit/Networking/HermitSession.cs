using System;

namespace Hermit.Networking
{
    /// <summary>
    /// Minimal session shape shared by IAuthService and ISessionStore.
    /// Fields are placeholders — C4 fills these with real Supabase Auth values.
    /// No real token is ever assigned to this struct in C3.
    /// </summary>
    [Serializable]
    public struct HermitSession
    {
        public string UserId;
        public string AccessToken;
        public string RefreshToken;
        public long ExpiresAtUnixSeconds;
    }
}
