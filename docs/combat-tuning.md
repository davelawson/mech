# Tactical simulation defaults

The first tactical slice implements [the confirmed combat design](combat-design.md). `CombatSimulation` owns copied condition, equipment, ammunition availability and MechWarrior skills. It has no campaign reference or save operation. Rendering and input live separately in `CombatViewport` and `CombatScreen`.

## Space and time

| Value | Default |
| --- | --- |
| Distance unit | Metre; one world unit is one metre |
| Arena | 5,760 × 5,760 metres, centered on the origin |
| Starts | Z = +1,920 / −1,920; 240-metre spacing |
| Occupied radius | 9.75 metres, with swept-disc collision checks |
| Identification range | `1500 × (1 + (6 − Sensors) / 4) × (0.5 + (target tons − 20) / 160)` metres |
| Detailed status range | Half identification range; same terrain sight requirement |
| Movement limits | Stock Walk / Run MP × 25/9 metres per second (exactly 10 km/h per MP) for every chassis |
| Acceleration | Running speed × Walk MP / 9 metres per second² |
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
| Preferred distance | Damage-weighted average usable scaled short range, clamped to 90–1,350 metres |
| Distance control | 135–1,890 metres; 15-metre increments |
| Follow spacing | About 90 metres, with normal terrain and occupied-space clearance |
| Position tolerances | 12 metres engagement distance; 6 metres arrival |

All chassis, including future imports, move at 25/9 metres per second per MP, exactly 10 km/h, so a 4/6 mech walks at 40 and runs at 60 km/h. (It was 2.7 m/s, 9.72 km/h, before November 11, 2026, and 5.4 before that.) `CombatRules.MovementSpeed` supplies the shared scale to combat and hangar previews without chassis exceptions. The Wolverine (5/8 MP) walks at about 13.9 and runs at about 22.2 metres per second (50 and 80 km/h). Catalog movement points and turn rates stay unchanged. Acceleration and braking scale with running speed, preserving their timing; reverse uses the walking limit and leg damage still halves both limits. The Awesome (3/5 MP) reaches full run in three seconds, stops from it in 1.5 seconds, and turns 180° in place in four seconds. The Jenner (7/11 MP) takes approximately 1.29, 0.64 and 1.71 seconds respectively. Walk/Run selects the speed limit; changing modes does not snap actual speed.

Mechs travel along their leg facing. Moving turns become wider as speed rises; direction changes of 60° or more command braking while the chassis turns. Destination approach uses braking distance to stop within the arrival tolerance. Stop clears the order immediately and brakes existing momentum. Attack orders reverse at up to walking speed to open engagement distance, maintaining facing toward the target; forward/reverse transitions brake to zero first. Stationary target-facing uses the same turn rate. Torso tracking and firing arcs follow the confirmed torso-twisting extension; stopped legs turn toward the aim target while the torso compensates independently. Collisions and arena boundaries can halt travel immediately.

Move toward the center to make contact. Enemy AI advances there until it detects a target. Steering looks ahead by stopping distance and tries direct movement then ±30°, ±60° and ±90° alternatives around occupied space. Each candidate supplies a desired heading; the mech must turn toward it before traveling. Destroyed mechs immediately stop occupying movement space, including Follow destinations, while disabled mechs remain solid. Wrecks remain visible and selectable/inspectable. Arena bounds and separate group destinations apply to both forces. Preferred distance never prevents an otherwise valid shot.

## Natural terrain

Terrain is an immutable, seeded heightfield shared by combat, navigation, rendering and mouse picking. Ground positions remain continuous X/Z; elevation comes from the same triangulated heightfield, sampled every 30 metres, used by the mesh. Combat randomness has a separate seed. Deployment shows an editable signed 32-bit terrain seed, a New terrain button and a Biome picker; the battlefield, pause menu and results retain the seed and biome.

