# Stock BattleMech specifications

`mech-catalog.json` contains the mechanical specifications of the twelve stock 3025 variants offered by the initial draft. The four pools contain three 35-ton, three 55-ton, three 65-ton, and three 80-ton BattleMechs. All twelve predate 3025. It includes front/rear armor, standard internal structure, movement points, single heat sinks, weapon locations/arcs/ranges/heat/damage, ammunition bins and installed critical systems. The original descriptive lore, quirks and combat algorithms are not imported.

Mechanical unit definitions are adapted from the [MegaMek Data repository](https://github.com/MegaMek/mm-data), pinned at revision `71fefad545e254b4a224f0d232eaef24473db1ee`. Each entry contains its exact upstream source URL. The upstream authoring references include Technical Readout: 3039 and Technical Readout: Succession Wars. Official comparison sheets are available from the [BattleTech downloads page](https://battletech.com/downloads/), including the [A Game of Armored Combat sheets](https://battletech.com/wp-content/uploads/sites/6/2025/07/RS%20AGoAC%20Epub%201-26-19a.pdf).

The adapted data is attributed to MegaMek Data (C) 2025–2026 by The MegaMek Team and distributed under [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/), as identified in the upstream files. This attribution and license apply to the adapted data; BattleTech, BattleMech and MechWarrior names and underlying rights remain with their respective owners. This project is not endorsed by those owners.

Recreate the catalog from the pinned sources with Python 3 and a network connection:

```sh
python3 scripts/import-mech-catalog.py
```

An explicit `--revision SHA` updates the source revision. The game embeds the resulting JSON into its assembly and does not access the network at runtime. Owned unit damage, ammunition and pilot assignments are separate campaign state. MechWarrior identities and portraits are original project content. The role profiles use G/P 4/5, 3/6 and 5/4 for regular pools and G/P 3/4, 2/5 and 4/3 for the veteran pool; these are authored choices rather than special abilities.
