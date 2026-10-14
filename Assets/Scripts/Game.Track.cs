using System.Collections.Generic;
using UnityEngine;

// The track: terrain chunks and the hazards on them, generated a little ahead
// of the screen one segment at a time. A segment is one feature (a rock, a pit,
// a ledge, a combo...) followed by a flat run-up to the next one, sized in
// seconds of travel so the spacing stays fair as the speed climbs.
public partial class Game
{
    readonly List<Chunk> chunks = new List<Chunk>();
    readonly Queue<(float x, bool win)> boundaries = new Queue<(float, bool)>();
    readonly List<int> seg = new List<int>();
    float genX, segX, genLevelStart;
    int genCol, genHeight, genLevel;
    bool genTitle, genDone;

    LevelDef GL => Levels.All[genLevel];
    float GenProgress => (genX - DinoX - genLevelStart) / GL.length;
    float BaseGenSpeed => Mathf.Lerp(GL.speedStart, GL.speedEnd, Mathf.Clamp01(GenProgress));
    // Run-ups assume the top multiplier, so gaps never get too short in time.
    float GenSpeed => BaseGenSpeed * (1f + SpeedPerMult * (MaxMult - 1));
    int RockSet => genLevel >= 3 ? 1 : 0;

    void ResetTrack(int lvl, bool title)
    {
        foreach (var c in chunks) DestroyChunk(c);
        chunks.Clear();
        for (int i = things.Count - 1; i >= 0; i--) RemoveThing(i);
        boundaries.Clear();
        scroll = 0;
        genX = -2f;
        genCol = genHeight = 0;
        genLevel = lvl;
        genLevelStart = 0;
        genTitle = title;
        genDone = false;

        BeginSegment();
        Flat(Cols(2f + DinoX + (title ? unitsW : 1.8f * GL.speedStart)));
        Commit();
        Generate();
        PlaceTrack();
    }

    void Generate()
    {
        while (genX < scroll + unitsW + 4f) GenerateSegment();
        while (chunks.Count > 0 && chunks[0].Right - scroll < -2f)
        {
            DestroyChunk(chunks[0]);
            chunks.RemoveAt(0);
        }
    }

    void PlaceTrack()
    {
        foreach (var c in chunks) c.sr.transform.position = new Vector3(Snap(c.x - scroll), 0, 0);
    }

    // Height of the ground at a track position, or -infinity over a pit.
    float HeightAt(float x)
    {
        foreach (var c in chunks)
        {
            if (x >= c.Right) continue;
            if (x < c.x) break;
            int h = c.h[Mathf.Clamp((int)((x - c.x) / PX), 0, c.h.Length - 1)];
            return h == Chunk.Pit ? float.NegativeInfinity : h * PX;
        }
        return 0;
    }

    void DestroyChunk(Chunk c)
    {
        Destroy(c.sr.sprite);
        Destroy(c.tex);
        Destroy(c.sr.gameObject);
    }

    // ------------------------------------------------------------------ segment building

    int Cols(float units) => Mathf.Max(1, Mathf.RoundToInt(units / PX));
    float SegX(int col) => segX + col * PX;

    void BeginSegment()
    {
        seg.Clear();
        segX = genX;
    }

    void Commit()
    {
        var c = new Chunk { x = segX, h = seg.ToArray(), col0 = genCol, style = GL.ground };
        c.sr = MakeSprite("Ground", null, 0);
        GroundPainter.Paint(c, depthPx);
        c.sr.color = palette.front;
        c.sr.transform.position = new Vector3(Snap(c.x - scroll), 0, 0);
        chunks.Add(c);
        genX += c.h.Length * PX;
        genCol += c.h.Length;
    }

    void Flat(int n)
    {
        for (int i = 0; i < n; i++) seg.Add(genHeight);
    }