| Value | Default |
| --- | --- |
| Valley relief | Fractal value noise (3 octaves, 1,400 m base wavelength) through a smoothstep, up to 230 m, plus 8 m of detail |
| Arctic relief | Ridged noise (3 octaves, 1,200 m) raised to the 2.4th power, up to 270 m, plus 10 m of detail |
| Lunar relief | 45 m of gentle noise and 40 craters, radius 60–480 m (mostly small), depth 15–30% of radius, rim 6% |
| Highlands relief | Fractal noise (3 octaves, 1,600 m) through a smoothstep, up to 300 m, plus cubed ridged noise (800 m) up to 90 m, plus 10 m of detail |
| Desert relief | 70 m of broad noise (2,200 m) plus dunes: squared ridged noise stretched 650 m along X and 260 m along Z, up to 22 m, plus 4 m of detail |
| Tropical relief | Fractal noise (1,000 m) through a smoothstep, up to 170 m, plus 12 m of detail |
| Grassland relief | 60 m of broad noise (2,400 m) plus 4 m of detail |
| Archipelago relief | Fractal noise (1,100 m) through a narrow smoothstep, up to 150 m, so most ground is low shore; plus 6 m of detail |
| Volcanic relief | 60 m of ridged noise (900 m), 8 m of detail, and 5 cones, radius 350–650 m, height 35% of radius, with a caldera at the inner 22% sunk by a quarter of the cone's height |
| Toxic relief | Fractal noise (1,200 m) through a smoothstep, up to 140 m, plus 40 m of ridged noise (500 m) and 6 m of detail |
| Valley features | 40 rocks (radius 39–90 m, height 90–180 m), 48 forests (radius 90–180 m), 16 water patches (radius 90–180 m) |
| Arctic features | 56 rocks (as Valley), 24 conifer stands (radius 60–120 m), 14 frozen lakes (radius 120–240 m) |
| Lunar features | 84 boulders (radius 24–60 m, height 30–100 m) in 12 fields |
| Highlands features | 64 rocks (as Valley), 28 conifer stands (radius 60–120 m), 8 tarns (radius 60–120 m) |
| Desert features | 48 rocks (radius 45–110 m, height 60–200 m), 10 scrub patches (radius 45–90 m), 4 oases (radius 60–90 m) |
| Tropical features | 18 rivers and pools (radius 90–180 m), 72 jungle patches (radius 90–200 m), 24 rocks (as Valley) |
| Grassland features | 16 rocks (radius 39–90 m, height 60–140 m), 14 copses (radius 60–120 m), 6 ponds (radius 90–150 m) |
| Archipelago features | 16 deep-water channels (radius 180–360 m), 14 shallows (radius 90–180 m), 24 palm groves (radius 60–120 m), 20 rocks (as Valley) |
| Volcanic features | 12 lava flows (radius 90–200 m), 64 rocks (as Valley) |
| Toxic features | 18 toxic pools (water, radius 90–180 m), 20 fungal thickets (radius 60–120 m), 40 rocks (as Valley) |
| Water / ice | Flat at their centre's level, banks blended over 120 m; water 50% speed, ice full speed and no cover. Placed on the lowest 30% of the ground where possible |
| Lava / deep water | Flat and placed like water; impassable within patch radius + mech radius; open to sight and fire |
| Vegetation | 70% speed |
| Settlements | 1–6 clusters, 15–50 buildings in total, 15–60 m tall; 50 m lots on a grid with 60 m streets, levelled pads blended over 150 m |
| Steepness limit | Gradient 0.65 (about 33°), including the mech footprint |
| Slope speed factor | Divide terrain speed by 1 + local gradient |
| Solid clearance | Rock radius, or building footprint, + mech radius + 1.5 metres |
| Detection / firing height | 22.5 metres above the ground at each endpoint |
| Vegetation hit modifier | +1 per crossed patch, capped at +2 |
| Navigation | Eight-neighbor 30-metre grid, continuous segment clearance checks every 7.5 metres |
| Sight checks | Every 6 metres along the elevated sight ray; rocks use ray/cylinder and buildings ray/box intersection |

Generation permits unrestricted asymmetry while reserving clear spawn bands and a clear central rendezvous: relief fades to flat within 400 metres of them. Lakes keep 150 metres from spawns and settlements, and other features 60 metres from spawns and 150 metres from settlements. Generation validates paths between all possible spawn slots and the center, retries disconnected candidates, and requires every feature kind of its biome. Patch placement avoids overlapping patches. Hand-built test terrain may still use cosine hills. The 30-metre navigation and heightfield grid is 193 × 193.

