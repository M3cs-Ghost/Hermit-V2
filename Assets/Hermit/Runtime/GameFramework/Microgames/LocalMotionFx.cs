using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hermit.Runtime.GameFramework.Microgames
{
    /// <summary>Holds one in-flight coroutine reference for one animated
    /// target. Allocated once per animatable element at Build time (a target
    /// is never animated by more than one <see cref="LocalMotionFx"/> call at
    /// a time), never per-trigger — see <see cref="LocalMotionFx"/>.</summary>
    internal sealed class MotionHandle
    {
        public Coroutine Current;
    }

    /// <summary>
    /// C8.1b: a small, shared, reusable "local reaction" toolkit every Gold
    /// presenter's own correct/incorrect motion is built from — punch, shake,
    /// color flash, alpha fade. Every routine here follows the exact
    /// "no-op if already playing, always restore on completion" contract
    /// <see cref="Hermit.Runtime.GameFramework.ClasicoHud"/>'s punch/shake/
    /// transition already proved safe (see Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md's
    /// shake-bug writeup, and Docs/C8_1_GOLD_MICROGAME_SLICE.md's transition
    /// black-screen writeup) — deliberately not reinvented per world, so no
    /// presenter can reintroduce that bug class by hand-rolling its own
    /// restart-on-call coroutine.
    /// </summary>
    internal static class LocalMotionFx
    {
        public static void Punch(MonoBehaviour host, MotionHandle handle, RectTransform target, float duration, float peakScale = 1.18f)
        {
            if (handle.Current != null)
            {
                return;
            }

            handle.Current = host.StartCoroutine(PunchRoutine(handle, target, duration, peakScale));
        }

        private static IEnumerator PunchRoutine(MotionHandle handle, RectTransform target, float duration, float peakScale)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var scale = 1f + Mathf.Sin(t * Mathf.PI) * (peakScale - 1f);
                target.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            target.localScale = Vector3.one;
            handle.Current = null;
        }

        public static void Shake(MonoBehaviour host, MotionHandle handle, RectTransform target, float duration, float amplitude = 10f)
        {
            if (handle.Current != null)
            {
                return;
            }

            handle.Current = host.StartCoroutine(ShakeRoutine(handle, target, duration, amplitude));
        }

        private static IEnumerator ShakeRoutine(MotionHandle handle, RectTransform target, float duration, float amplitude)
        {
            var basePosition = target.anchoredPosition;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                var damping = 1f - t;
                var offset = Mathf.Sin(t * Mathf.PI * 10f) * amplitude * damping;
                target.anchoredPosition = basePosition + new Vector2(offset, 0f);
                yield return null;
            }

            target.anchoredPosition = basePosition;
            handle.Current = null;
        }

        public static void FlashColor(MonoBehaviour host, MotionHandle handle, Image target, Color flashColor, Color restColor, float duration)
        {
            if (handle.Current != null)
            {
                return;
            }

            handle.Current = host.StartCoroutine(FlashRoutine(handle, target, flashColor, restColor, duration));
        }

        private static IEnumerator FlashRoutine(MotionHandle handle, Image target, Color flashColor, Color restColor, float duration)
        {
            target.color = flashColor;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                target.color = Color.Lerp(flashColor, restColor, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            target.color = restColor;
            handle.Current = null;
        }

        public static void FadeAlpha(MonoBehaviour host, MotionHandle handle, CanvasGroup group, float from, float to, float duration)
        {
            if (handle.Current != null)
            {
                return;
            }

            handle.Current = host.StartCoroutine(FadeRoutine(handle, group, from, to, duration));
        }

        private static IEnumerator FadeRoutine(MotionHandle handle, CanvasGroup group, float from, float to, float duration)
        {
            group.alpha = from;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = to;
            handle.Current = null;
        }
    }
}
