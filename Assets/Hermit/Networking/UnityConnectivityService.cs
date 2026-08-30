using System;
using UnityEngine;

namespace Hermit.Networking
{
    /// <summary>
    /// See IConnectivityService for the "device online != Supabase reachable"
    /// rationale. This class never makes its own network calls — it only
    /// tracks the last-known state and reacts to reports from the real
    /// networking classes.
    /// </summary>
    public sealed class UnityConnectivityService : IConnectivityService
    {
        private bool _isConnected;

        public UnityConnectivityService()
        {
            _isConnected = Application.internetReachability != NetworkReachability.NotReachable;
        }

        public bool IsConnected => _isConnected;

        public event Action<bool> ConnectivityChanged;

        public void ReportBackendReachable(bool reachable)
        {
            if (reachable == _isConnected)
            {
                return;
            }

            _isConnected = reachable;
            ConnectivityChanged?.Invoke(_isConnected);
        }
    }
}