Move orders resolve inaccessible destinations to the nearest reachable navigation point in the unit's connected component. A unit held up by an obstacle for 1.5 seconds replans its route from where it stands. Paths detour around rocks, buildings and steep slopes, with terrain speed weighting for detours; direct clear routes remain direct. Waypoints feed the existing acceleration, braking, turning, and occupied-space avoidance. A unit nearing the end of its route while its goal has moved replans to the current goal, so a moving Follow leader cannot strand it short. Both sides seek a reachable clear firing position when a detected attack target is behind terrain, reconsidering at most once per simulated second. There is no deliberate cover-seeking AI. Shared detection cannot authorize a shot through solid terrain.

## System biomes

A star system's biome comes from its primary world's 3025 climate in `Data/system-climate.json`. The first matching rule wins. Temperatures are equatorial °C, and Terra is recorded at 30 °C, so the thresholds sit relative to Terra.

| Rule | Biome | Systems |
| --- | --- | --- |
| Pressure vacuum or trace | Lunar | 2 |
| Toxic atmosphere | Toxic | 25 |
| Temperature above 50 | Volcanic | 43 |
| Temperature 24 or below | Arctic | 394 |
| Water 85% or more | Archipelago | 62 |
| Temperature above 40, or above 30 with water below 30% | Desert if water below 50%, else Tropical | 282 Desert |
| Temperature above 30 with water 50% or more | Tropical | 345 in all |
| Thin pressure | Highlands | 213 |
| Water below 40% | Grassland | 297 |
| Otherwise | Valley | 490 |

The Arctic, Tropical and Grassland thresholds were widened on November 4, 2026 to halve Valley (from 964 systems) and spread its worlds across those three biomes.

Systems of 4,000,000,000 people or more (116) are also Urban.

## Mission opposition

A mission's enemy pilots are unnamed, called by rank and number ("Veteran 2"), with the same rating in Gunnery, Piloting and Sensors by rank (`MissionTuning.EnemySkill`):

| Rank | Skill |
| --- | --- |
| Green | 5 |
| Regular | 4 |
| Veteran | 3 |
| Elite | 2 |

Their mechs are stock designs, undamaged with full ammunition.

## Weapons and hit rolls

| Weapon | Cooldown |
| --- | --- |
| Machine gun | 2 seconds |
| Small Laser | 3 seconds |
| Medium Laser | 5 seconds |
| Flamer | 6 seconds |
| Large / ER Large Laser | 7 seconds |
| Other weapons | 10 seconds |

Cooldowns live with each weapon kind's stock mass, critical slots, ammunition and mounting in `Scripts/WeaponDefinitions.cs`, the single table of weapon kinds.

Weapons start ready; cooldowns progress only during unpaused simulation time, including thermal shutdown. Armor, structure and weapon damage are counted in tenths of a stock point, so the catalog holds stock values × 10: a medium laser deals 50, a Firestarter's center torso has 130 armor, 60 rear armor and 110 structure, and every head has 30 structure. The machine gun is cut to half a stock point, 5 per burst. Combat converts all four stock weapon range boundaries at 90 metres per stock point; the imported catalog remains unchanged, and weapon inspection shows effective ranges in metres. A medium laser therefore has short/medium/long ranges of 270/540/810 metres. PPC minimum range is 270 metres and LRM minimum range is 540. Both sides use the same multiplier for targeting, firing, hit chance and terrain firing-position searches. Each missile of an LRM or SRM salvo rolls its own hit and location and deals its per-missile damage, 10 for LRMs and 20 for SRMs. Each successful flamer hit deals its ordinary damage and adds 2 target heat, including through armor. Misses add no target heat. Each discharge of an ammunition-fed weapon spends one shot from the emptiest intact compatible bin; stock bins hold 200 (machine gun), 45 (AC/2), 20 (AC/5), 10 (AC/10) and 5 (AC/20) shots per ton.

Lasers split damage across a one-second burn: half (rounded up) at discharge, the remainder 0.5 seconds later. Small 15 + 15, medium 25 + 25, large and ER large 40 + 40. If the target moved, turned or twisted its torso during the step it was struck, the second location is rolled at discharge, weighted by the hit-location table below among these neighbors; otherwise the remainder lands on the struck location:

| Struck location | Adjacent locations |
| --- | --- |
| Head | Center torso |
| Center torso | Head, left torso, right torso, left leg, right leg |
| Left / right torso | Center torso, same-side arm, same-side leg |
| Left / right arm | Same-side torso |
| Left / right leg | Center torso, same-side torso |

