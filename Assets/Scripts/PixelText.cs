using System.Collections.Generic;
using UnityEngine;

// Text drawn with a 3x5 bitmap font into a small texture, so it sits on the
// same pixel grid as the rest of the art.
public class PixelText : MonoBehaviour
{
    public enum Align { Left, Center, Right }

    // Each glyph is 5 rows of 3 pixels, top row first.
    static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
    {
        ['A'] = ".#. #.# ### #.# #.#", ['B'] = "##. #.# ##. #.# ##.", ['C'] = ".## #.. #.. #.. .##",
        ['D'] = "##. #.# #.# #.# ##.", ['E'] = "### #.. ##. #.. ###", ['F'] = "### #.. ##. #.. #..",
        ['G'] = ".## #.. #.# #.# .##", ['H'] = "#.# #.# ### #.# #.#", ['I'] = "### .#. .#. .#. ###",
        ['J'] = "..# ..# ..# #.# .#.", ['K'] = "#.# #.# ##. #.# #.#", ['L'] = "#.. #.. #.. #.. ###",
        ['M'] = "#.# ### ### #.# #.#", ['N'] = "##. #.# #.# #.# #.#", ['O'] = "### #.# #.# #.# ###",
        ['P'] = "##. #.# ##. #.. #..", ['Q'] = ".#. #.# #.# ##. .##", ['R'] = "##. #.# ##. #.# #.#",
        ['S'] = ".## #.. .#. ..# ##.", ['T'] = "### .#. .#. .#. .#.", ['U'] = "#.# #.# #.# #.# ###",
        ['V'] = "#.# #.# #.# #.# .#.", ['W'] = "#.# #.# ### ### #.#", ['X'] = "#.# #.# .#. #.# #.#",
        ['Y'] = "#.# #.# .#. .#. .#.", ['Z'] = "### ..# .#. #.. ###",
        ['0'] = "### #.# #.# #.# ###", ['1'] = ".#. ##. .#. .#. ###", ['2'] = "##. ..# .#. #.. ###",
        ['3'] = "##. ..# .#. ..# ##.", ['4'] = "#.# #.# ### ..# ..#", ['5'] = "### #.. ##. ..# ##.",
        ['6'] = ".## #.. ### #.# ###", ['7'] = "### ..# .#. .#. .#.", ['8'] = "### #.# ### #.# ###",
        ['9'] = "### #.# ### ..# ##.",
        ['!'] = ".#. .#. .#. ... .#.", [':'] = "... .#. ... .#. ...", ['.'] = "... ... ... ... .#.",
        ['-'] = "... ... ### ... ...", ['+'] = "... .#. ### .#. ...", ['\''] = ".#. .#. ... ... ...",
        [','] = "... ... ... .#. #..", ['?'] = "##. ..# .#. ... .#.", [' '] = "... ... ... ... ...",
        ['<'] = "..# .#. #.. .#. ..#", ['>'] = "#.. .#. ..# .#. #..", ['='] = "... ### ... ### ...",
    };

    static string Glyph(char c) =>
        (Glyphs.TryGetValue(c, out var g) ? g : Glyphs[' ']).Replace(" ", "");

    SpriteRenderer sr;
    Texture2D tex;
    Color32[] px;
    string text;
    int scale;
    Align align;
    Color32 color;

    public SpriteRenderer Renderer => sr;

    public static PixelText Create(string name, Transform parent, int scale, Align align, Color32 color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<PixelText>();
        t.sr = go.AddComponent<SpriteRenderer>();
        t.sr.sortingOrder = order;
        t.scale = scale;
        t.align = align;
        t.color = color;
        return t;
    }

    public void SetColor(Color32 c)
    {
        if (c.Equals(color)) return;
        color = c;
        Redraw();
    }

    public void Set(string s)
    {
        s = s.ToUpperInvariant();
        if (s == text) return;
        text = s;
        Redraw();
    }

    public float WidthUnits => tex == null ? 0 : tex.width * Art.PX;

    void Redraw()
    {
        int n = Mathf.Max(1, text.Length);
        int w = (n * 4 - 1) * scale + 2, h = 5 * scale + 2;
        if (tex == null || tex.width != w || tex.height != h)
        {
            if (tex != null) { Destroy(sr.sprite); Destroy(tex); }
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            px = new Color32[w * h];
            float pivotX = align == Align.Left ? 0 : align == Align.Right ? w : w / 2;
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(pivotX / w, 1f), Art.PPU, 0, SpriteMeshType.FullRect);
        }

        var mask = new bool[w * h];
        for (int i = 0; i < text.Length; i++)
        {
            string g = Glyph(text[i]);
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 3; col++)
                {
                    if (g[row * 3 + col] != '#') continue;
                    for (int sy = 0; sy < scale; sy++)
                        for (int sx = 0; sx < scale; sx++)
                        {
                            int x = 1 + (i * 4 + col) * scale + sx;
                            int y = h - 2 - (row * scale + sy);
                            mask[y * w + x] = true;
                        }
                }
        }

        var outline = Art.Outline;
        var clear = new Color32(0, 0, 0, 0);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (mask[y * w + x]) { px[y * w + x] = color; continue; }
                bool near = false;
                for (int dy = -1; dy <= 1 && !near; dy++)
                    for (int dx = -1; dx <= 1 && !near; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        near = nx >= 0 && ny >= 0 && nx < w && ny < h && mask[ny * w + nx];
                    }
                px[y * w + x] = near ? outline : clear;
            }
        tex.SetPixels32(px);
        tex.Apply(false);
    }

    void OnDestroy()
    {
        if (tex != null) { Destroy(sr.sprite); Destroy(tex); }
    }
}
