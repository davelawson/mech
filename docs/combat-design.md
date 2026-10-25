# Combat design

Status: confirmed October 25, 2026. The initial design interview is complete. The first slice is implemented with procedural 3D stand-ins; [implementation defaults and tuning](combat-tuning.md) document numerical choices. Explicitly deferred features remain outside this slice.

## Established direction

- Tactical combat runs in real time with pausing.
- The player commands deployed BattleMechs through movement orders, attack orders, priority targets, and stances to defeat an opposing force or achieve battlefield objectives. An attack order directs a MechWarrior to path as necessary to reach and attack the specified target. A priority target instead biases target selection when an opportunity presents; it does not itself order pursuit. Each mech has one active order, replaced by a new order, and a separately stored persistent priority target. Moving permits opportunistic firing without diversion from the destination; an explicit attack order takes precedence over target preference. The first slice provides Fire at will and Hold fire settings; additional stances are deferred.
- The broader battlefield design uses continuous positions in three dimensions, with geometry governing movement and sight and understandable abstractions for cover and vegetation. Terrain features, buildings, and vegetation belong to that broader design; the first playable slice is completely flat and empty.
- Movement is continuous and weapons have independent cooldowns, without shared combat rounds. Initial resolution draws on basic BattleTech rules, including hit chances, damage, heat, critical hits, Piloting checks, and weapon ranges and damage. The first slice retains hit resolution, weapon ranges and damage, and location armor/internal structure; criticals, heat, ammunition consumption, facing, falls, and MechWarrior injury are explicitly deferred. Hit chances use probability rolls influenced by Gunnery, weapon range bands, and both mechs’ movement. Damage is allocated to randomly selected body locations, reducing front armor before passing excess damage into that location’s internal structure. Rear armor is unused and unchanged while facing is deferred. Exact hit modifiers and hit-location weights remain tuning work. Further damage to a destroyed location transfers inward using the explicit paths below.
- Tabletop fidelity is not a permanent constraint; mechanics may evolve further to exploit real time.
- Enemy visibility depends on detection. Current sightings and last-known positions are distinct. Observable enemy damage is visible; exact enemy internal condition is hidden. The first slice uses a single detection radius: detected enemies are visible and identifiable, and enemies outside detection remain hidden. Lost contacts leave a last-known position that does not track subsequent movement. Detection is shared across the friendly force: any friendly detection reveals the enemy, and another friendly mech may fire if its weapons reach even outside its own detection radius. A last-known marker appears when all friendly detection is lost. Separate sensor and visual contacts are deferred.
- The first playable slice includes only BattleMechs on both sides, on a completely flat, empty battlefield. Its objective is defeat of the opposing force by destruction or disablement. The simulation has no tactical retreat mechanic; the combat menu permits ending the simulation.

## Orders, firing, and pacing

- Attack orders attempt to establish and maintain a player-adjustable preferred engagement distance, with a default based on the mech's loadout.
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
- When preferred engagement distance cannot be achieved, move toward the nearest achievable position within the arena while avoiding occupied space. Continue firing whenever permitted and in range; preferred distance is a goal, not a firing prerequisite.
- Mechs occupy space and navigate around one another, without pushing or collision damage.
- Hits select among Head, Center Torso, Left Torso, Right Torso, Left Arm, Right Arm, Left Leg, and Right Leg.
- Damage reduces the selected location's front armor, then its internal structure. Rear armor remains unchanged.
- No criticals, heat, ammunition consumption, facing, falls, or MechWarrior injury/casualties are simulated. Location-loss and defeat rules are specified below.
- Show the selected friendly mech's calculated hit chance against a detected target.
- Resolve hit and damage when a weapon fires. Projectile and beam effects illustrate that result; projectiles do not independently collide or intercept. Friendly fire and firing-arc restrictions are omitted.
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

Excess damage from the blow that destroys a location transfers immediately. Transferred damage reduces the receiving location's front armor before its internal structure. Continue along the path if the receiving location is already destroyed. A leg transfers directly to the center torso, without passing through a side torso.

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
- Use a bounded, flat, empty arena with authored opposing starting positions, initially outside detection range.
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

- Left-click selects, drag selection selects multiple friendly mechs, right-click ground issues Move, and right-click an enemy issues Attack.
- Priority targets have a separate explicit control, rather than sharing Attack's meaning.
- Escape opens the combat menu.
- Combat explicitly overrides the existing office/subscreen right-click-back behavior: right-click remains a battlefield command while in combat.
- Use an angled overhead camera with pan, zoom, and rotation.
- Provide a friendly-unit selection bar and selected-unit panel showing orders, movement mode, firing setting, preferred engagement distance, weapon readiness, and armor/internal structure by location.
- Enemy displays show identity and observable damage, without exact internal values.

## Existing campaign constraints

- The setting is BattleTech, initially 3025–3049.
- Initial deployments contain one to four operational BattleMechs, without making this a permanent deployment limit.
- Deployment commits mech condition, equipment, assigned MechWarriors and skills, mission objectives, and opposition.
- Combat reports battlefield facts. Strategy owns contractual payment, penalties, and repair quotes. See [the strategy/combat boundary](adr/0001-strategy-and-combat-boundary.md).
- Objective outcome, recovered mech condition, unrecoverable mechs, and MechWarrior casualties remain separate results.
- Missions consume their declared campaign duration, initially one full day, including retreat. Tactical battle time is distinct from this campaign duration.

## Numerical authoring and tuning

- Begin with the existing stock armor, internal structure, weapon damage, and weapon range data.
- Exact hit modifiers, hit-location probabilities, detection radius, arena dimensions, movement scaling, default engagement distances, and weapon cooldowns remain authoring and tuning work.
- Document explicit numerical defaults during implementation and tune toward five-to-ten-minute engagements, excluding pauses. Roughly ten-second cycles for major weapons are a provisional starting target.
- These deliberately simplified rules take precedence over tabletop fidelity; numerical tuning must not silently introduce deferred mechanics.

## Deferred beyond the first slice

- Terrain relief, buildings, vegetation, and their movement/sight/cover effects.
- Infantry, vehicles, aircraft, and other combatant types.
- Critical hits, heat and heat policies, ammunition consumption, facing/rear-armor hits, firing arcs, falls, jumping, and MechWarrior casualties.
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
6. Damage front armor and structure by location; leave rear armor unchanged. Transfer excess and subsequent damage along the specified paths, including legs directly to center torso and receiving front armor before structure.
7. Demonstrate destroyed-location weapon loss, side-torso/arm dependency, one-leg movement reduction, and the distinct destruction/disablement conditions. Weapon toggles alone must not disable a mech as a battle casualty.
8. Keep mechs inside the arena, avoid overlap, and continue valid fire when an engagement-distance goal cannot be reached. Enemy orders obey the same visibility and combat rules.
9. Resolve victory, defeat, and simultaneous elimination as the specified outcomes. End Simulation remains available before completion.
10. Return to the same hangar selection with a damage/outcome summary and no changes to roster condition, equipment, assignments, MechWarriors, campaign date, or campaign finances.

## Design review status

The user confirmed shared understanding of this initial combat design on October 25, 2026. The design interview is complete. Numerical authoring and deferred features retain the explicit scope above.
