# Tactical simulation defaults

The first tactical slice implements [the confirmed combat design](combat-design.md). `CombatSimulation` owns copied condition, equipment, ammunition availability and MechWarrior skills. It has no campaign reference or save operation. Rendering and input live separately in `CombatViewport` and `CombatScreen`.

## Space and time

| Value | Default |
| --- | --- |
| Distance unit | One stock range hex (nominally 30 metres) |
| Arena | 192 × 192 hexes, centered on the origin |
| Starts | Z = +64 / −64; eight-hex spacing |
| Occupied radius | 0.65 hex, with swept-disc collision checks |
| Identification range | `50 × (1 + (6 − Sensors) / 4) × (0.5 + (target tons − 20) / 160)` hexes |
| Detailed status range | Half identification range; same terrain sight requirement |
| Movement limits | Stock Walk / Run MP × 0.18 hexes per second for every chassis |
| Acceleration | Running speed × Walk MP / 9 hexes per second² |
| Braking | Twice acceleration |
| Stationary turn rate | Walk MP × 15 degrees per second |
| Moving turn rate | Stationary rate × (1 − 0.55 × actual speed / running speed) |
| Torso twist limit | ±90° relative to legs |
| Torso twist speed | Light 90°/s; Medium 75°/s; Heavy 60°/s; Assault 45°/s, relative to legs |
| Weapon arc | ±30° about mount facing; leg mounts use legs; rear mounts point backward |
| Rear armor arc | Rear 120° about torso facing, including boundaries |
| Reverse limit | Walking speed, including when Run is selected |
| One leg lost | Half movement speed |
| Simulation step | 0.05 seconds, independent of rendering rate |
| Preferred distance | Damage-weighted average usable scaled short range, clamped to 3–45 hexes |
| Distance control | 4.5–63 hexes; 0.5-hex increments |
| Follow spacing | About 3 hexes, with normal terrain and occupied-space clearance |
| Position tolerances | 0.4 hex engagement distance; 0.2 hex arrival |

All chassis, including future imports, move 50% faster than the original 0.12-hex-per-MP scale. `CombatRules.MovementSpeed` supplies the shared 0.18 scale to combat and hangar previews without chassis exceptions. Wolverine keeps its accepted 0.90 walking / 1.44 running hexes per second. Catalog movement points and turn rates stay unchanged. Acceleration and braking scale with running speed, preserving their timing; reverse uses the increased walking limit and leg damage still halves both limits. The Awesome (3/5 MP) reaches full run in three seconds, stops from it in 1.5 seconds, and turns 180° in place in four seconds. The Jenner (7/11 MP) takes approximately 1.29, 0.64 and 1.71 seconds respectively. Walk/Run selects the speed limit; changing modes does not snap actual speed.

Mechs travel along their leg facing. Moving turns become wider as speed rises; direction changes of 60° or more command braking while the chassis turns. Destination approach uses braking distance to stop within the arrival tolerance. Stop clears the order immediately and brakes existing momentum. Attack orders reverse at up to walking speed to open engagement distance, maintaining facing toward the target; forward/reverse transitions brake to zero first. Stationary target-facing uses the same turn rate. Torso tracking and firing arcs follow the confirmed torso-twisting extension; stopped legs turn toward the aim target while the torso compensates independently. Collisions and arena boundaries can halt travel immediately.

Move toward the center to make contact. Enemy AI advances there until it detects a target. Steering looks ahead by stopping distance and tries direct movement then ±30°, ±60° and ±90° alternatives around occupied space. Each candidate supplies a desired heading; the mech must turn toward it before traveling. Destroyed mechs immediately stop occupying movement space, including Follow destinations, while disabled mechs remain solid. Wrecks remain visible and selectable/inspectable. Arena bounds and separate group destinations apply to both forces. Preferred distance never prevents an otherwise valid shot.

## Natural terrain

Terrain is an immutable, seeded heightfield shared by combat, navigation, rendering and mouse picking. Ground positions remain continuous X/Z; elevation comes from the same triangulated one-hex heightfield used by the mesh. Combat randomness has a separate seed. Deployment shows an editable signed 32-bit terrain seed and a New terrain button; the battlefield, pause menu and results retain the seed.

| Value | Default |
| --- | --- |
| Hills | 28 cosine hills, radius 6–11, height 3–7; overlapping elevations combine |
| Rocks | Up to 64, radius 1.3–3, height 3–6; solid vertical cylinders |
| Vegetation | Up to 40 patches, radius 3–6; 70% speed |
| Water | Up to 16 shallow patches, radius 3–6; 50% speed and flat banks |
| Steepness limit | Gradient 0.65 (about 33°), including the mech footprint |
| Slope speed factor | Divide terrain speed by 1 + local gradient |
| Solid clearance | Rock radius + mech radius + 0.1 hex |
| Detection / firing height | 1.5 hexes above the ground at each endpoint |
| Vegetation hit modifier | +1 per crossed patch, capped at +2 |
| Navigation | Eight-neighbor one-hex grid, continuous segment clearance checks every 0.25 hex |
| Hill sight checks | Every 0.2 hex along the elevated sight ray; rocks use ray/cylinder intersection |

