using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hermit.Runtime
{
    /// <summary>
    /// Fixes a real gap found during C6 manual validation: a standalone
    /// Player always starts at build index 0 (00_Bootstrap), which has never
    /// held anything beyond a Main Camera. Opening a gameplay scene directly
    /// in the Editor and pressing Play always worked — Editor Play loads
    /// whichever scene is currently open, not build index 0 — which is
    /// exactly why this was never caught by Editor-only manual validation.
    /// No scene-flow logic existed anywhere in this codebase before C6
    /// (confirmed by grep: no other SceneManager/LoadScene call exists).
    ///
    /// Minimal fix: if the active scene really is the entry scene, move on.
    /// C7 changed the destination from 02_GameplaySandbox to 01_Shell now
    /// that Shell is the real product entry point (see
    /// Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md, "Shell") —
    /// 02_GameplaySandbox remains reachable by opening it directly in the
    /// Editor for development/PlayMode testing, per the brief's explicit
    /// instruction to keep it for exactly that.
    ///
    /// Deliberately a separate hook from HermitRuntimeInstaller: that one
    /// owns Networking bootstrap and is intentionally scene-agnostic; this
    /// one owns scene routing and is intentionally scene-specific. Both use
    /// AfterSceneLoad (fires once, at app start, regardless of relative
    /// order between the two — HermitRuntimeInstaller's DontDestroyOnLoad
    /// root survives the scene swap this class triggers either way, and
    /// ShellInstaller.Awake() runs as a normal consequence of 01_Shell
    /// loading, independent of this attribute entirely).
    /// </summary>
    public static class BootstrapSceneFlow
    {
        public const string EntrySceneName = "00_Bootstrap";
        public const string DestinationSceneName = "01_Shell";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AdvancePastEntryScene()
        {
            if (ShouldAdvance(SceneManager.GetActiveScene().name))
            {
                SceneManager.LoadScene(DestinationSceneName);
            }
        }

        /// <summary>Pure decision logic, public — like GameSessionInstaller and
        /// GameSelectorHud — specifically so a PlayMode test can assert on it
        /// directly. Actually loading a scene isn't something a unit test
        /// should do.</summary>
        public static bool ShouldAdvance(string activeSceneName) => activeSceneName == EntrySceneName;
    }
}
