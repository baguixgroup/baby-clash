using UnityEngine;

namespace BabyClash
{
    public enum SfxId
    {
        Hit,
        HitHard,
        Squeak,
        Clack,
        Slosh,
        Pickup,
        Throw,
        Dash,
        Shove,
        Cry,
        Cheer,
        Tick,
        Go,
        Click,
        Spawn,
        Land,
    }

    public enum MusicId
    {
        None,
        Title,
        Match,
    }

    /// <summary>Renders every placeholder sound and music track (called once at startup).</summary>
    public static class SoundBank
    {
        public static AudioClip[] CreateEffects()
        {
            var clips = new AudioClip[System.Enum.GetValues(typeof(SfxId)).Length];
            clips[(int)SfxId.Hit] = Make("Boing", 0.35f, b => Synth.Sweep(b, 0f, 0.35f, 620f, 150f, 18f, 0.12f, 0f, 1f));
            clips[(int)SfxId.HitHard] = Make("Bonk", 0.4f, b =>
            {
                Synth.Sweep(b, 0f, 0.4f, 380f, 90f, 14f, 0.1f, 0.3f, 1f);
                Synth.Noise(b, 0f, 0.08f, 40f, 0.3f, 0.6f, 11);
            });
            clips[(int)SfxId.Squeak] = Make("Squeak", 0.28f, b =>
            {
                Synth.Sweep(b, 0f, 0.14f, 900f, 1500f, 30f, 0.05f, 0.6f, 0.8f);
                Synth.Sweep(b, 0.12f, 0.16f, 1400f, 800f, 30f, 0.05f, 0.6f, 0.7f);
            });
            clips[(int)SfxId.Clack] = Make("Clack", 0.2f, b =>
            {
                Synth.Noise(b, 0f, 0.12f, 50f, 0.6f, 1f, 5);
                Synth.Sweep(b, 0f, 0.1f, 1200f, 700f, 0f, 0f, 0.4f, 0.5f);
            });
            clips[(int)SfxId.Slosh] = Make("Slosh", 0.45f, b =>
            {
                Synth.Noise(b, 0f, 0.4f, 8f, 0.08f, 1f, 7);
                Synth.Sweep(b, 0.02f, 0.3f, 300f, 520f, 12f, 0.2f, 0f, 0.5f);
            });
            clips[(int)SfxId.Pickup] = Make("Pop", 0.1f, b => Synth.Sweep(b, 0f, 0.1f, 300f, 950f, 0f, 0f, 0f, 1f));
            clips[(int)SfxId.Throw] = Make("Whoosh", 0.22f, b => Synth.Noise(b, 0f, 0.22f, 9f, 0.18f, 1f, 3));
            clips[(int)SfxId.Dash] = Make("Slide", 0.32f, b =>
            {
                Synth.Noise(b, 0f, 0.32f, 6f, 0.05f, 1f, 13);
                Synth.Sweep(b, 0f, 0.25f, 240f, 520f, 0f, 0f, 0.2f, 0.25f);
            });
            clips[(int)SfxId.Shove] = Make("Oof", 0.3f, b =>
            {
                Synth.Sweep(b, 0f, 0.3f, 220f, 110f, 8f, 0.1f, 0.5f, 1f);
                Synth.Kick(b, 0f, 0.8f);
            });
            clips[(int)SfxId.Cry] = Make("Waah", 1.1f, b =>
            {
                Synth.Sweep(b, 0f, 0.45f, 480f, 560f, 7f, 0.08f, 0.8f, 0.9f);
                Synth.Sweep(b, 0.4f, 0.7f, 560f, 280f, 7f, 0.1f, 0.8f, 1f);
            });
            clips[(int)SfxId.Cheer] = Make("Cheer", 1.1f, b =>
            {
                int[] notes = { 72, 76, 79, 84 };
                for (int i = 0; i < notes.Length; i++)
                    Synth.Note(b, i * 0.09f, 0.12f, notes[i], Synth.Voice.MusicBox, 1f);
                Synth.Note(b, 0.38f, 0.5f, 88, Synth.Voice.MusicBox, 0.9f);
                Synth.Note(b, 0.38f, 0.5f, 84, Synth.Voice.MusicBox, 0.7f);
            });
            clips[(int)SfxId.Tick] = Make("Tick", 0.25f, b => Synth.Note(b, 0f, 0.08f, 79, Synth.Voice.MusicBox, 1f));
            clips[(int)SfxId.Go] = Make("Whistle", 0.5f, b =>
            {
                Synth.Sweep(b, 0f, 0.5f, 1800f, 2100f, 24f, 0.03f, 0f, 1f);
                Synth.Note(b, 0f, 0.3f, 84, Synth.Voice.MusicBox, 0.6f);
            });
            clips[(int)SfxId.Click] = Make("Click", 0.08f, b => Synth.Note(b, 0f, 0.03f, 91, Synth.Voice.Pluck, 1f));
            clips[(int)SfxId.Spawn] = Make("Plink", 0.3f, b =>
            {
                Synth.Note(b, 0f, 0.06f, 88, Synth.Voice.MusicBox, 1f);
                Synth.Note(b, 0.07f, 0.1f, 93, Synth.Voice.MusicBox, 0.8f);
            });
            clips[(int)SfxId.Land] = Make("Thud", 0.18f, b =>
            {
                Synth.Kick(b, 0f, 1f);
                Synth.Noise(b, 0f, 0.06f, 50f, 0.1f, 0.4f, 17);
            });
            return clips;
        }

        static readonly int[] Roots = { 48, 45, 41, 43 }; // C, Am, F, G
        static readonly int[][] Chords =
        {
            new[] { 60, 64, 67 },
            new[] { 57, 60, 64 },
            new[] { 53, 57, 60 },
            new[] { 55, 59, 62 },
        };

