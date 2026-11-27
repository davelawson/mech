# Combat design

Status: confirmed October 25, 2026. The initial design interview is complete. The first slice uses procedural 3D models, with a custom AWESOME and a shared location/damage contract; [model and paperdoll details](mech-models.md) record the confirmed visual design, and [implementation defaults and tuning](combat-tuning.md) document numerical choices. Explicitly deferred features remain outside this slice.

## Established direction

- Tactical combat runs in real time with pausing.
- The player commands deployed BattleMechs through Move and Follow orders, priority targets, [Maintain Range](#maintain-range-and-priority-targets-confirmed-november-17-2026) and stances to defeat an opposing force or achieve battlefield objectives. Players control their mechs' movement: there is no player Attack order. A priority target biases target selection when an opportunity presents, and a mech maintaining range keeps its preferred distance from it while it has no Move or Follow order. Each mech has one active order, replaced by a new order, and a separately stored persistent priority target. Moving permits opportunistic firing without diversion from the destination. Enemy AI and patrols use an internal Attack order: it directs a MechWarrior to path as necessary to reach and attack the specified target, and takes precedence over target preference. The first slice provides Fire at will and Hold fire settings; additional stances are deferred.
- The broader battlefield design uses continuous positions in three dimensions, with geometry governing movement and sight and understandable abstractions for cover and vegetation. The terrain extension adds procedural natural terrain with elevation, solid obstacles, vegetation and shallow water. Buildings remain deferred.
- Movement is continuous and weapons have independent cooldowns, without shared combat rounds. Initial resolution draws on basic BattleTech rules, including hit chances, damage, heat, critical hits, Piloting checks, and weapon ranges and damage. Combat includes hit resolution, weapon ranges and damage, location armor/internal structure, ammunition consumption, and the confirmed heat extension below; [criticals](#critical-hits-confirmed-november-14-2026) and [knockdowns](#knockdowns-confirmed-november-15-2026) are specified below; MechWarrior death follows head destruction. Hit chances use probability rolls influenced by Gunnery, weapon range bands, movement and heat. Damage is allocated to randomly selected body locations, reducing the incoming-facing armor before passing excess damage into that location’s internal structure. Torso twisting, firing arcs and rear armor follow the confirmed extension below. Exact hit modifiers and hit-location weights remain tuning work. Further damage to a destroyed location transfers inward using the explicit paths below.
- Tabletop fidelity is not a permanent constraint; mechanics may evolve further to exploit real time.
- Enemy visibility depends on the observing pilot's Sensors and the target's tonnage. Sensors 6 detects a 100-ton target at 1,500 metres; Sensors 2 doubles that to 3,000, with linear interpolation. Target mass multiplies that radius linearly from 0.5 at 20 tons to 1.0 at 100 tons. Full radius identifies only the chassis model; within half radius, exact armor, rear armor, internals and effective equipment condition/availability become visible. Physical damage and movement remain visible at either tier. Each side shares its best current observation from an active observer. Leaving detailed range hides live condition immediately; losing all detection retains a stationary last-known position. Rocks and hills block either tier. Shared identification permits firing when weapons reach and the shooter's own line of fire is clear. Enemy pilots obey identical rules.
- The first playable slice includes only BattleMechs on both sides, on a procedurally generated natural battlefield. Its objective is defeat of the opposing force by destruction or disablement. The simulation has no tactical retreat mechanic; the combat menu permits ending the simulation, or withdrawing from a mission.
- A mission deployment ([Quick Launch](design.md#quick-launch-confirmed-november-9-2026)) fields the selected mechs as they stand against the mission's opposition, stock and fully supplied, on the mission system's biome. The sides may differ in size. Every battle opens with a [drop phase](#drop-phase-confirmed-november-23-2026) in which the players choose where their lances land.

## Orders, firing, and pacing

- Each mech has a preferred engagement distance, with a default based on its loadout. A player mech keeps it from its priority target through Maintain Range; an enemy's Attack order keeps it from the attack target.
- Follow orders continuously track an active friendly mech using normal pathfinding and collision clearance, waiting about 90 metres away and resuming when the leader moves. They retain each follower's movement mode, firing setting, and persistent priority target. Followers may fire opportunistically without diverting from following. A group order leaves the clicked leader's own orders unchanged, even when selected. Follow persists until replaced; disabling or destroying the leader switches its followers to Hold.
- MechWarriors automatically choose and fire ready, enabled weapons, best expected damage per heat first, while their mech's heat is at or below its heat limit. Weapons that generate no heat, such as machine guns, fire at any heat. The player may enable or disable individual weapons. The confirmed heat extension below specifies the limit and the order weapons are checked in.
- Provisional pacing targets are five to ten minutes for an ordinary engagement, excluding pauses, and roughly ten-second cycles for major weapons. These are tuning targets, not settled per-weapon statistics.
- Manual pause allows inspection and all normal commands. Orders issued while paused take effect when time resumes, without a separate planning-phase commitment.
- The battle runs at 1x or 4x (fast-forward), and starts paused at 1x. Fast-forward runs the same fixed simulation steps four times as often, so the battle plays out identically and only real time is shortened. Walk cycles speed up with it. Automatic pause triggers are deferred.
- **Time panel.** A small translucent panel floating at the top center of the battlefield shows the mission's elapsed time (`mm:ss`, `h:mm:ss` past an hour). Beneath it are three cassette-style icon buttons: Pause (two vertical bars, Space), Play (a right-pointing triangle, 1x, `,`) and Fast-forward (two right-pointing triangles, 4x, `.`). The tooltips name each speed and hotkey. Pause toggles exactly as Space does. Play and Fast-forward pick their speed and release the player's own pause. The icon for the player's own pick is gold: Pause while their toggle is on, otherwise the speed they picked, which is kept while paused. Like Space, the hotkeys work whatever was last clicked, and do nothing while a modal is open. (Flashing confirmed November 23, 2026.) While the battle stands paused for any reason (any player's toggle, a waiting surrender, the start of the battle) the Pause button flashes: its background alternates between its own colour and bright yellow, 0.4 s each, with the icon dark while yellow. Fast-forward flashes the same way while the battle runs at 1x and at least one player, the local player included, has picked 4x. Neither flashes during the drop phase, once the outcome is decided, or while a modal is open.
- In co-op every player picks their own speed, Pause, 1x or 4x, and the battle takes the slowest pick: it is paused while any player's toggle is on, and runs at 4x only while every player has picked 4x, otherwise at 1x. Each pick reaches the leader as a command and travels with the leader's battle state, and each replica runs its clock at the resulting speed between states. Every player's buttons and `,` `.` work. Each time button's tooltip adds a line *Selected by …* naming the other players whose pick it is; a paused player counts only under Pause, including one with the Combat menu open. The line is left out when no other player has that pick, and in single-player battles. (Until November 23, 2026, only the leader set the speed.)
- A player mech under Hold fire fires at nothing, its priority target included. An enemy's Attack order authorizes firing at its target even with Hold fire set, while Hold fire still suppresses its opportunistic shots at other enemies. Individually disabled weapons remain disabled regardless of orders.
- Mechs without active orders fire opportunistically according to their firing setting and priority target. A player mech without Maintain Range, or without a priority target, holds position and never pursues; with both, it keeps its range from the priority target. Completing a move returns a mech to this behavior.
- Losing detection of an enemy's attack target sends the attacking mech to the last-known position without tracking hidden movement. Reacquiring that target resumes pursuit; otherwise arrival ends the order. Opportunistic fire at other detected enemies is permitted during this movement.

## Movement and damage in the first slice

- Ground movement includes walking and running. Running is faster and reduces firing accuracy. Mechs with jump jets can also jump; see [Jumping](#jumping-confirmed-november-2-2026).
- Walking is the initial default; a new movement order retains each mech's chosen movement mode.
- Group Move assigns separate, non-overlapping destinations around the clicked point. A formation system is deferred.
- When preferred engagement distance cannot be achieved, move toward the nearest achievable position within the arena while avoiding occupied space. Destroyed mechs immediately stop obstructing movement and Follow destinations; disabled mechs remain solid. Wrecks remain visible and selectable/inspectable. Continue firing whenever permitted and in range; preferred distance is a goal, not a firing prerequisite.
- Mechs occupy space and navigate around one another, without pushing or collision damage.
- Hits select among Head, Center Torso, Left Torso, Right Torso, Left Arm, Right Arm, Left Leg, and Right Leg.
- Damage reduces the selected location's front or rear armor according to incoming direction, then its internal structure. Only torsos have separate rear armor; head and limbs always use ordinary armor.
- [Critical hits](#critical-hits-confirmed-november-14-2026) can damage or destroy mounted systems, set off ammunition explosions and injure the MechWarrior. Heavy damage in a short time can [knock a mech down](#knockdowns-confirmed-november-15-2026). Location-loss and defeat rules are specified below; heat is specified in its confirmed extension.
- Show the selected friendly mech's calculated hit chance against a detected target.
- Resolve the hit when a weapon fires. Damage also applies then, except for the second half of a laser burn and the impact of an autocannon shell, machine gun burst, missile salvo or PPC bolt, all described below. Projectile and beam effects illustrate that result; projectiles do not independently collide or intercept. Friendly fire is omitted. Firing arcs follow the torso-twisting rules below.
- Each firing effect starts at the firing weapon's source firing marker on the shooter's model, cycling through a launcher's tubes on successive shots. A weapon without a marker in its catalog location uses an unclaimed marker of its kind elsewhere on the model (Crusader leg SRMs fire from the torso racks), or else the center of its location. Hits end at the center of the struck location on the target model. Misses pass beside and beyond that location. Both ends follow the animated models while the effect fades.
- Armor, structure and weapon damage count tenths of a stock point, so every value stays whole: stock values are ten times larger in the game, and a machine gun burst's half point is 5.
- Each weapon discharge makes one hit roll and applies one damage packet, except that a laser divides its packet across two adjacent locations and each missile of an LRM or SRM salvo rolls separately, and a PPC also splashes adjacent locations. There is no cluster table.
- Weapon categories differ mechanically and visually. Lasers, autocannons, machine guns, missile racks and PPCs are refined; flamers retain the line effect until they receive their own treatment.
- Whatever the weapon, every location an attack destroys on a friendly or detected mech sets off a large explosion at that location: a white flash, a swelling orange fireball inside a slower dark-red bloom, and yellow debris flung outward. It follows the location as the mech moves or falls as a wreck, and ages with simulation time.

### Lasers

- Lasers are the premium damage-per-second weapons, paid for in heat and in damage split across locations. They cycle faster than other weapons of their class, and smaller lasers cycle fastest.
- Small, medium, large and ER large lasers discharge as a one-second burn. A laser hit applies half its damage, rounded up, to the rolled location at discharge. Half a second later the burn applies the remainder. If the target was moving, turning or twisting its torso when struck, the burn spreads to an adjacent location, rolled at discharge from the struck location's neighbors and weighted by the hit-location table; a stationary target takes the remainder on the struck location. Destroyed locations remain eligible and transfer damage normally.
- The one hit roll covers both halves. Heat and cooldown apply at discharge, and volley selection uses full expected damage.
- The second half lands with that step's exchange, using current positions and torso facing to choose front or rear armor. It lands even if the shooter is disabled or shut down, if its laser location is lost, or if the target moves out of sight or range. It is lost if the target has been destroyed. Destroying the shooter cancels the burn, unless the second half is due in the same step that destroys the shooter.
- The combat log records the discharge, including the pending burn, and a separate burn-continuation entry when the second half lands. Misses and cancelled burns have no continuation entry.
- Beams are glowing 3D cylinders with a lighter core of the same color. Small lasers are red, medium green, large and ER large blue, with width increasing by class. The beam extends from the muzzle to full length over 0.1 seconds, sweeps steadily from the first location to the second over the whole burn, then fades briefly; on a stationary target it holds on one location. When the second half of the damage lands the beam is midway between them. A miss keeps its aim beside its first location for the whole burn, but the beam carries on past the target: solid out to the laser's long range, then losing coherence, growing steadily more transparent until it vanishes at 1.5 times long range. A miss stops where it meets the ground, a hill or a rock, at whatever strength it has there, and scorches a dark mark at that point. The mark follows the beam's ground point while it burns, then stays where the beam left it and fades away over 5 seconds. Each struck location shows an impact ring in the laser's color as the beam reaches it, while damage still applies at discharge and at half the burn. A destroyed shooter's beam vanishes at once. Effects age with simulation time, so pausing freezes them.

### Autocannons

- AC/2, AC/5, AC/10 and AC/20 fire one shell per discharge. The hit and location are rolled at discharge; a hit's damage applies when the shell lands, after the planar distance divided by the shell speed (one second at 1,800 metres for every caliber). Heat and cooldown apply at discharge, and volley selection uses full expected damage.
- Impact uses the target's torso facing at that moment against the bearing from where the shell was fired. A shell always lands, even if its shooter is disabled, shut down or destroyed, or the target moves out of sight or range. A shell reaching a destroyed target explodes on the wreck without damage. Shells in flight land during the wind-down and can produce a draw.
- The combat log records the discharge with its flight time, and a separate impact entry with the actual losses for hits. Misses have no impact entry.
- A short muzzle flash marks the firing marker. The shell is a glowing yellow bullet with no trail, sized by caliber, flying nose-first straight to the struck location on the moving target. A miss flies beside and beyond its location and explodes where it meets the ground or terrain, or fades 300 metres beyond long range. Landing shells show a fireball around a brief white-hot core, both growing in size and duration with caliber. Effects age with simulation time, so pausing freezes them.

### Machine guns

- A machine gun discharge is a one-second burst, and the gun fires a new burst every two seconds. The burst makes one hit roll, deals one damage packet of half a stock point, spends one ammunition shot and adds its heat at discharge, with one combat log entry. The stream of rounds illustrates that result.
- A hit's damage applies when the burst's first round arrives, after the planar distance divided by the bullet speed. Like shells, rounds always land, a destroyed target takes them without damage, and bursts in flight land during the wind-down. The combat log records the discharge with its flight time, and a separate impact entry with the actual losses for hits.
- The burst is a high-speed, continuous stream of tiny bright yellow and orange tracers, each a short glowing streak far smaller than an AC/2 shell, with a small muzzle flash on every round. Each round leaves the muzzle where it is when fired and flies to where its target is at that moment, so the stream sweeps with moving mechs. Hit rounds scatter slightly around the struck location and each strikes with a tiny spark, with no fireball. Missed rounds spray beside and beyond the target and vanish where they meet the ground or terrain, or fade 300 metres beyond long range, without sparks. A shooter that is destroyed, shuts down or loses the gun stops firing at once; rounds already in the air fly on. Effects age with simulation time, so pausing freezes them.

### Missile racks

- LRM-5, LRM-10, LRM-15, LRM-20, SRM-2, SRM-4 and SRM-6 fire a salvo of one missile per rack tube for one ammunition shot. Each missile makes its own hit roll at the weapon's chance and rolls its own location, so a salvo spreads its damage. Each hit deals the stock per-missile damage and transfers normally. The salvo counts as a hit when any missile hits.
- Rolls happen at discharge; damage applies when the salvo lands. LRM flight time is the planar distance divided by the missile speed. SRMs leave the rack slower and accelerate steadily, so a shot at the SRM's long range takes as long as an LRM over that distance and shorter shots take relatively longer. Every missile of a salvo lands in the same step, against the target's torso facing at that moment and the bearing from where the salvo was fired. Like shells, missiles always land, a destroyed target takes them without damage, and salvos in flight land during the wind-down. Heat and cooldown apply at discharge, and volley selection uses the chance times the full rack damage.
- **Indirect fire.** Only LRMs fire indirectly; SRMs always need the shooter's own clear sight. LRMs can fire at a target the shooter cannot see when that target is the shooter's priority target or the target of its (enemy AI) Attack order; opportunistic shots always need the shooter's own clear sight. Indirect fire needs a spotter: an active friendly with its own clear sight to the detected target. The spotter with the lowest vegetation modifier to the target is used, then the nearest. Indirect fire adds a flat penalty and uses the spotter's vegetation instead of the shooter's; range, minimum range, movement and heat remain the shooter's. Firing arcs, Hold fire and Attack permissions apply as usual, and a blocked attacker still seeks a clear firing position. The target readout shows the spotter and penalty in place of the blocked-fire notice.
- The combat log records the discharge with missiles hit out of the rack size, struck locations, flight time, and for indirect fire the spotter (an enemy spotter is named only at Detailed contact). Debug lists every missile's roll. A separate impact entry carries the combined losses; a salvo whose missiles all miss has no impact entry.
- The salvo ripples out of the rack, missile by missile through its tubes, with a small flash at each tube. Each missile is a small exhaust glow with a short flame trail, white at the missile through yellow to orange, fading toward the tail. Every missile speeds up through its flight: LRMs climb slowly and accelerate into the dive, and SRMs accelerate steadily. SRM salvos fly flat. A direct LRM salvo flies a moderate lofted arc; an indirect one flies a high arc that clears the blocking terrain. Each missile skitters along its own smooth random path, strongest mid-flight and gathering back as it nears its impact point, then homes on its struck location on the moving target and bursts in a small explosion. Missed missiles come down beside and beyond the target and burst on the ground. The skitter is visual only and never affects simulation draws. Effects age with simulation time, so pausing freezes them.

### PPCs

- A PPC fires one bolt per discharge. The hit and location are rolled at discharge; damage applies when the bolt lands. Bolts fly at a constant speed, so nearer targets are struck sooner. Like shells, bolts always land, a destroyed target takes them without damage, and bolts in flight land during the wind-down. Heat and cooldown apply at discharge.
- A hit deals full damage to the struck location, plus splash damage of half the weapon's damage, rounded down, shared at random among the locations adjacent to it in the laser sweep table. The head is never splashed, so a head hit puts all of it on the center torso. The splash is rolled at discharge one stock point (10 damage) at a time, any remainder last, each chunk to an equally likely adjacent location, and lands with the primary damage, using the same front/rear bearing and transferring normally.
- **Electro-interference.** A PPC hit adds a flat penalty to every attack the target makes for a few seconds. A further hit restarts the timer; it does not stack. Misses and impacts on wrecks cause none. The selected unit's readout shows the penalty and time left.
- The combat log records the discharge with its flight time nominal primary and splash damage, and where the splash lands (Detailed target), and a separate impact entry with the actual losses across all struck locations and the electro-interference. Misses have no impact entry.
- A thin bluish-violet beam flashes from the muzzle to the target at full length and holds while a glowing violet bolt races down it to the struck location on the moving target, fading out just after the bolt lands. The bolt bursts in a coruscating explosion: a violet-white flash inside a swelling violet glow, with jagged lightning arcs that flicker outward and jump to the center of each location the splash reached. A miss flies beside and beyond its location and bursts where it meets the ground or terrain, or fades 300 metres beyond long range. The flicker is visual only and never affects simulation draws. Effects age with simulation time, so pausing freezes them.

### Ammunition

- Every ammunition-fed weapon (autocannons, machine guns, missile racks) spends one shot per discharge, hit or miss. Each bin starts with its stock shots per ton.
- A mech's intact compatible bins form one pool per ammunition kind. Each shot comes from the intact bin with the fewest shots remaining, ties in catalog order. Bins that are destroyed or in destroyed locations leave the pool.
- A weapon with an empty pool cannot fire and its readout shows Out of ammunition; volley selection ignores remaining shots. If a volley empties a shared pool, later weapons in it are withheld and stay ready. Running dry is not physical weapon loss and never disables a mech; losing every compatible bin is.
- Remaining shots appear in friendly weapon readouts. Enemy ammunition is never known: the paperdoll tooltip shows destroyed or lost bins but no shot counts, and an enemy weapon that has only run dry still appears available. The combat log never reports ammunition.

- Ready, enabled weapons with valid targets enter heat-budgeted volley selection. Weapons start ready, cooldown begins only when fired, and cooldown progresses only while unpaused, including during thermal shutdown.

### Damage transfer

Further damage to a destroyed location transfers according to these paths:

| Destroyed location | Transfer destination |
| --- | --- |
| Left or right leg | Center torso |
| Left arm | Left torso |
| Right arm | Right torso |
| Left or right torso | Center torso |
| Center torso | Head |

Excess damage from the blow that destroys a location transfers immediately. Transferred damage uses the original incoming attack bearing against torso facing to select front or rear armor whenever it reaches a torso, before reducing internal structure. Continue along the path if the receiving location is already destroyed. A leg transfers directly to the center torso, without passing through a side torso.

### Location loss and defeat

- A location is destroyed when its internal structure reaches zero; its mounted weapons cease functioning.
- Destroying a side torso also destroys its attached arm.
- One destroyed leg halves movement speed; both destroyed legs immobilize the mech.
- Only center-torso destruction destroys the mech. Head destruction disables it and kills its MechWarrior, so it can still be salvaged whole or rebuilt (changed November 20, 2026; it used to destroy the mech).
- **MechWarrior death** (confirmed November 5, 2026; ejection added November 20, 2026). A MechWarrior dies when the head is destroyed or when a [critical hit](#critical-hits-confirmed-november-14-2026) strikes the cockpit. A destroyed center torso always ejects its MechWarrior first, injured, so overkill carried up to the head no longer kills them; an ejected pilot is safe from anything that happens to the mech afterwards. The rule is the same for both sides; the results show the pilot as *Killed*. Enemy MechWarriors who survive count toward [reputation](design.md#reputation-confirmed-november-5-2026).
- Loss of both legs, loss of the head, a critical hit on the cockpit, or an ejected MechWarrior disables the mech. Losing every weapon no longer disables it (changed November 20, 2026): an **unarmed** mech can still move, extract, flee or surrender. Player weapon enable/disable settings do not themselves constitute physical weapon loss.
- Destroyed, disabled, surrendered, fled and extracted mechs count as defeated and no longer participate in combat. Thermal shutdown is not defeat.
- **The end of a battle.** A side is beaten when none of its mechs is still fighting: every one is defeated or unarmed. An unarmed enemy left on the field can be salvaged whole and its pilot counts as spared; your unarmed mechs come home as usual.
- **Kills** (confirmed November 11, 2026). The hit that leaves an enemy mech defeated, disabled or destroyed, credits its shooter with one kill; the mission report carries each friendly mech's kills to its MechWarrior's record.
- When every mech on one side is defeated, a three-second wind-down begins and cannot be undone. The simulation continues without new discharges so pending effects can conclude and the player can see the final shots. The outcome is decided when the wind-down ends, so a pending burn that defeats the other side produces a draw. Pause, camera and inspection remain available, and ending the simulation manually remains immediate.
- Component damage and MechWarrior injury follow [critical hits](#critical-hits-confirmed-november-14-2026).

## Automatic targeting

- Prefer the explicit attack target when engageable, then the priority target when engageable, then the nearest detected enemy within an enabled weapon's range.
- Keep the current target until it becomes invalid or a higher-priority choice is available.
- Automatic target selection does not change a movement order's destination.

## Simulation entry and battlefield setup

- Add a **Simulated Deployment** button to the mech hangar to launch this first combat slice.
- This is a standalone simulation using existing mech and MechWarrior definitions. It reports damage and outcome without changing the campaign; launching from an accepted mission and applying permanent results are deferred.
- Use a bounded 192 × 192 arena with procedural natural terrain and authored opposing starting positions, initially outside detection range. Keep spawns clear and connected. Expose a reproducible terrain seed independent of combat rolls.
- Start paused, after the [drop phase](#drop-phase-confirmed-november-23-2026), and support one to four mechs per side.
- Simulated Deployment first offers **Randomized enemy**, **Mirror match**, **Patrol** (a [patrol](#patrols-confirmed-november-13-2026) whose opposition is rolled from the company's reputation, as an offer's would be, for the deployed lance) or **Take and Hold** (a [Take and Hold](#take-and-hold-confirmed-november-26-2026) rolled the same way), then opens the existing force-selection and terrain setup, including the [biome](#biomes-confirmed-november-4-2026) picker. Canceling returns to the hangar. Every deployment goes through force selection; this is also a general rule for eventual campaign deployments.
- Select one to four operational roster mechs with healthy, assigned MechWarriors. Show why unavailable mechs cannot participate. Assignments remain managed in the hangar.
- A mirror match copies the selected mech types and assigned MechWarrior skills, with both sides fully repaired and supplied inside the simulation.
- Randomized opposition is generated on Launch using fresh candidate pools and uniformly random choices under the player's initial draft rules. Deploy the same number of enemies with the same weight categories as the selected player force, retaining their draft-assigned pilots. Generate another fresh legal draft whenever a category runs out, keeping unused pairs available for later slots. Enemy units start pristine; player units retain their current condition and ammunition in this mode. Repeating a terrain seed reproduces terrain independently of the enemy draft.
- Enemy AI uses the same detection and combat rules. It advances toward the arena center until contact, then issues attack orders against the nearest detected enemy and maintains preferred engagement distance. Coordinated flanking and privileged hidden-position knowledge are excluded.

## Simulation completion and exit

- Stop combat when all mechs on a side are defeated and show the [post-battle summary](#post-battle-summary-confirmed-november-21-2026). Defeating only the enemy force is victory; losing only the friendly force is defeat; eliminating both at the same simulation instant is a draw.
- Return to the same hangar selection after the summary.
- The combat menu offers **End Simulation** at any time. Enemies can flee (see [stress, surrender and ejection](#stress-surrender-and-ejection-confirmed-november-20-2026)); the company has no retreat beyond a patrol's extraction.
- Simulation causes no campaign time advancement or permanent consequences. It does not save in-progress battles.
- Resolve shots due at the same simulation instant before deciding the outcome, so processing order does not determine victory.

## Battlefield controls

- Left-click selects, drag selection selects multiple friendly mechs, right-click ground issues Move, right-click an enemy sets the priority target, and right-click a friendly issues Follow.
- Every direct mech click uses the same padded envelope of the mech's visible model geometry, including gaps between limbs, scaled with the model and zoom. Overlaps resolve to the frontmost envelope regardless of allegiance; hills and rocks block hidden portions. Drag selection retains its center-point rule.
- Hover previews that same mech with a thicker, lighter [silhouette outline](mech-models.md): 4 pixels instead of 2, its team colour mixed 50% toward white, still at half opacity where terrain hides it. It preserves the selected unit's gold ground ring and never changes selection, inspection, or orders. UI overlays, menus, camera gestures and loss of enemy detection suppress hover. A mech out of the fight shows no outline, even when hovered.
- Right-clicking an enemy sets the priority target and issues no order, so an active Move or Follow carries on. **Clear priority target** removes it.
- Escape opens the combat menu: **Resume**, **Mission Details**, **Audio** (the audio settings, `docs/audio.md`), and **End Simulation** or **Withdraw**.
- **Mission Details** replaces the menu over the still-paused battle. Its MISSION section gives a mission's briefing, objective and contract terms (employer, opponent, contract kind, patrol, posting date, payment, salvage, insurance, location, biome, opposition tonnage and expected opposition), worded as on the mission board but without reputation, travel or deadline. A practice battle names its opponent type instead and notes that it has no campaign consequences. Its BATTLEFIELD section gives the biome and description, ambient temperature and cooling efficiency, counts of hills, terrain patches, lakes, rivers and buildings, whether there is a sea, patrol points visited (on a patrol) and combat time, and offers the terrain seed in a read-only box for copying; the seed is no longer shown on the menu itself. **Back**, Escape or right-click return to the Combat menu without resuming. Every co-op player can open it.
- Combat explicitly overrides the existing office/subscreen right-click-back behavior: right-click remains a battlefield command while in combat.
- Use an angled overhead camera with pan, zoom, and rotation.
- Panning (WASD, arrows or a middle-drag) moves the camera across a level plane, so the view keeps a steady pace over hills and slopes. Rotation pivots on the ground at the centre of the view.
- Provide a friendly-unit selection bar and selected-unit panel showing orders, movement mode, firing setting, preferred engagement distance, weapon readiness, and armor/internal structure by location.
- Enemy inspection retains the friendly selection. Model-only contacts show the chassis name; detailed contacts add the shared paperdoll and its location tooltips. Inspection draws the union of identification ranges (blue) and, separately, half-range status ranges (gold) of all selected active friendlies for that target. Without an inspected enemy, draw the union of their 100-ton identification ranges and label the 20–100-ton detection span. Draw only exposed boundary arcs, preserving separate regions and uncovered holes, with no fill or internal overlap arcs. Disabled and destroyed selections contribute nothing. With multiple active contributors, label the readout “Selected mechs” and show minimum–maximum contributor ranges for each displayed tier; retain the single-mech readout when only one active contributor remains. These are distance boundaries; terrain may still block observation, and side-wide detection continues to use all active observers regardless of selection.
- **Range overlay modes.** R, or the range button on its own row beneath Fire at will / Hold fire, cycles the battlefield's range overlay: Sensor range → Weapon range → Firing arcs → Sensor range. The button's label names the current mode (for example "Weapon range [R]"). The mode is a view setting for the whole battlefield, not a per-mech order; each battle starts on Sensor range and nothing is saved. Sensor range draws the detection rings above. The two weapon modes have no readout, ignore enemy inspection, and draw nothing for disabled or destroyed mechs or before the drop.
  - **Weapon range** draws, around each selected active friendly separately, an outline circle at the long range of its weapons that are switched on, intact and have ammunition. Weapons sharing a long range share one circle. A circle is drawn in the hangar colour of its weapon class (ballistic, energy, missile or AMS, as on the hangar's critical slots); when weapons of several classes share it, the circle alternates their colours in dashes. Rear mounts get full circles too.
  - **Firing arcs** draws the same weapons as outline wedges: two radial edges and an arc at long range, covering each weapon's [firing arc](#torso-twisting-confirmed-october-29-2026) and following the live leg and torso facing. Weapons share a wedge when they share both arc (forward, left arm, right arm, leg or rear) and long range, with the same colours and dashes as circles. Longer wedges are drawn first, and edges may overlap.

## Battlefield sound (added November 16, 2026)

- Every weapon fires with a sound by kind at its muzzle, and each shell, PPC bolt, missile and machine-gun round sounds where it lands, timed with its effect. Laser hits, destroyed locations, destroyed mechs, critical hits and ammunition explosions sound at the struck location.
- Mechs plant each foot audibly, heavier chassis lower and louder. Jump jets roar for the whole flight and land with a thud. A knockdown crashes when the mech hits the ground, and servos whine as it rises.
- Sounds are positional around the camera's focus and quieter when zoomed out. Undetected enemies make no sound. An alarm repeats every 2 s while any active friendly mech's heat is at or above 20 and the battle is running.
- Cue files, voice caps and sources are in [audio](audio.md).

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

- Destructible buildings and terrain, roads and bridges, flowing-water animation, rivers that rise inside the map, and advanced cover-seeking tactics.
- Infantry, vehicles, aircraft, and other combatant types.
- Heat damage and heat-triggered ammunition explosions, water cooling bonuses, ammunition consumption, falls, Death from Above, and MechWarrior casualties beyond death and [injury](#critical-hits-confirmed-november-14-2026).
- Physical projectile collision, friendly fire, melee, called shots, deliberate ammunition dumping, and other special actions.
- Separate sensor/visual contacts, additional stances, formations, coordinated enemy tactics, speeds other than 1x and 4x, and automatic pause triggers.
- An opponent editor, tactical retreat/recovery mechanics, and saving in-progress simulations.
- Campaign mission launch, persistent battle consequences, and objective types beyond destroying the enemy, [patrols](#patrols-confirmed-november-13-2026) and [Take and Hold](#take-and-hold-confirmed-november-26-2026).

## Acceptance scenarios

1. Open **Simulated Deployment** from the hangar, choose randomized opposition or a mirror match, select one to four eligible mechs, and start paused. Randomized opposition matches count and weight categories using fresh legal drafts; a mirror matches types and pilot skills with both sides pristine. Ineligible roster entries explain their unavailable state.
2. Issue and replace movement/attack orders while paused; resume to execute them. Group movement uses separate destinations, and walking/running modes persist.
3. Demonstrate that Maintain Range causes preferred-distance positioning on the priority target, while a priority target without Maintain Range never causes movement. A moving mech fires opportunistically without diverting from its destination.
4. Verify automatic target precedence, weapon enable/disable controls, independent cooldowns, and Hold fire's suppression of every player shot.
5. Share a detected target across friendlies; allow a friendly outside its own detection radius to shoot within weapon range. Lose all detection, retain a stationary last-known marker, and pursue that position without tracking hidden movement.
6. Damage front/rear torso armor according to torso facing and ordinary head/limb armor. Transfer excess and subsequent damage along the specified paths, including legs directly to center torso, preserving the incoming bearing and consuming the receiving armor face before structure.
7. Demonstrate destroyed-location weapon loss, side-torso/arm dependency, one-leg movement reduction, and the distinct destruction/disablement conditions. Weapon toggles alone must not disable a mech as a battle casualty.
8. Keep mechs inside the arena, avoid overlap, and continue valid fire when an engagement-distance goal cannot be reached. Enemy orders obey the same visibility and combat rules.
9. Resolve victory, defeat, and simultaneous elimination as the specified outcomes. End Simulation remains available before completion.
10. Return to the same hangar selection with a damage/outcome summary and no changes to roster condition, equipment, assignments, MechWarriors, campaign date, or campaign finances.

## Design review status

The user confirmed shared understanding of this initial combat design on October 25, 2026. The design interview is complete. Numerical authoring and deferred features retain the explicit scope above.

## Confirmed terrain design

The simulated battlefield uses reproducible, asymmetric procedural natural terrain: rocks, vegetation, hills and water. Players may enter or share a terrain seed, independent of combat randomness. Retain the existing arena and starting positions (5,760 × 5,760 metres since [engagement spacing](#engagement-spacing-confirmed-october-29-2026)), clear the spawn areas, and guarantee a traversable route between sides.

Rocks are impassable and block detection and fire. Hills affect movement and line of sight through their elevation, with no separate height bonus. Water slows movement. Vegetation slows movement and reduces hit chance when the target is within it or the shot passes through it; cap the penalty. Detection remains shared, but each attacker needs a clear line of fire.

Both sides navigate around obstacles and seek a clear firing position for blocked attack orders. A movement order to an inaccessible point resolves to the nearest reachable point and displays the actual destination. Combat information explains blocked fire and vegetation penalties. Use simple, readable visuals and one default terrain density; document and test initial numerical tuning.


## Biomes (confirmed November 4, 2026)

Every generated battlefield belongs to a **biome**: Valley, Arctic, Lunar, Highlands, Desert, Tropical, Grassland, Archipelago, Volcanic or Toxic. The last seven were added November 4, 2026, so that every star system's climate maps onto one; their numbers are provisional. A biome sets the battlefield's climate, the shape of its ground, the features on it and its look. It changes nothing else: gravity, sensors and jumping are the same everywhere.

**Climate.** A biome has an **ambient temperature**, on the mech heat scale, and a **cooling efficiency**. Every second, each mech's heat moves toward ambient, by 1 heat per heat clock for every 5 points of difference. A mech hotter than ambient gains free cooling; a colder one warms up. The exchange is proportional, so it is strongest for the hottest mechs. Cooling efficiency multiplies heat-sink output only; ambient exchange, weapon, movement, jump and received flamer heat are unaffected. Heat still never falls below zero, and every mech starts at zero.

- **Valley**: average climate. Ambient is above zero, so a mech with few sinks slowly warms up while idle; ten single sinks hold an idle mech at zero.
- **Arctic**: cold and windy. A low ambient and efficient sinks.
- **Lunar**: vacuum. A very low ambient gives every hot mech strong free cooling, but sinks shed little, so a sink-heavy build gains far less than it would elsewhere.
- **Highlands**: thin air. Cool, but sinks are weakened.
- **Desert**: hot and dry, with slightly weakened sinks.
- **Tropical**: hot and humid; sinks lose a fifth of their output.
- **Grassland**: Valley's ambient with windy, slightly better cooling.
- **Archipelago**: cool sea winds and efficient sinks.
- **Volcanic**: the hottest biome. Ten single sinks barely hold an idle mech at zero.
- **Toxic**: warm, and the corrosive air badly fouls sinks.

**Terrain.** A biome shapes a seeded noise heightfield and chooses its features:

- **Valley**: rolling hills and ridges, forests, rocks, lakes on low ground and one or two rivers.
- **Arctic**: sharper snowy ridges, sparse conifer stands, rocks, frozen lakes and sometimes a frozen river. Ice keeps full speed and gives no cover. There is no open water.
- **Lunar**: cratered plains with raised rims, and boulder fields. No vegetation or water.
- **Highlands**: tall mountain massifs cut by ridges and passes, many rocks, conifer scrub, small tarns and sometimes a river.
- **Desert**: long dunes over rolling sand, tall rock outcrops, sparse scrub and a few oases.
- **Tropical**: broken hills under dense jungle, the most vegetation of any biome, with one or two deep rivers, lakes with deep centres and a few rocks.
- **Grassland**: nearly flat plains with few rocks, copses, ponds and sometimes a river, so sight lines are long.
- **Archipelago**: low islands in a sea, shallow at its shores and **deep water** further out, with palm groves and rocks.
- **Volcanic**: jagged basalt ground, five volcanic cones with summit calderas, **lava** pools, one or two lava rivers and many rocks. No vegetation or water.
- **Toxic**: eroded hills and ridges, toxic pools (shallow water), fungal thickets and rocks.

Lava and deep water slow nothing, because no mech may stand in them: they block movement, including the mech's own width, and jumping mechs cannot land in them. They do not block sight or fire. Navigation detours around them like rocks. How liquids lie on the ground is described under [lakes, rivers and the sea](#lakes-rivers-and-the-sea-confirmed-november-26-2026).

**Buildings.** Every biome has one to six settlements holding 15–50 buildings between them, each on a levelled pad, with streets wide enough for any mech. Buildings are solid and indestructible, like rocks: they block movement, sight and fire and give no cover penalty. Movement, jumping and blocked-attack firing positions treat them like rocks. Valley has farmsteads and small towns, Arctic prefab outposts with tanks and radio masts, and Lunar pressurised domes and habitat modules. Highlands has stone villages and relay masts; Desert walled flat-roofed blocks, water tanks and moisture domes; Tropical stilt houses and plantation sheds, with blocks in towns; Grassland ranches and grain silos; Archipelago fishing villages and fuel tanks, with blocks in towns; Volcanic geothermal plants with tanks and cooling stacks; and Toxic sealed domes, habitat modules and tanks.

**Choosing a biome.** Simulated Deployment has a **Biome** picker beside the terrain seed: Random or a named biome, with each biome's ambient temperature and cooling efficiency shown. The same seed and biome recreate a battlefield; the same seed in another biome is a different battlefield. The battlefield header, pause menu and results show the biome beside the seed. Selected details split cooling into sink output × efficiency and the current ambient exchange.

**Campaign.** Every star system has a biome, classified from its primary world's climate (see [combat tuning](combat-tuning.md#system-biomes)); the star chart and mission offers show it. Campaign missions will be fought in their host system's biome once they launch battles; until then only Simulated Deployment generates battlefields. **Urban** is not a biome but an overlay: systems of 4 billion people or more are marked Urban and will get denser settlements in their climate's biome once missions launch battles. Each system has exactly one biome.

**Weather** (deferred). Storms, rain, blizzards and sandstorms are a separate layer that can sit on any biome and may affect sensors and visibility, unlike biomes.

**Visuals.** Everything is drawn in code: noise-textured ground in each biome's palette, faceted rocks and boulders, broadleaf or conifer trees filling vegetation patches, water and ice surfaces, buildings per style, and each biome's sky and sunlight (a black starfield on Lunar). Lava glows; water darkens and turns opaque with depth. Snow and dust particles are deferred.

## Lakes, rivers and the sea (confirmed November 26, 2026)

Liquids follow the heightfield instead of sitting in circles. Water, deep water, ice and lava all take the same shapes; rocks and vegetation remain circular patches.

**Lakes** gather in low ground. A lake starts at a low point and floods outward over the connected ground below its surface, which rises until the lake covers its intended area or reaches the hollow's spill point, whichever comes first, so a lake never overflows its rim and its shore follows the ground's contour. Its surface is flat; the ground beneath keeps its shape. Where a hollow is too shallow to hold its lake, an irregular bowl is first sunk into the ground. Each biome keeps its number of lakes; a lake that reaches its spill point early comes out smaller. Lava pools gather first in the volcanic calderas. Lakes keep out of the drop zones and away from clear areas and settlements.

**Depth.** In Tropical and Archipelago maps, water deeper than 8 metres is deep water, so their lakes have impassable centres and wadeable margins. Every other biome's lakes and rivers are wadeable throughout.

**The sea.** The Archipelago has one sea level instead of lakes: everything below it is sea, about 40% of the battlefield, shallow near the shores and deep further out. Settlements stand on dry land. Where deep water would cut off a spawn area, the central rendezvous, a clearing or the objective settlement, generation raises a shallow sand bar to join them.

**Rivers** run in Valley and Tropical maps (one or two), Highlands and Grassland (none or one), Arctic (none or one, frozen) and Volcanic (one or two, of lava). Lunar, Desert, Archipelago and Toxic maps have none. A river enters where a map edge is high and leaves where an edge is lower, possibly the same edge, along a course that prefers low ground, winds from side to side and passes through the lakes on its way. It carves a trench with a flat bed and climbable banks. Its bed and surface only ever fall downstream; where the ground rises across its way, the trench cuts deeper, like a gorge. A lake the river passes through takes the river's level, lowered if the river arrives lower; a hollow beside the river that lies below its surface fills as a lake at its level. Rivers never cross each other or share a lake.

A **lava river** pours from a volcanic cone's caldera, breaching its flank, and flows downhill to a map edge or into a lava pool.

**Fords.** A deep or lava river has two or three fords, spread along its course: stretches about 120 metres long where the bed rises, to shallow water a mech can wade across in the Tropical biome, or to a cooled basalt crust in the Volcanic biome. A deep or lava river also fords wherever it crosses a spawn band, the central rendezvous or a clearing, so those stay passable. Shallow and frozen rivers may run through spawn bands as they are, so mechs may start wading.

**Settlements.** A shallow or frozen river may run through a settlement: lots in its channel stay empty. Deep and lava rivers keep off settlements, and no river crosses the objective settlement of a Take and Hold.

**Mission Details** counts lakes and rivers, and notes the sea, beside the battlefield's other features.

## Sensors and pilot budgets (confirmed October 27, 2026)

Each pilot has Gunnery, Piloting and Sensors in the range 2 (expert) to 6 (novice). All start at 6; each step of improvement costs one point, so a skill costs `6 - rating`. Regular pilots spend exactly six points, veterans eight. (Since November 20, 2026 a fourth skill, Grit, shares the budget, now eight and ten points; see [stress](#stress-surrender-and-ejection-confirmed-november-20-2026).) Existing named pilots retain identities and quality classifications but have rebalanced fixed ratings, including Sensors specialists. These allocations resolve through existing catalog IDs in saves. (Since November 24, 2026 MechWarriors are generated instead, with skills from 2 to 7; see [Generated MechWarriors](design.md#generated-mechwarriors-confirmed-november-24-2026).)

`identification radius = 1500 metres × (1 + (6 − Sensors) / 4) × (0.5 + (target tonnage − 20) / 160)`

`detailed status radius = identification radius / 2`

Use continuous planar distances, including each boundary. Only the target's tonnage affects its signature. Current catalog weights are 20–100 tons; radius inputs are bounded to these endpoints and Sensors 2–6. A novice identifies a 20-ton mech at 750 metres and a 100-ton mech at 1,500; an expert identifies them at 1,500 and 3,000. These values include the doubling from [engagement spacing](#engagement-spacing-confirmed-october-29-2026). Both tiers require clear terrain sight and are shared by each side. Disabled and destroyed units cannot observe but remain observable targets.

Close inspection exposes existing equipment state and availability, including equipment lost with its location and weapons whose ammunition bins are all lost; ammunition depletion stays hidden. It adds no heat, critical-hit or other damage mechanics. Visible missing limbs, fallen wrecks and movement still provide physical clues at model-only range. Post-battle damage reports retain their existing full results. Piloting sets the target number of [Piloting checks](#knockdowns-confirmed-november-15-2026) and how long a knocked-down mech takes to stand; the equal point costs establish the agreed allocation structure rather than equal combat value.


## Torso twisting (confirmed October 29, 2026)

Player and AI mechs share independent leg facing and an upper-body waist angle bounded to ±90°. Waist rotation is measured relative to the legs: Light 90°/s, Medium 75°/s, Heavy 60°/s, Assault 45°/s. Leg turns carry the torso in world space; tracking compensates within the waist speed and angle limits. Moving mechs preserve their route even when their target cannot be reached by forward weapons. Stopped mechs turn their legs toward the target while their torso tracks independently. With no aim target, the torso recenters at its class rate. Pause freezes simulation facing exactly.

Track a detected (enemy AI) Attack target first, then a detected priority target, including outside range, behind cover and under Hold Fire. Otherwise retain the existing ordinary acquisition rules: an enabled usable forward or leg weapon must reach a detected target through clear sight; Attack, priority, retained current target and nearest target establish precedence. Firing alignment is not an acquisition condition, so turning never causes target-selection deadlock. An unreachable main target is retained while the torso clamps; movement orders are not diverted. Lost detection never supplies a live aiming position.

Every weapon fires through a 120° arc, boundaries included. Forward head and torso weapons fire within ±60° of torso facing. Arm weapons use the same arc extended 60° further toward their own arm's side: a right-arm weapon covers 60° left to 120° right of torso facing, a left-arm weapon the mirror. Leg-mounted weapons use ±60° of leg facing, unaffected by torso twist, and the main target. Rear-mounted weapons, found only in torsos, use ±60° directly behind torso facing and select a separate rear target: eligible Attack target, priority, retained rear target, then nearest enemy. Rear eligibility requires detection, own clear sight, range and rear arc. Rear targeting never steers the torso. Individual weapons still require enabled/usable state and readiness. Hold Fire suppresses opportunistic shots; an enemy's Attack permits shots only at its ordered target.

The rear armor arc is 120° centered directly behind torso facing, including its boundaries; all other incoming bearings use front armor. Hit-location probabilities remain unchanged. Head, arms and legs use ordinary armor. Transfers into any torso reevaluate the original incoming bearing against torso facing, including transfers from limbs. Depleted rear armor exposes structure without consuming front armor. All torsos update before shots are captured, and all shots resolve before casualties, retaining simultaneous return fire.

The existing team-colored ground line indicates legs; a longer gold line and tip dot indicate torso direction. Weapon readouts label rear mounts, explain turning or outside-arc restrictions, and identify independently selected rear targets. Existing rear armor paperdolls update live, and damage totals include front plus rear armor loss.

## Heat buildup and dissipation (confirmed October 29, 2026)

Heat rewards deliberate bursts at the cost of future cooling time. Player and enemy mechs begin cold with a heat limit of 10 and use identical rules. Single sinks continuously remove 1 heat each per ten seconds; double sinks remove 2. Weapons generate their stock per-shot heat even on misses. Actual walking-speed travel generates 1 heat per ten seconds, actual running-speed travel generates 2, reversing counts as walking, and turning in place adds none. There is no water cooling bonus.

| Heat | Movement speed limit | Accuracy target-number modifier |
| --- | --- | --- |
| Below 10 | 100% | +0 |
| 10–below 20 | 90% | +1 |
| 20–below 30 | 75% | +2 |
| 30 or more | Shutdown | Cannot fire |

Thermal shutdown immediately stops movement, leg/torso turning, firing and sensor contribution. The mech remains solid, targetable and alive for victory checks. Players can change orders, weapon settings and the heat limit while it is shut down. Orders and Follow relationships persist. It restarts automatically once heat falls below 10, with no extra timer. A mech in the air never shuts down: reaching shutdown heat in flight shuts it down as it lands. Heat cannot go below zero and has no upper cap; further flamer hits prolong a shutdown. No internal heat damage or ammunition explosions are introduced.

**Heat limit (confirmed November 17, 2026).** Each mech has a whole-number heat limit from 0 to 30, replacing the former Normal/Aggressive policy. Every battle starts each mech at 10; the limit is not saved in the campaign, and the enemy AI keeps 10. Ready, enabled, usable weapons that can hit are checked one at a time: greatest expected ordinary damage against their current targets first (accounting for hit chance), then lower heat, then mount order. A weapon fires only while the heat so far is at or below the limit, where the heat so far includes every weapon already fired in that step. A weapon can therefore carry heat past the limit, and with a limit of 0 a mech fires once all earlier heat has gone. Held weapons remain ready. Main and independent rear targets share the same running heat. No manual vent command or additional enemy aggression logic is added.

All shots at the same simulation instant use the heat penalties from the start of that exchange. Apply firing and received flamer heat after all discharges and rolls are captured, preserving simultaneous return fire. Each successful flamer hit deals its existing damage plus 2 target heat, even through armor. Misses add no target heat; multiple hits and attackers stack without a special cap.

Explicit sink component groups contribute their grouped sink count. A Destroyed group or lost mounting location removes that contribution once, including sinks in an attached lost arm. Damaged groups retain full output. Internal sinks are the stock remainder and remain available while the mech survives. Individual component criticals and engine heat remain deferred.

Above each friendly mech the battlefield shows only its pilot's callsign, followed by their [stress face](#stress-surrender-and-ejection-confirmed-november-20-2026), over a heat bar; unit cards and the sidebar paperdoll show heat as a bar (see [heat and jump-fuel bars](#heat-and-jump-fuel-bars-confirmed-november-17-2026)). The sidebar's details leave heat to the doll's heat bar; the unit card's tooltip keeps the exact heat, band, limit, cooling and ambient exchange. The sidebar heat bar's [handle](#heat-and-jump-fuel-bars-confirmed-november-17-2026) sets the limit for every selected friendly, including shut-down units, and the group readout says Mixed when their limits differ. A friendly weapon held by heat reads **Too Hot!**; enemy weapon readouts never give heat as the reason, and an enemy's limit is never shown. Exact enemy heat appears only at Detailed contact tier. Numerical implementation details and checks are recorded in [tactical tuning](combat-tuning.md#heat).

## Engagement spacing (confirmed October 29, 2026)

To reduce crowding and lengthen the approach under fire, both sides use three times every stock firing bracket and preferred engagement distance. Minimum ranges also triple, while their penalties retain the same severity at equivalent relative distances. Imported weapon specifications remain stock; combat and weapon readouts apply the multiplier.

Detection and detailed-status distances double across the board. The battlefield grows from 2,880 × 2,880 to 5,760 × 5,760 metres, with deployment positions scaled proportionally, including 240-metre lateral spacing and opposing starts at Z = ±1,920. Terrain retains its density and individual feature sizes, giving approximately four times the features. Terrain, navigation, picking, camera panning and overview zoom cover the expanded field. This supersedes the earlier arena dimensions and sensor distances recorded above.

Movement speeds, firing cadence and damage retain their existing tuning for the first playtest. Group Attack retains its existing collision avoidance; deliberate spreading around targets is deferred until the range change has been playtested. See [current tuning values](combat-tuning.md).

## Tactical shortcuts and battle history (confirmed October 29, 2026)

Number keys 1–4 replace the friendly selection with the corresponding deployment slot. Shift+number toggles that slot in the selection, including removing its last member. Slots remain fixed after losses; disabled and destroyed mechs remain selectable for inspection, matching card clicks. These shortcuts clear enemy inspection and respect combat modals.

Backtick resets yaw, pitch and zoom to the starting combat view, centers the selected group's bounds, and widens zoom only as needed to fit its full models with a margin. If nothing is selected, use all friendly units without changing the actual selection. Home retains the existing arena overview. Hovering a detected enemy draws distinct dashed blue lines from every selected friendly, with continuous planar distance labeled in metres. Range lines do not assert weapon readiness or firing eligibility and follow the existing hover, terrain and UI-occlusion guards.

Shift+backtick toggles an initially hidden left panel floating over the battlefield, below the contact cards and objective; the battlefield keeps its size. It contains Combat and Debug tabs. Roughly twelve ordinary entries fit at the default window size; long entries wrap and smaller windows show fewer. Both tabs retain the entire current battle with newest events at the bottom. Wheel input over the log scrolls history without zooming the camera. Each tab retains its own scroll position and follows new events only while at the bottom. Hiding the log preserves both tabs and their history. A new battle begins hidden, with empty history and Combat selected.

Record one immutable event per weapon discharge, including misses and whole missile salvos. Entries contain time, side/slot identity, shooter and target names, weapon and mount, firing heat contribution and shooter exchange-end total, range, percentage chance and actual random draw, HIT/MISS, nominal delivered damage, actual front/rear armor and internal-structure losses per location, lost components/systems, and newly disabled/destroyed targets. Place heat between weapon and range. Successful flamers also report added target heat and target exchange-end heat. Keep the existing probability comparison and random-number sequence; lower draws hit. Damage attribution is captured during application, including transfers and attached-arm losses, before subsequent shots can alter condition. Exchange-end heat includes simultaneous incoming flamer heat. Individual component criticals remain deferred.

Debug exposes every event and all recorded fields. Combat captures sensor visibility when the shot happens: friendly information is always detailed; Identified enemies expose chassis and slot, while exact weapon identity, variant, condition, system losses, heat and enemy attack percentages require Detailed contact. Friendly shots and detected enemy shots appear. Hits on friendly units from hidden enemies appear as Unknown attacker, without enemy identity, source range or hidden details; hidden misses are omitted. Later detection never reveals old hidden information. Stable F1–F4 and E1–E4 labels distinguish mirrored names.


## Maintain Range and priority targets (confirmed November 17, 2026)

- **Tactical HUD (November 24, 2026).** The battlefield fills the whole window; there is no title or status bar and no bottom bar. The time panel, contact cards, unit cards and notice strip float over it as translucent overlays (55% black fill) that darken the field without hiding it. There is no Combat menu button: Escape opens the menu.
- **Unit card focus.** Clicking the card of the one mech already selected (alone, with no enemy inspected and without Shift) carries the camera to that mech instead of reselecting it: the pivot glides there in 0.4 s, easing in and out, with zoom and angle unchanged. Any manual camera move (WASD, middle-drag, `` ` ``, Home) stops the glide.
- **Contact cards.** Each enemy contact gets a small translucent card at the top left of the battlefield, added to the right of the last in the order the contacts were first detected, wrapping to a new row before the time panel. A card shows a silhouette of the mech's front (its front armour by condition once the contact is Detailed, plain grey before) and its chassis name ("Locust"). A lost contact's card stays in its place, dimmed; a defeated enemy's shows its status glyph. The tooltip gives the model once identified. Clicking a card glides the camera to the enemy, as a card focus does, and inspects it in the sidebar while keeping the selection; a lost contact's card glides to its last known position without inspecting it.
- **Objective and notices.** The mission objective floats as outlined gold text at the top left, below the contact cards: the patrol's progress on a patrol (*Patrol point 2 of 3*), otherwise the mission's objective, or *Defeat the opposing force.* in a practice battle. Notices for the player, such as a refused jump, show in red in a small translucent strip centred below the time panel for 2.5 s, then fade out over 0.5 s.
- **Global focus.** The tactical view has one global focus. Its panels (time panel, contact cards, selected-unit sidebar, unit cards, battle log, controls panel) never take keyboard focus, so every tactical hotkey (selection, orders, Space, `,` and `.`, V, WASD and arrows, Q/E, `` ` ``, `~`, `?` and Home) works whatever was last clicked. Only a modal holds them back: opponent or force selection, the Combat menu and its Audio settings, and the results.
- **Controls panel (confirmed November 18, 2026).** `?` (Shift+/, or whichever key types `?`) shows or hides a controls reference over the right edge of the battlefield, beside the sidebar (or at the edge while the sidebar is hidden); the battlefield keeps its size. It is not a modal: the battle keeps running, and orders, selection, camera and every hotkey still work while it is open. Escape closes it before opening the Combat menu (an armed jump is cancelled first), and so does its × button. It closes when a modal opens or a new battle starts. Grouped sections list every hotkey and mouse action: selection, orders, firing and weapon toggles, the heat limit, speed (the speed gauge, X/C, Maintain Range and its slider, Pause, 1x and 4x) and the camera and panels. In co-op it adds V and notes that each player has their own pause and speed pick, and that the battle runs at 4x only once every player picks it. There is no hint bar along the bottom of the screen: the unit cards sit at its foot. The battlefield shows no corner text either (terrain seed, terrain legend or sensor ranges in words); the seed and biome are in Mission Details, and sensor ranges show only as rings.
- **Selected-unit sidebar (confirmed November 17, 2026).** The sidebar floats over the battlefield's right edge, above the unit cards, and is hidden entirely while no friendly is selected and no enemy inspected. A centred title (callsign and chassis, or the selection count) and the paperdoll stay pinned at the top; everything below scrolls, padded 10 pixels inside the scroll area. A friendly selection's details open with the MechWarrior laid out as in the hangar: a 72-pixel portrait beside callsign, name and skills, with the skill budget as its tooltip and no assignment controls. Below it, one line gives model, status and stance, then the target line (target or aim preview and range, or no contact) and the line-of-fire, vegetation or indirect-fire line. Order, priority, heat, cooling, ambient, jump, electro-interference and terrain-slowing lines are not shown, and neither are armour/internal tables, a systems list or a ground-marker legend: condition is read from the paperdoll's colours and location tooltips. Inspected enemies show neither the pilot block nor heat text. The sidebar's buttons share their row's width and wrap their labels, and weapon toggles wrap, so the 280-pixel sidebar of small windows never overflows.
- **Maintain Range row.** The selected-unit sidebar has one row: a checkbox, the label *Maintain Range*, a slider and the selected range. The slider runs from 200 m to 1800 m in 100 m steps (the preferred-distance limits of 135–1890 m rounded inward to whole steps), and ignores the mouse wheel, which zooms the battlefield. The reading follows the handle while dragging; the range is sent once, on release. Using the slider also ticks the checkbox. The checkbox is ticked only while every selected mech maintains range, and toggling it applies to all of them. With differing distances the reading says *Mixed* and the handle sits at the primary selection's value. While unticked, the slider stays usable but is dimmed.
- **Defaults.** Every mech starts with Maintain Range on. The loadout default rounds to the nearest 100 m and clamps into 200–1800 m.
- **Movement.** A player mech with Maintain Range on, a priority target and no Move or Follow order keeps its preferred distance from that target: it closes or backs away along the line between them while in sight, and seeks a clear firing position when sight is blocked. When detection is lost it goes to the target's last-known position and waits there, keeping the priority target. Move and Follow orders take precedence; once they end (arrival, or Z), Maintain Range resumes. Unticking it leaves the mech where it is.
- **Firing.** Hold fire stops a player mech firing at anything, its priority target included.
- Enemy AI and patrols keep their internal Attack orders and always maintain range.

## Order shortcuts and condition displays (confirmed November 1, 2026)

Z stops the selected mechs, clearing Move and Follow orders while keeping priority targets; a mech maintaining range then resumes it. X and C set the desired speed to the current walking and running speed, G sets Hold fire and F sets Fire at will. They work while paused, skip defeated units, and do nothing while a modal is open. The Fire at will and Hold fire buttons and the controls panel show the keys.

Alt+right-click and Ctrl+right-click issue the ordinary right-click action (Move, priority target or Follow) after setting the selected mechs' desired speed to their walking or running speed respectively. The setting persists, just as after X or C, so a mech maintaining range closes on a Ctrl+right-clicked enemy at a run. A modified right-drag still orbits. Some Linux window managers reserve Alt+click for themselves, in which case Alt+right-click never reaches the game.

Holding Shift pans the camera with WASD or the arrows at five times the normal speed. Rotation, zoom and mouse panning are unchanged.

Hovering a location of the sidebar paperdoll lists every weapon, component and ammunition bin installed there. Front and rear torso regions list the same location. Identical systems in the same condition are grouped as a count, such as "3 x Medium Laser", and rear-mounted weapons are grouped separately. Each line is coloured by condition only: green when operational, yellow when damaged and red when destroyed. Everything in a lost location is red. Empty ammunition and weapons that are intact but cannot fire stay green. An inspected enemy shows the tooltip only at Detailed contact tier, when its paperdoll is visible.

Each friendly unit card carries a compact paperdoll of its mech's current armor and structure, with no labels or tooltip, beneath a single line naming the pilot's callsign and the mech's chassis without its model ("Kestrel · Locust"). Destroyed and disabled mechs keep theirs.

- **Card layout (confirmed November 26, 2026).** Unit cards sit a fixed gap apart: 2% of the window width, rounded to whole pixels. The row is left-justified, with the same gap between the window's left edge and the first card. Cards grow only up to 260 pixels wide, and the row ends at least one gap short of the details panel's column, whether or not the panel is shown, so selecting never resizes the cards. A narrow window shrinks them below 260 pixels, keeping the same gap.
- **Details panel height.** The details panel runs from 8 pixels below the window's top edge down to its bottom edge, beside the unit cards.
- **Status glyph (confirmed November 24, 2026).** The card's top left corner shows the mech's most pressing state as a tinted glyph, mirroring the stress face in the top right corner: Destroyed, then Disabled, Surrendered, Fled, Extracted, Fleeing, Shutdown and Prone. An active mech on its feet shows none. The card's tooltip keeps the full model, status, prone countdown and Fleeing text, with stress and heat.

### Heat and jump-fuel bars (confirmed November 17, 2026)

Every paperdoll, on the unit cards and in the right sidebar, has two vertical bars without text or numbers: jump fuel on the left of the doll and heat on its right. Each bar fills from the bottom over a dark outlined track, with a small icon beneath it.

- **Jump fuel** shows the jets' recharge: empty from takeoff, refilling over the recharge after landing, and full once recharged. It does not show other reasons a jump may be refused, such as a fall or shutdown. It is pale blue with a pale-blue water drop. A mech without jump jets keeps the slot empty so every doll lines up. The game has no separate fuel resource.
- **Heat** is full at the shutdown threshold. Light ticks mark heat 10 and 20. The whole bar and its thermometer icon are yellow below 10, orange from 10 and red from 20.
- **Heat limit.** On the sidebar doll of a friendly mech still in the battle, a white line across the heat bar and a pointer beyond its right edge mark the [heat limit](#heat-buildup-and-dissipation-confirmed-october-29-2026). Dragging the pointer, or clicking anywhere on the bar, sets the limit in whole steps from 0 at the bottom to 30 at the top, also while paused. Hovering shows "Heat limit N", and the number shows beside the pointer while dragging. Unit cards and inspected enemies show no limit.
- Shut-down, disabled and destroyed mechs show their real values without special styling.
- An enemy inspected at Detailed tier shows its heat bar but no fuel bar.

Unit cards no longer print heat; their tooltip keeps the exact figure. Sizes and colours are in [model and paperdoll details](mech-models.md#shared-paperdoll).

### Speed gauge and desired speed (confirmed November 18, 2026)

The sidebar doll carries a horizontal speed gauge along its foot. It is part of the pinned doll, so it never scrolls with the details. To its left is a monochrome sprinting-figure icon with speed lines, tinted like the bar.

- **Fill.** The gauge fills from the left with the mech's actual speed, full at 150 km/h. Reverse travel fills the same way, as a positive speed. Forward travel is green and reverse dark green, and the icon takes the same colour.
- **Notches.** Small notches mark every 20 km/h. Taller white notches mark the mech's current walking and running speeds: they include heat and leg or critical damage, but not shutdown or a fall, and move as those change.
- **Desired speed.** On a friendly mech still in the battle, a white line across the gauge and a triangle beneath it point at the desired speed. Dragging the pointer, or clicking the gauge, sets it in whole km/h for the whole selection, also while paused, and the value shows while dragging. It snaps to the walk and run notches and is held between 10 km/h and the current running speed. The desired speed is a fixed km/h: a mech travels at the lower of it and its current running speed, and in reverse at the lower of it and its walking speed. It starts each battle at the walking speed and is not saved. X / C and Alt / Ctrl+right-click set it to the mech's current walking / running speed.
- **Walking or running** comes from actual speed, not from the setting: above the unheated walking speed a mech is running, for heat and for to-hit modifiers alike.
- **Tooltip.** Hovering the gauge shows the gait, the actual speed and the desired speed, with reverse written negative: "Running 62 km/h / 70 km/h", "Reverse -16 km/h / 30 km/h", "Walking …" or "Stopped 0 km/h / 40 km/h". An inspected enemy's gauge has no pointer, and its tooltip leaves out the desired speed.
- **Unit cards (confirmed November 23, 2026).** Every unit card also shows the speed gauge and, beneath it, the ammo gauge under its compact doll, at about half the sidebar's size, with the same fill, notches, ticks, icons and colours. They only display: the pointers sit at the mech's desired speed and ammo economy and cannot be dragged, there is no tooltip, and clicks still select the card. A mech without ammo weapons keeps a blank ammo row so every card lines up. Cards of other players' lances, of mechs still dropping and of defeated mechs show them too; a defeated mech's has no pointers. When the cards are too short for the doll to keep its schematic, both rows are left out together.
- **No movement buttons.** The sidebar has no Walk, Run, Jump or Stop movement buttons; X, C, J and Z do those jobs. The group readout gives the desired speed ("Speed: 50 km/h" or "Mixed").

### Ammo economy (confirmed November 18, 2026)

Each mech has an ammo economy setting: a whole step from 0 to 10 that decides how freely its MechWarrior spends ammunition. It starts every battle at 0, is not saved, and the enemy AI keeps 0.

- **Rule.** Only ammo-fed weapons (autocannons, LRMs, SRMs and machine guns) are restricted; lasers, PPCs and flamers fire as before. A shot's *penalty* is everything its [target number](combat-tuning.md#weapons-and-hit-rolls) adds to gunnery and the base aim modifier: range band, minimum range, vegetation and indirect fire, the shooter's heat, PPC interference and critical damage, and both mechs' movement or jumping. The mech's own condition counts too. Step *n* accepts a penalty of at most 10 − *n*: at 0 every shot that can hit is taken (no shot over +10 can hit), and at 10 an ammo weapon fires only a perfect shot, at short range with neither mech moving, no cover, a cool shooter and no damage. Heat limits still apply on top.
- **Held weapons.** An ammo weapon the economy holds back is not chosen at all: it spends no ammunition and no heat, stays ready, and the volley carries on with the next weapon in heat order. Its sidebar status reads "Saving ammo (+4 over +2)".
- **Gauge.** Beneath the speed gauge, pinned with the doll, a yellow horizontal gauge shows the ammunition left: shots remaining in intact bins over the stock load, so destroyed bins show as losses. To its left is an icon of three upright shells, also yellow. Ticks mark the steps. On a friendly mech still in the battle a white line and triangle beneath the track mark the economy; dragging the pointer, or clicking the gauge, sets it in whole steps for every selected mech with ammo weapons, as the speed gauge sets the whole selection's speed, also while paused, and the step shows while dragging. Fully left is no restriction and fully right optimal shots only. The sidebar shows the gauge whenever any selected mech has ammo weapons: its fill, tooltip and pointer belong to the first selected mech if it has them, otherwise to the first selected mech that does. The gauge is left out when no selected mech has ammo weapons, and an inspected enemy's has no pointer.
- **Tooltip.** "No restriction · 140/150 shots", "Economy 4 · fires at +6 or better · 140/150 shots" or "Optimal shots only · 140/150 shots"; an enemy's gives only the shots. The group readout adds "Ammo economy: N" (or Mixed) over the selected mechs with ammo weapons, and leaves it out when none has them.

A mech travelling in reverse takes the running penalty on its own shots, whatever its selected mode. As a target it still counts as walking, unless it is set to Run. Reversing heat is unchanged.

## Stress, surrender and ejection (confirmed November 20, 2026)

Fewer mechs should end a battle destroyed and fewer MechWarriors dead. MechWarriors grow stressed, may eject, and enemies may surrender or flee. The same rules apply to both sides except where stated; the numbers are in [combat tuning](combat-tuning.md#stress-and-nerve-checks).

**Grit.** A fourth skill, rated 2–6 like the others (lower is better) and shown as a word everywhere skills are. It is the MechWarrior's determination under stress. Generated MechWarriors buy Grit like any other skill; enemy pilots take their rank's rating for every skill, Grit included.

**Stress.** Each MechWarrior is Calm, Steady, Excited, Stressed or Panicked, starting every battle Calm. Calm, Steady and Excited change nothing. Stressed plays every skill but Grit one rating worse, Panicked two: Gunnery for hit chances, Piloting for knockdown checks and stand-up time, Sensors for detection. A Green pilot can play worse than Green.

**Nerve checks.** Every 5 seconds of battle time, each MechWarrior still in the fight makes a nerve check: 2d6 + Grit + modifiers. A result of 11 or less moves them one step toward Calm, 12–14 leaves them as they are, and 15 or more moves them one step toward Panicked. (Lowered from 14 / 15–17 / 18 on November 21, 2026: an ordinary firefight averaged about 13, so almost nobody ever grew stressed.) Each mech's clock starts at a random point in the first 5 seconds, so the field does not roll together, and the clock stops while the battle is paused. A [passive](#patrols-confirmed-november-13-2026) patrol group, unaware of the battle, makes no nerve checks until it wakes. Every modifier makes calming harder, apart from the last:

- +1 per 10 heat at the moment of the check
- +1 per destroyed location
- +1 per injury to the MechWarrior, and +1 more on the first check after each new injury
- +1 per 200 damage taken since the last check (tenths of a stock point, as the log counts it)
- +1 if knocked down since the last check
- +1 if the head took damage since the last check
- +1 if shot at since the last check, hit or miss; otherwise −1, so a pilot pulled out of the fight calms down
- +1 if any mech of their own side has been defeated (disabled, destroyed, surrendered, ejected or fled; extracted mechs do not count), however many, and +1 more for each one lost since the last check (changed November 21, 2026 from +1 per mech lost on every check, which panicked whole sides once a few mechs fell)
- +1 if their side's fighting mechs are outnumbered by the enemy mechs their side can see

**Breaking.** A MechWarrior already Panicked whose check comes to 15 or more has an even chance to eject (since November 21, 2026; before, they always did). Otherwise they hold on, still Panicked, and the combat log says so ("… = 16 · Panicked · holds on"). Managing stress is the player's task: pull stressed and panicky pilots back from the front line.

**Damage thresholds (confirmed November 21, 2026).** Pilots ejected before it was reasonable, so two levels now wait on damage taken this battle; damage a mech brought into the battle does not count. A pilot whose mech has lost no armor or structure grows no worse than Excited: a check that would make them Stressed leaves them Excited. A Panicked pilot whose mech has lost no internal structure has no chance to break: a check of 15 or more leaves them holding on. Both sides follow these rules; an enemy's ejection on a crippled center torso (below) is unaffected.

**Ejection.** The MechWarrior leaves the cockpit and survives, Injured (one injury, 10–20 days). The mech is left disabled where it stands, with its cockpit needing repair; an enemy's can be claimed whole as salvage, and the pilot escapes, counting as spared. A MechWarrior ejects:

- when the player presses **Eject** in the sidebar twice (the first press arms it for 4 seconds), for every selected mech still in the fight;
- automatically, always, when the center torso is destroyed, before any overkill reaches the head. Losing the head directly, or a cockpit critical hit, still kills;
- when breaking from panic;
- for an enemy, when its center torso is crippled and it expects its side to win (below).

No kill is credited for an ejection that no hit caused; a destroyed center torso still credits its shooter.

**Enemy surrender.** Only enemies surrender, independently of nerve checks:

- An enemy that loses its last weapon offers to surrender at once.
- On each nerve check, an enemy whose center torso keeps half its stock structure or less judges the fight: its side's fighting mechs against the enemy mechs its side can see, each counted as tonnage × the share of its stock structure left. If the other side is stronger, it offers to surrender; otherwise it ejects.
- An enemy offers once per battle.

The offer pauses the battle for every player until it is answered. A notice over the battlefield names the mech and why it gives up. The leader sees **Accept** and **Refuse**; other players see *Waiting for the leader to answer…*. Answering lifts that pause, leaving every player's own pause toggle as it was; several offers are answered one at a time. A battle without a screen (tests, headless runs) refuses every offer at once.

- **Accepted:** the mech powers down where it stands, lowers its arms, and shows a white flag (⚑) on its ground label. It is defeated, can no longer be targeted, is not salvageable, and earns nobody a kill. Its MechWarrior is a hostage, ransomed after a victory (see [Quick Launch](design.md#quick-launch-confirmed-november-9-2026)).
- **Refused, still armed:** it fights on, and may still eject later.
- **Refused, unarmed:** it **flees**. It runs to the nearest point outside the longest weapon range (plus 60 m) of every enemy its side knows of, then on directly away from them to the arena edge, where it disappears as *Fled*. A fled mech is defeated, cannot be salvaged, counts toward the "enemy mech allowed to flee" reputation, and its pilot counts as spared. It can still be shot down on the way, and its pilot may still eject. If it is the last enemy, the battle is already won: the wind-down ends before it gets far, and it stays on the field to be salvaged.

**Display.** A friendly MechWarrior's stress shows as a face: in the top right corner of its unit card, right after its callsign above the mech on the battlefield, and above the sidebar paperdoll (hover for its effect and the pilot's Grit). Calm smokes a cigarette, Steady looks normal, Excited has wide eyes and a slightly open mouth, Stressed bares clenched teeth and Panicked screams. The faces are tinted Calm pale green, Steady green, Excited yellow, Stressed orange and Panicked red; a defeated mech shows none. Stress also appears in the sidebar readout with its penalty. **Enemy stress is never shown.** The combat log records friendly stress changes with the roll and its modifiers ("Nerve check · rolled 9 + Grit 4 (heat +1, under fire +1) = 15 · Steady → Excited"), and every surrender, refusal, ejection, flight and departure. The debug log records every nerve check for every mech, including those that leave the level unchanged ("… = 12 · Steady"), so enemy stress appears only there. An ejecting mech fires a seat from its head on a smoke trail, with a sound; a surrender plays a power-down sound. Ground labels add *· Fleeing*, and *· Ejected* on a mech its pilot left intact; a wreck whose centre torso was destroyed reads as any other wreck, even though its pilot ejected. The results show the pilot as *Ejected* or *Taken hostage*. Co-op states carry stress, ejection, surrender, flight, the waiting offers and the events added since the last state sent, as the shot log is.

## Jumping (confirmed November 2, 2026)

Jump is an explicit order, not a movement mode. J arms jump targeting; the next right-click on the ground sends every selected mech able to jump on one ballistic jump toward that point, and disarms it. Right-clicking a mech keeps targeting armed. Escape or selecting other mechs cancels it, and it lapses once no selected mech can jump. When J is refused, or armed targeting lapses, the notice strip shows why for three seconds (for example "Cannot jump: Hawk: Recharging") and the error cue sounds. The fuel bar shows only the recharge, so a prone or shut-down mech or lost jets can refuse a jump while it reads full. Heat never refuses one. While armed, the battlefield draws each able mech's range circle and its landing point under the pointer.

- A jump's range follows the mech's effective jump MP: its loadout's jump MP less every jet that is destroyed or in a lost location, halved with a lost leg. A click beyond range lands at maximum range along the line. Each mech lands on free, passable ground: the clicked point if it can, otherwise the nearest free point in range, never on another mech or another jump's landing point. A group spreads its landings as a group Move does.
- Flight is a ballistic arc over rocks, slopes, water, vegetation and other mechs, rising high enough to clear every rock and ridge beneath it. The mech faces its landing point and cannot steer or turn its legs in flight; its torso still twists and its weapons still fire. Disablement or destruction in the air never stops the landing, and a wreck lands as a wreck. Heat never shuts a mech down in the air: one at or above shutdown heat in flight stays active and shuts down as it lands. An airborne mech blocks no one; its landing point is treated as occupied.
- A jump costs heat at takeoff and the jets must recharge after landing before the next jump. Unlike a weapon, a jump starts whatever the mech's heat and heat limit; it may carry heat past the limit, and a mech it takes to shutdown heat finishes the jump and shuts down on landing. It generates no walking or running heat in the air.
- Jumping is a manoeuvre: Attack and Follow orders and Maintain Range resume on landing, while a Move order ends with the jump and the mech holds at its landing point. A new Move given in flight is carried out after landing.
- An airborne attacker takes a jumping penalty to hit instead of its walking or running penalty; an airborne target is likewise harder to hit than a walking or running one. Jumping counts as motion for laser burns.
- A jumping mech's eyes rise with it, so it can see, detect, fire and spot over rocks and hills mid-flight.
- Enemy AI jumps toward its movement goal when its jets are ready, its heat is within its heat limit, and either walking there means a long detour or a rock or hill hides its attack target.
- The model springs from a crouch, tucks its legs through the apex, reaches for the ground and absorbs the landing, with dust where it takes off and lands. Rocket exhaust fires from every location holding jump jets: torso jets from low on the back, angled slightly rearward, and leg jets from the soles. Each plume leaves its nozzle white-hot with shock diamonds, flares and fades through yellow and orange to a dull red tail. Its length follows the thrust (full at takeoff, idling through the apex, braking before touchdown), its width follows the location's working jets, and it flickers out of step with the other plumes. Destroyed jets narrow their plume, and a location whose jets are all destroyed or lost goes dark. Selected details report jump range and whether the jets are ready or recharging.

## Metric distances and mech scale (confirmed October 31, 2026)

Every combat distance, range, speed and size is expressed in metres, and one world unit is one metre. Earlier values convert at 30 metres per former distance unit, so ranges, speeds, sensors, the arena, terrain features and follow spacing play exactly as before. Each stock catalog range point becomes 90 metres in combat: the existing threefold [engagement spacing](#engagement-spacing-confirmed-october-29-2026) at a nominal 30 metres per point. Weapon readouts, the preferred-distance control, sensor and range overlays and the battle log all show metres.

Mech models are half their former size relative to the battlefield, at 15 metres per authored model unit, so they stand roughly 36–55 metres tall instead of 72–108. Everything tied to a mech's body shrinks with it: occupied radius (9.75 metres), the detection and firing height (22.5 metres), rock clearance, group-move spacing, battlefield markers and every weapon and destruction effect. Mechs therefore pack closer together, and hills and rocks block sight more often than before. Legs cycle twice as fast at the same ground speed, so feet stay planted. The hangar preview frames the model as before. See [current tuning values](combat-tuning.md).

## Lances, commands and pause toggles (confirmed November 9, 2026)

The first step of [co-op multiplayer](adr/0003-cooperative-multiplayer.md):

- **Lances in battle.** Each friendly mech fights for the lance it deployed from. That lance's player slot commands it, and each lance deploys at most four mechs.
- **Commands.** Orders and settings (move, follow, jump, hold, priority target, movement mode, firing, heat limit, preferred distance, Maintain Range and weapon toggles) are commands sent from the local player's slot. A command acts only on that slot's own lance and ignores other ids.
- **Pausing.** Each player in the battle has a pause toggle, and every toggle starts on. The battle runs only while every toggle is off. The pause button shows the local player's own toggle, and opening the combat menu turns it on until **Resume** restores it.

## Co-op battles (confirmed November 9, 2026)

The third step of [co-op multiplayer](adr/0003-cooperative-multiplayer.md); deployment and settlement are in the [design](design.md#co-op-missions-confirmed-november-9-2026).

- **One battle.** Only the leader's machine steps the battle. Every other player holds a replica: the same deployment, recreated from the leader's mechs and seeds, which never steps itself. Fifteen times a second the leader sends the battle's state: every mech's position, headings, orders, settings, heat, damage, ammunition, weapon cooldowns and jump arc, every contact, the pause toggles, and the log entries since the last state. A replica fires each new entry's shot effects and logs it once, in order. Between states its mechs glide toward their latest pose, so movement stays smooth. Pathing and enemy AI stay on the leader's machine.
- **Orders.** A player's orders, settings and pause toggle go to the leader's battle, and their effect shows when the next state arrives. Each player selects and orders only their own lance: the unit cards and 1–4 keys cover it, and other players' mechs cannot be selected. Each lance's mechs are outlined, and ringed at their base, in that lance's own [colour](mech-models.md), distinct from every other lance and from the opposition. Every player shares the company's vision and has their own camera and selection.
- **Pausing.** The Pause button's tooltip names whoever else holds the battle paused. A player who disconnects holds it paused.
- **Pings.** V marks the ground under the cursor for every player with pulsing rings and the player's name, fading after five seconds. Your own pings are gold, other players' blue.
- **Leader controls.** Only the leader withdraws: the other players' **Withdraw** is disabled. Until the settlement arrives, their results show *Waiting for the leader to settle the mission…*, or, on a won mission with something to salvage, the salvage list, read-only. Their return button reads *Waiting for leader*.
- **The whole company** is listed on the results' Outcome tab under *THE COMPANY*, and each lance's table on the Mechs tab names its player.

## Patrols (confirmed November 13, 2026)

A [patrol](design.md#patrols-confirmed-november-13-2026) is fought on its own layout. The numbers are in [combat tuning](combat-tuning.md#patrols).

- **Layout.** The company's [drop zone](#drop-phase-confirmed-november-23-2026) lies on a random start edge; lances without a chosen site drop around a random point along it. The extraction zone touches a different edge. The patrol points and the extraction zone lie wholly outside the drop zone, and a group's post moved into it is moved back out. The patrol points lie at least 400 m from every edge and, where they fit, 800 m from each other, the start and the extraction zone. They are then visited in the order that walks least from the start, through every point, to extraction, measured along the battlefield's paths. Terrain generation keeps the start, every point and the extraction zone clear and connected. Everything follows from the terrain seed, so co-op replicas rebuild the same layout.
- **Enemy groups.** One group guards each leg between consecutive locations, posted around the leg's midpoint, moved sideways by up to 300 m, each mech facing a random way. There is no group between the deployment and the first point.
- **Passive.** A group starts passive: powered and ready, but unaware of the company. It stands still and neither turns, twists its torso nor fires, and it ignores what the rest of its side has detected.
- **Waking.** A group becomes active, and stays active, when one of its own mechs detects a company mech, or one second after an attack on one of its mechs resolves, hit or miss, even from beyond its sensors. An attack resolves when the shot or salvo lands or reaches its miss point, at once for a laser. Only that group wakes.
- **Hunting.** An active group's mechs act individually with the ordinary enemy AI on their side's shared contacts, attacking the nearest company mech it detects. With none detected, the group heads for its freshest unsearched lead: a company mech's last known position, or where fire on it came from. Once there it searches, each mech moving between random points within 500 m. If nothing is found within 30 seconds of arriving, the group returns to its posts, still active and ready to react to its side's contacts. Any new detection ends the search.
- **Visiting.** A company mech within 100 m of the current patrol point visits it; points count only in order. The point turns from gold to green, and the log notes it.
- **Extraction.** A company mech entering the extraction zone, at any time, leaves the battle for good with its current damage, as *Extracted*. It can no longer be seen, targeted or ordered, and an attack already on its way to it does no damage.
- **Outcome.** The patrol is decided once no company mech remains on the field, after the usual three-second wind-down. It succeeds if every patrol point was visited and at least one mech extracted; it fails if the field emptied first through extraction (*Extracted before the patrol was complete*), and is a defeat if nobody extracted. Destroying every enemy does not end a patrol.
- **Display.** Each patrol point is a numbered ring on the ground: gold for the current one, dim for later ones, green once visited. The extraction zone is a blue ring, dim until every point is visited. The objective text reads *Patrol point 2 of 3*, then *Extract: 1 out · 2 still on the field*, and the battle log records each visited point and each extraction. The camera opens over the drop zone for the drop phase.
- **Co-op.** The points are shared by the whole company, each mech extracts on its own, and visited points, extractions, awake groups and the patrol log reach every player's replica.

## Control zones (confirmed November 26, 2026)

A **control zone** is a circle of ground the two sides fight to control. It is generic: a mission sets up its zones and reads their owners, and the zone knows nothing of the mission. Take and Hold uses one; later missions may use several. The numbers are in [combat tuning](combat-tuning.md#control-zones).

- **Control** runs from 0, the opposition's, to 1, the company's.
- **Counting.** Only Fighting units inside the zone count: not defeated, fled, extracted, surrendered or unarmed, and not reinforcements yet to arrive. They are sorted by distance to the centre, and the unbroken run of one side's units at the front **leads**. With N leading units, control moves toward that side at N ÷ 30 per second: a lone unit takes 30 s from one end to the other, two take 15 s, four 7.5 s, with no cap. A unit of the other side nearer the centre ends the run, so the units behind it add nothing.
- **An empty zone** keeps its control.
- **Ownership** changes only at the ends: the company takes the zone when control reaches 1, the opposition when it reaches 0. Between the ends the previous owner keeps it and the zone is **contested**.
- **Display.** Each zone is a ring on the ground, in the company's blue or the opposition's salmon, gold while contested, labelled with its name and the company's share ("OBJECTIVE 45%"). The combat HUD stacks one bar per zone at the top left, directly under the objective text: the zone's name and share ("OBJECTIVE · 45% · CONTESTED") over a bar that fills from the left in the company's colour against the opposition's.
- **Co-op.** The leader steps the zones; each player's replica takes on every zone's control and owner.

## Take and Hold (confirmed November 26, 2026)

A [Take and Hold](design.md#take-and-hold-confirmed-november-26-2026) sends the company to take an objective settlement from its garrison, then hold it against reinforcement waves. The numbers are in [combat tuning](combat-tuning.md#take-and-hold).

- **Layout.** The objective is a settlement of 15–25 buildings, on its own levelled pad, centred within 800 m of the arena centre so a wave has room to arrive on every side. Its [control zone](#control-zones-confirmed-november-26-2026) has a 500 m radius around it, every building inside, and starts held by the opposition. The company drops in its usual edge [drop zone](#drop-phase-confirmed-november-23-2026), which the zone never reaches; there is no enemy drop zone. The drop phase shows the objective's ring. Terrain generation builds the objective settlement in place of the central rendezvous and keeps its streets connected to the company's ground. Everything follows from the terrain seed, so co-op replicas rebuild the same layout.
- **Garrison.** The garrison stands at posts at random inside the zone, at least 40 m apart, on ground connected to the company's, each mech facing a random way. It is alert from the start. A garrison mech attacks the nearest company mech its side detects within 250 m of the zone's edge; beyond that leash, or with nothing to attack, it returns to its post.
- **Waves.** The waves exist from the start but are **inbound**: off the field, unseen, not solid and out of the fight. They start only once the company first takes the objective. Each wave arrives at whichever comes first: 3 minutes after the previous wave arrived (for the first, after the capture), or 5 seconds after no enemy on the field is fighting. Destroying the garrison before the capture brings no wave until the capture happens.
- **Arrival.** A wave arrives 1,500 m beyond the zone's edge, on a fresh random bearing that keeps it 200 m inside the arena's edges, preferring passable ground. It lands in lances of four, each at a 100 m site within 200 m of the arrival point, its mechs at least 40 m from every mech already standing there, on ground connected to the company's, facing the objective. A wave may land in the company's drop zone, on top of company mechs, spaced out among them. Its mechs then need detecting as usual.
- **Wave AI.** A wave mech heads for the objective's centre, firing at whatever it can engage on the way. Inside the zone it attacks the nearest company mech its side detects within the garrison's leash, and otherwise keeps heading in until it is within 250 m of the centre.
- **Victory** comes once every wave has arrived, no enemy is fighting and the company owns the objective, after the usual three-second wind-down.
- **Defeat.** The enemy retaking the objective (control back to 0) at any time after the company first took it fails the mission at once, after the wind-down; so does the company being beaten. There is no time limit, and Withdraw still fails the mission. If the company is beaten as the last enemy falls with the objective held, it is a draw.
- **Leading the advance.** The first company mech to come into the zone while fighting is first in; when several come in on the same step, the one nearest the centre. If it stays in the zone until the capture completes, its MechWarrior **led the advance**: a highlight, a line in the battle log, an entry in their [service history](design.md#service-history-confirmed-november-25-2026) and one extra Piloting [skill gain check](design.md#skill-growth-confirmed-november-24-2026). It loses the chance by leaving the zone, by being defeated (destroyed, disabled, ejected, fled or surrendered), or when the enemy drives control back to 0 after it had risen; then nobody earns it that battle, and it never passes to the next mech in. Becoming unarmed in the zone does not lose it. Recaptures earn nothing.
- **Display.** The objective text reads *Take the objective*, then *Hold the objective*, then *Destroy the remaining enemies* once every wave has arrived. Under the control bar a line reads *Reinforcements: 2 waves once the objective is taken*, then *Reinforcements: wave 1 of 3 in 2:14*, then *Reinforcements: all 3 waves have arrived*. The battle log records the objective contested (each time control leaves its owner's end), taken, retaken by the enemy, each wave's arrival and bearing ("Reinforcement wave 1 of 3 arriving from the north-east"), and who led the advance.
- **Results.** The Outcome tab says *The objective was held.* on a victory or *The objective was retaken.* after a retake. The highlights note the capture ("Wolverine WVR-6R (Ace) took the objective.") and each wave's arrival ("Reinforcement wave 2 of 3 arrived from the west."). A wave that never arrived is listed with the pilot status *Not engaged*, cannot be salvaged, and its MechWarriors do not count as spared.
- **Co-op.** The leader steps the battle; every replica takes on the zone, the capture, the arrived waves and their positions, the clocks behind the countdown, the retake, the log, and who led the advance.

## Drop phase (confirmed November 23, 2026)

Every battle opens with a drop phase: each player chooses where in the shared drop zone their lance lands. Simulated Deployment, Quick Launch, missions, patrols, solo play and co-op all go through it. The numbers are in [combat tuning](combat-tuning.md#drop-phase).

- **Drop zones.** The company's zone runs along one arena edge: the whole edge less 200 m of padding at each end, from 200 m to 1,152 m (a fifth of the arena) in from the edge. A standard battle picks the edge at random from the terrain seed, so every co-op replica agrees; the opposition drops in the mirrored zone on the opposite edge, so the sides start at least three fifths of the arena apart. A patrol's zone lies on its start edge, and it has no enemy zone: each enemy group stands at its post as before.
- **Terrain.** Generation keeps lakes, ice, deep water and lava out of both zones, keeps a clear band across the middle of each, and makes sure that band connects to the other zone. Objectives, such as patrol points and the extraction zone, lie outside the company's zone.
- **Drop sites.** A drop site is a circle of 100 m radius. Its centre must lie inside the zone; the circle may reach into the edge padding. Sites may not overlap, so centres are at least 200 m apart. A site must hold enough passable ground, on the battlefield's main connected region, to land the whole lance.
- **Landing.** The lances land when the drop phase ends, not when their sites are chosen. Each mech lands at a random passable point inside its lance's site, at least 40 m apart, facing into the arena and holding. Where each mech lands is unknown and unpredictable until then: the points are drawn afresh as the phase ends, so the same site lands differently from battle to battle, and nothing shows them beforehand. (Whether a site can hold the lance at all is checked against a fixed trial scatter, the same on every machine; if the random draw ever fails to fit, that trial scatter is used.)
- **Choosing.** All players choose at once. A left-click inside the zone places or moves the player's site; other players see it straight away. The cursor previews the site, in red with the reason when it cannot be used. **Ready** locks the site; **Not Ready** reopens it. The leader settles conflicting choices in the order they arrive: a site that overlaps one chosen first is turned away, and that player is told.
- **Starting.** There is no timer. Once every player is ready, the drop phase ends and the battle begins paused, as before. In co-op the leader also has **Force Start**: every lance without a site, including a disconnected player's, drops at a random valid site, chosen sites stand, and the battle begins.
- **Enemies.** Each enemy lance (the opposition in fours, in order) drops at a random valid site in the enemy zone, with the same landing rules.
- **What players see.** The map, its terrain and objectives; the company's zone tinted gold and, in a standard battle, the enemy zone tinted red, without the enemy's sites. Every chosen site is a ring in its lance's colour, labelled with its player's name and a tick once ready. Only the site rings are shown: no friendly mech appears until the drop phase ends and the lances land. Nothing is detected, and the battle cannot be unpaused, until every lance has landed. The camera opens over the drop zone with north up, whichever edge the zone is on, and moves to the player's own lance when the battle begins.
- **Tests and simulations without a drop phase.** A battle created without one lands every lance at once on its default site: evenly 300 m apart along the middle of the zone, centred on it.

## Critical hits (confirmed November 14, 2026)

Damage that reaches internal structure can strike the systems mounted there. The same rules apply to both sides; the numbers are in [combat tuning](combat-tuning.md#critical-hits).

**When to roll.** Each missile that damages a location's internal structure rolls on its own, so a salvo through stripped armor can cause many critical hits. Every other shot rolls once for each location whose structure it damaged, including damage carried on into the next location; a PPC's splash rolls once for each location it damaged, besides the bolt's own roll. A location destroyed by the damage does not roll: everything in it is lost anyway. Armor-only damage never rolls.

**How many.** Roll 2d6: 7–9 deals one potential critical hit, 10–11 two, and 12 three.

**Which slot.** Each potential critical hit picks a random critical slot of the location (6 in the head and each leg, 12 in each torso and arm), empty slots included. A slot already struck this battle is rerolled, so every slot can be struck once; when every slot has been struck, the hit does nothing. A struck empty slot, a slot of an already destroyed item, or a slot of a critically damaged system does nothing. An item split across two locations is one item, struck from either. Which slots were struck is forgotten after the battle; hits and damage levels are not.

**What a hit does.**

- A weapon, heat sink or ammunition bin taking one slot is destroyed. Catalog heat sinks and jump jets are one slot each.
- An item taking two or more slots (larger weapons, a double heat sink) is **damaged** by its first hit and destroyed by its second. A damaged weapon still fires, with +2 on its target number; a damaged double heat sink cools like a single one; a damaged jump-jet group keeps half its jets, rounded down. Destroyed weapons cannot fire, destroyed sinks do not cool, and destroyed jets give no jump.
- A **critical system** (engine, gyro, cockpit, sensors, life support, actuators) gains one damage level per hit, up to its maximum, and is never destroyed by a critical hit. It reads *Damaged* below its maximum and *Critically damaged* at it; further hits on it do nothing. Systems lost with their location still read Destroyed.

| System (maximum levels) | Effect per level |
| --- | --- |
| Engine (3) | +5 heat per heat clock while the mech runs |
| Gyro (2) | −25% top speed, acceleration and turn rate |
| Sensors (2) | +1 on every weapon's target number, −25% sensor range |
| Cockpit (1) | The mech is disabled and its MechWarrior killed; the hit's shooter earns the kill. The mech comes home repairable. |
| Life Support (1) | The MechWarrior is injured the first time the mech's heat reaches 15 while it is damaged |
| Shoulder (1) | +2 on that arm's weapons' target numbers |
| Upper or lower arm actuator (1) | +1 each on that arm's weapons' target numbers |
| Hand actuator (1) | None |
| Hip (1) | −50% top speed and acceleration, as a lost leg, and not on top of one |
| Upper or lower leg or foot actuator (1) | −10% top speed and acceleration each |

Target-number penalties add to each other and to every other modifier. Gyro, hip and actuator damage do not shorten jumps.

**Ammunition explosions.** A hit on a bin destroys it. If it still held shots, it explodes half the time, dealing half its remaining shots × stock damage per shot (a missile bin's shot is a full salvo), ×10. The damage goes straight into the location's internal structure, skipping its armor, then carries on along the usual transfer path. Every location it damages rolls for critical hits of its own, so explosions can chain. The MechWarrior is injured, and the shot that set it off earns any kill.

**Injuries.** Each explosion is one injury, and a damaged Life Support at 15 heat is one more, once per battle. A MechWarrior who survives the battle is Injured for 10–20 days per injury, added together, from the day the mission is settled.

**Display.** The shot's log entry lists each result: "Critical hit: RT Medium Laser damaged" or "… destroyed", "Critical hit: CT Fusion Engine damaged 2/3" or "… critically damaged", and "Ammo explosion · RT LRM 20 ammunition · 600 damage". The paperdoll tooltip shows Damaged (yellow), Critically damaged (orange) and Destroyed (red), with critical systems' level counts. Co-op states carry every item's condition and hits. After the battle, damaged items carry their hits home for [repairs](design.md#hangar-repairs-confirmed-november-13-2026), and items in a lost location count as destroyed.

**Battlefield effects (criticals).** On a friendly or detected mech, each result also shows at the struck location's center, following it as the mech moves or falls as a wreck. A damaged system gives a small yellow-white flash and a few sparks. A destroyed item or a critically damaged system gives a slightly larger flash with a small fireball, more sparks and a brief grey puff. An ammunition explosion is far bigger: a white flash and a second cook-off flash, a fireball sized by the explosion's damage inside a dark-red bloom, a burst of glowing fragments thrown up and outward, embers, and a dark smoke plume that keeps rising from the location for a few seconds and trails behind a moving mech. The flashes and fireballs age with simulation time and the particles stop while the battle is paused, so pausing freezes them.


## Knockdowns (confirmed November 15, 2026)

Heavy damage in a short time can knock a mech off its feet. The same rules apply to both sides; the numbers are in [combat tuning](combat-tuning.md#knockdowns).

**What counts.** Autocannon shells, PPC bolts, LRM and SRM missiles, and ammunition explosions, whatever set them off. Lasers, machine guns, flamers and a PPC's splash never count. A shot counts its full damage, armor or structure alike.

**When to check.** Each mech totals its counted damage from the last second. The hit that takes that total past 20% of its tonnage (in stock points: 10 for a 50-ton mech) makes a Piloting check at once, and the total starts again from zero. A destroyed mech makes no check, and neither does one already prone.

**The check.** Roll 2d6; the mech keeps its footing on a roll of at least its MechWarrior's Piloting plus:

- +1 for each further 10% of tonnage in the total, rounded down
- +3 per gyro damage level
- +2 for a damaged hip
- +1 per damaged upper leg, lower leg or foot actuator
- +5 per lost leg

**Prone.** A mech that fails falls prone. It cannot fire, twist its torso, turn, move or jump; it still spots, stays solid and can be targeted as a stationary mech. Its orders, follows, weapon toggles and heat limit are kept. It stands up by itself after as many seconds as its MechWarrior's Piloting; that time does not run while the mech is shut down, and a disabled mech never stands. A mech that fails in the air finishes its jump and lands prone. Falling does no damage.

**Display.** The hit's log entry ends with the check: "Piloting check · 7+ (dmg +2, gyro +3) · rolled 5 · KNOCKED DOWN" or "… keeps footing". Weapon statuses read Prone, the reason a jump is refused reads Prone, the readouts and the unit card's tooltip add "Prone · stands in 3s" and the card shows the Prone [status glyph](#order-shortcuts-and-condition-displays-confirmed-november-1-2026). The model topples forward about its feet, raises a dust cloud as it lands and lies face down. Over the last moments of its stand-up time it gets up through its joints: it braces its arms against the ground, pushes up onto its knees with its toes dug in, plants its feet in a crouch with its arms out, and straightens. Its feet stay where it fell, so it stands on its own position, and whatever part of it is lowest rests on the ground throughout. Chassis without a joint rig tip back upright in one piece. A mech destroyed while down stays face down. The animation follows simulation time, so it freezes on pause. Co-op states carry who is prone and for how long.
## Post-battle summary (confirmed November 21, 2026)

The results fill 90% of the screen in three tabs, opening on **Outcome**. The action buttons (Take Nothing / Claim Salvage, Return to hangar, or *Waiting for leader*) stay below the tabs on every tab. Simulated deployments, missions and co-op players other than the leader all get the same three tabs; a simulation has no salvage or settlement. The chosen tab and the open weapon tables survive the results being rebuilt, as when the settlement arrives.

**Outcome.** The title, combat time, salvage or settlement text and the salvage picker, terrain seed and biome, as before, then *YOUR FORCE* (or *THE COMPANY* in co-op): each company mech under its lance, on one line coloured by its condition: "Atlas AS7-D · “Ace” Jane Doe: Crippled · Injured (14 days)". The enemy is not listed here.

**Mechs.** One table per lance (titled *Lance 1*, or *Lance 1 · <player>* in co-op) and one for the opposition (*Opposition*, or *Group 1*, *Group 2*… on a patrol), in deployment order, with every enemy detail shown. The columns are Mech, Pilot, Pilot Status, Mech Status, then **Damage Taken** and **Damage Dealt**, each Total / Armour / Internal, then Systems, Comps and Kills (hover a heading for its meaning). Hovering the pilot shows their name, rank (company pilots only) and skills; hovering an injured pilot's status shows their recovery days. Clicking a mech's row opens its weapon table beneath it, and clicking again closes it; any number may be open. The weapon table has a row per mounted weapon, *(destroyed)* when it was lost, with Shots, Hits, Accuracy and the same damage, Systems, Comps and Kills columns; a mech that never fired shows *No shots fired*. A mech's figures are the sum of its weapons'. Values are tenths of a stock point, as the hangar shows armor.

- **Mech status** is the mech's condition as the battle ends against its full loadout (installed armor, full structure, working systems), not what changed in the battle, so damage brought into the battle shows. Worst first: **Destroyed** (head or center torso gone), **Crippled** (any location gone), **Damaged** (structure missing or any system damaged or destroyed), **Scratched** (only armor below installed), **Undamaged**.
- **Pilot status**, worst first: Killed, Taken hostage, Ejected (always injured), Fled, Injured, Extracted, Healthy.
- **Damage taken** is armor (front and rear) and structure lost in this battle, including an arm lost with its torso and ammunition explosions.
- **Damage dealt** is the armor and structure a weapon's hits removed. Overkill and wreck hits deal none. An ammunition explosion is the victim's own damage and is credited to no one, but the weapon whose critical hit set it off is credited with the locations it destroys and any kill.
- **Shots and hits** count each missile of a salvo and each other discharge once; a laser's burn continuation is not a new shot.
- **Systems** counts each item a weapon's critical hits damaged or destroyed, one per damage level on a critical system; items lost with their location do not count. **Comps** (components) counts the enemy locations a weapon's hits destroyed, head and center torso included. **Kills** counts the mechs a weapon's hit disabled or destroyed, as the MechWarrior's career kills do.

**Highlights.** The battle's significant moments in order, one sentence per line after its time ("03:12  Atlas AS7-D (Ace) shut down from overheating."), tinted blue for the company and salmon for the opposition, with full details on both sides. Each names the mech and its MechWarrior's callsign:

- shutting down from heat, once per shutdown
- being knocked down, by which weapon, or in mid-jump
- an ammunition explosion: the bin, its location, its damage and the weapon whose critical hit set it off
- locations lost (other than head and center torso), and to which weapon
- destruction, from the final hit only: "Jenner JR7-D (Viper) was destroyed by Atlas AS7-D (Ace): AC/20 to the CT.", or "…destroyed when its LRM 20 Ammo in the right torso exploded after a hit from …"
- the MechWarrior killed, by the head's loss or a cockpit hit
- disabled without being destroyed
- ejection: from panic, from a crippled mech, on orders, or as the center torso is destroyed
- a pilot's first panic
- surrender offered, accepted (pilot taken hostage) or refused
- fleeing the field, and extracting

Restarts, standing up and lesser stress changes are left out. Co-op states carry each weapon's tally and the highlights added since the last state sent, so every player sees the leader's figures.

## Skill use (confirmed November 24, 2026)

A mission battle counts how each friendly MechWarrior uses their skills, for the [skill gain checks](design.md#skill-growth-confirmed-november-24-2026) made once it is over. Counting changes nothing in the battle itself, and enemies count nothing.

- **Gunnery:** the unit's hits (an LRM salvo once) and kills, as its results already tally them.
- **Piloting:** each [Piloting check](#knockdowns-confirmed-november-15-2026) made and each one passed, and the metres moved while the side identifies a fighting enemy.
- **Sensors:** for each enemy, the first friendly pilot to identify it (the nearest, when several do so at once), and the seconds a pilot spends as the only friendly identifying an enemy that a friendly is aiming at. Both use the pilot's own detection radius and sight, not the side's shared contacts.
- **Grit:** each [nerve check](#stress-surrender-and-ejection-confirmed-november-20-2026) with a result of 12 or more, each time a Panicked pilot holds on, and whether the pilot was ever Stressed or worse.

The checks are drawn from the battle's seed, the unit and these counts, so asking again gives the same results. The mission report carries the successful skills to the campaign. Co-op states carry each pilot's counts, so a joined player's summary rolls the same checks.

