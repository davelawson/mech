# Combat design

Status: confirmed October 25, 2026. The initial design interview is complete. The first slice uses procedural 3D models, with a custom AWESOME and a shared location/damage contract; [model and paperdoll details](mech-models.md) record the confirmed visual design, and [implementation defaults and tuning](combat-tuning.md) document numerical choices. Explicitly deferred features remain outside this slice.

## Established direction

- Tactical combat runs in real time with pausing.
- The player commands deployed BattleMechs through movement orders, attack orders, priority targets, and stances to defeat an opposing force or achieve battlefield objectives. An attack order directs a MechWarrior to path as necessary to reach and attack the specified target. A priority target instead biases target selection when an opportunity presents; it does not itself order pursuit. Each mech has one active order, replaced by a new order, and a separately stored persistent priority target. Moving permits opportunistic firing without diversion from the destination; an explicit attack order takes precedence over target preference. The first slice provides Fire at will and Hold fire settings; additional stances are deferred.
- The broader battlefield design uses continuous positions in three dimensions, with geometry governing movement and sight and understandable abstractions for cover and vegetation. The terrain extension adds procedural natural terrain with elevation, solid obstacles, vegetation and shallow water. Buildings remain deferred.
- Movement is continuous and weapons have independent cooldowns, without shared combat rounds. Initial resolution draws on basic BattleTech rules, including hit chances, damage, heat, critical hits, Piloting checks, and weapon ranges and damage. Combat includes hit resolution, weapon ranges and damage, location armor/internal structure, ammunition consumption, and the confirmed heat extension below; criticals, falls, and MechWarrior injury are deferred. Hit chances use probability rolls influenced by Gunnery, weapon range bands, movement and heat. Damage is allocated to randomly selected body locations, reducing the incoming-facing armor before passing excess damage into that location’s internal structure. Torso twisting, firing arcs and rear armor follow the confirmed extension below. Exact hit modifiers and hit-location weights remain tuning work. Further damage to a destroyed location transfers inward using the explicit paths below.
- Tabletop fidelity is not a permanent constraint; mechanics may evolve further to exploit real time.
- Enemy visibility depends on the observing pilot's Sensors and the target's tonnage. Sensors 6 detects a 100-ton target at 50 hexes; Sensors 2 doubles that to 100, with linear interpolation. Target mass multiplies that radius linearly from 0.5 at 20 tons to 1.0 at 100 tons. Full radius identifies only the chassis model; within half radius, exact armor, rear armor, internals and effective equipment condition/availability become visible. Physical damage and movement remain visible at either tier. Each side shares its best current observation from an active observer. Leaving detailed range hides live condition immediately; losing all detection retains a stationary last-known position. Rocks and hills block either tier. Shared identification permits firing when weapons reach and the shooter's own line of fire is clear. Enemy pilots obey identical rules.
- The first playable slice includes only BattleMechs on both sides, on a procedurally generated natural battlefield. Its objective is defeat of the opposing force by destruction or disablement. The simulation has no tactical retreat mechanic; the combat menu permits ending the simulation.

## Orders, firing, and pacing

