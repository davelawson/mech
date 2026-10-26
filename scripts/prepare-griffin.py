"""Prepare the MW5 Griffin from external exports; use --source to override the model directory."""
import sys
sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

WEAPONS = (
    ('RightTorso', 'Lrm10Housing', 'Torso_Pitch', 'Weapon_Mech_GRF_Torso_Right_MH1_Missile10_SKM'),
    ('RightArm', 'PpcEmitter', 'Forearm_Right', 'Weapon_Mech_GRF_Forearm_Right_EH1_PPC_SKM'),
)

if __name__ == "__main__":
    prepare('Griffin', 'GRF', 3.0, 1.175, WEAPONS, "prepare-griffin.py", 0,
            "Stock MW5 loadout -> Griffin_HPS visual mappings; torso and arm sockets attach to Torso_Pitch and Forearm bones. Griffin_MDL attaches the exterior cockpit to Cockpit.", glass_section=1)
