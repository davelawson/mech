# Mech

A standalone BattleTech mercenary company management game built with **Godot and C#**, targeting Windows 11 x64, Linux x64, and macOS on Apple Silicon and Intel.

The game opens at a main menu with New Company, Load Game, Quit, and **Resume** when a live company is in memory. A new company enters a painted DropShip office and immediately drafts four BattleMechs and four MechWarriors from eight simultaneously visible pools. Each new draft starts with a random company name and one randomly selected candidate per pool. Review or edit these defaults, then click **Establish Company** to begin operations on January 1, 3025, with 2.5 million C-bills. Three warrior pools offer regular recruits; the fourth offers veterans.

The desk rolodex opens personnel cards over the office and displays the number of healthy warriors ready for deployment. The porthole opens an illustrated hangar with classic mech details, persistent pilot assignments, and **Simulate Deployment**: a standalone tactical battle against an undamaged mirrored force. The calendar advances time, the safe manages quarterly bills, the filing cabinet saves/loads the company, and the wall chart opens the official 3025 Inner Sphere map. Campaign mission combat and permanent battlefield consequences follow later.

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

- Initial draft: all eight pools of three remain visible, including at 900 × 500. A random name and one random choice per pool are filled in for every new draft, so you can establish immediately. Click a candidate to change a selection; use **…** to inspect the full record without selecting. Choices and the company name remain editable until **Establish Company**. Escape from a record returns to drafting; Escape from the draft asks to abandon it. Partial drafts cannot be saved or completed drafts reopened.
- Rolodex: click the physical index file on the desk. Flip individual cards or choose a name from the index. Cards show skills, health, recovery date, and assignment. The desk count includes healthy assigned and unassigned warriors, excludes injured/deceased warriors, and updates after recovery. Injured warriors retain assignments; deceased warriors are unassigned and preserved in the memorial section.
- Hangar: click the right porthole. Select a mech along the bottom to view its illustration and Overview, Armor, Weapons and Systems tabs. Choose a living warrior and assign or unassign them. Transferring an occupied assignment or swapping two pilots requires confirmation. Drafted mechs start intact, with full ammunition and no pilot assignments.
- Simulate Deployment: assign healthy MechWarriors, then select 1–4 operational mechs with the hangar button. The flat 3D arena starts paused. Click/drag selects; Shift adds; right-click ground moves and right-click a detected enemy attacks. Space pauses/resumes with battlefield focus. The selected-unit panel provides Walk/Run, Fire at will/Hold fire, preferred distance, separate priority targeting and weapon toggles. Move toward the center to find opposition. WASD/arrows or middle-drag pan, wheel zooms, and right-drag orbits the ground point at the center of the view. Right-click orders issue on release; moving more than five pixels starts rotation instead. Q/E or Shift-middle also rotates; Home restores the arena overview. Escape opens the combat menu. End Simulation and completed battles show damage/outcome reports before returning to the same hangar selection. Campaign condition, assignments, finances and time are preserved. See [tactical defaults](docs/combat-tuning.md).
- Right-click closes the topmost popup, confirmation, or subscreen, returning one level toward the office and restoring focus. Personnel opened from the hangar returns to the same selected mech and tab; its Back button and Escape do the same. Right-click in the office only stops time. Escape from the hangar, map, or office rolodex returns to the office. Escape in the office opens the main menu. **Resume** preserves unsaved company state and returns to the office; it does not write a save. New Company and Load Game ask before replacing a live company. Roster interfaces pause time.
- Office: click the wall chart or focus it with Tab and press Enter/Space. Hover or keyboard focus outlines the painted frame. Its miniature always shows the complete reference chart, independently of the opened map’s zoom and pan.
- Calendar: click for one day. Click its “Reminder...” post-it to tick through days until the next event, at 100 ms per day. Any click stops time and activates the clicked object; calendar/post-it clicks only stop it. The painted display shows the current date and “bills due in XX days” (or “day” when one remains), and stays synchronized with the header on both screens. Hover the calendar for its date and countdown in small windows.
- Safe: open quarterly finances. Transit accrues at 2,000 C-bills per whole day, with each jump taking ceil(distance / 6) days and a 30-light-year jump taking five days. Orbit is free. Accrued transit costs join the 100,000 C-bill company charge that falls due on April 1, July 1, October 1, and January 1. The safe’s digital display shows live cash, such as “₡ 2,500,000”. Time blocks and that readout blinks until you pay in full or confirm Pay Later. Hover the safe for the current balance in small windows. Deferral preserves cash, carries the full bill, and keeps the countdown red. At the following quarter, insufficient funds for both bills cause bankruptcy and return you to the main menu after its dialog.
- Phone: a silent flashing light indicates new mission offers. Opening it marks all available offers as seen. Browse briefings beside the offer list, accept one mission, or inspect the current mission while browsing others. Accepting returns to the office; abandon with confirmation before accepting another. Abandoned offers remain available and seen. Mission selection and seen status use the same manual save slot. Easy Peasy is the initial offer; its terms are provisional, with no payment or deadline effects yet. Deployment follows starmap movement.
- Filing cabinet: manually save or load the single campaign slot. Confirm before overwriting or discarding current progress. There is no autosave; use Load Game at the menu or cabinet to resume. Older finance-only saves require the initial draft while preserving their finances, calendar, and events; the saved file is changed only by a subsequent manual save. Saves are stored in Godot’s per-user application-data directory as `campaign.json`, outside the installation folder.
- Map: click a named system to preview a blue route, or find it with the destination search. Embark commits it as a green journey; departure waits until the next day. In orbit, Cancel Journey removes only the embarked journey, and selecting another candidate leaves the active journey intact. Each arrival pauses the calendar for that day; the next office time advance continues onward. Plotting and cancellation are unavailable between systems. New companies and older saves start at Galatea. Drag to pan; scroll or use the + / − buttons to zoom. With the map focused, + / − zoom, arrow keys pan, and Home fits the complete chart.
- The DropShip has a prominent gold ship symbol, halo, and permanent location label. If it is outside the viewed region, a gold direction arrow leads back to it. Click the arrow or **Find DropShip** to center its current orbit or position between systems without changing zoom or travel plans.
- Return with “Back to office”, right-click, or Escape. Map position survives navigation, and keyboard focus returns to the wall chart.
- 100% zoom fits the whole chart; maximum zoom is 1200%. Resizing preserves the image aspect ratio, hotspot alignment, and map location.
- The default window is 1440 × 900, with a 900 × 500 minimum.