- Attack orders attempt to establish and maintain a player-adjustable preferred engagement distance, with a default based on the mech's loadout.
- Follow orders continuously track an active friendly mech using normal pathfinding and collision clearance, waiting about three hexes away and resuming when the leader moves. They retain each follower's movement mode, firing setting, and persistent priority target. Followers may fire opportunistically without diverting from following. A group order leaves the clicked leader's own orders unchanged, even when selected. Follow persists until replaced; disabling or destroying the leader switches its followers to Hold.
- MechWarriors automatically choose and fire ready, enabled weapons within the selected Normal or Aggressive heat policy. The player may enable or disable individual weapons. The confirmed heat extension below specifies limits and volley selection.
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
- No criticals, ammunition explosions, falls, or MechWarrior injury/casualties are simulated. Location-loss and defeat rules are specified below; heat is specified in its confirmed extension.
- Show the selected friendly mech's calculated hit chance against a detected target.
- Resolve the hit when a weapon fires. Damage also applies then, except for the second half of a laser burn and the impact of an autocannon shell, machine gun burst, missile salvo or PPC bolt, all described below. Projectile and beam effects illustrate that result; projectiles do not independently collide or intercept. Friendly fire is omitted. Firing arcs follow the torso-twisting rules below.
- Each firing effect starts at the firing weapon's source firing marker on the shooter's model, cycling through a launcher's tubes on successive shots. A weapon without a marker in its catalog location uses an unclaimed marker of its kind elsewhere on the model (Crusader leg SRMs fire from the torso racks), or else the center of its location. Hits end at the center of the struck location on the target model. Misses pass beside and beyond that location. Both ends follow the animated models while the effect fades.
- Armor, structure and weapon damage count tenths of a stock point, so every value stays whole: stock values are ten times larger in the game, and a machine gun burst's half point is 5.
- Each weapon discharge makes one hit roll and applies one damage packet, except that a laser divides its packet across two adjacent locations and each missile of an LRM or SRM salvo rolls separately, and a PPC also splashes adjacent locations. There is no cluster table.
- Weapon categories differ mechanically and visually. Lasers, autocannons, machine guns, missile racks and PPCs are refined; flamers retain the line effect until they receive their own treatment.
- Whatever the weapon, every location an attack destroys on a friendly or detected mech sets off a large explosion at that location: a white flash, a swelling orange fireball inside a slower dark-red bloom, and yellow debris flung outward. It follows the location as the mech moves or falls as a wreck, and ages with simulation time.

### Lasers

- Small, medium, large and ER large lasers discharge as a one-second burn. A laser hit applies half its damage, rounded up, to the rolled location at discharge. Half a second later the burn applies the remainder. If the target was moving, turning or twisting its torso when struck, the burn spreads to an adjacent location, rolled at discharge from the struck location's neighbors and weighted by the hit-location table; a stationary target takes the remainder on the struck location. Destroyed locations remain eligible and transfer damage normally.
- The one hit roll covers both halves. Heat and cooldown apply at discharge, and volley selection uses full expected damage.
- The second half lands with that step's exchange, using current positions and torso facing to choose front or rear armor. It lands even if the shooter is disabled or shut down, if its laser location is lost, or if the target moves out of sight or range. It is lost if the target has been destroyed. Destroying the shooter cancels the burn, unless the second half is due in the same step that destroys the shooter.
- The combat log records the discharge, including the pending burn, and a separate burn-continuation entry when the second half lands. Misses and cancelled burns have no continuation entry.
- Beams are glowing 3D cylinders with a lighter core of the same color. Small lasers are red, medium green, large and ER large blue, with width increasing by class. The beam extends from the muzzle to full length over 0.1 seconds, sweeps steadily from the first location to the second over the whole burn, then fades briefly; on a stationary target it holds on one location. When the second half of the damage lands the beam is midway between them. A miss holds beside and beyond its first location for the whole burn. Each struck location shows an impact ring in the laser's color as the beam reaches it, while damage still applies at discharge and at half the burn. A destroyed shooter's beam vanishes at once. Effects age with simulation time, so pausing freezes them.

### Autocannons

- AC/2, AC/5, AC/10 and AC/20 fire one shell per discharge. The hit and location are rolled at discharge; a hit's damage applies when the shell lands, after the planar distance divided by the shell speed (one second at 60 hexes for every caliber). Heat and cooldown apply at discharge, and volley selection uses full expected damage.
- Impact uses the target's torso facing at that moment against the bearing from where the shell was fired. A shell always lands, even if its shooter is disabled, shut down or destroyed, or the target moves out of sight or range. A shell reaching a destroyed target explodes on the wreck without damage. Shells in flight land during the wind-down and can produce a draw.
- The combat log records the discharge with its flight time, and a separate impact entry with the actual losses for hits. Misses have no impact entry.
- A short muzzle flash marks the firing marker. The shell is a glowing yellow bullet with no trail, sized by caliber, flying nose-first straight to the struck location on the moving target. A miss flies beside and beyond its location and explodes where it meets the ground or terrain, or fades past long range plus 10 hexes. Landing shells show a fireball around a brief white-hot core, both growing in size and duration with caliber. Effects age with simulation time, so pausing freezes them.

