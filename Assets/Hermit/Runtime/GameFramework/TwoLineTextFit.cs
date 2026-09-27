using UnityEngine;
using UnityEngine.UI;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// C8.1k.3: a line-count-aware replacement for legacy <c>Text</c> Best
    /// Fit on short labels. Unity's Best Fit only picks the largest size
    /// whose wrapped text fits the box HEIGHT — it never minimizes line
    /// count, so in a box tall enough for two lines of a large font it will
    /// happily settle on three lines of a smaller one. This resolves the
    /// largest size (max → min) at which the text wraps into at most
    /// <c>maxLines</c> lines and every glyph stays inside the label's own
    /// rect, using the label's real font/rect/wrap settings and the canvas
    /// scale factor, so the result is what actually renders. Public (like
    /// <c>WesternAudioEvents</c>) so PlayMode tests can audit every shipped
    /// string with the exact same measurement the runtime uses.
    /// </summary>
    public static class TwoLineTextFit
    {
        public readonly struct Measurement
        {
            public readonly int LineCount;
            public readonly bool AllCharactersVisible;
            public readonly float PreferredHeight;
            public readonly Rect GlyphBounds;

            public Measurement(int lineCount, bool allCharactersVisible, float preferredHeight, Rect glyphBounds)
            {
                LineCount = lineCount;
                AllCharactersVisible = allCharactersVisible;
                PreferredHeight = preferredHeight;
                GlyphBounds = glyphBounds;
            }

            /// <summary>True when the text wraps into at most
            /// <paramref name="maxLines"/> lines, nothing is dropped, and
            /// every glyph (and the line stack's own height) lies inside
            /// <paramref name="box"/> — the label's local rect.</summary>
            public bool FitsIn(Rect box, int maxLines) =>
                LineCount <= maxLines
                && AllCharactersVisible
                && PreferredHeight <= box.height + 0.01f
                && GlyphBounds.xMin >= box.xMin - 0.01f
                && GlyphBounds.xMax <= box.xMax + 0.01f
                && GlyphBounds.yMin >= box.yMin - 0.01f
                && GlyphBounds.yMax <= box.yMax + 0.01f;
        }

        private static readonly TextGenerator Generator = new TextGenerator();

        /// <summary>Lays <paramref name="content"/> out exactly as
        /// <paramref name="label"/> would at <paramref name="fontSize"/> on
        /// a canvas with <paramref name="scaleFactor"/>, with Best Fit off.
        /// Bounds are in the label's local (canvas) units.</summary>
        public static Measurement Measure(Text label, string content, int fontSize, float scaleFactor)
        {
            var rect = label.rectTransform.rect;
            var settings = label.GetGenerationSettings(rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = fontSize;
            settings.scaleFactor = scaleFactor;
            settings.verticalOverflow = VerticalWrapMode.Overflow;

            Generator.Invalidate();
            Generator.Populate(content, settings);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var verts = Generator.verts;
            for (var i = 0; i < verts.Count; i++)
            {
                var p = (Vector2)verts[i].position / scaleFactor;
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            // Verts and RectTransform.rect share the same local space (origin
            // at the pivot), so the bounds compare to rect directly.
            var bounds = verts.Count > 0
                ? Rect.MinMaxRect(min.x, min.y, max.x, max.y)
                : new Rect(0f, 0f, 0f, 0f);

            var visible = Generator.characterCountVisible >= CountNonWhitespace(content);
            var preferredHeight = Generator.GetPreferredHeight(content, settings) / scaleFactor;

            return new Measurement(Generator.lineCount, visible, preferredHeight, bounds);
        }

        /// <summary>The largest font size in [<paramref name="minSize"/>,
        /// <paramref name="maxSize"/>] at which <paramref name="content"/>
        /// fits <paramref name="label"/>'s rect in at most
        /// <paramref name="maxLines"/> lines; <paramref name="minSize"/> if
        /// none does (the PlayMode audit guarantees no shipped string gets
        /// there).</summary>
        public static int ResolveFontSize(Text label, string content, int maxSize, int minSize, int maxLines, float scaleFactor)
        {
            var box = label.rectTransform.rect;
            for (var size = maxSize; size > minSize; size--)
            {
                if (Measure(label, content, size, scaleFactor).FitsIn(box, maxLines))
                {
                    return size;
                }
            }

            return minSize;
        }

        /// <summary>Resolves and applies the size for the label's current
        /// text at its real canvas scale. Best Fit is switched off — this
        /// replaces it.</summary>
        public static int Apply(Text label, int maxSize, int minSize, int maxLines)
        {
            label.resizeTextForBestFit = false;
            label.fontSize = ResolveFontSize(label, label.text ?? string.Empty, maxSize, minSize, maxLines, Mathf.Max(0.01f, label.pixelsPerUnit));
            return label.fontSize;
        }

        private static int CountNonWhitespace(string content)
        {
            var count = 0;
            foreach (var c in content)
            {
                if (!char.IsWhiteSpace(c))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
