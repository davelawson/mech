`catalog-baseline.json` freezes the pre-refactor stock catalog and authored warrior records. Its legacy damage strings are an independent regression oracle, not runtime input. The stock facts retain the attribution and license recorded inside the fixture and in [Data/README.md](../../Data/README.md).

`definition-save.json` uses the existing version-four campaign format with damaged armor/equipment, partial ammunition, an assignment, and a recovering warrior. It contains only definition IDs and condition, never serialized definitions.

`mtf/` contains mechanical excerpts from MegaMek/mm-data revision `71fefad545e254b4a224f0d232eaef24473db1ee`, with upstream notices retained and descriptive lore removed. Sources:

- [Firestarter FS9-H](https://raw.githubusercontent.com/MegaMek/mm-data/71fefad545e254b4a224f0d232eaef24473db1ee/data/mekfiles/meks/3039u/Firestarter%20FS9-H.mtf)
- [Atlas AS7-D](https://raw.githubusercontent.com/MegaMek/mm-data/71fefad545e254b4a224f0d232eaef24473db1ee/data/mekfiles/meks/3039u/Atlas%20AS7-D.mtf)
- [Crusader CRD-3R](https://raw.githubusercontent.com/MegaMek/mm-data/71fefad545e254b4a224f0d232eaef24473db1ee/data/mekfiles/meks/3039u/Crusader%20CRD-3R.mtf)
- [Mauler MAL-1R](https://raw.githubusercontent.com/MegaMek/mm-data/71fefad545e254b4a224f0d232eaef24473db1ee/data/mekfiles/meks/3050U/Mauler%20MAL-1R.mtf)

The excerpts are adapted MegaMek Data (C) 2025–2026 by The MegaMek Team, licensed under [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/). The half-bin test modifies the Firestarter excerpt in memory; it does not assert a different stock loadout.