    void GenerateSegment()
    {
        BeginSegment();
        if (genTitle)
        {
            if (rng.NextDouble() < 0.4) Mound(Cols(Rand(3f, 6f)), rng.Next(4, 10));
            Flat(Cols(Rand(3f, 8f)));
            Commit();
            return;
        }
        if (genDone)
        {
            Flat(Cols(10f));
            Commit();
            return;
        }
        if (GenProgress >= 1f)
        {
            Finish();
            return;
        }

        var l = GL;
        float[] weights =
        {
            l.rockS, l.rockM, l.rockL, l.fern, l.log, l.cluster, l.pteroLow, l.pteroHigh,
            l.fireball, l.pit, l.step, l.ramp, l.mound, l.combo,
        };
        switch (WeightedPick(weights))
        {
            case 0: Obstacle(Kind.Rock, Pick(Art.RocksSmall[RockSet]), true); break;
            case 1: Obstacle(Kind.Rock, Pick(Art.RocksMedium[RockSet]), true); break;
            case 2: Obstacle(Kind.Rock, Pick(Art.RocksLarge[RockSet]), true); break;
            case 3: Obstacle(Kind.Fern, Art.Fern, true); break;
            case 4: Obstacle(Kind.Log, Art.Log, true); break;
            case 5: Cluster(); break;
            case 6: Ptero(Kind.PteroLow); break;
            case 7: Ptero(Kind.PteroHigh); break;
            case 8: Fireball(); break;
            case 9: Pit(true); break;
            case 10: Step(); break;
            case 11: Ramp(); break;
            case 12: Mound(Cols(Rand(3f, 6f)), rng.Next(6, 15)); break;
            default: Combo(); break;
        }

        // Flat run-up to the next feature, sometimes with a row of amber.
        float run = Rand(l.gapMin, l.gapMax) * GenSpeed;
        int start = seg.Count;
        Flat(Cols(run));
        if (run > 4f && rng.NextDouble() < 0.35) AmberRow(start, seg.Count);
        Commit();
    }

    // The finish flag, then the next level's ground.
    void Finish()
    {
        Flat(Cols(1f));
        float flagX = SegX(seg.Count);
        AddThing(Kind.Flag, Art.Flag, flagX, genHeight * PX, default, 8);
        Flat(Cols(1.2f * GenSpeed));
        bool win = genLevel == Levels.All.Length - 1;
        Commit();
        boundaries.Enqueue((flagX, win));
        if (win)
        {
            genDone = true;
            return;
        }
        genLevel++;
        genLevelStart = flagX - DinoX;
        BeginSegment();
        Flat(Cols(1.2f * GenSpeed));
        Commit();
    }

    // ------------------------------------------------------------------ features

    void Obstacle(Kind kind, Sprite sprite, bool amberArc)
    {
        int w = (int)sprite.rect.width;
        Flat(6);
        int c = seg.Count + w / 2;
        Flat(w + 4);
        float x = SegX(c), y = genHeight * PX;
        AddThing(kind, sprite, x, y, GroundHit(sprite), 10);
        if (amberArc && rng.NextDouble() < 0.35)
        {
            float top = y + (sprite.rect.height - 2) * PX;
            for (int k = -1; k <= 1; k++) AddAmber(x + k * 0.95f, top + (k == 0 ? 1.55f : 1.05f));
        }
    }

    void Cluster()
    {
        Flat(6);
        int n = rng.Next(2, 4);
        for (int i = 0; i < n; i++)
        {
            var s = Pick(Art.RocksSmall[RockSet]);
            AddThing(Kind.Rock, s, SegX(seg.Count + 6), genHeight * PX, GroundHit(s), 10);
            Flat(12);
        }
        Flat(8);
    }

    // Low ones have to be jumped; high ones fly over a dino that stays down.
    void Ptero(Kind kind)
    {
        Flat(Cols(0.4f * GenSpeed));
        float y = genHeight * PX + (kind == Kind.PteroLow ? 0.15f : 1.3f);
        var t = AddThing(kind, Art.Ptero[0], SegX(seg.Count), y, Rect.MinMaxRect(-8 * PX, 0.28f, 10 * PX, 0.6f), 10);
        t.vx = 1.5f;
        t.anim = Rand(0, 1);
        Flat(Cols(0.4f * GenSpeed));
    }

    void Fireball()
    {
        Flat(6);
        int c = seg.Count + 9;
        Flat(26);
        var t = AddThing(Kind.Fireball, Art.FireballFall, SegX(c), genHeight * PX, default, 11);
        t.sr.enabled = false;
        t.marker = MakeSprite("Target", Art.Target, 9);
    }

    // Pit widths are timed against the base speed, so a faster dino only finds them easier.
    void Pit(bool amber)
    {
        Flat(6);
        int start = seg.Count;
        int n = Cols(Rand(GL.pitMin, GL.pitMax) * BaseGenSpeed);
        for (int i = 0; i < n; i++) seg.Add(Chunk.Pit);
        Flat(8);
        if (amber && rng.NextDouble() < 0.4)
        {
            float mid = SegX(start + n / 2), y = genHeight * PX;
            AddAmber(mid - 1f, y + 1.9f);
            AddAmber(mid, y + 2.5f);
            AddAmber(mid + 1f, y + 1.9f);
        }
    }

    void Step()
    {
        int max = GL.maxHeight;
        var options = new List<int>();
        if (genHeight + 16 <= max) options.Add(genHeight + 16);
        if (GL.bigSteps && genHeight + 32 <= max) options.Add(genHeight + 32);
        if (genHeight >= 16) options.Add(genHeight - 16);
        if (genHeight >= 32) options.Add(0);
        Flat(6);
        if (options.Count > 0) genHeight = options[rng.Next(options.Count)];
        Flat(10);
    }

