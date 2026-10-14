using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// HUD, input, and the testing hooks (bot player and headless screenshots).
public partial class Game
{
    static readonly Color32 Yellow = Art.Hex(0xffd23a), White = Art.Hex(0xffffff), Grey = Art.Hex(0xd0d4dc),
        Red = Art.Hex(0xff6a4a), AmberColor = Art.Hex(0xf09a1c);

    PixelText scoreText, hiText, multText, levelText, titleText, levelSelText, arrowLeft, arrowRight,
        tip1, tip2, tip3, hintText, overText, resultText, bestText, tapText, bannerText, bannerSub, stampText;
    Bar progressBar, rushBar;
    float bannerTime;

    // input
    bool pressed, held, down;
    int stepInput, pressSide;

    // Testing: "-autoplay" lets a bot play; "-shots <dir>" also saves
    // screenshots (every "-shotevery" seconds until "-shotmax", plus level-ups
    // and deaths) and then quits. "-screen WxH" fakes the window size,
    // "-level N" starts straight into level N, and "-botquit <seconds>" makes
    // the bot give up so the game-over screen gets checked.
    bool autoplay;
    string shotDir;
    float botHold, botCooldown, clock, botQuitAt = -1, shotEvery = 10f, shotMax = 100f, nextShot;
    int forcedW, forcedH, startLevel = -1, deaths;
    readonly List<(string name, float at)> pendingShots = new List<(string, float)>();

    int ScreenW => forcedW > 0 ? forcedW : Screen.width;
    int ScreenH => forcedH > 0 ? forcedH : Screen.height;
    bool Testing => shotDir != null;

    // A small bar drawn from three stretched pixels.
    class Bar
    {
        readonly SpriteRenderer frame, back, fill;
        readonly int width;

        public Bar(Game g, int width)
        {
            this.width = width;
            frame = g.MakeSprite("Bar", Art.Dot, 100);
            back = g.MakeSprite("Bar", Art.Dot, 101);
            fill = g.MakeSprite("Bar", Art.Dot, 102);
            frame.color = Art.Outline;
            back.color = Art.Hex(0x3a3440);
        }

        public void Show(bool on) => frame.enabled = back.enabled = fill.enabled = on;

        // (x, y) is the top-left corner.
        public void Set(float x, float y, float value, Color color)
        {
            frame.transform.position = new Vector3(x - PX, y - 5 * PX, 0);
            frame.transform.localScale = new Vector3(width + 2, 5, 1);
            back.transform.position = new Vector3(x, y - 4 * PX, 0);
            back.transform.localScale = new Vector3(width, 3, 1);
            fill.transform.position = back.transform.position;
            fill.transform.localScale = new Vector3(Mathf.Round(Mathf.Clamp01(value) * width), 3, 1);
            fill.color = color;
        }
    }

