using System;
using UnityEngine;

namespace BounceLab
{
    public sealed class BounceAudio : MonoBehaviour
    {
        public const int SampleRate = 24000;
        public const float Tempo = 126f;
        public const int Bars = 8;
        private const string MusicKey = "BounceLabMusicV1";
        private const string SfxKey = "BounceLabSfxV1";

        private AudioSource musicSource;
        private AudioSource sfxSource;
        private AudioClip bounceClip, springClip, deathClip, clearClip, uiClip, paintClip;
        private bool initialized;

        public bool MusicEnabled { get; private set; }
        public bool SfxEnabled { get; private set; }
        public int MusicSampleCount { get; private set; }

        private void Awake() { Initialize(); }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            MusicEnabled = PlayerPrefs.GetInt(MusicKey, 1) != 0;
            SfxEnabled = PlayerPrefs.GetInt(SfxKey, 1) != 0;
            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0;
            musicSource.volume = .48f;
            musicSource.clip = Clip("Neon Rebound", BuildMusicSamples());
            MusicSampleCount = musicSource.clip.samples;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0;
            sfxSource.volume = .72f;
            bounceClip = Clip("Bounce", BuildEffect(.13f, Effect.Bounce));
            springClip = Clip("Spring", BuildEffect(.28f, Effect.Spring));
            deathClip = Clip("Fall", BuildEffect(.42f, Effect.Death));
            clearClip = Clip("Clear", BuildEffect(.82f, Effect.Clear));
            uiClip = Clip("UI", BuildEffect(.055f, Effect.Ui));
            paintClip = Clip("Paint", BuildEffect(.075f, Effect.Paint));
            if (MusicEnabled) musicSource.Play();
        }

        public void ToggleMusic()
        {
            MusicEnabled = !MusicEnabled;
            PlayerPrefs.SetInt(MusicKey, MusicEnabled ? 1 : 0);
            PlayerPrefs.Save();
            if (MusicEnabled) musicSource.Play(); else musicSource.Stop();
        }

        public void ToggleSfx()
        {
            SfxEnabled = !SfxEnabled;
            PlayerPrefs.SetInt(SfxKey, SfxEnabled ? 1 : 0);
            PlayerPrefs.Save();
            if (SfxEnabled) Play(uiClip, .65f);
        }

        public void PlayUi() { Play(uiClip, .58f); }
        public void PlayPaint() { Play(paintClip, .52f); }
        public void PlayBounce(bool spring) { Play(spring ? springClip : bounceClip, spring ? .85f : .42f); }
        public void PlayDeath() { Play(deathClip, .9f); }
        public void PlayClear() { Play(clearClip, 1f); }

        private void Play(AudioClip clip, float volume)
        {
            if (initialized && SfxEnabled && clip != null) sfxSource.PlayOneShot(clip, volume);
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static float[] BuildMusicSamples()
        {
            float beat = 60f / Tempo;
            int length = Mathf.CeilToInt(Bars * 4f * beat * SampleRate);
            var mix = new float[length];
            // D minor with a final A-major turnaround: neon, bright, and slightly restless.
            int[] roots = { 38, 34, 41, 36, 38, 36, 34, 33 };
            int[,] lead = {
                { 62, 69, 65, 69, 72, 69, 65, 64 },
                { 58, 65, 62, 65, 69, 65, 62, 60 },
                { 65, 72, 69, 72, 74, 72, 69, 67 },
                { 60, 67, 64, 67, 72, 67, 64, 62 },
                { 62, 65, 69, 72, 74, 72, 69, 65 },
                { 60, 64, 67, 72, 74, 72, 67, 64 },
                { 58, 62, 65, 69, 70, 69, 65, 62 },
                { 57, 61, 64, 69, 73, 69, 64, 61 }
            };
            int[,] chords = {
                { 50, 53, 57 }, { 46, 50, 53 }, { 53, 57, 60 }, { 48, 52, 55 },
                { 50, 53, 57 }, { 48, 52, 55 }, { 46, 50, 53 }, { 45, 49, 52 }
            };

            for (int bar = 0; bar < Bars; bar++)
            {
                float barStart = bar * 4f * beat;
                for (int note = 0; note < 8; note++)
                {
                    float start = barStart + note * beat * .5f;
                    AddTone(mix, start, beat * .36f, Midi(lead[bar, note]), .16f, Wave.Pulse, .008f, .075f);
                    if ((note & 1) == 1)
                        AddTone(mix, start, beat * .18f, Midi(lead[bar, note] + 12), .045f, Wave.Sine, .004f, .05f);
                }
                for (int chordNote = 0; chordNote < 3; chordNote++)
                    AddTone(mix, barStart, beat * 3.72f, Midi(chords[bar, chordNote]), .038f,
                        Wave.Triangle, .12f, .28f);
                for (int step = 0; step < 4; step++)
                {
                    float start = barStart + step * beat;
                    int bass = roots[bar] + (step == 3 && bar % 2 == 0 ? 7 : 0);
                    AddTone(mix, start, beat * .72f, Midi(bass), .21f, Wave.Triangle, .008f, .11f);
                    AddKick(mix, start, .22f);
                    if (step == 1 || step == 3) AddSnare(mix, start, .14f, (uint)(bar * 17 + step));
                    AddHat(mix, start, .045f, (uint)(bar * 31 + step));
                    AddHat(mix, start + beat * .5f, .035f, (uint)(bar * 47 + step + 9));
                }
            }

            for (int i = 0; i < mix.Length; i++)
                mix[i] = (float)Math.Tanh(mix[i] * .92f) * .78f;
            return mix;
        }

        public static string CompositionMetrics()
        {
            float[] samples = BuildMusicSamples();
            double energy = 0;
            float peak = 0;
            int crossings = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float value = samples[i];
                energy += value * value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
                if (i > 0 && (value >= 0) != (samples[i - 1] >= 0)) crossings++;
            }
            return samples.Length + "," + peak.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "," +
                Math.Sqrt(energy / samples.Length).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "," + crossings;
        }

