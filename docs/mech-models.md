# Mech models and condition display

Confirmed October 26, 2026. The AWESOME AWS-8Q uses an articulated, optimized runtime copy of `generated/Awesome.glb`, retaining its olive paint textures, broad upright shoulders, recessed amber cockpit and sensor dish, two torso PPCs, right-arm PPC and left fist. The Catapult CPLT-C1 uses assembled MW5 source geometry with this game's olive palette. The AWESOME, Jenner JR7-D, Wolverine WVR-6R and Catapult CPLT-C1 have custom models and procedural walking, running, reverse walking and pivot steps.

## Model contract

`Scripts/MechModel.cs` provides the factory and shared rendering/condition contract used by both combat and the hangar. `Scripts/AwesomeModel.cs` provides the prepared-model loader; it and `Scripts/CatapultModel.cs` load the corresponding GLBs in `Assets/Models/`, attach rigid sections to damage roots and give each instance independent team outlines. Add future chassis geometry through that factory; share the rendering/condition contract, while authoring each chassis's own proportions and shape. Other chassis currently use the generic geometry. `Scripts/MechChassisModels.cs` contains the procedural Jenner and Wolverine geometry and their shared hull, launcher and articulated-leg helpers.

- There are eight `Node3D` location roots. Every mesh, including weapons, visor and joints, belongs beneath one root.
- Shoulder slabs belong to arms; chest PPCs belong to side torsos. Hiding a destroyed location hides all its details. Combat's existing side-torso destruction also removes its attached arm.
- Local forward is negative Z, local up is positive Y, and the mech's anatomical right is positive X. The origin is at ground level. Model dimensions are scaled by the existing tonnage factor.
- `SetCondition` accepts an intact-location predicate from combat or owned condition. The hangar preview shows the selected owned mech's current condition without altering its pristine illustration.
- A pixel-width inverted-hull material pass outlines the armor in the team's color: friendly `#85d0df`, opposition `#ee9c85`. Combat base markers project onto the ground. Allegiance does not recolor the custom chassis’s olive plates.
- The hangar preview rotates automatically while visible. Assignment refreshes preserve its model and rotation. The reference illustration remains alongside the dominant 3D view, with both scaled to fit compact layouts. An Idle/Walk/Run selector previews every articulated chassis’s gaits; it is hidden for generic chassis. Camera framing measures the full geometry and fits all turntable orientations, accounting for broad launcher pods and projecting cannons at both supported window sizes.

The silhouette should read at normal battlefield zoom. Individual visible locations should read through shape, seams and spacing at close zoom. This does not require small surface details to remain legible at the arena overview.

## Detailed Awesome replacement

The original `generated/Awesome.glb` remains untouched: 241,352 triangles, one continuous textured mesh, no rig or animations. `generated/.gdignore` keeps this preparation source out of Godot imports and game exports. The checked-in runtime asset is `Assets/Models/Awesome.glb`; `Assets/Models/Awesome.json` records the source SHA-256, preparation tool, per-part counts and total geometry.

The prepared model contains **89,627 base triangles** within the agreed 60,000–100,000 budget, including dark joint interiors. There are **24 rigid render meshes** across eight damage roots. Parts retain the original 2048×2048 color and metallic/roughness textures and their UV coordinates; simplification interpolates UVs. Geometry and textures are shared across instances, while material overrides and team-outline passes are independent. The outline is 0.55 pixels for Awesome, 0.65 for the imported Catapult and 1.15 for procedural chassis.

The source faces positive Z and is centered vertically. Preparation turns it to negative Z with the cannon on anatomical positive X, grounds both soles, and scales it to 2.8 local units (about 3.64 displayed units with the unchanged tonnage factor). The imported geometry defines the silhouette and proportions. Gameplay stats, collision, weapon behavior and movement rules remain unchanged.

The continuous mesh is cut into rigid thighs, shins, feet, arms and torso/head regions while preserving the authored texture. Cut interiors are capped with dark material, with discreet mechanical joints covering articulation seams. These small interior additions and seam changes are intentional. Hip, knee, ankle and shoulder pivots follow the new proportions using the existing shared movement controller. PPC emitters and the left fist remain named source-mesh sections beneath their owning damage roots; no replacement weapons are overlaid on the asset.

Rebuild using Blender **4.2.3 LTS** (Blender is needed only for preparation, not game builds):

```sh
blender --background --factory-startup --python scripts/prepare-awesome.py
scripts/mech test
scripts/mech capture
```

The preparation script welds coincident positions while keeping per-corner UV seams, cuts exact region boundaries, caps cut interiors, simplifies each rigid section to a shared budget, and exports the runtime GLB and manifest. Keep `Scripts/MechLocomotion.cs`'s Awesome joint profile aligned with the preparation script's normalized joint coordinates when changing the model's proportions.

