using System;

namespace Hermit.Networking
{
    /// <summary>
    /// IsConnected starts from Application.internetReachability but is
    /// corrected by real backend call outcomes via ReportBackendReachable —
    /// device-level network state is not proof Supabase itself is reachable
    /// (captive portal, DNS issue, backend down all look "online" to the OS).
    ///
    /// ReportBackendReachable was added during C4: the C3 draft had no way for
    /// the networking layer to report the authoritative signal back.
    /// </summary>
    public interface IConnectivityService
    {
        bool IsConnected { get; }

        event Action<bool> ConnectivityChanged;

        void ReportBackendReachable(bool reachable);
    }
}
