# Artwork

The retained runtime artwork is `Assets/dropship-office-missions.png`, `Assets/inner-sphere-3025-starscape.jpg`, and the mech illustrations, personnel portraits and hangar background in `Assets/Roster/`. The two active GLBs and their required companion textures live in `Assets/Models/`. Unused office/map iterations, Trellis experiments and the standalone Awesome reference were removed on October 26, 2026. Historical prompts below describe provenance; their intermediate images are no longer retained. Models are rebuilt from external MW5 exports; see [mech models](mech-models.md).

## DropShip office

### Integrated controls edit (October 24, 2026)

- Earlier iteration: `dropship-office-integrated.png` (1672 × 940), edited with the built-in imagegen tool from the original painting. Both intermediate images were superseded by the active missions office and removed during cleanup.
- The brass wall frame holds the complete reference image, including its title, legend and attribution. `CommandApp` shares `MapScreen.Viewport.Texture` through `OfficeScreen.SetWallMap(Texture2D?)`, including reference reload/failure. The wall uses its own fixed 7:6 fit rectangle; the opened map's zoom and pan never change it.
- Calendar, post-it and safe are painted objects with blank surfaces. Only live date, countdown, reminder and balance text is overlaid. Their transparent hotspots retain subtle hover and keyboard focus outlines. The digital balance alone blinks during a pending payment.
- Every region below is in source-image pixels, measured from the top-left of the complete painting. Hotspots, text and image use the same uniform fit scale, without a minimum control scale. Date and balance tooltips provide detail at small window sizes.

| Object / surface | Left | Top | Width | Height |
| --- | ---: | ---: | ---: | ---: |
| Brass chart hotspot | 330 | 84 | 322 | 308 |
| Complete reference-map fit | 347 | 112 | 288 | 246.857 |
| Electronic calendar hotspot | 722 | 168 | 190 | 102 |
| Current-date display | 749 | 194 | 120 | 31 |
| Bills countdown / Payment Due | 749 | 230 | 120 | 29 |
| Post-it hotspot | 767 | 271 | 95 | 40 |
| Post-it label | 769 | 275 | 91 | 31 |
| Recessed safe hotspot | 324 | 447 | 273 | 234 |
| Live balance display | 382 | 476 | 157 | 34 |
| Filing cabinet hotspot | 4 | 329 | 155 | 477 |

The generated chart has a slightly irregular painted inner edge; the exact 7:6 map rectangle sits inside its dark opening with a small dark surround, preserving every part of the reference without cropping or stretching. The calendar and safe sit clear of the chair and desk.

### Image-edit prompts

The final asset was produced through these three built-in imagegen edits, each using the preceding image as its edit target. Coordinates in prompts are requested positions; the measured regions above describe the accepted result.

#### 1. Room objects

```text
Use case: precise-object-edit.
Asset type: painted background for an existing desktop point-and-click game.
Input image 1 is the edit target, the existing DropShip office. Preserve its wide 1672:940 aspect ratio, camera, richly detailed hand-painted style, warm amber lights and cool olive ship shadows. Preserve the desk, chair, filing cabinet at left, bookshelf, porthole, lamp, papers, CRT, conduits, and all foreground furniture in their original positions.
Change only the rear wall's chart and add two physical mounted objects in the space freed around it.
1. Reduce the large chart to a smaller brass-framed, front-facing rectangle. Its EMPTY dark chart opening must have exactly 7:6 width:height aspect ratio for a real map inserted by the game. In original 1672x940 coordinates put the opening approximately x=280 to 742 and y=88 to 484 (462x396). Thin weathered brass frame outside that rectangle. Keep its sides horizontal/vertical, flat and rectangular, without perspective skew. No stars, map, letters, markings or graphics inside the blank opening.
2. Mount a small believable retro-futurist electronic calendar on the wall BETWEEN this chart and the bookshelf, approximately x=790 to 966, y=150 to 315. Weathered metal case, inset blank dark green digital screen with two clear blank horizontal lines of space for live date and countdown text. Screen opening approximately x=802 to 954, y=170 to 254. Attach a physical pale yellow post-it along the calendar's bottom edge, approximately x=820 to 947, y=277 to 320. The post-it is blank, unobscured, lying nearly flat and horizontal for a live reminder label.
3. Paint a recessed metal company safe BENEATH the chart on the left rear wall, approximately x=326 to 562, y=520 to 700. This must be entirely clear of the chair, rolled charts and desk. Scuffed dark metal safe door, recessed heavy hinges, small handle, and a blank wide inset dark green electronic balance display near its top, approximately x=343 to 545, y=542 to 584. Keep the blank display front-facing horizontal rectangle for live numbers.
All three objects must feel physically mounted in the painted room, with shadows, worn materials and matching light. Make blank digital displays dark and uniform. NO headings, NO baked-in numbers, NO readable lettering, no logos, no UI overlays, no HUD, no watermark. Preserve the rest of the original scene.
```

