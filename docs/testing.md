# Testing

Tests are split into **test groups**, mostly one per screen. A group is a part of the headless Godot test scene (`Tests/SmokeTests.tscn`), the xUnit classes in `Tests.Unit/` that check its models, or both. Run only the groups covering the code you changed:

```sh
scripts/mech test hangar            # one group
scripts/mech test roster hangar     # several groups
scripts/mech test --list            # group names
scripts/mech test                   # full suite, about 45 s
scripts/mech capture combat-ui      # rendered run with screenshots in TestResults/
```

`scripts/mech test` builds once, then runs each named Godot group as its own headless process, at most four at a time (`MECH_TEST_JOBS=8` allows more). The slowest groups start first. The group's unit classes run alongside as one more job. Each job prints one line, with its check count and seconds. Its full output is in `TestResults/test-logs/<job>.log`, and a failure prints the first error lines and the log path. An unknown group name fails and lists the valid ones.

To run Godot groups in a single process, without their unit classes, use `godot --headless --path . --scene res://Tests/SmokeTests.tscn -- --groups=roster,hangar`. `capture` runs its groups this way, one after another, because a windowed run needs focus.

## Unit tests

The engine-free check suites run only as xUnit tests in `Tests.Unit/`, without starting Godot:

```sh
scripts/mech unit                   # same as dotnet test --project Tests.Unit
```

Each test class runs one suite from `Tests/` (`CatalogChecks`, `CampaignChecks`, `MissionChecks`, `ReputationChecks`, `RosterChecks`, `TravelChecks`, `HangarChecks`, `NetworkChecks`, `CombatChecks`, `TerrainChecks`) and reports every failed check, not only the first. xUnit runs classes in parallel. The longest suites are split into parts, each with its own class (`TerrainGeneration`, `TerrainFeatures`, `TerrainEngagements`, `TerrainLanceEngagement`, `NetworkSessions`, `NetworkReplica`), and the suite's `Run` still calls every part. Split any part that grows past about 20 s the same way. A whole run takes about 20 s, led by the replicated battle and the four-on-four terrain engagement. GitHub Actions (`.github/workflows/unit-tests.yml`) runs it on every push and pull request.

These suites must stay engine-free: they may use Godot math structs (`Vector2`, `Vector3`, `Mathf`) but nothing that calls into the engine, such as `GD.Print`, nodes, resources, or static members of node classes that build meshes or materials. Write diagnostic output with `Console.WriteLine`. An engine call crashes the unit run with exit code 139 rather than failing a check. Hardpoint data therefore lives in `PreparedMechModels`, not on `MechModel`.

`scripts/mech test <group>` runs a group's unit classes for you. `unit_classes` in `scripts/mech` maps each group to its classes: `map` → `Travel`, `roster` → `Roster`, `hangar` → `Hangar`, `missions` → `Missions`, `Reputation`, `finance` → `Campaign`, `multiplayer` → `Network*`. `catalog` → `Catalog` and `combat-sim` → `Combat`, `Terrain*` are unit classes only. When you add a suite or a class, add it there and here. Run `scripts/mech unit` for the whole unit suite.

Generating a battlefield takes about 0.1 s. It used to take 2 s, before navigation edges were evaluated once per pair and in parallel rows. Check how long a new test spends building terrain before adding many maps.

## Groups

