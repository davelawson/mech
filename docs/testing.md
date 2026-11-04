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

## Unit tests

The engine-free check suites also run as xUnit tests in `Tests.Unit/`, without starting Godot:

```sh
scripts/mech unit                   # same as dotnet test --project Tests.Unit
```

Each test runs one suite from `Tests/` (`CatalogChecks`, `CampaignChecks`, `MissionChecks`, `RosterChecks`, `TravelChecks`, `HangarChecks`, `CombatChecks`, `TerrainChecks`) and reports every failed check, not only the first. The Godot groups still call the same suites. A run takes under a minute, mostly the terrain engagements. GitHub Actions (`.github/workflows/unit-tests.yml`) runs it on every push and pull request.

These suites must stay engine-free: they may use Godot math structs (`Vector2`, `Vector3`, `Mathf`) but nothing that calls into the engine, such as `GD.Print`, nodes, resources, or static members of node classes that build meshes or materials. Write diagnostic output with `Console.WriteLine`. An engine call crashes the unit run with exit code 139 rather than failing a check. Hardpoint data therefore lives in `PreparedMechModels`, not on `MechModel`.

Run `scripts/mech unit` alongside the Godot groups when changing a file covered by `catalog`, `finance`, `missions`, `roster`, `map` (`Travel`), `hangar` (`Equipment`) or `combat-sim`, or a suite listed above.

## Groups

| Group | Covers | Run it when changing |
|---|---|---|
| `menu` | Main menu start, back navigation (right-click/Escape), nested navigation routes, Resume, unsaved-change prompts | `MainMenuScreen`, `CommandNavigator`, `CampaignDialog`, `CampaignStore` |
| `draft` | Initial draft, legacy-save draft | `DraftScreen` |
| `office` | Office artwork, wall chart, missing artwork | `OfficeScreen`, `ArtworkHotspot` |
| `map` | Map transform, star map zoom/pan/mouse, DropShip locator, travel UI and model | `MapScreen`, `MapViewport`, `MapTransform`, `StarChart`, `Travel` |
| `roster` | Roster model, rolodex, injured and memorial records | `RolodexPanel`, `RosterUi`, `Roster` |
| `hangar` | Hangar model, assignments and swaps, hangar board and location panels, loadout removal and installation (drag and drop), weapon mount slots, over-tonnage, Apply/Reset and pending changes, Inventory/Market tabs and pending purchases | `HangarScreen`, `HangarLocationPanel`, `HangarStorageLine`, `HangarDragItem`, `HangarConditionBar`, `MechPreview`, `Equipment` |
| `missions` | Mission board model and phone overlay | `MissionsPanel`, `Missions` |
| `finance` | Campaign model, calendar, quarterly payments, save/load, bankruptcy | `Campaign` (time and money) |
| `catalog` | Mech and weapon definitions | `CatalogLoader`, `GameDefinitions`, `WeaponDefinitions`, `DefinitionText`, `Data/` |
| `combat-sim` | Combat simulation (weapons, heat and climate, twisting, sensors, following, jumping, log) and terrain, biomes and buildings | `Combat`, `Heat`, `Biome`, `Jumping`, `TorsoTwisting`, `BattlefieldTerrain`, `CircleUnion`, `CombatLog`, `MechLocomotion` |
| `combat-ui` | Combat layouts, picking, detection overlay, shot effects, tactical screen, jump targeting | `CombatScreen`, `CombatViewport`, `CombatPaperdoll`, `CombatLogPanel`, `BattlefieldTerrainView` |
| `combat-layout` | Combat layout subset of `combat-ui` (freeze regression); not in the full run | as `combat-ui`, for a quick check |
| `mech-visuals` | Imported models, gaits, jump poses and jets, damage hiding, blueprint framing, weapon hardpoints | `MechModel`, `JumpJets`, `PreparedMechModels`, `PreparedModelLoader`, `WeaponMounts`, `Assets/` models and weapon libraries, `Data/mech-models.json` |
| `layout` | Window scaling and office hotspots at several window sizes | `Ui`, `NativeViewportContainer` |

A test file under `Tests/` belongs to the group that calls it. When you change a test, run that group.

## Full suite

Run the full suite (`scripts/mech test` with no groups) when you change shared code:

- `CommandApp.cs`, `Ui.cs`, `Campaign.cs` / `CampaignStore.cs` save formats
- `WeaponDefinitions.cs`: catalog validation, hangar, combat and mounts all read it
- `project.godot`, `Mech.csproj`, `global.json`, `Scenes/`, `Tests.Unit/` (also run `scripts/mech unit`)
- the harness in `Tests/SmokeTests.cs` (the group registry, `StartApp`, input helpers)
- any script not listed in the table above

Also run it when the user asks.

## Rules

- **Fixed company.** Every test app drafts from seed 3025, so a drafted group sees the same company on every run and any failure reproduces. The failure message names the seed. Pass `--draft-seed=N` after `--` to try another company, for example `-- --groups=hangar --draft-seed=53`. A test that needs a particular kind of mech should check that one exists rather than index blindly; the game itself still drafts at random.
- **Isolation.** Each group passes alone. A UI group gets a fresh `CommandApp` with its own temporary save: at the main menu (`Setup.Menu`) or in the office after a default draft (`Setup.Drafted`). A group must never rely on state left behind by another group. Set up what it needs inside the group.
- **New tests** go in the group for the screen they exercise. Add the call to that group in `SmokeTests.Groups()`.
- **New screens** get a new group, a row in this table, and an entry in `Groups()`.
- **Exit checks** (`--discard-quit`, `--clean-window-quit`, `--menu-quit`) end the process, so they run alone after the `menu` group: `-- --groups=menu --menu-quit`, under an external `timeout 90s` in case the exit fails.