#### 2. Chart height refinement

```text
Use case: precise-object-edit. Edit only the brass chart frame and its blank opening in this office image. Preserve all other pixels and objects: the mounted electronic calendar with blank post-it, metal safe, desk, chair, filing cabinet, bookshelf, porthole, lights, painting style, camera and composition.
The blank chart opening is currently too wide relative to its height. It MUST have precisely a 7:6 width:height ratio, almost square and a little wider than tall. Make it taller UPWARDS, while keeping its left/right edges and bottom approximately where they are. At the image's 1672x940 scale, the blank opening should be x=288 to 686, y=54 to 395 (398 pixels wide, 341 pixels tall). Thin brass frame surrounds that opening. The bottom of the frame remains above the safe, with visible wall space. All four opening edges are horizontal/vertical; no perspective skew. Interior uniformly blank dark green, no lettering or graphics. Preserve everything else.
```

#### 3. Final chart width refinement

```text
Use case: precise-object-edit. Edit this painted office image. Change ONLY the brass wall chart: make it NARROWER by bringing BOTH vertical sides inward. Keep its current top and bottom. The chart opening must be almost square, with width only 16.7% more than height (7:6). In this 1672x940 image the current opening extends roughly x288..687 y99..391; the REQUIRED new blank opening is x315..658 y99..393 (343 wide,294 tall). Its surrounding thin brass frame therefore should extend roughly x299..674, y80..410. Fill vacated wall with matching olive bulkhead. Opening is front-facing, flat, dark, blank, four horizontal/vertical edges. Preserve the entire rest of the scene including safe, electronic calendar, blank yellow post-it, bookshelf, all furniture, chair, desk, porthole, lighting, camera, painted style. No text or numbers.
```


### Original generation prompt

Use case: stylized-concept. Asset type: finished background illustration for a desktop point-and-click BattleTech mercenary strategy game. Create a wide landscape 16:9 painted environment: the empty captain's office aboard an old mercenary DropShip in the year 3025. Grounded retro-futurist industrial science fiction, richly detailed hand-painted game concept art, understated and believable. Wide straight-on view of the room. Worn dark olive steel bulkheads, exposed conduits, modest battered desk occupying lower foreground with papers, warm amber task lamp and an old green CRT; utilitarian captain chair, a small round porthole at right showing a dark starfield. The primary feature is a large framed star chart on the LEFT-CENTER rear wall, occupying roughly x=19% to 54% and y=17% to 57% of the image, clearly visible and unobscured. Its chart face is dark with fine star dots and muted green red blue yellow purple territorial shapes; it only suggests the Inner Sphere at room scale, no readable star names needed. Face of wall map is almost front-facing, rectangular, with simple brass frame. Warm desk light against cool ship shadows, aged metal and scuffed leather. Keep scene readable at screen size, not too dark. Composition includes the entire wall map and enough surroundings to feel like a working office; desk must NOT occlude the wall map. No people, no characters, no weapons in foreground, no UI overlays, no buttons, no HUD, no logos, no readable lettering, no watermark. This is the actual game background, not a screenshot or web page mockup.

