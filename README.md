# Mech

A standalone BattleTech mercenary company management game built with **Godot and C#**, targeting Windows 11 x64, Linux x64, and macOS on Apple Silicon and Intel.

You draft a company of four BattleMechs and four MechWarriors, then run it from a painted DropShip office: advance the calendar, pay quarterly bills, travel the 3025 Inner Sphere map, accept missions, manage pilots in the hangar, and fight standalone real-time tactical simulations. Game rules live in [`docs/`](#structure-and-design), not in this README.

## Development prerequisites

- [Godot **4.7.2 .NET**](https://godotengine.org/download/) and its matching **.NET export templates**. The standard Godot edition does not run C# projects. Install export templates through Editor → Manage Export Templates.
- [.NET SDK **10.0.402**](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or a newer patch in its feature band, as specified by `global.json`. The game targets .NET 10.
- Bash for the convenience commands below (available on Linux and macOS), and the `zip` command when packaging the Windows build. A Windows-specific development launcher is deferred; the exported Windows game needs neither tool.

Put `dotnet` on PATH and set `GODOT_BIN` to your .NET Godot executable if it is not already discoverable:

```sh
# Linux example; use the path where you extracted the .NET editor.
export GODOT_BIN="$HOME/tools/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64"
# macOS example:
# export GODOT_BIN="/Applications/Godot_mono.app/Contents/MacOS/Godot"

./scripts/mech editor
./scripts/mech run
```

The script verifies the Godot version, builds C#, and imports assets before running. It also discovers the tools installed for this rewrite under `~/.local/share/mech-tools/`, without changing your existing Godot or shell configuration. Personal editor setup is left to you.

All artwork and the stock mech catalog are packaged locally. Running the game requires no network connection or separate .NET installation. The map source link optionally opens the original PDF in the system browser.

## Controls

Behavior behind each input is specified in [docs/design.md](docs/design.md) (strategy screens) and [docs/combat-design.md](docs/combat-design.md) with [docs/combat-tuning.md](docs/combat-tuning.md) (tactical combat).

| Input | Action |
| --- | --- |
| **Global** | |
| Right-click | Close the topmost popup, confirmation or subscreen (outside combat) |
| Escape | Return toward the office; in the office, open the main menu |
| Header **Main menu** | Open the main menu |
| **Initial draft** | |
| Click candidate | Select it for that pool |
| **…** | Inspect the full record without selecting |
| **Establish Company** | Commit the company name and all eight choices |
| **Office** | |
| Click wall chart, or Tab then Enter/Space | Open the star map |
| Click calendar | Advance one day |
| Click “Reminder...” post-it | Advance to the next event |
| Click anywhere while time advances | Stop time (and activate the clicked object) |
| Click rolodex / porthole / safe / phone / filing cabinet | Personnel / hangar / finances / missions / save and load |
| Right-click empty office | Stop time and open the main menu |
| **Star map** | |
| Click a named system, or destination search | Preview a route |
| **Embark** / **Cancel Journey** | Commit / cancel the journey |
| Drag, or arrow keys | Pan |
| Scroll, + / − | Zoom |
| Home | Fit the complete chart |
| **Find DropShip**, or the gold arrow | Center the DropShip |
| **Hangar** | |
| Click a mech along the bottom | Select it |
| Idle / Walk / Run | Preview gaits (articulated chassis only; see [mech models](docs/mech-models.md)) |
| **Simulate Deployment** | Choose Randomized enemy or Mirror match, then 1–4 mechs and a terrain seed |
| **Combat** | |
| Click / drag | Select friendly mechs |
| Shift+click | Add to / remove from selection |
| 1–4 / Shift+1–4 | Select / toggle a deployment slot |
| Right-click ground / enemy / friendly | Move / Attack / Follow |
| Space | Pause or resume |
| WASD / arrows, middle-drag | Pan |
| Wheel | Zoom (scrolls the battle log while over it) |
| Right-drag, Q/E, Shift+middle-drag | Rotate |
| Home | Arena overview |
| Backtick (`` ` ``) | Reset camera and frame the selection |
| Shift+backtick (~) | Toggle the battle log |
| Hover a detected enemy | Show range lines from selected friendlies |
| Escape | Combat menu (End Simulation) |

## Build and test

```sh
./scripts/mech build
./scripts/mech test
./scripts/mech capture
```

`test` runs a C# headless Godot test scene with a nonzero exit code on failure. It checks all ten tactical acceptance scenarios, campaign isolation, deployment eligibility, simultaneous fire, location damage, shared detection, pursuit, targeting, AI, and actual combat controls, alongside star routing, travel, quarterly finances, saves, missions, drafting, assignments, health, navigation and resizing. Tests use isolated temporary saves. `capture` runs the same checks with a graphical display and saves deployment, combat, damage reports, draft, rolodex, hangar, office, mission, map, menu, finance, and payment screenshots to `TestResults/`.

After building, the menu and unsaved-change checks can also run independently:

```sh
timeout 90s "$GODOT_BIN" --headless --path . --scene res://Tests/SmokeTests.tscn -- --menu-only
```

This checks explicit navigation routes, nested popup and modal back actions, draft abandonment, screen restoration and focus recovery, dirty and clean replacement flows, canceled quit, save/load failures, and clean menu Quit. Add `--clean-window-quit` to check clean window close instead, or `--discard-quit` to check confirmed Quit Without Saving. These exit checks use separate processes because success terminates the game.

The focused callback-queue regression can run without desktop mouse focus. After building, run the following with the pinned .NET Godot executable; the external timeout also catches layout loops that block engine timers:

```sh
timeout 90s "$GODOT_BIN" --headless --path . --scene res://Tests/SmokeTests.tscn -- --combat-layout-only
# Render the same cases and save contact-preview/inspection captures:
timeout 90s "$GODOT_BIN" --path . --scene res://Tests/SmokeTests.tscn -- --combat-layout-only --screenshots
```

These layout checks also run in the full suite. The isolated scrollbar reproduction sweeps panel heights around the former freeze threshold; live contact checks cover detection before firing, information-band changes and resizing.

The equivalent direct commands, once tools are on PATH, are:

```sh
dotnet build Mech.csproj
godot --headless --path . --editor --import
godot --headless --path . --scene res://Tests/SmokeTests.tscn
godot --path .
```

`godot` above must be the pinned .NET editor. The test scene is excluded from release exports; its C# test runner is also excluded from release compilation.

## Desktop exports

```sh
./scripts/mech export-linux
./scripts/mech export-macos
./scripts/mech export-windows
# Or build all three:
./scripts/mech exports
```

- **Windows 11 (Intel/AMD x64):** `Builds/Mech-windows-x64.zip`. Extract the entire archive to a folder, then launch `Mech.exe`. Keep `Mech.pck` and the `data_Mech_windows_x86_64` runtime directory beside the executable. The runtime is bundled, so players do not install .NET separately. This is an unsigned development build without an installer.
- **Linux:** `Builds/Mech-linux-x64.tar.gz`. Extract the entire archive, then run `./Mech.x86_64`. Keep its `data_Mech_linuxbsd_x86_64` directory beside the executable; it contains the bundled .NET runtime and game assembly.
- **macOS 14 or newer:** `Builds/macOS/Mech.zip`, containing a Universal 2 `.app` with both Apple Silicon and Intel runtime payloads. Extract and open `Mech.app`. The development preset uses ad-hoc signing, without Apple notarization. macOS may require explicit approval in System Settings → Privacy & Security for downloaded development builds.

The Compatibility renderer supports the 2D command screens and the procedural 3D tactical arena. Windows executable resource modification is disabled so development exports can be produced from Linux/macOS without Windows resource-editing tools. Public-release signing and macOS notarization are outside this first slice.

### Platform verification

| Target | Export/package inspection | Native launch and input |
| --- | --- | --- |
| Linux x64 | Verified | Verified |
| Windows 11 x64 | Verified x64 executable, resource pack, bundled runtime, and ZIP integrity | Pending Windows environment |
| macOS Apple Silicon and Intel | Verified Universal 2 bundle and both runtimes | Pending Mac hardware |

The native acceptance loop is: launch without a separately installed .NET runtime → open the wall map → pan/zoom with mouse and keyboard → resize the window → return to the office and reopen the map. Producing and inspecting a cross-platform export on Linux does not establish native runtime verification.

## Structure and design

`Scenes/` contains the command shell, office, and map scenes. `Scripts/` contains their C# behavior, native menus/dialogs, shared Control styling, map geometry, the scene-independent campaign model with atomic manual saves, and the isolated combat model plus procedural battlefield/UI. `Assets/` holds only active runtime artwork, prepared models, their required textures and import settings. Model preparation reads external MW5 source exports directly; see [mech models](docs/mech-models.md). `Tests/` contains the native acceptance scene and campaign/combat checks. There is no browser application or Node.js dependency.

- [Confirmed design](docs/design.md)
- [Tactical combat design](docs/combat-design.md) and [implementation defaults](docs/combat-tuning.md)
- [Mech models and condition display](docs/mech-models.md)
- [Domain glossary](CONTEXT.md)
- [Strategy and combat boundary](docs/adr/0001-strategy-and-combat-boundary.md) and [continuous combat timing](docs/adr/0002-continuous-combat-timing.md)
- [Artwork sources, image-edit prompts, and measured display regions](docs/artwork.md)
- [Classic stock unit data, source revision and attribution](Data/README.md)

Godot Engine is distributed under the [MIT license](https://godotengine.org/license/). The third-party BattleTech map retains its original rights and attribution.
