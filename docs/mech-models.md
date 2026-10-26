# Mech models and condition display

Confirmed October 26, 2026. Awesome AWS-8Q, Jenner JR7-D, Wolverine WVR-6R and Catapult CPLT-C1 use assembled MechWarrior 5 source geometry with this game's olive palette, amber exterior cockpits and team outlines. All four have articulated walking, running, reverse walking and pivot steps. Their roster illustrations and gameplay specifications are unchanged.

## Model contract

`Scripts/MechModel.cs` provides the factory and shared rendering/condition contract used by both combat and the hangar. `Scripts/AwesomeModel.cs` provides the prepared-model loader; it, `Scripts/JennerModel.cs`, `Scripts/CatapultModel.cs` and `Scripts/WolverineModel.cs` load the corresponding GLBs in `Assets/Models/`, attach rigid sections to damage roots and give each instance independent team outlines. Add future chassis geometry through that factory; share the rendering/condition contract, while authoring each chassis's own proportions and shape. Other chassis currently use the generic geometry.

- There are eight `Node3D` location roots. Every mesh, including weapons, visor and joints, belongs beneath one root.
- Shoulder slabs belong to arms; chest PPCs belong to side torsos. Hiding a destroyed location hides all its details. Combat's existing side-torso destruction also removes its attached arm.
- Local forward is negative Z, local up is positive Y, and the mech's anatomical right is positive X. The origin is at ground level. Model dimensions are scaled by the existing tonnage factor.
- `SetCondition` accepts an intact-location predicate from combat or owned condition. The hangar preview shows the selected owned mech's current condition without altering its pristine illustration.
- A pixel-width inverted-hull material pass outlines the armor in the team's color: friendly `#85d0df`, opposition `#ee9c85`. Combat base markers project onto the ground. Allegiance does not recolor the custom chassis’s olive plates.
- The hangar preview rotates automatically while visible. Assignment refreshes preserve its model and rotation. The reference illustration remains alongside the dominant 3D view, with both scaled to fit compact layouts. An Idle/Walk/Run selector previews every articulated chassis’s gaits; it is hidden for generic chassis. Camera framing measures the full geometry and fits all turntable orientations, accounting for broad launcher pods and projecting cannons at both supported window sizes.

The silhouette should read at normal battlefield zoom. Individual visible locations should read through shape, seams and spacing at close zoom. This does not require small surface details to remain legible at the arena overview.

## Imported Awesome

`Assets/Models/Awesome.glb` replaces the previous generated model in combat and the hangar. The MW5 AWS-8Q contains **18,243 triangles in 19 rigid render meshes** at the existing **3.64 displayed height**. Its stock loadout has a right-arm PPC, one PPC in each side torso and a head-mounted small laser. The left forearm retains the source fist.

Preparation reads external exports at `~/Models/MW5Mercenaries/Game/Objects/Mechs/Awesome/Model/`. `AWS-8Q_Loadout` selects the stock hardpoints; `Awesome_HPS` selects the three `EH1_PPC` visuals and the head `EH1_Laser`. Blueprint defaults identify the source skeletal meshes. Shared `Mech_SKL` sockets attach the right PPC to `Forearm_Right`, both torso PPCs and head laser to `Torso_Pitch`, with identity local transforms. The exterior `AWS_TP_Cockpit_DM0_STM` attaches to `Cockpit`. The laser and exterior cockpit belong to Head for gameplay damage hiding. Source firing points are retained as non-rendering markers inside the prepared weapons.

Source bone weights define the rigid damage groups and thigh/shin/foot sections. Armor faces are neither cut nor simplified. Source pivots replace the generated model's joint coordinates while preserving the heavy gait. Default/wear masks produce olive paint, source UVs and 2048px normal detail remain intact, and exterior glass is amber. All imported MW5 chassis use a 0.65-pixel team outline; generic geometry uses 1.15 pixels.