Autocannon shells fly at 1,800 metres per second, so flight time is planar distance / 1,800: about 0.45 seconds at the AC/20's 810-metre long range and 1.2 seconds at the AC/2's 2,160. Impact is applied in the first 0.05-second step at or after that time.

| Caliber | Shell radius | Explosion radius | Explosion duration |
| --- | --- | --- | --- |
| AC/2 | 0.9 m | 4.5 m | 0.3 s |
| AC/5 | 1.2 m | 7.5 m | 0.4 s |
| AC/10 | 1.5 m | 12 m | 0.5 s |
| AC/20 | 1.95 m | 18 m | 0.6 s |

A machine gun burst lasts 1 second. Bullets fly at 3,600 metres per second, so the damage lands 0.075 seconds after discharge at the 270-metre long range, in the first 0.05-second step at or after that time. The burst shows 15 rounds, one every 1/15 second, each randomly bright yellow (1, 0.9, 0.3) or orange (1, 0.55, 0.1). A round is an additive streak 0.3 metres in radius, a third of the AC/2 shell's, and up to 6 metres long, trailing its head back toward the muzzle; hit rounds take the discharge's flight time. Each round re-lights a 1.2-metre muzzle flash that fades over 0.04 seconds. Hit rounds land within ±2.25 metres on each axis of the struck location's center and spark in a 0.75-metre white-yellow (1, 1, 0.75) flash that fades over 0.08 seconds. Missed rounds aim within ±6.75 metres of the miss point and fade over 0.1 seconds 300 metres beyond long range. Visual scatter and colors are seeded by the discharge and never touch simulation draws.

PPC bolts fly at one constant speed per weapon, the speed that reaches its long range in 0.4 seconds: 4050 metres per second for the stock PPC's 1620-metre long range, so a target at 540 metres is struck after 0.133 seconds. A miss flies on at the same speed. Splash is half the damage rounded down, 50 for the stock PPC, rolled in 10-point chunks (any remainder last), each uniformly among the adjacent locations except the head, so every hit deals 100 + 50 in total. Electro-interference adds +2 to the target's own target numbers for 5 seconds, restarting on each further hit. Every part uses bluish violet (0.6, 0.4, 1). The beam has a 0.3-metre halo with a core at 40% blended halfway to white; it holds for 0.4 seconds, past the bolt's landing at any range, and fades out by 0.5 seconds. The bolt is a 2.7-metre violet glow around a core of half that, white tinted 30% violet. The burst lasts 0.8 seconds: a violet glow swells from 5.4 to 13.5 metres at 80% opacity, fading linearly; a violet-tinted white flash reaches 6.75 metres over its first 30%. Six radial arcs reach about 15–23 metres, plus one arc per location the splash reached; each is three jagged segments, blended 60% toward white, re-randomized every 0.06 seconds.

LRM flight time is planar distance / 525 metres per second: about 1.03 seconds at the 540-metre minimum range and 3.6 seconds at the 1,890-metre long range. SRMs launch at 168 metres per second and accelerate steadily at about 462 metres/s², calibrated so the 810-metre long range takes as long as an LRM over that distance, about 1.54 seconds; flight time is `(−168 + √(168² + 2 · acceleration · distance)) / acceleration`, about 0.78 seconds at 270 metres. Indirect fire adds +1 to the target number. A salvo ripples out over 0.25 seconds (or 40% of the flight, if shorter), and each missile's flight is shortened to land on the impact step. Each missile accelerates steadily over its own launch-to-arrival window: an LRM leaves at 40% of its average speed and dives in at 160%, reaching its apex after about 64% of the flight; an SRM follows its launch speed and acceleration, including misses out to their ground point. SRMs fly flat. A direct LRM arc peaks at 15% of the planar range; an indirect arc peaks at 35% or high enough to pass blocking terrain by 90 metres, up to the full range. Each missile's skitter is two lateral and two vertical sine waves of 1.5–4 cycles per flight, with amplitude 5.6–11.25 metres; it builds over the first quarter of the path and fades to zero over the last fifth. The flame trail spans the last 0.04 seconds of flight, so it lengthens as a missile speeds up. It is additive and runs from white at the missile through yellow (1, 0.9, 0.2) at its middle to orange (1, 0.45, 0.05) at the tail, its opacity fading linearly to zero at the tail. Each impact bursts to 3.75 metres over 0.3 seconds. Missed missiles land 15–37.5 metres beside and 15–45 metres beyond their rolled location.