        // (beat, length in beats, midi note) per bar, bouncy melody over C - Am - F - G.
        static readonly float[][] Melody =
        {
            new[] { 0f, 0.5f, 76f, 0.5f, 0.5f, 79f, 1f, 1f, 84f, 2f, 0.5f, 79f, 2.5f, 0.5f, 76f, 3f, 1f, 72f },
            new[] { 0f, 1f, 81f, 1f, 0.5f, 79f, 1.5f, 0.5f, 76f, 2f, 1f, 72f, 3f, 1f, 76f },
            new[] { 0f, 0.5f, 77f, 0.5f, 0.5f, 81f, 1f, 1f, 84f, 2f, 0.5f, 81f, 2.5f, 0.5f, 77f, 3f, 1f, 74f },
            new[] { 0f, 1f, 79f, 1f, 0.5f, 83f, 1.5f, 0.5f, 86f, 2f, 1f, 83f, 3f, 0.5f, 79f, 3.5f, 0.5f, 74f },
            new[] { 0f, 0.5f, 76f, 0.5f, 0.5f, 79f, 1f, 0.5f, 84f, 1.5f, 0.5f, 86f, 2f, 1f, 88f, 3f, 1f, 84f },
            new[] { 0f, 1f, 81f, 1f, 1f, 84f, 2f, 0.5f, 81f, 2.5f, 0.5f, 79f, 3f, 1f, 76f },
            new[] { 0f, 0.5f, 77f, 0.5f, 0.5f, 76f, 1f, 0.5f, 74f, 1.5f, 0.5f, 72f, 2f, 1f, 74f, 3f, 1f, 77f },
            new[] { 0f, 1f, 79f, 1f, 1f, 74f, 2f, 2f, 72f },
        };

        /// <summary>Upbeat 8-bar loop: oom-pah bass, plucked chords, a light lead and soft drums.</summary>
        public static AudioClip CreateMatchTheme()
        {
            const float bpm = 132f;
            float beat = 60f / bpm;
            int bars = Melody.Length;
            float[] b = Synth.Buffer(bars * 4 * beat + 0.8f);
            for (int bar = 0; bar < bars; bar++)
            {
                float barStart = bar * 4 * beat;
                int chord = bar % 4;
                for (int step = 0; step < 4; step++)
                {
                    float t = barStart + step * beat;
                    // Oom on 1 and 3, pah (chord) on 2 and 4.
                    if (step % 2 == 0)
                    {
                        Synth.Note(b, t, beat * 0.8f, Roots[chord] - (step == 2 ? 5 : 0) + (step == 2 ? 12 : 0), Synth.Voice.Bass, 0.5f);
                        Synth.Kick(b, t, 0.45f);
                    }
                    else
                    {
                        foreach (int note in Chords[chord])
                            Synth.Note(b, t, beat * 0.4f, note, Synth.Voice.Pluck, 0.16f);
                        Synth.Noise(b, t, 0.12f, 30f, 0.5f, 0.12f, bar * 4 + step);
                    }
                    Synth.Noise(b, t + beat * 0.5f, 0.04f, 80f, 0.9f, 0.05f, 100 + bar * 4 + step);
                }

                float[] melody = Melody[bar];
                for (int i = 0; i + 2 < melody.Length; i += 3)
                    Synth.Note(b, barStart + melody[i] * beat, melody[i + 1] * beat * 0.9f, (int)melody[i + 2], Synth.Voice.Lead, 0.22f);
            }
            return MakeLoop("MatchTheme", b, bars * 4 * beat);
        }

        /// <summary>Gentle music-box lullaby for the title screen.</summary>
        public static AudioClip CreateTitleTheme()
        {
            const float bpm = 96f;
            float beat = 60f / bpm;
            int bars = 8;
            float[] b = Synth.Buffer(bars * 4 * beat + 1.5f);
            for (int bar = 0; bar < bars; bar++)
            {
                float barStart = bar * 4 * beat;
                int chord = bar % 4;
                Synth.Note(b, barStart, beat * 3.6f, Roots[chord], Synth.Voice.Bass, 0.3f);
                int[] tones = Chords[chord];
                // Rolling arpeggio in eighths.
                for (int step = 0; step < 8; step++)
                {
                    int note = tones[step % 3] + (step >= 4 ? 12 : 0);
                    Synth.Note(b, barStart + step * beat * 0.5f, beat * 0.5f, note, Synth.Voice.MusicBox, 0.2f);
                }
                float[] melody = Melody[bar];
                for (int i = 0; i + 2 < melody.Length; i += 3)
                    Synth.Note(b, barStart + melody[i] * beat, melody[i + 1] * beat, (int)melody[i + 2] + 12, Synth.Voice.MusicBox, 0.28f);
            }
            return MakeLoop("TitleTheme", b, bars * 4 * beat);
        }

        static AudioClip Make(string name, float seconds, System.Action<float[]> render)
        {
            float[] buffer = Synth.Buffer(seconds);
            render(buffer);
            return Synth.ToClip(name, buffer);
        }

        /// <summary>Folds the ringing tail back onto the start so the loop is seamless.</summary>
        static AudioClip MakeLoop(string name, float[] buffer, float loopSeconds)
        {
            int loopLength = Mathf.Min(buffer.Length, Mathf.RoundToInt(loopSeconds * Synth.SampleRate));
            var loop = new float[loopLength];
            for (int i = 0; i < buffer.Length; i++)
                loop[i % loopLength] += buffer[i];
            return Synth.ToClip(name, loop, 0.7f);
        }
    }
}
