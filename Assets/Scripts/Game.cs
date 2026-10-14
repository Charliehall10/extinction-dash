using System;
using System.Collections.Generic;
using UnityEngine;

// Extinction Dash: a one-button dino runner across five levels. The whole game is built
// here at runtime, so the scene in the build is empty.
//
// Everything renders into a low-resolution texture (180px tall in landscape)
// which is then scaled up to the window, keeping the pixel art crisp.
//
// Split across files: Game.cs (loop, dino, hazards, scenery), Game.Track.cs
// (terrain and level generation) and Game.Hud.cs (HUD, bot, screenshots).
public partial class Game : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => new GameObject("Extinction Dash").AddComponent<Game>();

    const float PX = Art.PX;

    // Jump tuning: a full jump reaches ~3.2 units and lasts ~0.65s. Letting go
    // early cuts the rise short; holding down drops faster.
    const float Gravity = 62f, JumpVelocity = 20f, ReleaseGravity = 3f, FastFallGravity = 3.5f;
    const float JumpBufferTime = 0.12f, CoyoteTime = 0.08f;
    const float TitleSpeed = 4f, DinoX = 3f;
    const int MinViewHeight = 180, MinViewWidth = 280, MaxViewHeight = 720;

    // Amber Rush: every few amber in a row raises the multiplier, which scales
    // both points and speed. Missing an amber drops a tier. A full rush bar
    // sets off a stampede that smashes through everything.
    const float ScorePerUnit = 2f, AmberBonus = 25f, SmashBonus = 50f, SpeedPerMult = 0.05f;
    const float StampedeTime = 5f, StampedeBoost = 1.35f, RushPerAmber = 0.0625f;
    const int ChainPerMult = 5, MaxMult = 5;

    enum State { Title, Playing, Dead, Won }
    enum Kind { Rock, Fern, Log, PteroLow, PteroHigh, Amber, Fireball, Flag }

    class Thing
    {
        public Kind kind;
        public SpriteRenderer sr, marker;
        public float tx, y, vx, vy, ground, anim;
        public int phase; // fireballs: 0 waiting, 1 falling, 2 landed
        public bool missed;
        public Rect hit; // relative to (x, y)

        public bool Solid => kind != Kind.Amber && kind != Kind.Flag && !(kind == Kind.Fireball && phase == 0);
    }

    class Particle
    {
        public SpriteRenderer sr;
        public Vector2 pos, vel;
        public Color color;
        public float life, maxLife, gravity, grow;
    }

    class Popup
    {
        public PixelText text;
        public float x, y, life;
    }

    // view
    Camera cam;
    RenderTexture rt;
    int viewW, viewH, screenW, screenH, depthPx;
    float unitsW, unitsH, viewTop, groundShown;
    Vector3 camBase;

    // scenery
    SpriteRenderer sky, stars, sun, moon, doom, far, mid, flash;
    Texture2D skyTex;
    readonly Color32[] skyPx = new Color32[16];
    readonly List<SpriteRenderer> clouds = new List<SpriteRenderer>();
    readonly List<(SpriteRenderer sr, Vector2 vel)> meteors = new List<(SpriteRenderer, Vector2)>();
    float scrollFar, scrollMid, meteorTimer = 3f, smokeTimer, weatherTimer;
    Sky palette, targetPalette;

    // dino
    SpriteRenderer dino;
    float dinoY, dinoVy, runAnim, jumpBuffer, coyote, dustTimer, lastSolid;
    bool grounded = true;

    // run
    State state = State.Title;
    bool paused, newBest;
    int level, titleLevel, unlocked, best, chain;
    float speed, scroll, score, levelStartScroll, levelStartScore, stateTime, shake, flashAlpha, rush, stampede;
    readonly List<Thing> things = new List<Thing>();
    readonly List<Particle> particles = new List<Particle>();
    readonly List<Popup> popups = new List<Popup>();
    readonly System.Random rng = new System.Random();
    Transform root;
    Sfx sfx;

    int Mult => Mathf.Min(MaxMult, 1 + chain / ChainPerMult);
    LevelDef L => Levels.All[level];
    float LevelProgress => Mathf.Clamp01((scroll - levelStartScroll) / L.length);
    int SceneryLevel => state == State.Title ? titleLevel : level;

    static float Snap(float v) => Mathf.Round(v * Art.PPU) / Art.PPU;

    float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    // ------------------------------------------------------------------ setup

    void Awake()
    {
#if !UNITY_WEBGL
        Application.targetFrameRate = 60;
#endif
        Art.Build();
        ParseArgs();
        best = Testing ? 0 : PlayerPrefs.GetInt("best", 0);
        unlocked = Testing ? 1 : Mathf.Clamp(PlayerPrefs.GetInt("unlocked", 1), 1, Levels.All.Length);
        titleLevel = unlocked - 1;

        root = new GameObject("World").transform;

        var camGo = new GameObject("Camera");
        cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();

        // Something has to draw to the actual screen; this camera just clears
        // it, and OnGUI paints the low-res frame on top.
        var screenCam = new GameObject("Screen Camera").AddComponent<Camera>();
        screenCam.cullingMask = 0;
        screenCam.clearFlags = CameraClearFlags.SolidColor;
        screenCam.backgroundColor = Color.black;
        screenCam.depth = 1;

        BuildScenery();
        dino = MakeSprite("Dino", Art.DinoStand, 20);
        BuildHud();
        sfx = gameObject.AddComponent<Sfx>();

        for (int i = 0; i < 200; i++)
        {
            var p = new Particle { sr = MakeSprite("Particle", Art.Dot, 15) };
            p.sr.enabled = false;
            particles.Add(p);
        }

        palette = targetPalette = Levels.All[titleLevel].sky;
        Layout();
        if (startLevel >= 0) StartRun(startLevel, 0);
        else EnterTitle();
    }

    SpriteRenderer MakeSprite(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    SpriteRenderer MakeTiled(string name, Sprite sprite, int order)
    {
        var sr = MakeSprite(name, sprite, order);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        return sr;
    }

    void BuildScenery()
    {
        skyTex = new Texture2D(1, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        sky = MakeSprite("Sky", Sprite.Create(skyTex, new Rect(0, 0, 1, 16), Vector2.zero, Art.PPU, 0, SpriteMeshType.FullRect), -100);
        stars = MakeTiled("Stars", Art.StarField, -95);
        sun = MakeSprite("Sun", Art.Sun, -90);
        moon = MakeSprite("Moon", Art.Moon, -90);
        doom = MakeSprite("Doom", Art.DoomMeteor, -88);
        for (int i = 0; i < 5; i++) clouds.Add(MakeSprite("Cloud", Art.Clouds[i % Art.Clouds.Length], -80));
        far = MakeTiled("Far", Art.Far, -70);
        mid = MakeTiled("Mid", Art.Mid, -60);
        flash = MakeSprite("Flash", Art.Dot, 90);
        flash.color = new Color(1, 1, 1, 0);
    }

    // Recomputes the low-res view whenever the window changes shape. Narrow
    // (portrait) screens get a taller view with the ground lifted towards the
    // middle and more earth drawn underneath.
    void Layout()
    {
        screenW = ScreenW;
        screenH = ScreenH;
        float aspect = screenW / (float)Mathf.Max(1, screenH);
        viewH = MinViewHeight;
        if (viewH * aspect < MinViewWidth) viewH = Mathf.CeilToInt(MinViewWidth / aspect);
        viewH = Mathf.Min(viewH, MaxViewHeight);
        viewH += viewH & 1;
        viewW = Mathf.RoundToInt(viewH * aspect);
        viewW += viewW & 1;

        if (rt != null)
        {
            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
        }
        rt = new RenderTexture(viewW, viewH, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
        rt.Create();
        cam.targetTexture = rt;

        unitsW = viewW * PX;
        unitsH = viewH * PX;
        groundShown = Snap(2.5f + Mathf.Max(0, unitsH - MinViewHeight * PX) * 0.4f);
        depthPx = Mathf.CeilToInt((groundShown + 0.6f) / PX);
        viewTop = unitsH - groundShown;
        cam.orthographicSize = unitsH / 2f;
        camBase = new Vector3(unitsW / 2f, -groundShown + unitsH / 2f, -10f);
        cam.transform.position = camBase;

        // Backdrops overhang the view by half a unit so screen shake never shows an edge.
        const float m = 0.5f;
        sky.transform.position = new Vector3(-m, -groundShown - m, 0);
        sky.transform.localScale = new Vector3((unitsW + 2 * m) / PX, unitsH + 2 * m, 1);
        stars.transform.position = new Vector3(-m, 2f, 0);
        stars.size = new Vector2(unitsW + 2 * m, Mathf.Max(1f, viewTop - 2f + m));
        far.size = new Vector2(unitsW + Art.FarTile * PX + 2 * m, Art.Far.rect.height * PX);
        mid.size = new Vector2(unitsW + Art.MidTile * PX + 2 * m, Art.Mid.rect.height * PX);
        flash.transform.position = new Vector3(-m, -groundShown - m, 0);
        flash.transform.localScale = new Vector3((unitsW + 2 * m) / PX, (unitsH + 2 * m) / PX, 1);

        foreach (var c in clouds) PlaceCloud(c, Rand(0, unitsW));
        foreach (var c in chunks) GroundPainter.Paint(c, depthPx);
    }

    void PlaceCloud(SpriteRenderer c, float x)
    {
        float lo = Mathf.Min(4f, viewTop - 2f);
        c.transform.position = new Vector3(Snap(x), Snap(Rand(lo, viewTop - 1f)), 0);
    }

    // ------------------------------------------------------------------ loop

    void Update()
    {
        if (ScreenW != screenW || ScreenH != screenH) Layout();
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        clock += Time.deltaTime;
        ReadInput(dt);

        if (paused)
        {
            if (pressed) paused = false;
            UpdateHud(dt);
            return;
        }

        stateTime += dt;
        switch (state)
        {
            case State.Title: UpdateTitle(dt); break;
            case State.Playing: UpdatePlaying(dt); break;
            case State.Dead:
                if (pressed && stateTime > 0.6f) StartRun(level, levelStartScore);
                break;
            case State.Won: UpdateWon(dt); break;
        }

        PlaceTrack();
        UpdateScenery(dt, state == State.Dead ? 0 : speed);
        UpdateParticles(dt);
        UpdatePopups(dt);
        ApplyPalette(dt);
        UpdateHud(dt);
        UpdateCamera(dt);
    }

    void OnGUI()
    {
        if (Event.current.type == EventType.Repaint && rt != null)
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), rt, ScaleMode.StretchToFill, false);
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused && state == State.Playing && !autoplay) paused = true;
    }

    // ------------------------------------------------------------------ states

    void EnterTitle()
    {
        state = State.Title;
        stateTime = 0;
        speed = TitleSpeed;
        stampede = rush = 0;
        chain = 0;
        ResetTrack(titleLevel, true);
        ResetDino();
        targetPalette = Levels.All[titleLevel].sky;
    }

    void UpdateTitle(float dt)
    {
        speed = TitleSpeed;
        scroll += speed * dt;
        Generate();
        UpdateDino(dt, false, false);

        int step = pressSide != 0 ? pressSide : stepInput;
        if (unlocked > 1 && step != 0)
        {
            titleLevel = (titleLevel + step + unlocked) % unlocked;
            ResetTrack(titleLevel, true);
            ResetDino();
            targetPalette = Levels.All[titleLevel].sky;
            sfx.Play("tier", 0.6f);
        }
        else if (pressed) StartRun(titleLevel, 0);
    }

    void StartRun(int startAt, float startScore)
    {
        level = startAt;
        ResetTrack(level, false);
        ResetDino();
        speed = L.speedStart;
        score = levelStartScore = startScore;
        levelStartScroll = 0;
        chain = 0;
        rush = stampede = 0;
        newBest = false;
        state = State.Playing;
        stateTime = 0;
        targetPalette = L.sky;
        Unlock(level);
        Banner();
        sfx.Play("level", 0.7f);
    }

    void ResetDino()
    {
        dinoY = dinoVy = lastSolid = 0;
        grounded = true;
        jumpBuffer = coyote = 0;
    }

    void Unlock(int lvl)
    {
        if (lvl + 1 <= unlocked) return;
        unlocked = lvl + 1;
        if (Testing) return;
        PlayerPrefs.SetInt("unlocked", unlocked);
        PlayerPrefs.Save();
    }

    void UpdatePlaying(float dt)
    {
        float target = Mathf.Lerp(L.speedStart, L.speedEnd, LevelProgress) * (1f + SpeedPerMult * (Mult - 1));
        if (stampede > 0) target *= StampedeBoost;
        speed = Mathf.MoveTowards(speed, target, 5f * dt);
        scroll += speed * dt;
        score += speed * dt * ScorePerUnit * Mult;
        Generate();

        if (boundaries.Count > 0 && scroll + DinoX >= boundaries.Peek().x)
        {
            if (boundaries.Dequeue().win)
            {
                Win();
                return;
            }
            LevelUp();
        }

        if (stampede > 0)
        {
            stampede -= dt;
            // Never drop the dino into a pit the moment a stampede runs out.
            if (stampede <= 0 && float.IsNegativeInfinity(Support(scroll + DinoX))) stampede = 0.05f;
        }

        UpdateDino(dt, true, true);
        if (state != State.Playing) return;
        UpdateThings(dt, true);
    }

    void LevelUp()
    {
        level++;
        levelStartScroll = scroll;
        levelStartScore = score;
        Unlock(level);
        targetPalette = L.sky;
        Banner();
        sfx.Play("level", 0.8f);
        Debug.Log($"[ExtinctionDash] level {level + 1} {L.name} score={score:0} t={clock:0.0}");
        QueueShot($"level{level + 1}", 1.5f);
    }

    void Win()
    {
        state = State.Won;
        stateTime = 0;
        flashAlpha = 1f;
        shake = 0.8f;
        sfx.Play("boom");
        targetPalette = Levels.Aftermath;
        SaveBest();
        Unlock(Levels.All.Length - 1);
        Debug.Log($"[ExtinctionDash] won score={score:0} t={clock:0.0}");
        QueueShot("won", 2f);
    }

    void UpdateWon(float dt)
    {
        speed = Mathf.MoveTowards(speed, TitleSpeed, 4f * dt);
        scroll += speed * dt;
        Generate();
        UpdateDino(dt, true, false);
        UpdateThings(dt, false);
        if (pressed && stateTime > 2f) EnterTitle();
    }

    void Die(string cause)
    {
        state = State.Dead;
        stateTime = 0;
        dino.sprite = Art.DinoDead;
        dino.transform.position = new Vector3(Snap(DinoX), Snap(Mathf.Max(dinoY, -0.5f)), 0);
        shake = 0.35f;
        flashAlpha = 0.7f;
        stampede = 0;
        sfx.Play("hit");
        SaveBest();
        for (int i = 0; i < 10; i++)
            Emit(new Vector2(DinoX + 0.3f, dinoY + 0.6f), new Vector2(Rand(-3f, 3f), Rand(1f, 5f)), Color.white, 0.5f, 14f);
        Debug.Log($"[ExtinctionDash] died level={level + 1} cause={cause} progress={LevelProgress:0.00} speed={speed:0.0} score={score:0} x{Mult}");
        QueueShot($"death{++deaths}", 0.2f);
    }

    void SaveBest()
    {
        int s = Mathf.FloorToInt(score);
        newBest = s > best;
        if (!newBest) return;
        best = s;
        if (Testing) return;
        PlayerPrefs.SetInt("best", best);
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------ dino

    float Support(float feet) => Mathf.Max(HeightAt(feet - 0.15f), HeightAt(feet + 0.22f));

    void UpdateDino(float dt, bool canJump, bool deadly)
    {
        float feet = scroll + DinoX;
        float support = Support(feet);
        if (stampede > 0 && float.IsNegativeInfinity(support)) support = lastSolid; // stampedes run straight over pits

        coyote -= dt;
        if (canJump)
        {
            jumpBuffer = pressed ? JumpBufferTime : jumpBuffer - dt;
            if (jumpBuffer > 0 && (grounded || coyote > 0))
            {
                grounded = false;
                coyote = 0;
                jumpBuffer = 0;
                dinoVy = JumpVelocity;
                sfx.Play("jump", 0.6f);
            }
        }

        if (grounded)
        {
            // Small changes (slopes, hills) are followed; bigger drops mean falling.
            if (support < dinoY - 8 * PX)
            {
                grounded = false;
                coyote = CoyoteTime;
                dinoVy = 0;
            }
            else dinoY = lastSolid = support;
        }

        if (!grounded)
        {
            float g = Gravity;
            if (dinoVy > 0 && !held) g *= ReleaseGravity;
            if (down) g *= FastFallGravity;
            float prevY = dinoY;
            dinoVy -= g * dt;
            dinoY += dinoVy * dt;
            if (dinoVy <= 0 && !float.IsNegativeInfinity(support) && dinoY <= support && prevY >= support - 0.25f)
            {
                dinoY = lastSolid = support;
                dinoVy = 0;
                grounded = true;
                for (int i = 0; i < 5; i++) Dust(Rand(-0.4f, 0.4f), Rand(0.6f, 1.6f));
            }
        }

        if (deadly)
        {
            // Running into a ledge face ends the run (a stampede climbs it instead).
            float front = HeightAt(feet + 0.32f);
            if (!float.IsNegativeInfinity(front) && front > dinoY + 0.3f)
            {
                if (stampede > 0)
                {
                    dinoY = lastSolid = front;
                    dinoVy = Mathf.Max(dinoVy, 0);
                    grounded = true;
                }
                else
                {
                    Die(dinoY < -0.1f ? "pit" : "wall");
                    return;
                }
            }
            if (dinoY < -0.55f)
            {
                Die("pit");
                return;
            }
        }

        runAnim += dt * (6f + speed * 0.6f);
        dino.sprite = grounded ? Art.DinoRun[(int)runAnim % 2] : Art.DinoJump;
        dino.transform.position = new Vector3(Snap(DinoX), Snap(dinoY), 0);

        dustTimer -= dt;
        if (grounded && dustTimer <= 0)
        {
            dustTimer = 0.09f;
            Dust(-speed * 0.35f, Rand(0.3f, 0.9f));
        }
    }

    // ------------------------------------------------------------------ hazards and amber

    void UpdateThings(float dt, bool collide)
    {
        var body = new Rect(DinoX - 0.28f, dinoY + 0.06f, 0.6f, 0.62f);
        var head = new Rect(DinoX, dinoY + 0.72f, 0.56f, 0.34f);
        bool blink = (int)(clock * 8f) % 2 == 0;

        for (int i = things.Count - 1; i >= 0; i--)
        {
            var t = things[i];
            t.anim += dt;
            t.tx -= t.vx * dt;
            float sx = t.tx - scroll;
            float bob = 0;

            switch (t.kind)
            {
                case Kind.PteroLow:
                case Kind.PteroHigh:
                    t.sr.sprite = Art.Ptero[(int)(t.anim * 6f) % 2];
                    break;
                case Kind.Amber:
                    bob = Mathf.Sin(t.anim * 5f) * 1.5f * PX;
                    if (stampede > 0 && Mathf.Abs(sx - DinoX) < 3f)
                    {
                        // Stampede magnet: nearby amber flies to the dino.
                        t.tx += (scroll + DinoX - t.tx) * Mathf.Min(1f, dt * 10f);
                        t.y += (dinoY + 0.6f - t.y) * Mathf.Min(1f, dt * 10f);
                        sx = t.tx - scroll;
                    }
                    if (!t.missed && sx < DinoX - 1f)
                    {
                        t.missed = true;
                        if (collide) MissAmber();
                    }
                    break;
                case Kind.Fireball:
                    UpdateFireball(t, sx, dt);
                    break;
            }

            t.sr.transform.position = new Vector3(Snap(sx), Snap(t.y + bob), 0);
            if (t.marker != null)
            {
                t.marker.enabled = t.phase < 2 && blink;
                t.marker.transform.position = new Vector3(Snap(sx), Snap(t.ground), 0);
            }

            if (sx < -3f)
            {
                RemoveThing(i);
                continue;
            }
            if (!collide || (!t.Solid && t.kind != Kind.Amber)) continue;

            var hit = new Rect(sx + t.hit.x, t.y + t.hit.y, t.hit.width, t.hit.height);
            if (!hit.Overlaps(body) && !hit.Overlaps(head)) continue;
            if (t.kind == Kind.Amber)
            {
                Collect(t, sx);
                RemoveThing(i);
            }
            else if (stampede > 0)
            {
                Smash(t, sx);
                RemoveThing(i);
            }
            else
            {
                Die(t.kind.ToString().ToLowerInvariant());
                return;
            }
        }
    }

    void RemoveThing(int i)
    {
        var t = things[i];
        Destroy(t.sr.gameObject);
        if (t.marker != null) Destroy(t.marker.gameObject);
        things.RemoveAt(i);
    }

    // Flaming boulders wait as a blinking marker, drop when the dino gets
    // close, and land well ahead of it as a burning rock to jump.
    void UpdateFireball(Thing t, float sx, float dt)
    {
        if (t.phase == 0 && sx - DinoX < speed * 1.45f)
        {
            t.phase = 1;
            t.y = Mathf.Max(viewTop, t.ground + 6f) + 1.5f;
            t.vy = -(t.y - t.ground) / 0.55f;
            t.sr.enabled = true;
            t.sr.sprite = Art.FireballFall;
            t.hit = Rect.MinMaxRect(-0.3f, 0.05f, 0.3f, 0.65f);
        }
        else if (t.phase == 1)
        {
            t.y += t.vy * dt;
            if (t.y > t.ground) return;
            t.y = t.ground;
            t.phase = 2;
            t.sr.sprite = Art.BurningRock;
            t.hit = GroundHit(Art.BurningRock);
            shake = Mathf.Max(shake, 0.15f);
            sfx.Play("boom", 0.6f);
            for (int i = 0; i < 12; i++)
                Emit(new Vector2(sx, t.ground + 0.3f), new Vector2(Rand(-4f, 4f), Rand(1f, 5f)),
                    i % 2 == 0 ? Art.Hex(0xff8a2a) : Art.Hex(0xffd060), 0.5f, 12f);
        }
        else if (t.phase == 2 && rng.NextDouble() < dt * 6)
        {
            Emit(new Vector2(sx + Rand(-0.4f, 0.4f), t.ground + 0.8f), new Vector2(Rand(-0.5f, 0.5f), Rand(1f, 2f)), Art.Hex(0xff9a2a), 0.6f, 0f);
        }
    }

    void Collect(Thing t, float sx)
    {
        int before = Mult;
        chain++;
        int gain = Mathf.RoundToInt(AmberBonus * Mult);
        score += gain;
        sfx.Play("amber", 0.7f);
        for (int i = 0; i < 6; i++)
            Emit(new Vector2(sx, t.y), new Vector2(Rand(-2f, 2f), Rand(-2f, 2f)), Art.Hex(0xffe07a), 0.35f, 0f);
        ShowPopup("+" + gain, sx, t.y + 0.5f, Yellow);
        if (Mult > before)
        {
            sfx.Play("tier", 0.8f);
            ShowPopup("X" + Mult + "!", DinoX + 0.3f, dinoY + 1.8f, Yellow);
        }
        if (stampede <= 0)
        {
            rush += RushPerAmber;
            if (rush >= 0.999f) StartStampede();
        }
    }

    void MissAmber()
    {
        if (stampede > 0 || Mult == 1) return;
        chain = Mathf.Max(0, (Mult - 2) * ChainPerMult);
        ShowPopup("X" + Mult, DinoX, dinoY + 1.8f, Red);
    }

    void StartStampede()
    {
        stampede = StampedeTime;
        rush = 0;
        shake = 0.25f;
        sfx.Play("roar");
    }

    void Smash(Thing t, float sx)
    {
        int gain = Mathf.RoundToInt(SmashBonus * Mult);
        score += gain;
        shake = Mathf.Max(shake, 0.12f);
        sfx.Play("smash", 0.8f);
        var c = t.kind == Kind.PteroLow || t.kind == Kind.PteroHigh ? Art.Hex(0xb05c8a) : Art.Hex(0x8b8496);
        for (int i = 0; i < 10; i++)
            Emit(new Vector2(sx, t.y + 0.4f), new Vector2(Rand(2f, 8f), Rand(1f, 6f)), c, 0.6f, 16f);
        ShowPopup("+" + gain, sx, t.y + 1f, Yellow);
    }

    // ------------------------------------------------------------------ effects

    void Dust(float vx, float vy) =>
        Emit(new Vector2(DinoX - 0.25f + Rand(-0.1f, 0.1f), dinoY + 0.05f), new Vector2(vx, vy), Art.Hex(0xd9b98a), Rand(0.25f, 0.4f), 4f);

    void Emit(Vector2 pos, Vector2 vel, Color color, float life, float gravity, Sprite sprite = null, int order = 15, float grow = 0)
    {
        foreach (var p in particles)
        {
            if (p.sr.enabled) continue;
            p.pos = pos;
            p.vel = vel;
            p.color = color;
            p.life = p.maxLife = life;
            p.gravity = gravity;
            p.grow = grow;
            p.sr.sprite = sprite != null ? sprite : Art.Dot;
            p.sr.sortingOrder = order;
            p.sr.enabled = true;
            p.sr.transform.localScale = Vector3.one * (sprite == null && rng.NextDouble() < 0.4 ? 2 : 1);
            return;
        }
    }

    void UpdateParticles(float dt)
    {
        foreach (var p in particles)
        {
            if (!p.sr.enabled) continue;
            p.life -= dt;
            if (p.life <= 0)
            {
                p.sr.enabled = false;
                continue;
            }
            p.vel.y -= p.gravity * dt;
            p.pos += p.vel * dt;
            float age = 1f - p.life / p.maxLife;
            var c = p.color;
            if (p.sr.sortingOrder >= 0) c *= palette.front;
            c.a = p.color.a * Mathf.Clamp01(p.life / p.maxLife * 1.5f);
            p.sr.color = c;
            if (p.grow > 0) p.sr.transform.localScale = Vector3.one * (1 + Mathf.Floor(age * p.grow));
            p.sr.transform.position = new Vector3(Snap(p.pos.x), Snap(p.pos.y), 0);
        }
    }

    void ShowPopup(string text, float x, float y, Color32 color)
    {
        var t = PixelText.Create("Popup", root, 1, PixelText.Align.Center, color, 30);
        t.Set(text);
        popups.Add(new Popup { text = t, x = x, y = y, life = 0.7f });
    }

    void UpdatePopups(float dt)
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var p = popups[i];
            p.life -= dt;
            p.y += dt * 1.4f;
            if (state == State.Playing) p.x -= speed * dt * 0.3f;
            p.text.transform.position = new Vector3(Snap(p.x), Snap(p.y), 0);
            p.text.Renderer.enabled = p.life > 0.2f || (int)(p.life * 20) % 2 == 0;
            if (p.life > 0) continue;
            Destroy(p.text.gameObject);
            popups.RemoveAt(i);
        }
    }

    // ------------------------------------------------------------------ scenery

    void UpdateScenery(float dt, float move)
    {
        var lvl = Levels.All[SceneryLevel];
        scrollMid += move * 0.35f * dt;
        scrollFar += move * 0.12f * dt;
        mid.transform.position = new Vector3(Snap(-(scrollMid % (Art.MidTile * PX))) - 0.5f, -0.25f, 0);
        far.transform.position = new Vector3(Snap(-(scrollFar % (Art.FarTile * PX))) - 0.5f, 0, 0);

        foreach (var c in clouds)
        {
            var p = c.transform.position;
            p.x -= (move * 0.06f + 0.25f) * dt;
            if (p.x < -3f) PlaceCloud(c, unitsW + Rand(2f, 6f));
            else c.transform.position = new Vector3(p.x, p.y, 0);
        }

        if (state != State.Dead)
        {
            meteorTimer -= dt;
            if (meteorTimer <= 0)
            {
                meteorTimer = lvl.meteorEvery * Rand(0.6f, 1.4f);
                var m = MakeSprite("Meteor", Art.MeteorStreak, -75);
                m.transform.position = new Vector3(unitsW * Rand(0.4f, 1.1f), viewTop + 0.5f, 0);
                meteors.Add((m, new Vector2(-7f, -3.5f)));
            }
        }
        for (int i = meteors.Count - 1; i >= 0; i--)
        {
            var (m, vel) = meteors[i];
            var p = (Vector2)m.transform.position + vel * dt;
            if (p.y < 1f)
            {
                Destroy(m.gameObject);
                meteors.RemoveAt(i);
                continue;
            }
            m.transform.position = new Vector3(Snap(p.x), Snap(p.y), 0);
        }

        // Smoke from the volcanoes in the far range.
        if (lvl.smoke > 0 && state != State.Won)
        {
            smokeTimer -= dt * lvl.smoke;
            if (smokeTimer <= 0)
            {
                smokeTimer = 1f;
                float tile = Art.FarTile * PX;
                for (float x = far.transform.position.x + 210 * PX; x < unitsW + 2f; x += tile)
                    if (x > -2f)
                        Emit(new Vector2(x + Rand(-0.2f, 0.2f), 79 * PX), new Vector2(0.3f - move * 0.12f, 0.6f),
                            new Color(0.75f, 0.72f, 0.75f, 0.85f) * palette.far, 3.5f, -0.05f, Art.Puff, -69, 3f);
            }
        }

        // Falling ash or rising embers.
        if (lvl.weather != Weather.None && state != State.Won)
        {
            weatherTimer -= dt * (lvl.weather == Weather.Ash ? 25f : 30f);
            while (weatherTimer <= 0)
            {
                weatherTimer += 1f;
                if (lvl.weather == Weather.Ash)
                    Emit(new Vector2(Rand(0, unitsW + 4f), viewTop + 0.2f), new Vector2(-1.5f - move * 0.1f, Rand(-1.4f, -0.7f)),
                        new Color(0.75f, 0.72f, 0.75f, 0.8f), 7f, 0f, null, 16);
                else
                    Emit(new Vector2(Rand(0, unitsW + 4f), Rand(-0.5f, 2.5f)), new Vector2(-1f - move * 0.2f, Rand(1.5f, 3f)),
                        rng.NextDouble() < 0.5 ? Art.Hex(0xff8a2a) : Art.Hex(0xffd060), 1.6f, 0f, null, 16);
            }
        }
    }

    void ApplyPalette(float dt)
    {
        palette = Sky.Lerp(palette, targetPalette, 1f - Mathf.Exp(-dt * 2.5f));

        for (int i = 0; i < 16; i++) skyPx[i] = Color.Lerp(palette.bottom, palette.top, i / 15f);
        skyTex.SetPixels32(skyPx);
        skyTex.Apply(false);

        stars.color = new Color(1, 1, 1, palette.stars);
        sun.transform.position = new Vector3(Snap(unitsW * 0.75f), Snap(Mathf.Lerp(0.3f, viewTop - 2f, palette.sunY)), 0);
        moon.transform.position = new Vector3(Snap(unitsW * 0.3f), Snap(Mathf.Lerp(0.3f, viewTop - 2f, palette.moonY)), 0);

        // The extinction meteor grows as the last level goes on.
        float grow = state == State.Playing && level == Levels.All.Length - 1 ? LevelProgress : 0;
        doom.enabled = palette.doom > 0.02f && state != State.Won;
        doom.color = new Color(1, 1, 1, palette.doom);
        doom.transform.localScale = Vector3.one * (1 + Mathf.Floor(grow * 2.99f));
        doom.transform.position = new Vector3(Snap(unitsW * 0.7f), Snap(viewTop - 2.5f - grow * 1.5f), 0);

        far.color = palette.far;
        mid.color = palette.mid;
        foreach (var c in clouds) c.color = Color.Lerp(palette.far, Color.white, 0.3f);
        foreach (var c in chunks) c.sr.color = palette.front;
        foreach (var t in things)
            t.sr.color = t.kind == Kind.Amber || t.kind == Kind.Fireball ? Color.Lerp(palette.front, Color.white, 0.6f) : palette.front;

        bool gold = stampede > 0 && (int)(clock * 12f) % 2 == 0;
        dino.color = gold ? new Color(1f, 0.85f, 0.3f) : palette.front;
    }

    void UpdateCamera(float dt)
    {
        shake -= dt;
        flashAlpha = Mathf.Max(0, flashAlpha - dt * 3f);
        flash.color = new Color(1, 1, 1, flashAlpha);
        var offset = Vector3.zero;
        if (shake > 0) offset = new Vector3(rng.Next(-2, 3), rng.Next(-2, 3), 0) * PX;
        cam.transform.position = camBase + offset;
    }
}
