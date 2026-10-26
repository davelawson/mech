"""Prepare the MW5 Awesome from external UE Viewer exports (Python 3 + Pillow).

Run: python3 scripts/prepare-awesome.py [--source /path/to/Awesome/Model]
Only the runtime GLB, required imported textures and manifest belong in the project.
"""
import sys

sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

# Verified stock loadout -> hardpoint visuals -> blueprint SKM -> source attachment bone.
WEAPONS = (
    ('RightArm', 'PpcEmitter', 'Forearm_Right', 'Weapon_Mech_AWS_Forearm_Right_EH1_PPC_SKM'),
    ('LeftTorso', 'PpcEmitter', 'Torso_Pitch', 'Weapon_Mech_AWS_Torso_Left_EH1_PPC_SKM'),
    ('RightTorso', 'PpcEmitter', 'Torso_Pitch', 'Weapon_Mech_AWS_Torso_Right_EH1_PPC_SKM'),
    ('Head', 'LaserEmitter', 'Torso_Pitch', 'Weapon_Mech_AWS_Head_EH1_Laser_SKM'),
)

if __name__ == "__main__":
    prepare('Awesome', 'AWS', 3.64, 1.3, WEAPONS, 'prepare-awesome.py', 0,
            'AWS-8Q_Loadout -> Awesome_HPS -> weapon blueprint SKM defaults; shared Mech_SKL sockets and component locals are identity. Awesome_MDL attaches exterior cockpit to Cockpit. Head small laser attaches via Torso_Pitch but belongs to Head damage location.')