Generation permits unrestricted asymmetry while reserving clear spawn bands and a clear central rendezvous. It validates paths between all possible spawn slots and the center, retries disconnected candidates, and requires every terrain kind. Patch placement avoids overlapping patches. Hills may overlap patches; shallow water flattens its immediate surroundings. There is one default density. Doubling each battlefield dimension quadruples the feature counts while retaining individual feature sizes. The one-hex navigation and heightfield grid is 193 × 193.

Move orders resolve inaccessible destinations to the nearest reachable navigation point in the unit's connected component. Paths detour around rocks and steep slopes, with terrain speed weighting for detours; direct clear routes remain direct. Waypoints feed the existing acceleration, braking, turning, and occupied-space avoidance. Both sides seek a reachable clear firing position when a detected attack target is behind terrain, reconsidering at most once per simulated second. There is no deliberate cover-seeking AI. Shared detection cannot authorize a shot through solid terrain.

## Weapons and hit rolls

| Weapon | Cooldown |
| --- | --- |
| Machine gun | 4 seconds |
| Flamer / Small Laser | 6 seconds |
| Medium Laser | 8 seconds |
| Other weapons | 10 seconds |

Weapons start ready; cooldowns progress only during unpaused simulation time, including thermal shutdown. Stock weapon damage, armor and structure are retained. Combat multiplies all four stock weapon range boundaries by three; the imported catalog remains unchanged, and weapon inspection shows effective ranges. A medium laser therefore has short/medium/long ranges of 9/18/27 hexes. PPC minimum range is 9 and LRM minimum range is 18. Both sides use the same multiplier for targeting, firing, hit chance and terrain firing-position searches. Each missile of an LRM or SRM salvo rolls its own hit and location and deals stock per-missile damage. Each successful flamer hit deals its ordinary damage and adds 2 target heat, including through armor. Misses add no target heat. Each discharge of an ammunition-fed weapon spends one shot from the emptiest intact compatible bin; stock bins hold 45 (AC/2), 20 (AC/5), 10 (AC/10) and 5 (AC/20) shots per ton.

Lasers split damage across a one-second burn: half (rounded up) at discharge, the remainder 0.5 seconds later. Small 2 + 1, medium 3 + 2, large and ER large 4 + 4. If the target moved, turned or twisted its torso during the step it was struck, the second location is rolled at discharge, weighted by the hit-location table below among these neighbors; otherwise the remainder lands on the struck location:

| Struck location | Adjacent locations |
| --- | --- |
| Head | Center torso |
| Center torso | Head, left torso, right torso, left leg, right leg |
| Left / right torso | Center torso, same-side arm, same-side leg |
| Left / right arm | Same-side torso |
| Left / right leg | Center torso, same-side torso |

Autocannon shells fly at 60 hexes per second, so flight time is planar distance / 60: about 0.45 seconds at the AC/20's 27-hex long range and 1.2 seconds at the AC/2's 72. Impact is applied in the first 0.05-second step at or after that time.

| Caliber | Shell radius | Explosion radius | Explosion duration |
| --- | --- | --- | --- |
| AC/2 | 0.04 hex | 0.3 hex | 0.3 s |
| AC/5 | 0.06 hex | 0.5 hex | 0.4 s |
| AC/10 | 0.08 hex | 0.8 hex | 0.5 s |
| AC/20 | 0.11 hex | 1.2 hex | 0.6 s |