        private enum Wave { Sine, Triangle, Pulse }
        private enum Effect { Bounce, Spring, Death, Clear, Ui, Paint }

        private static void AddTone(float[] mix, float start, float duration, float frequency, float volume,
            Wave wave, float attack, float release)
        {
            int first = Mathf.Max(0, Mathf.RoundToInt(start * SampleRate));
            int count = Mathf.Min(mix.Length - first, Mathf.RoundToInt(duration * SampleRate));
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;
                float envelope = Mathf.Min(1, time / Mathf.Max(.001f, attack)) *
                    Mathf.Min(1, (duration - time) / Mathf.Max(.001f, release));
                float phase = time * frequency;
                float value;
                if (wave == Wave.Triangle) value = Mathf.Asin(Mathf.Sin(phase * Mathf.PI * 2)) * (2f / Mathf.PI);
                else if (wave == Wave.Pulse)
                    value = Mathf.Sin(phase * Mathf.PI * 2) + .30f * Mathf.Sin(phase * Mathf.PI * 6) +
                        .14f * Mathf.Sin(phase * Mathf.PI * 10);
                else value = Mathf.Sin(phase * Mathf.PI * 2);
                mix[first + i] += value * volume * envelope;
            }
        }

        private static void AddKick(float[] mix, float start, float duration)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.Min(mix.Length - first, Mathf.RoundToInt(duration * SampleRate));
            float phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                phase += Mathf.Lerp(145f, 48f, t / duration) / SampleRate;
                mix[first + i] += Mathf.Sin(phase * Mathf.PI * 2) * Mathf.Exp(-t * 17f) * .34f;
            }
        }

        private static void AddSnare(float[] mix, float start, float duration, uint seed)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.Min(mix.Length - first, Mathf.RoundToInt(duration * SampleRate));
            uint state = seed + 0x9e3779b9u;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float noise = Noise(ref state);
                mix[first + i] += (noise * .23f + Mathf.Sin(t * 180f * Mathf.PI * 2) * .08f) * Mathf.Exp(-t * 25f);
            }
        }

        private static void AddHat(float[] mix, float start, float duration, uint seed)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.Min(mix.Length - first, Mathf.RoundToInt(duration * SampleRate));
            uint state = seed + 0x85ebca6bu;
            float previous = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float noise = Noise(ref state);
                float high = noise - previous * .78f;
                previous = noise;
                mix[first + i] += high * Mathf.Exp(-t * 70f) * .085f;
            }
        }

        private static float[] BuildEffect(float duration, Effect effect)
        {
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            uint state = 0x1234567u + (uint)effect * 977u;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)SampleRate;
                float p = t / duration;
                float value = 0;
                if (effect == Effect.Bounce)
                    value = Mathf.Sin((210f * t + 145f * t * t) * Mathf.PI * 2) * Mathf.Exp(-t * 24f);
                else if (effect == Effect.Spring)
                {
                    float note = p < .34f ? 392f : p < .67f ? 587.33f : 880f;
                    value = Mathf.Sin(note * t * Mathf.PI * 2) * Mathf.Pow(1f - p, .55f);
                }
                else if (effect == Effect.Death)
                    value = (Mathf.Sin((260f * t - 210f * t * t) * Mathf.PI * 2) * .7f + Noise(ref state) * .3f) * (1f - p);
                else if (effect == Effect.Clear)
                {
                    int step = Mathf.Min(3, Mathf.FloorToInt(p * 4));
                    float note = step == 0 ? 587.33f : step == 1 ? 698.46f : step == 2 ? 880f : 1174.66f;
                    float local = p * 4 - step;
                    value = (Mathf.Sin(note * t * Mathf.PI * 2) + .25f * Mathf.Sin(note * 2 * t * Mathf.PI * 2)) *
                        Mathf.Sin(Mathf.Clamp01(local) * Mathf.PI);
                }
                else if (effect == Effect.Paint)
                    value = Mathf.Sin((520f + p * 130f) * t * Mathf.PI * 2) * Mathf.Exp(-t * 45f);
                else value = Mathf.Sin(760f * t * Mathf.PI * 2) * Mathf.Exp(-t * 65f);
                samples[i] = Mathf.Clamp(value * .62f, -.8f, .8f);
            }
            return samples;
        }

        private static float Noise(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0xffff) / 32767.5f - 1f;
        }

        private static float Midi(int note) { return 440f * Mathf.Pow(2f, (note - 69) / 12f); }
    }
}
