"""Prepare the MW5 Jenner from external UE Viewer exports (Python 3 + Pillow).

Run: python3 scripts/prepare-jenner.py [--source /path/to/Jenner/Model]
Only the runtime GLB, required imported textures and manifest belong in the project.
"""
import sys

sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

# Verified stock loadout -> hardpoint visuals -> blueprint SKM -> source attachment bone.
WEAPONS = (
    ('CenterTorso', 'Srm4Housing', 'Torso_Pitch', 'Weapon_Mech_JR7_Torso_Center_MH3_Missile4_SKM'),
    ('LeftArm', 'LaserEmitter0', 'Forearm_Left', 'Weapon_Mech_JR7_Forearm_Left_EH1_Laser_SKM'),
    ('LeftArm', 'LaserEmitter1', 'Forearm_Left', 'Weapon_Mech_JR7_Forearm_Left_EH2_Laser_SKM'),
    ('RightArm', 'LaserEmitter0', 'Forearm_Right', 'Weapon_Mech_JR7_Forearm_Right_EH1_Laser_SKM'),
    ('RightArm', 'LaserEmitter1', 'Forearm_Right', 'Weapon_Mech_JR7_Forearm_Right_EH2_Laser_SKM'),
)

if __name__ == "__main__":
    prepare('Jenner', 'JR7', 2.4, 1.075, WEAPONS, 'prepare-jenner.py', 4,
            'JR7-D_Loadout -> Jenner_HPS -> weapon blueprint SKM defaults; shared Mech_SKL sockets and component locals are identity. Torso_Center_MH3_srm4 uses MH3_Missile4. Jenner_MDL attaches exterior cockpit to Cockpit.')