LRM flight time is planar distance / 17.5 hexes per second: about 1.03 seconds at the 18-hex minimum range and 3.6 seconds at the 63-hex long range. SRMs launch at 5.6 hexes per second and accelerate steadily at about 15.4 hexes/s², calibrated so the 27-hex long range takes as long as an LRM over that distance, about 1.54 seconds; flight time is `(−5.6 + √(5.6² + 2 · acceleration · distance)) / acceleration`, about 0.78 seconds at 9 hexes. Indirect fire adds +1 to the target number. A salvo ripples out over 0.25 seconds (or 40% of the flight, if shorter), and each missile's flight is shortened to land on the impact step. Each missile accelerates steadily over its own launch-to-arrival window: an LRM leaves at 40% of its average speed and dives in at 160%, reaching its apex after about 64% of the flight; an SRM follows its launch speed and acceleration, including misses out to their ground point. SRMs fly flat. A direct LRM arc peaks at 15% of the planar range; an indirect arc peaks at 35% or high enough to pass blocking terrain by 3 hexes, up to the full range. Each missile's skitter is two lateral and two vertical sine waves of 1.5–4 cycles per flight, with amplitude 0.375–0.75 hexes; it builds over the first quarter of the path and fades to zero over the last fifth. The flame trail spans the last 0.04 seconds of flight, so it lengthens as a missile speeds up. It is additive and runs from white at the missile through yellow (1, 0.9, 0.2) at its middle to orange (1, 0.45, 0.05) at the tail, its opacity fading linearly to zero at the tail. Each impact bursts to 0.25 hex over 0.3 seconds. Missed missiles land 1–2.5 hexes beside and 1–3 hexes beyond their rolled location.

The muzzle flash lasts 0.1 seconds. A tracer streak trails the slug by 0.8 hex plus six shell radii. Misses fly on up to long range plus 10 hexes from the muzzle before fading over 0.1 seconds.

The extra location roll is drawn only for laser hits on a target in motion. Beam halo radii are 0.02 (small), 0.04 (medium) and 0.06 (large and ER large) hexes, with a core at 40% of the halo. Halos are pure red (small), pure green (medium) and deep blue (large and ER large); the core blends that color 30% toward white so additive beams keep their color. The beam reaches full length 0.1 seconds after discharge, sweeps linearly from the first location to the second over the 1-second burn (holding still when they are the same) and fades over 0.15 seconds after the burn. A side's defeat starts a 3-second wind-down before the outcome.

Hit chance is the exact probability of 2d6 meeting this threshold:

`Gunnery + 3 (real-time aim modifier) + range + minimum-range penalty + attacker movement + target movement + vegetation + attacker heat (+ 1 indirect)`

- Range: +0 short, +2 medium, +4 long; no shots beyond long range.
- Minimum range: `ceil((scaled minimum − distance) / 3)` when inside minimum, preserving penalty severity at equivalent relative distances.
- Vegetation contributes +1 per intersected patch, including target vegetation, capped at +2. Indirect LRM fire counts the spotter's line to the target instead. It does not block detection or reduce damage.
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

Each step moves units, integrates movement heat and cooling, refreshes detection, updates every torso, and selects each unit's best volley within its heat policy. All discharges and hit rolls are captured before applying directional damage, firing heat and flamer heat. Shots in the same exchange use the starting heat penalties; received flamer heat cannot cancel simultaneous return fire. Outcomes follow the entire batch, permitting simultaneous lethal return fire and draws. Visual effects illustrate already-resolved results.

## Heat

All units begin at zero heat with Normal policy. Heat is continuous, never negative, and not capped at shutdown. Pausing freezes both generation and dissipation. Stock per-shot heat is added even on misses. Single sinks remove 1 heat per sink per 10 seconds; double sinks remove 2. Actual walking-speed travel adds 1 heat per 10 seconds, actual speed above the unheated walking limit adds 2, reversing counts as walking, and turning in place adds none. Selected Walk/Run mode alone does not generate heat.

| Heat | Speed limit | Attacker hit difficulty |
| --- | --- | --- |
| Below 10 | 100% | +0 |
| 10–below 20 | 90% | +1 |
| 20–below 30 | 75% | +2 |
| 30 or more | Thermal shutdown | Cannot fire |

Heat reduces walking and running limits without changing the underlying acceleration, braking or turn-rate tuning. Thermal shutdown is latched until heat falls below 10, then restarts automatically without another timer. It stops travel, leg/torso turning, firing and sensor contribution. The unit remains targetable, solid, alive for victory checks, and editable. Orders, Follow relationships, weapon settings and policy survive shutdown. Excess flamer heat extends cooling time; multiple successful hits stack without a special cap.

Normal only permits volleys whose projected heat is strictly below 20; Aggressive permits heat strictly below 30. Ready, enabled, usable weapons with valid firing arcs and targets compete in a single budget, including rear mounts. Selection maximizes ordinary damage × current hit chance, then minimizes heat, then favors catalog order. Rolls are made only after selection and remain in catalog order. Withheld weapons keep their cooldown ready; their readouts explain cooling. Zero-heat weapons may fire when the policy ceiling permits them. Both sides use the same rules; enemies initially use Normal.

The stock total includes internal sinks. Explicit Heat Sink component entries publish their grouped contribution as `HeatSinkCount`, independently of critical-slot count; existing single-sink groups retain the previous contribution equal to their Slots count. Remove the whole group's contribution when its component is Destroyed or its mounting location is lost, including attached arm loss; do not subtract it twice. Damaged groups retain full output. The unlisted internal remainder survives while the mech survives. Mauler's 11 internal double sinks remove 2.2 heat per second. Individual critical hits and engine heat are deferred.