The superseded generated source is archived outside the project at `/home/dave/Models/Archive/mech-generated-Awesome.glb`. Its runtime texture companions and Blender preparation code have been replaced. Only the current runtime GLB, four required Godot companion PNGs, manifest and preparation code remain in the project.

Rebuild with Python 3 and Pillow:

```sh
python3 scripts/prepare-awesome.py
# On another machine, supply the external Model or Body directory:
python3 scripts/prepare-awesome.py --source /path/to/Awesome/Model
scripts/mech test
scripts/mech capture
```

## Imported Jenner

`Assets/Models/Jenner.glb` replaces the procedural JR7-D in combat and the hangar. It retains **22,932 triangles in 22 rigid render meshes**, grounded and facing negative Z at the existing **2.40 displayed height**. This includes the complete exterior cockpit nose, four arm lasers and the center-torso SRM4. The obsolete procedural Jenner geometry and its unused authoring helpers have been removed.

Preparation reads external exports at `~/Models/MW5Mercenaries/Game/Objects/Mechs/Jenner/Model/`. The stock `JR7-D_Loadout` selects `Torso_Center_MH3_srm4`, which maps through `Jenner_HPS` to the **MH3 Missile4** visual. The four arm slots select `Forearm_Left/Right_EH1/EH2_Laser`. Blueprint defaults identify matching skeletal meshes. Shared skeleton sockets attach the rack to `Torso_Pitch` and each pair of lasers to the corresponding `Forearm_Left/Right`, with identity local transforms. The exterior `JR7_TP_Cockpit_DM0_STM` attaches to `Cockpit`; its glazing is material section 0 and its detailed nose/frame is section 1. The entire exterior cockpit belongs to Head.

Four non-rendering `MissileTube` markers and four beam muzzle markers preserve source firing positions. Source bone ownership groups the body without cutting or simplification; source hip, knee, ankle and shoulder pivots drive the existing quick gait and restrained weapon-pod motion. Paint, normal detail and independent team outlines use the shared MW5 pipeline.

```sh
python3 scripts/prepare-jenner.py
python3 scripts/prepare-jenner.py --source /path/to/Jenner/Model
scripts/mech test
scripts/mech capture
```

Awesome, Jenner and Wolverine share rigid assembly code in `scripts/prepare_mw5_mech.py`; their small preparation entry points specify chassis scale, stock attachments and source evidence. The glTF reader, GLB writer and paint functions are shared with Catapult in `scripts/mech_asset_tools.py`. The refactor preserves the existing Wolverine and Catapult GLBs and manifests byte for byte. Each chassis manifest records every selected input hash, part counts, attachment choices and source-derived pivots. Raw exports and first-person cockpit interiors stay outside the project. Runtime builds require only prepared assets; rebuilding them requires the external exports.

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

## Imported Wolverine

`Assets/Models/Wolverine.glb` replaces the procedural WVR-6R in combat and the hangar. It retains **25,911 triangles in 20 rigid render meshes**, grounded and facing negative Z at the existing **2.94 displayed height**. The roster illustration, gameplay statistics, damage rules and weapon behavior remain unchanged.

Preparation reads the external exports at `/home/dave/Models/MW5Mercenaries/Game/Objects/Mechs/Wolverine/Model/`. Raw exports are not copied into the project. The selected inputs are the body, exterior `WVR_TP_Cockpit_DM0_STM`, three stock weapon meshes, their binary buffers and six body/variant mask and normal maps. The first-person cockpit interior and unused emissive maps are excluded. `Assets/Models/Wolverine.json` records only the inputs actually used, with their hashes, attachment evidence, source-derived pivots and part counts.

