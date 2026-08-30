using System;
using System.Threading;
using System.Threading.Tasks;
using Hermit.Core;
using Hermit.Networking;

namespace Hermit.Runtime
{
    /// <summary>
    /// The one composition root of the app: the only class allowed to
    /// construct concrete Hermit.Networking implementations directly. Wired by
    /// hand in the constructor — no DI framework, no service locator
    /// (ADR-003 already rejected Zenject/Extenject for this reason).
    ///
    /// UI/Games code should depend on this class (or, later, on narrower
    /// framework-level services split out of it) — never on
    /// SupabaseAuthService/SupabaseRestBackendClient directly. That boundary
    /// is what keeps Supabase swappable and keeps Hermit.Games from ever
    /// knowing Supabase exists (Docs/ARCHITECTURE.md).
    /// </summary>
    public sealed class HermitAppContext
    {
        public EnvironmentConfig Config { get; }
        public IAuthService Auth { get; }
        public IBackendClient Backend { get; }
        public ISessionStore SessionStore { get; }
        public IConnectivityService Connectivity { get; }

        public HermitSession? Session { get; private set; }
        public string LastDisplayName { get; private set; } = "-";
        public string LastWalletBalance { get; private set; } = "-";

        public HermitAppContext(EnvironmentConfig config)
        {
            Config = config;

            var connectivity = new UnityConnectivityService();
            Connectivity = connectivity;
            Auth = new SupabaseAuthService(config, connectivity);
            Backend = new SupabaseRestBackendClient(config, connectivity);
            SessionStore = new LocalFileSessionStore();
        }

        public async Task RestoreSessionAsync(CancellationToken cancellationToken = default)
        {
            if (!SessionStore.HasStoredSession)
            {
                HermitLog.Info("Session restore: nothing stored.");
                return;
            }

            var stored = SessionStore.Load();
            if (!stored.HasValue)
            {
                return;
            }

            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (SessionExpiry.IsStillValid(stored.Value, nowUnix))
            {
                Session = stored.Value;
                HermitLog.Info("Session restore: stored token still valid.");
                return;
            }

            HermitLog.Info("Session restore: stored token expired, attempting refresh.");
            try
            {
                var refreshed = await Auth.RefreshAsync(stored.Value.RefreshToken, cancellationToken);
                Session = refreshed;
                SessionStore.Save(refreshed);
                HermitLog.Info("Session restore: refresh succeeded.");
            }
            catch (HermitException ex)
            {
                HermitLog.Warning($"Session restore: refresh failed ({ex.Error.Kind}), clearing stored session.");
                SessionStore.Clear();
                Session = null;
            }
        }

        public async Task<HermitResult<string>> LoginAsync(string email, string password)
        {
            try
            {
                var session = await Auth.SignInAsync(email, password, CancellationToken.None);
                Session = session;
                SessionStore.Save(session);
                return HermitResult<string>.Ok($"Logged in. user_id={session.UserId}");
            }
            catch (HermitException ex)
            {
                return HermitResult<string>.Fail(ex.Error);
            }
        }

        public async Task<HermitResult<string>> RefreshAsync()
        {
            if (!Session.HasValue)
            {
                return HermitResult<string>.Fail(NoSessionError());
            }

            try
            {
                var refreshed = await Auth.RefreshAsync(Session.Value.RefreshToken, CancellationToken.None);
                Session = refreshed;
                SessionStore.Save(refreshed);
                return HermitResult<string>.Ok($"Refreshed. new_expiry_unix={refreshed.ExpiresAtUnixSeconds}");
            }
            catch (HermitException ex)
            {
                return HermitResult<string>.Fail(ex.Error);
            }
        }

        public async Task<HermitResult<string>> LogoutAsync()
        {
            try
            {
                if (Session.HasValue)
                {
                    await Auth.SignOutAsync(Session.Value.AccessToken, CancellationToken.None);
                }

                SessionStore.Clear();
                Session = null;
                LastDisplayName = "-";
                LastWalletBalance = "-";
                return HermitResult<string>.Ok("Logged out.");
            }
            catch (HermitException ex)
            {
                // A failed network call must never leave a zombie session —
                // clear local state regardless of whether the server call
                // itself succeeded.
                SessionStore.Clear();
                Session = null;
                return HermitResult<string>.Fail(ex.Error);
            }
        }

        public async Task<HermitResult<string>> LoadProfileAsync()
        {
            if (!Session.HasValue)
            {
                return HermitResult<string>.Fail(NoSessionError());
            }

            try
            {
                var path = $"profiles?id=eq.{Session.Value.UserId}&select=id,display_name,theme";
                var profile = await Backend.GetAsync<ProfileRow>(path, Session.Value.AccessToken, CancellationToken.None);
                LastDisplayName = string.IsNullOrEmpty(profile.display_name) ? "(sin nombre)" : profile.display_name;
                return HermitResult<string>.Ok($"display_name={LastDisplayName}");
            }
            catch (HermitException ex)
            {
                return HermitResult<string>.Fail(ex.Error);
            }
        }

        public async Task<HermitResult<string>> LoadWalletAsync()
        {
            if (!Session.HasValue)
            {
                return HermitResult<string>.Fail(NoSessionError());
            }

            try
            {
                var path = $"wallets?user_id=eq.{Session.Value.UserId}&select=user_id,balance";
                var wallet = await Backend.GetAsync<WalletRow>(path, Session.Value.AccessToken, CancellationToken.None);
                LastWalletBalance = wallet.balance.ToString("0.00");
                return HermitResult<string>.Ok($"balance={LastWalletBalance}");
            }
            catch (HermitException ex)
            {
                return HermitResult<string>.Fail(ex.Error);
            }
        }

        public async Task<HermitResult<string>> CallSampleRpcAsync()
        {
            if (!Session.HasValue)
            {
                return HermitResult<string>.Fail(NoSessionError());
            }

            try
            {
                // get_my_rank(p_game_mode) — read-only, explicitly approved for
                // this spike. Never award_coins/submit_score (mutating).
                var payload = new GetMyRankRequest { p_game_mode = "leyenda_arcade" };
                var raw = await Backend.InvokeRpcAsync<GetMyRankRequest, string>(
                    "get_my_rank", payload, Session.Value.AccessToken, CancellationToken.None);
                return HermitResult<string>.Ok(raw);
            }
            catch (HermitException ex)
            {
                return HermitResult<string>.Fail(ex.Error);
            }
        }

        private static HermitError NoSessionError() =>
            new HermitError(HermitErrorKind.SessionExpired, "Inicia sesión primero.", "No active session in HermitAppContext");

        [Serializable] private struct ProfileRow { public string id; public string display_name; public string theme; }
        [Serializable] private struct WalletRow { public string user_id; public double balance; }
        [Serializable] private struct GetMyRankRequest { public string p_game_mode; }
    }
}
