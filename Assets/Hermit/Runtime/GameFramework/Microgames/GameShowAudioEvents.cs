using System.Collections.Generic;
using UnityEngine;

namespace Hermit.Runtime.GameFramework.Microgames
{
    /// <summary>
    /// C8.1k: a process-lifetime log of every GameShow_* audio hook fired by
    /// <see cref="GameShowPresenter"/> — same shape and purpose as
    /// <see cref="WesternAudioEvents"/>: lets PlayMode tests prove which
    /// showmanship cue fired, in what order, and that nothing fires after
    /// Hide()/abort, without depending on AudioSource internals.
    /// </summary>
    public static class GameShowAudioEvents
    {
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

        public static void Clear()
        {
            Log.Clear();
        }
    }
}
