# ER2 FOV Unlock

An unofficial BepInEx 6 IL2CPP mod for **Easy Red 2** that gives you independent first- and
third-person field of view, configurable aim / hold-breath zoom, and smooth zoom transitions —
while leaving the game's own scopes and vehicle optics completely untouched.

The mod never writes to the game's saved FOV setting. Turn it off or delete the DLL and the game
returns to its native behaviour.

---

## Features

- **Independent FOV per view mode** — separate first-person and third-person values,
  adjustable from 65 to 130 degrees with sliders or numpad hotkeys.
- **Configurable zoom** — aim zoom (0.70–1.00) and hold-breath zoom (0.45–0.70),
  both shown to two decimals and applied on top of the selected FOV.
- **Smooth transitions** — zoom changes are interpolated instead of snapping. An optional
  *Breath-holding Animation Improvement* for on-foot infantry uses a slower exponential
  ease-in: barely any zoom at first, then a quick snap to full magnification.
- **Vanilla optics passthrough** — when the game enters a weapon scope, fixed artillery sight,
  tank or ground-vehicle sight, the plugin detects the low native camera FOV and fully yields
  control back to the game. No more seeing your own gun barrel and hands inside a scope.
- **Safe input blocking** — opening the menu releases the cursor and uses the game's
  own inventory gate to block firing, movement and looking. `Time.timeScale` is never frozen
  and the game is never paused, so the match keeps running.
- **Only active during a match** — the main menu and loading screens keep their native camera.
- **Four languages** — English, Simplified Chinese, Traditional Chinese and Japanese, with an
  *Auto* option that follows the game language. Runtime log messages are always English and
  silent by default (warnings and errors only).
- **Rebindable hotkeys** — every key can be reassigned or cleared from the in-game menu.

---

## Requirements

- A legal copy of **Easy Red 2** (Windows x64).
- **BepInEx v6.0.0-be.789 – Windows x64 IL2CPP**. Newer BepInEx 6 IL2CPP bleeding-edge builds
  are expected to work as well, but 6.0.0-be.789 is the version this mod was built and tested
  against. Make sure you pick the **IL2CPP / Unity IL2CPP** build — the Mono build will not work.

---

## Installation

### 1. Install BepInEx 6 IL2CPP

Download BepInEx from either of these pages:

- https://www.nexusmods.com/site/mods/115?tab=files
- https://builds.bepinex.dev/projects/bepinex_be

Choose the **Windows x64 IL2CPP** package for **v6.0.0-be.789** (or a newer IL2CPP build).

