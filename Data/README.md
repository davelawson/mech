# Stock BattleMech specifications

`mech-catalog.json` contains mechanical specifications for 49 stock variants. The initial draft samples three candidates from each weight class, including variants introduced after the campaign start in 3025. Records include armor, structure, movement, heat sinks, weapons, ammunition and installed critical systems. The original descriptive lore, quirks and combat algorithms are not imported.

Mechanical unit definitions are adapted from the [MegaMek Data repository](https://github.com/MegaMek/mm-data), pinned at revision `71fefad545e254b4a224f0d232eaef24473db1ee`. Each entry contains its exact upstream source URL. The upstream authoring references include Technical Readout: 3039 and Technical Readout: Succession Wars. Official comparison sheets are available from the [BattleTech downloads page](https://battletech.com/downloads/), including the [A Game of Armored Combat sheets](https://battletech.com/wp-content/uploads/sites/6/2025/07/RS%20AGoAC%20Epub%201-26-19a.pdf).

The adapted data is attributed to MegaMek Data (C) 2025–2026 by The MegaMek Team and distributed under [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/), as identified in the upstream files. This attribution and license apply to the adapted data; BattleTech, BattleMech and MechWarrior names and underlying rights remain with their respective owners. This project is not endorsed by those owners.

Recreate the catalog from the pinned sources with Python 3 and a network connection:

```sh
python3 scripts/import-mech-catalog.py
```

An explicit `--revision SHA` updates the source revision. The game embeds the resulting JSON into its assembly and does not access the network at runtime. Owned unit damage, ammunition and pilot assignments are separate campaign state. MechWarrior identities and portraits are original project content. The role profiles use G/P 4/5, 3/6 and 5/4 for regular pools and G/P 3/4, 2/5 and 4/3 for the veteran pool; these are authored choices rather than special abilities.

## Typed catalog schema

Catalog `SchemaVersion: 1` is independent of campaign save versions. Weapons carry an explicit category, numeric damage per projectile, projectile count, target heat, and a typed ammunition category. Components carry a category and an explicit `HeatSinkCount`, separate from their critical-slot count. Weight class and single/double sink kind are typed categories. Display names remain source text; damage and warrior experience labels are formatted from typed values. Real-time weapon cooldowns remain authored rules in `CombatRules`, keyed by weapon category, and are not attributed to the upstream stock data.

The loader requires the complete schema and validates identities, locations, numeric bounds, categories, ammunition compatibility, sink contributions, and provenance before copying transport arrays into immutable definitions. Definitions are shared safely by owned and deployed units; condition remains separate. Definition and equipment IDs, catalog order, and source URLs are stable. Campaign saves contain only IDs and condition. Armor, structure and damage are stored at ten times the stock values (tenths of a stock point), and the importer cuts machine gun damage to half a stock point; version 5 saves use that scale, and loading an older save multiplies its armor and structure by 10.

Explicit single-sink groups retain their previous contribution, equal to their grouped slot count. Mauler's 11 double sinks remain internal because the existing importer does not publish explicit double-sink component groups. Extending supported critical equipment or changing these mechanics is a separate balance/data change.

For reproducible offline generation, cache the pinned MTF sources as `<mech-id>.mtf`, then run:

```sh
python3 scripts/import-mech-catalog.py --source-dir /path/to/cached-mtf --output /tmp/mech-catalog.json
python3 -m unittest discover -s scripts/tests -v
```

Offline importer checks use attributed mechanical excerpts under `Tests/Fixtures/mtf`. The headless game suite also compares all 49 definitions and 24 warriors against the frozen pre-refactor facts, display text, damage/cooldowns, and provenance; checks malformed catalogs and shared-definition immutability; and round-trips an existing version-four save.


## Star system geography

`star-systems.json` is generated from the pinned official 3025 vector chart in `docs/sources/inner-sphere-3025.pdf`. The source SHA-256 and distance-ruler calibration are recorded in the catalog and checked by `scripts/extract-star-systems.py`. Regenerate with `python3 scripts/extract-star-systems.py` (Python 3 and Poppler's `pdftotext` required). The game consumes the embedded JSON and needs neither tool at runtime.

Each system stores its original name, symbol center, clickable label bounds, and a stable ID derived from source coordinates. Names are not unique identifiers. Positions map directly to the 7000 × 6000 starscape image; the source 120-light-year ruler spans 169.302 PDF points, or 4.898784722 image pixels per light-year. Decorative stars, faction labels, legend samples, and annotations without destination symbols are excluded. The named Brocchi and Enders cluster markers remain selectable destinations. Multiline names and capital symbols require explicit handling in the extractor; regenerating against a changed PDF requires reviewing those associations.

The geography retains the original BattleTech map's rights and attribution; see [artwork sources](../docs/artwork.md).
