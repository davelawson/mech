# Tactical simulation defaults

The first tactical slice implements [the confirmed combat design](combat-design.md). `CombatSimulation` owns copied condition, equipment, ammunition availability and MechWarrior skills. It has no campaign reference or save operation. Rendering and input live separately in `CombatViewport` and `CombatScreen`.

## Space and time

| Value | Default |
| --- | --- |
| Distance unit | One stock range hex (nominally 30 metres) |
| Arena | 96 × 96 hexes, centered on the origin |
| Starts | Z = +32 / −32; four-hex spacing |
| Occupied radius | 0.65 hex, with swept-disc collision checks |
| Identification range | `25 × (1 + (6 − Sensors) / 4) × (0.5 + (target tons − 20) / 160)` hexes |
| Detailed status range | Half identification range; same terrain sight requirement |
| Movement limits | Stock Walk / Run MP × 0.18 hexes per second for every chassis |
| Acceleration | Running speed × Walk MP / 9 hexes per second² |
| Braking | Twice acceleration |
| Stationary turn rate | Walk MP × 15 degrees per second |
| Moving turn rate | Stationary rate × (1 − 0.55 × actual speed / running speed) |
| Reverse limit | Walking speed, including when Run is selected |
| One leg lost | Half movement speed |
| Simulation step | 0.05 seconds, independent of rendering rate |
| Preferred distance | Damage-weighted average usable short range, clamped to 1–15 hexes |
| Distance control | 1.5–21 hexes; 0.5-hex increments |
| Follow spacing | About 3 hexes, with normal terrain and occupied-space clearance |
| Position tolerances | 0.4 hex engagement distance; 0.2 hex arrival |

All chassis, including future imports, move 50% faster than the original 0.12-hex-per-MP scale. `CombatRules.MovementSpeed` supplies the shared 0.18 scale to combat and hangar previews without chassis exceptions. Wolverine keeps its accepted 0.90 walking / 1.44 running hexes per second. Catalog movement points and turn rates stay unchanged. Acceleration and braking scale with running speed, preserving their timing; reverse uses the increased walking limit and leg damage still halves both limits. The Awesome (3/5 MP) reaches full run in three seconds, stops from it in 1.5 seconds, and turns 180° in place in four seconds. The Jenner (7/11 MP) takes approximately 1.29, 0.64 and 1.71 seconds respectively. Walk/Run selects the speed limit; changing modes does not snap actual speed.

Mechs travel along their leg facing. Moving turns become wider as speed rises; direction changes of 60° or more command braking while the chassis turns. Destination approach uses braking distance to stop within the arrival tolerance. Stop clears the order immediately and brakes existing momentum. Attack orders reverse at up to walking speed to open engagement distance, maintaining facing toward the target; forward/reverse transitions brake to zero first. Stationary target-facing uses the same turn rate. Facing still imposes no firing arcs or rear-hit rules. Collisions and arena boundaries can halt travel immediately.

Move toward the center to make contact. Enemy AI advances there until it detects a target. Steering looks ahead by stopping distance and tries direct movement then ±30°, ±60° and ±90° alternatives around occupied space. Each candidate supplies a desired heading; the mech must turn toward it before traveling. Destroyed mechs immediately stop occupying movement space, including Follow destinations, while disabled mechs remain solid. Wrecks remain visible and selectable/inspectable. Arena bounds and separate group destinations apply to both forces. Preferred distance never prevents an otherwise valid shot.

## Natural terrain

Terrain is an immutable, seeded heightfield shared by combat, navigation, rendering and mouse picking. Ground positions remain continuous X/Z; elevation comes from the same triangulated one-hex heightfield used by the mesh. Combat randomness has a separate seed. Deployment shows an editable signed 32-bit terrain seed and a New terrain button; the battlefield, pause menu and results retain the seed.