## Build and test

```sh
./scripts/mech build
./scripts/mech test
./scripts/mech capture
```

`test` runs a C# headless Godot test scene with a nonzero exit code on failure. It checks all ten tactical acceptance scenarios, campaign isolation, deployment eligibility, simultaneous fire, location damage, shared detection, pursuit, targeting, AI, and actual combat controls, alongside star routing, travel, quarterly finances, saves, missions, drafting, assignments, health, navigation and resizing. Tests use isolated temporary saves. `capture` runs the same checks with a graphical display and saves deployment, combat, damage reports, draft, rolodex, hangar, office, mission, map, menu, finance, and payment screenshots to `TestResults/`.

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

`Scenes/` contains the command shell, office, and map scenes. `Scripts/` contains their C# behavior, native menus/dialogs, shared Control styling, map geometry, the scene-independent campaign model with atomic manual saves, and the isolated combat model plus procedural battlefield/UI. `Assets/` holds reused artwork and import settings. `Tests/` contains the native acceptance scene and campaign/combat checks. There is no browser application or Node.js dependency.

- [Confirmed design](docs/design.md)
- [Tactical combat design](docs/combat-design.md) and [implementation defaults](docs/combat-tuning.md)
- [Domain glossary](CONTEXT.md)
- [Strategy and combat boundary](docs/adr/0001-strategy-and-combat-boundary.md)
- [Artwork sources, image-edit prompts, and measured display regions](docs/artwork.md)
- [Classic stock unit data, source revision and attribution](Data/README.md)

Godot Engine is distributed under the [MIT license](https://godotengine.org/license/). The third-party BattleTech map retains its original rights and attribution.