### Machine guns

- A machine gun discharge is a one-second burst, and the gun fires a new burst every two seconds. The burst makes one hit roll, deals one damage packet of half a stock point, spends one ammunition shot and adds its heat at discharge, with one combat log entry. The stream of rounds illustrates that result.
- A hit's damage applies when the burst's first round arrives, after the planar distance divided by the bullet speed. Like shells, rounds always land, a destroyed target takes them without damage, and bursts in flight land during the wind-down. The combat log records the discharge with its flight time, and a separate impact entry with the actual losses for hits.
- The burst is a high-speed, continuous stream of tiny bright yellow and orange tracers, each a short glowing streak far smaller than an AC/2 shell, with a small muzzle flash on every round. Each round leaves the muzzle where it is when fired and flies to where its target is at that moment, so the stream sweeps with moving mechs. Hit rounds scatter slightly around the struck location and each strikes with a tiny spark, with no fireball. Missed rounds spray beside and beyond the target and vanish where they meet the ground or terrain, or fade past long range plus 10 hexes, without sparks. A shooter that is destroyed, shuts down or loses the gun stops firing at once; rounds already in the air fly on. Effects age with simulation time, so pausing freezes them.

### Missile racks

- LRM-5, LRM-10, LRM-15, LRM-20, SRM-2, SRM-4 and SRM-6 fire a salvo of one missile per rack tube for one ammunition shot. Each missile makes its own hit roll at the weapon's chance and rolls its own location, so a salvo spreads its damage. Each hit deals the stock per-missile damage and transfers normally. The salvo counts as a hit when any missile hits.
- Rolls happen at discharge; damage applies when the salvo lands. LRM flight time is the planar distance divided by the missile speed. SRMs leave the rack slower and accelerate steadily, so a shot at the SRM's long range takes as long as an LRM over that distance and shorter shots take relatively longer. Every missile of a salvo lands in the same step, against the target's torso facing at that moment and the bearing from where the salvo was fired. Like shells, missiles always land, a destroyed target takes them without damage, and salvos in flight land during the wind-down. Heat and cooldown apply at discharge, and volley selection uses the chance times the full rack damage.
- **Indirect fire.** Only LRMs fire indirectly; SRMs always need the shooter's own clear sight. LRMs can fire at a target the shooter cannot see when that target is the shooter's priority target or the target of its explicit Attack order; opportunistic shots always need the shooter's own clear sight. Indirect fire needs a spotter: an active friendly with its own clear sight to the detected target. The spotter with the lowest vegetation modifier to the target is used, then the nearest. Indirect fire adds a flat penalty and uses the spotter's vegetation instead of the shooter's; range, minimum range, movement and heat remain the shooter's. Firing arcs, Hold fire and Attack permissions apply as usual, and a blocked attacker still seeks a clear firing position. The target readout shows the spotter and penalty in place of the blocked-fire notice.
- The combat log records the discharge with missiles hit out of the rack size, struck locations, flight time, and for indirect fire the spotter (an enemy spotter is named only at Detailed contact). Debug lists every missile's roll. A separate impact entry carries the combined losses; a salvo whose missiles all miss has no impact entry.
- The salvo ripples out of the rack, missile by missile through its tubes, with a small flash at each tube. Each missile is a small exhaust glow with a short flame trail, white at the missile through yellow to orange, fading toward the tail. Every missile speeds up through its flight: LRMs climb slowly and accelerate into the dive, and SRMs accelerate steadily. SRM salvos fly flat. A direct LRM salvo flies a moderate lofted arc; an indirect one flies a high arc that clears the blocking terrain. Each missile skitters along its own smooth random path, strongest mid-flight and gathering back as it nears its impact point, then homes on its struck location on the moving target and bursts in a small explosion. Missed missiles come down beside and beyond the target and burst on the ground. The skitter is visual only and never affects simulation draws. Effects age with simulation time, so pausing freezes them.

### PPCs

