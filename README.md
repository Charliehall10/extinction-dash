# Extinction Dash

A one-button dino runner made in Unity 6 (6000.6.5f1). Run through five levels, each faster and meaner than the last, and outrun the meteor that wiped out the dinosaurs.

**Play it in your browser:** https://charliehall10.github.io/extinction-dash/play/ (keyboard, mouse or touch) · **Case study:** https://charliehall10.github.io/extinction-dash/

## Levels

| # | Level | Speed | New hazards |
| --- | --- | --- | --- |
| 1 | Fern Valley | 8 → 10.5 | Rocks, ferns, logs, rolling hills, low ledges |
| 2 | Tar Pits | 10.5 → 13 | Tar pits, pterodactyls, two-hazard combos |
| 3 | Raptor Ridge | 13 → 15.5 | Two-unit cliffs (full jump only), wider gaps, stairs |
| 4 | Volcano | 15.5 → 18 | Lava pits, flaming boulders that drop from the sky after a warning marker |
| 5 | Extinction | 18 → 21 | Everything, tighter combos, under a giant meteor that grows as you near the end |

Levels run straight into each other past a finish flag. Dying restarts the current level with the score you had when you reached it. Unlocked levels can be picked on the title screen.

## Amber Rush

- Every 5 amber in a row raises the multiplier (up to ×5). The multiplier scales **both points and speed**.
- Letting an amber go past drops you a tier, so going for risky amber is a gamble.
- 16 amber fill the rush bar and set off a **STAMPEDE**: 5 seconds of +35% speed, smashing obstacles for bonus points, running over pits, climbing ledges and pulling in nearby amber.

## Controls

| Action | Keyboard | Touch / mouse |
| --- | --- | --- |
| Jump (hold for higher) | Space, Up, W | Tap / click and hold |
| Drop faster | Down, S | – |
| Pick level (title) | Left/Right, A/D | Tap the left or right edge |
| Pause | P | – |
| Mute | M | – |

## How it's built

Everything is created in code at runtime, so the scene is empty and there are no art or audio assets:

- `Assets/Scripts/Game.cs`: game loop, dino physics on uneven ground, hazards, amber and stampede, scenery. Renders into a 180px-tall texture that is scaled up so the pixel art stays crisp; portrait screens get a taller view with the ground lifted.
- `Assets/Scripts/Game.Track.cs`: terrain and level generation. Each segment is one feature (rock, pit, ledge, ramp, combo…) plus a flat run-up sized in seconds of travel, so spacing stays fair as the speed climbs.
- `Assets/Scripts/Game.Hud.cs`: HUD, input, autoplay testing and screenshots.
- `Assets/Scripts/Levels.cs`: the five levels (speeds, spacing, hazard weights, sky and ground colours).
- `Assets/Scripts/Terrain.cs`: paints each stretch of ground (grass, dirt, rock layers, fossils, tar/lava/chasm pits) into its own texture.
- `Assets/Scripts/Art.cs`: sprites, as pixel-art strings or small generators.
- `Assets/Scripts/PixelText.cs`: 3×5 bitmap font.
- `Assets/Scripts/SfxSynth.cs`, `Sfx.cs`: square-wave and noise sound effects generated at startup.
- `Assets/Editor/Build.cs`: project settings and builds.

## Building

```
UNITY=~/Unity/Hub/Editor/6000.6.5f1/Editor/Unity
$UNITY -batchmode -quit -projectPath . -buildTarget WebGL   -executeMethod Build.WebGL   # -> Builds/WebGL
$UNITY -batchmode -quit -projectPath . -buildTarget Linux64 -executeMethod Build.Linux   # -> Builds/Linux
```

The same builds are under the **Extinction Dash** menu in the editor. To try the WebGL build locally:

```
cd Builds/WebGL && python3 -m http.server 8000
```

## Publishing

`bash site/publish.sh` publishes to GitHub Pages: the case study (`site/index.html` and `site/media`) at the root and the WebGL build at `/play/`. Build the WebGL version first. `--preview` assembles the site into `site/dist` without publishing.

## Testing

An autoplay mode plays the game by itself for testing: add `?autoplay` to the WebGL URL, or pass `-autoplay` to the Linux build. For headless screenshots:

```
Builds/Linux/ExtinctionDash.x86_64 -batchmode -shots /tmp/shots -screen 1600x900 [-level 3] [-shotevery 10] [-shotmax 100] [-botquit 20]
```

This saves PNGs at a fixed interval plus one at each level-up and death, logs every level-up and death (with its cause), and quits at `-shotmax`. `-level` starts straight into a level and `-botquit` makes autoplay stop jumping after that many seconds. Test runs never read or write saved progress.

## Assets

There are no third-party assets: every sprite, the font and every sound effect is drawn or synthesised in code. Built with Unity Personal.

© 2026 Charlie Hall. All rights reserved.