    void ParseArgs()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string next = i + 1 < args.Length ? args[i + 1] : "";
            switch (args[i])
            {
                case "-autoplay": autoplay = true; break;
                case "-shots": shotDir = next; autoplay = true; break;
                case "-botquit": float.TryParse(next, out botQuitAt); break;
                case "-shotevery": float.TryParse(next, out shotEvery); break;
                case "-shotmax": float.TryParse(next, out shotMax); break;
                case "-level":
                    if (int.TryParse(next, out int n)) startLevel = Mathf.Clamp(n, 1, Levels.All.Length) - 1;
                    break;
                case "-screen":
                    var parts = next.Split('x');
                    int.TryParse(parts[0], out forcedW);
                    if (parts.Length > 1) int.TryParse(parts[1], out forcedH);
                    break;
            }
        }
        if (Application.platform == RuntimePlatform.WebGLPlayer && Application.absoluteURL.Contains("autoplay"))
            autoplay = true;
        if (Testing)
        {
            Directory.CreateDirectory(shotDir);
            Time.captureFramerate = 60;
            nextShot = shotEvery; // test runs also never read or write saved progress
        }
    }

    void ReadInput(float dt)
    {
        bool touchBegan = false, touching = false;
        Vector2 touchPos = Vector2.zero;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            touching = true;
            if (t.phase != TouchPhase.Began) continue;
            touchBegan = true;
            touchPos = t.position;
        }
        bool click = Input.GetMouseButtonDown(0);
        pressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) ||
                  Input.GetKeyDown(KeyCode.W) || click || touchBegan;
        held = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow) ||
               Input.GetKey(KeyCode.W) || Input.GetMouseButton(0) || touching;
        down = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
        stepInput = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) ? -1
                  : Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) ? 1 : 0;

        // Taps near the left or right edge pick a level on the title screen.
        pressSide = 0;
        if (click || touchBegan)
        {
            float f = (touchBegan ? touchPos.x : Input.mousePosition.x) / Mathf.Max(1, Screen.width);
            pressSide = f < 0.22f ? -1 : f > 0.78f ? 1 : 0;
        }

        if (Input.GetKeyDown(KeyCode.M)) AudioListener.volume = AudioListener.volume > 0 ? 0 : 1;
        if (Input.GetKeyDown(KeyCode.P) && state == State.Playing)
        {
            paused = !paused;
            pressed = false;
        }

        if (autoplay) Bot(dt);
    }

    // Jumps pits, ledges and anything on the ground; ignores high pterodactyls.
    void Bot(float dt)
    {
        botHold -= dt;
        botCooldown -= dt;
        pressed = false;
        held = botHold > 0;
        down = false;
        stepInput = pressSide = 0;
        if (state != State.Playing)
        {
            pressed = stateTime > (state == State.Won ? 3f : 1.5f);
            return;
        }
        if ((botQuitAt > 0 && clock > botQuitAt) || botCooldown > 0) return;

        float feet = scroll + DinoX;
        float baseH = grounded ? dinoY : lastSolid;
        bool jump = false;
        for (float d = 0.25f; d < 0.25f + speed * 0.5f; d += 2 * PX)
        {
            float h = HeightAt(feet + d), dist = d - 0.25f;
            if (float.IsNegativeInfinity(h))
            {
                jump = dist < speed * 0.05f + 0.1f;
                break;
            }
            if (h > baseH + 0.3f)
            {
                jump = dist < speed * 0.13f + 0.25f;
                break;
            }
        }
        if (!jump)
        {
            float nearest = float.MaxValue;
            foreach (var t in things)
            {
                if (!t.Solid || t.kind == Kind.PteroHigh) continue;
                float front = t.tx - scroll + t.hit.xMin - (DinoX + 0.32f);
                if (front >= -0.3f) nearest = Mathf.Min(nearest, front);
            }
            jump = nearest < speed * 0.11f + 0.2f;
        }
        if (!jump) return;
        pressed = held = true;
        botHold = 0.28f;
        botCooldown = 0.15f;
    }

    // ------------------------------------------------------------------ HUD

    void BuildHud()
    {
        PixelText T(string name, int scale, PixelText.Align align, Color32 color) =>
            PixelText.Create(name, root, scale, align, color, 100);

        scoreText = T("Score", 1, PixelText.Align.Right, White);
        hiText = T("Hi", 1, PixelText.Align.Right, Grey);
        multText = T("Mult", 1, PixelText.Align.Right, Yellow);
        levelText = T("Level", 1, PixelText.Align.Left, White);
        titleText = T("Title", 3, PixelText.Align.Center, Yellow);
        levelSelText = T("LevelSelect", 1, PixelText.Align.Center, Yellow);
        arrowLeft = T("ArrowLeft", 2, PixelText.Align.Left, White);
        arrowRight = T("ArrowRight", 2, PixelText.Align.Right, White);
        tip1 = T("Tip1", 1, PixelText.Align.Center, White);
        tip2 = T("Tip2", 1, PixelText.Align.Center, White);
        tip3 = T("Tip3", 1, PixelText.Align.Center, White);
        hintText = T("Hint", 1, PixelText.Align.Center, White);
        overText = T("Over", 2, PixelText.Align.Center, Red);
        resultText = T("Result", 1, PixelText.Align.Center, White);
        bestText = T("Best", 1, PixelText.Align.Center, Yellow);
        tapText = T("Tap", 1, PixelText.Align.Center, White);
        bannerText = T("Banner", 2, PixelText.Align.Center, Yellow);
        bannerSub = T("BannerSub", 1, PixelText.Align.Center, White);
        stampText = T("Stampede", 2, PixelText.Align.Center, Yellow);

        titleText.Set("EXTINCTION DASH");
        arrowLeft.Set("<");
        arrowRight.Set(">");
        tip2.Set("AMBER = MORE POINTS AND MORE SPEED");
        tip3.Set("FILL THE AMBER BAR TO STAMPEDE!");
        stampText.Set("STAMPEDE!");
        progressBar = new Bar(this, 60);
        rushBar = new Bar(this, 40);
    }

    void Banner()
    {
        bannerTime = 2.5f;
        bannerText.Set("LEVEL " + (level + 1));
        bannerSub.Set(L.name);
    }

    static void Place(PixelText t, float x, float y) => t.transform.position = new Vector3(Snap(x), Snap(y), 0);

    void UpdateHud(float dt)
    {
        bannerTime -= dt;
        float right = unitsW - 6 * PX, left = 6 * PX, top = viewTop - 6 * PX, centre = unitsW / 2f;
        float headline = Mathf.Min(Mathf.Lerp(3f, viewTop, 0.62f), 7f);
        bool blink = (int)(clock * 2.5f) % 2 == 0, fast = (int)(clock * 8f) % 2 == 0;
        bool title = state == State.Title && !paused;
        bool run = state != State.Title;
        bool live = state == State.Playing || state == State.Dead;
        bool over = state == State.Dead && !paused, won = state == State.Won;

        // Score, best, multiplier and rush bar on the right.
        scoreText.Set(Mathf.Min(99999, Mathf.FloorToInt(score)).ToString("D5"));
        Place(scoreText, right, top);
        scoreText.Renderer.enabled = run;
        hiText.Set("HI " + Mathf.Min(99999, best).ToString("D5"));
        Place(hiText, run ? right - scoreText.WidthUnits - 8 * PX : right, top);
        hiText.Renderer.enabled = best > 0;

        multText.Set("X" + Mult);
        Place(multText, right, top - 9 * PX);
        multText.Renderer.enabled = live;
        rushBar.Show(live);
        float rushValue = stampede > 0 ? stampede / StampedeTime : rush;
        rushBar.Set(Snap(right - multText.WidthUnits - 43 * PX), Snap(top - 10 * PX), rushValue,
            stampede > 0 ? (fast ? Color.white : (Color)Yellow) : (Color)AmberColor);

        // Level name and progress on the left.
        levelText.Set($"LEVEL {level + 1} {L.name}");
        Place(levelText, left, top);
        levelText.Renderer.enabled = live;
        progressBar.Show(live);
        progressBar.Set(Snap(left + PX), Snap(top - 10 * PX), LevelProgress, Yellow);

        // Title screen.
        titleText.Renderer.enabled = levelSelText.Renderer.enabled = tip2.Renderer.enabled = tip3.Renderer.enabled = title;
        tip1.Renderer.enabled = title || won;
        hintText.Renderer.enabled = title && blink;
        arrowLeft.Renderer.enabled = arrowRight.Renderer.enabled = title && unlocked > 1;
        if (title)
        {
            float bob = Mathf.Round(Mathf.Sin(clock * 3f)) * PX;
            Place(titleText, centre, headline + 1.2f + bob);
            levelSelText.Set($"LEVEL {titleLevel + 1}: {Levels.All[titleLevel].name}");
            Place(levelSelText, centre, headline - 0.2f);
            Place(arrowLeft, left, headline - 0.05f);
            Place(arrowRight, right, headline - 0.05f);
            tip1.Set("HOLD TO JUMP HIGHER");
            Place(tip1, centre, headline - 0.9f);
            Place(tip2, centre, headline - 1.35f);
            Place(tip3, centre, headline - 1.8f);
            hintText.Set(Application.isMobilePlatform ? "TAP TO START" : "PRESS SPACE OR TAP TO START");
            Place(hintText, centre, headline - 2.6f);
        }

        // Level banners and the stampede call-out.
        bool banner = state == State.Playing && bannerTime > 0 && (bannerTime > 0.5f || fast);
        bannerText.Renderer.enabled = bannerSub.Renderer.enabled = banner;
        Place(bannerText, centre, headline + 0.6f);
        Place(bannerSub, centre, headline - 0.5f);
        stampText.Renderer.enabled = state == State.Playing && stampede > 0 && bannerTime <= 0 && fast;
        Place(stampText, centre, headline + 0.6f);

        // Game over, victory and pause.
        overText.Renderer.enabled = over || won || paused;
        overText.Set(paused ? "PAUSED" : won ? "YOU MADE IT!" : "GAME OVER");
        overText.SetColor(won ? Yellow : Red);
        Place(overText, centre, headline + 0.6f);
        if (won)
        {
            tip1.Set("YOU OUTRAN THE EXTINCTION!");
            Place(tip1, centre, headline - 0.3f);
        }
        resultText.Renderer.enabled = over || won;
        resultText.Set("SCORE " + Mathf.FloorToInt(score).ToString("D5"));
        Place(resultText, centre, headline - (won ? 1.0f : 0.5f));
        bestText.Renderer.enabled = (over || won) && (!newBest || blink);
        bestText.Set(newBest ? "NEW BEST!" : "BEST " + best.ToString("D5"));
        Place(bestText, centre, headline - (won ? 1.5f : 1.0f));
        tapText.Renderer.enabled = (paused || (over && stateTime > 0.6f) || (won && stateTime > 2f)) && blink;
        tapText.Set(paused ? "TAP TO RESUME" : won ? "TAP TO PLAY AGAIN" : $"TAP TO RETRY LEVEL {level + 1}");
        Place(tapText, centre, headline - (paused ? 0.6f : won ? 2.3f : 1.9f));
    }

    // ------------------------------------------------------------------ screenshots

    void QueueShot(string name, float delay)
    {
        if (Testing) pendingShots.Add((name, clock + delay));
    }

    void LateUpdate()
    {
        if (!Testing) return;
        for (int i = pendingShots.Count - 1; i >= 0; i--)
        {
            if (clock < pendingShots[i].at) continue;
            Capture(pendingShots[i].name);
            pendingShots.RemoveAt(i);
        }
        if (clock >= nextShot)
        {
            Capture($"t{nextShot:000.###}");
            nextShot += shotEvery;
        }
        if (clock >= shotMax) Application.Quit();
    }

    void Capture(string name)
    {
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(viewW, viewH, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, viewW, viewH), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        const int s = 3;
        var src = tex.GetPixels32();
        var dst = new Color32[viewW * s * viewH * s];
        for (int y = 0; y < viewH * s; y++)
            for (int x = 0; x < viewW * s; x++)
                dst[y * viewW * s + x] = src[(y / s) * viewW + x / s];
        var big = new Texture2D(viewW * s, viewH * s, TextureFormat.RGB24, false);
        big.SetPixels32(dst);
        File.WriteAllBytes(Path.Combine(shotDir, name + ".png"), big.EncodeToPNG());
        Destroy(tex);
        Destroy(big);
        Debug.Log($"[ExtinctionDash] shot {name} t={clock:0.0} state={state} level={level + 1} progress={LevelProgress:0.00} score={score:0} speed={speed:0.0} x{Mult}");
    }
}