The muzzle flash lasts 0.1 seconds. The bullet is a capsule of brass yellow (1, 0.85, 0.3), one shell diameter wide and five shell radii long, with no trail. Each fireball's white core swells to 45% of the explosion radius over the first 40% of its duration while fading out.

A destroyed location's explosion lasts 1.1 seconds. Its white flash reaches 18.75 metres over the first quarter; the orange fireball swells from 11.25 to 37.5 metres, fading as 1 − t²; the dark-red bloom swells from 18.75 to 45 metres at 60% opacity, fading linearly. Five yellow debris blobs fly 45–67.5 metres outward and upward on directions seeded from the shot, shrinking from 4.5 to 2.25 metres. Misses fly on up to 300 metres beyond long range from the muzzle before fading over 0.1 seconds.

The extra location roll is drawn only for laser hits on a target in motion. Beam halo radii are 0.3 (small), 0.6 (medium) and 0.9 (large and ER large) metres, with a core at 40% of the halo. Halos are pure red (small), pure green (medium) and deep blue (large and ER large); the core blends that color 30% toward white so additive beams keep their color. The beam reaches full length 0.1 seconds after discharge, sweeps linearly from the first location to the second over the 1-second burn (holding still when they are the same) and fades over 0.15 seconds after the burn. A miss is solid to long range, then its halo and core fade linearly to invisible at 1.5 × long range from the muzzle: small 270 → 405 m, medium 810 → 1215 m, large 1350 → 2025 m, ER large 1710 → 2565 m. Terrain and rocks cut a miss short. Where it strikes, a charcoal (0.08, 0.06, 0.05) scorch disc of six times the halo radius (1.8, 3.6 and 5.4 metres) lies 0.3 metres above the surface, with a radial falloff from 85% opacity at its center to transparent at its rim. On a rock face it faces back along the beam. It holds full strength while the beam burns, then fades linearly over 5 seconds. A side's defeat starts a 3-second wind-down before the outcome.

Hit chance is the exact probability of 2d6 meeting this threshold:

`Gunnery + 3 (real-time aim modifier) + range + minimum-range penalty + attacker movement + target movement + vegetation + attacker heat (+ 1 indirect) (+ 2 electro-interference)`

- Range: +0 short, +2 medium, +4 long; no shots beyond long range.
- Minimum range: `ceil((scaled minimum − distance) / 90)` when inside minimum, preserving penalty severity at equivalent relative distances.
- Vegetation contributes +1 per intersected patch, including target vegetation, capped at +2. Indirect LRM fire counts the spotter's line to the target instead. It does not block detection or reduce damage.
- Each moving mech contributes +1 walking or +2 running; stationary contributes zero. An attacker travelling in reverse contributes +2 whatever its mode; a reversing target contributes as its mode does.
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

## Jumping

| Value | Default |
| --- | --- |
| Range | Effective jump MP × 45 m (half a weapon-range hex), × 0.5 with a lost leg; a Jenner jumps 225 m |
| Minimum jump | 15 m |
| Flight time | 1.5 s + distance / 60 m/s (5.25 s for 225 m) |
| Arc | Parabola peaking max(20 m, 0.2 × distance) above the line between takeoff and landing ground, raised to clear every rock and ridge beneath it by 6 m |
| Heat | max(3, ⌈distance / 45 m⌉) at takeoff; refused if heat would reach the policy limit (20 Normal, 30 Aggressive) |
| Recharge | 10 s from landing |
| Hit difficulty | +3 for an airborne attacker or target, replacing its walk/run/reverse modifier |

Landing points are searched from the clamped click outward along a golden-angle spiral in 4.5 m rings, up to 160 candidates within range. Enemy AI jumps toward its movement goal, capped at range, when the goal is at least 60 m away and either its waypoint route is at least 1.5 times the straight distance or it has detected but cannot see its Attack target.

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

**Climate.** Every second, heat moves toward the biome's ambient temperature by (ambient − heat) ÷ 5 per 10-second heat clock, that is 0.02 × (ambient − heat) per second. Cooling efficiency multiplies sink output only. Hand-built test terrain has no climate: no exchange and 100% efficiency.