- A PPC fires one bolt per discharge. The hit and location are rolled at discharge; damage applies when the bolt lands, after the same short flight time at every range. Like shells, bolts always land, a destroyed target takes them without damage, and bolts in flight land during the wind-down. Heat and cooldown apply at discharge.
- A hit deals full damage to the struck location, plus splash damage of half the weapon's damage, rounded down, shared at random among the locations adjacent to it in the laser sweep table. The head is never splashed, so a head hit puts all of it on the center torso. The splash is rolled at discharge one stock point (10 damage) at a time, any remainder last, each chunk to an equally likely adjacent location, and lands with the primary damage, using the same front/rear bearing and transferring normally.
- **Electro-interference.** A PPC hit adds a flat penalty to every attack the target makes for a few seconds. A further hit restarts the timer; it does not stack. Misses and impacts on wrecks cause none. The selected unit's readout shows the penalty and time left.
- The combat log records the discharge with its flight time nominal primary and splash damage, and where the splash lands (Detailed target), and a separate impact entry with the actual losses across all struck locations and the electro-interference. Misses have no impact entry.
- A thin bluish-violet beam flashes from the muzzle to the target at full length and holds while a glowing violet bolt races down it to the struck location on the moving target, fading out just after the bolt lands. The bolt bursts in a coruscating explosion: a violet-white flash inside a swelling violet glow, with jagged lightning arcs that flicker outward and jump to the center of each location the splash reached. A miss flies beside and beyond its location and bursts where it meets the ground or terrain, or fades past long range plus 10 hexes. The flicker is visual only and never affects simulation draws. Effects age with simulation time, so pausing freezes them.

### Ammunition

- Every ammunition-fed weapon (autocannons, machine guns, missile racks) spends one shot per discharge, hit or miss. Each bin starts with its stock shots per ton.
- A mech's intact compatible bins form one pool per ammunition kind. Each shot comes from the intact bin with the fewest shots remaining, ties in catalog order. Bins that are destroyed or in destroyed locations leave the pool.
- A weapon with an empty pool cannot fire and its readout shows Out of ammunition; volley selection ignores remaining shots. If a volley empties a shared pool, later weapons in it are withheld and stay ready. Running dry is not physical weapon loss and never disables a mech; losing every compatible bin is.
- Remaining shots appear in friendly weapon readouts and per bin in friendly inspection. Enemy ammunition is never known: close inspection shows destroyed or lost bins but no shot counts, and an enemy weapon that has only run dry still appears available. The combat log never reports ammunition.

- Ready, enabled weapons with valid targets enter heat-budgeted volley selection. Weapons start ready, cooldown begins only when fired, and cooldown progresses only while unpaused, including during thermal shutdown.

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
- Destroyed and disabled mechs both count as defeated and no longer participate in combat. Thermal shutdown is not defeat.
- When every mech on one side is defeated, a three-second wind-down begins and cannot be undone. The simulation continues without new discharges so pending effects can conclude and the player can see the final shots. The outcome is decided when the wind-down ends, so a pending burn that defeats the other side produces a draw. Pause, camera and inspection remain available, and ending the simulation manually remains immediate.
- Individual component criticals and MechWarrior casualties remain outside this simulation.

## Automatic targeting

- Prefer the explicit attack target when engageable, then the priority target when engageable, then the nearest detected enemy within an enabled weapon's range.
- Keep the current target until it becomes invalid or a higher-priority choice is available.
- Automatic target selection does not change a movement order's destination.

## Simulation entry and battlefield setup

