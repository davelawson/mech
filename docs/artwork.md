# Artwork

## DropShip office

- App asset: `Assets/dropship-office.png` (1672 × 940).
- Created with the built-in image generation tool for this project.
- The office’s wall chart is illustrative. The separate map screen uses the official reference chart below.
- The wall-map hotspot is measured against the complete office image: left 14.9%, top 8.3%, width 38.8%, height 45.3%. Its container follows the image’s natural aspect ratio rather than the window’s aspect ratio.

### Final generation prompt

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
