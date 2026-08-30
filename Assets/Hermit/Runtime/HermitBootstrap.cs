using UnityEngine;
using Hermit.Core;

namespace Hermit.Runtime
{
    /// <summary>
    /// Marker component confirming the Hermit runtime loaded. Spawned
    /// automatically by HermitRuntimeInstaller — nothing needs to be manually
    /// placed in any scene.
    ///
    /// Moved here from Hermit.Core during C4: Core must stay dependency-free
    /// (it is the future home of the domain-agnostic Game Framework), but
    /// Bootstrap now needs to construct HermitAppContext, which knows about
    /// Hermit.Networking. Hermit.Runtime is the one assembly allowed to
    /// reference both.
    /// </summary>
    public sealed class HermitBootstrap : MonoBehaviour
    {
        public static bool IsInitialized { get; private set; }

        private void Awake()
        {
            IsInitialized = true;
            HermitLog.Info("Bootstrap initialized");
        }
    }
}
