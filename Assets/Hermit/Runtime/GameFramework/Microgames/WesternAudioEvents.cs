using System.Collections.Generic;
using UnityEngine;

namespace Hermit.Runtime.GameFramework.Microgames
{
    /// <summary>
    /// C8.1d.8: a minimal, public, test-only event/timestamp log for
    /// verifying Western's audio call ORDER (and, via the recorded clip's
    /// own peak amplitude, relative loudness) — never waveform/pitch
    /// content. Deliberately not a general debug system: no UI, no
    /// persistence, no production code ever reads it — its only consumer is
    /// PlayMode tests proving "gunshot fires before impact, impact before
    /// outcome feedback, pre-draw stays quieter than the gunshot" without
    /// reflection or internal-type access (this type is public specifically
    /// so <c>Hermit.Tests.PlayMode</c> can read it with no
    /// <c>InternalsVisibleTo</c> needed, matching every other test in this
    /// project, which interacts with production code only through public
    /// surface). See Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.8".
    /// </summary>
    public static class WesternAudioEvents
    {
        /// <summary>One recorded audio call: <paramref name="Name"/> is a
        /// short, stable event id (e.g. "CinematicGunshot",
        /// "GameplayImpact"); <paramref name="Time"/> is
        /// <see cref="UnityEngine.Time.realtimeSinceStartup"/> at the moment
        /// of the call, for ordering; <paramref name="Clip"/> is the exact
        /// <see cref="AudioClip"/> instance passed to <c>PlayOneShot</c>, so
        /// a test can read its own peak amplitude via the clip's public
        /// <c>GetData</c> API for a coarse, non-brittle loudness comparison.</summary>
        public readonly struct Entry
        {
            public readonly string Name;
            public readonly float Time;
            public readonly AudioClip Clip;

            public Entry(string name, float time, AudioClip clip)
            {
                Name = name;
                Time = time;
                Clip = clip;
            }
        }

        private static readonly List<Entry> Log = new List<Entry>();

        public static void Record(string eventName, AudioClip clip)
        {
            Log.Add(new Entry(eventName, Time.realtimeSinceStartup, clip));
        }

        public static IReadOnlyList<Entry> Events => Log;

        /// <summary>Tests call this in setup — the log is a static (process-
        /// lifetime) list specifically so it survives across the scene
        /// rebuilds each test's own fixture performs, but that means it
        /// must be reset per test rather than assumed empty.</summary>
        public static void Clear()
        {
            Log.Clear();
        }
    }
}
