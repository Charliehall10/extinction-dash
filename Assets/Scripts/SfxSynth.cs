using UnityEngine;

// Retro sound effects generated from square waves and noise when the game
// starts, so there are no audio files to import.
public static class SfxSynth
{
    public const int Rate = 22050;
    public static readonly string[] Names = { "jump", "amber", "hit", "tier", "level", "roar", "boom", "smash" };

    public static float[] Make(string name)
    {
        switch (name)
        {
            case "jump": return Sweep(0.16f, 260f, 620f, 0.22f);
            case "amber": return Notes(new[] { 988f, 1319f }, new[] { 0.05f, 0.12f }, 0.18f);
            case "hit": return Hit();
            case "tier": return Notes(new[] { 784f, 1175f }, new[] { 0.05f, 0.1f }, 0.15f);
            case "level": return Notes(new[] { 523f, 659f, 784f, 1047f, 784f, 1047f }, new[] { 0.08f, 0.08f, 0.08f, 0.12f, 0.08f, 0.25f }, 0.16f);
            case "roar": return Roar();
            case "boom": return Noise(0.5f, 0.08f, 0.9f, 70f, 30f, 0.25f);
            case "smash": return Noise(0.15f, 0.5f, 0.45f, 300f, 120f, 0.12f);
            default: return new float[1];
        }
    }

    static float Square(ref float phase, float freq)
    {
        phase += freq / Rate;
        phase -= Mathf.Floor(phase);
        return phase < 0.5f ? 1f : -1f;
    }

    static float[] Sweep(float dur, float from, float to, float amp)
    {
        int n = (int)(dur * Rate);
        var d = new float[n];
        float ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = from * Mathf.Pow(to / from, t / dur);
            d[i] = Square(ref ph, f) * Mathf.Pow(1f - t / dur, 1.5f) * amp;
        }
        return d;
    }

    static float[] Notes(float[] freqs, float[] durs, float amp)
    {
        int total = 0;
        foreach (var dur in durs) total += (int)(dur * Rate);
        var d = new float[total];
        int at = 0;
        float ph = 0;
        for (int k = 0; k < freqs.Length; k++)
        {
            int n = (int)(durs[k] * Rate);
            for (int i = 0; i < n; i++)
            {
                float env = k == freqs.Length - 1 ? 1f - i / (float)n : 0.9f;
                d[at + i] = Square(ref ph, freqs[k]) * env * amp;
            }
            at += n;
        }
        return d;
    }

    // A low growl: a falling square wave with vibrato over rough noise.
    static float[] Roar()
    {
        const float dur = 0.7f;
        int n = (int)(dur * Rate);
        var d = new float[n];
        var rng = new System.Random(2);
        float ph = 0, noise = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Min(1f, t / 0.05f) * Mathf.Pow(1f - t / dur, 1.2f);
            float f = Mathf.Lerp(190f, 80f, t / dur) * (1f + 0.12f * Mathf.Sin(t * 2f * Mathf.PI * 28f));
            noise = Mathf.Lerp(noise, (float)rng.NextDouble() * 2f - 1f, 0.2f);
            d[i] = (Square(ref ph, f) * 0.2f + noise * 0.3f) * env;
        }
        return d;
    }

    // Filtered noise plus a falling square: smoothing lower = deeper rumble.
    static float[] Noise(float dur, float smoothing, float noiseAmp, float from, float to, float toneAmp)
    {
        int n = (int)(dur * Rate);
        var d = new float[n];
        var rng = new System.Random(3);
        float ph = 0, noise = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Pow(1f - t / dur, 1.5f);
            noise = Mathf.Lerp(noise, (float)rng.NextDouble() * 2f - 1f, smoothing);
            d[i] = (noise * noiseAmp + Square(ref ph, Mathf.Lerp(from, to, t / dur)) * toneAmp) * env;
        }
        return d;
    }

    static float[] Hit()
    {
        const float dur = 0.38f;
        int n = (int)(dur * Rate);
        var d = new float[n];
        var rng = new System.Random(1);
        float ph = 0, noise = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Pow(1f - t / dur, 2f);
            // Low-passed noise for the crunch plus a falling square for the thud.
            noise = Mathf.Lerp(noise, (float)rng.NextDouble() * 2f - 1f, 0.35f);
            float thud = Square(ref ph, Mathf.Lerp(140f, 50f, t / dur));
            d[i] = (noise * 0.45f + thud * 0.18f) * env;
        }
        return d;
    }
}