- Add a **Simulated Deployment** button to the mech hangar to launch this first combat slice.
- This is a standalone simulation using existing mech and MechWarrior definitions. It reports damage and outcome without changing the campaign; launching from an accepted mission and applying permanent results are deferred.
- Use a bounded 192 × 192 arena with procedural natural terrain and authored opposing starting positions, initially outside detection range. Keep spawns clear and connected. Expose a reproducible terrain seed independent of combat rolls.
- Start paused and support one to four mechs per side. There is no deployment-placement phase.
- Simulated Deployment first offers **Randomized enemy** or **Mirror match**, then opens the existing force-selection and terrain setup. Canceling returns to the hangar. Every deployment goes through force selection; this is also a general rule for eventual campaign deployments.
- Select one to four operational roster mechs with healthy, assigned MechWarriors. Show why unavailable mechs cannot participate. Assignments remain managed in the hangar.
- A mirror match copies the selected mech types and assigned MechWarrior skills, with both sides fully repaired and supplied inside the simulation.
- Randomized opposition is generated on Launch using fresh candidate pools and uniformly random choices under the player's initial draft rules. Deploy the same number of enemies with the same weight categories as the selected player force, retaining their draft-assigned pilots. Generate another fresh legal draft whenever a category runs out, keeping unused pairs available for later slots. Enemy units start pristine; player units retain their current condition and ammunition in this mode. Repeating a terrain seed reproduces terrain independently of the enemy draft.
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
- Critical hits, heat damage and ammunition explosions, water cooling bonuses, ammunition consumption, falls, jumping, and MechWarrior casualties.
- Physical projectile collision, friendly fire, melee, called shots, deliberate ammunition dumping, and other special actions.
- Separate sensor/visual contacts, additional stances, formations, coordinated enemy tactics, additional time speeds, and automatic pause triggers.
- Deployment placement, an opponent editor, tactical retreat/recovery mechanics, and saving in-progress simulations.
- Campaign mission launch, persistent battle consequences, and other objective types.

## Acceptance scenarios

1. Open **Simulated Deployment** from the hangar, choose randomized opposition or a mirror match, select one to four eligible mechs, and start paused. Randomized opposition matches count and weight categories using fresh legal drafts; a mirror matches types and pilot skills with both sides pristine. Ineligible roster entries explain their unavailable state.
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

