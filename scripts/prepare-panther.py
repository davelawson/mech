"""Prepare the MW5 Panther from external exports; use --source to override the model directory."""
import sys
sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

WEAPONS = (
    ('CenterTorso', 'Srm4Housing', 'Torso_Pitch', 'Weapon_Mech_PNT_Torso_Center_MH1_Missile4_SKM'),
    ('RightArm', 'PpcEmitter', 'Forearm_Right', 'Weapon_Mech_PNT_Forearm_Right_EH1_PPC_SKM'),
)

if __name__ == "__main__":
    prepare('Panther', 'PNT', 2.48, 1.075, WEAPONS, "prepare-panther.py", 4,
            "Stock MW5 loadout -> Panther_HPS visual mappings; torso and arm sockets attach to Torso_Pitch and Forearm bones. Panther_MDL attaches the exterior cockpit to Cockpit.", glass_section=1)
