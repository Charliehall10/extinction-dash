using UnityEngine;

public enum PitKind { Tar, Chasm, Lava }
public enum Weather { None, Ash, Embers }

// Colours for the sky and background layers.
public struct Sky
{
    public Color top, bottom, far, mid, front;
    public float sunY, moonY, stars, doom;

    public static Sky Lerp(Sky a, Sky b, float t) => new Sky
    {
        top = Color.Lerp(a.top, b.top, t), bottom = Color.Lerp(a.bottom, b.bottom, t),
        far = Color.Lerp(a.far, b.far, t), mid = Color.Lerp(a.mid, b.mid, t),
        front = Color.Lerp(a.front, b.front, t),
        sunY = Mathf.Lerp(a.sunY, b.sunY, t), moonY = Mathf.Lerp(a.moonY, b.moonY, t),
        stars = Mathf.Lerp(a.stars, b.stars, t), doom = Mathf.Lerp(a.doom, b.doom, t),
    };
}

// Colours and materials the terrain is painted with.
public class GroundStyle
{
    public Color32 grass, grassDark, grassLight, dirt, dirtDark, dirtLight, deep, deepBand, pebble, pebbleLight, bone, glow;
    public PitKind pit;
    public bool blades = true, cracks;
}

public class LevelDef
{
    public string name;
    public float length;               // distance to the finish flag
    public float speedStart, speedEnd; // base speed across the level
    public float gapMin, gapMax;       // seconds of flat running between features
    public float pitMin, pitMax;       // pit widths, in seconds of travel at base speed
    public float comboGap;             // seconds between the parts of a combo
    public int maxHeight;              // tallest ledge, in pixels
    public bool bigSteps;              // ledges two units high
    public float meteorEvery, smoke;
    public Weather weather;
    public Sky sky;
    public GroundStyle ground;

    // Feature weights.
    public float rockS, rockM, rockL, fern, log, cluster, pteroLow, pteroHigh, fireball, pit, step, ramp, mound, combo;
}

public static class Levels
{
    static Color C(int rgb) => Art.Hex(rgb);
    static Color32 H(int rgb) => Art.Hex(rgb);

    public static readonly Sky Aftermath = new Sky
    {
        top = C(0x4a3a6a), bottom = C(0xf0a070), far = new Color(0.8f, 0.62f, 0.62f), mid = new Color(0.6f, 0.5f, 0.52f),
        front = new Color(1f, 0.9f, 0.85f), sunY = 0.25f, stars = 0.2f,
    };