| Value | Default |
| --- | --- |
| Hills | Seven cosine hills, radius 6–11, height 3–7; overlapping elevations combine |
| Rocks | Up to 16, radius 1.3–3, height 3–6; solid vertical cylinders |
| Vegetation | Up to ten patches, radius 3–6; 70% speed |
| Water | Up to four shallow patches, radius 3–6; 50% speed and flat banks |
| Steepness limit | Gradient 0.65 (about 33°), including the mech footprint |
| Slope speed factor | Divide terrain speed by 1 + local gradient |
| Solid clearance | Rock radius + mech radius + 0.1 hex |
| Detection / firing height | 1.5 hexes above the ground at each endpoint |
| Vegetation hit modifier | +1 per crossed patch, capped at +2 |
| Navigation | Eight-neighbor one-hex grid, continuous segment clearance checks every 0.25 hex |
| Hill sight checks | Every 0.2 hex along the elevated sight ray; rocks use ray/cylinder intersection |

Generation permits unrestricted asymmetry while reserving clear spawn bands and a clear central rendezvous. It validates paths between all possible spawn slots and the center, retries disconnected candidates, and requires every terrain kind. Patch placement avoids overlapping patches. Hills may overlap patches; shallow water flattens its immediate surroundings. There is one default density.

Move orders resolve inaccessible destinations to the nearest reachable navigation point in the unit's connected component. Paths detour around rocks and steep slopes, with terrain speed weighting for detours; direct clear routes remain direct. Waypoints feed the existing acceleration, braking, turning, and occupied-space avoidance. Both sides seek a reachable clear firing position when a detected attack target is behind terrain, reconsidering at most once per simulated second. There is no deliberate cover-seeking AI. Shared detection cannot authorize a shot through solid terrain.

## Weapons and hit rolls

| Weapon | Cooldown |
| --- | --- |
| Machine gun | 4 seconds |
| Flamer / Small Laser | 6 seconds |
| Medium Laser | 8 seconds |
| Other weapons | 10 seconds |

Weapons start ready; cooldowns progress only during active simulation time. Stock weapon damage, ranges, armor and structure are retained. Missile racks deliver stock per-missile damage × rack size in one packet on one roll. Flamers apply numeric damage only. Ammunition availability and destroyed bins affect usable weapons; ammunition is not consumed.

Hit chance is the exact probability of 2d6 meeting this threshold:

`Gunnery + 3 (real-time aim modifier) + range + minimum-range penalty + attacker movement + target movement + vegetation`

- Range: +0 short, +2 medium, +4 long; no shots beyond long range.
- Minimum range: `ceil(minimum − distance)` when inside minimum.
- Vegetation contributes +1 per intersected patch, including target vegetation, capped at +2. It does not block detection or reduce damage.
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

Five-to-ten minutes remains a provisional ordinary-engagement target. With the budgeted pilot catalog and Sensors ranges, the seeded flat-ground mixed-lance check finishes in 262 simulated seconds; one-on-one Jenner, Griffin and Awesome checks finish in 54, 206 and 143 seconds. The seeded natural-terrain mixed-lance check finishes in 370 seconds. These seeded results are repeatable comparisons, not guarantees for every fight. One-on-one light-mech fights can finish substantially sooner than lance battles.

## Interaction defaults

