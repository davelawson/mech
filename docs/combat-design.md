# Combat design

Status: confirmed October 25, 2026. The initial design interview is complete. The first slice uses procedural 3D models, with a custom AWESOME and a shared location/damage contract; [model and paperdoll details](mech-models.md) record the confirmed visual design, and [implementation defaults and tuning](combat-tuning.md) document numerical choices. Explicitly deferred features remain outside this slice.

## Established direction

- Tactical combat runs in real time with pausing.
- The player commands deployed BattleMechs through movement orders, attack orders, priority targets, and stances to defeat an opposing force or achieve battlefield objectives. An attack order directs a MechWarrior to path as necessary to reach and attack the specified target. A priority target instead biases target selection when an opportunity presents; it does not itself order pursuit. Each mech has one active order, replaced by a new order, and a separately stored persistent priority target. Moving permits opportunistic firing without diversion from the destination; an explicit attack order takes precedence over target preference. The first slice provides Fire at will and Hold fire settings; additional stances are deferred.
- The broader battlefield design uses continuous positions in three dimensions, with geometry governing movement and sight and understandable abstractions for cover and vegetation. The terrain extension adds procedural natural terrain with elevation, solid obstacles, vegetation and shallow water. Buildings remain deferred.
- Movement is continuous and weapons have independent cooldowns, without shared combat rounds. Initial resolution draws on basic BattleTech rules, including hit chances, damage, heat, critical hits, Piloting checks, and weapon ranges and damage. The first slice retains hit resolution, weapon ranges and damage, and location armor/internal structure; criticals, heat, ammunition consumption, falls, and MechWarrior injury are explicitly deferred. Hit chances use probability rolls influenced by Gunnery, weapon range bands, and both mechs’ movement. Damage is allocated to randomly selected body locations, reducing the incoming-facing armor before passing excess damage into that location’s internal structure. Torso twisting, firing arcs and rear armor follow the confirmed extension below. Exact hit modifiers and hit-location weights remain tuning work. Further damage to a destroyed location transfers inward using the explicit paths below.
- Tabletop fidelity is not a permanent constraint; mechanics may evolve further to exploit real time.
- Enemy visibility depends on the observing pilot's Sensors and the target's tonnage. Sensors 6 detects a 100-ton target at 25 hexes; Sensors 2 doubles that to 50, with linear interpolation. Target mass multiplies that radius linearly from 0.5 at 20 tons to 1.0 at 100 tons. Full radius identifies only the chassis model; within half radius, exact armor, rear armor, internals and effective equipment condition/availability become visible. Physical damage and movement remain visible at either tier. Each side shares its best current observation from an active observer. Leaving detailed range hides live condition immediately; losing all detection retains a stationary last-known position. Rocks and hills block either tier. Shared identification permits firing when weapons reach and the shooter's own line of fire is clear. Enemy pilots obey identical rules.
- The first playable slice includes only BattleMechs on both sides, on a procedurally generated natural battlefield. Its objective is defeat of the opposing force by destruction or disablement. The simulation has no tactical retreat mechanic; the combat menu permits ending the simulation.

## Orders, firing, and pacing

- Attack orders attempt to establish and maintain a player-adjustable preferred engagement distance, with a default based on the mech's loadout.
- Follow orders continuously track an active friendly mech using normal pathfinding and collision clearance, waiting about three hexes away and resuming when the leader moves. They retain each follower's movement mode, firing setting, and persistent priority target. Followers may fire opportunistically without diverting from following. A group order leaves the clicked leader's own orders unchanged, even when selected. Follow persists until replaced; disabling or destroying the leader switches its followers to Hold.
- MechWarriors automatically choose and fire ready, enabled weapons. The player may enable or disable individual weapons. Broader combat includes configurable heat policies, with a normal policy avoiding predictable overheating and an aggressive policy permitting more risk; heat and these policies are outside the first slice.
- Provisional pacing targets are five to ten minutes for an ordinary engagement, excluding pauses, and roughly ten-second cycles for major weapons. These are tuning targets, not settled per-weapon statistics.
- Manual pause allows inspection and all normal commands. Orders issued while paused take effect when time resumes, without a separate planning-phase commitment.
- The first slice provides pause and normal speed. Additional speeds and automatic pause triggers are deferred.
- An explicit Attack order authorizes firing at its target even with Hold fire set. Hold fire still suppresses opportunistic shots at other enemies; individually disabled weapons remain disabled regardless of orders.
- Mechs without active orders hold position and fire opportunistically according to their firing setting and priority target; they do not autonomously pursue enemies. Completing a move returns a mech to this behavior.
- Losing detection of an attack target sends the attacking mech to the last-known position without tracking hidden movement. Reacquiring that target resumes pursuit; otherwise arrival ends the order. Opportunistic fire at other detected enemies is permitted during this movement.

