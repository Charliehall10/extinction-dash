using System.Collections.Generic;
using UnityEngine;

// Every sprite in the game is drawn here in code: pixel-art strings for the
// characters, and small generators for rocks, logs, ground and scenery.
public static class Art
{
    public const int PPU = 16;
    public const float PX = 1f / PPU;

    public static Color32 Hex(int rgb, byte a = 255) =>
        new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, a);

    public static readonly Color32 Outline = Hex(0x1d261f);
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
    {
        ['K'] = Outline,
        ['G'] = Hex(0x6cc24a), // dino green
        ['D'] = Hex(0x3f8f3c), // dino back stripe
        ['L'] = Hex(0xd8ee9a), // dino belly
        ['W'] = Hex(0xffffff),
        ['R'] = Hex(0xd94a3a), // tongue
        ['P'] = Hex(0xb05c8a), // pterodactyl
        ['Q'] = Hex(0x7a3a66), // pterodactyl shade
        ['S'] = Hex(0xf0c090), // pterodactyl belly
        ['Y'] = Hex(0xf2b13c), // beak
        ['A'] = Hex(0xf09a1c), // amber
        ['a'] = Hex(0xffe07a), // amber highlight
        ['O'] = Hex(0xb85e0c), // amber shade
        ['F'] = Hex(0x2f9e5a), // fern
        ['f'] = Hex(0x1f6e44), // fern stem
        ['T'] = Hex(0x8a5a34), // dirt mound
    };

    // ---------------------------------------------------------------- pixel grid

    public class PixelGrid
    {
        public readonly int W, H;
        public readonly Color32[] Px;

        public PixelGrid(int w, int h) { W = w; H = h; Px = new Color32[w * h]; }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Px[y * W + x] = c;
        }

        // Wraps horizontally so tiling textures stay seamless.
        public void SetWrap(int x, int y, Color32 c)
        {
            if (y < 0 || y >= H) return;
            x = ((x % W) + W) % W;
            Px[y * W + x] = c;
        }

        public Color32 Get(int x, int y) =>
            x < 0 || y < 0 || x >= W || y >= H ? Clear : Px[y * W + x];

        public bool Solid(int x, int y) => Get(x, y).a > 0;

        public void Disc(float cx, float cy, float r, Color32 c)
        {
            for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r) Set(x, y, c);
                }
        }
    }

    // Rows are written top to bottom; '.' is transparent.
    static PixelGrid FromStrings(string[] rows)
    {
        int w = 0;
        foreach (var r in rows) w = Mathf.Max(w, r.Length);
        var g = new PixelGrid(w, rows.Length);
        for (int row = 0; row < rows.Length; row++)
            for (int x = 0; x < rows[row].Length; x++)
                if (Palette.TryGetValue(rows[row][x], out var c))
                    g.Set(x, rows.Length - 1 - row, c);
        return g;
    }

    // Pads by one pixel and rings every solid shape with the outline colour.
    static PixelGrid WithOutline(PixelGrid src)
    {
        var g = new PixelGrid(src.W + 2, src.H + 2);
        for (int y = 0; y < src.H; y++)
            for (int x = 0; x < src.W; x++)
                g.Set(x + 1, y + 1, src.Get(x, y));
        var copy = (Color32[])g.Px.Clone();
        for (int y = 0; y < g.H; y++)
            for (int x = 0; x < g.W; x++)
            {
                if (copy[y * g.W + x].a > 0) continue;
                bool edge = Has(copy, g, x - 1, y) || Has(copy, g, x + 1, y) ||
                            Has(copy, g, x, y - 1) || Has(copy, g, x, y + 1);
                if (edge) g.Set(x, y, Outline);
            }
        return g;
    }

    static bool Has(Color32[] px, PixelGrid g, int x, int y) =>
        x >= 0 && y >= 0 && x < g.W && y < g.H && px[y * g.W + x].a > 0;

    // Pivot is given in whole pixels so sprites land exactly on the pixel grid.
    public static Sprite ToSprite(PixelGrid g, int pivotX, int pivotY, bool repeat = false)
    {
        var tex = new Texture2D(g.W, g.H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
        };
        tex.SetPixels32(g.Px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, g.W, g.H),
            new Vector2(pivotX / (float)g.W, pivotY / (float)g.H), PPU, 0, SpriteMeshType.FullRect);
    }

    // Standing sprite: pivot at the bottom-centre, one pixel up so the
    // outline under the feet overlaps the top row of the ground.
    static Sprite Standing(PixelGrid g) => ToSprite(g, g.W / 2, 1);

    // ---------------------------------------------------------------- characters

    static readonly string[] DinoHead =
    {
        "..........GGGGGGG...",
        ".........GGGGGGGGGG.",
        ".........GGWKGGGGGGG",
        ".........GGWKGGGGGGG",
        ".........GGGGGGGGGGG",
        ".........GGGGGGKKKKK",
        ".........GGGGGGGGGG.",
    };

    static readonly string[] DinoDeadHead =
    {
        "..........GGGGGGG...",
        ".........GGGGGGGGGG.",
        ".........GKGKGGGGGGG",
        ".........GGKGGGGGGGG",
        ".........GKGKGGGGGGG",
        ".........GGGGGGKKKKK",
        ".........GGGGGGGRRG.",
    };

    static readonly string[] DinoBody =
    {
        "........DGGGGGG.....",
        "D......DGGGGLLGGG...",
        "GD....DGGGGLLLLGGG..",
        "GGD..DGGGGLLLLLG.G..",
        ".GGDDGGGGGLLLLLG....",
        "..GGGGGGGGLLLLLG....",
        "....GGGGGGGLLLG.....",
        ".....GGGGGGGGG......",
    };

    static readonly string[] LegsStand =
    {
        "......GGG..GGG......",
        "......GG....GG......",
        "......GGG...GGG.....",
    };

    static readonly string[] LegsRunA =
    {
        "......GGG..GGG......",
        ".....GG......GG.....",
        "....GG........GG....",
    };

    static readonly string[] LegsRunB =
    {
        "......GGG..GGG......",
        ".......GG..GG.......",
        ".......GGG.GGG......",
    };

    static readonly string[] LegsJump =
    {
        "......GGG..GGG......",
        ".....GGG....GGG.....",
        "....................",
    };

    static readonly string[] PteroUp =
    {
        "..........QQ............",
        "..........PQQ...........",
        ".........PPPQQ..........",
        ".........PPPPQQ.........",
        "........PPPPPPPQ........",
        ".....QQ.PPPPPPPPQ.......",
        "....PPPPPPPPPPPPPP......",
        "YYYPKWPPPPPPPPPPPPPPP...",
        "...YPPPPPPSSSSSSPPPPPPPP",
        "......PPPSSSSSSSPPP.....",
        "..........PP...PP.......",
        "........................",
        "........................",
        "........................",
    };

    static readonly string[] PteroDown =
    {
        "........................",
        "........................",
        "........................",
        "........................",
        "........................",
        ".....QQ.................",
        "....PPPPPPPPPPPPPP......",
        "YYYPKWPPPPPPPPPPPPPPP...",
        "...YPPPPPPSSSSSSPPPPPPPP",
        "......PPPPPPPPPPPPPP....",
        "........PPPPPPPPQ.......",
        ".........PPPPPPQ........",
        ".........PPPPQ..........",
        "..........PQ............",
    };

    static readonly string[] AmberRows =
    {
        "..AAA..",
        ".AaaAA.",
        "AaaAAAA",
        "AaAAAAA",
        "AAAKAAO",
        "AAAAAAO",
        ".AAAAO.",
        "..AOO..",
    };

    static readonly string[] FernRows =
    {
        "......F......",
        ".....FF..F...",
        "..F..FF.FF...",
        "..FF.Ff.F..F.",
        "...FFfFFF.FF.",
        ".F..FfFFFFF..",
        ".FF..fFFF..F.",
        "..FFFfFF.FF..",
        "....FfFFFF...",
        ".....fFFF....",
        ".....fFF.....",
        "....TTTTT....",
        "...TTTTTTT...",
    };

    static string[] Concat(params string[][] parts)
    {
        var list = new List<string>();
        foreach (var p in parts) list.AddRange(p);
        return list.ToArray();
    }

    static Sprite Dino(string[] head, string[] legs) =>
        Standing(WithOutline(FromStrings(Concat(head, DinoBody, legs))));

    // ---------------------------------------------------------------- generators

    static readonly Color32[] GreyRock = { Hex(0x5b5566), Hex(0x8b8496), Hex(0xb9b2c2), Hex(0x48424f) };
    static readonly Color32[] Basalt = { Hex(0x2e2830), Hex(0x4a404c), Hex(0x6a5e6a), Hex(0x1e1a20) };
    static readonly Color32[] Burning = { Hex(0x2e2830), Hex(0x4a404c), Hex(0x6a5e6a), Hex(0xff7a2a) };

    static PixelGrid Rock(int w, int h, int seed, Color32[] pal, int cracks = -1)
    {
        var rng = new System.Random(seed);
        var g = new PixelGrid(w, h);
        Color32 dark = pal[0], mid = pal[1], light = pal[2], crack = pal[3];
        float ph = (float)rng.NextDouble() * 6f;
        for (int x = 0; x < w; x++)
        {
            float nx = (x + 0.5f) / w * 2f - 1f;
            float top = Mathf.Pow(Mathf.Max(0f, 1f - nx * nx), 0.4f) * h + Mathf.Sin(x * 0.9f + ph) * 0.7f;
            int ty = Mathf.Clamp(Mathf.RoundToInt(top), 1, h);
            for (int y = 0; y < ty; y++)
            {
                float lit = -nx * 0.55f + (y / (float)ty) * 0.9f;
                var c = lit > 0.62f ? light : lit < 0.12f ? dark : mid;
                g.Set(x, y, c);
            }
        }
        // A couple of cracks.
        for (int i = 0; i < (cracks < 0 ? w / 6 : cracks); i++)
        {
            int cx = rng.Next(2, w - 2), cy = rng.Next(1, Mathf.Max(2, h - 3));
            for (int k = 0; k < 3; k++)
                if (g.Solid(cx + k, cy + (k % 2)) && g.Solid(cx + k, cy + 2)) g.Set(cx + k, cy + (k % 2), crack);
        }
        return g;
    }

    static PixelGrid LogGrid(int w, int h)
    {
        var g = new PixelGrid(w, h);
        Color32 bark = Hex(0x8a5a34), barkDark = Hex(0x6a4224), barkLight = Hex(0xa87444);
        Color32 wood = Hex(0xe0b980), ring = Hex(0xb08050);
        var rng = new System.Random(7);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w - 3; x++)
            {
                if (x == 0 && (y == 0 || y == h - 1)) continue;
                var c = y == h - 1 ? barkLight : bark;
                if ((y == 2 || y == 5) && rng.NextDouble() < 0.7) c = barkDark;
                g.Set(x, y, c);
            }
        // Cut end: an ellipse with growth rings.
        float cx = w - 4.5f, cy = h / 2f, rx = 3.5f, ry = h / 2f;
        for (int y = 0; y < h; y++)
            for (int x = w - 8; x < w; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                float d = dx * dx + dy * dy;
                if (d > 1f) continue;
                g.Set(x, y, d < 0.12f || (d > 0.35f && d < 0.55f) ? ring : wood);
            }
        // A knot on the bark.
        g.Set(6, 3, barkDark); g.Set(7, 3, barkDark); g.Set(6, 4, barkDark);
        return g;
    }

    static float Wave(int x, int w, params (float amp, int k, float ph)[] parts)
    {
        float v = 0;
        foreach (var p in parts) v += p.amp * Mathf.Sin(2f * Mathf.PI * p.k * x / w + p.ph);
        return v;
    }

    public const int FarTile = 320, MidTile = 256;

    static PixelGrid FarGrid()
    {
        int W = FarTile, H = 96;
        var g = new PixelGrid(W, H);
        Color32 body = Hex(0x6a6fa8), rim = Hex(0x8a8fc8), lava = Hex(0xff8a3a), lavaHot = Hex(0xffd060);
        int volcano = 210;
        for (int x = 0; x < W; x++)
        {
            float h = 34 + Wave(x, W, (10, 2, 1f), (6, 5, 2f), (3, 11, 0.5f));
            int dv = Mathf.Abs(x - volcano);
            float cone = 84 - dv * 1.35f;
            if (dv < 6) cone = 78; // crater
            int top = Mathf.RoundToInt(Mathf.Max(h, cone));
            for (int y = 0; y < top; y++) g.Set(x, y, y >= top - 1 ? rim : body);
        }
        // Lava glow in the crater and a couple of streaks down the side.
        for (int x = volcano - 5; x <= volcano + 5; x++) { g.Set(x, 77, lava); if (Mathf.Abs(x - volcano) < 3) g.Set(x, 78, lavaHot); }
        for (int k = 0; k < 9; k++) { g.Set(volcano - 7 - k, 75 - Mathf.RoundToInt(k * 1.35f), lava); }
        for (int k = 0; k < 6; k++) { g.Set(volcano + 7 + k, 75 - Mathf.RoundToInt(k * 1.35f), lava); }
        return g;
    }

    static PixelGrid MidGrid()
    {
        int W = MidTile, H = 80;
        var g = new PixelGrid(W, H);
        Color32 body = Hex(0x2f6e4a), rim = Hex(0x3f8a5a), dark = Hex(0x24573a);
        var tops = new int[W];
        for (int x = 0; x < W; x++)
        {
            tops[x] = Mathf.RoundToInt(24 + Wave(x, W, (6, 2, 0.3f), (4, 5, 1.3f), (2, 13, 0.7f)));
            for (int y = 0; y < tops[x]; y++) g.Set(x, y, y >= tops[x] - 1 ? rim : body);
        }
        foreach (var (px, len, lean) in new[] { (40, 34, 1f), (132, 44, -1f), (150, 30, 1f), (214, 40, 1f) })
        {
            int baseY = tops[px] - 2;
            int tx = px, ty = baseY;
            for (int t = 0; t < len; t++)
            {
                tx = px + Mathf.RoundToInt(Mathf.Sin(t / (float)len * 1.3f) * 6f * lean);
                ty = baseY + t;
                g.SetWrap(tx, ty, dark); g.SetWrap(tx + 1, ty, dark);
            }
            // Fronds arch out of the crown and droop, with leaflets hanging underneath.
            foreach (var (dir, reach, rise) in new[] { (-1f, 13, 0.6f), (1f, 13, 0.6f), (-1f, 10, 0.15f), (1f, 10, 0.15f), (-0.5f, 8, 1.1f), (0.5f, 8, 1.1f) })
                for (int i = 0; i <= reach; i++)
                {
                    int fx = tx + Mathf.RoundToInt(dir * i);
                    int fy = ty + Mathf.RoundToInt(i * rise - i * i * (rise + 0.6f) / reach);
                    g.SetWrap(fx, fy, dark);
                    if (i < reach / 2) g.SetWrap(fx, fy + 1, dark);
                    if (i > 2 && i < reach - 1 && i % 2 == 0)
                    {
                        g.SetWrap(fx, fy - 1, dark);
                        if (i > 4) g.SetWrap(fx, fy - 2, dark);
                    }
                }
            g.SetWrap(tx - 1, ty - 1, dark); g.SetWrap(tx + 2, ty - 1, dark);
        }
        return g;
    }

    static PixelGrid Cloud(int w, int h, int seed)
    {
        var g = new PixelGrid(w, h);
        var rng = new System.Random(seed);
        Color32 white = Hex(0xffffff), shade = Hex(0xd6e4f0);
        int puffs = w / 6;
        for (int i = 0; i < puffs; i++)
        {
            float cx = Mathf.Lerp(h * 0.45f, w - h * 0.45f, i / (float)(puffs - 1));
            float r = h * (0.32f + (float)rng.NextDouble() * 0.2f);
            g.Disc(cx, r * 0.9f + 1, r, white);
        }
        for (int x = 0; x < w; x++)
            for (int y = 0; y < 3; y++)
                if (g.Solid(x, y)) g.Set(x, y, shade);
        return g;
    }

    static PixelGrid Stars(int w, int h)
    {
        var g = new PixelGrid(w, h);
        var rng = new System.Random(3);
        for (int i = 0; i < 70; i++)
        {
            int x = rng.Next(w), y = rng.Next(h);
            var c = rng.NextDouble() < 0.3 ? Hex(0xfff2c0) : Hex(0xffffff);
            g.Set(x, y, c);
            if (rng.NextDouble() < 0.12) { g.Set(x + 1, y, c); g.Set(x - 1, y, c); g.Set(x, y + 1, c); g.Set(x, y - 1, c); }
        }
        return g;
    }

    static PixelGrid Meteor()
    {
        // Head at the bottom-left, tail trailing up and to the right.
        var g = new PixelGrid(28, 14);
        for (int i = 0; i < 26; i++)
        {
            int x = 2 + i, y = 2 + i / 2;
            byte a = (byte)Mathf.Clamp(255 - i * 10, 0, 255);
            g.Set(x, y, Hex(i < 8 ? 0xffb050 : 0xff7a3a, a));
            if (i < 14) g.Set(x, y + 1, Hex(0xff7a3a, (byte)(a / 2)));
        }
        g.Disc(3f, 3f, 2.2f, Hex(0xfff0b0));
        return g;
    }

    static PixelGrid Fireball()
    {
        // A glowing rock with flames streaming up behind it as it falls.
        var g = new PixelGrid(14, 28);
        for (int y = 8; y < 28; y++)
        {
            float half = Mathf.Max(0.6f, 4.5f - (y - 8) * 0.22f);
            byte a = (byte)Mathf.Clamp(300 - (y - 8) * 14, 60, 255);
            var c = y < 13 ? Hex(0xffe070, a) : y < 19 ? Hex(0xff9a2a, a) : Hex(0xe0401a, a);
            float cx = 7f + Mathf.Sin(y * 0.8f) * 0.8f;
            for (int x = Mathf.FloorToInt(cx - half); x <= Mathf.CeilToInt(cx + half); x++)
                if (Mathf.Abs(x + 0.5f - cx) <= half) g.Set(x, y, c);
        }
        g.Disc(7, 6, 5.5f, Hex(0xff7a2a));
        g.Disc(7, 6, 4.5f, Hex(0x4a3a3a));
        g.Disc(6, 7, 2.2f, Hex(0x6a5050));
        g.Set(8, 4, Hex(0xff7a2a)); g.Set(9, 5, Hex(0xff7a2a));
        return g;
    }

    static readonly string[] TargetRows =
    {
        "..RRRRRRRRR..",
        ".R.........R.",
        "R...........R",
        ".R.........R.",
        "..RRRRRRRRR..",
    };

    static readonly string[] FlagRows =
    {
        ".TWWRRWWRRWW",
        ".TWWRRWWRRWW",
        ".TRRWWRRWWRR",
        ".TRRWWRRWWRR",
        ".TWWRRWWRRWW",
        ".TWWRRWWRRWW",
        ".T..........", ".T..........", ".T..........", ".T..........", ".T..........",
        ".T..........", ".T..........", ".T..........", ".T..........", ".T..........",
        ".T..........", ".T..........", ".T..........", ".T..........", ".T..........",
        "TTT.........",
    };

    static PixelGrid Doom()
    {
        var g = new PixelGrid(32, 32);
        for (int i = 0; i < 18; i++)
        {
            float r = 4.2f - i * 0.2f;
            var c = i < 5 ? Hex(0xffd060) : i < 11 ? Hex(0xff8a2a) : Hex(0xd0401a, 200);
            g.Disc(14 + i, 14 + i, r, c);
        }
        g.Disc(12, 12, 9.3f, Hex(0xff8a2a, 220));
        g.Disc(12, 12, 8f, Hex(0x5a2e22));
        g.Disc(10, 14, 4f, Hex(0x7a4030));
        g.Disc(14, 9, 1.5f, Hex(0x401a14));
        g.Disc(8, 10, 1f, Hex(0x401a14));
        g.Disc(16, 14, 1f, Hex(0x401a14));
        return g;
    }

    // ---------------------------------------------------------------- the sprite set

    public static Sprite[] DinoRun;
    public static Sprite DinoStand, DinoJump, DinoDead;
    public static Sprite[] Ptero;
    public static Sprite Amber, Fern, Log;
    // Rocks come in two sets: [0] grey, [1] volcanic basalt.
    public static Sprite[][] RocksSmall, RocksMedium, RocksLarge;
    public static Sprite FireballFall, BurningRock, Target, Flag, Puff, DoomMeteor;
    public static Sprite Far, Mid, StarField, Sun, Moon, MeteorStreak, Dot;
    public static Sprite[] Clouds;

    static bool built;

    public static void Build()
    {
        if (built) return;
        built = true;

        DinoStand = Dino(DinoHead, LegsStand);
        DinoRun = new[] { Dino(DinoHead, LegsRunA), Dino(DinoHead, LegsRunB) };
        DinoJump = Dino(DinoHead, LegsJump);
        DinoDead = Dino(DinoDeadHead, LegsStand);

        var up = WithOutline(FromStrings(PteroUp));
        var down = WithOutline(FromStrings(PteroDown));
        Ptero = new[] { ToSprite(up, up.W / 2, 0), ToSprite(down, down.W / 2, 0) };

        var amber = WithOutline(FromStrings(AmberRows));
        Amber = ToSprite(amber, amber.W / 2, amber.H / 2);
        Fern = Standing(WithOutline(FromStrings(FernRows)));
        Log = Standing(WithOutline(LogGrid(26, 10)));

        RocksSmall = new Sprite[2][];
        RocksMedium = new Sprite[2][];
        RocksLarge = new Sprite[2][];
        for (int set = 0; set < 2; set++)
        {
            var pal = set == 0 ? GreyRock : Basalt;
            RocksSmall[set] = new[] { Standing(WithOutline(Rock(11, 9, 1, pal))), Standing(WithOutline(Rock(12, 10, 2, pal))) };
            RocksMedium[set] = new[] { Standing(WithOutline(Rock(16, 14, 3, pal))), Standing(WithOutline(Rock(18, 15, 4, pal))) };
            RocksLarge[set] = new[] { Standing(WithOutline(Rock(22, 22, 5, pal))), Standing(WithOutline(Rock(20, 24, 6, pal))) };
        }
        BurningRock = Standing(WithOutline(Rock(17, 14, 9, Burning, 7)));
        FireballFall = ToSprite(Fireball(), 7, 0);
        var target = FromStrings(TargetRows);
        Target = ToSprite(target, target.W / 2, 2);
        Flag = Standing(WithOutline(FromStrings(FlagRows)));
        var puff = new PixelGrid(6, 6);
        puff.Disc(3, 3, 3f, Hex(0xe4e4ec));
        Puff = ToSprite(puff, 3, 3);
        DoomMeteor = ToSprite(Doom(), 12, 12);
        Far = ToSprite(FarGrid(), 0, 0, true);
        Mid = ToSprite(MidGrid(), 0, 0, true);
        StarField = ToSprite(Stars(200, 120), 0, 0, true);
        Clouds = new[]
        {
            ToSprite(Cloud(30, 12, 1), 15, 0), ToSprite(Cloud(42, 15, 2), 21, 0), ToSprite(Cloud(24, 10, 3), 12, 0),
        };

        var sun = new PixelGrid(24, 24);
        sun.Disc(12, 12, 11, Hex(0xffe9a0));
        sun.Disc(12, 12, 8, Hex(0xfff6d8));
        Sun = ToSprite(sun, 12, 12);

        var moon = new PixelGrid(18, 18);
        moon.Disc(9, 9, 8, Hex(0xe8ecf8));
        moon.Disc(6, 11, 2, Hex(0xc4cbe0));
        moon.Disc(11, 6, 1.5f, Hex(0xc4cbe0));
        moon.Disc(12, 12, 1, Hex(0xc4cbe0));
        Moon = ToSprite(moon, 9, 9);

        MeteorStreak = ToSprite(Meteor(), 3, 3);

        var dot = new PixelGrid(1, 1);
        dot.Set(0, 0, Hex(0xffffff));
        Dot = ToSprite(dot, 0, 0);
    }
}
