using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Hermit.Editor
{
    /// <summary>
    /// C9.1b: one-time generator for the Marcellus TMP font asset (see
    /// Assets/Hermit/Content/Fonts/Marcellus/SOURCE.md for the font's own
    /// provenance/license). Uses <see cref="TMP_FontAsset.CreateFontAsset(Font,int,int,GlyphRenderMode,int,int,AtlasPopulationMode,bool)"/> —
    /// the public, cross-assembly-safe factory Unity ships specifically for
    /// scripted/headless font-asset generation (the in-editor "Assets >
    /// Create > TextMeshPro > Font Asset > SDF" context menu instead calls
    /// a private-setter-heavy internal path that only compiles from inside
    /// TMPro's own assembly, not from this project's code). Run once via
    /// <c>-executeMethod Hermit.Editor.MarcellusFontAssetImporter.CreateIfMissing</c>
    /// (without <c>-quit</c> — see <see cref="TmpEssentialResourcesImporter"/>'s
    /// own doc-comment for why).
    /// </summary>
    public static class MarcellusFontAssetImporter
    {
        private const string SourceFontPath = "Assets/Hermit/Content/Fonts/Marcellus/Marcellus-Regular.ttf";

        // Under Content/Resources/ (not Content/Fonts/) — this project's
        // established convention is that everything RuntimeUIFactory loads
        // at runtime (art, audio, the theme asset) lives under a folder
        // literally named "Resources" so Resources.Load can find it; the
        // raw, human-authored .ttf + its license/provenance stay in
        // Content/Fonts/ since only the generated TMP asset needs to be
        // Resources-loadable, not the source font file itself.
        private const string OutputFontAssetPath = "Assets/Hermit/Content/Resources/Fonts/Marcellus-Regular SDF.asset";

        [MenuItem("Hermit/Create Marcellus TMP Font Asset")]
        public static void CreateIfMissing()
        {
            if (File.Exists(OutputFontAssetPath))
            {
                Debug.Log("[Hermit] Marcellus TMP font asset already present — skipping creation.");
                EditorApplication.Exit(0);
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (font == null)
            {
                Debug.LogError($"[Hermit] Could not load the Marcellus source font at {SourceFontPath} — see Assets/Hermit/Content/Fonts/Marcellus/SOURCE.md.");
                EditorApplication.Exit(1);
                return;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                Debug.LogError($"[Hermit] TMP_FontAsset.CreateFontAsset returned null for {SourceFontPath} — check that \"Include Font Data\" is enabled in its Font import settings.");
                EditorApplication.Exit(1);
                return;
            }

            var outputDir = Path.GetDirectoryName(OutputFontAssetPath);
            if (!AssetDatabase.IsValidFolder(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(fontAsset, OutputFontAssetPath);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Hermit] Marcellus TMP font asset created at {OutputFontAssetPath}.");
            EditorApplication.Exit(0);
        }
    }
}