## Movement and damage in the first slice

- Ground movement includes walking and running. Running is faster and reduces firing accuracy. Jumping is deferred.
- Walking is the initial default; a new movement order retains each mech's chosen movement mode.
- Group Move assigns separate, non-overlapping destinations around the clicked point. A formation system is deferred.
- When preferred engagement distance cannot be achieved, move toward the nearest achievable position within the arena while avoiding occupied space. Destroyed mechs immediately stop obstructing movement and Follow destinations; disabled mechs remain solid. Wrecks remain visible and selectable/inspectable. Continue firing whenever permitted and in range; preferred distance is a goal, not a firing prerequisite.
- Mechs occupy space and navigate around one another, without pushing or collision damage.
- Hits select among Head, Center Torso, Left Torso, Right Torso, Left Arm, Right Arm, Left Leg, and Right Leg.
- Damage reduces the selected location's front or rear armor according to incoming direction, then its internal structure. Only torsos have separate rear armor; head and limbs always use ordinary armor.
- No criticals, heat, ammunition consumption, falls, or MechWarrior injury/casualties are simulated. Location-loss and defeat rules are specified below.
- Show the selected friendly mech's calculated hit chance against a detected target.
- Resolve hit and damage when a weapon fires. Projectile and beam effects illustrate that result; projectiles do not independently collide or intercept. Friendly fire is omitted. Firing arcs follow the torso-twisting rules below.
- Each weapon discharge makes one hit roll and applies one damage packet, including missile launchers. Missile clustering and more detailed weapon-specific behavior are deferred.
- Each enabled weapon fires independently when ready and a valid target is available. Weapons start ready, cooldown begins when fired, and cooldown progresses only while unpaused.

### Damage transfer

Further damage to a destroyed location transfers according to these paths:

| Destroyed location | Transfer destination |
| --- | --- |
| Left or right leg | Center torso |
| Left arm | Left torso |
| Right arm | Right torso |
| Left or right torso | Center torso |

Excess damage from the blow that destroys a location transfers immediately. Transferred damage uses the original incoming attack bearing against torso facing to select front or rear armor whenever it reaches a torso, before reducing internal structure. Continue along the path if the receiving location is already destroyed. A leg transfers directly to the center torso, without passing through a side torso.

### Location loss and defeat

- A location is destroyed when its internal structure reaches zero; its mounted weapons cease functioning.
- Destroying a side torso also destroys its attached arm.
- One destroyed leg halves movement speed; both destroyed legs immobilize the mech.
- Head or center-torso destruction destroys the mech.
- Loss of both legs or all usable weapons disables the mech. Player weapon enable/disable settings do not themselves constitute physical weapon loss.
- Destroyed and disabled mechs both count as defeated and no longer participate in combat.
- Individual component criticals and MechWarrior casualties remain outside this simulation.

## Automatic targeting

- Prefer the explicit attack target when engageable, then the priority target when engageable, then the nearest detected enemy within an enabled weapon's range.
- Keep the current target until it becomes invalid or a higher-priority choice is available.
- Automatic target selection does not change a movement order's destination.

## Simulation entry and battlefield setup