Friendly ground labels and cards show current heat; selected details show heat band, policy and effective cooling. Normal/Aggressive controls apply to selected units including thermal shutdowns, with a Mixed group readout when appropriate. Exact enemy heat and cooling appear only at Detailed contact tier and disappear when that tier is lost. Water provides no cooling bonus. Heat damage, ammunition explosions and manual venting remain deferred.

Five-to-ten minutes remains a provisional ordinary-engagement target. With the expanded ranges and Normal heat policies, the seeded flat-ground mixed-lance check finishes in 540 simulated seconds; one-on-one Jenner, Griffin and Awesome checks finish in 136, 234 and 237 seconds. The seeded natural-terrain mixed-lance check finishes in 491 seconds. These seeded results are repeatable comparisons, not guarantees for every fight. One-on-one light-mech fights can finish substantially sooner than lance battles.

## Calculation reuse

Navigation stores immutable per-edge movement costs alongside the terrain's connectivity graph. A* rents its cost and predecessor buffers per query and returns independent waypoint lists; concurrent queries share no writable search state. Firing-position searches limit candidates to the weapon-range bounding box and reuse a pooled sorting buffer. Equal scores still favor the original row-major node order.

The weapon sidebar evaluates each distinct main/rear target pair's heat volley once per refresh, sharing it across weapon statuses. Preview-only targets retain the existing mount-specific fallback. Results are discarded after that refresh so paused edits, cooldowns, contacts, heat and damage are reflected immediately; previews never consume random draws.

The terrain and heat checks cover stable route/firing choices, concurrent searches, allocation budgets, batched status equivalence and unchanged firing events. Allocation checks warm the buffer pools first and avoid wall-clock timing assertions.

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
- The cursor is hidden and captured during right-drag, allowing continuous rotation at window edges. Release restores its mode and press position. Menus, loss of window or battlefield focus, and leaving battle cancel the gesture and release capture. Home restores arena center, default elevation (~49°), zero yaw and orthographic size 220; zoom ranges from 12 to 280. A new deployment starts at ground focus (0, 44), default elevation, zero yaw and size 50.
- Escape suspends combat. Resume restores the preceding running/paused state. End Simulation shows an ended damage summary; results return to the original hangar selection and tab.
- Enemy inspection at full range reveals only the chassis model. Within half range it also shows exact armor, rear armor, internals and effective equipment condition/availability. The side shares its best current observation; live details disappear when all close observers lose them. Physical damage remains visible.
- Inspection retains the friendly command selection and draws separate unions of target-specific identification (blue) and detailed-status (gold) ranges for all selected active mechs. Otherwise show the union of 100-ton identification ranges and label the 20–100-ton detection span. Only exposed outlines are drawn: no fill or internal overlap arcs; separated regions and uncovered holes remain. Disabled/destroyed mechs contribute no ranges. Multiple active contributors use “Selected mechs” with minimum–maximum ranges for each displayed tier; one active contributor keeps the single-mech readout. Terrain can block contacts within these boundaries; detection remains shared by the entire side independently of selection.
- Pilot skills are rated 2–6. Each improvement from 6 costs one point, with six points for regulars and eight for veterans. Fixed catalog allocations include Sensors specialists; no allocation screen or progression is added.
- Procedural 3D mech stand-ins have faction colors, facing indicators, selection/priority rings, order lines and shot/hit effects. Team-colored ground lines show leg facing; longer gold lines with tip dots show torso facing. Weapon arcs and incoming torso armor use those simulation headings.

## Validation

After building, the pinned .NET Godot engine can run `--headless --path . --scene res://Tests/SmokeTests.tscn -- --combat-simulation-only` for combat and terrain checks, including twist rates/limits, tracking, firing arcs, rear targets, directional damage transfer, heat rates, sink loss, exact policy budgets, optimal volley selection, threshold penalties, shutdown/restart, flamer stacking and simultaneous heat resolution. The optimizer is checked against exhaustive subset selection on generated small loadouts. `--combat-layout-only` includes group heat-policy controls, editing during shutdown and enemy heat masking at both window sizes.

`./scripts/mech test` exercises seeded generation, connected spawns, slope/rock clearance, terrain detours, cover, sight, water movement, generated-map engagements, terrain picking and seed controls alongside the ten acceptance scenarios with deterministic model checks and actual mouse/keyboard input. `./scripts/mech capture` repeats the suite with graphical rendering and saves deployment, combat, minimum-window, menu and report captures in `TestResults/`. Linux checks do not establish native Windows/macOS runtime verification.
