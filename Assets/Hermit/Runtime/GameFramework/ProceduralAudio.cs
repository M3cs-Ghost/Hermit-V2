using UnityEngine;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// C8.1b: Clásico's audio identity, generated at runtime — no external
    /// assets, no asset hunt, per the C8.0 Design Lock's own audio direction
    /// ("generated/procedural tones"). Every clip is synthesized once (cheap:
    /// a few thousand samples of sine/noise math) and cached for the process
    /// lifetime, same "load once, reuse" discipline as
    /// <see cref="RuntimeUIFactory"/>'s procedural rounded-rect sprites.
    /// </summary>
    internal static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        /// <summary>A short, clean tone — used for the command cue and the
        /// incorrect buzz. A short attack/decay envelope avoids the audible
        /// "click" a raw sine burst would otherwise start/stop with.</summary>
        public static AudioClip Tone(string name, float frequency, float duration, float volume = 0.28f)
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            var data = new float[sampleCount];

            const float attack = 0.008f;
            var release = duration * 0.35f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Min(Mathf.Clamp01(t / attack), Mathf.Clamp01((duration - t) / release));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A tone that linearly sweeps from one frequency to
        /// another — reads as an "ascending chime" (correct) or a "falling
        /// whoosh" (transition) without needing to stitch multiple clips.</summary>
        public static AudioClip Sweep(string name, float fromFrequency, float toFrequency, float duration, float volume = 0.28f)
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            var data = new float[sampleCount];

            const float attack = 0.008f;
            var release = duration * 0.35f;
            var phase = 0f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var progress = duration > 0f ? t / duration : 1f;
                var frequency = Mathf.Lerp(fromFrequency, toFrequency, progress);
                phase += frequency / SampleRate;
                var envelope = Mathf.Min(Mathf.Clamp01(t / attack), Mathf.Clamp01((duration - t) / release));
                data[i] = Mathf.Sin(2f * Mathf.PI * phase) * volume * envelope;
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A short burst of filtered noise — reads as a percussive
        /// "tick"/"whoosh" transient, used for the transition cut.</summary>
        public static AudioClip Noise(string name, float duration, float volume = 0.18f)
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            var data = new float[sampleCount];
            var rng = new System.Random(12345);
            var previous = 0f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Clamp01((duration - t) / duration);
                var raw = (float)(rng.NextDouble() * 2.0 - 1.0);
                // A one-pole low-pass so this reads as a soft "whoosh", not harsh static.
                previous = Mathf.Lerp(previous, raw, 0.35f);
                data[i] = previous * volume * envelope * envelope;
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>C8.1d.4: a single composite gunshot — three layers baked
        /// into one clip (rather than three separately-triggered cues,
        /// unlike the wind/twang/pulse cues) because a shot needs one
        /// cohesive transient, not three independently-timed overlays:
        /// (1) a very short (~8ms), bright, fast-decaying noise burst — the
        /// "crack"; (2) a punchy low-frequency tone (~90Hz) with an
        /// exponential decay — the "body"/weight; (3) a longer, quieter,
        /// heavily-filtered noise tail — a cheap "dusty outdoor" sense of
        /// space, not a real reverb. Deliberately avoids a cartoon pop
        /// (no single short sine click), a laser (no pitch sweep), and an
        /// overlong Hollywood boom (tail stays under the clip's own
        /// <paramref name="duration"/>, ~0.3-0.4s total).</summary>
        public static AudioClip Gunshot(string name, float duration, float volume = 0.42f)
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            var data = new float[sampleCount];
            var rng = new System.Random(9001);

            const float crackDuration = 0.012f;
            const float bodyFrequency = 92f;
            const float bodyDecay = 14f;
            const float tailDecay = 5f;
            var previousTailNoise = 0f;
            var previousCrackNoise = 0f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;

                // Crack: bright, fast one-pole-filtered noise, gone in ~12ms.
                var crackRaw = (float)(rng.NextDouble() * 2.0 - 1.0);
                previousCrackNoise = Mathf.Lerp(previousCrackNoise, crackRaw, 0.8f);
                var crackEnvelope = t < crackDuration ? Mathf.Pow(1f - t / crackDuration, 2f) : 0f;
                var crack = previousCrackNoise * crackEnvelope * 0.9f;

                // Body: low tone, punchy exponential decay — the "weight".
                var bodyEnvelope = Mathf.Exp(-bodyDecay * t);
                var body = Mathf.Sin(2f * Mathf.PI * bodyFrequency * t) * bodyEnvelope * 0.75f;

                // Tail: quiet, soft-filtered noise decaying across the rest
                // of the clip — a cheap "dusty outdoor" sense of space.
                var tailRaw = (float)(rng.NextDouble() * 2.0 - 1.0);
                previousTailNoise = Mathf.Lerp(previousTailNoise, tailRaw, 0.15f);
                var tailEnvelope = Mathf.Exp(-tailDecay * t) * 0.35f;
                var tail = previousTailNoise * tailEnvelope;

                data[i] = Mathf.Clamp((crack + body + tail) * volume, -1f, 1f);
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>C8.1d.7: the gameplay firearm cue — deliberately a
        /// *different* composite from <see cref="Gunshot"/> (the cinematic's
        /// scene-punctuation shot), not the same clip reused at a shorter
        /// duration, per the brief's explicit "different sonic character":
        /// (1) a shorter (~7ms), brighter/harder-filtered noise crack; (2) a
        /// much higher (~185Hz, vs. the cinematic's ~92Hz), much faster-
        /// decaying "body" — reads as a tight crack, not a low boom;
        /// (3) a very short, quiet noise tail (decays roughly 5x faster than
        /// the cinematic's own) for just enough "outdoor" sense of space
        /// without any cinematic reverb tail. Meant to be fired many times
        /// per Encounter (once per round) without fatigue — dry, short,
        /// repeatable, arcade-readable, per the brief's "CRACK, not
        /// BOOOOOOM".</summary>
        public static AudioClip GameplayGunshot(string name, float duration, float volume = 0.38f)
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            var data = new float[sampleCount];
            var rng = new System.Random(4242);

            const float crackDuration = 0.007f;
            const float bodyFrequency = 185f;
            const float bodyDecay = 42f;
            const float tailDecay = 26f;
            var previousTailNoise = 0f;
            var previousCrackNoise = 0f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;

                // Crack: brighter, even faster-filtered noise than the
                // cinematic shot's — gone in ~7ms.
                var crackRaw = (float)(rng.NextDouble() * 2.0 - 1.0);
                previousCrackNoise = Mathf.Lerp(previousCrackNoise, crackRaw, 0.9f);
                var crackEnvelope = t < crackDuration ? Mathf.Pow(1f - t / crackDuration, 2f) : 0f;
                var crack = previousCrackNoise * crackEnvelope;

                // Body: higher-pitched, much faster exponential decay — a
                // dry "crack" rather than a low "boom".
                var bodyEnvelope = Mathf.Exp(-bodyDecay * t);
                var body = Mathf.Sin(2f * Mathf.PI * bodyFrequency * t) * bodyEnvelope * 0.6f;

                // Tail: very short and quiet — just enough outdoor "air",
                // never a cinematic reverb tail.
                var tailRaw = (float)(rng.NextDouble() * 2.0 - 1.0);
                previousTailNoise = Mathf.Lerp(previousTailNoise, tailRaw, 0.2f);
                var tailEnvelope = Mathf.Exp(-tailDecay * t) * 0.18f;
                var tail = previousTailNoise * tailEnvelope;

                data[i] = Mathf.Clamp((crack + body + tail) * volume, -1f, 1f);
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>C8.1d.9: the correct outlaw's own "return fire" cue,
        /// fired only on a wrong answer/timeout countershot — deliberately
        /// related to but distinguishable from <see cref="GameplayGunshot"/>
        /// (the player's own shot), never a copy of it and never the
        /// cinematic's <see cref="Gunshot"/>: a lower (~140Hz vs. 185Hz),
        /// slightly slower-decaying "body" and a duller (less bright, more
        /// heavily smoothed) crack read as "the same family of shot, but
        /// from a bit further away," per the brief's "slightly lower / more
        /// distant crack" — while a longer, slightly louder tail than the
        /// player's own shot gives it a touch more "outdoor" presence so it
        /// still reads clearly as a distinct, real event rather than an
        /// echo.</summary>
        public static AudioClip EnemyGunshot(string name, float duration, float volume = 0.36f)
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            var data = new float[sampleCount];
            var rng = new System.Random(6699);

            const float crackDuration = 0.010f;
            const float bodyFrequency = 140f;
            const float bodyDecay = 34f;
            const float tailDecay = 20f;
            var previousTailNoise = 0f;
            var previousCrackNoise = 0f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;

                // Crack: duller/less bright than the player's own shot —
                // more smoothing, gone slightly slower.
                var crackRaw = (float)(rng.NextDouble() * 2.0 - 1.0);
                previousCrackNoise = Mathf.Lerp(previousCrackNoise, crackRaw, 0.75f);
                var crackEnvelope = t < crackDuration ? Mathf.Pow(1f - t / crackDuration, 2f) : 0f;
                var crack = previousCrackNoise * crackEnvelope * 0.9f;

                // Body: lower-pitched, slightly slower decay than the
                // player's shot — reads as related but distinct.
                var bodyEnvelope = Mathf.Exp(-bodyDecay * t);
                var body = Mathf.Sin(2f * Mathf.PI * bodyFrequency * t) * bodyEnvelope * 0.6f;

                // Tail: a touch longer/louder than the player's own shot —
                // "a bit further away", not a second copy of the same shot.
                var tailRaw = (float)(rng.NextDouble() * 2.0 - 1.0);
                previousTailNoise = Mathf.Lerp(previousTailNoise, tailRaw, 0.2f);
                var tailEnvelope = Mathf.Exp(-tailDecay * t) * 0.22f;
                var tail = previousTailNoise * tailEnvelope;

                data[i] = Mathf.Clamp((crack + body + tail) * volume, -1f, 1f);
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>C8.1d.8: replaces the old cinematic pre-draw cue, which
        /// was a sharp 1800Hz <see cref="Tone"/> burst — an instant-attack
        /// pure sine that manual validation reported as reading like a
        /// click/pop/mini-shot immediately before the cinematic gunshot,
        /// exactly the "second impact-like event" this generator exists to
        /// avoid. Deliberately has a real attack ramp (never an instant
        /// onset) and is built from a heavily low-pass-filtered noise
        /// ("leather/cloth" texture) plus only the faintest damped high
        /// partial (a bare hint of "metal", never a distinct tone) — meant
        /// to sit under the near-silence window rather than announce
        /// itself. Quieter by construction than <see cref="Gunshot"/> at
        /// this project's own chosen call-site volumes (see
        /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.8" for the measured
        /// peak-amplitude comparison).</summary>
        public static AudioClip PreDrawTension(string name, float duration, float volume = 0.05f)
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            var data = new float[sampleCount];
            var rng = new System.Random(7331);
            var previous = 0f;

            var attack = duration * 0.35f;
            var release = duration * 0.45f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Min(Mathf.Clamp01(t / attack), Mathf.Clamp01((duration - t) / release));

                // Leather/cloth: heavily low-pass-filtered noise (a much
                // stronger smoothing coefficient than Noise's own 0.35) —
                // soft and dark, never a click.
                var raw = (float)(rng.NextDouble() * 2.0 - 1.0);
                previous = Mathf.Lerp(previous, raw, 0.12f);
                var leather = previous * envelope;

                // Metal: a faint, fully-damped high partial — just a hint
                // of touch, never a standalone audible tone.
                var metal = Mathf.Sin(2f * Mathf.PI * 2600f * t) * envelope * 0.15f;

                data[i] = Mathf.Clamp((leather + metal) * volume, -1f, 1f);
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>C9.1: a short, warm confirm cue for Press Start — two
        /// soft sine partials (a perfect fifth) with a gentle attack, never
        /// a sharp click or arcade blip. This and <see cref="EntrySwell"/>
        /// are deliberately the *only* two audio cues the Start Screen/Hub
        /// transition uses, per the brief's explicit "keep placeholder
        /// audio minimal" instruction — see
        /// Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md.</summary>
        public static AudioClip SoftConfirmChime(string name, float duration, float volume = 0.22f)
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            var data = new float[sampleCount];

            const float attack = 0.05f;
            var release = duration * 0.7f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Min(Mathf.Clamp01(t / attack), Mathf.Clamp01((duration - t) / release));

                var low = Mathf.Sin(2f * Mathf.PI * 440f * t);
                var high = Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.5f;

                data[i] = Mathf.Clamp((low + high) * envelope * volume, -1f, 1f);
            }

            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>C9.1: one composite "entry" cue standing in for both the
        /// Start Screen transition's rising swell AND its Hub-arrival
        /// resolution — deliberately a single clip, not two, per the
        /// brief's "one restrained transition swell / arrival cue"
        /// instruction. A slow, warm three-partial pad (root/fifth/octave
        /// shimmer) whose own amplitude envelope rises through roughly the
        /// first third of its length, holds, then gently settles through
        /// the last third — so playing it once at confirm time carries the
        /// whole "awakening -&gt; arrival" arc without a second discrete
        /// trigger. Deliberately no noise/percussive layer at all — the
        /// desired emotion is "opening," never "trailer climax".</summary>
        public static AudioClip EntrySwell(string name, float duration, float volume = 0.30f)
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * duration);
            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            var data = new float[sampleCount];

            var attack = duration * 0.35f;
            var release = duration * 0.35f;
            var releaseStart = duration - release;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = t < attack
                    ? Mathf.Clamp01(t / attack)
                    : t > releaseStart
                        ? Mathf.Clamp01((duration - t) / release)
                        : 1f;

                var root = Mathf.Sin(2f * Mathf.PI * 220f * t);
                var fifth = Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.6f;
                var shimmer = Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.3f * Mathf.Clamp01((t - attack * 0.5f) / attack);

                data[i] = Mathf.Clamp((root + fifth + shimmer) * envelope * volume, -1f, 1f);
            }

            clip.SetData(data, 0);
            return clip;
        }
    }
}