- Add a **Simulated Deployment** button to the mech hangar to launch this first combat slice.
- This is a standalone simulation using existing mech and MechWarrior definitions. It reports damage and outcome without changing the campaign; launching from an accepted mission and applying permanent results are deferred.
- Use a bounded 96 × 96 arena with procedural natural terrain and authored opposing starting positions, initially outside detection range. Keep spawns clear and connected. Expose a reproducible terrain seed independent of combat rolls.
- Start paused and support one to four mechs per side. There is no deployment-placement phase.
- Every deployment goes through a force-selection panel, including Simulated Deployment; this is also a general rule for eventual campaign deployments.
- Select one to four operational roster mechs with healthy, assigned MechWarriors. Show why unavailable mechs cannot participate. Assignments remain managed in the hangar.
- Copy selected mechs' current condition, equipment, and assigned MechWarrior skills into the simulation.
- Simulated opposition is an undamaged mirror of the selected force, matching mech types and MechWarrior skills.
- Enemy AI uses the same detection and combat rules. It advances toward the arena center until contact, then issues attack orders against the nearest detected enemy and maintains preferred engagement distance. Coordinated flanking and privileged hidden-position knowledge are excluded.

## Simulation completion and exit

- Stop combat when all mechs on a side are defeated and show an outcome and damage summary. Defeating only the enemy force is victory; losing only the friendly force is defeat; eliminating both at the same simulation instant is a draw.
- Return to the same hangar selection after the summary.
- The combat menu offers **End Simulation** at any time; there is no tactical retreat mechanic in this slice.
- Simulation causes no campaign time advancement or permanent consequences. It does not save in-progress battles.
- Resolve shots due at the same simulation instant before deciding the outcome, so processing order does not determine victory.

## Battlefield controls

- Left-click selects, drag selection selects multiple friendly mechs, right-click ground issues Move, right-click an enemy issues Attack, and right-click a friendly issues Follow.
- Every direct mech click uses the same padded envelope of the mech's visible model geometry, including gaps between limbs, scaled with the model and zoom. Overlaps resolve to the frontmost envelope regardless of allegiance; hills and rocks block hidden portions. Drag selection retains its center-point rule.
- Hover previews that same mech with a brighter, thicker team-colored outline. It preserves the selected unit's gold ground ring and never changes selection, inspection, or orders. UI overlays, menus, camera gestures and loss of enemy detection suppress hover; the outline remains depth-tested against terrain.
- Priority targets have a separate explicit control, rather than sharing Attack's meaning. While choosing one, a friendly right-click leaves priority targeting armed and issues no Follow order.
- Escape opens the combat menu.
- Combat explicitly overrides the existing office/subscreen right-click-back behavior: right-click remains a battlefield command while in combat.
- Use an angled overhead camera with pan, zoom, and rotation.
- Provide a friendly-unit selection bar and selected-unit panel showing orders, movement mode, firing setting, preferred engagement distance, weapon readiness, and armor/internal structure by location.
- Enemy inspection retains the friendly selection. Model-only contacts show the chassis name; detailed contacts add exact location values, the shared paperdoll and effective systems status. Inspection draws the union of identification ranges (blue) and, separately, half-range status ranges (gold) of all selected active friendlies for that target. Without an inspected enemy, draw the union of their 100-ton identification ranges and label the 20–100-ton detection span. Draw only exposed boundary arcs, preserving separate regions and uncovered holes, with no fill or internal overlap arcs. Disabled and destroyed selections contribute nothing. With multiple active contributors, label the readout “Selected mechs” and show minimum–maximum contributor ranges for each displayed tier; retain the single-mech readout when only one active contributor remains. These are distance boundaries; terrain may still block observation, and side-wide detection continues to use all active observers regardless of selection.

## Existing campaign constraints

- The setting is BattleTech, initially 3025–3049.
- Initial deployments contain one to four operational BattleMechs, without making this a permanent deployment limit.
- Deployment commits mech condition, equipment, assigned MechWarriors and skills, mission objectives, and opposition.
- Combat reports battlefield facts. Strategy owns contractual payment, penalties, and repair quotes. See [the strategy/combat boundary](adr/0001-strategy-and-combat-boundary.md).
- Objective outcome, recovered mech condition, unrecoverable mechs, and MechWarrior casualties remain separate results.
- Missions consume their declared campaign duration, initially one full day, including retreat. Tactical battle time is distinct from this campaign duration.

