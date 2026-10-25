# Tactical simulation defaults

The first tactical slice implements [the confirmed combat design](combat-design.md). `CombatSimulation` owns copied condition, equipment, ammunition availability and MechWarrior skills. It has no campaign reference or save operation. Rendering and input live separately in `CombatViewport` and `CombatScreen`.

## Space and time

| Value | Default |
| --- | --- |
| Distance unit | One stock range hex (nominally 30 metres) |
| Arena | 96 × 96 hexes, centered on the origin |
| Starts | Z = +32 / −32; four-hex spacing |
| Occupied radius | 0.65 hex, with swept-disc collision checks |
| Shared detection | 15 hexes, shorter than PPC/LRM reach |
| Movement | Stock Walk / Run MP × 0.12 hexes per second |
| One leg lost | Half movement speed |
| Simulation step | 0.05 seconds, independent of rendering rate |
| Preferred distance | Damage-weighted average usable short range, clamped to 1–15 hexes |
| Distance control | 1.5–21 hexes; 0.5-hex increments |
| Position tolerances | 0.4 hex engagement distance; 0.2 hex arrival |

Move toward the center to make contact. Enemy AI advances there until it detects a target. Steering tries direct movement then ±30°, ±60° and ±90° alternatives around occupied space. Wrecks remain occupied space. Arena bounds and separate group destinations apply to both forces. Preferred distance never prevents an otherwise valid shot.

## Weapons and hit rolls

| Weapon | Cooldown |
| --- | --- |
| Machine gun | 4 seconds |
| Flamer / Small Laser | 6 seconds |
| Medium Laser | 8 seconds |
| Other weapons | 10 seconds |

Weapons start ready; cooldowns progress only during active simulation time. Stock weapon damage, ranges, armor and structure are retained. Missile racks deliver stock per-missile damage × rack size in one packet on one roll. Flamers apply numeric damage only. Ammunition availability and destroyed bins affect usable weapons; ammunition is not consumed.

Hit chance is the exact probability of 2d6 meeting this threshold:

`Gunnery + 3 (real-time aim modifier) + range + minimum-range penalty + attacker movement + target movement`

- Range: +0 short, +2 medium, +4 long; no shots beyond long range.
- Minimum range: `ceil(minimum − distance)` when inside minimum.
- Each moving mech contributes +1 walking or +2 running; stationary contributes zero.
- There is no artificial minimum/maximum chance clamp. Closing range and stopping can improve accuracy.
- Target preference does not wait for cooldowns. Valid current targets persist until invalid or superseded by Attack / priority.

Hit locations use a separate 2d6 roll:

| Roll | Location | Weight |
| --- | --- | --- |
| 2, 7 | Center torso | 7/36 |
| 3, 4 | Right arm | 5/36 |
| 5 | Right leg | 4/36 |
| 6 | Right torso | 5/36 |
| 8 | Left torso | 5/36 |
| 9 | Left leg | 4/36 |
| 10, 11 | Left arm | 5/36 |
| 12 | Head | 1/36 |

Each step captures every due discharge and hit roll before applying damage. Outcomes follow the entire batch, permitting simultaneous lethal return fire and draws. Visual effects illustrate already-resolved results.

Five-to-ten minutes remains a provisional ordinary-engagement target. The initial mixed-lance check finishes in 285 simulated seconds (about five minutes); one-on-one Jenner, Griffin and Awesome checks finish in 105, 211 and 168 seconds. These seeded results are repeatable comparisons, not guarantees for every fight. One-on-one light-mech fights can finish substantially sooner than lance battles.

## Interaction defaults

- Preselect the current eligible hangar mech, otherwise the first eligible mech. Choose one to four; unavailable entries explain why. Assignments remain in the hangar.
- Click / drag selects friendlies; Shift adds or removes selection. Right-click ground moves, or a detected enemy attacks. Last-known markers are stationary ground destinations.
- Set priority target arms a separate right-click action; priority alone never causes pursuit. Stop clears the active order while preserving priority.
- Group order, movement, firing, priority and distance edits affect active selected units. Weapon controls and location values show the primary unit. Unselected mode buttons indicate mixed settings; mixed distances have a tooltip.
- Space pauses with battlefield focus; the pause button always works. WASD/arrows or middle-drag pan, wheel zooms, Q/E or Shift-middle rotates, Home fits the arena. Camera controls work paused.
- Escape suspends combat. Resume restores the preceding running/paused state. End Simulation shows an ended damage summary; results return to the original hangar selection and tab.
- Enemy inspection shows identity and observable damage descriptions. Exact enemy internals appear only in the completed report.
- Procedural 3D mech stand-ins have faction colors, facing indicators, selection/priority rings, order lines and shot/hit effects. Facing is cosmetic: it introduces no firing arcs or rear-armor hits.

## Validation

`./scripts/mech test` exercises the ten acceptance scenarios with deterministic model checks and actual mouse/keyboard input. `./scripts/mech capture` repeats the suite with graphical rendering and saves deployment, combat, minimum-window, menu and report captures in `TestResults/`. Linux checks do not establish native Windows/macOS runtime verification.
