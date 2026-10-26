"""Prepare the MW5 Shadowhawk from external exports; use --source to override the model directory."""
import sys
sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

WEAPONS = (
    ('Head', 'Srm2Housing', 'Torso_Pitch', 'Weapon_Mech_SHD_Head_MH1_Missile2_SKM'),
    ('LeftTorso', 'CannonHousing', 'Torso_Pitch', 'Weapon_Mech_SHD_Torso_Left_BH1_AC5_SKM'),
    ('RightTorso', 'Lrm5Housing', 'Torso_Pitch', 'Weapon_Mech_SHD_Torso_Right_MH1_Missile5_SKM'),
    ('RightArm', 'LaserEmitter', 'Forearm_Right', 'Weapon_Mech_SHD_Arm_Right_EH1_Laser_SKM'),
)

if __name__ == "__main__":
    prepare('Shadowhawk', 'SHD', 3.0, 1.175, WEAPONS, "prepare-shadowhawk.py", 2,
            "Stock MW5 loadout -> Shadowhawk_HPS visual mappings; torso and arm sockets attach to Torso_Pitch and Forearm bones. Shadowhawk_MDL attaches the exterior cockpit to Cockpit.", glass_section=0)
