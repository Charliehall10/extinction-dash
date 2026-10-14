using UnityEngine;

// A stretch of ground: one height per pixel column, painted into its own texture.
public class Chunk
{
    public const int Pit = int.MinValue;

    public float x;    // track position of the left edge (a whole pixel)
    public int[] h;    // surface height per column in pixels, or Pit
    public int col0;   // global column index, so the noise lines up across chunks
    public GroundStyle style;
    public SpriteRenderer sr;
    public Texture2D tex;

    public float Right => x + h.Length * Art.PX;
}

public static class GroundPainter
{
    public const int Above = 36; // room above the base line for ledges and grass blades

    static readonly Color32 TarTop = Art.Hex(0x3a2e48), TarShine = Art.Hex(0x6a5a80), Tar = Art.Hex(0x16121c),
        LavaTop = Art.Hex(0xffd860), Lava = Art.Hex(0xff8a1a), LavaDeep = Art.Hex(0xc8400e), LavaDeeper = Art.Hex(0x8a2410),
        ChasmTop = Art.Hex(0x2a1a24), Chasm = Art.Hex(0x120a12);

    static float Hash(int x, int y, int salt)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xffffff) / (float)0x1000000;
        }
    }

    static int Div(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;
    static int Mod(int a, int b) => ((a % b) + b) % b;

    // depth: pixels of earth to draw below the base line.
    public static void Paint(Chunk c, int depth)
    {
        int w = c.h.Length, H = depth + Above;
        if (c.tex == null || c.tex.width != w || c.tex.height != H)
        {
            if (c.tex != null) { Object.Destroy(c.sr.sprite); Object.Destroy(c.tex); }
            c.tex = new Texture2D(w, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        }
        else if (c.sr.sprite != null) Object.Destroy(c.sr.sprite);

        var px = new Color32[w * H];
        var s = c.style;
        for (int i = 0; i < w; i++)
        {
            int wx = c.col0 + i;
            int hc = c.h[i];
            if (hc == Chunk.Pit)
            {
                PaintPit(px, w, depth, i, wx, s);
                continue;
            }
            int hl = i > 0 ? c.h[i - 1] : hc, hr = i < w - 1 ? c.h[i + 1] : hc;
            int top = Mathf.Min(hc, Above - 3);
            for (int y = -depth; y < top; y++)
            {
                int d = top - 1 - y;
                Color32 col;
                if (d == 0) col = s.grassLight;
                else if (d < 3) col = Hash(wx, y, 1) < 0.15f ? s.grassDark : s.grass;
                else if (d < 5) col = Hash(wx, y, 2) < 0.5f ? s.dirtDark : s.dirt;
                else if (y < -44 - (int)(Hash(wx, 0, 3) * 3)) col = Deep(wx, y, s);
                else col = Dirt(wx, y, s);
                if (s.cracks && d >= 3 && Hash(Div(wx, 2), Div(y, 4), 11) < 0.016f) col = s.glow;
                px[(y + depth) * w + i] = col;
            }

            // Ledge faces and pit walls get an outline so steps read clearly.
            int faceFrom = int.MaxValue;
            if (hl == Chunk.Pit || hl < hc - 2) faceFrom = hl == Chunk.Pit ? -PitDepth : hl;
            if (hr == Chunk.Pit || hr < hc - 2) faceFrom = Mathf.Min(faceFrom, hr == Chunk.Pit ? -PitDepth : hr);
            if (faceFrom != int.MaxValue)
            {
                for (int y = Mathf.Max(-depth, faceFrom); y < top; y++) px[(y + depth) * w + i] = Art.Outline;
            }
            else if (s.blades)
            {
                float r = Hash(wx, 5, 4);
                if (r < 0.3f) px[(top + depth) * w + i] = s.grass;
                if (r < 0.1f) px[(top + 1 + depth) * w + i] = s.grassLight;
            }
        }

        c.tex.SetPixels32(px);
        c.tex.Apply(false);
        c.sr.sprite = Sprite.Create(c.tex, new Rect(0, 0, w, H), new Vector2(0, depth / (float)H), Art.PPU, 0, SpriteMeshType.FullRect);
    }

    // Pits are filled a few units deep; below that is rock, so on tall
    // (portrait) screens they don't run all the way down the screen.
    const int PitDepth = 40;

    static void PaintPit(Color32[] px, int w, int depth, int i, int wx, GroundStyle s)
    {
        for (int y = -depth; y < 0; y++)
        {
            Color32 col;
            if (y < -PitDepth - (int)(Hash(wx, 0, 14) * 3)) col = Deep(wx, y, s);
            else switch (s.pit)
            {
                case PitKind.Tar:
                    if (y > -7) continue;
                    col = y == -7 ? (Hash(wx, 0, 12) < 0.2f ? TarShine : TarTop) : Tar;
                    break;
                case PitKind.Lava:
                    if (y > -7) continue;
                    col = y == -7 ? LavaTop : y > -10 ? (Hash(wx, y, 13) < 0.15f ? LavaTop : Lava) : y > -18 ? LavaDeep : LavaDeeper;
                    break;
                default:
                    col = y > -4 ? ChasmTop : Chasm;
                    break;
            }
            px[(y + depth) * w + i] = col;
        }
    }

    static Color32 Dirt(int wx, int y, GroundStyle s)
    {
        if (Hash(Div(wx, 3), Div(y, 2), 8) < 0.02f)
            return Mod(wx, 3) == 0 && Mod(y, 2) == 1 ? s.pebbleLight : s.pebble;
        float n = Hash(wx, y, 10);
        return n < 0.05f ? s.dirtDark : n < 0.08f ? s.dirtLight : s.dirt;
    }

    static Color32 Deep(int wx, int y, GroundStyle s)
    {
        int s1 = -64 + Mathf.RoundToInt(3 * Mathf.Sin(wx * 0.07f));
        int s2 = -128 + Mathf.RoundToInt(4 * Mathf.Sin(wx * 0.05f + 1f));
        if (y == s1 || y == s1 - 1 || y == s2 || y == s2 - 1 || ((y == s1 - 2 || y == s2 - 2) && Mod(wx, 3) == 0))
            return s.deepBand;

        // Now and then an ammonite fossil.
        int cx = Div(wx, 32), cy = Div(y, 32);
        if (y < -60 && Hash(cx, cy, 9) < 0.08f)
        {
            float dx = wx - (cx * 32 + 16) + 0.5f, dy = y - (cy * 32 + 16) + 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r < 6f && (Mathf.Abs(r - 5f) < 0.7f || Mathf.Abs(r - 2.8f) < 0.7f || r < 1.2f)) return s.bone;
        }

        if (Hash(Div(wx, 3), Div(y, 2), 6) < 0.015f) return s.pebble;
        float n = Hash(wx, y, 7);
        return n < 0.05f ? s.deepBand : n < 0.07f ? s.dirt : s.deep;
    }
}