Extract the contents of the archive **into the game's root folder** — the folder that contains
`Easy Red 2.exe`. After extraction you should see `BepInEx\`, `doorstop_config.ini` and
`winhttp.dll` next to the executable.

Launch the game once and quit. On that first run BepInEx generates `BepInEx\interop\`,
`BepInEx\config\` and `BepInEx\plugins\`.

### 2. Install the mod

Copy `ER2_FovUnlock.dll` into:

```
<game root>\BepInEx\plugins\
```

### 3. Verify

Start the game, join a match and press **F8**. The settings menu should open. On first launch
BepInEx also creates `BepInEx\config\ER2_FOVUnlock.cfg` with English comments.

To uninstall, remove `ER2_FovUnlock.dll` from `BepInEx\plugins\`. No game files are modified.

---

## Using the mod

### Menu layout

| Row | Control |
|---|---|
| Top | Master enable switch (left) and language dropdown (right) |
| | First-person FOV slider (65–130) |
| | Third-person FOV slider (65–130) |
| | Aim zoom slider (0.70–1.00) |
| | Hold-breath zoom slider (0.45–0.70) |
| | *Breath-holding Animation Improvement* toggle |
| | Four hotkey rows, each with **Bind** / **Clear** buttons |
| Bottom | **Close** button |

Click **Bind** and press any key to reassign a hotkey. **Clear** sets it to *None* so that
function can only be used from the menu. The language dropdown lists Auto, English,
简体中文, 繁體中文 and 日本語; hovering an item highlights it.

The menu closes with **F8** or the **Close** button. **Esc is deliberately not used**, because
it conflicts with the game's own pause menu.

### Default controls

| Action | Default key | Notes |
|---|---|---|
| Open / close menu | `F8` | |
| Enable / disable FOV override | `F7` | Also available as the master switch in the menu |
| Increase FOV | Numpad `+` | Adjusts the FOV of the current view mode; hold to repeat |
| Decrease FOV | Numpad `-` | Same, one degree per step by default |

All four keys can be rebound or cleared in the menu.

### Aim and hold-breath behaviour

- **On foot, in ground vehicles and on mounted weapons**: hold-breath zoom only applies while
  you are aiming. Pressing the breath key alone does nothing, in first or third person.
- **Air vehicles, third person**: holding breath zooms directly without aiming.
- **Air vehicles, first person**: aiming is required, as with infantry.
- **Scopes / artillery / ground-vehicle optics**: the plugin steps aside and the game renders
  the optic exactly as it does without the mod.

---

## Configuration

Settings live in `BepInEx\config\ER2_FOVUnlock.cfg` and are documented in English in that file.
Most values can be changed directly in the menu. Editing the cfg while the game is running is
picked up within about five seconds (not while the menu is open).

Some useful advanced entries:

| Entry | Default | Purpose |
|---|---|---|
| `FovFirstPerson` / `FovThirdPerson` | 100 / 100 | Base FOV per view mode |
| `AimZoomRatio` / `HoldBreathZoomRatio` | 0.75 / 0.55 | Zoom multipliers |
| `ZoomSmoothSpeed` | 2 | Linear zoom transition speed |
| `HoldBreathSmoothSpeed` | 0.7 | Transition speed for the on-foot breath animation |
| `HoldBreathCurve` | 4 | Exponential ease-in strength; higher means more "slow then sudden" |
| `EnhancedBreathOnFoot` | true | Toggles the Breath-holding Animation Improvement |
| `VanillaViewRatio` | 0.45 | Threshold for detecting scopes / vehicle optics |
| `Language` | Auto | Menu language |
| `DebugWarnings` | false | Emits diagnostic lines at warning level |
| `UseInventoryGate` | true | Uses the game's inventory gate to block input while the menu is open |
| `BlockInputViaPause` | false | Optional fallback; may affect multiplayer, off by default |

---

## Building from source

You need:

- A **.NET 6 SDK** (`net6.0` target framework).
- The game with BepInEx installed, for `BepInEx\core\*.dll`.
- The IL2CPP interop assemblies generated by BepInEx on your machine
  (usually in `<game root>\BepInEx\interop\`).

These DLLs are **not included in this repository** — they are generated locally by BepInEx and
derived from the game, so they must not be redistributed.

1. Copy `Directory.Build.props.example` to `Directory.Build.props` in the same folder and edit
   `GameDir` (game root) and `InteropDir` (folder holding the interop DLLs). The copy is
   gitignored.
2. Build:

   ```
   dotnet build ER2_FovUnlock.csproj -c Release -p:InstallToGame=true
   ```

   On Windows you can also run `build.bat`.

The output is `bin\Release\net6.0\ER2_FovUnlock.dll`. Passing `-p:InstallToGame=true` also
copies it into `<game root>\BepInEx\plugins\`. You can override paths on the command line with
`-p:GameDir="..."` and `-p:InteropDir="..."`.

Restart the game after rebuilding — the DLL is loaded at startup.

---

## Troubleshooting

| Symptom | What to check |
|---|---|
| F8 does nothing | BepInEx did not load (check `BepInEx\LogOutput.log`), or another mod uses F8 — rebind `MenuKey` |
| FOV never changes | `Enabled` is off, or you are on the main menu / loading screen (`OnlyInMatch`) |
| Scope view looks wrong | Raise `VanillaViewRatio` slightly, or add the scope camera name to `ExcludedCameraNames` |
| Firing still happens while the menu is open | Make sure `UseInventoryGate` is true; `BlockInputViaPause` is an optional fallback |
| Build reports missing DLLs | Check `GameDir` / `InteropDir` and launch the game once so BepInEx generates its interop folder |
| `dotnet build` is not recognised | Install a .NET 6 SDK; a .NET runtime alone is not enough |

---

## Notes

- This is an unofficial mod and is not affiliated with or endorsed by the developers or
  publishers of Easy Red 2.
- This repository contains **no game files**. A legal copy of the game and a BepInEx install
  are required.
- The mod does not freeze or pause the game, but check the rules of any multiplayer server
  before using client-side mods.
- Runtime logs are English only and silent unless a warning or error occurs.

## License

MIT — see [LICENSE](LICENSE).
