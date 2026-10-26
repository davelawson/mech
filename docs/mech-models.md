# Mech models and condition display

Confirmed October 25, 2026. The AWESOME AWS-8Q is the first custom chassis model. Its hangar illustration supplies the silhouette, proportions, olive armor, amber visor, two side-torso PPCs and right-arm PPC. The left arm ends in a fist. The rear extends that visual language with a reactor housing, cooling vents and separate torso sections. This first pass uses beveled plates, recessed joints and geometric details; fine weathering and progressive material wear are deferred. The AWESOME, Jenner JR7-D, Wolverine WVR-6R and Catapult CPLT-C1 have custom models and procedural walking, running, reverse walking and pivot steps.

## Model contract

`Scripts/MechModel.cs` builds the geometry used by both combat and the hangar. Add future chassis geometry through that factory; share the rendering/condition contract, while authoring each chassis's own proportions and shape. Other chassis currently use the generic geometry. `Scripts/MechChassisModels.cs` contains the Jenner, Wolverine and Catapult geometry and their shared hull, launcher and articulated-leg helpers.

- There are eight `Node3D` location roots. Every mesh, including weapons, visor and joints, belongs beneath one root.
- Shoulder slabs belong to arms; chest PPCs belong to side torsos. Hiding a destroyed location hides all its details. Combat's existing side-torso destruction also removes its attached arm.
- Local forward is negative Z, local up is positive Y, and the mech's anatomical right is positive X. The origin is at ground level. Model dimensions are scaled by the existing tonnage factor.
- `SetCondition` accepts an intact-location predicate from combat or owned condition. The hangar preview shows the selected owned mech's current condition without altering its pristine illustration.
- A pixel-width inverted-hull material pass outlines the armor in the team's color: friendly `#85d0df`, opposition `#ee9c85`. Combat base markers project onto the ground. Allegiance does not recolor the custom chassis’s olive plates.
- The hangar preview rotates automatically while visible. Assignment refreshes preserve its model and rotation. The reference illustration remains alongside the dominant 3D view, with both scaled to fit compact layouts. An Idle/Walk/Run selector previews every articulated chassis’s gaits; it is hidden for generic chassis. Camera framing measures the full geometry and fits all turntable orientations, accounting for broad launcher pods and projecting cannons at both supported window sizes.

The silhouette should read at normal battlefield zoom. Individual visible locations should read through shape, seams and spacing at close zoom. This does not require small surface details to remain legible at the arena overview.

## Additional chassis

The existing hangar illustrations guide silhouette, palette and proportions; catalog specifications determine visible weapon counts and damage-location ownership. The illustrations remain unchanged.

| Chassis | Silhouette and weapons | Movement character |
| --- | --- | --- |
| Jenner JR7-D | Low wedge body, projecting amber cockpit, exposed angular legs and split toes. Two lasers in each stub arm; one center-torso SRM4. | Quick, compact steps and stable weapon stubs. |
| Wolverine WVR-6R | Upright humanoid body, helmet visor, right-arm AC/5, left-torso six-tube shoulder rack and head laser. | Balanced stride; left arm swings while the cannon arm stays braced forward. |
| Catapult CPLT-C1 | Rounded central hull and cockpit, narrow angular legs, enormous arm-mounted fifteen-tube launchers, four torso lasers. | Deliberate steps with restrained body and launcher movement. |

In the current visual scale the Jenner is about 2.40 units tall, Wolverine 2.94 and Catapult 3.16, versus Awesome 3.28. Catapult launchers form the broadest silhouette. These presentation proportions do not change collision radii, movement stats or handling. The Jenner illustration's extra missile apertures and Catapult's sixteen-hole grids are interpreted as four and fifteen actual tubes respectively, matching stock loadouts.

## Shared locomotion

`Scripts/MechLocomotion.cs` uses a joint-position profile and gait profile per chassis. It articulates hips, knees and ankles beneath the existing leg damage roots, and shoulder pivots beneath arm roots. Upper-body weight transfer uses a waist pivot without changing location ownership. The same controller runs in combat and the hangar.

The grounded gait uses short steps, overlapping stance phases, restrained shoulder movement and subtle body sway. Two-link leg articulation derives segment lengths and rest angles from each rig, lifts swing feet and keeps support feet at ground level. Cadence follows signed actual travel speed and model scale. Speeds above walking speed blend into the running stride; reversing plays walking backward. Stationary rotation produces alternating pivot steps. Stopping eases back to the neutral pose. Combat pause freezes the animation while leaving camera controls available.

The remaining generic chassis share acceleration, braking and turn rates in the simulation but retain their existing static geometry.

## Shared paperdoll

`Scripts/CombatPaperdoll.cs` draws the same simplified shape for every chassis. It appears directly below the primary selection's identity in the right sidebar, with a full front schematic and smaller rear-torso inset. Multiple selection follows the first selected mech. Detected enemy inspection uses the same data presentation as friendly selection; lost contacts expose no updating condition panel.

An outer band represents armor and the inner core represents structure, independently. Each uses the stock maximum in `MechSpec.Locations`, including when a unit deploys already damaged. Deployment-time condition is not a maximum.

| Remaining fraction | Color |
| --- | --- |
| Full | Gray `#b4b8b6` |
| Greater than two thirds, below full | Yellow `#f1d34c` |
| Greater than one third, at most two thirds | Orange `#ef8a32` |
| Positive, at most one third | Red `#e84c43` |
| No armor | Armor band disappears |
| No structure | Faint empty location outline |

This follows the MWO visual language described in the [community Mech Manual](https://mwo.fandom.com/wiki/Mech_Manual). These RGB values and thirds-based thresholds are this project's explicit choices, not claimed exact MWO constants. Friendly and enemy numeric readouts both show current/stock front armor, internal structure and rear torso armor. Rear armor reflects stored values; combat's existing front-only damage rules remain in effect.

## Verification

`Tests/ChassisVisualChecks.cs` verifies all three new chassis: stock weapon counts and anatomical ownership, damage hiding, relative proportions, articulated walking/running/reversing/pivoting, stable weapon carriage, neutral settling, pause behavior, complete rotation framing at both window sizes, and the shared combat/hangar factory. `Tests/MechVisualChecks.cs` exercises the common geometry in combat and hangar, weapon handedness, automatic rotation, gait selection, articulated foot contact, paused poses, owned damage, loss of all attached details, stock-relative condition colors and enemy information parity. `scripts/mech test` runs headless smoke checks. `scripts/mech capture` also exercises rendered Compatibility materials and writes each custom chassis’s idle/walk/run hangar captures and mixed-chassis combat captures, alongside AWESOME hangar/combat captures at 1440×900 and 900×500 to `TestResults/`.