| Biome | Ambient | Cooling efficiency | At heat 0 | At heat 20 |
| --- | --- | --- | --- | --- |
| Valley | 10 | 100% | +0.2 heat/s | −0.2 heat/s |
| Arctic | −20 | 150% | heat stays 0 | −0.8 heat/s |
| Lunar | −50 | 25% | heat stays 0 | −1.4 heat/s |
| Highlands | 0 | 75% | heat stays 0 | −0.4 heat/s |
| Desert | 20 | 85% | +0.4 heat/s | 0 |
| Tropical | 15 | 80% | +0.3 heat/s | −0.1 heat/s |
| Grassland | 10 | 110% | +0.2 heat/s | −0.2 heat/s |
| Archipelago | 5 | 120% | +0.1 heat/s | −0.3 heat/s |
| Volcanic | 30 | 80% | +0.6 heat/s | +0.2 heat/s |
| Toxic | 15 | 60% | +0.3 heat/s | −0.1 heat/s |

The "At heat" columns are ambient exchange only, before sinks. Ten single sinks shed 1 heat/s at 100%, 1.5 in the Arctic and 0.25 on Lunar; in the Volcanic biome they shed 0.8, just above the 0.6 ambient pull at heat 0. In the Valley an idle mech without sinks settles at 10 heat, one with a single sink at 5, and one with two or more sinks at 0. The numbers are provisional.

Friendly ground labels show a 30-pixel heat bar (full at the shutdown threshold) under the callsign, and cards show current heat; selected details show heat band, policy, sink cooling × efficiency, and ambient temperature with its current exchange. Normal/Aggressive controls apply to selected units including thermal shutdowns, with a Mixed group readout when appropriate. Exact enemy heat and cooling appear only at Detailed contact tier and disappear when that tier is lost. Water provides no cooling bonus. Heat damage, ammunition explosions and manual venting remain deferred.

Five-to-ten minutes remains a provisional ordinary-engagement target. With the expanded ranges and Normal heat policies, the seeded flat-ground mixed-lance check finishes in 761 simulated seconds; one-on-one Jenner, Griffin and Awesome checks finish in 121, 176 and 183 seconds. The seeded natural-terrain (Valley) mixed-lance check, with enemy AI jumping, finishes in 596 seconds. These seeded results are repeatable comparisons, not guarantees for every fight. One-on-one light-mech fights can finish substantially sooner than lance battles.

## Calculation reuse

Navigation stores immutable per-edge movement costs alongside the terrain's connectivity graph. A* rents its cost and predecessor buffers per query and returns independent waypoint lists; concurrent queries share no writable search state. Firing-position searches limit candidates to the weapon-range bounding box and reuse a pooled sorting buffer. Equal scores still favor the original row-major node order.

The weapon sidebar evaluates each distinct main/rear target pair's heat volley once per refresh, sharing it across weapon statuses. Preview-only targets retain the existing mount-specific fallback. Results are discarded after that refresh so paused edits, cooldowns, contacts, heat and damage are reflected immediately; previews never consume random draws.

The terrain and heat checks cover stable route/firing choices, concurrent searches, allocation budgets, batched status equivalence and unchanged firing events. Allocation checks warm the buffer pools first and avoid wall-clock timing assertions.

## Interaction defaults

