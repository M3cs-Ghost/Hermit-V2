using System;
using System.IO;
using UnityEngine;
using Hermit.Core;

namespace Hermit.Networking
{
    /// <summary>
    /// Local, PLAINTEXT, UNENCRYPTED session storage — deliberately simple for
    /// the C4 spike, not a production answer.
    ///
    /// A JSON file under Application.persistentDataPath is trivially readable
    /// by anything with file-system access to the device (same caveat applies
    /// to PlayerPrefs, which is why this isn't used instead — a plain file is
    /// no less secure but is a closer shape to what a real secure-storage
    /// implementation will look like later). Production needs Keychain (iOS) /
    /// Keystore (Android) / DPAPI (Windows) — tracked as an open item in
    /// Docs/C4_SUPABASE_SPIKE.md, not solved here.
    /// </summary>
    public sealed class LocalFileSessionStore : ISessionStore
    {
        [Serializable]
        private struct StoredSession
        {
            public string userId;
            public string accessToken;
            public string refreshToken;
            public long expiresAtUnixSeconds;
        }

        private readonly string _filePath;

        public LocalFileSessionStore()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "hermit_session.json");
        }

        public bool HasStoredSession => File.Exists(_filePath);

        public void Save(HermitSession session)
        {
            var stored = new StoredSession
            {
                userId = session.UserId,
                accessToken = session.AccessToken,
                refreshToken = session.RefreshToken,
                expiresAtUnixSeconds = session.ExpiresAtUnixSeconds
            };

            File.WriteAllText(_filePath, JsonUtility.ToJson(stored));
        }

        public HermitSession? Load()
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            try
            {
                var stored = JsonUtility.FromJson<StoredSession>(File.ReadAllText(_filePath));
                return new HermitSession
                {
                    UserId = stored.userId,
                    AccessToken = stored.accessToken,
                    RefreshToken = stored.refreshToken,
                    ExpiresAtUnixSeconds = stored.expiresAtUnixSeconds
                };
            }
            catch (Exception ex)
            {
                HermitLog.Warning($"Stored session file unreadable, clearing it: {ex.Message}");
                Clear();
                return null;
            }
        }

        public void Clear()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
    }
}
