using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hermit.Networking
{
    /// <summary>
    /// Contract for the future Supabase Auth client. No implementation ships in
    /// C3 — C4's Technical Spike decides which concrete client backs this
    /// (supabase-csharp, a Unity-packaged alternative, or direct REST — see
    /// Docs/C4_SUPABASE_SPIKE.md).
    /// </summary>
    public interface IAuthService
    {
        bool IsAuthenticated { get; }

        event Action<HermitSession?> SessionChanged;

        Task<HermitSession> SignInAsync(string email, string password, CancellationToken cancellationToken);

        Task SignOutAsync(CancellationToken cancellationToken);

        Task<HermitSession> RefreshAsync(CancellationToken cancellationToken);
    }
}