## Inner Sphere map

- Original reference: a 7000 × 6000 JPEG rendering of the one-page official PDF. This superseded JPEG was removed; `docs/sources/inner-sphere-3025.pdf` remains the input to the active starscape renderer.
- Source: [Inner Sphere at War: 3025](https://battletech.com/wp-content/uploads/sites/6/2025/03/BattleTech-Inner-Spher-at-War-3025.pdf), linked from the [official BattleTech downloads page](https://battletech.com/downloads/).
- Retrieved October 24, 2026. The original copyright and attribution remain visible in the image; the map screen also links to the original PDF.
- Third-party BattleTech map artwork retains its original rights; this project does not assert ownership or a new license over it. Download availability does not establish a redistribution license.
- Godot imports the active chart losslessly with mipmaps for smooth downscaling, without reducing its source resolution. The complete chart is retained, including title, legend, and copyright notice. It is a reference image rather than the future playable system/route dataset.

To render the original PDF for comparison with Poppler, keep the temporary reference outside the runtime assets:

```sh
mkdir -p TestResults
pdftoppm -singlefile -jpeg -jpegopt quality=94 -scale-to 7000 docs/sources/inner-sphere-3025.pdf TestResults/inner-sphere-3025-reference
```

### Starscape edit (October 25, 2026)

The active shared chart is `Assets/inner-sphere-3025-starscape.jpg` (7000 × 6000). It removes the hex outlines, 2,256 coordinate numbers (including the legend example), the legend hex symbol, and its interstellar-hex explanation. A static near-black navy background carries 1,100 faint decorative stars, smaller and dimmer than named systems. Muted faction fills, lighter borders, labels and compass details retain the original map geometry and lettering. The office wall and opened chart share the new texture, with lossless Godot import and mipmaps.

The final artwork is rendered from the original vector PDF, pinned in `docs/sources/inner-sphere-3025.pdf` with SHA-256 `40966bc3a317e7c7cc071b4c34ed6638a78eac8a659b237ef110393deaedb238`. This allows clean removal of the grid and coordinate forms without reconstructing any systems or labels. The source's capital symbols, title, compass, distance scale, remaining legend, logo and attribution are retained. Poppler text-and-position comparison confirmed that all 2,528 remaining words have exactly their original text and coordinates; the only omitted words are the hex coordinates and six words in the removed explanation.

Regenerate offline from the pinned source with Node.js and Poppler installed:

```sh
node scripts/render-star-map.cjs
```

The script checks the source hash, removes the original grid forms, adjusts vector colors, and adds fixed-seed background stars before rendering the JPEG at its original resolution. Its intermediate PDF is saved under ignored `TestResults/star-map-work/`.

A built-in `image_gen.imagegen` edit was tried first. The full-resolution input was rejected; a smaller reference generated an edit that altered names and positions, so that generated output is **not used by the game**. The final asset uses the original vector drawing commands instead. The attempted built-in prompt was:

```text
Use case: precise-object-edit.
Asset type: production map texture for a Godot game, shared by a zoomable star chart and a framed office wall map.
Input image 1 is the edit target: a full-composition reference of the complete Inner Sphere map.
Primary request: remove the entire baked-in hex grid, every hex coordinate number, and the hex symbol and "INTERSTELLAR HEX = 30 LIGHT YEARS" entry in the legend. Replace the gray backdrop with a near-black navy sparse starscape. Carry this dark treatment through all faction territories: use subdued, translucent-looking versions of their existing blue, red, purple, green, yellow, tan, and teal fills over the dark background. Keep faction borders clearly visible. Add only a few tiny, very dim white background stars, less prominent than the actual named systems. Static artwork; no nebulae, glow clouds, gradients, lens flares or bright decorative stars.
CRITICAL PRESERVATION: This is an edit of the existing map, not a redesign or an approximation. Retain the exact full 7:6 image composition with no crop or stretch. Preserve every actual system dot, its original location and correct readable name, every capital symbol, every faction boundary and island, and all existing map geography. Preserve the title "INNER SPHERE AT WAR: 3025", remaining legend, compass, distance scale, maximum-jump text, credits, BattleTech logo and bottom copyright/attribution. Adapt text, symbols, and borders to clear light colors as necessary for readability against the dark ground. Make the legend dark and visually integrated, with readable light lettering. Use the highest practical resolution and tiny cartographic lettering as faithfully as possible, suitable for close zoom. Do not invent, move, omit, blur, or replace systems or system names. No hexagons, hex numbering, coordinate mesh or grid lines anywhere.
```

## Roster artwork

The roster implementation adds 26 project-local raster assets generated with the built-in `image_gen.imagegen` tool on October 24, 2026. The exact prompt for every asset is recorded in [roster-art-prompts.json](roster-art-prompts.json).

- The former `dropship-office-rosters.png` was an edit of the integrated office that added a physical rolodex and an enclosed repair-bay view beyond the porthole. The active missions office retains those additions; the intermediate office images were removed during cleanup.
- `Assets/Roster/hangar-background.png` is an empty maintenance bay with matching olive steel, amber lamps and painted industrial textures.
- `Assets/Roster/{firestarter,jenner,panther,griffin,shadow-hawk,wolverine,catapult,crusader,thunderbolt,awesome,victor,zeus}.png` are individually generated transparent mech illustrations. They are visual interpretations of the chassis, not technical record sheets; the adjacent classic specifications are authoritative.
- The 24 personnel PNGs in `Assets/Roster/` are individually generated original portraits, named by the warrior IDs in `DraftCatalog`.

All new textures are imported losslessly with mipmaps. Cutout transparency is preserved. No project asset depends on an image remaining in the generator's output directory.

The new office hotspot rectangles in the existing 1672×940 source coordinate system are: rolodex `(695,552,124,104)` and hangar porthole `(1470,70,202,390)`. The live ready-count readout sits on the rolodex plaque and scales with the same artwork transform.

### Awesome reference redesign (October 25, 2026)

The active `Assets/Roster/awesome.png` (1254 × 1254 RGBA) replaces the original illustration with a detailed 3D-render appearance. The design followed a supplied `Awesome.avif` reference: segmented torso and shoulder armour, upright outer shoulder panels, head dish, articulated legs and round knee guards. The standalone reference was removed during cleanup. The active illustration preserves olive drab, dark steel, muted brass and amber, with restrained wear, a grounded three-quarter stance, three PPCs and a lowered left fist.

Generated with the built-in `image_gen.imagegen` tool using the previous Awesome PNG as the palette/material reference and a lossless PNG decode of the AVIF as the primary design reference. A second built-in edit repaired the framing to include the complete shoulder tips and leave transparent margins. The selected output is copied directly into the active asset path with its alpha intact. Existing lossless import and mipmap settings are retained. The separate runtime 3D mech model is unaffected by this raster artwork replacement.

Initial generation prompt:

```text
Use case: sketch-to-render.
Asset type: replacement production BattleMech roster illustration for a Godot game, a single isolated transparent PNG.
Input image 1 (/home/dave/dev/mech/Assets/Roster/awesome.png): PALETTE AND MATERIAL REFERENCE ONLY. Preserve its weathered olive drab paint, dark steel joints, muted brass edges, amber cockpit visor and subtle amber PPC glow. Do not preserve its simpler slab silhouette.
Input image 2 (/home/dave/.codex/generated_images/awesome-inspiration-reference.png): PRIMARY DESIGN REFERENCE. Closely translate this Awesome drawing into a beautifully detailed, high-polygon 3D-render appearance, adopting its new silhouette, proportions and armour geometry. Follow its broad segmented upper torso, tall upright outer shoulder panels, small central recessed head with a small circular dish above it, layered bevelled chest and abdomen plates, complex waist and hip armour, large round knee shields, articulated mechanical legs and segmented broad feet. This must visibly resemble image 2 in form, not merely add texture to image 1.
Subject: a recognizable classic Awesome AWS-8Q, a massive 80-ton humanoid assault BattleMech. Exactly THREE main PPC weapons: one long right-forearm cannon (on viewer left in this view) and two round chest-mounted PPC muzzles, plus an articulated left arm and clenched hand. No missiles or extra guns.
Pose/composition: grounded stable front three-quarter display stance, both feet planted, legs slightly apart, left fist lowered in a natural relaxed display pose rather than raised for combat. Right cannon visible angled forward without obscuring chest. Entire mech including shoulder panels, head dish, arms and both feet visible with comfortable 5-8% transparent margins. Centered square composition at high resolution, ideally 2048x2048. Strong readable silhouette when reduced for roster cards.
Style/materials: polished detailed 3D-render appearance, smoothly rendered bevels and curved joints, convincingly engineered armour with meaningful layered plates, inset vents, panel seams, fasteners, pistons and mechanical joints. Restrained military wear: subtle scratches, chipped edges, dark recesses, broad readable olive panels, no excessive grunge. Warm amber overhead highlights and cool green shadows matching input 1. Dark steel mechanisms, subdued brass trim, amber lights, no new bright colour accents.
Background: genuine transparent alpha cutout, no environment, no ground plane, no cast floor shadow.
Avoid: white or checkerboard baked-in background; text, signature, logos, watermarks, labels, human figures, scenery, smoke, firing beams, explosion, excessive glow, cropped extremities, old simple slab silhouette, low-poly faceting, sketch lines or illustration paper.
```

Final framing edit prompt (input: the first generated candidate):

```text
Use case: precise-object-edit. Input image is the approved design candidate for a transparent game roster cutout. Change ONLY framing: zoom out / expand the square canvas around the mech so the COMPLETE mech fits inside the image with a clearly visible transparent margin of at least 8% of canvas width on ALL FOUR SIDES. Currently the top ends of the tall shoulder panels are cut off at the top edge: complete those panel tops as needed and include them entirely. Both feet, both shoulder tips and the head dish must all be fully visible. The mech should occupy at most 82% of the canvas height and width. Keep the exact same mech design, grounded three-quarter pose, silhouette and proportions, three PPCs, left hand, segmented olive armour, round knee guards, dish, steel joints, muted brass and amber palette, fine details, restrained scratches and realistic high-polygon 3D-render appearance. No redesign, no new weapons. Output a square high-resolution PNG, preferably 2048x2048, with genuine transparent alpha, no ground plane, scenery, shadow on background, text, watermark or checkerboard. This is a framing repair; preserve the mech appearance.
```

## Mission phone artwork

`Assets/dropship-office-missions.png` was generated as an edit of the former roster office with the built-in `image_gen.imagegen` tool on October 24, 2026. The active image is 1673 × 940; existing controls retain the established 1672 × 940 logical artwork coordinates. The existing office composition and openings are preserved. The phone hotspot is `(1116,558,129,83)`; an amber light at local `(85,49,8,6)` flashes in the live interface for unseen offers. The phone is silent. The texture is imported losslessly with mipmaps.

Final prompt:

```text
Use case: precise-object-edit. Asset type: painted point-and-click game office backdrop. Input image 1 is the edit target. Add ONE compact old industrial olive-green corded desk telephone with a clearly visible small dark amber notification lamp on its front face, resting on the rear edge of the desk just to the right of the captain chair and left of the existing lamp, approximately x=1110..1285, y=545..655 in this 1672x940 source. Handset resting on cradle, coiled cord, recognizable physical telephone; lamp unlit so the game can animate it. Match the existing painted retro-futurist industrial style, worn metal, warm amber lighting and perspective. Preserve the exact image dimensions 1672x940 and all existing scene geometry and objects, especially the rolodex at x695..819 y552..656, map frame and blank opening, calendar and post-it, safe and its blank screen, filing cabinet, porthole repair bay, chair, CRT, papers, and desk. Change only the small region needed for this phone; no new lettering, no other props, no people, no red launch button, no UI, no watermark.
```
