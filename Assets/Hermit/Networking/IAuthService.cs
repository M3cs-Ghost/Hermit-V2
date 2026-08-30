using System.Threading;
using System.Threading.Tasks;

namespace Hermit.Networking
{
    /// <summary>
    /// Contract for the Supabase Auth (GoTrue) client. Stateless on purpose —
    /// HermitAppContext (Hermit.Runtime) owns the current HermitSession; this
    /// service only performs REST operations and returns/consumes sessions
    /// explicitly, it never holds one itself.
    ///
    /// Refined during C4 from the C3 draft: the original shape had
    /// IsAuthenticated + a SessionChanged event, implying the service held its
    /// own session state — that would have duplicated the state the
    /// composition root already needs to own, with no way to keep both in
    /// sync. RefreshAsync/SignOutAsync now take the token explicitly instead.
    /// </summary>
    public interface IAuthService
    {
        Task<HermitSession> SignInAsync(string email, string password, CancellationToken cancellationToken);

        Task<HermitSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

        Task SignOutAsync(string accessToken, CancellationToken cancellationToken);
    }
}
