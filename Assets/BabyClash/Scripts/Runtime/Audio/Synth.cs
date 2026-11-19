using UnityEngine;

namespace BabyClash
{
    /// <summary>
    /// Tiny offline synthesizer used to render the game's placeholder music and sound effects at startup.
    /// Everything writes into a mono float buffer that becomes an AudioClip.
    /// </summary>
    public static class Synth
    {
        public const int SampleRate = 44100;

        public enum Voice
        {
            MusicBox,
            Pluck,
            Bass,
            Lead,
        }

        public static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * SampleRate)];

        public static float MidiToHz(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        public static void Note(float[] buffer, float start, float duration, int midi, Voice voice, float volume)
        {
            float hz = MidiToHz(midi);
            int from = Mathf.Max(0, (int)(start * SampleRate));
            float tail = voice == Voice.Bass ? 0.08f : voice == Voice.Lead ? 0.12f : 0.6f;
            int to = Mathf.Min(buffer.Length, (int)((start + duration + tail) * SampleRate));
            double phase = 0.0;
            double step = 2.0 * Mathf.PI * hz / SampleRate;
            for (int i = from; i < to; i++)
            {
                float t = (i - from) / (float)SampleRate;
                float held = t < duration ? 1f : Mathf.Max(0f, 1f - (t - duration) / tail);
                float attack = Mathf.Min(1f, t / 0.006f);
                float s;
                switch (voice)
                {
                    case Voice.MusicBox:
                        s = (Mathf.Sin((float)phase) + 0.35f * Mathf.Sin((float)(phase * 2.0)) + 0.12f * Mathf.Sin((float)(phase * 4.01)))
                            * Mathf.Exp(-t * 3.2f);
                        break;
                    case Voice.Pluck:
                        s = Triangle(phase) * Mathf.Exp(-t * 7f);
                        break;
                    case Voice.Bass:
                        s = (Mathf.Sin((float)phase) * 0.8f + Triangle(phase) * 0.3f) * held;
                        break;
                    default: // Lead: soft square with vibrato
                        float vibrato = 1f + 0.004f * Mathf.Sin(t * 30f);
                        s = (float)System.Math.Tanh(Mathf.Sin((float)(phase * vibrato)) * 2.2) * 0.55f * held;
                        break;
                }
                buffer[i] += s * attack * volume;
                phase += step;
            }
        }

        public static void Kick(float[] buffer, float start, float volume)
        {
            int from = (int)(start * SampleRate);
            int length = (int)(0.18f * SampleRate);
            double phase = 0.0;
            for (int i = 0; i < length && from + i < buffer.Length; i++)
            {
                float t = i / (float)SampleRate;
                float hz = Mathf.Lerp(140f, 45f, Mathf.Clamp01(t / 0.12f));
                phase += 2.0 * Mathf.PI * hz / SampleRate;
                buffer[from + i] += Mathf.Sin((float)phase) * Mathf.Exp(-t * 22f) * volume;
            }
        }

        public static void Noise(float[] buffer, float start, float duration, float decay, float brightness, float volume, int seed)
        {
            int from = (int)(start * SampleRate);
            int length = (int)(duration * SampleRate);
            var random = new System.Random(seed);
            float low = 0f;
            for (int i = 0; i < length && from + i < buffer.Length; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)random.NextDouble() * 2f - 1f;
                low += (white - low) * brightness;
                buffer[from + i] += (white - low) * Mathf.Exp(-t * decay) * volume;
            }
        }

        /// <summary>Pitch sweep (cartoon boings, zips, wails).</summary>
        public static void Sweep(float[] buffer, float start, float duration, float fromHz, float toHz, float vibratoHz,
            float vibratoDepth, float squareness, float volume)
        {
            int from = (int)(start * SampleRate);
            int length = (int)(duration * SampleRate);
            double phase = 0.0;
            for (int i = 0; i < length && from + i < buffer.Length; i++)
            {
                float u = i / (float)length;
                float t = i / (float)SampleRate;
                float hz = Mathf.Lerp(fromHz, toHz, u) * (1f + vibratoDepth * Mathf.Sin(2f * Mathf.PI * vibratoHz * t));
                phase += 2.0 * Mathf.PI * hz / SampleRate;
                float wave = Mathf.Sin((float)phase);
                if (squareness > 0f)
                    wave = Mathf.Lerp(wave, (float)System.Math.Tanh(wave * 4.0), squareness);
                float envelope = Mathf.Min(1f, t / 0.01f) * (1f - u) * (1f - u);
                buffer[from + i] += wave * envelope * volume;
            }
        }

        public static AudioClip ToClip(string name, float[] buffer, float peak = 0.85f)
        {
            float max = 0.0001f;
            foreach (float sample in buffer)
                max = Mathf.Max(max, Mathf.Abs(sample));
            float gain = peak / max;
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] *= gain;

            AudioClip clip = AudioClip.Create(name, buffer.Length, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        static float Triangle(double phase)
        {
            double p = phase / (2.0 * System.Math.PI);
            p -= System.Math.Floor(p);
            return (float)(p < 0.5 ? p * 4.0 - 1.0 : 3.0 - p * 4.0);
        }
    }
}
