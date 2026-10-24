# Artwork

## DropShip office

### Integrated controls edit (October 24, 2026)

- Active app asset: `Assets/dropship-office-integrated.png` (1672 × 940), edited with the built-in imagegen tool from the original painting. The original `Assets/dropship-office.png` is retained.
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

- App asset: `Assets/inner-sphere-3025.jpg` (7000 × 6000), a JPEG rendering of the one-page official PDF.
- Source: [Inner Sphere at War: 3025](https://battletech.com/wp-content/uploads/sites/6/2025/03/BattleTech-Inner-Spher-at-War-3025.pdf), linked from the [official BattleTech downloads page](https://battletech.com/downloads/).
- Retrieved October 24, 2026. The original copyright and attribution remain visible in the image; the map screen also links to the original PDF.
- Third-party BattleTech map artwork retains its original rights; this project does not assert ownership or a new license over it. Download availability does not establish a redistribution license.
- Godot imports both images losslessly with mipmaps for smooth downscaling, without reducing their source resolution. The complete chart is retained, including title, legend, and copyright notice. It is a reference image rather than the future playable system/route dataset.

To regenerate the map asset with Poppler after downloading the PDF:

```sh
pdftoppm -singlefile -jpeg -jpegopt quality=94 -scale-to 7000 BattleTech-Inner-Spher-at-War-3025.pdf Assets/inner-sphere-3025
```
