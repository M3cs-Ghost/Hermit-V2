using UnityEngine;

namespace Hermit.Core
{
    /// <summary>
    /// Single, minimal entry point for the Hermit runtime. Lives only in the
    /// 00_Bootstrap scene.
    ///
    /// Deliberately does nothing beyond confirming the runtime loaded:
    /// no Supabase, no service locator, no DI container. Real service wiring
    /// (Auth, Networking, GameManager) starts in C4/C5, not here.
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
