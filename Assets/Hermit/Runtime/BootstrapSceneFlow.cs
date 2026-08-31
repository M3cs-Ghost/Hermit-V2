using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hermit.Runtime
{
    /// <summary>
    /// Fixes a real gap found during C6 manual validation: a standalone
    /// Player always starts at build index 0 (00_Bootstrap), which has never
    /// held anything beyond a Main Camera. Opening 02_GameplaySandbox
    /// directly in the Editor and pressing Play always worked — Editor Play
    /// loads whichever scene is currently open, not build index 0 — which is
    /// exactly why this was never caught by Editor-only manual validation.
    /// No scene-flow logic existed anywhere in this codebase before this
    /// (confirmed by grep: no SceneManager/LoadScene call anywhere else).
    ///
    /// Minimal fix: if the active scene really is the entry scene, move on to
    /// gameplay. 01_Shell is skipped on purpose — it is still an empty
    /// placeholder (see Docs/ARCHITECTURE.md) and loading it would just show
    /// another blank camera instead of fixing anything.
    ///
    /// Deliberately a separate hook from HermitRuntimeInstaller: that one
    /// owns Networking bootstrap and is intentionally scene-agnostic; this
    /// one owns scene routing and is intentionally scene-specific. Both use
    /// AfterSceneLoad (fires once, at app start, regardless of relative
    /// order between the two — HermitRuntimeInstaller's DontDestroyOnLoad
    /// root survives the scene swap this class triggers either way, and
    /// GameSessionInstaller.Awake() runs as a normal consequence of
    /// 02_GameplaySandbox loading, independent of this attribute entirely).
    /// </summary>
    public static class BootstrapSceneFlow
    {
        public const string EntrySceneName = "00_Bootstrap";
        public const string GameplaySceneName = "02_GameplaySandbox";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AdvancePastEntryScene()
        {
            if (ShouldAdvance(SceneManager.GetActiveScene().name))
            {
                SceneManager.LoadScene(GameplaySceneName);
            }
        }

        /// <summary>Pure decision logic, public — like GameSessionInstaller and
        /// GameSelectorHud — specifically so a PlayMode test can assert on it
        /// directly. Actually loading a scene isn't something a unit test
        /// should do.</summary>
        public static bool ShouldAdvance(string activeSceneName) => activeSceneName == EntrySceneName;
    }
}