| Group | Covers | Run it when changing |
|---|---|---|
| `menu` | Main menu start, back navigation (right-click/Escape), nested navigation routes, Resume, unsaved-change prompts, loading from the save grid and failed saves and loads | `MainMenuScreen`, `CommandNavigator`, `CampaignDialog`, `CampaignStore`, `SaveBrowser` |
| `draft` | Initial draft, legacy-save draft | `DraftScreen` |
| `office` | Office artwork, wall chart, missing artwork | `OfficeScreen`, `ArtworkHotspot` |
| `map` | Map transform, star map zoom/pan/mouse, DropShip locator, travel UI and model, system owners | `MapScreen`, `MapViewport`, `MapTransform`, `StarChart`, `Travel`, `Data/system-factions.json` |
| `roster` | Roster model, rolodex, injured and memorial records | `RolodexPanel`, `RosterUi`, `Roster` |
| `hangar` | Hangar model, assignments and swaps, lance pool moves, hangar board and location panels, loadout removal and installation (drag and drop), weapon mount slots, over-tonnage, Apply/Reset and pending changes, Inventory/Market tabs and pending purchases | `HangarScreen`, `HangarLocationPanel`, `HangarStorageLine`, `HangarDragItem`, `HangarConditionBar`, `MechPreview`, `Equipment` |
| `missions` | Mission board model, reputation, and phone overlay | `MissionsPanel`, `Missions`, `Reputation`, `Factions` |
| `finance` | Campaign model, calendar, quarterly payments, save slots (`SaveLibrary`) and the save grid (empty slot, name prompt, overwrite, paging), bankruptcy | `Campaign` (time and money), `SaveLibrary`, `SaveBrowser` |
| `catalog` | Mech and weapon definitions (unit tests only) | `CatalogLoader`, `GameDefinitions`, `WeaponDefinitions`, `DefinitionText`, `Data/` |
| `combat-sim` | Combat simulation (weapons, heat and climate, twisting, sensors, following, jumping, log) and terrain, biomes and buildings (unit tests only) | `Combat`, `Heat`, `Biome`, `Jumping`, `TorsoTwisting`, `BattlefieldTerrain`, `CircleUnion`, `CombatLog`, `MechLocomotion` |
| `combat-ui` | Combat layouts, picking, detection overlay, shot effects, tactical screen, Quick Launch and mission settlement, jump targeting | `CombatScreen`, `CombatViewport`, `CombatPaperdoll`, `CombatLogPanel`, `BattlefieldTerrainView` |
| `combat-layout` | Combat layout subset of `combat-ui` (freeze regression); not in the full run | as `combat-ui`, for a quick check |
| `mech-visuals` | Imported models, gaits, jump poses and jets, damage hiding, blueprint framing, weapon hardpoints | `MechModel`, `JumpJets`, `PreparedMechModels`, `PreparedModelLoader`, `WeaponMounts`, `Assets/` models and weapon libraries, `Data/mech-models.json` |
| `multiplayer` | Co-op protocol and sessions (engine-free), battle replicas matched field by field against a fought battle, then the app as leader and as a joined player over ENet loopback in one process: lobby, drafts, commands, sync, leader-only controls, disconnects, co-op force selection, battles, pings and results, waiting for a dropped player, Stop Waiting, Rejoin and the leader leaving, co-op save slots (resuming a chosen save, the cabinet in co-op, loading mid-game back to the lobby) | `CoopSession`, `NetProtocol`, `ENetTransport`, `LobbyScreen`, `HostAddress`, `RouterPort`, `CombatState`, co-op paths in `CommandApp` and `CombatScreen` |
| `layout` | Window scaling and office hotspots at several window sizes | `Ui`, `NativeViewportContainer` |

A test file under `Tests/` belongs to the group that calls it, or whose unit classes run it. When you change a test, run that group.

## Full suite

Run the full suite (`scripts/mech test` with no groups) when you change shared code:

- `CommandApp.cs`, `Ui.cs`, `Campaign.cs` / `CampaignStore.cs` / `SaveLibrary.cs` save formats
- `WeaponDefinitions.cs`: catalog validation, hangar, combat and mounts all read it
- `project.godot`, `Mech.csproj`, `global.json`, `Scenes/`, `Tests.Unit/`, `scripts/mech`
- the harness in `Tests/SmokeTests.cs` (the group registry, `StartApp`, input helpers)
- any script not listed in the table above

Also run it when the user asks.

## Rules

- **Fixed company.** Every test app drafts from seed 3025, so a drafted group sees the same company on every run and any failure reproduces. The failure message names the seed. Pass `--draft-seed=N` after `--` to try another company, for example `-- --groups=hangar --draft-seed=53`. A test that needs a particular kind of mech should check that one exists rather than index blindly; the game itself still drafts at random.
- **Isolation.** Each group passes alone. A UI group gets a fresh `CommandApp` with its own temporary save: at the main menu (`Setup.Menu`) or in the office after a default draft (`Setup.Drafted`). A group must never rely on state left behind by another group. Set up what it needs inside the group. Groups run at the same time in separate processes. They must not share a fixed file, port or other machine-wide resource. Only `multiplayer` opens a real ENet port.
- **New tests** go in the group for the screen they exercise. Add the call to that group in `SmokeTests.Groups()`. Engine-free checks go in a suite under `Tests/` with a class in `Tests.Unit/ModelChecks.cs`, not in a Godot group.
- **New screens** get a new group, a row in this table, and an entry in `Groups()`.
- **Exit checks** (`--discard-quit`, `--clean-window-quit`, `--menu-quit`) end the process, so they run alone after the `menu` group: `-- --groups=menu --menu-quit`, under an external `timeout 90s` in case the exit fails.
