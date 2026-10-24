# Mech

A standalone BattleTech mercenary company management game built with **Godot and C#**, targeting Windows 11 x64, Linux x64, and macOS on Apple Silicon and Intel.

The game opens at a main menu with New Company, Load Game, and Quit. A new company begins in a painted DropShip captain’s office on January 1, 3025, with 2.5 million C-bills. The wall calendar advances time, the safe manages quarterly bills, and the filing cabinet saves and loads the company. The wall chart opens the official 3025 Inner Sphere map. Other campaign simulation and combat follow later.

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

Both images are packaged locally. Running the game requires no network connection or separate .NET installation. The map source link optionally opens the original PDF in the system browser.

## Controls

- Office: click the wall chart or focus it with Tab and press Enter/Space. Hover or keyboard focus outlines the painted frame. Its miniature always shows the complete reference chart, independently of the opened map’s zoom and pan.
- Calendar: click for one day. Click its “Reminder...” post-it to tick through days until the next event, at 100 ms per day. Any click stops time and activates the clicked object; calendar/post-it clicks only stop it. The painted display shows the current date and “bills due in XX days” (or “day” when one remains), and stays synchronized with the header on both screens. Hover the calendar for its date and countdown in small windows.
- Safe: open quarterly finances. A 100,000 C-bill charge falls due on April 1, July 1, October 1, and January 1. The safe’s digital display shows live cash, such as “₡ 2,500,000”. Time blocks and that readout blinks until you pay in full or confirm Pay Later. Hover the safe for the current balance in small windows. Deferral preserves cash, carries the full bill, and keeps the countdown red. At the following quarter, insufficient funds for both bills cause bankruptcy and return you to the main menu after its dialog.
- Filing cabinet: manually save or load the single campaign slot. Confirm before overwriting or discarding current progress. There is no autosave; use Load Game at the menu or cabinet to resume. Saves are stored in Godot’s per-user application-data directory as `campaign.json`, outside the installation folder.
- Map: drag to pan; scroll or use the + / − buttons to zoom. With the map focused, + / − zoom, arrow keys pan, and Home fits the complete chart.
- Return with “Back to office” or Escape. Map position survives navigation, and keyboard focus returns to the wall chart.
- 100% zoom fits the whole chart; maximum zoom is 1200%. Resizing preserves the image aspect ratio, hotspot alignment, and map location.
- The default window is 1440 × 900, with a 900 × 500 minimum.

## Build and test

```sh
./scripts/mech build
./scripts/mech test
./scripts/mech capture
```

`test` runs a C# headless Godot test scene with a nonzero exit code on failure. It checks campaign dates and event ordering, quarterly payments and deferral, bankruptcy, save validation and disk round trips, plus navigation, actual mouse/keyboard input, modal focus, zoom, pan, resizing, and failed image loading. Tests use isolated temporary saves. `capture` runs the same checks with a graphical display and saves office, map, menu, finance, and payment screenshots to `TestResults/`.

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

The Compatibility renderer is used for these 2D screens. Windows executable resource modification is disabled so development exports can be produced from Linux/macOS without Windows resource-editing tools. Public-release signing and macOS notarization are outside this first slice.

### Platform verification

| Target | Export/package inspection | Native launch and input |
| --- | --- | --- |
| Linux x64 | Verified | Verified |
| Windows 11 x64 | Verified x64 executable, resource pack, bundled runtime, and ZIP integrity | Pending Windows environment |
| macOS Apple Silicon and Intel | Verified Universal 2 bundle and both runtimes | Pending Mac hardware |

The native acceptance loop is: launch without a separately installed .NET runtime → open the wall map → pan/zoom with mouse and keyboard → resize the window → return to the office and reopen the map. Producing and inspecting a cross-platform export on Linux does not establish native runtime verification.

## Structure and design

`Scenes/` contains the command shell, office, and map scenes. `Scripts/` contains their C# behavior, native menus/dialogs, shared Control styling, the map geometry model, and a scene-independent campaign/date/event model with atomic manual saves. `Assets/` holds the reused artwork and its import settings. `Tests/` contains the native acceptance scene and campaign checks. There is no browser application or Node.js dependency.

- [Confirmed design](docs/design.md)
- [Domain glossary](CONTEXT.md)
- [Strategy and combat boundary](docs/adr/0001-strategy-and-combat-boundary.md)
- [Artwork sources, image-edit prompts, and measured display regions](docs/artwork.md)

Godot Engine is distributed under the [MIT license](https://godotengine.org/license/). The third-party BattleTech map retains its original rights and attribution.
