# Testing

The headless test scene (`Tests/SmokeTests.tscn`) is split into **test groups**, mostly one per screen. Run only the groups covering the code you changed:

```sh
scripts/mech test hangar            # one group
scripts/mech test roster hangar     # several groups
scripts/mech test --list            # group names
scripts/mech test                   # full suite
scripts/mech capture combat-ui      # rendered run with screenshots in TestResults/
```

The direct equivalent is `godot --headless --path . --scene res://Tests/SmokeTests.tscn -- --groups=roster,hangar`. Each group prints its check count. An unknown group name fails and lists the valid ones.

## Groups

| Group | Covers | Run it when changing |
|---|---|---|
| `menu` | Main menu start, back navigation (right-click/Escape), nested navigation routes, Resume, unsaved-change prompts | `MainMenuScreen`, `CommandNavigator`, `CampaignDialog`, `CampaignStore` |
| `draft` | Initial draft, legacy-save draft | `DraftScreen` |
| `office` | Office artwork, wall chart, missing artwork | `OfficeScreen`, `ArtworkHotspot` |
| `map` | Map transform, star map zoom/pan/mouse, DropShip locator, travel UI and model | `MapScreen`, `MapViewport`, `MapTransform`, `StarChart`, `Travel` |
| `roster` | Roster model, rolodex, injured and memorial records | `RolodexPanel`, `RosterUi`, `Roster` |
| `hangar` | Hangar model, assignments and swaps, hangar board and location panels, loadout removal and installation (drag and drop), weapon mount slots, over-tonnage, Apply/Reset and pending changes | `HangarScreen`, `HangarLocationPanel`, `HangarStorageLine`, `MechPreview`, `Equipment` |
| `missions` | Mission board model and phone overlay | `MissionsPanel`, `Missions` |
| `finance` | Campaign model, calendar, quarterly payments, save/load, bankruptcy | `Campaign` (time and money) |
| `catalog` | Mech and weapon definitions | `CatalogLoader`, `GameDefinitions`, `DefinitionText`, `Data/` |
| `combat-sim` | Combat simulation (weapons, heat, twisting, sensors, following, log) and terrain | `Combat`, `Heat`, `TorsoTwisting`, `BattlefieldTerrain`, `CircleUnion`, `CombatLog`, `MechLocomotion` |
| `combat-ui` | Combat layouts, picking, detection overlay, shot effects, tactical screen | `CombatScreen`, `CombatViewport`, `CombatPaperdoll`, `CombatLogPanel`, `BattlefieldTerrainView` |
| `combat-layout` | Combat layout subset of `combat-ui` (freeze regression); not in the full run | as `combat-ui`, for a quick check |
| `mech-visuals` | Imported models, gaits, damage hiding, blueprint framing, weapon hardpoints | `MechModel`, `PreparedMechModels`, `PreparedModelLoader`, `WeaponMounts`, `Assets/` models and weapon libraries, `Data/mech-models.json` |
| `layout` | Window scaling and office hotspots at several window sizes | `Ui`, `NativeViewportContainer` |

A test file under `Tests/` belongs to the group that calls it. When you change a test, run that group.

## Full suite

Run the full suite (`scripts/mech test` with no groups) when you change shared code:

- `CommandApp.cs`, `Ui.cs`, `Campaign.cs` / `CampaignStore.cs` save formats
- `project.godot`, `Mech.csproj`, `Scenes/`
- the harness in `Tests/SmokeTests.cs` (the group registry, `StartApp`, input helpers)
- any script not listed in the table above

Also run it when the user asks.

## Rules

- **Isolation.** Each group passes alone. A UI group gets a fresh `CommandApp` with its own temporary save: at the main menu (`Setup.Menu`) or in the office after a default draft (`Setup.Drafted`). A group must never rely on state left behind by another group. Set up what it needs inside the group.
- **New tests** go in the group for the screen they exercise. Add the call to that group in `SmokeTests.Groups()`.
- **New screens** get a new group, a row in this table, and an entry in `Groups()`.
- **Exit checks** (`--discard-quit`, `--clean-window-quit`, `--menu-quit`) end the process, so they run alone after the `menu` group: `-- --groups=menu --menu-quit`, under an external `timeout 90s` in case the exit fails.