- Enter a terrain seed or use New terrain before launching; the chosen seed reproduces terrain only.
- Preselect the current eligible hangar mech, otherwise the first eligible mech. Choose one to four; unavailable entries explain why. Assignments remain in the hangar.
- Click / drag selects friendlies; Shift adds or removes selection. Right-click ground moves, a detected enemy attacks, or a friendly becomes the Follow leader. Orders issue on release only if the pointer stays within five screen pixels of its press position; small accidental movements still count as a click. Last-known markers are stationary ground destinations.
- Every mech click and hover shares one camera ray against the visible animated model bounds, padded by 0.08 local units. These envelopes include limb gaps, follow facing and zoom, and shrink with lost limbs. The nearest envelope wins overlaps regardless of allegiance, with terrain blocking hidden portions. Hover adds 1.3 pixels to the team outline and blends its color halfway to white, while retaining the gold selection ring. UI overlays, menus, drags and lost contacts suppress hover.
- Follow uses normal pathfinding to keep about three hexes from its leader, choosing nearby free space when another follower occupies its approach. It waits when close enough and resumes when the leader moves. The clicked leader keeps its own order if selected; disabled or destroyed leaders cause followers to Hold. Movement, firing settings and priority targets are retained.
- Set priority target arms a separate right-click action; priority alone never causes pursuit. Friendly clicks keep that mode armed and issue no Follow. Stop clears the active order while preserving priority.
- Group order, movement, firing, priority and distance edits affect active selected units. Weapon controls and location values show the primary unit. Unselected mode buttons indicate mixed settings; mixed distances have a tooltip.
- Space pauses with battlefield focus; the pause button always works. WASD/arrows pan across the ground relative to camera facing, starting and stopping immediately with normalized diagonal speed. Speed is half the orthographic zoom size in hexes per second. Middle-drag also pans; wheel zoom and Q/E or Shift-middle yaw rotation remain available. Camera controls work paused with battlefield focus, but not while a menu or text field has focus.
- Right-drag orbits the center-view ground intersection. Rotation starts only after moving more than five screen pixels from the press position; threshold movement is discarded to avoid a jump. A drag never issues an order, even if it returns to its start. Sensitivity is 0.25° per screen pixel without smoothing: upward drag raises the camera, rightward drag orbits clockwise from above. Elevation is limited to 15°–85° downward. WASD can translate the orbit point during rotation; the point stays within the arena bounds.
- The cursor is hidden and captured during right-drag, allowing continuous rotation at window edges. Release restores its mode and press position. Menus, loss of window or battlefield focus, and leaving battle cancel the gesture and release capture. Home restores arena center, default elevation (~49°), zero yaw and orthographic size 110; a new deployment starts at ground focus (0, 12), default elevation, zero yaw and size 50.
- Escape suspends combat. Resume restores the preceding running/paused state. End Simulation shows an ended damage summary; results return to the original hangar selection and tab.
- Enemy inspection at full range reveals only the chassis model. Within half range it also shows exact armor, rear armor, internals and effective equipment condition/availability. The side shares its best current observation; live details disappear when all close observers lose them. Physical damage remains visible.
- Inspection retains the friendly command selection and draws separate unions of target-specific identification (blue) and detailed-status (gold) ranges for all selected active mechs. Otherwise show the union of 100-ton identification ranges and label the 20–100-ton detection span. Only exposed outlines are drawn: no fill or internal overlap arcs; separated regions and uncovered holes remain. Disabled/destroyed mechs contribute no ranges. Multiple active contributors use “Selected mechs” with minimum–maximum ranges for each displayed tier; one active contributor keeps the single-mech readout. Terrain can block contacts within these boundaries; detection remains shared by the entire side independently of selection.
- Pilot skills are rated 2–6. Each improvement from 6 costs one point, with six points for regulars and eight for veterans. Fixed catalog allocations include Sensors specialists; no allocation screen or progression is added.
- Procedural 3D mech stand-ins have faction colors, facing indicators, selection/priority rings, order lines and shot/hit effects. Facing is cosmetic: it introduces no firing arcs or rear-armor hits.

## Validation

`./scripts/mech test` exercises seeded generation, connected spawns, slope/rock clearance, terrain detours, cover, sight, water movement, generated-map engagements, terrain picking and seed controls alongside the ten acceptance scenarios with deterministic model checks and actual mouse/keyboard input. `./scripts/mech capture` repeats the suite with graphical rendering and saves deployment, combat, minimum-window, menu and report captures in `TestResults/`. Linux checks do not establish native Windows/macOS runtime verification.