    void Ramp()
    {
        int to = genHeight + (rng.NextDouble() < 0.5 ? 16 : -16);
        if (to < 0 || to > GL.maxHeight) to = 2 * genHeight - to;
        Flat(4);
        int from = genHeight, n = Mathf.Abs(to - from) * 2;
        for (int i = 1; i <= n; i++) seg.Add(Mathf.RoundToInt(Mathf.Lerp(from, to, i / (float)n)));
        genHeight = to;
        Flat(6);
    }

    void Mound(int n, int amp)
    {
        amp = Mathf.Min(amp, GroundPainter.Above - 5 - genHeight);
        if (amp < 3)
        {
            Flat(n);
            return;
        }
        for (int i = 0; i < n; i++)
            seg.Add(genHeight + Mathf.RoundToInt(amp * Mathf.Sin(Mathf.PI * (i + 0.5f) / n)));
    }

    // Two hazards close together. The gap between them is set per level and
    // checked against the jump: there's always time to land and go again.
    void Combo()
    {
        float gap = GL.comboGap * BaseGenSpeed;
        int kinds = genLevel <= 1 ? 2 : GL.fireball > 0 ? 5 : 4;
        var small = Pick(Art.RocksSmall[RockSet]);
        switch (rng.Next(kinds))
        {
            case 0: // pit, then a rock just after landing
                Pit(false);
                Flat(Mathf.Max(4, Cols(gap) - 8 - 6 - 7));
                Obstacle(Kind.Rock, small, false);
                break;
            case 1: // two rocks in a quick rhythm
                Obstacle(Kind.Rock, Pick(Art.RocksSmall[RockSet]), false);
                Flat(Mathf.Max(4, Cols(gap) - 24));
                Obstacle(Kind.Rock, rng.NextDouble() < 0.5 ? small : Pick(Art.RocksMedium[RockSet]), false);
                break;
            case 2: // up a ledge with a rock on top
                if (genHeight + 16 > GL.maxHeight)
                {
                    Step();
                    break;
                }
                Flat(6);
                genHeight += 16;
                Flat(Mathf.Max(4, Cols(gap + 0.15f * BaseGenSpeed) - 6 - 7));
                Obstacle(Kind.Rock, small, false);
                break;
            case 3: // stairs, up or down
                int dir = genHeight + 32 <= GL.maxHeight ? 16 : -16;
                if (genHeight + 2 * dir < 0) dir = 16;
                Flat(6);
                genHeight += dir;
                Flat(Cols(gap + 0.1f * BaseGenSpeed));
                genHeight = Mathf.Clamp(genHeight + dir, 0, GL.maxHeight);
                Flat(8);
                break;
            default: // a boulder lands, then a rock
                Fireball();
                Flat(Mathf.Max(4, Cols(gap) - 20));
                Obstacle(Kind.Rock, small, false);
                break;
        }
    }

    // ------------------------------------------------------------------ things

    Thing AddThing(Kind kind, Sprite sprite, float x, float y, Rect hit, int order)
    {
        var t = new Thing { kind = kind, sr = MakeSprite(kind.ToString(), sprite, order), tx = x, y = y, ground = y, hit = hit };
        t.sr.transform.position = new Vector3(Snap(x - scroll), Snap(y), 0);
        t.sr.color = palette.front;
        things.Add(t);
        return t;
    }

    void AddAmber(float x, float y)
    {
        var t = AddThing(Kind.Amber, Art.Amber, x, y, Rect.MinMaxRect(-0.38f, -0.38f, 0.38f, 0.38f), 5);
        t.anim = Rand(0, 6);
    }

    void AmberRow(int from, int to)
    {
        float mid = (SegX(from) + SegX(to)) / 2f;
        for (int i = 0; i < 4; i++) AddAmber(mid + (i - 1.5f) * 0.8f, genHeight * PX + 0.5f);
    }

    // Ground obstacles use a hitbox a few pixels inside the art so near
    // misses feel fair.
    static Rect GroundHit(Sprite sprite)
    {
        int w = (int)sprite.rect.width, h = (int)sprite.rect.height, left = w / 2;
        return Rect.MinMaxRect((-left + 3) * PX, 0, (w - left - 3) * PX, (h - 4) * PX);
    }

    Sprite Pick(Sprite[] sprites) => sprites[rng.Next(sprites.Length)];

    int WeightedPick(float[] weights)
    {
        float total = 0;
        foreach (var w in weights) total += w;
        float r = Rand(0, total);
        for (int i = 0; i < weights.Length; i++)
        {
            r -= weights[i];
            if (r < 0 && weights[i] > 0) return i;
        }
        return 0;
    }
}
