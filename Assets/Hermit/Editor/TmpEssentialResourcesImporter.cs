using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hermit.Editor
{
    /// <summary>
    /// C9.1a: one-time bootstrap for TextMeshPro. This project's UGUI package
    /// (Unity 6's merged com.unity.ugui + TMP) ships TMPro's runtime/editor
    /// assemblies, but not its "Essential Resources" (the default font asset
    /// and TMP Settings asset every <c>TextMeshProUGUI</c> needs at runtime) —
    /// those normally come from Window > TextMeshPro > Import TMP Essential
    /// Resources, a one-click editor action with no scripting-API menu
    /// equivalent other than <see cref="AssetDatabase.ImportPackage"/> against
    /// the package's own bundled .unitypackage. Run once via
    /// <c>-executeMethod Hermit.Editor.TmpEssentialResourcesImporter.ImportIfMissing</c>;
    /// safe to run again (no-ops once <c>Assets/TextMesh Pro/Resources/TMP Settings.asset</c>
    /// exists).
    /// </summary>
    public static class TmpEssentialResourcesImporter
    {
        private const string SettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Hermit/Import TMP Essential Resources")]
        public static void ImportIfMissing()
        {
            if (File.Exists(SettingsAssetPath))
            {
                Debug.Log("[Hermit] TMP Essential Resources already present — skipping import.");
                EditorApplication.Exit(0);
                return;
            }

            // The package is a Unity Package Manager package resolved into
            // Library/PackageCache with a content-hash suffix that varies by
            // machine/lockfile state — UnityEditor.PackageManager.PackageInfo is the only reliable way
            // to find its real on-disk root (a raw "Packages/..." relative
            // path does not exist on the filesystem; Unity mounts it as a
            // virtual path).
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");
            if (packageInfo == null)
            {
                Debug.LogError("[Hermit] Could not resolve the com.unity.ugui package location — TextMeshPro will have no default font asset until TMP Essential Resources is imported.");
                EditorApplication.Exit(1);
                return;
            }

            var packagePath = Path.Combine(packageInfo.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(packagePath))
            {
                Debug.LogError($"[Hermit] TMP Essential Resources.unitypackage not found at {packagePath} — TextMeshPro will have no default font asset until this is imported.");
                EditorApplication.Exit(1);
                return;
            }

            // ImportPackage(interactive: false) is asynchronous — it schedules
            // the unpack/import over subsequent editor ticks and returns
            // immediately. Exiting right after calling it would kill the
            // process mid-import, so wait for the completion/failure
            // callback before calling EditorApplication.Exit.
            Debug.Log($"[Hermit] Importing {packagePath}...");
            AssetDatabase.importPackageCompleted += OnImportCompleted;
            AssetDatabase.importPackageFailed += OnImportFailed;
            AssetDatabase.ImportPackage(packagePath, false);
        }

        private static void OnImportCompleted(string packageName)
        {
            Debug.Log($"[Hermit] TMP Essential Resources import completed ({packageName}).");
            EditorApplication.Exit(0);
        }

        private static void OnImportFailed(string packageName, string errorMessage)
        {
            Debug.LogError($"[Hermit] TMP Essential Resources import failed ({packageName}): {errorMessage}");
            EditorApplication.Exit(1);
        }
    }
}
