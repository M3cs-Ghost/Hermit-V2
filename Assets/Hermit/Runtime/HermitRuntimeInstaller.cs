using UnityEngine;
using Hermit.Core;

namespace Hermit.Runtime
{
    /// <summary>
    /// Entry point. Runs automatically the moment the game starts (no scene
    /// wiring, no dragging components onto GameObjects needed) via
    /// RuntimeInitializeOnLoadMethod. Spawns one persistent GameObject holding
    /// HermitBootstrap and, if EnvironmentConfig loads successfully, a
    /// HermitAppContext plus (Development only) the C4 debug panel.
    /// </summary>
    internal static class HermitRuntimeInstaller
    {
        /// <summary>The one HermitAppContext this process ever creates, once
        /// Install() has run — null before that, or if EnvironmentConfig
        /// failed to load. Added in C7 so Shell can show a discrete session
        /// status line ("acceso discreto a estado del usuario si está
        /// disponible") by reading Networking's own state instead of Shell
        /// constructing or calling into Supabase itself — Shell still never
        /// touches Hermit.Networking directly.</summary>
        public static HermitAppContext Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (HermitBootstrap.IsInitialized)
            {
                return;
            }

            var root = new GameObject("Hermit.Runtime");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<HermitBootstrap>();

            var config = Resources.Load<EnvironmentConfig>("EnvironmentConfig_Development");
            if (config == null)
            {
                HermitLog.Error(
                    "EnvironmentConfig_Development not found under a Resources/ folder — " +
                    "networking and the C4 debug panel will not start. " +
                    "See Docs/C4_SUPABASE_SPIKE.md.");
                return;
            }

            if (!config.IsConfigured)
            {
                HermitLog.Error("EnvironmentConfig_Development has no URL/anon key set — networking will not start.");
                return;
            }

            var context = new HermitAppContext(config);
            Current = context;
            _ = context.RestoreSessionAsync();

            // Debug panel only ever spawns for Development — a build carrying a
            // Staging/Production EnvironmentConfig never shows it.
            if (config.Environment == HermitEnvironment.Development)
            {
                var panel = root.AddComponent<C4DebugPanel>();
                panel.Initialize(context);
            }
        }
    }
}
