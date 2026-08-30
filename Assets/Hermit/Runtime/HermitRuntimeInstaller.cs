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
