"""Prepare the MW5 Victor from external exports; --source overrides the model directory."""
import sys
sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

WEAPONS = (
    ('LeftTorso', 'Srm4Housing', 'Torso_Pitch', 'Weapon_Mech_VTR_Torso_Left_MH1_Missile4_SKM'),
    ('RightArm', 'CannonHousing', 'Forearm_Right', 'Weapon_Mech_VTR_Forearm_Right_BH1_AC20_SKM'),
    ('LeftArm', 'LaserEmitter1', 'Forearm_Left', 'Weapon_Mech_VTR_Forearm_Left_EH1_Laser_SKM'),
    ('LeftArm', 'LaserEmitter2', 'Forearm_Left', 'Weapon_Mech_VTR_Forearm_Left_EH2_Laser_SKM'),
)

if __name__ == "__main__":
    prepare('Victor', 'VTR', 3.64, 1.3, WEAPONS, 'prepare-victor.py', 4,
            "VTR-9B loadout -> Victor_HPS visual mappings -> source blueprint skeletal meshes. Torso and forearm sockets use Torso_Pitch and Forearm bones; cockpit attaches to Cockpit.", glass_section=1, source_tree='Game')