## Numerical authoring and tuning

- Begin with the existing stock armor, internal structure, weapon damage, and weapon range data.
- Exact hit modifiers, hit-location probabilities, arena dimensions, movement scaling, default engagement distances, and weapon cooldowns remain authoring and tuning work. Detection distances and pilot budgets follow the confirmed Sensors design below.
- Document explicit numerical defaults during implementation and tune toward five-to-ten-minute engagements, excluding pauses. Roughly ten-second cycles for major weapons are a provisional starting target.
- These deliberately simplified rules take precedence over tabletop fidelity; numerical tuning must not silently introduce deferred mechanics.

## Deferred beyond the first slice

- Buildings, destructible terrain, deep water, and advanced cover-seeking tactics.
- Infantry, vehicles, aircraft, and other combatant types.
- Critical hits, heat and heat policies, ammunition consumption, falls, jumping, and MechWarrior casualties.
- Missile clustering, physical projectile collision, friendly fire, melee, called shots, deliberate ammunition dumping, and other special actions.
- Separate sensor/visual contacts, additional stances, formations, coordinated enemy tactics, additional time speeds, and automatic pause triggers.
- Deployment placement, an opponent editor, tactical retreat/recovery mechanics, and saving in-progress simulations.
- Campaign mission launch, persistent battle consequences, and other objective types.

## Acceptance scenarios

1. Open **Simulated Deployment** from the hangar, select one to four eligible mechs, and start paused against an undamaged mirrored force. Ineligible roster entries explain their unavailable state.
2. Issue and replace movement/attack orders while paused; resume to execute them. Group movement uses separate destinations, and walking/running modes persist.
3. Demonstrate that Attack causes pursuit and preferred-distance positioning, while assigning a priority target alone never causes movement. A moving mech fires opportunistically without diverting from its destination.
4. Verify automatic target precedence, weapon enable/disable controls, independent cooldowns, and Hold fire's suppression of opportunistic shots while permitting an explicit attack on its target.
5. Share a detected target across friendlies; allow a friendly outside its own detection radius to shoot within weapon range. Lose all detection, retain a stationary last-known marker, and pursue that position without tracking hidden movement.
6. Damage front/rear torso armor according to torso facing and ordinary head/limb armor. Transfer excess and subsequent damage along the specified paths, including legs directly to center torso, preserving the incoming bearing and consuming the receiving armor face before structure.
7. Demonstrate destroyed-location weapon loss, side-torso/arm dependency, one-leg movement reduction, and the distinct destruction/disablement conditions. Weapon toggles alone must not disable a mech as a battle casualty.
8. Keep mechs inside the arena, avoid overlap, and continue valid fire when an engagement-distance goal cannot be reached. Enemy orders obey the same visibility and combat rules.
9. Resolve victory, defeat, and simultaneous elimination as the specified outcomes. End Simulation remains available before completion.
10. Return to the same hangar selection with a damage/outcome summary and no changes to roster condition, equipment, assignments, MechWarriors, campaign date, or campaign finances.

## Design review status

The user confirmed shared understanding of this initial combat design on October 25, 2026. The design interview is complete. Numerical authoring and deferred features retain the explicit scope above.

## Confirmed terrain design

The simulated battlefield uses reproducible, asymmetric procedural natural terrain: rocks, vegetation, hills and water. Players may enter or share a terrain seed, independent of combat randomness. Retain the 96 × 96 arena and existing starting positions, clear the spawn areas, and guarantee a traversable route between sides.

Rocks are impassable and block detection and fire. Hills affect movement and line of sight through their elevation, with no separate height bonus. Water slows movement. Vegetation slows movement and reduces hit chance when the target is within it or the shot passes through it; cap the penalty. Detection remains shared, but each attacker needs a clear line of fire.

