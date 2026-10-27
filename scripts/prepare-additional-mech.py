"""Prepare one selected MW5 chassis using its recorded blueprint attachments.

Usage: python3 scripts/prepare-additional-mech.py <chassis> [--source /path/to/Model]
"""
import json
from pathlib import Path
import sys
sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

if len(sys.argv) < 2:
    raise SystemExit(__doc__)
name = sys.argv.pop(1)
models = json.loads((Path(__file__).resolve().parents[1] / 'Data/additional-mech-models.json').read_text())
model = next((m for m in models if m['id'] == name or m['chassis'].casefold() == name.casefold()), None)
if model is None:
    raise SystemExit('Unknown chassis: ' + name)
weapons = [tuple(item[:4]) for item in model['weapons']]
prepare(model['chassis'], model['prefix'], model['displayed_height'], model['model_scale'], weapons,
        'prepare-additional-mech.py', 0,
        f"{model['variant']} MW5 stock loadout -> hardpoint tag mapping -> weapon blueprint SKM; source skeleton sockets and exterior cockpit.",
        glass_section=model['glass_section'])