## Imported Catapult

`Assets/Models/Catapult.glb` replaces the procedural CPLT-C1 in both combat and the hangar. It contains **37,904 triangles in 21 rigid render meshes**, retaining the source silhouette at the existing **3.16 displayed height**. It faces negative Z, is grounded, and owns every mesh beneath one of the eight damage roots. The existing illustration is retained.

Preparation inputs are the local UE Viewer exports in `generated/mw5-catapult/`, excluded from Godot imports and game exports by `generated/.gdignore`. The selection contains the body, exterior `CPLT_TP_Cockpit_DM0_STM` canopy, two launcher meshes, four torso laser meshes and six source texture maps used by the preparation script. Unused emissive maps and the first-person cockpit interior are excluded. `Assets/Models/Catapult.json` records every input hash, per-part counts, attachment choices and normalized joint coordinates.

The stock loadout's logical `Upperarm_*_MH1_lrm15` slots map through `Catapult_HPS` to the **MH3 Missile15 visual meshes**. Source weapon blueprints and shared `Mech_SKL` sockets establish identity attachments onto `Upperarm_Left/Right`; the four lasers attach onto `Torso_Pitch`, and the exterior canopy attaches onto `Cockpit`. `attachments.json` records this chain. Each launcher retains fifteen source firing positions, represented by non-rendering `MissileTube` markers; the visible tube faces are the extracted mesh, not overlaid procedural geometry.

The source body is rigidly weighted, and each triangle belongs wholly to one bone. Preparation groups these source faces into damage locations and thigh/shin/foot sections without cutting or simplifying the armor. Feet include the source toes and talons. Pivot coordinates come from the source skeleton. The shared leg solver chooses its bend direction from each rig's rest pose, preserving the Catapult's rearward knees alongside the existing chassis.

Source UVs and 2048px normal maps are retained. Olive, light olive, steel and restrained wear are baked from the default and wear masks; normal-map green channels are converted from DirectX to glTF convention. The exterior glass is amber. These are authored game materials, not a reconstruction of MW5's full paint shader. Friendly and enemy instances share meshes and textures with independent outline passes.

Rebuild from the retained local exports with Python 3 and Pillow:

```sh
python3 scripts/prepare-catapult.py
scripts/mech test
scripts/mech capture
```

## Additional chassis

The existing hangar illustrations guide the procedural chassis and shared palette; MW5 geometry defines the Catapult's proportions. Catalog specifications determine visible weapon counts and damage-location ownership. The illustrations remain unchanged.

| Chassis | Silhouette and weapons | Movement character |
| --- | --- | --- |
| Jenner JR7-D | Low wedge body, projecting amber cockpit, exposed angular legs and split toes. Two lasers in each stub arm; one center-torso SRM4. | Quick, compact steps and stable weapon stubs. |
| Wolverine WVR-6R | Upright humanoid body, helmet visor, right-arm AC/5, left-torso six-tube shoulder rack and head laser. | Balanced stride; left arm swings while the cannon arm stays braced forward. |
| Catapult CPLT-C1 | Rounded central hull and cockpit, narrow angular legs, enormous arm-mounted fifteen-tube launchers, four torso lasers. | Deliberate steps with restrained body and launcher movement. |

In the current visual scale the Jenner is about 2.40 units tall, Wolverine 2.94 and Catapult 3.16, versus the imported Awesome about 3.64. The Catapult retains its source proportions and broad launcher silhouette. These presentation proportions do not change collision radii, movement stats or handling. The Jenner illustration's extra missile apertures are interpreted as four actual tubes; the imported Catapult has fifteen source firing positions per launcher, matching the stock loadout.

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

`Tests/ChassisVisualChecks.cs` verifies stock weapon counts and anatomical ownership, damage hiding, relative proportions, articulated walking/running/reversing/pivoting, stable weapon carriage, neutral settling, pause behavior, complete rotation framing at both window sizes, and the shared combat/hangar factory. `Tests/CatapultVisualChecks.cs` checks the imported geometry budget, preserved height, exterior canopy, source texture detail, fifteen firing positions inside each extracted launcher, shared meshes and independent outlines. `Tests/MechVisualChecks.cs` measures the Awesome triangle budget, its complexity relative to the procedural chassis, render-mesh limit and full turntable framing. It also exercises owned damage, condition colors and enemy information parity. `scripts/mech test` runs headless smoke checks. `scripts/mech capture` exercises rendered Compatibility materials and writes idle/walk/run hangar captures and mixed-chassis combat captures, alongside AWESOME front/rear hangar and combat captures at 1440×900 and 900×500 to `TestResults/`.