Both sides navigate around obstacles and seek a clear firing position for blocked attack orders. A movement order to an inaccessible point resolves to the nearest reachable point and displays the actual destination. Combat information explains blocked fire and vegetation penalties. Use simple, readable visuals and one default terrain density; document and test initial numerical tuning.


## Sensors and pilot budgets (confirmed October 27, 2026)

Each pilot has Gunnery, Piloting and Sensors in the range 2 (expert) to 6 (novice). All start at 6; each step of improvement costs one point, so a skill costs `6 - rating`. Regular pilots spend exactly six points, veterans eight. Existing named pilots retain identities and quality classifications but have rebalanced fixed ratings, including Sensors specialists. These allocations resolve through existing catalog IDs in saves.

`identification radius = 25 × (1 + (6 − Sensors) / 4) × (0.5 + (target tonnage − 20) / 160)`

`detailed status radius = identification radius / 2`

Use continuous planar distances, including each boundary. Only the target's tonnage affects its signature. Current catalog weights are 20–100 tons; radius inputs are bounded to these endpoints and Sensors 2–6. A novice identifies a 20-ton mech at 12.5 hexes and a 100-ton mech at 25; an expert identifies them at 25 and 50. Both tiers require clear terrain sight and are shared by each side. Disabled and destroyed units cannot observe but remain observable targets.

Close inspection exposes existing equipment state and availability, including equipment lost with its location and weapons without usable ammunition. It adds no heat, critical-hit or other damage mechanics. Visible missing limbs, fallen wrecks and movement still provide physical clues at model-only range. Post-battle damage reports retain their existing full results. Piloting remains a roster skill pending its combat mechanics; the equal point costs establish the agreed allocation structure rather than equal current combat value.


## Torso twisting (confirmed October 29, 2026)

Player and AI mechs share independent leg facing and an upper-body waist angle bounded to ±90°. Waist rotation is measured relative to the legs: Light 90°/s, Medium 75°/s, Heavy 60°/s, Assault 45°/s. Leg turns carry the torso in world space; tracking compensates within the waist speed and angle limits. Moving mechs preserve their route even when their target cannot be reached by forward weapons. Stopped mechs turn their legs toward the target while their torso tracks independently. With no aim target, the torso recenters at its class rate. Pause freezes simulation facing exactly.

Track a detected explicit Attack target first, then a detected priority target, including outside range, behind cover and under Hold Fire. Otherwise retain the existing ordinary acquisition rules: an enabled usable forward or leg weapon must reach a detected target through clear sight; Attack, priority, retained current target and nearest target establish precedence. Firing alignment is not an acquisition condition, so turning never causes target-selection deadlock. An unreachable main target is retained while the torso clamps; movement orders are not diverted. Lost detection never supplies a live aiming position.

Forward head, torso and arm weapons fire within ±30° of torso facing. Leg-mounted weapons use ±30° of leg facing and the main target. Rear-mounted weapons use ±30° directly behind torso facing and select a separate rear target: eligible Attack target, priority, retained rear target, then nearest enemy. Rear eligibility requires detection, own clear sight, range and rear arc. Rear targeting never steers the torso. Individual weapons still require enabled/usable state and readiness. Hold Fire suppresses opportunistic shots; explicit Attack permits shots only at its ordered target.

The rear armor arc is 120° centered directly behind torso facing, including its boundaries; all other incoming bearings use front armor. Hit-location probabilities remain unchanged. Head, arms and legs use ordinary armor. Transfers into any torso reevaluate the original incoming bearing against torso facing, including transfers from limbs. Depleted rear armor exposes structure without consuming front armor. All torsos update before shots are captured, and all shots resolve before casualties, retaining simultaneous return fire.

The existing team-colored ground line indicates legs; a longer gold line and tip dot indicate torso direction. Weapon readouts label rear mounts, explain turning or outside-arc restrictions, and identify independently selected rear targets. Existing rear armor paperdolls update live, and damage totals include front plus rear armor loss.