    public static readonly LevelDef[] All =
    {
        new LevelDef
        {
            name = "FERN VALLEY", length = 450, speedStart = 8f, speedEnd = 10.5f,
            gapMin = 1.0f, gapMax = 1.8f, comboGap = 0.85f, maxHeight = 16, meteorEvery = 10f,
            rockS = 3, rockM = 2, rockL = 0.6f, fern = 2, log = 1.5f, cluster = 0.5f,
            step = 0.6f, ramp = 0.8f, mound = 1.4f,
            sky = new Sky { top = C(0x4aa8e0), bottom = C(0xbfe8f0), far = Color.white, mid = Color.white, front = Color.white, sunY = 1f },
            ground = new GroundStyle
            {
                grass = H(0x5fae3c), grassDark = H(0x3f8a2e), grassLight = H(0x8fd45a), dirt = H(0xa0683a), dirtDark = H(0x7d4f2c),
                dirtLight = H(0xb9824e), deep = H(0x8e5a32), deepBand = H(0x6e4426), pebble = H(0x8b8496), pebbleLight = H(0xb9b2c2),
                bone = H(0xefe6d0), pit = PitKind.Chasm,
            },
        },
        new LevelDef
        {
            name = "TAR PITS", length = 600, speedStart = 10.5f, speedEnd = 13f,
            gapMin = 0.9f, gapMax = 1.5f, pitMin = 0.22f, pitMax = 0.34f, comboGap = 0.8f, maxHeight = 32, meteorEvery = 8f,
            rockS = 2.5f, rockM = 2, rockL = 1, fern = 1.5f, log = 1.5f, cluster = 1,
            pteroLow = 1, pteroHigh = 0.8f, pit = 2.5f, step = 1, ramp = 1, mound = 0.6f, combo = 0.8f,
            sky = new Sky
            {
                top = C(0x6a9ad0), bottom = C(0xf0dca0), far = new Color(1f, 0.92f, 0.85f), mid = new Color(0.95f, 0.9f, 0.8f),
                front = Color.white, sunY = 0.7f,
            },
            ground = new GroundStyle
            {
                grass = H(0x8a9a3a), grassDark = H(0x6a7a2a), grassLight = H(0xb0bc5a), dirt = H(0x8a6040), dirtDark = H(0x6a4630),
                dirtLight = H(0xa07450), deep = H(0x74502f), deepBand = H(0x5a3c22), pebble = H(0x7a7480), pebbleLight = H(0xa49eaa),
                bone = H(0xefe6d0), pit = PitKind.Tar,
            },
        },
        new LevelDef
        {
            name = "RAPTOR RIDGE", length = 750, speedStart = 13f, speedEnd = 15.5f,
            gapMin = 0.85f, gapMax = 1.35f, pitMin = 0.28f, pitMax = 0.4f, comboGap = 0.75f, maxHeight = 32, bigSteps = true,
            meteorEvery = 6f, smoke = 1.5f,
            rockS = 2, rockM = 2, rockL = 1.5f, fern = 1, log = 1, cluster = 1.5f,
            pteroLow = 1.2f, pteroHigh = 1.2f, pit = 2, step = 2.5f, ramp = 1, mound = 0.4f, combo = 1.5f,
            sky = new Sky
            {
                top = C(0x5a4f9a), bottom = C(0xf4a06a), far = new Color(1f, 0.78f, 0.78f), mid = new Color(0.85f, 0.72f, 0.72f),
                front = new Color(1f, 0.9f, 0.85f), sunY = 0.32f, stars = 0.15f,
            },
            ground = new GroundStyle
            {
                grass = H(0x4fa05a), grassDark = H(0x2f7a40), grassLight = H(0x86d07a), dirt = H(0xc07a4a), dirtDark = H(0x9a5a34),
                dirtLight = H(0xd8955e), deep = H(0xa8653c), deepBand = H(0x864c2a), pebble = H(0x8b8496), pebbleLight = H(0xb9b2c2),
                bone = H(0xefe6d0), pit = PitKind.Chasm,
            },
        },
        new LevelDef
        {
            name = "VOLCANO", length = 900, speedStart = 15.5f, speedEnd = 18f,
            gapMin = 0.82f, gapMax = 1.25f, pitMin = 0.3f, pitMax = 0.43f, comboGap = 0.7f, maxHeight = 32, bigSteps = true,
            meteorEvery = 4f, smoke = 4f, weather = Weather.Ash,
            rockS = 1.5f, rockM = 2, rockL = 1.5f, log = 0.5f, cluster = 1.5f,
            pteroLow = 1.2f, pteroHigh = 1.2f, fireball = 2, pit = 2.2f, step = 1.5f, ramp = 0.8f, mound = 0.3f, combo = 2,
            sky = new Sky
            {
                top = C(0x120c1e), bottom = C(0x5a1e2a), far = new Color(0.55f, 0.35f, 0.45f), mid = new Color(0.35f, 0.28f, 0.35f),
                front = new Color(0.85f, 0.75f, 0.8f), moonY = 0.9f, stars = 0.7f,
            },
            ground = new GroundStyle
            {
                grass = H(0x5a5560), grassDark = H(0x3a3540), grassLight = H(0x7a7580), dirt = H(0x3a2e34), dirtDark = H(0x2a2026),
                dirtLight = H(0x4a3c44), deep = H(0x30252b), deepBand = H(0x22191e), pebble = H(0x5a5060), pebbleLight = H(0x7a7080),
                bone = H(0xc8bca8), glow = H(0xff6a2a), pit = PitKind.Lava, blades = false, cracks = true,
            },
        },
        new LevelDef
        {
            name = "EXTINCTION", length = 1050, speedStart = 18f, speedEnd = 21f,
            gapMin = 0.8f, gapMax = 1.15f, pitMin = 0.32f, pitMax = 0.45f, comboGap = 0.66f, maxHeight = 32, bigSteps = true,
            meteorEvery = 1.5f, smoke = 6f, weather = Weather.Embers,
            rockS = 1.5f, rockM = 2, rockL = 2, log = 0.5f, cluster = 2,
            pteroLow = 1.3f, pteroHigh = 1.3f, fireball = 3, pit = 2.2f, step = 1.5f, ramp = 0.8f, mound = 0.2f, combo = 3,
            sky = new Sky
            {
                top = C(0x3a0a10), bottom = C(0xc0401a), far = new Color(0.6f, 0.3f, 0.25f), mid = new Color(0.4f, 0.2f, 0.18f),
                front = new Color(1f, 0.82f, 0.75f), stars = 0.2f, doom = 1f,
            },
            ground = new GroundStyle
            {
                grass = H(0x6a3a2a), grassDark = H(0x4a2418), grassLight = H(0x8a5034), dirt = H(0x5a2e22), dirtDark = H(0x40201a),
                dirtLight = H(0x703a2a), deep = H(0x4a2218), deepBand = H(0x341610), pebble = H(0x5a4448), pebbleLight = H(0x7a6064),
                bone = H(0xd8c8b0), glow = H(0xff8a2a), pit = PitKind.Lava, blades = false, cracks = true,
            },
        },
    };
}