- Enter a terrain seed or use New terrain before launching; the chosen seed reproduces terrain only.
- Preselect the current eligible hangar mech, otherwise the first eligible mech. Choose one to four; unavailable entries explain why. Assignments remain in the hangar.
- Click / drag selects friendlies; Shift adds or removes selection. Right-click ground moves, a detected enemy attacks, or a friendly becomes the Follow leader. Orders issue on release only if the pointer stays within five screen pixels of its press position; small accidental movements still count as a click. Last-known markers are stationary ground destinations.
- Every mech click and hover shares one camera ray against the visible animated model bounds, padded by 0.08 local units. These envelopes include limb gaps, follow facing and zoom, and shrink with lost limbs. The nearest envelope wins overlaps regardless of allegiance, with terrain blocking hidden portions. Hover adds 1.3 pixels to the team outline and blends its color halfway to white, while retaining the gold selection ring. UI overlays, menus, drags and lost contacts suppress hover.
- Follow uses normal pathfinding to keep about 90 metres from its leader, choosing nearby free space when another follower occupies its approach. It waits when close enough and resumes when the leader moves. The clicked leader keeps its own order if selected; disabled or destroyed leaders cause followers to Hold. Movement, firing settings and priority targets are retained.
- Set priority target arms a separate right-click action; priority alone never causes pursuit. Friendly clicks keep that mode armed and issue no Follow, and Alt/Ctrl modifiers change no movement mode. Stop (Z) clears the active order while preserving priority.
- Alt+right-click sets Walk and Ctrl+right-click sets Run before issuing the ordinary order; Ctrl wins if both are held. X/C set Walk/Run and G/F set Hold fire/Fire at will for active selected units, with battlefield focus only.
- Group order, movement, firing, priority and distance edits affect active selected units. Weapon controls and location values show the primary unit. Unselected mode buttons indicate mixed settings; mixed distances have a tooltip.
- Space pauses with battlefield focus; the pause button always works. WASD/arrows pan across the ground relative to camera facing, starting and stopping immediately with normalized diagonal speed. Speed is half the orthographic zoom size in metres per second, five times that while Shift is held. Middle-drag also pans; wheel zoom and Q/E or Shift-middle yaw rotation remain available. Camera controls work paused with battlefield focus, but not while a menu or text field has focus.
- Right-drag orbits the center-view ground intersection. Rotation starts only after moving more than five screen pixels from the press position; threshold movement is discarded to avoid a jump. A drag never issues an order, even if it returns to its start. Sensitivity is 0.25° per screen pixel without smoothing: upward drag raises the camera, rightward drag orbits clockwise from above. Elevation is limited to 15°–85° downward. WASD can translate the orbit point during rotation; the point stays within the arena bounds.
- The cursor is hidden and captured during right-drag, allowing continuous rotation at window edges. Release restores its mode and press position. Menus, loss of window or battlefield focus, and leaving battle cancel the gesture and release capture. Home restores arena center, default elevation (~49°), zero yaw and orthographic size 6,600 metres; zoom ranges from 180 to 8,400. A new deployment starts at ground focus (0, 1,320), default elevation, zero yaw and size 1,500.
- Escape suspends combat. Resume restores the preceding running/paused state. End Simulation shows an ended damage summary; results return to the original hangar selection and tab.
- Enemy inspection at full range reveals only the chassis model. Within half range it also shows exact armor, rear armor, internals and effective equipment condition/availability. The side shares its best current observation; live details disappear when all close observers lose them. Physical damage remains visible.
- Inspection retains the friendly command selection and draws separate unions of target-specific identification (blue) and detailed-status (gold) ranges for all selected active mechs. Otherwise show the union of 100-ton identification ranges and label the 20–100-ton detection span. Only exposed outlines are drawn: no fill or internal overlap arcs; separated regions and uncovered holes remain. Disabled/destroyed mechs contribute no ranges. Multiple active contributors use “Selected mechs” with minimum–maximum ranges for each displayed tier; one active contributor keeps the single-mech readout. Terrain can block contacts within these boundaries; detection remains shared by the entire side independently of selection.
- Pilot skills are rated 2–6. Each improvement from 6 costs one point, with six points for regulars and eight for veterans. Fixed catalog allocations include Sensors specialists; no allocation screen or progression is added.
- Procedural 3D mech stand-ins have faction colors, facing indicators, selection/priority rings, order lines and shot/hit effects. Team-colored ground lines show leg facing; longer gold lines with tip dots show torso facing. Weapon arcs and incoming torso armor use those simulation headings.

## Validation

`./scripts/mech test combat-sim` runs the combat and terrain checks, including twist rates/limits, tracking, firing arcs, rear targets, directional damage transfer, heat rates, sink loss, exact policy budgets, optimal volley selection, threshold penalties, shutdown/restart, flamer stacking and simultaneous heat resolution. The optimizer is checked against exhaustive subset selection on generated small loadouts. The `combat-layout` group (also part of `combat-ui`) includes group heat-policy controls, editing during shutdown and enemy heat masking at both window sizes.

`./scripts/mech test` exercises seeded generation, connected spawns, slope/rock clearance, terrain detours, cover, sight, water movement, generated-map engagements, terrain picking and seed controls alongside the ten acceptance scenarios with deterministic model checks and actual mouse/keyboard input. `./scripts/mech capture` repeats the suite with graphical rendering and saves deployment, combat, minimum-window, menu and report captures in `TestResults/`. Linux checks do not establish native Windows/macOS runtime verification.