The WVR-6R source loadout, hardpoint mappings, visual blueprint defaults and shared skeleton sockets establish the attachments: the right-arm `BH1_AC5` mesh attaches to `Forearm_Right`; the left-torso `MH1_Missile6` and head `EH1_Laser` meshes attach to `Torso_Pitch`. The exterior cockpit attaches to `Cockpit`. All local attachment transforms are identity. The head laser uses the source torso coordinate frame for placement, but belongs to this game's Head damage root. Six `MissileTube` markers and one `CannonMuzzle` marker retain source firing positions without adding visible geometry.

Rigid source faces are grouped by their owning bones without geometric cutting or simplification. Pelvis flaps remain with the center torso; thigh flaps follow the corresponding thigh. The gait uses the source's forward-bending knees, preserves the free left-arm swing and braces the right cannon arm. Source UVs, 2048px mask/normal detail, the shared olive palette, amber canopy and independent team outlines follow the Catapult approach.

The Python glTF reader, GLB writer and paint functions are shared in `scripts/mech_asset_tools.py`. Extracting these helpers preserves the existing Catapult GLB and manifest byte for byte. Rebuild Wolverine with Python 3 and Pillow:

```sh
python3 scripts/prepare-wolverine.py
# On another machine, supply the external Model or Body directory:
python3 scripts/prepare-wolverine.py --source /path/to/Wolverine/Model
scripts/mech test
scripts/mech capture
```

The project retains the prepared GLB, four required Godot companion textures, the manifest and preparation code. Rebuilding requires the external source exports; running the game does not.

## Chassis presentation

MW5 geometry defines all four custom chassis silhouettes; the existing hangar illustrations guide the shared palette. Catalog specifications determine visible weapon counts and damage-location ownership. The illustrations remain unchanged.

| Chassis | Silhouette and weapons | Movement character |
| --- | --- | --- |
| Awesome AWS-8Q | Broad upright shoulders, amber cockpit, right-arm and side-torso PPCs, head small laser and left fist. | Heavy, deliberate steps. |
| Jenner JR7-D | Broad body, projecting amber cockpit nose, exposed angular legs and split toes. Two lasers in each stub arm; one center-torso SRM4. | Quick, compact steps and stable weapon stubs. |
| Wolverine WVR-6R | Upright humanoid body, helmet visor, right-arm AC/5, left-torso six-tube shoulder rack and head laser. | Balanced stride; left arm swings while the cannon arm stays braced forward. |
| Catapult CPLT-C1 | Rounded central hull and cockpit, narrow angular legs, enormous arm-mounted fifteen-tube launchers, four torso lasers. | Deliberate steps with restrained body and launcher movement. |

In the current visual scale the Jenner is about 2.40 units tall, Wolverine 2.94 and Catapult 3.16, versus the imported Awesome about 3.64. The Catapult retains its source proportions and broad launcher silhouette. These presentation proportions do not change collision radii, movement stats or handling. Jenner retains four source SRM firing positions; Catapult has fifteen per launcher, matching their stock loadouts.

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

`Tests/ChassisVisualChecks.cs` verifies stock weapon counts and anatomical ownership, damage hiding, relative proportions, articulated walking/running/reversing/pivoting, stable weapon carriage, neutral settling, pause behavior, complete rotation framing at both window sizes, and the shared combat/hangar factory. `Tests/ImportedChassisVisualChecks.cs`, `Tests/CatapultVisualChecks.cs` and `Tests/WolverineVisualChecks.cs` check the imported geometry budgets, preserved heights, exterior canopies, source texture detail, firing positions inside extracted weapons, shared meshes and independent outlines. Wolverine checks also verify the forward-facing right-arm cannon and unarmed left fist. `Tests/MechVisualChecks.cs` measures the Awesome triangle budget, render-mesh limit and full turntable framing. It also exercises owned damage, condition colors and enemy information parity. `scripts/mech test` runs headless smoke checks. `scripts/mech capture` exercises rendered Compatibility materials and writes idle/walk/run hangar captures and mixed-chassis combat captures, alongside AWESOME front/rear hangar and combat captures at 1440×900 and 900×500 to `TestResults/`.