The simulated battlefield uses reproducible, asymmetric procedural natural terrain: rocks, vegetation, hills and water. Players may enter or share a terrain seed, independent of combat randomness. Retain the existing arena and starting positions (192 × 192 since [engagement spacing](#engagement-spacing-confirmed-october-29-2026)), clear the spawn areas, and guarantee a traversable route between sides.

Rocks are impassable and block detection and fire. Hills affect movement and line of sight through their elevation, with no separate height bonus. Water slows movement. Vegetation slows movement and reduces hit chance when the target is within it or the shot passes through it; cap the penalty. Detection remains shared, but each attacker needs a clear line of fire.

Both sides navigate around obstacles and seek a clear firing position for blocked attack orders. A movement order to an inaccessible point resolves to the nearest reachable point and displays the actual destination. Combat information explains blocked fire and vegetation penalties. Use simple, readable visuals and one default terrain density; document and test initial numerical tuning.


## Sensors and pilot budgets (confirmed October 27, 2026)

Each pilot has Gunnery, Piloting and Sensors in the range 2 (expert) to 6 (novice). All start at 6; each step of improvement costs one point, so a skill costs `6 - rating`. Regular pilots spend exactly six points, veterans eight. Existing named pilots retain identities and quality classifications but have rebalanced fixed ratings, including Sensors specialists. These allocations resolve through existing catalog IDs in saves.

`identification radius = 50 × (1 + (6 − Sensors) / 4) × (0.5 + (target tonnage − 20) / 160)`

`detailed status radius = identification radius / 2`

Use continuous planar distances, including each boundary. Only the target's tonnage affects its signature. Current catalog weights are 20–100 tons; radius inputs are bounded to these endpoints and Sensors 2–6. A novice identifies a 20-ton mech at 25 hexes and a 100-ton mech at 50; an expert identifies them at 50 and 100. These values include the doubling from [engagement spacing](#engagement-spacing-confirmed-october-29-2026). Both tiers require clear terrain sight and are shared by each side. Disabled and destroyed units cannot observe but remain observable targets.

Close inspection exposes existing equipment state and availability, including equipment lost with its location and weapons whose ammunition bins are all lost; ammunition depletion stays hidden. It adds no heat, critical-hit or other damage mechanics. Visible missing limbs, fallen wrecks and movement still provide physical clues at model-only range. Post-battle damage reports retain their existing full results. Piloting remains a roster skill pending its combat mechanics; the equal point costs establish the agreed allocation structure rather than equal current combat value.


## Torso twisting (confirmed October 29, 2026)

Player and AI mechs share independent leg facing and an upper-body waist angle bounded to ±90°. Waist rotation is measured relative to the legs: Light 90°/s, Medium 75°/s, Heavy 60°/s, Assault 45°/s. Leg turns carry the torso in world space; tracking compensates within the waist speed and angle limits. Moving mechs preserve their route even when their target cannot be reached by forward weapons. Stopped mechs turn their legs toward the target while their torso tracks independently. With no aim target, the torso recenters at its class rate. Pause freezes simulation facing exactly.

Track a detected explicit Attack target first, then a detected priority target, including outside range, behind cover and under Hold Fire. Otherwise retain the existing ordinary acquisition rules: an enabled usable forward or leg weapon must reach a detected target through clear sight; Attack, priority, retained current target and nearest target establish precedence. Firing alignment is not an acquisition condition, so turning never causes target-selection deadlock. An unreachable main target is retained while the torso clamps; movement orders are not diverted. Lost detection never supplies a live aiming position.

Forward head, torso and arm weapons fire within ±30° of torso facing. Leg-mounted weapons use ±30° of leg facing and the main target. Rear-mounted weapons use ±30° directly behind torso facing and select a separate rear target: eligible Attack target, priority, retained rear target, then nearest enemy. Rear eligibility requires detection, own clear sight, range and rear arc. Rear targeting never steers the torso. Individual weapons still require enabled/usable state and readiness. Hold Fire suppresses opportunistic shots; explicit Attack permits shots only at its ordered target.

The rear armor arc is 120° centered directly behind torso facing, including its boundaries; all other incoming bearings use front armor. Hit-location probabilities remain unchanged. Head, arms and legs use ordinary armor. Transfers into any torso reevaluate the original incoming bearing against torso facing, including transfers from limbs. Depleted rear armor exposes structure without consuming front armor. All torsos update before shots are captured, and all shots resolve before casualties, retaining simultaneous return fire.

The existing team-colored ground line indicates legs; a longer gold line and tip dot indicate torso direction. Weapon readouts label rear mounts, explain turning or outside-arc restrictions, and identify independently selected rear targets. Existing rear armor paperdolls update live, and damage totals include front plus rear armor loss.

## Heat buildup and dissipation (confirmed October 29, 2026)

Heat rewards deliberate bursts at the cost of future cooling time. Player and enemy mechs begin cold with Normal policy and use identical rules. Single sinks continuously remove 1 heat each per ten seconds; double sinks remove 2. Weapons generate their stock per-shot heat even on misses. Actual walking-speed travel generates 1 heat per ten seconds, actual running-speed travel generates 2, reversing counts as walking, and turning in place adds none. There is no water cooling bonus.

| Heat | Movement speed limit | Accuracy target-number modifier |
| --- | --- | --- |
| Below 10 | 100% | +0 |
| 10–below 20 | 90% | +1 |
| 20–below 30 | 75% | +2 |
| 30 or more | Shutdown | Cannot fire |

Thermal shutdown immediately stops movement, leg/torso turning, firing and sensor contribution. The mech remains solid, targetable and alive for victory checks. Players can change orders, weapon settings and policy while it is shut down. Orders and Follow relationships persist. It restarts automatically once heat falls below 10, with no extra timer. Heat cannot go below zero and has no upper cap; further flamer hits prolong a shutdown. No internal heat damage or ammunition explosions are introduced.

Normal permits firing only when resulting heat stays strictly below 20; Aggressive permits heat strictly below 30. Select the combination of ready, enabled, usable weapons with the greatest expected ordinary damage against their current targets, accounting for hit chance. Break equal-damage ties by lower heat, then catalog order. Withheld weapons remain ready. Main and independent rear targets share the same heat budget. Both sides default to Normal; no manual vent command or additional enemy aggression logic is added.

All shots at the same simulation instant use the heat penalties from the start of that exchange. Apply firing and received flamer heat after all discharges and rolls are captured, preserving simultaneous return fire. Each successful flamer hit deals its existing damage plus 2 target heat, even through armor. Misses add no target heat; multiple hits and attackers stack without a special cap.

Explicit sink component groups contribute their grouped sink count. A Destroyed group or lost mounting location removes that contribution once, including sinks in an attached lost arm. Damaged groups retain full output. Internal sinks are the stock remainder and remain available while the mech survives. Individual component criticals and engine heat remain deferred.

Friendly battlefield indicators and unit cards show heat. Selected details show current heat, band, policy and effective cooling. Normal/Aggressive controls apply per mech or to the selected group, including shut-down units, with mixed-policy feedback. Weapon readouts explain heat withholding. Exact enemy heat appears only at Detailed contact tier. Numerical implementation details and checks are recorded in [tactical tuning](combat-tuning.md#heat).

## Engagement spacing (confirmed October 29, 2026)

To reduce crowding and lengthen the approach under fire, both sides use three times every stock firing bracket and preferred engagement distance. Minimum ranges also triple, while their penalties retain the same severity at equivalent relative distances. Imported weapon specifications remain stock; combat and weapon readouts apply the multiplier.

Detection and detailed-status distances double across the board. The battlefield grows from 96 × 96 to 192 × 192 hexes, with deployment positions scaled proportionally, including eight-hex lateral spacing and opposing starts at Z = ±64. Terrain retains its density and individual feature sizes, giving approximately four times the features. Terrain, navigation, picking, camera panning and overview zoom cover the expanded field. This supersedes the earlier arena dimensions and sensor distances recorded above.

Movement speeds, firing cadence and damage retain their existing tuning for the first playtest. Group Attack retains its existing collision avoidance; deliberate spreading around targets is deferred until the range change has been playtested. See [current tuning values](combat-tuning.md).

## Tactical shortcuts and battle history (confirmed October 29, 2026)

Number keys 1–4 replace the friendly selection with the corresponding deployment slot. Shift+number toggles that slot in the selection, including removing its last member. Slots remain fixed after losses; disabled and destroyed mechs remain selectable for inspection, matching card clicks. These shortcuts clear enemy inspection, restore battlefield focus, respect combat modals, and leave numeric text entry available.

Backtick resets yaw, pitch and zoom to the starting combat view, centers the selected group's bounds, and widens zoom only as needed to fit its full models with a margin. If nothing is selected, use all friendly units without changing the actual selection. Home retains the existing arena overview. Hovering a detected enemy draws distinct dashed blue lines from every selected friendly, with continuous planar distance labeled in hexes. Range lines do not assert weapon readiness or firing eligibility and follow the existing hover, terrain and UI-occlusion guards.

Shift+backtick toggles an initially hidden left panel docked beside a narrower battlefield viewport. It contains Combat and Debug tabs. Roughly twelve ordinary entries fit at the default window size; long entries wrap and smaller windows show fewer. Both tabs retain the entire current battle with newest events at the bottom. Wheel input over the log scrolls history without zooming the camera. Each tab retains its own scroll position and follows new events only while at the bottom. Hiding the log preserves both tabs and their history. A new battle begins hidden, with empty history and Combat selected.

Record one immutable event per weapon discharge, including misses and whole missile salvos. Entries contain time, side/slot identity, shooter and target names, weapon and mount, firing heat contribution and shooter exchange-end total, range, percentage chance and actual random draw, HIT/MISS, nominal delivered damage, actual front/rear armor and internal-structure losses per location, lost components/systems, and newly disabled/destroyed targets. Place heat between weapon and range. Successful flamers also report added target heat and target exchange-end heat. Keep the existing probability comparison and random-number sequence; lower draws hit. Damage attribution is captured during application, including transfers and attached-arm losses, before subsequent shots can alter condition. Exchange-end heat includes simultaneous incoming flamer heat. Individual component criticals remain deferred.

Debug exposes every event and all recorded fields. Combat captures sensor visibility when the shot happens: friendly information is always detailed; Identified enemies expose chassis and slot, while exact weapon identity, variant, condition, system losses, heat and enemy attack percentages require Detailed contact. Friendly shots and detected enemy shots appear. Hits on friendly units from hidden enemies appear as Unknown attacker, without enemy identity, source range or hidden details; hidden misses are omitted. Later detection never reveals old hidden information. Stable F1–F4 and E1–E4 labels distinguish mirrored names.
