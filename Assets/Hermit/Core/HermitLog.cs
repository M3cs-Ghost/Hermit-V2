using UnityEngine;

namespace Hermit.Core
{
    /// <summary>
    /// Minimal logging wrapper so call sites never depend on UnityEngine.Debug
    /// directly. Intentionally thin — swap the implementation here, not at every
    /// call site, if a real logging backend is ever needed.
    /// </summary>
    public static class HermitLog
    {
        private const string Prefix = "[Hermit]";

        public static void Info(string message)
        {
            Debug.Log($"{Prefix} {message}");
        }

        public static void Warning(string message)
        {
            Debug.LogWarning($"{Prefix} {message}");
        }

        public static void Error(string message)
        {
            Debug.LogError($"{Prefix} {message}");
        }
    }
}
