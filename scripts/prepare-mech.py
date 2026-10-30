"""Prepare MW5 chassis from external UE Viewer exports using their recorded blueprint attachments (Python 3 + Pillow).

Usage: python3 scripts/prepare-mech.py <chassis>... [--source /path/to/Model]
       python3 scripts/prepare-mech.py --all
Chassis parameters live in Data/mech-models.json. Only the runtime GLB, required imported textures
and manifest belong in the project.
"""
import argparse
import json
from pathlib import Path
import sys
sys.dont_write_bytecode = True
from prepare_mw5_mech import default_source, prepare

parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
parser.add_argument("chassis", nargs="*", help="chassis ID or name, e.g. shadow-hawk or Shadowhawk")
parser.add_argument("--all", action="store_true", help="prepare every chassis in Data/mech-models.json")
parser.add_argument("--source", type=Path, help="model directory for a single chassis")
args = parser.parse_args()
models = json.loads((Path(__file__).resolve().parents[1] / "Data/mech-models.json").read_text())
if args.all == bool(args.chassis):
    parser.error("name one or more chassis, or pass --all")
if args.source and (args.all or len(args.chassis) != 1):
    parser.error("--source applies to a single chassis")

selected = models if args.all else []
for name in args.chassis:
    model = next((m for m in models if m["id"] == name or m["chassis"].casefold() == name.casefold()), None)
    if model is None:
        parser.error("unknown chassis: " + name)
    selected.append(model)

for model in selected:
    source_tree = model.get("source_tree", "Game")
    evidence = model.get("evidence", f"{model['variant']} MW5 stock loadout -> hardpoint tag mapping -> weapon blueprint SKM; "
                                     "source skeleton sockets and exterior cockpit.")
    prepare(model["chassis"], model["prefix"], model["displayed_height"], model["model_scale"],
            [tuple(item[:4]) for item in model["weapons"]], model.get("missile_tubes", 0), evidence,
            args.source or default_source(model["chassis"], source_tree), glass_section=model["glass_section"])
