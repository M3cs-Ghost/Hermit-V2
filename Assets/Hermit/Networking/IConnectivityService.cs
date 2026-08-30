using System;

namespace Hermit.Networking
{
    /// <summary>
    /// Contract for observing network reachability. C3 ships no implementation —
    /// C4 wires this to Application.internetReachability as part of the offline
    /// queue design (Blueprint §J).
    /// </summary>
    public interface IConnectivityService
    {
        bool IsConnected { get; }

        event Action<bool> ConnectivityChanged;
    }
}
